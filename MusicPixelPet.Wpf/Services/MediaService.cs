using MusicPixelPet.Wpf.Models;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace MusicPixelPet.Wpf.Services;

public sealed class MediaService : IDisposable
{
    private static readonly TimeSpan CommandGracePeriod = TimeSpan.FromSeconds(3);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _activeSession;
    private CancellationTokenSource? _pollingCancellation;
    private IReadOnlyList<string> _playerWhitelist = new[] { "cloudmusic", "qqmusic" };
    private MediaSnapshot? _lastConnectedSnapshot;
    private DateTimeOffset? _lastCommandIssuedAt;

    public event EventHandler? Ready;
    public event EventHandler<MediaSnapshot>? SnapshotChanged;
    public event EventHandler<string>? ErrorOccurred;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_manager is not null)
        {
            return;
        }

        _pollingCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.SessionsChanged += OnSessionsChanged;
        _manager.CurrentSessionChanged += OnCurrentSessionChanged;

        Ready?.Invoke(this, EventArgs.Empty);
        await RefreshAsync();
        _ = PollSnapshotAsync(_pollingCancellation.Token);
    }

    public async Task ConfigurePlayersAsync(IEnumerable<string> playerWhitelist)
    {
        _playerWhitelist = playerWhitelist
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .ToArray();

        if (_manager is not null)
        {
            await RefreshAsync();
        }
    }

    public Task TogglePlayPauseAsync()
    {
        return ExecuteCommandAsync("playPause", null);
    }

    public Task NextAsync()
    {
        return ExecuteCommandAsync("next", null);
    }

    public Task PreviousAsync()
    {
        return ExecuteCommandAsync("previous", null);
    }

    public Task AdjustVolumeAsync(int delta)
    {
        return ExecuteCommandAsync("adjustVolume", delta);
    }

    public void Dispose()
    {
        _pollingCancellation?.Cancel();
        _pollingCancellation?.Dispose();

        if (_manager is not null)
        {
            _manager.SessionsChanged -= OnSessionsChanged;
            _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
        }

        DetachSessionEvents(_activeSession);
        _refreshLock.Dispose();
    }

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        _ = RefreshAsync();
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        _ = RefreshAsync();
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        _ = PublishSnapshotAsync();
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        _ = PublishSnapshotAsync();
    }

    private async Task RefreshAsync()
    {
        if (_manager is null)
        {
            ErrorOccurred?.Invoke(this, "Windows media session manager is not initialized.");
            return;
        }

        await _refreshLock.WaitAsync();

        try
        {
            var nextSession = PickTargetSession(_manager);
            if (!ReferenceEquals(_activeSession, nextSession))
            {
                DetachSessionEvents(_activeSession);
                _activeSession = nextSession;
                AttachSessionEvents(_activeSession);
            }

            await PublishSnapshotAsync();
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private GlobalSystemMediaTransportControlsSession? PickTargetSession(
        GlobalSystemMediaTransportControlsSessionManager manager)
    {
        var currentSession = manager.GetCurrentSession();
        if (currentSession is not null && IsTargetPlayer(currentSession.SourceAppUserModelId))
        {
            return currentSession;
        }

        return manager.GetSessions()
            .FirstOrDefault(session => IsTargetPlayer(session.SourceAppUserModelId));
    }

    private bool IsTargetPlayer(string sourceAppId)
    {
        return _playerWhitelist.Any(keyword => sourceAppId.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private void AttachSessionEvents(GlobalSystemMediaTransportControlsSession? session)
    {
        if (session is null)
        {
            return;
        }

        session.PlaybackInfoChanged += OnPlaybackInfoChanged;
        session.MediaPropertiesChanged += OnMediaPropertiesChanged;
    }

    private void DetachSessionEvents(GlobalSystemMediaTransportControlsSession? session)
    {
        if (session is null)
        {
            return;
        }

        session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
    }

    private async Task PublishSnapshotAsync()
    {
        var snapshot = StabilizeSnapshot(await BuildSnapshotAsync());

        if (snapshot.Connected)
        {
            _lastConnectedSnapshot = snapshot;
        }

        SnapshotChanged?.Invoke(this, snapshot);
    }

    private async Task<MediaSnapshot> BuildSnapshotAsync()
    {
        try
        {
            if (_activeSession is null)
            {
                return new MediaSnapshot(
                    Connected: false,
                    ActivePlayer: null,
                    Status: PlaybackStatus.Unknown,
                    Track: null,
                    VolumeLevel: SystemVolumeReader.GetMasterVolumeLevel(),
                    CanPlayPause: false,
                    CanGoNext: false,
                    CanGoPrevious: false,
                    LastUpdatedAt: DateTimeOffset.Now,
                    ErrorMessage: "No whitelisted media player session is active.");
            }

            var mediaProperties = await _activeSession.TryGetMediaPropertiesAsync();
            var playbackInfo = _activeSession.GetPlaybackInfo();
            var sourceAppId = _activeSession.SourceAppUserModelId;

            return new MediaSnapshot(
                Connected: true,
                ActivePlayer: NormalizePlayerName(sourceAppId),
                Status: ToPlaybackStatus(playbackInfo.PlaybackStatus),
                Track: new MediaTrack(
                    Title: mediaProperties.Title ?? string.Empty,
                    Artist: mediaProperties.Artist ?? string.Empty,
                    Album: mediaProperties.AlbumTitle ?? string.Empty,
                    ArtworkDataUrl: await TryReadArtworkAsync(mediaProperties.Thumbnail),
                    SourceAppId: sourceAppId,
                    SourceAppName: NormalizePlayerName(sourceAppId)),
                VolumeLevel: SystemVolumeReader.GetMasterVolumeLevel(),
                CanPlayPause: playbackInfo.Controls.IsPlayPauseToggleEnabled,
                CanGoNext: playbackInfo.Controls.IsNextEnabled,
                CanGoPrevious: playbackInfo.Controls.IsPreviousEnabled,
                LastUpdatedAt: DateTimeOffset.Now,
                ErrorMessage: null);
        }
        catch (Exception exception)
        {
            ErrorOccurred?.Invoke(this, exception.Message);
            return new MediaSnapshot(
                Connected: false,
                ActivePlayer: null,
                Status: PlaybackStatus.Unknown,
                Track: null,
                VolumeLevel: SystemVolumeReader.GetMasterVolumeLevel(),
                CanPlayPause: false,
                CanGoNext: false,
                CanGoPrevious: false,
                LastUpdatedAt: DateTimeOffset.Now,
                ErrorMessage: exception.Message);
        }
    }

    private async Task ExecuteCommandAsync(string command, int? delta)
    {
        if (command.Equals("adjustVolume", StringComparison.OrdinalIgnoreCase))
        {
            VolumeController.AdjustMasterVolume(delta ?? 0);
            await Task.Delay(80);
            await PublishSnapshotAsync();
            return;
        }

        try
        {
            _lastCommandIssuedAt = DateTimeOffset.UtcNow;

            switch (command)
            {
                case "playPause":
                    VolumeController.TogglePlayPause();
                    break;
                case "next":
                    VolumeController.SkipNext();
                    break;
                case "previous":
                    VolumeController.SkipPrevious();
                    break;
            }

            QueueRefreshesAfterCommand();
        }
        catch (Exception exception)
        {
            ErrorOccurred?.Invoke(this, exception.Message);
        }
    }

    private MediaSnapshot StabilizeSnapshot(MediaSnapshot snapshot)
    {
        if (snapshot.Connected)
        {
            return snapshot;
        }

        if (_lastConnectedSnapshot is null || !IsWithinCommandGracePeriod())
        {
            return snapshot;
        }

        return _lastConnectedSnapshot with
        {
            VolumeLevel = SystemVolumeReader.GetMasterVolumeLevel(),
            LastUpdatedAt = DateTimeOffset.Now,
            ErrorMessage = null
        };
    }

    private bool IsWithinCommandGracePeriod()
    {
        return _lastCommandIssuedAt is not null
            && DateTimeOffset.UtcNow - _lastCommandIssuedAt.Value <= CommandGracePeriod;
    }

    private void QueueRefreshesAfterCommand()
    {
        _ = Task.Run(async () =>
        {
            foreach (var delay in new[] { 250, 800, 1600 })
            {
                await Task.Delay(delay);
                await RefreshAsync();
            }
        });
    }

    private async Task PollSnapshotAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (_activeSession is not null)
                {
                    await PublishSnapshotAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static string NormalizePlayerName(string sourceAppId)
    {
        if (sourceAppId.Contains("cloudmusic", StringComparison.OrdinalIgnoreCase))
        {
            return "网易云音乐";
        }

        if (sourceAppId.Contains("qqmusic", StringComparison.OrdinalIgnoreCase))
        {
            return "QQ音乐";
        }

        return sourceAppId;
    }

    private static PlaybackStatus ToPlaybackStatus(GlobalSystemMediaTransportControlsSessionPlaybackStatus status)
    {
        return status switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlaybackStatus.Playing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlaybackStatus.Paused,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => PlaybackStatus.Stopped,
            _ => PlaybackStatus.Unknown
        };
    }

    private static async Task<string?> TryReadArtworkAsync(IRandomAccessStreamReference? thumbnail)
    {
        if (thumbnail is null)
        {
            return null;
        }

        try
        {
            using var stream = await thumbnail.OpenReadAsync();
            using var inputStream = stream.GetInputStreamAt(0);
            using var reader = new DataReader(inputStream);
            await reader.LoadAsync((uint)stream.Size);

            var buffer = new byte[(int)stream.Size];
            reader.ReadBytes(buffer);
            return $"data:image/png;base64,{Convert.ToBase64String(buffer)}";
        }
        catch
        {
            return null;
        }
    }
}
