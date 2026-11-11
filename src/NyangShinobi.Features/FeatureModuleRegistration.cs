using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NyangShinobi.Contracts;
using NyangShinobi.Features.Mirror;
using NyangShinobi.Features.Mission;
using NyangShinobi.Features.NekoNinja;

namespace NyangShinobi.Features;

public static class FeatureModuleRegistration
{
    public static IServiceCollection AddFeatureModules(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IGameFlow, MissionFlow>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IGameFlow, MirrorFlow>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IGameFlow, NekoNinjaFlow>());
        return services;
    }
}
