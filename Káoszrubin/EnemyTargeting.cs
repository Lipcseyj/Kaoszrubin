using KaoszRubin.Domain.Characters;

namespace KaoszRubin;

public static class EnemyTargeting
{
    public static (LiveCharacter Character, Position Position)? ChooseNearestVisible(
        Position observerPosition,
        IEnumerable<(LiveCharacter Character, Position Position)> candidates,
        Func<Position, bool> canSee,
        Random random)
    {
        var visible = candidates.Where(candidate => candidate.Character.IsAlive && canSee(candidate.Position)).ToArray();
        if (visible.Length == 0) return null;
        var nearestDistance = visible.Min(candidate => Distance(observerPosition, candidate.Position));
        var nearest = visible.Where(candidate => Distance(observerPosition, candidate.Position) == nearestDistance)
            .ToArray();
        return nearest[random.Next(nearest.Length)];
    }

    private static int Distance(Position first, Position second) =>
        Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);
}
