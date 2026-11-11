using System.IO;
using System.Text;
using NyangShinobi.Application;
using NyangShinobi.Contracts;
using NyangShinobi.Device.Adb;
using NyangShinobi.Domain;

namespace NyangShinobi.Integration.Tests;

public sealed class AdbTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-s")]
    public async Task InvalidSerialNeverRunsAdb(string serial)
    {
        var fake = new FakeAdb();
        var capture = CreateCapture(fake, out _);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => capture.CaptureAsync(serial, Token));
        Assert.Empty(fake.Commands);
    }

    [Theory]
    [InlineData("")]
    [InlineData("other device\nextra device")]
    [InlineData("selected offline")]
    [InlineData("selected unauthorized")]
    [InlineData("selected device\nselected device")]
    public async Task MissingAmbiguousOrNotReadyTargetNeverReceivesCommand(string devices)
    {
        var fake = new FakeAdb { Devices = devices };
        var capture = CreateCapture(fake, out _);
        await Assert.ThrowsAsync<InvalidOperationException>(() => capture.CaptureAsync("selected", Token));
        Assert.Single(fake.Commands);
        Assert.Equal(new[] { "devices", "-l" }, fake.Commands[0]);
    }

    [Fact]
    public async Task ExplicitSerialAmongSeveralDevicesUsesPngAndNormalizedCoordinates()
    {
        var fake = new FakeAdb { Devices = "other device\nselected device model:fake" };
        var capture = CreateCapture(fake, out var client);
        var frame = await capture.CaptureAsync("selected", Token);
        Assert.Equal(48, frame.Width);
        Assert.Equal(132, frame.Height);
        Assert.Equal(new[] { "-s", "selected", "exec-out", "screencap", "-p" }, fake.Commands[1]);
        var input = new AdbInputController(client);
        Assert.Equal(InputOutcome.Pending, await input.TapAsync("selected", new(0.5, 0.5), Token));
        Assert.Equal(new[] { "-s", "selected", "shell", "input", "tap", "24", "66" }, fake.Commands[3]);
        Assert.Equal(InputOutcome.Pending, await input.SwipeAsync("selected", new(0, 0), new(1, 1), TimeSpan.FromMilliseconds(123), Token));
        Assert.Equal(new[] { "-s", "selected", "shell", "input", "swipe", "0", "0", "47", "131", "123" }, fake.Commands[5]);
    }

    [Fact]
    public async Task ConcurrentDirectCapturesAreSerialized()
    {
        var fake = new FakeAdb();
        var capture = CreateCapture(fake, out _);
        var frames = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => capture.CaptureAsync("selected", Token)));
        Assert.Equal(8, frames.Select(frame => frame.FrameId).Distinct().Count());
        Assert.Equal(1, fake.MaxCaptures);
    }

    [Fact]
    public async Task InputRequiresCaptureAndRechecksDeviceReadiness()
    {
        var fake = new FakeAdb();
        var capture = CreateCapture(fake, out var client);
        var input = new AdbInputController(client);
        await Assert.ThrowsAsync<InvalidOperationException>(() => input.TapAsync("selected", new(0.5, 0.5), Token));
        Assert.Empty(fake.Commands);
        await capture.CaptureAsync("selected", Token);
        fake.Devices = "selected offline";
        await Assert.ThrowsAsync<InvalidOperationException>(() => input.TapAsync("selected", new(0.5, 0.5), Token));
        Assert.DoesNotContain(fake.Commands, command => command.Contains("input"));
    }

    [Fact]
    public async Task StopBeforeInputOrBetweenDiscoveryAndSendPreventsCommand()
    {
        var fake = new FakeAdb();
        var capture = CreateCapture(fake, out var client);
        await capture.CaptureAsync("selected", Token);
        var input = new AdbInputController(client);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => input.TapAsync("selected", new(0.5, 0.5), new(true)));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        fake.AfterDiscovery = cancellation.Cancel;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => input.TapAsync("selected", new(0.5, 0.5), cancellation.Token));
        Assert.DoesNotContain(fake.Commands, command => command.Contains("input"));
    }

    [Fact]
    public async Task NonzeroExitAndUnexpectedInputOutputNeverReportSuccess()
    {
        var fake = new FakeAdb();
        var capture = CreateCapture(fake, out var client);
        await capture.CaptureAsync("selected", Token);
        fake.ExitCode = 1;
        await Assert.ThrowsAsync<IOException>(() => capture.CaptureAsync("selected", Token));
        fake.ExitCode = 0;
        fake.InputOutput = "Error: input rejected";
        await Assert.ThrowsAsync<IOException>(() => new AdbInputController(client).TapAsync("selected", new(0.5, 0.5), Token));
    }

    private static AdbScreenCapture CreateCapture(FakeAdb fake, out AdbDeviceClient client)
    {
        client = new AdbDeviceClient(fake);
        return new(client, new FakeDecoder(), new SystemGameClock());
    }

    private sealed class FakeDecoder : IImageFrameDecoder
    {
        public Task<ImageFrame> DecodePngAsync(ReadOnlyMemory<byte> png, long frameId, DateTimeOffset capturedAt, CancellationToken cancellationToken) =>
            Task.FromResult(new ImageFrame(frameId, capturedAt, 48, 132, 48 * 4, new byte[48 * 132 * 4]));
    }

    private sealed class FakeAdb : IAdbCommandRunner
    {
        public string Devices { get; set; } = "selected device";
        public List<string[]> Commands { get; } = [];
        public Action? AfterDiscovery { get; set; }
        public int ExitCode { get; set; }
        public string InputOutput { get; set; } = "";
        public int MaxCaptures { get; private set; }
        private int captures;
        public async Task<AdbCommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(arguments.ToArray());
            if (arguments[0] == "devices")
            {
                AfterDiscovery?.Invoke();
                return new(Encoding.UTF8.GetBytes("List of devices attached\n" + Devices + "\n"), "", 0);
            }
            if (arguments.Contains("screencap"))
            {
                MaxCaptures = Math.Max(MaxCaptures, ++captures);
                try { await Task.Yield(); return new([1, 2, 3], "fake error", ExitCode); }
                finally { captures--; }
            }
            return new(Encoding.UTF8.GetBytes(InputOutput), "", ExitCode);
        }
    }
}
