using MusicPixelPet.Wpf.Models;

namespace MusicPixelPet.Wpf.Pet;

public sealed class PetAnimationRules
{
    private static readonly TimeSpan VibeSwitchDebounce = TimeSpan.FromSeconds(3);
    private static readonly VibeRule[] VibeRules =
    [
        new(MusicVibe.Silence, ScoreSilence),
        new(MusicVibe.AmbientOrClassical, ScoreAmbientOrClassical),
        new(MusicVibe.AcousticOrFolk, ScoreAcousticOrFolk),
        new(MusicVibe.Pop, ScorePop),
        new(MusicVibe.RockOrMetal, ScoreRockOrMetal),
        new(MusicVibe.ElectronicOrHipHop, ScoreElectronicOrHipHop)
    ];

    private MusicVibe _currentVibe = MusicVibe.Silence;
    private MusicVibe _candidateVibe = MusicVibe.Silence;
    private DateTimeOffset _candidateSince = DateTimeOffset.MinValue;
    private PetAnimationId _currentAnimation = PetAnimationId.Idle;

    public MusicVibe CurrentVibe => _currentVibe;
    public MusicVibe CandidateVibe => _candidateVibe;

    public PetAnimationId Derive(MediaSnapshot snapshot, SpectrumData spectrum, float bpm)
    {
        var now = DateTimeOffset.Now;
        if (!snapshot.Connected || snapshot.Track is null)
        {
            return CommitImmediate(MusicVibe.Silence, PetAnimationId.Idle, now);
        }

        if (snapshot.Status is PlaybackStatus.Paused or PlaybackStatus.Stopped)
        {
            return CommitImmediate(MusicVibe.Silence, PetAnimationId.Paused, now);
        }

        if (snapshot.Status != PlaybackStatus.Playing)
        {
            return CommitImmediate(MusicVibe.Silence, PetAnimationId.Idle, now);
        }

        var inferredVibe = InferVibe(spectrum, bpm);
        if (inferredVibe == _currentVibe)
        {
            _candidateVibe = inferredVibe;
            _candidateSince = now;
            _currentAnimation = MapVibeToAnimation(_currentVibe);
            return _currentAnimation;
        }

        if (inferredVibe != _candidateVibe)
        {
            _candidateVibe = inferredVibe;
            _candidateSince = now;
            return _currentAnimation;
        }

        if (now - _candidateSince < VibeSwitchDebounce)
        {
            return _currentAnimation;
        }

        _currentVibe = inferredVibe;
        _currentAnimation = MapVibeToAnimation(_currentVibe);
        return _currentAnimation;
    }

    public static MusicVibe InferVibe(SpectrumData spectrum, float bpm)
    {
        var features = MusicFeatures.From(spectrum, bpm);
        var bestVibe = MusicVibe.Silence;
        var bestScore = float.MinValue;

        for (var index = 0; index < VibeRules.Length; index += 1)
        {
            var rule = VibeRules[index];
            var score = rule.Score(features);
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestVibe = rule.Vibe;
        }

        return bestVibe;
    }

    public static PetAnimationId MapVibeToAnimation(MusicVibe vibe)
    {
        return vibe switch
        {
            MusicVibe.Silence => PetAnimationId.Sleeping,
            MusicVibe.AmbientOrClassical => PetAnimationId.Idle,
            MusicVibe.AcousticOrFolk => PetAnimationId.Idle,
            MusicVibe.Pop => PetAnimationId.Playing,
            MusicVibe.RockOrMetal => PetAnimationId.Celebrating,
            MusicVibe.ElectronicOrHipHop => PetAnimationId.Celebrating,
            _ => PetAnimationId.Idle
        };
    }

    private PetAnimationId CommitImmediate(MusicVibe vibe, PetAnimationId animationId, DateTimeOffset now)
    {
        _currentVibe = vibe;
        _candidateVibe = vibe;
        _candidateSince = now;
        _currentAnimation = animationId;
        return _currentAnimation;
    }

    private static float ScoreSilence(MusicFeatures features)
    {
        return AddIf(features.Rms < 0.01f, 80)
            + AddIf(features.TotalEnergy < 0.004f, 50)
            + AddIf(features.Bpm <= 0, 8);
    }

    private static float ScoreAmbientOrClassical(MusicFeatures features)
    {
        return AddIf(features.Rms >= 0.006f, 8)
            + AddIf(features.Bpm is > 0 and < 85, 30)
            + AddIf(features.BassRatio < 0.24f, 28)
            + AddIf(features.MidRatio + features.HighRatio > 0.68f, 24)
            + TargetScore(features.HighRatio, 0.35f, 0.25f, 18)
            + AddIf(features.Rms < 0.08f, 12);
    }

    private static float ScoreAcousticOrFolk(MusicFeatures features)
    {
        return AddIf(features.Bpm is >= 60 and <= 115, 32)
            + AddIf(features.MidRatio > features.BassRatio && features.MidRatio > features.HighRatio, 30)
            + TargetScore(features.MidRatio, 0.50f, 0.28f, 24)
            + AddIf(features.BassRatio < 0.34f, 18)
            + AddIf(features.HighRatio < 0.38f, 10);
    }

    private static float ScorePop(MusicFeatures features)
    {
        return AddIf(features.Bpm is >= 85 and <= 140, 34)
            + TargetScore(features.BassRatio, 0.34f, 0.22f, 22)
            + TargetScore(features.MidRatio, 0.38f, 0.24f, 22)
            + TargetScore(features.HighRatio, 0.28f, 0.22f, 18)
            + AddIf(features.Balance > 0.56f, 18);
    }

    private static float ScoreRockOrMetal(MusicFeatures features)
    {
        return AddIf(features.Bpm >= 120, 32)
            + AddIf(features.MidRatio + features.HighRatio > 0.66f, 32)
            + AddIf(features.HighRatio > 0.25f, 18)
            + AddIf(features.Rms > 0.035f, 18)
            + AddIf(features.BassRatio < 0.50f, 10);
    }

    private static float ScoreElectronicOrHipHop(MusicFeatures features)
    {
        return AddIf(features.BassRatio > 0.52f, 48)
            + AddIf(features.BassRatio > 0.62f, 20)
            + AddIf(features.BassDominance > 1.45f, 24)
            + AddIf(features.Bpm is >= 70 and <= 170, 18)
            + AddIf(features.Rms > 0.025f, 10);
    }

    private static float AddIf(bool condition, float value)
    {
        return condition ? value : 0;
    }

    private static float TargetScore(float value, float target, float tolerance, float maxScore)
    {
        var distance = MathF.Abs(value - target);
        var normalized = Math.Clamp(1 - distance / tolerance, 0, 1);
        return normalized * maxScore;
    }

    private readonly record struct VibeRule(MusicVibe Vibe, Func<MusicFeatures, float> Score);

    private readonly record struct MusicFeatures(
        float Rms,
        float BassRatio,
        float MidRatio,
        float HighRatio,
        float TotalEnergy,
        float Bpm,
        float BassDominance,
        float Balance)
    {
        public static MusicFeatures From(SpectrumData spectrum, float bpm)
        {
            const float epsilon = 0.000001f;
            var total = Math.Max(spectrum.Bass + spectrum.Mid + spectrum.High, epsilon);
            var bassRatio = spectrum.Bass / total;
            var midRatio = spectrum.Mid / total;
            var highRatio = spectrum.High / total;
            var maxRatio = Math.Max(bassRatio, Math.Max(midRatio, highRatio));
            var minRatio = Math.Min(bassRatio, Math.Min(midRatio, highRatio));

            return new MusicFeatures(
                Rms: spectrum.Rms,
                BassRatio: bassRatio,
                MidRatio: midRatio,
                HighRatio: highRatio,
                TotalEnergy: total,
                Bpm: bpm,
                BassDominance: spectrum.Bass / Math.Max(spectrum.Mid + spectrum.High, epsilon),
                Balance: 1 - (maxRatio - minRatio));
        }
    }
}
