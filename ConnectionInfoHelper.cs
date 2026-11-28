using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ScrcpyLauncher
{
    public class ConnectionLinkInfo
    {
        public bool IsWireless;
        public string Tier;        // "fast", "mid", "warn", "none"
        public string BadgeText;
        public string DetailText;
        public string TooltipText;
    }

    public class BatteryLinkInfo
    {
        public int Level;
        public double TempC;
        public double ApTempC;
        public double SkinTempC;
        public int ThermalStatus;  // 0=None, 1=Light, 2=Moderate, 3=Severe, 4+=Critical
        public bool IsCharging;
        public string Tier;        // "cool", "warm", "hot"
        public string BadgeText;
        public string TooltipText;
    }

    public static class ConnectionInfoHelper
    {
        private static readonly Guid GUID_DEVINTERFACE_USB_HUB = new Guid("f18a0e88-c30c-11d0-8815-00a0c906bed8");
        private const uint DIGCF_PRESENT = 0x02;
        private const uint DIGCF_DEVICEINTERFACE = 0x10;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 1;
        private const uint FILE_SHARE_WRITE = 2;
        private const uint OPEN_EXISTING = 3;

        private const uint IOCTL_USB_GET_NODE_INFORMATION = 0x00220408;
        private const uint IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX = 0x00220448;
        private const uint IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX_V2 = 0x0022045C;
        private const uint IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION = 0x00220410;

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SP_DEVICE_INTERFACE_DETAIL_DATA
        {
            public int cbSize;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
            public string DevicePath;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct USB_HUB_DESCRIPTOR
        {
            public byte bDescriptorLength;
            public byte bDescriptorType;
            public byte bNumberOfPorts;
            public ushort wHubCharacteristics;
            public byte bPowerOnToPowerGood;
            public byte bHubControlCurrent;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] bRemoveAndPowerMask;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct USB_NODE_INFORMATION
        {
            public int NodeType;
            public USB_HUB_DESCRIPTOR HubDescriptor;
            public byte HubIsBusPowered;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct USB_DEVICE_DESCRIPTOR
        {
            public byte bLength, bDescriptorType;
            public ushort bcdUSB;
            public byte bDeviceClass, bDeviceSubClass, bDeviceProtocol, bMaxPacketSize0;
            public ushort idVendor, idProduct, bcdDevice;
            public byte iManufacturer, iProduct, iSerialNumber, bNumConfigurations;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct USB_NODE_CONNECTION_INFORMATION_EX
        {
            public uint ConnectionIndex;
            public USB_DEVICE_DESCRIPTOR DeviceDescriptor;
            public byte CurrentConfigurationValue;
            public byte Speed;
            public byte DeviceIsHub;
            public ushort DeviceAddress;
            public uint NumberOfOpenPipes;
            public uint ConnectionStatus;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct USB_NODE_CONNECTION_INFORMATION_EX_V2
        {
            public uint ConnectionIndex;
            public uint Length;
            public uint SupportedUsbProtocols;
            public uint Flags;
        }

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, uint Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(IntPtr DeviceInfoSet, IntPtr DeviceInfoData, ref Guid InterfaceClassGuid, uint MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr DeviceInfoSet, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, ref SP_DEVICE_INTERFACE_DETAIL_DATA DeviceInterfaceDetailData, int DeviceInterfaceDetailDataSize, out int RequiredSize, IntPtr DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode, IntPtr lpInBuffer, int nInBufferSize, IntPtr lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private static string GetUsbString(IntPtr hHub, uint port, byte index)
        {
            if (index == 0) return "";
            int reqSize = 12 + 256;
            IntPtr buf = Marshal.AllocHGlobal(reqSize);
            try
            {
                for (int i = 0; i < reqSize; i++) Marshal.WriteByte(buf, i, 0);
                Marshal.WriteInt32(buf, 0, (int)port);
                Marshal.WriteByte(buf, 4, 0x80);
                Marshal.WriteByte(buf, 5, 0x06);
                Marshal.WriteInt16(buf, 6, (short)((3 << 8) | index));
                Marshal.WriteInt16(buf, 8, 0x0409);
                Marshal.WriteInt16(buf, 10, 252);
                int ret;
                if (!DeviceIoControl(hHub, IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION, buf, reqSize, buf, reqSize, out ret, IntPtr.Zero))
                    return "";
                if (ret < 14) return "";
                int strLen = Marshal.ReadByte(buf, 12);
                if (strLen < 2 || 12 + strLen > ret) return "";
                byte[] bytes = new byte[strLen - 2];
                Marshal.Copy(new IntPtr(buf.ToInt64() + 14), bytes, 0, bytes.Length);
                return Encoding.Unicode.GetString(bytes).Trim('\0');
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        public static async Task<ConnectionLinkInfo> ProbeAsync(AdbService adb, DeviceInfo dev)
        {
            if (dev == null || dev.State != "device")
                return null;

            if (dev.IsWifi)
                return await ProbeWifiAsync(adb, dev.Serial);

            return await Task.Run(() => ProbeUsbBySerial(dev.Serial));
        }

        private static ConnectionLinkInfo ProbeUsbBySerial(string targetSerial)
        {
            if (string.IsNullOrWhiteSpace(targetSerial)) return null;
            Guid guid = GUID_DEVINTERFACE_USB_HUB;
            IntPtr hInfo = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (hInfo == IntPtr.Zero || hInfo == new IntPtr(-1))
                return MakeFallbackUsb();

            // Track highest detected USB speed across all hubs (external USB 3.0 hubs expose both a 2.0 companion hub and a 3.0 SS hub!)
            int bestRank = -1; // 0=Low, 1=Full, 2=High(USB2), 3=SuperSpeed(5Gbps), 4=SuperSpeedPlus(10Gbps+)
            bool bestSsCapable = false;

            try
            {
                uint idx = 0;
                var ifData = new SP_DEVICE_INTERFACE_DATA();
                ifData.cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));

                while (SetupDiEnumDeviceInterfaces(hInfo, IntPtr.Zero, ref guid, idx++, ref ifData))
                {
                    var det = new SP_DEVICE_INTERFACE_DETAIL_DATA();
                    det.cbSize = (IntPtr.Size == 8) ? 8 : 6;
                    int req;
                    if (!SetupDiGetDeviceInterfaceDetail(hInfo, ref ifData, ref det, Marshal.SizeOf(det), out req, IntPtr.Zero))
                        continue;

                    IntPtr hHub = CreateFile(det.DevicePath, GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                    if (hHub == IntPtr.Zero || hHub == new IntPtr(-1)) continue;

                    try
                    {
                        int nodeSz = Marshal.SizeOf(typeof(USB_NODE_INFORMATION));
                        IntPtr pNode = Marshal.AllocHGlobal(nodeSz);
                        try
                        {
                            int ret;
                            if (!DeviceIoControl(hHub, IOCTL_USB_GET_NODE_INFORMATION, pNode, nodeSz, pNode, nodeSz, out ret, IntPtr.Zero))
                                continue;
                            var node = (USB_NODE_INFORMATION)Marshal.PtrToStructure(pNode, typeof(USB_NODE_INFORMATION));
                            int ports = node.HubDescriptor.bNumberOfPorts;

                            for (uint p = 1; p <= ports; p++)
                            {
                                int exSz = Marshal.SizeOf(typeof(USB_NODE_CONNECTION_INFORMATION_EX)) + 1024;
                                IntPtr pEx = Marshal.AllocHGlobal(exSz);
                                try
                                {
                                    for (int k = 0; k < 64; k++) Marshal.WriteByte(pEx, k, 0);
                                    Marshal.WriteInt32(pEx, 0, (int)p);
                                    if (!DeviceIoControl(hHub, IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX, pEx, exSz, pEx, exSz, out ret, IntPtr.Zero))
                                        continue;
                                    var ex = (USB_NODE_CONNECTION_INFORMATION_EX)Marshal.PtrToStructure(pEx, typeof(USB_NODE_CONNECTION_INFORMATION_EX));
                                    if (ex.ConnectionStatus != 1 || ex.DeviceIsHub != 0) continue;

                                    string ser = GetUsbString(hHub, p, ex.DeviceDescriptor.iSerialNumber);
                                    if (!string.Equals(ser, targetSerial, StringComparison.OrdinalIgnoreCase))
                                        continue;

                                    bool isSs = (ex.Speed == 3) || (ex.DeviceDescriptor.bcdUSB >= 0x0300);
                                    bool isSsp = false;
                                    bool isSsCapable = (ex.DeviceDescriptor.bcdUSB >= 0x0210);

                                    int v2Sz = Marshal.SizeOf(typeof(USB_NODE_CONNECTION_INFORMATION_EX_V2));
                                    IntPtr pV2 = Marshal.AllocHGlobal(v2Sz);
                                    try
                                    {
                                        var v2 = new USB_NODE_CONNECTION_INFORMATION_EX_V2
                                        {
                                            ConnectionIndex = p,
                                            Length = (uint)v2Sz,
                                            SupportedUsbProtocols = 7
                                        };
                                        Marshal.StructureToPtr(v2, pV2, false);
                                        int r2;
                                        if (DeviceIoControl(hHub, IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX_V2, pV2, v2Sz, pV2, v2Sz, out r2, IntPtr.Zero))
                                        {
                                            var v2Out = (USB_NODE_CONNECTION_INFORMATION_EX_V2)Marshal.PtrToStructure(pV2, typeof(USB_NODE_CONNECTION_INFORMATION_EX_V2));
                                            // bit 0: DeviceIsOperatingAtSuperSpeedOrHigher
                                            // bit 1: DeviceIsSuperSpeedCapableOrHigher
                                            // bit 2: DeviceIsOperatingAtSuperSpeedPlusOrHigher
                                            // bit 3: DeviceIsSuperSpeedPlusCapableOrHigher
                                            if ((v2Out.Flags & 1) != 0) isSs = true;
                                            if ((v2Out.Flags & 4) != 0) isSsp = true;
                                            if ((v2Out.Flags & 2) != 0 || (v2Out.Flags & 8) != 0) isSsCapable = true;
                                        }
                                    }
                                    finally { Marshal.FreeHGlobal(pV2); }

                                    int rank = isSsp ? 4 : (isSs ? 3 : (int)ex.Speed);
                                    if (rank > bestRank)
                                    {
                                        bestRank = rank;
                                        bestSsCapable = isSsCapable;
                                    }
                                }
                                finally { Marshal.FreeHGlobal(pEx); }
                            }
                        }
                        finally { Marshal.FreeHGlobal(pNode); }
                    }
                    finally { CloseHandle(hHub); }
                }
            }
            catch { }
            finally { SetupDiDestroyDeviceInfoList(hInfo); }

            if (bestRank == 4)
            {
                return new ConnectionLinkInfo
                {
                    IsWireless = false,
                    Tier = "fast",
                    BadgeText = "⚡ USB 3.2 Gen 2 · 10 Gbps+",
                    DetailText = Loc.T("conn_usb3_ok"),
                    TooltipText = Loc.T("conn_usb3_tip")
                };
            }
            if (bestRank == 3)
            {
                return new ConnectionLinkInfo
                {
                    IsWireless = false,
                    Tier = "fast",
                    BadgeText = "⚡ USB 3.2 Gen 1 · 5 Gbps",
                    DetailText = Loc.T("conn_usb3_ok"),
                    TooltipText = Loc.T("conn_usb3_tip")
                };
            }
            if (bestRank == 2)
            {
                return new ConnectionLinkInfo
                {
                    IsWireless = false,
                    Tier = "mid",
                    BadgeText = "🔌 USB 2.0 · 480 Mbps",
                    DetailText = bestSsCapable ? Loc.T("conn_usb2_upgrade") : Loc.T("conn_usb2_ok"),
                    TooltipText = bestSsCapable ? Loc.T("conn_usb2_upgrade_tip") : Loc.T("conn_usb2_tip")
                };
            }
            if (bestRank == 0 || bestRank == 1)
            {
                return new ConnectionLinkInfo
                {
                    IsWireless = false,
                    Tier = "warn",
                    BadgeText = bestRank == 1 ? "⚠ USB 1.1 · 12 Mbps" : "⚠ USB 1.0 · 1.5 Mbps",
                    DetailText = Loc.T("conn_usb1_warn"),
                    TooltipText = Loc.T("conn_usb1_warn")
                };
            }

            return MakeFallbackUsb();
        }

        private static ConnectionLinkInfo MakeFallbackUsb()
        {
            return new ConnectionLinkInfo
            {
                IsWireless = false,
                Tier = "mid",
                BadgeText = "🔌 USB",
                DetailText = Loc.T("conn_usb_generic"),
                TooltipText = Loc.T("conn_usb_generic")
            };
        }

        private static async Task<ConnectionLinkInfo> ProbeWifiAsync(AdbService adb, string serial)
        {
            if (adb == null || !adb.IsValid)
            {
                return new ConnectionLinkInfo
                {
                    IsWireless = true,
                    Tier = "mid",
                    BadgeText = "📶 Wi-Fi ADB",
                    DetailText = Loc.T("conn_wifi_generic"),
                    TooltipText = Loc.T("conn_wifi_generic")
                };
            }

            try
            {
                var res = await adb.RunAsync(adb.AdbPath, string.Format("-s \"{0}\" shell \"cmd wifi status 2>/dev/null; dumpsys wifi | grep -m 1 'Link speed'\"", serial), 2500);
                string txt = (res.StdOut ?? "") + "\n" + (res.StdErr ?? "");
                var mSpeed = Regex.Match(txt, @"(?:Link speed|Tx Link speed):\s*(\d+)\s*Mbps", RegexOptions.IgnoreCase);
                var mFreq = Regex.Match(txt, @"Frequency:\s*(\d+)\s*MHz", RegexOptions.IgnoreCase);

                int speed = mSpeed.Success ? int.Parse(mSpeed.Groups[1].Value) : 0;
                int freq = mFreq.Success ? int.Parse(mFreq.Groups[1].Value) : 0;
                string band = freq >= 5900 ? "6 GHz" : (freq >= 4900 ? "5 GHz" : (freq >= 2400 ? "2.4 GHz" : ""));

                if (speed > 0)
                {
                    bool isFast = (freq >= 4900 || speed >= 300);
                    string badge = string.IsNullOrEmpty(band)
                        ? string.Format("📶 Wi-Fi · {0} Mbps", speed)
                        : string.Format("📶 Wi-Fi {0} · {1} Mbps", band, speed);
                    return new ConnectionLinkInfo
                    {
                        IsWireless = true,
                        Tier = isFast ? "fast" : "mid",
                        BadgeText = badge,
                        DetailText = isFast ? Loc.T("conn_wifi_fast") : Loc.T("conn_wifi_slow"),
                        TooltipText = isFast ? Loc.T("conn_wifi_fast") : Loc.T("conn_wifi_slow")
                    };
                }
            }
            catch { }

            return new ConnectionLinkInfo
            {
                IsWireless = true,
                Tier = "mid",
                BadgeText = "📶 Wi-Fi ADB",
                DetailText = Loc.T("conn_wifi_generic"),
                TooltipText = Loc.T("conn_wifi_generic")
            };
        }

        public static async Task<BatteryLinkInfo> ProbeBatteryAsync(AdbService adb, DeviceInfo dev)
        {
            if (adb == null || !adb.IsValid || dev == null || dev.State != "device")
                return null;

            try
            {
                var res = await adb.RunAsync(adb.AdbPath, string.Format("-s \"{0}\" shell \"dumpsys battery; echo ===THERM===; dumpsys thermalservice 2>/dev/null\"", dev.Serial), 3000);
                return ParseBatteryAndThermal(res.StdOut ?? "");
            }
            catch
            {
                return null;
            }
        }

        public static BatteryLinkInfo ParseBatteryAndThermal(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            string battTxt = raw;
            string thermTxt = "";
            int splitIdx = raw.IndexOf("===THERM===", StringComparison.Ordinal);
            if (splitIdx >= 0)
            {
                battTxt = raw.Substring(0, splitIdx);
                thermTxt = raw.Substring(splitIdx + 11);
            }

            var mLevel = Regex.Match(battTxt, @"^\s*level:\s*(\d+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            var mTemp = Regex.Match(battTxt, @"^\s*temperature:\s*(-?\d+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (!mLevel.Success && !mTemp.Success) return null;

            int level = mLevel.Success ? int.Parse(mLevel.Groups[1].Value) : -1;
            double tempC = mTemp.Success ? (int.Parse(mTemp.Groups[1].Value) / 10.0) : -1.0;

            bool ac = Regex.IsMatch(battTxt, @"AC powered:\s*true", RegexOptions.IgnoreCase);
            bool usb = Regex.IsMatch(battTxt, @"USB powered:\s*true", RegexOptions.IgnoreCase);
            bool wl = Regex.IsMatch(battTxt, @"Wireless powered:\s*true", RegexOptions.IgnoreCase);
            bool stChg = Regex.IsMatch(battTxt, @"^\s*status:\s*(2|5)\b", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            bool charging = ac || usb || wl || stChg;

            // Parse live AP (mType=0) & SKIN (mType=3) temperatures and Android thermal throttling status
            double apTempC = -1.0;
            double skinTempC = -1.0;
            int thermalStatus = 0;

            if (!string.IsNullOrEmpty(thermTxt))
            {
                // Android 10-12: "Current Status: N", Android 13-16 (AIDL): "Thermal Status: N"
                var mCurSt = Regex.Match(thermTxt, @"^\s*(?:Thermal|Current)\s+Status\s*:\s*(\d+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                if (mCurSt.Success)
                {
                    int cs;
                    if (int.TryParse(mCurSt.Groups[1].Value, out cs) && cs > thermalStatus)
                        thermalStatus = cs;
                }

                // Prefer live "Current temperatures from HAL:" over stale "Cached temperatures:"
                // ("Cached temperatures:" only updates on threshold callbacks and freezes AP at old peaks!)
                string sensorSection = thermTxt;
                var mHalSec = Regex.Match(thermTxt, @"Current\s+temperatures\s+from\s+HAL\s*:", RegexOptions.IgnoreCase);
                if (mHalSec.Success)
                {
                    string afterHal = thermTxt.Substring(mHalSec.Index + mHalSec.Length);
                    var mNextSec = Regex.Match(afterHal, @"(?:Current\s+cooling\s+devices|Temperature\s+static\s+thresholds|Temperature\s+headroom)", RegexOptions.IgnoreCase);
                    string halBlock = mNextSec.Success ? afterHal.Substring(0, mNextSec.Index) : afterHal;
                    if (halBlock.IndexOf("Temperature{", StringComparison.Ordinal) >= 0)
                        sensorSection = halBlock;
                }

                var tMatches = Regex.Matches(sensorSection, @"Temperature\{mValue=([0-9.\-]+),\s*mType=(\d+),\s*mName=([^,]+),\s*mStatus=(\d+)\}");
                foreach (Match tm in tMatches)
                {
                    double val;
                    int mtype, mst;
                    if (!double.TryParse(tm.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out val))
                        continue;
                    if (!int.TryParse(tm.Groups[2].Value, out mtype)) continue;
                    if (!int.TryParse(tm.Groups[4].Value, out mst)) mst = 0;

                    if (val < 10.0 || val > 115.0) continue; // Ignore bogus/uncalibrated sensors
                    if (mtype == 0 && val > apTempC) apTempC = val;
                    if (mtype == 3 && val > skinTempC) skinTempC = val;
                    // Track throttling severity from CPU(0), GPU(1), BAT(2), SKIN(3), NPU(9)
                    if ((mtype == 0 || mtype == 1 || mtype == 2 || mtype == 3 || mtype == 9) && mst > thermalStatus)
                        thermalStatus = mst;
                }
            }

            bool severeThrottle = (thermalStatus >= 3) || (tempC >= 43.0) || (skinTempC >= 44.0);
            bool moderateThrottle = !severeThrottle && ((thermalStatus == 2) || (tempC >= 39.0) || (skinTempC >= 41.0));
            string tier = severeThrottle ? "hot" : (moderateThrottle ? "warm" : "cool");

            string icon = severeThrottle ? "🔥" : (charging ? "⚡" : "🔋");

            // When throttling is triggered by SKIN/SoC heat while battery is cooler (e.g. foldables), show the hotter SKIN/AP sensor on the badge
            string tempDisplay = tempC > 0 ? string.Format("{0:0.0}°C", tempC) : "";
            if ((severeThrottle || moderateThrottle) && skinTempC > 0 && skinTempC > tempC + 1.5)
                tempDisplay = string.Format("SKIN {0:0.0}°C", skinTempC);
            else if ((severeThrottle || moderateThrottle) && skinTempC <= 0 && apTempC >= 55.0 && tempC < 40.0)
                tempDisplay = string.Format("AP {0:0.0}°C", apTempC);

            string badgeBase = (level >= 0 && !string.IsNullOrEmpty(tempDisplay))
                ? string.Format("{0} {1}% · {2}", icon, level, tempDisplay)
                : (level >= 0 ? string.Format("{0} {1}%", icon, level) : string.Format("🌡 {0}", tempDisplay));

            string badge = severeThrottle
                ? (badgeBase + " · " + Loc.T("batt_throttle_hot"))
                : (moderateThrottle ? (badgeBase + " · " + Loc.T("batt_throttle_warm")) : badgeBase);

            string stateTxt = charging ? Loc.T("batt_charging") : Loc.T("batt_discharging");
            string tipKey = tier == "hot" ? "batt_tip_hot" : (tier == "warm" ? "batt_tip_warm" : "batt_tip_cool");
            string tip = Loc.F(tipKey, level >= 0 ? level.ToString() : "?", tempC > 0 ? tempC.ToString("0.0") : "?", stateTxt);

            if (apTempC > 0 || skinTempC > 0 || thermalStatus > 0)
            {
                string apStr = apTempC > 0 ? (apTempC.ToString("0.0") + "°C") : "-";
                string skinStr = skinTempC > 0 ? (skinTempC.ToString("0.0") + "°C") : "-";
                tip += "\n" + Loc.F("batt_therm_detail", apStr, skinStr, thermalStatus);
            }

            return new BatteryLinkInfo
            {
                Level = level,
                TempC = tempC,
                ApTempC = apTempC,
                SkinTempC = skinTempC,
                ThermalStatus = thermalStatus,
                IsCharging = charging,
                Tier = tier,
                BadgeText = badge,
                TooltipText = tip
            };
        }
    }
}
