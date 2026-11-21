using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ScrcpyLauncher
{
    public class InputHookEngine : IDisposable
    {
        private readonly ScrcpyControlClient _client;
        private readonly object _stateLock = new object();
        private KeymapProfile _profile;
        private IntPtr _targetHwnd = IntPtr.Zero;

        private IntPtr _kbHook = IntPtr.Zero;
        private IntPtr _mouseHook = IntPtr.Zero;
        private LowLevelProc _kbProc;
        private LowLevelProc _mouseProc;
        private System.Threading.Timer _tickTimer;

        private bool _active = false;
        private bool _isPaused = false;
        private long _nextTouchId = 1;

        // Camera state (flushed smoothly on 10ms EngineTick — no SetCursorPos inside WH_MOUSE_LL!)
        private bool _cameraLocked = false;
        private long _camTouchId = 0;
        private double _camCurX = 6060;
        private double _camCurY = 2780;
        private int _camAccumDx = 0;
        private int _camAccumDy = 0;
        private DateTime _lastCamMotionUtc = DateTime.MinValue;
        private int _centerScreenX = 0;
        private int _centerScreenY = 0;
        private int _videoWidth = 0;
        private int _videoHeight = 0;

        public int CenterScreenX { get { return _centerScreenX; } }
        public int CenterScreenY { get { return _centerScreenY; } }

        public void SetVideoSize(int width, int height)
        {
            if (width > 0 && height > 0)
            {
                _videoWidth = width;
                _videoHeight = height;
            }
        }

        // Manual cursor touch when camera lock is OFF (routes through our SAME control socket to avoid 2-server MotionEvent conflicts!)
        private long _manualTouchId = 0;
        private int _manualLastX = 5000;
        private int _manualLastY = 5000;

        // Joystick state (50ms smooth interpolation)
        private bool _joyUp, _joyDown, _joyLeft, _joyRight, _joySprint;
        private long _joyTouchId = 0;
        private double _joyCurX, _joyCurY;
        private double _joyFromX, _joyFromY;
        private double _joyTargetX, _joyTargetY;
        private double _joyFirstTargetX, _joyFirstTargetY;
        private bool _joyMoving = false;
        private bool _joyHasMoved = false;
        private DateTime _joyMoveStartedUtc = DateTime.MinValue;
        private DateTime _joyLastKeepAliveUtc = DateTime.MinValue;
        private int _joyKeepAliveToggle = 0;
        private const double JoySmoothMs = 50.0;

        // Action keys currently held down (bind -> pointerId)
        private readonly Dictionary<KeyBindItem, long> _heldKeys = new Dictionary<KeyBindItem, long>();
        private long _leftTouchId = 0;
        private long _rightTouchId = 0;
        private bool _camToggleHeld = false;
        private bool _camToggleUsedAsModifier = false;

        // Wheel-drag selection key state (e.g. Tab part targeting, G item/ammo wheel)
        private long _wheelTouchId = 0;
        private int _wheelVk = 0;
        private double _wheelOriginX, _wheelOriginY;
        private double _wheelCurX, _wheelCurY;
        private int _wheelRadius = 1500;
        private int _wheelAccumDx = 0;
        private int _wheelAccumDy = 0;

        public bool IsWheelDragging { get { return _wheelTouchId != 0; } }

        public event Action<bool> CameraLockChanged;
        public event Action<string> StatusMessage;
        public event Action HintToggleRequested;
        public event Action<int> ProfileSwitchRequested;
        public event Action<bool> PauseToggled;

        public bool IsPaused { get { return _isPaused; } }

        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP = 0x0208;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int WM_XBUTTONUP = 0x020C;

        private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern bool ClipCursor(ref RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClipCursor(IntPtr lpRect);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private static int GetCurrentModifiers()
        {
            int m = 0;
            if ((GetAsyncKeyState(17) & 0x8000) != 0) m |= 1; // Ctrl
            if ((GetAsyncKeyState(16) & 0x8000) != 0) m |= 2; // Shift
            if ((GetAsyncKeyState(18) & 0x8000) != 0) m |= 4; // Alt
            return m;
        }

        private KeyBindItem FindMatchingBind(int vk, int curMods)
        {
            if (_profile == null || _profile.Keys == null) return null;
            // 1. Exact combo match takes priority (e.g. Alt+1, Ctrl+Q, Shift+E)
            if (curMods != 0)
            {
                for (int i = 0; i < _profile.Keys.Count; i++)
                {
                    var item = _profile.Keys[i];
                    if (item.Modifiers != 0 && item.Modifiers == curMods && NormalizeVk(item.VkCode) == vk)
                        return item;
                }
            }
            // If Alt is held and no explicit Alt+Key combo matched, do NOT fall back to plain keys
            // so Windows (Alt+Tab, Alt+F4) and scrcpy (Alt+F, Alt+O, Alt+I) shortcuts work natively!
            if ((curMods & 4) != 0)
                return null;

            // 2. Fallback to plain key match (Modifiers == 0) — works during Shift sprint too!
            for (int i = 0; i < _profile.Keys.Count; i++)
            {
                var item = _profile.Keys[i];
                if (item.Modifiers == 0 && NormalizeVk(item.VkCode) == vk)
                    return item;
            }
            return null;
        }

        public bool IsActive { get { return _active; } }
        public bool IsCameraLocked { get { return _cameraLocked; } }

        public InputHookEngine(ScrcpyControlClient client)
        {
            _client = client;
            _kbProc = KeyboardHookCallback;
            _mouseProc = MouseHookCallback;
        }

        public void SetProfile(KeymapProfile profile)
        {
            ReleaseAllInputs();
            _profile = profile;
        }

        public void SetTargetWindow(IntPtr hwnd)
        {
            _targetHwnd = hwnd;
        }

        public void Start()
        {
            if (_active) return;
            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                IntPtr hMod = GetModuleHandle(curModule.ModuleName);
                _kbHook = SetWindowsHookEx(WH_KEYBOARD_LL, _kbProc, hMod, 0);
                _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hMod, 0);
            }
            _active = true;
            _tickTimer = new System.Threading.Timer(EngineTick, null, 8, 8);
        }

        public void Stop()
        {
            if (!_active) return;
            _active = false;
            _isPaused = false;
            if (_tickTimer != null)
            {
                _tickTimer.Dispose();
                _tickTimer = null;
            }
            SetCameraLock(false);
            ReleaseAllInputs();

            if (_kbHook != IntPtr.Zero) { UnhookWindowsHookEx(_kbHook); _kbHook = IntPtr.Zero; }
            if (_mouseHook != IntPtr.Zero) { UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
        }

        private void EngineTick(object state)
        {
            if (!_active || _profile == null || !_client.IsConnected) return;

            DateTime now = DateTime.UtcNow;
            lock (_stateLock)
            {
                // 1. Flush wheel-drag mouse delta (takes priority over camera while wheel key is held!)
                if (_wheelTouchId != 0 && (_wheelAccumDx != 0 || _wheelAccumDy != 0))
                {
                    int wdx = _wheelAccumDx;
                    int wdy = _wheelAccumDy;
                    _wheelAccumDx = 0;
                    _wheelAccumDy = 0;

                    double nx = _wheelCurX + wdx * 12.0;
                    double ny = _wheelCurY + wdy * 18.0;
                    double offX = nx - _wheelOriginX;
                    double offY = (ny - _wheelOriginY) / 1.778;
                    double dist = Math.Sqrt(offX * offX + offY * offY);
                    if (dist > _wheelRadius && dist > 0.001)
                    {
                        double ratio = _wheelRadius / dist;
                        nx = _wheelOriginX + offX * ratio;
                        ny = _wheelOriginY + offY * ratio * 1.778;
                    }
                    _wheelCurX = Math.Max(100, Math.Min(9900, nx));
                    _wheelCurY = Math.Max(100, Math.Min(9900, ny));
                    _client.InjectTouch(2, _wheelTouchId, (int)Math.Round(_wheelCurX), (int)Math.Round(_wheelCurY));
                }

                // 2. Flush accumulated mouse camera delta at smooth ~125Hz (paused while wheel-dragging)
                if (_cameraLocked && _wheelTouchId == 0 && (_camAccumDx != 0 || _camAccumDy != 0))
                {
                    int adx = _camAccumDx;
                    int ady = _camAccumDy;
                    _camAccumDx = 0;
                    _camAccumDy = 0;
                    FlushCameraDeltaLocked(adx, ady, now);
                }

                // 2. Smooth 50ms joystick interpolation + 30Hz keep-alive while held (fixes solo WASD in normal mode!)
                if (_joyTouchId != 0)
                {
                    if (_joyMoving)
                    {
                        double elapsed = (now - _joyMoveStartedUtc).TotalMilliseconds;
                        if (elapsed > 0)
                        {
                            double progress = Math.Min(1.0, elapsed / JoySmoothMs);
                            double x = progress >= 1.0 ? _joyTargetX : _joyFromX + (_joyTargetX - _joyFromX) * progress;
                            double y = progress >= 1.0 ? _joyTargetY : _joyFromY + (_joyTargetY - _joyFromY) * progress;
                            if (progress >= 1.0) _joyMoving = false;

                            if (Math.Abs(_joyCurX - x) >= 0.5 || Math.Abs(_joyCurY - y) >= 0.5)
                            {
                                _joyCurX = x;
                                _joyCurY = y;
                                _joyHasMoved = true;
                                _joyLastKeepAliveUtc = now;
                                _client.InjectTouch(2, _joyTouchId, (int)Math.Round(x), (int)Math.Round(y));
                            }
                        }
                    }
                    else if ((now - _joyLastKeepAliveUtc).TotalMilliseconds >= 32.0)
                    {
                        _joyLastKeepAliveUtc = now;
                        _joyKeepAliveToggle ^= 1;
                        int kx = (int)Math.Round(_joyCurX) + _joyKeepAliveToggle;
                        int ky = (int)Math.Round(_joyCurY);
                        _client.InjectTouch(2, _joyTouchId, kx, ky);
                    }
                }

                // 3. Auto-release camera finger after 500ms of mouse idle
                if (_camTouchId != 0 && (now - _lastCamMotionUtc).TotalMilliseconds >= 500.0)
                {
                    StopCameraFingerLocked();
                }
            }
        }

        public void CheckFocusSafety()
        {
            if (!_active) return;
            if (_targetHwnd == IntPtr.Zero || GetForegroundWindow() != _targetHwnd)
            {
                if (_cameraLocked) SetCameraLock(false);
                bool anyHeld;
                lock (_stateLock)
                {
                    anyHeld = _joyTouchId != 0 || _camTouchId != 0 || _wheelTouchId != 0 || _heldKeys.Count > 0 || _leftTouchId != 0 || _rightTouchId != 0 || _manualTouchId != 0;
                }
                if (anyHeld) ReleaseAllInputs();
            }
        }

        private void SetCameraLock(bool locked)
        {
            if (_cameraLocked == locked) return;
            _cameraLocked = locked;

            if (locked && _targetHwnd != IntPtr.Zero)
            {
                RECT rc;
                if (GetWindowRect(_targetHwnd, out rc))
                {
                    _centerScreenX = (rc.Left + rc.Right) / 2;
                    _centerScreenY = (rc.Top + rc.Bottom) / 2;
                    SetCursorPos(_centerScreenX, _centerScreenY);
                    ClipCursor(ref rc);
                }
                lock (_stateLock)
                {
                    if (_manualTouchId != 0)
                    {
                        _client.InjectTouch(1, _manualTouchId, _manualLastX, _manualLastY);
                        _manualTouchId = 0;
                    }
                    _camCurX = _profile != null ? _profile.CameraX : 6060;
                    _camCurY = _profile != null ? _profile.CameraY : 2780;
                    _camAccumDx = 0;
                    _camAccumDy = 0;
                    StopCameraFingerLocked();

                if (_wheelTouchId != 0)
                {
                    _client.InjectTouch(1, _wheelTouchId, (int)Math.Round(_wheelCurX), (int)Math.Round(_wheelCurY));
                    _wheelTouchId = 0;
                    _wheelVk = 0;
                    _wheelAccumDx = 0;
                    _wheelAccumDy = 0;
                }
                }
            }
            else
            {
                ClipCursor(IntPtr.Zero);
                lock (_stateLock)
                {
                    _camAccumDx = 0;
                    _camAccumDy = 0;
                    StopCameraFingerLocked();

                if (_wheelTouchId != 0)
                {
                    _client.InjectTouch(1, _wheelTouchId, (int)Math.Round(_wheelCurX), (int)Math.Round(_wheelCurY));
                    _wheelTouchId = 0;
                    _wheelVk = 0;
                    _wheelAccumDx = 0;
                    _wheelAccumDy = 0;
                }
                    if (_leftTouchId != 0 && _profile != null)
                    {
                        _client.InjectTouch(1, _leftTouchId, _profile.LeftClickX, _profile.LeftClickY);
                        _leftTouchId = 0;
                    }
                    if (_rightTouchId != 0 && _profile != null)
                    {
                        _client.InjectTouch(1, _rightTouchId, _profile.RightClickX, _profile.RightClickY);
                        _rightTouchId = 0;
                    }
                }
            }

            if (CameraLockChanged != null) CameraLockChanged(_cameraLocked);
            if (StatusMessage != null)
                StatusMessage(_cameraLocked ? "[키 매핑] 시점 회전 모드 ON (커서 잠금)" : "[키 매핑] 시점 회전 모드 OFF (커서 해제)");
        }

        private void StopCameraFingerLocked()
        {
            if (_camTouchId != 0)
            {
                _client.InjectTouch(1, _camTouchId, (int)Math.Round(_camCurX), (int)Math.Round(_camCurY));
                _camTouchId = 0;
            }
        }

        private void ReleaseAllInputs()
        {
            lock (_stateLock)
            {
                if (_joyTouchId != 0)
                {
                    _client.InjectTouch(1, _joyTouchId, (int)Math.Round(_joyCurX), (int)Math.Round(_joyCurY));
                    _joyTouchId = 0;
                }
                _joyMoving = false;
                _joyHasMoved = false;
                _joyUp = _joyDown = _joyLeft = _joyRight = _joySprint = false;

                StopCameraFingerLocked();

                if (_wheelTouchId != 0)
                {
                    _client.InjectTouch(1, _wheelTouchId, (int)Math.Round(_wheelCurX), (int)Math.Round(_wheelCurY));
                    _wheelTouchId = 0;
                    _wheelVk = 0;
                    _wheelAccumDx = 0;
                    _wheelAccumDy = 0;
                }

                if (_manualTouchId != 0)
                {
                    _client.InjectTouch(1, _manualTouchId, _manualLastX, _manualLastY);
                    _manualTouchId = 0;
                }

                foreach (var kv in _heldKeys)
                {
                    _client.InjectTouch(1, kv.Value, kv.Key.X, kv.Key.Y);
                }
                _heldKeys.Clear();
                _camToggleHeld = false;
                _camToggleUsedAsModifier = false;

                if (_leftTouchId != 0 && _profile != null)
                {
                    _client.InjectTouch(1, _leftTouchId, _profile.LeftClickX, _profile.LeftClickY);
                    _leftTouchId = 0;
                }
                if (_rightTouchId != 0 && _profile != null)
                {
                    _client.InjectTouch(1, _rightTouchId, _profile.RightClickX, _profile.RightClickY);
                    _rightTouchId = 0;
                }
            }
        }

        private static int NormalizeVk(int vk)
        {
            if (vk == 160 || vk == 161) return 16;
            if (vk == 162 || vk == 163) return 17;
            if (vk == 164 || vk == 165) return 18;
            return vk;
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _active && _profile != null && _client.IsConnected && _targetHwnd != IntPtr.Zero)
            {
                if (GetForegroundWindow() == _targetHwnd)
                {
                    int msg = wParam.ToInt32();
                    bool isDown = (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN);
                    bool isUp = (msg == WM_KEYUP || msg == WM_SYSKEYUP);

                    if (isDown || isUp)
                    {
                        var kb = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                        int vk = NormalizeVk((int)kb.vkCode);

                        if (vk == 91 || vk == 92)
                            return CallNextHookEx(_kbHook, nCode, wParam, lParam);

                        int curMods = GetCurrentModifiers();

                        // Ctrl + F1..F9 switches keymap profiles instantly (0..8)
                        if (vk >= 112 && vk <= 120 && (curMods & 1) != 0)
                        {
                            if (isUp && ProfileSwitchRequested != null)
                                ProfileSwitchRequested(vk - 112);
                            return (IntPtr)1;
                        }

                        // F2 (without modifiers) toggles Chat / Typing pause mode (lets Korean/English keyboard type into game!)
                        if (vk == 113 && curMods == 0)
                        {
                            if (isUp)
                            {
                                _isPaused = !_isPaused;
                                if (_isPaused)
                                {
                                    SetCameraLock(false);
                                    ReleaseAllInputs();
                                }
                                if (PauseToggled != null) PauseToggled(_isPaused);
                            }
                            return (IntPtr)1;
                        }

                        // While paused (Chat / Typing mode), let all keyboard keys pass straight through to Android UHID keyboard!
                        if (_isPaused)
                            return CallNextHookEx(_kbHook, nCode, wParam, lParam);

                        // F1 (without Ctrl) toggles on-screen key hints mid-game
                        if (vk == 112 && curMods == 0)
                        {
                            if (isUp && HintToggleRequested != null) HintToggleRequested();
                            return (IntPtr)1;
                        }

                        // Camera toggle key (e.g. Alt): pass Alt through to Windows/scrcpy so Alt+Tab and Alt+F/O/I work natively!
                        // Toggle camera lock ONLY when Alt is tapped and released alone without pressing another key.
                        if (_profile.EnableMouseCamera && vk == NormalizeVk(_profile.CameraToggleVk))
                        {
                            if (isDown)
                            {
                                if (!_camToggleHeld)
                                {
                                    _camToggleHeld = true;
                                    _camToggleUsedAsModifier = false;
                                }
                            }
                            else if (isUp)
                            {
                                bool usedAsMod = _camToggleUsedAsModifier;
                                _camToggleHeld = false;
                                _camToggleUsedAsModifier = false;
                                if (!usedAsMod) SetCameraLock(!_cameraLocked);
                            }
                            return (vk == 18) ? CallNextHookEx(_kbHook, nCode, wParam, lParam) : (IntPtr)1;
                        }

                        // Native Windows Alt+Tab: release camera lock & held touches, then pass through to OS!
                        if (vk == 9 && (curMods & 4) != 0 && FindMatchingBind(vk, curMods) == null)
                        {
                            if (isDown)
                            {
                                SetCameraLock(false);
                                ReleaseAllInputs();
                                _camToggleUsedAsModifier = true;
                            }
                            return CallNextHookEx(_kbHook, nCode, wParam, lParam);
                        }

                        if (_profile.EnableJoystick)
                        {
                            bool joyHit = false;
                            bool changed = false;
                            lock (_stateLock)
                            {
                                if (vk == NormalizeVk(_profile.JoyUpVk)) { changed = (_joyUp != isDown); _joyUp = isDown; joyHit = true; }
                                else if (vk == NormalizeVk(_profile.JoyDownVk)) { changed = (_joyDown != isDown); _joyDown = isDown; joyHit = true; }
                                else if (vk == NormalizeVk(_profile.JoyLeftVk)) { changed = (_joyLeft != isDown); _joyLeft = isDown; joyHit = true; }
                                else if (vk == NormalizeVk(_profile.JoyRightVk)) { changed = (_joyRight != isDown); _joyRight = isDown; joyHit = true; }
                                else if (_profile.SprintVk > 0 && vk == NormalizeVk(_profile.SprintVk)) { changed = (_joySprint != isDown); _joySprint = isDown; joyHit = true; }

                                if (joyHit && changed)
                                    UpdateJoystickLocked(isUp);
                            }
                            if (joyHit) return (IntPtr)1;
                        }

                        // On key UP, release any held action/wheel bind whose main VkCode matches (even if modifier was released first)
                        if (isUp)
                        {
                            bool releasedAny = false;
                            lock (_stateLock)
                            {
                                if (_wheelTouchId != 0 && NormalizeVk(_wheelVk) == vk)
                                {
                                    _client.InjectTouch(1, _wheelTouchId, (int)Math.Round(_wheelCurX), (int)Math.Round(_wheelCurY));
                                    _wheelTouchId = 0;
                                    _wheelVk = 0;
                                    _wheelAccumDx = 0;
                                    _wheelAccumDy = 0;
                                    releasedAny = true;
                                }

                                List<KeyBindItem> toRelease = null;
                                foreach (var kv in _heldKeys)
                                {
                                    if (NormalizeVk(kv.Key.VkCode) == vk)
                                    {
                                        if (toRelease == null) toRelease = new List<KeyBindItem>();
                                        toRelease.Add(kv.Key);
                                    }
                                }
                                if (toRelease != null)
                                {
                                    for (int r = 0; r < toRelease.Count; r++)
                                    {
                                        var relItem = toRelease[r];
                                        long relId = _heldKeys[relItem];
                                        _heldKeys.Remove(relItem);
                                        _client.InjectTouch(1, relId, relItem.X, relItem.Y);
                                        releasedAny = true;
                                    }
                                }
                            }
                            if (releasedAny) return (IntPtr)1;
                        }

                        // On key DOWN, match combo first (Ctrl/Shift/Alt+Key), then plain key
                        var matched = FindMatchingBind(vk, curMods);
                        if (matched != null)
                        {
                            if (isDown)
                            {
                                if ((matched.Modifiers & 4) != 0 && NormalizeVk(_profile.CameraToggleVk) == 18)
                                    _camToggleUsedAsModifier = true;

                                lock (_stateLock)
                                {
                                    if (matched.IsWheelDrag)
                                    {
                                        if (_wheelTouchId == 0)
                                        {
                                            StopCameraFingerLocked();
                                            _camAccumDx = 0;
                                            _camAccumDy = 0;
                                            _wheelVk = matched.VkCode;
                                            _wheelOriginX = matched.X;
                                            _wheelOriginY = matched.Y;
                                            _wheelCurX = matched.X;
                                            _wheelCurY = matched.Y;
                                            _wheelRadius = matched.WheelRadius > 200 ? matched.WheelRadius : 1500;
                                            _wheelAccumDx = 0;
                                            _wheelAccumDy = 0;
                                            _wheelTouchId = _nextTouchId++;
                                            _client.InjectTouch(0, _wheelTouchId, matched.X, matched.Y);
                                        }
                                    }
                                    else
                                    {
                                        if (!_heldKeys.ContainsKey(matched))
                                        {
                                            long id = _nextTouchId++;
                                            _heldKeys[matched] = id;
                                            _client.InjectTouch(0, id, matched.X, matched.Y);
                                        }
                                    }
                                }
                            }
                            return (IntPtr)1;
                        }

                        // Any other key pressed while Alt is held (e.g. Alt+F, Alt+O, Alt+I, Alt+F4):
                        // mark Alt as used so releasing Alt does NOT toggle camera lock, then pass through to scrcpy/Windows!
                        if ((curMods & 4) != 0 && isDown)
                            _camToggleUsedAsModifier = true;
                    }
                }
            }
            return CallNextHookEx(_kbHook, nCode, wParam, lParam);
        }

        private void UpdateJoystickLocked(bool keyReleased)
        {
            int dx = (_joyRight ? 1 : 0) - (_joyLeft ? 1 : 0);
            int dy = (_joyDown ? 1 : 0) - (_joyUp ? 1 : 0);

            if (dx == 0 && dy == 0)
            {
                long oldId = _joyTouchId;
                _joyTouchId = 0;
                _joyMoving = false;
                if (oldId != 0)
                {
                    bool anyHeld = _joyUp || _joyDown || _joyLeft || _joyRight;
                    if (keyReleased && !anyHeld && !_joyHasMoved)
                    {
                        _joyCurX = _joyFirstTargetX;
                        _joyCurY = _joyFirstTargetY;
                        _client.InjectTouch(2, oldId, (int)Math.Round(_joyCurX), (int)Math.Round(_joyCurY));
                    }
                    _client.InjectTouch(1, oldId, (int)Math.Round(_joyCurX), (int)Math.Round(_joyCurY));
                }
                return;
            }

            double len = Math.Sqrt(dx * dx + dy * dy);
            double scale = _joySprint && _profile.SprintScale > 1.0 ? _profile.SprintScale : 1.0;
            double rad = _profile.JoyRadius * scale;

            double tx = Math.Max(100, Math.Min(9900, _profile.JoyX + (dx / len) * rad));
            double ty = Math.Max(100, Math.Min(9900, _profile.JoyY + (dy / len) * rad * 1.778));

            bool starting = (_joyTouchId == 0);
            if (starting)
            {
                _joyTouchId = _nextTouchId++;
                _joyCurX = _profile.JoyX;
                _joyCurY = _profile.JoyY;
                _joyHasMoved = false;
                _joyFirstTargetX = tx;
                _joyFirstTargetY = ty;
                _client.InjectTouch(0, _joyTouchId, (int)Math.Round(_joyCurX), (int)Math.Round(_joyCurY));
            }

            if (!starting && Math.Abs(_joyTargetX - tx) < 1 && Math.Abs(_joyTargetY - ty) < 1)
                return;

            _joyFromX = _joyCurX;
            _joyFromY = _joyCurY;
            _joyTargetX = tx;
            _joyTargetY = ty;
            _joyMoveStartedUtc = DateTime.UtcNow;
            _joyMoving = true;
        }

        private bool TryScreenToClientNorm(int screenX, int screenY, out int normX, out int normY, bool clampOutside = false)
        {
            normX = 5000; normY = 5000;
            RECT rc;
            POINT origin = new POINT { X = 0, Y = 0 };
            if (!GetClientRect(_targetHwnd, out rc) || !ClientToScreen(_targetHwnd, ref origin))
                return false;

            int w = rc.Right - rc.Left;
            int h = rc.Bottom - rc.Top;
            if (w <= 0 || h <= 0) return false;

            // Compute actual centered video rect inside client rect (accounts for left/right pillarbox & top/bottom letterbox!)
            double vx = 0, vy = 0, vw = w, vh = h;
            int vidW = _videoWidth > 0 ? _videoWidth : _client.ScreenWidth;
            int vidH = _videoHeight > 0 ? _videoHeight : _client.ScreenHeight;
            if (vidW > 0 && vidH > 0)
            {
                double winAspect = (double)w / h;
                double vidAspect = (double)vidW / vidH;
                if (winAspect > vidAspect + 0.002)
                {
                    vh = h;
                    vw = h * vidAspect;
                    vx = (w - vw) / 2.0;
                    vy = 0;
                }
                else if (winAspect < vidAspect - 0.002)
                {
                    vw = w;
                    vh = w / vidAspect;
                    vx = 0;
                    vy = (h - vh) / 2.0;
                }
            }

            double rx = screenX - origin.X;
            double ry = screenY - origin.Y;
            if (!clampOutside && (rx < vx || rx > vx + vw || ry < vy || ry > vy + vh))
                return false;

            double nxD = ((rx - vx) / vw) * 10000.0;
            double nyD = ((ry - vy) / vh) * 10000.0;
            normX = Math.Max(0, Math.Min(10000, (int)Math.Round(nxD)));
            normY = Math.Max(0, Math.Min(10000, (int)Math.Round(nyD)));
            return true;
        }

        public void OnRawMouseDelta(int dx, int dy)
        {
            if (!_active || _profile == null || !_client.IsConnected || _targetHwnd == IntPtr.Zero)
                return;
            if (!_cameraLocked && _wheelTouchId == 0)
                return;
            if (GetForegroundWindow() != _targetHwnd)
                return;
            if (dx == 0 && dy == 0) return;

            lock (_stateLock)
            {
                if (_wheelTouchId != 0)
                {
                    _wheelAccumDx += dx;
                    _wheelAccumDy += dy;
                }
                else if (_cameraLocked)
                {
                    _camAccumDx += dx;
                    _camAccumDy += dy;
                }
            }
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _active && _profile != null && _client.IsConnected && _targetHwnd != IntPtr.Zero)
            {
                if (GetForegroundWindow() == _targetHwnd)
                {
                    int msg = wParam.ToInt32();
                    var ms = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

                    // Extra mouse buttons (Middle=4, Mouse4=5, Mouse5=6) work as action keys in both modes
                    int extraVk = 0;
                    bool extraDown = false;
                    bool extraUp = false;
                    if (msg == WM_MBUTTONDOWN) { extraVk = 4; extraDown = true; }
                    else if (msg == WM_MBUTTONUP) { extraVk = 4; extraUp = true; }
                    else if (msg == WM_XBUTTONDOWN || msg == WM_XBUTTONUP)
                    {
                        uint xbtn = (ms.mouseData >> 16) & 0xFFFF;
                        if (xbtn == 1) extraVk = 5;
                        else if (xbtn == 2) extraVk = 6;
                        extraDown = (msg == WM_XBUTTONDOWN);
                        extraUp = (msg == WM_XBUTTONUP);
                    }

                    if (extraVk > 0)
                    {
                        if (extraUp)
                        {
                            bool releasedAny = false;
                            lock (_stateLock)
                            {
                                if (_wheelTouchId != 0 && _wheelVk == extraVk)
                                {
                                    _client.InjectTouch(1, _wheelTouchId, (int)Math.Round(_wheelCurX), (int)Math.Round(_wheelCurY));
                                    _wheelTouchId = 0;
                                    _wheelVk = 0;
                                    _wheelAccumDx = 0;
                                    _wheelAccumDy = 0;
                                    releasedAny = true;
                                }
                                List<KeyBindItem> toRelease = null;
                                foreach (var kv in _heldKeys)
                                {
                                    if (kv.Key.VkCode == extraVk)
                                    {
                                        if (toRelease == null) toRelease = new List<KeyBindItem>();
                                        toRelease.Add(kv.Key);
                                    }
                                }
                                if (toRelease != null)
                                {
                                    for (int r = 0; r < toRelease.Count; r++)
                                    {
                                        var relItem = toRelease[r];
                                        long relId = _heldKeys[relItem];
                                        _heldKeys.Remove(relItem);
                                        _client.InjectTouch(1, relId, relItem.X, relItem.Y);
                                        releasedAny = true;
                                    }
                                }
                            }
                            if (releasedAny) return (IntPtr)1;
                        }

                        int curMods = GetCurrentModifiers();
                        var matched = FindMatchingBind(extraVk, curMods);
                        if (matched != null)
                        {
                            if (extraDown)
                            {
                                if ((matched.Modifiers & 4) != 0 && NormalizeVk(_profile.CameraToggleVk) == 18)
                                    _camToggleUsedAsModifier = true;

                                lock (_stateLock)
                                {
                                    if (matched.IsWheelDrag)
                                    {
                                        if (_wheelTouchId == 0)
                                        {
                                            StopCameraFingerLocked();
                                            _camAccumDx = 0;
                                            _camAccumDy = 0;
                                            _wheelVk = matched.VkCode;
                                            _wheelOriginX = matched.X;
                                            _wheelOriginY = matched.Y;
                                            _wheelCurX = matched.X;
                                            _wheelCurY = matched.Y;
                                            _wheelRadius = matched.WheelRadius > 200 ? matched.WheelRadius : 1500;
                                            _wheelAccumDx = 0;
                                            _wheelAccumDy = 0;
                                            _wheelTouchId = _nextTouchId++;
                                            _client.InjectTouch(0, _wheelTouchId, matched.X, matched.Y);
                                        }
                                    }
                                    else if (!_heldKeys.ContainsKey(matched))
                                    {
                                        long id = _nextTouchId++;
                                        _heldKeys[matched] = id;
                                        _client.InjectTouch(0, id, matched.X, matched.Y);
                                    }
                                }
                            }
                            return (IntPtr)1;
                        }
                    }

                    // Right-click mapped action works in BOTH camera-locked mode AND normal mode!
                    if (msg == WM_RBUTTONDOWN && _profile.EnableRightClick)
                    {
                        lock (_stateLock)
                        {
                            if (_rightTouchId == 0)
                            {
                                _rightTouchId = _nextTouchId++;
                                _client.InjectTouch(0, _rightTouchId, _profile.RightClickX, _profile.RightClickY);
                            }
                        }
                        return (IntPtr)1;
                    }
                    else if (msg == WM_RBUTTONUP && _profile.EnableRightClick)
                    {
                        lock (_stateLock)
                        {
                            if (_rightTouchId != 0)
                            {
                                _client.InjectTouch(1, _rightTouchId, _profile.RightClickX, _profile.RightClickY);
                                _rightTouchId = 0;
                            }
                        }
                        return (IntPtr)1;
                    }

                    if (_cameraLocked)
                    {
                        if (msg == WM_MOUSEMOVE)
                        {
                            // Hardware mouse deltas are captured via Win32 Raw Input (WM_INPUT);
                            // simply swallow WM_MOUSEMOVE here so the cursor stays pinned cleanly.
                            return (IntPtr)1;
                        }
                        else if (msg == WM_LBUTTONDOWN && _profile.EnableLeftClick)
                        {
                            lock (_stateLock)
                            {
                                if (_leftTouchId == 0)
                                {
                                    _leftTouchId = _nextTouchId++;
                                    _client.InjectTouch(0, _leftTouchId, _profile.LeftClickX, _profile.LeftClickY);
                                }
                            }
                            return (IntPtr)1;
                        }
                        else if (msg == WM_LBUTTONUP && _profile.EnableLeftClick)
                        {
                            lock (_stateLock)
                            {
                                if (_leftTouchId != 0)
                                {
                                    _client.InjectTouch(1, _leftTouchId, _profile.LeftClickX, _profile.LeftClickY);
                                    _leftTouchId = 0;
                                }
                            }
                            return (IntPtr)1;
                        }
                        else if (msg == WM_RBUTTONDOWN)
                        {
                            lock (_stateLock)
                            {
                                if (_profile.EnableRightClick && _rightTouchId == 0)
                                {
                                    _rightTouchId = _nextTouchId++;
                                    _client.InjectTouch(0, _rightTouchId, _profile.RightClickX, _profile.RightClickY);
                                }
                            }
                            return (IntPtr)1;
                        }
                        else if (msg == WM_RBUTTONUP)
                        {
                            lock (_stateLock)
                            {
                                if (_profile.EnableRightClick && _rightTouchId != 0)
                                {
                                    _client.InjectTouch(1, _rightTouchId, _profile.RightClickX, _profile.RightClickY);
                                    _rightTouchId = 0;
                                }
                            }
                            return (IntPtr)1;
                        }
                    }
                    else
                    {
                        // Camera lock OFF: route client-area left clicks through our SINGLE control socket so scrcpy.exe never cancels WASD!
                        int nx, ny;
                        if (msg == WM_LBUTTONDOWN && TryScreenToClientNorm(ms.pt.X, ms.pt.Y, out nx, out ny))
                        {
                            lock (_stateLock)
                            {
                                _manualTouchId = _nextTouchId++;
                                _manualLastX = nx;
                                _manualLastY = ny;
                                _client.InjectTouch(0, _manualTouchId, nx, ny);
                            }
                            return (IntPtr)1;
                        }
                        else if (msg == WM_MOUSEMOVE && _manualTouchId != 0)
                        {
                            if (TryScreenToClientNorm(ms.pt.X, ms.pt.Y, out nx, out ny, true))
                            {
                                lock (_stateLock)
                                {
                                    if (_manualTouchId != 0)
                                    {
                                        _manualLastX = nx;
                                        _manualLastY = ny;
                                        _client.InjectTouch(2, _manualTouchId, nx, ny);
                                    }
                                }
                            }
                            // Let cursor move normally visually
                        }
                        else if (msg == WM_LBUTTONUP && _manualTouchId != 0)
                        {
                            lock (_stateLock)
                            {
                                if (_manualTouchId != 0)
                                {
                                    _client.InjectTouch(1, _manualTouchId, _manualLastX, _manualLastY);
                                    _manualTouchId = 0;
                                }
                            }
                            return (IntPtr)1;
                        }
                    }
                }
            }
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        private void FlushCameraDeltaLocked(int adx, int ady, DateTime now)
        {
            double sensX = (_profile.CameraSensX > 0 ? _profile.CameraSensX : 1.0) * 8.0;
            double sensY = (_profile.CameraSensY > 0 ? _profile.CameraSensY : 1.0) * 12.0;

            double remX = Math.Max(-20000, Math.Min(20000, adx * sensX));
            double remY = Math.Max(-20000, Math.Min(20000, ady * sensY));

            _lastCamMotionUtc = now;

            for (int step = 0; step < 40 && (Math.Abs(remX) > 1.0 || Math.Abs(remY) > 1.0); step++)
            {
                if (_camTouchId == 0)
                {
                    _camCurX = Math.Max(500, Math.Min(9500, _profile.CameraX));
                    _camCurY = Math.Max(500, Math.Min(9500, _profile.CameraY));
                    _camTouchId = _nextTouchId++;
                    _client.InjectTouch(0, _camTouchId, (int)Math.Round(_camCurX), (int)Math.Round(_camCurY));
                }

                double sx = Math.Max(-500.0, Math.Min(500.0, remX));
                double sy = Math.Max(-500.0, Math.Min(500.0, remY));
                double nx = _camCurX + sx;
                double ny = _camCurY + sy;

                if (nx < 500 || nx > 9500 || ny < 500 || ny > 9500)
                {
                    StopCameraFingerLocked();
                    _camCurX = Math.Max(500, Math.Min(9500, _profile.CameraX));
                    _camCurY = Math.Max(500, Math.Min(9500, _profile.CameraY));
                    _camTouchId = _nextTouchId++;
                    _client.InjectTouch(0, _camTouchId, (int)Math.Round(_camCurX), (int)Math.Round(_camCurY));
                    nx = Math.Max(500, Math.Min(9500, _camCurX + sx));
                    ny = Math.Max(500, Math.Min(9500, _camCurY + sy));
                }

                sx = nx - _camCurX;
                sy = ny - _camCurY;
                _camCurX = nx;
                _camCurY = ny;
                _client.InjectTouch(2, _camTouchId, (int)Math.Round(_camCurX), (int)Math.Round(_camCurY));
                remX -= sx;
                remY -= sy;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
