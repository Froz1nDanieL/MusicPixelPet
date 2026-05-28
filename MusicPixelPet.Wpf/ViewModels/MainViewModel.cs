using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicPixelPet.Wpf.Models;
using MusicPixelPet.Wpf.Pet;
using MusicPixelPet.Wpf.Services;
using System.Windows;
using System.Windows.Media;

namespace MusicPixelPet.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly TimeSpan BeatAnimationArmDelay = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan BeatAnimationMinGap = TimeSpan.FromMilliseconds(900);

    private readonly MediaService _mediaService;
    private readonly AudioAnalyzerService _audioAnalyzerService;
    private readonly SettingsService _settingsService;
    private DateTimeOffset _playingAnimationEnteredAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastBeatAnimationAt = DateTimeOffset.MinValue;

    [ObservableProperty]
    private AppSettings settings = AppSettings.CreateDefault();

    [ObservableProperty]
    private MediaSnapshot media = MediaSnapshot.Disconnected();

    [ObservableProperty]
    private bool isReady;

    [ObservableProperty]
    private bool isHovered;

    [ObservableProperty]
    private PetAnimationId currentAnimation = PetAnimationId.Idle;

    [ObservableProperty]
    private ImageSource? petFrame;

    [ObservableProperty]
    private float audioLevel;

    public MainViewModel(MediaService mediaService, AudioAnalyzerService audioAnalyzerService, SettingsService settingsService)
    {
        _mediaService = mediaService;
        _audioAnalyzerService = audioAnalyzerService;
        _settingsService = settingsService;

        _settingsService.SettingsChanged += (_, nextSettings) => RunOnUiThread(() => Settings = nextSettings);
        _mediaService.Ready += (_, _) => RunOnUiThread(() => IsReady = true);
        _mediaService.SnapshotChanged += (_, snapshot) => RunOnUiThread(() => Media = snapshot);
        _audioAnalyzerService.LevelChanged += (_, level) => RunOnUiThread(() => AudioLevel = level);
        _audioAnalyzerService.BeatDetected += (_, _) =>
        {
            RunOnUiThread(() =>
            {
                if (!CanShowBeatAnimation())
                {
                    return;
                }

                _lastBeatAnimationAt = DateTimeOffset.Now;
                CurrentAnimation = PetAnimationId.Celebrating;
                _ = ReturnToPlayingAfterBeatAsync();
            });
        };
    }

    public bool ControlBarVisible => Settings.ControlBarMode == ControlBarDisplayMode.Always || IsHovered;
    public bool HasTrack => Media.Track is not null;
    public string ActivePlayerLabel => Media.ActivePlayer ?? "播放器";
    public string PlaybackLabel => Media.Status == PlaybackStatus.Playing ? "播放中" : "已暂停";
    public string TrackTitle => Media.Track?.Title.Length > 0 ? Media.Track.Title : "未知歌曲";
    public string TrackSubtitle => BuildTrackSubtitle();
    public string StatusTitle => HasTrack ? TrackTitle : "当前没有可用的网易云音乐或 QQ 音乐会话";
    public string StatusSubtitle => HasTrack ? TrackSubtitle : Media.ErrorMessage ?? "启动支持的播放器后，桌宠会自动接管状态显示。";
    public double VolumeLevel => Math.Clamp(Media.VolumeLevel, 0, 1);
    public bool IsPlaying => Media.Status == PlaybackStatus.Playing;

    public async Task InitializeAsync()
    {
        Settings = _settingsService.Load();
        await _mediaService.ConfigurePlayersAsync(Settings.PlayerWhitelist);
        await Task.WhenAll(
            _mediaService.StartAsync(),
            _audioAnalyzerService.StartAsync());
    }

    public async Task SaveSettingsAsync(AppSettings nextSettings)
    {
        nextSettings.WindowBounds = Settings.WindowBounds;
        Settings = _settingsService.Save(nextSettings);
        await _mediaService.ConfigurePlayersAsync(Settings.PlayerWhitelist);
    }

    public void SaveWindowBounds(double left, double top, double width, double height)
    {
        Settings = _settingsService.UpdateWindowBounds(new WindowBounds
        {
            X = left,
            Y = top,
            Width = width,
            Height = height
        });
    }

    partial void OnMediaChanged(MediaSnapshot value)
    {
        CurrentAnimation = PetAnimationRules.Derive(value, Settings.MusicRules);
        OnMediaDependentPropertiesChanged();
    }

    partial void OnSettingsChanged(AppSettings value)
    {
        CurrentAnimation = PetAnimationRules.Derive(Media, value.MusicRules);
        OnPropertyChanged(nameof(ControlBarVisible));
    }

    partial void OnIsHoveredChanged(bool value)
    {
        OnPropertyChanged(nameof(ControlBarVisible));
    }

    partial void OnCurrentAnimationChanged(PetAnimationId value)
    {
        if (value == PetAnimationId.Playing)
        {
            _playingAnimationEnteredAt = DateTimeOffset.Now;
        }
    }

    [RelayCommand]
    private Task PlayPauseAsync()
    {
        return _mediaService.TogglePlayPauseAsync();
    }

    [RelayCommand]
    private Task NextAsync()
    {
        return _mediaService.NextAsync();
    }

    [RelayCommand]
    private Task PreviousAsync()
    {
        return _mediaService.PreviousAsync();
    }

    [RelayCommand]
    private Task AdjustVolumeAsync(int delta)
    {
        return Settings.WheelVolumeEnabled
            ? _mediaService.AdjustVolumeAsync(delta)
            : Task.CompletedTask;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? OpenSettingsRequested;

    private async Task ReturnToPlayingAfterBeatAsync()
    {
        await Task.Delay(380);
        if (Media.Status == PlaybackStatus.Playing)
        {
            CurrentAnimation = PetAnimationRules.Derive(Media, Settings.MusicRules);
        }
    }

    private bool CanShowBeatAnimation()
    {
        var now = DateTimeOffset.Now;
        return Media.Status == PlaybackStatus.Playing
            && CurrentAnimation == PetAnimationId.Playing
            && now - _playingAnimationEnteredAt >= BeatAnimationArmDelay
            && now - _lastBeatAnimationAt >= BeatAnimationMinGap;
    }

    private void OnMediaDependentPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasTrack));
        OnPropertyChanged(nameof(ActivePlayerLabel));
        OnPropertyChanged(nameof(PlaybackLabel));
        OnPropertyChanged(nameof(TrackTitle));
        OnPropertyChanged(nameof(TrackSubtitle));
        OnPropertyChanged(nameof(StatusTitle));
        OnPropertyChanged(nameof(StatusSubtitle));
        OnPropertyChanged(nameof(VolumeLevel));
        OnPropertyChanged(nameof(IsPlaying));
    }

    private string BuildTrackSubtitle()
    {
        var artist = Media.Track?.Artist;
        var album = Media.Track?.Album;

        if (string.IsNullOrWhiteSpace(artist))
        {
            artist = "未知歌手";
        }

        return string.IsNullOrWhiteSpace(album) ? artist : $"{artist} · {album}";
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }
}
