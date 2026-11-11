using NyangShinobi.Domain;

namespace NyangShinobi.Contracts;

public interface IVisionEngine
{
    DetectionResult Detect(ImageFrame frame, DetectionRequest request);
}
