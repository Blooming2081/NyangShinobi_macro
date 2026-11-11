using NyangShinobi.Domain;

namespace NyangShinobi.Contracts;

public interface IScreenDetector
{
    string ScreenId { get; }
    DetectionResult Detect(ImageFrame frame);
}
