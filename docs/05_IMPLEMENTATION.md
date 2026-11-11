# 구현 규칙 · 인터페이스 · 성능 정책

> **최신 변경:** 아래의 4개 프로젝트 구조는 최초 설계 예시이다. **실제 구현 구조는 `docs/10_ARCHITECTURE.md`와 `data/architecture.v0.1.json`의 7개 프로젝트를 반드시 따른다.** 날짜 변경은 v0.1에 포함하며 신규 이벤트는 `IGameEventHandler`로 분리한다.

## 권장 최소 구조 (역사적 참고, 구현에는 사용하지 않음)
```
src/
  NyangShinobi.App/       # WPF / UI 입력 / MVVM 최소
  NyangShinobi.Core/      # MissionFlow, MirrorFlow, 상태/설정, 게임 진행
  NyangShinobi.Vision/    # OpenCvSharpVisionEngine, 화면 식별
  NyangShinobi.Adb/       # ADB 캡처 및 터치
 tests/                   # xUnit + fixture
```
Core는 WPF와 OpenCV의 구체 타입을 참조하지 않는다. v0.1 초기에는 **C# 단일 언어**로 동작한다.

## 최소 계약 (설계 예시)
```csharp
public enum GameMode { Mission, Mirror, NekoNinja }
public enum ScreenCertainty { Detected, Uncertain, NotFound }
public enum ActionStatus { Verified, Pending, NeedsUserAction }
public record ImageFrame(ReadOnlyMemory<byte> Pixels, int Width, int Height,
                         int Stride, PixelFormat Format, long FrameId);
public record Evidence(string Id, double Score, RectI? Bounds);
public record ScreenDetection(ScreenCertainty Certainty, string ScreenId,
                              IReadOnlyList<Evidence> Evidence);
public interface IVisionEngine {
    ScreenDetection Detect(ImageFrame frame, string expectedState);
}
public interface IScreenCapture {
    Task<ImageFrame> CaptureAsync(CancellationToken ct);
}
public interface IInputController {
    Task TapAsync(int x, int y, CancellationToken ct);
    Task SwipeAsync(int x1, int y1, int x2, int y2, int durationMs, CancellationToken ct);
}
```
여기서 `PixelFormat`, `RectI`는 Core의 자체 값 타입으로 정의한다. 위 코드는 그대로 컴파일되는 완제품이 아닌 계약 예시다.

## 캡처/입력
- 기본 대기 상태는 **3,000ms 후 화면 1회 촬영**. 같은 프레임에서 여러 조건 검사.
- 터치 명령이 **완료된 이후 500ms** 뒤 첫 검증 프레임. 검증 중에는 기본 타이머 일시정지.
- 화면 전환 중 짧은 확인 루프는 200~500ms 수준에서 **해당 단계에 한정**하여 사용 가능. 기존 룰렛/특수 비기 분기처럼 짧은 이벤트는 별도 실측 값으로 설계. 상시 고속 감시 금지.
- 카드 선택 직후 화면 소실만으로 성공 처리하지 않고, 유효한 다음 화면 증거 확인.
- 프레임은 메모리상 단일 소유자가 캐시하며, OpenCV Mat의 네이티브 메모리는 명시적으로 Dispose/재사용.
- ADB PNG(`exec-out screencap -p`)를 최초 작동 기준으로 구현하고, RAW(`exec-out screencap`)는 **측정 후 후보**. RAW는 픽셀 헤더/포맷/stride 검증이 필요.
- 터치는 기본 ADB `shell input tap` 사용. 지속형 Shell은 동일 인터페이스에서 프로파일링 후 도입. 5037 직접 소켓/H.264 등은 제외.

## 네코 닌자·광고 2배 처리
- 네코 닌자 모드는 **별도 실행 모드**, 사용자가 해당 화면을 미리 열어둔 상태에서 시작. 결과 화면과 뽑기 화면 외에는 터치 금지. 10회 뽑기는 재화 소모가 있으므로 UI에서 소비 주의 문구 및 즉시 중지를 제공한다.
- 기존 네코 루프의 50ms 무조건 반복 스크린샷을 복사하지 않는다. 기본 3초 검사/터치 후 0.5초 첫 검증을 적용하고 실제 화면 안정화에 필요할 때에만 임시 단기 관측한다.
- 성공 확정 이후에만 선택적 광고 보상을 분기하고, 광고 전환/복귀 확인 전에 공통 확인 버튼을 누르지 않는다. 클릭만으로 보상이 지급됐다고 기록하지 않는다.

## 실패 정책 (자동 복구 아님)
- 화면/장치/이미지 인식 오류가 발생하면 현재 단계 입력을 멈추고 **사용자에게 알려 수동 조치**.
- 같은 버튼을 단순 시간 간격으로 계속 두드리지 않는다. 상태 근거를 확인한 재시도만 가능.
- 재진입, 실패 결과 닫기, 룰렛 확인, 프리셋 목록 닫힘 검사는 **게임 정상 흐름**이므로 유지.
- 작업 취소 요청 뒤 새 입력 명령을 발행하지 않음.

## 성능 측정
- 기준 PC와 동일 앱플레이어 해상도에서 기존 v2.0.6과 비교.
- 측정값: 평균/최악 캡처시간, 프레임 분석 시간, 클릭-반영 시간, 게임 중 CPU·메모리·FPS 변화, 오검출 수.
- 3초 감시는 **일반 상태의 시작 설정**일 뿐 짧은 이벤트 보장을 뜻하지 않는다. 비기/룰렛 특수 정책은 리플레이와 실전으로 확인할 것.
