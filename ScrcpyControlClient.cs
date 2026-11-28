using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
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
        private static readonly Random _rng = new Random();
        private static readonly object _rngLock = new object();

        private string _serial = "";
        private int _localPort = 27199;
        private volatile int _screenWidth = 1920;
        private volatile int _screenHeight = 1080;
        private volatile bool _running = false;
        private volatile bool _noVideoMode = false;
        private bool _isVirtualDisplay = false;
        private volatile int _nativeLongSide = 0;
        private volatile int _nativeShortSide = 0;
        private int _refreshingNativeSize = 0;

        public bool IsConnected
        {
            get { return _ctrlTcp != null && _ctrlTcp.Connected && _ctrlStream != null; }
        }

        public bool IsNoVideoMode { get { return _noVideoMode; } }
        public int ScreenWidth { get { return _screenWidth; } }
        public int ScreenHeight { get { return _screenHeight; } }

        public ScrcpyControlClient(AdbService adb)
        {
            _adb = adb;
        }

        public void SetScreenSize(int width, int height)
        {
            if (!_noVideoMode) return;
            if (width <= 0 || height <= 0) return;

            ApplyTextureDimensions(width, height);

            // If physical foldable switched between cover & inner display (aspect ratio jump), re-query wm size asynchronously
            if (!_isVirtualDisplay && _nativeLongSide > 0 && _nativeShortSide > 0)
            {
                double texRatio = (double)Math.Max(width, height) / Math.Max(1, Math.Min(width, height));
                double natRatio = (double)_nativeLongSide / Math.Max(1, _nativeShortSide);
                if (Math.Abs(texRatio - natRatio) > 0.18)
                {
                    RefreshFoldableDisplaySizeInBackground(width, height);
                }
            }
        }

        private void ApplyTextureDimensions(int texW, int texH)
        {
            if (_isVirtualDisplay || _nativeLongSide <= 0 || _nativeShortSide <= 0)
            {
                if (texW > 0 && texH > 0)
                {
                    _screenWidth = texW;
                    _screenHeight = texH;
                }
                return;
            }

            bool landscape = (texW <= 0 || texH <= 0) ? true : (texW >= texH);
            _screenWidth = landscape ? _nativeLongSide : _nativeShortSide;
            _screenHeight = landscape ? _nativeShortSide : _nativeLongSide;
        }

        private void RefreshFoldableDisplaySizeInBackground(int latestTexW, int latestTexH)
        {
            if (Interlocked.CompareExchange(ref _refreshingNativeSize, 1, 0) != 0) return;
            string ser = _serial;
            Task.Run(async () =>
            {
                try
                {
                    await ProbePhysicalDisplaySizeAsync(ser);
                    ApplyTextureDimensions(latestTexW, latestTexH);
                }
                catch { }
                finally
                {
                    Interlocked.Exchange(ref _refreshingNativeSize, 0);
                }
            });
        }

        private async Task ProbePhysicalDisplaySizeAsync(string serial)
        {
            if (!_adb.IsValid || string.IsNullOrWhiteSpace(serial)) return;
            try
            {
                var res = await _adb.RunAsync(_adb.AdbPath, string.Format("-s \"{0}\" shell wm size", serial), 2500);
                string txt = res.StdOut ?? "";
                int w = 0, h = 0;

                var mOver = Regex.Match(txt, @"Override\s+size:\s*(\d+)x(\d+)", RegexOptions.IgnoreCase);
                if (mOver.Success)
                {
                    int.TryParse(mOver.Groups[1].Value, out w);
                    int.TryParse(mOver.Groups[2].Value, out h);
                }
                else
                {
                    var mPhys = Regex.Match(txt, @"Physical\s+size:\s*(\d+)x(\d+)", RegexOptions.IgnoreCase);
                    if (mPhys.Success)
                    {
                        int.TryParse(mPhys.Groups[1].Value, out w);
                        int.TryParse(mPhys.Groups[2].Value, out h);
                    }
                }

                if (w > 100 && h > 100 && w < 10000 && h < 10000)
                {
                    _nativeLongSide = Math.Max(w, h);
                    _nativeShortSide = Math.Min(w, h);
                }
            }
            catch { }
        }

        private static bool SupportsNoVideoControl(string ver)
        {
            if (string.IsNullOrWhiteSpace(ver)) return true;
            var m = Regex.Match(ver, @"^(\d+)\.(\d+)");
            if (!m.Success) return true;
            int maj, min;
            if (!int.TryParse(m.Groups[1].Value, out maj)) return true;
            if (!int.TryParse(m.Groups[2].Value, out min)) min = 0;
            return (maj > 2) || (maj == 2 && min >= 1);
        }

        public async Task<Tuple<bool, string>> ConnectAsync(string serial, string scrcpyVersion, string displayId, int width, int height)
        {
            await DisconnectAsync();
            _serial = serial;
            _isVirtualDisplay = !string.IsNullOrWhiteSpace(displayId) && displayId != "0";
            _nativeLongSide = 0;
            _nativeShortSide = 0;

            if (!_adb.IsValid)
                return Tuple.Create(false, "scrcpy 폴더가 설정되지 않았습니다.");

            string serverJar = Path.Combine(_adb.ScrcpyDir, "scrcpy-server");
            if (!File.Exists(serverJar))
                return Tuple.Create(false, "scrcpy-server 파일을 찾을 수 없습니다.");

            string ver = (scrcpyVersion ?? "").TrimStart('v', 'V');
            if (string.IsNullOrEmpty(ver)) ver = "3.2";

            var pushRes = await _adb.RunAsync(_adb.AdbPath,
                string.Format("-s \"{0}\" push \"{1}\" /data/local/tmp/scrcpy-server.jar", serial, serverJar), 8000);
            if (pushRes.ExitCode != 0)
                return Tuple.Create(false, "scrcpy-server 전송 실패: " + pushRes.Combined.Trim());

            if (!_isVirtualDisplay)
                await ProbePhysicalDisplaySizeAsync(serial);

            int firstPort = 0;
            if (SupportsNoVideoControl(ver))
            {
                var pureRes = await ConnectNoVideoOnceAsync(serial, ver, displayId, width, height, 0, p => firstPort = p);
                if (pureRes.Item1)
                    return pureRes;
            }

            // Automatic fallback for legacy scrcpy (< 2.1) servers
            return await ConnectLegacyVideoOnceAsync(serial, ver, displayId, firstPort);
        }

        private async Task<Tuple<bool, string>> ConnectNoVideoOnceAsync(string serial, string ver, string displayId, int width, int height, int avoidPort, Action<int> onPortPicked)
        {
            string scid;
            int port;
            lock (_rngLock)
            {
                scid = _rng.Next(0x10000000, 0x7FFFFFFF).ToString("x8");
                do
                {
                    port = _rng.Next(27200, 27900);
                } while (port == avoidPort);
            }
            _localPort = port;
            if (onPortPicked != null) onPortPicked(port);

            var fwdRes = await _adb.RunAsync(_adb.AdbPath,
                string.Format("-s \"{0}\" forward tcp:{1} localabstract:scrcpy_{2}", serial, _localPort, scid), 5000);
            if (fwdRes.ExitCode != 0)
                return Tuple.Create(false, "ADB 포트 포워딩 실패: " + fwdRes.Combined.Trim());

            // Zero-Encoder Pure Control Server (video=false audio=false control=true):
            // Injects native 1:1 display pixel coordinates with 0% GPU/MediaCodec overhead and zero encoder contention!
            string dispArg = string.IsNullOrWhiteSpace(displayId) ? "" : (" display_id=" + displayId);
            string shellCmd = string.Format(
                "CLASSPATH=/data/local/tmp/scrcpy-server.jar app_process / com.genymobile.scrcpy.Server {0} scid={1} log_level=warn tunnel_forward=true video=false audio=false control=true clipboard_autosync=false cleanup=false send_device_meta=false send_dummy_byte=true{2}",
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

            var connectRes = await Task.Run(() =>
            {
                for (int i = 0; i < 35; i++)
                {
                    Thread.Sleep(60);
                    if (_serverProc.HasExited)
                        return Tuple.Create(false, "제어 서버가 종료됨: " + errBuf.ToString().Trim());

                    TcpClient ctl = null;
                    try
                    {
                        ctl = new TcpClient();
                        ctl.NoDelay = true;
                        ctl.Connect("127.0.0.1", _localPort);
                        var cs = ctl.GetStream();
                        cs.ReadTimeout = 1200;
                        int dummy = cs.ReadByte();
                        if (dummy < 0)
                        {
                            ctl.Close();
                            continue;
                        }

                        cs.ReadTimeout = Timeout.Infinite;
                        _noVideoMode = true;
                        if (width > 0 && height > 0)
                            ApplyTextureDimensions(width, height);
                        else if (_nativeLongSide > 0 && _nativeShortSide > 0)
                            ApplyTextureDimensions(_nativeLongSide, _nativeShortSide);

                        _ctrlTcp = ctl;
                        _ctrlStream = cs;
                        _running = true;

                        _ctrlDrainThread = new Thread(DrainControlLoop) { IsBackground = true };
                        _ctrlDrainThread.Start();

                        _sendQueue = new BlockingCollection<byte[]>();
                        _sendThread = new Thread(SendLoop) { IsBackground = true, Priority = ThreadPriority.Highest };
                        _sendThread.Start();

                        return Tuple.Create(true, string.Format("멀티터치 무부하 제어 연결 완료 (Zero-Encoder {0}x{1}, display={2})",
                            _screenWidth, _screenHeight, string.IsNullOrEmpty(displayId) ? "0" : displayId));
                    }
                    catch
                    {
                        try { if (ctl != null) ctl.Close(); } catch { }
                    }
                }
                return Tuple.Create(false, "");
            });

            if (connectRes.Item1)
                return connectRes;

            await DisconnectAsync();
            string err = errBuf.ToString().Trim();
            return Tuple.Create(false, string.IsNullOrEmpty(connectRes.Item2)
                ? ("제어 소켓 연결 시간 초과" + (string.IsNullOrEmpty(err) ? "" : ": " + err))
                : connectRes.Item2);
        }

        private async Task<Tuple<bool, string>> ConnectLegacyVideoOnceAsync(string serial, string ver, string displayId, int avoidPort)
        {
            string scid;
            int port;
            lock (_rngLock)
            {
                scid = _rng.Next(0x10000000, 0x7FFFFFFF).ToString("x8");
                do
                {
                    port = _rng.Next(27200, 27900);
                } while (port == avoidPort);
            }
            _localPort = port;

            var fwdRes = await _adb.RunAsync(_adb.AdbPath,
                string.Format("-s \"{0}\" forward tcp:{1} localabstract:scrcpy_{2}", serial, _localPort, scid), 5000);
            if (fwdRes.ExitCode != 0)
                return Tuple.Create(false, "ADB 포트 포워딩 실패: " + fwdRes.Combined.Trim());

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

                        byte[] meta = ReadExact(vs, 12);
                        if (meta == null)
                        {
                            ctl.Close();
                            vid.Close();
                            continue;
                        }

                        int vw = ReadInt32BE(meta, 4);
                        int vh = ReadInt32BE(meta, 8);

                        _noVideoMode = false;
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
                await DisconnectAsync();
                return connectRes;
            }

            await DisconnectAsync();
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

        private void EnqueuePacket(byte[] buf)
        {
            var q = _sendQueue;
            if (q != null && !q.IsAddingCompleted)
            {
                try { q.TryAdd(buf); } catch { }
            }
        }

        // action: 0 = ACTION_DOWN, 1 = ACTION_UP, 2 = ACTION_MOVE
        public void InjectTouch(byte action, long pointerId, int normX, int normY)
        {
            if (!IsConnected) return;

            if (normX < 0) normX = 0; else if (normX > 10000) normX = 10000;
            if (normY < 0) normY = 0; else if (normY > 10000) normY = 10000;

            int sw = _screenWidth;
            int sh = _screenHeight;
            if (sw <= 0) sw = 1920;
            if (sh <= 0) sh = 1080;

            int px = (int)Math.Round(normX * (sw / 10000.0));
            int py = (int)Math.Round(normY * (sh / 10000.0));
            if (px >= sw) px = sw - 1;
            if (py >= sh) py = sh - 1;

            ushort pressure = (action == 1) ? (ushort)0 : (ushort)0xFFFF;

            // scrcpy INJECT_TOUCH_EVENT (32 bytes, big-endian)
            byte[] buf = new byte[32];
            buf[0] = 2; // SC_CONTROL_MSG_TYPE_INJECT_TOUCH_EVENT
            buf[1] = action;
            WriteInt64BE(buf, 2, pointerId);
            WriteInt32BE(buf, 10, px);
            WriteInt32BE(buf, 14, py);
            WriteUInt16BE(buf, 18, (ushort)sw);
            WriteUInt16BE(buf, 20, (ushort)sh);
            WriteUInt16BE(buf, 22, pressure);
            WriteInt32BE(buf, 24, 0);
            WriteInt32BE(buf, 28, 0);

            EnqueuePacket(buf);
        }

        // SC_CONTROL_MSG_TYPE_INJECT_KEYCODE (type = 0, 14 bytes):
        // Common Android keycodes: KEYCODE_BACK=4, KEYCODE_ENTER=66, KEYCODE_DEL(Backspace)=67, KEYCODE_ESCAPE=111
        public void InjectKeycode(byte action, int androidKeycode, int metaState = 0)
        {
            if (!IsConnected) return;
            byte[] buf = new byte[14];
            buf[0] = 0; // TYPE_INJECT_KEYCODE
            buf[1] = action;
            WriteInt32BE(buf, 2, androidKeycode);
            WriteInt32BE(buf, 6, 0); // repeat
            WriteInt32BE(buf, 10, metaState);
            EnqueuePacket(buf);
        }

        public void PressReleaseKeycode(int androidKeycode, int metaState = 0)
        {
            InjectKeycode(0, androidKeycode, metaState);
            InjectKeycode(1, androidKeycode, metaState);
        }

        // SC_CONTROL_MSG_TYPE_BACK_OR_SCREEN_ON (type = 4, 2 bytes)
        public void InjectBackOrScreenOn(byte action)
        {
            if (!IsConnected) return;
            byte[] buf = new byte[2];
            buf[0] = 4; // TYPE_BACK_OR_SCREEN_ON
            buf[1] = action;
            EnqueuePacket(buf);
        }

        public void PressBackOrScreenOn()
        {
            InjectBackOrScreenOn(0);
            InjectBackOrScreenOn(1);
        }

        // SC_CONTROL_MSG_TYPE_INJECT_TEXT (type = 1): injects ASCII/UTF-8 text up to 300 bytes per message
        public bool InjectText(string text)
        {
            if (!IsConnected || string.IsNullOrEmpty(text)) return false;
            try
            {
                byte[] textBytes = Encoding.UTF8.GetBytes(text);
                byte[] buf = new byte[5 + textBytes.Length];
                buf[0] = 1; // TYPE_INJECT_TEXT
                WriteInt32BE(buf, 1, textBytes.Length);
                Buffer.BlockCopy(textBytes, 0, buf, 5, textBytes.Length);
                EnqueuePacket(buf);
                return true;
            }
            catch { }
            return false;
        }

        // SC_CONTROL_MSG_TYPE_SET_CLIPBOARD (type = 9): sends UTF-8 Korean/text directly to Android and pastes immediately!
        public bool SetClipboardAndPaste(string text)
        {
            if (!IsConnected || string.IsNullOrEmpty(text)) return false;
            try
            {
                byte[] textBytes = Encoding.UTF8.GetBytes(text);
                byte[] buf = new byte[14 + textBytes.Length];
                buf[0] = 9; // SC_CONTROL_MSG_TYPE_SET_CLIPBOARD
                // bytes 1..8: sequence = 0 (no ack)
                buf[9] = 1; // paste = true
                WriteInt32BE(buf, 10, textBytes.Length);
                Buffer.BlockCopy(textBytes, 0, buf, 14, textBytes.Length);
                EnqueuePacket(buf);
                return true;
            }
            catch { }
            return false;
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

        private Tuple<string, int> StopResourcesCore()
        {
            _running = false;
            _noVideoMode = false;
            var oldQueue = _sendQueue;
            _sendQueue = null;
            if (oldQueue != null)
            {
                try { oldQueue.CompleteAdding(); } catch { }
                try { oldQueue.Dispose(); } catch { }
            }
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

            string ser = _serial;
            int port = _localPort;
            _localPort = 0;
            return Tuple.Create(ser, port);
        }

        public async Task DisconnectAsync()
        {
            var info = StopResourcesCore();
            if (!string.IsNullOrEmpty(info.Item1) && info.Item2 > 0 && _adb.IsValid)
            {
                try
                {
                    await _adb.RunAsync(_adb.AdbPath, string.Format("-s \"{0}\" forward --remove tcp:{1}", info.Item1, info.Item2), 2000);
                }
                catch { }
            }
        }

        public void Disconnect()
        {
            var info = StopResourcesCore();
            if (!string.IsNullOrEmpty(info.Item1) && info.Item2 > 0 && _adb.IsValid)
            {
                try
                {
                    _adb.RunAsync(_adb.AdbPath, string.Format("-s \"{0}\" forward --remove tcp:{1}", info.Item1, info.Item2), 2000);
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
