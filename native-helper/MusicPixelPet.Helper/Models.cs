namespace MusicPixelPet.Helper;

// Electron 主进程发给 Helper 的请求模型。
public sealed class HelperRequest
{
    public string Type { get; set; } = string.Empty;
    public string[]? PlayerWhitelist { get; set; }
    public string? Command { get; set; }
    public int? Delta { get; set; }
}

// Helper 写回 Electron 主进程的事件模型。
public sealed class HelperEvent
{
    public string Type { get; init; } = string.Empty;
    public MediaSnapshot? Snapshot { get; init; }
    public string? Message { get; init; }
}

// 当前播放器状态快照，字段名会按 camelCase 序列化给前端。
public sealed class MediaSnapshot
{
    public bool Connected { get; init; }
    public string? ActivePlayer { get; init; }
    public string Status { get; init; } = "unknown";
    public MediaTrack? Track { get; init; }
    public double VolumeLevel { get; init; }
    public bool CanPlayPause { get; init; }
    public bool CanGoNext { get; init; }
    public bool CanGoPrevious { get; init; }
    public string LastUpdatedAt { get; init; } = DateTimeOffset.UtcNow.ToString("O");
    public string? ErrorMessage { get; init; }
}

// 当前曲目信息，来源于 Windows SMTC 媒体属性。
public sealed class MediaTrack
{
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string Album { get; init; } = string.Empty;
    public string? ArtworkDataUrl { get; init; }
    public string SourceAppId { get; init; } = string.Empty;
    public string SourceAppName { get; init; } = string.Empty;
}
