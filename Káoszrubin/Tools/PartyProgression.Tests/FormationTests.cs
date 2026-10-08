using System.Reflection;
using System.Text;
using System.Text.Json;
using KaoszRubin.Application;
using KaoszRubin.Combat;
using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.UI;
using KaoszRubin.World;

internal static class FormationTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("A 2×3 és 3×2 rács minden irányban és vezérhelyen helyes", Geometry),
        ("Az ötödik tag és az üres hely megmarad normalizáláskor", EmptySlot),
        ("A normalizálás minden élő azonosítót egyszer őriz meg", Normalization),
        ("A régi hátsó sor bővítéskor középső sor lesz", ShapeTransition),
        ("Az alakzatválasztó kapacitás és létszám alapján korlátoz", ShapeAvailability),
        ("A libasor hat tagot követ kanyarban és visszaforduláskor", SingleFile),
        ("A hat tag téglalapfordulása végig járható és ütközésmentes", RotationPlan),
        ("A járható végpont nem enged falon keresztüli forgatást", UnreachableRotation),
        ("A foglalt célmező sikertelen terve nem módosít szereplőt", OccupiedDestination),
        ("Az ideiglenes követő nem marad a blokk célhelyén", FollowerPlacement),
        ("Az üres középső slot megszakítja a harmadik sor védelmét", ProtectionGap),
        ("A védőkapcsolat minden formában és támadási irányban helyes", ProtectionConditions),
        ("A védő csak a tervezett szomszédos helyről védhet", ProtectionPosition),
        ("A harmadik sor nem támad két társon keresztül", ThirdRowReach),
        ("Csak szomszédos sorok között lehet harci helycsere", AdjacentSwap),
        ("A lekötések mindkét csereirányban a mezőn maradnak", EngagementTransfer),
        ("A felkészítés minden szabad támogatóra értelmezhető", Preparation),
        ("Az alakzati helycsere és felkészítés célszemélye replikálható", CommandRoundTrip),
        ("Az új alakzat és a hat slot menthető a régi slotok megőrzésével", SaveShape),
        ("A hat státuszsor és az alattuk lévő ikonok elférnek", Display),
        ("A karakterlap sima és színes sora végrehajtható", PanelRendering)
    ];

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static LiveCharacter Character(string name) => new(name,
        new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero),
        new CharacterClassDefinition("C001", "Harcos", PrimaryAbilities.Zero, false, 1),
        new PrimaryAbilities(10, 10, 10, 10), 20, 0, 0, 0);
    private static LiveCharacter[] Members() => Enumerable.Range(1, 6).Select(index => Character($"Tag{index}")).ToArray();
    private static PartyFormationSnapshot Formation(LiveCharacter[] members, PartyFormationShape shape = PartyFormationShape.Column2x3) =>
        PartyFormationRules.WithShape(PartyFormationRules.CreateDefault(members.Select(member => member.Id), members[0].Id,
            Direction.Up), shape) with { State = PartyFormationState.Locked };
    private static Maze OpenMaze(int width = 14, int height = 14)
    {
        var maze = new Maze(width, height);
        for (var y = 1; y < height - 1; y++)
        for (var x = 1; x < width - 1; x++) maze.SetTile(new(x, y), Maze.Floor);
        return maze;
    }
    private static GameDataCatalog Catalog() => CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, "Data", "game-data.csv"));
    private static (BattleEncounter Battle, LiveCharacter[] Members, Enemy Enemy) Battle(PartyFormationSnapshot? formation = null,
        LiveCharacter[]? members = null, Position? enemyPosition = null)
    {
        members ??= Members();
        formation ??= Formation(members);
        var positions = PartyFormationRules.Positions(formation, members[0].Id, new(5, 5));
        var enemy = new ConfiguredEnemy(enemyPosition ?? new(5, 4), Catalog().Enemies.First());
        var participants = members.Where(member => positions.ContainsKey(member.Id)).Select(member =>
            new BattleCharacterParticipant(member, positions[member.Id], TacticalParticipantKind.PartyMember, 10, 3, 1,
                new CharacterBattleChoices(member)));
        var encounter = new BattleEncounter(enemy.Position, participants, [new(enemy, 5, 3, 1)],
            members[0].Id, enemy.Id, formation: formation);
        return (encounter, members, enemy);
    }

    public static void Geometry()
    {
        var members = Members();
        foreach (var shape in new[] { PartyFormationShape.Column2x3, PartyFormationShape.Wide3x2 })
        foreach (var facing in Enum.GetValues<Direction>())
        foreach (var anchor in members)
        {
            var formation = Formation(members, shape) with { Facing = facing };
            var positions = PartyFormationRules.Positions(formation, anchor.Id, new(7, 7));
            var forward = PartyFormationRules.ForwardOffset(facing);
            var right = new Position(-forward.Y, forward.X);
            var anchorIndex = PartyFormationRules.SlotIndexOf(formation, anchor.Id);
            for (var index = 0; index < formation.Slots.Count; index++)
            {
                var columnDelta = index % formation.Width - anchorIndex % formation.Width;
                var rowDelta = index / formation.Width - anchorIndex / formation.Width;
                Check(positions[formation.Slots[index]!.Value] == new Position(7 + right.X * columnDelta - forward.X * rowDelta,
                    7 + right.Y * columnDelta - forward.Y * rowDelta), "Hibás geometria vagy vezérhez igazítás.");
            }
            Check(positions.Count == 6 && positions.Values.Distinct().Count() == 6, "Elveszett vagy ütköző slot.");
        }
    }
    public static void EmptySlot()
    {
        var members = Members().Take(5).ToArray();
        var formation = Formation(members);
        var slots = formation.Slots.ToArray();
        (slots[1], slots[5]) = (slots[5], slots[1]);
        formation = PartyFormationRules.WithSlots(formation, slots);
        var normalized = PartyFormationRules.Normalize(formation, members.Select(member => member.Id), members[0].Id);
        Check(normalized.Shape == PartyFormationShape.Column2x3 && normalized.Slots[1] is null &&
            normalized.Slots.SequenceEqual(slots), "Az ötös csapat szándékos rése elmozdult.");
    }
    public static void Normalization()
    {
        var members = Members();
        var formation = Formation(members) with { FrontRight = members[0].Id, ReserveLeft = CharacterId.New() };
        var normalized = PartyFormationRules.Normalize(formation, members.Select(member => member.Id).Concat([members[1].Id]), members[0].Id);
        Check(normalized.Slots.OfType<CharacterId>().Distinct().Count() == 6 &&
            normalized.Slots.OfType<CharacterId>().ToHashSet().SetEquals(members.Select(member => member.Id)), "Duplikált vagy elveszett tag.");
    }
    public static void ShapeTransition()
    {
        var members = Members();
        var square = PartyFormationRules.CreateDefault(members.Take(4).Select(member => member.Id), members[0].Id);
        var column = PartyFormationRules.Normalize(square, members.Select(member => member.Id), members[0].Id);
        Check(column.Shape == PartyFormationShape.Column2x3 && column.Slots.Take(4).SequenceEqual(square.Slots), "A régi hátsó tagok két sorral hátrébb kerültek.");
        var wide = PartyFormationRules.WithShape(column, PartyFormationShape.Wide3x2);
        Check(wide.Slots.OfType<CharacterId>().ToHashSet().SetEquals(members.Select(member => member.Id)) &&
            wide.Slots[3] == members[2].Id && wide.Slots[4] == members[3].Id, "Formaváltás elvesztett tagot vagy támogatói sort.");
    }
    public static void ShapeAvailability()
    {
        Check(FormationEditor.AvailableShapes(4, 4).SequenceEqual([PartyFormationShape.Block2x2]) &&
            FormationEditor.AvailableShapes(5, 5).SequenceEqual([PartyFormationShape.Column2x3]) &&
            FormationEditor.AvailableShapes(6, 5).SequenceEqual([PartyFormationShape.Column2x3, PartyFormationShape.Wide3x2]),
            "Lezárt vagy túl kicsi alakzat kiválasztható.");
    }
    public static void SingleFile()
    {
        var members = Members();
        var formation = Formation(members) with { Layout = PartyFormationLayout.SingleFile };
        var positions = PartyFormationRules.Positions(formation, members[0].Id, new(5, 5));
        var shifted = PartyFormationController.SingleFileDestinations(formation, positions, members[0].Id, new(5, 4));
        Check(shifted.Count == 6 && shifted.Values.Distinct().Count() == 6 && shifted[members[5].Id] == positions[members[4].Id], "Hat tag követése hibás.");
        var turned = PartyFormationController.SingleFileDestinations(formation, shifted, members[0].Id, new(4, 4));
        Check(turned[members[1].Id] == shifted[members[0].Id] && turned.Values.Distinct().Count() == 6, "Kanyarban elveszett a nyomvonal.");
        var reversed = PartyFormationController.SingleFileDestinations(formation, positions, members[0].Id, positions[members[1].Id]);
        Check(reversed[members[1].Id] == positions[members[0].Id], "A sor nem tud visszafordulni.");
    }
    public static void RotationPlan()
    {
        var members = Members();
        foreach (var shape in new[] { PartyFormationShape.Column2x3, PartyFormationShape.Wide3x2 })
        foreach (var facing in Enum.GetValues<Direction>())
        {
            var formation = Formation(members, shape) with { Facing = facing };
            var maze = OpenMaze();
            var initial = PartyFormationRules.Positions(formation, members[0].Id, new(6, 6));
            foreach (var member in members.Skip(1)) maze.AddPartyMember(new(initial[member.Id], member));
            var rotated = PartyFormationRules.Rotate(formation, true);
            var targets = PartyFormationRules.PositionsInSameFootprint(formation, members[0].Id, initial[members[0].Id], rotated.Facing);
            var plan = PartyFormationAssemblyPlanner.PlanRelocation(maze, initial[members[0].Id], members[0], targets);
            Check(plan.Succeeded, plan.Failure ?? "Hibás forgatási terv.");
            var simulation = initial.ToDictionary(pair => pair.Key, pair => pair.Value);
            foreach (var step in plan.Steps)
            {
                Check(simulation[step.Member.Character.Id] == step.From &&
                    Math.Abs(step.From.X - step.To.X) + Math.Abs(step.From.Y - step.To.Y) == 1 && maze.IsWalkable(step.To), "A forgatás teleportál.");
                simulation[step.Member.Character.Id] = step.To;
                if (step.SwappedMember is { } swapped) simulation[swapped.Character.Id] = step.From;
                Check(simulation.Values.Distinct().Count() == 6, "Útvonalon ütköztek a társak.");
            }
            Check(targets.All(pair => simulation[pair.Key] == pair.Value), "Nem a tervezett rács állt össze.");
            Check(maze.PartyMembers.All(avatar => avatar.Position == initial[avatar.Character.Id]), "A tervezés pozíciót módosított.");
        }
    }
    public static void UnreachableRotation()
    {
        var leader = Character("Vezető"); var companion = Character("Társ");
        var maze = new Maze(9, 7);
        maze.SetTile(new(2, 2), Maze.Floor); maze.SetTile(new(2, 3), Maze.Floor); maze.SetTile(new(6, 3), Maze.Floor);
        var avatar = new PartyMemberAvatar(new(2, 3), companion); maze.AddPartyMember(avatar);
        var targets = new Dictionary<CharacterId, Position> { [leader.Id] = new(2, 2), [companion.Id] = new(6, 3) };
        Check(!PartyFormationAssemblyPlanner.PlanRelocation(maze, new(2, 2), leader, targets).Succeeded && avatar.Position == new Position(2, 3),
            "Járható, de elérhetetlen célmezőre átugrott a társ.");
    }
    public static void OccupiedDestination()
    {
        var members = Members(); var formation = Formation(members); var maze = OpenMaze();
        var initial = PartyFormationRules.Positions(formation, members[0].Id, new(6, 6));
        foreach (var member in members.Skip(1)) maze.AddPartyMember(new(initial[member.Id], member));
        var targets = PartyFormationRules.Positions(formation with { Facing = Direction.Right }, members[0].Id, new(6, 6));
        maze.AddEnemy(new ConfiguredEnemy(targets[members[5].Id], Catalog().Enemies.First()));
        Check(!PartyFormationAssemblyPlanner.PlanRelocation(maze, new(6, 6), members[0], targets).Succeeded &&
            maze.PartyMembers.All(avatar => avatar.Position == initial[avatar.Character.Id]), "Foglalt cél félig módosított állapotot hagyott.");
    }
    public static void FollowerPlacement()
    {
        var members = Members(); var formation = Formation(members); var maze = OpenMaze();
        var targets = PartyFormationRules.Positions(formation, members[0].Id, new(6, 6));
        var initial = targets.ToDictionary(pair => pair.Key, pair => pair.Value);
        initial[members[5].Id] = new(9, 9);
        foreach (var member in members.Skip(1)) maze.AddPartyMember(new(initial[member.Id], member));
        var follower = Character("Követő"); maze.AddPartyMember(new(targets[members[5].Id], follower));
        var plan = PartyFormationAssemblyPlanner.PlanRelocation(maze, initial[members[0].Id], members[0], targets);
        Check(plan.Succeeded, plan.Failure ?? "A követő elakasztotta a visszaállást.");
        var simulation = initial.ToDictionary(pair => pair.Key, pair => pair.Value); simulation[follower.Id] = targets[members[5].Id];
        foreach (var step in plan.Steps)
        {
            simulation[step.Member.Character.Id] = step.To;
            if (step.SwappedMember is { } other) simulation[other.Character.Id] = step.From;
        }
        Check(!targets.Values.Contains(simulation[follower.Id]), "A követő célhelyen maradt.");
    }
    public static void ProtectionGap()
    {
        var (battle, members, _) = Battle();
        Check(battle.IsProtectedRearTarget(members[4], new(5, 4)), "A közvetlen élő védő nem véd.");
        var formation = Formation(members) with { RearLeft = null };
        var missing = Battle(formation, members).Battle;
        Check(!missing.IsProtectedRearTarget(members[4], new(5, 4)), "A védelem átugrotta az üres középső helyet.");
    }
    public static void ProtectionConditions()
    {
        foreach (var shape in new[] { PartyFormationShape.Column2x3, PartyFormationShape.Wide3x2 })
        foreach (var facing in Enum.GetValues<Direction>())
        {
            var members = Members(); var formation = Formation(members, shape) with { Facing = facing };
            var (battle, _, enemy) = Battle(formation, members);
            var target = members[shape == PartyFormationShape.Wide3x2 ? 3 : 4];
            var protector = battle.FrontPartnerOf(target)!;
            var position = battle.PositionOf(target);
            var forward = PartyFormationRules.ForwardOffset(facing);
            var front = new Position(position.X + forward.X, position.Y + forward.Y);
            var back = new Position(position.X - forward.X, position.Y - forward.Y);
            var side = new Position(position.X - forward.Y, position.Y + forward.X);
            Check(battle.IsProtectedRearTarget(target, front) && !battle.IsProtectedRearTarget(target, back) &&
                !battle.IsProtectedRearTarget(target, side), "Oldalról vagy hátulról is védett a sor.");
            battle.Engage(target, enemy);
            Check(!battle.IsProtectedRearTarget(target, front), "Lekötött tag védett maradt.");
            var single = Battle(formation with { Layout = PartyFormationLayout.SingleFile }, members).Battle;
            Check(!single.IsProtectedRearTarget(target, front), "A libasor blokk-védelmet adott.");
            var disbanded = Battle(formation with { State = PartyFormationState.Disbanded }, members).Battle;
            Check(!disbanded.IsProtectedRearTarget(target, front), "Feloszlott alakzat védelmet adott.");
            var live = Battle(formation, members).Battle;
            protector.ReceiveDamage(protector.CurrentVitality);
            Check(!live.IsProtectedRearTarget(target, front), "Az elesett védő tovább védett.");
        }
    }
    public static void ProtectionPosition()
    {
        var (battle, members, _) = Battle();
        battle.UpdatePosition(members[2], new(8, 8));
        Check(!battle.IsProtectedRearTarget(members[4], new(5, 4)), "Elmozdult védő tovább védett.");
        battle.UpdatePosition(members[2], new(5, 6));
        battle.StaggerCharacter(members[2]);
        Check(!battle.IsProtectedRearTarget(members[4], new(5, 4)), "Megingott védő tovább védett.");
    }
    public static void ThirdRowReach()
    {
        foreach (var facing in Enum.GetValues<Direction>())
        {
            var members = Members();
            var formation = Formation(members) with { Facing = facing };
            var forward = PartyFormationRules.ForwardOffset(facing);
            var enemyPosition = new Position(5 + forward.X, 5 + forward.Y);
            var (battle, _, enemy) = Battle(formation, members, enemyPosition);
            var weapon = Catalog().Weapons.First(weapon => weapon.CanAttackFromRear && !weapon.IsRanged);
            members[2].SetInventoryItem(InventorySlotKind.Weapon, 0, weapon);
            members[4].SetInventoryItem(InventorySlotKind.Weapon, 0, weapon);
            battle.Engage(members[0], enemy);
            battle.Engage(members[2], enemy);
            Check(battle.RearFormationEnemiesInReach(members[2]).Contains(enemy) &&
                battle.RearFormationEnemiesInReach(members[4]).Count == 0, "A harmadik sor két tagon át támadhatott.");
        }
    }
    public static void AdjacentSwap()
    {
        var (battle, members, _) = Battle();
        Check(!battle.TrySwapAdjacentRows(members[4], members[0].Id, out _, out _, out _, out _) &&
            !battle.TrySwapAdjacentRows(members[4], members[5].Id, out _, out _, out _, out _), "Nem szomszédos sorba lehetett cserélni.");
        Check(battle.TrySwapAdjacentRows(members[4], members[2].Id, out _, out _, out _, out _) &&
            battle.Formation!.Slots[2] == members[4].Id && battle.Formation.Slots[4] == members[2].Id, "A harmadik sorból középre cserélés hibás.");
        Check(battle.TrySwapAdjacentRows(members[4], members[0].Id, out _, out _, out _, out _) &&
            battle.Formation!.Slots[0] == members[4].Id, "Két külön helycserével nem jutott előre a harmadik sor.");
    }
    public static void EngagementTransfer()
    {
        var (battle, members, enemy) = Battle(); battle.Engage(members[0], enemy);
        Check(battle.TrySwapAdjacentRows(members[2], members[0].Id, out _, out _, out _, out var count) && count == 1 &&
            battle.IsEngaged(members[2]) && !battle.IsEngaged(members[0]), "Előrelépés eldobta a lekötést.");
        Check(battle.TrySwapToRear(members[2], out _, out _, out _, out _) && battle.IsEngaged(members[0]), "Hátracsere eldobta a lekötést.");
    }
    public static void Preparation()
    {
        var (battle, members, enemy) = Battle();
        Check(battle.PreparationTargets().Count == 4 && battle.TryOrderCombatPreparation(members[4].Id, out _) &&
            battle.ShouldPrioritizeRearSelfBuff(members[4]) && !battle.TryOrderCombatPreparation(members[0].Id, out _), "Hibás háromsoros felkészítés.");
        battle.Engage(members[4], enemy);
        Check(!battle.TryOrderCombatPreparation(members[4].Id, out _) && !battle.ShouldPrioritizeRearSelfBuff(members[4]),
            "Lekötött támogató biztonságosan készülhetett.");
    }
    public static void CommandRoundTrip()
    {
        var command = new BattleActionCommand(PlayerId.New(), 1, CharacterId.New(), BattleId.New(), 1,
            BattleActionKind.SwapFormationRows, TargetCharacterId: CharacterId.New());
        var restored = JsonSerializer.Deserialize<BattleActionCommand>(JsonSerializer.Serialize(command))!;
        Check(restored == command, "Elveszett a harci célszemély.");
    }
    public static void SaveShape()
    {
        var members = Members();
        foreach (var shape in new[] { PartyFormationShape.Column2x3, PartyFormationShape.Wide3x2 })
        {
            var formation = Formation(members, shape);
            var restored = JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(new GameSaveData { Formation = formation }))!;
            Check(restored.Formation == formation && restored.Formation.Slots.Count == 6, "A mentés elvesztette a rácsot vagy az új helyeket.");
        }
        var square = PartyFormationRules.CreateDefault(members.Take(4).Select(member => member.Id), members[0].Id);
        var migrated = GameSaveFormat.MigrateToCurrent(new GameSaveData { Version = 36, Formation = square });
        Check(migrated.Formation == square && migrated.Formation.Shape == PartyFormationShape.Block2x2, "A régi beosztás megváltozott.");
    }
    public static void PanelRendering()
    {
        var members = Members();
        var party = new Party(new PartyCapacityRules { ExpandedPartyEnabled = true });
        party.SetLeader(members[0]); party.RecordCampaignLevelCompletion(8);
        foreach (var member in members.Skip(1)) Check(party.Add(member), "Hiányzó teszttag.");
        var renderer = new ConsoleRenderer(Catalog(), party);
        var sheet = renderer.CharacterSheet;
        var writeLine = sheet.GetType().GetMethod("WriteCharacterSheetPanelLine", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var originalOutput = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            writeLine.Invoke(sheet, [new CharacterSheetPanelLine(41, "Parti: 6/6", ConsoleColor.Cyan)]);
            sheet.GetType().GetField("_displayedCharacter", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(sheet, members[0]);
            sheet.SetFormationStatus(Formation(members));
            var cache = (Dictionary<int, CharacterSheetPanelLine>)sheet.GetType()
                .GetField("_lastCharacterSheetLines", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(sheet)!;
            Check(cache.TryGetValue(48, out var line) && line.Segments is not null &&
                line.Segments.Count(segment => segment.Text == "●") == 6, "Az ikon nem a színes panelrajzolóhoz került.");
        }
        finally { Console.SetOut(originalOutput); }
    }
    public static void Display()
    {
        var members = Members(); var formation = Formation(members);
        Check(PartyFormationDisplay.PartyStartRow == 42 && PartyFormationDisplay.PartyRows == 6 && PartyFormationDisplay.FormationRow == 48,
            "A státusz és alakzatikon rossz sorra került.");
        Check(PartyFormationDisplay.OccupancyGlyph(formation) == "⠿" &&
            PartyFormationDisplay.Text(formation).Count(character => character == '/') == 2 &&
            BattleCommandPanel.DisplayWidth(PartyFormationDisplay.Text(formation)) < CharacterSheetPanel.Width,
            "A háromsoros ikon nem ábrázolja a rácsot vagy nem fér el.");
        Check(PartyFormationDisplay.EmptySlotText(4, 4).Contains("5.") && PartyFormationDisplay.EmptySlotText(5, 4).Contains("8.") &&
            PartyFormationDisplay.EmptySlotText(4, 5).Contains("Üres"), "Összekeveredett az üres és lezárt hely.");
    }
}
