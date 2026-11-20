using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
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
    }

    public static class UpdateChecker
    {
        public const string CurrentVersion = "1.2.0";
        public const string RepoOwner = "TKay76";
        public const string RepoName = "ScrcpyLauncher";
        public const string ReleasesPageUrl = "https://github.com/TKay76/ScrcpyLauncher/releases";
        private const string LatestApiUrl = "https://api.github.com/repos/TKay76/ScrcpyLauncher/releases/latest";

        public static Task<UpdateInfo> CheckAsync()
        {
            return Task.Run(() =>
            {
                var info = new UpdateInfo { HasUpdate = false, LatestTag = "", ReleaseUrl = ReleasesPageUrl };
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
