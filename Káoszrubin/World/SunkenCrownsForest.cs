namespace KaoszRubin.World;

/// <summary>Beépített tartalék a level-13.json szerkeszthető erdőgráfjához.</summary>
public static class SunkenCrownsForest
{
    public const int CampaignLevel = 13;

    public static ExplicitForestAreaGraphConfiguration CreateGraph() => new(
        Areas:
        [
            new("REED_GATE", "A nádas kapuja", new(0, 0), "reed-marsh", new()
            {
                ForestDensity = 0.32,
                TrailWidth = 2,
                MarshCount = new(4, 6),
                BuildingCount = new(1, 1)
            }),
            new("DROWNED_WOOD", "Fuldokló erdő", new(1, 0), "flooded-wood", new()
            {
                LakeCount = new(4, 6),
                MarshCount = new(5, 7),
                BuildingCount = new(1, 2)
            }),
            new("FERRY_ISLAND", "A révész szigete", new(2, 0), "island-groves", new()
            {
                LakeRadius = new(4, 7),
                BuildingCount = new(2, 2),
                ManorBuildingChance = 0.1,
                TrailWidth = 2,
                BuildingSize = new(6, 8)
            }),
            new("CROWN_CAUSEWAY", "Koronák töltése", new(3, 0), "flooded-wood", new()
            {
                ForestDensity = 0.34,
                TrailWidth = 3,
                ExtraTrailChance = 0.6,
                BuildingCount = new(1, 2),
                ManorBuildingChance = 0.65
            }),
            new("SUNKEN_COURT", "Elsüllyedt udvarházak", new(4, 0), "ruined-estate", new()
            {
                LakeCount = new(3, 5),
                MarshCount = new(3, 5),
                BuildingCount = new(3, 4),
                ManorBuildingChance = 1,
                LabyrinthBuildingChance = 0,
                ManorRoomCount = new(4, 6)
            }),
            new("DROWNED_THRONE", "A vízbe fúlt trón", new(5, 0), "lost-manor", new()
            {
                ForestDensity = 0.45,
                LakeCount = new(4, 6),
                MarshCount = new(2, 4),
                BuildingCount = new(2, 3),
                ManorBuildingChance = 1,
                LabyrinthBuildingChance = 0,
                ManorRoomCount = new(4, 6)
            }),
            new("LEECH_MIRE", "Piócák ingoványa", new(1, 1), "huge-swamp", new()
            {
                MarshCount = new(10, 14),
                MarshRadius = new(6, 10),
                LakeCount = new(3, 5)
            }),
            new("BLACK_MIRROR", "Fekete tükör", new(2, 1), "blackwater-bog", new()
            {
                LakeCount = new(5, 7),
                MarshCount = new(7, 10),
                MarshRadius = new(4, 8),
                BuildingCount = new(0, 0)
            }),
            new("REED_LABYRINTH", "Nádrengeteg", new(3, 1), "reed-marsh", new()
            {
                UndergrowthChance = 0.48,
                DenseUndergrowthChance = 0.24,
                MarshCount = new(8, 12),
                TrailWinding = 0.96,
                ExtraTrailChance = 0.16
            }),
            new("WITCH_GROVE", "Boszorkányok ligete", new(4, 1), "ancient-forest", new()
            {
                ForestDensity = 0.72,
                LakeCount = new(2, 4),
                MarshChance = 0.85,
                MarshCount = new(4, 6),
                BuildingCount = new(1, 2),
                ManorBuildingChance = 0.1
            }),
            new("CROCODILE_LAKES", "Krokodilok tavai", new(2, -1), "mirror-lakes", new()
            {
                LakeCount = new(7, 10),
                LakeRadius = new(4, 8),
                MarshChance = 0.9,
                MarshCount = new(4, 7),
                BuildingCount = new(0, 0)
            }),
            new("OLD_SLUICE", "A királyi zsilip romjai", new(3, -1), "ruined-estate", new()
            {
                ForestDensity = 0.35,
                LakeCount = new(2, 4),
                MarshCount = new(2, 4),
                BuildingCount = new(2, 3),
                ManorBuildingChance = 0.9,
                LabyrinthBuildingChance = 0.1
            })
        ],
        Connections:
        [
            new("REED_GATE", "DROWNED_WOOD"),
            new("DROWNED_WOOD", "FERRY_ISLAND"),
            new("FERRY_ISLAND", "CROWN_CAUSEWAY"),
            new("CROWN_CAUSEWAY", "SUNKEN_COURT"),
            new("SUNKEN_COURT", "DROWNED_THRONE"),
            new("DROWNED_WOOD", "LEECH_MIRE"),
            new("LEECH_MIRE", "BLACK_MIRROR"),
            new("BLACK_MIRROR", "FERRY_ISLAND"),
            new("BLACK_MIRROR", "REED_LABYRINTH"),
            new("REED_LABYRINTH", "WITCH_GROVE"),
            new("WITCH_GROVE", "SUNKEN_COURT"),
            new("FERRY_ISLAND", "CROCODILE_LAKES"),
            new("CROCODILE_LAKES", "OLD_SLUICE"),
            new("OLD_SLUICE", "CROWN_CAUSEWAY")
        ],
        EntranceAreaId: "REED_GATE",
        ExitAreaId: "DROWNED_THRONE", Templates: []);
}
