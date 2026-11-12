# Windows x64 포터블 배포 검증 기록

## 2026-11-12 — RC.4: 캡처 비교 기능 제거 / Task 03 이전 기준점

개발자 진단의 캡처 비교 하위 탭·버튼·통계/결과 표, 전용 실행기·RAW 변환기·결과 모델과 37개 전용 테스트 및 사용 안내를 제거했습니다. 스크린샷은 하위 탭 없이 기존 미리보기에 표시합니다. 유지할 PNG 진단의 직렬화/취소 검증은 PNG 요청 두 개를 사용하는 공통 테스트 1개로 남겼습니다.

| 로컬 회귀 검사 | 결과 |
|---|---|
| 전체 restore / Release build / 자체 포함 publish | 성공, 경고 0 / 오류 0 |
| 전체 자동 테스트 | **167 통과 / 0 실패 / 0 건너뜀** (203 - 전용 37 + PNG 직렬화 1) |
| 프로젝트별 | Domain 8, Application 26, Features 2, Integration 131 |
| WPF 진단 | CMD 실행, 장치 미선택, 에셋 79개, 하위 탭 없는 미리보기 확인 |
| 게시/이동 ZIP EXE | 동봉 .NET 10.0.15 / X64 / 로컬 OpenCV / PNG 디코딩·합성 ROI 점수 1.0 |
| 이동 경로 GUI | 한글·공백·특수문자 경로에서 CMD→WPF→진단 및 절대 번들 ADB 경로 확인 |
| 실제 별도 도우미 / 실패 / 복원 | 격리 설치·창 없는 재실행, 손상 EXE 실행 실패 후 롤백, 개인 설정/스크린샷 보존 통과 |
| 명세·에셋 / actionlint / workflow 분기 | 통과, 가짜 gh의 Stable/RC 및 실패 차단 12개 통과 |
| 제거 코드 미사용 참조 | src/tests/tools에서 제거한 타입·바인딩·RAW 실행 경로 참조 없음 |

로컬 사전 검증 출력은 `artifacts/portable/20261112-193749-363/`의 TRX, 게시/ZIP 자가 검사, `wpf-ui-check.json`, `zip-wpf-ui-check.json`입니다. 도우미 결과는 `artifacts/update-install/20261112-194014-383/report.json`, 분기 검사는 `artifacts/release-policy/45b97a479a3f2413/`입니다. 사전 ZIP은 커밋 전 소스로 검증한 것이며 공개 배포용 ZIP은 **태그 Actions에서 해당 커밋으로 다시 생성**합니다. 공개 ZIP 검증에는 Release에 첨부된 SHA-256을 사용합니다.

제거 파일: `Diagnostics/CaptureBenchmark.cs`, `CaptureBenchmarkResult.cs`, `RawBenchmarkDecoder.cs`, `Integration.Tests/CaptureBenchmarkTests.cs`, `docs/CAPTURE_BENCHMARK.md`. 진단 서비스/ViewModel/XAML·기존 WPF 테스트·PNG 진단 테스트·배포 GUI 검사 스크립트와 현재 안내/Release 노트를 정리했습니다. 오래된 비교 기능 전용 검증 절은 삭제했습니다.

PNG 캡처·에셋/ROI 인식·ADB 입력/세션 취소, 3초/500ms 정책, 자동 업데이트/롤백, Stable/Pre-release 두 채널과 app/ 구조는 유지합니다. 캡처/입력/비전/게임 구현·임계값·이미지 원본·프로젝트/패키지는 변경하지 않았습니다. 앞서 검증한 app/ 배포 정리는 별도 커밋 `c080294`로 보존했습니다.

원격 태그 RC.1~RC.3과 인증된 Release/draft 목록을 확인해 비어 있는 **`v0.1.0-rc.4`**를 선택했습니다. 이 태그의 제거 완료 커밋을 Task 03 이전 기준점으로 사용하며 기존 태그를 이동하거나 재사용하지 않습니다. GitHub의 실제 실행/게시 성공 여부는 해당 태그의 [Actions](https://github.com/Blooming2081/NyangShinobi_macro/actions)와 [Release 및 ZIP/SHA 첨부](https://github.com/Blooming2081/NyangShinobi_macro/releases/tag/v0.1.0-rc.4)를 별도로 확인합니다. 이 절의 표는 로컬 결과입니다.

Task 03의 임무 자동화는 이 Release에 포함하지 않습니다. 다음 개발은 RC.4 커밋을 기준으로 별도 커밋에서 시작합니다. 실제 MuMu/BlueStacks 성능/정확도, 깨끗한 새 Windows PC와 실제 사용자 폴더의 GUI 업데이트 전 과정은 이번 로컬 검증 범위에 포함하지 않습니다. 평면 구조인 구형 배포 사용자는 새 폴더에 수동 설치해야 합니다.

## 2026-11-12 — 사용자용 app/ 배포 구조

최상위에는 `냥냥시노비 실행.cmd`, `사용 설명서.txt`, `app/`만 배포합니다. CMD는 `%~dp0` 기준으로 `app/NyangShinobi.App.exe`를 실행하며 절대 경로를 저장하는 바로가기, 두 번째 런타임, 관리자 권한 상승을 사용하지 않습니다. 원래 런타임/DLL 배치는 `app/` 안에서 유지하고 에셋·설정·ADB 경로는 앱의 콘텐츠 기준 경로로 구성합니다. 상세 안내는 [포터블 배포 안내](PORTABLE_DISTRIBUTION.md)를 따릅니다.

### 최종 결과

| 항목 | 결과 |
|---|---|
| 전체 restore / Release build / 자체 포함 publish | 성공, 경고 0 / 오류 0 |
| 전체 자동 테스트 | 기존 189 + 신규 14 = **203 통과 / 0 실패 / 0 건너뜀** |
| 프로젝트별 | Domain 8, Application 26, Features 2, Integration 167 |
| 게시 폴더 및 이동 ZIP의 EXE 자가 검사 | 실제 실행 exit 0, .NET 10.0.15 / X64 / 로컬 런타임 / 템플릿 79 / 합성 ROI 점수 1.0 |
| 실제 최상위 CMD → WPF → 개발자 진단 | 게시 폴더와 이동·압축 해제 사본 모두 성공, 로컬 coreclr/OpenCvSharpExtern 로딩, 에셋 79, 장치 자동 선택 없음 |
| 번들 ADB | 진단 UI의 절대 경로가 각 사본의 `app/adb.exe`와 일치, 동봉 파일 해시 및 오프라인 `adb version` 통과 |
| 언어별 리소스 | 13개 폴더 / 156개 파일 보존, 이전 평면 배포와 SHA-256 동일 |
| 실제 도우미 격리 설치 | 새 구조끼리의 설치·창 없는 재실행 프로토콜 확인 통과 |
| 실제 실행 실패 / 롤백 / 개인 파일 | 손상 EXE의 Win32 실행 실패 후 이전 버전 복원, 바깥 폴더 및 app 내부 개인 파일 보존 |
| 기존 구조 자동 전환 | 양방향 모두 백업/파일 교체 전 거부, 수동 설치 안내 단위 테스트 통과 |
| 명세/원본 에셋 / actionlint / 공백 검사 | 통과 |
| 실제 workflow 게시 블록 + 가짜 gh | Stable/RC, draft 재시도, 덮어쓰기/해시/업로드 실패 차단 **12개 통과**, GitHub 요청 없음 |

최종 출력은 다음과 같습니다. 모든 출력·ZIP·검사 사본은 기존처럼 Git에서 제외합니다.

```text
artifacts/portable/20261112-190901-917/
├── NyangShinobi-v0.1.0-dev.0.layout-win-x64/
│   ├── 냥냥시노비 실행.cmd
│   ├── 사용 설명서.txt
│   └── app/  # EXE/ADB/DLL/언어별 폴더/assets/data/docs/licenses
├── NyangShinobi-v0.1.0-dev.0.layout-win-x64.zip
├── NyangShinobi-v0.1.0-dev.0.layout-win-x64.zip.sha256
├── tests/  # 4개 TRX
├── portable-check.json / zip-check.json
├── layout-check.json / zip-layout-check.json
├── wpf-ui-check.json / zip-wpf-ui-check.json
├── language-check.json / native-dependencies.json
└── 다른 경로 & ZIP 이동 (검증)!/압축 해제/...
```

ZIP 크기 **118,947,988 bytes**, SHA-256: `9449F0681CA88788B3202C579D1E10EDEE1E86EFEDB732B9015372745CC86034`.

최종 도우미 보고서는 `artifacts/update-install/20261112-191112-043/report.json`입니다. 원본 배포를 덮어쓰지 않고 `installation/` 격리 복사본에서 테스트 버전 rc.9001→rc.9002 설치, rc.9003 손상 EXE 실패·롤백, 복원 EXE 자가 검사와 개인 파일 보존을 검증했습니다. 작업/백업은 `installation/app/.updates/<GUID>/` 안에 있고 설치 경계는 `installation/`입니다. 한 번 앞선 동일 앱 코드 배포에서도 설치/롤백이 통과했습니다(`20261112-190354-905/report.json`).

이동 실행은 ZIP을 한글·공백·`&`·괄호·`!`를 포함한 다른 경로에 복사한 뒤 압축 해제해 검사했습니다. 같은 앱 코드의 앞선 이동 사본에서 최종 검사 스크립트로 CMD→진단 검사를 **3회 연속 통과**했고(`20261112-190123-308/zip-wpf-final-1/2/3-check.json`), 최신 설명서가 반영된 위 최종 ZIP도 별도로 재검증했습니다. 초기 검사 실패는 시작 시 업데이트 조회가 완료되면서 기존 앱이 업데이트 탭을 선택하는 것과 진단 탭 검사가 경합한 것이었습니다. 실패 UI 트리로 이를 확인했고, 검사 스크립트만 기존 조회 완료 상태와 컨트롤 준비 상태를 제한 시간 내 기다리도록 수정했습니다. 앱의 시작 시 업데이트 동작은 변경하지 않았습니다.

### 업데이트 호환성과 기존 사용자 안내

`app/version.json`, `app/install-manifest.json`은 schema 2입니다. 관리 목록은 바깥 배포 폴더 기준이며 진입점/설명서와 app 내부 관리 파일을 함께 검증·백업·교체·롤백합니다. 다운로드/도우미/백업은 `app/.updates/`에 둡니다. 신규 코드의 준비 단계와 도우미 양쪽에서 schema 1↔2 전환을 차단합니다. 구형 코드도 ZIP 루트의 schema 1 설치 목록이 없는 새 패키지를 승인할 수 없다는 것을 코드 경로로 확인했습니다. **구형 실행본의 실제 오류 UI와 자동 마이그레이션은 실기 검증하지 않았으며 자동 전환을 지원하지 않습니다.**

기존 사용자는 새 ZIP 전체를 **새 폴더**에 풀어 실행합니다. 구형 폴더에 덮어쓰거나 기존 DLL/런타임을 복사하지 않습니다. 기존 `%LocalAppData%/NyangShinobi`의 사용자 설정·채널 설정은 그대로 읽습니다. 구형 배포 폴더에 별도로 저장한 스크린샷/개인 파일은 직접 보관합니다. Stable/Pre-release 두 채널, 기존 게임/캡처/인식 및 3초/500ms 정책은 유지합니다.

### 변경 파일과 미검증 범위

- 배포/검사: `tools/Publish-Portable.ps1`, 신규 `tools/Test-PortableLayout.ps1`, `tools/Test-UpdateInstall.ps1`, `.editorconfig`의 해당 PowerShell UTF-8 BOM 설정.
- 설치 경계: 신규 `Updates/PortableLayout.cs`, `UpdateFiles.cs`, `UpdatePackage.cs`, `UpdateCoordinator.cs`, `UpdateInstaller.cs`, `InstallRootGuard.cs`, `UpdateViewModel.cs`.
- 앱 조립/검증: `App.xaml.cs`의 설치 잠금 경계, `DependencyInjection.cs`의 번들 ADB 절대 경로/명시 경로 보존, `Diagnostics/PortableValidation.cs`의 전체 배포 파일 검증.
- 테스트: 신규 `PortableLayoutTests.cs` 14개와 `UpdateTestData.cs`의 새 구조 fixture. 기존 테스트는 유지했습니다.
- 안내: `.github/workflows/portable.yml`의 실행/수동 전환 노트, README 및 포터블/GitHub/업데이트/검증 문서.

기존 7개 본체·4개 테스트 프로젝트, NuGet 패키지, 이미지 원본, 게임·ADB 명령·캡처/비전 구현은 변경하지 않았습니다. 단일 파일/trimming/무거운 런처/Task 03은 추가하지 않았습니다. ADB 서버·게임·앱플레이어를 실행하거나 종료하지 않았습니다.

**이번 변경의 실제 GitHub Actions 실행/Release 게시/새 포맷 Release에서의 GUI 업데이트 전 과정은 미검증입니다.** 로컬 결과를 CI 성공으로 간주하지 않습니다. 다른 경로 이동은 현재 Windows PC에서 확인했으며 **SDK/Runtime 없는 깨끗한 새 Windows PC**의 실행, 실제 MuMu/BlueStacks 성능·정확도는 별도 검증 항목입니다. 아래는 이전 배포 구조의 이력입니다.

## 2026-11-12 — Stable / Pre-release 최종 배포 정책

UI의 `Development`만 **Pre-release (테스트)**로 표시합니다. 내부 enum과 저장 값 `Development=0`/`Stable=1`은 유지하며 채널은 두 개입니다. 기존 설정 파일을 읽어도 다시 쓰지 않는 호환성 테스트 2개를 추가했습니다. Stable의 정식 전용 필터와 Pre-release의 RC·정식 비교는 기존 구현/테스트를 유지했습니다.

| 검증 | 결과 |
|---|---|
| restore / 전체 Release build / 자체 포함 publish | 성공 |
| 전체 .NET 테스트 | **189 통과 / 0 실패 / 0 건너뜀** (Domain 8, Application 26, Features 2, Integration 153) |
| 실제 workflow 게시 블록 + 가짜 gh | **12개 통과**, GitHub 요청/게시 없음 |
| `v0.1.0` / 기존 호환 `v0.1` | 생성·공개 모두 `--prerelease=false`, Stable 제목·노트 확인 |
| `v0.1.0-rc.3` / `v0.1.0-rc.10` | 생성·공개 모두 `--prerelease=true`, Pre-release 제목·노트 확인 |
| 기존 draft 재시도 | Stable / RC 모두 올바른 채널로 공개 |
| 공개 Release 덮어쓰기 / 잘못된 해시 / 업로드 실패 | Stable / RC 모두 공개 차단 |
| 명세·원본 에셋 / actionlint / diff 공백 검사 | 통과 |
| 게시 폴더 및 ZIP 재압축 해제 EXE | 실제 실행 성공, 동봉 .NET 10.0.15 / X64 / 템플릿 79 / 합성 ROI 점수 1.0 |
| 격리 복사본의 실제 업데이트 도우미 | 설치·창 없는 재실행 확인 / 손상 EXE 설치 실패 후 롤백 / 개인 파일 보존 통과 |

배포본은 `artifacts/portable/20261112-161536-817/NyangShinobi-v0.1.0-dev.0.channel-policy-win-x64.zip`이며 **118,949,191 bytes**입니다. SHA-256: `07E5F5EEE0D3518CB3F3B9C2D52E77CBC2463E6BF5337F0EBF5E1C0970769A1E`. 테스트 TRX와 게시/ZIP/ADB 검증 JSON은 같은 시각 폴더, 분기 검사 로그는 `artifacts/release-policy/b901a41af297c5ba/`에 있습니다.

최신 배포본으로 실행한 `tools/Test-UpdateInstall.ps1` 결과는 `artifacts/update-install/20261112-161743-146/report.json`입니다. 정상 설치 및 재실행은 창 없는 검증 모드로 확인했고, 별도 실패용 복사본은 실제 Win32 EXE 실행 실패를 유도해 이전 버전과 개인 설정/스크린샷이 복원·보존됨을 확인했습니다. 실제 사용자 설치 폴더와 설정은 변경하지 않았습니다.

이번 정책 정리 대상은 `UpdatesView.xaml`, `UpdateChannelLabelConverter.cs`, `UpdateViewModel.cs`의 안내 문구, `UpdateViewModelTests.cs`, `.github/workflows/portable.yml`, `tools/Test-ReleasePolicy.py` 및 README/배포·업데이트 문서입니다. 다운로드·SemVer·ZIP 검증·파일 교체·롤백·개인 설정 보존·종료 인계를 담당하는 기존 파일 11개의 작업 전후 SHA-256이 일치했습니다. 게임/앱플레이어 강제 종료, ADB 서버 재시작, 새 채널·프로젝트·패키지와 Task 03 기능은 추가하지 않았습니다.

**실제 GitHub Actions에서 이 정책 변경을 실행하거나 Stable/RC Release를 게시하지 않았습니다.** 태그 Push 후 Windows 러너의 전체 검증과 Release 배지·첨부 ZIP·버전 메타데이터를 확인해야 합니다. 새 포맷 RC→RC 및 RC→Stable의 실제 사용자 GUI 업데이트, 깨끗한 새 Windows PC, 실제 MuMu/BlueStacks 상태 유지도 추가 실기 검증 항목입니다. 아래 기록은 정책 변경 전의 이력입니다.

## 2026-11-12 — GitHub Releases 자동 업데이트

[사용법·안전 조건](AUTO_UPDATE.md)에 따라 앱 시작 1회 확인, 수동 확인, Development/Stable 채널, SemVer 비교, 동의 후 다운로드/설치, 백업·롤백 및 재실행을 구현했습니다. **신규 포맷을 GitHub Release로 게시한 뒤 실제 사용자 설치 폴더에 적용하는 전체 과정은 아직 검증하지 않았습니다.** 아래는 가짜 서버 테스트, 실제 공개 서버 다운로드 및 격리 설치 테스트를 구분한 기록입니다. 업데이트 코드의 GitHub Actions 실행/신규 Release 게시도 아직 하지 않았습니다.

### 최종 로컬 자동 검증

| 항목 | 결과 |
|---|---|
| 전체 restore / Release build | 성공, 경고 0 / 오류 0 |
| 전체 테스트 | 기존 121 + 신규 66 = **187 통과 / 0 실패 / 0 건너뜀** |
| 프로젝트별 | Domain 8, Application 26, Features 2, Integration 151 |
| 명세/에셋 및 워크플로 정적 검사 | 기존 명세 PASS, actionlint 통과 |
| 포터블/ZIP 자가 검사 | 동봉 .NET 10.0.15, X64, 템플릿 79, 합성 ROI score 1.0, 실제 배포 버전 일치 |
| 배포 메타데이터 | `version.json`, 전체 관리 파일 SHA-256/크기의 `install-manifest.json` 포함 및 검증 |

SemVer RC 숫자·정식·build metadata·잘못된 형식/개행, 새 버전 있음/없음, 채널 필터, 네트워크 오류, HTTPS/host 리다이렉트 제한, 크기/해시, 다운로드 취소/스트림 해제, 손상 ZIP/버전 불일치/경로 이탈/예약 이름/대소문자 중복/링크를 검사했습니다. 실제 Windows 파일 잠금으로 부분 교체 실패와 롤백, 재실행 실패의 롤백, 새 프로세스가 살아 있을 때 위험한 롤백 중단, 개인 파일 충돌·보존도 검사했습니다.

시작 시 1회 확인·나중에/수동 확인, 실행 중 매크로/진단 보호, 다운로드 중 시작된 매크로 보호, 인계 전 종료 확인·실패 시 UI 복구, 동일 설치 폴더의 GUI 중복 실행·설치 잠금, WPF 바인딩을 확인했습니다. 기존 임무/거울/비전/PNG/RAW 벤치마크 테스트는 유지합니다.

최종 배포 출력:

```text
artifacts/portable/20261112-155754-564/
├── NyangShinobi-v0.1.0-dev.0.updater-win-x64/
├── NyangShinobi-v0.1.0-dev.0.updater-win-x64.zip
├── NyangShinobi-v0.1.0-dev.0.updater-win-x64.zip.sha256
└── tests/  # 4개 TRX 및 같은 시각 폴더의 게시/ZIP/ADB 검사 보고서

ZIP SHA256: 0168FA8AE505038A32530F207ED966EA87CD0948605F348DC6E79B28E7F6845D
```

### 실제 GitHub Releases 읽기/다운로드

앱의 `--verify-update-source`를 네트워크 허용 환경에서 실행했습니다. 공개 `v0.1.0-rc.2`의 Windows x64 ZIP **122,249,236 bytes**를 HTTPS로 실제 다운로드했고 첨부 SHA-256과 일치했습니다. 이 구형 ZIP에는 `install-manifest.json`이 없어 자동 설치 가능한 패키지로는 거부됐으며 **설치는 하지 않았습니다**.

결과 사본은 `.tools/updater-source-verification.json`에 있습니다. 원본 보고서/다운로드는 `artifacts/portable/20261112-144836-324/NyangShinobi-v0.1.0-rc.99-win-x64/.updates/0eb1c61a5cb142c0805af2af46720203/`에 있습니다. rc.99는 이전 로컬 검증용 이름이며 GitHub에 게시한 태그가 아닙니다. 이 실서버 검증은 최종본과 같은 HTTP/다운로드 로직을 사용합니다.

### 실제 도우미 프로세스·격리 설치

최종 배포본에 `tools/Test-UpdateInstall.ps1`을 실행했습니다. 대상은 원본 배포가 아닌 `artifacts/update-install/20261112-160011-208/app/` 복사본입니다. 테스트 전용 버전 rc.9001→rc.9002로 실제 별도 도우미가 관리 파일을 교체하고, 새 EXE가 자체 포함 런타임·PNG·템플릿·ROI 검사를 실행한 뒤 시작 응답을 남겼습니다.

다음 rc.9003 복사본은 EXE를 의도적으로 손상시키고 파일 목록 해시를 맞춰 재실행 실패를 유도했습니다. 실제 Win32 실행 실패 후 rc.9002 파일/버전이 복원됐으며 복원된 EXE의 자가 검사도 통과했습니다. 앱 폴더의 개인 설정/스크린샷 파일이 그대로임을 확인했습니다. 실제 사용자 `%LocalAppData%` 설정을 변경하지 않았습니다. 첫 검증을 포함해 격리 설치/롤백 검사는 두 배포본에서 각각 통과했습니다.

최종 결과: `artifacts/update-install/20261112-160011-208/report.json`. 정상 재실행은 창 없는 검증 모드를 사용했으며 **실제 사용자 GUI가 종료·재실행되는 현장 검증은 별도**입니다. GitHub Actions에도 같은 스크립트를 ZIP 업로드 앞 단계에 추가했지만 원격 실행 성공을 주장하지 않습니다.

### 변경 파일과 남은 확인

- 신규 `App/Updates/`: `SemVersion`, `UpdateFiles`(메타데이터/관리 목록/경로), `GitHubUpdateClient`, `UpdatePackage`, `UpdateInstaller`, `InstallRootGuard`, `UpdateCoordinator`, `UpdateViewModel`, `UpdatesView.xaml(.cs)`, `UpdateSourceVerification`.
- 수정 `App.xaml.cs`, `DependencyInjection.cs`, `ViewModels/MainViewModel.cs`, `Views/MainWindow.xaml(.cs)`, `Diagnostics/PortableValidation.cs`: 시작/종료·DI·UI·자가 검사 연결.
- 신규 통합 테스트 `UpdateReleaseTests`, `UpdatePackageTests`, `UpdateInstallerTests`, `UpdateViewModelTests`, `UpdateTestData`; 기존 `WindowLifecycleTests` 확장.
- `tools/Publish-Portable.ps1`, 신규 `tools/Test-UpdateInstall.ps1`, `.github/workflows/portable.yml`, `.gitignore`, `README.md`, 배포/업데이트 안내 및 이 기록.

새 프로젝트·외부 패키지는 추가하지 않았습니다. 기존 EXE/런타임을 도우미 임시 폴더로 복사하며 ZIP에는 추가 런타임이 없습니다. 설치 중에는 복사본·추출본·백업 디스크 공간이 필요합니다. 게임/이미지 인식/PNG 기본 방식·3초/500ms 정책은 변경하지 않았으며 Task 03은 구현하지 않았습니다.

남은 실제 배포 검증은 메타데이터를 포함한 신규 RC Release 게시→앱 UI 선택→정상 사용자 폴더 설치·GUI 재실행입니다. 현재 공개된 구형 Release는 자동 설치 대상이 아니므로 **새 포맷 포터블의 초기 수동 설치 및 신규 Release 게시가 실사용 전제**입니다. 정식 Stable Release가 아직 없을 때 정식 채널에 업데이트가 없는 것은 정상입니다.

## 2026-11-12 — CI 프로세스 경계 테스트의 준비 동기화 수정

대상 실패 실행은 [43133784338 / job 130904080757](https://github.com/Blooming2081/NyangShinobi_macro/actions/runs/43133784338/job/130904080757)입니다. 공개 API로 `windows-2025` 검증 작업 실패와 ZIP 업로드 건너뜀, 진단 Artifact 생성 성공을 확인했습니다. 상세 로그 다운로드는 HTTP 403, TRX Artifact 다운로드는 HTTP 401로 제한돼 과거 CI의 CPU 부하·콜드 스타트·임시 파일 쓰기 오류 여부까지 확인하지는 못했습니다. **아래 수정본의 GitHub 재실행 결과는 아직 없습니다.**

### 확인한 원인과 재현

기존 테스트는 `CommandTimeout=2초`를 PowerShell 스크립트 진입/PID 기록 시간과 분리하지 않았습니다. runner는 `Process.Start()` 전부터 제한 시간을 재며, timeout 분기에서는 준비 상태 확인 없이 예외 후 PID 파일이 존재한다고 단정했습니다. cancel 분기도 준비 대기 5초보다 명령 제한 2초가 먼저 만료될 수 있었습니다. 파일 존재만 확인하므로 부분 기록까지 완료된 것인지도 보장하지 못했습니다.

보고된 실패 위치는 `Assert.ThrowsAsync<TimeoutException>` 이후입니다. runner의 `TimeoutException` 변환은 `Process.Start()` 성공 이후의 try/catch에 있으므로, 해당 실행은 실행 파일을 못 찾아 시작하지 못한 경우와 구분됩니다. 다만 기존 코드는 timeout 시 가짜 CLI의 stderr를 테스트 보고서에 남기지 않아, PID 기록 지연과 파일 쓰기 오류의 세부 원인을 과거 로그만으로 확정할 수 없습니다. 실행 경로/인자 문제라고 단정할 근거도 없습니다.

로컬에서 기존 가짜 CLI의 PID 기록 앞에 3초 지연을 넣자, 2초 뒤 **동일한 56번째 줄 메시지**로 실패했습니다. 이것으로 준비 완료 전에 timeout이 발생하면 실패하는 테스트의 경쟁 조건을 재현했습니다. 재현 TRX: `artifacts/adb-runner-fix/reproduction/startup-race.trx`.

### 수정 및 진단

- PID 파일과 폴링을 비동기 Named Pipe의 PID 준비 응답으로 교체했습니다. 사용자가 취소하는 분기는 실제 준비 응답 후에만 취소합니다.
- 테스트의 준비 제한은 15초, 생성 전부터 시작하는 전체 명령 제한은 준비 예산을 포함한 18초입니다. 제품의 ADB 설정 및 3초/0.5초 정책은 그대로입니다. 준비 전 실행 실패/종료는 기다리지 않고 즉시 보고합니다.
- 정상/3초 지연 × timeout/cancel의 4개 경우를 검증합니다. 가짜 CLI의 종료와 별도로 생성한 같은 실행 파일의 비교용 프로세스 생존을 모두 확인하며, 실패/취소 경로에서도 소유한 프로세스·파이프를 정리합니다.
- 시작 실패는 실행 경로·Win32 코드, 준비 전 종료는 exit code·stderr, 준비 제한시간 초과는 파이프 연결 여부·runner 상태로 구분합니다. 잘못된 실행 경로 및 준비 전 exit 17/stderr 분류도 테스트합니다.
- xUnit 출력에 OS/.NET/아키텍처, 경로·인자, 준비 소요 시간, 대상/비교용 PID와 종료 상태를 남깁니다. TRX `StdOut` 포함을 확인했으므로 기존 CI `verification-*` Artifact에서 볼 수 있습니다. 배포 조건·권한·워크플로 변경은 없습니다.

### 로컬 검증

| 검사 | 결과 |
|---|---|
| 전체 Release build (`--no-restore`) | 경고 0 / 오류 0 |
| 프로세스 테스트 반복 | 8개 × 5회 = **40 통과 / 0 실패** |
| 위 반복 중 timeout/cancel | 4개 × 5회 = **20 통과**, 대상만 종료·비교용 프로세스 생존 |
| 준비 응답 | 373~3,415ms (3초 의도적 지연 포함) |
| 전체 테스트 | **84 통과 / 0 실패 / 0 건너뜀** (Domain 8, Application 26, Features 2, Integration 48) |
| 명세·에셋 및 workflow 정적 검사 | 기존 명세 PASS, actionlint 오류 없음 |

Named Pipe는 로컬 샌드박스의 접근 제한으로 최초 검증이 차단돼 권한 제한 밖에서 위 반복/전체 테스트를 수행했습니다. 실제 GitHub Actions 성공으로 보고하지 않습니다. 반복 로그/TRX는 `artifacts/adb-runner-fix/repeated-unrestricted/run-1`~`run-5`, 전체 보고서는 `artifacts/adb-runner-fix/full-final/`에 있습니다. 생성물은 Git에서 제외됩니다.

변경 파일은 `tests/NyangShinobi.Integration.Tests/AdbProcessRunnerTests.cs`, 같은 폴더의 `README.md`, 이 기록입니다. 프로덕션 `AdbProcessRunner`의 결함은 발견되지 않아 수정하지 않았습니다. 7개 본체·4개 테스트 프로젝트, NuGet 패키지, 게임/임무/거울/이미지 인식, Release 조건을 유지했으며 Task 03은 구현하지 않았습니다.

## 2026-11-12 — GitHub Actions 및 ADB 번들 로컬 검증

**실제 GitHub Actions/Release는 실행·게시하지 않았습니다.** 아래 결과는 Windows x64 개발 PC에서의 실행이며, Push 이후 호스팅 러너 실행과 Release 첨부 파일을 별도로 확인해야 합니다. 사용 절차는 [GitHub 배포 안내](GITHUB_DISTRIBUTION.md)를 따릅니다.

기존 `Publish-Portable.ps1`을 개발용 `-PackageVersion 0.1.0-dev.local` 및 태그용 `-ReleaseTag v0.1.0` 모드로 각각 끝까지 실행했습니다. 최초 샌드박스 실행은 NuGet 네트워크 제한으로 실패했으며, 네트워크를 허용한 실행에서 복원부터 재검증했습니다. 실패한 실행은 ZIP/성공 출력 경로를 제공하지 않았습니다.

| 검증 | 결과 |
|---|---|
| 전체 restore / Release build | 성공, 경고 0 / 오류 0 |
| 기존 전체 테스트 | 두 배포 실행 모두 79 통과 / 0 실패 / 0 건너뜀 (Domain 8, Application 26, Features 2, Integration 43) |
| 명세·에셋 | PASS: 86 catalog assets, 7 project specs, 6 modules, 1 event definitions |
| 워크플로 정적 검사 | 공식 actionlint 1.7.12, 오류 없음; 모든 외부 Action의 고정 SHA는 공식 GitHub API에서 확인 |
| PowerShell 구문 | 배포/ADB 번들 스크립트 검사 통과 |
| 게시 및 ZIP 재압축 해제 EXE | 양쪽 모두 exit 0, 동봉 .NET 10.0.15, X64, 템플릿 79, 합성 ROI 점수 1.0 |
| 게시 및 ZIP 재압축 해제 ADB | Google Platform-Tools 37.0.1-15733141, 양쪽 `adb version` exit 0; ADB 서버/장치 접속 없음 |
| 번들 파일 | ADB와 동반 DLL·원본 NOTICE/source.properties 포함, 고정 ZIP 및 개별 파일 SHA-256 일치 |
| 네이티브 의존성 | OpenCV 2개 및 ADB 3개 파일의 일반/지연 import 검사, 미동봉 VC++ 런타임 의존성 없음 |
| GitHub 출력 | 모든 검사 후 ZIP/체크섬/패키지 이름이 `GITHUB_OUTPUT`에 기록되고 실제 파일과 일치 |
| 실패 차단 | 버전 불일치, 잘못된 태그/패키지 경로, 태그·패키지 버전 동시 지정 거부; 가짜 SDK의 build/test 실패 시 패키징 중단·성공 출력 없음; 변조된 ADB ZIP은 추출 전에 차단 |
| Release 셸 로컬 검사 | Git Bash 구문 통과. `gh`를 가짜 함수로 대체한 신규 draft 생성→업로드→공개 및 기존 draft 재시도 성공. 이미 공개된 Release와 잘못된 체크섬은 업로드 없이 중단. **GitHub API 게시 검증은 아님** |
| Git 제외 | `.tools/`, `artifacts/` 및 ZIP 출력의 기존 제외 규칙 유지·확인 |

태그용 출력:

```text
artifacts/portable/20261112-023737-968/
├── NyangShinobi-v0.1.0-win-x64/
├── NyangShinobi-v0.1.0-win-x64.zip
├── NyangShinobi-v0.1.0-win-x64.zip.sha256
├── portable-check.json / zip-check.json
├── adb-check.txt / zip-adb-check.txt
├── native-dependencies.json
├── tests/
└── zip-verification/
```

ZIP 크기는 **118,846,913 bytes (약 113.3 MiB)**, SHA-256은 다음과 같습니다.

```text
E5640F82874FE3F439563E507B951040E08F05811497AE87A7C698D47F9087AD
```

개발용 ZIP은 `artifacts/portable/20261112-023420-257/NyangShinobi-v0.1.0-dev.local-win-x64.zip`입니다. 실패 차단 검사 로그는 `artifacts/packaging-checks/20261112-023828-017/`, 가짜 gh 검사 로그는 `artifacts/release-script-checks/`에 있습니다. 모두 생성물이므로 Git에 포함하지 않습니다.

생성 파일: `.github/workflows/portable.yml`, `tools/Copy-PortableAdb.ps1`, `tools/dependencies/platform-tools.windows.json`, `docs/GITHUB_DISTRIBUTION.md`. 수정 파일: `tools/Publish-Portable.ps1`, `README.md`, `docs/PORTABLE_DISTRIBUTION.md`, 이 기록. **src/tests 프로젝트·C#·패키지·원본 에셋·게임 로직 변경 없음**, Task 03 미구현 유지.

미검증: 실제 GitHub 호스팅 러너의 전체 실행/Artifact/Release 업로드·권한·다운로드, 깨끗한 새 Windows PC, 실제 MuMu/BlueStacks 연결·캡처·인식 성능. 새 ADB 다운로드 로직의 최초 캐시 생성은 이번 실행에서 공식 ZIP을 미리 내려받아 캐시했으므로 CI의 빈 캐시 환경에서 추가 확인해야 합니다. 아래 2026-11-11의 일반 WPF UI 검사는 이전 배포 기록이며 이번에는 EXE의 `--verify-portable` 모드를 재실행했습니다.

## 2026-11-11 — 초기 포터블 배포 기록

2026-11-11 (한국 시간), Windows x64, .NET SDK 10.0.404로 `tools/Publish-Portable.ps1`를 실제 실행했습니다. 실행 방법은 [포터블 배포 안내](PORTABLE_DISTRIBUTION.md)를 참조하세요.

## 생성 결과

```text
artifacts/portable/20261111-230302-058/
├── NyangShinobi-v0.1.0-win-x64/
├── NyangShinobi-v0.1.0-win-x64.zip
├── NyangShinobi-v0.1.0-win-x64.zip.sha256
├── portable-check.json
├── zip-check.json
├── native-dependencies.json
├── wpf-ui-check.json
├── tests/
└── zip-verification/
```

ZIP은 **114,526,458 bytes (약 109.2 MiB)**이며 SHA-256은 다음과 같습니다.

```text
B8E9E9E1D7F6FC2CD3289A579C22EB5200C1CE31DACE75A8AD2556E801CAC905
```

이 경로들은 생성물로 Git에서 제외합니다. 이후 스크립트 실행은 새로운 시각 폴더를 만들며, 위 경로는 이번 실행 기록입니다.

## 실제 실행 결과

| 검증 | 결과 |
|---|---|
| solution restore | 성공 |
| 전체 Release build | 경고 0 / 오류 0 |
| 전체 test | **79 통과 / 0 실패 / 0 건너뜀** (Domain 8, Application 26, Features 2, Integration 43) |
| self-contained publish | Windows x64, .NET 10.0.15 Core + WindowsDesktop 포함, single-file/trimming 비활성 |
| 게시 EXE 자가 검사 | exit 0, `LocalRuntime=true`, `X64`, 템플릿 79, 합성 ROI 점수 1.0 |
| ZIP 재압축 해제 EXE | 별도 폴더에서 같은 검사 exit 0, 같은 결과 |
| 일반 WPF EXE | 게시 EXE를 임시 폴더를 작업 디렉터리로 숨김 실행, 정상 창 생성·진단 탭 접근·79개 에셋 로딩 확인 후 종료 |
| WPF의 네이티브 모듈 | `coreclr.dll`과 `OpenCvSharpExtern.dll`이 실제 게시 폴더에서 로드됨 |
| 진단 장치 선택 | 자동 선택 없음, 장치 미선택 상태에서 캡처 비활성 |
| 명세·에셋 검증 | PASS: 86 catalog assets, 7 project specs, 6 modules, 1 event definitions |

기존 78개 테스트에, 전역 런타임을 쓰는 테스트 호스트를 자체 포함 실행으로 잘못 보고하지 않는 검증 1개를 추가했습니다. 배포 자가 검사는 사용자 설정/ADB/게임을 사용하지 않고 동봉 기본 설정·에셋·디코더·진단 인식 API를 사용합니다. 합성 ROI 점수는 게임 성능/정확도 지표가 아닙니다.

## VC++와 OS 의존성

고정 OpenCvSharp5 5.0.0.20261109의 `OpenCvSharpExtern.dll`과 `opencv_videoio_ffmpeg500_64.dll`에 대해 x64 PE 일반/지연 import를 검사했습니다. 외부 `MSVCP`/`VCRUNTIME`/`CONCRT` 의존성은 없었습니다. .NET WPF용 `vcruntime140_cor3.dll`은 게시 폴더에 포함됨을 확인했습니다. 현재 배포의 별도 VC++ 재배포 패키지 설치를 필수로 지정하지 않았습니다.

OpenCV full runtime의 Media Foundation 등 Windows 시스템 DLL은 OS 의존성으로 남습니다. N/KN 또는 미디어 기능이 빠진 환경은 배포 안내의 조건을 확인해야 합니다.

## 변경 파일과 범위

- `tools/Publish-Portable.ps1`: 전체 빌드/테스트, 자체 포함 게시, 필수 파일·JSON·에셋 해시·native import 확인, 게시/ZIP EXE 검사, ZIP/SHA 생성.
- `src/NyangShinobi.App/Properties/PublishProfiles/PortableWinX64.pubxml`: 자체 포함 x64 폴더 게시 설정.
- `src/NyangShinobi.App/NyangShinobi.App.csproj`: 기존 에셋/JSON의 게시 복사 정책 명시.
- `src/NyangShinobi.App/Diagnostics/PortableValidation.cs`, `App.xaml.cs`: `--verify-portable` 자가 검사 경로. 일반 시작/진단 동작은 유지.
- `tests/NyangShinobi.Integration.Tests/PortableValidationTests.cs`: 프레임워크 의존 테스트 호스트의 잘못된 자체 포함 보고 방지.
- `.gitignore`, README, 시작/진단 안내 및 이 배포 문서들: 출력 제외·배포 사용 방법·검증 범위 기록.

7개 본체·4개 테스트 프로젝트, 기존 참조 방향, 이미지 원본과 게임 워크플로는 유지했습니다. Task 03은 구현하지 않았습니다. 깨끗한 새 Windows VM 및 실제 MuMu/BlueStacks의 ADB 연결·캡처 성능·인식 정확도는 이번 검증에 포함하지 않았습니다.
