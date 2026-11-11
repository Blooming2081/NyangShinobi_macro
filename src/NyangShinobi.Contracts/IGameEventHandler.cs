using NyangShinobi.Domain;

namespace NyangShinobi.Contracts;

public sealed record EventProbe(ScreenCertainty Certainty, IReadOnlyList<ScreenEvidence> Evidence);
public sealed record EventContext(FlowContext Flow, EventPhase Phase);

public interface IGameEventHandler
{
    string Id { get; }
    int Priority { get; }
    bool IsEnabled(GameSessionSettings settings);
    bool CanRun(GameMode mode, GameState state, EventPhase phase);
    // 재캡처하지 않고 공유 프레임으로만 조사한다.
    ValueTask<EventProbe> ProbeAsync(FlowContext context, CancellationToken cancellationToken);
    Task<FlowOutcome> HandleAsync(EventContext context, CancellationToken cancellationToken);
}
