using CommunityToolkit.Mvvm.ComponentModel;
using CompressForDiscord.Models;
using CompressForDiscord.Services;

namespace CompressForDiscord.ViewModels;

internal sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _settings;

    [ObservableProperty]
    private bool _isBusy;

    public MainWindowViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _settings = settingsService.Load();
        _customLimitMiB = (decimal)_settings.CustomLimitMiB;
    }

    public bool IsFree10
    {
        get => _settings.Preset == SizePreset.Free10;
        set { if (value) SetPreset(SizePreset.Free10); }
    }

    public bool IsNitroBasic50
    {
        get => _settings.Preset == SizePreset.NitroBasic50;
        set { if (value) SetPreset(SizePreset.NitroBasic50); }
    }

    public bool IsNitro500
    {
        get => _settings.Preset == SizePreset.Nitro500;
        set { if (value) SetPreset(SizePreset.Nitro500); }
    }

    public bool IsCustom
    {
        get => _settings.Preset == SizePreset.Custom;
        set { if (value) SetPreset(SizePreset.Custom); }
    }

    [ObservableProperty]
    private decimal _customLimitMiB;

    partial void OnCustomLimitMiBChanged(decimal value)
    {
        _settings.CustomLimitMiB = (double)value;
        _settingsService.Save(_settings);
    }

    private void SetPreset(SizePreset preset)
    {
        if (_settings.Preset == preset)
        {
            return;
        }

        _settings.Preset = preset;
        _settingsService.Save(_settings);
        OnPropertyChanged(nameof(IsFree10));
        OnPropertyChanged(nameof(IsNitroBasic50));
        OnPropertyChanged(nameof(IsNitro500));
        OnPropertyChanged(nameof(IsCustom));
    }
}
