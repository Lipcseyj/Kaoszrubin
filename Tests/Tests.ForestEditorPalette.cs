using System.ComponentModel;

internal static partial class Program
{
    static void ForestEditorPaletteChangesSurviveSave()
    {
        var common = ((ForestMazeLayoutConfiguration)MazeLevelConfigurations.Get(6).Layout!).Forest;
        var graph = new ExplicitForestAreaGraphConfiguration(
            [new("A", "Első láp", new(0, 0), ForestAreaTemplateCatalog.BlackwaterBog),
             new("B", "Második láp", new(1, 0), ForestAreaTemplateCatalog.BlackwaterBog)],
            [new("A", "B")], "A", "B");
        var inherited = ForestAreaConfigurationResolver.Resolve(common, graph, graph.Areas[0]);
        var originalMarsh = inherited.Palette.Marsh with { };
        var edited = ForestConfigurationEditing.Snapshot(inherited);
        Assert(ForestConfigurationEditing.Difference(edited, inherited) is null,
            "A változatlan másolat felesleges felülírásokat mentene.");

        // Ugyanaz a PropertyDescriptor.SetValue útvonal, amelyet a WinForms PropertyGrid használ.
        Set(edited.Palette.Marsh, nameof(MazeTerrainStyle.ForegroundColor), ConsoleColor.Green);
        Set(edited.Palette.Marsh, nameof(MazeTerrainStyle.BackgroundColor), ConsoleColor.DarkMagenta);
        Set(edited.Palette.Marsh, nameof(MazeTerrainStyle.Rune), new Rune('~'));
        Assert(inherited.Palette.Marsh == originalMarsh &&
               ForestAreaConfigurationResolver.Resolve(common, graph, graph.Areas[1]).Palette.Marsh == originalMarsh,
            "A szerkesztés átírta a sablont vagy a másik képernyőt.");
        var patch = ForestConfigurationEditing.Difference(edited, inherited);
        Assert(patch?.Palette is not null && patch.BuildingStyles is null && patch.GroveSize is null,
            "A palettaváltozás hiányzik vagy más beállításokat is feleslegesen felülír.");
        var saved = graph with { Areas = [graph.Areas[0] with { Overrides = patch }, graph.Areas[1]] };
        var json = ForestConfigurationJson.Serialize(6, saved);
        using (var parsed = JsonDocument.Parse(json))
        {
            var marsh = parsed.RootElement.GetProperty("Graph").GetProperty("Areas")[0]
                .GetProperty("Overrides").GetProperty("Palette").GetProperty("Marsh");
            Assert(marsh.GetProperty("ForegroundColor").GetString() == "Green" &&
                   marsh.GetProperty("Rune").GetString() == "~", "A mocsár módosításai hiányoznak a JSON-ból.");
        }
        var restored = ForestConfigurationJson.DeserializeDocument(json);
        var resolved = ForestAreaConfigurationResolver.Resolve(common, restored.Graph, restored.Graph.Areas[0]);
        Assert(resolved.Palette.All.SequenceEqual(edited.Palette.All) &&
               ForestAreaConfigurationResolver.Resolve(common, restored.Graph, restored.Graph.Areas[1]).Palette.Marsh == originalMarsh,
            "A visszatöltés elvesztette a színeket/rúnákat, vagy átírta a másik képernyőt.");
        var reopened = ForestConfigurationEditing.Snapshot(resolved);
        Assert(ForestConfigurationJson.Serialize(6, saved with
            { Areas = [saved.Areas[0] with { Overrides = ForestConfigurationEditing.Difference(reopened, inherited) }, saved.Areas[1]] }) == json,
            "Az ismételt mentés megváltoztatta a helyi felülírást.");
        Set(edited.Palette.Marsh, nameof(MazeTerrainStyle.ForegroundColor), ConsoleColor.Red);
        Assert(patch!.Palette!.Marsh.ForegroundColor == ConsoleColor.Green,
            "A későbbi szerkesztés módosította a már alkalmazott felülírást.");
        Set(reopened.Palette.Marsh, nameof(MazeTerrainStyle.ForegroundColor), originalMarsh.ForegroundColor);
        Set(reopened.Palette.Marsh, nameof(MazeTerrainStyle.BackgroundColor), originalMarsh.BackgroundColor);
        Set(reopened.Palette.Marsh, nameof(MazeTerrainStyle.Rune), originalMarsh.Rune);
        Assert(ForestConfigurationEditing.Difference(reopened, inherited) is null,
            "Az eredeti értékek visszaállítása után megmaradt a felesleges palettafelülírás.");

        var legacyJson = json.Replace("\"Rune\": \"~\"", "\"Rune\": {\"Value\":126}");
        Assert(ForestConfigurationJson.Deserialize(legacyJson).Areas[0].Overrides!.Palette!.Marsh.Rune == new Rune('~'),
            "A korábbi objektumos rúnaformátum nem tölthető be.");
        var copy = ForestConfigurationEditing.Snapshot(common);
        Set(copy.GroveSize, nameof(IntRange.Minimum), 99);
        Set(copy.BuildingStyles[0].Wall, nameof(MazeTerrainStyle.ForegroundColor), ConsoleColor.Magenta);
        Assert(common.GroveSize.Minimum != 99 && !ReferenceEquals(copy.BuildingStyles[0].Wall, common.BuildingStyles[0].Wall),
            "Az összetett tulajdonságok másolata megosztott szerkeszthető objektumot tartalmaz.");

        static void Set(object target, string name, object value) =>
            TypeDescriptor.GetProperties(target)[name]!.SetValue(target, value);
    }
}
