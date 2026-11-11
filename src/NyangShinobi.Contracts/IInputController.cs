using NyangShinobi.Domain;

namespace NyangShinobi.Contracts;

// 전송 결과 Pending과 화면 검증 결과 Verified는 구분한다.
public interface IInputController
{
    Task<InputOutcome> TapAsync(string deviceSerial, NormalizedPoint point, CancellationToken cancellationToken);
    Task<InputOutcome> SwipeAsync(string deviceSerial, NormalizedPoint from, NormalizedPoint to,
        TimeSpan duration, CancellationToken cancellationToken);
}
