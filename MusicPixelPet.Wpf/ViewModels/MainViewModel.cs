using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicPixelPet.Wpf.Models;
using MusicPixelPet.Wpf.Pet;
using MusicPixelPet.Wpf.Services;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace MusicPixelPet.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan SpectrumUiInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan VibeEvaluationInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan BeatUiMinimumGap = TimeSpan.FromMilliseconds(120);

    private readonly MediaService _mediaService;
    private readonly AudioAnalyzerService _audioAnalyzerService;
    private readonly SettingsService _settingsService;
    private readonly PetAnimationRules _petAnimationRules = new();
    private readonly object _audioStateSyncRoot = new();
    private readonly DispatcherTimer _spectrumUiTimer;
    private readonly DispatcherTimer _vibeEvaluationTimer;
    private SpectrumData _latestSpectrum;
    private float _latestBpm;
    private DateTimeOffset _lastBeatUiUpdateAt = DateTimeOffset.MinValue;
    private bool _hasPendingSpectrum;

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
    private SpectrumData spectrum;

    [ObservableProperty]
    private float audioLevel;

    [ObservableProperty]
    private float bpm;

    [ObservableProperty]
    private MusicVibe currentVibe = MusicVibe.Silence;

    public MainViewModel(MediaService mediaService, AudioAnalyzerService audioAnalyzerService, SettingsService settingsService)
    {
        _mediaService = mediaService;
        _audioAnalyzerService = audioAnalyzerService;
        _settingsService = settingsService;
        _spectrumUiTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = SpectrumUiInterval
        };
        _spectrumUiTimer.Tick += (_, _) => FlushSpectrumToUi();

        _vibeEvaluationTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = VibeEvaluationInterval
        };
        _vibeEvaluationTimer.Tick += (_, _) => RefreshPetAnimation();

        _settingsService.SettingsChanged += (_, nextSettings) => RunOnUiThread(() => Settings = nextSettings);
        _mediaService.Ready += (_, _) => RunOnUiThread(() => IsReady = true);
        _mediaService.SnapshotChanged += (_, snapshot) => RunOnUiThread(() => Media = snapshot);
        _audioAnalyzerService.SpectrumAnalyzed += (_, data) => CacheLatestSpectrum(data);
        _audioAnalyzerService.BeatDetected += (_, beat) => CacheBeat(beat);

        _spectrumUiTimer.Start();
        _vibeEvaluationTimer.Start();
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
        RefreshPetAnimation();
        OnMediaDependentPropertiesChanged();
    }

    partial void OnSettingsChanged(AppSettings value)
    {
        RefreshPetAnimation();
        OnPropertyChanged(nameof(ControlBarVisible));
    }

    partial void OnIsHoveredChanged(bool value)
    {
        OnPropertyChanged(nameof(ControlBarVisible));
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

    public void Dispose()
    {
        _spectrumUiTimer.Stop();
        _vibeEvaluationTimer.Stop();
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

    private void RefreshPetAnimation()
    {
        CurrentAnimation = _petAnimationRules.Derive(Media, Spectrum, Bpm);
        CurrentVibe = _petAnimationRules.CurrentVibe;
    }

    private void CacheLatestSpectrum(SpectrumData data)
    {
        lock (_audioStateSyncRoot)
        {
            _latestSpectrum = data;
            _hasPendingSpectrum = true;
        }
    }

    private void CacheBeat(BeatEventArgs beat)
    {
        lock (_audioStateSyncRoot)
        {
            _latestBpm = beat.Bpm;
        }

        var now = DateTimeOffset.Now;
        if (now - _lastBeatUiUpdateAt < BeatUiMinimumGap)
        {
            return;
        }

        _lastBeatUiUpdateAt = now;
        Application.Current.Dispatcher.BeginInvoke(FlushBeatToUi, DispatcherPriority.Background);
    }

    private void FlushSpectrumToUi()
    {
        SpectrumData nextSpectrum;
        lock (_audioStateSyncRoot)
        {
            if (!_hasPendingSpectrum)
            {
                return;
            }

            nextSpectrum = _latestSpectrum;
            _hasPendingSpectrum = false;
        }

        Spectrum = nextSpectrum;
        AudioLevel = nextSpectrum.Rms;
    }

    private void FlushBeatToUi()
    {
        float nextBpm;
        lock (_audioStateSyncRoot)
        {
            nextBpm = _latestBpm;
        }

        Bpm = nextBpm;
        RefreshPetAnimation();
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
