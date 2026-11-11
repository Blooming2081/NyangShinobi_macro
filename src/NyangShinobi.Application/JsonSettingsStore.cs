using System.Text.Json;
using System.Text.Json.Serialization;
using NyangShinobi.Contracts;

namespace NyangShinobi.Application;

/// <summary>사용자 설정이 있으면 우선하며 잘못된 설정을 조용히 기본값으로 되돌리지 않는다.</summary>
public sealed class JsonSettingsStore(string defaultPath, string? userPath = null) : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    public async Task<GameSessionSettings> LoadAsync(CancellationToken cancellationToken)
    {
        var path = userPath is not null && File.Exists(userPath) ? userPath : defaultPath;
        await using var stream = File.OpenRead(path);
        var settings = await JsonSerializer.DeserializeAsync<GameSessionSettings>(stream, Options, cancellationToken)
            ?? throw new JsonException("설정 루트가 null입니다.");
        SettingsValidation.Validate(settings);
        return settings;
    }
}
