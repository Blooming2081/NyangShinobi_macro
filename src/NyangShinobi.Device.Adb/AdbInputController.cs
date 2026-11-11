using NyangShinobi.Contracts;
using NyangShinobi.Domain;

namespace NyangShinobi.Device.Adb;

// adb 프로세스를 시작하지 않는다. 성공/Pending을 반환하여 입력한 것처럼 보이지 않는다.
public sealed class AdbInputController : IInputController
{
    public Task<InputOutcome> TapAsync(string deviceSerial, NormalizedPoint point, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("ADB 입력은 Task 01 범위 밖입니다.");
    }

    public Task<InputOutcome> SwipeAsync(string deviceSerial, NormalizedPoint from, NormalizedPoint to,
        TimeSpan duration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("ADB 입력은 Task 01 범위 밖입니다.");
    }
}
