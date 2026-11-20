using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace ScrcpyLauncher
{
    public class KeymapOverlayWindow : Window
    {
        private readonly Canvas _canvas;
        private readonly Border _editToolbar;
        private KeymapProfile _profile;
        private bool _isEditMode;

        public event Action ProfileEdited;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        public bool IsEditMode { get { return _isEditMode; } }

        public KeymapOverlayWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid();
            _canvas = new Canvas { Background = Brushes.Transparent };
            grid.Children.Add(_canvas);

            // Top floating toolbar (only visible in edit mode)
            _editToolbar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(230, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(160, 59, 130, 246)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 6, 12, 6),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 12, 0, 0),
                Visibility = Visibility.Collapsed
            };

            var barPanel = new StackPanel { Orientation = Orientation.Horizontal };
            barPanel.Children.Add(new TextBlock
            {
                Text = "키 배치 편집 모드  |  아이콘을 드래그해서 게임 버튼 위로 옮기세요",
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            });

            var btnDone = new Button
            {
                Content = "배치 완료",
                Padding = new Thickness(12, 4, 12, 4),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 12,
                FontWeight = FontWeights.Bold
            };
            btnDone.Click += (s, e) =>
            {
                SetEditMode(false);
                if (ProfileEdited != null) ProfileEdited();
            };
            barPanel.Children.Add(btnDone);
            _editToolbar.Child = barPanel;
            grid.Children.Add(_editToolbar);

            Content = grid;
            SourceInitialized += (s, e) => ApplyWindowClickThrough(!_isEditMode);
        }

        public void SetProfile(KeymapProfile profile)
        {
            _profile = profile;
            Redraw();
        }

        public void SetEditMode(bool edit)
        {
            _isEditMode = edit;
            _canvas.Background = edit
                ? new SolidColorBrush(Color.FromArgb(95, 15, 23, 42))
                : Brushes.Transparent;
            _editToolbar.Visibility = edit ? Visibility.Visible : Visibility.Collapsed;
            ApplyWindowClickThrough(!edit);
            Redraw();
        }

        private void ApplyWindowClickThrough(bool clickThrough)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (clickThrough)
                SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
            else
                SetWindowLong(hwnd, GWL_EXSTYLE, (ex | WS_EX_TOOLWINDOW) & ~(WS_EX_TRANSPARENT | WS_EX_NOACTIVATE));
        }

        public void SyncToTargetWindow(IntPtr targetHwnd, IntPtr launcherHwnd)
        {
            if (targetHwnd == IntPtr.Zero || !IsWindowVisible(targetHwnd) || IsIconic(targetHwnd))
            {
                if (IsVisible) Hide();
                return;
            }

            if (!_isEditMode)
            {
                IntPtr fg = GetForegroundWindow();
                IntPtr myHwnd = new WindowInteropHelper(this).Handle;
                if (fg != targetHwnd && fg != launcherHwnd && fg != myHwnd)
                {
                    if (IsVisible) Hide();
                    return;
                }
            }

            RECT rc;
            POINT pt = new POINT { X = 0, Y = 0 };
            if (!GetClientRect(targetHwnd, out rc) || !ClientToScreen(targetHwnd, ref pt))
            {
                if (IsVisible) Hide();
                return;
            }

            double scale = 1.0;
            try
            {
                uint dpi = GetDpiForWindow(targetHwnd);
                if (dpi >= 96) scale = dpi / 96.0;
            }
            catch { }

            double w = (rc.Right - rc.Left) / scale;
            double h = (rc.Bottom - rc.Top) / scale;
            if (w <= 20 || h <= 20) return;

            bool sizeChanged = Math.Abs(Width - w) > 1 || Math.Abs(Height - h) > 1;
            Left = pt.X / scale;
            Top = pt.Y / scale;
            Width = w;
            Height = h;

            if (!IsVisible)
            {
                Show();
                Redraw();
            }
            else if (sizeChanged)
            {
                Redraw();
            }
        }

        public void Redraw()
        {
            _canvas.Children.Clear();
            if (_profile == null || double.IsNaN(Width) || double.IsNaN(Height) || Width <= 20 || Height <= 20)
                return;

            // 1. WASD Joystick circle
            if (_profile.EnableJoystick)
            {
                double rPx = (_profile.JoyRadius / 10000.0) * Width;
                if (double.IsNaN(rPx) || double.IsInfinity(rPx) || rPx < 36) rPx = 36;
                var joy = CreateBadge(
                    "WASD",
                    _profile.SprintVk > 0 ? KeyNameHelper.GetName(_profile.SprintVk) + " 질주" : "",
                    rPx * 2, rPx * 2,
                    Color.FromArgb((byte)(_isEditMode ? 155 : 26), 37, 99, 235),
                    Color.FromArgb((byte)(_isEditMode ? 220 : 85), 147, 197, 253),
                    true);
                PlaceAndMakeDraggable(joy, _profile.JoyX, _profile.JoyY, (nx, ny) => { _profile.JoyX = nx; _profile.JoyY = ny; });
            }

            // 2. Camera look anchor
            if (_profile.EnableMouseCamera)
            {
                var cam = CreateBadge(
                    "시점",
                    KeyNameHelper.GetName(_profile.CameraToggleVk),
                    56, 36,
                    Color.FromArgb((byte)(_isEditMode ? 190 : 48), 124, 58, 237),
                    Color.FromArgb((byte)(_isEditMode ? 220 : 95), 196, 181, 253),
                    false);
                PlaceAndMakeDraggable(cam, _profile.CameraX, _profile.CameraY, (nx, ny) => { _profile.CameraX = nx; _profile.CameraY = ny; });
            }

            // 3. Mouse Left / Right Click
            if (_profile.EnableLeftClick)
            {
                var lc = CreateBadge(
                    "좌클릭",
                    _profile.LeftClickLabel ?? "",
                    54, 38,
                    Color.FromArgb((byte)(_isEditMode ? 200 : 52), 220, 38, 38),
                    Color.FromArgb((byte)(_isEditMode ? 220 : 100), 252, 165, 165),
                    false);
                PlaceAndMakeDraggable(lc, _profile.LeftClickX, _profile.LeftClickY, (nx, ny) => { _profile.LeftClickX = nx; _profile.LeftClickY = ny; });
            }

            if (_profile.EnableRightClick)
            {
                var rc = CreateBadge(
                    "우클릭",
                    _profile.RightClickLabel ?? "",
                    54, 38,
                    Color.FromArgb((byte)(_isEditMode ? 200 : 52), 217, 119, 6),
                    Color.FromArgb((byte)(_isEditMode ? 220 : 100), 253, 224, 71),
                    false);
                PlaceAndMakeDraggable(rc, _profile.RightClickX, _profile.RightClickY, (nx, ny) => { _profile.RightClickX = nx; _profile.RightClickY = ny; });
            }

            // 4. Action keys (normal + wheel drag)
            foreach (var item in _profile.Keys)
            {
                var k = item;
                if (k.IsWheelDrag)
                {
                    var wheelBadge = CreateBadge(
                        k.KeyName,
                        _isEditMode ? (string.IsNullOrWhiteSpace(k.Label) ? "휠" : k.Label) : "휠",
                        _isEditMode ? 52 : 36,
                        _isEditMode ? 52 : 36,
                        Color.FromArgb((byte)(_isEditMode ? 190 : 54), 13, 148, 136),
                        Color.FromArgb((byte)(_isEditMode ? 225 : 105), 94, 234, 212),
                        true);
                    PlaceAndMakeDraggable(wheelBadge, k.X, k.Y, (nx, ny) => { k.X = nx; k.Y = ny; });
                }
                else
                {
                    var badge = CreateBadge(
                        k.KeyName,
                        _isEditMode ? (k.Label ?? "") : "",
                        _isEditMode ? 48 : 32,
                        _isEditMode ? 36 : 24,
                        Color.FromArgb((byte)(_isEditMode ? 190 : 58), 15, 23, 42),
                        Color.FromArgb((byte)(_isEditMode ? 210 : 90), 148, 163, 184),
                        false);
                    PlaceAndMakeDraggable(badge, k.X, k.Y, (nx, ny) => { k.X = nx; k.Y = ny; });
                }
            }
        }

        private Border CreateBadge(string title, string sub, double w, double h, Color bg, Color borderCol, bool circle)
        {
            byte textAlpha = (byte)(_isEditMode ? 255 : 170);
            byte subAlpha = (byte)(_isEditMode ? 220 : 135);
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = new SolidColorBrush(Color.FromArgb(textAlpha, 255, 255, 255)),
                FontSize = circle ? 13 : 11,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            if (!string.IsNullOrWhiteSpace(sub))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = sub,
                    Foreground = new SolidColorBrush(Color.FromArgb(subAlpha, 226, 232, 240)),
                    FontSize = 9.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = Math.Max(46, w - 4)
                });
            }

            return new Border
            {
                Width = w,
                Height = h,
                Background = new SolidColorBrush(bg),
                BorderBrush = new SolidColorBrush(borderCol),
                BorderThickness = new Thickness(_isEditMode ? 1.5 : 1.0),
                CornerRadius = new CornerRadius(circle ? w / 2.0 : 8),
                Child = stack,
                Cursor = _isEditMode ? Cursors.SizeAll : Cursors.Arrow
            };
        }

        private void PlaceAndMakeDraggable(Border el, int normX, int normY, Action<int, int> onMoved)
        {
            double cx = (normX / 10000.0) * Width;
            double cy = (normY / 10000.0) * Height;
            Canvas.SetLeft(el, cx - el.Width / 2.0);
            Canvas.SetTop(el, cy - el.Height / 2.0);
            _canvas.Children.Add(el);

            if (!_isEditMode) return;

            bool dragging = false;
            Point offset = new Point();

            el.MouseLeftButtonDown += (s, e) =>
            {
                dragging = true;
                offset = e.GetPosition(el);
                el.CaptureMouse();
                e.Handled = true;
            };
            el.MouseMove += (s, e) =>
            {
                if (!dragging) return;
                Point p = e.GetPosition(_canvas);
                double left = p.X - offset.X;
                double top = p.Y - offset.Y;
                Canvas.SetLeft(el, left);
                Canvas.SetTop(el, top);

                int nx = (int)Math.Round(((left + el.Width / 2.0) / Width) * 10000.0);
                int ny = (int)Math.Round(((top + el.Height / 2.0) / Height) * 10000.0);
                nx = Math.Max(0, Math.Min(10000, nx));
                ny = Math.Max(0, Math.Min(10000, ny));
                onMoved(nx, ny);
            };
            el.MouseLeftButtonUp += (s, e) =>
            {
                if (!dragging) return;
                dragging = false;
                el.ReleaseMouseCapture();
                e.Handled = true;
            };
        }
    }
}
