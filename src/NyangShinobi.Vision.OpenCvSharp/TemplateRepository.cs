using System.Security.Cryptography;
using System.Text.Json;
using global::OpenCvSharp;

namespace NyangShinobi.Vision.OpenCvSharp;

public sealed record TemplateDescriptor(string TemplateId, string ScreenId);

/// <summary>최초 사용 시 무결성을 검사하고 원본 템플릿 Mat를 캐시한다. 네이티브 자원의 소유자다.</summary>
public sealed class TemplateRepository(string contentRoot, VisionOptions options) : IDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<string, (string ScreenId, Mat Image)> templates = new(StringComparer.Ordinal);
    private bool loaded;
    private bool disposed;

    public int LoadedTemplateCount { get { lock (gate) return templates.Count; } }

    public async Task<IReadOnlyList<TemplateDescriptor>> GetDescriptorsAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return templates.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new TemplateDescriptor(pair.Key, pair.Value.ScreenId)).ToArray();
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.OperationTimeout);
        try
        {
            await Task.Run(() => { lock (gate) EnsureLoaded(timeout.Token); }, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new TimeoutException("템플릿 최초 로딩 제한 시간을 초과했습니다."); }
    }

    internal T Use<T>(string screenId, string? templateId, Func<string, Mat, T> action, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            EnsureLoaded(cancellationToken);
            var candidates = templates.Where(pair => pair.Value.ScreenId == screenId &&
                (templateId is null || pair.Key == templateId)).ToArray();
            if (candidates.Length != 1)
                throw new InvalidDataException($"화면 {screenId}의 사용 가능한 템플릿이 없거나 모호합니다. 고유 TemplateId를 지정하세요.");
            cancellationToken.ThrowIfCancellationRequested();
            return action(candidates[0].Key, candidates[0].Value.Image);
        }
    }

    private void EnsureLoaded(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (loaded) return;
        options.Validate();
        var root = Path.GetFullPath(contentRoot);
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "data", "asset_manifest.v0.1.json")));
        using var catalog = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "data", "screen_catalog.v0.1.json")));
        var known = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Dictionary<string, (string ScreenId, Mat Image)>(StringComparer.Ordinal);
        try
        {
            foreach (var entry in manifest.RootElement.GetProperty("assets").EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = entry.GetProperty("path").GetString()!;
                if (!known.Add(relative)) throw new InvalidDataException($"중복 에셋: {relative}");
                var status = entry.GetProperty("status").GetString();
                if (!entry.GetProperty("included").GetBoolean())
                {
                    if (status == "required") throw new InvalidDataException($"필수 에셋 누락: {relative}");
                    continue;
                }
                var path = Path.GetFullPath(Path.Combine(root, relative));
                var assetRoot = Path.Combine(root, "assets") + Path.DirectorySeparatorChar;
                if (!path.StartsWith(assetRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("에셋 경로가 assets/ 밖을 가리킵니다.");
                var bytes = File.ReadAllBytes(path);
                if (bytes.LongLength != entry.GetProperty("sizeBytes").GetInt64() ||
                    !Convert.ToHexString(SHA256.HashData(bytes)).Equals(entry.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"에셋 무결성 검사 실패: {relative}");
                // 전체 화면 참조는 그대로 매칭하지 않는다. 별도 검증된 crop 프로필이 필요하다.
                if (status != "required" || entry.GetProperty("role").GetString() is "full_screen_reference" or "reference") continue;
                PngFrameDecoder.ValidatePng(bytes, options.MaxPixels);
                var image = Cv2.ImDecode(bytes, ImreadModes.Color);
                try
                {
                    if (image.Empty()) throw new InvalidDataException($"에셋 디코딩 실패: {relative}");
                    Cv2.MeanStdDev(image, out _, out var deviation);
                    if (deviation.Val0 + deviation.Val1 + deviation.Val2 <= double.Epsilon)
                        throw new InvalidDataException($"단색 템플릿은 정규화 상관 매칭에 사용할 수 없습니다: {relative}");
                    pending.Add(relative, (entry.GetProperty("screenId").GetString()!, image));
                }
                catch { image.Dispose(); throw; }
            }
            foreach (var entry in catalog.RootElement.GetProperty("screenElements").EnumerateArray())
                if (!known.Contains(entry.GetProperty("path").GetString()!))
                    throw new InvalidDataException("카탈로그에 manifest 미등록 에셋이 있습니다.");
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var pair in pending) templates.Add(pair.Key, pair.Value);
            loaded = true;
        }
        catch
        {
            foreach (var value in pending.Values) value.Image.Dispose();
            templates.Clear();
            throw;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            foreach (var value in templates.Values) value.Image.Dispose();
            templates.Clear();
        }
    }
}
