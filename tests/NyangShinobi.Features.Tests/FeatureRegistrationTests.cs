using Microsoft.Extensions.DependencyInjection;
using NyangShinobi.Application;
using NyangShinobi.Contracts;
using NyangShinobi.Domain;
using NyangShinobi.Features.Events;

namespace NyangShinobi.Features.Tests;

public sealed class FeatureRegistrationTests
{
    [Fact]
    public async Task SkeletonModesRequestUserActionInsteadOfAdvancingM01R01N01()
    {
        using var services = new ServiceCollection().AddFeatureModules().BuildServiceProvider();
        var settings = await new JsonSettingsStore(Path.Combine(AppContext.BaseDirectory,
            "data", "settings.default.v0.1.json")).LoadAsync(TestContext.Current.CancellationToken);
        var frame = new ImageFrame(1, DateTimeOffset.UtcNow, 1, 1, 4, new byte[4]);
        foreach (var flow in services.GetServices<IGameFlow>())
        {
            var stepId = flow.Mode switch { GameMode.Mission => "M01", GameMode.Mirror => "R01", _ => "N01" };
            var context = new FlowContext(settings, GameState.Unknown, frame, stepId);
            Assert.Equal(FlowOutcome.NeedsUserAction, await flow.StepAsync(context, TestContext.Current.CancellationToken));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                flow.StepAsync(context, new CancellationToken(true)));
        }
    }

    [Fact]
    public void EventCanBeAddedAndRemovedWithoutChangingModeRegistrations()
    {
        var services = new ServiceCollection().AddFeatureModules();
        var baseline = services.Where(service => service.ServiceType == typeof(IGameFlow)).ToArray();
        services.AddGameEventHandler<ExampleEvent>();
        services.AddGameEventHandler<ExampleEvent>();
        using var withEvent = services.BuildServiceProvider();
        Assert.Single(withEvent.GetServices<IGameEventHandler>());
        Assert.Equal(baseline, services.Where(service => service.ServiceType == typeof(IGameFlow)).ToArray());
        using var withoutEvent = new ServiceCollection().AddFeatureModules().BuildServiceProvider();
        Assert.Empty(withoutEvent.GetServices<IGameEventHandler>());
        Assert.Equal(3, withoutEvent.GetServices<IGameFlow>().Count());
    }

    private sealed class ExampleEvent : IGameEventHandler
    {
        public ExampleEvent() { }
        public string Id => "test_registration_only";
        public int Priority => 0;
        public bool IsEnabled(GameSessionSettings settings) => false;
        public bool CanRun(GameMode mode, GameState state, EventPhase phase) => false;
        public ValueTask<EventProbe> ProbeAsync(FlowContext context, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<FlowOutcome> HandleAsync(EventContext context, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
