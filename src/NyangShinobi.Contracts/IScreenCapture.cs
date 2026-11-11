using NyangShinobi.Domain;

namespace NyangShinobi.Contracts;

public interface IScreenCapture
{
    Task<ImageFrame> CaptureAsync(string deviceSerial, CancellationToken cancellationToken);
}
