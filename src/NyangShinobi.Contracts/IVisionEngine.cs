using NyangShinobi.Domain;

namespace NyangShinobi.Contracts;

public interface IVisionEngine
{
    DetectionResult Detect(ImageFrame frame, DetectionRequest request);
    // UI 호출자는 이 경로를 사용한다. 동기 Detect는 작업 스레드 전용이다.
    Task<DetectionResult> DetectAsync(ImageFrame frame, DetectionRequest request, CancellationToken cancellationToken) =>
        Task.Run(() => Detect(frame, request), cancellationToken);
}
