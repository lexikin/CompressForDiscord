using System.Text.Json;
using CompressForDiscord.Models;
using Xunit;

namespace CompressForDiscord.Tests.Unit;

public sealed class AppSettingsTests
{
    [Theory]
    [InlineData(SizePreset.Free10, 10L * 1024 * 1024)]
    [InlineData(SizePreset.NitroBasic50, 50L * 1024 * 1024)]
    [InlineData(SizePreset.Nitro500, 500L * 1024 * 1024)]
    public void Presets_MapToBinaryMiB(SizePreset preset, long expectedBytes)
    {
        var settings = new AppSettings { Preset = preset };

        Assert.Equal(expectedBytes, settings.EffectiveLimitBytes);
    }

    [Fact]
    public void CustomLimit_IsClampedToSaneRange()
    {
        var tooSmall = new AppSettings { Preset = SizePreset.Custom, CustomLimitMiB = 0.001 };
        var tooBig = new AppSettings { Preset = SizePreset.Custom, CustomLimitMiB = 999_999 };

        Assert.Equal(1 * AppSettings.BytesPerUnit, tooSmall.EffectiveLimitBytes);
        Assert.Equal(10_000 * AppSettings.BytesPerUnit, tooBig.EffectiveLimitBytes);
    }

    [Fact]
    public void RoundTrips_ThroughSourceGeneratedJson_WithEnumAsString()
    {
        var settings = new AppSettings { Preset = SizePreset.NitroBasic50, CustomLimitMiB = 25 };

        string json = JsonSerializer.Serialize(settings, AppJsonContext.Default.AppSettings);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettings);

        Assert.Contains("NitroBasic50", json); // enum stored as string for hand-editability
        Assert.Equal(SizePreset.NitroBasic50, back!.Preset);
        Assert.Equal(25, back.CustomLimitMiB);
        Assert.Equal(1, back.SchemaVersion);
    }

    [Fact]
    public void UnknownJsonProperties_AreIgnored()
    {
        const string futureJson = """{ "SchemaVersion": 2, "Preset": "Free10", "SomeFutureKnob": true }""";

        var settings = JsonSerializer.Deserialize(futureJson, AppJsonContext.Default.AppSettings);

        Assert.Equal(2, settings!.SchemaVersion);
    }
}
