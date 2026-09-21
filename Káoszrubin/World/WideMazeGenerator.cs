using KaoszRubin.Domain.Combat;

namespace KaoszRubin.World;

/// <summary>Hárommezős folyosókat és 3×3-as csomópontokat készítő, a klasszikustól különálló pályagenerátor.</summary>
public sealed class WideMazeGenerator : MazeGenerator
{
    public WideMazeGenerator(MazeGenerationSettings settings,
        IReadOnlyList<ResolvedEnemyEncounter> roomEncounters,
        IReadOnlyList<ResolvedEnemyEncounter> corridorEncounters, Random? random = null)
        : base(settings, roomEncounters, corridorEncounters, random) { }

    protected override int CorridorNodeWidth => 3;
    protected override int GridStep => 6;
    protected override int RollConnectionWidth() =>
        Random.NextDouble() < Settings.WideCorridorNarrowingChance ? 2 : 3;

    protected override void CreateStartingRoom(Maze maze)
    {
        var room = new Room(new Position(1, 1), 3, 3);
        foreach (var position in room.InteriorPositions()) maze.Carve(position);
        maze.SetStartingRoom(room);
    }
}
