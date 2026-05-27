using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicPixelPet.Wpf.Models;

namespace MusicPixelPet.Wpf.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string skinId = "default";

    [ObservableProperty]
    private bool wheelVolumeEnabled = true;

    [ObservableProperty]
    private string playerWhitelistText = "cloudmusic, qqmusic";

    [ObservableProperty]
    private bool autoLaunch;

    [ObservableProperty]
    private bool alwaysOnTop = true;

    [ObservableProperty]
    private ControlBarDisplayMode controlBarMode = ControlBarDisplayMode.Hover;

    public SettingsViewModel(AppSettings settings)
    {
        Load(settings);
    }

    public event EventHandler<AppSettings>? SaveRequested;
    public event EventHandler? CloseRequested;

    public ObservableCollection<MusicRule> MusicRules { get; } = [];
    public IReadOnlyList<ControlBarDisplayMode> ControlBarModes { get; } = Enum.GetValues<ControlBarDisplayMode>();
    public IReadOnlyList<RuleMatchField> RuleFields { get; } = Enum.GetValues<RuleMatchField>();
    public IReadOnlyList<RulePetMode> RuleModes { get; } = Enum.GetValues<RulePetMode>();

    [RelayCommand]
    private void AddRule()
    {
        MusicRules.Add(new MusicRule());
    }

    [RelayCommand]
    private void RemoveRule(MusicRule? rule)
    {
        if (rule is not null)
        {
            MusicRules.Remove(rule);
        }
    }

    [RelayCommand]
    private void Save()
    {
        SaveRequested?.Invoke(this, ToSettings());
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Load(AppSettings settings)
    {
        SkinId = settings.SkinId;
        WheelVolumeEnabled = settings.WheelVolumeEnabled;
        PlayerWhitelistText = string.Join(", ", settings.PlayerWhitelist);
        AutoLaunch = settings.AutoLaunch;
        AlwaysOnTop = settings.AlwaysOnTop;
        ControlBarMode = settings.ControlBarMode;

        MusicRules.Clear();
        foreach (var rule in settings.MusicRules)
        {
            MusicRules.Add(new MusicRule
            {
                Id = rule.Id,
                Keyword = rule.Keyword,
                Field = rule.Field,
                Mode = rule.Mode
            });
        }
    }

    private AppSettings ToSettings()
    {
        return new AppSettings
        {
            SkinId = SkinId,
            WheelVolumeEnabled = WheelVolumeEnabled,
            PlayerWhitelist = PlayerWhitelistText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => value.ToLowerInvariant())
                .ToArray(),
            MusicRules = new ObservableCollection<MusicRule>(
                MusicRules
                    .Where(rule => !string.IsNullOrWhiteSpace(rule.Keyword))
                    .Select(rule => new MusicRule
                    {
                        Id = rule.Id,
                        Keyword = rule.Keyword.Trim(),
                        Field = rule.Field,
                        Mode = rule.Mode
                    })),
            AutoLaunch = AutoLaunch,
            AlwaysOnTop = AlwaysOnTop,
            ControlBarMode = ControlBarMode
        };
    }
}
