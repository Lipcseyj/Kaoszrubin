using MazeGame;
using MazeGame.Application;
using MazeGame.Combat;
using MazeGame.Data;
using MazeGame.Domain.Characters;
using MazeGame.Domain.Combat;
using System.Text.Json;

var tests = new (string Name, Action Run)[]
{
    ("A host mozgási parancsa átmegy", HostMovementIsAccepted),
    ("A vendég átvehet egy NPC-t", RemotePlayerCanTakeNpcControl),
    ("A vendég nem adhat leader-parancsot", RemotePlayerCannotIssueLeaderAction),
    ("A duplikált parancs elutasításra kerül", DuplicateCommandIsRejected),
    ("Harc közben nem futhat felfedezési parancs", ExplorationCommandIsRejectedDuringBattle),
    ("A CharacterId mentés után is stabil", CharacterIdSurvivesSerialization),
    ("Disconnectkor AI veszi át, reconnectkor visszakapja", DisconnectAndReconnectRestoreControl),
    ("A léptethető csata egy hívásra egy akciót futtat", BattleAdvanceRunsOneAction),
    ("A csata megvárhatja a játékos hálózati akcióját", BattleCanWaitForPlayerAction),
    ("A támogatás a fő akció előtt lezárhatja a csatát", SupportCanFinishBattleBeforePlayerAction),
    ("A régi Resolve API az állapotgépet hajtja", ResolveUsesStateMachineAdapter),
    ("A Resolve támogatói győzelemnél nem kér fölösleges akciót", ResolveSkipsActionAfterSupportVictory),
    ("Csak az aktív BattleId és TurnId parancsa fogadható el", BattleCommandRequiresCurrentPrompt),
    ("A varázslat command csak szemantikus választást hordoz", SpellBattleCommandIsAccepted),
    ("Hiányos varázslat command nem juthat át", MalformedSpellBattleCommandIsRejected),
    ("A promptban nem engedélyezett harci akció elutasításra kerül", DisallowedBattleActionIsRejected),
    ("A session snapshot JSON-on körbeírható", SessionSnapshotRoundTripsThroughJson),
    ("A snapshot csak az aktív harci promptot fogadja el", SnapshotRequiresCurrentBattlePrompt),
    ("A world snapshot nem szivárogtat rejtett entitást", WorldSnapshotOnlyContainsRevealedState),
    ("A mozgó world entity azonosítója stabil", WorldEntityIdSurvivesMovement)
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.WriteLine($"FAIL  {test.Name}: {exception.Message}");
    }
}

return failures == 0 ? 0 : 1;

static void HostMovementIsAccepted()
{
    var (session, leader, _) = CreateSession();
    Assert(session.Submit(new MoveCharacterCommand(session.HostPlayerId, 1, leader.Id, Direction.Right)),
        "A parancs nem került a sorba.");
    Assert(session.TryReadCommand(out var command) && command is MoveCharacterCommand,
        "A host érvényes mozgási parancsa nem olvasható ki.");
}

static void RemotePlayerCanTakeNpcControl()
{
    var (session, _, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var error), error);
    Assert(session.Submit(new MoveCharacterCommand(remote, 1, companion.Id, Direction.Left)),
        "A vendég parancsa nem került a sorba.");
    Assert(session.TryReadCommand(out var command) && command.SenderId == remote,
        "A vendég saját karakterének parancsát elutasította a session.");
}

static void RemotePlayerCannotIssueLeaderAction()
{
    var (session, _, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var error), error);
    var events = CollectEvents(session);
    session.Submit(new LeaderActionCommand(remote, 1, companion.Id, LeaderAction.Rest));
    Assert(!session.TryReadCommand(out _), "A vendég leader-parancsa átjutott.");
    Assert(events.OfType<GameCommandRejectedEvent>().Any(),
        "Az elutasított parancsról nem keletkezett esemény.");
}

static void DuplicateCommandIsRejected()
{
    var (session, leader, _) = CreateSession();
    var events = CollectEvents(session);
    session.Submit(new MoveCharacterCommand(session.HostPlayerId, 1, leader.Id, Direction.Right));
    Assert(session.TryReadCommand(out _), "Az első parancsot is elutasította a session.");
    session.Submit(new MoveCharacterCommand(session.HostPlayerId, 1, leader.Id, Direction.Left));
    Assert(!session.TryReadCommand(out _), "A duplikált parancs átjutott.");
    Assert(events.OfType<GameCommandRejectedEvent>()
        .Any(rejected => rejected.Reason.Contains("Ismételt", StringComparison.Ordinal)),
        "A duplikáció oka nem jelent meg az eseményben.");
}

static void ExplorationCommandIsRejectedDuringBattle()
{
    var (session, leader, _) = CreateSession();
    var events = CollectEvents(session);
    session.SetPhase(GameSessionPhase.Battle);
    session.Submit(new MoveCharacterCommand(session.HostPlayerId, 1, leader.Id, Direction.Right));
    Assert(!session.TryReadCommand(out _), "Harc közben átjutott egy mozgási parancs.");
    Assert(events.OfType<GameCommandRejectedEvent>()
        .Any(rejected => rejected.Reason.Contains("felfedezés", StringComparison.Ordinal)),
        "A hibás session-fázis nem jelent meg az elutasításban.");
}

static void CharacterIdSurvivesSerialization()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, "adatok.csv"));
    var roster = new CharacterRoster();
    var character = CreateCharacter("Persistent");
    roster.Add(character);
    roster.Select(character);
    var service = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-character-save.json"), data);
    var restored = service.Deserialize(service.Serialize(roster));
    Assert(restored.SelectedCharacter?.Id == character.Id, "A karakter stabil azonosítója megváltozott mentéskor.");
}

static void DisconnectAndReconnectRestoreControl()
{
    var (session, _, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var error), error);
    session.MarkPlayerDisconnected(remote);
    Assert(!session.IsHumanControlled(companion.Id), "Disconnect után emberi maradt a vezérlés.");
    session.Submit(new MoveCharacterCommand(remote, 1, companion.Id, Direction.Right));
    Assert(!session.TryReadCommand(out _), "Disconnect után átjutott a vendég parancsa.");
    Assert(session.TryReconnectPlayer(remote), "A reconnect nem találta meg a foglalt karaktert.");
    Assert(session.IsHumanControlled(companion.Id), "Reconnect után nem állt vissza az emberi vezérlés.");
    session.Submit(new MoveCharacterCommand(remote, 2, companion.Id, Direction.Right));
    Assert(session.TryReadCommand(out _), "Reconnect után elutasította az új parancsot.");
}

static void BattleAdvanceRunsOneAction()
{
    var system = CreateBattleSystem(11);
    var player = CreateCharacter("Fighter", vitality: 500);
    var enemy = CreateEnemy(hitPoints: 500, strength: 1);
    var started = system.StartBattle(player, enemy);
    var previousTurnId = started.State.TurnId;
    var step = system.Advance(started.State);
    Assert(step.State.Round == 1, "Egy Advance nem pontosan egy akciót futtatott.");
    Assert(step.State.TurnId == previousTurnId + 1, "A harci turn ID nem növekedett.");
    Assert(step.Entries.Count == 1, "Egy akció nem pontosan egy naplóbejegyzést adott.");
    Assert(!step.IsCompleted, "A nagy HP-jú tesztcsata váratlanul lezárult.");
}

static void BattleCanWaitForPlayerAction()
{
    var system = CreateBattleSystem(22);
    var player = CreateCharacter("Fighter", vitality: 500);
    var enemy = CreateEnemy(hitPoints: 100, strength: 1);
    var state = system.StartBattle(player, enemy).State;
    while (!state.IsPlayerTurn && !state.IsCompleted) system.Advance(state);
    Assert(!state.IsCompleted && state.IsPlayerTurn, "A teszt nem jutott el játékosakcióig.");
    var turnId = state.TurnId;
    Assert(state.Round <= 1, "A csata input nélkül túlhaladt a játékos körén.");
    var step = system.Advance(state, new BattlePlayerAction("Hálózatról érkezett varázslat.",
        DamageToEnemy: 100));
    Assert(step.IsCompleted && step.Result?.PlayerWon == true, "A beadott játékosakció nem zárta le a csatát.");
    Assert(state.TurnId == turnId + 1, "Nem a várt hálózati turn oldódott fel.");
}

static void SupportCanFinishBattleBeforePlayerAction()
{
    var system = CreateBattleSystem(33);
    var player = CreateCharacter("Fighter", vitality: 100);
    var enemy = CreateEnemy(hitPoints: 20, strength: 1);
    var state = system.StartBattle(player, enemy).State;
    var step = system.Advance(state, supportDamage: 20);
    Assert(step.IsCompleted && enemy.CurrentHitPoints == 0, "A támogatói sebzés nem zárta le a csatát.");
    Assert(step.Entries.Single().Message.Contains("támogató", StringComparison.Ordinal),
        "A támogatói győzelem nem kapott saját eseményt.");
}

static void ResolveUsesStateMachineAdapter()
{
    var system = CreateBattleSystem(44);
    var player = CreateCharacter("Fighter", vitality: 100);
    var enemy = CreateEnemy(hitPoints: 50, strength: 1);
    var entries = new List<BattleLogEntry>();
    var actionRequests = 0;
    var result = system.Resolve(player, enemy, entries.Add, () =>
    {
        actionRequests++;
        return new BattlePlayerAction("Adapter-akció.", DamageToEnemy: 50);
    });
    Assert(result.PlayerWon && enemy.CurrentHitPoints == 0, "A kompatibilitási Resolve nem fejezte be a csatát.");
    Assert(actionRequests == 1, "A Resolve nem egyszer kérte be a győztes játékosakciót.");
    Assert(entries.Count >= 2, "A kezdő- és akcióesemények nem jutottak el a callbackhez.");
}

static void ResolveSkipsActionAfterSupportVictory()
{
    var system = CreateBattleSystem(55);
    var player = CreateCharacter("Fighter", vitality: 100);
    var enemy = CreateEnemy(hitPoints: 10, strength: 1);
    var actionRequests = 0;
    var result = system.Resolve(player, enemy, _ => { }, () =>
    {
        actionRequests++;
        return null;
    }, () => 10);
    Assert(result.PlayerWon && actionRequests == 0,
        "A támogatói győzelem után a kompatibilitási adapter még játékosakciót kért.");
}

static void BattleCommandRequiresCurrentPrompt()
{
    var (session, leader, _) = CreateSession();
    var battleId = BattleId.New();
    var events = CollectEvents(session);
    session.SetBattlePrompt(battleId, 7, leader.Id);
    Assert(events.OfType<BattlePromptEvent>().Any(prompt => prompt.BattleId == battleId && prompt.TurnId == 7),
        "A session nem publikálta a harci promptot.");
    session.Submit(new BattleActionCommand(session.HostPlayerId, 1, leader.Id, battleId, 6,
        BattleActionKind.PhysicalAttack));
    Assert(!session.TryReadCommand(out _), "A lejárt TurnId harci parancsa átjutott.");
    session.Submit(new BattleActionCommand(session.HostPlayerId, 2, leader.Id, battleId, 7,
        BattleActionKind.PhysicalAttack));
    Assert(session.TryReadCommand(out var command) && command is BattleActionCommand,
        "Az aktív BattleId/TurnId érvényes parancsát elutasította a session.");
    session.EndBattle(battleId);
    Assert(events.OfType<BattleEndedEvent>().Any(ended => ended.BattleId == battleId),
        "A session nem publikálta a csata végét.");
}

static void SpellBattleCommandIsAccepted()
{
    var (session, leader, _) = CreateSession();
    var battleId = BattleId.New();
    session.SetBattlePrompt(battleId, 3, leader.Id,
        [BattleActionKind.PhysicalAttack, BattleActionKind.CastSpell]);
    var target = new Position(4, 2);
    session.Submit(new BattleActionCommand(session.HostPlayerId, 1, leader.Id, battleId, 3,
        BattleActionKind.CastSpell, "SP-TEST", 1, target));
    Assert(session.TryReadCommand(out var command) && command is BattleActionCommand
        {
            Action: BattleActionKind.CastSpell,
            SpellId: "SP-TEST",
            CastingItemSlotIndex: 1,
            Target: { } acceptedTarget
        } && acceptedTarget == target, "A teljes szemantikus varázslat-commandot elutasította a session.");
}

static void MalformedSpellBattleCommandIsRejected()
{
    var (session, leader, _) = CreateSession();
    var battleId = BattleId.New();
    var events = CollectEvents(session);
    session.SetBattlePrompt(battleId, 1, leader.Id, [BattleActionKind.CastSpell]);
    session.Submit(new BattleActionCommand(session.HostPlayerId, 1, leader.Id, battleId, 1,
        BattleActionKind.CastSpell, SpellId: "SP-TEST"));
    Assert(!session.TryReadCommand(out _), "A célpont nélküli varázslat-command átjutott.");
    Assert(events.OfType<GameCommandRejectedEvent>().Any(rejected => rejected.CommandId == 1),
        "A hiányos varázslat-command elutasításáról nem keletkezett esemény.");
}

static void DisallowedBattleActionIsRejected()
{
    var (session, leader, _) = CreateSession();
    var battleId = BattleId.New();
    session.SetBattlePrompt(battleId, 1, leader.Id, [BattleActionKind.PhysicalAttack]);
    session.Submit(new BattleActionCommand(session.HostPlayerId, 1, leader.Id, battleId, 1,
        BattleActionKind.TurnUndead));
    Assert(!session.TryReadCommand(out _), "A promptban nem szereplő halottűzés átjutott.");
}

static void SessionSnapshotRoundTripsThroughJson()
{
    var (session, leader, companion) = CreateSession();
    var positions = new Dictionary<CharacterId, Position>
    {
        [leader.Id] = new Position(2, 2),
        [companion.Id] = new Position(3, 2)
    };
    var snapshot = session.CreateSnapshot(new SessionSnapshotContext(4, "Tesztlabirintus", positions));
    var json = JsonSerializer.Serialize(snapshot);
    var restored = JsonSerializer.Deserialize<SessionSnapshot>(json);
    Assert(restored is not null && restored.ProtocolVersion == SessionProtocol.Version &&
           restored.Phase == GameSessionPhase.Exploration && restored.Party.Count == 2 &&
           restored.Party.Single(character => character.CharacterId == companion.Id).Position == new Position(3, 2),
        "A session snapshot JSON round-trip közben megváltozott.");
    var next = session.CreateSnapshot(new SessionSnapshotContext(4, "Tesztlabirintus", positions));
    Assert(next.SnapshotSequence == snapshot.SnapshotSequence + 1,
        "A publikált snapshot sorszáma nem monoton nő.");
}

static void SnapshotRequiresCurrentBattlePrompt()
{
    var (session, leader, _) = CreateSession();
    var battleId = BattleId.New();
    session.SetBattlePrompt(battleId, 2, leader.Id,
        [BattleActionKind.PhysicalAttack, BattleActionKind.TurnUndead]);
    var battle = new BattleSnapshot(battleId, 2, 1, true, leader.Id,
        new SessionEnemySnapshot("E-TEST", "Tesztellenfél", new Position(3, 2), 8, 10),
        [BattleActionKind.PhysicalAttack, BattleActionKind.TurnUndead]);
    var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "Tesztlabirintus",
        new Dictionary<CharacterId, Position> { [leader.Id] = new Position(2, 2) }, battle));
    Assert(snapshot.Battle?.BattleId == battleId && snapshot.Battle.AllowedActions.Count == 2,
        "Az aktív harci prompt nem került a snapshotba.");

    var stale = battle with { TurnId = 1 };
    var rejected = false;
    try
    {
        session.CreateSnapshot(new SessionSnapshotContext(1, "Tesztlabirintus",
            new Dictionary<CharacterId, Position> { [leader.Id] = new Position(2, 2) }, stale));
    }
    catch (ArgumentException)
    {
        rejected = true;
    }
    Assert(rejected, "A lejárt körhöz tartozó harci snapshotot elfogadta a session.");
}

static void WorldSnapshotOnlyContainsRevealedState()
{
    var maze = new Maze(7, 7);
    var visibleEnemy = CreateEnemyAt(new Position(3, 2), "E-VISIBLE");
    var hiddenEnemy = CreateEnemyAt(new Position(4, 5), "E-HIDDEN");
    foreach (var position in new[] { visibleEnemy.Position, hiddenEnemy.Position, new Position(1, 2) })
        maze.Carve(position);
    maze.AddEnemy(visibleEnemy);
    maze.AddEnemy(hiddenEnemy);
    maze.AddTreasureChest(new TreasureChest(new Position(1, 2), 99));
    maze.PlaceDoor(new Position(2, 3), DoorState.Closed);
    var fog = new FogOfWar(maze.Width, maze.Height, 1);
    fog.RevealFrom(maze, maze.Entrance);

    var world = WorldSnapshotProjector.Create(maze, fog);
    Assert(world.Enemies.Count == 1 && world.Enemies[0].EntityId == visibleEnemy.Id,
        "A world snapshot rejtett ellenfelet is publikált, vagy kihagyta a láthatót.");
    Assert(world.Chests.Count == 1 && world.Doors.Count == 1,
        "A felfedett statikus entitások hiányoznak a world snapshotból.");
    Assert(world.Exit is null && world.RevealedCells.All(cell => fog.IsVisible(cell.Position)),
        "A world snapshot rejtett kijáratot vagy cellát publikált.");
    var restored = JsonSerializer.Deserialize<WorldSnapshot>(JsonSerializer.Serialize(world));
    Assert(restored?.Enemies.Single().DefinitionId == "E-VISIBLE",
        "A world snapshot JSON round-trip közben megváltozott.");
}

static void WorldEntityIdSurvivesMovement()
{
    var maze = new Maze(7, 7);
    var enemy = CreateEnemyAt(new Position(2, 3), "E-MOVING");
    var destination = new Position(3, 3);
    maze.Carve(enemy.Position);
    maze.Carve(destination);
    maze.AddEnemy(enemy);
    var entityId = enemy.Id;
    Assert(maze.TryMoveEnemy(enemy, destination), "A tesztellenfél nem tudott elmozdulni.");
    Assert(enemy.Id == entityId, "A world entity azonosítója mozgáskor megváltozott.");
}

static (GameSession Session, LiveCharacter Leader, LiveCharacter Companion) CreateSession()
{
    var party = new Party();
    var leader = CreateCharacter("Leader");
    var companion = CreateCharacter("Companion");
    party.SetLeader(leader);
    party.Add(companion);
    return (new GameSession(party, leader), leader, companion);
}

static LiveCharacter CreateCharacter(string name, int vitality = 20)
{
    var abilities = new PrimaryAbilities(5, 5, 5, 5);
    var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
    var characterClass = new CharacterClassDefinition("C001", "Harcos", PrimaryAbilities.Zero, false, 1.0);
    return new LiveCharacter(name, race, characterClass, abilities, vitality, 0, 1, 0);
}

static BattleSystem CreateBattleSystem(int seed) => new(new Random(seed),
    Array.Empty<MonsterAbilityDefinition>(), Array.Empty<StatusDefinition>(),
    Array.Empty<StrengthHitBonusDefinition>());

static ConfiguredEnemy CreateEnemy(int hitPoints, int strength) => new(new Position(1, 1),
    new EnemyDefinition("E-TEST", "Tesztellenfél", "e", strength, hitPoints, 0, 1,
        1, 1, Array.Empty<string>()));

static ConfiguredEnemy CreateEnemyAt(Position position, string id) => new(position,
    new EnemyDefinition(id, "Tesztellenfél", "e", 1, 10, 0, 1,
        1, 1, Array.Empty<string>()));

static List<GameSessionEvent> CollectEvents(GameSession session)
{
    var events = new List<GameSessionEvent>();
    session.EventPublished += events.Add;
    return events;
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
