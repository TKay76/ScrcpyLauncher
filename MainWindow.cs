using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace ScrcpyLauncher
{
    public class MainWindow : Window
    {
        private readonly AdbService _adb = new AdbService();
        private LauncherSettings _settings;
        private readonly DispatcherTimer _deviceTimer;
        private readonly DispatcherTimer _recordTimer;
        private readonly DispatcherTimer _overlayTimer;
        private readonly List<QuestEntry> _scannedQuests = new List<QuestEntry>();
        private bool _isQuestScanningNow = false;
        private FpsOverlayWindow _fpsOverlay;
        private KeymapOverlayWindow _keymapOverlay;
        private CursorHiderWindow _cursorHider;
        private ScrcpyControlClient _controlClient;
        private InputHookEngine _hookEngine;
        private List<KeymapProfile> _keymapProfiles = new List<KeymapProfile>();
        private KeymapProfile _activeProfile;
        private string _detectedScrcpyVersion = "5.0.1";
        private int _activeDisplayWidth = 1920;
        private int _activeDisplayHeight = 1080;
        private int _pendingNewKeyVk = 0;
        private int _pendingNewKeyMods = 0;
        private bool _autoKeymapTriggered = false;
        private Process _scrcpyProcess;
        private Process _recordProcess;
        private string _currentRecordPath = "";
        private DateTime _recordStartTime;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate handlerRoutine, bool add);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, byte[] pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;

        private const int WM_INPUT = 0x00FF;
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_QUEST_SCAN = 9001;
        private const int HOTKEY_SCREENSHOT = 9002;
        private const uint VK_F8 = 0x77;
        private const uint VK_F12 = 0x7B;
        private const uint RIM_TYPEHID = 2;
        private bool _questHotKeyRegistered = false;
        private bool _screenshotHotKeyRegistered = false;
        private bool _isCapturingScreenshot = false;
        private DateTime _lastShareShotUtc = DateTime.MinValue;
        private const uint RID_INPUT = 0x10000003;
        private const uint RIDEV_INPUTSINK = 0x00000100;
        private const uint RIM_TYPEMOUSE = 0;

        private delegate bool ConsoleCtrlDelegate(uint ctrlType);

        private List<DeviceInfo> _devices = new List<DeviceInfo>();
        private List<AppItem> _allApps = new List<AppItem>();
        private string _appsLoadedForSerial = "";
        private string _activeDisplayId = "";
        private bool _isRefreshingDevices = false;
        private bool _isLoadingApps = false;
        private bool _isInitializing = true;

        // UI Controls
        private TextBlock _txtScrcpyStatus;
        private Border _bdScrcpyStatus;
        private Button _btnUpdate;
        private UpdateInfo _updateInfo;
        private string _updateReleaseUrl = "";
        private Button _btnDownloadScrcpy;
        private Button _btnSetupGuide;
        private Button _btnShortcuts;
        private Button _btnSelectFolder;
        private ComboBox _cmbLanguage;

        private ComboBox _cmbDevices;
        private Button _btnRefreshDevices;
        private Button _btnUsbToWifi;
        private Button _btnDisconnectWifi;
        private Button _btnRestartAdb;
        private TextBox _txtPairAddr;
        private TextBox _txtPairCode;
        private Button _btnPair;
        private TextBox _txtWifiAddr;
        private Button _btnWifiConnect;

        private ComboBox _cmbCodec;
        private TextBox _txtBitrate;
        private ComboBox _cmbMaxSize;
        private ComboBox _cmbMaxFps;
        private ComboBox _cmbRenderer;

        // Control Mode Cards
        private RadioButton _rbModeKeymap;
        private RadioButton _rbModeGamepad;
        private RadioButton _rbModeNormal;
        private TextBlock _txtModeSummary;

        private CheckBox _chkTurnScreenOff;
        private CheckBox _chkStayAwake;
        private CheckBox _chkLowLatency;
        private CheckBox _chkNoAudio;
        private CheckBox _chkFullscreen;
        private CheckBox _chkAlwaysOnTop;
        private CheckBox _chkPrintFps;

        private TextBox _txtRecordFolder;
        private Button _btnBrowseRecord;
        private Button _btnOpenRecord;

        private CheckBox _chkVirtualDisplay;
        private ComboBox _cmbVdSpec;
        private CheckBox _chkNoVdDecorations;
        private TextBox _txtAppSearch;
        private ComboBox _cmbApps;
        private Button _btnRefreshApps;

        private TextBlock _txtGuideTitle;
        private TextBlock _txtGuideBody;
        private Button _btnCopyCmd;

        private Button _btnStart;
        private Button _btnStop;
        private Button _btnScreenshot;
        private Button _btnRecordToggle;
        private Button _btnClearLog;
        private TextBlock _txtFpsBadge;
        private Border _bdFpsBadge;
        private TextBox _txtLog;

        // Keymap Tab Controls
        private Border _bdKmModeWarning;
        private TextBlock _txtKmModeWarning;
        private CheckBox _chkGamepadKbAssist;
        private ComboBox _cmbKeyProfiles;
        private Button _btnImportKmp;
        private Button _btnSaveProfile;
        private Button _btnSaveAsProfile;
        private Button _btnRenameProfile;
        private Button _btnDeleteProfile;
        private Button _btnReloadProfiles;
        private Button _btnExportCode;
        private Button _btnImportCode;
        private CheckBox _chkKmJoy;
        private CheckBox _chkKmCam;
        private CheckBox _chkKmLeft;
        private CheckBox _chkKmRight;
        private CheckBox _chkKmRightWheel;
        private CheckBox _chkKmHints;
        private ComboBox _cmbKmToggleKey;
        private Slider _sldKmSensX;
        private TextBlock _txtKmSensXVal;
        private Slider _sldKmSensY;
        private TextBlock _txtKmSensYVal;
        private Slider _sldHintOpacity;
        private TextBlock _txtHintOpacityVal;
        private Slider _sldHintScale;
        private TextBlock _txtHintScaleVal;
        private Button _btnKmStart;
        private Button _btnKmStop;
        private Button _btnKmEditOverlay;
        private ListBox _lstKmKeys;
        private TextBox _txtKmNewKey;
        private TextBox _txtKmNewLabel;
        private CheckBox _chkKmNewWheel;
        private Button _btnKmAddKey;
        private Button _btnKmDelKey;

        // Quest Planner Tab Controls
        private Button _btnQuestScanOne;
        private Button _btnQuestUndo;
        private Button _btnQuestSpeciesGuide;
        private Button _btnQuestResetDaily;
        private Button _btnQuestClear;
        private TextBlock _txtQuestDaily;
        private TextBlock _txtQuestCount;
        private TextBlock _txtQuestBanner;
        private StackPanel _pnlQuestPlans;

        private const string ScrcpyReleasesUrl = "https://github.com/Genymobile/scrcpy/releases";
        private const string DefaultGuideTitle = "옵션 가이드";
        private const string DefaultGuideBody = "각 옵션 위에 마우스를 올리면 언제 켜고 끄면 좋은지 설명이 표시됩니다.";

        public MainWindow()
        {
            Title = "Scrcpy Launcher v1.3.5 — Android 미러링";
            Width = 780;
            Height = 845;
            MinWidth = 720;
            MinHeight = 720;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEF2F6"));
            FontFamily = new FontFamily("Malgun Gothic, Segoe UI");

            try
            {
                var exeIcon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (exeIcon != null)
                {
                    Icon = Imaging.CreateBitmapSourceFromHIcon(
                        exeIcon.Handle,
                        Int32Rect.Empty,
                        System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                }
            }
            catch { }

            Content = BuildUi();
            BindControls();

            _settings = LauncherSettings.Load();
            Loc.SetLanguage(_settings.Language);
            ApplySettingsToUi();
            ApplyLanguage();

            _controlClient = new ScrcpyControlClient(_adb);
            _hookEngine = new InputHookEngine(_controlClient);
            _hookEngine.StatusMessage += (msg) => Dispatcher.BeginInvoke(new Action(() => AppendLog(msg)));
            _hookEngine.CameraLockChanged += (locked) => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (locked && _scrcpyProcess != null && !_scrcpyProcess.HasExited)
                {
                    if (_cursorHider == null) _cursorHider = new CursorHiderWindow();
                    _cursorHider.ShowAtScreenPoint(_hookEngine.CenterScreenX, _hookEngine.CenterScreenY, _scrcpyProcess.MainWindowHandle);
                }
                else if (_cursorHider != null)
                {
                    _cursorHider.HideSafe();
                }
            }));
            _hookEngine.HintToggleRequested += () => Dispatcher.BeginInvoke(new Action(() =>
            {
                bool next = !(_chkKmHints.IsChecked == true);
                _chkKmHints.IsChecked = next;
                if (_keymapOverlay != null) _keymapOverlay.SetShowHints(next);
                AppendLog(next ? "[키 매핑] 화면 키 힌트 ON (F1)" : "[키 매핑] 화면 키 힌트 OFF (F1)");
            }));
            _hookEngine.PauseToggled += (paused) => Dispatcher.BeginInvoke(new Action(() =>
            {
                string msg = Loc.T(paused ? "toast_chat_on" : "toast_chat_off");
                AppendLog("[키 매핑] " + msg);
                if (_keymapOverlay != null)
                {
                    _keymapOverlay.ShowToast(msg);
                    SyncFpsOverlay();
                }
            }));
            _hookEngine.ProfileSwitchRequested += (idx) => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (idx >= 0 && idx < _keymapProfiles.Count)
                {
                    _cmbKeyProfiles.SelectedIndex = idx;
                    var p = _keymapProfiles[idx];
                    string toast = Loc.F("toast_profile", idx + 1, p.Name);
                    AppendLog("[키 매핑] " + toast);
                    if (_keymapOverlay != null)
                    {
                        _keymapOverlay.ShowToast(toast);
                        SyncFpsOverlay();
                    }
                }
            }));
            LoadKeymapProfiles();

            string detected = AdbService.DetectDefaultDirectory(_settings.CustomScrcpyDir);
            _adb.SetDirectory(detected);

            _deviceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _deviceTimer.Tick += async (s, e) => await RefreshDevicesAsync(true);

            _recordTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _recordTimer.Tick += (s, e) => UpdateRecordButtonAppearance();

            _overlayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _overlayTimer.Tick += (s, e) => SyncFpsOverlay();

            Loaded += OnLoaded;
            SourceInitialized += OnSourceInitialized;
            Closing += OnClosing;

            _isInitializing = false;
            UpdateUiStates();
            UpdateCommandPreview();
        }

        private UIElement BuildUi()
        {
            string xaml = @"
<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
      xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
      Margin='16,14,16,14'>
  <Grid.Resources>
    <Style TargetType='Border' x:Key='Card'>
      <Setter Property='Background' Value='#FFFFFF'/>
      <Setter Property='BorderBrush' Value='#E2E8F0'/>
      <Setter Property='BorderThickness' Value='1'/>
      <Setter Property='CornerRadius' Value='10'/>
      <Setter Property='Padding' Value='14,12'/>
      <Setter Property='Margin' Value='0,0,0,10'/>
      <Setter Property='Effect'>
        <Setter.Value>
          <DropShadowEffect BlurRadius='10' ShadowDepth='2' Direction='270' Color='#0F172A' Opacity='0.05'/>
        </Setter.Value>
      </Setter>
    </Style>
    <Style TargetType='Border' x:Key='StepBadge'>
      <Setter Property='Background' Value='#DBEAFE'/>
      <Setter Property='CornerRadius' Value='10'/>
      <Setter Property='Width' Value='20'/>
      <Setter Property='Height' Value='20'/>
      <Setter Property='Margin' Value='0,0,7,0'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
    </Style>
    <Style TargetType='TextBlock' x:Key='StepNum'>
      <Setter Property='FontSize' Value='11'/>
      <Setter Property='FontWeight' Value='Bold'/>
      <Setter Property='Foreground' Value='#1D4ED8'/>
      <Setter Property='HorizontalAlignment' Value='Center'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
    </Style>
    <Style TargetType='TextBlock' x:Key='SectionTitle'>
      <Setter Property='FontSize' Value='13'/>
      <Setter Property='FontWeight' Value='Bold'/>
      <Setter Property='Foreground' Value='#0F172A'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
    </Style>
    <Style TargetType='TextBlock' x:Key='FieldLabel'>
      <Setter Property='FontSize' Value='12'/>
      <Setter Property='Foreground' Value='#475569'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
      <Setter Property='Margin' Value='0,0,6,0'/>
    </Style>
    <Style TargetType='TextBox' x:Key='ModernTextBox'>
      <Setter Property='Padding' Value='8,5'/>
      <Setter Property='FontSize' Value='12'/>
      <Setter Property='Height' Value='30'/>
      <Setter Property='Background' Value='#FFFFFF'/>
      <Setter Property='Foreground' Value='#0F172A'/>
      <Setter Property='BorderBrush' Value='#CBD5E1'/>
      <Setter Property='BorderThickness' Value='1'/>
      <Setter Property='VerticalContentAlignment' Value='Center'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='TextBox'>
            <Border x:Name='bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='6'>
              <ScrollViewer x:Name='PART_ContentHost' VerticalAlignment='Center' Margin='2,0'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsKeyboardFocused' Value='True'>
                <Setter TargetName='bd' Property='BorderBrush' Value='#2563EB'/>
              </Trigger>
              <Trigger Property='IsEnabled' Value='False'>
                <Setter TargetName='bd' Property='Opacity' Value='0.55'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style TargetType='TextBox' BasedOn='{StaticResource ModernTextBox}'/>
    <Style TargetType='ComboBox'>
      <Setter Property='Padding' Value='8,4'/>
      <Setter Property='Height' Value='30'/>
      <Setter Property='FontSize' Value='12'/>
      <Setter Property='VerticalContentAlignment' Value='Center'/>
    </Style>
    <Style TargetType='CheckBox'>
      <Setter Property='FontSize' Value='12'/>
      <Setter Property='Foreground' Value='#1E293B'/>
      <Setter Property='VerticalContentAlignment' Value='Center'/>
      <Setter Property='Margin' Value='0,4,10,4'/>
      <Setter Property='Cursor' Value='Hand'/>
    </Style>

    <Style TargetType='Button' x:Key='RoundedBtnBase'>
      <Setter Property='Padding' Value='12,6'/>
      <Setter Property='Height' Value='30'/>
      <Setter Property='FontSize' Value='12'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='6' Padding='{TemplateBinding Padding}'>
              <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='bd' Property='Opacity' Value='0.88'/>
              </Trigger>
              <Trigger Property='IsPressed' Value='True'>
                <Setter TargetName='bd' Property='Opacity' Value='0.75'/>
              </Trigger>
              <Trigger Property='IsEnabled' Value='False'>
                <Setter TargetName='bd' Property='Opacity' Value='0.42'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style TargetType='Button' x:Key='SubBtn' BasedOn='{StaticResource RoundedBtnBase}'>
      <Setter Property='Background' Value='#F1F5F9'/>
      <Setter Property='Foreground' Value='#1E293B'/>
      <Setter Property='BorderBrush' Value='#CBD5E1'/>
      <Setter Property='BorderThickness' Value='1'/>
    </Style>
    <Style TargetType='Button' x:Key='SoftBlueBtn' BasedOn='{StaticResource RoundedBtnBase}'>
      <Setter Property='Background' Value='#EFF6FF'/>
      <Setter Property='Foreground' Value='#1D4ED8'/>
      <Setter Property='BorderBrush' Value='#93C5FD'/>
      <Setter Property='BorderThickness' Value='1'/>
    </Style>
    <Style TargetType='Button' x:Key='SoftAmberBtn' BasedOn='{StaticResource RoundedBtnBase}'>
      <Setter Property='Background' Value='#FFF7ED'/>
      <Setter Property='Foreground' Value='#C2410C'/>
      <Setter Property='BorderBrush' Value='#FDBA74'/>
      <Setter Property='BorderThickness' Value='1'/>
    </Style>
    <Style TargetType='Button' x:Key='SoftRedBtn' BasedOn='{StaticResource RoundedBtnBase}'>
      <Setter Property='Background' Value='#FEF2F2'/>
      <Setter Property='Foreground' Value='#B91C1C'/>
      <Setter Property='BorderBrush' Value='#FCA5A5'/>
      <Setter Property='BorderThickness' Value='1'/>
    </Style>
    <Style TargetType='Button' x:Key='PrimaryBtn' BasedOn='{StaticResource RoundedBtnBase}'>
      <Setter Property='Height' Value='36'/>
      <Setter Property='Padding' Value='20,8'/>
      <Setter Property='FontSize' Value='13'/>
      <Setter Property='FontWeight' Value='Bold'/>
      <Setter Property='Background' Value='#2563EB'/>
      <Setter Property='Foreground' Value='#FFFFFF'/>
      <Setter Property='BorderThickness' Value='0'/>
    </Style>
    <Style TargetType='Button' x:Key='DangerBtn' BasedOn='{StaticResource RoundedBtnBase}'>
      <Setter Property='Height' Value='36'/>
      <Setter Property='Padding' Value='16,8'/>
      <Setter Property='FontSize' Value='13'/>
      <Setter Property='FontWeight' Value='Bold'/>
      <Setter Property='Background' Value='#DC2626'/>
      <Setter Property='Foreground' Value='#FFFFFF'/>
      <Setter Property='BorderThickness' Value='0'/>
    </Style>
    <Style TargetType='Button' x:Key='ActionSubBtn' BasedOn='{StaticResource SubBtn}'>
      <Setter Property='Height' Value='36'/>
      <Setter Property='Padding' Value='14,8'/>
      <Setter Property='FontWeight' Value='SemiBold'/>
    </Style>
    <Style TargetType='Button' x:Key='DarkGhostBtn' BasedOn='{StaticResource RoundedBtnBase}'>
      <Setter Property='Height' Value='24'/>
      <Setter Property='Padding' Value='10,2'/>
      <Setter Property='FontSize' Value='11'/>
      <Setter Property='Background' Value='#1E293B'/>
      <Setter Property='Foreground' Value='#CBD5E1'/>
      <Setter Property='BorderBrush' Value='#334155'/>
      <Setter Property='BorderThickness' Value='1'/>
    </Style>
    <Style TargetType='RadioButton' x:Key='ModeCard'>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='RadioButton'>
            <Border x:Name='card' Background='#FFFFFF' BorderBrush='#CBD5E1' BorderThickness='1.5' CornerRadius='9' Padding='12,8'>
              <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsChecked' Value='True'>
                <Setter TargetName='card' Property='Background' Value='#EFF6FF'/>
                <Setter TargetName='card' Property='BorderBrush' Value='#2563EB'/>
              </Trigger>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='card' Property='BorderBrush' Value='#60A5FA'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style TargetType='TabItem' x:Key='PillTabItem'>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Foreground' Value='#475569'/>
      <Setter Property='FontSize' Value='12.5'/>
      <Setter Property='FontWeight' Value='SemiBold'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='TabItem'>
            <Border x:Name='pill' Background='Transparent' CornerRadius='7' Padding='18,7' Margin='2'>
              <ContentPresenter ContentSource='Header' HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsSelected' Value='True'>
                <Setter TargetName='pill' Property='Background' Value='#FFFFFF'/>
                <Setter Property='Foreground' Value='#1D4ED8'/>
                <Setter Property='FontWeight' Value='Bold'/>
              </Trigger>
              <MultiTrigger>
                <MultiTrigger.Conditions>
                  <Condition Property='IsMouseOver' Value='True'/>
                  <Condition Property='IsSelected' Value='False'/>
                </MultiTrigger.Conditions>
                <Setter TargetName='pill' Property='Background' Value='#F1F5F9'/>
              </MultiTrigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style TargetType='TabControl' x:Key='PillTabControl'>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='TabControl'>
            <StackPanel>
              <Border Background='#E2E8F0' CornerRadius='9' Padding='2' HorizontalAlignment='Left' Margin='0,0,0,8'>
                <TabPanel IsItemsHost='True'/>
              </Border>
              <ContentPresenter ContentSource='SelectedContent'/>
            </StackPanel>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </Grid.Resources>

  <Grid.RowDefinitions>
    <RowDefinition Height='Auto'/>
    <RowDefinition Height='Auto'/>
    <RowDefinition Height='Auto'/>
    <RowDefinition Height='Auto'/>
    <RowDefinition Height='Auto'/>
    <RowDefinition Height='*'/>
  </Grid.RowDefinitions>

  <!-- Header -->
  <Grid Grid.Row='0' Margin='2,0,2,10'>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width='*'/>
      <ColumnDefinition Width='Auto'/>
      <ColumnDefinition Width='Auto'/>
      <ColumnDefinition Width='Auto'/>
      <ColumnDefinition Width='Auto'/>
      <ColumnDefinition Width='Auto'/>
      <ColumnDefinition Width='Auto'/>
    </Grid.ColumnDefinitions>
    <StackPanel Orientation='Horizontal' VerticalAlignment='Center'>
      <TextBlock Text='Scrcpy Launcher' FontSize='18' FontWeight='Bold' Foreground='#0F172A'/>
      <Border Background='#E0E7FF' CornerRadius='8' Padding='7,2' Margin='8,0,6,0' VerticalAlignment='Center'>
        <TextBlock Text='v1.5.6' FontSize='11' FontWeight='Bold' Foreground='#3730A3'/>
      </Border>
      <Button x:Name='btnUpdate' Content='새 버전' Style='{StaticResource SoftBlueBtn}' Height='24' Padding='8,2' FontSize='11' FontWeight='Bold' Background='#DCFCE7' BorderBrush='#86EFAC' Foreground='#15803D' Margin='0,0,6,0' Visibility='Collapsed'/>
    </StackPanel>
    <Border x:Name='bdScrcpyStatus' Grid.Column='1' Background='#DCFCE7' CornerRadius='12' Padding='11,5' Margin='0,0,6,0' VerticalAlignment='Center'>
      <TextBlock x:Name='txtScrcpyStatus' Text='scrcpy 확인 중...' FontSize='11' FontWeight='SemiBold' Foreground='#166534'/>
    </Border>
    <Button x:Name='btnDownloadScrcpy' Grid.Column='2' Content='scrcpy 다운로드' Style='{StaticResource SoftRedBtn}' Margin='0,0,5,0' Visibility='Collapsed'/>
    <Button x:Name='btnSetupGuide' Grid.Column='3' Content='연결 · 문제해결 ?' Style='{StaticResource SoftBlueBtn}' Margin='0,0,5,0'/>
    <Button x:Name='btnShortcuts' Grid.Column='4' Content='단축키' Style='{StaticResource SubBtn}' Margin='0,0,5,0'/>
    <Button x:Name='btnSelectFolder' Grid.Column='5' Content='폴더 변경' Style='{StaticResource SubBtn}' Margin='0,0,5,0'/>
    <ComboBox x:Name='cmbLanguage' Grid.Column='6' Width='76' Height='30' FontSize='11.5'>
      <ComboBoxItem Content='한국어' Tag='ko'/>
      <ComboBoxItem Content='English' Tag='en'/>
      <ComboBoxItem Content='日本語' Tag='ja'/>
    </ComboBox>
  </Grid>

  <!-- Control Mode 3-Card Selector -->
  <StackPanel Grid.Row='1' Margin='0,0,0,10'>
    <UniformGrid Columns='3'>
      <RadioButton x:Name='rbModeKeymap' GroupName='CtrlMode' Style='{StaticResource ModeCard}' Margin='0,0,6,0'>
        <StackPanel>
          <TextBlock x:Name='txtModeKmTitle' Text='키보드 + 마우스 (키 매핑)' FontSize='12.5' FontWeight='Bold' Foreground='#0F172A' HorizontalAlignment='Center'/>
          <TextBlock x:Name='txtModeKmSub' Text='WASD · 시점 회전 · 자동 연결' FontSize='10.5' Foreground='#64748B' HorizontalAlignment='Center' Margin='0,2,0,0'/>
        </StackPanel>
      </RadioButton>
      <RadioButton x:Name='rbModeGamepad' GroupName='CtrlMode' Style='{StaticResource ModeCard}' Margin='3,0,3,0'>
        <StackPanel>
          <TextBlock x:Name='txtModeGpTitle' Text='게임패드 (Xbox / PS)' FontSize='12.5' FontWeight='Bold' Foreground='#0F172A' HorizontalAlignment='Center'/>
          <TextBlock x:Name='txtModeGpSub' Text='UHID 컨트롤러 · 우클릭 충돌 방지' FontSize='10.5' Foreground='#64748B' HorizontalAlignment='Center' Margin='0,2,0,0'/>
        </StackPanel>
      </RadioButton>
      <RadioButton x:Name='rbModeNormal' GroupName='CtrlMode' Style='{StaticResource ModeCard}' Margin='6,0,0,0'>
        <StackPanel>
          <TextBlock x:Name='txtModeNmTitle' Text='일반 미러링 (기본)' FontSize='12.5' FontWeight='Bold' Foreground='#0F172A' HorizontalAlignment='Center'/>
          <TextBlock x:Name='txtModeNmSub' Text='일반 터치 · 마우스 · 영상 시청' FontSize='10.5' Foreground='#64748B' HorizontalAlignment='Center' Margin='0,2,0,0'/>
        </StackPanel>
      </RadioButton>
    </UniformGrid>
    <TextBlock x:Name='txtModeSummary' Text='' FontSize='11' Foreground='#1D4ED8' FontWeight='SemiBold' Margin='4,5,0,0'/>
  </StackPanel>

  <!-- Main Segmented Tabs -->
  <TabControl Grid.Row='2' Style='{StaticResource PillTabControl}' Margin='0,0,0,2'>
    <TabItem x:Name='tabConn' Header='연결 및 영상' Style='{StaticResource PillTabItem}'>
      <StackPanel>
  <!-- 1. Device Connection -->
  <Border Style='{StaticResource Card}'>
    <StackPanel>
      <StackPanel Orientation='Horizontal' Margin='0,0,0,9'>
        <Border Style='{StaticResource StepBadge}'><TextBlock Text='1' Style='{StaticResource StepNum}'/></Border>
        <TextBlock x:Name='txtSec1Title' Text='기기 연결 (USB 자동 감지 · 무선 전환 · ADB 관리)' Style='{StaticResource SectionTitle}'/>
      </StackPanel>
      <Grid Margin='0,0,0,6'>
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width='*'/>
          <ColumnDefinition Width='Auto'/>
          <ColumnDefinition Width='Auto'/>
          <ColumnDefinition Width='Auto'/>
          <ColumnDefinition Width='Auto'/>
        </Grid.ColumnDefinitions>
        <ComboBox x:Name='cmbDevices' Grid.Column='0' Margin='0,0,6,0'/>
        <Button x:Name='btnRefreshDevices' Grid.Column='1' Content='새로고침' Style='{StaticResource SubBtn}' Margin='0,0,5,0'/>
        <Button x:Name='btnUsbToWifi' Grid.Column='2' Content='유선 → 무선' Style='{StaticResource SoftBlueBtn}' Margin='0,0,5,0'/>
        <Button x:Name='btnDisconnectWifi' Grid.Column='3' Content='무선 해제' Style='{StaticResource SubBtn}' Margin='0,0,5,0'/>
        <Button x:Name='btnRestartAdb' Grid.Column='4' Content='ADB 재시작' Style='{StaticResource SoftAmberBtn}'/>
      </Grid>

      <Expander x:Name='expWireless' Header='수동 무선 디버깅 페어링 / IP 직접 연결 (케이블 없이 연결할 때)' FontSize='12' Foreground='#475569' Margin='0,2,0,0'>
        <Border Background='#F8FAFC' BorderBrush='#E2E8F0' BorderThickness='1' CornerRadius='8' Padding='10,8' Margin='0,6,0,0'>
          <StackPanel>
            <Grid Margin='0,0,0,6'>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width='125'/>
                <ColumnDefinition Width='*'/>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='95'/>
                <ColumnDefinition Width='Auto'/>
              </Grid.ColumnDefinitions>
              <TextBlock x:Name='lblPairAddr' Grid.Column='0' Text='① 페어링 IP:포트' Style='{StaticResource FieldLabel}'/>
              <TextBox x:Name='txtPairAddr' Grid.Column='1' Margin='0,0,10,0'/>
              <TextBlock x:Name='lblPairCode' Grid.Column='2' Text='6자리 코드' Style='{StaticResource FieldLabel}'/>
              <TextBox x:Name='txtPairCode' Grid.Column='3' Margin='0,0,8,0'/>
              <Button x:Name='btnPair' Grid.Column='4' Content='페어링' Style='{StaticResource SubBtn}' Width='78'/>
            </Grid>
            <Grid>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width='125'/>
                <ColumnDefinition Width='*'/>
                <ColumnDefinition Width='Auto'/>
              </Grid.ColumnDefinitions>
              <TextBlock x:Name='lblWifiAddr' Grid.Column='0' Text='② 무선 디버깅 IP:포트' Style='{StaticResource FieldLabel}'/>
              <TextBox x:Name='txtWifiAddr' Grid.Column='1' Margin='0,0,8,0'/>
              <Button x:Name='btnWifiConnect' Grid.Column='2' Content='무선 연결' Style='{StaticResource SoftBlueBtn}' Width='78'/>
            </Grid>
          </StackPanel>
        </Border>
      </Expander>
    </StackPanel>
  </Border>

  <!-- 2. Video, Performance & Recording -->
  <Border Style='{StaticResource Card}'>
    <StackPanel>
      <StackPanel Orientation='Horizontal' Margin='0,0,0,9'>
        <Border Style='{StaticResource StepBadge}'><TextBlock Text='2' Style='{StaticResource StepNum}'/></Border>
        <TextBlock x:Name='txtSec2Title' Text='영상 · 성능 · 저장 설정' Style='{StaticResource SectionTitle}'/>
      </StackPanel>
      <Grid Margin='0,0,0,8'>
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width='Auto'/>
          <ColumnDefinition Width='125'/>
          <ColumnDefinition Width='Auto'/>
          <ColumnDefinition Width='58'/>
          <ColumnDefinition Width='Auto'/>
          <ColumnDefinition Width='108'/>
          <ColumnDefinition Width='Auto'/>
          <ColumnDefinition Width='88'/>
          <ColumnDefinition Width='Auto'/>
          <ColumnDefinition Width='*'/>
        </Grid.ColumnDefinitions>
        <TextBlock x:Name='lblCodec' Grid.Column='0' Text='코덱' Style='{StaticResource FieldLabel}'/>
        <ComboBox x:Name='cmbCodec' Grid.Column='1' Margin='0,0,10,0'>
          <ComboBoxItem Content='h264 (저지연)' Tag='h264'/>
          <ComboBoxItem Content='h265 (고화질)' Tag='h265'/>
          <ComboBoxItem Content='av1' Tag='av1'/>
        </ComboBox>

        <TextBlock Grid.Column='2' Text='Mbps' Style='{StaticResource FieldLabel}'/>
        <TextBox x:Name='txtBitrate' Grid.Column='3' Margin='0,0,10,0' Text='20'/>

        <TextBlock x:Name='lblMaxSize' Grid.Column='4' Text='최대크기' Style='{StaticResource FieldLabel}'/>
        <ComboBox x:Name='cmbMaxSize' Grid.Column='5' Margin='0,0,10,0' IsEditable='True'>
          <ComboBoxItem Content='0 (제한없음)' Tag='0'/>
          <ComboBoxItem Content='1920' Tag='1920'/>
          <ComboBoxItem Content='1600' Tag='1600'/>
          <ComboBoxItem Content='1280' Tag='1280'/>
        </ComboBox>

        <TextBlock Grid.Column='6' Text='FPS' Style='{StaticResource FieldLabel}'/>
        <ComboBox x:Name='cmbMaxFps' Grid.Column='7' Margin='0,0,10,0' IsEditable='True'>
          <ComboBoxItem Content='0 (자동)' Tag='0'/>
          <ComboBoxItem Content='60' Tag='60'/>
          <ComboBoxItem Content='120' Tag='120'/>
        </ComboBox>

        <TextBlock x:Name='lblRenderer' Grid.Column='8' Text='렌더러' Style='{StaticResource FieldLabel}'/>
        <ComboBox x:Name='cmbRenderer' Grid.Column='9'>
          <ComboBoxItem Content='자동 (D3D11)' Tag='auto'/>
          <ComboBoxItem Content='OpenGL' Tag='opengl'/>
        </ComboBox>
      </Grid>

      <WrapPanel Margin='2,2,0,8'>
        <CheckBox x:Name='chkTurnScreenOff' Content='폰 화면 끄기 (-S)'/>
        <CheckBox x:Name='chkStayAwake' Content='절전 방지 (-w)'/>
        <CheckBox x:Name='chkLowLatency' Content='저지연 인코딩'/>
        <CheckBox x:Name='chkNoAudio' Content='소리 끄기'/>
        <CheckBox x:Name='chkFullscreen' Content='전체화면 시작 (-f)'/>
        <CheckBox x:Name='chkAlwaysOnTop' Content='항상 위에 (실시간)'/>
        <CheckBox x:Name='chkPrintFps' Content='화면 FPS 오버레이'/>
      </WrapPanel>

      <Border Height='1' Background='#F1F5F9' Margin='0,0,0,8'/>

      <Grid>
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width='Auto'/>
          <ColumnDefinition Width='*'/>
          <ColumnDefinition Width='Auto'/>
          <ColumnDefinition Width='Auto'/>
        </Grid.ColumnDefinitions>
        <TextBlock x:Name='lblRecFolder' Grid.Column='0' Text='캡처 · 녹화 저장 폴더' Style='{StaticResource FieldLabel}'/>
        <TextBox x:Name='txtRecordFolder' Grid.Column='1' IsReadOnly='True' Background='#F8FAFC' Foreground='#475569' Margin='0,0,6,0'/>
        <Button x:Name='btnBrowseRecord' Grid.Column='2' Content='폴더 선택' Style='{StaticResource SubBtn}' Margin='0,0,5,0'/>
        <Button x:Name='btnOpenRecord' Grid.Column='3' Content='폴더 열기' Style='{StaticResource SubBtn}'/>
      </Grid>
    </StackPanel>
  </Border>

  <!-- 3. Virtual Display & App Launch -->
  <Border Style='{StaticResource Card}'>
    <StackPanel>
      <StackPanel Orientation='Horizontal' Margin='0,0,0,9'>
        <Border Style='{StaticResource StepBadge}'><TextBlock Text='3' Style='{StaticResource StepNum}'/></Border>
        <TextBlock x:Name='txtSec3Title' Text='가상 디스플레이 · 앱 바로 실행 (16:9 / 21:9 최적화)' Style='{StaticResource SectionTitle}'/>
      </StackPanel>
      <Grid Margin='0,0,0,8'>
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width='Auto'/>
          <ColumnDefinition Width='175'/>
          <ColumnDefinition Width='*'/>
        </Grid.ColumnDefinitions>
        <CheckBox x:Name='chkVirtualDisplay' Grid.Column='0' Content='가상 디스플레이 사용' VerticalAlignment='Center' Margin='0,0,10,0'/>
        <ComboBox x:Name='cmbVdSpec' Grid.Column='1' IsEditable='True' Margin='0,0,16,0'>
          <ComboBoxItem Content='1920x1080/280'/>
          <ComboBoxItem Content='2560x1440/373'/>
          <ComboBoxItem Content='2560x1080/280'/>
          <ComboBoxItem Content='3440x1440/373'/>
        </ComboBox>
        <CheckBox x:Name='chkNoVdDecorations' Grid.Column='2' Content='DeX / 시스템 UI 건너뛰기 (앱 단독 실행)' VerticalAlignment='Center' Margin='0'/>
      </Grid>

      <Grid>
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width='Auto'/>
          <ColumnDefinition Width='165'/>
          <ColumnDefinition Width='*'/>
          <ColumnDefinition Width='Auto'/>
        </Grid.ColumnDefinitions>
        <TextBlock x:Name='lblApp' Grid.Column='0' Text='실행 앱' Style='{StaticResource FieldLabel}'/>
        <Grid Grid.Column='1' Margin='0,0,6,0'>
          <TextBox x:Name='txtAppSearch'/>
          <TextBlock x:Name='txtAppSearchHint' Text='앱 이름 · 패키지 검색...' FontSize='11.5' Foreground='#94A3B8' IsHitTestVisible='False' VerticalAlignment='Center' Margin='9,0,0,0'/>
        </Grid>
        <ComboBox x:Name='cmbApps' Grid.Column='2' Margin='0,0,6,0'/>
        <Button x:Name='btnRefreshApps' Grid.Column='3' Content='앱 불러오기' Style='{StaticResource SubBtn}'/>
      </Grid>
    </StackPanel>
  </Border>
      </StackPanel>
    </TabItem>

    <TabItem x:Name='tabKeymap' Header='키 매핑' Style='{StaticResource PillTabItem}'>
      <StackPanel>
        <!-- Mode Warning Banner (shown when Gamepad or Normal mode is active) -->
        <Border x:Name='bdKmModeWarning' Background='#FFFBEB' BorderBrush='#FCD34D' BorderThickness='1' CornerRadius='8' Padding='11,7' Margin='0,0,0,8' Visibility='Collapsed'>
          <StackPanel>
            <TextBlock x:Name='txtKmModeWarning' Text='현재 키보드+마우스 모드가 아닙니다. 키 매핑 기능은 상단에서 [키보드 + 마우스] 모드를 선택했을 때 작동합니다. (게임패드 버튼 리매핑은 미지원)' FontSize='11.5' Foreground='#92400E' TextWrapping='Wrap'/>
            <CheckBox x:Name='chkGamepadKbAssist' Content='게임패드 모드에서도 키보드 보조 매핑 사용 (모험단 춤 방향키 등 · 마우스 시점 제외)' FontSize='11.5' FontWeight='SemiBold' Foreground='#1E3A8A' Margin='0,5,0,0'/>
          </StackPanel>
        </Border>

        <!-- Profile Card -->
        <Border Style='{StaticResource Card}'>
          <StackPanel>
            <StackPanel Orientation='Horizontal' Margin='0,0,0,9'>
              <Border Style='{StaticResource StepBadge}'><TextBlock Text='1' Style='{StaticResource StepNum}'/></Border>
              <TextBlock x:Name='txtKmSec1' Text='키 매핑 프로파일 (무기별 관리 · LD플레이어 .kmp 호환)' Style='{StaticResource SectionTitle}'/>
            </StackPanel>
            <Grid Margin='0,0,0,6'>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width='*'/>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='Auto'/>
              </Grid.ColumnDefinitions>
              <ComboBox x:Name='cmbKeyProfiles' Grid.Column='0' Margin='0,0,6,0'/>
              <Button x:Name='btnReloadProfiles' Grid.Column='1' Content='새로고침' Style='{StaticResource SubBtn}' Margin='0,0,5,0'/>
              <Button x:Name='btnImportKmp' Grid.Column='2' Content='LD .kmp 불러오기' Style='{StaticResource SoftBlueBtn}' Margin='0,0,5,0'/>
              <Button x:Name='btnSaveProfile' Grid.Column='3' Content='저장' Style='{StaticResource SubBtn}'/>
            </Grid>
            <StackPanel Orientation='Horizontal'>
              <Button x:Name='btnSaveAsProfile' Content='다른 이름으로 저장' Style='{StaticResource SubBtn}' Height='26' Padding='10,3' FontSize='11.5' Margin='0,0,5,0'/>
              <Button x:Name='btnRenameProfile' Content='이름 변경' Style='{StaticResource SubBtn}' Height='26' Padding='10,3' FontSize='11.5' Margin='0,0,5,0'/>
              <Button x:Name='btnDeleteProfile' Content='프로파일 삭제' Style='{StaticResource SoftRedBtn}' Height='26' Padding='10,3' FontSize='11.5' Margin='0,0,10,0'/>
              <Button x:Name='btnExportCode' Content='코드 복사' Style='{StaticResource SoftBlueBtn}' Height='26' Padding='10,3' FontSize='11.5' Margin='0,0,5,0'/>
              <Button x:Name='btnImportCode' Content='코드 불러오기' Style='{StaticResource SoftBlueBtn}' Height='26' Padding='10,3' FontSize='11.5' Margin='0,0,8,0'/>
              <TextBlock x:Name='lblHotkeyHint' Text='게임 중 Ctrl+F1~F9 전환' FontSize='11' Foreground='#64748B' VerticalAlignment='Center'/>
            </StackPanel>
          </StackPanel>
        </Border>

        <!-- Control & Camera Card -->
        <Border Style='{StaticResource Card}'>
          <StackPanel>
            <StackPanel Orientation='Horizontal' Margin='0,0,0,9'>
              <Border Style='{StaticResource StepBadge}'><TextBlock Text='2' Style='{StaticResource StepNum}'/></Border>
              <TextBlock x:Name='txtKmSec2' Text='멀티터치 조작 · 시점 회전 설정' Style='{StaticResource SectionTitle}'/>
            </StackPanel>
            <WrapPanel Margin='2,0,0,8'>
              <CheckBox x:Name='chkKmJoy' Content='WASD 이동'/>
              <CheckBox x:Name='chkKmCam' Content='마우스 시점 회전'/>
              <CheckBox x:Name='chkKmLeft' Content='좌클릭 공격'/>
              <CheckBox x:Name='chkKmRight' Content='우클릭 보조'/>
              <CheckBox x:Name='chkKmRightWheel' Content='우클릭 휠드래그'/>
              <CheckBox x:Name='chkKmHints' Content='화면 키 힌트' IsChecked='True'/>
            </WrapPanel>
            <Grid Margin='0,0,0,10'>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='95'/>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='*'/>
                <ColumnDefinition Width='36'/>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='*'/>
                <ColumnDefinition Width='36'/>
              </Grid.ColumnDefinitions>
              <TextBlock x:Name='lblCamToggle' Grid.Column='0' Text='시점 토글' Style='{StaticResource FieldLabel}'/>
              <ComboBox x:Name='cmbKmToggleKey' Grid.Column='1' Margin='0,0,12,0'>
                <ComboBoxItem Content='Alt' Tag='18'/>
                <ComboBoxItem Content='` (~)' Tag='192'/>
                <ComboBoxItem Content='Tab' Tag='9'/>
              </ComboBox>
              <TextBlock x:Name='lblSensX' Grid.Column='2' Text='감도 X' Style='{StaticResource FieldLabel}'/>
              <Slider x:Name='sldKmSensX' Grid.Column='3' Minimum='0.1' Maximum='3.0' Value='1.0' TickFrequency='0.05' IsSnapToTickEnabled='True' VerticalAlignment='Center' Margin='0,0,4,0'/>
              <TextBlock x:Name='txtKmSensXVal' Grid.Column='4' Text='1.00' FontSize='11.5' FontWeight='SemiBold' Foreground='#1E293B' VerticalAlignment='Center' Margin='0,0,10,0'/>
              <TextBlock x:Name='lblSensY' Grid.Column='5' Text='감도 Y' Style='{StaticResource FieldLabel}'/>
              <Slider x:Name='sldKmSensY' Grid.Column='6' Minimum='0.1' Maximum='3.0' Value='1.0' TickFrequency='0.05' IsSnapToTickEnabled='True' VerticalAlignment='Center' Margin='0,0,4,0'/>
              <TextBlock x:Name='txtKmSensYVal' Grid.Column='7' Text='1.00' FontSize='11.5' FontWeight='SemiBold' Foreground='#1E293B' VerticalAlignment='Center'/>
            </Grid>
            <Grid Margin='0,0,0,10'>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='*'/>
                <ColumnDefinition Width='40'/>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='*'/>
                <ColumnDefinition Width='40'/>
              </Grid.ColumnDefinitions>
              <TextBlock x:Name='lblHintOp' Grid.Column='0' Text='힌트 투명도' Style='{StaticResource FieldLabel}'/>
              <Slider x:Name='sldHintOpacity' Grid.Column='1' Minimum='0.25' Maximum='1.0' Value='1.0' TickFrequency='0.05' IsSnapToTickEnabled='True' VerticalAlignment='Center' Margin='0,0,4,0'/>
              <TextBlock x:Name='txtHintOpacityVal' Grid.Column='2' Text='100%' FontSize='11.5' FontWeight='SemiBold' Foreground='#1E293B' VerticalAlignment='Center' Margin='0,0,12,0'/>
              <TextBlock x:Name='lblHintSc' Grid.Column='3' Text='힌트 크기' Style='{StaticResource FieldLabel}'/>
              <Slider x:Name='sldHintScale' Grid.Column='4' Minimum='0.7' Maximum='1.5' Value='1.0' TickFrequency='0.05' IsSnapToTickEnabled='True' VerticalAlignment='Center' Margin='0,0,4,0'/>
              <TextBlock x:Name='txtHintScaleVal' Grid.Column='5' Text='100%' FontSize='11.5' FontWeight='SemiBold' Foreground='#1E293B' VerticalAlignment='Center'/>
            </Grid>
            <StackPanel Orientation='Horizontal'>
              <Button x:Name='btnKmEditOverlay' Content='화면에서 키 배치' Style='{StaticResource SoftBlueBtn}' Height='32' Padding='16,6' Margin='0,0,8,0'/>
              <Button x:Name='btnKmStart' Content='수동 재연결' Style='{StaticResource SubBtn}' Height='32' Padding='12,6' Margin='0,0,6,0'/>
              <Button x:Name='btnKmStop' Content='매핑 일시중지' Style='{StaticResource SoftRedBtn}' Height='32' Padding='12,6' IsEnabled='False'/>
            </StackPanel>
          </StackPanel>
        </Border>

        <!-- Mapped Keys List Card -->
        <Border Style='{StaticResource Card}'>
          <StackPanel>
            <StackPanel Orientation='Horizontal' Margin='0,0,0,8'>
              <Border Style='{StaticResource StepBadge}'><TextBlock Text='3' Style='{StaticResource StepNum}'/></Border>
              <TextBlock x:Name='txtKmSec3' Text='매핑된 액션 키 목록 (누름/홀드 멀티터치 지원)' Style='{StaticResource SectionTitle}'/>
            </StackPanel>
            <ListBox x:Name='lstKmKeys' Height='115' BorderBrush='#CBD5E1' BorderThickness='1' FontSize='12' Margin='0,0,0,8'/>
            <Grid>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width='115'/>
                <ColumnDefinition Width='*'/>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='Auto'/>
              </Grid.ColumnDefinitions>
              <TextBox x:Name='txtKmNewKey' Grid.Column='0' IsReadOnly='True' Text='클릭 후 키 누름' Foreground='#64748B' Margin='0,0,6,0'/>
              <TextBox x:Name='txtKmNewLabel' Grid.Column='1' Margin='0,0,8,0'/>
              <CheckBox x:Name='chkKmNewWheel' Grid.Column='2' Content='휠 드래그' VerticalAlignment='Center' Margin='0,0,8,0'/>
              <Button x:Name='btnKmAddKey' Grid.Column='3' Content='+ 키 추가' Style='{StaticResource SubBtn}' Margin='0,0,5,0'/>
              <Button x:Name='btnKmDelKey' Grid.Column='4' Content='선택 삭제' Style='{StaticResource SoftRedBtn}'/>
            </Grid>
          </StackPanel>
        </Border>
      </StackPanel>
    </TabItem>

    <TabItem x:Name='tabQuest' Header='퀘스트 플래너' Style='{StaticResource PillTabItem}'>
      <StackPanel>
        <!-- Quest Scan Controls & Top Recommendation -->
        <Border Style='{StaticResource Card}'>
          <StackPanel>
            <StackPanel Orientation='Horizontal' Margin='0,0,0,8'>
              <Border Style='{StaticResource StepBadge}'><TextBlock Text='1' Style='{StaticResource StepNum}'/></Border>
              <TextBlock Text='퀘스트 수집 (게임에서 퀘스트 클릭 후 [F8] 눌러 추가 · 둥지 먼저 비우기 → 필드 중복 클리어)' Style='{StaticResource SectionTitle}'/>
            </StackPanel>
            <Grid Margin='0,0,0,8'>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='*'/>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='Auto'/>
              </Grid.ColumnDefinitions>
              <Button x:Name='btnQuestScanOne' Grid.Column='0' Content='+ 수집 (F8)' Style='{StaticResource SoftBlueBtn}' Margin='0,0,5,0'/>
              <Button x:Name='btnQuestUndo' Grid.Column='1' Content='↩ 취소' Style='{StaticResource SubBtn}' Margin='0,0,5,0'/>
              <Button x:Name='btnQuestSpeciesGuide' Grid.Column='2' Content='종족 도감 ?' Style='{StaticResource SubBtn}' Margin='0,0,5,0'/>
              <Border Grid.Column='4' Background='#ECFDF5' BorderBrush='#A7F3D0' BorderThickness='1' CornerRadius='6' Padding='8,4' Margin='0,0,4,0' VerticalAlignment='Center' ToolTip='매일 한국시간 오전 5시에 자동 초기화됩니다.'>
                <TextBlock x:Name='txtQuestDaily' Text='오늘 완료: 0건' FontSize='11.5' FontWeight='Bold' Foreground='#047857'/>
              </Border>
              <Button x:Name='btnQuestResetDaily' Grid.Column='5' Content='리셋' Style='{StaticResource SubBtn}' Padding='7,4' Margin='0,0,6,0' ToolTip='오늘 완료 카운터를 0으로 초기화합니다.'/>
              <Border Grid.Column='6' Background='#EFF6FF' BorderBrush='#BFDBFE' BorderThickness='1' CornerRadius='6' Padding='8,4' Margin='0,0,5,0' VerticalAlignment='Center'>
                <TextBlock x:Name='txtQuestCount' Text='보유: 0 / 10' FontSize='11.5' FontWeight='Bold' Foreground='#1D4ED8'/>
              </Border>
              <Button x:Name='btnQuestClear' Grid.Column='7' Content='비우기' Style='{StaticResource SoftRedBtn}' Padding='9,4'/>
            </Grid>
            <Border Background='#F0FDF4' BorderBrush='#86EFAC' BorderThickness='1' CornerRadius='7' Padding='10,6'>
              <TextBlock x:Name='txtQuestBanner' Text='게임에서 퀘스트를 클릭한 뒤 [F8]을 눌러 수집하세요. (최대 10개)' FontSize='12' FontWeight='SemiBold' Foreground='#166534' TextWrapping='Wrap'/>
            </Border>
          </StackPanel>
        </Border>

        <!-- Simultaneous Clear Plans (with 1-click group & individual clear buttons) -->
        <Border Style='{StaticResource Card}'>
          <StackPanel>
            <StackPanel Orientation='Horizontal' Margin='0,0,0,8'>
              <Border Style='{StaticResource StepBadge}'><TextBlock Text='2' Style='{StaticResource StepNum}'/></Border>
              <TextBlock Text='동시 클리어 추천 조합 (클리어한 조합이나 개별 항목의 [✓]를 눌러 비우고 새 퀘스트만 추가하세요)' Style='{StaticResource SectionTitle}'/>
            </StackPanel>
            <ScrollViewer Height='325' VerticalScrollBarVisibility='Auto'>
              <StackPanel x:Name='pnlQuestPlans'>
                <TextBlock Text='아직 수집된 퀘스트가 없습니다. 게임에서 퀘스트를 선택한 뒤 [F8]을 눌러 수집하세요.' FontSize='12' Foreground='#64748B' TextWrapping='Wrap' Margin='4,12'/>
              </StackPanel>
            </ScrollViewer>
          </StackPanel>
        </Border>
      </StackPanel>
    </TabItem>
  </TabControl>

  <!-- Dynamic Option Guide -->
  <Border Grid.Row='3' Background='#EFF6FF' BorderBrush='#BFDBFE' BorderThickness='1' CornerRadius='8' Padding='12,7' Margin='0,0,0,9'>
    <DockPanel>
      <Border Background='#2563EB' CornerRadius='4' Padding='6,2' Margin='0,0,9,0' VerticalAlignment='Center' DockPanel.Dock='Left'>
        <TextBlock x:Name='txtGuideTitle' Text='옵션 가이드' FontSize='11' FontWeight='Bold' Foreground='#FFFFFF'/>
      </Border>
      <TextBlock x:Name='txtGuideBody' Text='각 옵션 위에 마우스를 올리면 언제 켜고 끄면 좋은지 설명이 표시됩니다.' FontSize='12' Foreground='#1E3A8A' VerticalAlignment='Center' TextWrapping='Wrap'/>
    </DockPanel>
  </Border>

  <!-- Action Buttons -->
  <Grid Grid.Row='4' Margin='0,0,0,9'>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width='Auto'/>
      <ColumnDefinition Width='Auto'/>
      <ColumnDefinition Width='*'/>
      <ColumnDefinition Width='Auto'/>
      <ColumnDefinition Width='Auto'/>
    </Grid.ColumnDefinitions>
    <Button x:Name='btnStart' Grid.Column='0' Content='▶  키마 미러링 시작' Style='{StaticResource PrimaryBtn}' Margin='0,0,6,0'/>
    <Button x:Name='btnStop' Grid.Column='1' Content='■  중지' Style='{StaticResource DangerBtn}' IsEnabled='False'/>
    <Button x:Name='btnScreenshot' Grid.Column='3' Content='화면 캡처' Style='{StaticResource ActionSubBtn}' Margin='0,0,6,0'/>
    <Button x:Name='btnRecordToggle' Grid.Column='4' Content='● 녹화 시작' Style='{StaticResource ActionSubBtn}'/>
  </Grid>

  <!-- Live Console Log -->
  <Border Grid.Row='5' Background='#0F172A' CornerRadius='10' Padding='10,8,10,10'>
    <Grid>
      <Grid.RowDefinitions>
        <RowDefinition Height='Auto'/>
        <RowDefinition Height='*'/>
      </Grid.RowDefinitions>
      <Grid Grid.Row='0' Margin='2,0,2,6'>
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width='*'/>
          <ColumnDefinition Width='Auto'/>
          <ColumnDefinition Width='Auto'/>
          <ColumnDefinition Width='Auto'/>
        </Grid.ColumnDefinitions>
        <TextBlock x:Name='lblLog' Grid.Column='0' Text='실시간 로그' FontSize='11.5' FontWeight='SemiBold' Foreground='#94A3B8' VerticalAlignment='Center'/>
        <Border x:Name='bdFpsBadge' Grid.Column='1' Background='#1E293B' BorderBrush='#334155' BorderThickness='1' CornerRadius='10' Padding='9,2' Margin='0,0,6,0' VerticalAlignment='Center' Visibility='Collapsed'>
          <TextBlock x:Name='txtFpsBadge' Text='FPS: --' FontSize='11' FontWeight='Bold' Foreground='#4ADE80'/>
        </Border>
        <Button x:Name='btnCopyCmd' Grid.Column='2' Content='명령어 복사' Style='{StaticResource DarkGhostBtn}' Margin='0,0,5,0'/>
        <Button x:Name='btnClearLog' Grid.Column='3' Content='지우기' Style='{StaticResource DarkGhostBtn}'/>
      </Grid>
      <TextBox x:Name='txtLog'
               Grid.Row='1'
               IsReadOnly='True'
               Background='Transparent'
               Foreground='#E2E8F0'
               BorderThickness='0'
               Height='Auto'
               Padding='0'
               FontFamily='Consolas, Malgun Gothic'
               FontSize='11.5'
               TextWrapping='Wrap'
               VerticalContentAlignment='Top'
               VerticalScrollBarVisibility='Auto'
               AcceptsReturn='True'>
        <TextBox.Template>
          <ControlTemplate TargetType='TextBox'>
            <ScrollViewer x:Name='PART_ContentHost'/>
          </ControlTemplate>
        </TextBox.Template>
      </TextBox>
    </Grid>
  </Border>
</Grid>";

            return (UIElement)XamlReader.Parse(xaml);
        }

        private void BindControls()
        {
            var root = (FrameworkElement)Content;

            _txtScrcpyStatus = (TextBlock)root.FindName("txtScrcpyStatus");
            _bdScrcpyStatus = (Border)root.FindName("bdScrcpyStatus");
            _btnUpdate = (Button)root.FindName("btnUpdate");
            _btnDownloadScrcpy = (Button)root.FindName("btnDownloadScrcpy");
            _btnSetupGuide = (Button)root.FindName("btnSetupGuide");
            _btnShortcuts = (Button)root.FindName("btnShortcuts");
            _btnSelectFolder = (Button)root.FindName("btnSelectFolder");
            _cmbLanguage = (ComboBox)root.FindName("cmbLanguage");

            _cmbDevices = (ComboBox)root.FindName("cmbDevices");
            _btnRefreshDevices = (Button)root.FindName("btnRefreshDevices");
            _btnUsbToWifi = (Button)root.FindName("btnUsbToWifi");
            _btnDisconnectWifi = (Button)root.FindName("btnDisconnectWifi");
            _btnRestartAdb = (Button)root.FindName("btnRestartAdb");
            _txtPairAddr = (TextBox)root.FindName("txtPairAddr");
            _txtPairCode = (TextBox)root.FindName("txtPairCode");
            _btnPair = (Button)root.FindName("btnPair");
            _txtWifiAddr = (TextBox)root.FindName("txtWifiAddr");
            _btnWifiConnect = (Button)root.FindName("btnWifiConnect");

            _cmbCodec = (ComboBox)root.FindName("cmbCodec");
            _txtBitrate = (TextBox)root.FindName("txtBitrate");
            _cmbMaxSize = (ComboBox)root.FindName("cmbMaxSize");
            _cmbMaxFps = (ComboBox)root.FindName("cmbMaxFps");
            _cmbRenderer = (ComboBox)root.FindName("cmbRenderer");

            _rbModeKeymap = (RadioButton)root.FindName("rbModeKeymap");
            _rbModeGamepad = (RadioButton)root.FindName("rbModeGamepad");
            _rbModeNormal = (RadioButton)root.FindName("rbModeNormal");
            _txtModeSummary = (TextBlock)root.FindName("txtModeSummary");

            _chkTurnScreenOff = (CheckBox)root.FindName("chkTurnScreenOff");
            _chkStayAwake = (CheckBox)root.FindName("chkStayAwake");
            _chkLowLatency = (CheckBox)root.FindName("chkLowLatency");
            _chkNoAudio = (CheckBox)root.FindName("chkNoAudio");
            _chkFullscreen = (CheckBox)root.FindName("chkFullscreen");
            _chkAlwaysOnTop = (CheckBox)root.FindName("chkAlwaysOnTop");
            _chkPrintFps = (CheckBox)root.FindName("chkPrintFps");

            _txtRecordFolder = (TextBox)root.FindName("txtRecordFolder");
            _btnBrowseRecord = (Button)root.FindName("btnBrowseRecord");
            _btnOpenRecord = (Button)root.FindName("btnOpenRecord");

            _chkVirtualDisplay = (CheckBox)root.FindName("chkVirtualDisplay");
            _cmbVdSpec = (ComboBox)root.FindName("cmbVdSpec");
            _chkNoVdDecorations = (CheckBox)root.FindName("chkNoVdDecorations");
            _txtAppSearch = (TextBox)root.FindName("txtAppSearch");
            _cmbApps = (ComboBox)root.FindName("cmbApps");
            _btnRefreshApps = (Button)root.FindName("btnRefreshApps");

            _txtGuideTitle = (TextBlock)root.FindName("txtGuideTitle");
            _txtGuideBody = (TextBlock)root.FindName("txtGuideBody");
            _btnCopyCmd = (Button)root.FindName("btnCopyCmd");

            _btnStart = (Button)root.FindName("btnStart");
            _btnStop = (Button)root.FindName("btnStop");
            _btnScreenshot = (Button)root.FindName("btnScreenshot");
            _btnRecordToggle = (Button)root.FindName("btnRecordToggle");
            _btnClearLog = (Button)root.FindName("btnClearLog");
            _txtFpsBadge = (TextBlock)root.FindName("txtFpsBadge");
            _bdFpsBadge = (Border)root.FindName("bdFpsBadge");
            _txtLog = (TextBox)root.FindName("txtLog");

            // Keymap controls
            _bdKmModeWarning = (Border)root.FindName("bdKmModeWarning");
            _txtKmModeWarning = (TextBlock)root.FindName("txtKmModeWarning");
            _chkGamepadKbAssist = (CheckBox)root.FindName("chkGamepadKbAssist");
            _cmbKeyProfiles = (ComboBox)root.FindName("cmbKeyProfiles");
            _btnImportKmp = (Button)root.FindName("btnImportKmp");
            _btnSaveProfile = (Button)root.FindName("btnSaveProfile");
            _btnSaveAsProfile = (Button)root.FindName("btnSaveAsProfile");
            _btnRenameProfile = (Button)root.FindName("btnRenameProfile");
            _btnDeleteProfile = (Button)root.FindName("btnDeleteProfile");
            _btnReloadProfiles = (Button)root.FindName("btnReloadProfiles");
            _btnExportCode = (Button)root.FindName("btnExportCode");
            _btnImportCode = (Button)root.FindName("btnImportCode");
            _chkKmJoy = (CheckBox)root.FindName("chkKmJoy");
            _chkKmCam = (CheckBox)root.FindName("chkKmCam");
            _chkKmLeft = (CheckBox)root.FindName("chkKmLeft");
            _chkKmRight = (CheckBox)root.FindName("chkKmRight");
            _chkKmRightWheel = (CheckBox)root.FindName("chkKmRightWheel");
            _chkKmHints = (CheckBox)root.FindName("chkKmHints");
            _cmbKmToggleKey = (ComboBox)root.FindName("cmbKmToggleKey");
            _sldKmSensX = (Slider)root.FindName("sldKmSensX");
            _txtKmSensXVal = (TextBlock)root.FindName("txtKmSensXVal");
            _sldKmSensY = (Slider)root.FindName("sldKmSensY");
            _txtKmSensYVal = (TextBlock)root.FindName("txtKmSensYVal");
            _sldHintOpacity = (Slider)root.FindName("sldHintOpacity");
            _txtHintOpacityVal = (TextBlock)root.FindName("txtHintOpacityVal");
            _sldHintScale = (Slider)root.FindName("sldHintScale");
            _txtHintScaleVal = (TextBlock)root.FindName("txtHintScaleVal");
            _btnKmStart = (Button)root.FindName("btnKmStart");
            _btnKmStop = (Button)root.FindName("btnKmStop");
            _btnKmEditOverlay = (Button)root.FindName("btnKmEditOverlay");
            _lstKmKeys = (ListBox)root.FindName("lstKmKeys");
            _txtKmNewKey = (TextBox)root.FindName("txtKmNewKey");
            _txtKmNewLabel = (TextBox)root.FindName("txtKmNewLabel");
            _chkKmNewWheel = (CheckBox)root.FindName("chkKmNewWheel");
            _btnKmAddKey = (Button)root.FindName("btnKmAddKey");
            _btnKmDelKey = (Button)root.FindName("btnKmDelKey");

            // Quest Planner controls
            _btnQuestScanOne = (Button)root.FindName("btnQuestScanOne");
            _btnQuestUndo = (Button)root.FindName("btnQuestUndo");
            _btnQuestSpeciesGuide = (Button)root.FindName("btnQuestSpeciesGuide");
            _btnQuestResetDaily = (Button)root.FindName("btnQuestResetDaily");
            _btnQuestClear = (Button)root.FindName("btnQuestClear");
            _txtQuestDaily = (TextBlock)root.FindName("txtQuestDaily");
            _txtQuestCount = (TextBlock)root.FindName("txtQuestCount");
            _txtQuestBanner = (TextBlock)root.FindName("txtQuestBanner");
            _pnlQuestPlans = (StackPanel)root.FindName("pnlQuestPlans");

            // Interactive guides + tooltips (generic phrasing for public distribution)
            AttachGuide(_btnDownloadScrcpy, "scrcpy 공식 다운로드", "공식 GitHub Releases 페이지에서 최신 Windows용 scrcpy zip 파일을 받아 압축을 푼 뒤, 이 실행 파일을 같은 폴더에 넣어주세요.");
            AttachGuide(_btnSetupGuide, "스마트폰 최초 연결 방법", "개발자 옵션 활성화, USB 디버깅 켜기, 무선 디버깅 연결 방법을 처음 사용하는 분도 따라할 수 있게 단계별로 안내합니다.");
            AttachGuide(_btnShortcuts, "실행 중 단축키 안내", "전체화면 전환(Alt+F), 폰 화면 끄기/켜기(Alt+O / Alt+Shift+O), FPS 표시(Alt+I) 등 유용한 단축키를 확인합니다.");
            AttachGuide(_btnUsbToWifi, "유선 → 무선 전환", "USB로 연결된 상태에서 클릭하면 폰의 Wi-Fi IP를 자동 조회해 무선 연결로 바꿔줍니다. 완료 후 케이블을 뽑고 충전기를 연결할 수 있습니다.");
            AttachGuide(_btnDisconnectWifi, "무선 연결 해제", "현재 연결된 무선(Wi-Fi) ADB 기기 연결을 끊습니다. (선택된 무선 기기 또는 전체 무선 연결 해제)");
            AttachGuide(_btnRestartAdb, "ADB 서버 재시작", "기기가 offline 또는 unauthorized 상태로 멈추거나 인식이 꼬였을 때 ADB 서버를 깔끔하게 재시작합니다.");
            AttachGuide(_cmbCodec, "비디오 코덱", "H.264: USB 유선·실시간 게임에 가장 빠르고 지연이 적음. / H.265: Wi-Fi 무선이나 낮은 비트레이트에서 화질이 더 선명함.");
            AttachGuide(_txtBitrate, "비트레이트 (Mbps)", "영상 전송 대역폭입니다. 1080p 유선은 15~25Mbps, 무선은 8~16Mbps가 적당합니다. 너무 높이면 인코딩 발열·지연이 늘어납니다.");
            AttachGuide(_cmbMaxSize, "최대 크기 (-m)", "일반 미러링 시 화면 긴 변의 최대 픽셀을 제한합니다. 가상 디스플레이를 쓰면 무시됩니다. 고해상도 기기에서 버벅일 때 1920이나 1600으로 줄여보세요.");
            AttachGuide(_cmbMaxFps, "최대 FPS", "0(자동)을 권장합니다. 일부 안드로이드 기기에서는 60으로 제한하면 오히려 프레임이 낮아질 수 있으니 문제 시 0으로 두세요.");
            AttachGuide(_cmbRenderer, "PC 렌더러", "자동(Direct3D11)은 PC GPU 하드웨어 가속(D3D11VA)을 사용해 가장 빠릅니다. 화면이 깨지거나 안 나올 때만 OpenGL을 시도하세요.");
            AttachGuide(_rbModeKeymap, "키보드 + 마우스 (키 매핑) 모드", "scrcpy 자체 입력 충돌을 자동 차단하고, 미러링 시작 즉시 WASD · 마우스 시점 회전 · 스킬 키 매핑과 일반 클릭 터치를 자동 연결합니다.");
            AttachGuide(_rbModeGamepad, "게임패드 (Xbox / PS) 모드", "PC에 연결된 물리 컨트롤러를 UHID 게임패드(-G)로 인식시키며, 우클릭 뒤로가기 충돌을 방지합니다.");
            AttachGuide(_rbModeNormal, "일반 미러링 모드", "패드나 키 매핑 없이 스마트폰 화면을 일반 마우스와 키보드로 편하게 조작할 때 사용합니다.");
            AttachGuide(_chkTurnScreenOff, "폰 화면 끄기 (-S)", "미러링 중 스마트폰 화면을 꺼서 발열과 배터리 소모를 줄입니다. 간혹 기기 절전 때문에 프레임이 떨어지면 체크를 해제해 보세요.");
            AttachGuide(_chkStayAwake, "절전 방지 (-w)", "연결 중 스마트폰이 자동으로 잠기지 않도록 유지합니다.");
            AttachGuide(_chkLowLatency, "저지연 인코딩", "하드웨어 인코더에 low-latency=1 옵션을 전달해 PC 화면이 반박자 밀리는 현상을 줄여줍니다.");
            AttachGuide(_chkNoAudio, "소리 끄기", "오디오 전송을 끕니다. 블루투스 이어폰을 폰에 직접 연결해 쓰거나, 오디오 버퍼로 인한 지연 여부를 테스트할 때 유용합니다.");
            AttachGuide(_chkFullscreen, "전체화면 시작 (-f)", "실행 즉시 PC 모니터 전체화면으로 띄웁니다. 실행 중에도 Alt + F 키로 언제든 전환할 수 있습니다.");
            AttachGuide(_chkAlwaysOnTop, "항상 위에 (실시간)", "미러링 창을 항상 다른 창 위에 고정합니다. 미러링 도중에도 체크를 켜거나 끄면 즉시 반영됩니다.");
            AttachGuide(_chkPrintFps, "화면 FPS 오버레이", "미러링 창 좌측 상단에 클릭을 방해하지 않는 투명 FPS 표시기를 띄웁니다. FPS 수치는 로그창을 어지럽히지 않고 오버레이와 배지에만 표시됩니다.");
            AttachGuide(_chkGamepadKbAssist, "게임패드 모드 키보드 보조 매핑", "게임패드 모드에서도 키보드 액션 매핑(예: 모험단 춤 방향키)을 함께 사용할 수 있습니다. 마우스 시점과 클릭은 패드/일반 조작을 방해하지 않도록 제외됩니다.");
            AttachGuide(_btnScreenshot, "화면 캡처 (.png)", "연결된 기기의 현재 화면(가상 디스플레이 실행 중이면 해당 화면 포함)을 PNG 이미지로 즉시 저장합니다.");
            AttachGuide(_btnRecordToggle, "화면 녹화 시작 / 중지 (.mp4)", "클릭하면 미러링을 끊지 않고 즉시 백그라운드 녹화를 시작합니다. 다시 눌러 녹화를 중지하면 지정 폴더에 MP4 파일로 저장됩니다.");
            AttachGuide(_chkVirtualDisplay, "가상 디스플레이 (--new-display)", "폴더블·태블릿 등 화면 비율이 다른 기기에서도 PC 모니터에 꽉 차는 16:9(1920x1080 등) 가상 화면을 별도로 띄웁니다.");
            AttachGuide(_cmbVdSpec, "가상 디스플레이 해상도", "1920x1080이 가장 부드럽고, 2560x1440은 선명하지만 기기 GPU 부하가 큽니다. 뒤에 /320 처럼 DPI를 붙일 수 있습니다.");
            AttachGuide(_chkNoVdDecorations, "DeX / 시스템 UI 건너뛰기", "데스크톱 UI(예: 삼성 DeX) 대신 선택한 앱만 16:9 화면으로 단독 실행합니다. 반드시 아래에서 '실행 앱'을 선택해야 검은 화면이 안 뜹니다.");
            AttachGuide(_cmbApps, "앱 바로 실행 (--start-app)", "미러링 시작과 동시에 선택한 앱을 바로 켭니다. 기기가 연결되면 설치된 앱 목록을 자동으로 불러옵니다.");
            AttachGuide(_txtAppSearch, "앱 검색", "앱 이름이나 패키지명의 일부를 입력하면 오른쪽 목록이 즉시 필터링됩니다. (예: 게임 이름, 패키지명)");

            // Events
            _btnUpdate.Click += BtnUpdate_Click;
            _btnDownloadScrcpy.Click += (s, e) => OpenUrl(ScrcpyReleasesUrl);
            _btnSetupGuide.Click += (s, e) => ShowSetupGuideDialog();
            _btnShortcuts.Click += (s, e) => ShowShortcutsDialog();
            _btnSelectFolder.Click += BtnSelectFolder_Click;
            _cmbLanguage.SelectionChanged += (s, e) =>
            {
                if (_isInitializing) return;
                string lang = GetSelectedTag(_cmbLanguage, "ko");
                Loc.SetLanguage(lang);
                _settings.Language = lang;
                _settings.Save();
                ApplyLanguage();
            };
            _btnRefreshDevices.Click += async (s, e) => await RefreshDevicesAsync(false);
            _btnUsbToWifi.Click += BtnUsbToWifi_Click;
            _btnDisconnectWifi.Click += BtnDisconnectWifi_Click;
            _btnRestartAdb.Click += BtnRestartAdb_Click;
            _btnPair.Click += BtnPair_Click;
            _btnWifiConnect.Click += BtnWifiConnect_Click;
            _btnBrowseRecord.Click += BtnBrowseRecord_Click;
            _btnOpenRecord.Click += BtnOpenRecord_Click;
            _btnRefreshApps.Click += async (s, e) => await LoadAppsAsync(true);
            _btnCopyCmd.Click += (s, e) => { Clipboard.SetText("scrcpy.exe " + BuildScrcpyArguments(null)); AppendLog("[안내] 현재 설정의 실행 명령어가 클립보드에 복사되었습니다."); };
            _btnStart.Click += BtnStart_Click;
            _btnStop.Click += (s, e) => StopScrcpy();
            _btnScreenshot.Click += BtnScreenshot_Click;
            _btnRecordToggle.Click += BtnRecordToggle_Click;
            _btnClearLog.Click += (s, e) => _txtLog.Clear();

            // Keymap events
            AttachGuide(_btnImportKmp, "LDPlayer .kmp 불러오기", "LD플레이어에서 사용하던 .kmp 키매핑 파일을 그대로 불러와 적용합니다.");
            AttachGuide(_btnKmStart, "키 매핑 시작", "scrcpy-server 멀티터치 제어 소켓을 연결해 WASD 이동, 마우스 시점 회전, 스킬 키를 1~3ms 저지연으로 입력합니다. 먼저 미러링을 시작해 주세요.");
            AttachGuide(_btnKmEditOverlay, "화면에서 키 배치", "실행 중인 미러링 화면 위에 반투명 편집 오버레이를 띄워 드래그 앤 드롭으로 키 위치를 직접 맞춥니다.");

            _cmbKeyProfiles.SelectionChanged += (s, e) =>
            {
                var p = _cmbKeyProfiles.SelectedItem as KeymapProfile;
                if (p != null)
                {
                    _activeProfile = p;
                    ApplyProfileToUi();
                    _hookEngine.SetProfile(_activeProfile);
                    if (_keymapOverlay != null) _keymapOverlay.SetProfile(_activeProfile);
                    UpdateModeSummary();
                    UpdateUiStates();
                }
            };
            _btnImportKmp.Click += BtnImportKmp_Click;
            _btnSaveProfile.Click += BtnSaveProfile_Click;
            _btnSaveAsProfile.Click += BtnSaveAsProfile_Click;
            _btnRenameProfile.Click += BtnRenameProfile_Click;
            _btnDeleteProfile.Click += BtnDeleteProfile_Click;
            _btnReloadProfiles.Click += (s, e) => { LoadKeymapProfiles(_activeProfile != null ? _activeProfile.Name : null); AppendLog("[키 매핑] 프로파일 목록을 새로고침했습니다."); };
            _btnExportCode.Click += BtnExportCode_Click;
            _btnImportCode.Click += BtnImportCode_Click;
            AttachGuide(_btnExportCode, "프리셋 코드 복사", "현재 프로파일을 초압축 한 줄 공유 코드(SLKM2:...)로 클립보드에 복사합니다. 댓글이나 커뮤니티에 바로 공유할 수 있습니다.");
            AttachGuide(_btnImportCode, "프리셋 코드 불러오기", "다른 사람이 공유한 프리셋 코드(SLKM2:... / SLKM1:...)를 붙여넣어 내 프로파일 목록에 즉시 추가합니다.");
            _btnKmStart.Click += BtnKmStart_Click;
            _btnKmStop.Click += (s, e) => StopKeymap();
            _btnKmEditOverlay.Click += BtnKmEditOverlay_Click;
            _btnKmAddKey.Click += BtnKmAddKey_Click;
            _btnKmDelKey.Click += BtnKmDelKey_Click;

            _btnQuestScanOne.Click += async (s, e) => await ScanCurrentQuestAsync(true);
            _btnQuestUndo.Click += (s, e) =>
            {
                if (_scannedQuests.Count == 0) return;
                var last = _scannedQuests[_scannedQuests.Count - 1];
                _scannedQuests.RemoveAt(_scannedQuests.Count - 1);
                RefreshQuestPlannerUi();
                AppendLog("[퀘스트] 마지막 수집 취소: " + last.ObjectiveText);
            };
            _btnQuestSpeciesGuide.Click += (s, e) =>
            {
                MessageBox.Show(this, QuestOcrService.GetSpeciesGuideText(), "종족별 몬스터 & 약점 도감", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            _btnQuestClear.Click += (s, e) =>
            {
                _scannedQuests.Clear();
                RefreshQuestPlannerUi();
                AppendLog("[퀘스트] 보유 퀘스트 목록을 모두 비웠습니다.");
            };
            _btnQuestResetDaily.Click += (s, e) =>
            {
                _settings.DailyQuestClearedCount = 0;
                _settings.DailyQuestDateKey = GetDailyQuestDateKey();
                _settings.Save();
                UpdateDailyQuestCounterUi();
                AppendLog("[퀘스트] 오늘 완료 카운터를 0으로 초기화했습니다.");
            };
            AttachGuide(_chkKmNewWheel, "휠 드래그 키 (부위 조준 · 아이템/탄환 휠)", "체크 후 추가하면 키를 누른 채 마우스를 움직여 방향을 선택하고, 떼면 확정되는 휠 선택 키로 작동합니다. (예: Tab 부위 조준, G 아이템/탄환 휠)");
            AttachGuide(_chkKmRight, "우클릭 보조", "시점 고정 ON/OFF 모두에서 마우스 우클릭을 지정된 버튼(특수 공격·회피·탄 선택 등)으로 사용합니다. 일반 모드에서 좌클릭은 화면 터치로 그대로 유지됩니다.");
            AttachGuide(_chkKmRightWheel, "우클릭 휠 드래그 (헤보 탄 선택 등)", "체크하면 마우스 우클릭을 꾹 누른 채 마우스를 움직여 방향을 선택하고, 떼면 확정되는 휠 선택 키로 작동합니다.");

            _txtKmNewKey.PreviewKeyDown += (s, e) =>
            {
                Key k = (e.Key == Key.System) ? e.SystemKey : e.Key;
                int vk = KeyInterop.VirtualKeyFromKey(k);
                int mods = 0;
                if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) mods |= 1;
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) mods |= 2;
                if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) mods |= 4;

                // Wait for main key when only a modifier (Ctrl/Shift/Alt) is pressed
                if (vk == 16 || vk == 160 || vk == 161 || vk == 17 || vk == 162 || vk == 163 || vk == 18 || vk == 164 || vk == 165)
                {
                    var mp = new List<string>();
                    if ((mods & 1) != 0 || vk == 17 || vk == 162 || vk == 163) mp.Add("Ctrl");
                    if ((mods & 2) != 0 || vk == 16 || vk == 160 || vk == 161) mp.Add("Shift");
                    if ((mods & 4) != 0 || vk == 18 || vk == 164 || vk == 165) mp.Add("Alt");
                    _txtKmNewKey.Text = string.Join("+", mp.ToArray()) + "+...";
                    _txtKmNewKey.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
                    e.Handled = true;
                    return;
                }

                _pendingNewKeyVk = vk;
                _pendingNewKeyMods = mods;
                _txtKmNewKey.Text = KeyNameHelper.GetComboName(_pendingNewKeyMods, _pendingNewKeyVk);
                _txtKmNewKey.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
                e.Handled = true;
            };
            _txtKmNewKey.PreviewMouseDown += (s, e) =>
            {
                int mvk = 0;
                if (e.ChangedButton == MouseButton.Middle) mvk = 4;
                else if (e.ChangedButton == MouseButton.XButton1) mvk = 5;
                else if (e.ChangedButton == MouseButton.XButton2) mvk = 6;
                if (mvk > 0)
                {
                    int mods = 0;
                    if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) mods |= 1;
                    if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) mods |= 2;
                    if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) mods |= 4;
                    _pendingNewKeyVk = mvk;
                    _pendingNewKeyMods = mods;
                    _txtKmNewKey.Text = KeyNameHelper.GetComboName(_pendingNewKeyMods, _pendingNewKeyVk);
                    _txtKmNewKey.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
                    e.Handled = true;
                }
            };
            AttachGuide(_txtKmNewKey, "매핑 키 입력 (조합키 지원)", "이 칸을 클릭한 뒤 원하는 키(예: Q, Ctrl+1, Shift+E, Alt+2) 또는 마우스 4·5·휠클릭 버튼을 누르면 등록할 수 있습니다.");

            Action onKmChanged = () =>
            {
                if (_isInitializing || _activeProfile == null) return;
                SyncProfileFromUi();
                if (_keymapOverlay != null) _keymapOverlay.Redraw();
            };
            _chkKmJoy.Checked += (s, e) => onKmChanged();
            _chkKmJoy.Unchecked += (s, e) => onKmChanged();
            _chkKmCam.Checked += (s, e) => onKmChanged();
            _chkKmCam.Unchecked += (s, e) => onKmChanged();
            _chkKmLeft.Checked += (s, e) => onKmChanged();
            _chkKmLeft.Unchecked += (s, e) => onKmChanged();
            _chkKmRight.Checked += (s, e) => onKmChanged();
            _chkKmRight.Unchecked += (s, e) => onKmChanged();
            _chkKmRightWheel.Checked += (s, e) => onKmChanged();
            _chkKmRightWheel.Unchecked += (s, e) => onKmChanged();
            _chkKmHints.Checked += (s, e) => { if (_keymapOverlay != null) _keymapOverlay.SetShowHints(true); SyncFpsOverlay(); };
            _chkKmHints.Unchecked += (s, e) => { if (_keymapOverlay != null) _keymapOverlay.SetShowHints(false); };
            _cmbKmToggleKey.SelectionChanged += (s, e) => onKmChanged();
            _sldKmSensX.ValueChanged += (s, e) =>
            {
                if (_txtKmSensXVal != null) _txtKmSensXVal.Text = _sldKmSensX.Value.ToString("0.00");
                onKmChanged();
            };
            _sldKmSensY.ValueChanged += (s, e) =>
            {
                if (_txtKmSensYVal != null) _txtKmSensYVal.Text = _sldKmSensY.Value.ToString("0.00");
                onKmChanged();
            };
            Action onHintStyleChanged = () =>
            {
                if (_txtHintOpacityVal != null) _txtHintOpacityVal.Text = Math.Round(_sldHintOpacity.Value * 100) + "%";
                if (_txtHintScaleVal != null) _txtHintScaleVal.Text = Math.Round(_sldHintScale.Value * 100) + "%";
                if (_keymapOverlay != null) _keymapOverlay.SetHintAppearance(_sldHintOpacity.Value, _sldHintScale.Value);
            };
            _sldHintOpacity.ValueChanged += (s, e) => onHintStyleChanged();
            _sldHintScale.ValueChanged += (s, e) => onHintStyleChanged();

            _cmbDevices.SelectionChanged += async (s, e) =>
            {
                UpdateUiStates();
                UpdateCommandPreview();
                var dev = _cmbDevices.SelectedItem as DeviceInfo;
                if (dev != null && dev.State == "device" && dev.Serial != _appsLoadedForSerial)
                    await LoadAppsAsync(false);
            };

            _txtAppSearch.TextChanged += (s, e) =>
            {
                var hint = ((FrameworkElement)Content).FindName("txtAppSearchHint") as TextBlock;
                if (hint != null)
                    hint.Visibility = string.IsNullOrEmpty(_txtAppSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
                FilterApps();
            };

            Action onOptChanged = () =>
            {
                if (_isInitializing) return;
                UpdateUiStates();
                UpdateCommandPreview();
            };

            _cmbCodec.SelectionChanged += (s, e) => onOptChanged();
            _txtBitrate.TextChanged += (s, e) => onOptChanged();
            _cmbMaxSize.SelectionChanged += (s, e) => onOptChanged();
            _cmbMaxSize.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler((s, e) => onOptChanged()));
            _cmbMaxFps.SelectionChanged += (s, e) => onOptChanged();
            _cmbMaxFps.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler((s, e) => onOptChanged()));
            _cmbRenderer.SelectionChanged += (s, e) => onOptChanged();

            _rbModeKeymap.Checked += (s, e) => onOptChanged();
            _rbModeGamepad.Checked += (s, e) => onOptChanged();
            _rbModeNormal.Checked += (s, e) => onOptChanged();

            _chkTurnScreenOff.Checked += (s, e) => onOptChanged();
            _chkTurnScreenOff.Unchecked += (s, e) => onOptChanged();
            _chkStayAwake.Checked += (s, e) => onOptChanged();
            _chkStayAwake.Unchecked += (s, e) => onOptChanged();
            _chkLowLatency.Checked += (s, e) => onOptChanged();
            _chkLowLatency.Unchecked += (s, e) => onOptChanged();
            _chkNoAudio.Checked += (s, e) => onOptChanged();
            _chkNoAudio.Unchecked += (s, e) => onOptChanged();
            _chkFullscreen.Checked += (s, e) => onOptChanged();
            _chkFullscreen.Unchecked += (s, e) => onOptChanged();
            _chkAlwaysOnTop.Checked += (s, e) => { if (!_isInitializing) ApplyAlwaysOnTopLive(true); onOptChanged(); };
            _chkAlwaysOnTop.Unchecked += (s, e) => { if (!_isInitializing) ApplyAlwaysOnTopLive(false); onOptChanged(); };
            _chkPrintFps.Checked += (s, e) => onOptChanged();
            _chkPrintFps.Unchecked += (s, e) => onOptChanged();
            _chkGamepadKbAssist.Checked += (s, e) =>
            {
                if (_isInitializing) return;
                _settings.GamepadKeyboardAssist = true;
                _settings.Save();
                if (_activeProfile != null && _activeProfile.IsBuiltIn && _activeProfile.EnableJoystick)
                {
                    for (int i = 0; i < _keymapProfiles.Count; i++)
                    {
                        if (_keymapProfiles[i].Name.Contains("게임패드 보조"))
                        {
                            _cmbKeyProfiles.SelectedIndex = i;
                            break;
                        }
                    }
                }
                if (GetControlMode() == "gamepad" && _scrcpyProcess != null && !_scrcpyProcess.HasExited && _hookEngine != null && !_hookEngine.IsActive)
                    BtnKmStart_Click(null, null);
            };
            _chkGamepadKbAssist.Unchecked += (s, e) =>
            {
                if (_isInitializing) return;
                _settings.GamepadKeyboardAssist = false;
                _settings.Save();
                if (GetControlMode() == "gamepad" && _hookEngine != null && _hookEngine.IsActive)
                    StopKeymap();
            };

            _chkVirtualDisplay.Checked += (s, e) => onOptChanged();
            _chkVirtualDisplay.Unchecked += (s, e) => onOptChanged();
            _cmbVdSpec.SelectionChanged += (s, e) => onOptChanged();
            _cmbVdSpec.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler((s, e) => onOptChanged()));
            _chkNoVdDecorations.Checked += (s, e) => onOptChanged();
            _chkNoVdDecorations.Unchecked += (s, e) => onOptChanged();
            _cmbApps.SelectionChanged += (s, e) => onOptChanged();
        }

        private void AttachGuide(FrameworkElement el, string title, string body)
        {
            el.ToolTip = body;
            el.MouseEnter += (s, e) =>
            {
                _txtGuideTitle.Text = title;
                _txtGuideBody.Text = body;
            };
            el.MouseLeave += (s, e) =>
            {
                _txtGuideTitle.Text = Loc.T("guide_title");
                _txtGuideBody.Text = Loc.T("guide_body");
            };
        }

        private void SetTextByName(FrameworkElement root, string name, string text)
        {
            var tb = root.FindName(name) as TextBlock;
            if (tb != null) tb.Text = text;
        }

        private void ApplyLanguage()
        {
            var root = Content as FrameworkElement;
            if (root == null) return;

            Title = Loc.T("win_title");
            _btnDownloadScrcpy.Content = Loc.T("btn_download_scrcpy");
            _btnSetupGuide.Content = Loc.T("btn_setup_guide");
            _btnShortcuts.Content = Loc.T("btn_shortcuts");
            _btnSelectFolder.Content = Loc.T("btn_folder");
            if (_updateInfo != null && _updateInfo.HasUpdate)
                _btnUpdate.Content = Loc.F("btn_update", _updateInfo.LatestTag);

            if (_adb != null)
            {
                if (_adb.IsValid)
                    _txtScrcpyStatus.Text = string.IsNullOrEmpty(_detectedScrcpyVersion) ? Loc.T("scrcpy_ready") : Loc.F("scrcpy_ready_ver", _detectedScrcpyVersion);
                else
                    _txtScrcpyStatus.Text = Loc.T("scrcpy_missing");
            }

            SetTextByName(root, "txtModeKmTitle", Loc.T("mode_km_title"));
            SetTextByName(root, "txtModeKmSub", Loc.T("mode_km_sub"));
            SetTextByName(root, "txtModeGpTitle", Loc.T("mode_gp_title"));
            SetTextByName(root, "txtModeGpSub", Loc.T("mode_gp_sub"));
            SetTextByName(root, "txtModeNmTitle", Loc.T("mode_nm_title"));
            SetTextByName(root, "txtModeNmSub", Loc.T("mode_nm_sub"));

            var tConn = root.FindName("tabConn") as TabItem;
            if (tConn != null) tConn.Header = Loc.T("tab_conn");
            var tKm = root.FindName("tabKeymap") as TabItem;
            if (tKm != null) tKm.Header = Loc.T("tab_keymap");
            var tQuest = root.FindName("tabQuest") as TabItem;
            if (tQuest != null) tQuest.Header = Loc.T("tab_quest");

            SetTextByName(root, "txtSec1Title", Loc.T("sec1_title"));
            _btnRefreshDevices.Content = Loc.T("btn_refresh");
            _btnUsbToWifi.Content = Loc.T("btn_usb_wifi");
            _btnDisconnectWifi.Content = Loc.T("btn_wifi_disc");
            _btnRestartAdb.Content = Loc.T("btn_adb_restart");
            var expW = root.FindName("expWireless") as Expander;
            if (expW != null) expW.Header = Loc.T("exp_wireless");
            SetTextByName(root, "lblPairAddr", Loc.T("lbl_pair_addr"));
            SetTextByName(root, "lblPairCode", Loc.T("lbl_pair_code"));
            _btnPair.Content = Loc.T("btn_pair");
            SetTextByName(root, "lblWifiAddr", Loc.T("lbl_wifi_addr"));
            _btnWifiConnect.Content = Loc.T("btn_wifi_conn");

            SetTextByName(root, "txtSec2Title", Loc.T("sec2_title"));
            SetTextByName(root, "lblCodec", Loc.T("lbl_codec"));
            SetTextByName(root, "lblMaxSize", Loc.T("lbl_maxsize"));
            SetTextByName(root, "lblRenderer", Loc.T("lbl_renderer"));
            _chkTurnScreenOff.Content = Loc.T("chk_screen_off");
            _chkStayAwake.Content = Loc.T("chk_stay_awake");
            _chkLowLatency.Content = Loc.T("chk_low_latency");
            _chkNoAudio.Content = Loc.T("chk_no_audio");
            _chkFullscreen.Content = Loc.T("chk_fullscreen");
            _chkAlwaysOnTop.Content = Loc.T("chk_always_on_top");
            _chkPrintFps.Content = Loc.T("chk_fps_overlay");
            SetTextByName(root, "lblRecFolder", Loc.T("lbl_rec_folder"));
            _btnBrowseRecord.Content = Loc.T("btn_browse");
            _btnOpenRecord.Content = Loc.T("btn_open_folder");

            SetTextByName(root, "txtSec3Title", Loc.T("sec3_title"));
            _chkVirtualDisplay.Content = Loc.T("chk_vd");
            _chkNoVdDecorations.Content = Loc.T("chk_no_decor");
            SetTextByName(root, "lblApp", Loc.T("lbl_app"));
            SetTextByName(root, "txtAppSearchHint", Loc.T("hint_app_search"));
            _btnRefreshApps.Content = Loc.T("btn_load_apps");

            _txtKmModeWarning.Text = Loc.T("km_warn");
            if (_chkGamepadKbAssist != null) _chkGamepadKbAssist.Content = Loc.T("chk_gamepad_kb_assist");
            SetTextByName(root, "txtKmSec1", Loc.T("km_sec1"));
            _btnReloadProfiles.Content = Loc.T("btn_refresh");
            _btnImportKmp.Content = Loc.T("btn_import_kmp");
            _btnSaveProfile.Content = Loc.T("btn_save");
            _btnSaveAsProfile.Content = Loc.T("btn_save_as");
            _btnRenameProfile.Content = Loc.T("btn_rename");
            _btnDeleteProfile.Content = Loc.T("btn_delete");
            _btnExportCode.Content = Loc.T("btn_copy_code");
            _btnImportCode.Content = Loc.T("btn_import_code");
            SetTextByName(root, "lblHotkeyHint", Loc.T("lbl_hotkey_hint"));

            SetTextByName(root, "txtKmSec2", Loc.T("km_sec2"));
            _chkKmJoy.Content = Loc.T("chk_km_joy");
            _chkKmCam.Content = Loc.T("chk_km_cam");
            _chkKmLeft.Content = Loc.T("chk_km_left");
            _chkKmRight.Content = Loc.T("chk_km_right");
            _chkKmRightWheel.Content = Loc.T("chk_km_right_wheel");
            _chkKmHints.Content = Loc.T("chk_km_hints");
            SetTextByName(root, "lblCamToggle", Loc.T("lbl_cam_toggle"));
            SetTextByName(root, "lblSensX", Loc.T("lbl_sens_x"));
            SetTextByName(root, "lblSensY", Loc.T("lbl_sens_y"));
            SetTextByName(root, "lblHintOp", Loc.T("lbl_hint_op"));
            SetTextByName(root, "lblHintSc", Loc.T("lbl_hint_sc"));
            _btnKmEditOverlay.Content = Loc.T("btn_edit_overlay");
            _btnKmStart.Content = Loc.T("btn_km_reconnect");
            _btnKmStop.Content = Loc.T("btn_km_pause");

            SetTextByName(root, "txtKmSec3", Loc.T("km_sec3"));
            if (_pendingNewKeyVk <= 0) _txtKmNewKey.Text = Loc.T("hint_new_key");
            _chkKmNewWheel.Content = Loc.T("chk_new_wheel");
            _btnKmAddKey.Content = Loc.T("btn_add_key");
            _btnKmDelKey.Content = Loc.T("btn_del_key");

            _txtGuideTitle.Text = Loc.T("guide_title");
            _txtGuideBody.Text = Loc.T("guide_body");
            _btnStop.Content = Loc.T("btn_stop");
            _btnScreenshot.Content = Loc.T("btn_screenshot");
            SetTextByName(root, "lblLog", Loc.T("lbl_log"));
            _btnCopyCmd.Content = Loc.T("btn_copy_cmd");
            _btnClearLog.Content = Loc.T("btn_clear_log");

            UpdateModeSummary();
            UpdateRecordButtonAppearance();
            RefreshKeyListBox();
            if (_keymapOverlay != null) _keymapOverlay.RefreshLanguage();
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("브라우저를 열 수 없습니다: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowSetupGuideDialog()
        {
            MessageBox.Show(this, Loc.T("dlg_guide_body"), Loc.T("dlg_guide_title"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowShortcutsDialog()
        {
            MessageBox.Show(this, Loc.T("dlg_short_body"), Loc.T("dlg_short_title"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                var src = HwndSource.FromHwnd(hwnd);
                if (src != null)
                {
                    src.AddHook(WndProc);
                    var rid = new RAWINPUTDEVICE[]
                    {
                        new RAWINPUTDEVICE
                        {
                            usUsagePage = 0x01, // Generic Desktop
                            usUsage = 0x02,     // Mouse
                            dwFlags = RIDEV_INPUTSINK,
                            hwndTarget = hwnd
                        },
                        new RAWINPUTDEVICE
                        {
                            usUsagePage = 0x0C, // Consumer Control (Xbox Share button)
                            usUsage = 0x01,     // Consumer Control
                            dwFlags = RIDEV_INPUTSINK,
                            hwndTarget = hwnd
                        }
                    };
                    RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
                }
            }
            catch { }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_QUEST_SCAN)
                {
                    handled = true;
                    var _ = ScanCurrentQuestAsync(true);
                    return IntPtr.Zero;
                }
                if (id == HOTKEY_SCREENSHOT)
                {
                    handled = true;
                    if (_scrcpyProcess != null && !_scrcpyProcess.HasExited)
                        BtnScreenshot_Click(null, null);
                    return IntPtr.Zero;
                }
            }
            if (msg == WM_INPUT)
            {
                bool needMouse = _hookEngine != null && _hookEngine.IsActive && (_hookEngine.IsCameraLocked || _hookEngine.IsWheelDragging);
                bool mirroring = _scrcpyProcess != null && !_scrcpyProcess.HasExited;
                if (needMouse || mirroring)
                {
                    uint headerSize = (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER));
                    uint size = 0;
                    GetRawInputData(lParam, RID_INPUT, null, ref size, headerSize);
                    if (size >= headerSize + 8 && size <= 512)
                    {
                        byte[] buf = new byte[size];
                        if (GetRawInputData(lParam, RID_INPUT, buf, ref size, headerSize) == size)
                        {
                            uint dwType = BitConverter.ToUInt32(buf, 0);
                            if (dwType == RIM_TYPEMOUSE && needMouse && size >= headerSize + 20)
                            {
                                ushort usFlags = BitConverter.ToUInt16(buf, (int)headerSize);
                                if ((usFlags & 0x01) == 0) // Relative mouse movement (MOUSE_MOVE_RELATIVE)
                                {
                                    int dx = BitConverter.ToInt32(buf, (int)headerSize + 12);
                                    int dy = BitConverter.ToInt32(buf, (int)headerSize + 16);
                                    if (dx != 0 || dy != 0)
                                        _hookEngine.OnRawMouseDelta(dx, dy);
                                }
                            }
                            else if (dwType == RIM_TYPEHID && mirroring)
                            {
                                uint dwSizeHid = BitConverter.ToUInt32(buf, (int)headerSize);
                                uint dwCount = BitConverter.ToUInt32(buf, (int)headerSize + 4);
                                int rawOffset = (int)headerSize + 8;
                                if (dwSizeHid >= 2 && dwSizeHid <= 8 && dwCount >= 1 && rawOffset + (int)dwSizeHid <= buf.Length)
                                {
                                    // Xbox Series Share button emits Consumer Control usage 0x00B2 (Record)
                                    bool isShare = false;
                                    if (buf[rawOffset] == 0xB2 && (dwSizeHid < 2 || buf[rawOffset + 1] == 0x00))
                                        isShare = true;
                                    else if (dwSizeHid >= 2 && buf[rawOffset + 1] == 0xB2 && (dwSizeHid < 3 || buf[rawOffset + 2] == 0x00))
                                        isShare = true;

                                    if (isShare && (DateTime.UtcNow - _lastShareShotUtc).TotalMilliseconds > 800)
                                    {
                                        _lastShareShotUtc = DateTime.UtcNow;
                                        BtnScreenshot_Click(null, null);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return IntPtr.Zero;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await UpdateScrcpyStatusAsync();
            await RefreshDevicesAsync(false);
            _deviceTimer.Start();
            CheckForUpdatesInBackground();
        }

        private async void CheckForUpdatesInBackground()
        {
            var info = await UpdateChecker.CheckAsync();
            if (info != null && info.HasUpdate)
            {
                _updateInfo = info;
                _updateReleaseUrl = info.ReleaseUrl;
                _btnUpdate.Content = Loc.F("btn_update", info.LatestTag);
                _btnUpdate.ToolTip = Loc.F("btn_update", info.LatestTag);
                _btnUpdate.Visibility = Visibility.Visible;
                AppendLog(string.Format("[Update] New version {0} available!", info.LatestTag));
            }
        }

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            string url = string.IsNullOrEmpty(_updateReleaseUrl) ? UpdateChecker.ReleasesPageUrl : _updateReleaseUrl;
            if (_updateInfo == null || string.IsNullOrEmpty(_updateInfo.ExeDownloadUrl))
            {
                OpenUrl(url);
                return;
            }

            var choice = MessageBox.Show(
                this,
                Loc.F("upd_prompt", _updateInfo.LatestTag),
                Loc.T("upd_title"),
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (choice == MessageBoxResult.No)
            {
                OpenUrl(url);
                return;
            }
            if (choice != MessageBoxResult.Yes) return;

            _btnUpdate.IsEnabled = false;
            _btnUpdate.Content = Loc.T("btn_updating");
            AppendLog("[Update] Downloading " + _updateInfo.LatestTag + "...");

            var res = await UpdateChecker.DownloadAndApplyUpdateAsync(_updateInfo.ExeDownloadUrl);
            if (!res.Item1)
            {
                _btnUpdate.IsEnabled = true;
                _btnUpdate.Content = Loc.F("btn_update", _updateInfo.LatestTag);
                AppendLog("[Update Failed] " + res.Item2);
                var openRes = MessageBox.Show(this, res.Item2 + "\n\nOpen GitHub Releases?", Loc.T("upd_title"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (openRes == MessageBoxResult.Yes) OpenUrl(url);
                return;
            }

            AppendLog("[Update] Restarting...");
            Close();
        }

        private void SetQuestHotKeyActive(bool active)
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                if (active)
                {
                    if (!_questHotKeyRegistered)
                        _questHotKeyRegistered = RegisterHotKey(hwnd, HOTKEY_QUEST_SCAN, 0, VK_F8);
                    if (!_screenshotHotKeyRegistered)
                        _screenshotHotKeyRegistered = RegisterHotKey(hwnd, HOTKEY_SCREENSHOT, 0, VK_F12);
                }
                else
                {
                    if (_questHotKeyRegistered)
                    {
                        UnregisterHotKey(hwnd, HOTKEY_QUEST_SCAN);
                        _questHotKeyRegistered = false;
                    }
                    if (_screenshotHotKeyRegistered)
                    {
                        UnregisterHotKey(hwnd, HOTKEY_SCREENSHOT);
                        _screenshotHotKeyRegistered = false;
                    }
                }
            }
            catch { }
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SetQuestHotKeyActive(false);
            _deviceTimer.Stop();
            _recordTimer.Stop();
            _overlayTimer.Stop();
            StopKeymap();
            if (_fpsOverlay != null)
            {
                try { _fpsOverlay.Close(); } catch { }
                _fpsOverlay = null;
            }
            if (_keymapOverlay != null)
            {
                try { _keymapOverlay.Close(); } catch { }
                _keymapOverlay = null;
            }
            if (_cursorHider != null)
            {
                try { _cursorHider.Close(); } catch { }
                _cursorHider = null;
            }
            if (_hookEngine != null) _hookEngine.Dispose();
            if (_controlClient != null) _controlClient.Dispose();
            SaveSettingsFromUi();
            StopRecording();
            StopScrcpy();
        }

        private async Task UpdateScrcpyStatusAsync()
        {
            if (_adb.IsValid)
            {
                string ver = await _adb.GetScrcpyVersionAsync();
                if (!string.IsNullOrEmpty(ver)) _detectedScrcpyVersion = ver;
                _bdScrcpyStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7"));
                _txtScrcpyStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#166534"));
                _txtScrcpyStatus.Text = string.IsNullOrEmpty(ver) ? Loc.T("scrcpy_ready") : Loc.F("scrcpy_ready_ver", ver);
                _bdScrcpyStatus.ToolTip = _adb.ScrcpyDir;
                _btnDownloadScrcpy.Visibility = Visibility.Collapsed;
                AppendLog("[준비] scrcpy 경로 인식 완료: " + _adb.ScrcpyDir);
            }
            else
            {
                _bdScrcpyStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
                _txtScrcpyStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B"));
                _txtScrcpyStatus.Text = Loc.T("scrcpy_missing");
                _btnDownloadScrcpy.Visibility = Visibility.Visible;
                AppendLog("[주의] 현재 폴더에 scrcpy.exe가 없습니다. 우측 상단 [scrcpy 다운로드]로 받은 뒤 같은 폴더에 넣거나 [폴더 변경]으로 지정해 주세요.");
            }
            UpdateUiStates();
        }

        private async void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "scrcpy.exe와 adb.exe가 들어있는 폴더를 선택하세요.";
                if (!string.IsNullOrEmpty(_adb.ScrcpyDir) && Directory.Exists(_adb.ScrcpyDir))
                    dlg.SelectedPath = _adb.ScrcpyDir;

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _adb.SetDirectory(dlg.SelectedPath);
                    _settings.CustomScrcpyDir = dlg.SelectedPath;
                    _settings.Save();
                    await UpdateScrcpyStatusAsync();
                    await RefreshDevicesAsync(false);
                }
            }
        }

        private string GetEffectiveRecordFolder()
        {
            string folder = (_txtRecordFolder.Text ?? "").Trim();
            if (!string.IsNullOrEmpty(folder)) return folder;
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "recordings");
        }

        private void BtnBrowseRecord_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "녹화 영상(.mp4)을 저장할 폴더를 선택하세요.";
                string cur = GetEffectiveRecordFolder();
                if (Directory.Exists(cur)) dlg.SelectedPath = cur;

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _txtRecordFolder.Text = dlg.SelectedPath;
                    _settings.RecordFolder = dlg.SelectedPath;
                    _settings.Save();
                    UpdateCommandPreview();
                }
            }
        }

        private void BtnOpenRecord_Click(object sender, RoutedEventArgs e)
        {
            string folder = GetEffectiveRecordFolder();
            try
            {
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + folder + "\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppendLog("[오류] 녹화 폴더를 열 수 없습니다: " + ex.Message);
            }
        }

        private async Task RefreshDevicesAsync(bool silent)
        {
            if (!_adb.IsValid || _isRefreshingDevices) return;
            _isRefreshingDevices = true;
            try
            {
                var found = await _adb.GetDevicesAsync();
                var prevDev = _cmbDevices.SelectedItem as DeviceInfo;
                string prevSerial = prevDev != null ? prevDev.Serial : "";

                bool changed = found.Count != _devices.Count ||
                               found.Zip(_devices, (a, b) => a.Serial == b.Serial && a.State == b.State).Any(eq => !eq);

                if (changed || !silent)
                {
                    _devices = found;
                    _cmbDevices.Items.Clear();

                    if (_devices.Count == 0)
                    {
                        _cmbDevices.Items.Add("(연결된 기기 없음 — USB를 꽂거나 무선 연결하세요)");
                        _cmbDevices.SelectedIndex = 0;
                    }
                    else
                    {
                        int selectIdx = 0;
                        for (int i = 0; i < _devices.Count; i++)
                        {
                            _cmbDevices.Items.Add(_devices[i]);
                            if (_devices[i].Serial == prevSerial) selectIdx = i;
                        }
                        _cmbDevices.SelectedIndex = selectIdx;
                    }

                    if (!silent)
                        AppendLog(string.Format("[기기] 연결된 기기 {0}대 확인됨", _devices.Count));
                }
            }
            finally
            {
                _isRefreshingDevices = false;
                UpdateUiStates();
            }
        }

        private async void BtnUsbToWifi_Click(object sender, RoutedEventArgs e)
        {
            var dev = _cmbDevices.SelectedItem as DeviceInfo;
            if (dev == null || dev.IsWifi || dev.State != "device")
            {
                MessageBox.Show("먼저 USB로 연결된 기기를 선택해 주세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _btnUsbToWifi.IsEnabled = false;
            AppendLog("[무선 전환] USB 기기(" + dev.Serial + ")의 Wi-Fi IP를 조회하고 무선 연결을 시도합니다...");
            try
            {
                var res = await _adb.SwitchUsbToWifiAsync(dev.Serial);
                AppendLog("[무선 전환] " + res.Item2);
                if (res.Item1)
                {
                    _txtWifiAddr.Text = res.Item3;
                    await RefreshDevicesAsync(false);
                    foreach (var item in _cmbDevices.Items)
                    {
                        var d = item as DeviceInfo;
                        if (d != null && d.Serial == res.Item3)
                        {
                            _cmbDevices.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
            finally
            {
                _btnUsbToWifi.IsEnabled = true;
                UpdateUiStates();
            }
        }

        private async void BtnDisconnectWifi_Click(object sender, RoutedEventArgs e)
        {
            if (!_adb.IsValid) return;
            var dev = _cmbDevices.SelectedItem as DeviceInfo;
            string target = (dev != null && dev.IsWifi) ? dev.Serial : "";

            _btnDisconnectWifi.IsEnabled = false;
            AppendLog(string.IsNullOrEmpty(target) ? "[무선 해제] 모든 무선 연결을 해제합니다..." : "[무선 해제] " + target + " 연결을 해제합니다...");
            try
            {
                var res = await _adb.DisconnectWirelessAsync(target);
                AppendLog("[무선 해제 결과] " + res.Combined.Trim());
                await RefreshDevicesAsync(false);
            }
            finally
            {
                _btnDisconnectWifi.IsEnabled = true;
                UpdateUiStates();
            }
        }

        private async void BtnRestartAdb_Click(object sender, RoutedEventArgs e)
        {
            if (!_adb.IsValid) return;
            StopScrcpy();
            _btnRestartAdb.IsEnabled = false;
            AppendLog("[ADB] ADB 서버를 재시작하는 중입니다...");
            try
            {
                var res = await _adb.RestartAdbServerAsync();
                string msg = res.Combined.Trim();
                AppendLog("[ADB] 재시작 완료" + (string.IsNullOrEmpty(msg) ? "" : ": " + msg));
                _appsLoadedForSerial = "";
                await RefreshDevicesAsync(false);
            }
            finally
            {
                _btnRestartAdb.IsEnabled = true;
                UpdateUiStates();
            }
        }

        private async void BtnPair_Click(object sender, RoutedEventArgs e)
        {
            string addr = _txtPairAddr.Text.Trim();
            string code = _txtPairCode.Text.Trim();
            if (string.IsNullOrEmpty(addr) || string.IsNullOrEmpty(code))
            {
                MessageBox.Show("페어링 팝업의 IP:포트와 6자리 코드를 모두 입력해 주세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _btnPair.IsEnabled = false;
            AppendLog("[페어링] " + addr + " 페어링 시도 중...");
            try
            {
                var res = await _adb.PairWirelessAsync(addr, code);
                AppendLog("[페어링 결과] " + res.Combined.Trim());
            }
            finally
            {
                _btnPair.IsEnabled = true;
            }
        }

        private async void BtnWifiConnect_Click(object sender, RoutedEventArgs e)
        {
            string addr = _txtWifiAddr.Text.Trim();
            if (string.IsNullOrEmpty(addr))
            {
                MessageBox.Show("무선 디버깅 IP:포트를 입력해 주세요. (예: 192.168.0.20:5555)", "안내", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _btnWifiConnect.IsEnabled = false;
            AppendLog("[무선 연결] " + addr + " 연결 시도 중...");
            try
            {
                var res = await _adb.ConnectWirelessAsync(addr);
                AppendLog("[무선 연결 결과] " + res.Combined.Trim());
                await RefreshDevicesAsync(false);
            }
            finally
            {
                _btnWifiConnect.IsEnabled = true;
            }
        }

        private async Task LoadAppsAsync(bool manual)
        {
            var dev = _cmbDevices.SelectedItem as DeviceInfo;
            if (!_adb.IsValid || dev == null || dev.State != "device" || _isLoadingApps)
                return;

            _isLoadingApps = true;
            _btnRefreshApps.IsEnabled = false;
            if (manual) AppendLog("[앱 목록] 설치된 앱 목록을 불러오는 중...");

            try
            {
                var apps = await _adb.GetInstalledAppsAsync(dev.Serial);
                _allApps = apps;
                _appsLoadedForSerial = dev.Serial;
                FilterApps(_settings.StartAppPackage);
                AppendLog(string.Format("[앱 목록] 사용자 설치 앱 {0}개를 불러왔습니다.", _allApps.Count));
            }
            finally
            {
                _isLoadingApps = false;
                _btnRefreshApps.IsEnabled = true;
            }
        }

        private void FilterApps(string preferredPackage = null)
        {
            string query = (_txtAppSearch.Text ?? "").Trim();
            string currentPkg = preferredPackage;
            if (currentPkg == null)
            {
                var cur = _cmbApps.SelectedItem as AppItem;
                currentPkg = cur != null ? cur.PackageName : "";
            }

            _cmbApps.Items.Clear();
            var noneItem = new AppItem { Name = "(선택 안 함 — 현재 화면 그대로)", PackageName = "" };
            _cmbApps.Items.Add(noneItem);

            int selectIdx = 0;
            foreach (var app in _allApps)
            {
                if (!string.IsNullOrEmpty(query) &&
                    app.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                    app.PackageName.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                int idx = _cmbApps.Items.Add(app);
                if (!string.IsNullOrEmpty(currentPkg) && app.PackageName == currentPkg)
                    selectIdx = idx;
            }

            _cmbApps.SelectedIndex = selectIdx;
            UpdateCommandPreview();
        }

        private string GetSelectedTag(ComboBox cmb, string fallback)
        {
            var item = cmb.SelectedItem as ComboBoxItem;
            if (item != null && item.Tag != null)
                return item.Tag.ToString();
            string txt = (cmb.Text ?? "").Trim();
            var m = Regex.Match(txt, @"^\d+");
            return m.Success ? m.Value : fallback;
        }

        private string BuildScrcpyArguments(string explicitRecordFilePath)
        {
            var args = new List<string>();

            var dev = _cmbDevices.SelectedItem as DeviceInfo;
            if (dev != null && !string.IsNullOrEmpty(dev.Serial))
                args.Add(string.Format("-s \"{0}\"", dev.Serial));

            string codec = GetSelectedTag(_cmbCodec, "h264");
            args.Add("--video-codec=" + codec);

            int bitrate;
            if (int.TryParse(_txtBitrate.Text.Trim(), out bitrate) && bitrate > 0)
                args.Add("-b" + bitrate + "M");

            bool useVd = _chkVirtualDisplay.IsChecked == true;
            if (!useVd)
            {
                int maxSize;
                if (int.TryParse(GetSelectedTag(_cmbMaxSize, "0"), out maxSize) && maxSize > 0)
                    args.Add("-m" + maxSize);
            }

            int maxFps;
            if (int.TryParse(GetSelectedTag(_cmbMaxFps, "0"), out maxFps) && maxFps > 0)
                args.Add("--max-fps=" + maxFps);

            string renderer = GetSelectedTag(_cmbRenderer, "auto");
            if (renderer != "auto")
                args.Add("--render-driver=" + renderer);

            if (_chkTurnScreenOff.IsChecked == true) args.Add("-S");
            if (_chkStayAwake.IsChecked == true) args.Add("-w");
            if (_chkLowLatency.IsChecked == true) args.Add("--video-codec-options=low-latency=1");
            if (_chkNoAudio.IsChecked == true) args.Add("--no-audio");
            if (_chkFullscreen.IsChecked == true) args.Add("-f");
            if (_chkAlwaysOnTop.IsChecked == true) args.Add("--always-on-top");
            if (_chkPrintFps.IsChecked == true) args.Add("--print-fps");

            string mode = GetControlMode();
            if (mode == "keymap")
            {
                args.Add("--mouse=disabled");
                args.Add("--keyboard=uhid");
            }
            else if (mode == "gamepad")
            {
                args.Add("-G");
                args.Add("--keyboard=uhid");
                args.Add("--mouse-bind=-hsn:++++");
            }
            else
            {
                args.Add("--keyboard=uhid");
            }

            if (useVd)
            {
                string spec = (_cmbVdSpec.Text ?? "").Trim();
                if (string.IsNullOrEmpty(spec))
                    args.Add("--new-display");
                else
                    args.Add("--new-display=" + spec);

                if (_chkNoVdDecorations.IsChecked == true)
                    args.Add("--no-vd-system-decorations");
            }

            var selectedApp = _cmbApps.SelectedItem as AppItem;
            if (selectedApp != null && !string.IsNullOrEmpty(selectedApp.PackageName))
                args.Add("--start-app=" + selectedApp.PackageName);

            return string.Join(" ", args);
        }

        private string GetControlMode()
        {
            if (_rbModeGamepad.IsChecked == true) return "gamepad";
            if (_rbModeNormal.IsChecked == true) return "normal";
            return "keymap";
        }

        private void UpdateModeSummary()
        {
            string mode = GetControlMode();
            if (mode == "keymap")
            {
                _btnStart.Content = Loc.T("btn_start_km");
                string pName = _activeProfile != null ? _activeProfile.Name : "-";
                _txtModeSummary.Text = Loc.F("summary_km",
                    pName, _activeProfile != null ? KeyNameHelper.GetName(_activeProfile.CameraToggleVk) : "Alt");
                _txtModeSummary.Visibility = Visibility.Visible;
                if (_bdKmModeWarning != null) _bdKmModeWarning.Visibility = Visibility.Collapsed;
            }
            else if (mode == "gamepad")
            {
                _btnStart.Content = Loc.T("btn_start_gp");
                _txtModeSummary.Text = Loc.T("summary_gp");
                _txtModeSummary.Visibility = Visibility.Visible;
                if (_bdKmModeWarning != null) _bdKmModeWarning.Visibility = Visibility.Visible;
                if (_chkGamepadKbAssist != null) _chkGamepadKbAssist.Visibility = Visibility.Visible;
            }
            else
            {
                _btnStart.Content = Loc.T("btn_start_nm");
                _txtModeSummary.Text = Loc.T("summary_nm");
                _txtModeSummary.Visibility = Visibility.Visible;
                if (_bdKmModeWarning != null) _bdKmModeWarning.Visibility = Visibility.Visible;
                if (_chkGamepadKbAssist != null) _chkGamepadKbAssist.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateCommandPreview()
        {
            UpdateModeSummary();
        }

        private void UpdateUiStates()
        {
            bool running = _scrcpyProcess != null && !_scrcpyProcess.HasExited;
            bool recording = _recordProcess != null && !_recordProcess.HasExited;
            var dev = _cmbDevices.SelectedItem as DeviceInfo;
            bool hasDevice = dev != null && dev.State == "device";
            bool hasAnyWifi = _devices.Any(d => d.IsWifi);
            bool useVd = _chkVirtualDisplay.IsChecked == true;

            _cmbMaxSize.IsEnabled = !useVd;
            _cmbVdSpec.IsEnabled = useVd;
            _chkNoVdDecorations.IsEnabled = useVd;

            _btnUsbToWifi.IsEnabled = _adb.IsValid && hasDevice && !dev.IsWifi && !running && !recording;
            _btnDisconnectWifi.IsEnabled = _adb.IsValid && hasAnyWifi && !running && !recording;
            _btnRestartAdb.IsEnabled = _adb.IsValid;
            _btnStart.IsEnabled = _adb.IsValid && hasDevice && !running;
            _btnStop.IsEnabled = running;
            _btnScreenshot.IsEnabled = _adb.IsValid && hasDevice;
            _btnRecordToggle.IsEnabled = recording || (_adb.IsValid && hasDevice);
            bool kmActive = _hookEngine != null && _hookEngine.IsActive;
            _btnKmStart.IsEnabled = _adb.IsValid && hasDevice && running && !kmActive;
            _btnKmStop.IsEnabled = kmActive;
            _btnKmEditOverlay.IsEnabled = running && _activeProfile != null;
            bool isCustomProfile = _activeProfile != null && !_activeProfile.IsBuiltIn;
            if (_btnRenameProfile != null) _btnRenameProfile.IsEnabled = isCustomProfile;
            if (_btnDeleteProfile != null) _btnDeleteProfile.IsEnabled = isCustomProfile;
            UpdateRecordButtonAppearance();
        }

        private void UpdateRecordButtonAppearance()
        {
            bool recording = _recordProcess != null && !_recordProcess.HasExited;
            if (recording)
            {
                var elapsed = DateTime.Now - _recordStartTime;
                _btnRecordToggle.Content = Loc.F("btn_rec_stop", elapsed.ToString(@"mm\:ss"));
                _btnRecordToggle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                _btnRecordToggle.Foreground = Brushes.White;
                _btnRecordToggle.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B91C1C"));
            }
            else
            {
                _btnRecordToggle.Content = Loc.T("btn_rec_start");
                _btnRecordToggle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
                _btnRecordToggle.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));
                _btnRecordToggle.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
            }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!_adb.IsValid)
            {
                MessageBox.Show("scrcpy.exe 폴더를 먼저 지정해 주세요.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool useVd = _chkVirtualDisplay.IsChecked == true;
            bool noDecor = _chkNoVdDecorations.IsChecked == true;
            var app = _cmbApps.SelectedItem as AppItem;
            bool hasApp = app != null && !string.IsNullOrEmpty(app.PackageName);

            if (useVd && noDecor && !hasApp)
            {
                var r = MessageBox.Show(
                    "'DeX / 시스템 UI 건너뛰기'를 켠 상태에서 실행할 앱을 선택하지 않으면 검은 화면만 뜰 수 있습니다.\n그래도 계속 실행할까요?",
                    "주의",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (r != MessageBoxResult.Yes) return;
            }

            SaveSettingsFromUi();

            // Briefly wake device before launching scrcpy so touch input focus activates even if screen was off
            var dev = _cmbDevices.SelectedItem as DeviceInfo;
            if (dev != null && dev.State == "device")
            {
                _btnStart.IsEnabled = false;
                await _adb.WakeDeviceAsync(dev.Serial);
                await Task.Delay(350);
            }

            string args = BuildScrcpyArguments(null);
            AppendLog("--------------------------------------------------");
            AppendLog("[실행] scrcpy.exe " + args);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _adb.ScrcpyPath,
                    Arguments = args,
                    WorkingDirectory = _adb.ScrcpyDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                // Prevent SDL2 from minimizing fullscreen window on Alt-Tab focus loss
                psi.EnvironmentVariables["SDL_HINT_VIDEO_MINIMIZE_ON_FOCUS_LOSS"] = "0";

                _scrcpyProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _scrcpyProcess.OutputDataReceived += (s, ev) => { if (ev.Data != null) HandleScrcpyOutput(ev.Data); };
                _scrcpyProcess.ErrorDataReceived += (s, ev) => { if (ev.Data != null) HandleScrcpyOutput(ev.Data); };
                _scrcpyProcess.Exited += (s, ev) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        AppendLog("[종료] scrcpy가 종료되었습니다.");
                        _bdFpsBadge.Visibility = Visibility.Collapsed;
                        SetQuestHotKeyActive(false);
                        StopKeymap();
                        HideFpsOverlay();
                        _activeDisplayId = "";
                        _scrcpyProcess = null;
                        UpdateUiStates();
                    }));
                };

                _activeDisplayId = "";
                _autoKeymapTriggered = false;
                _scrcpyProcess.Start();
                _scrcpyProcess.BeginOutputReadLine();
                _scrcpyProcess.BeginErrorReadLine();
                SetQuestHotKeyActive(true);
                if (_chkPrintFps.IsChecked == true)
                    EnsureFpsOverlay();
                _overlayTimer.Start();
                UpdateUiStates();
            }
            catch (Exception ex)
            {
                AppendLog("[오류] 실행 실패: " + ex.Message);
                _scrcpyProcess = null;
                UpdateUiStates();
            }
        }

        private async void BtnScreenshot_Click(object sender, RoutedEventArgs e)
        {
            if (_isCapturingScreenshot) return;
            var dev = _cmbDevices.SelectedItem as DeviceInfo;
            if (!_adb.IsValid || dev == null || dev.State != "device")
            {
                if (sender != null)
                    MessageBox.Show("먼저 연결된 기기를 선택해 주세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string saveDir = GetEffectiveRecordFolder();
            try
            {
                if (!Directory.Exists(saveDir)) Directory.CreateDirectory(saveDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show("저장 폴더를 생성할 수 없습니다: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string fileName = string.Format("screenshot_{0:yyyyMMdd_HHmmss}.png", DateTime.Now);
            string fullPath = Path.Combine(saveDir, fileName);

            _isCapturingScreenshot = true;
            _btnScreenshot.IsEnabled = false;
            AppendLog("[캡처] 화면을 캡처하는 중...");
            try
            {
                // If scrcpy window is open and visible (especially for --new-display virtual screens), capture its client area directly
                bool capturedFromWindow = false;
                if (_scrcpyProcess != null && !_scrcpyProcess.HasExited)
                {
                    _scrcpyProcess.Refresh();
                    IntPtr hwnd = _scrcpyProcess.MainWindowHandle;
                    if (hwnd != IntPtr.Zero && !IsIconic(hwnd))
                    {
                        capturedFromWindow = TryCaptureWindowClientToPng(hwnd, fullPath);
                    }
                }

                bool savedOk = false;
                if (capturedFromWindow)
                {
                    savedOk = true;
                    AppendLog("[캡처] 미러링 화면 저장 완료: " + fullPath);
                }
                else
                {
                    var res = await _adb.CaptureScreenshotAsync(dev.Serial, fullPath, _activeDisplayId);
                    if (res.Item1)
                    {
                        savedOk = true;
                        AppendLog("[캡처] 기기 화면 저장 완료: " + res.Item2);
                    }
                    else
                    {
                        AppendLog("[캡처 실패] " + res.Item2);
                    }
                }

                if (savedOk && _scrcpyProcess != null && !_scrcpyProcess.HasExited)
                {
                    if (_keymapOverlay == null)
                    {
                        _keymapOverlay = new KeymapOverlayWindow();
                        _keymapOverlay.ProfileEdited += () => RefreshKeyListBox();
                        _keymapOverlay.SetShowHints(false);
                    }
                    _keymapOverlay.ShowToast("📸 스크린샷 저장됨 (" + fileName + ")");
                    if (!_overlayTimer.IsEnabled) _overlayTimer.Start();
                    SyncFpsOverlay();
                }
            }
            finally
            {
                _isCapturingScreenshot = false;
                UpdateUiStates();
            }
        }

        private bool TryCaptureWindowClientToPng(IntPtr hwnd, string outputPath)
        {
            try
            {
                RECT rc;
                if (!GetClientRect(hwnd, out rc)) return false;
                int w = rc.Right - rc.Left;
                int h = rc.Bottom - rc.Top;
                if (w <= 0 || h <= 0) return false;

                using (var bmp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (var g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        IntPtr hdc = g.GetHdc();
                        bool ok = PrintWindow(hwnd, hdc, 3); // PW_CLIENTONLY (1) | PW_RENDERFULLCONTENT (2)
                        g.ReleaseHdc(hdc);
                        if (!ok) return false;
                    }
                    bmp.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
                }
                return File.Exists(outputPath) && new FileInfo(outputPath).Length > 1024;
            }
            catch
            {
                return false;
            }
        }

        private void BtnRecordToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_recordProcess != null && !_recordProcess.HasExited)
                StopRecording();
            else
                StartRecording();
        }

        private void StartRecording()
        {
            var dev = _cmbDevices.SelectedItem as DeviceInfo;
            if (!_adb.IsValid || dev == null || dev.State != "device")
            {
                MessageBox.Show("먼저 연결된 기기를 선택해 주세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string saveDir = GetEffectiveRecordFolder();
            try
            {
                if (!Directory.Exists(saveDir)) Directory.CreateDirectory(saveDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show("저장 폴더를 생성할 수 없습니다: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string fileName = string.Format("record_{0:yyyyMMdd_HHmmss}.mp4", DateTime.Now);
            _currentRecordPath = Path.Combine(saveDir, fileName);

            var recArgs = new List<string>();
            recArgs.Add(string.Format("-s \"{0}\"", dev.Serial));
            recArgs.Add("--no-window");
            recArgs.Add("--no-control");

            if (!string.IsNullOrEmpty(_activeDisplayId))
                recArgs.Add("--display-id=" + _activeDisplayId);

            string codec = GetSelectedTag(_cmbCodec, "h264");
            recArgs.Add("--video-codec=" + codec);

            int bitrate;
            if (int.TryParse(_txtBitrate.Text.Trim(), out bitrate) && bitrate > 0)
                recArgs.Add("-b" + bitrate + "M");

            if (_chkNoAudio.IsChecked == true)
                recArgs.Add("--no-audio");

            recArgs.Add(string.Format("--record=\"{0}\"", _currentRecordPath));

            string argLine = string.Join(" ", recArgs);
            AppendLog("[녹화 시작] " + _currentRecordPath);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _adb.ScrcpyPath,
                    Arguments = argLine,
                    WorkingDirectory = _adb.ScrcpyDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                _recordProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _recordProcess.OutputDataReceived += (s, ev) =>
                {
                    if (ev.Data != null && (ev.Data.IndexOf("Recording", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            ev.Data.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0))
                        Dispatcher.BeginInvoke(new Action(() => AppendLog("[녹화] " + ev.Data)));
                };
                _recordProcess.ErrorDataReceived += (s, ev) =>
                {
                    if (ev.Data != null && ev.Data.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0)
                        Dispatcher.BeginInvoke(new Action(() => AppendLog("[녹화 오류] " + ev.Data)));
                };
                _recordProcess.Exited += (s, ev) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _recordTimer.Stop();
                        if (File.Exists(_currentRecordPath) && new FileInfo(_currentRecordPath).Length > 0)
                            AppendLog("[녹화] 저장 완료: " + _currentRecordPath);
                        _recordProcess = null;
                        UpdateUiStates();
                    }));
                };

                _recordStartTime = DateTime.Now;
                _recordProcess.Start();
                _recordProcess.BeginOutputReadLine();
                _recordProcess.BeginErrorReadLine();
                _recordTimer.Start();
                UpdateUiStates();
            }
            catch (Exception ex)
            {
                AppendLog("[녹화 오류] 시작 실패: " + ex.Message);
                _recordProcess = null;
                _recordTimer.Stop();
                UpdateUiStates();
            }
        }

        private void StopRecording()
        {
            if (_recordProcess == null) return;
            var proc = _recordProcess;
            _recordTimer.Stop();

            try
            {
                if (!proc.HasExited)
                {
                    AppendLog("[녹화] 녹화를 마무리하고 저장하는 중...");
                    bool gracefullyStopped = false;
                    try
                    {
                        if (AttachConsole((uint)proc.Id))
                        {
                            SetConsoleCtrlHandler(null, true);
                            GenerateConsoleCtrlEvent(0, 0);
                            gracefullyStopped = proc.WaitForExit(3000);
                            FreeConsole();
                            SetConsoleCtrlHandler(null, false);
                        }
                    }
                    catch { }

                    if (!gracefullyStopped && !proc.HasExited)
                    {
                        proc.Kill();
                        proc.WaitForExit(1000);
                    }
                }
            }
            catch { }
            finally
            {
                _recordProcess = null;
                UpdateUiStates();
            }
        }

        private void HandleScrcpyOutput(string line)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var md = Regex.Match(line, @"New display:\s*(\d+)x(\d+).*\(id=(\d+)\)");
                if (md.Success)
                {
                    int w, h;
                    if (int.TryParse(md.Groups[1].Value, out w) && int.TryParse(md.Groups[2].Value, out h))
                    {
                        _activeDisplayWidth = w;
                        _activeDisplayHeight = h;
                        _controlClient.SetScreenSize(w, h);
                    }
                    _activeDisplayId = md.Groups[3].Value;
                }
                else
                {
                    var mt = Regex.Match(line, @"Texture.*:\s*(\d+)x(\d+)");
                    if (mt.Success)
                    {
                        if (string.IsNullOrEmpty(_activeDisplayId))
                        {
                            int w, h;
                            if (int.TryParse(mt.Groups[1].Value, out w) && int.TryParse(mt.Groups[2].Value, out h))
                            {
                                _activeDisplayWidth = w;
                                _activeDisplayHeight = h;
                                _controlClient.SetScreenSize(w, h);
                            }
                        }

                        // Auto-start keymap in Keymap mode, or in Gamepad mode if Keyboard Assist is checked
                        string curMode = GetControlMode();
                        bool wantAutoKm = (curMode == "keymap") || (curMode == "gamepad" && _chkGamepadKbAssist != null && _chkGamepadKbAssist.IsChecked == true);
                        if (!_autoKeymapTriggered && wantAutoKm && _hookEngine != null && !_hookEngine.IsActive)
                        {
                            _autoKeymapTriggered = true;
                            BtnKmStart_Click(null, null);
                        }
                    }
                }

                var m = Regex.Match(line, @"INFO:\s+(\d+)\s+fps(?:\s+(\(\+\d+\s+frames\s+skipped\)))?");
                if (m.Success)
                {
                    int fps;
                    if (int.TryParse(m.Groups[1].Value, out fps))
                    {
                        _bdFpsBadge.Visibility = Visibility.Visible;
                        _txtFpsBadge.Text = "FPS: " + fps;

                        EnsureFpsOverlay();
                        if (!_overlayTimer.IsEnabled) _overlayTimer.Start();
                        string extra = m.Groups[2].Success ? m.Groups[2].Value : "";
                        _fpsOverlay.UpdateFps(fps, extra);
                    }
                    // Keep the log window clean by not printing repetitive FPS lines
                    return;
                }

                if (line.IndexOf("FPS counter stopped", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _bdFpsBadge.Visibility = Visibility.Collapsed;
                    HideFpsOverlay();
                    return;
                }

                AppendLog(line);
            }));
        }

        private void EnsureFpsOverlay()
        {
            if (_fpsOverlay == null)
                _fpsOverlay = new FpsOverlayWindow();
        }

        private void HideFpsOverlay()
        {
            _overlayTimer.Stop();
            if (_fpsOverlay != null && _fpsOverlay.IsVisible)
                _fpsOverlay.Hide();
            if (_keymapOverlay != null && _keymapOverlay.IsVisible)
                _keymapOverlay.Hide();
            if (_cursorHider != null)
                _cursorHider.HideSafe();
        }

        private void SyncFpsOverlay()
        {
            if (_scrcpyProcess == null || _scrcpyProcess.HasExited)
            {
                HideFpsOverlay();
                return;
            }

            try
            {
                _scrcpyProcess.Refresh();
                IntPtr targetHwnd = _scrcpyProcess.MainWindowHandle;
                IntPtr launcherHwnd = new WindowInteropHelper(this).Handle;

                int vidW = _activeDisplayWidth > 0 ? _activeDisplayWidth : (_controlClient != null ? _controlClient.ScreenWidth : 0);
                int vidH = _activeDisplayHeight > 0 ? _activeDisplayHeight : (_controlClient != null ? _controlClient.ScreenHeight : 0);

                if (_fpsOverlay != null && _chkPrintFps.IsChecked == true)
                    _fpsOverlay.SyncToTargetWindow(targetHwnd, launcherHwnd);

                if (_hookEngine != null && _hookEngine.IsActive)
                {
                    _hookEngine.SetTargetWindow(targetHwnd);
                    _hookEngine.SetVideoSize(vidW, vidH);
                    _hookEngine.CheckFocusSafety();
                }

                if (_keymapOverlay != null && (_keymapOverlay.IsEditMode || _keymapOverlay.HasActiveToast || (_hookEngine.IsActive && _chkKmHints.IsChecked == true)))
                {
                    _keymapOverlay.SyncToTargetWindow(targetHwnd, launcherHwnd, vidW, vidH);
                }
            }
            catch
            {
                HideFpsOverlay();
            }
        }

        private void LoadKeymapProfiles(string selectName = null)
        {
            _keymapProfiles.Clear();
            _keymapProfiles.AddRange(KeymapProfile.GetBuiltInPresets());
            _keymapProfiles.AddRange(KeymapProfile.LoadCustomProfiles());

            _cmbKeyProfiles.Items.Clear();
            for (int i = 0; i < _keymapProfiles.Count; i++)
            {
                _keymapProfiles[i].SlotIndex = i + 1;
                _cmbKeyProfiles.Items.Add(_keymapProfiles[i]);
            }

            _cmbKeyProfiles.DisplayMemberPath = "DisplayName";
            if (_cmbKeyProfiles.Items.Count > 0)
            {
                string want = !string.IsNullOrEmpty(selectName)
                    ? selectName
                    : (_settings != null ? _settings.SelectedKeymapProfile : "");
                int pick = 0;
                if (!string.IsNullOrEmpty(want))
                {
                    for (int i = 0; i < _keymapProfiles.Count; i++)
                    {
                        if (_keymapProfiles[i].Name == want) { pick = i; break; }
                    }
                }
                _cmbKeyProfiles.SelectedIndex = pick;
                _activeProfile = _keymapProfiles[pick];
                ApplyProfileToUi();
                UpdateModeSummary();
                UpdateUiStates();
            }
        }

        private void ApplyProfileToUi()
        {
            if (_activeProfile == null) return;
            bool prevInit = _isInitializing;
            _isInitializing = true;
            try
            {
                _chkKmJoy.IsChecked = _activeProfile.EnableJoystick;
                _chkKmCam.IsChecked = _activeProfile.EnableMouseCamera;
                _chkKmLeft.IsChecked = _activeProfile.EnableLeftClick;
                _chkKmRight.IsChecked = _activeProfile.EnableRightClick;
                _chkKmRightWheel.IsChecked = _activeProfile.RightClickIsWheelDrag;
                _chkKmRightWheel.IsEnabled = _activeProfile.EnableRightClick;
                SelectComboByTag(_cmbKmToggleKey, _activeProfile.CameraToggleVk.ToString(), 0);
                double sx = Math.Max(0.1, Math.Min(3.0, _activeProfile.CameraSensX > 0 ? _activeProfile.CameraSensX : 1.0));
                double sy = Math.Max(0.1, Math.Min(3.0, _activeProfile.CameraSensY > 0 ? _activeProfile.CameraSensY : 1.0));
                _sldKmSensX.Value = sx;
                _txtKmSensXVal.Text = sx.ToString("0.00");
                _sldKmSensY.Value = sy;
                _txtKmSensYVal.Text = sy.ToString("0.00");
                RefreshKeyListBox();
            }
            finally
            {
                _isInitializing = prevInit;
            }
        }

        private void SyncProfileFromUi()
        {
            if (_activeProfile == null) return;
            _activeProfile.EnableJoystick = _chkKmJoy.IsChecked == true;
            _activeProfile.EnableMouseCamera = _chkKmCam.IsChecked == true;
            _activeProfile.EnableLeftClick = _chkKmLeft.IsChecked == true;
            _activeProfile.EnableRightClick = _chkKmRight.IsChecked == true;
            _activeProfile.RightClickIsWheelDrag = _chkKmRightWheel.IsChecked == true;
            _chkKmRightWheel.IsEnabled = _activeProfile.EnableRightClick;

            int tk;
            if (int.TryParse(GetSelectedTag(_cmbKmToggleKey, "18"), out tk))
                _activeProfile.CameraToggleVk = tk;

            _activeProfile.CameraSensX = Math.Round(_sldKmSensX.Value, 2);
            _activeProfile.CameraSensY = Math.Round(_sldKmSensY.Value, 2);
        }

        private void RefreshKeyListBox()
        {
            _lstKmKeys.Items.Clear();
            if (_activeProfile == null) return;
            foreach (var k in _activeProfile.Keys)
                _lstKmKeys.Items.Add(k);
        }

        private async void BtnKmStart_Click(object sender, RoutedEventArgs e)
        {
            var dev = _cmbDevices.SelectedItem as DeviceInfo;
            if (dev == null || dev.State != "device" || _scrcpyProcess == null || _scrcpyProcess.HasExited)
            {
                MessageBox.Show("먼저 [연결 및 영상] 탭에서 미러링을 시작해 주세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_activeProfile == null) return;

            SyncProfileFromUi();
            _btnKmStart.IsEnabled = false;
            AppendLog("[키 매핑] 멀티터치 제어 소켓 연결 중...");

            var res = await _controlClient.ConnectAsync(dev.Serial, _detectedScrcpyVersion, _activeDisplayId, _activeDisplayWidth, _activeDisplayHeight);
            AppendLog("[키 매핑] " + res.Item2);
            if (!res.Item1)
            {
                UpdateUiStates();
                return;
            }

            _scrcpyProcess.Refresh();
            bool kbAssistOnly = (GetControlMode() == "gamepad");
            if (kbAssistOnly && _activeProfile != null && _activeProfile.IsBuiltIn && _activeProfile.EnableJoystick)
            {
                for (int i = 0; i < _keymapProfiles.Count; i++)
                {
                    if (_keymapProfiles[i].Name.Contains("게임패드 보조"))
                    {
                        _cmbKeyProfiles.SelectedIndex = i;
                        _activeProfile = _keymapProfiles[i];
                        break;
                    }
                }
            }
            _hookEngine.KeyboardOnlyAssist = kbAssistOnly;
            _hookEngine.SetProfile(_activeProfile);
            _hookEngine.SetTargetWindow(_scrcpyProcess.MainWindowHandle);
            _hookEngine.Start();

            if (_keymapOverlay == null)
            {
                _keymapOverlay = new KeymapOverlayWindow();
                _keymapOverlay.ProfileEdited += () => RefreshKeyListBox();
            }
            _keymapOverlay.SetShowHints(_chkKmHints.IsChecked == true);
            _keymapOverlay.SetHintAppearance(_sldHintOpacity.Value, _sldHintScale.Value);
            _keymapOverlay.SetProfile(_activeProfile);
            _keymapOverlay.SetEditMode(false);
            if (!_overlayTimer.IsEnabled) _overlayTimer.Start();

            if (kbAssistOnly)
                AppendLog(string.Format("[키 매핑] 게임패드 키보드 보조 매핑 활성화! (프로파일: {0} · WASD/마우스 제외)", _activeProfile.Name));
            else
                AppendLog(string.Format("[키 매핑] 활성화 완료! (프로파일: {0} / 시점 토글: {1})",
                    _activeProfile.Name, KeyNameHelper.GetName(_activeProfile.CameraToggleVk)));
            UpdateUiStates();
        }

        private void StopKeymap()
        {
            if (_hookEngine != null && _hookEngine.IsActive)
            {
                _hookEngine.Stop();
                AppendLog("[키 매핑] 중지되었습니다.");
            }
            if (_controlClient != null && _controlClient.IsConnected)
                _controlClient.Disconnect();
            if (_keymapOverlay != null && !_keymapOverlay.IsEditMode && _keymapOverlay.IsVisible)
                _keymapOverlay.Hide();
            if (_cursorHider != null)
                _cursorHider.HideSafe();
            UpdateUiStates();
        }

        private void BtnImportKmp_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "LDPlayer 키매핑 파일(.kmp) 선택",
                Filter = "LDPlayer Keymap (*.kmp)|*.kmp|All files (*.*)|*.*"
            };
            if (Directory.Exists(@"C:\LDPlayer"))
                dlg.InitialDirectory = @"C:\LDPlayer";

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var imported = KeymapProfile.ImportFromLdKmp(dlg.FileName);
                    imported.SaveCustom();
                    imported.SlotIndex = _keymapProfiles.Count + 1;
                    _keymapProfiles.Add(imported);
                    _cmbKeyProfiles.Items.Add(imported);
                    _cmbKeyProfiles.SelectedItem = imported;
                    AppendLog(string.Format("[키 매핑] LDPlayer 프리셋 불러오기 완료: {0} (키 {1}개)", imported.Name, imported.Keys.Count));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("불러오기 실패: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_activeProfile == null) return;
            SyncProfileFromUi();
            if (_activeProfile.IsBuiltIn)
            {
                BtnSaveAsProfile_Click(sender, e);
                return;
            }
            _activeProfile.SaveCustom();
            AppendLog("[키 매핑] 프로파일 저장 완료: " + _activeProfile.Name);
        }

        private void BtnSaveAsProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_activeProfile == null) return;
            SyncProfileFromUi();
            string defaultName = _activeProfile.Name.Replace("[기본] ", "") + " (커스텀)";
            string newName = PromptTextDialog("다른 이름으로 프로파일 저장", "새 프로파일 이름을 입력하세요 (예: 태도, 헤비보우건, 활):", defaultName);
            if (string.IsNullOrWhiteSpace(newName)) return;

            var copy = _activeProfile.Clone(newName.Trim());
            copy.SaveCustom();
            LoadKeymapProfiles(copy.Name);
            AppendLog("[키 매핑] 새 프로파일 저장 완료: " + copy.Name);
        }

        private void BtnRenameProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_activeProfile == null || _activeProfile.IsBuiltIn) return;
            string newName = PromptTextDialog("프로파일 이름 변경", "변경할 프로파일 이름을 입력하세요:", _activeProfile.Name);
            if (string.IsNullOrWhiteSpace(newName) || newName.Trim() == _activeProfile.Name) return;

            SyncProfileFromUi();
            _activeProfile.Name = newName.Trim();
            _activeProfile.SaveCustom();
            LoadKeymapProfiles(_activeProfile.Name);
            AppendLog("[키 매핑] 프로파일 이름 변경 완료: " + newName.Trim());
        }

        private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_activeProfile == null || _activeProfile.IsBuiltIn) return;
            var res = MessageBox.Show(this, "'" + _activeProfile.Name + "' 프로파일을 삭제할까요?", "프로파일 삭제", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) return;

            string deletedName = _activeProfile.Name;
            _activeProfile.DeleteCustomFile();
            LoadKeymapProfiles();
            AppendLog("[키 매핑] 프로파일 삭제 완료: " + deletedName);
        }

        private void BtnExportCode_Click(object sender, RoutedEventArgs e)
        {
            if (_activeProfile == null) return;
            try
            {
                SyncProfileFromUi();
                string code = _activeProfile.ExportToShareCode();
                Clipboard.SetText(code);
                AppendLog(string.Format("[키 매핑] 프리셋 공유 코드 복사 완료: {0} ({1}자)", _activeProfile.Name, code.Length));
                MessageBox.Show(this,
                    "현재 프로파일의 공유 코드가 클립보드에 복사되었습니다!\n\n커뮤니티나 메신저에 그대로 붙여넣어 공유할 수 있습니다.",
                    "프리셋 코드 복사 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "코드 복사 실패: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnImportCode_Click(object sender, RoutedEventArgs e)
        {
            string prefill = "";
            try
            {
                if (Clipboard.ContainsText())
                {
                    string clip = Clipboard.GetText().Trim();
                    if (clip.StartsWith("SLKM2:", StringComparison.OrdinalIgnoreCase) ||
                        clip.StartsWith("SLKM1:", StringComparison.OrdinalIgnoreCase))
                        prefill = clip;
                }
            }
            catch { }

            string input = PromptTextDialog("프리셋 코드 불러오기", "공유받은 프리셋 코드(SLKM2:... 또는 SLKM1:...)를 붙여넣으세요:", prefill);
            if (string.IsNullOrWhiteSpace(input)) return;

            try
            {
                var imported = KeymapProfile.ImportFromShareCode(input);
                imported.SaveCustom();
                LoadKeymapProfiles(imported.Name);
                AppendLog(string.Format("[키 매핑] 프리셋 코드 불러오기 완료: {0} (키 {1}개)", imported.Name, imported.Keys.Count));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "프리셋 코드 불러오기 실패:\n" + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string PromptTextDialog(string title, string message, string defaultValue)
        {
            var win = new Window
            {
                Title = title,
                Width = 390,
                Height = 165,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC")),
                FontFamily = FontFamily
            };

            var stack = new StackPanel { Margin = new Thickness(16) };
            stack.Children.Add(new TextBlock { Text = message, FontSize = 12, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")), Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap });
            var tb = new TextBox { Text = defaultValue ?? "", Height = 30, Padding = new Thickness(6, 4, 6, 4), FontSize = 12.5, Margin = new Thickness(0, 0, 0, 12) };
            stack.Children.Add(tb);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk = new Button { Content = "확인", Width = 76, Height = 28, IsDefault = true, Margin = new Thickness(0, 0, 6, 0), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")), Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            var btnCancel = new Button { Content = "취소", Width = 76, Height = 28, IsCancel = true, Cursor = Cursors.Hand };
            btnOk.Click += (s, ev) => { win.DialogResult = true; win.Close(); };
            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(btnPanel);

            win.Content = stack;
            win.Loaded += (s, ev) => { tb.Focus(); tb.SelectAll(); };
            return win.ShowDialog() == true ? tb.Text : null;
        }

        private void BtnKmEditOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (_scrcpyProcess == null || _scrcpyProcess.HasExited || _activeProfile == null)
            {
                MessageBox.Show("미러링이 실행 중일 때만 화면 위에서 키를 배치할 수 있습니다.", "안내", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_keymapOverlay == null)
            {
                _keymapOverlay = new KeymapOverlayWindow();
                _keymapOverlay.ProfileEdited += () => RefreshKeyListBox();
            }
            _keymapOverlay.SetShowHints(_chkKmHints.IsChecked == true);
            _keymapOverlay.SetHintAppearance(_sldHintOpacity.Value, _sldHintScale.Value);
            _keymapOverlay.SetProfile(_activeProfile);
            _keymapOverlay.SetEditMode(!_keymapOverlay.IsEditMode);
            if (!_overlayTimer.IsEnabled) _overlayTimer.Start();
            SyncFpsOverlay();
        }

        private void BtnKmAddKey_Click(object sender, RoutedEventArgs e)
        {
            if (_activeProfile == null) return;
            if (_pendingNewKeyVk <= 0)
            {
                MessageBox.Show("왼쪽 입력 칸을 클릭한 뒤 매핑할 키보드 키(또는 조합키/마우스 4·5)를 눌러주세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var item = new KeyBindItem
            {
                VkCode = _pendingNewKeyVk,
                Modifiers = _pendingNewKeyMods,
                Label = (_txtKmNewLabel.Text ?? "").Trim(),
                X = 5000,
                Y = 5000,
                IsWheelDrag = _chkKmNewWheel.IsChecked == true,
                WheelRadius = 1500
            };
            _activeProfile.Keys.Add(item);
            RefreshKeyListBox();
            if (_keymapOverlay != null) _keymapOverlay.Redraw();
            _pendingNewKeyVk = 0;
            _pendingNewKeyMods = 0;
            _txtKmNewKey.Text = "클릭 후 키 누름";
            _txtKmNewKey.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
            _txtKmNewLabel.Text = "";
            _chkKmNewWheel.IsChecked = false;
        }

        private void BtnKmDelKey_Click(object sender, RoutedEventArgs e)
        {
            if (_activeProfile == null) return;
            var sel = _lstKmKeys.SelectedItem as KeyBindItem;
            if (sel == null) return;
            _activeProfile.Keys.Remove(sel);
            RefreshKeyListBox();
            if (_keymapOverlay != null) _keymapOverlay.Redraw();
        }

        private async Task ScanCurrentQuestAsync(bool forceAdd)
        {
            if (_isQuestScanningNow) return;
            if (_scrcpyProcess == null || _scrcpyProcess.HasExited)
            {
                AppendLog("[퀘스트] 미러링이 실행 중일 때만 스캔할 수 있습니다.");
                return;
            }

            _isQuestScanningNow = true;
            try
            {
                _scrcpyProcess.Refresh();
                IntPtr hwnd = _scrcpyProcess.MainWindowHandle;
                int vidW = _activeDisplayWidth > 0 ? _activeDisplayWidth : (_controlClient != null ? _controlClient.ScreenWidth : 0);
                int vidH = _activeDisplayHeight > 0 ? _activeDisplayHeight : (_controlClient != null ? _controlClient.ScreenHeight : 0);

                var res = await QuestOcrService.ScanQuestDetailFromWindowAsync(hwnd, vidW, vidH);
                if (res.Item1 == null)
                {
                    AppendLog("[퀘스트] " + res.Item2);
                    return;
                }

                QuestEntry entry = res.Item1;
                _scannedQuests.Add(entry);
                QuestOcrService.AppendToQuestLog(entry);
                RefreshQuestPlannerUi();

                string spPart = string.IsNullOrEmpty(entry.Species) ? "" : (" · " + entry.Species);
                string actPart = (string.IsNullOrEmpty(entry.ActionType) || entry.ActionType == "기타") ? "" : (" " + entry.ActionType);
                string wepPart = string.IsNullOrEmpty(entry.WeaponType) ? "" : (" (" + entry.WeaponType + ")");
                string note = string.Format("[{0}/10] {1}{2}{3}{4}",
                    _scannedQuests.Count, entry.Region, spPart, actPart, wepPart);
                if (_scannedQuests.Count >= 10)
                {
                    var plans = QuestOcrService.BuildSimultaneousPlans(_scannedQuests);
                    note = string.Format("[10/10 완료] {0}", QuestOcrService.GetTopRecommendation(plans, _scannedQuests.Count));
                }

                AppendLog("[퀘스트] " + note);
                if (_keymapOverlay == null)
                {
                    _keymapOverlay = new KeymapOverlayWindow();
                    _keymapOverlay.ProfileEdited += () => RefreshKeyListBox();
                    _keymapOverlay.SetShowHints(false);
                }
                _keymapOverlay.ShowToast(note);
                if (!_overlayTimer.IsEnabled) _overlayTimer.Start();
                SyncFpsOverlay();
            }
            finally
            {
                _isQuestScanningNow = false;
            }
        }

        private void RefreshQuestPlannerUi()
        {
            _txtQuestCount.Text = string.Format("보유: {0} / 10", _scannedQuests.Count);

            var plans = QuestOcrService.BuildSimultaneousPlans(_scannedQuests);
            _txtQuestBanner.Text = QuestOcrService.GetTopRecommendation(plans, _scannedQuests.Count);

            _pnlQuestPlans.Children.Clear();
            if (_scannedQuests.Count == 0)
            {
                _pnlQuestPlans.Children.Add(new TextBlock
                {
                    Text = "아직 수집된 퀘스트가 없습니다. 게임에서 퀘스트를 선택한 뒤 [F8]을 눌러 수집하세요.",
                    FontSize = 12,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(4, 12, 4, 12)
                });
                return;
            }

            foreach (var plan in plans)
            {
                var capturedPlan = plan;
                bool isNest = plan.IsNestGroup;
                bool isMulti = plan.Count >= 2;
                string accentHex = isNest ? "#7C3AED" : (isMulti ? "#2563EB" : "#94A3B8");
                string borderHex = isNest ? "#DDD6FE" : (isMulti ? "#BFDBFE" : "#E2E8F0");
                string bgHex = isNest ? "#FDFBFF" : (isMulti ? "#FFFFFF" : "#F8FAFC");

                var card = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex)),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(borderHex)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(9),
                    Margin = new Thickness(0, 0, 0, 8),
                    ClipToBounds = true
                };

                var outerGrid = new Grid();
                outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
                outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var accentBar = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentHex)),
                    CornerRadius = new CornerRadius(8, 0, 0, 8)
                };
                Grid.SetColumn(accentBar, 0);
                outerGrid.Children.Add(accentBar);

                var sp = new StackPanel { Margin = new Thickness(11, 8, 10, 8) };

                // Header row: [Region - Species] + Action pill + Weapon pill + Count pill + [✓ 완료]
                var hdrGrid = new Grid();
                hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var tagWrap = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
                tagWrap.Children.Add(new TextBlock
                {
                    Text = plan.RegionTitle + " · " + plan.SpeciesTitle,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isNest ? "#5B21B6" : (isMulti ? "#0F172A" : "#334155"))),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 7, 0)
                });

                // Action Pill (★포획 / ★토벌 / ★사냥)
                string actBg = plan.ActionBadge == "★포획" ? "#FEF3C7" : (plan.ActionBadge == "★토벌" ? "#FEE2E2" : "#DBEAFE");
                string actFg = plan.ActionBadge == "★포획" ? "#B45309" : (plan.ActionBadge == "★토벌" ? "#B91C1C" : "#1D4ED8");
                tagWrap.Children.Add(new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(actBg)),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(6, 1.5, 6, 1.5),
                    Margin = new Thickness(0, 0, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = plan.ActionBadge,
                        FontSize = 10.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(actFg))
                    }
                });

                // Weapon Pill (원거리 무기 / 근거리 무기)
                if (!string.IsNullOrEmpty(plan.WeaponBadge))
                {
                    tagWrap.Children.Add(new Border
                    {
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCFBF1")),
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(6, 1.5, 6, 1.5),
                        Margin = new Thickness(0, 0, 5, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock
                        {
                            Text = plan.WeaponBadge,
                            FontSize = 10.5,
                            FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F766E"))
                        }
                    });
                }

                // Count Pill
                tagWrap.Children.Add(new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isMulti ? "#2563EB" : "#E2E8F0")),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(7, 1.5, 7, 1.5),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = isMulti ? (plan.Count + "개 동시") : "1개",
                        FontSize = 10.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = isMulti ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569"))
                    }
                });

                Grid.SetColumn(tagWrap, 0);
                hdrGrid.Children.Add(tagWrap);

                var btnDoneGroup = new Button
                {
                    Content = "✓ 완료",
                    Height = 24,
                    Padding = new Thickness(10, 1, 10, 1),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isNest ? "#7C3AED" : (isMulti ? "#2563EB" : "#64748B"))),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                btnDoneGroup.Click += (s, e) =>
                {
                    int doneCnt = capturedPlan.Quests.Count;
                    foreach (var q in capturedPlan.Quests)
                        _scannedQuests.Remove(q);
                    IncrementDailyQuestCleared(doneCnt);
                    RefreshQuestPlannerUi();
                    AppendLog(string.Format("[퀘스트] {0} 완료 처리! (+{1}건 · 오늘 누적 {2}건 · 남은 퀘스트: {3}/10)",
                        capturedPlan.Header, doneCnt, _settings.DailyQuestClearedCount, _scannedQuests.Count));
                };
                Grid.SetColumn(btnDoneGroup, 1);
                hdrGrid.Children.Add(btnDoneGroup);
                sp.Children.Add(hdrGrid);

                // Target monsters pill strip (omitted for pure Radiant or Nest)
                if (!string.IsNullOrEmpty(plan.TargetMonsters))
                {
                    sp.Children.Add(new Border
                    {
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(8, 3.5, 8, 3.5),
                        Margin = new Thickness(0, 6, 0, 4),
                        Child = new TextBlock
                        {
                            Text = plan.TargetMonsters,
                            FontSize = 11,
                            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569")),
                            TextWrapping = TextWrapping.Wrap
                        }
                    });
                }

                // Thin divider
                sp.Children.Add(new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),
                    Margin = new Thickness(0, 4, 0, 4)
                });

                // Individual quest rows (clean objective text without [아무 필드] prefix!)
                foreach (var q in plan.Quests)
                {
                    var capturedQ = q;
                    var qRow = new Grid { Margin = new Thickness(2, 2, 0, 2) };
                    qRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    qRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    string prog = string.IsNullOrEmpty(q.Progress) ? "" : (" (" + q.Progress + ")");
                    var tbQ = new TextBlock
                    {
                        Text = "• " + q.ObjectiveText + prog,
                        FontSize = 11.5,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")),
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    };
                    Grid.SetColumn(tbQ, 0);
                    qRow.Children.Add(tbQ);

                    var btnDoneOne = new Button
                    {
                        Content = "✓",
                        Width = 22,
                        Height = 20,
                        Padding = new Thickness(0),
                        FontSize = 10.5,
                        FontWeight = FontWeights.Bold,
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")),
                        BorderThickness = new Thickness(0),
                        Cursor = Cursors.Hand,
                        Margin = new Thickness(6, 0, 0, 0),
                        ToolTip = "이 퀘스트만 완료 처리"
                    };
                    btnDoneOne.Click += (s, e) =>
                    {
                        _scannedQuests.Remove(capturedQ);
                        IncrementDailyQuestCleared(1);
                        RefreshQuestPlannerUi();
                    };
                    Grid.SetColumn(btnDoneOne, 1);
                    qRow.Children.Add(btnDoneOne);

                    sp.Children.Add(qRow);
                }

                Grid.SetColumn(sp, 1);
                outerGrid.Children.Add(sp);
                card.Child = outerGrid;
                _pnlQuestPlans.Children.Add(card);
            }
        }

        private void ApplyAlwaysOnTopLive(bool top)
        {
            try
            {
                if (_scrcpyProcess != null && !_scrcpyProcess.HasExited)
                {
                    _scrcpyProcess.Refresh();
                    IntPtr hwnd = _scrcpyProcess.MainWindowHandle;
                    if (hwnd != IntPtr.Zero)
                    {
                        SetWindowPos(hwnd, top ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0,
                            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                    }
                }
            }
            catch { }
        }

        private string GetDailyQuestDateKey()
        {
            // Monster Hunter Outlanders daily reset is 05:00 AM KST (UTC+9 - 5h = UTC+4)
            return DateTime.UtcNow.AddHours(4).ToString("yyyy-MM-dd");
        }

        private void EnsureDailyQuestReset()
        {
            if (_settings == null) return;
            string todayKey = GetDailyQuestDateKey();
            if (_settings.DailyQuestDateKey != todayKey)
            {
                _settings.DailyQuestDateKey = todayKey;
                _settings.DailyQuestClearedCount = 0;
                _settings.Save();
            }
        }

        private void IncrementDailyQuestCleared(int delta)
        {
            if (_settings == null || delta <= 0) return;
            EnsureDailyQuestReset();
            _settings.DailyQuestClearedCount += delta;
            _settings.Save();
            UpdateDailyQuestCounterUi();
        }

        private void UpdateDailyQuestCounterUi()
        {
            if (_txtQuestDaily == null || _settings == null) return;
            EnsureDailyQuestReset();
            _txtQuestDaily.Text = string.Format("오늘 완료: {0}건", _settings.DailyQuestClearedCount);
        }

        private void StopScrcpy()
        {
            try
            {
                if (_scrcpyProcess != null && !_scrcpyProcess.HasExited)
                {
                    // Close main window gracefully first so MP4 recording finalizes cleanly
                    if (!_scrcpyProcess.CloseMainWindow() || !_scrcpyProcess.WaitForExit(1500))
                    {
                        _scrcpyProcess.Kill();
                    }
                    AppendLog("[중지] 사용자가 미러링을 중지했습니다.");
                }
            }
            catch { }
            finally
            {
                _scrcpyProcess = null;
                _bdFpsBadge.Visibility = Visibility.Collapsed;
                HideFpsOverlay();
                UpdateUiStates();
            }
        }

        private void AppendLog(string text)
        {
            _txtLog.AppendText(text + Environment.NewLine);
            if (_txtLog.LineCount > 400)
            {
                int cut = _txtLog.Text.IndexOf(Environment.NewLine);
                if (cut > 0) _txtLog.Text = _txtLog.Text.Substring(cut + Environment.NewLine.Length);
            }
            _txtLog.ScrollToEnd();
        }

        private void ApplySettingsToUi()
        {
            _txtWifiAddr.Text = _settings.LastWirelessAddress ?? "";
            SelectComboByTag(_cmbCodec, _settings.Codec, 0);
            _txtBitrate.Text = (_settings.BitrateMbps > 0 ? _settings.BitrateMbps : 20).ToString();
            SelectComboByTagOrText(_cmbMaxSize, _settings.MaxSize.ToString());
            SelectComboByTagOrText(_cmbMaxFps, _settings.MaxFps.ToString());
            SelectComboByTag(_cmbRenderer, _settings.RenderDriver, 0);

            string mode = (_settings.ControlMode ?? "keymap").ToLowerInvariant();
            if (mode == "gamepad") _rbModeGamepad.IsChecked = true;
            else if (mode == "normal") _rbModeNormal.IsChecked = true;
            else _rbModeKeymap.IsChecked = true;

            _chkTurnScreenOff.IsChecked = _settings.TurnScreenOff;
            _chkStayAwake.IsChecked = _settings.StayAwake;
            _chkLowLatency.IsChecked = _settings.LowLatency;
            _chkNoAudio.IsChecked = _settings.NoAudio;
            _chkFullscreen.IsChecked = _settings.Fullscreen;
            _chkAlwaysOnTop.IsChecked = _settings.AlwaysOnTop;
            _chkPrintFps.IsChecked = _settings.PrintFps;
            _chkGamepadKbAssist.IsChecked = _settings.GamepadKeyboardAssist;
            EnsureDailyQuestReset();
            UpdateDailyQuestCounterUi();

            _txtRecordFolder.Text = string.IsNullOrWhiteSpace(_settings.RecordFolder)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "recordings")
                : _settings.RecordFolder;

            _chkVirtualDisplay.IsChecked = _settings.UseVirtualDisplay;
            _cmbVdSpec.Text = string.IsNullOrWhiteSpace(_settings.VirtualDisplaySpec) ? "1920x1080/280" : _settings.VirtualDisplaySpec;
            _chkNoVdDecorations.IsChecked = _settings.NoVdSystemDecorations;

            double ho = _settings.OverlayOpacity >= 0.25 && _settings.OverlayOpacity <= 1.0 ? _settings.OverlayOpacity : 1.0;
            double hs = _settings.OverlayScale >= 0.7 && _settings.OverlayScale <= 1.5 ? _settings.OverlayScale : 1.0;
            _sldHintOpacity.Value = ho;
            _txtHintOpacityVal.Text = Math.Round(ho * 100) + "%";
            _sldHintScale.Value = hs;
            _txtHintScaleVal.Text = Math.Round(hs * 100) + "%";

            SelectComboByTag(_cmbLanguage, Loc.CurrentLang, 0);

            _cmbApps.Items.Add(new AppItem { Name = Loc.T("app_none"), PackageName = "" });
            _cmbApps.SelectedIndex = 0;
        }

        private void SaveSettingsFromUi()
        {
            _settings.Language = GetSelectedTag(_cmbLanguage, Loc.CurrentLang);
            _settings.LastWirelessAddress = _txtWifiAddr.Text.Trim();
            _settings.Codec = GetSelectedTag(_cmbCodec, "h264");

            int br;
            _settings.BitrateMbps = int.TryParse(_txtBitrate.Text.Trim(), out br) && br > 0 ? br : 20;

            int ms;
            _settings.MaxSize = int.TryParse(GetSelectedTag(_cmbMaxSize, "0"), out ms) ? ms : 0;

            int fps;
            _settings.MaxFps = int.TryParse(GetSelectedTag(_cmbMaxFps, "0"), out fps) ? fps : 0;

            _settings.ControlMode = GetControlMode();
            if (_activeProfile != null)
                _settings.SelectedKeymapProfile = _activeProfile.Name;

            _settings.RenderDriver = GetSelectedTag(_cmbRenderer, "auto");
            _settings.TurnScreenOff = _chkTurnScreenOff.IsChecked == true;
            _settings.StayAwake = _chkStayAwake.IsChecked == true;
            _settings.LowLatency = _chkLowLatency.IsChecked == true;
            _settings.NoAudio = _chkNoAudio.IsChecked == true;
            _settings.Fullscreen = _chkFullscreen.IsChecked == true;
            _settings.AlwaysOnTop = _chkAlwaysOnTop.IsChecked == true;
            _settings.PrintFps = _chkPrintFps.IsChecked == true;
            _settings.GamepadKeyboardAssist = _chkGamepadKbAssist.IsChecked == true;
            _settings.RecordFolder = _txtRecordFolder.Text.Trim();
            _settings.UseVirtualDisplay = _chkVirtualDisplay.IsChecked == true;
            _settings.VirtualDisplaySpec = _cmbVdSpec.Text.Trim();
            _settings.NoVdSystemDecorations = _chkNoVdDecorations.IsChecked == true;
            _settings.OverlayOpacity = Math.Round(_sldHintOpacity.Value, 2);
            _settings.OverlayScale = Math.Round(_sldHintScale.Value, 2);

            var app = _cmbApps.SelectedItem as AppItem;
            if (app != null)
                _settings.StartAppPackage = app.PackageName;

            _settings.Save();
        }

        private void SelectComboByTag(ComboBox cmb, string tag, int defaultIdx)
        {
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                var item = cmb.Items[i] as ComboBoxItem;
                if (item != null && item.Tag != null && item.Tag.ToString() == tag)
                {
                    cmb.SelectedIndex = i;
                    return;
                }
            }
            cmb.SelectedIndex = defaultIdx;
        }

        private void SelectComboByTagOrText(ComboBox cmb, string val)
        {
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                var item = cmb.Items[i] as ComboBoxItem;
                if (item != null && item.Tag != null && item.Tag.ToString() == val)
                {
                    cmb.SelectedIndex = i;
                    return;
                }
            }
            cmb.Text = val;
        }
    }
}
