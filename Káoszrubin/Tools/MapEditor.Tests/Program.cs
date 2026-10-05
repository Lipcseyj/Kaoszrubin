using System.Reflection;
using System.Text.Json;
using KaoszRubin.Domain.Combat;
using KaoszRubin.MapEditor;
using KaoszRubin.World;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var tests = new (string Name, Action Test)[]
        {
            ("Minden gyári típus és az egyedi csoport oda-vissza alakítható", FactoriesRoundTrip),
            ("Minden kampánytalálkozás veszteség nélkül szerkeszthető", CampaignRoundTrip),
            ("Név szerinti paraméterek, terepek és felülírások megmaradnak", OverridesRoundTrip),
            ("A hibás és futtatható kifejezéseket a szerkesztő elutasítja", InvalidExpressions),
            ("Csak az adott helyen érvényes célok fogadhatók el", ContextValidation),
            ("Az űrlap betöltése és alkalmazása megőrzi a találkozást", DialogRoundTrip),
            ("Az űrlap módosítása új C# kifejezést készít", DialogEditing),
            ("Az egyedi csoportban tagok hozzáadhatók, módosíthatók és törölhetők", CustomMembersEditing),
            ("A generált kifejezések C# fordítóval is érvényesek", CompileExpressions),
            ("A találkozások mentése csak a kijelölt listát módosítja", SaveAndReload)
        };
        var failed = 0;
        foreach (var (name, test) in tests)
            try { test(); Console.WriteLine("PASS " + name); }
            catch (Exception exception) { failed++; Console.WriteLine("FAIL " + name + ": " + exception); }
        Console.WriteLine($"{tests.Length - failed} sikeres, {failed} hibás.");
        if (args is ["--preview", var path])
        {
            using var dialog = new EncounterEditorDialog(new EncounterDraft("Mixed"), Context(), Monsters);
            dialog.ShowInTaskbar = false;
            dialog.Opacity = 0;
            dialog.Show();
            Application.DoEvents();
            using var bitmap = new System.Drawing.Bitmap(dialog.Width, dialog.Height);
            dialog.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height));
            bitmap.Save(path);
            dialog.Close();
        }
        return failed == 0 ? 0 : 1;
    }

    private static readonly IReadOnlyDictionary<string, string> Monsters = EncounterDraft.Monsters
        .GroupBy(entry => entry.Value).ToDictionary(group => group.Key, group => group.First().Key);
    private static EncounterEditorContext Context(bool forest = true, bool rooms = true) => new(forest, rooms, 2,
        ["A", "B"], forest ? [RoomKind.Clearing, RoomKind.Cabin, RoomKind.Manor, RoomKind.Labyrinth] : [RoomKind.Generic],
        Monsters.Keys.ToHashSet(), forest ? new Dictionary<string, IReadOnlyList<RoomKind>>
        { ["A"] = [RoomKind.Clearing, RoomKind.Cabin], ["B"] = [RoomKind.Clearing, RoomKind.Manor] } : null);
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static string Canonical(EnemyEncounterConfiguration configuration) => JsonSerializer.Serialize(configuration);

    private static void FactoriesRoundTrip()
    {
        foreach (var name in EncounterDraft.Factories.Keys.Append(EncounterDraft.Custom))
        {
            var draft = new EncounterDraft(name);
            if (name == "TerrainAmbush") draft.Arguments["terrainTags"] = TerrainTag.Bush | TerrainTag.Marsh;
            Context().Validate(draft.Configuration());
            var parsed = EncounterDraft.Parse(draft.Expression());
            Assert(Canonical(draft.Configuration()) == Canonical(parsed.Configuration()), name);
        }
    }

    private static void CampaignRoundTrip()
    {
        var count = 0;
        for (var level = 1; level <= MazeLevelConfigurations.FinalLevel; level++)
        {
            string block;
            try { block = EditorSources.ReadLevel(level); }
            catch (InvalidOperationException) { continue; } // Generált kampányszintnek nincs saját forrásblokkja.
            foreach (var property in new[] { "RoomEncounters", "CorridorEncounters" })
            foreach (var expression in EditorSources.CollectionItems(EditorSources.Property(block, property)))
            {
                var draft = EncounterDraft.Parse(expression);
                var roundTrip = EncounterDraft.Parse(draft.Expression());
                Assert(Canonical(draft.Configuration()) == Canonical(roundTrip.Configuration()), $"{level}: {expression}");
                count++;
            }
        }
        Assert(count > 100, "Túl kevés kampánytalálkozás lett ellenőrizve.");
        Console.WriteLine($"  {count} meglévő C# találkozás ellenőrizve.");
    }

    private static void OverridesRoundTrip()
    {
        var source = "Encounters.Mixed(secondEnemyId: MonsterIds.Ork, firstEnemyId: MonsterIds.Goblin, " +
            "firstCount: Amount.Pair, secondCount: Amount.Few, groups: Amount.Several, movement: null) " +
            "with { AreaId = \"A\", TargetRoomKind = RoomKind.Cabin, Posture = EnemyEncounterPosture.Ambush, " +
            "TargetTerrainTags = TerrainTag.Marsh | TerrainTag.Bush, TriggerDistance = 6 }";
        var draft = EncounterDraft.Parse(source);
        Context().Validate(draft.Configuration() with { TargetRoomKind = null });
        Assert(Canonical(draft.Configuration()) == Canonical(EncounterDraft.Parse(draft.Expression()).Configuration()), "Felülírás elveszett.");
        var custom = EncounterDraft.Parse("new EnemyEncounterConfiguration(new(1, 3), [new(MonsterIds.Ork, new(2, 4), EnemyGroupRole.Leader), new(\"E003\", Amount.One.Range())], ScreenNumber: 2)");
        Assert(custom.Configuration().Members.Count == 2 && custom.Configuration().Members[0].Role == EnemyGroupRole.Leader,
            "Az egyedi csoport tagjai vagy szerepei elvesztek.");
        Assert(Canonical(custom.Configuration()) == Canonical(EncounterDraft.Parse(custom.Expression()).Configuration()), "Egyedi csoport elveszett.");
        var escaped = EncounterDraft.Parse("Encounters.Solo(\"idegen\\\"id\", Amount.One) with { AreaId = \"út\\\\cél\" }");
        Assert(Canonical(escaped.Configuration()) == Canonical(EncounterDraft.Parse(escaped.Expression()).Configuration()), "C# szöveges escaping hibás.");
    }

    private static void InvalidExpressions()
    {
        foreach (var expression in new[]
        {
            "File.Delete(\"x\")", "Encounters.Solo(GetMonster(), Amount.One)", "Encounters.Solo(MonsterIds.Goblin)",
            "Encounters.Solo(MonsterIds.Goblin, Amount.One, count: Amount.Few)",
            "Encounters.Solo(MonsterIds.Goblin, Amount.One) with { Missing = 1 }", "Encounters.Solo("
        })
        {
            var rejected = false;
            try { EncounterDraft.Parse(expression); } catch (FormatException) { rejected = true; }
            Assert(rejected, expression);
        }
    }

    private static void ContextValidation()
    {
        var source = new EncounterDraft("Same").Configuration();
        foreach (var invalid in new[]
        {
            source with { ScreenNumber = 0 }, source with { ScreenNumber = 3 }, source with { AreaId = "missing" },
            source with { ScreenNumber = 1, AreaId = "A" }, source with { AreaId = "A", TargetRoomKind = RoomKind.Manor },
            source with { TargetRoomKind = RoomKind.Clearing, TargetTerrainTags = TerrainTag.Marsh },
            source with { GroupCount = new(5, 1) }, source with { Members = [] },
            source with { Members = [new("missing", new(1, 1))] },
            source with { Posture = EnemyEncounterPosture.Ambush }, source with { Members = [new(MonsterIds.Goblin, new(0, 1))] }
        }) Reject(Context(), invalid);
        Reject(Context(rooms: false), source with { TargetRoomKind = RoomKind.Clearing });
        Reject(Context(forest: false), source with { TargetTerrainTags = TerrainTag.Marsh });
        Context().Validate(source with { AreaId = "B", TargetRoomKind = RoomKind.Manor });
        Assert(Context().KindsFor("A").SequenceEqual(new[] { RoomKind.Clearing, RoomKind.Cabin }), "Szobatípus-szűrés hibás.");

        static void Reject(EncounterEditorContext context, EnemyEncounterConfiguration configuration)
        {
            try { context.Validate(configuration); } catch (InvalidDataException) { return; }
            throw new Exception("Érvénytelen kontextust fogadott el: " + Canonical(configuration));
        }
    }

    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private static void DialogRoundTrip()
    {
        foreach (var kind in EncounterDraft.Factories.Keys.Append(EncounterDraft.Custom))
        {
            var draft = new EncounterDraft(kind);
            if (kind == "TerrainAmbush") draft.Arguments["terrainTags"] = TerrainTag.Bush;
            draft.Overrides["AreaId"] = "B";
            draft.Overrides["TargetRoomKind"] = kind == "TerrainAmbush" ? null : RoomKind.Manor;
            var original = Canonical(draft.Configuration());
            using var dialog = new EncounterEditorDialog(draft, Context(), Monsters);
            var button = Field<Button>(dialog, "_accept");
            Assert(button.Enabled, Field<Label>(dialog, "_error").Text);
            var preview = Field<TextBox>(dialog, "_preview");
            Assert(preview.ReadOnly && Canonical(EncounterDraft.Parse(preview.Text).Configuration()) == original, kind + " megváltozott az űrlapon.");
        }
    }

    private static void DialogEditing()
    {
        using var dialog = new EncounterEditorDialog(new EncounterDraft("Same"), Context(), Monsters);
        var fields = Field<FlowLayoutPanel>(dialog, "_fields");
        var combos = fields.Controls.Cast<Control>().SelectMany(row => row.Controls.Cast<Control>()).OfType<ComboBox>().ToArray();
        combos[0].SelectedIndex = 1; // A szörnylista második eleme.
        var target = Field<ComboBox>(dialog, "_target");
        target.SelectedIndex = 3; // A terület, az automatikus és a két sorszám után.
        var room = Field<ComboBox>(dialog, "_roomKind");
        room.SelectedIndex = 2; // Kunyhó.
        var edited = EncounterDraft.Parse(Field<TextBox>(dialog, "_preview").Text).Configuration();
        Assert(edited.AreaId == "A" && edited.ScreenNumber is null && edited.TargetRoomKind == RoomKind.Cabin &&
            edited.Members[0].EnemyId == Monsters.Keys.ElementAt(1), "Az űrlap nem írta át a kifejezést.");
        target.SelectedIndex = 4; // B-n nincs kunyhó: ne változzon észrevétlenül automatikusra.
        Assert(!Field<Button>(dialog, "_accept").Enabled, "A célváltás érvénytelen szobatípusát nem jelezte.");
        room.SelectedIndex = 0;
        Assert(Field<Button>(dialog, "_accept").Enabled, "A cél javítása után nem alkalmazható.");
    }

    private static void CompileExpressions()
    {
        var expressions = EncounterDraft.Factories.Keys.Append(EncounterDraft.Custom).Select(kind =>
        {
            var draft = new EncounterDraft(kind);
            draft.Overrides["AreaId"] = "A";
            draft.Overrides["TargetRoomKind"] = RoomKind.Cabin;
            draft.Overrides["TargetTerrainTags"] = TerrainTag.Marsh | TerrainTag.Bush;
            return draft.Expression();
        }).ToArray();
        var source = "using KaoszRubin.World; using KaoszRubin.Domain.Combat; class Generated { " +
            "public static EnemyEncounterConfiguration[] All() => [" + string.Join(",", expressions) + "]; }";
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(Encounters).Assembly.Location).Distinct().Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("GeneratedEncounters", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var assembly = new MemoryStream();
        var result = compilation.Emit(assembly);
        Assert(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
    }

    private static void CustomMembersEditing()
    {
        using var dialog = new EncounterEditorDialog(new EncounterDraft(EncounterDraft.Custom), Context(), Monsters);
        var grid = Field<FlowLayoutPanel>(dialog, "_fields").Controls.OfType<DataGridView>().Single();
        grid.Rows.Add(MonsterIds.Ork, 2, 4, EnemyGroupRole.Leader);
        grid.Rows[0].Cells[1].Value = 3;
        grid.Rows[0].Cells[2].Value = 5;
        var configuration = EncounterDraft.Parse(Field<TextBox>(dialog, "_preview").Text).Configuration();
        Assert(configuration.Members.Count == 2 && configuration.Members[0].Count == new IntRange(3, 5) &&
            configuration.Members[1].EnemyId == MonsterIds.Ork && configuration.Members[1].Role == EnemyGroupRole.Leader,
            "A tagok hozzáadása vagy módosítása elveszett.");
        grid.Rows.RemoveAt(0);
        configuration = EncounterDraft.Parse(Field<TextBox>(dialog, "_preview").Text).Configuration();
        Assert(configuration.Members.Count == 1 && configuration.Members[0].EnemyId == MonsterIds.Ork, "A törölt tag megmaradt.");
        grid.Rows[0].Cells[1].Value = "hibás";
        Assert(!Field<Button>(dialog, "_accept").Enabled, "Hibás darabszámot fogadott el az űrlap.");
        grid.Rows[0].Cells[1].Value = 1;
        Assert(Field<Button>(dialog, "_accept").Enabled, "A javított darabszámot nem fogadja el.");
    }

    private static void SaveAndReload()
    {
        var source = File.ReadAllText(EditorSources.PathFor("World/MazeLevelConfiguration.cs"));
        var before = EditorSources.LevelBlock(source, 6);
        var items = EditorSources.CollectionItems(EditorSources.Property(before, "RoomEncounters")).ToList();
        items.RemoveAt(0);
        var edited = EncounterDraft.Parse(items[0]);
        edited.Overrides["AreaId"] = "A";
        items[0] = edited.Expression();
        items.Add(new EncounterDraft("MixedHorde").Expression());
        var updated = EditorSources.UpdateLevelSource(source, 6,
            new Dictionary<string, string> { ["RoomEncounters"] = "[" + string.Join(",\n", items) + "]" });
        var after = EditorSources.LevelBlock(updated, 6);
        Assert(EditorSources.CollectionItems(EditorSources.Property(after, "RoomEncounters")).SequenceEqual(items),
            "A hozzáadás/szerkesztés/törlés nem maradt meg a forrásban.");
        Assert(EditorSources.Property(before, "CorridorEncounters") == EditorSources.Property(after, "CorridorEncounters") &&
            source.Replace(before, "<level>") == updated.Replace(after, "<level>"), "Másik lista vagy pálya megváltozott.");
    }
}
