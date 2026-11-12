using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using NyangShinobi.App.Updates;

namespace NyangShinobi.Integration.Tests;

public sealed class UpdateReleaseTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    [Theory]
    [InlineData("0.1.0-rc.2", "0.1.0-rc.10")]
    [InlineData("0.1.0-rc.10", "0.1.0")]
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1")]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.beta")]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.11")]
    [InlineData("1.0.0", "1.0.1-rc.1")]
    [InlineData("99999999999999999999.0.0", "100000000000000000000.0.0")]
    public void SemVerOrdersNumericPrereleaseAndStableCorrectly(string lower, string higher) => Assert.True(SemVersion.Parse(lower).CompareTo(SemVersion.Parse(higher)) < 0);
    [Theory]
    [InlineData("0.1")]
    [InlineData("01.1.0")]
    [InlineData("0.1.0-rc.01")]
    [InlineData("0.1.0-")]
    [InlineData("0.1.0-a..b")]
    [InlineData("v0.1.0")]
    [InlineData("0.1.0\n")]
    public void SemVerRejectsInvalidVersions(string version) => Assert.False(SemVersion.TryParse(version, out _));
    [Fact]
    public void BuildMetadataDoesNotAffectPrecedence() => Assert.Equal(0, SemVersion.Parse("1.0.0+first").CompareTo(SemVersion.Parse("1.0.0+second")));

    [Theory]
    [InlineData(UpdateChannel.Development, "0.1.0-rc.10")]
    [InlineData(UpdateChannel.Stable, null)]
    public async Task DevelopmentIncludesRcAndStableFiltersPrereleases(UpdateChannel channel, string? expected)
    {
        using var client = Client(UpdateTestData.ReleasesJson(("0.1.0-rc.10", true, false), ("0.1.0-rc.3", true, false), ("0.1.0", false, true)));
        Assert.Equal(expected, (await client.CheckAsync("0.1.0-rc.2", channel, Token))?.Version);
    }
    [Fact]
    public async Task StableIsNewerThanRcInBothChannelsAndNoUpdateForSameVersion()
    {
        foreach (var channel in Enum.GetValues<UpdateChannel>())
        {
            using var client = Client(UpdateTestData.ReleasesJson(("0.1.0", false, false), ("0.1.0-rc.10", true, false)));
            Assert.Equal("0.1.0", (await client.CheckAsync("0.1.0-rc.9", channel, Token))?.Version);
            Assert.Null(await client.CheckAsync("0.1.0", channel, Token));
        }
    }
    [Fact]
    public async Task HttpFailureIsObservable()
    {
        using var client = new GitHubUpdateClient(new UpdateTestData.Handler((_, _) => Task.FromResult(UpdateTestData.Response("error", HttpStatusCode.ServiceUnavailable))));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.CheckAsync("0.1.0", UpdateChannel.Development, Token));
    }
    [Theory]
    [InlineData("missing-x64")]
    [InlineData("wrong-repository")]
    public async Task WrongAssetsCannotBeSelectedOrDownloaded(string kind)
    {
        var json = UpdateTestData.ReleasesJson(("0.1.1", false, false));
        json = kind == "missing-x64" ? json.Replace("win-x64", "linux-x64", StringComparison.Ordinal) : json.Replace("Blooming2081", "other-owner", StringComparison.Ordinal);
        using var client = Client(json);
        if (kind == "missing-x64") Assert.Null(await client.CheckAsync("0.1.0", UpdateChannel.Stable, Token));
        else await Assert.ThrowsAsync<InvalidDataException>(() => client.CheckAsync("0.1.0", UpdateChannel.Stable, Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DownloadChecksHashAndSize(bool wrongHash)
    {
        var (bytes, release) = UpdateTestData.Package();
        var digest = wrongHash ? new string('0', 64) : Convert.ToHexString(SHA256.HashData(bytes));
        using var client = new GitHubUpdateClient(new UpdateTestData.Handler((request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal) ? UpdateTestData.Response(digest) : UpdateTestData.Response(bytes))));
        var work = UpdateTestData.DirectoryPath();
        if (wrongHash)
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => client.DownloadAsync(release, work, null, Token));
            Assert.False(File.Exists(Path.Combine(work, "download.zip")));
        }
        else Assert.Equal(bytes, await File.ReadAllBytesAsync(await client.DownloadAsync(release, work, null, Token), Token));
    }
    [Theory]
    [InlineData("http://github.com/file")]
    [InlineData("https://github.com.evil.example/file")]
    [InlineData("https://evil.example/file")]
    public async Task RedirectsCannotBypassHttpsAndHostRules(string address)
    {
        using var client = new GitHubUpdateClient(new UpdateTestData.Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect)
        { Headers = { Location = new Uri(address) } })));
        await Assert.ThrowsAsync<InvalidDataException>(() => client.CheckAsync("0.1.0", UpdateChannel.Stable, Token));
    }
    [Fact]
    public async Task OversizedDownloadIsRejectedBeforeNetworkRequest()
    {
        var calls = 0;
        using var client = new GitHubUpdateClient(new UpdateTestData.Handler((_, _) => { calls++; throw new InvalidOperationException(); }));
        var release = UpdateTestData.Release("0.1.1", GitHubUpdateClient.MaxDownloadBytes + 1);
        await Assert.ThrowsAsync<InvalidDataException>(() => client.DownloadAsync(release, UpdateTestData.DirectoryPath(), null, Token));
        Assert.Equal(0, calls);
    }
    [Fact]
    public async Task DownloadDoesNotDeleteAnExistingUnownedFile()
    {
        var (bytes, release) = UpdateTestData.Package();
        var digest = Convert.ToHexString(SHA256.HashData(bytes));
        using var client = new GitHubUpdateClient(new UpdateTestData.Handler((request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal) ? UpdateTestData.Response(digest) : UpdateTestData.Response(bytes))));
        var work = UpdateTestData.DirectoryPath(); var existing = Path.Combine(work, "download.zip");
        await File.WriteAllTextAsync(existing, "existing personal file", Token);
        await Assert.ThrowsAsync<IOException>(() => client.DownloadAsync(release, work, null, Token));
        Assert.Equal("existing personal file", await File.ReadAllTextAsync(existing, Token));
    }
    [Fact]
    public async Task CancellationRemovesPartialDownloadAndDisposesStream()
    {
        var stream = new CancelableStream();
        var release = UpdateTestData.Release("0.1.1", 1024);
        using var client = new GitHubUpdateClient(new UpdateTestData.Handler((request, _) =>
            Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal) ? UpdateTestData.Response(new string('0', 64)) :
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) })));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var work = UpdateTestData.DirectoryPath();
        var download = client.DownloadAsync(release, work, null, cancellation.Token);
        await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => download);
        Assert.True(stream.Disposed); Assert.False(File.Exists(Path.Combine(work, "download.zip")));
    }
    private static GitHubUpdateClient Client(string json) => new(new UpdateTestData.Handler((_, _) => Task.FromResult(UpdateTestData.Response(json))));
    private sealed class CancelableStream : Stream
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed { get; private set; }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { Started.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); return 0; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
