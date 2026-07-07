using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CompressForDiscord.Models;
using CompressForDiscord.Services;

namespace CompressForDiscord.ViewModels;

internal sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IUpdateChecker _updateChecker;
    private readonly AppSettings _settings;
    private string? _updateUrl;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate))]
    private string? _updateText;

    public bool HasUpdate => UpdateText is not null;

    public MainWindowViewModel(ISettingsService settingsService, IUpdateChecker updateChecker)
    {
        _settingsService = settingsService;
        _updateChecker = updateChecker;
        _settings = settingsService.Load();
        _customLimitMiB = (decimal)_settings.CustomLimitMiB;
    }

    /// <summary>Fire-and-forget from the UI thread; surfaces a link if a newer release exists.</summary>
    public async Task CheckForUpdatesAsync(CancellationToken ct)
    {
        if (await _updateChecker.CheckAsync(ct) is { } info)
        {
            _updateUrl = info.ReleaseUrl;
            UpdateText = $"Update available: v{info.Version} — click to download";
        }
    }

    [RelayCommand]
    private void OpenUpdate()
    {
        if (_updateUrl is { } url)
        {
            ShellOpener.OpenUrl(url);
        }
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
