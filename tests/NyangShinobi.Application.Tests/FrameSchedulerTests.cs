using NyangShinobi.Contracts;
using NyangShinobi.Domain;

namespace NyangShinobi.Application.Tests;

public sealed class FrameSchedulerTests
{
    private static readonly CaptureSettings Profile = new(3000, 500, "adb_png", "per_state_not_global");
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static DetectionResult Evidence(ImageFrame frame, ScreenCertainty certainty = ScreenCertainty.Detected) =>
        new(frame.FrameId, "test_target", certainty, [new(frame.FrameId, "synthetic", 1, new(0.5, 0.5))]);
    private static readonly DetectionRequest Expected = new("test_next", new(new(0, 0), new(1, 1)), 0.9);

    [Fact]
    public async Task V01_P01_ConcurrentRequestsWaitThreeSecondsAndShareOneFrame()
    {
        var clock = new ManualClock();
        var capture = new FakeCapture();
        var frames = new FrameScheduler(capture, clock);
        var first = frames.GetNextAsync("selected", Profile, Token);
        var second = frames.GetNextAsync("selected", Profile, Token);
        await clock.WaitForPendingAsync(2);
        clock.AdvanceBy(TimeSpan.FromMilliseconds(2999));
        Assert.Equal(0, capture.Calls);
        clock.AdvanceBy(TimeSpan.FromMilliseconds(1));
        Assert.Same(await first.WaitAsync(TimeSpan.FromSeconds(5), Token), await second.WaitAsync(TimeSpan.FromSeconds(5), Token));
        Assert.Equal(1, capture.Calls);
        var next = frames.GetNextAsync("selected", Profile, Token);
        await clock.WaitForPendingAsync(1);
        clock.AdvanceBy(TimeSpan.FromSeconds(3));
        Assert.Equal(2, (await next.WaitAsync(TimeSpan.FromSeconds(5), Token)).FrameId);
        Assert.Equal(1, capture.MaxConcurrent);
    }

    [Fact]
    public async Task V01_P01_VerificationIsFiveHundredMsAfterInputCompletionAndPreemptsIdle()
    {
        var clock = new ManualClock();
        var capture = new FakeCapture();
        var frames = new FrameScheduler(capture, clock);
        var source = await FirstFrame(frames, clock);
        var input = new FakeInput { Hold = true };
        var vision = new FakeVision();
        var sequencer = new InputSequencer(frames, input, vision);
        var idle = frames.GetNextAsync("selected", Profile, Token);
        await clock.WaitForPendingAsync(1);
        var verification = sequencer.TapAndVerifyAsync("selected", Profile, source, Evidence(source), Expected, Token);
        await input.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        clock.AdvanceBy(TimeSpan.FromSeconds(4));
        Assert.Equal(1, capture.Calls);
        input.Complete.TrySetResult();
        await clock.WaitForPendingAsync(1);
        clock.AdvanceBy(TimeSpan.FromMilliseconds(499));
        Assert.Equal(1, capture.Calls);
        clock.AdvanceBy(TimeSpan.FromMilliseconds(1));
        Assert.Equal(InputOutcome.Verified, await verification.WaitAsync(TimeSpan.FromSeconds(5), Token));
        Assert.Same(vision.LastFrame, await idle.WaitAsync(TimeSpan.FromSeconds(5), Token));
        Assert.Equal(2, capture.Calls);
        Assert.Equal(1, capture.MaxConcurrent);
    }

    [Fact]
    public async Task FailedVerificationHaltsWithoutRetapOrRecapture()
    {
        var clock = new ManualClock();
        var capture = new FakeCapture();
        var frames = new FrameScheduler(capture, clock);
        var source = await FirstFrame(frames, clock);
        var input = new FakeInput();
        var sequencer = new InputSequencer(frames, input, new FakeVision { Certainty = ScreenCertainty.Uncertain });
        var verification = sequencer.TapAndVerifyAsync("selected", Profile, source, Evidence(source), Expected, Token);
        await clock.WaitForPendingAsync(1);
        clock.AdvanceBy(TimeSpan.FromMilliseconds(500));
        Assert.Equal(InputOutcome.NeedsUserAction, await verification.WaitAsync(TimeSpan.FromSeconds(5), Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sequencer.TapAndVerifyAsync("selected", Profile, source, Evidence(source), Expected, Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => frames.GetNextAsync("selected", Profile, Token));
        Assert.Equal(1, input.Calls);
        Assert.Equal(2, capture.Calls);
    }

    [Fact]
    public async Task UnknownOrWrongFrameEvidenceCannotSendInput()
    {
        var clock = new ManualClock();
        var capture = new FakeCapture();
        var frames = new FrameScheduler(capture, clock);
        var source = await FirstFrame(frames, clock);
        var input = new FakeInput();
        var sequencer = new InputSequencer(frames, input, new FakeVision());
        await Assert.ThrowsAsync<InvalidOperationException>(() => sequencer.TapAndVerifyAsync("selected", Profile, source,
            Evidence(source, ScreenCertainty.Uncertain), Expected, Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sequencer.TapAndVerifyAsync("selected", Profile, source,
            Evidence(source) with { FrameId = source.FrameId + 1 }, Expected, Token));
        Assert.Equal(0, input.Calls);
    }

    [Fact]
    public async Task DuplicateQueuedTapCannotReuseOldFrame()
    {
        var clock = new ManualClock();
        var capture = new FakeCapture();
        var frames = new FrameScheduler(capture, clock);
        var source = await FirstFrame(frames, clock);
        var input = new FakeInput();
        var sequencer = new InputSequencer(frames, input, new FakeVision());
        var first = sequencer.TapAndVerifyAsync("selected", Profile, source, Evidence(source), Expected, Token);
        var second = sequencer.TapAndVerifyAsync("selected", Profile, source, Evidence(source), Expected, Token);
        await clock.WaitForPendingAsync(1);
        clock.AdvanceBy(TimeSpan.FromMilliseconds(500));
        Assert.Equal(InputOutcome.Verified, await first.WaitAsync(TimeSpan.FromSeconds(5), Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => second);
        Assert.Equal(1, input.Calls);
    }

    [Fact]
    public async Task V01_P02_StopDuringInputCannotCaptureOrRetap()
    {
        var clock = new ManualClock();
        var capture = new FakeCapture();
        var frames = new FrameScheduler(capture, clock);
        var source = await FirstFrame(frames, clock);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var input = new FakeInput { Hold = true };
        var sequencer = new InputSequencer(frames, input, new FakeVision());
        var run = sequencer.TapAndVerifyAsync("selected", Profile, source, Evidence(source), Expected, cancellation.Token);
        await input.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        await Assert.ThrowsAsync<InvalidOperationException>(() => frames.GetNextAsync("selected", Profile, Token));
        Assert.Equal(1, capture.Calls);
        Assert.Equal(1, input.Calls);
    }

    [Fact]
    public async Task CancelledIdleWaitDoesNotCapture()
    {
        var clock = new ManualClock();
        var capture = new FakeCapture();
        var frames = new FrameScheduler(capture, clock);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var run = frames.GetNextAsync("selected", Profile, cancellation.Token);
        await clock.WaitForPendingAsync(1);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        clock.AdvanceBy(TimeSpan.FromSeconds(10));
        Assert.Equal(0, capture.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WrongVerificationScreenOrFrameCannotCompleteInput(bool wrongScreen)
    {
        var clock = new ManualClock();
        var capture = new FakeCapture();
        var frames = new FrameScheduler(capture, clock);
        var source = await FirstFrame(frames, clock);
        var sequencer = new InputSequencer(frames, new FakeInput(), new FakeVision { WrongScreen = wrongScreen, WrongFrame = !wrongScreen });
        var run = sequencer.TapAndVerifyAsync("selected", Profile, source, Evidence(source), Expected, Token);
        await clock.WaitForPendingAsync(1);
        clock.AdvanceBy(TimeSpan.FromMilliseconds(500));
        Assert.Equal(InputOutcome.NeedsUserAction, await run.WaitAsync(TimeSpan.FromSeconds(5), Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => frames.GetNextAsync("selected", Profile, Token));
    }

    [Fact]
    public async Task CaptureFailureHaltsAndRawIsNotEnabled()
    {
        var clock = new ManualClock();
        var capture = new FakeCapture { Fail = true };
        var frames = new FrameScheduler(capture, clock);
        await Assert.ThrowsAsync<ArgumentException>(() => frames.GetNextAsync("selected", Profile with { Method = "adb_raw" }, Token));
        var run = frames.GetNextAsync("selected", Profile, Token);
        await clock.WaitForPendingAsync(1);
        clock.AdvanceBy(TimeSpan.FromSeconds(3));
        await Assert.ThrowsAsync<IOException>(() => run);
        await Assert.ThrowsAsync<InvalidOperationException>(() => frames.GetNextAsync("selected", Profile, Token));
        Assert.Equal(1, capture.Calls);
    }

    private static async Task<ImageFrame> FirstFrame(FrameScheduler frames, ManualClock clock)
    {
        var run = frames.GetNextAsync("selected", Profile, Token);
        await clock.WaitForPendingAsync(1);
        clock.AdvanceBy(TimeSpan.FromSeconds(3));
        return await run.WaitAsync(TimeSpan.FromSeconds(5), Token);
    }

    private sealed class FakeCapture : IScreenCapture
    {
        public int Calls { get; private set; }
        public int MaxConcurrent { get; private set; }
        public bool Fail { get; init; }
        private int active;
        public async Task<ImageFrame> CaptureAsync(string deviceSerial, CancellationToken cancellationToken)
        {
            MaxConcurrent = Math.Max(MaxConcurrent, ++active);
            try
            {
                Calls++;
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                if (Fail) throw new IOException("fake ADB failure");
                return new ImageFrame(Calls, DateTimeOffset.UnixEpoch, 1, 1, 4, new byte[4]);
            }
            finally { active--; }
        }
    }

    private sealed class FakeInput : IInputController
    {
        public bool Hold { get; init; }
        public int Calls { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Complete { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<InputOutcome> TapAsync(string deviceSerial, NormalizedPoint point, CancellationToken cancellationToken)
        {
            Calls++;
            Started.TrySetResult();
            if (Hold) await Complete.Task.WaitAsync(cancellationToken);
            return InputOutcome.Pending;
        }
        public Task<InputOutcome> SwipeAsync(string serial, NormalizedPoint from, NormalizedPoint to, TimeSpan duration, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class FakeVision : IVisionEngine
    {
        public ScreenCertainty Certainty { get; init; } = ScreenCertainty.Detected;
        public bool WrongScreen { get; init; }
        public bool WrongFrame { get; init; }
        public ImageFrame? LastFrame { get; private set; }
        public DetectionResult Detect(ImageFrame frame, DetectionRequest request)
        {
            LastFrame = frame;
            return Evidence(frame, Certainty) with
            {
                ScreenId = WrongScreen ? "unexpected" : request.ScreenId,
                FrameId = WrongFrame ? frame.FrameId - 1 : frame.FrameId
            };
        }
    }

    private sealed class ManualClock : IGameClock
    {
        private readonly object gate = new();
        private long ticks;
        private readonly List<(long Due, TaskCompletionSource Completion)> waiters = [];
        private TaskCompletionSource changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public DateTimeOffset LocalNow => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());
        public long GetTimestamp() { lock (gate) return ticks; }
        public TimeSpan GetElapsedTime(long startTimestamp) => TimeSpan.FromTicks(GetTimestamp() - startTimestamp);

        public async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (gate)
            {
                waiters.Add((ticks + delay.Ticks, completion));
                var previous = changed;
                changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
                previous.TrySetResult();
            }
            using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
            await completion.Task;
        }

        public void AdvanceBy(TimeSpan time)
        {
            lock (gate)
            {
                ticks += time.Ticks;
                foreach (var waiter in waiters.Where(waiter => waiter.Due <= ticks)) waiter.Completion.TrySetResult();
                waiters.RemoveAll(waiter => waiter.Completion.Task.IsCompleted);
            }
        }

        public async Task WaitForPendingAsync(int count)
        {
            while (true)
            {
                Task signal;
                lock (gate)
                {
                    if (waiters.Count(waiter => !waiter.Completion.Task.IsCompleted) >= count) return;
                    signal = changed.Task;
                }
                await signal.WaitAsync(TimeSpan.FromSeconds(5), Token);
            }
        }
    }
}
