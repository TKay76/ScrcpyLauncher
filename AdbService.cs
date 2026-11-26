using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ScrcpyLauncher
{
    public class ProcessResult
    {
        public int ExitCode { get; set; }
        public string StdOut { get; set; }
        public string StdErr { get; set; }
        public bool TimedOut { get; set; }

        public string Combined
        {
            get
            {
                if (string.IsNullOrWhiteSpace(StdErr)) return StdOut ?? "";
                if (string.IsNullOrWhiteSpace(StdOut)) return StdErr ?? "";
                return StdOut.TrimEnd() + Environment.NewLine + StdErr.TrimEnd();
            }
        }
    }

    public class AdbService
    {
        public string ScrcpyDir { get; private set; }
        public string AdbPath { get { return string.IsNullOrEmpty(ScrcpyDir) ? "" : Path.Combine(ScrcpyDir, "adb.exe"); } }
        public string ScrcpyPath { get { return string.IsNullOrEmpty(ScrcpyDir) ? "" : Path.Combine(ScrcpyDir, "scrcpy.exe"); } }

        public bool IsValid
        {
            get { return File.Exists(ScrcpyPath) && File.Exists(AdbPath); }
        }

        public void SetDirectory(string dir)
        {
            ScrcpyDir = dir ?? "";
        }

        public static string DetectDefaultDirectory(string savedCustomDir)
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            if (File.Exists(Path.Combine(exeDir, "scrcpy.exe")) && File.Exists(Path.Combine(exeDir, "adb.exe")))
                return exeDir.TrimEnd('\\');

            if (!string.IsNullOrWhiteSpace(savedCustomDir) &&
                File.Exists(Path.Combine(savedCustomDir, "scrcpy.exe")) &&
                File.Exists(Path.Combine(savedCustomDir, "adb.exe")))
                return savedCustomDir.TrimEnd('\\');

            return "";
        }

        public Task<ProcessResult> RunAsync(string exePath, string arguments, int timeoutMs = 10000)
        {
            return Task.Run(() =>
            {
                var result = new ProcessResult { ExitCode = -1, StdOut = "", StdErr = "" };
                if (!File.Exists(exePath))
                {
                    result.StdErr = "파일을 찾을 수 없습니다: " + exePath;
                    return result;
                }

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = arguments,
                        WorkingDirectory = ScrcpyDir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using (var proc = new Process { StartInfo = psi })
                    {
                        var stdout = new StringBuilder();
                        var stderr = new StringBuilder();

                        proc.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                        proc.ErrorDataReceived += (s, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

                        proc.Start();
                        proc.BeginOutputReadLine();
                        proc.BeginErrorReadLine();

                        if (!proc.WaitForExit(timeoutMs))
                        {
                            result.TimedOut = true;
                            try { proc.Kill(); } catch { }
                        }
                        else
                        {
                            proc.WaitForExit();
                            result.ExitCode = proc.ExitCode;
                        }

                        result.StdOut = stdout.ToString();
                        result.StdErr = stderr.ToString();
                    }
                }
                catch (Exception ex)
                {
                    result.StdErr = ex.Message;
                }

                return result;
            });
        }

        public async Task<string> GetScrcpyVersionAsync()
        {
            if (!IsValid) return "";
            var res = await RunAsync(ScrcpyPath, "--version", 4000);
            if (res.ExitCode == 0 && !string.IsNullOrWhiteSpace(res.StdOut))
            {
                string firstLine = res.StdOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                var m = Regex.Match(firstLine, @"scrcpy\s+([\d\.]+)");
                if (m.Success) return "v" + m.Groups[1].Value;
            }
            return "";
        }

        public async Task<List<DeviceInfo>> GetDevicesAsync()
        {
            var list = new List<DeviceInfo>();
            if (!IsValid) return list;

            var res = await RunAsync(AdbPath, "devices -l", 6000);
            if (res.ExitCode != 0) return list;

            var lines = res.StdOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var raw in lines)
            {
                string line = raw.Trim();
                if (line.StartsWith("List of devices") || line.StartsWith("*") || string.IsNullOrEmpty(line))
                    continue;

                var parts = Regex.Split(line, @"\s+");
                if (parts.Length < 2) continue;

                string serial = parts[0];
                string state = parts[1];
                string model = "";
                foreach (var p in parts.Skip(2))
                {
                    if (p.StartsWith("model:"))
                    {
                        model = p.Substring("model:".Length);
                        break;
                    }
                }

                list.Add(new DeviceInfo
                {
                    Serial = serial,
                    State = state,
                    Model = model,
                    IsWifi = serial.Contains(":") ||
                             serial.StartsWith("adb-", StringComparison.OrdinalIgnoreCase) ||
                             serial.IndexOf("._adb-tls-", StringComparison.OrdinalIgnoreCase) >= 0
                });
            }

            return list;
        }

        public async Task<ProcessResult> PairWirelessAsync(string ipPort, string code)
        {
            return await RunAsync(AdbPath, string.Format("pair {0} {1}", ipPort.Trim(), code.Trim()), 15000);
        }

        public async Task<ProcessResult> ConnectWirelessAsync(string ipPort)
        {
            return await RunAsync(AdbPath, string.Format("connect {0}", ipPort.Trim()), 10000);
        }

        public async Task<ProcessResult> DisconnectWirelessAsync(string ipPort)
        {
            if (string.IsNullOrWhiteSpace(ipPort))
                return await RunAsync(AdbPath, "disconnect", 6000);
            return await RunAsync(AdbPath, string.Format("disconnect {0}", ipPort.Trim()), 6000);
        }

        public async Task<ProcessResult> RestartAdbServerAsync()
        {
            await RunAsync(AdbPath, "kill-server", 6000);
            await Task.Delay(500);
            return await RunAsync(AdbPath, "start-server", 10000);
        }

        public async Task WakeDeviceAsync(string serial)
        {
            if (!IsValid || string.IsNullOrWhiteSpace(serial)) return;
            await RunAsync(AdbPath, string.Format("-s \"{0}\" shell input keyevent KEYCODE_WAKEUP", serial), 3000);
        }

        public async Task<Tuple<bool, string>> CaptureScreenshotAsync(string serial, string outputPath, string displayId)
        {
            if (!IsValid || string.IsNullOrWhiteSpace(serial))
                return Tuple.Create(false, "연결된 기기가 없습니다.");

            const string remoteTmp = "/data/local/tmp/_scrcpy_launcher_cap.png";
            try
            {
                // 1. If Virtual Display is active, try screencap -d {displayId} first and verify pulled file size >= 1024 bytes
                //    (Android 11-14 sometimes exits 0 while producing a 0-byte file on virtual displays!)
                if (!string.IsNullOrWhiteSpace(displayId) && displayId != "0")
                {
                    var vdCap = await RunAsync(AdbPath, string.Format("-s \"{0}\" shell screencap -d {1} -p {2}", serial, displayId, remoteTmp), 8000);
                    if (vdCap.ExitCode == 0)
                    {
                        var vdPull = await RunAsync(AdbPath, string.Format("-s \"{0}\" pull {1} \"{2}\"", serial, remoteTmp, outputPath), 8000);
                        await RunAsync(AdbPath, string.Format("-s \"{0}\" shell rm -f {1}", serial, remoteTmp), 4000);
                        if (vdPull.ExitCode == 0 && File.Exists(outputPath) && new FileInfo(outputPath).Length >= 1024)
                            return Tuple.Create(true, outputPath);
                    }
                    try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
                }

                // 2. Standard fallback (default display)
                var capRes = await RunAsync(AdbPath, string.Format("-s \"{0}\" shell screencap -p {1}", serial, remoteTmp), 8000);
                if (capRes.ExitCode != 0)
                    return Tuple.Create(false, "기기 화면 캡처 실패: " + capRes.Combined.Trim());

                var pullRes = await RunAsync(AdbPath, string.Format("-s \"{0}\" pull {1} \"{2}\"", serial, remoteTmp, outputPath), 8000);
                await RunAsync(AdbPath, string.Format("-s \"{0}\" shell rm -f {1}", serial, remoteTmp), 4000);

                if (pullRes.ExitCode != 0 || !File.Exists(outputPath) || new FileInfo(outputPath).Length < 1024)
                    return Tuple.Create(false, "캡처 파일 전송 실패: " + pullRes.Combined.Trim());

                return Tuple.Create(true, outputPath);
            }
            catch (Exception ex)
            {
                return Tuple.Create(false, "캡처 오류: " + ex.Message);
            }
        }

        public async Task<Tuple<bool, string, string>> SwitchUsbToWifiAsync(string usbSerial)
        {
            if (!IsValid)
                return Tuple.Create(false, "scrcpy 폴더가 설정되지 않았습니다.", "");

            // 1. Get device Wi-Fi IP
            string ip = await GetDeviceWifiIpAsync(usbSerial);
            if (string.IsNullOrWhiteSpace(ip))
                return Tuple.Create(false, "폰의 Wi-Fi IP를 찾을 수 없습니다. PC와 같은 공유기(Wi-Fi)에 연결되어 있는지 확인하세요.", "");

            // 2. Enable tcpip on port 5555
            var tcpRes = await RunAsync(AdbPath, string.Format("-s \"{0}\" tcpip 5555", usbSerial), 8000);
            if (tcpRes.ExitCode != 0)
                return Tuple.Create(false, "tcpip 5555 전환 실패: " + tcpRes.Combined, "");

            await Task.Delay(1200);

            // 3. Connect wirelessly
            string target = ip + ":5555";
            var connRes = await ConnectWirelessAsync(target);
            string combined = connRes.Combined.Trim();
            bool ok = connRes.ExitCode == 0 && (combined.IndexOf("connected to", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                combined.IndexOf("already connected", StringComparison.OrdinalIgnoreCase) >= 0);

            if (ok)
                return Tuple.Create(true, string.Format("무선 연결 성공 ({0})! 이제 USB 케이블을 뽑으셔도 됩니다.", target), target);

            return Tuple.Create(false, "무선 연결 실패: " + combined, target);
        }

        private async Task<string> GetDeviceWifiIpAsync(string serial)
        {
            var res = await RunAsync(AdbPath, string.Format("-s \"{0}\" shell ip -f inet addr show wlan0", serial), 5000);
            var m = Regex.Match(res.StdOut ?? "", @"inet\s+(\d+\.\d+\.\d+\.\d+)/");
            if (m.Success) return m.Groups[1].Value;

            res = await RunAsync(AdbPath, string.Format("-s \"{0}\" shell ip route", serial), 5000);
            m = Regex.Match(res.StdOut ?? "", @"src\s+(\d+\.\d+\.\d+\.\d+)");
            if (m.Success) return m.Groups[1].Value;

            return "";
        }

        public async Task<Tuple<List<string>, string>> GetVideoEncodersAsync(string serial, string codecFilter)
        {
            var encoders = new List<string>();
            if (!IsValid || string.IsNullOrWhiteSpace(serial))
                return Tuple.Create(encoders, "");

            var res = await RunAsync(ScrcpyPath, string.Format("-s \"{0}\" --list-encoders", serial), 12000);
            string raw = res.Combined ?? "";
            string wantCodec = (codecFilter ?? "").Trim().ToLowerInvariant();
            foreach (var line in raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var m = Regex.Match(line, @"--video-codec=(\S+)\s+--video-encoder=(\S+)");
                if (!m.Success) continue;
                string c = m.Groups[1].Value.ToLowerInvariant();
                string enc = m.Groups[2].Value.Trim();
                if (!string.IsNullOrEmpty(wantCodec) && c != wantCodec) continue;
                if (!encoders.Contains(enc))
                    encoders.Add(enc);
            }
            return Tuple.Create(encoders, raw);
        }

        public async Task<List<AppItem>> GetInstalledAppsAsync(string serial)
        {
            var apps = new List<AppItem>();
            if (!IsValid || string.IsNullOrWhiteSpace(serial)) return apps;

            // Try scrcpy --list-apps first (returns friendly display names + package names)
            var res = await RunAsync(ScrcpyPath, string.Format("-s \"{0}\" --list-apps", serial), 15000);
            if (res.ExitCode == 0 && !string.IsNullOrWhiteSpace(res.StdOut))
            {
                var lines = res.StdOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    // Format: " * App Name                    com.package.name" or " - App Name   com.package.name"
                    var m = Regex.Match(line, @"^\s*([*-])\s+(.+?)\s{2,}([a-zA-Z0-9_\.]+)\s*$");
                    if (m.Success)
                    {
                        bool isSystem = m.Groups[1].Value == "*";
                        string name = m.Groups[2].Value.Trim();
                        string pkg = m.Groups[3].Value.Trim();
                        if (!isSystem)
                        {
                            apps.Add(new AppItem { Name = name, PackageName = pkg, IsSystem = false });
                        }
                    }
                }
            }

            // Fallback to adb pm list packages -3 if scrcpy --list-apps returned empty
            if (apps.Count == 0)
            {
                var pmRes = await RunAsync(AdbPath, string.Format("-s \"{0}\" shell pm list packages -3", serial), 10000);
                if (pmRes.ExitCode == 0 && !string.IsNullOrWhiteSpace(pmRes.StdOut))
                {
                    foreach (var raw in pmRes.StdOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string line = raw.Trim();
                        if (line.StartsWith("package:"))
                        {
                            string pkg = line.Substring("package:".Length).Trim();
                            apps.Add(new AppItem { Name = pkg, PackageName = pkg, IsSystem = false });
                        }
                    }
                }
            }

            return apps.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
