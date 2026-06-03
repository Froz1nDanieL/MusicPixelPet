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
        new(MusicVibe.RnbOrSoul, ScoreRnbOrSoul),
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
    public static int VibeCount => VibeRules.Length;

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
        if (features.IsTrueSilence)
        {
            return MusicVibe.Silence;
        }

        var bestVibe = MusicVibe.Pop;
        var bestScore = float.MinValue;

        for (var index = 0; index < VibeRules.Length; index += 1)
        {
            var rule = VibeRules[index];
            if (rule.Vibe == MusicVibe.Silence)
            {
                continue;
            }

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

    public static void ScoreVibes(SpectrumData spectrum, float bpm, VibeScore[] scores)
    {
        var features = MusicFeatures.From(spectrum, bpm);
        var count = Math.Min(scores.Length, VibeRules.Length);
        for (var index = 0; index < count; index += 1)
        {
            var rule = VibeRules[index];
            scores[index] = new VibeScore(rule.Vibe, rule.Score(features));
        }
    }

    public static PetAnimationId MapVibeToAnimation(MusicVibe vibe)
    {
        return vibe switch
        {
            MusicVibe.Silence => PetAnimationId.Sleeping,
            MusicVibe.AmbientOrClassical => PetAnimationId.Idle,
            MusicVibe.AcousticOrFolk => PetAnimationId.Idle,
            MusicVibe.RnbOrSoul => PetAnimationId.Playing,
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
        return features.IsTrueSilence
            ? 100
            : Math.Max(0, 24 * (1 - SmoothStep(0.002f, 0.012f, features.Rms)));
    }

    private static float ScoreAmbientOrClassical(MusicFeatures features)
    {
        return 8
            + TempoScore(features.Bpm, 0, 88, 20)
            + InverseScore(features.BassRatio, 0.18f, 0.48f, 18)
            + TargetScore(features.MidRatio + features.HighRatio, 0.72f, 0.30f, 18)
            + TargetScore(features.Brightness, 0.42f, 0.34f, 16)
            + TargetScore(features.Airiness, 0.45f, 0.36f, 14)
            + InverseScore(features.FluxActivity, 0.12f, 0.62f, 18)
            + InverseScore(features.Rms, 0.025f, 0.12f, 8);
    }

    private static float ScoreAcousticOrFolk(MusicFeatures features)
    {
        return 8
            + TempoScore(features.Bpm, 58, 118, 20)
            + TargetScore(features.MidRatio, 0.48f, 0.24f, 24)
            + InverseScore(features.BassRatio, 0.24f, 0.54f, 14)
            + TargetScore(features.Brightness, 0.34f, 0.28f, 12)
            + InverseScore(features.Airiness, 0.36f, 0.78f, 10)
            + InverseScore(features.FluxActivity, 0.18f, 0.70f, 12)
            + TargetScore(features.MfccMidRatio, 0.34f, 0.22f, 10);
    }

    private static float ScoreRnbOrSoul(MusicFeatures features)
    {
        return 12
            + TempoScore(features.Bpm, 50, 122, 22)
            + TargetScore(features.BassRatio, 0.38f, 0.22f, 16)
            + TargetScore(features.MidRatio, 0.42f, 0.24f, 22)
            + InverseScore(features.HighRatio, 0.28f, 0.58f, 12)
            + TargetScore(features.Brightness, 0.28f, 0.24f, 14)
            + TargetScore(features.Airiness, 0.32f, 0.28f, 10)
            + TargetScore(features.FluxActivity, 0.24f, 0.20f, 12)
            + TargetScore(features.BassDominance, 1.25f, 0.80f, 10)
            + TargetScore(features.MfccCentroid, 0.38f, 0.24f, 10)
            + InverseScore(features.MfccHighRatio, 0.24f, 0.54f, 10)
            + (features.SoulfulTexture ? 10 : 0)
            - (features.BoomBapTexture ? 18 : 0)
            - SmoothStep(0.52f, 0.82f, features.FluxActivity) * 14;
    }

    private static float ScorePop(MusicFeatures features)
    {
        return 10
            + TempoScore(features.Bpm, 82, 148, 22)
            + TargetScore(features.BassRatio, 0.34f, 0.22f, 16)
            + TargetScore(features.MidRatio, 0.38f, 0.22f, 16)
            + TargetScore(features.HighRatio, 0.28f, 0.20f, 14)
            + TargetScore(features.Brightness, 0.42f, 0.30f, 12)
            + TargetScore(features.FluxActivity, 0.42f, 0.32f, 12)
            + features.Balance * 12;
    }

    private static float ScoreRockOrMetal(MusicFeatures features)
    {
        return 8
            + TempoScore(features.Bpm, 112, 220, 18)
            + TargetScore(features.MidRatio + features.HighRatio, 0.72f, 0.28f, 22)
            + TargetScore(features.HighRatio, 0.34f, 0.24f, 14)
            + TargetScore(features.Brightness, 0.58f, 0.32f, 16)
            + TargetScore(features.Airiness, 0.66f, 0.30f, 14)
            + TargetScore(features.FluxActivity, 0.58f, 0.34f, 12)
            + SmoothStep(0.028f, 0.12f, features.Rms) * 10;
    }

    private static float ScoreElectronicOrHipHop(MusicFeatures features)
    {
        return 8
            + Math.Max(TempoScore(features.Bpm, 72, 104, 16), TempoScore(features.Bpm, 105, 185, 18))
            + TargetScore(features.BassRatio, 0.54f, 0.24f, 18)
            + TargetScore(features.BassDominance, 1.70f, 1.20f, 14)
            + TargetScore(features.FluxActivity, 0.66f, 0.36f, 22)
            + TargetScore(features.Brightness, 0.44f, 0.42f, 12)
            + TargetScore(features.Airiness, 0.52f, 0.42f, 12)
            + TargetScore(features.MfccLowRatio, 0.46f, 0.28f, 8)
            + (features.BoomBapTexture ? 28 : 0)
            - (features.SoulfulTexture && !features.BoomBapTexture ? 12 : 0);
    }

    private static float TargetScore(float value, float target, float tolerance, float maxScore)
    {
        var distance = MathF.Abs(value - target);
        var normalized = Math.Clamp(1 - distance / tolerance, 0, 1);
        return normalized * maxScore;
    }

    private static float InverseScore(float value, float start, float end, float maxScore)
    {
        return (1 - SmoothStep(start, end, value)) * maxScore;
    }

    private static float TempoScore(float bpm, float min, float max, float maxScore)
    {
        if (bpm <= 0)
        {
            return min <= 0 ? maxScore * 0.7f : 0;
        }

        if (bpm >= min && bpm <= max)
        {
            return maxScore;
        }

        var center = (min + max) * 0.5f;
        var halfWidth = Math.Max((max - min) * 0.5f, 1);
        var distance = MathF.Abs(bpm - center);
        return Math.Clamp(1 - (distance - halfWidth) / 28f, 0, 1) * maxScore;
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private readonly record struct VibeRule(MusicVibe Vibe, Func<MusicFeatures, float> Score);

    public readonly record struct VibeScore(MusicVibe Vibe, float Score);

    private readonly record struct MusicFeatures(
        float Rms,
        float BassRatio,
        float MidRatio,
        float HighRatio,
        float TotalEnergy,
        float Bpm,
        float BassDominance,
        float Balance,
        bool MfccAvailable,
        float MfccLowRatio,
        float MfccMidRatio,
        float MfccHighRatio,
        float MfccCentroid,
        float Brightness,
        float Airiness,
        float FluxActivity,
        bool IsTrueSilence,
        bool SoulfulTexture,
        bool BoomBapTexture)
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
            var bassDominance = spectrum.Bass / Math.Max(spectrum.Mid + spectrum.High, epsilon);
            var balance = 1 - (maxRatio - minRatio);
            var brightness = Math.Clamp(spectrum.Centroid / 6500f, 0, 1);
            var airiness = Math.Clamp(spectrum.Rolloff / 11000f, 0, 1);
            var fluxActivity = Math.Clamp(spectrum.Flux, 0, 1);
            var mfcc = spectrum.Mfcc;
            var mfccAvailable = mfcc is { Length: >= SpectrumData.MfccLength };
            var mfccLowRatio = 0f;
            var mfccMidRatio = 0f;
            var mfccHighRatio = 0f;
            var mfccCentroid = 0f;

            if (mfccAvailable)
            {
                var lowEnergy = SumAbsMfcc(mfcc, 1, 4);
                var midEnergy = SumAbsMfcc(mfcc, 5, 4);
                var highEnergy = SumAbsMfcc(mfcc, 9, 4);
                var mfccEnergy = Math.Max(lowEnergy + midEnergy + highEnergy, epsilon);
                mfccLowRatio = lowEnergy / mfccEnergy;
                mfccMidRatio = midEnergy / mfccEnergy;
                mfccHighRatio = highEnergy / mfccEnergy;
                mfccCentroid = CalculateMfccCentroid(mfcc, mfccEnergy);
            }

            var soulfulTexture = bpm is >= 52 and <= 118
                && bassDominance < 1.95f
                && midRatio >= 0.28f
                && highRatio <= 0.36f
                && balance >= 0.44f
                && fluxActivity < 0.56f
                && (!mfccAvailable || (mfccMidRatio >= 0.25f && mfccHighRatio <= 0.34f));

            var boomBapTexture = bpm is >= 70 and <= 106
                && fluxActivity >= 0.46f
                && bassRatio >= 0.34f
                && bassDominance >= 0.85f
                && midRatio <= 0.48f
                && brightness <= 0.58f
                && airiness <= 0.72f;

            var isTrueSilence = spectrum.Rms < 0.0008f
                && total < 0.0012f
                && fluxActivity < 0.025f;

            return new MusicFeatures(
                Rms: spectrum.Rms,
                BassRatio: bassRatio,
                MidRatio: midRatio,
                HighRatio: highRatio,
                TotalEnergy: total,
                Bpm: bpm,
                BassDominance: bassDominance,
                Balance: balance,
                MfccAvailable: mfccAvailable,
                MfccLowRatio: mfccLowRatio,
                MfccMidRatio: mfccMidRatio,
                MfccHighRatio: mfccHighRatio,
                MfccCentroid: mfccCentroid,
                Brightness: brightness,
                Airiness: airiness,
                FluxActivity: fluxActivity,
                IsTrueSilence: isTrueSilence,
                SoulfulTexture: soulfulTexture,
                BoomBapTexture: boomBapTexture);
        }

        private static float SumAbsMfcc(float[] mfcc, int startIndex, int count)
        {
            var sum = 0f;
            var endIndex = Math.Min(startIndex + count, SpectrumData.MfccLength);
            for (var index = startIndex; index < endIndex; index += 1)
            {
                sum += MathF.Abs(mfcc[index]);
            }

            return sum;
        }

        private static float CalculateMfccCentroid(float[] mfcc, float totalEnergy)
        {
            var weightedSum = 0f;
            for (var index = 1; index < SpectrumData.MfccLength; index += 1)
            {
                weightedSum += MathF.Abs(mfcc[index]) * index;
            }

            return weightedSum / (totalEnergy * (SpectrumData.MfccLength - 1));
        }
    }
}
