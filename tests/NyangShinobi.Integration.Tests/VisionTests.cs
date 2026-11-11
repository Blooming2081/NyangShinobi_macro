using System.Diagnostics.Metrics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using NyangShinobi.Application;
using NyangShinobi.Contracts;
using NyangShinobi.Device.Adb;
using NyangShinobi.Domain;
using NyangShinobi.Vision.OpenCvSharp;
using OpenCvSharp;

namespace NyangShinobi.Integration.Tests;

public sealed class VisionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static readonly DetectionRequest Request = new("test_target", new(new(0, 0), new(1, 1)), 0.95);

    [Fact]
    public async Task PngDecodeReturnsOwnedBgraGeometryAndRejectsInvalidData()
    {
        using var fixture = new Fixture();
        var decoder = new PngFrameDecoder(fixture.Options);
        var png = fixture.PngFrame();
        var frame = await decoder.DecodePngAsync(png, 42, DateTimeOffset.UnixEpoch, Token);
        Assert.Equal(42, frame.FrameId);
        Assert.Equal(64, frame.Width);
        Assert.Equal(64, frame.Height);
        Assert.Equal(256, frame.Stride);
        Assert.Equal(255, frame.Pixels.Span[3]);
        var owned = frame.Pixels.ToArray();
        Array.Clear(png);
        Assert.Equal(owned, frame.Pixels.ToArray());
        await Assert.ThrowsAsync<InvalidDataException>(() => decoder.DecodePngAsync(new byte[48], 1, DateTimeOffset.UnixEpoch, Token));
        var small = new PngFrameDecoder(fixture.Options with { MaxPixels = 1 });
        await Assert.ThrowsAsync<InvalidDataException>(() => small.DecodePngAsync(fixture.PngFrame(), 1, DateTimeOffset.UnixEpoch, Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => decoder.DecodePngAsync(fixture.PngFrame(), 1, DateTimeOffset.UnixEpoch, new(true)));
    }

    [Fact]
    public async Task RoiReturnsScoreNormalizedCenterAndSameFrameEvidence()
    {
        using var fixture = new Fixture();
        var frame = fixture.Frame(padding: 8);
        var result = await fixture.Engine.DetectAsync(frame, Request, Token);
        Assert.Equal(ScreenCertainty.Detected, result.Certainty);
        var evidence = Assert.Single(result.Evidence);
        Assert.Equal(frame.FrameId, result.FrameId);
        Assert.Equal(frame.FrameId, evidence.FrameId);
        Assert.Equal("assets/test.png", evidence.TemplateId);
        Assert.InRange(evidence.Score, 0.999, 1);
        Assert.Equal(new NormalizedPoint(24.0 / 64, 32.0 / 64), evidence.Center);
        var outside = await fixture.Engine.DetectAsync(frame, Request with { Region = new(new(0, 0), new(0.25, 0.25)) }, Token);
        Assert.Equal(ScreenCertainty.NotFound, outside.Certainty);
    }

    [Fact]
    public async Task ScoreOnlyRequestCannotBecomeDetectedAndDoesNotChangeThresholds()
    {
        using var fixture = new Fixture();
        var frame = fixture.Frame();
        var measured = await fixture.Engine.DetectAsync(frame, Request with { MinimumScore = null }, Token);
        Assert.Equal(ScreenCertainty.Uncertain, measured.Certainty);
        Assert.InRange(Assert.Single(measured.Evidence).Score, 0.999, 1);
        Assert.Equal(0.95, Request.MinimumScore);
        Assert.Equal(ScreenCertainty.Detected, (await fixture.Engine.DetectAsync(frame, Request, Token)).Certainty);
    }

    [Fact]
    public async Task TemplateAndDetectionResultsAreCachedButFrameIdsCannotAliasPixels()
    {
        using var fixture = new Fixture();
        var frame = fixture.Frame();
        var first = await fixture.Engine.DetectAsync(frame, Request, Token);
        // 삭제 후에도 처음 검증·로드한 Mat를 재사용한다.
        File.Delete(Path.Combine(fixture.Root, "assets", "test.png"));
        Assert.Same(first, await fixture.Engine.DetectAsync(frame, Request, Token));
        Assert.Equal(1, fixture.Repository.LoadedTemplateCount);
        var different = fixture.Frame(includeTemplate: false); // 같은 frameId이지만 다른 스냅샷
        Assert.Equal(frame.FrameId, different.FrameId);
        Assert.Equal(ScreenCertainty.NotFound, (await fixture.Engine.DetectAsync(different, Request, Token)).Certainty);
    }

    [Fact]
    public async Task TemplateScalingUsesReferenceResolution()
    {
        using var fixture = new Fixture();
        var result = await fixture.Engine.DetectAsync(fixture.Frame(scale: 2), Request, Token);
        Assert.Equal(ScreenCertainty.Detected, result.Certainty);
        Assert.Equal(new NormalizedPoint(24.0 / 64, 32.0 / 64), Assert.Single(result.Evidence).Center);
    }

    [Fact]
    public async Task DownscalingToUniformTemplateCannotCreateFalsePerfectMatch()
    {
        using var fixture = new Fixture(checkerboard: true);
        var pixels = Enumerable.Repeat((byte)127, 32 * 32 * 4).ToArray();
        var frame = new ImageFrame(1, DateTimeOffset.UnixEpoch, 32, 32, 128, pixels);
        var result = await fixture.Engine.DetectAsync(frame, Request, Token);
        Assert.Equal(ScreenCertainty.Uncertain, result.Certainty);
        Assert.Empty(result.Evidence);
    }

    [Fact]
    public async Task NearThresholdIsUncertainAndInvalidRoiDoesNotMatch()
    {
        using var fixture = new Fixture();
        var frame = fixture.Frame(includeTemplate: false);
        var result = await fixture.Engine.DetectAsync(frame, Request, Token);
        var score = Assert.Single(result.Evidence).Score;
        Assert.InRange(score, 0.01, 0.7);
        Assert.Equal(ScreenCertainty.Uncertain, (await fixture.Engine.DetectAsync(frame, Request with { MinimumScore = score + 0.01 }, Token)).Certainty);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Engine.DetectAsync(frame, Request with { Region = new(new(0.8, 0.8), new(0.2, 0.2)) }, Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Engine.DetectAsync(frame, Request with { MinimumScore = double.NaN }, Token));
    }

    [Fact]
    public async Task MissingTamperedAndOutsideAssetPathsFailWithoutPartialCache()
    {
        foreach (var scenario in new[] { "missing", "hash", "path", "catalog" })
        {
            using var fixture = new Fixture();
            var file = Path.Combine(fixture.Root, "assets", "test.png");
            switch (scenario)
            {
                case "missing": File.Delete(file); break;
                case "hash": File.WriteAllBytes(file, [1, 2, 3]); break;
                case "path": fixture.WriteManifest("../outside.png"); break;
                case "catalog": fixture.WriteCatalog("assets/unregistered.png"); break;
            }
            if (scenario == "missing")
                await Assert.ThrowsAsync<FileNotFoundException>(() => fixture.Repository.InitializeAsync(Token));
            else
                await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Repository.InitializeAsync(Token));
            Assert.Equal(0, fixture.Repository.LoadedTemplateCount);
        }
    }

    [Fact]
    public async Task ReferenceScreensAndAmbiguousTemplateIdsAreNotGuessed()
    {
        using (var reference = new Fixture(role: "full_screen_reference"))
        {
            await reference.Repository.InitializeAsync(Token);
            Assert.Equal(0, reference.Repository.LoadedTemplateCount);
            await Assert.ThrowsAsync<InvalidDataException>(() => reference.Engine.DetectAsync(reference.Frame(), Request, Token));
        }
        using var ambiguous = new Fixture(duplicate: true);
        await Assert.ThrowsAsync<InvalidDataException>(() => ambiguous.Engine.DetectAsync(ambiguous.Frame(), Request, Token));
        Assert.Equal(ScreenCertainty.Detected, (await ambiguous.Engine.DetectAsync(ambiguous.Frame(), Request with { TemplateId = "assets/test.png" }, Token)).Certainty);
    }

    [Fact]
    public async Task NativeResourcesRejectUseAfterDispose()
    {
        using var fixture = new Fixture();
        await fixture.Engine.DetectAsync(fixture.Frame(), Request, Token);
        fixture.Engine.Dispose();
        fixture.Repository.Dispose();
        fixture.Engine.Dispose();
        fixture.Repository.Dispose();
        Assert.Equal(0, fixture.Repository.LoadedTemplateCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.Engine.DetectAsync(fixture.Frame(), Request, Token));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.Repository.InitializeAsync(Token));
    }

    [Fact]
    public async Task FakeAdbPngFlowsThroughDecoderAndVisionAndEmitsTimings()
    {
        using var fixture = new Fixture();
        var measured = new System.Collections.Concurrent.ConcurrentBag<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        { if (instrument.Meter.Name == RuntimeDiagnostics.MeterName) current.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) =>
        { Assert.True(value >= 0); measured.Add(instrument.Name); });
        listener.Start();
        var fake = new PngAdb(fixture.PngFrame());
        var client = new AdbDeviceClient(fake);
        var capture = new AdbScreenCapture(client, new PngFrameDecoder(fixture.Options), new SystemGameClock());
        var frame = await capture.CaptureAsync("selected", Token);
        Assert.Equal(ScreenCertainty.Detected, (await fixture.Engine.DetectAsync(frame, Request, Token)).Certainty);
        Assert.Contains("capture.duration", measured);
        Assert.Contains("vision.match.duration", measured);
        Assert.Contains("vision.detect.duration", measured);
    }

    [Fact]
    public async Task AllRepositoryAssetsPassInitialManifestAndDecodeValidation()
    {
        using var repository = new TemplateRepository(AppContext.BaseDirectory, new VisionOptions());
        await repository.InitializeAsync(Token);
        Assert.Equal(79, repository.LoadedTemplateCount); // required 82 중 전체 화면 참조 3개 제외
    }

    private sealed class PngAdb(byte[] png) : IAdbCommandRunner
    {
        public Task<AdbCommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
            Task.FromResult(arguments[0] == "devices"
                ? new AdbCommandResult("List of devices attached\nselected device\n"u8.ToArray(), "", 0)
                : new AdbCommandResult(png, "", 0));
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"NyangVision-{Guid.NewGuid():N}"));
        public VisionOptions Options { get; } = new() { ReferenceWidth = 64, ReferenceHeight = 64 };
        public TemplateRepository Repository { get; }
        public OpenCvVisionEngine Engine { get; }
        private readonly byte[] template = new byte[8 * 8 * 3];
        private readonly bool duplicate;
        private readonly string role;

        public Fixture(string role = "button", bool duplicate = false, bool checkerboard = false)
        {
            this.role = role;
            this.duplicate = duplicate;
            Directory.CreateDirectory(Path.Combine(Root, "data"));
            Directory.CreateDirectory(Path.Combine(Root, "assets"));
            new Random(17).NextBytes(template);
            if (checkerboard)
                for (var y = 0; y < 8; y++)
                    for (var x = 0; x < 8; x++)
                        for (var channel = 0; channel < 3; channel++) template[(y * 8 + x) * 3 + channel] = (byte)(((x + y) % 2) * 254);
            using var mat = MatFromBytes(8, 8, template);
            Cv2.ImEncode(".png", mat, out var png);
            File.WriteAllBytes(Path.Combine(Root, "assets", "test.png"), png);
            if (duplicate) File.WriteAllBytes(Path.Combine(Root, "assets", "second.png"), png);
            WriteManifest("assets/test.png");
            WriteCatalog("assets/test.png");
            Repository = new TemplateRepository(Root, Options);
            Engine = new OpenCvVisionEngine(Repository, Options);
        }

        public void WriteManifest(string path)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Root, "assets", "test.png"));
            object Entry(string name) => new { path = name, status = "required", included = true,
                sizeBytes = bytes.Length, sha256 = Convert.ToHexString(SHA256.HashData(bytes)), screenId = "test_target", role };
            var entries = new List<object> { Entry(path) };
            if (duplicate) entries.Add(Entry("assets/second.png"));
            File.WriteAllText(Path.Combine(Root, "data", "asset_manifest.v0.1.json"), JsonSerializer.Serialize(new { assets = entries }));
        }

        public void WriteCatalog(string path) => File.WriteAllText(Path.Combine(Root, "data", "screen_catalog.v0.1.json"),
            JsonSerializer.Serialize(new { screenElements = new[] { new { path } } }));

        public ImageFrame Frame(int scale = 1, bool includeTemplate = true, int padding = 0)
        {
            var width = 64 * scale;
            var stride = width * 4 + padding;
            var pixels = new byte[stride * width];
            new Random(9).NextBytes(pixels);
            for (var y = 0; y < width; y++)
                for (var x = 0; x < width; x++) pixels[y * stride + x * 4 + 3] = 255;
            if (includeTemplate)
            {
                using var original = MatFromBytes(8, 8, template);
                using var resized = new Mat();
                Cv2.Resize(original, resized, new Size(8 * scale, 8 * scale), 0, 0, InterpolationFlags.Linear);
                var data = new byte[8 * scale * 8 * scale * 3];
                Marshal.Copy(resized.Data, data, 0, data.Length);
                for (var y = 0; y < 8 * scale; y++)
                    for (var x = 0; x < 8 * scale; x++)
                        Array.Copy(data, (y * 8 * scale + x) * 3, pixels, (y + 28 * scale) * stride + (x + 20 * scale) * 4, 3);
            }
            return new ImageFrame(77, DateTimeOffset.UnixEpoch, width, width, stride, pixels);
        }

        public byte[] PngFrame()
        {
            var frame = Frame();
            var bgr = new byte[64 * 64 * 3];
            for (var index = 0; index < 64 * 64; index++) frame.Pixels.Span.Slice(index * 4, 3).CopyTo(bgr.AsSpan(index * 3, 3));
            using var mat = MatFromBytes(64, 64, bgr);
            Cv2.ImEncode(".png", mat, out var png);
            return png;
        }

        private static Mat MatFromBytes(int width, int height, byte[] bytes)
        {
            var mat = new Mat(height, width, MatType.CV_8UC3);
            Marshal.Copy(bytes, 0, mat.Data, bytes.Length);
            return mat;
        }

        public void Dispose()
        {
            Engine.Dispose();
            Repository.Dispose();
            // 임의 경로를 지우지 않는다. 이 fixture가 생성한 절대 임시 경로만 정리한다.
            var prefix = Path.GetFullPath(Path.GetTempPath()) + "NyangVision-";
            if (!Root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("잘못된 fixture 경로");
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
