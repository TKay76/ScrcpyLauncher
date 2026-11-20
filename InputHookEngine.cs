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

        // Action keys currently held down (vk -> pointerId)
        private readonly Dictionary<int, long> _heldKeys = new Dictionary<int, long>();
        private long _leftTouchId = 0;
        private long _rightTouchId = 0;

        public event Action<bool> CameraLockChanged;
        public event Action<string> StatusMessage;
        public event Action HintToggleRequested;

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
                // 1. Flush accumulated mouse camera delta at smooth ~125Hz (zero WH_MOUSE_LL contention)
                if (_cameraLocked && (_camAccumDx != 0 || _camAccumDy != 0))
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
                    anyHeld = _joyTouchId != 0 || _camTouchId != 0 || _heldKeys.Count > 0 || _leftTouchId != 0 || _rightTouchId != 0 || _manualTouchId != 0;
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

                if (_manualTouchId != 0)
                {
                    _client.InjectTouch(1, _manualTouchId, _manualLastX, _manualLastY);
                    _manualTouchId = 0;
                }

                if (_profile != null)
                {
                    foreach (var kv in _heldKeys)
                    {
                        var item = _profile.Keys.Find(k => k.VkCode == kv.Key);
                        if (item != null) _client.InjectTouch(1, kv.Value, item.X, item.Y);
                    }
                }
                _heldKeys.Clear();

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

                        // F1 toggles on-screen key hints mid-game
                        if (vk == 112)
                        {
                            if (isUp && HintToggleRequested != null) HintToggleRequested();
                            return (IntPtr)1;
                        }

                        if (_profile.EnableMouseCamera && vk == NormalizeVk(_profile.CameraToggleVk))
                        {
                            if (isUp) SetCameraLock(!_cameraLocked);
                            return (IntPtr)1;
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

                        for (int i = 0; i < _profile.Keys.Count; i++)
                        {
                            var item = _profile.Keys[i];
                            if (NormalizeVk(item.VkCode) == vk)
                            {
                                lock (_stateLock)
                                {
                                    if (isDown)
                                    {
                                        if (!_heldKeys.ContainsKey(item.VkCode))
                                        {
                                            long id = _nextTouchId++;
                                            _heldKeys[item.VkCode] = id;
                                            _client.InjectTouch(0, id, item.X, item.Y);
                                        }
                                    }
                                    else if (isUp)
                                    {
                                        long id;
                                        if (_heldKeys.TryGetValue(item.VkCode, out id))
                                        {
                                            _heldKeys.Remove(item.VkCode);
                                            _client.InjectTouch(1, id, item.X, item.Y);
                                        }
                                    }
                                }
                                return (IntPtr)1;
                            }
                        }
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

        private bool TryScreenToClientNorm(int screenX, int screenY, out int normX, out int normY)
        {
            normX = 5000; normY = 5000;
            RECT rc;
            POINT origin = new POINT { X = 0, Y = 0 };
            if (!GetClientRect(_targetHwnd, out rc) || !ClientToScreen(_targetHwnd, ref origin))
                return false;

            int w = rc.Right - rc.Left;
            int h = rc.Bottom - rc.Top;
            if (w <= 0 || h <= 0) return false;

            int rx = screenX - origin.X;
            int ry = screenY - origin.Y;
            if (rx < 0 || rx > w || ry < 0 || ry > h) return false;

            normX = (int)Math.Round((rx / (double)w) * 10000.0);
            normY = (int)Math.Round((ry / (double)h) * 10000.0);
            return true;
        }

        public void OnRawMouseDelta(int dx, int dy)
        {
            if (!_active || !_cameraLocked || _profile == null || !_client.IsConnected || _targetHwnd == IntPtr.Zero)
                return;
            if (GetForegroundWindow() != _targetHwnd)
                return;
            if (dx == 0 && dy == 0) return;

            lock (_stateLock)
            {
                _camAccumDx += dx;
                _camAccumDy += dy;
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
                            if (TryScreenToClientNorm(ms.pt.X, ms.pt.Y, out nx, out ny))
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
