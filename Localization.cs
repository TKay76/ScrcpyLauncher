using System;
using System.Collections.Generic;
using System.Globalization;

namespace ScrcpyLauncher
{
    public static class Loc
    {
        public static string CurrentLang { get; private set; }

        private static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>();

        static Loc()
        {
            CurrentLang = DetectDefaultLanguage();
            InitTable();
        }

        public static string DetectDefaultLanguage()
        {
            try
            {
                string iso = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
                if (iso == "ko") return "ko";
                if (iso == "ja") return "ja";
            }
            catch { }
            return "en";
        }

        public static void SetLanguage(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                CurrentLang = DetectDefaultLanguage();
                return;
            }
            string l = lang.Trim().ToLowerInvariant();
            if (l == "ko" || l == "en" || l == "ja")
                CurrentLang = l;
            else
                CurrentLang = DetectDefaultLanguage();
        }

        public static string T(string key)
        {
            string[] vals;
            if (!Table.TryGetValue(key, out vals) || vals == null || vals.Length < 3)
                return key;
            if (CurrentLang == "en") return vals[1];
            if (CurrentLang == "ja") return vals[2];
            return vals[0];
        }

        public static string F(string key, params object[] args)
        {
            try { return string.Format(T(key), args); }
            catch { return T(key); }
        }

        private static void A(string key, string ko, string en, string ja)
        {
            Table[key] = new[] { ko, en, ja };
        }

        private static void InitTable()
        {
            // Window & Header
            A("win_title", "HuntLink v2.0 — 몬헌 아웃랜더스 PC 런처", "HuntLink v2.0 — MHO PC Launcher", "HuntLink v2.0 — MHO PC ランチャー");
            A("scrcpy_checking", "scrcpy 확인 중...", "Checking scrcpy...", "scrcpy 確認中...");
            A("scrcpy_ready", "scrcpy 준비됨", "scrcpy Ready", "scrcpy 準備完了");
            A("scrcpy_ready_ver", "scrcpy {0} 준비됨", "scrcpy {0} Ready", "scrcpy {0} 準備完了");
            A("scrcpy_missing", "scrcpy.exe 미발견", "scrcpy.exe Missing", "scrcpy.exe 未検出");
            A("btn_download_scrcpy", "scrcpy 다운로드", "Download scrcpy", "scrcpy 取得");
            A("btn_setup_guide", "연결 · 문제해결 ?", "Guide & Fix ?", "接続・対処法 ?");
            A("btn_shortcuts", "단축키", "Shortcuts", "ショートカット");
            A("btn_folder", "폴더 변경", "Folder", "フォルダ");
            A("btn_update", "업데이트 {0}", "Update {0}", "更新 {0}");
            A("btn_updating", "업데이트 중...", "Updating...", "更新中...");

            // Control Mode Cards
            A("mode_km_title", "키보드 + 마우스 (키 매핑)", "Keyboard + Mouse (Keymap)", "キーボード＋マウス (キーマッピング)");
            A("mode_km_sub", "WASD · 시점 회전 · 자동 연결", "WASD · Mouse Look · Auto Connect", "WASD · カメラ操作 · 自動接続");
            A("mode_gp_title", "게임패드 (Xbox / PS)", "Gamepad (Xbox / PS)", "ゲームパッド (Xbox / PS)");
            A("mode_gp_sub", "UHID 컨트롤러 · 우클릭 충돌 방지", "UHID Controller · No Right-Click Back", "UHID コントローラー · 右クリック干渉防止");
            A("mode_nm_title", "일반 미러링 (기본)", "Standard Mirroring", "通常ミラーリング");
            A("mode_nm_sub", "일반 터치 · 마우스 · 영상 시청", "Standard Touch · Mouse · Viewing", "通常タッチ · マウス · 視聴");

            A("summary_km", "적용 프리셋: {0}  ·  시점 토글: {1}  ·  F1: 힌트  ·  F2: 채팅(한글)", "Preset: {0}  ·  Look Toggle: {1}  ·  F1: Hints  ·  F2: Chat", "プリセット: {0}  ·  視点切替: {1}  ·  F1: ヒント  ·  F2: チャット");
            A("summary_gp", "게임패드 모드: UHID 물리 컨트롤러(-G) 자동 활성화 · 우클릭 뒤로가기 방지", "Gamepad Mode: UHID Controller (-G) active · Right-click Back disabled", "ゲームパッドモード: UHID コントローラー(-G)有効 · 右クリック戻る防止");
            A("summary_nm", "일반 미러링 모드: 스마트폰 화면을 일반 마우스와 키보드로 조작합니다.", "Standard Mode: Control Android normally with mouse & keyboard.", "通常モード: 通常のマウスとキーボードで操作します。");

            // Tabs / Sidebar Navigation
            A("tab_conn", "홈 · 실행", "Home & Play", "ホーム・起動");
            A("tab_keymap", "키 매핑", "Key Mapping", "キーマッピング");
            A("tab_quest", "퀘스트 플래너", "Quest Planner", "クエスト整理");
            A("tab_codex", "몬스터 도감", "Monster Codex", "モンスター図鑑");
            A("tab_advanced", "고급 설정", "Advanced", "詳細設定");

            // HuntLink v2.0 Home, Presets, Quest, Codex, Advanced
            A("lbl_presets_title", "화면 · 성능 원클릭 프리셋", "One-Click Display & Performance Presets", "ワンクリック映像・性能プリセット");
            A("preset_1080", "추천 1080p", "Recommended 1080p", "おすすめ 1080p");
            A("preset_1440", "고화질 1440p", "High-Res 1440p", "高画質 1440p");
            A("preset_native", "기기 본체 화면", "Native Device Screen", "端末本体画面");
            A("preset_wifi", "무선 안정", "Wi-Fi Smooth", "無線安定");
            A("lbl_home_quest", "긴급 퀘스트 플래너", "Urgent Quest Planner", "緊急クエストプランナー");
            A("btn_go_quest", "플래너 열기 →", "Open Planner →", "プランナーへ →");
            A("lbl_quick_opts", "자주 쓰는 빠른 옵션 (세부 설정은 좌측 [고급 설정] 탭)", "Quick Options (See [Advanced] tab for full settings)", "よく使う設定 (詳細は左の[詳細設定]タブ)");
            A("btn_bottom_folder", "📁 폴더", "📁 Folder", "📁 フォルダ");
            A("btn_toggle_log_open", "📋 로그", "📋 Log", "📋 ログ");
            A("btn_toggle_log_close", "📋 로그 접기", "📋 Hide Log", "📋 ログを閉じる");

            A("q_sec1", "긴급 퀘스트 수집 ([F7] 왼쪽 목록 일괄 스캔 · [F8] 개별 수집 및 자동 학습)", "Quest Scan ([F7] Batch Scan Left List · [F8] Single Scan & Auto-Learn)", "クエスト収集 ([F7] 左リスト一括スキャン · [F8] 個別スキャン)");
            A("q_sec2", "동시 클리어 추천 조합 (클리어한 조합의 [✓ 완료]를 눌러 비우세요)", "Simultaneous Clear Plans (Click [✓] when cleared)", "同時クリアおすすめ組み合わせ (クリア済みに [✓] をクリック)");
            A("q_btn_batch", "⚡ 목록 일괄 (F7)", "⚡ Batch Scan (F7)", "⚡ 一括スキャン (F7)");
            A("q_btn_one", "+ 개별 (F8)", "+ Single (F8)", "+ 個別 (F8)");
            A("q_btn_undo", "↩ 취소", "↩ Undo", "↩ 戻す");
            A("q_btn_reset", "리셋", "Reset", "リセット");
            A("q_btn_clear", "비우기", "Clear", "全消去");
            A("q_daily_fmt", "오늘 완료: {0}건", "Cleared Today: {0}", "本日クリア: {0}件");
            A("q_count_fmt", "보유: {0} / 10", "Held: {0} / 10", "所持: {0} / 10");
            A("q_home_meta_fmt", "  ·  오늘 완료: {0}건  ·  보유: {1} / 10", "  ·  Today: {0}  ·  Held: {1} / 10", "  ·  本日: {0}件  ·  所持: {1} / 10");
            A("q_daily_tip", "매일 한국시간 오전 5시에 자동 초기화됩니다.", "Resets automatically every day at 05:00 KST.", "毎日 5:00 (KST) に自動リセットされます。");
            A("q_empty_banner", "게임에서 퀘스트 창을 열고 [F7] 목록 일괄 스캔 또는 [F8] 개별 수집을 누르세요.", "Open the in-game quest list and press [F7] Batch Scan or [F8] Single Scan.", "ゲーム内のクエスト一覧を開き、[F7] 一括スキャンまたは [F8] 個別スキャンを押してください。");
            A("q_empty_list", "아직 수집된 퀘스트가 없습니다. 게임에서 퀘스트 창을 연 뒤 [F7] 일괄 스캔 또는 [F8] 개별 수집을 누르세요.", "No quests scanned yet. Open the in-game quest list and press [F7] Batch Scan or [F8] Single Scan.", "まだ収集されたクエストがありません。クエスト画面で [F7] 一括または [F8] 個別スキャンを実行してください。");

            // Dropdown items & placeholders
            A("cmb_h264", "h264 (안정/저지연)", "h264 (Stable / Low-Lat)", "h264 (安定・低遅延)");
            A("cmb_h265", "h265 (고화질)", "h265 (High Quality)", "h265 (高画質)");
            A("cmb_maxsize_0", "0 (제한없음)", "0 (Unlimited)", "0 (無制限)");
            A("cmb_maxfps_0", "0 (자동)", "0 (Auto)", "0 (自動)");
            A("cmb_renderer_auto", "자동 (D3D11)", "Auto (D3D11)", "自動 (D3D11)");
            A("dev_none", "(연결된 기기 없음 — USB를 꽂거나 무선 연결하세요)", "(No device connected — plug in USB or connect via Wi-Fi)", "(デバイス未接続 — USB接続または無線接続してください)");

            // USB / Wi-Fi Connection Link Speed Status
            A("conn_usb3_ok", "초고속 유선 연결 — 최고 화질·최저 지연에 최적", "SuperSpeed wired link — ideal for max quality & lowest latency", "超高速有線接続 — 最高画質・最低遅延に最適");
            A("conn_usb3_tip", "USB 3.x 초고속 링크로 연결되어 있습니다. 고화질(1440p) 및 높은 비트레이트도 여유롭게 전송됩니다.", "Connected over USB 3.x SuperSpeed. Handles high resolution (1440p) and high bitrates effortlessly.", "USB 3.x 超高速リンクで接続中です。高画質(1440p)や高ビットレートも余裕で転送できます。");
            A("conn_usb2_upgrade", "미러링 충분 · 단, 기기는 USB 3.0+ 지원 (고속 케이블/포트 시 5Gbps+)", "Fine for mirroring · Device supports USB 3.0+ (use 5Gbps cable/port)", "ミラーリング可能 · 端末はUSB 3.0+対応 (高速ケーブル/ポートで5Gbps+)");
            A("conn_usb2_upgrade_tip", "현재 USB 2.0 (480 Mbps)로 연결됨. 1080p 20Mbps 미러링에는 충분하지만, 스마트폰은 USB 3.0+(5Gbps)을 지원하므로 고속 데이터 케이블(5Gbps/10Gbps)과 USB 3.x 포트를 사용하면 더 안정적입니다.", "Currently linked at USB 2.0 (480 Mbps). Enough for 1080p 20Mbps mirroring, but your device supports USB 3.0+ — try a 5Gbps/10Gbps data cable & USB 3.x port.", "現在 USB 2.0 (480 Mbps) 接続です。1080p 20Mbps には十分ですが、端末は USB 3.0+ 対応のため高速データケーブルと USB 3.x ポートで更に安定します。");
            A("conn_usb2_ok", "안정적인 USB 2.0 유선 연결 (1080p 미러링 충분)", "Stable USB 2.0 wired link (plenty for 1080p mirroring)", "安定した USB 2.0 有線接続 (1080p ミラーリングに十分)");
            A("conn_usb2_tip", "USB 2.0 (480 Mbps) 링크로 연결되어 있습니다. 1080p 15~25Mbps 미러링에 충분한 대역폭입니다.", "Connected at USB 2.0 (480 Mbps), providing plenty of bandwidth for 1080p 15–25 Mbps mirroring.", "USB 2.0 (480 Mbps) リンクで接続中です。1080p 15〜25Mbps のミラーリングに十分な帯域です。");
            A("conn_usb1_warn", "저속 USB 1.x 감지됨 — 케이블 또는 USB 포트를 교체해 주세요", "Slow USB 1.x link detected — please change cable or USB port", "低速 USB 1.x を検出 — ケーブルまたはポートを変更してください");
            A("conn_usb_generic", "유선 USB 연결됨", "Wired USB connected", "有線 USB 接続中");
            A("conn_wifi_fast", "고속 무선 연결 — 무선 안정 프리셋 권장", "Fast Wi-Fi link — Wi-Fi Smooth preset recommended", "高速無線接続 — 無線安定プリセット推奨");
            A("conn_wifi_slow", "2.4 GHz 또는 저속 Wi-Fi — 끊길 경우 5 GHz 연결 또는 유선 권장", "2.4 GHz / slower Wi-Fi — switch to 5 GHz or USB if stuttering", "2.4 GHz または低速 Wi-Fi — カクつく場合は 5 GHz か有線推奨");
            A("conn_wifi_generic", "무선 Wi-Fi ADB 연결됨", "Wireless Wi-Fi ADB connected", "ワイヤレス Wi-Fi ADB 接続中");
            A("batt_charging", "충전 중", "Charging", "充電中");
            A("batt_discharging", "배터리 사용 중", "On Battery", "バッテリー動作中");
            A("batt_tip_cool", "배터리 {0}% ({2}) · 온도 {1}°C — 양호한 온도입니다. (15초마다 자동 갱신)", "Battery {0}% ({2}) · Temp {1}°C — Normal temperature. (Auto-refreshes every 15s)", "バッテリー {0}% ({2}) · 温度 {1}°C — 正常な温度です。(15秒ごとに自動更新)");
            A("batt_tip_warm", "배터리 {0}% ({2}) · 온도 {1}°C — 약간 따뜻합니다. 발열이 오르면 '폰 화면 끄기(-S)'를 켜보세요.", "Battery {0}% ({2}) · Temp {1}°C — Getting warm. Try enabling 'Turn Screen Off (-S)' to reduce heat.", "バッテリー {0}% ({2}) · 温度 {1}°C — やや発熱しています。「端末画面オフ(-S)」の併用をおすすめします。");
            A("batt_tip_hot", "배터리 {0}% ({2}) · 온도 {1}°C — 고온 주의! 발열 스로틀링으로 프레임이 떨어질 수 있으니 쿨러 사용이나 잠시 휴식을 권장합니다.", "Battery {0}% ({2}) · Temp {1}°C — High temperature! Thermal throttling may reduce FPS; cooling or a short break is recommended.", "バッテリー {0}% ({2}) · 温度 {1}°C — 高温注意！発熱によるフレーム低下の恐れがあります。冷却をおすすめします。");

            // Interactive Guides & Tooltips
            A("g_dl_t", "scrcpy 공식 다운로드", "Official scrcpy Download", "scrcpy 公式ダウンロード");
            A("g_dl_b", "공식 GitHub Releases 페이지에서 최신 Windows용 scrcpy zip 파일을 받아 압축을 푼 뒤, 이 실행 파일을 같은 폴더에 넣어주세요.", "Download the latest Windows scrcpy zip from official GitHub Releases, extract it, and place this launcher in the same folder.", "公式 GitHub Releases から最新の Windows 版 scrcpy zip をダウンロードして展開し、このランチャーを同じフォルダに配置してください。");
            A("g_setup_t", "스마트폰 최초 연결 방법", "Initial Android Setup Guide", "スマホ初期接続ガイド");
            A("g_setup_b", "개발자 옵션 활성화, USB 디버깅 켜기, 무선 디버깅 연결 방법을 처음 사용하는 분도 따라할 수 있게 단계별로 안내합니다.", "Step-by-step guide to enabling Developer Options, USB Debugging, and Wireless Debugging.", "開発者向けオプション、USBデバッグ、ワイヤレスデバッグの有効化手順をわかりやすく案内します。");
            A("g_short_t", "실행 중 단축키 안내", "Active Shortcuts Guide", "実行中ショートカット一覧");
            A("g_short_b", "전체화면 전환(Alt+F), 폰 화면 끄기/켜기(Alt+O / Alt+Shift+O), FPS 표시(Alt+I) 등 유용한 단축키를 확인합니다.", "View useful shortcuts such as Fullscreen (Alt+F), Screen Off/On (Alt+O / Alt+Shift+O), F2 Chat, F7/F8 Quest Scan, and F12 Screenshot.", "フルスクリーン(Alt+F)、端末画面オフ/オン(Alt+O / Alt+Shift+O)、F2チャット、F7/F8クエスト収集などのショートカットを確認します。");
            A("g_u2w_t", "유선 → 무선 전환", "Switch USB → Wi-Fi", "有線 → 無線切替");
            A("g_u2w_b", "USB로 연결된 상태에서 클릭하면 폰의 Wi-Fi IP를 자동 조회해 무선 연결로 바꿔줍니다. 완료 후 케이블을 뽑고 충전기를 연결할 수 있습니다.", "While connected via USB, automatically detects the device's Wi-Fi IP and switches to wireless ADB so you can unplug the cable.", "USB接続中にクリックすると端末のWi-Fi IPを自動取得して無線接続へ切り替えます。完了後はケーブルを抜いてプレイ可能です。");
            A("g_discw_t", "무선 연결 해제", "Disconnect Wi-Fi ADB", "無線接続を解除");
            A("g_discw_b", "현재 연결된 무선(Wi-Fi) ADB 기기 연결을 끊습니다. (선택된 무선 기기 또는 전체 무선 연결 해제)", "Disconnects the selected wireless ADB device (or all wireless connections).", "現在接続中のワイヤレス(Wi-Fi) ADB デバイスを切断します。");
            A("g_adb_t", "ADB 서버 재시작", "Restart ADB Server", "ADB サーバー再起動");
            A("g_adb_b", "기기가 offline 또는 unauthorized 상태로 멈추거나 인식이 꼬였을 때 ADB 서버를 깔끔하게 재시작합니다.", "Cleanly restarts the ADB server if your device is stuck offline, unauthorized, or fails to detect.", "デバイスが offline / unauthorized のまま認識しないときに ADB サーバーを再起動します。");
            A("g_codec_t", "비디오 코덱", "Video Codec", "ビデオコーデック");
            A("g_codec_b", "H.264: USB 유선·실시간 게임에 가장 빠르고 지연이 적음. / H.265: Wi-Fi 무선이나 낮은 비트레이트에서 화질이 더 선명함.", "H.264: Fastest & lowest latency for wired USB gaming. / H.265: Sharper quality over Wi-Fi or lower bitrates.", "H.264: 有線USB・リアルタイムゲームに最速・低遅延。/ H.265: Wi-Fi無線や低ビットレートでも高画質。");
            A("g_br_t", "비트레이트 (Mbps)", "Bitrate (Mbps)", "ビットレート (Mbps)");
            A("g_br_b", "영상 전송 대역폭입니다. 1080p 유선은 15~25Mbps, 무선은 8~16Mbps가 적당합니다. 너무 높이면 인코딩 발열·지연이 늘어납니다.", "Video bandwidth. 15–25 Mbps is ideal for 1080p wired USB; 8–16 Mbps for Wi-Fi. Too high increases heat and latency.", "映像転送帯域です。1080p有線は15〜25Mbps、無線は8〜16Mbps推奨。高すぎると発熱や遅延が増えます。");
            A("g_ms_t", "최대 크기 (-m)", "Max Size (-m)", "最大解像度 (-m)");
            A("g_ms_b", "일반 미러링 시 화면 긴 변의 최대 픽셀을 제한합니다. 가상 디스플레이를 쓰면 무시됩니다. 고해상도 기기에서 버벅일 때 1920이나 1600으로 줄여보세요.", "Limits the longest side in pixels for standard mirroring (ignored when Virtual Display is enabled). Try 1920 or 1600 if lagging.", "通常ミラーリング時の長辺最大ピクセルを制限します（仮想ディスプレイ使用時は無視）。重い場合は1920や1600に下げてください。");
            A("g_fps_t", "최대 FPS", "Max FPS", "最大 FPS");
            A("g_fps_b", "0(자동)을 권장합니다. 일부 안드로이드 기기에서는 60으로 제한하면 오히려 프레임이 낮아질 수 있으니 문제 시 0으로 두세요.", "0 (Auto) is recommended. Capping to 60 can reduce frame rate on some Android devices; leave at 0 if unsure.", "0 (自動) を推奨します。一部端末では60に制限すると逆にフレームが落ちるため、問題時は0にしてください。");
            A("g_ren_t", "PC 렌더러", "PC Renderer", "PC レンダラ");
            A("g_ren_b", "자동(Direct3D11)은 PC GPU 하드웨어 가속(D3D11VA)을 사용해 가장 빠릅니다. 화면이 깨지거나 안 나올 때만 OpenGL을 시도하세요.", "Auto (Direct3D11) uses GPU hardware acceleration and is fastest. Switch to OpenGL only if you see a black or corrupted screen.", "自動 (Direct3D11) はGPUハードウェア支援で最速です。画面が真っ暗・崩れる場合のみ OpenGL を試してください。");
            A("g_mkm_t", "키보드 + 마우스 (키 매핑) 모드", "Keyboard + Mouse (Keymap) Mode", "キーボード＋マウス (キーマッピング) モード");
            A("g_mkm_b", "scrcpy 자체 입력 충돌을 자동 차단하고, 미러링 시작 즉시 WASD · 마우스 시점 회전 · 스킬 키 매핑과 일반 클릭 터치를 자동 연결합니다.", "Prevents scrcpy input conflicts and automatically connects WASD movement, mouse camera look, skill keys, and normal touch clicks.", "scrcpyの入力競合を自動回避し、起動と同時にWASD移動・マウス視点操作・スキルキー・通常タッチを接続します。");
            A("g_mgp_t", "게임패드 (Xbox / PS) 모드", "Gamepad (Xbox / PS) Mode", "ゲームパッド (Xbox / PS) モード");
            A("g_mgp_b", "PC에 연결된 물리 컨트롤러를 UHID 게임패드(-G)로 인식시키며, 우클릭 뒤로가기 충돌을 방지합니다.", "Passes your PC physical controller through as a UHID gamepad (-G) and blocks accidental right-click Back presses.", "PC接続の物理コントローラーを UHID ゲームパッド(-G)として認識させ、右クリック戻る誤爆を防ぎます。");
            A("g_mnm_t", "일반 미러링 모드", "Standard Mirroring Mode", "通常ミラーリングモード");
            A("g_mnm_b", "패드나 키 매핑 없이 스마트폰 화면을 일반 마우스와 키보드로 편하게 조작할 때 사용합니다.", "Control your Android screen directly with standard mouse and keyboard without gamepad or keymap hooks.", "ゲームパッドやキーマッピングを使わず、通常のマウスとキーボードでスマホ画面を操作します。");
            A("g_soff_t", "폰 화면 끄기 (-S)", "Turn Screen Off (-S)", "端末画面オフ (-S)");
            A("g_soff_b", "미러링 중 스마트폰 화면을 꺼서 발열과 배터리 소모를 줄입니다. 간혹 기기 절전 때문에 프레임이 떨어지면 체크를 해제해 보세요.", "Turns off the phone display during mirroring to reduce heat and battery drain. Uncheck if your device throttles FPS when screen is off.", "ミラーリング中にスマホ画面を消して発熱と電池消費を抑えます。省電力でFPSが落ちる端末ではオフにしてください。");
            A("g_awake_t", "절전 방지 (-w)", "Stay Awake (-w)", "スリープ防止 (-w)");
            A("g_awake_b", "연결 중 스마트폰이 자동으로 잠기지 않도록 유지합니다.", "Keeps the Android device awake and prevents auto-lock while connected.", "接続中にスマホが自動でスリープ・ロックしないようにします。");
            A("g_ll_t", "저지연 인코딩", "Low-Latency Encoding", "低遅延エンコード");
            A("g_ll_b", "하드웨어 인코더에 low-latency=1 옵션을 전달해 PC 화면이 반박자 밀리는 현상을 줄여줍니다.", "Passes low-latency=1 to the hardware video encoder to reduce display lag. Uncheck if your device shows a black screen.", "ハードウェアエンコーダに low-latency=1 を渡して表示遅延を減らします。画面が真っ暗になる機種ではオフにしてください。");
            A("g_noaud_t", "소리 끄기", "Mute Audio", "音声オフ");
            A("g_noaud_b", "오디오 전송을 끕니다. 블루투스 이어폰을 폰에 직접 연결해 쓰거나, 오디오 버퍼로 인한 지연 여부를 테스트할 때 유용합니다.", "Disables audio forwarding. Useful when using Bluetooth earbuds directly on your phone or testing audio latency.", "音声転送をオフにします。端末に直接Bluetoothイヤホンを繋ぐ場合や、音声遅延の切り分けに便利です。");
            A("g_audsafe_t", "오디오 안정화 (--audio-buffer=120)", "Smooth Audio (--audio-buffer=120)", "音割れ軽減 (--audio-buffer=120)");
            A("g_audsafe_b", "소리가 째지거나 지직거리며 끊길 때 오디오 버퍼에 여유(120ms)를 주어 사운드 깨짐을 방지합니다.", "Adds a 120ms audio buffer to prevent crackling, popping, or audio dropouts.", "音が割れたり途切れたりする場合に120msのバッファを確保して音声を安定させます。");
            A("g_fs_t", "전체화면 시작 (-f)", "Start Fullscreen (-f)", "フルスクリーン起動 (-f)");
            A("g_fs_b", "실행 즉시 PC 모니터 전체화면으로 띄웁니다. 실행 중에도 Alt + F 키로 언제든 전환할 수 있습니다.", "Launches immediately in fullscreen mode. You can toggle fullscreen anytime with Alt+F.", "起動直後にフルスクリーンで表示します。実行中も Alt + F でいつでも切り替え可能です。");
            A("g_aot_t", "항상 위에 (실시간)", "Always on Top (Live)", "常に手前に表示 (即時反映)");
            A("g_aot_b", "미러링 창을 항상 다른 창 위에 고정합니다. 미러링 도중에도 체크를 켜거나 끄면 즉시 반영됩니다.", "Keeps the mirroring window above all other windows. Toggling takes effect immediately even while running.", "ミラーリング画面を常に最前面に固定します。実行中でもチェックのオン/オフが即座に反映されます。");
            A("g_remwin_t", "창 위치 · 크기 기억", "Remember Window Bounds", "ウィンドウ位置・サイズ記憶");
            A("g_remwin_b", "창모드에서 마지막으로 두었던 미러링 창의 모니터 위치와 크기를 기억해 다음 실행 때 똑같은 자리에 띄워줍니다.", "Remembers your last windowed position and size and restores it on the next launch.", "ウィンドウモード時の最後の位置とサイズを記憶し、次回起動時に同じ位置へ復元します。");
            A("g_pfps_t", "화면 FPS 오버레이", "On-Screen FPS Overlay", "画面 FPS オーバーレイ");
            A("g_pfps_b", "미러링 창 좌측 상단에 클릭을 방해하지 않는 투명 FPS 표시기를 띄웁니다. FPS 수치는 로그창을 어지럽히지 않고 오버레이와 배지에만 표시됩니다.", "Displays a click-through transparent FPS counter in the top-left corner of the mirroring window.", "ミラーリング画面の左上にクリック透過のFPS表示を重ねます。ログ欄は汚さずオーバーレイにのみ表示します。");
            A("g_gpkb_t", "게임패드 모드 키보드 보조 매핑", "Gamepad Mode Keyboard Assist", "ゲームパッド時キーボード補助");
            A("g_gpkb_b", "게임패드 모드에서도 키보드 액션 매핑(예: 모험단 춤 방향키)을 함께 사용할 수 있습니다. 마우스 시점과 클릭은 패드/일반 조작을 방해하지 않도록 제외됩니다.", "Allows keyboard action mappings (e.g. arrow keys) alongside a physical gamepad. Mouse look/clicks stay disabled to avoid interference.", "ゲームパッドモードでもキーボードのアクション割当（方向キー等）を併用できます。マウス操作は干渉防止のため除外されます。");
            A("g_shot_t", "화면 캡처 (.png)", "Capture Screenshot (.png)", "画面キャプチャ (.png)");
            A("g_shot_b", "연결된 기기의 현재 화면(가상 디스플레이 실행 중이면 해당 화면 포함)을 PNG 이미지로 즉시 저장합니다.", "Saves the current mirrored screen (including Virtual Display) as a PNG image and copies it to clipboard.", "現在のミラーリング画面（仮想ディスプレイ含む）をPNG保存し、クリップボードにも自動コピーします。");
            A("g_rec_t", "화면 녹화 시작 / 중지 (.mp4)", "Start / Stop Recording (.mp4)", "画面録画 開始 / 停止 (.mp4)");
            A("g_rec_b", "클릭하면 미러링을 끊지 않고 즉시 백그라운드 녹화를 시작합니다. 다시 눌러 녹화를 중지하면 지정 폴더에 MP4 파일로 저장됩니다.", "Starts background MP4 recording without interrupting mirroring. Click again to stop and save.", "ミラーリングを止めずにバックグラウンド録画を開始します。もう一度押すと指定フォルダにMP4保存されます。");
            A("g_vd_t", "가상 디스플레이 (--new-display)", "Virtual Display (--new-display)", "仮想ディスプレイ (--new-display)");
            A("g_vd_b", "폴더블·태블릿 등 화면 비율이 다른 기기에서도 PC 모니터에 꽉 차는 16:9(1920x1080 등) 가상 화면을 별도로 띄웁니다.", "Creates a separate 16:9 (e.g. 1920x1080) virtual screen that fills your PC monitor even on foldables or tablets.", "折りたたみ端末やタブレットでもPCモニターいっぱいの16:9仮想画面を別枠で起動します。");
            A("g_vds_t", "가상 디스플레이 해상도", "Virtual Display Resolution", "仮想ディスプレイ解像度");
            A("g_vds_b", "1920x1080이 가장 부드럽고, 2560x1440은 선명하지만 기기 GPU 부하가 큽니다. 뒤에 /320 처럼 DPI를 붙일 수 있습니다.", "1920x1080 is smoothest; 2560x1440 is sharper but uses more device GPU. Append /DPI (e.g. /280) to adjust UI scale.", "1920x1080が最も滑らか、2560x1440は高精細ですが端末GPU負荷が増えます。末尾に /280 のようにDPIを指定可能です。");
            A("g_nodecor_t", "DeX / 시스템 UI 건너뛰기", "Skip DeX / System UI", "DeX / システムUIスキップ");
            A("g_nodecor_b", "데스크톱 UI(예: 삼성 DeX) 대신 선택한 앱만 16:9 화면으로 단독 실행합니다. 반드시 아래에서 '실행 앱'을 선택해야 검은 화면이 안 뜹니다.", "Launches only the selected app in 16:9 instead of desktop UI (e.g. Samsung DeX). Make sure to select a Launch App below.", "デスクトップUI（Samsung DeX等）を使わず選択アプリのみを16:9で単体起動します。必ず下の「起動アプリ」を選んでください。");
            A("g_apps_t", "앱 바로 실행 (--start-app)", "Auto-Launch App (--start-app)", "アプリ自動起動 (--start-app)");
            A("g_apps_b", "미러링 시작과 동시에 선택한 앱을 바로 켭니다. 기기가 연결되면 설치된 앱 목록을 자동으로 불러옵니다.", "Automatically launches the selected app when mirroring starts. Installed apps load automatically once a device connects.", "ミラーリング開始と同時に選択したアプリを起動します。デバイス接続後に一覧を自動読み込みします。");
            A("g_appsearch_t", "앱 검색", "Search Apps", "アプリ検索");
            A("g_appsearch_b", "앱 이름이나 패키지명의 일부를 입력하면 오른쪽 목록이 즉시 필터링됩니다. (예: 게임 이름, 패키지명)", "Type part of an app name or package name to filter the dropdown list on the right immediately.", "アプリ名やパッケージ名の一部を入力すると右側のリストを即座に絞り込みます。");
            A("g_kmp_t", "LDPlayer .kmp 불러오기", "Import LDPlayer .kmp", "LDPlayer .kmp 読み込み");
            A("g_kmp_b", "LD플레이어에서 사용하던 .kmp 키매핑 파일을 그대로 불러와 적용합니다.", "Imports an existing LDPlayer .kmp keymap file directly into HuntLink.", "LDPlayer で使っていた .kmp キーマッピングファイルをそのまま読み込んで適用します。");
            A("g_kmstart_t", "키 매핑 시작", "Start Key Mapping", "キーマッピング開始");
            A("g_kmstart_b", "scrcpy-server 멀티터치 제어 소켓을 연결해 WASD 이동, 마우스 시점 회전, 스킬 키를 1~3ms 저지연으로 입력합니다. 먼저 미러링을 시작해 주세요.", "Connects the low-latency scrcpy-server multi-touch control socket for WASD, mouse look, and skill keys. Start mirroring first.", "scrcpy-server マルチタッチ制御ソケットに接続し、WASD移動・視点操作・スキルキーを低遅延入力します。先にミラーリングを開始してください。");
            A("g_kmedit_t", "화면에서 키 배치", "Edit Key Layout on Screen", "画面上でキー配置を編集");
            A("g_kmedit_b", "실행 중인 미러링 화면 위에 반투명 편집 오버레이를 띄워 드래그 앤 드롭으로 키 위치를 직접 맞춥니다.", "Opens a translucent edit overlay over the running game window so you can drag and drop key positions directly.", "実行中のミラーリング画面上に半透明の編集オーバーレイを表示し、ドラッグ＆ドロップでキー位置を調整します。");
            A("g_expcode_t", "프리셋 코드 복사", "Copy Preset Share Code", "プリセットコードをコピー");
            A("g_expcode_b", "현재 프로파일을 초압축 한 줄 공유 코드(SLKM2:...)로 클립보드에 복사합니다. 댓글이나 커뮤니티에 바로 공유할 수 있습니다.", "Copies the current profile as a compact one-line share code (SLKM2:...) to your clipboard for easy sharing.", "現在のプロファイルを1行の共有コード (SLKM2:...) としてクリップボードにコピーします。");
            A("g_impcode_t", "프리셋 코드 불러오기", "Import Preset Share Code", "プリセットコードを読み込み");
            A("g_impcode_b", "다른 사람이 공유한 프리셋 코드(SLKM2:... / SLKM1:...)를 붙여넣어 내 프로파일 목록에 즉시 추가합니다.", "Paste a shared preset code (SLKM2:... / SLKM1:...) to add it straight to your profiles.", "他のユーザーが共有したコード (SLKM2:... / SLKM1:...) を貼り付けてプロファイルに追加します。");
            A("g_p1080_t", "추천 1080p 프리셋", "Recommended 1080p Preset", "おすすめ 1080p プリセット");
            A("g_p1080_b", "유선 폰·폴더블 추천: H.264 · 20Mbps · 가상 디스플레이 1920x1080 · DeX 건너뛰기 · 저지연 OFF (가장 안정적)", "Best for wired phones & foldables: H.264 · 20Mbps · Virtual Display 1920x1080 · Skip DeX · Low-Latency OFF (most stable).", "有線スマホ・折りたたみ端末向け: H.264・20Mbps・仮想1920x1080・DeXスキップ・低遅延OFF（最安定）。");
            A("g_p1440_t", "고화질 1440p 프리셋", "High-Res 1440p Preset", "高画質 1440p プリセット");
            A("g_p1440_b", "최신 플래그십 + QHD/4K 모니터 추천: H.265 · 20Mbps · 가상 디스플레이 2560x1440", "Best for flagship devices + QHD/4K monitors: H.265 · 20Mbps · Virtual Display 2560x1440.", "最新ハイエンド端末＋QHD/4Kモニター向け: H.265・20Mbps・仮想2560x1440。");
            A("g_pnat_t", "기기 본체 화면 프리셋", "Native Device Screen Preset", "端末本体画面プリセット");
            A("g_pnat_b", "태블릿(갤탭·Y700·샤오신) 추천: 가상 디스플레이 OFF로 PC모드 없이 본체 화면 그대로 미러링 + Alt+O 완벽 지원", "Best for tablets (Galaxy Tab, Y700, Xiaoxin): Virtual Display OFF for native mirroring without PC/DeX switch + full Alt+O support.", "タブレット(Galaxy Tab/Y700等)向け: 仮想ディスプレイOFFでPCモード誤切替を防止＋Alt+O完全対応。");
            A("g_pwifi_t", "무선 안정 프리셋", "Wi-Fi Smooth Preset", "無線安定プリセット");
            A("g_pwifi_b", "Wi-Fi 무선 연결 추천: H.265 · 12Mbps · 오디오 안정화(120ms) ON으로 끊김과 소리 째짐 최소화", "Best for Wi-Fi play: H.265 · 12Mbps · Smooth Audio (120ms) ON to minimize stutter and audio crackling.", "Wi-Fi無線プレイ向け: H.265・12Mbps・音割れ軽減(120ms) ONでカクつきと音切れを最小化。");
            A("g_qbatch_t", "왼쪽 목록 일괄 스캔 (F7)", "Batch Scan Left Quest List (F7)", "左クエスト一覧の一括スキャン (F7)");
            A("g_qbatch_b", "게임 퀘스트 창에서 F7을 누르면 화면에 보이는 왼쪽 퀘스트 목록(최대 7개)을 한 번에 수집합니다. 스크롤 후 한 번 더 누르면 겹친 항목을 자동 제외하고 나머지만 추가합니다.", "Press F7 in the quest screen to scan up to 7 visible quests at once. Scroll down and press F7 again to auto-deduplicate and append the rest.", "クエスト画面でF7を押すと左一覧の表示中クエスト（最大7件）を一括収集します。スクロール後にもう一度押すと重複を自動除外して残りを追加します。");
            A("g_extra_t", "순정 scrcpy 추가 옵션", "Extra scrcpy CLI Options", "純正 scrcpy 追加オプション");
            A("g_extra_b", "원하는 scrcpy 명령어 옵션을 자유롭게 덧붙일 수 있습니다. (예: --window-borderless --shortcut-mod=lctrl)", "Append any raw scrcpy CLI flags you want (e.g. --window-borderless --shortcut-mod=lctrl).", "任意の scrcpy コマンドライン引数を自由に追加できます（例: --window-borderless --shortcut-mod=lctrl）。");
            A("g_newwheel_t", "휠 드래그 키 (부위 조준 · 아이템/탄환 휠)", "Wheel Drag Key (Target / Item / Ammo Wheel)", "ホイール選択キー (部位照準・アイテム/弾ホイール)");
            A("g_newwheel_b", "체크 후 추가하면 키를 누른 채 마우스를 움직여 방향을 선택하고, 떼면 확정되는 휠 선택 키로 작동합니다. (예: Tab 부위 조준, G 아이템/탄환 휠)", "When checked, holding the key lets you move the mouse to pick a radial direction and releasing confirms it (e.g. Tab target wheel, G item/ammo wheel).", "チェックして追加すると、キーを押したままマウスで方向を選び、離すと確定するホイール選択キーになります。");
            A("g_rclick_t", "우클릭 보조", "Right-Click Action", "右クリック補助");
            A("g_rclick_b", "시점 고정 ON/OFF 모두에서 마우스 우클릭을 지정된 버튼(특수 공격·회피·탄 선택 등)으로 사용합니다. 일반 모드에서 좌클릭은 화면 터치로 그대로 유지됩니다.", "Maps right mouse button to a chosen game button (special attack, dodge, ammo) whether camera lock is ON or OFF.", "視点ロックのON/OFFに関わらず、右クリックを指定ボタン（特殊攻撃・回避・弾選択など）に割り当てます。");
            A("g_rwheel_t", "우클릭 휠 드래그 (헤보 탄 선택 등)", "Right-Click Wheel Drag", "右クリック ホイール選択");
            A("g_rwheel_b", "체크하면 마우스 우클릭을 꾹 누른 채 마우스를 움직여 방향을 선택하고, 떼면 확정되는 휠 선택 키로 작동합니다.", "When checked, holding right-click lets you select a radial wheel direction with the mouse and releasing confirms it.", "チェックすると右クリックを長押ししながらマウスで方向を選び、離すと確定するホイール選択として動作します。");
            A("g_newkey_t", "매핑 키 입력 (조합키 지원)", "Bind Key Input (Combos Supported)", "割当キー入力 (組み合わせキー対応)");
            A("g_newkey_b", "이 칸을 클릭한 뒤 원하는 키(예: Q, Ctrl+1, Shift+E, Alt+2) 또는 마우스 4·5·휠클릭 버튼을 누르면 등록할 수 있습니다.", "Click this box and press any key (e.g. Q, Ctrl+1, Shift+E, Alt+2) or Mouse 4 / 5 / Middle button to bind.", "この欄をクリックして任意のキー（例: Q, Ctrl+1, Shift+E）やマウス4/5/中クリックを押すと登録できます。");

            A("codex_title", "몬스터 도감 · 서식지 빠른 검색 (39종 · 4개 위키 교차 검증 DB)", "Monster Codex · Habitat Lookup (39 Monsters · 4-Wiki Verified)", "モンスター図鑑 · 生息地検索 (39種 · 4ウィキ検証済)");
            A("codex_search_hint", "몬스터 이름 검색 (예: 안쟈나프, 디아블로스, 융광종...)", "Search monster name or species...", "モンスター名・種族を検索...");
            A("codex_all_regions", "전체 지역", "All Regions", "全エリア");
            A("codex_all_species", "전체 종족", "All Species", "全種族");
            A("codex_radiant_only", "융광종만", "Radiant Only", "融光種のみ");

            A("lbl_extra_args", "scrcpy 추가 옵션", "Extra scrcpy Args", "scrcpy 追加引数");
            A("hint_extra_args", "순정 scrcpy 옵션 직접 추가 (예: --window-borderless --shortcut-mod=lctrl)", "Custom scrcpy flags (e.g. --window-borderless --shortcut-mod=lctrl)", "カスタム scrcpy 引数 (例: --window-borderless)");
            A("lbl_adv_sec3", "수동 무선 연결 · 저장 폴더 · 도움말", "Wireless Pairing · Storage · Help", "手動無線接続 · 保存先 · ヘルプ");

            // Section 1: Device
            A("sec1_title", "기기 연결 (USB 자동 감지 · 무선 전환 · ADB 관리)", "Device Connection (USB Auto · Wireless · ADB)", "デバイス接続 (USB自動検出 · 無線切替 · ADB)");
            A("btn_refresh", "새로고침", "Refresh", "更新");
            A("btn_usb_wifi", "유선 → 무선", "USB → Wi-Fi", "USB → 無線");
            A("btn_wifi_disc", "무선 해제", "Disconnect", "無線切断");
            A("btn_adb_restart", "ADB 재시작", "Restart ADB", "ADB 再起動");
            A("exp_wireless", "수동 무선 디버깅 페어링 / IP 직접 연결 (케이블 없이 연결할 때)", "Manual Wireless Debugging Pairing / Direct IP (No Cable)", "手動ワイヤレスデバッグ ペアリング / IP直接接続 (ケーブル不要)");
            A("lbl_pair_addr", "① 페어링 IP:포트", "① Pairing IP:Port", "① ペアリング IP:ポート");
            A("lbl_pair_code", "6자리 코드", "6-digit Code", "6桁コード");
            A("btn_pair", "페어링", "Pair", "ペアリング");
            A("lbl_wifi_addr", "② 무선 디버깅 IP:포트", "② Wireless IP:Port", "② ワイヤレス IP:ポート");
            A("btn_wifi_conn", "무선 연결", "Connect", "無線接続");

            // Section 2: Video
            A("sec2_title", "영상 · 성능 · 저장 설정", "Video · Performance · Recording", "映像・パフォーマンス・録画設定");
            A("lbl_codec", "코덱", "Codec", "コーデック");
            A("lbl_maxsize", "최대크기", "Max Size", "最大サイズ");
            A("lbl_renderer", "렌더러", "Renderer", "レンダラ");
            A("chk_screen_off", "폰 화면 끄기 (-S)", "Turn Screen Off (-S)", "端末画面オフ (-S)");
            A("chk_stay_awake", "절전 방지 (-w)", "Stay Awake (-w)", "スリープ防止 (-w)");
            A("chk_low_latency", "저지연 인코딩", "Low Latency", "低遅延エンコード");
            A("chk_no_audio", "소리 끄기", "Mute Audio", "音声オフ");
            A("chk_audio_safe", "오디오 안정화", "Smooth Audio", "音割れ軽減");
            A("chk_fullscreen", "전체화면 시작 (-f)", "Start Fullscreen (-f)", "フルスクリーン起動 (-f)");
            A("chk_always_on_top", "항상 위에 (실시간)", "Always on Top", "常に手前に表示");
            A("chk_remember_win", "창 위치·크기 기억", "Remember Window", "ウィンドウ位置記憶");
            A("chk_gamepad_kb_assist", "게임패드 모드에서도 키보드 보조 매핑 사용 (실시간 적용 · WASD/마우스 제외)", "Enable keyboard assist mapping in Gamepad mode (live · no WASD/mouse)", "ゲームパッドモードでもキーボード補助マッピングを使用 (即時反映・WASD/マウス除外)");
            A("chk_fps_overlay", "화면 FPS 오버레이", "FPS Overlay", "FPS オーバーレイ");
            A("lbl_rec_folder", "캡처 · 녹화 저장 폴더", "Capture / Record Folder", "スクショ・録画保存先");
            A("btn_browse", "폴더 선택", "Browse", "選択");
            A("btn_open_folder", "폴더 열기", "Open", "開く");

            // Section 3: Virtual Display
            A("sec3_title", "가상 디스플레이 · 앱 바로 실행 (16:9 / 21:9 최적화)", "Virtual Display · Auto-Launch App (16:9 / 21:9)", "仮想ディスプレイ・アプリ自動起動 (16:9 / 21:9)");
            A("chk_vd", "가상 디스플레이 사용", "Use Virtual Display", "仮想ディスプレイ使用");
            A("chk_no_decor", "DeX / 시스템 UI 건너뛰기 (앱 단독 실행)", "Skip DeX / System UI (App Only)", "DeX / システムUI無効 (アプリ単体起動)");
            A("lbl_app", "실행 앱", "Launch App", "起動アプリ");
            A("hint_app_search", "앱 이름 · 패키지 검색...", "Search app / package...", "アプリ名・パッケージ検索...");
            A("btn_load_apps", "앱 불러오기", "Load Apps", "アプリ読込");
            A("app_none", "(선택 안 함 — 현재 화면 그대로)", "(None — Current Screen)", "(指定なし — 現在の画面)");

            // Keymap Tab
            A("km_warn", "현재 키보드+마우스 모드가 아닙니다. 키 매핑 기능은 상단에서 [키보드 + 마우스] 모드를 선택했을 때 작동합니다. (게임패드 버튼 리매핑은 미지원)", "Not in Keyboard+Mouse mode. Select [Keyboard + Mouse] above to use key mapping. (Gamepad button remapping is not supported.)", "現在キーボード＋マウスモードではありません。上部で [キーボード＋マウス] を選択すると有効になります。(ゲームパッドのボタン再割当は非対応)");
            A("km_sec1", "키 매핑 프로파일 (무기별 관리 · LD플레이어 .kmp 호환)", "Keymap Profiles (Per-Weapon · LDPlayer .kmp Compatible)", "キーマッピング プロファイル (武器別管理 · LDPlayer .kmp対応)");
            A("btn_import_kmp", "LD .kmp 불러오기", "Import LD .kmp", "LD .kmp 読込");
            A("btn_save", "저장", "Save", "保存");
            A("btn_save_as", "다른 이름으로 저장", "Save As", "別名保存");
            A("btn_rename", "이름 변경", "Rename", "名前変更");
            A("btn_delete", "프로파일 삭제", "Delete", "削除");
            A("btn_copy_code", "코드 복사", "Copy Code", "コードコピー");
            A("btn_import_code", "코드 불러오기", "Import Code", "コード読込");
            A("lbl_hotkey_hint", "게임 중 Ctrl+F1~F9 전환", "In-game: Ctrl+F1~F9", "ゲーム中 Ctrl+F1~F9 切替");

            A("km_sec2", "멀티터치 조작 · 시점 회전 설정", "Multi-Touch Controls · Camera Look", "マルチタッチ操作・カメラ設定");
            A("chk_km_joy", "WASD + Shift 질주", "WASD + Shift Sprint", "WASD + Shift ダッシュ");
            A("chk_km_cam", "마우스 시점 회전", "Mouse Camera Look", "マウス視点操作");
            A("chk_km_left", "좌클릭 공격", "Left-Click Action", "左クリック攻撃");
            A("chk_km_right", "우클릭 보조", "Right-Click Action", "右クリック補助");
            A("chk_km_right_wheel", "우클릭 휠드래그", "Right-Click Wheel", "右クリックホイール");
            A("chk_km_hints", "화면 키 힌트", "On-Screen Hints", "画面キーヒント");
            A("lbl_cam_toggle", "시점 토글", "Look Toggle", "視点切替");
            A("lbl_sens_x", "감도 X", "Sens X", "感度 X");
            A("lbl_sens_y", "감도 Y", "Sens Y", "感度 Y");
            A("lbl_hint_op", "힌트 투명도", "Hint Opacity", "ヒント透明度");
            A("lbl_hint_sc", "힌트 크기", "Hint Scale", "ヒントサイズ");
            A("btn_edit_overlay", "화면에서 키 배치", "Edit Layout on Screen", "画面上でキー配置");
            A("btn_km_reconnect", "수동 재연결", "Reconnect", "手動再接続");
            A("btn_km_pause", "매핑 일시중지", "Pause Mapping", "マッピング停止");

            A("km_sec3", "매핑된 액션 키 목록 (누름/홀드 · 조합키 · 휠드래그 지원)", "Mapped Action Keys (Tap/Hold · Combos · Wheel Drag)", "アクションキー一覧 (長押し・組合せキー・ホイール対応)");
            A("hint_new_key", "클릭 후 키 누름", "Click & Press Key", "クリック後キー入力");
            A("chk_new_wheel", "휠 드래그", "Wheel Drag", "ホイール選択");
            A("btn_add_key", "+ 키 추가", "+ Add Key", "+ キー追加");
            A("btn_del_key", "선택 삭제", "Remove", "選択削除");

            // Bottom bar & Guide
            A("guide_title", "옵션 가이드", "Option Guide", "オプションガイド");
            A("guide_body", "각 옵션 위에 마우스를 올리면 언제 켜고 끄면 좋은지 설명이 표시됩니다.", "Hover over any option to see when to enable or disable it.", "各オプションにカーソルを合わせると詳しい説明が表示されます。");
            A("btn_start_km", "▶  키마 미러링 시작", "▶  Start Keymap Mirroring", "▶  キーマッピング開始");
            A("btn_start_gp", "▶  패드 미러링 시작", "▶  Start Gamepad Mirroring", "▶  ゲームパッド開始");
            A("btn_start_nm", "▶  일반 미러링 시작", "▶  Start Mirroring", "▶  ミラーリング開始");
            A("btn_stop", "■  중지", "■  Stop", "■  停止");
            A("btn_screenshot", "화면 캡처", "Screenshot", "スクショ");
            A("btn_rec_start", "● 녹화 시작", "● Record", "● 録画開始");
            A("btn_rec_stop", "■ 녹화 중지 ({0})", "■ Stop Rec ({0})", "■ 録画停止 ({0})");
            A("lbl_log", "실시간 로그", "Live Log", "リアルタイムログ");
            A("btn_copy_cmd", "명령어 복사", "Copy Cmd", "コマンドコピー");
            A("btn_clear_log", "지우기", "Clear", "クリア");

            // Overlay
            A("ov_edit_bar", "키 배치 편집 모드  |  아이콘을 드래그해서 게임 버튼 위로 옮기세요", "Layout Edit Mode  |  Drag icons onto game buttons", "キー配置編集モード  |  アイコンをゲームボタン上にドラッグしてください");
            A("ov_done", "배치 완료", "Done", "配置完了");
            A("ov_cam", "시점", "Look", "視点");
            A("ov_lclick", "좌클릭", "LMB", "左クリック");
            A("ov_rclick", "우클릭", "RMB", "右クリック");
            A("ov_wheel", "휠", "Wheel", "ホイール");
            A("ov_sprint", " 질주", " Sprint", " ダッシュ");

            // Key names
            A("key_mbtn", "휠클릭", "Middle", "中クリック");
            A("key_m4", "마우스4", "Mouse4", "マウス4");
            A("key_m5", "마우스5", "Mouse5", "マウス5");

            // Toast & Dialogs
            A("toast_chat_on", "채팅 모드 ON (한글/영문 입력 · F2로 복귀)", "Chat Mode ON (Typing enabled · F2 to resume)", "チャットモード ON (文字入力可 · F2で復帰)");
            A("toast_chat_off", "키 매핑 재개 (F2)", "Key Mapping Resumed (F2)", "キーマッピング再開 (F2)");
            A("toast_profile", "프로파일 전환 (Ctrl+F{0}): {1}", "Profile (Ctrl+F{0}): {1}", "プロファイル切替 (Ctrl+F{0}): {1}");

            A("upd_title", "Scrcpy Launcher 자동 업데이트", "Scrcpy Launcher Auto-Update", "Scrcpy Launcher 自動アップデート");
            A("upd_prompt",
                "새 버전 ({0})으로 원클릭 자동 업데이트를 진행할까요?\n\n• [예] : 지금 바로 다운로드 후 자동 교체 및 재실행\n• [아니오] : GitHub 릴리즈 웹페이지 열기\n• [취소] : 닫기",
                "Update to new version ({0}) automatically?\n\n• [Yes]: Download, replace & restart now\n• [No]: Open GitHub Releases page\n• [Cancel]: Close",
                "新バージョン ({0}) へ自動アップデートしますか？\n\n• [はい]: ダウンロードして自動置換・再起動\n• [いいえ]: GitHub リリースページを開く\n• [キャンセル]: 閉じる");

            A("dlg_guide_title", "연결 방법 및 문제 해결 가이드", "Setup & Troubleshooting Guide", "接続方法・トラブルシューティング");
            A("dlg_guide_body",
                "[ 스마트폰 최초 연결 방법 (1회만 설정) ]\n\n" +
                "1단계. 개발자 옵션 켜기\n" +
                "  • 설정 → 휴대전화 정보 → 소프트웨어 정보 → '빌드번호' 7번 연속 터치\n\n" +
                "2단계. USB 디버깅 켜기 (유선 연결 · 추천)\n" +
                "  • 설정 → '개발자 옵션' → 'USB 디버깅' 켜기\n" +
                "  • USB 연결 후 폰 화면의 'USB 디버깅 허용' 팝업에서 허용\n\n" +
                "3단계. 무선(Wi-Fi) 연결\n" +
                "  • USB 연결 상태에서 런처의 [유선 → 무선] 버튼 클릭 후 케이블 분리\n\n" +
                "[ 자주 발생하는 문제 해결 ]\n" +
                "• 가상 디스플레이 실행 시 게임이 바로 튕길 때\n" +
                "  → 개발자 옵션 맨 아래 '활동 크기 조정 가능(활동을 크기 조절 가능하게 강제 설정)' 켜고 재부팅\n" +
                "• 소리는 나는데 검은 화면만 나올 때\n" +
                "  → '저지연 인코딩' 해제, 코덱을 h264로 변경, 렌더러를 OpenGL로 변경하거나 '폰 화면 끄기' 해제\n" +
                "• 하단 뒤로가기/홈 바 때문에 아래쪽 터치가 안 될 때\n" +
                "  → 'DeX / 시스템 UI 건너뛰기' 체크 확인, 또는 '전체화면 시작'·'폰 화면 끄기' 해제 / 폰 제스처 네비게이션 사용\n" +
                "• 입력·화면 지연(딜레이)이 심할 때\n" +
                "  → USB 유선 연결 + h264 + 10~16Mbps 권장, 일부 기종은 '폰 화면 끄기(-S)' 해제 시 절전 스로틀링 해소",

                "[ Initial Android Setup (One-time) ]\n\n" +
                "Step 1. Enable Developer Options\n" +
                "  • Settings → About Phone → Software Info → Tap 'Build Number' 7 times\n\n" +
                "Step 2. Enable USB Debugging (Wired · Recommended)\n" +
                "  • Settings → Developer Options → Enable 'USB Debugging'\n" +
                "  • Connect via data USB cable and tap Allow on the phone popup\n\n" +
                "Step 3. Wireless (Wi-Fi) Mode\n" +
                "  • While connected via USB, click [USB → Wi-Fi] then unplug the cable\n\n" +
                "[ Troubleshooting ]\n" +
                "• Game crashes immediately on Virtual Display\n" +
                "  → Enable 'Force activities to be resizable' in Developer Options and reboot phone\n" +
                "• Audio plays but screen stays black\n" +
                "  → Uncheck 'Low Latency', set Codec to h264, try Renderer: OpenGL, or uncheck 'Turn Screen Off'\n" +
                "• Bottom Android nav bar blocks bottom touches\n" +
                "  → Ensure 'Skip DeX / System UI' is checked, or switch phone nav bar to Gesture mode / uncheck Fullscreen + Screen Off\n" +
                "• High latency / lag\n" +
                "  → Use wired USB + h264 + 10~16 Mbps; on some devices unchecking 'Turn Screen Off (-S)' prevents thermal/sleep throttling",

                "[ Android 初期設定 (初回のみ) ]\n\n" +
                "手順1. 開発者向けオプションを有効化\n" +
                "  • 設定 → 端末情報 → ソフトウェア情報 →「ビルド番号」を7回連続タップ\n\n" +
                "手順2. USBデバッグを有効化 (有線接続・推奨)\n" +
                "  • 設定 → 開発者向けオプション →「USBデバッグ」をオン\n" +
                "  • データ通信対応USBケーブルで接続し、端末の許可ポップアップで「許可」\n\n" +
                "手順3. ワイヤレス (Wi-Fi) 接続\n" +
                "  • USB接続中に [USB → 無線] をクリックするとケーブルを抜いてプレイ可能\n\n" +
                "[ よくあるトラブルと対処法 ]\n" +
                "• 仮想ディスプレイ起動時にゲームが落ちる\n" +
                "  → 開発者向けオプションの「アクティビティをサイズ変更可能にする」をオンにして端末を再起動\n" +
                "• 音は鳴るのに画面が真っ暗になる\n" +
                "  →「低遅延エンコード」をオフ、コーデックを h264、レンダラを OpenGL に変更、または「端末画面オフ」をオフ\n" +
                "• 下部のナビゲーションバーがタッチを邪魔する\n" +
                "  →「DeX / システムUI無効」を確認、または端末側をジェスチャーナビゲーションに変更\n" +
                "• 遅延(ラグ)が大きい\n" +
                "  → 有線USB + h264 + 10~16Mbps 推奨。一部端末は「端末画面オフ(-S)」を外すと改善します");

            A("dlg_short_title", "단축키 안내", "Shortcuts Guide", "ショートカット一覧");
            A("dlg_short_body",
                "[ 실행 중 주요 단축키 ]\n\n" +
                "• Alt  :  시점 회전 모드 ON / OFF (커서 잠금/해제)\n" +
                "• F1  :  키 매핑 화면 힌트 켜기 / 끄기\n" +
                "• F2  :  미니 한글 채팅창 열기 (한글 입력 후 Enter로 즉시 전송 · 전 모드 지원)\n" +
                "• F7  :  퀘스트 왼쪽 목록 일괄 스캔 (최대 7개 한 방 수집)\n" +
                "• F8  :  현재 선택한 퀘스트 1개 수집 및 자동 학습\n" +
                "• F12 / Xbox Share  :  즉시 화면 캡처 (.png)\n" +
                "• Ctrl + F1 ~ F9  :  무기별 프로파일 즉시 전환\n" +
                "• Alt + F  :  전체화면 전환\n" +
                "• Alt + O  /  Alt + Shift + O  :  폰 화면 끄기 / 켜기\n" +
                "• Alt + I  :  실시간 FPS 표시 켜기 / 끄기\n" +
                "• Alt + H / B / S  :  홈 / 뒤로 가기 / 최근 앱\n" +
                "• Alt + R  :  화면 회전  |  Alt + ↑ / ↓  :  볼륨 조절",

                "[ Active Shortcuts ]\n\n" +
                "• Alt  :  Toggle Mouse Camera Look ON / OFF\n" +
                "• F1  :  Toggle On-Screen Key Hints\n" +
                "• F2  :  Open Mini Chat Bar (Type & Enter to send · Works in all modes)\n" +
                "• F7  :  Batch Scan Left Quest List (Up to 7 quests at once)\n" +
                "• F8  :  Scan Selected Quest & Auto-Learn (Quest Planner)\n" +
                "• F12 / Xbox Share  :  Instant Screenshot (.png)\n" +
                "• Ctrl + F1 ~ F9  :  Instant Profile Switch (Per-Weapon)\n" +
                "• Alt + F  :  Toggle Fullscreen\n" +
                "• Alt + O  /  Alt + Shift + O  :  Turn Phone Screen Off / On\n" +
                "• Alt + I  :  Toggle FPS Counter\n" +
                "• Alt + H / B / S  :  Home / Back / Recent Apps\n" +
                "• Alt + R  :  Rotate Screen  |  Alt + ↑ / ↓  :  Volume",

                "[ 実行中の主なショートカット ]\n\n" +
                "• Alt  :  マウス視点操作モード ON / OFF\n" +
                "• F1  :  画面キーヒント 表示 / 非表示\n" +
                "• F2  :  ミニチャット欄を開く (入力してEnterで送信・全モード対応)\n" +
                "• F7  :  クエスト左リスト一括スキャン (最大7件まとめて収集)\n" +
                "• F8  :  選択中クエストを1件収集・自動学習 (クエスト整理)\n" +
                "• F12 / Xbox Share  :  即時スクリーンショット (.png)\n" +
                "• Ctrl + F1 ~ F9  :  武器別プロファイル即時切替\n" +
                "• Alt + F  :  フルスクリーン切替\n" +
                "• Alt + O  /  Alt + Shift + O  :  端末画面オフ / オン\n" +
                "• Alt + I  :  FPS 表示 ON / OFF\n" +
                "• Alt + H / B / S  :  ホーム / 戻る / アプリ履歴\n" +
                "• Alt + R  :  画面回転  |  Alt + ↑ / ↓  :  音量調整");
        }
    }
}
