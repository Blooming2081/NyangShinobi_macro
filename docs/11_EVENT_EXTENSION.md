# 신규 기간 한정 이벤트 / 일일 팝업 — 확장 명세

> 요구사항: 날짜 변경 후 새로운 한정 이벤트 UI가 나타나더라도 `MissionFlow`/`MirrorFlow`의 코드를 매번 수정하지 않는다. 이벤트 종료 시 원래 모드를 재개한다. 이벤트 활성화 이전에 템플릿과 다음 화면을 확인해야 한다.

## 두 레벨 확장

**A. 단순 UI 이벤트: JSON + PNG**
- 단순 팝업 '닫기', 확인 후 원래 화면으로 복귀 등 **정해진 안전 동작**만 `data/events/*.json`으로 표현한다.
- 새 C# 코드 없이 이벤트 정의와 템플릿 1~2개 추가 → 스키마/캡처 테스트 → `enabled=true`로 활성화.
- 허용 액션은 `tap_detected` 및 `wait_for_screen`만; `run_shell`, 무제한 좌표 클릭, 조건 없는 연타, 임의 코드 실행, 광고 클릭·재화 소비 금지.
- 전역 모달 검출에 단일 이미지 하나만 쓰는 경우 오검출 위험이 있으므로 위치 제한, 일치율 검증, 다음 화면 확인을 필수로 한다. 게임에서 검증된 스크린샷 fixture 없이는 `enabled=false` 유지.

**B. 복합 이벤트: C# 클래스 추가**
- 분기/선택/보상 수령 조건/다단계 페이지 등은 `Features/Events/BuiltIn/<EventName>/` 폴더에 `IGameEventHandler` 구현 및 `EventModuleRegistration`에 DI 등록.
- 입력/캡처는 공용 인터페이스 사용. 필요 시 `assets/events/<event_id>` 별도 자료, `data/events/<event_id>.json` 옵션 제공.
- 이벤트 처리의 성공/실패를 명시적으로 반환한다. **자동으로 홈 화면 버튼을 추측해 누르지 않는다**.

## 실행 시 개입 위치와 우선순위

```text
FrameScheduler.Capture() — 필요 시 프레임 1장 수신
  ↓
PendingInputVerify / 사용자 Stop 확인
  ↓
DateChangeFlow (날짜 변경 = 최우선 게임 팝업)
  ↓
EventDispatcher (등록되고 enabled인 이벤트만)
   ├─ AfterDateChange: 날짜 변경/일일 팝업 뒤, 홈 안정화 전에 실행
   ├─ GlobalOverlay: 현재 모드와 무관한 고유 팝업
   ├─ BeforeModeStep / AfterModeStep: 해당 단계 한정
   └─ EventNotDetected → 현재 임무·거울·네코 흐름에 프레임 양도
  ↓
활성 IGameFlow 1개가 화면 판단/입력
```

규칙:
1. 사용자 Stop 및 진행 중인 터치 검증을 먼저 처리한 뒤 새 화면 후보 판단. 날짜 팝업은 그 다음 우선.
2. 이벤트는 `modeScope`, `allowedPhases`, 현재 게임 상태, **동일 프레임의 증거**가 맞을 때만 실행한다.
3. 한 프레임에서 두 이벤트가 맞으면 높은 `priority`가 우선. 같은 우선순위는 id 오름차순으로 **결정적으로** 처리하며, 서로 충돌하면 모호 상태로 멈추도록 구성할 수 있다.
4. 이벤트 실행은 원래 `ModeFlow`를 **일시 중단**한다. 화면이 허용된 완료 상태로 바뀐 것이 확인될 때만 원래 Flow를 재개한다.
5. 이벤트 동안 3초 폴링을 무조건 유지하지 않고 단계별 한시적 관찰 구간을 허용한다. 새 캡처 스레드를 따로 만들지 않는다.
6. `eventId + visiblePopupFingerprint + session`으로 중복 터치 방지. 이벤트가 닫힌 후 새 등장했을 때만 별도 처리. 무한 재시도/자동 재입장 금지.
7. Unknown, 미승인 보상 클릭, 이벤트 타임아웃, 다른 화면이 나올 때 `NeedsUserAction`으로 종료.
8. 이벤트는 게임 재화를 소비하거나 광고를 실행하는 명령을 **선언형 JSON으로 정의할 수 없다**. 필요한 경우 명시적 사용자 설정과 전용 C# 모듈 및 테스트를 요구.

## C# 모듈 계약 예시 (API 설계 스케치 — Codex가 Task 09에서 작성)

```csharp
public interface IGameEventHandler
{
    string Id { get; }
    int Priority { get; }
    bool IsEnabled(GameSessionSettings settings);
    bool CanRun(GameMode mode, GameState state, EventPhase phase);

    // Capture()를 호출하지 않고, 공유 프레임에서 검출 결과만 반환한다.
    ValueTask<EventProbe> ProbeAsync(FrameContext frame, CancellationToken ct);

    // 하나의 이벤트 실행이 완료/보류/사용자 조치 필요를 반환한다.
    Task<EventOutcome> HandleAsync(EventContext context, CancellationToken ct);
}
```

정확한 타입 선언/버퍼 소유권은 `docs/13_CONTRACTS_AND_SETTINGS.md`에 따라 구현한다. C# 핸들러는 액션 시작 시점과 종료 시점에 이벤트 ID, 상태 ID, 입력 명령 ID를 기록한다.

## 간단한 신규 이벤트 추가 절차

1. 게임의 실제 신규 이벤트 화면을 확보하고 어떤 위치에 고유 특징과 닫기/확인 버튼이 있는지 확인한다.
2. `assets/events/<event_id>/title.png`, `close.png`를 추가한다(버튼 위치/크기 등 기록).
3. `data/events/example_limited_time_popup.disabled.json`을 복사해서 고유 ID, 허용 모드, phase, ROI, 검증 대상 화면을 기입한다.
4. `data/events/event.v0.1.schema.json` 검사 및 캡처 이미지 정상/오검출 fixture 테스트를 추가한다.
5. `enabled=true`로 변경 후 MuMu와 BlueStacks에서 동일 시나리오를 테스트한다.
6. 이벤트를 제거할 때는 설정 비활성화 후 관련 에셋과 정의만 삭제한다. Mission/Mirror는 수정하지 않는다.

## 예시 JSON이 실제 구현을 뜻하지 않는 이유

`data/events/example_limited_time_popup.disabled.json`은 **이미지를 아직 확보하지 않은 명세 예시**이므로 실행 가능 상태가 아니다. `title.png`와 `close.png`를 게임에서 직접 얻어야 한다. 검증 완료 전 `enabled=true`로 변경하면 로더는 실패로 처리해야 한다.

## 단위 테스트 필수 시나리오

- 모드 불일치, phase 불일치, 이벤트 비활성 → 이벤트 패스 / 원래 흐름 실행.
- 날짜 팝업 + 이벤트가 동시에 검출 → 날짜 우선.
- 두 이벤트 후보 일치 → priority/충돌 정책대로 하나만 처리.
- 이벤트 닫기 버튼 탭 후 500ms에 검증 실패 → 임의 재탭/원래 모드 재개 금지.
- 같은 팝업이 계속 유지 → 중복 누름 금지, 화면 바뀌면 재무장.
- 이벤트 종료 후 → 선택된 모드의 정확한 이전 stepId부터 재개 또는 홈 복귀 계약에 따른 재진입.
- Stop 요청 → 이벤트/모드 모두 추가 입력 0회.
