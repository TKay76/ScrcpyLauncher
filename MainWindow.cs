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
        private string _detectedScrcpyVersion = "3.2";
        private int _activeDisplayWidth = 1920;
        private int _activeDisplayHeight = 1080;
        private int _pendingNewKeyVk = 0;
        private int _pendingNewKeyMods = 0;
        private bool _autoKeymapTriggered = false;
        private bool _pendingWinBoundsRestore = false;
        private bool _isStoppingRecording = false;
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
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref WIN32POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        private struct WIN32POINT
        {
            public int X;
            public int Y;
        }

        private const int GWL_STYLE = -16;
        private const int WS_CAPTION = 0x00C00000;

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

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SetWindowText(IntPtr hWnd, string lpString);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint KEYEVENTF_KEYUP = 0x0002;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        private const int WM_INPUT = 0x00FF;
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_QUEST_SCAN = 9001;
        private const int HOTKEY_SCREENSHOT = 9002;
        private const int HOTKEY_CHAT = 9003;
        private const int HOTKEY_QUEST_BATCH = 9004;
        private const uint VK_F2 = 0x71;
        private const uint VK_F7 = 0x76;
        private const uint VK_F8 = 0x77;
        private const uint VK_F12 = 0x7B;
        private const uint RIM_TYPEHID = 2;
        private bool _questHotKeyRegistered = false;
        private bool _questBatchHotKeyRegistered = false;
        private bool _screenshotHotKeyRegistered = false;
        private bool _chatHotKeyRegistered = false;
        private ChatInputWindow _chatWindow;
        private bool _isCapturingScreenshot = false;
        private bool _isConnectingKeymap = false;
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
        private ComboBox _cmbVideoEncoder;
        private Button _btnListEncoders;
        private ComboBox _cmbVideoBuffer;

        // Control Mode Cards
        private RadioButton _rbModeKeymap;
        private RadioButton _rbModeGamepad;
        private RadioButton _rbModeNormal;
        private TextBlock _txtModeSummary;

        private CheckBox _chkTurnScreenOff;
        private CheckBox _chkStayAwake;
        private CheckBox _chkLowLatency;
        private CheckBox _chkNoAudio;
        private CheckBox _chkAudioSafe;
        private CheckBox _chkFullscreen;
        private CheckBox _chkAlwaysOnTop;
        private CheckBox _chkRememberWin;
        private CheckBox _chkPrintFps;
        private int _trackedWinX = -32000;
        private int _trackedWinY = -32000;
        private int _trackedWinW = 0;
        private int _trackedWinH = 0;

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
        private readonly List<Tuple<FrameworkElement, string, string>> _guideBindings = new List<Tuple<FrameworkElement, string, string>>();
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
        private Button _btnQuestBatchScan;
        private Button _btnQuestScanOne;
        private Button _btnQuestUndo;
        private Button _btnQuestResetDaily;
        private Button _btnQuestClear;
        private TextBlock _txtQuestDaily;
        private TextBlock _txtQuestCount;
        private TextBlock _txtQuestBanner;
        private StackPanel _pnlQuestPlans;

        // HuntLink v2.0 Sidebar, Presets, Home Summary, Codex, Extra Args, Collapsible Log
        private RadioButton _navHome, _navKeymap, _navQuest, _navCodex, _navAdvanced;
        private FrameworkElement _pageHome, _pageKeymap, _pageQuest, _pageCodex, _pageAdvanced;
        private Button _btnPreset1080, _btnPreset1440, _btnPresetMonitor, _btnPresetNative, _btnPresetWifi;
        private CheckBox _chkAutoMuteBg, _chkQuestNestFirst;
        private bool _lastAutoMuteState = false;
        private Border _bdHomeQuestCard, _bdLogDrawer;
        private Button _btnGoQuestTab, _btnBottomOpenFolder, _btnToggleLog;
        private TextBlock _txtHomeQuestMeta, _txtHomeQuestRec;
        private TextBox _txtCodexSearch, _txtExtraArgs;
        private TextBlock _txtCodexSearchHint, _txtExtraArgsHint;
        private ComboBox _cmbCodexRegion, _cmbCodexSpecies;
        private StackPanel _pnlCodexList;
        private DockPanel _pnlConnStatus;
        private Border _bdConnBadge, _bdBattBadge;
        private TextBlock _txtConnBadge, _txtBattBadge, _txtConnDetail;
        private int _connProbeSeq = 0;
        private bool _isProbingBattery = false;
        private DispatcherTimer _batteryTimer;
        private string _liveFpsText = "";
        private string _liveBattText = "";
        private string _liveQuestRecShort = "";
        private string _lastAppliedMirrorTitle = "";

        private const string ScrcpyReleasesUrl = "https://github.com/Genymobile/scrcpy/releases";
        private const string DefaultGuideTitle = "옵션 가이드";
        private const string DefaultGuideBody = "각 옵션 위에 마우스를 올리면 언제 켜고 끄면 좋은지 설명이 표시됩니다.";

        public MainWindow()
        {
            Title = "HuntLink v2.0 — 몬헌 아웃랜더스 PC 런처";
            Width = 940;
            Height = 690;
            MinWidth = 860;
            MinHeight = 620;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F1E8"));
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

            _scannedQuests.Clear();
            _scannedQuests.AddRange(QuestOcrService.LoadActiveQuests());
            RefreshQuestPlannerUi();
            RefreshMonsterCodexUi();
            HighlightActiveVisualPreset();

            _isInitializing = false;
            UpdateUiStates();
            UpdateCommandPreview();
        }

        private UIElement BuildUi()
        {
            return HuntLinkUiBuilder.Build();
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
            _cmbVideoEncoder = (ComboBox)root.FindName("cmbVideoEncoder");
            _btnListEncoders = (Button)root.FindName("btnListEncoders");
            _cmbVideoBuffer = (ComboBox)root.FindName("cmbVideoBuffer");

            _rbModeKeymap = (RadioButton)root.FindName("rbModeKeymap");
            _rbModeGamepad = (RadioButton)root.FindName("rbModeGamepad");
            _rbModeNormal = (RadioButton)root.FindName("rbModeNormal");
            _txtModeSummary = (TextBlock)root.FindName("txtModeSummary");

            _chkTurnScreenOff = (CheckBox)root.FindName("chkTurnScreenOff");
            _chkStayAwake = (CheckBox)root.FindName("chkStayAwake");
            _chkLowLatency = (CheckBox)root.FindName("chkLowLatency");
            _chkNoAudio = (CheckBox)root.FindName("chkNoAudio");
            _chkAudioSafe = (CheckBox)root.FindName("chkAudioSafe");
            _chkFullscreen = (CheckBox)root.FindName("chkFullscreen");
            _chkAlwaysOnTop = (CheckBox)root.FindName("chkAlwaysOnTop");
            _chkRememberWin = (CheckBox)root.FindName("chkRememberWin");
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
            _btnQuestBatchScan = (Button)root.FindName("btnQuestBatchScan");
            _btnQuestScanOne = (Button)root.FindName("btnQuestScanOne");
            _btnQuestUndo = (Button)root.FindName("btnQuestUndo");
            _btnQuestResetDaily = (Button)root.FindName("btnQuestResetDaily");
            _btnQuestClear = (Button)root.FindName("btnQuestClear");
            _txtQuestDaily = (TextBlock)root.FindName("txtQuestDaily");
            _txtQuestCount = (TextBlock)root.FindName("txtQuestCount");
            _txtQuestBanner = (TextBlock)root.FindName("txtQuestBanner");
            _pnlQuestPlans = (StackPanel)root.FindName("pnlQuestPlans");

            // HuntLink v2.0 Sidebar, Presets, Home Summary, Codex, Extra Args, Collapsible Log
            _navHome = (RadioButton)root.FindName("navHome");
            _navKeymap = (RadioButton)root.FindName("navKeymap");
            _navQuest = (RadioButton)root.FindName("navQuest");
            _navCodex = (RadioButton)root.FindName("navCodex");
            _navAdvanced = (RadioButton)root.FindName("navAdvanced");
            _pageHome = (FrameworkElement)root.FindName("pageHome");
            _pageKeymap = (FrameworkElement)root.FindName("pageKeymap");
            _pageQuest = (FrameworkElement)root.FindName("pageQuest");
            _pageCodex = (FrameworkElement)root.FindName("pageCodex");
            _pageAdvanced = (FrameworkElement)root.FindName("pageAdvanced");

            _btnPreset1080 = (Button)root.FindName("btnPreset1080");
            _btnPreset1440 = (Button)root.FindName("btnPreset1440");
            _btnPresetMonitor = (Button)root.FindName("btnPresetMonitor");
            _btnPresetNative = (Button)root.FindName("btnPresetNative");
            _btnPresetWifi = (Button)root.FindName("btnPresetWifi");
            _chkAutoMuteBg = (CheckBox)root.FindName("chkAutoMuteBg");
            _chkQuestNestFirst = (CheckBox)root.FindName("chkQuestNestFirst");

            _bdHomeQuestCard = (Border)root.FindName("bdHomeQuestCard");
            _btnGoQuestTab = (Button)root.FindName("btnGoQuestTab");
            _txtHomeQuestMeta = (TextBlock)root.FindName("txtHomeQuestMeta");
            _txtHomeQuestRec = (TextBlock)root.FindName("txtHomeQuestRec");
            _btnBottomOpenFolder = (Button)root.FindName("btnBottomOpenFolder");
            _btnToggleLog = (Button)root.FindName("btnToggleLog");
            _bdLogDrawer = (Border)root.FindName("bdLogDrawer");

            _txtCodexSearch = (TextBox)root.FindName("txtCodexSearch");
            _txtCodexSearchHint = (TextBlock)root.FindName("txtCodexSearchHint");
            _cmbCodexRegion = (ComboBox)root.FindName("cmbCodexRegion");
            _cmbCodexSpecies = (ComboBox)root.FindName("cmbCodexSpecies");
            _pnlCodexList = (StackPanel)root.FindName("pnlCodexList");

            _txtExtraArgs = (TextBox)root.FindName("txtExtraArgs");
            _txtExtraArgsHint = (TextBlock)root.FindName("txtExtraArgsHint");

            _pnlConnStatus = (DockPanel)root.FindName("pnlConnStatus");
            _bdConnBadge = (Border)root.FindName("bdConnBadge");
            _txtConnBadge = (TextBlock)root.FindName("txtConnBadge");
            _bdBattBadge = (Border)root.FindName("bdBattBadge");
            _txtBattBadge = (TextBlock)root.FindName("txtBattBadge");
            _txtConnDetail = (TextBlock)root.FindName("txtConnDetail");

            _batteryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _batteryTimer.Tick += async (s, e) =>
            {
                var d = _cmbDevices != null ? _cmbDevices.SelectedItem as DeviceInfo : null;
                if (d != null && d.State == "device")
                    await RefreshBatteryBadgeAsync(d);
            };
            _batteryTimer.Start();

            // Interactive guides + tooltips (localized via Loc keys)
            AttachGuide(_btnDownloadScrcpy, "g_dl_t", "g_dl_b");
            AttachGuide(_btnSetupGuide, "g_setup_t", "g_setup_b");
            AttachGuide(_btnShortcuts, "g_short_t", "g_short_b");
            AttachGuide(_btnUsbToWifi, "g_u2w_t", "g_u2w_b");
            AttachGuide(_btnDisconnectWifi, "g_discw_t", "g_discw_b");
            AttachGuide(_btnRestartAdb, "g_adb_t", "g_adb_b");
            AttachGuide(_cmbCodec, "g_codec_t", "g_codec_b");
            AttachGuide(_txtBitrate, "g_br_t", "g_br_b");
            AttachGuide(_cmbMaxSize, "g_ms_t", "g_ms_b");
            AttachGuide(_cmbMaxFps, "g_fps_t", "g_fps_b");
            AttachGuide(_cmbRenderer, "g_ren_t", "g_ren_b");
            AttachGuide(_cmbVideoEncoder, "g_venc_t", "g_venc_b");
            AttachGuide(_btnListEncoders, "g_venc_t", "g_venc_b");
            AttachGuide(_cmbVideoBuffer, "g_vbuf_t", "g_vbuf_b");
            AttachGuide(_rbModeKeymap, "g_mkm_t", "g_mkm_b");
            AttachGuide(_rbModeGamepad, "g_mgp_t", "g_mgp_b");
            AttachGuide(_rbModeNormal, "g_mnm_t", "g_mnm_b");
            AttachGuide(_chkTurnScreenOff, "g_soff_t", "g_soff_b");
            AttachGuide(_chkStayAwake, "g_awake_t", "g_awake_b");
            AttachGuide(_chkLowLatency, "g_ll_t", "g_ll_b");
            AttachGuide(_chkNoAudio, "g_noaud_t", "g_noaud_b");
            AttachGuide(_chkAudioSafe, "g_audsafe_t", "g_audsafe_b");
            AttachGuide(_chkFullscreen, "g_fs_t", "g_fs_b");
            AttachGuide(_chkAlwaysOnTop, "g_aot_t", "g_aot_b");
            AttachGuide(_chkRememberWin, "g_remwin_t", "g_remwin_b");
            AttachGuide(_chkPrintFps, "g_pfps_t", "g_pfps_b");
            AttachGuide(_chkGamepadKbAssist, "g_gpkb_t", "g_gpkb_b");
            AttachGuide(_btnScreenshot, "g_shot_t", "g_shot_b");
            AttachGuide(_btnRecordToggle, "g_rec_t", "g_rec_b");
            AttachGuide(_chkVirtualDisplay, "g_vd_t", "g_vd_b");
            AttachGuide(_cmbVdSpec, "g_vds_t", "g_vds_b");
            AttachGuide(_chkNoVdDecorations, "g_nodecor_t", "g_nodecor_b");
            AttachGuide(_cmbApps, "g_apps_t", "g_apps_b");
            AttachGuide(_txtAppSearch, "g_appsearch_t", "g_appsearch_b");

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
            _btnListEncoders.Click += async (s, e) => await LoadVideoEncodersAsync();
            _btnCopyCmd.Click += (s, e) => { Clipboard.SetText("scrcpy.exe " + BuildScrcpyArguments(null)); AppendLog("[안내] 현재 설정의 실행 명령어가 클립보드에 복사되었습니다."); };
            _btnStart.Click += BtnStart_Click;
            _btnStop.Click += (s, e) => StopScrcpy();
            _btnScreenshot.Click += BtnScreenshot_Click;
            _btnRecordToggle.Click += BtnRecordToggle_Click;
            _btnClearLog.Click += (s, e) => _txtLog.Clear();

            // Keymap events
            AttachGuide(_btnImportKmp, "g_kmp_t", "g_kmp_b");
            AttachGuide(_btnKmStart, "g_kmstart_t", "g_kmstart_b");
            AttachGuide(_btnKmEditOverlay, "g_kmedit_t", "g_kmedit_b");

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
                    UpdateMirrorWindowTitle();
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
            AttachGuide(_btnExportCode, "g_expcode_t", "g_expcode_b");
            AttachGuide(_btnImportCode, "g_impcode_t", "g_impcode_b");
            _btnKmStart.Click += BtnKmStart_Click;
            _btnKmStop.Click += (s, e) => StopKeymap();
            _btnKmEditOverlay.Click += BtnKmEditOverlay_Click;
            _btnKmAddKey.Click += BtnKmAddKey_Click;
            _btnKmDelKey.Click += BtnKmDelKey_Click;

            _btnQuestBatchScan.Click += async (s, e) => await ScanQuestListBatchAsync();
            _btnQuestScanOne.Click += async (s, e) => await ScanCurrentQuestAsync(true);
            _btnQuestUndo.Click += (s, e) =>
            {
                if (_scannedQuests.Count == 0) return;
                var last = _scannedQuests[_scannedQuests.Count - 1];
                _scannedQuests.RemoveAt(_scannedQuests.Count - 1);
                RefreshQuestPlannerUi();
                AppendLog("[퀘스트] 마지막 수집 취소: " + last.ObjectiveText);
            };
            _btnQuestClear.Click += (s, e) =>
            {
                _scannedQuests.Clear();
                RefreshQuestPlannerUi();
                AppendLog("[퀘스트] 보유 퀘스트 목록을 모두 비웠습니다.");
            };

            // HuntLink v2.0 Navigation, Presets, Codex, Bottom Bar & Extra Args events
            _navHome.Checked += (s, e) => SwitchPage("home");
            _navKeymap.Checked += (s, e) => SwitchPage("keymap");
            _navQuest.Checked += (s, e) => SwitchPage("quest");
            _navCodex.Checked += (s, e) => SwitchPage("codex");
            _navAdvanced.Checked += (s, e) => SwitchPage("advanced");

            _btnPreset1080.Click += (s, e) => ApplyVisualPreset("1080p");
            _btnPreset1440.Click += (s, e) => ApplyVisualPreset("1440p");
            _btnPresetMonitor.Click += (s, e) => ApplyVisualPreset("monitor");
            _btnPresetNative.Click += (s, e) => ApplyVisualPreset("native");
            _btnPresetWifi.Click += (s, e) => ApplyVisualPreset("wifi");

            AttachGuide(_btnPreset1080, "g_p1080_t", "g_p1080_b");
            AttachGuide(_btnPreset1440, "g_p1440_t", "g_p1440_b");
            AttachGuide(_btnPresetMonitor, "g_pmon_t", "g_pmon_b");
            AttachGuide(_btnPresetNative, "g_pnat_t", "g_pnat_b");
            AttachGuide(_btnPresetWifi, "g_pwifi_t", "g_pwifi_b");
            AttachGuide(_chkAutoMuteBg, "g_amute_t", "g_amute_b");
            AttachGuide(_chkQuestNestFirst, "g_qnest_t", "g_qnest_b");
            AttachGuide(_btnQuestBatchScan, "g_qbatch_t", "g_qbatch_b");
            AttachGuide(_txtExtraArgs, "g_extra_t", "g_extra_b");

            _chkAutoMuteBg.Checked += (s, e) => { if (!_isInitializing) SaveSettingsFromUi(); };
            _chkAutoMuteBg.Unchecked += (s, e) =>
            {
                if (!_isInitializing) SaveSettingsFromUi();
                if (_scrcpyProcess != null && !_scrcpyProcess.HasExited && _lastAutoMuteState)
                {
                    AudioMuteHelper.SetProcessMute(_scrcpyProcess.Id, false);
                    _lastAutoMuteState = false;
                }
            };
            _chkQuestNestFirst.Checked += (s, e) => { if (!_isInitializing) { SaveSettingsFromUi(); RefreshQuestPlannerUi(); } };
            _chkQuestNestFirst.Unchecked += (s, e) => { if (!_isInitializing) { SaveSettingsFromUi(); RefreshQuestPlannerUi(); } };

            _btnGoQuestTab.Click += (s, e) => { _navQuest.IsChecked = true; };
            _bdHomeQuestCard.MouseLeftButtonUp += (s, e) => { _navQuest.IsChecked = true; };
            _btnBottomOpenFolder.Click += BtnOpenRecord_Click;
            _btnToggleLog.Click += (s, e) =>
            {
                bool open = _bdLogDrawer.Visibility != Visibility.Visible;
                _bdLogDrawer.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
                _btnToggleLog.Content = open ? Loc.T("btn_toggle_log_close") : Loc.T("btn_toggle_log_open");
            };

            _txtCodexSearch.TextChanged += (s, e) =>
            {
                if (_txtCodexSearchHint != null)
                    _txtCodexSearchHint.Visibility = string.IsNullOrEmpty(_txtCodexSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
                RefreshMonsterCodexUi();
            };
            _cmbCodexRegion.SelectionChanged += (s, e) => RefreshMonsterCodexUi();
            _cmbCodexSpecies.SelectionChanged += (s, e) => RefreshMonsterCodexUi();

            _txtExtraArgs.TextChanged += (s, e) =>
            {
                if (_txtExtraArgsHint != null)
                    _txtExtraArgsHint.Visibility = string.IsNullOrEmpty(_txtExtraArgs.Text) ? Visibility.Visible : Visibility.Collapsed;
            };
            _btnQuestResetDaily.Click += (s, e) =>
            {
                _settings.DailyQuestClearedCount = 0;
                _settings.DailyQuestDateKey = GetDailyQuestDateKey();
                _settings.Save();
                UpdateDailyQuestCounterUi();
                AppendLog("[퀘스트] 오늘 완료 카운터를 0으로 초기화했습니다.");
            };
            AttachGuide(_chkKmNewWheel, "g_newwheel_t", "g_newwheel_b");
            AttachGuide(_chkKmRight, "g_rclick_t", "g_rclick_b");
            AttachGuide(_chkKmRightWheel, "g_rwheel_t", "g_rwheel_b");

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
            AttachGuide(_txtKmNewKey, "g_newkey_t", "g_newkey_b");

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
                var _ = RefreshConnectionSpeedAsync(dev);
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
            _chkAudioSafe.Checked += (s, e) => onOptChanged();
            _chkAudioSafe.Unchecked += (s, e) => onOptChanged();
            _chkFullscreen.Checked += (s, e) => onOptChanged();
            _chkFullscreen.Unchecked += (s, e) => onOptChanged();
            _chkAlwaysOnTop.Checked += (s, e) => { if (!_isInitializing) ApplyAlwaysOnTopLive(true); onOptChanged(); };
            _chkAlwaysOnTop.Unchecked += (s, e) => { if (!_isInitializing) ApplyAlwaysOnTopLive(false); onOptChanged(); };
            _chkRememberWin.Checked += (s, e) => onOptChanged();
            _chkRememberWin.Unchecked += (s, e) => onOptChanged();
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

        private void AttachGuide(FrameworkElement el, string titleKey, string bodyKey)
        {
            if (el == null) return;
            _guideBindings.Add(Tuple.Create(el, titleKey, bodyKey));
            el.ToolTip = Loc.T(bodyKey);
            el.MouseEnter += (s, e) =>
            {
                _txtGuideTitle.Text = Loc.T(titleKey);
                _txtGuideBody.Text = Loc.T(bodyKey);
            };
            el.MouseLeave += (s, e) =>
            {
                _txtGuideTitle.Text = Loc.T("guide_title");
                _txtGuideBody.Text = Loc.T("guide_body");
            };
        }

        private void SetComboItemContentByTag(ComboBox cmb, string tag, string content)
        {
            if (cmb == null) return;
            bool wasSelected = false;
            int selIdx = cmb.SelectedIndex;
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                var item = cmb.Items[i] as ComboBoxItem;
                if (item != null && item.Tag != null && string.Equals(item.Tag.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    item.Content = content;
                    if (i == selIdx) wasSelected = true;
                }
            }
            if (wasSelected && cmb.IsEditable)
                cmb.Text = content;
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

            SetTextByName(root, "txtNavHome", Loc.T("tab_conn"));
            SetTextByName(root, "txtNavKeymap", Loc.T("tab_keymap"));
            SetTextByName(root, "txtNavQuest", Loc.T("tab_quest"));
            SetTextByName(root, "txtNavCodex", Loc.T("tab_codex"));
            SetTextByName(root, "txtNavAdvanced", Loc.T("tab_advanced"));

            SetTextByName(root, "lblPresetsTitle", Loc.T("lbl_presets_title"));
            if (_btnPreset1080 != null) _btnPreset1080.Content = Loc.T("preset_1080");
            if (_btnPreset1440 != null) _btnPreset1440.Content = Loc.T("preset_1440");
            if (_btnPresetMonitor != null) _btnPresetMonitor.Content = Loc.T("preset_monitor");
            if (_btnPresetNative != null) _btnPresetNative.Content = Loc.T("preset_native");
            if (_btnPresetWifi != null) _btnPresetWifi.Content = Loc.T("preset_wifi");
            if (_chkAutoMuteBg != null) _chkAutoMuteBg.Content = Loc.T("chk_auto_mute_bg");
            if (_chkQuestNestFirst != null) _chkQuestNestFirst.Content = Loc.T("chk_quest_nest_first");
            SetTextByName(root, "lblHomeQuestTitle", Loc.T("lbl_home_quest"));
            if (_btnGoQuestTab != null) _btnGoQuestTab.Content = Loc.T("btn_go_quest");
            SetTextByName(root, "lblQuickOpts", Loc.T("lbl_quick_opts"));
            if (_btnBottomOpenFolder != null) _btnBottomOpenFolder.Content = Loc.T("btn_bottom_folder");
            if (_btnToggleLog != null && _bdLogDrawer != null)
                _btnToggleLog.Content = _bdLogDrawer.Visibility == Visibility.Visible ? Loc.T("btn_toggle_log_close") : Loc.T("btn_toggle_log_open");

            SetTextByName(root, "lblQuestSec1", Loc.T("q_sec1"));
            SetTextByName(root, "lblQuestSec2", Loc.T("q_sec2"));
            if (_btnQuestBatchScan != null) _btnQuestBatchScan.Content = Loc.T("q_btn_batch");
            if (_btnQuestScanOne != null) _btnQuestScanOne.Content = Loc.T("q_btn_one");
            if (_btnQuestUndo != null) _btnQuestUndo.Content = Loc.T("q_btn_undo");
            if (_btnQuestResetDaily != null) _btnQuestResetDaily.Content = Loc.T("q_btn_reset");
            if (_btnQuestClear != null) _btnQuestClear.Content = Loc.T("q_btn_clear");

            SetTextByName(root, "lblCodexTitle", Loc.T("codex_title"));
            SetTextByName(root, "txtCodexSearchHint", Loc.T("codex_search_hint"));
            if (_cmbCodexRegion != null && _cmbCodexRegion.Items.Count > 0)
            {
                var c0 = _cmbCodexRegion.Items[0] as ComboBoxItem;
                if (c0 != null) c0.Content = Loc.T("codex_all_regions");
            }
            if (_cmbCodexSpecies != null && _cmbCodexSpecies.Items.Count > 0)
            {
                var s0 = _cmbCodexSpecies.Items[0] as ComboBoxItem;
                if (s0 != null) s0.Content = Loc.T("codex_all_species");
                var sLast = _cmbCodexSpecies.Items[_cmbCodexSpecies.Items.Count - 1] as ComboBoxItem;
                if (sLast != null) sLast.Content = Loc.T("codex_radiant_only");
            }

            SetTextByName(root, "lblExtraArgs", Loc.T("lbl_extra_args"));
            SetTextByName(root, "txtExtraArgsHint", Loc.T("hint_extra_args"));
            SetTextByName(root, "lblAdvSec3", Loc.T("lbl_adv_sec3"));
            UpdateDailyQuestCounterUi();
            if (_txtQuestCount != null) _txtQuestCount.Text = Loc.F("q_count_fmt", _scannedQuests.Count);

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
            SetTextByName(root, "lblVideoEncoder", Loc.T("lbl_video_encoder"));
            if (_btnListEncoders != null) _btnListEncoders.Content = Loc.T("btn_list_encoders");
            SetTextByName(root, "lblVideoBuffer", Loc.T("lbl_video_buffer"));
            SetComboItemContentByTag(_cmbVideoEncoder, "", Loc.T("cmb_encoder_auto"));
            SetComboItemContentByTag(_cmbVideoBuffer, "0", Loc.T("cmb_vbuf_0"));
            _chkTurnScreenOff.Content = Loc.T("chk_screen_off");
            _chkStayAwake.Content = Loc.T("chk_stay_awake");
            _chkLowLatency.Content = Loc.T("chk_low_latency");
            _chkNoAudio.Content = Loc.T("chk_no_audio");
            _chkAudioSafe.Content = Loc.T("chk_audio_safe");
            _chkFullscreen.Content = Loc.T("chk_fullscreen");
            _chkAlwaysOnTop.Content = Loc.T("chk_always_on_top");
            _chkRememberWin.Content = Loc.T("chk_remember_win");
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
            foreach (var gb in _guideBindings)
                gb.Item1.ToolTip = Loc.T(gb.Item3);
            var bdQuestDaily = root.FindName("bdQuestDaily") as Border;
            if (bdQuestDaily != null) bdQuestDaily.ToolTip = Loc.T("q_daily_tip");

            // Localize dropdown items & empty-device / none-app placeholders
            SetComboItemContentByTag(_cmbCodec, "h264", Loc.T("cmb_h264"));
            SetComboItemContentByTag(_cmbCodec, "h265", Loc.T("cmb_h265"));
            SetComboItemContentByTag(_cmbMaxSize, "0", Loc.T("cmb_maxsize_0"));
            SetComboItemContentByTag(_cmbMaxFps, "0", Loc.T("cmb_maxfps_0"));
            SetComboItemContentByTag(_cmbRenderer, "auto", Loc.T("cmb_renderer_auto"));
            if (_devices.Count == 0 && _cmbDevices != null && _cmbDevices.Items.Count == 1 && _cmbDevices.Items[0] is string)
            {
                _cmbDevices.Items[0] = Loc.T("dev_none");
                _cmbDevices.SelectedIndex = 0;
            }
            if (_cmbApps != null)
            {
                for (int i = 0; i < _cmbApps.Items.Count; i++)
                {
                    var ai = _cmbApps.Items[i] as AppItem;
                    if (ai != null && string.IsNullOrEmpty(ai.PackageName))
                    {
                        bool sel = (_cmbApps.SelectedIndex == i);
                        _cmbApps.Items[i] = new AppItem { Name = Loc.T("app_none"), PackageName = "" };
                        if (sel) _cmbApps.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (_scannedQuests.Count == 0 && _pnlQuestPlans != null)
                RefreshQuestPlannerUi();
            var curDev = _cmbDevices != null ? _cmbDevices.SelectedItem as DeviceInfo : null;
            if (curDev != null && curDev.State == "device")
            {
                var _ = RefreshConnectionSpeedAsync(curDev);
            }

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

        private bool IsOurOrMirrorWindowFocused()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            IntPtr launcherHwnd = new WindowInteropHelper(this).Handle;
            if (launcherHwnd != IntPtr.Zero && fg == launcherHwnd) return true;
            if (_chatWindow != null && _chatWindow.IsVisible)
            {
                IntPtr chatHwnd = new WindowInteropHelper(_chatWindow).Handle;
                if (chatHwnd != IntPtr.Zero && fg == chatHwnd) return true;
            }
            if (_scrcpyProcess != null && !_scrcpyProcess.HasExited)
            {
                try
                {
                    IntPtr targetHwnd = _scrcpyProcess.MainWindowHandle;
                    if (targetHwnd != IntPtr.Zero && fg == targetHwnd) return true;
                }
                catch { }
            }
            return false;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                if (!IsOurOrMirrorWindowFocused())
                    return IntPtr.Zero;
                int id = wParam.ToInt32();
                if (id == HOTKEY_QUEST_SCAN)
                {
                    handled = true;
                    var _ = ScanCurrentQuestAsync(true);
                    return IntPtr.Zero;
                }
                if (id == HOTKEY_QUEST_BATCH)
                {
                    handled = true;
                    var _ = ScanQuestListBatchAsync();
                    return IntPtr.Zero;
                }
                if (id == HOTKEY_SCREENSHOT)
                {
                    handled = true;
                    if (_scrcpyProcess != null && !_scrcpyProcess.HasExited)
                        BtnScreenshot_Click(null, null);
                    return IntPtr.Zero;
                }
                if (id == HOTKEY_CHAT)
                {
                    handled = true;
                    if (_scrcpyProcess != null && !_scrcpyProcess.HasExited)
                        ToggleChatBar();
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
                                        IntPtr targetHwnd = IntPtr.Zero;
                                        try { _scrcpyProcess.Refresh(); targetHwnd = _scrcpyProcess.MainWindowHandle; } catch { }
                                        if (targetHwnd != IntPtr.Zero && GetForegroundWindow() == targetHwnd)
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

            var res = await UpdateChecker.DownloadAndApplyUpdateAsync(_updateInfo.ExeDownloadUrl, _updateInfo.ExeSha256);
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
                    if (!_questBatchHotKeyRegistered)
                        _questBatchHotKeyRegistered = RegisterHotKey(hwnd, HOTKEY_QUEST_BATCH, 0, VK_F7);
                    if (!_screenshotHotKeyRegistered)
                        _screenshotHotKeyRegistered = RegisterHotKey(hwnd, HOTKEY_SCREENSHOT, 0, VK_F12);
                    if (!_chatHotKeyRegistered)
                        _chatHotKeyRegistered = RegisterHotKey(hwnd, HOTKEY_CHAT, 0, VK_F2);
                }
                else
                {
                    if (_questHotKeyRegistered)
                    {
                        UnregisterHotKey(hwnd, HOTKEY_QUEST_SCAN);
                        _questHotKeyRegistered = false;
                    }
                    if (_questBatchHotKeyRegistered)
                    {
                        UnregisterHotKey(hwnd, HOTKEY_QUEST_BATCH);
                        _questBatchHotKeyRegistered = false;
                    }
                    if (_screenshotHotKeyRegistered)
                    {
                        UnregisterHotKey(hwnd, HOTKEY_SCREENSHOT);
                        _screenshotHotKeyRegistered = false;
                    }
                    if (_chatHotKeyRegistered)
                    {
                        UnregisterHotKey(hwnd, HOTKEY_CHAT);
                        _chatHotKeyRegistered = false;
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
            if (_chatWindow != null)
            {
                try { _chatWindow.Close(); } catch { }
                _chatWindow = null;
            }
            if (_hookEngine != null) _hookEngine.Dispose();
            if (_controlClient != null) _controlClient.Dispose();
            SaveSettingsFromUi();
            StopRecordingSync(3000);
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
                        _cmbDevices.Items.Add(Loc.T("dev_none"));
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

        private async Task RefreshConnectionSpeedAsync(DeviceInfo dev)
        {
            if (_pnlConnStatus == null) return;
            int seq = ++_connProbeSeq;
            if (dev == null || dev.State != "device")
            {
                _pnlConnStatus.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                var info = await ConnectionInfoHelper.ProbeAsync(_adb, dev);
                if (seq != _connProbeSeq) return;
                if (info == null)
                {
                    _pnlConnStatus.Visibility = Visibility.Collapsed;
                    return;
                }

                string bg = "#E6EFE9", bd = "#B8D0C0", fg = "#1E462E";
                if (info.Tier == "mid")
                {
                    bg = "#FDF6E7"; bd = "#E6C98B"; fg = "#8C5E14";
                }
                else if (info.Tier == "warn")
                {
                    bg = "#FEF2F2"; bd = "#FECACA"; fg = "#B91C1C";
                }

                _bdConnBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
                _bdConnBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bd));
                _txtConnBadge.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg));
                _txtConnBadge.Text = info.BadgeText;
                _txtConnDetail.Text = info.DetailText;
                _pnlConnStatus.ToolTip = info.TooltipText;
                _pnlConnStatus.Visibility = Visibility.Visible;

                await RefreshBatteryBadgeAsync(dev);
            }
            catch
            {
                if (seq == _connProbeSeq)
                    _pnlConnStatus.Visibility = Visibility.Collapsed;
            }
        }

        private async Task RefreshBatteryBadgeAsync(DeviceInfo dev)
        {
            if (_bdBattBadge == null || _isProbingBattery) return;
            if (dev == null || dev.State != "device")
            {
                _bdBattBadge.Visibility = Visibility.Collapsed;
                return;
            }

            _isProbingBattery = true;
            try
            {
                var batt = await ConnectionInfoHelper.ProbeBatteryAsync(_adb, dev);
                var cur = _cmbDevices != null ? _cmbDevices.SelectedItem as DeviceInfo : null;
                if (cur == null || cur.Serial != dev.Serial || batt == null)
                {
                    if (batt == null) _bdBattBadge.Visibility = Visibility.Collapsed;
                    return;
                }

                string bg = "#E6EFE9", bd = "#B8D0C0", fg = "#1E462E";
                if (batt.Tier == "warm")
                {
                    bg = "#FDF6E7"; bd = "#E6C98B"; fg = "#8C5E14";
                }
                else if (batt.Tier == "hot")
                {
                    bg = "#FEF2F2"; bd = "#FECACA"; fg = "#B91C1C";
                }

                _bdBattBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
                _bdBattBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bd));
                _txtBattBadge.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg));
                _txtBattBadge.Text = batt.BadgeText;
                _bdBattBadge.ToolTip = batt.TooltipText;
                _bdBattBadge.Visibility = Visibility.Visible;
                _liveBattText = batt.BadgeText;
                UpdateMirrorWindowTitle();
            }
            catch
            {
                _bdBattBadge.Visibility = Visibility.Collapsed;
            }
            finally
            {
                _isProbingBattery = false;
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

                // Auto-select Monster Hunter Outlanders for first-time users if no app is saved yet
                if (string.IsNullOrEmpty(_settings.StartAppPackage) && _allApps != null && _allApps.Count > 0)
                {
                    AppItem mho = _allApps.FirstOrDefault(a =>
                        string.Equals(a.PackageName, "com.garena.game.mho", StringComparison.OrdinalIgnoreCase));
                    if (mho == null)
                    {
                        mho = _allApps.FirstOrDefault(a =>
                            (a.PackageName != null && (a.PackageName.IndexOf("outlanders", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                       a.PackageName.IndexOf(".mho", StringComparison.OrdinalIgnoreCase) >= 0)) ||
                            (a.Name != null && (a.Name.IndexOf("outlanders", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                a.Name.Contains("아웃랜더스") || a.Name.Contains("アウトランダーズ"))));
                    }
                    if (mho != null)
                    {
                        _settings.StartAppPackage = mho.PackageName;
                        _settings.Save();
                        AppendLog("[앱 목록] 몬스터헌터 아웃랜더스 자동 선택됨: " + mho.PackageName);
                    }
                }

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
            var noneItem = new AppItem { Name = Loc.T("app_none"), PackageName = "" };
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

        private string GetSelectedVideoEncoder()
        {
            if (_cmbVideoEncoder == null) return "";
            var item = _cmbVideoEncoder.SelectedItem as ComboBoxItem;
            if (item != null && item.Tag != null)
                return item.Tag.ToString().Trim();
            string txt = (_cmbVideoEncoder.Text ?? "").Trim();
            if (string.IsNullOrEmpty(txt) || txt.StartsWith("자동") || txt.StartsWith("Auto") || txt.StartsWith("自動"))
                return "";
            return txt;
        }

        private async Task LoadVideoEncodersAsync()
        {
            var dev = _cmbDevices.SelectedItem as DeviceInfo;
            if (!_adb.IsValid || dev == null || dev.State != "device")
            {
                AppendLog("[진단] 기기가 연결된 상태에서 인코더를 조회할 수 있습니다.");
                return;
            }
            string codec = GetSelectedTag(_cmbCodec, "h264");
            _btnListEncoders.IsEnabled = false;
            AppendLog(string.Format("[진단] {0} ({1}) 영상 인코더 목록 조회 중...", dev.Model, codec));
            try
            {
                var res = await _adb.GetVideoEncodersAsync(dev.Serial, codec);
                var list = res.Item1;
                string prev = GetSelectedVideoEncoder();

                _cmbVideoEncoder.Items.Clear();
                _cmbVideoEncoder.Items.Add(new ComboBoxItem { Content = Loc.T("cmb_encoder_auto"), Tag = "" });
                int selIdx = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    var ci = new ComboBoxItem { Content = list[i], Tag = list[i] };
                    int idx = _cmbVideoEncoder.Items.Add(ci);
                    if (string.Equals(list[i], prev, StringComparison.OrdinalIgnoreCase))
                        selIdx = idx;
                }
                _cmbVideoEncoder.SelectedIndex = selIdx;

                if (list.Count > 0)
                    AppendLog(string.Format("[진단] 사용 가능한 {0} 인코더 ({1}개): {2}", codec, list.Count, string.Join(", ", list.ToArray())));
                else
                    AppendLog("[진단] 인코더 목록을 가져오지 못했습니다. (로그 참고)");

                // Open log drawer automatically so user can inspect the full diagnostic output
                if (_bdLogDrawer != null && _bdLogDrawer.Visibility != Visibility.Visible)
                {
                    _bdLogDrawer.Visibility = Visibility.Visible;
                    if (_btnToggleLog != null) _btnToggleLog.Content = Loc.T("btn_toggle_log_close");
                }
            }
            catch (Exception ex)
            {
                AppendLog("[진단 오류] " + ex.Message);
            }
            finally
            {
                _btnListEncoders.IsEnabled = true;
            }
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

            string enc = GetSelectedVideoEncoder();
            if (!string.IsNullOrEmpty(enc))
                args.Add("--video-encoder=" + enc);

            int vbuf;
            if (int.TryParse(GetSelectedTag(_cmbVideoBuffer, "0"), out vbuf) && vbuf > 0)
                args.Add("--video-buffer=" + vbuf);

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
            else if (_chkAudioSafe.IsChecked == true) args.Add("--audio-buffer=120");
            if (_chkFullscreen.IsChecked == true) args.Add("-f");
            // Note: Windowed bounds restore is applied via Win32 SetWindowPos once the window is visible
            // so physical pixel dimensions never inflate on 125%/150%/200% or mixed-DPI multi-monitor setups!
            if (_chkAlwaysOnTop.IsChecked == true) args.Add("--always-on-top");
            if (_chkPrintFps.IsChecked == true) args.Add("--print-fps");

            string mode = GetControlMode();
            if (mode == "keymap")
            {
                args.Add("--mouse=disabled");
            }
            else if (mode == "gamepad")
            {
                args.Add("-G");
                args.Add("--mouse-bind=-hsn:++++");
            }
            // Note: --keyboard=uhid is intentionally omitted so Android tablets (Galaxy Tab DeX / Lenovo Y700 / Xiaoxin ZUI)
            // never auto-switch to PC/DeX mode, Alt+O works natively, and Korean chat works cleanly via the F2 mini chat bar!

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

            string extra = _txtExtraArgs != null ? (_txtExtraArgs.Text ?? "").Trim() : "";
            if (!string.IsNullOrEmpty(extra))
                args.Add(extra);

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
                // Prevent SDL2 & SDL3 (scrcpy 3.x/5.x) from minimizing fullscreen window on Alt-Tab focus loss
                psi.EnvironmentVariables["SDL_VIDEO_MINIMIZE_ON_FOCUS_LOSS"] = "0";
                psi.EnvironmentVariables["SDL_HINT_VIDEO_MINIMIZE_ON_FOCUS_LOSS"] = "0";

                _scrcpyProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _scrcpyProcess.OutputDataReceived += (s, ev) => { if (ev.Data != null) HandleScrcpyOutput(ev.Data); };
                _scrcpyProcess.ErrorDataReceived += (s, ev) => { if (ev.Data != null) HandleScrcpyOutput(ev.Data); };
                _scrcpyProcess.Exited += (s, ev) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        PersistTrackedWindowBounds();
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

                _trackedWinW = 0;
                _trackedWinH = 0;
                _pendingWinBoundsRestore = (_chkFullscreen.IsChecked != true &&
                                            _chkRememberWin.IsChecked == true &&
                                            _settings != null &&
                                            _settings.LastWinW >= 300 &&
                                            _settings.LastWinH >= 200 &&
                                            IsBoundsOnAnyScreen(_settings.LastWinX, _settings.LastWinY, _settings.LastWinW, _settings.LastWinH));
                _activeDisplayId = "";
                _autoKeymapTriggered = false;
                _scrcpyProcess.Start();
                _scrcpyProcess.BeginOutputReadLine();
                _scrcpyProcess.BeginErrorReadLine();
                SetQuestHotKeyActive(IsOurOrMirrorWindowFocused());
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

        private static string BuildUniqueMediaPath(string dir, string prefix, string ext, out string outFileName)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            outFileName = string.Format("{0}_{1}{2}", prefix, stamp, ext);
            string full = Path.Combine(dir, outFileName);
            int n = 2;
            while (File.Exists(full))
            {
                outFileName = string.Format("{0}_{1}_{2}{3}", prefix, stamp, n++, ext);
                full = Path.Combine(dir, outFileName);
            }
            return full;
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

            string fileName;
            string fullPath = BuildUniqueMediaPath(saveDir, "screenshot", ".png", out fileName);

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
                string finalSavedPath = fullPath;
                if (capturedFromWindow)
                {
                    savedOk = true;
                }
                else
                {
                    var res = await _adb.CaptureScreenshotAsync(dev.Serial, fullPath, _activeDisplayId);
                    if (res.Item1)
                    {
                        savedOk = true;
                        finalSavedPath = res.Item2;
                    }
                    else
                    {
                        AppendLog("[캡처 실패] " + res.Item2);
                    }
                }

                if (savedOk)
                {
                    bool copiedClip = TryCopyPngToClipboard(finalSavedPath);
                    AppendLog((copiedClip ? "[캡처] 저장 및 클립보드 복사 완료: " : "[캡처] 화면 저장 완료: ") + finalSavedPath);

                    if (_scrcpyProcess != null && !_scrcpyProcess.HasExited)
                    {
                        if (_keymapOverlay == null)
                        {
                            _keymapOverlay = new KeymapOverlayWindow();
                            _keymapOverlay.ProfileEdited += () => RefreshKeyListBox();
                            _keymapOverlay.SetShowHints(false);
                        }
                        _keymapOverlay.ShowToast(copiedClip
                            ? ("📸 스샷 저장 + 클립보드 복사됨 (" + fileName + ")")
                            : ("📸 스크린샷 저장됨 (" + fileName + ")"));
                        if (!_overlayTimer.IsEnabled) _overlayTimer.Start();
                        SyncFpsOverlay();
                    }
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

        private async void BtnRecordToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isStoppingRecording) return;
            if (_recordProcess != null && !_recordProcess.HasExited)
                await StopRecordingAsync();
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

            string recFileName;
            _currentRecordPath = BuildUniqueMediaPath(saveDir, "record", ".mp4", out recFileName);

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

        private void FinalizeRecordProcessCore(Process proc, int waitMs)
        {
            if (proc == null) return;
            try
            {
                if (!proc.HasExited)
                {
                    bool gracefullyStopped = false;
                    bool attached = false;
                    try
                    {
                        attached = AttachConsole((uint)proc.Id);
                        if (attached)
                        {
                            SetConsoleCtrlHandler(null, true);
                            GenerateConsoleCtrlEvent(0, 0);
                            gracefullyStopped = proc.WaitForExit(waitMs);
                        }
                    }
                    catch { }
                    finally
                    {
                        if (attached)
                        {
                            try { FreeConsole(); } catch { }
                            try { SetConsoleCtrlHandler(null, false); } catch { }
                        }
                    }

                    if (!gracefullyStopped && !proc.HasExited)
                    {
                        try { proc.Kill(); } catch { }
                        try { proc.WaitForExit(1000); } catch { }
                    }
                }
            }
            catch { }
        }

        private async Task StopRecordingAsync()
        {
            if (_recordProcess == null || _isStoppingRecording) return;
            var proc = _recordProcess;
            _isStoppingRecording = true;
            _recordTimer.Stop();
            _btnRecordToggle.IsEnabled = false;
            AppendLog("[녹화] 녹화를 마무리하고 저장하는 중...");
            try
            {
                await Task.Run(() => FinalizeRecordProcessCore(proc, 6000));
            }
            finally
            {
                _recordProcess = null;
                _isStoppingRecording = false;
                UpdateUiStates();
            }
        }

        private void StopRecordingSync(int waitMs)
        {
            if (_recordProcess == null) return;
            var proc = _recordProcess;
            _recordTimer.Stop();
            try
            {
                FinalizeRecordProcessCore(proc, waitMs);
            }
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
                        _liveFpsText = fps.ToString();
                        UpdateMirrorWindowTitle();
                        if (!_overlayTimer.IsEnabled) _overlayTimer.Start();
                    }
                    // Keep the log window clean by not printing repetitive FPS lines (displayed in Title Bar & log badge instead)
                    return;
                }

                if (line.IndexOf("FPS counter stopped", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _bdFpsBadge.Visibility = Visibility.Collapsed;
                    _liveFpsText = "";
                    UpdateMirrorWindowTitle();
                    if (_fpsOverlay != null && _fpsOverlay.IsVisible) _fpsOverlay.Hide();
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

        private void UpdateMirrorWindowTitle(IntPtr hwndOverride = default(IntPtr))
        {
            try
            {
                if (_scrcpyProcess == null || _scrcpyProcess.HasExited) return;
                IntPtr hwnd = hwndOverride;
                if (hwnd == IntPtr.Zero)
                {
                    _scrcpyProcess.Refresh();
                    hwnd = _scrcpyProcess.MainWindowHandle;
                }
                if (hwnd == IntPtr.Zero) return;

                var parts = new List<string>();
                parts.Add("HuntLink");

                bool showProfile = _activeProfile != null && !string.IsNullOrWhiteSpace(_activeProfile.Name) &&
                    ((_rbModeKeymap != null && _rbModeKeymap.IsChecked == true) ||
                     (_rbModeGamepad != null && _rbModeGamepad.IsChecked == true && _chkGamepadKbAssist != null && _chkGamepadKbAssist.IsChecked == true));
                if (showProfile)
                    parts.Add("[" + _activeProfile.Name + "]");

                if (!string.IsNullOrEmpty(_liveFpsText))
                    parts.Add(_liveFpsText + " FPS");

                if (!string.IsNullOrEmpty(_liveBattText))
                    parts.Add(_liveBattText);

                if (_scannedQuests != null && _scannedQuests.Count > 0 && !string.IsNullOrEmpty(_liveQuestRecShort))
                    parts.Add("📋 " + _liveQuestRecShort);

                string title = string.Join("  |  ", parts.ToArray());
                if (title != _lastAppliedMirrorTitle)
                {
                    _lastAppliedMirrorTitle = title;
                    SetWindowText(hwnd, title);
                }
            }
            catch { }
        }

        private void SyncFpsOverlay()
        {
            if (_scrcpyProcess == null || _scrcpyProcess.HasExited)
            {
                _liveFpsText = "";
                _lastAppliedMirrorTitle = "";
                _lastAutoMuteState = false;
                _pendingWinBoundsRestore = false;
                SetQuestHotKeyActive(false);
                HideFpsOverlay();
                return;
            }

            try
            {
                _scrcpyProcess.Refresh();
                IntPtr targetHwnd = _scrcpyProcess.MainWindowHandle;
                if (targetHwnd != IntPtr.Zero)
                    UpdateMirrorWindowTitle(targetHwnd);
                IntPtr launcherHwnd = new WindowInteropHelper(this).Handle;

                // ② Dynamically register F2/F7/F8/F12 hotkeys ONLY while scrcpy / HuntLink / ChatInputWindow is focused!
                SetQuestHotKeyActive(IsOurOrMirrorWindowFocused());

                // F3: Auto-Mute scrcpy audio when switching to another window (Alt+Tab)
                if (_chkAutoMuteBg != null && _chkAutoMuteBg.IsChecked == true && targetHwnd != IntPtr.Zero)
                {
                    IntPtr fg = GetForegroundWindow();
                    IntPtr chatHwnd = (_chatWindow != null && _chatWindow.IsVisible) ? new WindowInteropHelper(_chatWindow).Handle : IntPtr.Zero;
                    bool isGameFocused = (fg == targetHwnd) || (chatHwnd != IntPtr.Zero && fg == chatHwnd);
                    bool shouldMute = !isGameFocused;
                    if (shouldMute != _lastAutoMuteState)
                    {
                        if (AudioMuteHelper.SetProcessMute(_scrcpyProcess.Id, shouldMute))
                            _lastAutoMuteState = shouldMute;
                    }
                }

                // ④ DPI-invariant physical window bounds restore & tracking (uses outer GetWindowRect + SetWindowPos)
                if (targetHwnd != IntPtr.Zero && IsWindowVisible(targetHwnd) && !IsIconic(targetHwnd) && !IsZoomed(targetHwnd) &&
                    (GetWindowLong(targetHwnd, GWL_STYLE) & WS_CAPTION) != 0)
                {
                    if (_pendingWinBoundsRestore)
                    {
                        _pendingWinBoundsRestore = false;
                        if (_chkFullscreen.IsChecked != true && _settings != null &&
                            _settings.LastWinW >= 300 && _settings.LastWinH >= 200 &&
                            IsBoundsOnAnyScreen(_settings.LastWinX, _settings.LastWinY, _settings.LastWinW, _settings.LastWinH))
                        {
                            SetWindowPos(targetHwnd, IntPtr.Zero, _settings.LastWinX, _settings.LastWinY,
                                _settings.LastWinW, _settings.LastWinH, SWP_NOZORDER | SWP_NOACTIVATE);
                        }
                    }
                    else
                    {
                        RECT wrc;
                        if (GetWindowRect(targetHwnd, out wrc))
                        {
                            int ww = wrc.Right - wrc.Left;
                            int wh = wrc.Bottom - wrc.Top;
                            if (ww >= 300 && wh >= 200)
                            {
                                _trackedWinX = wrc.Left;
                                _trackedWinY = wrc.Top;
                                _trackedWinW = ww;
                                _trackedWinH = wh;
                            }
                        }
                    }
                }

                int vidW = _activeDisplayWidth > 0 ? _activeDisplayWidth : (_controlClient != null ? _controlClient.ScreenWidth : 0);
                int vidH = _activeDisplayHeight > 0 ? _activeDisplayHeight : (_controlClient != null ? _controlClient.ScreenHeight : 0);

                if (_hookEngine != null && _hookEngine.IsActive)
                {
                    _hookEngine.SetTargetWindow(targetHwnd);
                    _hookEngine.SetVideoSize(vidW, vidH);
                    if (_hookEngine.RefreshCameraClipRect() && _cursorHider != null && targetHwnd != IntPtr.Zero)
                        _cursorHider.ShowAtScreenPoint(_hookEngine.CenterScreenX, _hookEngine.CenterScreenY, targetHwnd);
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
            if (_isConnectingKeymap) return;
            var dev = _cmbDevices.SelectedItem as DeviceInfo;
            if (dev == null || dev.State != "device" || _scrcpyProcess == null || _scrcpyProcess.HasExited)
            {
                if (sender != null)
                    MessageBox.Show("먼저 [연결 및 영상] 탭에서 미러링을 시작해 주세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_activeProfile == null) return;

            _isConnectingKeymap = true;
            try
            {
                SyncProfileFromUi();
                _btnKmStart.IsEnabled = false;
                AppendLog("[키 매핑] 멀티터치 제어 소켓 연결 중...");

                var res = await _controlClient.ConnectAsync(dev.Serial, _detectedScrcpyVersion, _activeDisplayId, _activeDisplayWidth, _activeDisplayHeight);
                AppendLog("[키 매핑] " + res.Item2);
                if (!res.Item1)
                    return;

                if (_scrcpyProcess == null || _scrcpyProcess.HasExited)
                    return;

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
            }
            finally
            {
                _isConnectingKeymap = false;
                UpdateUiStates();
            }
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
            // Replace existing key bind if same (Modifiers + VkCode) already exists so duplicates never shadow each other
            KeyBindItem existingKey = null;
            for (int i = 0; i < _activeProfile.Keys.Count; i++)
            {
                var k = _activeProfile.Keys[i];
                if (k.Modifiers == _pendingNewKeyMods && k.VkCode == _pendingNewKeyVk)
                {
                    existingKey = k;
                    break;
                }
            }
            if (existingKey != null)
            {
                existingKey.Label = (_txtKmNewLabel.Text ?? "").Trim();
                existingKey.IsWheelDrag = _chkKmNewWheel.IsChecked == true;
            }
            else
            {
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
            }
            RefreshKeyListBox();
            if (_keymapOverlay != null) _keymapOverlay.Redraw();
            _pendingNewKeyVk = 0;
            _pendingNewKeyMods = 0;
            _txtKmNewKey.Text = Loc.T("hint_new_key");
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
                // Always learn unknown quest titles FIRST even if already 10/10 or duplicate!
                QuestOcrService.LearnQuestTitle(entry);
                QuestOcrService.AppendToQuestLog(entry);

                string skipToast = null;
                for (int i = 0; i < _scannedQuests.Count; i++)
                {
                    if (QuestOcrService.IsLikelyDuplicate(_scannedQuests[i], entry))
                    {
                        skipToast = Loc.F("q_dup_skip", _scannedQuests.Count, string.IsNullOrEmpty(entry.Title) ? entry.ObjectiveText : entry.Title);
                        break;
                    }
                }
                if (skipToast == null && _scannedQuests.Count >= 10)
                {
                    skipToast = Loc.T("q_full_skip");
                }

                if (skipToast != null)
                {
                    AppendLog("[퀘스트] " + skipToast);
                    if (_keymapOverlay == null)
                    {
                        _keymapOverlay = new KeymapOverlayWindow();
                        _keymapOverlay.ProfileEdited += () => RefreshKeyListBox();
                        _keymapOverlay.SetShowHints(false);
                    }
                    _keymapOverlay.ShowToast(skipToast);
                    if (!_overlayTimer.IsEnabled) _overlayTimer.Start();
                    SyncFpsOverlay();
                    return;
                }

                _scannedQuests.Add(entry);
                RefreshQuestPlannerUi();

                string spPart = string.IsNullOrEmpty(entry.Species) ? "" : (" · " + entry.Species);
                string actPart = (string.IsNullOrEmpty(entry.ActionType) || entry.ActionType == "기타") ? "" : (" " + entry.ActionType);
                string wepPart = string.IsNullOrEmpty(entry.WeaponType) ? "" : (" (" + entry.WeaponType + ")");
                string note = string.Format("[{0}/10] {1}{2}{3}{4}",
                    _scannedQuests.Count, entry.Region, spPart, actPart, wepPart);
                if (_scannedQuests.Count >= 10)
                {
                    bool nestFirst = _chkQuestNestFirst != null && _chkQuestNestFirst.IsChecked == true;
            var plans = QuestOcrService.BuildSimultaneousPlans(_scannedQuests, nestFirst);
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

        private async Task ScanQuestListBatchAsync()
        {
            if (_isQuestScanningNow) return;
            if (_scrcpyProcess == null || _scrcpyProcess.HasExited)
            {
                AppendLog("[퀘스트] 미러링이 실행 중일 때만 목록 일괄 스캔할 수 있습니다.");
                return;
            }

            _isQuestScanningNow = true;
            try
            {
                _scrcpyProcess.Refresh();
                IntPtr hwnd = _scrcpyProcess.MainWindowHandle;
                int vidW = _activeDisplayWidth > 0 ? _activeDisplayWidth : (_controlClient != null ? _controlClient.ScreenWidth : 0);
                int vidH = _activeDisplayHeight > 0 ? _activeDisplayHeight : (_controlClient != null ? _controlClient.ScreenHeight : 0);

                var res = await QuestOcrService.ScanQuestListFromWindowAsync(hwnd, vidW, vidH);
                if (!string.IsNullOrEmpty(res.ErrorMessage))
                {
                    AppendLog("[퀘스트 일괄] " + res.ErrorMessage);
                    return;
                }

                int addedCnt = QuestOcrService.MergeBatchIntoExisting(_scannedQuests, res.Matched, 10);
                RefreshQuestPlannerUi();

                string unkPart = (res.UnknownTitles != null && res.UnknownTitles.Count > 0)
                    ? string.Format(" (미등록 {0}개: 클릭 후 F8 학습)", res.UnknownTitles.Count)
                    : "";
                string note = addedCnt > 0
                    ? string.Format("⚡ 목록 스캔 +{0}개 추가 (총 {1}/10){2}", addedCnt, _scannedQuests.Count, unkPart)
                    : string.Format("⚡ 새 퀘스트 없음 (총 {0}/10){1}", _scannedQuests.Count, unkPart);

                AppendLog("[퀘스트 일괄] " + note);
                if (res.UnknownTitles != null && res.UnknownTitles.Count > 0)
                    AppendLog("[퀘스트 미등록 제목] " + string.Join(", ", res.UnknownTitles.ToArray()));

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
            if (!_isInitializing)
                QuestOcrService.SaveActiveQuests(_scannedQuests);
            _txtQuestCount.Text = Loc.F("q_count_fmt", _scannedQuests.Count);

            bool nestFirst = _chkQuestNestFirst != null && _chkQuestNestFirst.IsChecked == true;
            var plans = QuestOcrService.BuildSimultaneousPlans(_scannedQuests, nestFirst);
            string topRec = _scannedQuests.Count == 0
                ? Loc.T("q_empty_banner")
                : QuestOcrService.GetTopRecommendation(plans, _scannedQuests.Count);
            _txtQuestBanner.Text = topRec;
            UpdateHomeQuestSummary(topRec);

            if (_scannedQuests.Count == 0 || plans == null || plans.Count == 0)
            {
                _liveQuestRecShort = "";
            }
            else
            {
                var p0 = plans[0];
                string shortRec = !string.IsNullOrEmpty(p0.Header) ? p0.Header : topRec;
                if (shortRec.Length > 42) shortRec = shortRec.Substring(0, 41) + "…";
                _liveQuestRecShort = shortRec;
            }
            UpdateMirrorWindowTitle();

            _pnlQuestPlans.Children.Clear();
            if (_scannedQuests.Count == 0)
            {
                _pnlQuestPlans.Children.Add(new TextBlock
                {
                    Text = Loc.T("q_empty_list"),
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

        private void ToggleChatBar()
        {
            if (_scrcpyProcess == null || _scrcpyProcess.HasExited) return;
            _scrcpyProcess.Refresh();
            IntPtr hwnd = _scrcpyProcess.MainWindowHandle;

            if (_chatWindow == null)
            {
                _chatWindow = new ChatInputWindow();
                _chatWindow.TextSubmitted += (text) => SendChatTextToGameAsync(text);
                _chatWindow.Cancelled += () =>
                {
                    if (_scrcpyProcess != null && !_scrcpyProcess.HasExited)
                    {
                        try { _scrcpyProcess.Refresh(); SetForegroundWindow(_scrcpyProcess.MainWindowHandle); } catch { }
                    }
                };
            }

            if (_chatWindow.IsVisible)
            {
                _chatWindow.Hide();
                if (hwnd != IntPtr.Zero) SetForegroundWindow(hwnd);
                return;
            }

            if (_hookEngine != null && _hookEngine.IsActive)
                _hookEngine.ReleaseForOverlay();

            _chatWindow.OpenForTarget(hwnd);
        }

        private async void SendChatTextToGameAsync(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                IntPtr hwnd = IntPtr.Zero;
                if (_scrcpyProcess != null && !_scrcpyProcess.HasExited)
                {
                    _scrcpyProcess.Refresh();
                    hwnd = _scrcpyProcess.MainWindowHandle;
                }

                // Always copy to PC clipboard first & restore game window focus BEFORE pasting
                // (Fixes friend search / Unity TMP_InputField boxes that drop paste if sent while unfocused!)
                try { Clipboard.SetText(text); } catch { }
                if (hwnd != IntPtr.Zero)
                {
                    SetForegroundWindow(hwnd);
                    await Task.Delay(75);
                }

                if (_controlClient != null && _controlClient.IsConnected && _controlClient.SetClipboardAndPaste(text))
                {
                    AppendLog("[한글 채팅] 전송 완료: " + text);
                    return;
                }

                // Fallback for Gamepad / Normal mode (when auxiliary control client is not connected):
                // Send Ctrl+V to focused scrcpy window so scrcpy pastes UTF-8 directly into Android!
                if (hwnd != IntPtr.Zero)
                {
                    keybd_event(0x11, 0, 0, UIntPtr.Zero);               // Ctrl down
                    keybd_event(0x56, 0, 0, UIntPtr.Zero);               // V down
                    keybd_event(0x56, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // V up
                    keybd_event(0x11, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // Ctrl up
                }
                AppendLog("[한글 채팅] 붙여넣기 전송 완료: " + text);
            }
            catch (Exception ex)
            {
                AppendLog("[한글 채팅 오류] " + ex.Message);
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
            _txtQuestDaily.Text = Loc.F("q_daily_fmt", _settings.DailyQuestClearedCount);
            UpdateHomeQuestSummary(null);
        }

        private void UpdateHomeQuestSummary(string recText)
        {
            if (_txtHomeQuestMeta != null && _settings != null)
                _txtHomeQuestMeta.Text = Loc.F("q_home_meta_fmt", _settings.DailyQuestClearedCount, _scannedQuests.Count);
            if (_txtHomeQuestRec != null && recText != null)
                _txtHomeQuestRec.Text = recText;
        }

        private void SwitchPage(string name)
        {
            if (_pageHome == null) return;
            _pageHome.Visibility = name == "home" ? Visibility.Visible : Visibility.Collapsed;
            _pageKeymap.Visibility = name == "keymap" ? Visibility.Visible : Visibility.Collapsed;
            _pageQuest.Visibility = name == "quest" ? Visibility.Visible : Visibility.Collapsed;
            _pageCodex.Visibility = name == "codex" ? Visibility.Visible : Visibility.Collapsed;
            _pageAdvanced.Visibility = name == "advanced" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ApplyVisualPreset(string presetId)
        {
            bool prev = _isInitializing;
            _isInitializing = true;
            try
            {
                if (presetId == "1080p")
                {
                    SelectComboByTag(_cmbCodec, "h264", 0);
                    _txtBitrate.Text = "20";
                    _chkVirtualDisplay.IsChecked = true;
                    _cmbVdSpec.Text = "1920x1080/280";
                    _chkNoVdDecorations.IsChecked = true;
                    _chkLowLatency.IsChecked = false;
                    _chkAudioSafe.IsChecked = false;
                }
                else if (presetId == "1440p")
                {
                    SelectComboByTag(_cmbCodec, "h265", 1);
                    _txtBitrate.Text = "20";
                    _chkVirtualDisplay.IsChecked = true;
                    _cmbVdSpec.Text = "2560x1440/373";
                    _chkNoVdDecorations.IsChecked = true;
                    _chkLowLatency.IsChecked = false;
                    _chkAudioSafe.IsChecked = false;
                }
                else if (presetId == "monitor")
                {
                    var scr = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                    int mw = scr.Width;
                    int mh = scr.Height;
                    // Cap ultra-high 4K+ height to 1440p preserving exact monitor aspect ratio so mobile GPU stays smooth
                    if (mh > 1440)
                    {
                        double scale = 1440.0 / mh;
                        mw = ((int)Math.Round(mw * scale) / 8) * 8;
                        mh = 1440;
                    }
                    int dpi = Math.Max(220, Math.Min(420, (int)Math.Round(mh * 280.0 / 1080.0)));
                    SelectComboByTag(_cmbCodec, mw >= 2560 ? "h265" : "h264", mw >= 2560 ? 1 : 0);
                    _txtBitrate.Text = "20";
                    _chkVirtualDisplay.IsChecked = true;
                    _cmbVdSpec.Text = string.Format("{0}x{1}/{2}", mw, mh, dpi);
                    _chkNoVdDecorations.IsChecked = true;
                    _chkLowLatency.IsChecked = false;
                    _chkAudioSafe.IsChecked = false;
                }
                else if (presetId == "native")
                {
                    SelectComboByTag(_cmbCodec, "h264", 0);
                    _txtBitrate.Text = "20";
                    _chkVirtualDisplay.IsChecked = false;
                    _chkLowLatency.IsChecked = false;
                    _chkAudioSafe.IsChecked = false;
                }
                else if (presetId == "wifi")
                {
                    SelectComboByTag(_cmbCodec, "h265", 1);
                    _txtBitrate.Text = "12";
                    _chkLowLatency.IsChecked = false;
                    _chkAudioSafe.IsChecked = true;
                }
                if (_settings != null) _settings.SelectedVisualPreset = presetId;
            }
            finally
            {
                _isInitializing = prev;
            }
            HighlightActiveVisualPreset();
            UpdateUiStates();
            UpdateCommandPreview();
            SaveSettingsFromUi();
            AppendLog("[프리셋] 화면 프리셋 적용됨: " + presetId);
        }

        private void HighlightActiveVisualPreset()
        {
            if (_btnPreset1080 == null) return;
            string cur = _settings != null ? (_settings.SelectedVisualPreset ?? "1080p") : "1080p";
            var map = new Tuple<string, Button>[]
            {
                Tuple.Create("1080p", _btnPreset1080),
                Tuple.Create("1440p", _btnPreset1440),
                Tuple.Create("monitor", _btnPresetMonitor),
                Tuple.Create("native", _btnPresetNative),
                Tuple.Create("wifi", _btnPresetWifi)
            };
            var activeBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C5E3F"));
            var activeFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFDF8"));
            var activeBd = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B8832A"));
            var idleBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFDF8"));
            var idleFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2A24"));
            var idleBd = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4C9B4"));

            foreach (var pair in map)
            {
                bool active = pair.Item1 == cur;
                pair.Item2.Background = active ? activeBg : idleBg;
                pair.Item2.Foreground = active ? activeFg : idleFg;
                pair.Item2.BorderBrush = active ? activeBd : idleBd;
            }
        }

        private void RefreshMonsterCodexUi()
        {
            if (_pnlCodexList == null) return;
            _pnlCodexList.Children.Clear();

            string query = _txtCodexSearch != null ? (_txtCodexSearch.Text ?? "").Trim() : "";
            string selRegion = _cmbCodexRegion != null ? GetSelectedTag(_cmbCodexRegion, "") : "";
            string selSpecies = _cmbCodexSpecies != null ? GetSelectedTag(_cmbCodexSpecies, "") : "";

            int shown = 0;
            for (int i = 0; i < QuestOcrService.MonsterCatalog.Length; i++)
            {
                var m = QuestOcrService.MonsterCatalog[i];
                if (!string.IsNullOrEmpty(query) &&
                    m.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                    m.Species.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (!string.IsNullOrEmpty(selRegion))
                {
                    bool hasReg = false;
                    for (int r = 0; r < m.Regions.Length; r++)
                        if (m.Regions[r] == selRegion) { hasReg = true; break; }
                    if (!hasReg) continue;
                }

                if (!string.IsNullOrEmpty(selSpecies))
                {
                    if (selSpecies == "융광종") { if (!m.IsRadiant) continue; }
                    else if (m.Species != selSpecies) continue;
                }

                shown++;
                var rowCard = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(m.IsRadiant ? "#FDF6E7" : "#FFFFFF")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(m.IsRadiant ? "#E6C98B" : "#E5DEC9")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 0, 0, 6)
                };

                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(195) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var tbName = new TextBlock
                {
                    Text = m.Name,
                    FontSize = 12.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(m.IsRadiant ? "#8C5E14" : "#1F2922")),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(tbName, 0);
                g.Children.Add(tbName);

                var spBadge = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E6EFE9")),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(6, 2, 6, 2),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = m.Species,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E462E"))
                    }
                };
                Grid.SetColumn(spBadge, 1);
                g.Children.Add(spBadge);

                var regWrap = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
                if (m.Regions == null || m.Regions.Length == 0)
                {
                    regWrap.Children.Add(new TextBlock { Text = "특수 / 이벤트 전용", FontSize = 11, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8F8677")) });
                }
                else
                {
                    for (int r = 0; r < m.Regions.Length; r++)
                    {
                        regWrap.Children.Add(new Border
                        {
                            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F1E8")),
                            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDD4C3")),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(5),
                            Padding = new Thickness(7, 1.5, 7, 1.5),
                            Margin = new Thickness(0, 0, 5, 0),
                            Child = new TextBlock
                            {
                                Text = m.Regions[r],
                                FontSize = 11,
                                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A443A"))
                            }
                        });
                    }
                }
                Grid.SetColumn(regWrap, 2);
                g.Children.Add(regWrap);

                rowCard.Child = g;
                _pnlCodexList.Children.Add(rowCard);
            }

            if (shown == 0)
            {
                _pnlCodexList.Children.Add(new TextBlock
                {
                    Text = "조건에 일치하는 몬스터가 없습니다.",
                    FontSize = 12,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8F8677")),
                    Margin = new Thickness(6, 12, 6, 12)
                });
            }
        }

        private static bool TryCopyPngToClipboard(string pngPath)
        {
            try
            {
                if (string.IsNullOrEmpty(pngPath) || !File.Exists(pngPath)) return false;
                byte[] bytes = File.ReadAllBytes(pngPath);
                using (var ms = new MemoryStream(bytes))
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    Clipboard.SetImage(bmp);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsBoundsOnAnyScreen(int x, int y, int w, int h)
        {
            try
            {
                int cx = x + w / 2;
                int cy = y + Math.Min(h / 2, 60);
                foreach (var scr in System.Windows.Forms.Screen.AllScreens)
                {
                    if (scr.WorkingArea.Contains(cx, cy))
                        return true;
                }
            }
            catch { }
            return false;
        }

        private void PersistTrackedWindowBounds()
        {
            if (_settings == null || _trackedWinW < 300 || _trackedWinH < 200) return;
            _settings.LastWinX = _trackedWinX;
            _settings.LastWinY = _trackedWinY;
            _settings.LastWinW = _trackedWinW;
            _settings.LastWinH = _trackedWinH;
            _settings.Save();
        }

        private void StopScrcpy()
        {
            PersistTrackedWindowBounds();
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
            if (_cmbVideoEncoder != null)
            {
                if (!string.IsNullOrWhiteSpace(_settings.VideoEncoder))
                    _cmbVideoEncoder.Text = _settings.VideoEncoder;
                else
                    _cmbVideoEncoder.SelectedIndex = 0;
            }
            if (_cmbVideoBuffer != null)
            {
                string vbStr = _settings.VideoBufferMs > 0 ? _settings.VideoBufferMs.ToString() : "0";
                SelectComboByTag(_cmbVideoBuffer, vbStr, 0);
                if (_settings.VideoBufferMs > 0 && GetSelectedTag(_cmbVideoBuffer, "0") != vbStr)
                    _cmbVideoBuffer.Text = vbStr + " ms";
            }

            string mode = (_settings.ControlMode ?? "keymap").ToLowerInvariant();
            if (mode == "gamepad") _rbModeGamepad.IsChecked = true;
            else if (mode == "normal") _rbModeNormal.IsChecked = true;
            else _rbModeKeymap.IsChecked = true;

            _chkTurnScreenOff.IsChecked = _settings.TurnScreenOff;
            _chkStayAwake.IsChecked = _settings.StayAwake;
            _chkLowLatency.IsChecked = _settings.LowLatency;
            _chkNoAudio.IsChecked = _settings.NoAudio;
            _chkAudioSafe.IsChecked = _settings.AudioBufferSafe;
            if (_chkAutoMuteBg != null) _chkAutoMuteBg.IsChecked = _settings.AutoMuteBackground;
            if (_chkQuestNestFirst != null) _chkQuestNestFirst.IsChecked = _settings.NestQuestsFirst;
            _chkFullscreen.IsChecked = _settings.Fullscreen;
            _chkAlwaysOnTop.IsChecked = _settings.AlwaysOnTop;
            _chkRememberWin.IsChecked = _settings.RememberWindowBounds;
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
            if (_txtExtraArgs != null)
            {
                _txtExtraArgs.Text = _settings.ExtraScrcpyArgs ?? "";
                if (_txtExtraArgsHint != null)
                    _txtExtraArgsHint.Visibility = string.IsNullOrEmpty(_txtExtraArgs.Text) ? Visibility.Visible : Visibility.Collapsed;
            }

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
            _settings.VideoEncoder = GetSelectedVideoEncoder();
            int vbufSave;
            _settings.VideoBufferMs = int.TryParse(GetSelectedTag(_cmbVideoBuffer, "0"), out vbufSave) ? vbufSave : 0;
            _settings.TurnScreenOff = _chkTurnScreenOff.IsChecked == true;
            _settings.StayAwake = _chkStayAwake.IsChecked == true;
            _settings.LowLatency = _chkLowLatency.IsChecked == true;
            _settings.NoAudio = _chkNoAudio.IsChecked == true;
            _settings.AudioBufferSafe = _chkAudioSafe.IsChecked == true;
            if (_chkAutoMuteBg != null) _settings.AutoMuteBackground = _chkAutoMuteBg.IsChecked == true;
            if (_chkQuestNestFirst != null) _settings.NestQuestsFirst = _chkQuestNestFirst.IsChecked == true;
            _settings.Fullscreen = _chkFullscreen.IsChecked == true;
            _settings.AlwaysOnTop = _chkAlwaysOnTop.IsChecked == true;
            _settings.RememberWindowBounds = _chkRememberWin.IsChecked == true;
            _settings.PrintFps = _chkPrintFps.IsChecked == true;
            _settings.GamepadKeyboardAssist = _chkGamepadKbAssist.IsChecked == true;
            _settings.RecordFolder = _txtRecordFolder.Text.Trim();
            _settings.UseVirtualDisplay = _chkVirtualDisplay.IsChecked == true;
            _settings.VirtualDisplaySpec = _cmbVdSpec.Text.Trim();
            _settings.NoVdSystemDecorations = _chkNoVdDecorations.IsChecked == true;
            if (_txtExtraArgs != null) _settings.ExtraScrcpyArgs = (_txtExtraArgs.Text ?? "").Trim();
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
