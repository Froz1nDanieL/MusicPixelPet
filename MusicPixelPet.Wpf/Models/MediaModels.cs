namespace MusicPixelPet.Wpf.Models;

public enum PlaybackStatus
{
    Playing,
    Paused,
    Stopped,
    Unknown
}

public enum MusicVibe
{
    Silence,
    AmbientOrClassical,
    AcousticOrFolk,
    RnbOrSoul,
    Pop,
    RockOrMetal,
    ElectronicOrHipHop
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

public record struct SpectrumData
{
    public const int MfccLength = 13;

    public static SpectrumData CreateEmpty()
    {
        return new SpectrumData(0, 0, 0, 0, new float[MfccLength], 0, 0, 0);
    }

    public SpectrumData(float rms, float bass, float mid, float high, float[] mfcc)
        : this(rms, bass, mid, high, mfcc, 0, 0, 0)
    {
    }

    public SpectrumData(
        float rms,
        float bass,
        float mid,
        float high,
        float[] mfcc,
        float centroid,
        float flux,
        float rolloff)
    {
        Rms = rms;
        Bass = bass;
        Mid = mid;
        High = high;
        Mfcc = mfcc;
        Centroid = centroid;
        Flux = flux;
        Rolloff = rolloff;
    }

    public float Rms { get; set; }
    public float Bass { get; set; }
    public float Mid { get; set; }
    public float High { get; set; }
    public float[] Mfcc { get; set; }
    public float Centroid { get; set; }
    public float Flux { get; set; }
    public float Rolloff { get; set; }
}

public sealed class BeatEventArgs : EventArgs
{
    public BeatEventArgs(float level, DateTimeOffset detectedAt, float bpm)
    {
        Update(level, detectedAt, bpm);
    }

    public float Level { get; private set; }
    public DateTimeOffset DetectedAt { get; private set; }
    public float Bpm { get; private set; }

    internal void Update(float level, DateTimeOffset detectedAt, float bpm)
    {
        Level = level;
        DetectedAt = detectedAt;
        Bpm = bpm;
    }
}
