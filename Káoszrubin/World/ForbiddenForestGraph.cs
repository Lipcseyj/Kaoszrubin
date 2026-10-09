namespace KaoszRubin.World;

/// <summary>Beépített tartalék a level-6.json szerkeszthető erdőgráfjához.</summary>
public static class ForbiddenForestGraph
{
    public const int CampaignLevel = 6;

    public static ExplicitForestAreaGraphConfiguration CreateGraph() => new(
        Areas:
        [
            new("MOSS_GATE", "Mohakapu", new(0, 0), "mixed-forest", new()
            {
                ManorBuildingChance = 0.55
            }),
            new("WHISPERING_WOOD", "Suttogó rengeteg", new(1, 0), "dense-cabin-forest", new()
            {
                UndergrowthChance = 0.57,
                DenseUndergrowthChance = 0.31,
                MarshCount = new(0, 0),
                BuildingCount = new(2, 3),
                BuildingSize = new(3, 4),
                ManorBuildingChance = 0,
                LabyrinthBuildingChance = 0
            }),
            new("RAVEN_CROSSING", "Hollók elágazása", new(2, 0), "hunter-camp", new()
            {
                BuildingCount = new(3, 4),
                BuildingSize = new(6, 8)
            }),
            new("OLD_PINES", "Az öreg fenyves", new(2, -1), "old-pines", new()
            {
                PineChance = 0.72,
                LakeCount = new(3, 4),
                MarshChance = 0,
                MarshCount = new(0, 0),
                BuildingCount = new(3, 4),
                LockedBuildingDoorChance = 0.9,
                OpenBuildingDoorChance = 0.05
            }),
            new("LOST_MANOR", "Az elveszett kúriák", new(3, 0), "lakes-and-manors", new()
            {
                LakeCount = new(5, 6),
                LakeRadius = new(5, 8),
                ManorBuildingChance = 1,
                LabyrinthBuildingChance = 0,
                BuildingCount = new(2, 3),
                ManorBuildingWidth = new(16, 22),
                ManorBuildingHeight = new(10, 16),
                ManorRoomCount = new(3, 5)
            }),
            new("BLACKWATER", "Feketevíz lápja", new(2, 1), "swamp", new()
            {
                LakeCount = new(6, 9),
                LakeRadius = new(4, 8),
                MarshChance = 0.9,
                MarshCount = new(12, 15),
                MarshRadius = new(3, 9),
                Palette = new ForestTerrainPalette
                {
                    Tree = new("forbidden-tree", new('♠'), ConsoleColor.DarkGreen, ConsoleColor.Black, false, true),
                    Bush = new("forest-bush", new('♣'), ConsoleColor.Green, ConsoleColor.Black, true, false),
                    Pine = new("forest-pine", new('▲'), ConsoleColor.Green, ConsoleColor.Black, false, true),
                    FlowerBush = new("forbidden-flower-bush", new('✿'), ConsoleColor.DarkMagenta, ConsoleColor.Black, true, false),
                    Thicket = new("forest-thicket", new('#'), ConsoleColor.DarkGreen, ConsoleColor.Black, false, true),
                    Undergrowth = new("forest-undergrowth", new('░'), ConsoleColor.DarkGreen, ConsoleColor.Black, true, false),
                    DenseUndergrowth = new("forest-dense-undergrowth", new('▒'), ConsoleColor.Green, ConsoleColor.Black, true, false),
                    Water = new("forbidden-water", new('≈'), ConsoleColor.DarkBlue, ConsoleColor.Black, false, false),
                    Marsh = new("forbidden-marsh", new('≋'), ConsoleColor.Black, ConsoleColor.DarkBlue, true, false),
                    BuildingWall = new("forbidden-building-wall", new('█'), ConsoleColor.Gray, ConsoleColor.Black, false, true)
                }
            }),
            new("THORN_MAZE", "A tövisek útvesztője", new(3, 1), "forest-labyrinth", new()
            {
                ThicketChance = 0.5
            }),
            new("WINDLESS_GLADE", "A Szélcsend tisztása", new(3, 2), "open-groves", new()
            {
                LakeCount = new(3, 5),
                MarshChance = 0.05,
                MarshCount = new(0, 1),
                MarshRadius = new(2, 4)
            })
        ],
        Connections:
        [
            new("MOSS_GATE", "WHISPERING_WOOD"),
            new("WHISPERING_WOOD", "RAVEN_CROSSING"),
            new("RAVEN_CROSSING", "BLACKWATER"),
            new("BLACKWATER", "THORN_MAZE"),
            new("THORN_MAZE", "WINDLESS_GLADE"),
            new("RAVEN_CROSSING", "LOST_MANOR"),
            new("RAVEN_CROSSING", "OLD_PINES")
        ],
        EntranceAreaId: "MOSS_GATE",
        ExitAreaId: "WINDLESS_GLADE", Templates: []);
}
