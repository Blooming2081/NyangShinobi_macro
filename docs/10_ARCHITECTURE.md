# v0.1 정식 아키텍처 — Windows x64 / C# 단일 언어

> 이 문서는 `docs/05_IMPLEMENTATION.md`의 단순 추천 구조를 **대체하는 최종 파일 구조 계약**이다. 상세 게임 상태는 기존 `docs/02_*`, `03_*`, `08_*`, `09_*`를 그대로 따른다. Codex는 단위 파일을 줄이기 위해 모듈 경계를 병합하지 않는다.

## 설계 판단

- 플랫폼: Windows 10/11 x64. 타깃 `net10.0-windows`(WPF), 나머지 라이브러리 `net10.0`.
- C# 단일 언어 + WPF(MVVM 최소) + OpenCvSharp5 Windows 패키지(버전 중앙 고정) + adb.exe/ADB shell.
- 이미지 인식 속도 문제를 증명하기 전 별도 C++ DLL / 영상 스트리밍 / minicap / minitouch / 자체 ADB 5037 클라이언트 추가 금지.
- 단일 앱 프로세스. 장치별 단일 캡처 스케줄러 + 단일 순차 입력 큐. 비동기/취소 가능 상태 머신 사용.
- 확장 모듈은 실행 중 DLL 다운로드/동적 로딩이 아니라 **소스코드와 DI 등록에 의한 모듈화**. 게임 이벤트 단순 팝업만 스키마 검증된 JSON 정의 허용.
- `Domain`, `Contracts`, `Application`은 `OpenCvSharp`, `WPF`, `adb.exe` 구현을 참조하지 않음.

## 권장 저장소 구조 (Task 01 골격 및 후속 Task 구현 계획)

문서의 이동·통합만 반영했으며 프로젝트 경계와 후속 구현 계획은 유지한다. 아래 소스 트리에는 후속 Task에서 생성할 파일도 포함된다. 현재 구현 상태는 [Task 01 결과](TASK01_FOUNDATION.md)와 [Task 02 결과](TASK02_CAPTURE_VISION.md)를 참조한다.

```text
NyangShinobi/
├── NyangShinobi.slnx
├── Directory.Build.props                  # nullable, analyzers, version, deterministic
├── Directory.Packages.props               # NuGet 버전 중앙 관리
├── global.json                            # SDK 10.0.x 고정(개발 PC에 맞춰 실존 버전으로)
├── NuGet.Config                           # 저장소 NuGet 소스
├── .editorconfig
├── .gitignore
├── AGENTS.md
├── README.md                              # 현재 상태 / 빌드 / 실행 / 저장소 안내
├── docs/
│   ├── START_HERE.md                      # 개발·Codex 시작 안내 통합
│   ├── 01_SCOPE.md ... 09_DOUBLE_REWARD.md
│   ├── 10_ARCHITECTURE.md
│   ├── 11_EVENT_EXTENSION.md
│   ├── 12_DATE_CHANGE.md
│   ├── 13_CONTRACTS_AND_SETTINGS.md
│   ├── 14_ROADMAP_AND_TESTS.md
│   ├── 15_ARCHITECTURE_DECISIONS.md
│   ├── 16_EVENT_IMPLEMENTATION_CHECKLIST.md
│   ├── TASK01_FOUNDATION.md
│   ├── TASK02_CAPTURE_VISION.md
│   └── changelog/                         # 과거 설계 변경 이력
├── data/                                  # 설계의 기계 판독용 데이터
│   ├── architecture.v0.1.json
│   ├── workflows.v0.1.json
│   ├── modules.v0.1.json
│   ├── screen_catalog.v0.1.json
│   ├── settings.default.v0.1.json
│   └── events/
│       ├── event.v0.1.schema.json
│       └── example_limited_time_popup.disabled.json
├── assets/
│   ├── images/                            # 거울 원본
│   ├── images_nav/                        # 임무/거울/날짜/보상/네코 원본
│   ├── skill_priority/                    # 우선순위
│   ├── secret_skill/                      # 비기
│   └── events/<event_id>/                 # 향후 이벤트
├── src/
│   ├── NyangShinobi.App/                  # WPF 진입점 (Composition Root)
│   │   ├── App.xaml(.cs)
│   │   ├── Views/                         # MainWindow, Settings, EventSettings
│   │   ├── ViewModels/                    # MainViewModel, SettingsViewModel
│   │   ├── Services/                      # UI 상태 구독/디스패처
│   │   └── DependencyInjection.cs         # 모듈 등록, config validation
│   ├── NyangShinobi.Domain/               # 게임 상태/값 타입/순수 정책
│   │   ├── GameMode.cs
│   │   ├── GameState.cs
│   │   ├── ScreenEvidence.cs
│   │   ├── ScreenGeometry.cs
│   │   ├── FlowOutcome.cs
│   │   └── GameDatePolicy.cs
│   ├── NyangShinobi.Contracts/            # 구현 독립 인터페이스
│   │   ├── IVisionEngine.cs
│   │   ├── IScreenCapture.cs
│   │   ├── IInputController.cs
│   │   ├── IGameFlow.cs
│   │   ├── IGameEventHandler.cs
│   │   ├── IGameClock.cs
│   │   └── ISettingsStore.cs
│   ├── NyangShinobi.Application/          # 공통 런타임 오케스트레이션
│   │   ├── MacroSession.cs                # 시작/취소/종료, 모드 단일 소유자
│   │   ├── FlowCoordinator.cs             # 현재 모드 실행
│   │   ├── FrameScheduler.cs              # 캡처 1곳, 프레임 1장 재사용
│   │   ├── InputSequencer.cs              # 입력 중복 방지, 완료/검증
│   │   ├── EventDispatcher.cs             # 날짜/이벤트 우선순위 처리
│   │   └── SettingsValidation.cs
│   ├── NyangShinobi.Features/             # 게임 기능별 독립 모듈
│   │   ├── Mission/
│   │   │   ├── MissionFlow.cs
│   │   │   ├── Navigation/
│   │   │   ├── Presets/
│   │   │   ├── Combat/
│   │   │   └── Results/
│   │   ├── Mirror/
│   │   │   ├── MirrorFlow.cs
│   │   │   ├── Entry/
│   │   │   ├── CardSelection/
│   │   │   └── Results/
│   │   ├── NekoNinja/NekoNinjaFlow.cs
│   │   ├── DoubleReward/DoubleRewardSubflow.cs
│   │   ├── DateChange/DateChangeFlow.cs
│   │   ├── Events/
│   │   │   ├── BuiltIn/                   # 신규 복합 이벤트 C# 코드
│   │   │   ├── Declarative/               # 검증된 JSON 규칙 해석
│   │   │   └── EventModuleRegistration.cs
│   │   └── FeatureModuleRegistration.cs
│   ├── NyangShinobi.Vision.OpenCvSharp/    # OpenCV 네이티브 자원 소유
│   │   ├── OpenCvVisionEngine.cs
│   │   ├── TemplateRepository.cs
│   │   └── RoiMatcher.cs
│   └── NyangShinobi.Device.Adb/           # adb process, serial, png/raw, shell
│       ├── AdbDeviceClient.cs
│       ├── AdbScreenCapture.cs
│       ├── AdbInputController.cs
│       └── PersistentShell.cs
├── tests/
│   ├── NyangShinobi.Domain.Tests/
│   ├── NyangShinobi.Application.Tests/
│   ├── NyangShinobi.Features.Tests/
│   └── NyangShinobi.Integration.Tests/    # fake ADB/녹화 fixture; 실기 별도
├── tools/validate_spec.py                # 명세/에셋/JSON 구조 검사
├── tasks/
└── reference/legacy_v206_methods/         # 실행/빌드/배포 금지, 읽기 전용
```

## 프로젝트 참조 방향 (순환 금지)

```text
App(Composition Root)
 ├── Application → Contracts → Domain
 ├── Features → Application / Contracts / Domain
 ├── Vision.OpenCvSharp → Contracts / Domain
 └── Device.Adb → Contracts / Domain
```

- App만 구체 구현을 조립/주입한다. Application은 Mission 등의 구체 클래스 이름을 모른다.
- `Features`가 `App`, `Vision.OpenCvSharp`, `Device.Adb` 프로젝트를 직접 참조하지 않는다.
- `Domain`은 NuGet 없이 가능한 순수 규칙 타입으로 유지한다.
- 공통 기능이 등장하기 전부터 범용 plugin framework, reflection 기반 로더, 미니 DSL, 이벤트 별 신규 스레드를 만들지 않는다.
- 한 프로젝트에 기능별 네임스페이스/폴더를 두되 복잡도가 실제로 커지면 `Features`의 개별 프로젝트 분리를 ADR로 결정한다.

## 서비스 수명과 자원

| 소유자 | 인스턴스 수 | 책임 |
|---|---|---|
| WPF App | 프로세스당 1 | 사용자 입력/바인딩/설정 |
| MacroSession | 활성 장치당 1 | 모드 하나 실행, 취소 요청 |
| FrameScheduler | 활성 장치당 1 | 캡처 예약, 캐시, 버퍼 수명 |
| InputSequencer | 활성 장치당 1 | 순차 입력, 검증 대기, 중복 차단 |
| OpenCvVisionEngine | 세션당 1 | Mat 소유/Dispose, ROI 검사 |
| EventDispatcher | 세션당 1 | 한 프레임에서 우선 처리 하나 선택 |
| Event handler | 이벤트 등록당 1 | trigger 판별 + 상태 전환, 필요 시 상태 보관 |

이미지 버퍼는 한 프레임 검사 동안 유효해야 하며 외부 핸들러가 저장하지 않는다. .NET에서 메모리 반환/네이티브 Mat Dispose 책임을 명시하고, UI에는 결과·점수·좌표만 전달한다.

## 실패 처리

- `NeedsUserAction`은 정상 종료 상태이며 **자동복구가 아니다**.
- 발생 원인을 사용자에게 제시하고 추가 입력 없이 취소 처리한다.
- 게임 진행에 필수적인 판정 검증은 유지한다(카드/보상/날짜 팝업/이벤트).
- 날짜 변경이나 이벤트 후 홈 재진입은 **게임 UI 흐름**으로만 구현한다. 앱플레이어나 Android 게임 프로세스는 재시작하지 않는다.
