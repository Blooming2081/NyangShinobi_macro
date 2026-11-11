using NyangShinobi.Contracts;

namespace NyangShinobi.Application;

// Task 01은 등록 보관만 제공한다. 우선순위/phase/중복 방지 실행은 Task 09의 책임이다.
public sealed class EventRegistry(IEnumerable<IGameEventHandler> handlers) : IEventRegistry
{
    public IReadOnlyList<IGameEventHandler> Handlers { get; } = Array.AsReadOnly(handlers.ToArray());
}
