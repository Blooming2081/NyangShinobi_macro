# Windows x64 포터블 배포 검증 기록

## 2026-11-12 — RAW vs PNG 개발자 벤치마크

[사용법과 측정 정의](CAPTURE_BENCHMARK.md)에 따라 기존 개발자 진단 탭에 수동 벤치마크를 추가했습니다. **실제 MuMu/BlueStacks 캡처나 성능 측정은 하지 않았습니다.** 아래 값은 자동 검증 결과이며 가짜 ADB 실행 시간은 실기 성능으로 제시하지 않습니다. 새 GitHub Actions 실행도 수행하지 않았습니다.

| 검사 | 로컬 결과 |
|---|---|
| `dotnet restore` | 성공 |
| 전체 Release build | 경고 0 / 오류 0 |
| 신규 벤치마크 테스트 | **37 통과 / 0 실패** |
| 전체 테스트 | 기존 84 + 신규 37 = **121 통과 / 0 실패 / 0 건너뜀** |
| 프로젝트별 | Domain 8, Application 26, Features 2, Integration 85 |
| 명세·에셋 | PASS: 86 catalog assets, 7 project specs, 6 modules, 1 event definitions |
| GitHub workflow 정적 검사 | actionlint 통과, Release 조건·권한 변경 없음 |
| 포터블/ZIP EXE 실행 | 각각 동봉 .NET 10.0.15, X64, 템플릿 79, 합성 ROI score 1.0 |
| 동봉 ADB | 게시 및 ZIP 재압축 해제 경로에서 오프라인 `adb version` 성공 |
| 경계 보존 | 게임 캡처/스케줄러/입력 시퀀서/비전 엔진·원본 에셋·패키지·프로젝트·배포 스크립트 변경 없음 |

가짜 ADB 테스트는 예열 제외·10회씩 순서 교대·직접 선택한 경로/serial·회차별 시간/크기/해상도, 두 방식 BGRA32 생성, RAW 12/16바이트 헤더와 RGBA/RGBX/BGRA 변환을 확인했습니다. 잘못된 해상도·버퍼 길이·stride·오버플로·미지원 형식/색공간·PNG 헤더/payload·ADB 비정상 종료/timeout도 검증했습니다. 픽셀 일치를 요구하지 않도록 PNG/RAW 해상도가 다른 경우도 통과했습니다.

취소/창 닫기, 일반 매크로·다른 진단·중복 요청 차단, 진단 서비스의 캡처 직렬화, 마지막 화면/결과 수명, 실제 WPF 버튼 바인딩을 확인했습니다. 기존 실제 OS CLI의 timeout/취소·소유 프로세스 회수 테스트도 전체 테스트에서 유지했습니다.

실행한 포터블 명령:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Publish-Portable.ps1 -PackageVersion 0.1.0-dev.raw-benchmark
```

출력: `artifacts/portable/20261112-131001-898/NyangShinobi-v0.1.0-dev.raw-benchmark-win-x64.zip`

```text
SHA256: 44E2B6A2CFD8A20C46E3249E09D4127A7C19C5D5545F740E8F3A539DFC61A82A
```

같은 시각 폴더에 전체 4개 TRX, native 의존성, 게시/ZIP EXE 및 ADB 검사 보고서가 있습니다. 신규 테스트만 실행한 TRX는 `artifacts/capture-benchmark/final-focused/`에 있습니다. 모두 Git 제외 생성물입니다.

생성·수정 파일:

- 신규 `src/NyangShinobi.App/Diagnostics/CaptureBenchmark.cs`: 제한된 순차 ADB 캡처·예열·교대·시간/크기 측정·오류/취소 결과.
- 신규 `CaptureBenchmarkResult.cs`, `RawBenchmarkDecoder.cs` (같은 Diagnostics 폴더): 통계/표본/지원 결과, 벤치마크 전용 RAW 검증·BGRA32 변환.
- 기존 `CaptureDiagnostics.cs`, `DiagnosticsViewModel.cs`, `DiagnosticsView.xaml` (같은 폴더): runner 재사용·직렬화·기존 작업 잠금·진행/결과 UI.
- 신규 `tests/NyangShinobi.Integration.Tests/CaptureBenchmarkTests.cs`, 기존 `WindowLifecycleTests.cs`: 가짜 ADB·RAW·통계·취소/오류·상호 배제·WPF 바인딩.
- 신규 `docs/CAPTURE_BENCHMARK.md`, 기존 `README.md`, `docs/TASK02_DIAGNOSTICS.md`, `docs/PORTABLE_DISTRIBUTION.md`, 이 기록: 사용법과 검증 범위.

실기에서 남은 항목은 MuMu/BlueStacks의 실제 RAW 헤더/색상/회전 지원, 같은 조건에서 시간·전송 크기 분포 및 취소 응답입니다. 검증 결과만으로 RAW 전환을 결정하지 않았으며, 기본 PNG와 3초/500ms 정책 및 Task 03 미구현 상태를 유지합니다.

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
