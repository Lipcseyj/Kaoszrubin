using KaoszRubin.World;

namespace KaoszRubin.MapEditor;

internal sealed class EncounterEditorDialog : Form
{
    private sealed record Choice(object? Value, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly EncounterEditorContext _context;
    private readonly IReadOnlyDictionary<string, string> _monsters;
    private readonly ComboBox _kind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420 };
    private readonly FlowLayoutPanel _fields = new()
        { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
    private readonly TextBox _preview = new()
        { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly Label _error = new() { Dock = DockStyle.Fill, ForeColor = Color.Firebrick, AutoEllipsis = true };
    private readonly Button _accept = new() { Text = "Alkalmazás", AutoSize = true };
    private readonly Dictionary<string, Func<object?>> _arguments = [];
    private readonly Dictionary<string, Func<object?>> _properties = [];
    private readonly Dictionary<string, string> _mapped = new()
    {
        ["movement"] = nameof(EnemyEncounterConfiguration.MovementProfile),
        ["terrainTags"] = nameof(EnemyEncounterConfiguration.TargetTerrainTags),
        ["triggerDistance"] = nameof(EnemyEncounterConfiguration.TriggerDistance)
    };
    private ComboBox _target = null!;
    private ComboBox? _roomKind;
    private EncounterDraft _draft;
    private bool _loading;
    public string Expression { get; private set; } = "";

    public EncounterEditorDialog(EncounterDraft draft, EncounterEditorContext context,
        IReadOnlyDictionary<string, string> monsters)
    {
        _context = context;
        _monsters = monsters;
        _draft = draft;
        if (draft.Overrides.ContainsKey("Members") || draft.Overrides.ContainsKey("GroupCount"))
        {
            var effective = draft.Configuration();
            _draft = new EncounterDraft(EncounterDraft.Custom);
            foreach (var parameter in _draft.Parameters)
                _draft.Arguments[parameter.Name!] = typeof(EnemyEncounterConfiguration).GetProperty(parameter.Name!)!.GetValue(effective);
        }
        Text = "Találkozás szerkesztése";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(800, 840);
        MinimumSize = new Size(660, 600);
        MaximizeBox = true;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 6 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        var typeRow = new FlowLayoutPanel { Dock = DockStyle.Fill };
        typeRow.Controls.Add(new Label { Text = "Találkozás típusa", AutoSize = true, Padding = new Padding(0, 5, 10, 0) });
        typeRow.Controls.Add(_kind);
        foreach (var name in EncounterDraft.Factories.Keys.Where(name => context.Forest || name != "TerrainAmbush"))
            _kind.Items.Add(new Choice(name, KindLabel(name)));
        _kind.Items.Add(new Choice(EncounterDraft.Custom, "Egyedi csoport – tetszőleges tagok és darabszámok"));
        if (!_kind.Items.Cast<Choice>().Any(c => Equals(c.Value, _draft.Kind)))
            _kind.Items.Add(new Choice(_draft.Kind, KindLabel(_draft.Kind) + " (ezen a pályán nem érvényes)"));
        _kind.SelectedItem = _kind.Items.Cast<Choice>().Single(c => Equals(c.Value, _draft.Kind));
        _kind.SelectedIndexChanged += (_, _) =>
        {
            var next = new EncounterDraft((string)((Choice)_kind.SelectedItem!).Value!);
            foreach (var parameter in next.Parameters)
                if (_arguments.TryGetValue(parameter.Name!, out var read))
                    try { next.Arguments[parameter.Name!] = read(); } catch (FormatException) { }
            var previous = _draft.Configuration();
            next.Overrides["AreaId"] = previous.AreaId;
            next.Overrides["ScreenNumber"] = previous.ScreenNumber;
            next.Overrides["TargetRoomKind"] = previous.TargetRoomKind;
            _draft = next;
            BuildFields();
        };
        layout.Controls.Add(typeRow, 0, 0);
        layout.Controls.Add(_fields, 0, 1);
        layout.Controls.Add(new Label { Text = "A fájlba kerülő C# kifejezés", Dock = DockStyle.Fill }, 0, 2);
        layout.Controls.Add(_preview, 0, 3);
        layout.Controls.Add(_error, 0, 4);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "Mégse", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(_accept);
        _accept.Click += (_, _) =>
        {
            UpdatePreview();
            if (!_accept.Enabled) return;
            Expression = _preview.Text;
            DialogResult = DialogResult.OK;
        };
        AcceptButton = _accept;
        CancelButton = cancel;
        layout.Controls.Add(buttons, 0, 5);
        Controls.Add(layout);
        BuildFields();
    }

    private void BuildFields()
    {
        _loading = true;
        foreach (var control in _fields.Controls.Cast<Control>().ToArray()) control.Dispose();
        _fields.Controls.Clear();
        _arguments.Clear();
        _properties.Clear();
        _roomKind = null;
        var effective = _draft.Configuration();
        foreach (var parameter in _draft.Parameters)
        {
            var name = parameter.Name!;
            if (_mapped.ContainsKey(name) || name == "screen" ||
                name is not ("Members" or "GroupCount") && typeof(EnemyEncounterConfiguration).GetProperty(name) is not null) continue;
            _arguments[name] = AddValue(LabelFor(name), parameter.ParameterType, _draft.Arguments[name]);
        }
        var targets = new List<Choice> { new(null, "Automatikus elosztás") };
        targets.AddRange(Enumerable.Range(1, _context.GuaranteedScreens).Select(number => new Choice(number, $"{number}. képernyő")));
        if (_context.Forest) targets.AddRange(_context.AreaIds.Select(id => new Choice(id, "Terület: " + id)));
        object? selectedTarget = (object?)effective.AreaId ?? effective.ScreenNumber;
        if (effective.AreaId is not null && effective.ScreenNumber is not null)
            selectedTarget = $"Érvénytelen kettős cél: {effective.AreaId} / {effective.ScreenNumber}";
        _target = ChoiceBox(targets, selectedTarget);
        _target.SelectedIndexChanged += (_, _) => RefreshRoomKinds();
        AddRow("Célképernyő / AreaId", _target);
        foreach (var name in new[] { "MovementProfile", "Behavior", "TargetRoomKind", "TargetTerrainTags", "Posture", "TriggerDistance" })
        {
            var property = typeof(EnemyEncounterConfiguration).GetProperty(name)!;
            _properties[name] = AddValue(LabelFor(name), property.PropertyType, property.GetValue(effective));
        }
        _loading = false;
        RefreshRoomKinds();
    }

    private Func<object?> AddValue(string label, Type type, object? value)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(string))
        {
            var combo = ChoiceBox(_monsters.Select(m => new Choice(m.Key, m.Value + " (" + m.Key + ")")), value);
            AddRow(label, combo);
            return () => ((Choice)combo.SelectedItem!).Value;
        }
        if (underlying == typeof(TerrainTag))
        {
            var list = new CheckedListBox { Width = 430, Height = 100, CheckOnClick = true,
                Enabled = _context.Forest || (TerrainTag)value! != TerrainTag.None };
            foreach (var tag in Enum.GetValues<TerrainTag>().Where(tag => tag != TerrainTag.None))
                list.Items.Add(new Choice(tag, LabelFor(tag.ToString())), ((TerrainTag)value!).HasFlag(tag));
            list.ItemCheck += (_, _) => { if (IsHandleCreated) BeginInvoke((Action)RefreshRoomKinds); };
            AddRow(label, list);
            return () => list.CheckedItems.Cast<Choice>().Aggregate(TerrainTag.None, (tags, choice) => tags | (TerrainTag)choice.Value!);
        }
        if (underlying.IsEnum)
        {
            var options = Enum.GetValues(underlying).Cast<object>();
            if (underlying == typeof(RoomKind)) options = _context.KindsFor(((Choice)_target.SelectedItem!).Value).Cast<object>();
            if (underlying == typeof(EnemyEncounterPosture) && !_context.Forest) options = [EnemyEncounterPosture.Normal];
            var choices = options.Select(item => new Choice(item, underlying == typeof(Amount)
                ? $"{item} ({((Amount)item).Range().Minimum}–{((Amount)item).Range().Maximum})" : LabelFor(item.ToString()!))).ToList();
            if (Nullable.GetUnderlyingType(type) is not null)
                choices.Insert(0, new(null, underlying == typeof(RoomKind) ? "Bármely szobatípus" : "A szörny saját beállítása"));
            var combo = ChoiceBox(choices, value);
            if (underlying == typeof(RoomKind)) _roomKind = combo;
            AddRow(label, combo);
            return () => ((Choice)combo.SelectedItem!).Value;
        }
        if (underlying == typeof(int))
        {
            var number = Number((int)value!);
            AddRow(label, number);
            return () => (int)number.Value;
        }
        if (underlying == typeof(IntRange))
        {
            var range = (IntRange)value!;
            var min = Number(range.Minimum);
            var max = Number(range.Maximum);
            var panel = new FlowLayoutPanel { Width = 430, Height = 32 };
            panel.Controls.Add(min);
            panel.Controls.Add(new Label { Text = "–", AutoSize = true });
            panel.Controls.Add(max);
            AddRow(label, panel);
            return () => new IntRange((int)min.Value, (int)max.Value);
        }
        if (underlying == typeof(IReadOnlyList<EnemyGroupMemberConfiguration>))
        {
            var grid = new DataGridView
            {
                Width = 690, Height = 170, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = true, AllowUserToDeleteRows = true, RowHeadersWidth = 25
            };
            grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "Enemy", HeaderText = "Ellenfél", DataSource = _monsters.Select(m => new Choice(m.Key, m.Value + " (" + m.Key + ")")).ToList(),
                ValueMember = nameof(Choice.Value), DisplayMember = nameof(Choice.Label), FillWeight = 220
            });
            grid.Columns.Add("Min", "Minimum");
            grid.Columns.Add("Max", "Maximum");
            grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "Role", HeaderText = "Szerep", DataSource = Enum.GetValues<EnemyGroupRole>() });
            foreach (var member in (IReadOnlyList<EnemyGroupMemberConfiguration>)value!)
                grid.Rows.Add(member.EnemyId, member.Count.Minimum, member.Count.Maximum, member.Role);
            grid.DefaultValuesNeeded += (_, e) =>
            {
                e.Row.Cells[0].Value = _monsters.Keys.FirstOrDefault();
                e.Row.Cells[1].Value = 1; e.Row.Cells[2].Value = 1; e.Row.Cells[3].Value = EnemyGroupRole.Member;
            };
            grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            grid.CellValueChanged += (_, _) => UpdatePreview();
            grid.RowsRemoved += (_, _) => UpdatePreview();
            grid.DataError += (_, e) => { e.ThrowException = false; _error.Text = "Válassz létező ellenfelet és szerepet."; };
            _fields.Controls.Add(new Label { Text = label + " — új sor: hozzáadás; sorkijelölés + Delete: törlés", AutoSize = true });
            _fields.Controls.Add(grid);
            return () => grid.Rows.Cast<DataGridViewRow>().Where(row => !row.IsNewRow).Select(row =>
            {
                if (row.Cells[0].Value is not string enemy ||
                    !int.TryParse(row.Cells[1].Value?.ToString(), out var minimum) ||
                    !int.TryParse(row.Cells[2].Value?.ToString(), out var maximum) ||
                    row.Cells[3].Value is not EnemyGroupRole role)
                    throw new FormatException("Töltsd ki a csoporttag ellenfelét, minimumát, maximumát és szerepét.");
                return new EnemyGroupMemberConfiguration(enemy, new(minimum, maximum), role);
            }).ToArray();
        }
        throw new InvalidOperationException("Nem támogatott mezőtípus: " + type.Name);
    }

    private ComboBox ChoiceBox(IEnumerable<Choice> values, object? selected)
    {
        var combo = new ComboBox { Width = 430, DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 530 };
        combo.Items.AddRange(values.Cast<object>().ToArray());
        var choice = combo.Items.Cast<Choice>().FirstOrDefault(item => Equals(item.Value, selected));
        if (choice is null)
        {
            choice = new Choice(selected, $"{selected} (jelenleg nem elérhető; válassz másikat)");
            combo.Items.Add(choice);
        }
        combo.SelectedItem = choice;
        combo.SelectedIndexChanged += (_, _) => UpdatePreview();
        return combo;
    }

    private NumericUpDown Number(int value)
    {
        var number = new NumericUpDown { Minimum = Math.Min(0, value), Maximum = int.MaxValue, Value = value, Width = 130 };
        number.ValueChanged += (_, _) => UpdatePreview();
        return number;
    }

    private void AddRow(string label, Control control)
    {
        var row = new FlowLayoutPanel { Width = 710, Height = Math.Max(36, control.Height + 8), WrapContents = false };
        row.Controls.Add(new Label { Text = label, Width = 230, Height = row.Height, Padding = new Padding(0, 5, 0, 0) });
        row.Controls.Add(control);
        _fields.Controls.Add(row);
    }

    private void UpdatePreview()
    {
        if (_loading) return;
        try
        {
            foreach (var (name, read) in _arguments) _draft.Arguments[name] = read();
            var properties = _properties.ToDictionary(entry => entry.Key, entry => entry.Value());
            if (properties["Posture"] is EnemyEncounterPosture.Ambush && (int)properties["TriggerDistance"]! < 1)
                throw new InvalidDataException("A rajtaütés aktiválási távolsága legalább 1 mező legyen.");
            foreach (var parameter in _draft.Parameters)
            {
                var name = parameter.Name!;
                if (_mapped.TryGetValue(name, out var property)) _draft.Arguments[name] = properties[property];
                else if (properties.TryGetValue(name, out var value)) _draft.Arguments[name] = value;
                if (name is "screen" or "ScreenNumber" or "AreaId") _draft.Arguments[name] = null;
            }
            var target = ((Choice)_target.SelectedItem!).Value;
            properties["AreaId"] = target as string;
            properties["ScreenNumber"] = target is int number ? number : null;
            foreach (var property in properties.Keys) _draft.Overrides.Remove(property);
            var baseline = _draft.Configuration();
            foreach (var (name, value) in properties)
                if (!Equals(typeof(EnemyEncounterConfiguration).GetProperty(name)!.GetValue(baseline), value))
                    _draft.Overrides[name] = value;
            _preview.Text = _draft.Expression();
            _context.Validate(_draft.Configuration());
            _error.Text = "";
            _accept.Enabled = true;
        }
        catch (Exception exception) when (exception is FormatException or InvalidDataException or ArgumentException)
        {
            _error.Text = exception.Message;
            _accept.Enabled = false;
        }
    }

    private void RefreshRoomKinds()
    {
        if (_loading || _roomKind is null) return;
        var selected = (_roomKind.SelectedItem as Choice)?.Value;
        _loading = true;
        _roomKind.Items.Clear();
        _roomKind.Items.Add(new Choice(null, "Bármely szobatípus"));
        var terrain = _properties.TryGetValue("TargetTerrainTags", out var read) ? (TerrainTag)read()! : TerrainTag.None;
        foreach (var kind in terrain == TerrainTag.None ? _context.KindsFor(((Choice)_target.SelectedItem!).Value) : [])
            _roomKind.Items.Add(new Choice(kind, LabelFor(kind.ToString())));
        var matching = _roomKind.Items.Cast<Choice>().FirstOrDefault(choice => Equals(choice.Value, selected));
        if (matching is null)
        {
            matching = new Choice(selected, $"{selected} (itt nem elérhető; válassz másikat)");
            _roomKind.Items.Add(matching);
        }
        _roomKind.SelectedItem = matching;
        _loading = false;
        UpdatePreview();
    }

    private static string KindLabel(string kind) => kind switch
    {
        "Same" => "Same – azonos ellenfelek csoportja", "Solo" => "Solo – különálló ellenfelek",
        "Mixed" => "Mixed – kétféle ellenfél", "LeaderGroup" => "LeaderGroup – vezér és kísérők",
        "Horde" => "Horde – azonos ellenfelek hordája", "MixedHorde" => "MixedHorde – vegyes horda",
        "LeaderHorde" => "LeaderHorde – vezéres horda", "TerrainAmbush" => "TerrainAmbush – terepi rajtaütés",
        _ => kind
    };

    private static string LabelFor(string name) => name switch
    {
        "enemyId" => "Ellenfél", "firstEnemyId" => "Első ellenfél", "secondEnemyId" => "Második ellenfél",
        "leaderId" => "Vezér", "followerId" => "Kísérő", "groups" or "GroupCount" => "Csoportok száma",
        "size" => "Létszám csoportonként", "count" => "Különálló ellenfelek száma", "followers" => "Kísérők csoportonként",
        "firstCount" => "Első ellenfél / csoport", "secondCount" => "Második ellenfél / csoport", "Members" => "Csoporttagok",
        "MovementProfile" => "Mozgás", "Behavior" => "Csoportviselkedés", "TargetRoomKind" => "Célzott szobatípus",
        "TargetTerrainTags" => "Célzott terepek", "Posture" => "Harckészültség", "TriggerDistance" => "Rajtaütési távolság (mező)",
        "Stationary" => "Helyben marad", "Wander" => "Vándorol", "Patrol" => "Járőrözik",
        "Default" => "Alapértelmezett", "Horde" => "Horda", "Normal" => "Normál", "Ambush" => "Rajtaütés",
        "Generic" => "Általános szoba", "Clearing" => "Tisztás", "Cabin" => "Kunyhó", "Manor" => "Kúria", "Labyrinth" => "Labirintusépület",
        "Bush" => "Bokor", "Undergrowth" => "Aljnövényzet", "DenseUndergrowth" => "Sűrű aljnövényzet",
        "Marsh" => "Mocsár", "ThicketEdge" => "Bozótszegély", "TreeCanopy" => "Lombkorona", _ => name
    };
}
