# Scrcpy Launcher

Windows용 [scrcpy](https://github.com/Genymobile/scrcpy) 유선·무선 Android 미러링 GUI 런처입니다.
복잡한 명령어나 배치 파일 없이 클릭만으로 기기 연결, 가상 디스플레이(16:9), 게임패드, 화면 캡처·녹화, 실시간 투명 FPS 오버레이까지 사용할 수 있습니다.

---

## 주요 기능

- **유선(USB) + 무선(Wi-Fi) 통합 지원**
  - USB 기기 3초 주기 자동 감지
  - **`[유선 → 무선]` 원클릭 전환**: USB 연결 상태에서 폰 Wi-Fi IP를 자동 조회해 무선 ADB로 전환
  - 수동 무선 디버깅 페어링(6자리 코드) / IP 직접 연결 지원
  - **`[무선 해제]`** 및 **`[ADB 재시작]`** 문제 해결 버튼
- **영상 · 게이밍 최적화 옵션**
  - 코덱(`H.264` / `H.265` / `AV1`), 비트레이트(Mbps), 최대 해상도, FPS, PC 렌더러(`Direct3D11` / `OpenGL`)
  - `폰 화면 끄기 (-S)`, `절전 방지 (-w)`, `UHID 게임패드 (-G)`, `저지연 인코딩 (low-latency=1)`, `소리 끄기`, `전체화면 시작 (-f)`
  - 모든 옵션에 마우스를 올리면 **언제 켜고 끄면 좋은지 실시간 가이드** 표시
- **가상 디스플레이 & 앱 바로 실행**
  - 16:9 모니터 비율에 맞춘 `--new-display` (`1920x1080`, `2560x1440` 등)
  - `DeX / 시스템 UI 건너뛰기 (--no-vd-system-decorations)`
  - 기기에 설치된 사용자 앱 목록 자동 불러오기 및 실시간 검색
- **화면 캡처 · 녹화 · 투명 FPS 오버레이**
  - **`[화면 캡처]`**: 미러링 화면(가상 화면 포함) 즉시 `.png` 저장
  - **`[● 녹화 시작 / 중지]`**: 미러링을 끊지 않고 백그라운드에서 `.mp4` 녹화
  - **`화면 FPS 오버레이`**: 미러링 창 좌측 상단에 클릭을 방해하지 않는 반투명 FPS 카운터 표시
- **초보자용 내장 가이드 & 자동 업데이트 알림**
  - 상단 **`[연결 방법 ?]`**(개발자 옵션 / USB 디버깅 설정법) 및 **`[단축키]`** 안내 팝업
  - 마지막 설정 자동 저장 (`scrcpy_launcher_settings.json`)
  - GitHub Releases 새 버전 자동 확인

---

## 설치 및 사용 방법

1. 공식 **scrcpy (Windows 64-bit)**를 다운로드하여 압축을 풉니다.
   - https://github.com/Genymobile/scrcpy/releases
2. [Releases 페이지](https://github.com/TKay76/ScrcpyLauncher/releases)에서 **`ScrcpyLauncher.exe`**를 다운로드합니다.
3. 압축을 푼 `scrcpy` 폴더(`scrcpy.exe`, `adb.exe`가 있는 곳)에 `ScrcpyLauncher.exe`를 넣고 더블클릭하면 바로 실행됩니다.
   - Windows 10 / 11 기본 내장 .NET Framework 4.8 기반으로 별도 런타임 설치가 필요 없습니다.

> **최초 실행 시 Windows 보호 창이 뜰 경우**
> 개인 빌드 실행 파일 특성상 Windows SmartScreen 경고가 나타날 수 있습니다. **`[추가 정보]` → `[실행]`**을 눌러 진행해 주세요.

---

## 스마트폰 최초 설정 (1회만)

1. **개발자 옵션 켜기**: `설정` → `휴대전화 정보` → `소프트웨어 정보` → **빌드번호** 7번 연속 터치
2. **USB 디버깅 켜기**: `설정` → `개발자 옵션` → **USB 디버깅** 활성화
3. USB 케이블 연결 후 폰 화면의 **"USB 디버깅 허용"** 팝업에서 허용

---

## 직접 빌드하기 (Windows)

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```
Windows 기본 내장 `csc.exe`(.NET Framework 4.x)로 별도 SDK 설치 없이 즉시 빌드됩니다.
