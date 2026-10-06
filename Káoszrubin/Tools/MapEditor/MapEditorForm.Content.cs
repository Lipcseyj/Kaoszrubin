using System.Text.RegularExpressions;
using KaoszRubin.World;

namespace KaoszRubin.MapEditor;

internal sealed partial class MapEditorForm
{
    private const string InformativeNpcNameColumn = "NpcName";
    private readonly TabPage _forestTab = new("Erdős pálya");
    private readonly TabPage _mazeTab = new("Labirintus pálya");
    private readonly TabPage _encountersTab = new("Találkozások");
    private readonly TabPage _npcsTab = new("NPC-k");
    private readonly TabControl _mainTabs = new() { Dock = DockStyle.Fill };
    private readonly TabControl _npcTabs = new() { Dock = DockStyle.Fill };
    private readonly Dictionary<string, TextBox> _levelFields = [];
    private readonly Dictionary<string, DataGridView> _levelDictionaryFields = [];
    private readonly ToolTip _mazeToolTip = new() { AutoPopDelay = 18000, InitialDelay = 350, ReshowDelay = 150, ShowAlways = true };
    private readonly ListBox _roomEncounterList = new() { Dock = DockStyle.Fill };
    private readonly ListBox _corridorEncounterList = new() { Dock = DockStyle.Fill };
    private readonly TextBox _encounterExpression = new() { Dock = DockStyle.Top, Multiline = true, Height = 94, ScrollBars = ScrollBars.Vertical };
    private readonly TabControl _encounterKinds = new() { Dock = DockStyle.Fill };
    private readonly Dictionary<string, DataGridView> _csvGrids = [];
    private readonly Dictionary<string, Label> _csvDirtyIndicators = [];
    private readonly CheckBox _showAllNpcs = new() { Text = "Összes pálya NPC-i", AutoSize = true };
    private readonly ComboBox _npcEncounterSelector = new() { Width = 270, DropDownStyle = ComboBoxStyle.DropDownList };
    private IReadOnlyDictionary<string, string> _npcNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private bool _refreshingContent;
    private int? _encounterLevel;

    private Control BuildTabs()
    {
        _forestTab.Controls.Add(BuildInspector());
        _mazeTab.Controls.Add(BuildMazeTab());
        _encountersTab.Controls.Add(BuildEncountersTab());
        _npcsTab.Controls.Add(BuildNpcTab());
        _mainTabs.TabPages.AddRange([_forestTab, _mazeTab, _encountersTab, _npcsTab]);
        return _mainTabs;
    }

    private Control BuildMazeTab()
    {
        var outer = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10) };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true,
            FlowDirection = FlowDirection.TopDown, WrapContents = false, Width = 390 };
        flow.Controls.Add(Heading("Pályakonfiguráció"));
        flow.Controls.Add(Description("Az értékek a MazeLevelConfiguration.cs kiválasztott pályájába kerülnek. C# kifejezések is megadhatók."));
        foreach (var (name, label, hint, help) in new (string, string, string, string)[]
        {
            ("Name", "Pálya neve", "A játékban látható név.", "C# szövegként add meg, például: \"A holtak katakombái\"."),
            ("Layout", "Képernyők elrendezése", "Üresen klasszikus; WideMazeLayout több képernyőt ad.", "Példa: new WideMazeLayoutConfiguration(new IntRange(2, 3), NarrowingChance: 0.12). Az üres mező a meglévő alapértelmezést hagyja érvényben."),
            ("RoomCount", "Termek száma", "Az egész pályán generált termek darabszáma.", "Zárt tartomány: new(8, 12), vagy Amount.Pack.Range(). Több képernyőn a termek eloszlanak."),
            ("RoomSize", "Teremméret", "A generált termek oldalhosszának tartománya.", "Példa: new(4, 7) azt jelenti, hogy a terem mérete 4 és 7 mező közé eshet."),
            ("TreasureChestCount", "Kincsesládák száma", "Az egész pályán elhelyezett ládák száma.", "Zárt darabszámtartomány, például new(3, 6) vagy Amount.Several.Range()."),
            ("TreasureGold", "Arany ládánként", "Egy véletlen kincs aranymennyiségének tartománya.", "Példa: new(240, 480). A két határérték is lehetséges eredmény."),
            ("WallRune", "Fal karaktere", "A labirintus falainak megjelenő jele.", "C# Rune kifejezés, például new('▓'). Erdőben a helyi terepsablonok is befolyásolják a megjelenést."),
            ("WallColor", "Fal színe", "A fal alapértelmezett konzolszíne.", "ConsoleColor érték, például ConsoleColor.DarkGray."),
            ("DoubleWidthCorridorChance", "Dupla széles folyosó esélye", "0 és 1 közötti arány; klasszikus layoutnál hat.", "Példa: 0.82 = 82%. WideMazeLayout esetén a NarrowingChance szabályozza a szűkületeket."),
            ("QuestRoomIds", "Küldetésszobák azonosítói", "Garantáltan létrejövő különleges szobák.", "Azonosítók listája, például [\"KING_CHEST_ROOM\"]. A többi quest-szobamező ezekre hivatkozhat."),
            ("BossRoomIds", "Boss-szobák azonosítói", "Garantáltan létrejövő boss-szobák.", "Azonosítók listája, például [\"GOBLIN_CHIEF_ROOM\"]."),
            ("SpecialRoomPlacements", "Különleges szobák helye", "Szobánként főút vagy mellékág választható.", "Szobaazonosítót SpecialRoomPlacement.MiddleRoute vagy SideBranch értékhez rendelő C# szótár."),
            ("QuestChestPlacements", "Küldetésládák helye", "Szobaazonosítót questláda-azonosítóhoz köt.", "A láda azonosítója a game-data.csv Quest ládák szekciójában szerepeljen."),
            ("QuestDoorRequirements", "Küldetéshez kötött ajtók", "A szobába lépéshez szükséges questet adja meg.", "Szobaazonosítót QuestId értékhez rendelő C# szótár."),
            ("QuestRoomEnemyEncounters", "Garantált szobaellenfelek", "Adott quest- vagy boss-szobába kerülő ellenfelek.", "Példa: [new(\"MALREC_CHAMBER\", MonsterIds.SirMalrec, 1)]."),
        })
        {
            if (name is "SpecialRoomPlacements" or "QuestChestPlacements" or "QuestDoorRequirements")
            {
                var grid = BuildLevelDictionaryGrid(name);
                RegisterGrid("level." + name, grid);
                _levelDictionaryFields.Add(name, grid);
                var dictionaryGroup = new FlowLayoutPanel { Width = 370, Height = 238,
                    FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 0, 0, 4) };
                var dictionaryTitle = new Label { Text = label + "  ⓘ", AutoSize = false, Width = 355, Height = 20,
                    Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };
                var dictionaryExplanation = new Label { Text = hint, AutoSize = false, Width = 355, Height = 35,
                    ForeColor = Color.DimGray };
                _mazeToolTip.SetToolTip(dictionaryTitle, help);
                _mazeToolTip.SetToolTip(dictionaryExplanation, help);
                dictionaryGroup.Controls.Add(dictionaryTitle);
                dictionaryGroup.Controls.Add(dictionaryExplanation);
                dictionaryGroup.Controls.Add(grid);
                flow.Controls.Add(dictionaryGroup);
                continue;
            }
            var field = new TextBox
            {
                Width = 348,
                Height = 23
            };
            _levelFields.Add(name, field);
            var group = new FlowLayoutPanel { Width = 370, Height = 104,
                FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 0, 0, 4) };
            var title = new Label { Text = label + "  ⓘ", AutoSize = false, Width = 355, Height = 20,
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };
            var explanation = new Label { Text = hint, AutoSize = false, Width = 355, Height = 35,
                ForeColor = Color.DimGray };
            _mazeToolTip.SetToolTip(title, help);
            _mazeToolTip.SetToolTip(explanation, help);
            _mazeToolTip.SetToolTip(field, help);
            group.Controls.Add(title);
            group.Controls.Add(explanation);
            group.Controls.Add(field);
            flow.Controls.Add(group);
        }
        flow.Controls.Add(Button("Pályaadatok mentése", (_, _) => SaveLevelFields()));
        outer.Controls.Add(flow);
        return outer;
    }

    private static DataGridView BuildLevelDictionaryGrid(string propertyName)
    {
        var grid = new DataGridView
        {
            Width = 355,
            Height = 172,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            AllowUserToResizeColumns = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            RowHeadersWidth = 26
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RoomId", HeaderText = "Szobaazonosító", Width = 185 });
        if (propertyName == "SpecialRoomPlacements")
            grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "Value",
                HeaderText = "Elhelyezés",
                Width = 140,
                DataSource = Enum.GetNames<SpecialRoomPlacement>()
            });
        else
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Value",
                HeaderText = propertyName == "QuestDoorRequirements" ? "Küldetésazonosító" : "Questláda-azonosító",
                Width = 140
            });
        return grid;
    }

    private Control BuildEncountersTab()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        _encounterKinds.TabPages.Add(new TabPage("Szobai") { Controls = { _roomEncounterList } });
        _encounterKinds.TabPages.Add(new TabPage("Folyosói") { Controls = { _corridorEncounterList } });
        _encounterKinds.SelectedIndexChanged += (_, _) => ShowEncounterExpression();
        foreach (var list in new[] { _roomEncounterList, _corridorEncounterList })
        {
            list.HorizontalScrollbar = true;
            list.SelectedIndexChanged += (_, _) => ShowEncounterExpression();
            list.DoubleClick += (_, _) => EditEncounter();
        }
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 145, Padding = new Padding(0, 8, 0, 4) };
        _encounterExpression.ReadOnly = true;
        _encounterExpression.Dock = DockStyle.Fill;
        bottom.Controls.Add(_encounterExpression);
        bottom.Controls.Add(new Label { Text = "Kijelölt találkozás – generált C#", Dock = DockStyle.Top, Height = 24 });
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 78 };
        actions.Controls.Add(Button("Új találkozás…", (_, _) => AddEncounter()));
        actions.Controls.Add(Button("Szerkesztés…", (_, _) => EditEncounter()));
        actions.Controls.Add(Button("Törlés", (_, _) => RemoveEncounter()));
        actions.Controls.Add(Button("Találkozások mentése", (_, _) => SaveEncounters()));
        panel.Controls.Add(_encounterKinds);
        panel.Controls.Add(bottom);
        panel.Controls.Add(actions);
        return panel;
    }
    private Control BuildNpcTab()
    {
        var container = new Panel { Dock = DockStyle.Fill };
        _showAllNpcs.Dock = DockStyle.Top;
        _showAllNpcs.CheckedChanged += (_, _) => FilterNpcRows();
        _npcEncounterSelector.SelectedIndexChanged += (_, _) => FilterNpcRows();
        var encounterPicker = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 31 };
        encounterPicker.Controls.Add(new Label { Text = "NPC-találkozás:", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        encounterPicker.Controls.Add(_npcEncounterSelector);
        var storyHint = new Label { Dock = DockStyle.Top, Height = 42, ForeColor = Color.DarkOrange,
            Text = "Válassz NPC-találkozást: az új párbeszéd és quest ahhoz kötődik. A helyi szöveg elsőbbséget élvez; az üres találkozásmező általános." };
        foreach (var (section, label) in new (string, string)[]
        {
            ("NPC találkozások", "Elhelyezés"), ("NPC-k", "NPC-k"),
            ("NPC küldetések", "Questek"), ("NPC párbeszédek", "Párbeszédek"),
            ("Egyedi NPC karakterlap", "Karakterlap"), ("NPC történeti választások", "Történet")
        })
        {
            var page = new TabPage(label);
            var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = true,
                AllowUserToDeleteRows = true, AllowUserToResizeColumns = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                RowHeadersWidth = 26, ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText };
            _csvGrids[section] = grid;
            RegisterGrid("csv." + section, grid);
            grid.CellValueChanged += NpcIdCellValueChanged;
            grid.CellValueChanged += (_, _) => SetCsvDirty(section, true);
            grid.RowsAdded += (_, _) => SetCsvDirty(section, true);
            grid.RowsRemoved += (_, _) => SetCsvDirty(section, true);
            if (section == "NPC találkozások")
                grid.SelectionChanged += (_, _) =>
                {
                    if (_refreshingContent || grid.CurrentRow is not { IsNewRow: false } row || row.Cells.Count <= 6) return;
                    var encounterId = row.Cells[0].Value?.ToString();
                    if (encounterId is not null && _npcEncounterSelector.Items.Contains(encounterId))
                        _npcEncounterSelector.SelectedItem = encounterId;
                    var area = _areas.FirstOrDefault(candidate => candidate.Id == row.Cells[6].Value?.ToString());
                    if (area is not null) SelectArea(area);
                };
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 69, AutoScroll = true };
            actions.Controls.Add(Button("Új sor", (_, _) => AddCsvRow(section)));
            actions.Controls.Add(Button("Utolsó sor másolása", (_, _) =>
            {
                if (!CopyLastVisibleCsvRow(grid, section)) AddCsvRow(section);
            }));
            actions.Controls.Add(Button("Kijelölt sor törlése", (_, _) =>
            {
                if (grid.CurrentRow is { IsNewRow: false } row) grid.Rows.Remove(row);
            }));
            if (section == "NPC találkozások")
                actions.Controls.Add(Button("Kijelölt AreaId", (_, _) => SetNpcArea(grid)));
            actions.Controls.Add(Button("CSV mentése", (_, _) => SaveCsvSection(section)));
            var dirtyIndicator = new Label { AutoSize = true, Padding = new Padding(4, 6, 0, 0) };
            _csvDirtyIndicators[section] = dirtyIndicator;
            actions.Controls.Add(dirtyIndicator);
            SetCsvDirty(section, false);
            page.Controls.Add(grid); page.Controls.Add(actions);
            _npcTabs.TabPages.Add(page);
        }
        container.Controls.Add(_npcTabs);
        container.Controls.Add(storyHint);
        container.Controls.Add(_showAllNpcs);
        container.Controls.Add(encounterPicker);
        return container;
    }

    private void RefreshAdditionalTabs()
    {
        _refreshingContent = true;
        _encounterLevel = null;
        try
        {
            var level = (int)_level.Value;
            var block = EditorSources.ReadLevel(level);
            foreach (var (name, field) in _levelFields)
                field.Text = EditorSources.Property(block, name) ?? "";
            foreach (var (name, grid) in _levelDictionaryFields)
            {
                grid.Rows.Clear();
                foreach (var (roomId, expression) in EditorSources.DictionaryEntries(EditorSources.Property(block, name)))
                {
                    var value = name switch
                    {
                        "SpecialRoomPlacements" => expression[(expression.LastIndexOf('.') + 1)..],
                        "QuestDoorRequirements" => expression[(expression.LastIndexOf('.') + 1)..],
                        "QuestChestPlacements" when expression.StartsWith("new(\"", StringComparison.Ordinal) &&
                                                    expression.EndsWith("\")", StringComparison.Ordinal) =>
                            expression[5..^2],
                        _ => expression
                    };
                    grid.Rows.Add(roomId, value);
                }
            }
            LoadEncounterList(_roomEncounterList, EditorSources.Property(block, "RoomEncounters"));
            LoadEncounterList(_corridorEncounterList, EditorSources.Property(block, "CorridorEncounters"));
            _encounterLevel = level;
            var csv = new CsvSectionEditor(EditorSources.PathFor("Data/game-data.csv"));
            _npcNames = csv.Rows("NPC-k")
                .Where(row => row.Length > 1 && !string.IsNullOrWhiteSpace(row[0]))
                .ToDictionary(row => row[0], row => row[1], StringComparer.OrdinalIgnoreCase);
            foreach (var (section, grid) in _csvGrids)
            {
                CaptureGridColumnWidths("csv." + section, grid);
                grid.Columns.Clear(); grid.Rows.Clear();
                var headers = csv.Headers(section);
                foreach (var header in headers)
                {
                    var column = grid.Columns[grid.Columns.Add(header, header)];
                    column.ToolTipText = header switch
                    {
                        "VisszatérőViszony" => "Igen esetén ugyanaz a karakter és a mentett viszony jelenik meg a későbbi pályákon.",
                        "TalálkozásId" => "Üresen minden megjelenésnél érvényes. Kitöltve csak a megadott NPC-találkozásnál.",
                        "MinimumViszony" => "Ennél kisebb viszonynál a quest nem ajánlható fel (0–10).",
                        "MaximumViszony" => "Ennél nagyobb viszonynál a quest nem ajánlható fel (0–10).",
                        "MinimumBarátságosság" => "A párbeszédhez szükséges legalacsonyabb viszony (0–10).",
                        "MaximumBarátságosság" => "A párbeszédhez megengedett legmagasabb viszony (0–10).",
                        _ => ""
                    };
                }
                var npcIdColumnIndex = Array.FindIndex(headers,
                    header => string.Equals(header, "NpcId", StringComparison.OrdinalIgnoreCase));
                if (npcIdColumnIndex >= 0)
                {
                    var npcNameColumn = new DataGridViewTextBoxColumn
                    {
                        Name = InformativeNpcNameColumn,
                        HeaderText = InformativeNpcNameColumn,
                        ReadOnly = true,
                        Tag = InformativeNpcNameColumn
                    };
                    grid.Columns.Add(npcNameColumn);
                    npcNameColumn.DisplayIndex = 0;
                }
                ApplyGridColumnWidths("csv." + section, grid);
                foreach (var row in csv.Rows(section))
                {
                    var values = new object[grid.Columns.Count];
                    for (var i = 0; i < values.Length; i++) values[i] = i < row.Length ? row[i] : "";
                    if (npcIdColumnIndex >= 0 && _npcNames.TryGetValue(values[npcIdColumnIndex].ToString()!, out var npcName))
                        values[^1] = npcName;
                    var rowIndex = grid.Rows.Add(values);
                    if (section == "NPC találkozások" && values.Length > 2 && values[2]?.ToString() == level.ToString())
                        grid.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightGoldenrodYellow;
                }
            }
            RefreshNpcEncounterChoices();
            FilterNpcRows();
            foreach (var section in _csvGrids.Keys) SetCsvDirty(section, false);
        }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "Adatbetöltési hiba"); }
        finally { _refreshingContent = false; ShowEncounterExpression(); }
    }

    private void NpcIdCellValueChanged(object? sender, DataGridViewCellEventArgs args)
    {
        if (_refreshingContent || sender is not DataGridView grid || args.RowIndex < 0 ||
            !grid.Columns.Contains(InformativeNpcNameColumn))
            return;
        var npcIdColumn = grid.Columns["NpcId"];
        if (npcIdColumn is null || args.ColumnIndex != npcIdColumn.Index) return;
        var row = grid.Rows[args.RowIndex];
        var npcId = row.Cells["NpcId"].Value?.ToString();
        row.Cells[InformativeNpcNameColumn].Value = npcId is not null && _npcNames.TryGetValue(npcId, out var npcName)
            ? npcName
            : "";
    }

    private void SetCsvDirty(string section, bool dirty)
    {
        if (dirty && _refreshingContent || !_csvDirtyIndicators.TryGetValue(section, out var indicator)) return;
        indicator.Text = dirty ? "● Nincs mentve" : "● Mentve";
        indicator.ForeColor = dirty ? Color.Firebrick : Color.ForestGreen;
    }

    private void RefreshNpcEncounterChoices()
    {
        var previouslySelected = _npcEncounterSelector.SelectedItem?.ToString();
        _npcEncounterSelector.Items.Clear();
        _npcEncounterSelector.Items.Add("(pálya összes NPC-je)");
        foreach (DataGridViewRow row in _csvGrids["NPC találkozások"].Rows)
            if (!row.IsNewRow && row.Cells[2].Value?.ToString() == ((int)_level.Value).ToString() &&
                row.Cells[0].Value?.ToString() is { Length: > 0 } id)
                _npcEncounterSelector.Items.Add(id);
        _npcEncounterSelector.SelectedItem = previouslySelected is not null &&
            _npcEncounterSelector.Items.Contains(previouslySelected)
            ? previouslySelected : "(pálya összes NPC-je)";
    }

    private static void LoadEncounterList(ListBox list, string? property)
    {
        list.Items.Clear();
        foreach (var item in EditorSources.CollectionItems(property)) list.Items.Add(item);
        if (list.Items.Count > 0) list.SelectedIndex = 0;
    }

    private ListBox CurrentEncounterList => _encounterKinds.SelectedIndex == 0
        ? _roomEncounterList : _corridorEncounterList;

    private void ShowEncounterExpression()
    {
        if (_refreshingContent) return;
        _encounterExpression.Text = CurrentEncounterList.SelectedItem?.ToString() ?? "";
    }

    private void AddEncounter() => OpenEncounterEditor(null);
    private void EditEncounter()
    {
        if (CurrentEncounterList.SelectedItem is string expression) OpenEncounterEditor(expression);
    }

    private IReadOnlyDictionary<string, string> EncounterMonsters() =>
        new CsvSectionEditor(EditorSources.PathFor("Data/game-data.csv")).Rows("Ellenségek")
            .Where(row => row.Length >= 2 && !string.IsNullOrWhiteSpace(row[0]))
            .OrderBy(row => row[1], StringComparer.CurrentCulture)
            .ToDictionary(row => row[0], row => row[1], StringComparer.Ordinal);

    private EncounterEditorContext EncounterContext(bool roomList, IReadOnlyDictionary<string, string> monsters)
    {
        RequireEncounterLevel();
        var layout = MazeLevelConfigurations.Get((int)_level.Value).Layout;
        var forest = layout as ForestMazeLayoutConfiguration;
        var guaranteed = forest is null ? _minimumScreenCount : _layoutSeed.Visible
            ? forest.Graph.AreaCount.Minimum : _areas.Count;
        var areaIds = _areas.Take(guaranteed).Select(area => area.Id).ToArray();
        var kinds = new HashSet<RoomKind>();
        var areaKinds = new Dictionary<string, IReadOnlyList<RoomKind>>();
        if (forest is null) kinds.Add(RoomKind.Generic);
        else
        {
            kinds.Add(RoomKind.Clearing);
            var graph = new ExplicitForestAreaGraphConfiguration(_areas.ToArray(), _connections.ToArray(),
                _areas[0].Id, _areas[^1].Id, _customTemplates.ToArray());
            foreach (var area in _areas)
            {
                var localKinds = new List<RoomKind> { RoomKind.Clearing };
                areaKinds[area.Id] = localKinds;
                var profile = ForestAreaConfigurationResolver.Resolve(_previewBaseConfiguration, graph, area);
                if (profile.BuildingCount.Maximum == 0) continue;
                if (profile.ManorBuildingChance + profile.LabyrinthBuildingChance < 1) localKinds.Add(RoomKind.Cabin);
                if (profile.ManorBuildingChance > 0) localKinds.Add(RoomKind.Manor);
                if (profile.LabyrinthBuildingChance > 0) localKinds.Add(RoomKind.Labyrinth);
                kinds.UnionWith(localKinds);
            }
        }
        return new(forest is not null, roomList, guaranteed, areaIds, kinds.Order().ToArray(), monsters.Keys.ToHashSet(), areaKinds);
    }

    private void OpenEncounterEditor(string? expression)
    {
        try
        {
            var list = CurrentEncounterList;
            var index = list.SelectedIndex;
            var draft = expression is null ? new EncounterDraft("Same") : EncounterDraft.Parse(expression);
            var monsters = EncounterMonsters();
            using var dialog = new EncounterEditorDialog(draft, EncounterContext(list == _roomEncounterList, monsters), monsters);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            if (expression is null) index = list.Items.Add(dialog.Expression);
            else list.Items[index] = dialog.Expression;
            list.SelectedIndex = index;
            ShowEncounterExpression();
            UpdateStatus("Találkozás alkalmazva. A fájlba íráshoz: Találkozások mentése.");
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message + "\nAz eredeti találkozáskifejezés változatlan maradt.", "Találkozás szerkesztése");
        }
    }

    private void RequireEncounterLevel()
    {
        if (_encounterLevel != (int)_level.Value)
            throw new InvalidOperationException("Előbb töltsd be a kiválasztott pályát a Pálya betöltése gombbal.");
    }
    private void RemoveEncounter()
    {
        var list = CurrentEncounterList;
        var index = list.SelectedIndex;
        if (index < 0) return;
        list.Items.RemoveAt(index);
        if (list.Items.Count > 0) list.SelectedIndex = Math.Min(index, list.Items.Count - 1);
        ShowEncounterExpression();
    }

    private void SaveEncounters()
    {
        try
        {
            RequireEncounterLevel();
            var monsters = EncounterMonsters();
            var original = EditorSources.ReadLevel((int)_level.Value);
            foreach (var (name, list) in new[] { ("RoomEncounters", _roomEncounterList), ("CorridorEncounters", _corridorEncounterList) })
            {
                var context = EncounterContext(list == _roomEncounterList, monsters);
                var previous = EditorSources.CollectionItems(EditorSources.Property(original, name));
                foreach (string expression in list.Items)
                {
                    EncounterDraft draft;
                    try { draft = EncounterDraft.Parse(expression); }
                    catch (FormatException) when (previous.Contains(expression)) { continue; }
                    context.Validate(draft.Configuration());
                }
            }
            static string Expression(ListBox list) => "[" + Environment.NewLine +
                string.Join("," + Environment.NewLine, list.Items.Cast<string>().Select(item => "                    " + item)) +
                Environment.NewLine + "                ]";
            var block = EditorSources.ReadLevel((int)_level.Value);
            var changes = new Dictionary<string, string>();
            foreach (var (name, list) in new[]
                { ("RoomEncounters", _roomEncounterList), ("CorridorEncounters", _corridorEncounterList) })
                if (!EditorSources.CollectionItems(EditorSources.Property(block, name))
                        .SequenceEqual(list.Items.Cast<string>()))
                    changes[name] = Expression(list);
            EditorSources.SaveLevel((int)_level.Value, changes);
            UpdateStatus("A találkozások mentve. Az előnézethez indítsd újra a szerkesztőt a fordítás után.");
        }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "Mentési hiba"); }
    }

    private void SaveLevelFields()
    {
        try
        {
            var values = _levelFields.Where(entry => !string.IsNullOrWhiteSpace(entry.Value.Text))
                .ToDictionary(entry => entry.Key, entry => entry.Value.Text.Trim());
            foreach (var (name, grid) in _levelDictionaryFields)
            {
                grid.EndEdit();
                var entries = grid.Rows.Cast<DataGridViewRow>().Where(row => !row.IsNewRow)
                    .Select(row => (RoomId: row.Cells["RoomId"].Value?.ToString()?.Trim() ?? "",
                        Value: row.Cells["Value"].Value?.ToString()?.Trim() ?? ""))
                    .Where(entry => entry.RoomId.Length > 0 || entry.Value.Length > 0).ToArray();
                if (entries.Any(entry => entry.RoomId.Length == 0 || entry.Value.Length == 0))
                    throw new InvalidDataException($"A(z) {name} minden sorában kötelező a szobaazonosító és az érték.");
                if (entries.Select(entry => entry.RoomId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length)
                    throw new InvalidDataException($"A(z) {name} szobaazonosítói nem ismétlődhetnek.");
                var type = name switch
                {
                    "SpecialRoomPlacements" => "SpecialRoomPlacement",
                    "QuestDoorRequirements" => "Domain.Quests.QuestId",
                    _ => "Domain.Quests.QuestChestId"
                };
                var lines = entries.Select(entry =>
                {
                    var roomId = entry.RoomId.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    var value = name switch
                    {
                        "SpecialRoomPlacements" => $"SpecialRoomPlacement.{entry.Value}",
                        "QuestDoorRequirements" => $"Domain.Quests.QuestId.{entry.Value}",
                        _ => $"new(\"{entry.Value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\")"
                    };
                    return $"                    [\"{roomId}\"] = {value}";
                });
                values[name] = $"new Dictionary<string, {type}>\n                {{\n" +
                    string.Join(",\n", lines) + "\n                }";
            }
            EditorSources.SaveLevel((int)_level.Value, values);
            UpdateStatus("A pályaadatok mentve. Az előnézethez indítsd újra a szerkesztőt a fordítás után.");
        }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "Mentési hiba"); }
    }

    private void AddCsvRow(string section)
    {
        var grid = _csvGrids[section];
        var values = Enumerable.Repeat<object>("", grid.Columns.Count).ToArray();
        values[0] = NextCsvId(grid, section);
        if (section == "NPC találkozások")
        {
            values[2] = ((int)_level.Value).ToString(); values[3] = "6"; values[4] = "14";
            if (_selected is not null && grid.Columns.Count > 6) values[6] = _selected.Id;
        }
        if (section == "NPC küldetések") { values[2] = "Kill"; values[4] = "1"; values[5] = "0"; }
        if (section is "NPC küldetések" or "NPC párbeszédek" &&
            _npcEncounterSelector.SelectedIndex > 0)
        {
            var encounterId = _npcEncounterSelector.SelectedItem?.ToString();
            var encounter = _csvGrids["NPC találkozások"].Rows.Cast<DataGridViewRow>()
                .FirstOrDefault(row => !row.IsNewRow && row.Cells[0].Value?.ToString() == encounterId);
            if (encounter is not null)
            {
                values[1] = encounter.Cells[1].Value?.ToString() ?? "";
                values[section == "NPC küldetések" ? 16 : 5] = encounterId ?? "";
            }
        }
        if (section == "NPC párbeszédek") { values[2] = "0"; values[3] = "10"; }
        var index = grid.Rows.Add(values);
        grid.CurrentCell = grid.Rows[index].Cells[0];
    }

    internal static bool CopyLastVisibleCsvRow(DataGridView grid, string section)
    {
        grid.EndEdit();
        var source = grid.Rows.Cast<DataGridViewRow>().LastOrDefault(row => !row.IsNewRow && row.Visible);
        if (source is null) return false;
        var values = source.Cells.Cast<DataGridViewCell>()
            .Select(cell => (object)(cell.Value?.ToString() ?? "")).ToArray();
        values[0] = NextCsvId(grid, section);
        if (grid.Columns.Contains(InformativeNpcNameColumn) && section == "Egyedi NPC karakterlap")
            values[grid.Columns[InformativeNpcNameColumn]!.Index] = "";
        var index = grid.Rows.Add(values);
        grid.CurrentCell = grid.Rows[index].Cells[0];
        return true;
    }

    private static string NextCsvId(DataGridView grid, string section)
    {
        var prefix = section switch
        {
            "NPC-k" => "NPC", "NPC találkozások" => "NPCE", "NPC küldetések" => "NPCQ",
            "NPC párbeszédek" => "NPCD", "NPC történeti választások" => "NSC", _ => ""
        };
        if (prefix.Length == 0) return "";
        var used = grid.Rows.Cast<DataGridViewRow>().Where(row => !row.IsNewRow)
            .Select(row => row.Cells[0].Value?.ToString() ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var next = 1;
        while (used.Contains(prefix + next.ToString("D3"))) next++;
        return prefix + next.ToString("D3");
    }

    private void FilterNpcRows()
    {
        if (!_csvGrids.TryGetValue("NPC találkozások", out var encounters)) return;
        var level = ((int)_level.Value).ToString();
        var placed = encounters.Rows.Cast<DataGridViewRow>().Where(row => !row.IsNewRow &&
                row.Cells.Count > 2 && row.Cells[2].Value?.ToString() == level)
            .Select(row => row.Cells[1].Value?.ToString()).Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedEncounter = _npcEncounterSelector.SelectedIndex > 0
            ? _npcEncounterSelector.SelectedItem?.ToString() : null;
        var selectedNpc = encounters.Rows.Cast<DataGridViewRow>().FirstOrDefault(row =>
            !row.IsNewRow && row.Cells[0].Value?.ToString() == selectedEncounter)?.Cells[1].Value?.ToString();
        foreach (var (section, grid) in _csvGrids)
        {
            var visibility = new List<(DataGridViewRow Row, bool Show)>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                var show = _showAllNpcs.Checked || section switch
                {
                    "NPC találkozások" => row.Cells[2].Value?.ToString() == level,
                    "NPC-k" or "Egyedi NPC karakterlap" => selectedNpc is not null
                        ? row.Cells[0].Value?.ToString() == selectedNpc
                        : placed.Contains(row.Cells[0].Value?.ToString()),
                    "NPC küldetések" or "NPC párbeszédek" => selectedNpc is not null
                        ? row.Cells[1].Value?.ToString() == selectedNpc &&
                          (row.Cells[section == "NPC küldetések" ? 16 : 5].Value?.ToString() is null or "" ||
                           row.Cells[section == "NPC küldetések" ? 16 : 5].Value?.ToString() == selectedEncounter)
                        : placed.Contains(row.Cells[1].Value?.ToString()),
                    "NPC történeti választások" => true,
                    _ => true
                };
                visibility.Add((row, show));
            }
            // A kijelölést csak akkor szüntessük meg, ha a sor valóban eltűnik.
            // Ellenkező esetben az elhelyezési rács kiválasztása után azonnal
            // elveszne az aktív cella, és nem lehetne szerkeszteni.
            if (grid.CurrentRow is { IsNewRow: false } current &&
                visibility.Any(entry => entry.Row == current && !entry.Show))
                grid.CurrentCell = null;
            foreach (var (row, show) in visibility) row.Visible = show;
        }
    }

    private void SetNpcArea(DataGridView grid)
    {
        if (_selected is null || grid.CurrentRow is not { IsNewRow: false } row || grid.Columns.Count <= 6) return;
        row.Cells[6].Value = _selected.Id;
    }

    private void SaveCsvSection(string section)
    {
        try
        {
            var grid = _csvGrids[section];
            grid.EndEdit();
            var persistedColumnIndexes = grid.Columns.Cast<DataGridViewColumn>()
                .Where(column => !Equals(column.Tag, InformativeNpcNameColumn))
                .Select(column => column.Index).ToArray();
            var rows = grid.Rows.Cast<DataGridViewRow>().Where(row => !row.IsNewRow)
                .Select(row => persistedColumnIndexes.Select(index => row.Cells[index].Value?.ToString() ?? "").ToArray())
                .Where(row => row.Any(value => value.Length > 0)).ToArray();
            var missingId = rows.FirstOrDefault(row => string.IsNullOrWhiteSpace(row[0]));
            if (missingId is not null)
                throw new InvalidDataException($"A(z) {section} szekcióban van azonosító nélküli sor.");
            var repeatedId = rows.GroupBy(row => row[0], StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (repeatedId is not null)
                throw new InvalidDataException($"A(z) {section} szekcióban ismétlődő azonosító: {repeatedId.Key}. " +
                    "A találkozások Id mezője különbözzön akkor is, ha ugyanaz a NpcId.");
            if (section == "NPC találkozások")
            {
                var recurringNpcs = new CsvSectionEditor(EditorSources.PathFor("Data/game-data.csv"))
                    .Rows("NPC-k")
                    .Where(npc => npc.Length > 9 && npc[9].Equals("igen", StringComparison.OrdinalIgnoreCase))
                    .Select(npc => npc[0]).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var repeatedRecurringNpc = rows.Where(row => row.Length > 2 && recurringNpcs.Contains(row[1]))
                    .GroupBy(row => (NpcId: row[1].ToUpperInvariant(), Level: row[2]))
                    .FirstOrDefault(group => group.Count() > 1);
                if (repeatedRecurringNpc is not null)
                    throw new InvalidDataException($"A(z) {repeatedRecurringNpc.Key.NpcId} visszatérő NPC a(z) " +
                        $"{repeatedRecurringNpc.Key.Level}. pályán csak egyszer helyezhető el, különböző AreaId esetén is.");
                foreach (var row in rows)
                {
                    if (row.Length < 7 || !int.TryParse(row[2], out var level) || level < 1 ||
                        !int.TryParse(row[3], out var minimum) || !int.TryParse(row[4], out var maximum) ||
                        minimum < 1 || maximum < minimum)
                        throw new InvalidDataException($"Érvénytelen NPC-találkozás: {row[0]}");
                    if (row[5].Length > 0 && row[6].Length > 0)
                        throw new InvalidDataException($"A(z) {row[0]} találkozásnál QuestRoomId és AreaId közül egyet válassz.");
                    if (level == (int)_level.Value && row[6].Length > 0 && !_areas.Any(area => area.Id == row[6]))
                        throw new InvalidDataException($"A(z) {row[0]} ismeretlen AreaId-ra mutat: {row[6]}");
                }
            }
            if (section is "NPC küldetések" or "NPC párbeszédek")
            {
                var savedEncounters = new CsvSectionEditor(EditorSources.PathFor("Data/game-data.csv"))
                    .Rows("NPC találkozások").ToDictionary(row => row[0], StringComparer.OrdinalIgnoreCase);
                foreach (var row in rows)
                {
                    var encounterId = row[section == "NPC küldetések" ? 16 : 5];
                    if (encounterId.Length > 0 &&
                        (!savedEncounters.TryGetValue(encounterId, out var encounter) ||
                         !string.Equals(encounter[1], row[1], StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException($"A(z) {row[0]} találkozásazonosítója hiányzik, vagy más NPC-hez tartozik: {encounterId}. Előbb mentsd az elhelyezést.");
                    var minimum = section == "NPC küldetések" ? 17 : 2;
                    var maximum = section == "NPC küldetések" ? 18 : 3;
                    if (row[minimum].Length > 0 && (!int.TryParse(row[minimum], out var lower) || lower is < 0 or > 10) ||
                        row[maximum].Length > 0 && (!int.TryParse(row[maximum], out var upper) || upper is < 0 or > 10) ||
                        int.TryParse(row[minimum], out var minValue) &&
                        int.TryParse(row[maximum], out var maxValue) && minValue > maxValue)
                        throw new InvalidDataException($"A(z) {row[0]} viszonytartománya érvénytelen (0–10).");
                }
            }
            var paths = new List<string> { EditorSources.PathFor("Data/game-data.csv") };
            if (section is "NPC-k" or "NPC küldetések")
            {
                var npc = section == "NPC-k";
                paths.Add(EditorSources.PathFor($"Domain/Quests/{(npc ? "QuestNpcId" : "QuestId")}.cs"));
                paths.Add(EditorSources.PathFor($"Infrastructure/Quests/{(npc ? "LegacyNpcIdMap" : "LegacyQuestIdMap")}.cs"));
            }
            var originals = paths.ToDictionary(path => path, File.ReadAllBytes);
            try
            {
                if (section is "NPC-k" or "NPC küldetések")
                    LegacyMappingWriter.EnsureMappings(section, rows);
                var csv = new CsvSectionEditor(paths[0]);
                csv.Save(section, rows);
            }
            catch
            {
                foreach (var (path, bytes) in originals) File.WriteAllBytes(path, bytes);
                throw;
            }
            SetCsvDirty(section, false);
            UpdateStatus($"{section}: CSV és azonosítók mentve.");
            if (section == "NPC találkozások") RefreshNpcEncounterChoices();
        }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "CSV mentési hiba"); }
    }
}
