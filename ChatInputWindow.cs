using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace ScrcpyLauncher
{
    public class ChatInputWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        private readonly TextBox _tb;
        private readonly TextBlock _hint;
        private bool _submitted = false;

        public event Action<string> TextSubmitted;
        public event Action Cancelled;

        public ChatInputWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            Width = 460;
            Height = 52;
            FontFamily = new FontFamily("Malgun Gothic, Segoe UI");

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(238, 15, 23, 42)),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8")),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 6, 10, 6)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var badge = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7")),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(7, 2, 7, 2),
                Margin = new Thickness(0, 0, 9, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "한글 채팅",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                }
            };
            Grid.SetColumn(badge, 0);
            grid.Children.Add(badge);

            var inputGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            _tb = new TextBox
            {
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                CaretBrush = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 13.5,
                VerticalContentAlignment = VerticalAlignment.Center,
                AcceptsReturn = false
            };
            _hint = new TextBlock
            {
                Text = "메시지 입력 후 Enter (Esc 취소)",
                FontSize = 12.5,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
                IsHitTestVisible = false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0)
            };
            _tb.TextChanged += (s, e) =>
            {
                _hint.Visibility = string.IsNullOrEmpty(_tb.Text) ? Visibility.Visible : Visibility.Collapsed;
            };
            inputGrid.Children.Add(_tb);
            inputGrid.Children.Add(_hint);
            Grid.SetColumn(inputGrid, 1);
            grid.Children.Add(inputGrid);

            var btnSend = new Button
            {
                Content = "전송 ↵",
                Height = 28,
                Padding = new Thickness(10, 2, 10, 2),
                Margin = new Thickness(8, 0, 0, 0),
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnSend.Click += (s, e) => SubmitAndHide();
            Grid.SetColumn(btnSend, 2);
            grid.Children.Add(btnSend);

            border.Child = grid;
            Content = border;

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    SubmitAndHide();
                }
                else if (e.Key == Key.Escape || e.Key == Key.F2)
                {
                    e.Handled = true;
                    CancelAndHide();
                }
            };

            Deactivated += (s, e) =>
            {
                if (IsVisible && !_submitted)
                    CancelAndHide();
            };
        }

        public void OpenForTarget(IntPtr targetHwnd)
        {
            _submitted = false;
            _tb.Text = "";
            _hint.Visibility = Visibility.Visible;

            try
            {
                RECT rc;
                if (targetHwnd != IntPtr.Zero && GetWindowRect(targetHwnd, out rc))
                {
                    var src = PresentationSource.FromVisual(this);
                    double dpiX = (src != null && src.CompositionTarget != null) ? src.CompositionTarget.TransformToDevice.M11 : 1.0;
                    double dpiY = (src != null && src.CompositionTarget != null) ? src.CompositionTarget.TransformToDevice.M22 : 1.0;

                    double winLeft = rc.Left / dpiX;
                    double winTop = rc.Top / dpiY;
                    double winW = (rc.Right - rc.Left) / dpiX;
                    double winH = (rc.Bottom - rc.Top) / dpiY;

                    Left = winLeft + (winW - Width) / 2.0;
                    Top = winTop + winH - Height - 36.0;
                }
                else
                {
                    Left = (SystemParameters.PrimaryScreenWidth - Width) / 2.0;
                    Top = SystemParameters.PrimaryScreenHeight - Height - 100.0;
                }
            }
            catch { }

            Show();
            Activate();
            _tb.Focus();
            Keyboard.Focus(_tb);
        }

        private void SubmitAndHide()
        {
            if (_submitted) return;
            _submitted = true;
            string text = (_tb.Text ?? "").Trim();
            Hide();
            if (!string.IsNullOrEmpty(text))
            {
                if (TextSubmitted != null) TextSubmitted(text);
            }
            else
            {
                if (Cancelled != null) Cancelled();
            }
        }

        private void CancelAndHide()
        {
            if (_submitted) return;
            _submitted = true;
            Hide();
            if (Cancelled != null) Cancelled();
        }
    }
}
