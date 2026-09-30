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
    private readonly PropertyGrid _forestProperties = new() { Width = 390, Height = 620, HelpVisible = true, ToolbarVisible = true };
    private bool _loadingSelection;
    private ForestGenerationConfiguration _propertyGridInherited = new();
    private ForestAreaDefinition? _selected;
    private string? _currentFilePath;
    private ForestGenerationConfiguration _previewBaseConfiguration = new();
    private int? _lastPreviewSeed;

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
        _template.SelectedIndexChanged += (_, _) => RefreshEffectiveProperties();
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
        bar.Controls.Add(Button("Template-részletek", (_, _) => ShowTemplateDetails()));
        bar.Controls.Add(Button("Új előnézet", (_, _) => PreviewSelected(reuseLastSeed: false)));
        bar.Controls.Add(Button("Előző seed ismétlése", (_, _) => PreviewSelected(reuseLastSeed: true)));
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
        flow.Controls.Add(Heading("Minden generálási tulajdonság"));
        flow.Controls.Add(Description("A rács a template és a helyi felülírások eredő értékeit mutatja. A módosított teljes állapot helyi felülírásként mentődik."));
        flow.Controls.Add(_forestProperties);
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
        _previewBaseConfiguration = new();
        _lastPreviewSeed = null;
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
            if (document.Level is { } level)
            {
                _level.Value = Math.Clamp(level, (int)_level.Minimum, (int)_level.Maximum);
                _previewBaseConfiguration = ForestBaseConfigurationForLevel(level);
            }
            else
            {
                _previewBaseConfiguration = ForestBaseConfigurationForLevel((int)_level.Value);
                MessageBox.Show(this,
                    "A megnyitott fájl nem tartalmaz pályaszámot (régi formátum). A jelenlegi kiválasztott pálya marad érvényben.",
                    "Régi dokumentum");
            }
            _lastPreviewSeed = null;
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
        _previewBaseConfiguration = layout.Forest;
        _lastPreviewSeed = null;
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
            "   Nem erdei pályánál a szerkesztő figyelmeztet és kihagyja a betöltést.\n" +
            "7. Az Új előnézet mindig új seedet használ. Az Előző seed ismétlése ugyanazt a térképet generálja újra.\n\n" +
            "Ha a jobb panel rejtve van, nyomd meg az Oldalpanel gombot.",
            "Erdei pályagenerátor – gyors súgó");
    }

    private void ShowTemplateDetails()
    {
        try
        {
            ApplySelected();
            var level = (int)_level.Value;
            var levelConfiguration = MazeLevelConfigurations.Get(level);
            var forestLayout = levelConfiguration.Layout as ForestMazeLayoutConfiguration;
            var common = forestLayout?.Forest ?? new ForestGenerationConfiguration();
            var builtInIds = ForestAreaTemplateCatalog.BuiltIns.Select(template => template.Id)
                .ToHashSet(StringComparer.Ordinal);
            var graph = new ExplicitForestAreaGraphConfiguration(
                [new("PREVIEW", "Template összehasonlítás", new(0, 0))],
                [], "PREVIEW", "PREVIEW", _customTemplates.ToArray());
            var templates = ForestAreaTemplateCatalog.BuiltIns.Concat(_customTemplates)
                .OrderBy(template => template.Id, StringComparer.Ordinal).ToArray();

            var text = new System.Text.StringBuilder();
            text.AppendLine($"Pálya: {level} — {levelConfiguration.Name}");
            text.AppendLine(forestLayout is null
                ? "A kiválasztott pálya nem erdei layout; összehasonlítás alapértelmezett erdőprofilból készül."
                : "Összehasonlítás a kiválasztott pálya közös erdőprofilja alapján készült.");
            text.AppendLine();

            foreach (var template in templates)
            {
                var area = new ForestAreaDefinition("PREVIEW", "Template összehasonlítás", new(0, 0), template.Id);
                var resolved = ForestAreaConfigurationResolver.Resolve(common, graph, area);
                text.AppendLine($"=== {template.Id} — {template.Name} [{(builtInIds.Contains(template.Id) ? "beépített" : "egyedi")}] ===");
                text.AppendLine($"Alapsablon: {template.BaseTemplateId ?? "(közös alap)"}");
                text.AppendLine("Felülírások:");
                AppendObjectDetails(text, template.Overrides, onlyNonNull: true);
                text.AppendLine("Feloldott beállítások:");
                AppendObjectDetails(text, resolved, onlyNonNull: false);
                text.AppendLine();
            }

            new TemplateDetailsForm(text.ToString()).ShowDialog(this);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Template-részletezési hiba");
        }
    }

    private static void AppendObjectDetails(System.Text.StringBuilder text, object source, bool onlyNonNull)
    {
        var hasAny = false;
        foreach (var property in source.GetType().GetProperties()
                     .Where(property => property.CanRead)
                     .OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            var value = property.GetValue(source);
            if (onlyNonNull && value is null) continue;
            hasAny = true;
            text.Append("  - ").Append(property.Name).Append(": ")
                .AppendLine(FormatDetailValue(value));
        }
        if (!hasAny) text.AppendLine("  - (nincs)");
    }

    private static string FormatDetailValue(object? value) => value switch
    {
        null => "(nincs)",
        string text => text,
        IntRange range => $"{range.Minimum}..{range.Maximum}",
        ForestBuildingStyleDefinition style => $"{style.Id}:{style.Weight}",
        ForestTerrainPalette palette =>
            $"tree={palette.Tree.Id}, pine={palette.Pine.Id}, bush={palette.Bush.Id}, flower={palette.FlowerBush.Id}, " +
            $"thicket={palette.Thicket.Id}, water={palette.Water.Id}, marsh={palette.Marsh.Id}, wall={palette.BuildingWall.Id}",
        IEnumerable<ForestBuildingStyleDefinition> styles => string.Join(", ", styles.Select(FormatDetailValue)),
        System.Collections.IEnumerable sequence when value is not string =>
            string.Join(", ", sequence.Cast<object?>().Select(FormatDetailValue)),
        _ => value.ToString() ?? "(ismeretlen)"
    };

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

    private void PreviewSelected(bool reuseLastSeed)
    {
        if (_selected is null || !ValidateDocument(showSuccess: false)) return;
        try
        {
            var graph = Document();
            var configuration = ForestAreaConfigurationResolver.Resolve(_previewBaseConfiguration, graph, _selected);
            var settings = new MazeGenerationSettings { RoomCount = 8, MinimumRoomSize = 4,
                MaximumRoomSize = 8, TreasureChestCount = 0, LevelName = _selected.Name };
            var seed = reuseLastSeed && _lastPreviewSeed is { } previousSeed
                ? previousSeed
                : Random.Shared.Next(1, int.MaxValue);
            _lastPreviewSeed = seed;
            var previewTitle = $"{_selected.Name} — seed: {seed}";
            var maze = new ForestMazeGenerator(settings, configuration, [], [], new Random(seed)).Create(170, 44);
            UpdateStatus($"Előnézet: {_selected.Name}, seed: {seed}");
            if (!TerminalMazePreview.TryShow(maze, previewTitle, out var error))
            {
                if (!string.IsNullOrWhiteSpace(error))
                    MessageBox.Show(this, error, "Windows Terminal előnézeti hiba");
                new ForestPreviewForm(maze, previewTitle).ShowDialog(this);
            }
        }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "Előnézeti hiba"); }
    }

    private static ForestGenerationConfiguration ForestBaseConfigurationForLevel(int level) =>
        MazeLevelConfigurations.Get(level).Layout is ForestMazeLayoutConfiguration forest
            ? forest.Forest
            : new ForestGenerationConfiguration();

    private void SelectArea(ForestAreaDefinition? area)
    {
        _loadingSelection = true;
        _selected = area;
        _canvas.SelectedArea = area;
        if (area is null) { _loadingSelection = false; return; }
        _id.Text = area.Id; _name.Text = area.Name; _template.SelectedItem = area.TemplateId;
        _x.Value = area.Coordinate.X; _y.Value = area.Coordinate.Y;
        var patch = area.Overrides;
        _densityEnabled.Checked = patch?.ForestDensity is not null;
        if (patch?.ForestDensity is { } density) _density.Value = (decimal)(density * 100);
        SetRange(patch?.LakeCount, _lakesEnabled, _lakeMin, _lakeMax);
        SetRange(patch?.MarshCount, _marshEnabled, _marshMin, _marshMax);
        SetRange(patch?.BuildingCount, _buildingsEnabled, _buildingMin, _buildingMax);
        _loadingSelection = false;
        RefreshEffectiveProperties();
        _canvas.Invalidate();
    }

    private void RefreshEffectiveProperties()
    {
        if (_loadingSelection || _selected is null) return;
        var templateId = _template.SelectedItem?.ToString() ?? ForestAreaTemplateCatalog.MixedForest;
        var previewArea = _selected with { TemplateId = templateId };
        var graph = new ExplicitForestAreaGraphConfiguration([previewArea], [], previewArea.Id, previewArea.Id,
            _customTemplates.ToArray());
        _propertyGridInherited = ForestAreaConfigurationResolver.Resolve(
            _previewBaseConfiguration, graph, previewArea with { Overrides = null });
        _forestProperties.SelectedObject = ForestAreaConfigurationResolver.Resolve(
            _previewBaseConfiguration, graph, previewArea);
        _forestProperties.Refresh();
    }

    private static ForestGenerationConfigurationPatch DifferencePatch(ForestGenerationConfiguration configuration,
        ForestGenerationConfiguration inherited)
    {
        var patch = new ForestGenerationConfigurationPatch();
        foreach (var patchProperty in typeof(ForestGenerationConfigurationPatch).GetProperties())
        {
            var sourceProperty = typeof(ForestGenerationConfiguration).GetProperty(patchProperty.Name);
            if (sourceProperty is null) continue;
            var value = sourceProperty.GetValue(configuration);
            if (!Equals(value, sourceProperty.GetValue(inherited))) patchProperty.SetValue(patch, value);
        }
        return patch;
    }

internal static class TerminalMazePreview
{
    public static bool TryShow(Maze maze, string title, out string? error)
    {
        error = null;
        try
        {
            var directory = Path.Combine(Path.GetTempPath(), "Kaoszrubin", "forest-previews");
            Directory.CreateDirectory(directory);
            var columns = Math.Clamp(maze.Width + 4, 100, 260);
            var rows = Math.Clamp(maze.Height + 6, 30, 140);
            var filePath = Path.Combine(directory, $"preview-{DateTime.Now:yyyyMMdd-HHmmss-fff}.ansi");
            File.WriteAllText(filePath, BuildAnsiMap(maze, title, columns, rows), new System.Text.UTF8Encoding(false));

            var scriptPath = Path.Combine(directory, "render-preview.ps1");
            File.WriteAllText(scriptPath,
                "param([Parameter(Mandatory=$true)][string]$PreviewPath,[int]$Columns=120,[int]$Rows=40)\r\n" +
                "$OutputEncoding=[Console]::OutputEncoding=[System.Text.UTF8Encoding]::UTF8\r\n" +
                "try {\r\n" +
                "  $raw=$Host.UI.RawUI\r\n" +
                "  $max=$raw.MaxPhysicalWindowSize\r\n" +
                "  $w=[Math]::Min([Math]::Max(40,$Columns),$max.Width)\r\n" +
                "  $h=[Math]::Min([Math]::Max(20,$Rows),$max.Height)\r\n" +
                "  $raw.BufferSize=New-Object Management.Automation.Host.Size([Math]::Max($raw.BufferSize.Width,$w),[Math]::Max($raw.BufferSize.Height,$h))\r\n" +
                "  $raw.WindowSize=New-Object Management.Automation.Host.Size($w,$h)\r\n" +
                "} catch {}\r\n" +
                "[Console]::Write(\"`e[8;${Rows};${Columns}t\")\r\n" +
                "[Console]::Write([IO.File]::ReadAllText($PreviewPath,[System.Text.Encoding]::UTF8))\r\n",
                new System.Text.UTF8Encoding(false));

            var startInfo = new System.Diagnostics.ProcessStartInfo("wt.exe")
            {
                UseShellExecute = true
            };
            startInfo.ArgumentList.Add("-w");
            startInfo.ArgumentList.Add("new");
            startInfo.ArgumentList.Add("new-tab");
            startInfo.ArgumentList.Add("--title");
            startInfo.ArgumentList.Add($"Előnézet – {title}");
            startInfo.ArgumentList.Add("powershell");
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoExit");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add(filePath);
            startInfo.ArgumentList.Add(columns.ToString(System.Globalization.CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add(rows.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _ = System.Diagnostics.Process.Start(startInfo);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static string BuildAnsiMap(Maze maze, string title, int columns, int rows)
    {
        var text = new System.Text.StringBuilder();
        text.Append("\u001b[8;").Append(rows).Append(';').Append(columns).Append("t");
        text.Append("\u001b[0m");
        text.Append(title).Append("\r\n");
        for (var y = 0; y < maze.Height; y++)
        {
            for (var x = 0; x < maze.Width; x++)
            {
                var position = new Position(x, y);
                var (rune, foreground, background) = CellVisual(maze, position);
                text.Append(Ansi(foreground, background));
                text.Append(rune.ToString());
            }
            text.Append("\u001b[0m\r\n");
        }
        text.Append("\u001b[0m");
        return text.ToString();
    }

    private static (System.Text.Rune Rune, ConsoleColor Foreground, ConsoleColor Background)
        CellVisual(Maze maze, Position position)
    {
        if (maze.GetDoorAt(position) is { } door)
        {
            var color = door.State switch
            {
                DoorState.Locked => ConsoleColor.Red,
                DoorState.Open => ConsoleColor.DarkGreen,
                DoorState.Closed => ConsoleColor.DarkYellow,
                DoorState.Smashed => ConsoleColor.DarkGray,
                _ => ConsoleColor.Gray
            };
            return (door.Symbol, color, ConsoleColor.Black);
        }
        if (maze.GetPassageAt(position) is not null)
            return (MazePassage.Symbol, ConsoleColor.Cyan, ConsoleColor.Black);

        var rune = maze.Tiles[position.X, position.Y];
        var terrain = maze.GetTerrainStyle(position);
        if (terrain is not null) return (rune, terrain.ForegroundColor, terrain.BackgroundColor);
        if (rune == maze.WallRune) return (rune, maze.WallColor, ConsoleColor.Black);
        if (rune == Maze.ExitMarker) return (rune, ConsoleColor.Green, ConsoleColor.Black);
        return (rune, ConsoleColor.Black, ConsoleColor.Black);
    }

    private static string Ansi(ConsoleColor foreground, ConsoleColor background) =>
        $"\u001b[{ToForegroundCode(foreground)};{ToBackgroundCode(background)}m";

    private static int ToForegroundCode(ConsoleColor color) => color switch
    {
        ConsoleColor.Black => 30,
        ConsoleColor.DarkRed => 31,
        ConsoleColor.DarkGreen => 32,
        ConsoleColor.DarkYellow => 33,
        ConsoleColor.DarkBlue => 34,
        ConsoleColor.DarkMagenta => 35,
        ConsoleColor.DarkCyan => 36,
        ConsoleColor.Gray => 37,
        ConsoleColor.DarkGray => 90,
        ConsoleColor.Red => 91,
        ConsoleColor.Green => 92,
        ConsoleColor.Yellow => 93,
        ConsoleColor.Blue => 94,
        ConsoleColor.Magenta => 95,
        ConsoleColor.Cyan => 96,
        ConsoleColor.White => 97,
        _ => 39
    };

    private static int ToBackgroundCode(ConsoleColor color) => color switch
    {
        ConsoleColor.Black => 40,
        ConsoleColor.DarkRed => 41,
        ConsoleColor.DarkGreen => 42,
        ConsoleColor.DarkYellow => 43,
        ConsoleColor.DarkBlue => 44,
        ConsoleColor.DarkMagenta => 45,
        ConsoleColor.DarkCyan => 46,
        ConsoleColor.Gray => 47,
        ConsoleColor.DarkGray => 100,
        ConsoleColor.Red => 101,
        ConsoleColor.Green => 102,
        ConsoleColor.Yellow => 103,
        ConsoleColor.Blue => 104,
        ConsoleColor.Magenta => 105,
        ConsoleColor.Cyan => 106,
        ConsoleColor.White => 107,
        _ => 49
    };
}

    private void ApplySelected()
    {
        if (_selected is null) return;
        var index = _areas.IndexOf(_selected);
        if (index < 0) return;
        var patch = _forestProperties.SelectedObject is ForestGenerationConfiguration edited
            ? DifferencePatch(edited, _propertyGridInherited)
            : _selected.Overrides ?? new ForestGenerationConfigurationPatch();
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

internal sealed class TemplateDetailsForm : Form
{
    public TemplateDetailsForm(string details)
    {
        Text = "Template-részletek";
        Width = 980;
        Height = 760;
        Controls.Add(new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            WordWrap = false,
            Font = new Font("Cascadia Mono", 9),
            BackColor = Color.Black,
            ForeColor = Color.LightGray,
            Text = details
        });
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
