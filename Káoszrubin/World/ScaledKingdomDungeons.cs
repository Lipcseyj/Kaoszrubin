using System.Text;

namespace KaoszRubin.World;

/// <summary>A lápi előőrsök mögötti két hüllőbirodalmi dungeon.</summary>
public static class ScaledKingdomDungeons
{
    public const int PalaceLevel = 15;
    public const int TempleLevel = 16;
    public const string ThroneRoom = "SCALED_KING_THRONE";
    public const string CrocodilePool = "SCALED_CROCODILE_POOL";
    public const string HighAltar = "SHEDDING_HIGH_ALTAR";
    public const string HydraSanctum = "SHEDDING_HYDRA_SANCTUM";

    public static string? AreaName(MazeLevelConfiguration configuration, int index, int count)
    {
        string[] names = configuration.QuestRoomIds.Contains(ThroneRoom)
            ? ["Az elárasztott kapu", "Krokodilmedencék", "A pikkelylégió kaszárnyái", "A királyi pikkelytrón"]
            : configuration.QuestRoomIds.Contains(HighAltar)
                ? ["A vedlés kapuja", "Mérgek csarnoka", "Papi körmenetek termei", "Az ősi őrzők szentélye", "A vedlő isten főoltára"]
                : [];
        if (names.Length == 0) return null;
        return names[index == count - 1 ? names.Length - 1 : index];
    }

    /// <summary>A medence sekély, gázolható vize nem szakíthatja meg a dungeon bejárhatóságát.</summary>
    public static void ApplyTerrain(Maze maze)
    {
        if (maze.GetRoomByContentId(CrocodilePool) is not { } room) return;
        var water = new MazeTerrainStyle("scaled-palace-pool", new Rune('≈'),
            ConsoleColor.Cyan, ConsoleColor.DarkBlue, Walkable: true, BlocksSight: false);
        maze.RegisterTerrainGameplayProfile(water.Id,
            new TerrainGameplayProfile(TerrainTag.Marsh, MovementDelayPercent: 25, ExertionCost: 1));
        foreach (var position in room.InteriorPositions().Where(position => maze.IsWalkable(position) &&
                     position != maze.Entrance && position != maze.Exit && maze.GetDoorAt(position) is null))
            maze.SetTerrain(position, water);
    }
}
