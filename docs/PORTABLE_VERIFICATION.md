# Windows x64 포터블 배포 검증 기록

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
