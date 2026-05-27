using MusicPixelPet.Wpf.Models;

namespace MusicPixelPet.Wpf.Pet;

public static class PetAnimationRules
{
    public static PetAnimationId Derive(MediaSnapshot snapshot, IEnumerable<MusicRule> rules)
    {
        if (!snapshot.Connected || snapshot.Track is null)
        {
            return PetAnimationId.Idle;
        }

        if (snapshot.Status is PlaybackStatus.Paused or PlaybackStatus.Stopped)
        {
            return PetAnimationId.Paused;
        }

        var matchedRule = rules.FirstOrDefault(rule => MatchesRule(snapshot.Track, rule));
        return matchedRule?.Mode switch
        {
            RulePetMode.Sleepy => PetAnimationId.Sleeping,
            RulePetMode.Energetic => PetAnimationId.Celebrating,
            _ => PetAnimationId.Playing
        };
    }

    private static bool MatchesRule(MediaTrack track, MusicRule rule)
    {
        var keyword = rule.Keyword.Trim();
        if (keyword.Length == 0)
        {
            return false;
        }

        var candidates = rule.Field switch
        {
            RuleMatchField.Title => [track.Title],
            RuleMatchField.Artist => [track.Artist],
            RuleMatchField.Album => [track.Album],
            _ => new[] { track.Title, track.Artist, track.Album }
        };

        return candidates.Any(candidate => candidate.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
