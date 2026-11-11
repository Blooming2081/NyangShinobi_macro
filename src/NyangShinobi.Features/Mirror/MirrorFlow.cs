using NyangShinobi.Contracts;
using NyangShinobi.Domain;

namespace NyangShinobi.Features.Mirror;

// Task 05에서 구현. 기존 거울 화면 순서를 대체하는 상태 전환은 추가하지 않는다.
public sealed class MirrorFlow : IGameFlow
{
    public GameMode Mode => GameMode.Mirror;
    public void Pause() { }
    public void Resume() { }
    public Task<FlowOutcome> StepAsync(FlowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(FlowOutcome.NeedsUserAction);
    }
}
