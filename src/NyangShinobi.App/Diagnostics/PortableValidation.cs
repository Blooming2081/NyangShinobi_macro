using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using NyangShinobi.Contracts;
using NyangShinobi.Domain;
using NyangShinobi.Vision.OpenCvSharp;
using NyangShinobi.App.Updates;

namespace NyangShinobi.App.Diagnostics;

public sealed record PortableValidationResult(string Runtime, string Architecture, bool LocalRuntime,
    int Templates, long FrameId, double MatchScore, string Version);

/// <summary>게시 EXE의 오프라인 자가 검사. 실제 ADB·게임 화면·사용자 설정을 사용하지 않는다.</summary>
public static class PortableValidation
{
    public static async Task<PortableValidationResult> VerifyAsync(string contentRoot, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(contentRoot);
        var runtimePath = Path.GetFullPath(RuntimeEnvironment.GetRuntimeDirectory());
        var localRuntime = runtimePath.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
        if (!localRuntime || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new InvalidOperationException("게시 폴더의 자체 포함 x64 런타임이 사용되지 않았습니다.");
        var metadata = BuildMetadata.Read(root);
        var installRoot = PortableLayout.InstallationRoot(root);
        foreach (var file in InstallManifest.Read(installRoot).Files) UpdateFiles.VerifyFile(installRoot, file);

        using var services = new ServiceCollection().AddNyangShinobi(Path.Combine(root, "data", "settings.default.v0.1.json"))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        await services.GetRequiredService<ISettingsStore>().LoadAsync(cancellationToken).ConfigureAwait(false);
        var diagnostics = services.GetRequiredService<CaptureDiagnostics>();
        var assets = await diagnostics.GetAssetsAsync(cancellationToken).ConfigureAwait(false);
        const string templateId = "assets/images_nav/mission.png";
        var asset = assets.Single(item => item.TemplateId == templateId);
        var png = await File.ReadAllBytesAsync(Path.Combine(root, templateId), cancellationToken).ConfigureAwait(false);
        var template = await services.GetRequiredService<IImageFrameDecoder>()
            .DecodePngAsync(png, 1, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        var options = services.GetRequiredService<VisionOptions>();
        const int left = 20, top = 100;
        var frame = await Task.Run(() =>
        {
            if (left + template.Width > options.ReferenceWidth || top + template.Height > options.ReferenceHeight)
                throw new InvalidDataException("자가 검사 템플릿이 기준 프레임에 들어가지 않습니다.");
            var stride = checked(options.ReferenceWidth * 4);
            var pixels = new byte[checked(stride * options.ReferenceHeight)];
            for (var index = 3; index < pixels.Length; index += 4) pixels[index] = 255;
            for (var row = 0; row < template.Height; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                template.Pixels.Span.Slice(row * template.Stride, template.Width * 4)
                    .CopyTo(pixels.AsSpan((row + top) * stride + left * 4, template.Width * 4));
            }
            return new ImageFrame(2, DateTimeOffset.UtcNow, options.ReferenceWidth, options.ReferenceHeight, stride, pixels);
        }, cancellationToken).ConfigureAwait(false);
        var region = new NormalizedRegion(new((double)left / frame.Width, (double)top / frame.Height),
            new((double)(left + template.Width) / frame.Width, (double)(top + template.Height) / frame.Height));
        var match = await diagnostics.MatchAsync(frame, asset, region, cancellationToken).ConfigureAwait(false);
        var evidence = match.Result.Evidence.Single();
        if (match.Result.FrameId != frame.FrameId || evidence.FrameId != frame.FrameId || evidence.Score < 0.999)
            throw new InvalidDataException("게시 파일을 사용한 합성 ROI 매칭 자가 검사에 실패했습니다.");
        return new(RuntimeInformation.FrameworkDescription, RuntimeInformation.ProcessArchitecture.ToString(),
            localRuntime, assets.Count, frame.FrameId, evidence.Score, metadata.Version);
    }
}
