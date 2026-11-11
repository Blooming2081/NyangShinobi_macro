namespace NyangShinobi.Contracts;

public interface IEventRegistry
{
    IReadOnlyList<IGameEventHandler> Handlers { get; }
}
