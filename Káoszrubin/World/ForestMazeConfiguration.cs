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
    /// <summary>
    /// A belső terület fákkal/bozóttal borított hányada a tavak, tisztások és ösvények előtt.
    /// 0: nyílt táj; 0.18: elszórt ligetek; 0.55: mozaikos erdő; 0.95–1: sűrű erdő ösvényekkel.
    /// </summary>
    public double ForestDensity { get; init; } = 0.72;

    /// <summary>A facsoportok jellemző térbeli léptéke mezőben; több lépték keveredik, nem egyforma körök.</summary>
    public IntRange GroveSize { get; init; } = new(5, 13);

    /// <summary>A lombos és fenyves tájegységek léptéke; általában nagyobb, mint a facsoportoké.</summary>
    public int BiomeSize { get; init; } = 20;

    /// <summary>A fenyves tájegységek részaránya. A fajok csak a tájegységek határán találkoznak.</summary>
    public double PineChance { get; init; } = 0.16;

    /// <summary>A nyílt erdőszegély bokros, illetve virágos bokros foltjainak aránya.</summary>
    public double BushChance { get; init; } = 0.16;
    public double FlowerBushChance { get; init; } = 0.04;

    /// <summary>A bokorcsoportok térbeli léptéke és az erdőszegély szélessége mezőben.</summary>
    public IntRange BushGroupSize { get; init; } = new(2, 5);
    public int ForestEdgeWidth { get; init; } = 3;

    /// <summary>A fás terület összefüggő, sűrű bozóttal helyettesített részaránya.</summary>
    public double ThicketChance { get; init; } = 0.12;

    /// <summary>A szabad talajon megjelenő összefüggő aljnövényzetfoltok aránya.</summary>
    public double UndergrowthChance { get; init; } = 0.24;
    public double DenseUndergrowthChance { get; init; } = 0.10;

    /// <summary>A képernyőn létrejövő tavak száma és közelítő sugara.</summary>
    public IntRange LakeCount { get; init; } = new(1, 3);
    public IntRange LakeRadius { get; init; } = new(2, 5);

    /// <summary>A mocsaras tópartszakaszok aránya; 0 esetén csak az önálló mocsarak maradnak.</summary>
    public double MarshChance { get; init; } = 0.55;

    /// <summary>A tavaktól független, szabálytalan mocsárfoltok száma és közelítő sugara.</summary>
    public IntRange MarshCount { get; init; } = new(1, 2);
    public IntRange MarshRadius { get; init; } = new(3, 7);

    /// <summary>A fő ösvények szélessége mezőben.</summary>
    public int TrailWidth { get; init; } = 2;

    /// <summary>Az ösvénykanyarok erőssége 0 (közvetlen) és 1 (erősen kanyargó) között.</summary>
    public double TrailWinding { get; init; } = 0.65;

    /// <summary>A tisztások további, hurkot/kerülőutat adó összeköttetésének esélye.</summary>
    public double ExtraTrailChance { get; init; } = 0.30;

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
