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
            A("win_title", "Scrcpy Launcher v1.4 — Android 미러링", "Scrcpy Launcher v1.4 — Android Mirroring", "Scrcpy Launcher v1.4 — Android ミラーリング");
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

            // Tabs
            A("tab_conn", "연결 및 영상", "Connection & Video", "接続・映像");
            A("tab_keymap", "키 매핑", "Key Mapping", "キーマッピング");

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
            A("chk_fullscreen", "전체화면 시작 (-f)", "Start Fullscreen (-f)", "フルスクリーン起動 (-f)");
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
                "• F2  :  채팅 모드 토글 (키 매핑 일시정지 · 한글 타자 가능)\n" +
                "• Ctrl + F1 ~ F9  :  무기별 프로파일 즉시 전환\n" +
                "• Alt + F  :  전체화면 전환\n" +
                "• Alt + O  /  Alt + Shift + O  :  폰 화면 끄기 / 켜기\n" +
                "• Alt + I  :  실시간 FPS 표시 켜기 / 끄기\n" +
                "• Alt + H / B / S  :  홈 / 뒤로 가기 / 최근 앱\n" +
                "• Alt + R  :  화면 회전  |  Alt + ↑ / ↓  :  볼륨 조절",

                "[ Active Shortcuts ]\n\n" +
                "• Alt  :  Toggle Mouse Camera Look ON / OFF\n" +
                "• F1  :  Toggle On-Screen Key Hints\n" +
                "• F2  :  Toggle Chat / Typing Mode (Pause / Resume Keymap)\n" +
                "• Ctrl + F1 ~ F9  :  Instant Profile Switch (Per-Weapon)\n" +
                "• Alt + F  :  Toggle Fullscreen\n" +
                "• Alt + O  /  Alt + Shift + O  :  Turn Phone Screen Off / On\n" +
                "• Alt + I  :  Toggle FPS Counter\n" +
                "• Alt + H / B / S  :  Home / Back / Recent Apps\n" +
                "• Alt + R  :  Rotate Screen  |  Alt + ↑ / ↓  :  Volume",

                "[ 実行中の主なショートカット ]\n\n" +
                "• Alt  :  マウス視点操作モード ON / OFF\n" +
                "• F1  :  画面キーヒント 表示 / 非表示\n" +
                "• F2  :  チャットモード切替 (キーマッピング一時停止・文字入力)\n" +
                "• Ctrl + F1 ~ F9  :  武器別プロファイル即時切替\n" +
                "• Alt + F  :  フルスクリーン切替\n" +
                "• Alt + O  /  Alt + Shift + O  :  端末画面オフ / オン\n" +
                "• Alt + I  :  FPS 表示 ON / OFF\n" +
                "• Alt + H / B / S  :  ホーム / 戻る / アプリ履歴\n" +
                "• Alt + R  :  画面回転  |  Alt + ↑ / ↓  :  音量調整");
        }
    }
}
