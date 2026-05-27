using System.Text.Json;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace MusicPixelPet.Helper;

// 监听 Windows 系统媒体会话，并向 Electron 输出播放器状态快照。
public sealed class MediaSessionWatcher
{
    // 媒体键触发后，部分播放器会短暂丢失会话；保留旧快照避免 UI 闪断。
    private static readonly TimeSpan CommandGracePeriod = TimeSpan.FromSeconds(3);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly TextWriter _output;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _activeSession;
    // 仅关注项目支持的播放器，避免误读浏览器或其他媒体会话。
    private IReadOnlyList<string> _playerWhitelist = new[] { "cloudmusic", "qqmusic" };
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly CancellationTokenSource _pollingCancellation = new();
    private MediaSnapshot? _lastConnectedSnapshot;
    private DateTimeOffset? _lastCommandIssuedAt;

    public MediaSessionWatcher(TextWriter output)
    {
        _output = output;
    }

    public async Task StartAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        // 会话列表和当前会话变化时主动刷新，减少轮询延迟。
        _manager.SessionsChanged += OnSessionsChanged;
        _manager.CurrentSessionChanged += OnCurrentSessionChanged;

        await WriteAsync(new HelperEvent { Type = "ready" });
        await RefreshAsync();
        _ = PollSnapshotAsync(_pollingCancellation.Token);
    }

    public async Task HandleRequestAsync(HelperRequest request)
    {
        if (request.Type.Equals("configure", StringComparison.OrdinalIgnoreCase))
        {
            // 白名单由主进程配置，Helper 内部统一使用小写匹配。
            _playerWhitelist = (request.PlayerWhitelist ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim().ToLowerInvariant())
                .ToArray();

            await RefreshAsync();
            return;
        }

        if (request.Type.Equals("command", StringComparison.OrdinalIgnoreCase))
        {
            await ExecuteCommandAsync(request.Command, request.Delta);
        }
    }

    private async void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        await RefreshAsync();
    }

    private async void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        await RefreshAsync();
    }

    private async void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        await PublishSnapshotAsync();
    }

    private async void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        await PublishSnapshotAsync();
    }

    private async Task RefreshAsync()
    {
        if (_manager is null)
        {
            await WriteErrorAsync("未能初始化 Windows 媒体会话管理器。");
            return;
        }

        await _refreshLock.WaitAsync();

        try
        {
            // 刷新时重新选择目标会话，并只给当前目标绑定事件。
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

    private GlobalSystemMediaTransportControlsSession? PickTargetSession(GlobalSystemMediaTransportControlsSessionManager manager)
    {
        var sessions = manager.GetSessions().ToArray();
        var currentSession = manager.GetCurrentSession();

        // 优先使用系统当前会话，否则从全部会话中找白名单播放器。
        if (currentSession is not null && IsTargetPlayer(currentSession.SourceAppUserModelId))
        {
            return currentSession;
        }

        return sessions.FirstOrDefault(session => IsTargetPlayer(session.SourceAppUserModelId));
    }

    private bool IsTargetPlayer(string sourceAppId)
    {
        var normalizedSource = sourceAppId.ToLowerInvariant();

        return _playerWhitelist.Any(keyword => normalizedSource.Contains(keyword, StringComparison.OrdinalIgnoreCase));
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

        // 保存最近一次有效快照，用于命令后的短暂断连兜底。
        if (snapshot.Connected)
        {
            _lastConnectedSnapshot = snapshot;
        }

        await WriteAsync(new HelperEvent
        {
            Type = "snapshot",
            Snapshot = snapshot
        });
    }

    private async Task<MediaSnapshot> BuildSnapshotAsync()
    {
        try
        {
            if (_activeSession is null)
            {
                return new MediaSnapshot
                {
                    Connected = false,
                    VolumeLevel = SystemVolumeReader.GetMasterVolumeLevel(),
                    ErrorMessage = "当前没有命中白名单的播放器会话。"
                };
            }

            var mediaProperties = await _activeSession.TryGetMediaPropertiesAsync();
            var playbackInfo = _activeSession.GetPlaybackInfo();
            var sourceAppId = _activeSession.SourceAppUserModelId;
            // SMTC 提供曲目信息和播放状态；系统音量由独立读取器补充。
            var status = ToPlaybackStatus(playbackInfo.PlaybackStatus);
            var title = mediaProperties.Title ?? string.Empty;
            var artist = mediaProperties.Artist ?? string.Empty;
            var album = mediaProperties.AlbumTitle ?? string.Empty;

            return new MediaSnapshot
            {
                Connected = true,
                ActivePlayer = NormalizePlayerName(sourceAppId),
                Status = status,
                VolumeLevel = SystemVolumeReader.GetMasterVolumeLevel(),
                CanPlayPause = playbackInfo.Controls.IsPlayPauseToggleEnabled,
                CanGoNext = playbackInfo.Controls.IsNextEnabled,
                CanGoPrevious = playbackInfo.Controls.IsPreviousEnabled,
                Track = new MediaTrack
                {
                    Title = title,
                    Artist = artist,
                    Album = album,
                    ArtworkDataUrl = await TryReadArtworkAsync(mediaProperties.Thumbnail),
                    SourceAppId = sourceAppId,
                    SourceAppName = NormalizePlayerName(sourceAppId)
                }
            };
        }
        catch (Exception exception)
        {
            return new MediaSnapshot
            {
                Connected = false,
                VolumeLevel = SystemVolumeReader.GetMasterVolumeLevel(),
                ErrorMessage = exception.Message
            };
        }
    }

    private async Task ExecuteCommandAsync(string? command, int? delta)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        if (command.Equals("adjustVolume", StringComparison.OrdinalIgnoreCase))
        {
            VolumeController.AdjustMasterVolume(delta ?? 0);
            // 等系统音量应用后再发布快照，避免 UI 读到旧值。
            await Task.Delay(80);
            await PublishSnapshotAsync();
            return;
        }

        try
        {
            _lastCommandIssuedAt = DateTimeOffset.UtcNow;

            // 播放控制使用系统媒体键，比 SMTC 控制 API 对网易云/QQ 更稳定。
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
            await WriteErrorAsync(exception.Message);
        }
    }

    private async Task WriteErrorAsync(string message)
    {
        await WriteAsync(new HelperEvent
        {
            Type = "error",
            Message = message
        });
    }

    private async Task WriteAsync(HelperEvent payload)
    {
        await _output.WriteLineAsync(JsonSerializer.Serialize(payload, _jsonOptions));
        await _output.FlushAsync();
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

        // 命令刚发出时若会话瞬断，继续展示上一份有效曲目信息。
        return new MediaSnapshot
        {
            Connected = true,
            ActivePlayer = _lastConnectedSnapshot.ActivePlayer,
            Status = _lastConnectedSnapshot.Status,
            Track = _lastConnectedSnapshot.Track,
            VolumeLevel = SystemVolumeReader.GetMasterVolumeLevel(),
            CanPlayPause = _lastConnectedSnapshot.CanPlayPause,
            CanGoNext = _lastConnectedSnapshot.CanGoNext,
            CanGoPrevious = _lastConnectedSnapshot.CanGoPrevious,
            ErrorMessage = null
        };
    }

    private bool IsWithinCommandGracePeriod()
    {
        return _lastCommandIssuedAt is not null && DateTimeOffset.UtcNow - _lastCommandIssuedAt.Value <= CommandGracePeriod;
    }

    private void QueueRefreshesAfterCommand()
    {
        // 不同播放器更新 SMTC 的速度不同，分几次刷新能提高状态同步概率。
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

        // 事件并不总是覆盖进度/状态变化，因此保持低频快照刷新。
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            if (_activeSession is null)
            {
                continue;
            }

            await PublishSnapshotAsync();
        }
    }

    private static string NormalizePlayerName(string sourceAppId)
    {
        var normalized = sourceAppId.ToLowerInvariant();

        if (normalized.Contains("cloudmusic", StringComparison.OrdinalIgnoreCase))
        {
            return "网易云音乐";
        }

        if (normalized.Contains("qqmusic", StringComparison.OrdinalIgnoreCase))
        {
            return "QQ音乐";
        }

        return sourceAppId;
    }

    private static string ToPlaybackStatus(GlobalSystemMediaTransportControlsSessionPlaybackStatus playbackStatus)
    {
        return playbackStatus switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => "playing",
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => "paused",
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => "stopped",
            _ => "unknown"
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
            // 前端直接消费 data URL，避免额外传输临时文件路径。
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
