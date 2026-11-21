using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace ScrcpyLauncher
{
    public class KeyBindItem
    {
        public int VkCode { get; set; }
        public int Modifiers { get; set; } // 0=None, 1=Ctrl, 2=Shift, 4=Alt
        public string Label { get; set; }
        public int X { get; set; } // 0..10000
        public int Y { get; set; } // 0..10000
        public bool IsWheelDrag { get; set; }
        public int WheelRadius { get; set; }

        public KeyBindItem()
        {
            WheelRadius = 1500;
        }

        public string KeyName
        {
            get { return KeyNameHelper.GetComboName(Modifiers, VkCode); }
        }

        public override string ToString()
        {
            string tag = IsWheelDrag ? " [휠드래그]" : "";
            string desc = string.IsNullOrWhiteSpace(Label) ? "" : " — " + Label;
            return string.Format("[{0}]{1}{2}  ({3:F1}%, {4:F1}%)", KeyName, tag, desc, X / 100.0, Y / 100.0);
        }
    }

    public class KeymapProfile
    {
        public string Name { get; set; }
        public bool IsBuiltIn { get; set; }
        public string FilePath { get; set; }

        [ScriptIgnore]
        public int SlotIndex { get; set; }

        [ScriptIgnore]
        public string DisplayName
        {
            get
            {
                if (SlotIndex >= 1 && SlotIndex <= 9)
                    return string.Format("[Ctrl+F{0}]  {1}", SlotIndex, Name);
                return Name;
            }
        }

        // WASD Joystick
        public bool EnableJoystick { get; set; }
        public int JoyX { get; set; }
        public int JoyY { get; set; }
        public int JoyRadius { get; set; }
        public int JoyUpVk { get; set; }
        public int JoyDownVk { get; set; }
        public int JoyLeftVk { get; set; }
        public int JoyRightVk { get; set; }
        public int SprintVk { get; set; }
        public double SprintScale { get; set; }

        // Mouse Camera Look
        public bool EnableMouseCamera { get; set; }
        public int CameraToggleVk { get; set; }
        public int CameraX { get; set; }
        public int CameraY { get; set; }
        public double CameraSensX { get; set; }
        public double CameraSensY { get; set; }

        // Mouse Clicks (while camera locked)
        public bool EnableLeftClick { get; set; }
        public int LeftClickX { get; set; }
        public int LeftClickY { get; set; }
        public string LeftClickLabel { get; set; }

        public bool EnableRightClick { get; set; }
        public bool RightClickIsWheelDrag { get; set; }
        public int RightClickX { get; set; }
        public int RightClickY { get; set; }
        public string RightClickLabel { get; set; }

        public List<KeyBindItem> Keys { get; set; }

        public KeymapProfile()
        {
            Name = "새 프로파일";
            EnableJoystick = true;
            JoyX = 1554;
            JoyY = 7442;
            JoyRadius = 792;
            JoyUpVk = 87;    // W
            JoyDownVk = 83;  // S
            JoyLeftVk = 65;  // A
            JoyRightVk = 68; // D
            SprintVk = 16;   // Shift
            SprintScale = 2.4;

            EnableMouseCamera = true;
            CameraToggleVk = 18; // Alt
            CameraX = 6335;
            CameraY = 2862;
            CameraSensX = 1.0;
            CameraSensY = 1.0;

            EnableLeftClick = true;
            LeftClickX = 8462;
            LeftClickY = 7876;
            LeftClickLabel = "공격/충전";

            EnableRightClick = false;
            RightClickX = 7411;
            RightClickY = 7578;
            RightClickLabel = "회피";

            Keys = new List<KeyBindItem>();
        }

        public KeymapProfile Clone(string newName)
        {
            var serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(this);
            var copy = serializer.Deserialize<KeymapProfile>(json);
            copy.Name = newName;
            copy.IsBuiltIn = false;
            return copy;
        }

        public static List<KeymapProfile> GetBuiltInPresets()
        {
            var list = new List<KeymapProfile>();

            // 1. Monster Hunter Outlanders — Classic Mode
            var mhoAdv = new KeymapProfile
            {
                Name = "[기본] 몬헌 아웃랜더스 (클래식 모드)",
                IsBuiltIn = true,
                EnableJoystick = true,
                JoyX = 1554,
                JoyY = 7442,
                JoyRadius = 850,
                SprintVk = 16, // Shift
                SprintScale = 2.4,
                EnableMouseCamera = true,
                CameraToggleVk = 18, // Alt
                CameraX = 6060,
                CameraY = 2780,
                CameraSensX = 1.0,
                CameraSensY = 1.0,
                EnableLeftClick = true,
                LeftClickX = 8460,
                LeftClickY = 7880,
                LeftClickLabel = "일반 공격",
                EnableRightClick = true,
                RightClickX = 7410,
                RightClickY = 7000,
                RightClickLabel = "특수 공격",
                Keys = new List<KeyBindItem>
                {
                    new KeyBindItem { VkCode = 32, Label = "회피", X = 9330, Y = 8750 },
                    new KeyBindItem { VkCode = 81, Label = "특수/Q", X = 7470, Y = 8750 },
                    new KeyBindItem { VkCode = 69, Label = "기술/E", X = 8390, Y = 6050 },
                    new KeyBindItem { VkCode = 82, Label = "동료 오의", X = 6710, Y = 6480 },
                    new KeyBindItem { VkCode = 70, Label = "상호작용", X = 5930, Y = 5280 },
                    new KeyBindItem { VkCode = 67, Label = "아이템 빠른 사용", X = 6790, Y = 7580 },
                    new KeyBindItem { VkCode = 71, Label = "아이템/탄환 휠", X = 6780, Y = 8760, IsWheelDrag = true, WheelRadius = 1500 },
                    new KeyBindItem { VkCode = 88, Label = "아이템 취소", X = 9010, Y = 6920 },
                    new KeyBindItem { VkCode = 49, Label = "오의 1번", X = 9290, Y = 2000 },
                    new KeyBindItem { VkCode = 50, Label = "슬롯 2", X = 9290, Y = 3180 },
                    new KeyBindItem { VkCode = 51, Label = "슬롯 3", X = 9290, Y = 4450 },
                    new KeyBindItem { VkCode = 52, Label = "슬롯 4", X = 9290, Y = 5700 },
                    new KeyBindItem { VkCode = 90, Label = "버튼 Z", X = 9340, Y = 6920 },
                    new KeyBindItem { VkCode = 72, Label = "버튼 H", X = 4860, Y = 7280 },
                    new KeyBindItem { VkCode = 86, Label = "버튼 V", X = 1340, Y = 3850 },
                    new KeyBindItem { VkCode = 84, Label = "버튼 T", X = 6740, Y = 3900 },
                    new KeyBindItem { VkCode = 74, Label = "구출", X = 4900, Y = 8850 },
                    new KeyBindItem { VkCode = 9, Label = "부위 조준 휠", X = 7760, Y = 3950, IsWheelDrag = true, WheelRadius = 1500 },
                    new KeyBindItem { VkCode = 77, Label = "지도", X = 960, Y = 1800 },
                    new KeyBindItem { VkCode = 192, Label = "건너뛰기", X = 9020, Y = 850 },
                    new KeyBindItem { VkCode = 27, Label = "메뉴", X = 9660, Y = 850 }
                }
            };
            list.Add(mhoAdv);

            // 2. Monster Hunter Outlanders — Starting Mode
            var mhoSimple = new KeymapProfile
            {
                Name = "[기본] 몬헌 아웃랜더스 (스타팅 모드)",
                IsBuiltIn = true,
                EnableJoystick = true,
                JoyX = 1554,
                JoyY = 7442,
                JoyRadius = 792,
                SprintVk = 16,
                SprintScale = 2.5,
                EnableMouseCamera = false,
                EnableLeftClick = false,
                EnableRightClick = false,
                Keys = new List<KeyBindItem>
                {
                    new KeyBindItem { VkCode = 37, Label = "버튼 ←", X = 7795, Y = 7094 },
                    new KeyBindItem { VkCode = 38, Label = "버튼 ↑", X = 8580, Y = 5862 },
                    new KeyBindItem { VkCode = 39, Label = "버튼 →", X = 9343, Y = 7396 },
                    new KeyBindItem { VkCode = 40, Label = "버튼 ↓", X = 8608, Y = 8579 }
                }
            };
            list.Add(mhoSimple);

            return list;
        }

        public static string GetKeymapsDir()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keymaps");
        }

        public static List<KeymapProfile> LoadCustomProfiles()
        {
            var result = new List<KeymapProfile>();
            string dir = GetKeymapsDir();
            if (!Directory.Exists(dir)) return result;

            var serializer = new JavaScriptSerializer();
            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file, Encoding.UTF8);
                    var p = serializer.Deserialize<KeymapProfile>(json);
                    if (p != null)
                    {
                        p.IsBuiltIn = false;
                        p.FilePath = file;
                        if (string.IsNullOrWhiteSpace(p.Name))
                            p.Name = Path.GetFileNameWithoutExtension(file);
                        if (p.Keys == null) p.Keys = new List<KeyBindItem>();
                        result.Add(p);
                    }
                }
                catch { }
            }
            return result;
        }

        public void SaveCustom()
        {
            string dir = GetKeymapsDir();
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string safeName = string.Join("_", (Name ?? "profile").Split(Path.GetInvalidFileNameChars())).Trim();
            if (string.IsNullOrEmpty(safeName)) safeName = "profile";
            string newPath = Path.Combine(dir, safeName + ".json");

            if (!string.IsNullOrEmpty(FilePath) && File.Exists(FilePath) &&
                !string.Equals(FilePath, newPath, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(FilePath); } catch { }
            }

            var serializer = new JavaScriptSerializer();
            IsBuiltIn = false;
            FilePath = newPath;
            File.WriteAllText(newPath, serializer.Serialize(this), new UTF8Encoding(false));
        }

        public void DeleteCustomFile()
        {
            if (IsBuiltIn) return;
            try
            {
                if (!string.IsNullOrEmpty(FilePath) && File.Exists(FilePath))
                    File.Delete(FilePath);
            }
            catch { }
        }

        public string ExportToShareCode()
        {
            using (var msOut = new MemoryStream())
            {
                using (var df = new DeflateStream(msOut, CompressionLevel.Optimal, true))
                using (var bw = new BinaryWriter(df, Encoding.UTF8, true))
                {
                    bw.Write((byte)2); // format v2
                    bw.Write(Name ?? "공유 프로파일");

                    byte flags = 0;
                    if (EnableJoystick) flags |= 1;
                    if (EnableMouseCamera) flags |= 2;
                    if (EnableLeftClick) flags |= 4;
                    if (EnableRightClick) flags |= 8;
                    if (RightClickIsWheelDrag) flags |= 16;
                    bw.Write(flags);

                    if (EnableJoystick)
                    {
                        bw.Write((ushort)Math.Max(0, Math.Min(10000, JoyX)));
                        bw.Write((ushort)Math.Max(0, Math.Min(10000, JoyY)));
                        bw.Write((ushort)Math.Max(100, Math.Min(5000, JoyRadius)));
                        bw.Write((byte)(JoyUpVk & 0xFF));
                        bw.Write((byte)(JoyDownVk & 0xFF));
                        bw.Write((byte)(JoyLeftVk & 0xFF));
                        bw.Write((byte)(JoyRightVk & 0xFF));
                        bw.Write((byte)(SprintVk & 0xFF));
                        bw.Write((byte)Math.Max(10, Math.Min(50, (int)Math.Round(SprintScale * 10.0))));
                    }

                    if (EnableMouseCamera)
                    {
                        bw.Write((byte)(CameraToggleVk & 0xFF));
                        bw.Write((ushort)Math.Max(0, Math.Min(10000, CameraX)));
                        bw.Write((ushort)Math.Max(0, Math.Min(10000, CameraY)));
                        bw.Write((byte)Math.Max(2, Math.Min(60, (int)Math.Round(CameraSensX * 20.0))));
                        bw.Write((byte)Math.Max(2, Math.Min(60, (int)Math.Round(CameraSensY * 20.0))));
                    }

                    if (EnableLeftClick)
                    {
                        bw.Write((ushort)Math.Max(0, Math.Min(10000, LeftClickX)));
                        bw.Write((ushort)Math.Max(0, Math.Min(10000, LeftClickY)));
                        bw.Write("");
                    }

                    if (EnableRightClick)
                    {
                        bw.Write((ushort)Math.Max(0, Math.Min(10000, RightClickX)));
                        bw.Write((ushort)Math.Max(0, Math.Min(10000, RightClickY)));
                        bw.Write("");
                    }

                    var keys = Keys ?? new List<KeyBindItem>();
                    int count = Math.Min(255, keys.Count);
                    bw.Write((byte)count);
                    for (int i = 0; i < count; i++)
                    {
                        var k = keys[i];
                        bool hasCustomRad = k.IsWheelDrag && k.WheelRadius != 1500;
                        // Omit per-key cosmetic labels in SLKM2 so even 25+ key profiles stay well under 300 chars (DCInside 400-char comment limit)
                        byte kf = (byte)((k.Modifiers & 0x07) | (k.IsWheelDrag ? 8 : 0) | (hasCustomRad ? 16 : 0));
                        bw.Write((byte)(k.VkCode & 0xFF));
                        bw.Write(kf);
                        bw.Write((ushort)Math.Max(0, Math.Min(10000, k.X)));
                        bw.Write((ushort)Math.Max(0, Math.Min(10000, k.Y)));
                        if (hasCustomRad) bw.Write((ushort)Math.Max(200, Math.Min(5000, k.WheelRadius)));
                    }
                }
                return "SLKM2:" + Convert.ToBase64String(msOut.ToArray());
            }
        }

        public static KeymapProfile ImportFromShareCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new Exception("프리셋 코드가 비어 있습니다.");

            string trimmed = code.Trim();
            if (trimmed.StartsWith("SLKM2:", StringComparison.OrdinalIgnoreCase))
            {
                byte[] compressed = Convert.FromBase64String(trimmed.Substring(6).Trim());
                using (var msIn = new MemoryStream(compressed))
                using (var df = new DeflateStream(msIn, CompressionMode.Decompress))
                using (var br = new BinaryReader(df, Encoding.UTF8))
                {
                    byte ver = br.ReadByte();
                    if (ver != 2) throw new Exception("지원하지 않는 프리셋 코드 버전입니다.");

                    var p = new KeymapProfile
                    {
                        Name = br.ReadString(),
                        IsBuiltIn = false,
                        FilePath = null,
                        Keys = new List<KeyBindItem>()
                    };

                    byte flags = br.ReadByte();
                    p.EnableJoystick = (flags & 1) != 0;
                    p.EnableMouseCamera = (flags & 2) != 0;
                    p.EnableLeftClick = (flags & 4) != 0;
                    p.EnableRightClick = (flags & 8) != 0;
                    p.RightClickIsWheelDrag = (flags & 16) != 0;

                    if (p.EnableJoystick)
                    {
                        p.JoyX = br.ReadUInt16();
                        p.JoyY = br.ReadUInt16();
                        p.JoyRadius = br.ReadUInt16();
                        p.JoyUpVk = br.ReadByte();
                        p.JoyDownVk = br.ReadByte();
                        p.JoyLeftVk = br.ReadByte();
                        p.JoyRightVk = br.ReadByte();
                        p.SprintVk = br.ReadByte();
                        p.SprintScale = br.ReadByte() / 10.0;
                    }

                    if (p.EnableMouseCamera)
                    {
                        p.CameraToggleVk = br.ReadByte();
                        p.CameraX = br.ReadUInt16();
                        p.CameraY = br.ReadUInt16();
                        p.CameraSensX = br.ReadByte() / 20.0;
                        p.CameraSensY = br.ReadByte() / 20.0;
                    }

                    if (p.EnableLeftClick)
                    {
                        p.LeftClickX = br.ReadUInt16();
                        p.LeftClickY = br.ReadUInt16();
                        p.LeftClickLabel = br.ReadString();
                    }

                    if (p.EnableRightClick)
                    {
                        p.RightClickX = br.ReadUInt16();
                        p.RightClickY = br.ReadUInt16();
                        p.RightClickLabel = br.ReadString();
                    }

                    int count = br.ReadByte();
                    for (int i = 0; i < count; i++)
                    {
                        int vk = br.ReadByte();
                        byte kf = br.ReadByte();
                        int x = br.ReadUInt16();
                        int y = br.ReadUInt16();
                        int rad = ((kf & 16) != 0) ? br.ReadUInt16() : 1500;
                        string lbl = ((kf & 32) != 0) ? br.ReadString() : "";
                        p.Keys.Add(new KeyBindItem
                        {
                            VkCode = vk,
                            Modifiers = kf & 0x07,
                            IsWheelDrag = (kf & 8) != 0,
                            WheelRadius = rad,
                            Label = lbl,
                            X = x,
                            Y = y
                        });
                    }

                    if (string.IsNullOrWhiteSpace(p.Name))
                        p.Name = "공유받은 프로파일";
                    return p;
                }
            }

            if (!trimmed.StartsWith("SLKM1:", StringComparison.OrdinalIgnoreCase))
                throw new Exception("올바른 Scrcpy Launcher 프리셋 코드(SLKM2:... 또는 SLKM1:...)가 아닙니다.");

            byte[] compV1 = Convert.FromBase64String(trimmed.Substring(6).Trim());
            string json;
            using (var msIn = new MemoryStream(compV1))
            using (var gz = new GZipStream(msIn, CompressionMode.Decompress))
            using (var reader = new StreamReader(gz, Encoding.UTF8))
            {
                json = reader.ReadToEnd();
            }

            var serializer = new JavaScriptSerializer();
            var profile = serializer.Deserialize<KeymapProfile>(json);
            if (profile == null)
                throw new Exception("프리셋 코드를 해석할 수 없습니다.");

            profile.IsBuiltIn = false;
            profile.FilePath = null;
            if (string.IsNullOrWhiteSpace(profile.Name))
                profile.Name = "공유받은 프로파일";
            if (profile.Keys == null)
                profile.Keys = new List<KeyBindItem>();
            return profile;
        }

        public static KeymapProfile ImportFromLdKmp(string filePath)
        {
            string json = File.ReadAllText(filePath, Encoding.UTF8);
            var serializer = new JavaScriptSerializer();
            var root = serializer.Deserialize<Dictionary<string, object>>(json);
            if (root == null || !root.ContainsKey("keyboardMappings"))
                throw new Exception("유효한 LDPlayer .kmp 파일이 아닙니다.");

            var profile = new KeymapProfile
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                IsBuiltIn = false,
                EnableJoystick = false,
                EnableMouseCamera = false,
                EnableLeftClick = false,
                EnableRightClick = false,
                Keys = new List<KeyBindItem>()
            };

            var mappings = root["keyboardMappings"] as System.Collections.ArrayList;
            if (mappings == null) return profile;

            foreach (var entryObj in mappings)
            {
                var entry = entryObj as Dictionary<string, object>;
                if (entry == null) continue;
                string cls = entry.ContainsKey("class") ? Convert.ToString(entry["class"]) : "";
                var data = entry.ContainsKey("data") ? entry["data"] as Dictionary<string, object> : null;
                if (data == null) continue;

                string desc = data.ContainsKey("description") ? Convert.ToString(data["description"]) : "";

                if (cls == "ClassKeyboardDisc")
                {
                    profile.EnableJoystick = true;
                    int ox, oy;
                    ReadPoint(data, "origin", out ox, out oy);
                    profile.JoyX = ox;
                    profile.JoyY = oy;
                    profile.JoyRadius = ReadInt(data, "radius", 792);
                    profile.JoyUpVk = ReadInt(data, "upKey", 87);
                    profile.JoyDownVk = ReadInt(data, "downKey", 83);
                    profile.JoyLeftVk = ReadInt(data, "leftKey", 65);
                    profile.JoyRightVk = ReadInt(data, "rightKey", 68);
                }
                else if (cls == "ClassMouseDrag")
                {
                    profile.EnableMouseCamera = true;
                    profile.CameraToggleVk = ReadInt(data, "key", 18);
                    int ox, oy;
                    ReadPoint(data, "origin", out ox, out oy);
                    profile.CameraX = ox;
                    profile.CameraY = oy;
                    profile.CameraSensX = ReadDouble(data, "sensitivity", 1.0);
                    profile.CameraSensY = ReadDouble(data, "sensitivity_y", 1.0);
                }
                else if (cls == "ClassMouseTrigger")
                {
                    profile.EnableLeftClick = true;
                    int px, py;
                    ReadPoint(data, "point", out px, out py);
                    profile.LeftClickX = px;
                    profile.LeftClickY = py;
                    profile.LeftClickLabel = string.IsNullOrWhiteSpace(desc) ? "좌클릭" : desc;
                }
                else if (cls == "ClassKeyboardCurve")
                {
                    int vk = ReadInt(data, "key", 0);
                    var curve = data.ContainsKey("curve") ? data["curve"] as System.Collections.ArrayList : null;
                    if (vk > 0 && curve != null && curve.Count > 0)
                    {
                        var first = curve[0] as Dictionary<string, object>;
                        if (first != null)
                        {
                            profile.Keys.Add(new KeyBindItem
                            {
                                VkCode = vk,
                                Label = desc,
                                X = ReadInt(first, "x", 5000),
                                Y = ReadInt(first, "y", 5000)
                            });
                        }
                    }
                }
                else if (cls == "ClassKeyboardClick")
                {
                    int vk = ReadInt(data, "key", 0);
                    if (vk > 0)
                    {
                        int px, py;
                        ReadPoint(data, "point", out px, out py);
                        profile.Keys.Add(new KeyBindItem { VkCode = vk, Label = desc, X = px, Y = py });
                    }
                }
                else if (cls == "ClassMouseDisc")
                {
                    int vk = ReadInt(data, "key", 0);
                    if (vk > 0)
                    {
                        int ox, oy;
                        ReadPoint(data, "origin", out ox, out oy);
                        int rad = ReadInt(data, "radius", 1400);
                        profile.Keys.Add(new KeyBindItem
                        {
                            VkCode = vk,
                            Label = string.IsNullOrWhiteSpace(desc) ? "휠 선택" : desc,
                            X = ox,
                            Y = oy,
                            IsWheelDrag = true,
                            WheelRadius = Math.Max(800, Math.Min(3000, rad + 400))
                        });
                    }
                }
                else if (cls == "ClassKeyboardMacros")
                {
                    int vk = ReadInt(data, "key", 0);
                    string macros = data.ContainsKey("macros") ? Convert.ToString(data["macros"]) : "";
                    if (macros.IndexOf("wasd-scale", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        profile.SprintVk = vk > 0 ? vk : 16;
                        var m = System.Text.RegularExpressions.Regex.Match(macros, @"wasd-scale\s+([\d\.]+)");
                        double sc;
                        if (m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out sc))
                            profile.SprintScale = sc;
                    }
                    else if (vk > 0 && data.ContainsKey("origin"))
                    {
                        int ox, oy;
                        ReadPoint(data, "origin", out ox, out oy);
                        profile.Keys.Add(new KeyBindItem { VkCode = vk, Label = desc, X = ox, Y = oy });
                    }
                }
            }

            return profile;
        }

        private static void ReadPoint(Dictionary<string, object> data, string key, out int x, out int y)
        {
            x = 5000; y = 5000;
            if (!data.ContainsKey(key)) return;
            var pt = data[key] as Dictionary<string, object>;
            if (pt == null) return;
            x = ReadInt(pt, "x", 5000);
            y = ReadInt(pt, "y", 5000);
        }

        private static int ReadInt(Dictionary<string, object> d, string key, int def)
        {
            if (!d.ContainsKey(key) || d[key] == null) return def;
            try { return Convert.ToInt32(d[key]); } catch { return def; }
        }

        private static double ReadDouble(Dictionary<string, object> d, string key, double def)
        {
            if (!d.ContainsKey(key) || d[key] == null) return def;
            try { return Convert.ToDouble(d[key]); } catch { return def; }
        }
    }

    public static class KeyNameHelper
    {
        public static string GetComboName(int modifiers, int vk)
        {
            string key = GetName(vk);
            if (modifiers == 0) return key;
            var parts = new List<string>();
            if ((modifiers & 1) != 0) parts.Add("Ctrl");
            if ((modifiers & 2) != 0) parts.Add("Shift");
            if ((modifiers & 4) != 0) parts.Add("Alt");
            parts.Add(key);
            return string.Join("+", parts.ToArray());
        }

        public static string GetName(int vk)
        {
            switch (vk)
            {
                case 4: return "휠클릭";
                case 5: return "마우스4";
                case 6: return "마우스5";
                case 8: return "Backspace";
                case 9: return "Tab";
                case 13: return "Enter";
                case 16: case 160: case 161: return "Shift";
                case 17: case 162: case 163: return "Ctrl";
                case 18: case 164: case 165: return "Alt";
                case 19: return "Pause";
                case 20: return "CapsLock";
                case 21: return "한/영";
                case 25: return "한자";
                case 27: return "ESC";
                case 32: return "Space";
                case 33: return "PgUp";
                case 34: return "PgDn";
                case 35: return "End";
                case 36: return "Home";
                case 37: return "←";
                case 38: return "↑";
                case 39: return "→";
                case 40: return "↓";
                case 44: return "PrtSc";
                case 45: return "Insert";
                case 46: return "Delete";
                case 106: return "Num*";
                case 107: return "Num+";
                case 109: return "Num-";
                case 110: return "Num.";
                case 111: return "Num/";
                case 144: return "NumLock";
                case 145: return "ScrLk";
                case 186: return ";";
                case 187: return "=";
                case 188: return ",";
                case 189: return "-";
                case 190: return ".";
                case 191: return "/";
                case 192: return "` (~)";
                case 219: return "[";
                case 220: return "\\";
                case 221: return "]";
                case 222: return "'";
                default:
                    if (vk >= 48 && vk <= 57) return ((char)vk).ToString();
                    if (vk >= 65 && vk <= 90) return ((char)vk).ToString();
                    if (vk >= 96 && vk <= 105) return "Num" + (vk - 96);
                    if (vk >= 112 && vk <= 123) return "F" + (vk - 111);
                    return "VK_" + vk;
            }
        }
    }
}
