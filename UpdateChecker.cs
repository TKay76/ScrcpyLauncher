using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ScrcpyLauncher
{
    public class UpdateInfo
    {
        public bool HasUpdate { get; set; }
        public string LatestTag { get; set; }
        public string ReleaseUrl { get; set; }
        public string ExeDownloadUrl { get; set; }
    }

    public static class UpdateChecker
    {
        public const string CurrentVersion = "1.3.4";
        public const string RepoOwner = "TKay76";
        public const string RepoName = "ScrcpyLauncher";
        public const string ReleasesPageUrl = "https://github.com/TKay76/ScrcpyLauncher/releases";
        private const string LatestApiUrl = "https://api.github.com/repos/TKay76/ScrcpyLauncher/releases/latest";

        public static Task<UpdateInfo> CheckAsync()
        {
            return Task.Run(() =>
            {
                var info = new UpdateInfo { HasUpdate = false, LatestTag = "", ReleaseUrl = ReleasesPageUrl, ExeDownloadUrl = "" };
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    var req = (HttpWebRequest)WebRequest.Create(LatestApiUrl);
                    req.Method = "GET";
                    req.UserAgent = "ScrcpyLauncher/" + CurrentVersion;
                    req.Accept = "application/vnd.github+json";
                    req.Timeout = 5000;
                    req.ReadWriteTimeout = 5000;

                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var stream = resp.GetResponseStream())
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string json = reader.ReadToEnd();
                        var serializer = new JavaScriptSerializer();
                        var dict = serializer.Deserialize<Dictionary<string, object>>(json);
                        if (dict != null && dict.ContainsKey("tag_name") && dict["tag_name"] != null)
                        {
                            string tag = dict["tag_name"].ToString().Trim();
                            string url = (dict.ContainsKey("html_url") && dict["html_url"] != null)
                                ? dict["html_url"].ToString().Trim()
                                : ReleasesPageUrl;

                            info.LatestTag = tag;
                            info.ReleaseUrl = url;
                            info.HasUpdate = IsNewerVersion(tag, CurrentVersion);

                            if (dict.ContainsKey("assets") && dict["assets"] is ArrayList)
                            {
                                var assets = (ArrayList)dict["assets"];
                                foreach (var assetObj in assets)
                                {
                                    var asset = assetObj as Dictionary<string, object>;
                                    if (asset == null) continue;
                                    string name = asset.ContainsKey("name") && asset["name"] != null ? asset["name"].ToString() : "";
                                    if (string.Equals(name, "ScrcpyLauncher.exe", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (asset.ContainsKey("browser_download_url") && asset["browser_download_url"] != null)
                                            info.ExeDownloadUrl = asset["browser_download_url"].ToString().Trim();
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Silently ignore network errors or 404 (before first release is published)
                }
                return info;
            });
        }

        public static Task<Tuple<bool, string>> DownloadAndApplyUpdateAsync(string downloadUrl)
        {
            return Task.Run(() =>
            {
                string curExe = Assembly.GetExecutingAssembly().Location;
                string newExe = curExe + ".new";
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    var req = (HttpWebRequest)WebRequest.Create(downloadUrl);
                    req.Method = "GET";
                    req.UserAgent = "ScrcpyLauncher/" + CurrentVersion;
                    req.AllowAutoRedirect = true;
                    req.Timeout = 15000;
                    req.ReadWriteTimeout = 30000;

                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var netStream = resp.GetResponseStream())
                    using (var fs = new FileStream(newExe, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        netStream.CopyTo(fs);
                    }

                    // Validate downloaded PE executable (size >= 50KB and starts with 'MZ')
                    var fi = new FileInfo(newExe);
                    if (!fi.Exists || fi.Length < 50000)
                    {
                        try { File.Delete(newExe); } catch { }
                        return Tuple.Create(false, "다운로드된 파일 크기가 올바르지 않습니다.");
                    }

                    byte[] hdr = new byte[2];
                    using (var fs = new FileStream(newExe, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        fs.Read(hdr, 0, 2);
                    }
                    if (hdr[0] != 0x4D || hdr[1] != 0x5A) // 'M', 'Z'
                    {
                        try { File.Delete(newExe); } catch { }
                        return Tuple.Create(false, "다운로드된 파일이 유효한 실행 파일이 아닙니다.");
                    }

                    string batPath = Path.Combine(Path.GetTempPath(), "scrcpy_launcher_update_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".bat");
                    string batContent =
                        "@echo off\r\n" +
                        "chcp 65001 >nul\r\n" +
                        "timeout /t 1 /nobreak >nul\r\n" +
                        "set /a tries=0\r\n" +
                        ":retry\r\n" +
                        "move /Y \"" + newExe + "\" \"" + curExe + "\" >nul 2>&1\r\n" +
                        "if errorlevel 1 (\r\n" +
                        "  set /a tries+=1\r\n" +
                        "  if %tries% LSS 15 (\r\n" +
                        "    timeout /t 1 /nobreak >nul\r\n" +
                        "    goto retry\r\n" +
                        "  )\r\n" +
                        "  exit /b 1\r\n" +
                        ")\r\n" +
                        "start \"\" \"" + curExe + "\"\r\n" +
                        "del \"%~f0\"\r\n";

                    File.WriteAllText(batPath, batContent, new UTF8Encoding(false));

                    var psi = new ProcessStartInfo("cmd.exe", "/c \"" + batPath + "\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    Process.Start(psi);
                    return Tuple.Create(true, "업데이트 준비 완료");
                }
                catch (Exception ex)
                {
                    try { if (File.Exists(newExe)) File.Delete(newExe); } catch { }
                    return Tuple.Create(false, ex.Message);
                }
            });
        }

        private static bool IsNewerVersion(string remoteTag, string currentVer)
        {
            Version remote = ParseVersion(remoteTag);
            Version current = ParseVersion(currentVer);
            if (remote == null || current == null) return false;
            return remote > current;
        }

        private static Version ParseVersion(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string cleaned = raw.Trim();
            if (cleaned.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(1);
            int dash = cleaned.IndexOf('-');
            if (dash > 0) cleaned = cleaned.Substring(0, dash);

            string[] parts = cleaned.Split('.');
            if (parts.Length == 2) cleaned = cleaned + ".0";
            else if (parts.Length == 1) cleaned = cleaned + ".0.0";

            Version v;
            return Version.TryParse(cleaned, out v) ? v : null;
        }
    }
}
