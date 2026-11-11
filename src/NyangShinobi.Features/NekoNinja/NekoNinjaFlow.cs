using NyangShinobi.Contracts;
using NyangShinobi.Domain;

namespace NyangShinobi.Features.NekoNinja;

// Task 07에서 명시적 선택/화면 검증/소비 정책과 함께 구현한다.
public sealed class NekoNinjaFlow : IGameFlow
{
    public GameMode Mode => GameMode.NekoNinja;
    public void Pause() { }
    public void Resume() { }
    public Task<FlowOutcome> StepAsync(FlowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(FlowOutcome.NeedsUserAction);
    }
}
