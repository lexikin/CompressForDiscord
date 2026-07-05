using System;
using System.IO;
using System.Text.Json;
using CompressForDiscord.Models;

namespace CompressForDiscord.Services;

internal interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
    string SettingsFilePath { get; }
}

/// <summary>
/// JSON settings under SpecialFolder.ApplicationData — %APPDATA% on Windows,
/// $XDG_CONFIG_HOME/~/.config on Linux — so one code path covers both.
/// </summary>
internal sealed class SettingsService : ISettingsService
{
    public SettingsService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CompressForDiscord", "settings.json"))
    {
    }

    internal SettingsService(string settingsFilePath) => SettingsFilePath = settingsFilePath;

    public string SettingsFilePath { get; }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize(
                File.ReadAllText(SettingsFilePath), AppJsonContext.Default.AppSettings)
                ?? new AppSettings();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            QuarantineCorruptFile();
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);

        // Atomic-ish: write a sibling temp file, then move over the target.
        string tempPath = SettingsFilePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, AppJsonContext.Default.AppSettings));
        File.Move(tempPath, SettingsFilePath, overwrite: true);
    }

    private void QuarantineCorruptFile()
    {
        try
        {
            File.Move(SettingsFilePath, SettingsFilePath + ".bad", overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Can't quarantine — defaults still apply.
        }
    }
}
