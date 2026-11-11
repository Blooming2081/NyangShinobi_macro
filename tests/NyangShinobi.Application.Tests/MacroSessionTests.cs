using NyangShinobi.Domain;

namespace NyangShinobi.Application.Tests;

public sealed class MacroSessionTests
{
    [Fact]
    public async Task StartStopAndRestartOwnOnlyOneMode()
    {
        var session = new MacroSession();
        var run = session.RunAsync(GameMode.Mission, TestContext.Current.CancellationToken);
        try
        {
            Assert.True(session.IsRunning);
            Assert.Equal(GameMode.Mission, session.ActiveMode);
            await Assert.ThrowsAsync<InvalidOperationException>(() => session.RunAsync(GameMode.Mirror, TestContext.Current.CancellationToken));
        }
        finally { session.Stop(); }
        Assert.Equal(FlowOutcome.Stopped, await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.False(session.IsRunning);
        Assert.Null(session.ActiveMode);

        var secondRun = session.RunAsync(GameMode.Mirror, TestContext.Current.CancellationToken);
        session.Stop();
        session.Stop();
        Assert.Equal(FlowOutcome.Stopped, await secondRun.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExternalCancellationEndsSession()
    {
        var session = new MacroSession();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var run = session.RunAsync(GameMode.Mirror, cancellation.Token);
        cancellation.Cancel();
        Assert.Equal(FlowOutcome.Stopped, await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.False(session.IsRunning);
    }

    [Fact]
    public async Task AlreadyCancelledStartDoesNotActivateSession()
    {
        var session = new MacroSession();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            session.RunAsync(GameMode.Mission, new CancellationToken(true)));
        Assert.False(session.IsRunning);
    }
}
