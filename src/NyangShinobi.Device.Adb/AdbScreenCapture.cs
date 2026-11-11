using NyangShinobi.Contracts;
using NyangShinobi.Domain;

namespace NyangShinobi.Device.Adb;

public sealed class AdbScreenCapture : IScreenCapture
{
    public Task<ImageFrame> CaptureAsync(string deviceSerial, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("ADB 캡처는 Task 02에서 구현합니다.");
    }
}
