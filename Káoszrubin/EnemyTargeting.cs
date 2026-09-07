using KaoszRubin.Domain.Characters;

namespace KaoszRubin;

public static class EnemyTargeting
{
    public static (LiveCharacter Character, Position Position)? ChooseNearestVisible(
        Position observerPosition,
        IEnumerable<(LiveCharacter Character, Position Position)> candidates,
        Func<Position, bool> canSee,
        Random random,
        CharacterId? preferredTargetCharacterId = null)
    {
        var visible = candidates.Where(candidate => candidate.Character.IsAlive && canSee(candidate.Position)).ToArray();
        if (visible.Length == 0) return null;
        if (preferredTargetCharacterId is { } preferredId)
        {
            var preferred = visible.FirstOrDefault(candidate => candidate.Character.Id == preferredId);
            if (preferred.Character is not null) return preferred;
        }
        var nearestDistance = visible.Min(candidate => Distance(observerPosition, candidate.Position));
        var nearest = visible.Where(candidate => Distance(observerPosition, candidate.Position) == nearestDistance)
            .ToArray();
        return nearest[random.Next(nearest.Length)];
    }

    public static (LiveCharacter Character, Position Position)? ChooseNearestSensed(
        Position observerPosition,
        IEnumerable<(LiveCharacter Character, Position Position)> candidates,
        int trackingSense,
        Func<Position, int?> pathDistance,
        Random random,
        CharacterId? preferredTargetCharacterId = null)
    {
        if (trackingSense <= 0) return null;
        var sensed = candidates.Where(candidate => candidate.Character.IsAlive)
            .Select(candidate => (Candidate: candidate, Distance: pathDistance(candidate.Position)))
            .Where(entry => entry.Distance is >= 0 && entry.Distance <= trackingSense).ToArray();
        if (sensed.Length == 0) return null;
        if (preferredTargetCharacterId is { } preferredId)
        {
            var preferred = sensed.FirstOrDefault(entry => entry.Candidate.Character.Id == preferredId);
            if (preferred.Candidate.Character is not null) return preferred.Candidate;
        }
        var nearestDistance = sensed.Min(entry => entry.Distance!.Value);
        var nearest = sensed.Where(entry => entry.Distance == nearestDistance).ToArray();
        return nearest[random.Next(nearest.Length)].Candidate;
    }

    private static int Distance(Position first, Position second) =>
        Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);
}
