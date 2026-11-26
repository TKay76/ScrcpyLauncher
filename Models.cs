using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace ScrcpyLauncher
{
    public class DeviceInfo
    {
        public string Serial { get; set; }
        public string State { get; set; }
        public string Model { get; set; }
        public bool IsWifi { get; set; }

        public string MaskedSerial
        {
            get
            {
                if (string.IsNullOrEmpty(Serial)) return "";
                if (IsWifi || Serial.Contains(":")) return Serial;
                if (Serial.Length >= 6)
                    return Serial.Substring(0, 3) + "••••" + Serial.Substring(Serial.Length - 2);
                if (Serial.Length > 2)
                    return Serial.Substring(0, 1) + "••••";
                return "••••";
            }
        }

        public string DisplayName
        {
            get
            {
                string conn = IsWifi ? "Wi-Fi" : "USB";
                string masked = MaskedSerial;
                string name = string.IsNullOrWhiteSpace(Model) ? masked : Model.Replace("_", " ");
                if (State != "device")
                    return string.Format("{0} ({1} · {2}) — {3}", name, conn, State, masked);
                return string.Format("{0} ({1}) — {2}", name, conn, masked);
            }
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public class AppItem
    {
        public string Name { get; set; }
        public string PackageName { get; set; }
        public bool IsSystem { get; set; }

        public string DisplayText
        {
            get
            {
                if (string.IsNullOrEmpty(PackageName))
                    return Name;
                if (string.IsNullOrWhiteSpace(Name) || Name == PackageName)
                    return PackageName;
                return string.Format("{0}  ({1})", Name, PackageName);
            }
        }

        public override string ToString()
        {
            return DisplayText;
        }
    }

    public class LauncherSettings
    {
        public string CustomScrcpyDir { get; set; }
        public string LastWirelessAddress { get; set; }
        public string ControlMode { get; set; } // "keymap", "gamepad", "normal"
        public string SelectedKeymapProfile { get; set; }
        public string Codec { get; set; }
        public int BitrateMbps { get; set; }
        public int MaxSize { get; set; }
        public int MaxFps { get; set; }
        public string RenderDriver { get; set; }
        public bool TurnScreenOff { get; set; }
        public bool StayAwake { get; set; }
        public bool UhidGamepad { get; set; }
        public bool LowLatency { get; set; }
        public bool NoAudio { get; set; }
        public bool Fullscreen { get; set; }
        public bool AlwaysOnTop { get; set; }
        public bool PrintFps { get; set; }
        public bool DisableRightClickBack { get; set; }
        public bool KeymapExclusiveInput { get; set; }
        public bool GamepadKeyboardAssist { get; set; }
        public bool UseVirtualDisplay { get; set; }
        public string VirtualDisplaySpec { get; set; }
        public bool NoVdSystemDecorations { get; set; }
        public string StartAppPackage { get; set; }
        public string RecordFolder { get; set; }
        public double OverlayOpacity { get; set; }
        public double OverlayScale { get; set; }
        public string Language { get; set; }
        public int DailyQuestClearedCount { get; set; }
        public string DailyQuestDateKey { get; set; }
        public bool AudioBufferSafe { get; set; }
        public bool RememberWindowBounds { get; set; }
        public int LastWinX { get; set; }
        public int LastWinY { get; set; }
        public int LastWinW { get; set; }
        public int LastWinH { get; set; }
        public string ExtraScrcpyArgs { get; set; }
        public string SelectedVisualPreset { get; set; }
        public bool NestQuestsFirst { get; set; }
        public bool AutoMuteBackground { get; set; }
        public string VideoEncoder { get; set; }
        public int VideoBufferMs { get; set; }

        public LauncherSettings()
        {
            CustomScrcpyDir = "";
            LastWirelessAddress = "";
            ControlMode = "keymap";
            SelectedKeymapProfile = "";
            Codec = "h264";
            BitrateMbps = 20;
            MaxSize = 0;
            MaxFps = 0;
            RenderDriver = "auto";
            TurnScreenOff = true;
            StayAwake = true;
            UhidGamepad = false;
            LowLatency = false;
            NoAudio = false;
            Fullscreen = false;
            AlwaysOnTop = false;
            PrintFps = false;
            DisableRightClickBack = true;
            KeymapExclusiveInput = true;
            GamepadKeyboardAssist = false;
            UseVirtualDisplay = true;
            VirtualDisplaySpec = "1920x1080/280";
            NoVdSystemDecorations = true;
            StartAppPackage = "";
            RecordFolder = "";
            OverlayOpacity = 1.0;
            OverlayScale = 1.0;
            Language = "";
            DailyQuestClearedCount = 0;
            DailyQuestDateKey = "";
            AudioBufferSafe = false;
            RememberWindowBounds = true;
            LastWinX = -32000;
            LastWinY = -32000;
            LastWinW = 0;
            LastWinH = 0;
            ExtraScrcpyArgs = "";
            SelectedVisualPreset = "1080p";
        }

        public static string GetSettingsPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scrcpy_launcher_settings.json");
        }

        public static LauncherSettings Load()
        {
            try
            {
                string path = GetSettingsPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    var serializer = new JavaScriptSerializer();
                    var loaded = serializer.Deserialize<LauncherSettings>(json);
                    if (loaded != null)
                        return loaded;
                }
            }
            catch
            {
                // Ignore corrupted settings and fall back to defaults
            }
            return new LauncherSettings();
        }

        public void Save()
        {
            try
            {
                string path = GetSettingsPath();
                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(this);
                File.WriteAllText(path, json, new UTF8Encoding(false));
            }
            catch
            {
                // Ignore save errors (e.g. read-only directory)
            }
        }
    }
}
