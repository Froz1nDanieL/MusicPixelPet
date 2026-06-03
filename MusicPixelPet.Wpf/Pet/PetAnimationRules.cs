using MusicPixelPet.Wpf.Models;

namespace MusicPixelPet.Wpf.Pet;

public sealed class PetAnimationRules
{
    private const float AmbiguousScoreMargin = 0.06f;
    private const float NeutralInstantThreshold = 0.38f;
    private const float StrongInstantThreshold = 0.56f;
    private const int VibeHistoryCapacity = 12;
    private const float NeutralVoteWeight = 0.30f;
    private const float CurrentVibeVoteBias = 0.65f;
    private const float RequiredVoteLead = 0.75f;
    private static readonly TimeSpan VibeHistoryWindow = TimeSpan.FromSeconds(5);
    private static readonly int VibeVoteBucketCount = Enum.GetValues<MusicVibe>().Length;
    private static readonly VibeRule[] VibeRules =
    [
        new(MusicVibe.Silence, ScoreSilence),
        new(MusicVibe.CalmOrchestral, ScoreCalmOrchestral),
        new(MusicVibe.SoftOrganic, ScoreSoftOrganic),
        new(MusicVibe.WarmGroove, ScoreWarmGroove),
        new(MusicVibe.Neutral, ScoreNeutral),
        new(MusicVibe.BrightEnergy, ScoreBrightEnergy),
        new(MusicVibe.BeatDriven, ScoreBeatDriven),
        new(MusicVibe.DarkHeavy, ScoreDarkHeavy)
    ];

    private MusicVibe _currentVibe = MusicVibe.Silence;
    private MusicVibe _candidateVibe = MusicVibe.Silence;
    private PetAnimationId _currentAnimation = PetAnimationId.Idle;
    private readonly VibeHistorySample[] _vibeHistory = new VibeHistorySample[VibeHistoryCapacity];
    private readonly float[] _vibeVotes = new float[VibeVoteBucketCount];
    private int _vibeHistoryStart;
    private int _vibeHistoryCount;

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
        AddVibeHistorySample(inferredVibe, now);

        var resolvedVibe = ResolveVibeFromHistory(now, inferredVibe);
        _candidateVibe = resolvedVibe;
        if (resolvedVibe == _currentVibe)
        {
            _currentAnimation = MapVibeToAnimation(_currentVibe);
            return _currentAnimation;
        }

        _currentVibe = resolvedVibe;
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

        var bestVibe = MusicVibe.Neutral;
        var bestScore = float.MinValue;
        var secondScore = float.MinValue;

        for (var index = 0; index < VibeRules.Length; index += 1)
        {
            var rule = VibeRules[index];
            if (rule.Vibe == MusicVibe.Silence || rule.Vibe == MusicVibe.Neutral)
            {
                continue;
            }

            var score = rule.Score(features);
            if (score > bestScore)
            {
                secondScore = bestScore;
                bestScore = score;
                bestVibe = rule.Vibe;
                continue;
            }

            if (score > secondScore)
            {
                secondScore = score;
            }
        }

        if (bestScore < NeutralInstantThreshold)
        {
            return MusicVibe.Neutral;
        }

        if (bestScore < StrongInstantThreshold && bestScore - secondScore < AmbiguousScoreMargin)
        {
            return MusicVibe.Neutral;
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

    public static string GetVibeDisplayName(MusicVibe vibe)
    {
        return vibe switch
        {
            MusicVibe.Silence => "安静",
            MusicVibe.WarmGroove => "温暖律动",
            MusicVibe.BeatDriven => "节拍驱动",
            MusicVibe.BrightEnergy => "明亮高能",
            MusicVibe.SoftOrganic => "柔和自然",
            MusicVibe.CalmOrchestral => "平静管弦",
            MusicVibe.DarkHeavy => "低沉厚重",
            MusicVibe.Neutral => "普通 / 中性",
            _ => "普通 / 中性"
        };
    }

    public static PetAnimationId MapVibeToAnimation(MusicVibe vibe)
    {
        return vibe switch
        {
            MusicVibe.Silence => PetAnimationId.Sleeping,
            MusicVibe.CalmOrchestral => PetAnimationId.Idle,
            MusicVibe.SoftOrganic => PetAnimationId.Idle,
            MusicVibe.WarmGroove => PetAnimationId.Playing,
            MusicVibe.Neutral => PetAnimationId.Idle,
            MusicVibe.BrightEnergy => PetAnimationId.Celebrating,
            MusicVibe.BeatDriven => PetAnimationId.Celebrating,
            MusicVibe.DarkHeavy => PetAnimationId.Playing,
            _ => PetAnimationId.Idle
        };
    }

    private PetAnimationId CommitImmediate(MusicVibe vibe, PetAnimationId animationId, DateTimeOffset now)
    {
        _currentVibe = vibe;
        _candidateVibe = vibe;
        _currentAnimation = animationId;
        ClearVibeHistory();
        return _currentAnimation;
    }

    private void AddVibeHistorySample(MusicVibe vibe, DateTimeOffset observedAt)
    {
        PruneVibeHistory(observedAt);

        if (_vibeHistoryCount == VibeHistoryCapacity)
        {
            _vibeHistoryStart = (_vibeHistoryStart + 1) % VibeHistoryCapacity;
            _vibeHistoryCount -= 1;
        }

        var insertIndex = (_vibeHistoryStart + _vibeHistoryCount) % VibeHistoryCapacity;
        _vibeHistory[insertIndex] = new VibeHistorySample(vibe, observedAt);
        _vibeHistoryCount += 1;
    }

    private MusicVibe ResolveVibeFromHistory(DateTimeOffset now, MusicVibe fallbackVibe)
    {
        PruneVibeHistory(now);
        if (_vibeHistoryCount < 3)
        {
            return fallbackVibe == MusicVibe.Silence ? MusicVibe.Neutral : fallbackVibe;
        }

        Array.Clear(_vibeVotes);
        for (var offset = 0; offset < _vibeHistoryCount; offset += 1)
        {
            var index = (_vibeHistoryStart + offset) % VibeHistoryCapacity;
            var sample = _vibeHistory[index];
            var voteWeight = sample.Vibe == MusicVibe.Neutral ? NeutralVoteWeight : 1f;
            _vibeVotes[(int)sample.Vibe] += voteWeight;
        }

        if (_currentVibe != MusicVibe.Silence)
        {
            _vibeVotes[(int)_currentVibe] += CurrentVibeVoteBias;
        }

        var bestVibe = MusicVibe.Neutral;
        var bestVotes = float.MinValue;
        var secondVotes = float.MinValue;
        for (var index = 0; index < _vibeVotes.Length; index += 1)
        {
            if ((MusicVibe)index == MusicVibe.Silence)
            {
                continue;
            }

            var votes = _vibeVotes[index];
            if (votes > bestVotes)
            {
                secondVotes = bestVotes;
                bestVotes = votes;
                bestVibe = (MusicVibe)index;
                continue;
            }

            if (votes > secondVotes)
            {
                secondVotes = votes;
            }
        }

        if (bestVotes - secondVotes < RequiredVoteLead)
        {
            return _currentVibe == MusicVibe.Silence ? MusicVibe.Neutral : _currentVibe;
        }

        return bestVibe;
    }

    private void PruneVibeHistory(DateTimeOffset now)
    {
        while (_vibeHistoryCount > 0)
        {
            var oldest = _vibeHistory[_vibeHistoryStart];
            if (now - oldest.ObservedAt <= VibeHistoryWindow)
            {
                return;
            }

            _vibeHistoryStart = (_vibeHistoryStart + 1) % VibeHistoryCapacity;
            _vibeHistoryCount -= 1;
        }
    }

    private void ClearVibeHistory()
    {
        _vibeHistoryStart = 0;
        _vibeHistoryCount = 0;
        Array.Clear(_vibeVotes);
    }

    private static float ScoreSilence(MusicFeatures features)
    {
        return features.IsTrueSilence
            ? 1
            : Math.Clamp(1 - SmoothStep(0.001f, 0.012f, features.Rms), 0, 0.45f);
    }

    private static float ScoreWarmGroove(MusicFeatures features)
    {
        var score = 0.08f
            + TempoScore(features.Bpm, 68, 115, 0.18f)
            + TargetScore(features.BassRatio, 0.34f, 0.14f, 0.14f)
            + TargetScore(features.MidRatio, 0.38f, 0.16f, 0.16f)
            + TargetScore(features.LowMidRatio + features.MidRatio, 0.48f, 0.18f, 0.12f)
            + InverseScore(features.PresenceRatio, 0.18f, 0.42f, 0.08f)
            + InverseScore(features.AirRatio, 0.10f, 0.24f, 0.06f)
            + TargetScore(features.FluxActivity, 0.26f, 0.18f, 0.13f)
            + TargetScore(features.BassDominance, 1.15f, 0.60f, 0.09f)
            + TargetScore(features.MfccMidRatio, 0.34f, 0.18f, 0.08f);

        if (features.BeatDrivenTexture)
        {
            score -= 0.22f;
        }

        if (features.BrightEnergyTexture)
        {
            score -= 0.18f;
        }

        if (features.CalmOrchestralTexture)
        {
            score -= 0.20f;
        }

        if (features.DarkHeavyTexture)
        {
            score -= 0.24f;
        }

        return ClampScore(score);
    }

    private static float ScoreBeatDriven(MusicFeatures features)
    {
        if (!features.BeatDrivenTexture && features.BeatPulseEvidence < 0.50f)
        {
            return 0;
        }

        var tempoScore = Math.Max(
            TempoScore(features.Bpm, 70, 105, 0.12f),
            TempoScore(features.Bpm, 130, 170, 0.12f));
        if (features.BeatDrivenTexture && tempoScore < 0.10f)
        {
            tempoScore = 0.10f;
        }

        var transientPulseScore = TargetScore(features.FluxActivity, 0.54f, 0.34f, 0.16f)
            * SmoothStep(0.34f, 0.54f, features.LowFrequencyWeight);
        var bassPulseScore = TargetScore(features.SubBassRatio + features.BassRatio, 0.50f, 0.20f, 0.16f)
            * SmoothStep(0.18f, 0.42f, features.FluxActivity);
        var pulseScore = Math.Max(transientPulseScore, bassPulseScore);

        var pulseGate = SmoothStep(0.38f, 0.72f, features.BeatPulseEvidence);
        var score = 0.04f
            + tempoScore
            + TargetScore(features.BassRatio, 0.48f, 0.18f, 0.13f)
            + TargetScore(features.BassDominance, 1.85f, 0.85f, 0.14f)
            + pulseScore
            + TargetScore(features.LowFrequencyWeight, 0.58f, 0.20f, 0.10f)
            + TargetScore(features.MfccLowRatio, 0.46f, 0.22f, 0.06f)
            + (features.BeatDrivenTexture ? 0.12f : 0);

        score *= Math.Max(0.45f, pulseGate);

        if (!features.BeatDrivenTexture && features.BeatPulseEvidence < 0.62f)
        {
            score = Math.Min(score, 0.30f);
        }

        if (features.BeatPulseEvidence < 0.48f)
        {
            score -= 0.22f;
        }

        if (features.Brightness > 0.82f && features.PresenceRatio > 0.22f)
        {
            score -= 0.08f;
        }

        if (features.FluxActivity < 0.22f && features.BassDominance < 1.45f)
        {
            score -= 0.18f;
        }

        if (features.FluxActivity < 0.26f
            && features.MidRatio >= 0.34f
            && features.PresenceRatio < 0.24f
            && features.AirRatio < 0.18f)
        {
            score -= 0.14f;
        }

        if (features.LightOrganicTexture)
        {
            score -= 0.24f;
        }

        if (features.OrchestralTexture)
        {
            score -= 0.30f;
        }

        if (features.GentleTexture)
        {
            score -= 0.30f;
        }

        if (features.GeneralDrumTexture)
        {
            score -= 0.26f;
        }

        if (features.LowFrequencyWeight < 0.42f
            && features.BassDominance < 1.10f
            && features.PresenceRatio + features.AirRatio > 0.22f)
        {
            score -= 0.12f;
        }

        return ClampScore(score);
    }

    private static float ScoreBrightEnergy(MusicFeatures features)
    {
        var score = 0.06f
            + TempoScore(features.Bpm, 96, 210, 0.15f)
            + TargetScore(features.Brightness, 0.62f, 0.36f, 0.14f)
            + TargetScore(features.PresenceRatio + features.HighMidRatio, 0.36f, 0.22f, 0.16f)
            + TargetScore(features.MidRatio + features.PresenceRatio, 0.52f, 0.24f, 0.13f)
            + TargetScore(features.FluxActivity, 0.58f, 0.32f, 0.14f)
            + SmoothStep(0.018f, 0.13f, features.Rms) * 0.12f
            + TargetScore(features.Airiness, 0.56f, 0.38f, 0.08f)
            + (features.BrightEnergyTexture ? 0.14f : 0)
            + (features.LightOrganicTexture && features.FluxActivity >= 0.42f ? 0.06f : 0);

        if (features.DarkHeavyTexture && features.Brightness < 0.42f)
        {
            score -= 0.12f;
        }

        return ClampScore(score);
    }

    private static float ScoreSoftOrganic(MusicFeatures features)
    {
        var score = 0.08f
            + TempoScore(features.Bpm, 55, 118, 0.15f)
            + TargetScore(features.MidRatio, 0.42f, 0.20f, 0.16f)
            + TargetScore(features.LowMidRatio, 0.16f, 0.12f, 0.11f)
            + InverseScore(features.BassDominance, 0.90f, 1.80f, 0.14f)
            + TargetScore(features.Brightness, 0.36f, 0.24f, 0.11f)
            + InverseScore(features.FluxActivity, 0.22f, 0.64f, 0.13f)
            + InverseScore(features.Rms, 0.035f, 0.14f, 0.08f)
            + TargetScore(features.MfccMidRatio, 0.36f, 0.22f, 0.08f)
            + (features.GentleTexture ? 0.10f : 0)
            + (features.LightOrganicTexture ? 0.12f : 0);

        if (features.BeatDrivenTexture)
        {
            score -= 0.16f;
        }

        if (features.BrightEnergyTexture)
        {
            score -= 0.14f;
        }

        if (features.DarkHeavyTexture)
        {
            score -= 0.18f;
        }

        return ClampScore(score);
    }

    private static float ScoreCalmOrchestral(MusicFeatures features)
    {
        var score = 0.05f
            + (!features.HasStableBeat && features.FluxActivity < 0.24f ? 0.06f : 0)
            + InverseScore(features.BassDominance, 0.65f, 1.35f, 0.13f)
            + InverseScore(features.FluxActivity, 0.10f, 0.34f, 0.18f)
            + TargetScore(features.MidRatio + features.PresenceRatio, 0.56f, 0.24f, 0.12f)
            + TargetScore(features.Brightness, 0.44f, 0.26f, 0.10f)
            + InverseScore(features.AirRatio, 0.14f, 0.30f, 0.07f)
            + InverseScore(features.LowFrequencyWeight, 0.18f, 0.40f, 0.12f)
            + InverseScore(features.Rms, 0.035f, 0.14f, 0.07f)
            + (features.OrchestralTexture ? 0.18f : 0);

        if (features.BeatDrivenTexture)
        {
            score -= 0.32f;
        }

        if (features.BrightEnergyTexture)
        {
            score -= 0.24f;
        }

        if (features.DarkHeavyTexture)
        {
            score -= 0.24f;
        }

        if (features.FluxActivity > 0.36f && !features.OrchestralTexture)
        {
            score -= 0.18f;
        }

        if (features.LowFrequencyWeight > 0.44f || features.BassDominance > 1.35f)
        {
            score -= 0.18f;
        }

        return ClampScore(score);
    }

    private static float ScoreDarkHeavy(MusicFeatures features)
    {
        var score = 0.06f
            + TargetScore(features.LowFrequencyWeight, 0.62f, 0.26f, 0.18f)
            + TargetScore(features.BassDominance, 2.10f, 1.25f, 0.16f)
            + TargetScore(features.BassRatio + features.SubBassRatio, 0.56f, 0.24f, 0.15f)
            + InverseScore(features.Brightness, 0.32f, 0.72f, 0.10f)
            + SmoothStep(0.024f, 0.15f, features.Rms) * 0.12f
            + TargetScore(features.FluxActivity, 0.48f, 0.36f, 0.10f)
            + (features.DarkHeavyTexture ? 0.15f : 0)
            + TempoScore(features.Bpm, 60, 170, 0.08f);

        if (features.BrightEnergyTexture && features.Brightness > 0.65f)
        {
            score -= 0.10f;
        }

        return ClampScore(score);
    }

    private static float ScoreNeutral(MusicFeatures features)
    {
        if (features.IsTrueSilence)
        {
            return 0;
        }

        return ClampScore(0.24f
            + TargetScore(features.Balance, 0.58f, 0.42f, 0.06f)
            - Math.Max(0, features.FluxActivity - 0.64f) * 0.16f
            - Math.Max(0, features.LowFrequencyWeight - 0.62f) * 0.16f
            - Math.Max(0, features.Brightness - 0.72f) * 0.16f);
    }

    private static float TargetScore(float value, float target, float tolerance, float maxScore)
    {
        var distance = MathF.Abs(value - target);
        var normalized = Math.Clamp(1 - distance / Math.Max(tolerance, 0.000001f), 0, 1);
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

    private static float ClampScore(float score)
    {
        return Math.Clamp(score, 0, 1);
    }

    private readonly record struct VibeHistorySample(MusicVibe Vibe, DateTimeOffset ObservedAt);

    private readonly record struct VibeRule(MusicVibe Vibe, Func<MusicFeatures, float> Score);

    public readonly record struct VibeScore(MusicVibe Vibe, float Score);

    private readonly record struct MusicFeatures(
        float Rms,
        float BassRatio,
        float MidRatio,
        float HighRatio,
        float SubBassRatio,
        float LowMidRatio,
        float PresenceRatio,
        float AirRatio,
        float HighMidRatio,
        float TotalEnergy,
        float Bpm,
        float BassDominance,
        float Balance,
        bool HasStableBeat,
        bool MfccAvailable,
        float MfccLowRatio,
        float MfccMidRatio,
        float MfccHighRatio,
        float MfccCentroid,
        float Brightness,
        float Airiness,
        float FluxActivity,
        float LowFrequencyWeight,
        float BeatPulseEvidence,
        bool IsTrueSilence,
        bool BeatDrivenTexture,
        bool GeneralDrumTexture,
        bool BrightEnergyTexture,
        bool CalmOrchestralTexture,
        bool DarkHeavyTexture,
        bool LightOrganicTexture,
        bool OrchestralTexture,
        bool GentleTexture)
    {
        public static MusicFeatures From(SpectrumData spectrum, float bpm)
        {
            const float epsilon = 0.000001f;
            var total = Math.Max(spectrum.Bass + spectrum.Mid + spectrum.High, epsilon);
            var bassRatio = spectrum.Bass / total;
            var midRatio = spectrum.Mid / total;
            var highRatio = spectrum.High / total;
            var subBassRatio = spectrum.SubBass / total;
            var lowMidRatio = spectrum.LowMid / total;
            var presenceRatio = spectrum.Presence / total;
            var airRatio = spectrum.Air / total;
            var highMidRatio = spectrum.HighMid / total;
            var maxRatio = Math.Max(bassRatio, Math.Max(midRatio, highRatio));
            var minRatio = Math.Min(bassRatio, Math.Min(midRatio, highRatio));
            var bassDominance = spectrum.Bass / Math.Max(spectrum.Mid + spectrum.High, epsilon);
            var balance = 1 - (maxRatio - minRatio);
            var brightness = Math.Clamp(spectrum.Centroid / 3500f, 0, 1);
            var airiness = Math.Clamp(spectrum.Rolloff / 8000f, 0, 1);
            var fluxActivity = Math.Clamp(spectrum.Flux, 0, 1);
            var lowFrequencyWeight = Math.Clamp(bassRatio + subBassRatio, 0, 1);
            var hasStableBeat = bpm is >= 55 and <= 210;
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

            var tempoLooksBeatDriven = bpm is >= 70 and <= 105 || bpm is >= 130 and <= 170;
            var beatPulseEvidence = Math.Max(
                SmoothStep(0.42f, 0.66f, lowFrequencyWeight) * SmoothStep(0.32f, 0.56f, fluxActivity),
                SmoothStep(1.35f, 2.05f, bassDominance) * SmoothStep(0.36f, 0.54f, bassRatio));
            var orchestralTexture = lowFrequencyWeight <= 0.44f
                && bassDominance <= 1.20f
                && midRatio + presenceRatio >= 0.50f
                && highRatio <= 0.42f
                && airRatio <= 0.26f
                && spectrum.Rms <= 0.16f
                && (!tempoLooksBeatDriven || fluxActivity <= 0.58f);
            var pulseLooksBeatDriven = bassRatio >= 0.32f
                && bassDominance >= 1.25f
                && beatPulseEvidence >= 0.46f
                && (fluxActivity >= 0.36f || (lowFrequencyWeight >= 0.62f && bassDominance >= 1.65f));
            var beatDrivenTexture = pulseLooksBeatDriven
                && !orchestralTexture
                && (tempoLooksBeatDriven || !hasStableBeat || lowFrequencyWeight >= 0.52f);
            var generalDrumTexture = !beatDrivenTexture
                && fluxActivity >= 0.34f
                && bassDominance < 1.35f
                && lowFrequencyWeight < 0.55f
                && midRatio + presenceRatio >= 0.44f;

            var brightEnergyTexture = brightness >= 0.52f
                && (presenceRatio + highMidRatio >= 0.24f || highRatio >= 0.24f)
                && (fluxActivity >= 0.38f || spectrum.Rms >= 0.035f);

            var calmOrchestralTexture = !beatDrivenTexture
                && !hasStableBeat
                && bassDominance <= 1.05f
                && lowFrequencyWeight <= 0.34f
                && fluxActivity <= 0.24f
                && brightness <= 0.66f;

            var darkHeavyTexture = lowFrequencyWeight >= 0.50f
                && bassDominance >= 1.45f
                && brightness <= 0.52f
                && spectrum.Rms >= 0.018f;

            var lightOrganicTexture = bpm is >= 86 and <= 155
                && lowFrequencyWeight <= 0.46f
                && bassDominance <= 1.20f
                && midRatio >= 0.30f
                && brightness >= 0.38f
                && presenceRatio + airRatio >= 0.20f
                && spectrum.Rms <= 0.12f;

            var gentleTexture = lowFrequencyWeight <= 0.48f
                && bassDominance <= 1.25f
                && fluxActivity <= 0.42f
                && spectrum.Rms <= 0.10f
                && midRatio >= 0.28f;

            var isTrueSilence = spectrum.Rms < 0.0008f
                && total < 0.0012f
                && fluxActivity < 0.025f;

            return new MusicFeatures(
                Rms: spectrum.Rms,
                BassRatio: bassRatio,
                MidRatio: midRatio,
                HighRatio: highRatio,
                SubBassRatio: subBassRatio,
                LowMidRatio: lowMidRatio,
                PresenceRatio: presenceRatio,
                AirRatio: airRatio,
                HighMidRatio: highMidRatio,
                TotalEnergy: total,
                Bpm: bpm,
                BassDominance: bassDominance,
                Balance: balance,
                HasStableBeat: hasStableBeat,
                MfccAvailable: mfccAvailable,
                MfccLowRatio: mfccLowRatio,
                MfccMidRatio: mfccMidRatio,
                MfccHighRatio: mfccHighRatio,
                MfccCentroid: mfccCentroid,
                Brightness: brightness,
                Airiness: airiness,
                FluxActivity: fluxActivity,
                LowFrequencyWeight: lowFrequencyWeight,
                BeatPulseEvidence: beatPulseEvidence,
                IsTrueSilence: isTrueSilence,
                BeatDrivenTexture: beatDrivenTexture,
                GeneralDrumTexture: generalDrumTexture,
                BrightEnergyTexture: brightEnergyTexture,
                CalmOrchestralTexture: calmOrchestralTexture,
                DarkHeavyTexture: darkHeavyTexture,
                LightOrganicTexture: lightOrganicTexture,
                OrchestralTexture: orchestralTexture,
                GentleTexture: gentleTexture);
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
