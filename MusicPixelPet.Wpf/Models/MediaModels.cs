namespace MusicPixelPet.Wpf.Models;

public enum PlaybackStatus
{
    Playing,
    Paused,
    Stopped,
    Unknown
}

public sealed record MediaTrack(
    string Title,
    string Artist,
    string Album,
    string? ArtworkDataUrl,
    string SourceAppId,
    string SourceAppName);

public sealed record MediaSnapshot(
    bool Connected,
    string? ActivePlayer,
    PlaybackStatus Status,
    MediaTrack? Track,
    double VolumeLevel,
    bool CanPlayPause,
    bool CanGoNext,
    bool CanGoPrevious,
    DateTimeOffset LastUpdatedAt,
    string? ErrorMessage)
{
    public static MediaSnapshot Disconnected(string? errorMessage = null)
    {
        return new MediaSnapshot(
            Connected: false,
            ActivePlayer: null,
            Status: PlaybackStatus.Unknown,
            Track: null,
            VolumeLevel: 0,
            CanPlayPause: false,
            CanGoNext: false,
            CanGoPrevious: false,
            LastUpdatedAt: DateTimeOffset.Now,
            ErrorMessage: errorMessage);
    }
}

public sealed class BeatEventArgs : EventArgs
{
    public BeatEventArgs(float level, DateTimeOffset detectedAt)
    {
        Level = level;
        DetectedAt = detectedAt;
    }

    public float Level { get; }
    public DateTimeOffset DetectedAt { get; }
}
