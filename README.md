# 냥냥시노비 v0.1

Windows x64 · C# · .NET 10 · WPF 신규 개발 프로젝트입니다. 현재 구현 범위는 **Task 01 골격 + Task 02 ADB PNG 캡처·입력·ROI 인식 기반**입니다. 게임 진행 로직은 아직 연결하지 않았습니다.

7개 본체 프로젝트와 4개 xUnit 테스트 프로젝트가 `NyangShinobi.slnx`에 포함됩니다. 설계의 최종 권위는 [아키텍처](docs/10_ARCHITECTURE.md)와 [프로젝트 참조 명세](data/architecture.v0.1.json)입니다.

개발·Codex 작업은 [통합 시작 안내](docs/START_HERE.md)와 [AGENTS.md](AGENTS.md)를 먼저 읽습니다. 기존 루트의 시작 안내 두 문서는 통합했으며, 과거 설계 변경 이력은 [docs/changelog/](docs/changelog/)로 옮겼습니다.

## 폴더 구성

```text
NyangShinobi/
├── AGENTS.md
├── README.md
├── NyangShinobi.slnx
├── global.json
├── Directory.Build.props
├── Directory.Packages.props
├── NuGet.Config
├── .editorconfig
├── .gitignore
├── docs/                              # 시작 안내 / 설계 / 검증 결과
│   ├── START_HERE.md
│   ├── 01_SCOPE.md ... 16_EVENT_IMPLEMENTATION_CHECKLIST.md
│   ├── TASK01_FOUNDATION.md
│   ├── TASK02_CAPTURE_VISION.md
│   ├── TASK02_DIAGNOSTICS.md
│   └── changelog/                     # 과거 설계 변경 이력 2개
├── src/
│   ├── NyangShinobi.App/
│   ├── NyangShinobi.Domain/
│   ├── NyangShinobi.Contracts/
│   ├── NyangShinobi.Application/
│   ├── NyangShinobi.Features/
│   ├── NyangShinobi.Vision.OpenCvSharp/
│   └── NyangShinobi.Device.Adb/
├── tests/
│   ├── NyangShinobi.Domain.Tests/
│   ├── NyangShinobi.Application.Tests/
│   ├── NyangShinobi.Features.Tests/
│   └── NyangShinobi.Integration.Tests/
├── tasks/                             # 작업 단위와 최초 요청 기록
├── data/                              # 기계 판독 명세와 기본 설정
├── assets/                            # 원본 이미지
├── reference/legacy_v206_methods/      # 읽기 전용 v2.0.6 참조
├── tools/validate_spec.py              # Git으로 관리하는 검증 도구
├── .tools/                            # 로컬 SDK/캐시, Git 제외
└── TestResults/                        # 테스트 생성물, Git 제외
```

각 프로젝트의 `bin/`·`obj/`, 로컬 IDE 상태·사용자 설정·로그도 Git에서 제외합니다. `.tools/`와 `tools/`는 용도가 다른 별도 폴더입니다.

## 빌드와 실행

`global.json`에 지정한 .NET SDK 10.0.404의 동일 feature band 패치와 Windows x64 환경이 필요합니다.

```powershell
dotnet restore NyangShinobi.slnx -m:1
dotnet build NyangShinobi.slnx -c Release --no-restore -m:1
dotnet test NyangShinobi.slnx -c Release --no-build --no-restore -m:1
dotnet run --project src/NyangShinobi.App -c Release --no-build --no-restore
python tools/validate_spec.py
```

이 작업에서 설치한 로컬 SDK를 사용할 경우, 현재 PowerShell에 다음을 먼저 적용합니다. `.tools/`는 Git에서 제외됩니다.

```powershell
$env:PATH = "$PWD\.tools\dotnet;$env:PATH"
$env:DOTNET_ROOT = "$PWD\.tools\dotnet"
$env:DOTNET_CLI_HOME = "$PWD\.tools\cli"
$env:NUGET_PACKAGES = "$PWD\.tools\nuget"
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
```

`NuGet.Config`와 `RestoreConfigFile`은 저장소의 nuget.org 소스만 사용하도록 지정합니다. 패키지 버전은 `Directory.Packages.props`에서 고정합니다.

위 명령은 코어 수가 많은 개발 환경에서도 MSBuild 프로세스 수를 제한합니다. 테스트 러너가 `VSTEST_ARTIFACTS_DIRECTORY` 미지정 메시지를 출력하면 개인 임시 폴더를 사용한다는 안내이며, 테스트 실패는 아닙니다.

## 현재 동작

- WPF 창에 버전, 모드, 설정 요약, 시작/중지 상태를 표시합니다.
- 시작은 게임을 조작하지 않는 골격 세션을 열며, 중지와 창 닫기는 세션 취소를 완료합니다. 실행 중 모드 변경과 중복 시작은 차단합니다.
- 출력 폴더의 `data/settings.default.v0.1.json`을 파싱합니다. `%LocalAppData%/NyangShinobi/settings.v0.1.json`이 존재하면 우선 읽습니다. 저장/설정 편집 UI는 구현하지 않았습니다.
- 잘못된 설정은 오류로 표시하며 기본값으로 조용히 대체하지 않습니다. 원본 기본 설정은 수정하지 않습니다.
- 임무/거울/네코 모드는 `IGameFlow` 골격만 등록하며 단계 호출은 `NeedsUserAction`을 반환합니다.
- 캡처·입력 어댑터는 명시한 serial의 준비된 장치만 사용합니다. PNG 단발 캡처 후 실제 해상도로 탭/스와이프 좌표를 변환하며, 전송 완료는 `Pending`입니다. 화면 검증과 구분합니다.
- OpenCvSharp5 엔진은 manifest 무결성 검사·템플릿 캐시·정규화 ROI 매칭을 지원합니다. 비동기 API가 ADB/이미지 처리를 작업 스레드에서 수행하며 UI에 Mat를 노출하지 않습니다.
- `FrameScheduler`/`InputSequencer`는 동시 요청의 프레임 공유, 기본 3초 대기, 입력 완료 후 0.5초 새 프레임 검증을 제공합니다. 검증 실패는 입력 중단으로 이어지며 자동 재터치하지 않습니다.
- 이벤트 핸들러는 기본 0개이며 `AddGameEventHandler<T>()`로 독립 등록할 수 있습니다. 예시 JSON을 로드하거나 활성화하지 않습니다.
- **개발자 진단** 탭에서 ADB 경로 지정 → 장치 조회·직접 선택 → 메모리 스크린샷 → 에셋/ROI 선택·일치율 측정을 수행합니다. 캡처/디코딩/인식 시간과 해상도를 표시하며, PNG는 저장 버튼으로 경로를 확인했을 때만 저장합니다. 진단에는 터치·스와이프가 없습니다.

UI 시작 버튼은 위 장치 API를 자동으로 호출하지 않습니다. 실제 사용에는 사용자가 준비한 ADB 실행 파일과 명시적 serial, 장치 세션 scope와 취소 토큰이 필요합니다. 원본 v2.0.6 흐름 및 에셋은 변경하지 않았습니다.

수동 실기 확인 방법은 [개발자 진단 안내](docs/TASK02_DIAGNOSTICS.md), 기반 API는 [Task 02 결과](docs/TASK02_CAPTURE_VISION.md), 초기 골격 기록은 [Task 01 결과](docs/TASK01_FOUNDATION.md)를 참조하세요. 임무·거울 진행, 날짜 팝업/광고/뽑기/이벤트 실행은 후속 Task이며 Task 03은 아직 시작하지 않았습니다. 실제 앱플레이어의 성능·정확도는 미검증입니다.
