using System.Text.Json;
using System.Text.Json.Nodes;
using NyangShinobi.Contracts;
using NyangShinobi.Domain;

namespace NyangShinobi.Application.Tests;

public sealed class SettingsTests
{
    private static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "data", "settings.default.v0.1.json");

    [Fact]
    public async Task RepositoryDefaultsParseWithoutChangingPolicies()
    {
        var settings = await new JsonSettingsStore(DefaultPath).LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(GameMode.Mission, settings.GetGameMode());
        Assert.Equal(3000, settings.Capture.DefaultIdleIntervalMs);
        Assert.Equal(500, settings.Capture.PostTapVerifyDelayMs);
        Assert.Equal(new PresetNumbers(2, 1, 10), settings.Mission.AutoPresetNumbers);
        Assert.Equal("비둔", settings.Mirror.Attribute);
        Assert.Equal(7, settings.Mirror.WeekdaySchedule.Count);
        Assert.False(settings.Mirror.WeekdayAuto);
        Assert.Equal(0, settings.NekoNinja.MaxTenPullCycles);
        Assert.True(settings.Mission.DoubleReward.Enabled);
        Assert.True(settings.DateChange.Enabled);
        Assert.True(settings.Events.Enabled);
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("mode")]
    [InlineData("capture")]
    public async Task MissingRequiredPropertyIsRejected(string key)
    {
        var json = JsonNode.Parse(await File.ReadAllTextAsync(DefaultPath, TestContext.Current.CancellationToken))!.AsObject();
        json.Remove(key);
        await WithUserSettings(json, async path =>
            await Assert.ThrowsAsync<JsonException>(() => new JsonSettingsStore(DefaultPath, path).LoadAsync(TestContext.Current.CancellationToken)));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("unknown")]
    [InlineData("wrong_type")]
    [InlineData("missing_nested")]
    public async Task MalformedSettingsDoNotSilentlyFallback(string scenario)
    {
        var json = JsonNode.Parse(await File.ReadAllTextAsync(DefaultPath, TestContext.Current.CancellationToken))!.AsObject();
        switch (scenario)
        {
            case "null": json["capture"] = null; break;
            case "unknown": json["unexpected"] = true; break;
            case "wrong_type": json["schemaVersion"] = "1"; break;
            case "missing_nested": json["safety"]!.AsObject().Remove("allowGameRestart"); break;
        }
        await WithUserSettings(json, async path =>
            await Assert.ThrowsAsync<JsonException>(() => new JsonSettingsStore(DefaultPath, path).LoadAsync(TestContext.Current.CancellationToken)));
    }

    [Theory]
    [InlineData("schemaVersion", "", 2)]
    [InlineData("safety", "allowGameRestart", 1)]
    [InlineData("adb", "autoRecoveryEnabled", 1)]
    [InlineData("capture", "defaultIdleIntervalMs", 0)]
    public async Task UnsupportedVersionOrUnsafeOptionsAreRejected(string section, string key, int value)
    {
        var json = JsonNode.Parse(await File.ReadAllTextAsync(DefaultPath, TestContext.Current.CancellationToken))!.AsObject();
        if (key == "") json[section] = value;
        else if (section == "capture") json[section]![key] = value;
        else json[section]![key] = value != 0;
        await WithUserSettings(json, async path =>
            await Assert.ThrowsAsync<InvalidDataException>(() => new JsonSettingsStore(DefaultPath, path).LoadAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task UserSettingsTakePrecedenceAndDefaultsStayUnchanged()
    {
        var original = await File.ReadAllTextAsync(DefaultPath, TestContext.Current.CancellationToken);
        var json = JsonNode.Parse(original)!.AsObject();
        json["mode"] = "mirror";
        await WithUserSettings(json, async path =>
        {
            var settings = await new JsonSettingsStore(DefaultPath, path).LoadAsync(TestContext.Current.CancellationToken);
            Assert.Equal(GameMode.Mirror, settings.GetGameMode());
        });
        Assert.Equal(original, await File.ReadAllTextAsync(DefaultPath, TestContext.Current.CancellationToken));
    }

    private static async Task WithUserSettings(JsonObject json, Func<string, Task> test)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nyang-settings-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, json.ToJsonString(), TestContext.Current.CancellationToken);
            await test(path);
        }
        finally { File.Delete(path); }
    }
}
