using System.Text.RegularExpressions;
using KaoszRubin.World;

namespace KaoszRubin.ForestMapEditor;

internal sealed partial class ForestMapEditorForm
{
    private readonly TabPage _forestTab = new("Erdős pálya");
    private readonly TabPage _mazeTab = new("Labirintus pálya");
    private readonly TabPage _encountersTab = new("Találkozások");
    private readonly TabPage _npcsTab = new("NPC-k");
    private readonly TabControl _mainTabs = new() { Dock = DockStyle.Fill };
    private readonly Dictionary<string, TextBox> _levelFields = [];
    private readonly ListBox _roomEncounterList = new() { Dock = DockStyle.Fill };
    private readonly ListBox _corridorEncounterList = new() { Dock = DockStyle.Fill };
    private readonly TextBox _encounterExpression = new() { Dock = DockStyle.Top, Multiline = true, Height = 94, ScrollBars = ScrollBars.Vertical };
    private readonly ComboBox _encounterArea = new() { Width = 190, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TabControl _encounterKinds = new() { Dock = DockStyle.Fill };
    private readonly Dictionary<string, DataGridView> _csvGrids = [];
    private readonly CheckBox _showAllNpcs = new() { Text = "Összes pálya NPC-i", AutoSize = true };
    private bool _refreshingContent;

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
        foreach (var (name, label) in new (string, string)[]
        {
            ("Name", "Név"), ("Layout", "Elrendezés"), ("RoomCount", "Termek száma"),
            ("RoomSize", "Teremméret"), ("TreasureChestCount", "Kincsesládák"),
            ("TreasureGold", "Arany"), ("WallRune", "Fal karaktere"),
            ("WallColor", "Fal színe"), ("DoubleWidthCorridorChance", "Széles folyosó esélye"),
            ("QuestRoomIds", "Quest szobák"), ("BossRoomIds", "Boss szobák"),
            ("SpecialRoomPlacements", "Különleges szobák helye"),
            ("QuestChestPlacements", "Quest ládák helye"),
            ("QuestDoorRequirements", "Quest ajtók"),
            ("QuestRoomEnemyEncounters", "Garantált ellenfelek")
        })
        {
            var field = new TextBox { Width = 360 };
            _levelFields.Add(name, field);
            flow.Controls.Add(Labelled(label, field));
        }
        flow.Controls.Add(Button("Pályaadatok mentése", (_, _) => SaveLevelFields()));
        outer.Controls.Add(flow);
        return outer;
    }

    private Control BuildEncountersTab()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        _encounterKinds.TabPages.Add(new TabPage("Szobai") { Controls = { _roomEncounterList } });
        _encounterKinds.TabPages.Add(new TabPage("Folyosói") { Controls = { _corridorEncounterList } });
        _encounterKinds.SelectedIndexChanged += (_, _) => ShowEncounterExpression();
        _roomEncounterList.SelectedIndexChanged += (_, _) => ShowEncounterExpression();
        _corridorEncounterList.SelectedIndexChanged += (_, _) => ShowEncounterExpression();
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 195,
            FlowDirection = FlowDirection.TopDown, WrapContents = false };
        bottom.Controls.Add(new Label { Text = "Kijelölt találkozás C# kifejezése", AutoSize = true });
        _encounterExpression.Width = 365;
        bottom.Controls.Add(_encounterExpression);
        var row = new FlowLayoutPanel { Width = 370, Height = 67 };
        row.Controls.Add(_encounterArea);
        row.Controls.Add(Button("AreaId beillesztése", (_, _) => InsertEncounterArea()));
        row.Controls.Add(Button("Kijelölt", (_, _) =>
        {
            if (_selected is not null) { _encounterArea.SelectedItem = _selected.Id; InsertEncounterArea(); }
        }));
        bottom.Controls.Add(row);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 72 };
        actions.Controls.Add(Button("Sor alkalmazása", (_, _) => ApplyEncounterExpression()));
        actions.Controls.Add(Button("Új találkozás", (_, _) => AddEncounter()));
        actions.Controls.Add(Button("Törlés", (_, _) => RemoveEncounter()));
        actions.Controls.Add(Button("Találkozások mentése", (_, _) => SaveEncounters()));
        panel.Controls.Add(_encounterKinds);
        panel.Controls.Add(actions);
        panel.Controls.Add(bottom);
        return panel;
    }

    private Control BuildNpcTab()
    {
        var container = new Panel { Dock = DockStyle.Fill };
        var tabs = new TabControl { Dock = DockStyle.Fill };
        _showAllNpcs.Dock = DockStyle.Top;
        _showAllNpcs.CheckedChanged += (_, _) => FilterNpcRows();
        var storyHint = new Label { Dock = DockStyle.Top, Height = 42, ForeColor = Color.DarkOrange,
            Text = "Az alap questek teljesen szerkeszthetők. Egyedi történeti szabályhoz a QuestCatalogBuilder C# kódját is módosítani kell." };
        foreach (var (section, label) in new (string, string)[]
        {
            ("NPC találkozások", "Elhelyezés"), ("NPC-k", "NPC-k"),
            ("NPC küldetések", "Questek"), ("NPC párbeszédek", "Párbeszédek"),
            ("Egyedi NPC karakterlap", "Karakterlap"), ("NPC történeti választások", "Történet")
        })
        {
            var page = new TabPage(label);
            var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = true,
                AllowUserToDeleteRows = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                RowHeadersWidth = 26, ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText };
            _csvGrids[section] = grid;
            if (section == "NPC találkozások")
                grid.SelectionChanged += (_, _) =>
                {
                    if (_refreshingContent || grid.CurrentRow is not { IsNewRow: false } row || row.Cells.Count <= 6) return;
                    var area = _areas.FirstOrDefault(candidate => candidate.Id == row.Cells[6].Value?.ToString());
                    if (area is not null) SelectArea(area);
                };
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 69, AutoScroll = true };
            actions.Controls.Add(Button("Új sor", (_, _) => AddCsvRow(section)));
            actions.Controls.Add(Button("Kijelölt sor törlése", (_, _) =>
            {
                if (grid.CurrentRow is { IsNewRow: false } row) grid.Rows.Remove(row);
            }));
            if (section == "NPC találkozások")
                actions.Controls.Add(Button("Kijelölt AreaId", (_, _) => SetNpcArea(grid)));
            actions.Controls.Add(Button("CSV mentése", (_, _) => SaveCsvSection(section)));
            page.Controls.Add(grid); page.Controls.Add(actions);
            tabs.TabPages.Add(page);
        }
        container.Controls.Add(tabs);
        container.Controls.Add(storyHint);
        container.Controls.Add(_showAllNpcs);
        return container;
    }

    private void RefreshAdditionalTabs()
    {
        _refreshingContent = true;
        try
        {
            var level = (int)_level.Value;
            var block = EditorSources.ReadLevel(level);
            foreach (var (name, field) in _levelFields)
                field.Text = EditorSources.Property(block, name) ?? "";
            LoadEncounterList(_roomEncounterList, EditorSources.Property(block, "RoomEncounters"));
            LoadEncounterList(_corridorEncounterList, EditorSources.Property(block, "CorridorEncounters"));
            _encounterArea.Items.Clear();
            _encounterArea.Items.Add("(automatikus)");
            _encounterArea.Items.AddRange(_areas.Select(area => area.Id).ToArray());
            _encounterArea.SelectedIndex = 0;
            var csv = new CsvSectionEditor(EditorSources.PathFor("Data/game-data.csv"));
            foreach (var (section, grid) in _csvGrids)
            {
                grid.Columns.Clear(); grid.Rows.Clear();
                foreach (var header in csv.Headers(section)) grid.Columns.Add(header, header);
                foreach (var row in csv.Rows(section))
                {
                    var values = new object[grid.Columns.Count];
                    for (var i = 0; i < values.Length; i++) values[i] = i < row.Length ? row[i] : "";
                    var rowIndex = grid.Rows.Add(values);
                    if (section == "NPC találkozások" && values.Length > 2 && values[2]?.ToString() == level.ToString())
                        grid.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightGoldenrodYellow;
                }
            }
            FilterNpcRows();
        }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "Adatbetöltési hiba"); }
        finally { _refreshingContent = false; }
    }

    private static void LoadEncounterList(ListBox list, string? property)
    {
        list.Items.Clear();
        foreach (var item in EditorSources.CollectionItems(property)) list.Items.Add(item);
    }

    private ListBox CurrentEncounterList => _encounterKinds.SelectedIndex == 0
        ? _roomEncounterList : _corridorEncounterList;

    private void ShowEncounterExpression()
    {
        if (_refreshingContent) return;
        _encounterExpression.Text = CurrentEncounterList.SelectedItem?.ToString() ?? "";
        var match = Regex.Match(_encounterExpression.Text, "AreaId\\s*=\\s*\"([^\"]+)\"");
        _encounterArea.SelectedItem = match.Success && _encounterArea.Items.Contains(match.Groups[1].Value)
            ? match.Groups[1].Value : "(automatikus)";
        if (match.Success && _areas.FirstOrDefault(area => area.Id == match.Groups[1].Value) is { } target)
            SelectArea(target);
    }

    private void InsertEncounterArea()
    {
        var expression = _encounterExpression.Text.Trim();
        if (expression.Length == 0) return;
        var field = "AreaId = \"" + _encounterArea.SelectedItem + "\"";
        var hasArea = Regex.IsMatch(expression, @"\bAreaId\s*=\s*""[^""]*""");
        if (_encounterArea.SelectedIndex <= 0)
        {
            expression = Regex.Replace(expression, @"\s*with\s*\{\s*AreaId\s*=\s*""[^""]*""\s*\}", "");
            expression = Regex.Replace(expression, @"\bAreaId\s*=\s*""[^""]*""\s*,\s*", "");
            expression = Regex.Replace(expression, @",\s*AreaId\s*=\s*""[^""]*""", "");
        }
        else if (hasArea)
            expression = Regex.Replace(expression, @"\bAreaId\s*=\s*""[^""]*""", field);
        else if (Regex.IsMatch(expression, @"\bwith\s*\{[^}]*\}\s*$"))
            expression = Regex.Replace(expression, @"\}\s*$", ", " + field + " }");
        else
            expression += " with { " + field + " }";
        _encounterExpression.Text = expression;
        ApplyEncounterExpression();
    }

    private void ApplyEncounterExpression()
    {
        var list = CurrentEncounterList;
        if (list.SelectedIndex < 0 || string.IsNullOrWhiteSpace(_encounterExpression.Text)) return;
        var index = list.SelectedIndex;
        list.Items[index] = _encounterExpression.Text.Trim();
        list.SelectedIndex = index;
    }

    private void AddEncounter()
    {
        var list = CurrentEncounterList;
        var index = list.Items.Add("Encounters.Solo(MonsterIds.Goblin, Amount.Few)");
        list.SelectedIndex = index;
    }

    private void RemoveEncounter()
    {
        var list = CurrentEncounterList;
        if (list.SelectedIndex >= 0) list.Items.RemoveAt(list.SelectedIndex);
    }

    private void SaveEncounters()
    {
        try
        {
            ApplyEncounterExpression();
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
            EditorSources.SaveLevel((int)_level.Value, values);
            UpdateStatus("A pályaadatok mentve. Az előnézethez indítsd újra a szerkesztőt a fordítás után.");
        }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "Mentési hiba"); }
    }

    private void AddCsvRow(string section)
    {
        var grid = _csvGrids[section];
        var values = Enumerable.Repeat<object>("", grid.Columns.Count).ToArray();
        var prefix = section switch
        {
            "NPC-k" => "NPC", "NPC találkozások" => "NPCE", "NPC küldetések" => "NPCQ",
            "NPC párbeszédek" => "NPCD", "NPC történeti választások" => "NSC", _ => ""
        };
        if (prefix.Length > 0)
        {
            var used = grid.Rows.Cast<DataGridViewRow>().Where(row => !row.IsNewRow)
                .Select(row => row.Cells[0].Value?.ToString() ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);
            var next = 1;
            while (used.Contains(prefix + next.ToString("D3"))) next++;
            values[0] = prefix + next.ToString("D3");
        }
        if (section == "NPC találkozások")
        {
            values[2] = ((int)_level.Value).ToString(); values[3] = "6"; values[4] = "14";
            if (_selected is not null && grid.Columns.Count > 6) values[6] = _selected.Id;
        }
        if (section == "NPC küldetések") { values[2] = "Kill"; values[4] = "1"; values[5] = "0"; }
        var index = grid.Rows.Add(values);
        grid.CurrentCell = grid.Rows[index].Cells[0];
    }

    private void FilterNpcRows()
    {
        if (!_csvGrids.TryGetValue("NPC találkozások", out var encounters)) return;
        var level = ((int)_level.Value).ToString();
        var placed = encounters.Rows.Cast<DataGridViewRow>().Where(row => !row.IsNewRow &&
                row.Cells.Count > 2 && row.Cells[2].Value?.ToString() == level)
            .Select(row => row.Cells[1].Value?.ToString()).Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (section, grid) in _csvGrids)
        {
            grid.CurrentCell = null;
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                var show = _showAllNpcs.Checked || section switch
                {
                    "NPC találkozások" => row.Cells[2].Value?.ToString() == level,
                    "NPC-k" or "Egyedi NPC karakterlap" => placed.Contains(row.Cells[0].Value?.ToString()),
                    "NPC küldetések" or "NPC párbeszédek" => placed.Contains(row.Cells[1].Value?.ToString()),
                    "NPC történeti választások" => true,
                    _ => true
                };
                row.Visible = show;
            }
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
            var rows = grid.Rows.Cast<DataGridViewRow>().Where(row => !row.IsNewRow)
                .Select(row => row.Cells.Cast<DataGridViewCell>().Select(cell => cell.Value?.ToString() ?? "").ToArray())
                .Where(row => row.Any(value => value.Length > 0)).ToArray();
            if (rows.Any(row => string.IsNullOrWhiteSpace(row[0])) ||
                rows.Select(row => row[0]).Distinct(StringComparer.OrdinalIgnoreCase).Count() != rows.Length)
                throw new InvalidDataException("Az azonosítók nem lehetnek üresek vagy ismétlődők.");
            if (section == "NPC találkozások")
            {
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
            UpdateStatus($"{section}: CSV és azonosítók mentve.");
        }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "CSV mentési hiba"); }
    }
}
