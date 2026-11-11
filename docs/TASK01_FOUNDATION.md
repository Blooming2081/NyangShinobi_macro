# Task 01 구현 결과

범위: `tasks/01_foundation.md`. 본체 7개 프로젝트, 테스트 4개 프로젝트, 최소 WPF UI, 설정 파싱, 취소 가능한 골격 세션, 핵심 계약과 정적 DI 등록입니다.

## 생성·수정 파일

| 위치 | 내용 |
|---|---|
| 루트 | `NyangShinobi.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `NuGet.Config`, `.editorconfig`, `.gitignore`, `README.md` |
| `src/NyangShinobi.Domain` | 프로젝트, 모드/상태/결과 타입, 정규화 좌표, 프레임/검출 증거 계약 |
| `src/NyangShinobi.Contracts` | 프로젝트, `IVisionEngine`, `IScreenCapture`, `IInputController`, `IGameFlow`, `IGameEventHandler`, `IEventRegistry`, `IScreenDetector`, `IGameClock`, `ISettingsStore`, 기본 설정 모델 |
| `src/NyangShinobi.Application` | 프로젝트, `MacroSession`, 설정 로더/기본 검증, 시스템 시계, 등록 이벤트 목록 |
| `src/NyangShinobi.Features` | 프로젝트, 임무/거울/네코 골격, `FeatureModuleRegistration`, `Events/EventModuleRegistration` |
| `src/NyangShinobi.Vision.OpenCvSharp` | 프로젝트, 호출을 거절하는 인식 어댑터 골격 |
| `src/NyangShinobi.Device.Adb` | 프로젝트, 호출을 거절하는 캡처/입력 어댑터 골격 |
| `src/NyangShinobi.App` | WPF 프로젝트, `App.xaml(.cs)`, `DependencyInjection.cs`, `Views/MainWindow.xaml(.cs)`, `ViewModels/MainViewModel.cs`, `RelayCommand.cs` |
| `tests/*` | 4개 xUnit 프로젝트 및 `FrameContractTests.cs`, `SettingsTests.cs`, `MacroSessionTests.cs`, `FeatureRegistrationTests.cs`, `CompositionTests.cs`, `WindowLifecycleTests.cs` |
| 각 프로젝트 `README.md` | Task 01의 실제 구현 범위로 예약 안내 갱신 |
| 이 문서 | 검증 결과 및 남은 범위 |

## 프로젝트 의존성

```text
Domain → 없음 (NuGet 없음)
Contracts → Domain
Application → Domain, Contracts
Features → Application, Contracts, Domain
Vision.OpenCvSharp → Contracts, Domain
Device.Adb → Contracts, Domain
App → Application, Features, Vision.OpenCvSharp, Device.Adb
```

라이브러리는 `net10.0`, WPF App은 `net10.0-windows`/`win-x64`입니다. 모든 프로젝트는 x64로 빌드합니다. 테스트는 해당 계층을 참조하고, Integration.Tests는 WPF App의 구성 루트를 검증하기 위해 `net10.0-windows`를 사용합니다.

Microsoft DI 구현은 App과 테스트에만 있으며 Features는 등록용 DI Abstractions만 사용합니다. Domain/Contracts/Application은 WPF/OpenCV/ADB 구현을 참조하지 않습니다. App의 UI에는 OpenCV 타입이 노출되지 않습니다.

`OpenCvSharp5.Windows` 버전은 중앙 파일에 후속 작업용으로 고정했지만 아직 PackageReference를 추가하지 않았습니다. Task 01에서는 OpenCV 네이티브 런타임을 배포하거나 로드하지 않습니다.

## 계약과 수명

- `ImageFrame`은 BGRA32 관리 메모리를 복사·소유합니다. ID/시각/해상도/stride/형식을 가지며 네이티브 자원은 없습니다. 모드와 이벤트는 같은 프레임을 받도록 계약을 공유합니다. 실제 스케줄러는 미구현입니다.
- `MacroSession`은 싱글턴이며 동시에 한 모드만 소유합니다. 시작 시 취소 신호만 비동기로 기다리며 게임 루프/스레드/타이머/캡처를 실행하지 않습니다.
- `IGameFlow`의 `StepAsync`와 `Pause/Resume`는 확장 계약입니다. 골격의 `Pause/Resume`는 상태/입력이 없는 빈 구현이며 단계 실행은 항상 `NeedsUserAction`입니다.
- 이벤트는 `Features/Events`에서 정적으로 등록합니다. 현재 날짜 변경 핸들러 및 일반 이벤트는 등록하지 않습니다. 우선순위/중복 방지/재개는 Task 08/09에서 구현합니다.
- 새로운 화면 전환은 구현하지 않았고 `data/workflows.v0.1.json`의 M/R/N/A/D 순서·분기는 보존했습니다. 게임 흐름의 의도적 변경은 없습니다.

## 검증

2026-11-11 (한국 시간), Windows x64, 작업 폴더 안에 설치한 .NET SDK **10.0.404**로 실제 실행했습니다. 기본 설치에는 SDK가 없어서 `.tools/dotnet`을 사용했고, CLI/NuGet 캐시도 `.tools`로 지정했습니다.

```powershell
.tools/dotnet/dotnet.exe restore NyangShinobi.slnx --configfile NuGet.Config -m:1
.tools/dotnet/dotnet.exe build NyangShinobi.slnx -c Release --no-restore -m:1
.tools/dotnet/dotnet.exe test NyangShinobi.slnx -c Release --no-build --no-restore -m:1 --logger trx --results-directory TestResults
python tools/validate_spec.py
```

| 확인 | 결과 |
|---|---|
| restore | 11개 프로젝트 성공 (exit 0) |
| Release build | 성공, 경고 0 / 오류 0 (exit 0) |
| Domain.Tests | 8 통과 |
| Application.Tests | 16 통과 |
| Features.Tests | 2 통과 |
| Integration.Tests | 4 통과 |
| 전체 테스트 | **30 통과 / 0 실패 / 0 건너뜀** (exit 0) |
| 명세/에셋 검증 | PASS: 86 catalog assets, 7 project specs, 6 modules, 1 event definitions |

TRX 파일은 Git에서 제외된 `TestResults/`에 있습니다. 테스트 러너의 `VSTEST_ARTIFACTS_DIRECTORY` 미지정 메시지는 개인 임시 폴더 사용 안내로 남아 있습니다. 테스트 성공과 Release 빌드 경고 수에는 영향이 없습니다.

실제 WPF 창은 표시하지 않고 STA Dispatcher에서 XAML을 생성하여 버튼 Command 바인딩, 활성화 상태, 시작/중지, 실행 중 창 닫기의 취소 완료를 확인했습니다. 눈으로 확인한 화면 표시나 실제 앱플레이어 검증을 주장하지 않습니다.

초기 무제한 MSBuild 병렬 실행은 이 환경에서 대기 프로세스를 남겨 단일 노드(`-m:1`)로 검증했습니다. 작업에서 설치한 `.tools/dotnet/dotnet.exe`의 잔여 프로세스만 정리했으며 앱플레이어/게임/ADB 프로세스는 조작하지 않았습니다.

## 미구현 및 검증 한계

- Task 02 이후의 캡처 스케줄러, 순차 입력/검증, OpenCvSharp 매칭, 게임 진행 상태 머신.
- 임무/거울의 실제 전환, 별도 네코 10회 뽑기, 성공 후 선택적 광고 2배 보상.
- 날짜 변경/일일 팝업/홈 재진입, JSON 이벤트 검증·실행, 이벤트 중복 방지/일시 중단·재개.
- 설정 저장/편집 UI 및 후속 기능별 상세 의미 검증, CI (Task 10).
- 복구/워치독/모니터링 스레드/게임·ADB·OS 재시작은 구현하지 않았으며 도입하지 않습니다.
- 실제 게임/앱플레이어, 이미지 인식 정확도와 성능, 후속 M/R/N/A/D/E 수락 시나리오는 검증하지 않았습니다. 골격 테스트 통과를 게임 동작 확인으로 해석하면 안 됩니다.

## Task 01 이후 폴더·문서 정리

프로젝트·기능·설계·패키지 변경 없이 문서와 Git 제외 규칙만 정리했습니다. 최종 폴더 트리는 [루트 README](../README.md#폴더-구성)에 있습니다.

| 이전 위치 | 정리 후 |
|---|---|
| 루트 `START_HERE.md` + `README_CODEX_FIRST.md` | [docs/START_HERE.md](START_HERE.md)로 통합, 중복 안내 제거 |
| 루트 `CHANGELOG_v0.1_FINAL_ARCHITECTURE.md` | [docs/changelog/CHANGELOG_v0.1_FINAL_ARCHITECTURE.md](changelog/CHANGELOG_v0.1_FINAL_ARCHITECTURE.md) |
| 루트 `CHANGELOG_v0.1_SCOPE_UPDATE.md` | [docs/changelog/CHANGELOG_v0.1_SCOPE_UPDATE.md](changelog/CHANGELOG_v0.1_SCOPE_UPDATE.md) |

루트의 `AGENTS.md`, `README.md`, `.gitignore`, `docs/10_ARCHITECTURE.md`, `tasks/00_first_codex_message.txt` 및 이 문서에서 경로·안내를 갱신했습니다. 변경 이력은 역사적 기록임을 명시하고 현재 Task 01 상태로 연결했습니다. 기존 설계 문서의 루트 기준 경로 표기는 유지하며 이동한 문서의 Markdown 링크는 실제 위치 기준으로 수정했습니다.

`.gitignore`는 `.tools/`, 모든 프로젝트의 `bin/`·`obj/`, `TestResults/`, 빌드 산출물, 로컬 IDE 상태, 사용자 설정·로그·Python 캐시를 제외합니다. `tools/validate_spec.py`는 계속 Git 관리 대상입니다. 생성물의 물리적 삭제는 수행하지 않았습니다.

본체 7개·테스트 4개 프로젝트, 루트 빌드 설정 5개, 원본 에셋, v2.0.6 참조 자료, 기계 판독 명세와 검증 스크립트는 그대로 유지합니다. Task 02 기능은 추가하지 않았습니다.

정리 후 2026-11-11 (한국 시간), 동일한 로컬 .NET SDK 10.0.404로 위 restore/build/test 및 명세 검증 명령을 다시 실행했습니다.

| 재검증 | 결과 |
|---|---|
| restore | 성공, 모든 프로젝트 최신 상태 (exit 0) |
| Release build | 성공, 경고 0 / 오류 0 (exit 0) |
| 전체 테스트 | **30 통과 / 0 실패 / 0 건너뜀**: Domain 8, Application 16, Features 2, Integration 4 (exit 0) |
| 명세·에셋 검증 | PASS: 86 catalog assets, 7 project specs, 6 modules, 1 event definitions |
| 보존 확인 | 소스·빌드 설정·명세·에셋·참조 등 190개 파일의 작업 전후 SHA-256 동일 |
| 문서 링크 | 로컬 Markdown 링크 50개 정상 |
| 프로젝트 참조 | 본체 7개 + 테스트 4개 유지, 아키텍처 참조 방향 일치 |
| Git 제외 | 생성물/로컬 파일 제외, 추적 중인 생성물 없음, `tools/validate_spec.py` 추적 유지 |

기존 테스트 러너의 개인 임시 폴더 사용 안내 메시지는 그대로이며 실패는 없습니다. 이번 정리에서 새로운 테스트나 외부 패키지를 추가하지 않았습니다.
