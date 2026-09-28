using System.Text;

namespace KaoszRubin.World;

/// <summary>Az erdei pálya rúnái és színei. Minden mezőtípus külön felülírható.</summary>
public sealed class ForestTerrainPalette
{
    public MazeTerrainStyle Tree { get; init; } =
        new("forest-tree", new Rune('♠'), ConsoleColor.DarkGreen, ConsoleColor.Black, false, true);
    public MazeTerrainStyle Bush { get; init; } =
        new("forest-bush", new Rune('♣'), ConsoleColor.Green, ConsoleColor.Black, false, false);
    public MazeTerrainStyle Pine { get; init; } =
        new("forest-pine", new Rune('▲'), ConsoleColor.Green, ConsoleColor.Black, false, true);
    public MazeTerrainStyle FlowerBush { get; init; } =
        new("forest-flower-bush", new Rune('✿'), ConsoleColor.Magenta, ConsoleColor.Black, false, false);
    public MazeTerrainStyle Thicket { get; init; } =
        new("forest-thicket", new Rune('#'), ConsoleColor.DarkGreen, ConsoleColor.Black, false, true);
    public MazeTerrainStyle Undergrowth { get; init; } =
        new("forest-undergrowth", new Rune('░'), ConsoleColor.DarkGreen, ConsoleColor.Black, true, false);
    public MazeTerrainStyle DenseUndergrowth { get; init; } =
        new("forest-dense-undergrowth", new Rune('▒'), ConsoleColor.Green, ConsoleColor.Black, true, false);
    public MazeTerrainStyle Water { get; init; } =
        new("forest-water", new Rune('≈'), ConsoleColor.Blue, ConsoleColor.DarkBlue, false, false);
    public MazeTerrainStyle Marsh { get; init; } =
        new("forest-marsh", new Rune('≋'), ConsoleColor.DarkYellow, ConsoleColor.Black, true, false);
    public MazeTerrainStyle BuildingWall { get; init; } =
        new("forest-building-wall", new Rune('▣'), ConsoleColor.DarkYellow, ConsoleColor.Black, false, true);

    public IReadOnlyList<MazeTerrainStyle> All =>
        [Tree, Bush, Pine, FlowerBush, Thicket, Undergrowth, DenseUndergrowth, Water, Marsh, BuildingWall];
}

/// <summary>Egy erdei képernyő terepeloszlásának szerkesztői beállításai.</summary>
public sealed class ForestGenerationConfiguration
{
    /// <summary>Az összefüggő erdőtömeg sűrűsége 0 és 1 között.</summary>
    public double ForestDensity { get; init; } = 0.72;

    /// <summary>Az akadálymezőkön a fenyők, bokrok, virágos bokrok és sűrű bozót aránya.</summary>
    public double PineChance { get; init; } = 0.16;
    public double BushChance { get; init; } = 0.16;
    public double FlowerBushChance { get; init; } = 0.04;
    public double ThicketChance { get; init; } = 0.12;

    /// <summary>A már járható mezőkön megjelenő aljnövényzet aránya.</summary>
    public double UndergrowthChance { get; init; } = 0.24;
    public double DenseUndergrowthChance { get; init; } = 0.10;

    /// <summary>A képernyőn létrejövő tavak száma és közelítő sugara.</summary>
    public IntRange LakeCount { get; init; } = new(1, 3);
    public IntRange LakeRadius { get; init; } = new(2, 5);

    /// <summary>A tóparttal szomszédos járható mezők mocsárrá válási esélye.</summary>
    public double MarshChance { get; init; } = 0.55;

    /// <summary>A fő ösvények szélessége mezőben.</summary>
    public int TrailWidth { get; init; } = 2;

    /// <summary>
    /// A képernyőn létrehozott, ajtóval lezárt erdei épületek száma. Az épületek a
    /// <see cref="MazeLevelConfiguration.RoomCount"/> által meghatározott teljes teremszámba beleszámítanak.
    /// </summary>
    public IntRange BuildingCount { get; init; } = new(0, 1);

    /// <summary>Az épületek belső terének minimális és maximális oldalhossza.</summary>
    public IntRange BuildingSize { get; init; } = new(5, 8);

    /// <summary>Ekkora eséllyel osztja egy belső fal és ajtó két helyiségre az épületet.</summary>
    public double BuildingPartitionChance { get; init; } = 0.70;

    /// <summary>A bejárati és belső ajtók zárt, illetve nyitott állapotának esélye.</summary>
    /// <remarks>A fennmaradó esély bezárt, de nem kulcsra zárt ajtót eredményez.</remarks>
    public double LockedBuildingDoorChance { get; init; } = 0.20;
    public double OpenBuildingDoorChance { get; init; } = 0.15;

    public ForestTerrainPalette Palette { get; init; } = new();
}
