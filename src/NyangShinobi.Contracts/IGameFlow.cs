using NyangShinobi.Domain;

namespace NyangShinobi.Contracts;

public sealed record FlowContext(GameSessionSettings Settings, GameState State, ImageFrame Frame, string StepId);

public interface IGameFlow
{
    GameMode Mode { get; }
    Task<FlowOutcome> StepAsync(FlowContext context, CancellationToken cancellationToken);
    void Pause();
    void Resume();
}
