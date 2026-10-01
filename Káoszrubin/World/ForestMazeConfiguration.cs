using System.Text;
using System.ComponentModel;

namespace KaoszRubin.World;

public enum ForestBuildingLayout { Cabin, Manor, Labyrinth }

[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed record ForestBuildingStyleDefinition(
    string Id,
    MazeTerrainStyle Wall,
    double Weight = 1,
    [property: TypeConverter(typeof(ForestBuildingLayoutsTypeConverter))]
    IReadOnlySet<ForestBuildingLayout>? AllowedLayouts = null)
{
    public bool Allows(ForestBuildingLayout layout) => AllowedLayouts is null || AllowedLayouts.Contains(layout);
}

/// <summary>Az erdei pálya rúnái és színei. Minden mezőtípus külön felülírható.</summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class ForestTerrainPalette
{
    public MazeTerrainStyle Tree { get; init; } =
        new("forest-tree", new Rune('♠'), ConsoleColor.DarkGreen, ConsoleColor.Black, false, true);
    public MazeTerrainStyle Bush { get; init; } =
        new("forest-bush", new Rune('♣'), ConsoleColor.Green, ConsoleColor.Black, true, false);
    public MazeTerrainStyle Pine { get; init; } =
        new("forest-pine", new Rune('▲'), ConsoleColor.Green, ConsoleColor.Black, false, true);
    public MazeTerrainStyle FlowerBush { get; init; } =
        new("forest-flower-bush", new Rune('✿'), ConsoleColor.Magenta, ConsoleColor.Black, true, false);
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
        new("forest-building-wall", new Rune('█'), ConsoleColor.DarkYellow, ConsoleColor.Black, false, true);

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
    [Description("A belső terület fákkal és bozóttal borított aránya 0 és 1 között.")]
    public double ForestDensity { get; init; } = 0.72;

    /// <summary>A facsoportok jellemző térbeli léptéke mezőben; több lépték keveredik, nem egyforma körök.</summary>
    [Description("A facsoportok jellemző mérettartománya mezőben.")]
    public IntRange GroveSize { get; init; } = new(5, 13);

    /// <summary>A lombos és fenyves tájegységek léptéke; általában nagyobb, mint a facsoportoké.</summary>
    [Description("A lombos és fenyves tájegységek térbeli léptéke.")]
    public int BiomeSize { get; init; } = 20;

    /// <summary>A fenyves tájegységek részaránya. A fajok csak a tájegységek határán találkoznak.</summary>
    [Description("A fenyves tájegységek részaránya.")]
    public double PineChance { get; init; } = 0.16;

    /// <summary>A nyílt erdőszegély bokros, illetve virágos bokros foltjainak aránya.</summary>
    [Description("A bokros foltok aránya az erdőszegélyen.")]
    public double BushChance { get; init; } = 0.16;
    [Description("A virágos bokorfoltok aránya az erdőszegélyen.")]
    public double FlowerBushChance { get; init; } = 0.04;

    /// <summary>A bokorcsoportok térbeli léptéke és az erdőszegély szélessége mezőben.</summary>
    [Description("A bokorcsoportok mérettartománya.")]
    public IntRange BushGroupSize { get; init; } = new(2, 5);
    [Description("Az erdőszegély szélessége mezőben.")]
    public int ForestEdgeWidth { get; init; } = 3;

    /// <summary>A fás terület összefüggő, sűrű bozóttal helyettesített részaránya.</summary>
    [Description("A fás terület sűrű bozóttá váló aránya.")]
    public double ThicketChance { get; init; } = 0.12;

    /// <summary>A szabad talajon megjelenő összefüggő aljnövényzetfoltok aránya.</summary>
    [Description("A szabad talaj aljnövényzettel borított aránya.")]
    public double UndergrowthChance { get; init; } = 0.24;
    [Description("A szabad talaj sűrű aljnövénnyel borított aránya.")]
    public double DenseUndergrowthChance { get; init; } = 0.10;

    /// <summary>A képernyőn létrejövő tavak száma és közelítő sugara.</summary>
    [Description("A tavak számának tartománya képernyőnként.")]
    public IntRange LakeCount { get; init; } = new(1, 3);
    [Description("A tavak közelítő sugártartománya.")]
    public IntRange LakeRadius { get; init; } = new(2, 5);

    /// <summary>A mocsaras tópartszakaszok aránya; 0 esetén csak az önálló mocsarak maradnak.</summary>
    [Description("A mocsarassá váló tópartszakaszok aránya.")]
    public double MarshChance { get; init; } = 0.55;

    /// <summary>A tavaktól független, szabálytalan mocsárfoltok száma és közelítő sugara.</summary>
    [Description("Az önálló mocsárfoltok számának tartománya.")]
    public IntRange MarshCount { get; init; } = new(1, 2);
    [Description("A mocsárfoltok közelítő sugártartománya.")]
    public IntRange MarshRadius { get; init; } = new(3, 7);

    /// <summary>A fő ösvények szélessége mezőben.</summary>
    [Description("A fő erdei ösvények szélessége mezőben.")]
    public int TrailWidth { get; init; } = 2;

    /// <summary>Az ösvénykanyarok erőssége 0 (közvetlen) és 1 (erősen kanyargó) között.</summary>
    [Description("Az ösvények kanyargása 0 és 1 között.")]
    public double TrailWinding { get; init; } = 0.65;

    /// <summary>A tisztások további, hurkot/kerülőutat adó összeköttetésének esélye.</summary>
    [Description("A tisztások közötti extra kerülőutak esélye.")]
    public double ExtraTrailChance { get; init; } = 0.30;

    /// <summary>
    /// A képernyőn létrehozott, ajtóval lezárt erdei épületek száma. Az épületek a
    /// <see cref="MazeLevelConfiguration.RoomCount"/> által meghatározott teljes teremszámba beleszámítanak.
    /// </summary>
    [Description("Az erdei épületek számának tartománya.")]
    public IntRange BuildingCount { get; init; } = new(0, 1);

    /// <summary>Az épületek belső terének minimális és maximális oldalhossza.</summary>
    [Description("A kunyhók belső oldalméretének tartománya.")]
    public IntRange BuildingSize { get; init; } = new(5, 8);

    /// <summary>A kunyhók esélye egyetlen belső válaszfalra.</summary>
    [Description("Annak esélye, hogy egy kunyhó belső válaszfalat kap.")]
    public double BuildingPartitionChance { get; init; } = 0.70;

    /// <summary>
    /// Az épületek alaprajztípusainak esélyei. A fennmaradó rész kis, egy- vagy kétszobás kunyhó.
    /// </summary>
    [Description("A kúria alaprajzú épületek esélye.")]
    public double ManorBuildingChance { get; init; } = 0.45;
    [Description("A labirintus alaprajzú épületek esélye.")]
    public double LabyrinthBuildingChance { get; init; } = 0.15;

    /// <summary>A rekurzívan szobákra osztott nagy épületek mérete és szobaszáma.</summary>
    [Description("A kúriák szélességtartománya.")]
    public IntRange ManorBuildingWidth { get; init; } = new(10, 18);
    [Description("A kúriák magasságtartománya.")]
    public IntRange ManorBuildingHeight { get; init; } = new(8, 14);
    [Description("A kúriák célzott szobaszámának tartománya.")]
    public IntRange ManorRoomCount { get; init; } = new(3, 8);
    [Description("Egy generált épületszoba minimális oldalmérete.")]
    public int BuildingMinimumRoomSize { get; init; } = 3;

    /// <summary>A helyi folyosóhálózattal készülő labirintusépületek mérete.</summary>
    [Description("A labirintusépületek szélességtartománya.")]
    public IntRange LabyrinthBuildingWidth { get; init; } = new(13, 22);
    [Description("A labirintusépületek magasságtartománya.")]
    public IntRange LabyrinthBuildingHeight { get; init; } = new(8, 17);

    /// <summary>Plusz belső ajtók és lehetséges második külső bejárat esélye.</summary>
    [Description("A szomszédos helyiségek közötti extra ajtók esélye.")]
    public double BuildingExtraConnectionChance { get; init; } = 0.18;
    [Description("Annak esélye, hogy a nagy épület második külső bejáratot kap.")]
    public double BuildingSecondEntranceChance { get; init; } = 0.20;

    /// <summary>
    /// Súlyozott épületfal-stílusok. Üres listánál a Palette.BuildingWall marad az egyetlen stílus.
    /// </summary>
    [Description("Az épületekhez súlyozottan választható falstílusok. A nyíllal lenyitható.")]
    [TypeConverter(typeof(ForestBuildingStylesTypeConverter))]
    public IReadOnlyList<ForestBuildingStyleDefinition> BuildingStyles { get; init; } = [];

    /// <summary>A bejárati és belső ajtók zárt, illetve nyitott állapotának esélye.</summary>
    /// <remarks>A fennmaradó esély bezárt, de nem kulcsra zárt ajtót eredményez.</remarks>
    [Description("A kulcsra zárt épületajtók esélye.")]
    public double LockedBuildingDoorChance { get; init; } = 0.20;
    [Description("A kezdetben nyitott épületajtók esélye.")]
    public double OpenBuildingDoorChance { get; init; } = 0.15;

    [Description("Az erdei tereptípusok rúnái, színei, járhatósága és látástakarása.")]
    public ForestTerrainPalette Palette { get; init; } = new();
}

public sealed class ForestBuildingLayoutsTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string);
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(string);
    public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object value)
    {
        var text = value.ToString()?.Trim();
        if (string.IsNullOrEmpty(text) || text == "*") return null;
        return text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(Enum.Parse<ForestBuildingLayout>).ToHashSet();
    }
    public override object? ConvertTo(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture,
        object? value, Type destinationType) => destinationType == typeof(string)
        ? value is IReadOnlySet<ForestBuildingLayout> layouts ? string.Join(", ", layouts) : "*"
        : base.ConvertTo(context, culture, value, destinationType);
}

public sealed class ForestBuildingStylesTypeConverter : ExpandableObjectConverter
{
    public override bool GetPropertiesSupported(ITypeDescriptorContext? context) => true;
    public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext? context, object value,
        Attribute[]? attributes) => value is IReadOnlyList<ForestBuildingStyleDefinition> styles
        ? new PropertyDescriptorCollection(styles.Select((_, index) =>
            (PropertyDescriptor)new StyleItemDescriptor(index)).ToArray())
        : new PropertyDescriptorCollection([]);
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
    public override object? ConvertTo(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture,
        object? value, Type destinationType) => destinationType == typeof(string) &&
        value is IReadOnlyList<ForestBuildingStyleDefinition> styles
        ? $"{styles.Count} falstílus" : base.ConvertTo(context, culture, value, destinationType);

    private sealed class StyleItemDescriptor(int index) : PropertyDescriptor($"[{index}]", null)
    {
        public override Type ComponentType => typeof(IReadOnlyList<ForestBuildingStyleDefinition>);
        public override bool IsReadOnly => true;
        public override Type PropertyType => typeof(ForestBuildingStyleDefinition);
        public override object? GetValue(object? component) =>
            component is IReadOnlyList<ForestBuildingStyleDefinition> styles ? styles[index] : null;
        public override void SetValue(object? component, object? value) { }
        public override bool CanResetValue(object component) => false;
        public override void ResetValue(object component) { }
        public override bool ShouldSerializeValue(object component) => false;
    }
}
