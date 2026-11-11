# C# 공통 계약, 설정, 컴포지션

> 구조 설계를 Codex에게 전달하는 계약서. 다음 코드는 **타입/흐름 예시**이며 이 ZIP에 컴파일 가능한 실행 매크로가 들어 있음을 의미하지 않는다.

## 필수 인터페이스

| 인터페이스 | 소유 위치 | 구현 책임 |
|---|---|---|
| `IVisionEngine` | Contracts | 명시적 대상 화면/ROI의 프레임 판별, 결과 반환. Mat 외부 노출 금지 |
| `IScreenCapture` | Contracts | 1회 캡처, 픽셀 버퍼 수명·해상도·stride 제공 |
| `IInputController` | Contracts | tap/swipe 비동기 실행, device serial 지정, 취소 지원 |
| `IGameFlow` | Contracts | `Mode`, `StepAsync`, `Pause/Resume`, 완료·사용자 조치 상태 |
| `IGameEventHandler` | Contracts | `ProbeAsync`, `HandleAsync`, priority/모드/phase, 완료 상태 |
| `IGameClock` | Contracts | 검증 가능한 Windows 시간, monotonic 타이머/테스트 가짜 시계 |
| `ISettingsStore` | Contracts | 버전 지정된 설정 로드/유효성 검사/저장 |

### 추천 계약의 의미

```csharp
public enum GameMode { Mission, Mirror, NekoNinja }
public enum FlowOutcome { Continue, Completed, PausedByEvent, NeedsUserAction, Stopped }
public enum ScreenCertainty { Detected, Uncertain, NotFound }
public enum EventPhase { GlobalOverlay, AfterDateChange, BeforeModeStep, AfterModeStep }

public readonly record struct NormalizedPoint(double X, double Y);
public sealed record Evidence(string TemplateId, double Score, NormalizedPoint Center);
public sealed record DetectionResult(string ScreenId, ScreenCertainty Certainty,
                                     IReadOnlyList<Evidence> Evidence);
public interface IVisionEngine
{
    DetectionResult Detect(ImageFrame frame, DetectionRequest request);
}
public interface IGameFlow
{
    GameMode Mode { get; }
    Task<FlowOutcome> StepAsync(FlowContext context, CancellationToken ct);
}
```

실제 빌드 시 `ImageFrame`, `DetectionRequest`, `FlowContext` 등의 타입 정의와 수명 계약을 별도 파일에 명시한다. `Core/Domain`에서 OpenCvSharp의 `Mat`, WPF의 `BitmapSource`를 참조하면 아키텍처 위반이다.

## 공용 한 프레임 판정

1. `FrameScheduler`가 필요할 때 ADB 캡처를 1회 요청.
2. 동일 `ImageFrame`과 한 번 계산한 ROI/템플릿 결과를 날짜·이벤트·모드에 공유.
3. 모드는 `CanRun`이 참인 대상만 검사. 이벤트 이미지 전체를 매 프레임 분석하지 않음.
4. `InputSequencer`가 명령을 수행한 후 500ms 대기하여 **새 프레임**으로 다음 상태 검증.
5. 3초 주기 스케줄러는 검증 작업이 진행 중일 때 중복 캡처를 발행하지 않음.

`ImageFrame`은 픽셀형식/stride/width/height/frameId/timestamp 필수, 버퍼 owner의 Dispose 규칙 필수. UI에서 `ImageFrame`을 장시간 보존하거나 매 렌더링마다 Mat 변환 금지.

## 설정 정책

- 기본값 `data/settings.default.v0.1.json`을 임의 변형하지 않고 사용자 설정은 `%LocalAppData%/NyangShinobi/settings.v0.1.json`에 저장한다.
- `settings.schemaVersion`의 타입/범위를 검증; 알 수 없는 필드/이벤트 모듈 ID/누락 템플릿을 사용자에게 알려야 함.
- 이벤트 목록은 `data/events/`의 `.json`을 읽되 `*.disabled.json`은 **항상 비활성**으로 취급. `enabled=true`가 명시되더라도 파일명 disabled면 실행 금지.
- 설정 변경은 모듈별 네임스페이스에서 관리하고 게임 흐름에 하드코딩하지 않음. JSON 실행 코드(`script`/`shell`)는 허용하지 않음.
- 기존 `mission.doubleReward.enabled` / `nekoNinja.maxTenPullCycles` / `mirror.weekdayAuto` 등은 유지.
- 날짜 변경 `dateChange.enabled=true`, `events.enabled=true`는 기본이지만 미검증 예시 이벤트는 disabled.
- 신규 이벤트는 `id`, `priority`, `modeScope`, `allowedPhases`, `trigger`, `steps`, `completion`, `timeoutMs`를 검증한다.

## 모듈 등록 예시

```csharp
// App composition root (개념 예시)
services.AddSingleton<IGameClock, SystemGameClock>();
services.AddSingleton<IScreenCapture, AdbScreenCapture>();
services.AddSingleton<IInputController, AdbInputController>();
services.AddSingleton<IVisionEngine, OpenCvVisionEngine>();
services.AddSingleton<IGameFlow, MissionFlow>();
services.AddSingleton<IGameFlow, MirrorFlow>();
services.AddSingleton<IGameFlow, NekoNinjaFlow>();
services.AddSingleton<IGameEventHandler, DateChangeFlow>(); // 최상위 게임 UI 인터럽트
services.AddSingleton<IEventRegistry, SafeJsonEventRegistry>();
// 새 복합 이벤트는 IGameEventHandler 등록 한 줄 + 독립 폴더 코드로 추가
```

위의 `DateChangeFlow`가 `IGameEventHandler`를 쓰더라도 런타임에서는 **일반 이벤트보다 우선**하는 내장 인터럽트로 처리한다. `IEventRegistry`는 Application/Contracts에 선언한다.

## 진단 로그 최소 필드

`timestamp`, `sessionId`, `deviceSerial`, `mode`, `stepId`, `frameId`, `moduleId`, `screenId`, `confidence`, `inputId`, `outcome`, `elapsedMs`.
디버그를 위해 모든 스크린샷을 저장하거나 전체 매칭 결과를 무제한 출력하지 않는다.
