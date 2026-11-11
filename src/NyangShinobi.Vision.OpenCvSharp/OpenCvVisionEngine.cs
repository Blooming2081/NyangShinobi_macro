using NyangShinobi.Contracts;
using NyangShinobi.Domain;

namespace NyangShinobi.Vision.OpenCvSharp;

// Task 02의 구현 경계만 예약한다. 실제 인식/네이티브 DLL 로드는 없다.
public sealed class OpenCvVisionEngine : IVisionEngine
{
    public DetectionResult Detect(ImageFrame frame, DetectionRequest request) =>
        throw new NotSupportedException("화면 인식은 Task 02에서 구현합니다.");
}
