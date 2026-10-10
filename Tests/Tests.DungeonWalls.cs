using System.Text;

internal static partial class Program
{
    static void DungeonWallStylesCoverAllRequestedGlyphs()
    {
        const string requested = "Ш≆≇≈≉≊≋≌≍≎≏≐≑≒≓≔≕≖≗≘≙≚≢≣⊟⊠⊡⊏⊐⊑⊒⊓⊔⊞⌸⌺⌻⌼⍁⍂⍝⍓⍔⍯▀▇█▉▊▋▐░▒▓▙▚▛▜▝▞▟■▢▣▤▥▦▧▨▩⠿⡿⢿⣿⬒⬓⬔⬕⬖⬗⬘⬙⬚⯀";
        var styles = DungeonWallStyles.All;
        Assert(styles.Select(style => style.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == styles.Count &&
               styles.Select(style => style.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == styles.Count &&
               requested.EnumerateRunes().All(rune => styles.Any(style => style.Rune == rune)),
            "A falkatalógusból hiányzik egy kért jel, vagy ismétlődő név/azonosító szerepel benne.");
        foreach (var style in styles)
        {
            Assert(ReferenceEquals(style, DungeonWallStyles.Get(" " + style.Id.ToLowerInvariant() + " ")) &&
                   ReferenceEquals(style, DungeonWallStyles.Get(style.Name)) &&
                   ReferenceEquals(style, typeof(DungeonWallStyles).GetProperty(style.Id)!.GetValue(null)) &&
                   style.Color != ConsoleColor.Black && style.Rune != Maze.Floor && style.Rune != Maze.ExitMarker,
                "A falstílus nem kérhető le stabilan, vagy nem használható falnak: " + style.Id);
            var maze = new MazeGenerator(new MazeGenerationSettings
            {
                WallRune = style.Rune, WallColor = style.Color, RoomCount = 2,
                MinimumRoomSize = 2, MaximumRoomSize = 4, TreasureChestCount = 0
            }, [], [], new Random(617)).Create(40, 25);
            var walls = Enumerable.Range(0, maze.Width).SelectMany(x =>
                Enumerable.Range(0, maze.Height).Select(y => new Position(x, y)))
                .Where(position => maze.Tiles[position.X, position.Y] == style.Rune).ToArray();
            Assert(walls.Length > 0 && walls.All(position => !maze.IsWalkable(position) && maze.BlocksSight(position)) &&
                   maze.WallColor == style.Color,
                "A falblokk megváltoztatta a járhatóságot, látást vagy színt: " + style.Id);
        }
    }

    static void DungeonWallStylesFlowThroughCampaignSettings()
    {
        var dungeonCount = 0;
        for (var level = 1; level <= MazeLevelConfigurations.FinalLevel; level++)
        {
            var configuration = MazeLevelConfigurations.Get(level);
            var settings = configuration.CreateGenerationSettings(new Random(level));
            if (configuration.Layout is ForestMazeLayoutConfiguration)
            {
                Assert(configuration.WallStyle is null && configuration.WallRune == new Rune('♠'),
                    "A dungeon-falkatalógus felülírta az erdő fáit.");
                continue;
            }
            dungeonCount++;
            Assert(configuration.WallStyle is { } style && settings.WallRune == style.Rune &&
                   settings.WallColor == style.Color && configuration.WallRune == style.Rune &&
                   configuration.WallColor == style.Color,
                "A közös falstílus nem jutott el a generátorhoz: " + level);
        }
        Assert(dungeonCount == 23 && MazeLevelConfigurations.Get(26).WallStyle is not null &&
               QuestLocationConfigurations.Get(QuestLocationConfigurations.RodericMalrec).WallStyle == DungeonWallStyles.MalrecChapel,
            "A kampány, tartalék pálya vagy küldetéshelyszín nem közös falstílust használ.");

        var custom = new MazeLevelConfiguration
        {
            Level = 1, Name = "Egyedi", RoomCount = new(2, 2), RoomSize = new(2, 4),
            TreasureChestCount = new(0, 0), TreasureGold = new(0, 0), RoomEncounters = [], CorridorEncounters = [],
            WallRune = new Rune('▓'), WallColor = ConsoleColor.Red
        };
        Assert(custom.WallRune == new Rune('▓') && custom.WallColor == ConsoleColor.Red,
            "A régi egyedi falbeállítás nem maradt kompatibilis.");
    }

    static void DungeonWallStylesDoNotTurnPoolWallsWalkable()
    {
        var maze = new Maze(16, 12, DungeonWallStyles.WaveStone.Rune);
        var pool = new Room(new Position(3, 3), 7, 6, ContentId: ScaledKingdomDungeons.CrocodilePool);
        maze.AddRoom(pool);
        foreach (var position in pool.InteriorPositions())
            maze.SetTile(position, Maze.Floor);
        ScaledKingdomDungeons.ApplyTerrain(maze);
        Assert(maze.TerrainStyles.Single().Rune != maze.WallRune &&
               pool.InteriorPositions().All(maze.IsWalkable) &&
               !maze.IsWalkable(new Position(0, 0)) && maze.BlocksSight(new Position(0, 0)),
            "A hullámfal és a krokodilmedence rúnája összekeverte a járhatóságot.");
    }
}
