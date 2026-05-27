using System.Collections.ObjectModel;

namespace MusicPixelPet.Wpf.Models;

public enum ControlBarDisplayMode
{
    Hover,
    Always
}

public enum RuleMatchField
{
    Any,
    Title,
    Artist,
    Album
}

public enum RulePetMode
{
    Default,
    Energetic,
    Sleepy
}

public enum PetAnimationId
{
    Idle,
    Playing,
    Paused,
    Sleeping,
    Celebrating
}

public sealed class MusicRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Keyword { get; set; } = string.Empty;
    public RuleMatchField Field { get; set; } = RuleMatchField.Any;
    public RulePetMode Mode { get; set; } = RulePetMode.Default;
}

public sealed class WindowBounds
{
    public double X { get; set; } = 80;
    public double Y { get; set; } = 80;
    public double Width { get; set; } = 300;
    public double Height { get; set; } = 360;
}

public sealed class AppSettings
{
    public string SkinId { get; set; } = "default";
    public bool WheelVolumeEnabled { get; set; } = true;
    public string[] PlayerWhitelist { get; set; } = ["cloudmusic", "qqmusic"];
    public ObservableCollection<MusicRule> MusicRules { get; set; } = [];
    public bool AutoLaunch { get; set; }
    public bool AlwaysOnTop { get; set; } = true;
    public ControlBarDisplayMode ControlBarMode { get; set; } = ControlBarDisplayMode.Hover;
    public WindowBounds WindowBounds { get; set; } = new();

    public static AppSettings CreateDefault()
    {
        return new AppSettings();
    }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            SkinId = SkinId,
            WheelVolumeEnabled = WheelVolumeEnabled,
            PlayerWhitelist = PlayerWhitelist.ToArray(),
            MusicRules = new ObservableCollection<MusicRule>(MusicRules.Select(rule => new MusicRule
            {
                Id = rule.Id,
                Keyword = rule.Keyword,
                Field = rule.Field,
                Mode = rule.Mode
            })),
            AutoLaunch = AutoLaunch,
            AlwaysOnTop = AlwaysOnTop,
            ControlBarMode = ControlBarMode,
            WindowBounds = new WindowBounds
            {
                X = WindowBounds.X,
                Y = WindowBounds.Y,
                Width = WindowBounds.Width,
                Height = WindowBounds.Height
            }
        };
    }
}
