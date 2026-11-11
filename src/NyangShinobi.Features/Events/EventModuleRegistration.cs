using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NyangShinobi.Contracts;

namespace NyangShinobi.Features.Events;

public static class EventModuleRegistration
{
    // 정적 DI 등록만 지원한다. Task 01에서는 이벤트/JSON 예시를 활성화하지 않는다.
    public static IServiceCollection AddGameEventHandler<T>(this IServiceCollection services)
        where T : class, IGameEventHandler
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IGameEventHandler, T>());
        return services;
    }
}
