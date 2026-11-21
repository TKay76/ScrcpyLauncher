using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ScrcpyLauncher
{
    public class ScrcpyControlClient : IDisposable
    {
        private readonly AdbService _adb;
        private Process _serverProc;
        private TcpClient _videoTcp;
        private NetworkStream _videoStream;
        private Thread _videoDrainThread;
        private TcpClient _ctrlTcp;
        private NetworkStream _ctrlStream;
        private Thread _ctrlDrainThread;
        private BlockingCollection<byte[]> _sendQueue;
        private Thread _sendThread;
        private readonly object _sendLock = new object();

        private string _serial = "";
        private int _localPort = 27199;
        private int _screenWidth = 1024;
        private int _screenHeight = 576;
        private volatile bool _running = false;

        public bool IsConnected
        {
            get { return _ctrlTcp != null && _ctrlTcp.Connected && _ctrlStream != null; }
        }

        public int ScreenWidth { get { return _screenWidth; } }
        public int ScreenHeight { get { return _screenHeight; } }

        public ScrcpyControlClient(AdbService adb)
        {
            _adb = adb;
        }

        public void SetScreenSize(int width, int height)
        {
            // Screen size for touch injection is authoritatively read from the video stream metadata
        }

        public async Task<Tuple<bool, string>> ConnectAsync(string serial, string scrcpyVersion, string displayId, int width, int height)
        {
            Disconnect();
            _serial = serial;

            if (!_adb.IsValid)
                return Tuple.Create(false, "scrcpy 폴더가 설정되지 않았습니다.");

            string serverJar = Path.Combine(_adb.ScrcpyDir, "scrcpy-server");
            if (!File.Exists(serverJar))
                return Tuple.Create(false, "scrcpy-server 파일을 찾을 수 없습니다.");

            string ver = (scrcpyVersion ?? "").TrimStart('v', 'V');
            if (string.IsNullOrEmpty(ver)) ver = "5.0.1";

            var pushRes = await _adb.RunAsync(_adb.AdbPath,
                string.Format("-s \"{0}\" push \"{1}\" /data/local/tmp/scrcpy-server.jar", serial, serverJar), 8000);
            if (pushRes.ExitCode != 0)
                return Tuple.Create(false, "scrcpy-server 전송 실패: " + pushRes.Combined.Trim());

            var rng = new Random();
            string scid = rng.Next(0x10000000, 0x7FFFFFFF).ToString("x8");
            _localPort = rng.Next(27200, 27900);

            var fwdRes = await _adb.RunAsync(_adb.AdbPath,
                string.Format("-s \"{0}\" forward tcp:{1} localabstract:scrcpy_{2}", serial, _localPort, scid), 5000);
            if (fwdRes.ExitCode != 0)
                return Tuple.Create(false, "ADB 포트 포워딩 실패: " + fwdRes.Combined.Trim());

            // video=true at 1fps is required so scrcpy-server initializes PositionMapper for INJECT_TOUCH_EVENT
            string dispArg = string.IsNullOrWhiteSpace(displayId) ? "" : (" display_id=" + displayId);
            string shellCmd = string.Format(
                "CLASSPATH=/data/local/tmp/scrcpy-server.jar app_process / com.genymobile.scrcpy.Server {0} scid={1} log_level=warn tunnel_forward=true video=true audio=false control=true clipboard_autosync=false cleanup=false max_fps=1 max_size=1024 video_bit_rate=100000 send_device_meta=false send_dummy_byte=true{2}",
                ver, scid, dispArg);

            var psi = new ProcessStartInfo
            {
                FileName = _adb.AdbPath,
                Arguments = string.Format("-s \"{0}\" shell {1}", serial, shellCmd),
                WorkingDirectory = _adb.ScrcpyDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var errBuf = new StringBuilder();
            _serverProc = new Process { StartInfo = psi };
            _serverProc.OutputDataReceived += (s, e) => { if (e.Data != null) errBuf.AppendLine(e.Data); };
            _serverProc.ErrorDataReceived += (s, e) => { if (e.Data != null) errBuf.AppendLine(e.Data); };
            _serverProc.Start();
            _serverProc.BeginOutputReadLine();
            _serverProc.BeginErrorReadLine();

            // Retry TCP connect on background thread so WPF UI never freezes
            var connectRes = await Task.Run(() =>
            {
                for (int i = 0; i < 40; i++)
                {
                    Thread.Sleep(100);
                    if (_serverProc.HasExited)
                        return Tuple.Create(false, "제어 서버가 종료됨: " + errBuf.ToString().Trim());

                    TcpClient vid = null;
                    TcpClient ctl = null;
                    try
                    {
                        vid = new TcpClient();
                        vid.NoDelay = true;
                        vid.Connect("127.0.0.1", _localPort);
                        var vs = vid.GetStream();
                        vs.ReadTimeout = 1500;
                        int dummy = vs.ReadByte();
                        if (dummy < 0)
                        {
                            vid.Close();
                            continue;
                        }

                        ctl = new TcpClient();
                        ctl.NoDelay = true;
                        ctl.Connect("127.0.0.1", _localPort);
                        var cs = ctl.GetStream();

                        byte[] meta = ReadExact(vs, 16);
                        if (meta == null)
                        {
                            ctl.Close();
                            vid.Close();
                            continue;
                        }

                        int field1 = ReadInt32BE(meta, 4);
                        int vw, vh;
                        if (field1 == unchecked((int)0x80000000))
                        {
                            vw = ReadInt32BE(meta, 8);
                            vh = ReadInt32BE(meta, 12);
                        }
                        else
                        {
                            vw = field1;
                            vh = ReadInt32BE(meta, 8);
                        }

                        if (vw > 0 && vw < 10000 && vh > 0 && vh < 10000)
                        {
                            _screenWidth = vw;
                            _screenHeight = vh;
                        }

                        vs.ReadTimeout = Timeout.Infinite;
                        _videoTcp = vid;
                        _videoStream = vs;
                        _ctrlTcp = ctl;
                        _ctrlStream = cs;
                        _running = true;

                        _videoDrainThread = new Thread(DrainVideoLoop) { IsBackground = true };
                        _videoDrainThread.Start();

                        _ctrlDrainThread = new Thread(DrainControlLoop) { IsBackground = true };
                        _ctrlDrainThread.Start();

                        _sendQueue = new BlockingCollection<byte[]>();
                        _sendThread = new Thread(SendLoop) { IsBackground = true, Priority = ThreadPriority.Highest };
                        _sendThread.Start();

                        return Tuple.Create(true, string.Format("멀티터치 제어 연결 완료 ({0}x{1}, display={2})", _screenWidth, _screenHeight, string.IsNullOrEmpty(displayId) ? "0" : displayId));
                    }
                    catch
                    {
                        try { if (ctl != null) ctl.Close(); } catch { }
                        try { if (vid != null) vid.Close(); } catch { }
                    }
                }
                return Tuple.Create(false, "");
            });

            if (connectRes.Item1)
                return connectRes;
            if (!string.IsNullOrEmpty(connectRes.Item2))
            {
                Disconnect();
                return connectRes;
            }

            Disconnect();
            string err = errBuf.ToString().Trim();
            return Tuple.Create(false, "제어 소켓 연결 시간 초과" + (string.IsNullOrEmpty(err) ? "" : ": " + err));
        }

        private void DrainVideoLoop()
        {
            byte[] dump = new byte[4096];
            try
            {
                while (_running && _videoStream != null)
                {
                    int n = _videoStream.Read(dump, 0, dump.Length);
                    if (n <= 0) break;
                }
            }
            catch { }
        }

        private void DrainControlLoop()
        {
            byte[] dump = new byte[4096];
            try
            {
                while (_running && _ctrlStream != null)
                {
                    int n = _ctrlStream.Read(dump, 0, dump.Length);
                    if (n <= 0) break;
                }
            }
            catch { }
        }

        private static byte[] ReadExact(NetworkStream s, int count)
        {
            byte[] buf = new byte[count];
            int total = 0;
            while (total < count)
            {
                int n = s.Read(buf, total, count - total);
                if (n <= 0) return null;
                total += n;
            }
            return buf;
        }

        private void SendLoop()
        {
            try
            {
                var q = _sendQueue;
                if (q == null) return;
                foreach (var pkt in q.GetConsumingEnumerable())
                {
                    if (!_running) break;
                    if (pkt == null) continue;
                    lock (_sendLock)
                    {
                        if (_ctrlStream != null)
                            _ctrlStream.Write(pkt, 0, pkt.Length);
                    }
                }
            }
            catch { }
        }

        public void EnqueueTinyDelay()
        {
            var q = _sendQueue;
            if (_running && q != null && !q.IsAddingCompleted)
            {
                try { q.TryAdd(new byte[] { 0xFF }); } catch { }
            }
        }

        // action: 0 = ACTION_DOWN, 1 = ACTION_UP, 2 = ACTION_MOVE
        public void InjectTouch(byte action, long pointerId, int normX, int normY)
        {
            if (!IsConnected) return;

            if (normX < 0) normX = 0; else if (normX > 10000) normX = 10000;
            if (normY < 0) normY = 0; else if (normY > 10000) normY = 10000;

            int px = (int)Math.Round(normX * (_screenWidth / 10000.0));
            int py = (int)Math.Round(normY * (_screenHeight / 10000.0));
            if (px >= _screenWidth) px = _screenWidth - 1;
            if (py >= _screenHeight) py = _screenHeight - 1;

            ushort pressure = (action == 1) ? (ushort)0 : (ushort)0xFFFF;

            // scrcpy INJECT_TOUCH_EVENT (32 bytes, big-endian)
            byte[] buf = new byte[32];
            buf[0] = 2; // SC_CONTROL_MSG_TYPE_INJECT_TOUCH_EVENT
            buf[1] = action;
            WriteInt64BE(buf, 2, pointerId);
            WriteInt32BE(buf, 10, px);
            WriteInt32BE(buf, 14, py);
            WriteUInt16BE(buf, 18, (ushort)_screenWidth);
            WriteUInt16BE(buf, 20, (ushort)_screenHeight);
            WriteUInt16BE(buf, 22, pressure);
            WriteInt32BE(buf, 24, 0);
            WriteInt32BE(buf, 28, 0);

            var q = _sendQueue;
            if (q != null && !q.IsAddingCompleted)
            {
                try { q.TryAdd(buf); } catch { }
            }
        }

        private static int ReadInt32BE(byte[] b, int o)
        {
            return (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
        }

        private static void WriteUInt16BE(byte[] b, int o, ushort v)
        {
            b[o] = (byte)(v >> 8);
            b[o + 1] = (byte)(v & 0xFF);
        }

        private static void WriteInt32BE(byte[] b, int o, int v)
        {
            b[o] = (byte)(v >> 24);
            b[o + 1] = (byte)(v >> 16);
            b[o + 2] = (byte)(v >> 8);
            b[o + 3] = (byte)(v & 0xFF);
        }

        private static void WriteInt64BE(byte[] b, int o, long v)
        {
            b[o] = (byte)(v >> 56);
            b[o + 1] = (byte)(v >> 48);
            b[o + 2] = (byte)(v >> 40);
            b[o + 3] = (byte)(v >> 32);
            b[o + 4] = (byte)(v >> 24);
            b[o + 5] = (byte)(v >> 16);
            b[o + 6] = (byte)(v >> 8);
            b[o + 7] = (byte)(v & 0xFF);
        }

        public void Disconnect()
        {
            _running = false;
            try { if (_sendQueue != null) _sendQueue.CompleteAdding(); } catch { }
            _sendQueue = null;
            lock (_sendLock)
            {
                try { if (_ctrlStream != null) _ctrlStream.Dispose(); } catch { }
                try { if (_ctrlTcp != null) _ctrlTcp.Close(); } catch { }
                _ctrlStream = null;
                _ctrlTcp = null;
            }
            try { if (_videoStream != null) _videoStream.Dispose(); } catch { }
            try { if (_videoTcp != null) _videoTcp.Close(); } catch { }
            _videoStream = null;
            _videoTcp = null;

            try
            {
                if (_serverProc != null && !_serverProc.HasExited)
                    _serverProc.Kill();
            }
            catch { }
            _serverProc = null;

            if (!string.IsNullOrEmpty(_serial) && _adb.IsValid)
            {
                try
                {
                    _adb.RunAsync(_adb.AdbPath, string.Format("-s \"{0}\" forward --remove tcp:{1}", _serial, _localPort), 2000);
                }
                catch { }
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
