using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace ScrcpyLauncher
{
    public class FpsOverlayWindow : Window
    {
        private readonly TextBlock _txtFps;

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
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        public FpsOverlayWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            IsHitTestVisible = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(190, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 148, 163, 184)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(9, 4, 9, 4)
            };

            _txtFps = new TextBlock
            {
                Text = "FPS --",
                FontFamily = new FontFamily("Consolas, Segoe UI, Malgun Gothic"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4ADE80"))
            };

            border.Child = _txtFps;
            Content = border;

            SourceInitialized += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
            };
        }

        public void UpdateFps(int fps, string extra)
        {
            _txtFps.Text = string.IsNullOrEmpty(extra)
                ? string.Format("{0} FPS", fps)
                : string.Format("{0} FPS {1}", fps, extra);

            Color color;
            if (fps >= 55)
                color = (Color)ColorConverter.ConvertFromString("#4ADE80"); // Green
            else if (fps >= 35)
                color = (Color)ColorConverter.ConvertFromString("#FACC15"); // Yellow
            else
                color = (Color)ColorConverter.ConvertFromString("#F87171"); // Red

            _txtFps.Foreground = new SolidColorBrush(color);
        }

        public void SyncToTargetWindow(IntPtr targetHwnd, IntPtr launcherHwnd)
        {
            if (targetHwnd == IntPtr.Zero || !IsWindowVisible(targetHwnd) || IsIconic(targetHwnd))
            {
                if (IsVisible) Hide();
                return;
            }

            IntPtr fg = GetForegroundWindow();
            IntPtr myHwnd = new WindowInteropHelper(this).Handle;
            if (fg != targetHwnd && fg != launcherHwnd && fg != myHwnd)
            {
                if (IsVisible) Hide();
                return;
            }

            POINT pt = new POINT { X = 0, Y = 0 };
            if (!ClientToScreen(targetHwnd, ref pt))
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
            catch
            {
                var src = PresentationSource.FromVisual(this);
                if (src != null && src.CompositionTarget != null)
                    scale = src.CompositionTarget.TransformToDevice.M11;
            }

            Left = (pt.X / scale) + 12;
            Top = (pt.Y / scale) + 10;

            if (!IsVisible) Show();
        }
    }
}
