using KaoszRubin.World;

namespace KaoszRubin.ForestMapEditor;

internal sealed class ForestMapEditorForm : Form
{
    private readonly ForestGraphCanvas _canvas = new();
    private readonly SplitContainer _split = new()
    {
        Dock = DockStyle.Fill,
        FixedPanel = FixedPanel.Panel2,
        SplitterWidth = 7
    };
    private readonly ToolStripStatusLabel _status = new();
    private readonly List<ForestAreaDefinition> _areas = [];
    private readonly List<ForestAreaConnectionDefinition> _connections = [];
    private readonly List<ForestAreaTemplateDefinition> _customTemplates = [];
    private readonly TextBox _id = new() { Dock = DockStyle.Top };
    private readonly TextBox _name = new() { Dock = DockStyle.Top };
    private readonly ComboBox _template = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _newTemplateId = new() { Width = 130, PlaceholderText = "pl. ködös-láp" };
    private readonly TextBox _newTemplateName = new() { Width = 130, PlaceholderText = "Megjelenő név" };
    private readonly NumericUpDown _x = Number(-20, 20);
    private readonly NumericUpDown _y = Number(-20, 20);
    private readonly CheckBox _densityEnabled = new() { Text = "Erdősűrűség felülírása", Dock = DockStyle.Top };
    private readonly NumericUpDown _density = Number(0, 100, 58);
    private readonly CheckBox _lakesEnabled = new() { Text = "Tavak felülírása", Dock = DockStyle.Top };
    private readonly NumericUpDown _lakeMin = Number(0, 12, 1);
    private readonly NumericUpDown _lakeMax = Number(0, 12, 3);
    private readonly CheckBox _marshEnabled = new() { Text = "Mocsarak felülírása", Dock = DockStyle.Top };
    private readonly NumericUpDown _marshMin = Number(0, 12, 1);
    private readonly NumericUpDown _marshMax = Number(0, 12, 3);
    private readonly CheckBox _buildingsEnabled = new() { Text = "Épületek felülírása", Dock = DockStyle.Top };
    private readonly NumericUpDown _buildingMin = Number(0, 8, 1);
    private readonly NumericUpDown _buildingMax = Number(0, 8, 2);
    private readonly ComboBox _connectionFrom = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 145 };
    private readonly ComboBox _connectionTo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 145 };
    private readonly ComboBox _entrance = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 145 };
    private readonly ComboBox _exit = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 145 };
    private readonly NumericUpDown _level = Number(1, 999, 6);
    private ForestAreaDefinition? _selected;
    private string? _currentFilePath;

    public ForestMapEditorForm()
    {
        Text = "Káoszrubin – erdei pályagráf-szerkesztő";
        Width = 1280;
        Height = 800;
        MinimumSize = new Size(900, 650);
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.CenterScreen;
        _template.Items.AddRange(ForestAreaTemplateCatalog.BuiltIns.Select(template => template.Id).ToArray());

        _split.Panel1.Controls.Add(_canvas);
        _split.Panel2.Controls.Add(BuildInspector());
        Controls.Add(_split);
        Controls.Add(BuildToolbar());
        Controls.Add(new StatusStrip { Items = { _status } });

        _canvas.SelectionChanged += SelectArea;
        _canvas.AreaMoved += MoveArea;
        Shown += (_, _) => EnsureInspectorVisible();
        SizeChanged += (_, _) => EnsureInspectorVisible();
        NewDocument();
    }

    private Control BuildToolbar()
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 76, Padding = new Padding(6),
            WrapContents = true, AutoScroll = true };
        bar.Controls.Add(Button("Új", (_, _) => NewDocument()));
        bar.Controls.Add(Button("Megnyitás", (_, _) => OpenDocument()));
        bar.Controls.Add(new Label { Text = "Pálya:", AutoSize = true, Padding = new Padding(0, 7, 0, 0) });
        bar.Controls.Add(_level);
        bar.Controls.Add(Button("Pálya betöltése", (_, _) => LoadSelectedLevel()));
        bar.Controls.Add(Button("Mentés", (_, _) => SaveDocument()));
        bar.Controls.Add(Button("Mentés másként", (_, _) => SaveDocumentAs()));
        bar.Controls.Add(Button("Validálás", (_, _) => ValidateDocument(showSuccess: true)));
        bar.Controls.Add(Button("Képernyő előnézete", (_, _) => PreviewSelected()));
        bar.Controls.Add(Button("Oldalpanel", (_, _) => ToggleInspector()));
        bar.Controls.Add(Button("Súgó", (_, _) => ShowEditorHelp()));
        bar.Controls.Add(new Label { Text = "   Bejárat:", AutoSize = true, Padding = new Padding(0, 7, 0, 0) });
        bar.Controls.Add(_entrance);
        bar.Controls.Add(new Label { Text = "Kijárat:", AutoSize = true, Padding = new Padding(0, 7, 0, 0) });
        bar.Controls.Add(_exit);
        return bar;
    }

    private Control BuildInspector()
    {
        var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12) };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoSize = true, Width = 390 };
        flow.Controls.Add(Heading("Kijelölt képernyő beállításai"));
        flow.Controls.Add(Description("Kattints bal oldalt egy területre. A bepipált értékek felülírják a kiválasztott template alapértékét."));
        flow.Controls.Add(Labelled("Azonosító", _id));
        flow.Controls.Add(Labelled("Név", _name));
        flow.Controls.Add(Labelled("Template", _template));
        flow.Controls.Add(Pair("Template ID", _newTemplateId, "Név", _newTemplateName));
        flow.Controls.Add(Button("Beállítások mentése új template-ként", CreateTemplate));
        flow.Controls.Add(Pair("X", _x, "Y", _y));
        flow.Controls.Add(_densityEnabled);
        flow.Controls.Add(Labelled("Erdősűrűség (%)", _density));
        flow.Controls.Add(_lakesEnabled);
        flow.Controls.Add(Pair("Minimum", _lakeMin, "Maximum", _lakeMax));
        flow.Controls.Add(_marshEnabled);
        flow.Controls.Add(Pair("Minimum", _marshMin, "Maximum", _marshMax));
        flow.Controls.Add(_buildingsEnabled);
        flow.Controls.Add(Pair("Minimum", _buildingMin, "Maximum", _buildingMax));
        flow.Controls.Add(Button("Módosítások alkalmazása", (_, _) => ApplySelected()));
        flow.Controls.Add(PairButtons("Új képernyő", AddArea, "Képernyő törlése", RemoveSelected));
        flow.Controls.Add(Heading("Kapcsolat"));
        flow.Controls.Add(Pair("Honnan", _connectionFrom, "Hová", _connectionTo));
        flow.Controls.Add(PairButtons("Kapcsolat hozzáadása", AddConnection, "Kapcsolat törlése", RemoveConnection));
        panel.Controls.Add(flow);
        return panel;
    }

    private void NewDocument()
    {
        _areas.Clear();
        _connections.Clear();
        _customTemplates.Clear();
        RefreshTemplateList();
        _areas.Add(new("ENTRANCE", "Mohakapu", new(0, 0)));
        _areas.Add(new("EXIT", "Szélcsend tisztása", new(1, 0), ForestAreaTemplateCatalog.OpenGroves));
        _connections.Add(new("ENTRANCE", "EXIT"));
        _currentFilePath = null;
        RefreshGraph();
        SelectArea(_areas[0]);
        UpdateStatus("Új, még nem mentett erdei gráf");
    }

    private ExplicitForestAreaGraphConfiguration Document()
    {
        ApplySelected();
        return new(_areas.ToArray(), _connections.ToArray(),
            _entrance.SelectedItem?.ToString() ?? _areas[0].Id,
            _exit.SelectedItem?.ToString() ?? _areas[^1].Id, _customTemplates.ToArray());
    }

    private void OpenDocument()
    {
        using var dialog = new OpenFileDialog { Filter = "Erdei gráf (*.json)|*.json|Minden fájl|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var document = ForestConfigurationJson.DeserializeDocument(File.ReadAllText(dialog.FileName));
            if (document.Level is { } level) _level.Value = Math.Clamp(level, (int)_level.Minimum, (int)_level.Maximum);
            else MessageBox.Show(this,
                "A megnyitott fájl nem tartalmaz pályaszámot (régi formátum). A jelenlegi kiválasztott pálya marad érvényben.",
                "Régi dokumentum");
            var graph = document.Graph;
            LoadGraph(graph, dialog.FileName);
            _currentFilePath = dialog.FileName;
            UpdateStatus($"Megnyitva: {dialog.FileName}");
        }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "Megnyitási hiba"); }
    }

    private void SaveDocument()
    {
        if (!ValidateDocument(showSuccess: false)) return;
        if (_currentFilePath is null) { SaveDocumentAs(); return; }
        WriteDocument(_currentFilePath);
    }

    private void SaveDocumentAs()
    {
        if (!ValidateDocument(showSuccess: false)) return;
        using var dialog = new SaveFileDialog { Filter = "Erdei gráf (*.json)|*.json", FileName = "forest-level.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _currentFilePath = dialog.FileName;
        WriteDocument(dialog.FileName);
    }

    private void WriteDocument(string fileName)
    {
        try
        {
            File.WriteAllText(fileName, ForestConfigurationJson.Serialize((int)_level.Value, Document()));
            UpdateStatus($"Mentve: {fileName}");
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Mentési hiba");
        }
    }

    private void LoadSelectedLevel()
    {
        var level = (int)_level.Value;
        var configuration = MazeLevelConfigurations.Get(level);
        var layout = configuration.Layout as ForestMazeLayoutConfiguration;
        if (layout is null)
        {
            MessageBox.Show(this,
                $"A(z) {level}. pálya nem erdei layout, ezért a szerkesztő kihagyja.",
                "Betöltés kihagyva");
            UpdateStatus($"Kihagyva: {level}. pálya nem erdei layout ({configuration.Name})");
            return;
        }
        if (layout.ExplicitGraph is null)
        {
            MessageBox.Show(this, $"A(z) {level}. pályához nem található explicit erdei gráf.", "Betöltési hiba");
            return;
        }
        LoadGraph(layout.ExplicitGraph, $"a játék beépített {level}. pályája");
        _currentFilePath = null;
        UpdateStatus($"A(z) {level}. pálya betöltve – Mentés gombbal válassz JSON-fájlt");
    }

    private void LoadGraph(ExplicitForestAreaGraphConfiguration graph, string source)
    {
        _areas.Clear();
        _areas.AddRange(graph.Areas);
        _connections.Clear();
        _connections.AddRange(graph.Connections);
        _customTemplates.Clear();
        _customTemplates.AddRange(graph.Templates ?? []);
        RefreshTemplateList();
        RefreshGraph(graph.EntranceAreaId, graph.ExitAreaId);
        SelectArea(_areas.FirstOrDefault());
        UpdateStatus($"Betöltve: {source}");
    }

    private void ToggleInspector()
    {
        _split.Panel2Collapsed = !_split.Panel2Collapsed;
        EnsureInspectorVisible();
    }

    private void EnsureInspectorVisible()
    {
        if (_split.Panel2Collapsed || _split.Width < 800) return;
        // A minimumok csak az első valódi ablakméret után állíthatók be. A SplitContainer
        // konstrukció közben még kb. 150 px széles, ott a 400 px-es jobb minimum kivételt dobna.
        _split.Panel1MinSize = 0;
        _split.Panel2MinSize = 0;
        var desired = _split.Width - 430;
        var maximum = _split.Width - 400 - _split.SplitterWidth;
        _split.SplitterDistance = Math.Clamp(desired, 350, maximum);
        _split.Panel1MinSize = 350;
        _split.Panel2MinSize = 400;
    }

    private void ShowEditorHelp()
    {
        MessageBox.Show(this,
            "1. Kattints egy területdobozra; a jobb oldali panel annak adatait mutatja.\n" +
            "2. A dobozt húzva rácsponton mozgathatod. Csak egymás melletti rácspontok köthetők össze.\n" +
            "3. A template adja az alapkaraktert. Csak a bepipált helyi értékek írják felül.\n" +
            "4. A kapcsolat részben válassz két területet, majd add hozzá vagy töröld az élt.\n" +
            "5. A Mentés JSON-fájlt készít. Megnyitás ugyanilyen JSON-t tölt vissza.\n" +
            "6. A pályaszám kiválasztása után a Pálya betöltése a játék beépített gráfját nyitja meg.\n" +
            "   Nem erdei pályánál a szerkesztő figyelmeztet és kihagyja a betöltést.\n\n" +
            "Ha a jobb panel rejtve van, nyomd meg az Oldalpanel gombot.",
            "Erdei pályagenerátor – gyors súgó");
    }

    private void UpdateStatus(string message) => _status.Text = message;

    private bool ValidateDocument(bool showSuccess)
    {
        try
        {
            Document().Validate();
            if (showSuccess) MessageBox.Show(this, "A gráf és minden template-hivatkozás érvényes.", "Siker");
            return true;
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Validációs hiba");
            return false;
        }
    }

    private void PreviewSelected()
    {
        if (_selected is null || !ValidateDocument(showSuccess: false)) return;
        try
        {
            var graph = Document();
            var configuration = ForestAreaConfigurationResolver.Resolve(new(), graph, _selected);
            var settings = new MazeGenerationSettings { RoomCount = 8, MinimumRoomSize = 4,
                MaximumRoomSize = 8, TreasureChestCount = 0, LevelName = _selected.Name };
            var maze = new ForestMazeGenerator(settings, configuration, [], [], new Random(12345)).Create(170, 44);
            new ForestPreviewForm(maze, _selected.Name).ShowDialog(this);
        }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "Előnézeti hiba"); }
    }

    private void SelectArea(ForestAreaDefinition? area)
    {
        _selected = area;
        _canvas.SelectedArea = area;
        if (area is null) return;
        _id.Text = area.Id; _name.Text = area.Name; _template.SelectedItem = area.TemplateId;
        _x.Value = area.Coordinate.X; _y.Value = area.Coordinate.Y;
        var patch = area.Overrides;
        _densityEnabled.Checked = patch?.ForestDensity is not null;
        if (patch?.ForestDensity is { } density) _density.Value = (decimal)(density * 100);
        SetRange(patch?.LakeCount, _lakesEnabled, _lakeMin, _lakeMax);
        SetRange(patch?.MarshCount, _marshEnabled, _marshMin, _marshMax);
        SetRange(patch?.BuildingCount, _buildingsEnabled, _buildingMin, _buildingMax);
        _canvas.Invalidate();
    }

    private void ApplySelected()
    {
        if (_selected is null) return;
        var index = _areas.IndexOf(_selected);
        if (index < 0) return;
        var patch = _selected.Overrides ?? new ForestGenerationConfigurationPatch();
        patch = patch with
        {
            ForestDensity = _densityEnabled.Checked ? (double)_density.Value / 100 : null,
            LakeCount = Range(_lakesEnabled, _lakeMin, _lakeMax),
            MarshCount = Range(_marshEnabled, _marshMin, _marshMax),
            BuildingCount = Range(_buildingsEnabled, _buildingMin, _buildingMax)
        };
        var replacement = _selected with
        {
            Id = _id.Text.Trim(), Name = _name.Text.Trim(),
            Coordinate = new((int)_x.Value, (int)_y.Value),
            TemplateId = _template.SelectedItem?.ToString() ?? ForestAreaTemplateCatalog.MixedForest,
            Overrides = patch
        };
        var oldId = _selected.Id;
        _areas[index] = replacement;
        for (var connectionIndex = 0; connectionIndex < _connections.Count; connectionIndex++)
        {
            var connection = _connections[connectionIndex];
            _connections[connectionIndex] = connection with
            {
                FirstAreaId = connection.FirstAreaId == oldId ? replacement.Id : connection.FirstAreaId,
                SecondAreaId = connection.SecondAreaId == oldId ? replacement.Id : connection.SecondAreaId
            };
        }
        var entrance = _entrance.SelectedItem?.ToString() == oldId ? replacement.Id : _entrance.SelectedItem?.ToString();
        var exit = _exit.SelectedItem?.ToString() == oldId ? replacement.Id : _exit.SelectedItem?.ToString();
        _selected = replacement;
        RefreshGraph(entrance, exit);
    }

    private void MoveArea(ForestAreaDefinition area, AreaCoordinate coordinate)
    {
        SelectArea(area); _x.Value = coordinate.X; _y.Value = coordinate.Y; ApplySelected();
    }

    private void AddArea(object? _, EventArgs __)
    {
        ApplySelected();
        var number = 1;
        while (_areas.Any(area => area.Id == $"AREA_{number}")) number++;
        var area = new ForestAreaDefinition($"AREA_{number}", $"Új terület {number}",
            new(_areas.Count == 0 ? 0 : _areas.Max(existing => existing.Coordinate.X) + 1, 0));
        _areas.Add(area); RefreshGraph(); SelectArea(area);
    }

    private void RemoveSelected(object? _, EventArgs __)
    {
        if (_selected is null || _areas.Count <= 1) return;
        _connections.RemoveAll(connection => connection.FirstAreaId == _selected.Id || connection.SecondAreaId == _selected.Id);
        _areas.Remove(_selected); RefreshGraph(); SelectArea(_areas.FirstOrDefault());
    }

    private void CreateTemplate(object? _, EventArgs __)
    {
        if (_selected is null) return;
        ApplySelected();
        var id = _newTemplateId.Text.Trim();
        var name = _newTemplateName.Text.Trim();
        var reservedIds = ForestAreaTemplateCatalog.BuiltIns.Select(template => template.Id)
            .Concat(_customTemplates.Select(template => template.Id));
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(this, "Az új template azonosítója és neve is kötelező.", "Hiányzó adat");
            return;
        }
        if (reservedIds.Contains(id, StringComparer.Ordinal))
        {
            MessageBox.Show(this, "Ez a template-azonosító már foglalt.", "Ismételt azonosító");
            return;
        }

        var index = _areas.IndexOf(_selected);
        var template = new ForestAreaTemplateDefinition(id, name, _selected.TemplateId,
            _selected.Overrides ?? new ForestGenerationConfigurationPatch());
        _customTemplates.Add(template);
        _selected = _selected with { TemplateId = id, Overrides = null };
        _areas[index] = _selected;
        RefreshTemplateList();
        _template.SelectedItem = id;
        _newTemplateId.Clear();
        _newTemplateName.Clear();
        RefreshGraph();
        SelectArea(_selected);
    }

    private void AddConnection(object? _, EventArgs __)
    {
        var first = _connectionFrom.SelectedItem?.ToString(); var second = _connectionTo.SelectedItem?.ToString();
        if (first is null || second is null || first == second) return;
        if (!_connections.Any(connection => connection.FirstAreaId == first && connection.SecondAreaId == second ||
                                            connection.FirstAreaId == second && connection.SecondAreaId == first))
            _connections.Add(new(first, second));
        RefreshGraph();
    }

    private void RemoveConnection(object? _, EventArgs __)
    {
        var first = _connectionFrom.SelectedItem?.ToString(); var second = _connectionTo.SelectedItem?.ToString();
        _connections.RemoveAll(connection => connection.FirstAreaId == first && connection.SecondAreaId == second ||
                                             connection.FirstAreaId == second && connection.SecondAreaId == first);
        RefreshGraph();
    }

    private void RefreshGraph(string? entrance = null, string? exit = null)
    {
        entrance ??= _entrance.SelectedItem?.ToString(); exit ??= _exit.SelectedItem?.ToString();
        foreach (var combo in new[] { _connectionFrom, _connectionTo, _entrance, _exit })
        {
            combo.Items.Clear(); combo.Items.AddRange(_areas.Select(area => area.Id).ToArray());
        }
        SelectCombo(_entrance, entrance ?? _areas.FirstOrDefault()?.Id);
        SelectCombo(_exit, exit ?? _areas.LastOrDefault()?.Id);
        if (_areas.Count > 0) { _connectionFrom.SelectedIndex = 0; _connectionTo.SelectedIndex = Math.Min(1, _areas.Count - 1); }
        _canvas.Areas = _areas; _canvas.Connections = _connections; _canvas.SelectedArea = _selected; _canvas.Invalidate();
    }

    private static IntRange? Range(CheckBox enabled, NumericUpDown minimum, NumericUpDown maximum) =>
        enabled.Checked ? new IntRange((int)minimum.Value, (int)maximum.Value) : null;

    private static void SetRange(IntRange? range, CheckBox enabled, NumericUpDown minimum, NumericUpDown maximum)
    {
        enabled.Checked = range is not null;
        if (range is null) return;
        minimum.Value = range.Minimum; maximum.Value = range.Maximum;
    }

    private static void SelectCombo(ComboBox combo, string? value)
    {
        if (value is not null && combo.Items.Contains(value)) combo.SelectedItem = value;
        else if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private void RefreshTemplateList()
    {
        _template.Items.Clear();
        _template.Items.AddRange(ForestAreaTemplateCatalog.BuiltIns.Select(template => template.Id)
            .Concat(_customTemplates.Select(template => template.Id)).Distinct(StringComparer.Ordinal).ToArray());
    }

    private static NumericUpDown Number(int minimum, int maximum, int value = 0) => new()
        { Minimum = minimum, Maximum = maximum, Value = value, Width = 90 };
    private static Button Button(string text, EventHandler click)
    {
        var button = new Button { Text = text, AutoSize = true }; button.Click += click; return button;
    }
    private static Label Heading(string text) => new() { Text = text, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
        AutoSize = true, Padding = new Padding(0, 12, 0, 4) };
    private static Label Description(string text) => new() { Text = text, ForeColor = Color.DimGray,
        AutoSize = false, Width = 380, Height = 52, Padding = new Padding(0, 0, 4, 4) };
    private static Control Labelled(string text, Control control)
    {
        var panel = new Panel { Width = 380, Height = 52 };
        panel.Controls.Add(control); panel.Controls.Add(new Label { Text = text, Dock = DockStyle.Top, Height = 20 }); return panel;
    }
    private static Control Pair(string firstText, Control first, string secondText, Control second)
    {
        var panel = new FlowLayoutPanel { Width = 380, Height = 54 };
        panel.Controls.Add(new Label { Text = firstText, Width = 66, Padding = new Padding(0, 8, 0, 0) }); panel.Controls.Add(first);
        panel.Controls.Add(new Label { Text = secondText, Width = 72, Padding = new Padding(0, 8, 0, 0) }); panel.Controls.Add(second); return panel;
    }
    private static Control PairButtons(string firstText, EventHandler first, string secondText, EventHandler second)
    {
        var panel = new FlowLayoutPanel { Width = 380, Height = 40 };
        panel.Controls.Add(Button(firstText, first)); panel.Controls.Add(Button(secondText, second)); return panel;
    }
}

internal sealed class ForestPreviewForm : Form
{
    public ForestPreviewForm(Maze maze, string title)
    {
        Text = $"Előnézet – {title}"; Width = 1300; Height = 780;
        var text = new System.Text.StringBuilder();
        for (var y = 0; y < maze.Height; y++)
        {
            for (var x = 0; x < maze.Width; x++)
            {
                var position = new Position(x, y);
                text.Append(maze.GetDoorAt(position)?.Symbol.ToString() ?? maze.Tiles[x, y].ToString());
            }
            text.AppendLine();
        }
        Controls.Add(new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, WordWrap = false,
            Font = new Font("Cascadia Mono", 9), BackColor = Color.Black, ForeColor = Color.LightGray,
            Text = text.ToString() });
    }
}
