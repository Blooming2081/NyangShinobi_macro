using NyangShinobi.Contracts;
using NyangShinobi.Domain;

namespace NyangShinobi.Application;

/// <summary>검출한 중심만 탭하고 새 프레임의 명시된 대상 화면을 검증한다. 자동 재터치는 없다.</summary>
public sealed class InputSequencer(FrameScheduler frames, IInputController input, IVisionEngine vision)
{
    public Task<InputOutcome> TapAndVerifyAsync(string serial, CaptureSettings profile, ImageFrame source,
        DetectionResult evidence, DetectionRequest expected, CancellationToken cancellationToken) =>
        frames.ExecuteInputAsync(serial, profile, source, evidence, expected.ScreenId,
            token => input.TapAsync(serial, evidence.Evidence[0].Center, token),
            (frame, token) => vision.DetectAsync(frame, expected, token), cancellationToken);
}
