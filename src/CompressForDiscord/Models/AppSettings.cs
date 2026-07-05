using System;
using System.Text.Json.Serialization;

namespace CompressForDiscord.Models;

public enum SizePreset // public: used in xunit theory signatures
{
    Free10,
    NitroBasic50,
    Nitro500,
    Custom,
}

internal sealed class AppSettings
{
    /// <summary>
    /// Discord's API max_upload_size has historically been binary (e.g. 26,214,400 for the
    /// "25 MB" era), so presets are MiB. Single constant so an empirical correction is one line.
    /// </summary>
    public const long BytesPerUnit = 1024 * 1024;

    public int SchemaVersion { get; set; } = 1;

    public SizePreset Preset { get; set; } = SizePreset.Free10;

    /// <summary>Only meaningful when <see cref="Preset"/> is Custom.</summary>
    public double CustomLimitMiB { get; set; } = 10;

    [JsonIgnore]
    public long EffectiveLimitBytes => Preset switch
    {
        SizePreset.Free10 => 10 * BytesPerUnit,
        SizePreset.NitroBasic50 => 50 * BytesPerUnit,
        SizePreset.Nitro500 => 500 * BytesPerUnit,
        SizePreset.Custom => (long)(Math.Clamp(CustomLimitMiB, 1, 10_000) * BytesPerUnit),
        _ => 10 * BytesPerUnit,
    };
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
