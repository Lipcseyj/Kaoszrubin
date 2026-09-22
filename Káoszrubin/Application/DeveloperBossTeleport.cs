using KaoszRubin.Domain.Combat;
using KaoszRubin.World;

namespace KaoszRubin.Application;

internal sealed record DeveloperBossTarget(string EnemyId, int MazeLevel);

internal static class DeveloperBossTeleport
{
    /// <summary>A kulcsbossokat az első, vezetőként konfigurált pályájuk szerint rendezi.</summary>
    public static IReadOnlyList<DeveloperBossTarget> Targets() =>
        Enumerable.Range(1, MazeLevelConfigurations.FinalLevel)
            .SelectMany(level => MazeLevelConfigurations.Get(level).RoomEncounters
                .SelectMany(encounter => encounter.Members)
                .Where(member => member.Role == EnemyGroupRole.Leader &&
                                 MonsterIds.Bosses.Contains(member.EnemyId))
                .Select(member => new DeveloperBossTarget(member.EnemyId, level)))
            .GroupBy(target => target.EnemyId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(target => target.MazeLevel).First())
            .OrderBy(target => target.MazeLevel)
            .ThenBy(target => target.EnemyId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>Megkeresi a boss közelében az egész parti számára szabad, csapdamentes elhelyezést.</summary>
    public static IReadOnlyList<Position> FindPartyDestinations(Maze maze, Position bossPosition,
        IReadOnlyCollection<PartyMemberAvatar> companions) =>
        Enumerable.Range(0, maze.Width * maze.Height)
            .Select(index => new Position(index % maze.Width, index / maze.Width))
            .Where(position => position != bossPosition && maze.GetDoorAt(position) is null)
            .OrderBy(position => Math.Abs(position.X - bossPosition.X) +
                                 Math.Abs(position.Y - bossPosition.Y))
            .ThenBy(position => position.Y)
            .ThenBy(position => position.X)
            .Select(position => DeveloperPartyTeleport.FindDestinations(maze, position, companions))
            .FirstOrDefault(positions => positions.Count == companions.Count + 1) ?? [];
}
