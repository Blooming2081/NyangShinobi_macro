using NyangShinobi.Contracts;
using NyangShinobi.Domain;

namespace NyangShinobi.Features.Mission;

// Task 03/04에서 v2.0.6 순서와 stepId를 구현한다. 현재는 실행 불가 골격이다.
public sealed class MissionFlow : IGameFlow
{
    public GameMode Mode => GameMode.Mission;
    public void Pause() { }
    public void Resume() { }
    public Task<FlowOutcome> StepAsync(FlowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(FlowOutcome.NeedsUserAction);
    }
}
