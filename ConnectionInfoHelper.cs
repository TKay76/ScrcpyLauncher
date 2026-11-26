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
                                            if ((v2Out.Flags & 4) != 0) isSsp = true;
                                            if ((v2Out.Flags & 2) != 0 || (v2Out.Flags & 8) != 0) isSsCapable = true;
                                        }
                                    }
                                    finally { Marshal.FreeHGlobal(pV2); }

                                    if (isSsp)
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
                                    if (ex.Speed == 3)
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
                                    if (ex.Speed == 2)
                                    {
                                        return new ConnectionLinkInfo
                                        {
                                            IsWireless = false,
                                            Tier = "mid",
                                            BadgeText = "🔌 USB 2.0 · 480 Mbps",
                                            DetailText = isSsCapable ? Loc.T("conn_usb2_upgrade") : Loc.T("conn_usb2_ok"),
                                            TooltipText = isSsCapable ? Loc.T("conn_usb2_upgrade_tip") : Loc.T("conn_usb2_tip")
                                        };
                                    }

                                    return new ConnectionLinkInfo
                                    {
                                        IsWireless = false,
                                        Tier = "warn",
                                        BadgeText = ex.Speed == 1 ? "⚠ USB 1.1 · 12 Mbps" : "⚠ USB 1.0 · 1.5 Mbps",
                                        DetailText = Loc.T("conn_usb1_warn"),
                                        TooltipText = Loc.T("conn_usb1_warn")
                                    };
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
                var res = await adb.RunAsync(adb.AdbPath, string.Format("-s {0} shell \"dumpsys wifi | grep -m 1 'Link speed'\"", serial), 2500);
                string txt = (res.StdOut ?? "") + "\n" + (res.StdErr ?? "");
                var mSpeed = Regex.Match(txt, @"Link speed:\s*(\d+)\s*Mbps", RegexOptions.IgnoreCase);
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
                var res = await adb.RunAsync(adb.AdbPath, string.Format("-s {0} shell dumpsys battery", dev.Serial), 2500);
                string txt = res.StdOut ?? "";
                if (string.IsNullOrWhiteSpace(txt)) return null;

                var mLevel = Regex.Match(txt, @"^\s*level:\s*(\d+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                var mTemp = Regex.Match(txt, @"^\s*temperature:\s*(-?\d+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                if (!mLevel.Success && !mTemp.Success) return null;

                int level = mLevel.Success ? int.Parse(mLevel.Groups[1].Value) : -1;
                double tempC = mTemp.Success ? (int.Parse(mTemp.Groups[1].Value) / 10.0) : -1.0;

                bool ac = Regex.IsMatch(txt, @"AC powered:\s*true", RegexOptions.IgnoreCase);
                bool usb = Regex.IsMatch(txt, @"USB powered:\s*true", RegexOptions.IgnoreCase);
                bool wl = Regex.IsMatch(txt, @"Wireless powered:\s*true", RegexOptions.IgnoreCase);
                bool stChg = Regex.IsMatch(txt, @"^\s*status:\s*(2|5)\b", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                bool charging = ac || usb || wl || stChg;

                string tier = tempC >= 43.0 ? "hot" : (tempC >= 39.0 ? "warm" : "cool");
                string icon = charging ? "⚡" : "🔋";
                string badge = (level >= 0 && tempC > 0)
                    ? string.Format("{0} {1}% · {2:0.0}°C", icon, level, tempC)
                    : (level >= 0 ? string.Format("{0} {1}%", icon, level) : string.Format("🌡 {0:0.0}°C", tempC));

                string stateTxt = charging ? Loc.T("batt_charging") : Loc.T("batt_discharging");
                string tipKey = tier == "hot" ? "batt_tip_hot" : (tier == "warm" ? "batt_tip_warm" : "batt_tip_cool");
                string tip = Loc.F(tipKey, level >= 0 ? level.ToString() : "?", tempC > 0 ? tempC.ToString("0.0") : "?", stateTxt);

                return new BatteryLinkInfo
                {
                    Level = level,
                    TempC = tempC,
                    IsCharging = charging,
                    Tier = tier,
                    BadgeText = badge,
                    TooltipText = tip
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
