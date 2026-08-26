using MazeGame;
using MazeGame.Application;
using MazeGame.Combat;
using MazeGame.Data;
using MazeGame.Domain.Characters;
using MazeGame.Domain.Combat;
using MazeGame.Domain.Inventory;
using MazeGame.Domain.Magic;
using MazeGame.Transport.SignalR;
using System.Text.Json;
using System.Text;

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
    ("A mozgó world entity azonosítója stabil", WorldEntityIdSurvivesMovement),
    ("A world delta minden lényeges változást leír", WorldDeltaCapturesChanges),
    ("Eltérő pályák között nem készülhet delta", WorldDeltaRejectsDifferentWorld),
    ("A publisher teljes snapshot után ACK-alapú deltát küld", ReplicationPublisherUsesAcknowledgedBaseline),
    ("Ismeretlen ACK teljes resyncet kényszerít", UnknownReplicationAckForcesResync),
    ("Pályaváltáskor a publisher teljes snapshotra vált", ReplicationPublisherUsesFullSnapshotForNewWorld),
    ("A kliens store teljes snapshotot és régi baseline-ról érkező deltát alkalmaz", ClientStoreAppliesReplicationFrames),
    ("Hiányzó delta-baseline esetén a kliens resyncet kér", ClientStoreRequestsResyncForMissingBaseline),
    ("Az inventory snapshot explicit slotokat és revíziót tartalmaz", InventorySnapshotHasSlotsAndRevision),
    ("A vendég csak saját inventory read modelt kap", ReplicationPublisherRedactsOtherInventories),
    ("Az inventory transfer atomi és megőrzi a töltetet", InventoryTransferIsAtomicAndPreservesCharges),
    ("Az elavult inventory-revízió elutasításra kerül", StaleInventoryRevisionIsRejected),
    ("A vendég nem mozgathat tárgyat más karakterhez", RemoteInventoryTransferCannotCrossCharacters),
    ("A használat, eldobás és pickup command alakja validált", InventoryActionCommandsAreValidated),
    ("Nem fogyasztható tárgy használata elutasításra kerül", NonConsumableUseIsRejected),
    ("A földi loot megőrzi a töltetet és revíziózott", GroundPilePreservesChargesAndRevision),
    ("A katalógus fingerprint determinisztikus", CatalogFingerprintIsDeterministic),
    ("A handshake verziót és katalógushasht ellenőriz", HandshakeValidatesProtocolAndCatalog),
    ("A reconnect-token ugyanazt a PlayerId-t állítja vissza", HandshakeReconnectRestoresPlayer),
    ("A JSON wire codec allowlistelt commandot ír körbe", ProtocolCodecRoundTripsCommand),
    ("A host gateway kapcsolathoz köti a PlayerId-t", HostGatewayBindsAuthenticatedPlayer),
    ("A host gateway kezeli a control-, replikáció- és disconnect-folyamot", HostGatewayRunsConnectionLifecycle),
    ("A SignalR LAN host elindítható és leállítható", () =>
        SignalRServerStartsAndStops().GetAwaiter().GetResult()),
    ("A SignalR kliens végigviszi a LAN coop kapcsolatot", () =>
        SignalRClientRunsLanProtocolFlow().GetAwaiter().GetResult()),
    ("Az in-memory transport végigviszi a coop protokollfolyamot", () =>
        InMemoryTransportRunsProtocolFlow().GetAwaiter().GetResult())
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
    fog.ToggleDeveloperReveal();

    var world = WorldSnapshotProjector.Create(maze, fog);
    Assert(world.Enemies.Count == 1 && world.Enemies[0].EntityId == visibleEnemy.Id,
        "A world snapshot rejtett ellenfelet is publikált, vagy kihagyta a láthatót.");
    Assert(world.Chests.Count == 1 && world.Doors.Count == 1,
        "A felfedett statikus entitások hiányoznak a world snapshotból.");
    Assert(world.Exit is null && world.RevealedCells.All(cell => fog.IsRevealed(cell.Position)),
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

static void WorldDeltaCapturesChanges()
{
    var (session, leader, _) = CreateSession();
    var characterPositions = new Dictionary<CharacterId, Position> { [leader.Id] = new Position(2, 2) };
    var maze = new Maze(7, 7);
    var enemy = CreateEnemyAt(new Position(3, 2), "E-DELTA");
    var chest = new TreasureChest(new Position(1, 2), 10);
    foreach (var position in new[] { enemy.Position, new Position(3, 3), chest.Position }) maze.Carve(position);
    maze.AddEnemy(enemy);
    maze.AddTreasureChest(chest);
    maze.PlaceDoor(new Position(2, 3), DoorState.Closed);
    var fog = new FogOfWar(maze.Width, maze.Height, 1);
    fog.RevealFrom(maze, maze.Entrance);
    var previous = WorldSnapshotProjector.Create(maze, fog);
    var previousSession = session.CreateSnapshot(new SessionSnapshotContext(1, "Delta-labirintus",
        characterPositions, World: previous));

    enemy.SetCurrentHitPoints(6);
    Assert(maze.TryMoveEnemy(enemy, new Position(3, 3)), "A delta tesztellenfele nem tudott mozogni.");
    maze.SetDoorState(maze.GetDoorAt(new Position(2, 3))!, DoorState.Open);
    maze.RemoveTreasureChest(chest);
    var corpse = new MonsterCorpse(new Position(1, 2), "Elesett", "E-DEAD");
    maze.AddCorpse(corpse);
    fog.RevealFrom(maze, new Position(3, 3));
    var current = WorldSnapshotProjector.Create(maze, fog);
    var currentSession = session.CreateSnapshot(new SessionSnapshotContext(1, "Delta-labirintus",
        characterPositions, World: current));

    var delta = WorldDeltaProjector.Create(previousSession, currentSession);
    Assert(delta.EnemyUpserts.Single() is { CurrentHitPoints: 6, Position: { X: 3, Y: 3 } },
        "Az ellenfél mozgása vagy HP-változása hiányzik a deltából.");
    Assert(delta.DoorUpserts.Single().State == DoorState.Open && delta.CorpseUpserts.Single().EntityId == corpse.Id,
        "Az ajtóállapot vagy az új tetem hiányzik a deltából.");
    Assert(delta.RemovedEntityIds.Contains(chest.Id) && delta.RevealedOrChangedCells.Count > 0,
        "Az entitáseltávolítás vagy cellafelfedés hiányzik a deltából.");
    var restored = JsonSerializer.Deserialize<WorldDelta>(JsonSerializer.Serialize(delta));
    Assert(restored?.ToSnapshotSequence == currentSession.SnapshotSequence && restored.EnemyUpserts.Count == 1,
        "A world delta JSON round-trip közben megváltozott.");
}

static void WorldDeltaRejectsDifferentWorld()
{
    var firstMaze = new Maze(7, 7);
    var secondMaze = new Maze(7, 7);
    var first = WorldSnapshotProjector.Create(firstMaze, new FogOfWar(7, 7, 0));
    var second = WorldSnapshotProjector.Create(secondMaze, new FogOfWar(7, 7, 0));
    var rejected = false;
    try
    {
        WorldDeltaProjector.Create(1, first, 2, second);
    }
    catch (ArgumentException)
    {
        rejected = true;
    }
    Assert(rejected, "Különböző WorldId értékek között is elkészült a delta.");
}

static void ReplicationPublisherUsesAcknowledgedBaseline()
{
    var (session, leader, _) = CreateSession();
    var positions = new Dictionary<CharacterId, Position> { [leader.Id] = new Position(2, 2) };
    var maze = new Maze(7, 7);
    maze.Carve(new Position(3, 2));
    var fog = new FogOfWar(7, 7, 1);
    fog.RevealFrom(maze, maze.Entrance);
    var first = session.CreateSnapshot(new SessionSnapshotContext(1, "Replikációs pálya", positions,
        World: WorldSnapshotProjector.Create(maze, fog)));
    var playerId = PlayerId.New();
    var publisher = new SessionReplicationPublisher();

    var full = publisher.CreateFrame(playerId, first);
    Assert(full.Kind == SessionReplicationFrameKind.FullSnapshot && full.Session.World is not null &&
           full.WorldDelta is null && full.BaseSnapshotSequence is null,
        "Az első replikációs frame nem teljes snapshot.");
    Assert(publisher.TryAcknowledge(playerId, first.SnapshotSequence, out var error), error);

    var enemy = CreateEnemyAt(new Position(3, 2), "E-REPLICATION");
    maze.AddEnemy(enemy);
    var second = session.CreateSnapshot(new SessionSnapshotContext(1, "Replikációs pálya", positions,
        World: WorldSnapshotProjector.Create(maze, fog)));
    var delta = publisher.CreateFrame(playerId, second);
    Assert(delta.Kind == SessionReplicationFrameKind.Delta && delta.Session.World is null &&
           delta.BaseSnapshotSequence == first.SnapshotSequence &&
           delta.WorldDelta?.EnemyUpserts.Single().EntityId == enemy.Id,
        "A nyugtázott baseline után nem megfelelő world delta készült.");
    var restored = JsonSerializer.Deserialize<SessionReplicationFrame>(JsonSerializer.Serialize(delta));
    Assert(restored?.WorldDelta?.ToSnapshotSequence == second.SnapshotSequence,
        "A replikációs frame JSON round-trip közben megváltozott.");
}

static void UnknownReplicationAckForcesResync()
{
    var (session, leader, _) = CreateSession();
    var maze = new Maze(7, 7);
    var fog = new FogOfWar(7, 7, 0);
    fog.RevealFrom(maze, maze.Entrance);
    var positions = new Dictionary<CharacterId, Position> { [leader.Id] = maze.Entrance };
    var publisher = new SessionReplicationPublisher();
    var playerId = PlayerId.New();
    var first = session.CreateSnapshot(new SessionSnapshotContext(1, "ACK pálya", positions,
        World: WorldSnapshotProjector.Create(maze, fog)));
    publisher.CreateFrame(playerId, first);
    Assert(!publisher.TryAcknowledge(playerId, first.SnapshotSequence + 99, out _),
        "Az ismeretlen snapshot ACK-ot elfogadta a publisher.");
    var second = session.CreateSnapshot(new SessionSnapshotContext(1, "ACK pálya", positions,
        World: WorldSnapshotProjector.Create(maze, fog)));
    Assert(publisher.CreateFrame(playerId, second).Kind == SessionReplicationFrameKind.FullSnapshot,
        "Ismeretlen ACK után nem történt teljes resync.");
}

static void ReplicationPublisherUsesFullSnapshotForNewWorld()
{
    var (session, leader, _) = CreateSession();
    var positions = new Dictionary<CharacterId, Position> { [leader.Id] = new Position(2, 2) };
    var firstMaze = new Maze(7, 7);
    var firstFog = new FogOfWar(7, 7, 0);
    firstFog.RevealFrom(firstMaze, firstMaze.Entrance);
    var publisher = new SessionReplicationPublisher();
    var playerId = PlayerId.New();
    var first = session.CreateSnapshot(new SessionSnapshotContext(1, "Első pálya", positions,
        World: WorldSnapshotProjector.Create(firstMaze, firstFog)));
    publisher.CreateFrame(playerId, first);
    Assert(publisher.TryAcknowledge(playerId, first.SnapshotSequence, out var error), error);

    var secondMaze = new Maze(7, 7);
    var secondFog = new FogOfWar(7, 7, 0);
    secondFog.RevealFrom(secondMaze, secondMaze.Entrance);
    var second = session.CreateSnapshot(new SessionSnapshotContext(2, "Második pálya", positions,
        World: WorldSnapshotProjector.Create(secondMaze, secondFog)));
    Assert(publisher.CreateFrame(playerId, second).Kind == SessionReplicationFrameKind.FullSnapshot,
        "Pályaváltáskor a publisher deltát próbált küldeni.");
}

static void ClientStoreAppliesReplicationFrames()
{
    var (session, leader, _) = CreateSession();
    var playerId = PlayerId.New();
    var publisher = new SessionReplicationPublisher();
    var store = new ClientSessionStore(playerId);
    var changedSnapshots = new List<SessionSnapshot>();
    store.SnapshotChanged += changedSnapshots.Add;
    var maze = new Maze(7, 7);
    var enemyPosition = new Position(3, 2);
    maze.Carve(maze.Entrance);
    maze.Carve(enemyPosition);
    var fog = new FogOfWar(7, 7, 1);
    fog.RevealFrom(maze, maze.Entrance);
    var positions = new Dictionary<CharacterId, Position> { [leader.Id] = maze.Entrance };

    var first = session.CreateSnapshot(new SessionSnapshotContext(1, "Kliens pálya", positions,
        World: WorldSnapshotProjector.Create(maze, fog)));
    var full = publisher.CreateFrame(playerId, first);
    var fullResult = store.Apply(full);
    Assert(fullResult is { Status: ClientFrameApplyStatus.Applied, Response: SnapshotAck } &&
           store.CurrentSnapshot?.World is not null, "A kliens store nem alkalmazta a teljes snapshotot.");
    Assert(publisher.TryAcknowledge(playerId, first.SnapshotSequence, out var ackError), ackError);

    var enemy = CreateEnemyAt(enemyPosition, "E-CLIENT-DELTA");
    maze.AddEnemy(enemy);
    var second = session.CreateSnapshot(new SessionSnapshotContext(1, "Kliens pálya", positions,
        World: WorldSnapshotProjector.Create(maze, fog)));
    var firstDelta = publisher.CreateFrame(playerId, second);
    Assert(store.Apply(firstDelta).Status == ClientFrameApplyStatus.Applied &&
           store.CurrentSnapshot!.World!.Enemies.Single().EntityId == enemy.Id,
        "A kliens store nem alkalmazta az entitás-upsertet.");

    // Az ACK még nem ért vissza a hosthoz: a következő delta továbbra is az első snapshotból indul.
    maze.ReplaceEnemyWithCorpse(enemy);
    var third = session.CreateSnapshot(new SessionSnapshotContext(1, "Kliens pálya", positions,
        World: WorldSnapshotProjector.Create(maze, fog)));
    var oldBaselineDelta = publisher.CreateFrame(playerId, third);
    Assert(oldBaselineDelta.BaseSnapshotSequence == first.SnapshotSequence,
        "A tesztframe nem a várt régi ACK-baseline-ról indult.");
    Assert(store.Apply(oldBaselineDelta).Status == ClientFrameApplyStatus.Applied &&
           store.CurrentSnapshot!.World!.Enemies.Count == 0 &&
           store.CurrentSnapshot.World.Corpses.Count == 1 && changedSnapshots.Count == 3,
        "A kliens nem a deklarált baseline-ra alkalmazta a deltát, vagy nem publikálta az új read modelt.");
}

static void ClientStoreRequestsResyncForMissingBaseline()
{
    var playerId = PlayerId.New();
    var world = new WorldSnapshot(WorldId.New(), 7, 7, null, null, [], [], [], [], [], []);
    var delta = new WorldDelta(10, 11, null, null, [], [], [], [], [], [], [], []);
    var session = new SessionSnapshot(SessionProtocol.Version, 11, 0, GameSessionPhase.Exploration,
        PlayerId.New(), CharacterId.New(), 1, "Hiányzó baseline", [], [], null);
    var frame = new SessionReplicationFrame(SessionReplicationFrameKind.Delta, playerId, 10, session, delta);
    var store = new ClientSessionStore(playerId);
    var result = store.Apply(frame);
    Assert(result is { Status: ClientFrameApplyStatus.ResyncRequired, Response: SnapshotResyncRequest } &&
           store.CurrentSnapshot is null,
        "A kliens hiányzó baseline esetén nem kért teljes resyncet.");

    var wrongRecipient = new SessionReplicationFrame(SessionReplicationFrameKind.FullSnapshot, PlayerId.New(),
        null, session with { SnapshotSequence = 1, World = world }, null);
    Assert(store.Apply(wrongRecipient).Status == ClientFrameApplyStatus.Rejected,
        "A kliens elfogadta a másik játékosnak címzett snapshotot.");
}

static void InventorySnapshotHasSlotsAndRevision()
{
    var character = CreateCharacter("Inventory");
    var ration = new MiscItemDefinition("I-FOOD", "Útravaló", "Tesztélelem", 2, ConsumableEffect.Food, 10);
    Assert(character.AddToBackpack(ration), "A teszttárgy nem került a hátizsákba.");
    var first = InventorySnapshotProjector.Create(character);
    Assert(first.Revision == 1 && first.Slots.Count == 16 &&
           first.Slots.Single(slot => slot.Kind == InventorySlotKind.Backpack && slot.Index == 0)
               .Item?.DefinitionId == ration.Id,
        "Az inventory snapshot slotjai vagy revíziója hibás.");
    Assert(character.SetInventoryItem(InventorySlotKind.Backpack, 0, null), "A teszttárgy nem távolítható el.");
    var second = InventorySnapshotProjector.Create(character);
    Assert(second.Revision == first.Revision + 1 &&
           second.Slots.Single(slot => slot.Kind == InventorySlotKind.Backpack && slot.Index == 0).Item is null,
        "Az inventory mutáció nem növelte pontosan egyszer a revíziót.");
}

static void ReplicationPublisherRedactsOtherInventories()
{
    var (session, leader, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
    leader.AddToBackpack(new MiscItemDefinition("I-LEADER", "Leader tárgy", "Teszt", 1));
    companion.AddToBackpack(new MiscItemDefinition("I-GUEST", "Vendég tárgy", "Teszt", 1));
    var maze = new Maze(7, 7);
    var fog = new FogOfWar(7, 7, 0);
    fog.RevealFrom(maze, maze.Entrance);
    var positions = new Dictionary<CharacterId, Position>
    {
        [leader.Id] = maze.Entrance,
        [companion.Id] = new Position(3, 2)
    };
    var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "Inventory pálya", positions,
        World: WorldSnapshotProjector.Create(maze, fog)));
    var publisher = new SessionReplicationPublisher();
    var hostFrame = publisher.CreateFrame(session.HostPlayerId, snapshot);
    var guestFrame = publisher.CreateFrame(remote, snapshot);

    Assert(hostFrame.Session.Party.All(character => character.Inventory is not null),
        "A host nem kapta meg a teljes parti inventory read modelt.");
    Assert(guestFrame.Session.Party.Single(character => character.CharacterId == companion.Id).Inventory is not null &&
           guestFrame.Session.Party.Single(character => character.CharacterId == leader.Id).Inventory is null,
        "A vendég más karakter inventoryját is megkapta, vagy a sajátját sem kapta meg.");
}

static void InventoryTransferIsAtomicAndPreservesCharges()
{
    var party = new Party();
    var leader = CreateCharacter("InvLeader");
    party.SetLeader(leader);
    var session = new GameSession(party, leader);
    var wand = new MagicItemDefinition("MI-TEST", "Tesztpálca", MagicItemKind.Wand, ItemRarity.Magic,
        10, 5, null, MagicItemEffect.None, 0, new HashSet<string> { leader.CharacterClass.Id }, "Teszt", 1);
    Assert(leader.AddMagicItem(wand), "A tesztpálca nem került a varázstárgyslotba.");
    var revision = leader.InventoryRevision;
    var command = new InventoryTransferCommand(session.HostPlayerId, 1, leader.Id, revision,
        InventorySlotKind.MagicItem, 0, leader.Id, revision, InventorySlotKind.Backpack, 1);
    session.Submit(command);
    Assert(session.TryReadCommand(out var accepted) && accepted == command,
        "Az érvényes inventory transfer commandot elutasította a session.");
    Assert(InventoryTransferService.TryExecute(party, command, out _, out var error), error);
    Assert(leader.GetInventoryItem(InventorySlotKind.MagicItem, 0) is null &&
           leader.GetInventoryItem(InventorySlotKind.Backpack, 1)?.Id == wand.Id &&
           leader.GetInventoryItemCharges(InventorySlotKind.Backpack, 1) == wand.MaximumCharges &&
           leader.InventoryRevision == revision + 1,
        "Az atomi transfer elvesztette a tárgyat, töltetet vagy hibásan növelte a revíziót.");
}

static void StaleInventoryRevisionIsRejected()
{
    var (session, leader, _) = CreateSession();
    leader.AddToBackpack(new MiscItemDefinition("I-SOURCE", "Forrás", "Teszt", 1));
    var staleRevision = leader.InventoryRevision;
    var command = new InventoryTransferCommand(session.HostPlayerId, 1, leader.Id, staleRevision,
        InventorySlotKind.Backpack, 0, leader.Id, staleRevision, InventorySlotKind.Backpack, 1);
    leader.AddToBackpack(new MiscItemDefinition("I-CHANGE", "Változás", "Teszt", 1));
    var events = CollectEvents(session);
    session.Submit(command);
    Assert(!session.TryReadCommand(out _) && events.OfType<GameCommandRejectedEvent>().Any(rejected =>
            rejected.Reason.Contains("megváltozott", StringComparison.OrdinalIgnoreCase)),
        "Az elavult inventory-revíziójú command átjutott.");
}

static void RemoteInventoryTransferCannotCrossCharacters()
{
    var (session, leader, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
    companion.AddToBackpack(new MiscItemDefinition("I-REMOTE", "Vendégtárgy", "Teszt", 1));
    var command = new InventoryTransferCommand(remote, 1, companion.Id, companion.InventoryRevision,
        InventorySlotKind.Backpack, 0, leader.Id, leader.InventoryRevision, InventorySlotKind.Backpack, 1);
    session.Submit(command);
    Assert(!session.TryReadCommand(out _), "A vendég másik karakterhez mozgathatott tárgyat.");
}

static void InventoryActionCommandsAreValidated()
{
    var (session, leader, _) = CreateSession();
    leader.AddToBackpack(new MiscItemDefinition("I-USE", "Gyógyital", "Teszt", 1, ConsumableEffect.Heal, 5));
    var revision = leader.InventoryRevision;
    session.Submit(new UseInventoryItemCommand(session.HostPlayerId, 1, leader.Id, revision, 0));
    Assert(session.TryReadCommand(out var use) && use is UseInventoryItemCommand,
        "Az érvényes használati commandot elutasította a session.");
    session.Submit(new DropInventoryItemCommand(session.HostPlayerId, 2, leader.Id, revision,
        InventorySlotKind.Backpack, 0));
    Assert(session.TryReadCommand(out var drop) && drop is DropInventoryItemCommand,
        "Az érvényes eldobási commandot elutasította a session.");
    session.Submit(new PickUpGroundItemCommand(session.HostPlayerId, 3, leader.Id, revision,
        WorldEntityId.New(), 1, 0, 1));
    Assert(session.TryReadCommand(out var pickup) && pickup is PickUpGroundItemCommand,
        "Az érvényes pickup command alakját elutasította a session.");
}

static void NonConsumableUseIsRejected()
{
    var (session, leader, _) = CreateSession();
    leader.AddToBackpack(new MiscItemDefinition("I-NOUSE", "Dísztárgy", "Teszt", 1));
    session.Submit(new UseInventoryItemCommand(session.HostPlayerId, 1, leader.Id,
        leader.InventoryRevision, 0));
    Assert(!session.TryReadCommand(out _), "A nem fogyasztható tárgy használati commandja átjutott.");
}

static void GroundPilePreservesChargesAndRevision()
{
    var character = CreateCharacter("PileTest");
    var wand = new MagicItemDefinition("MI-PILE", "Földi pálca", MagicItemKind.Wand, ItemRarity.Magic,
        10, 5, null, MagicItemEffect.None, 0, new HashSet<string> { character.CharacterClass.Id }, "Teszt", 1);
    var pile = new GroundItemPile(new Position(2, 2), wand, 3);
    Assert(pile.Revision == 1 && pile.Entries.Single().Charges == 3,
        "A földi kupac nem őrizte meg a kezdeti töltetet vagy revíziót.");
    Assert(!pile.TryTake(0, 2, out _), "A kupac elfogadta az elavult revíziót.");
    Assert(pile.TryTake(0, 1, out var entry) && entry.Charges == 3 && pile.Revision == 2,
        "A revíziózott pickup elvesztette a töltetet vagy nem növelte a kupacrevíziót.");
    var maze = new Maze(7, 7);
    maze.Carve(maze.Entrance);
    maze.DropItem(maze.Entrance, wand, 3);
    var fog = new FogOfWar(7, 7, 0);
    fog.RevealFrom(maze, maze.Entrance);
    var worldPile = WorldSnapshotProjector.Create(maze, fog).GroundPiles.Single();
    Assert(worldPile.Revision == 1 && worldPile.Items.Single().Charges == 3,
        "A world snapshot nem publikálta a kupac revízióját vagy töltetszámát.");
}

static void CatalogFingerprintIsDeterministic()
{
    var content = Encoding.UTF8.GetBytes("azonos katalógus\nR001;Ember");
    var first = CatalogFingerprint.Compute(content);
    var second = CatalogFingerprint.Compute(content);
    var changed = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("más katalógus"));
    Assert(first == second && first.Length == 64 && first != changed,
        "A katalógus SHA-256 fingerprint nem determinisztikus vagy nem érzékeli a változást.");
}

static void HandshakeValidatesProtocolAndCatalog()
{
    var (session, _, _) = CreateSession();
    var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
    var handshake = new SessionHandshakeService(session, "1.0.0", hash);
    var wrongProtocol = handshake.Handle(new ClientHello(SessionProtocol.Version + 1, "1.0.0", hash, "Vendég"));
    var wrongCatalog = handshake.Handle(new ClientHello(SessionProtocol.Version, "1.0.0",
        CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("other")), "Vendég"));
    var accepted = handshake.Handle(new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég"));
    Assert(!wrongProtocol.Accepted && !wrongCatalog.Accepted && accepted.Accepted &&
           accepted.PlayerId is not null && accepted.ReconnectToken?.Length == 64,
        "A handshake verzió-/katalógusellenőrzése vagy elfogadott válasza hibás.");
}

static void HandshakeReconnectRestoresPlayer()
{
    var (session, _, companion) = CreateSession();
    var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
    var handshake = new SessionHandshakeService(session, "1.0.0", hash);
    var first = handshake.Handle(new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég"));
    Assert(first is { Accepted: true, PlayerId: not null, ReconnectToken: not null },
        "Az első handshake sikertelen.");
    var playerId = first.PlayerId!.Value;
    var token = first.ReconnectToken!;
    Assert(session.TryAssignRemoteControl(playerId, companion.Id, out var error), error);
    session.MarkPlayerDisconnected(playerId);
    var reconnected = handshake.Handle(new ClientHello(SessionProtocol.Version, "1.0.0", hash,
        "Vendég", token));
    Assert(reconnected.Accepted && reconnected.PlayerId == playerId,
        "A reconnect-token nem az eredeti PlayerId-t állította vissza.");
}

static void ProtocolCodecRoundTripsCommand()
{
    var command = new BattleActionCommand(PlayerId.New(), 7, CharacterId.New(), BattleId.New(), 3,
        BattleActionKind.CastSpell, "SP-TEST", 1, new Position(4, 5));
    var restored = CoopProtocolJson.Decode(CoopProtocolJson.Encode(command));
    Assert(restored is BattleActionCommand decoded && decoded == command,
        "A JSON wire codec megváltoztatta a battle commandot.");
    var rejected = false;
    try
    {
        CoopProtocolJson.Decode("{\"Type\":\"command.unknown\",\"Payload\":{}}");
    }
    catch (JsonException)
    {
        rejected = true;
    }
    Assert(rejected, "A codec elfogadott egy nem allowlistelt üzenettípust.");
}

static void HostGatewayBindsAuthenticatedPlayer()
{
    var (session, _, companion) = CreateSession();
    var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
    var gateway = new CoopHostGateway(session, new SessionHandshakeService(session, "1.0.0", hash),
        new SessionReplicationPublisher());
    var helloMessages = gateway.HandleIncoming("connection-1", CoopProtocolJson.Encode(
        new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég")));
    var hello = (ServerHello)CoopProtocolJson.Decode(helloMessages.Single().WireMessage);
    Assert(hello is { Accepted: true, PlayerId: { } }, "A gateway handshake sikertelen.");
    var playerId = hello.PlayerId!.Value;

    var assignmentMessages = gateway.HandleIncoming("connection-1", CoopProtocolJson.Encode(
        new CharacterControlRequest(playerId, companion.Id)));
    var assignment = (CharacterControlResult)CoopProtocolJson.Decode(assignmentMessages.Single().WireMessage);
    Assert(assignment.Accepted && session.IsHumanControlled(companion.Id),
        "A gateway nem adta át a kiválasztott NPC vezérlését.");

    var impostor = new MoveCharacterCommand(PlayerId.New(), 1, companion.Id, Direction.Right);
    var rejectionMessages = gateway.HandleIncoming("connection-1", CoopProtocolJson.Encode(impostor));
    var rejection = (CoopProtocolError)CoopProtocolJson.Decode(rejectionMessages.Single().WireMessage);
    Assert(rejection.Code == "sender-mismatch" && !session.TryReadCommand(out _),
        "A gateway elfogadta a kapcsolattól eltérő PlayerId-jú commandot.");
}

static void HostGatewayRunsConnectionLifecycle()
{
    var (session, leader, companion) = CreateSession();
    var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
    var gateway = new CoopHostGateway(session, new SessionHandshakeService(session, "1.0.0", hash),
        new SessionReplicationPublisher());
    var helloMessage = gateway.HandleIncoming("connection-2", CoopProtocolJson.Encode(
        new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég"))).Single();
    var playerId = ((ServerHello)CoopProtocolJson.Decode(helloMessage.WireMessage)).PlayerId!.Value;
    gateway.HandleIncoming("connection-2", CoopProtocolJson.Encode(
        new CharacterControlRequest(playerId, companion.Id)));

    var move = new MoveCharacterCommand(playerId, 1, companion.Id, Direction.Left);
    Assert(gateway.HandleIncoming("connection-2", CoopProtocolJson.Encode(move)).Count == 0 &&
           session.TryReadCommand(out var accepted) && accepted == move,
        "A hitelesített gateway-command nem jutott el a session queue-ba.");

    var maze = new Maze(7, 7);
    maze.Carve(maze.Entrance);
    var fog = new FogOfWar(7, 7, 0);
    fog.RevealFrom(maze, maze.Entrance);
    var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "Gateway pálya",
        new Dictionary<CharacterId, Position>
        {
            [leader.Id] = maze.Entrance,
            [companion.Id] = new Position(3, 2)
        }, World: WorldSnapshotProjector.Create(maze, fog)));
    var replication = gateway.CreateReplicationMessages(snapshot);
    var frame = (SessionReplicationFrame)CoopProtocolJson.Decode(replication.Single().WireMessage);
    Assert(frame.Kind == SessionReplicationFrameKind.FullSnapshot && frame.RecipientPlayerId == playerId,
        "A gateway nem a csatlakozott játékosnak készítette a replikációs frame-et.");
    Assert(gateway.HandleIncoming("connection-2", CoopProtocolJson.Encode(
        new SnapshotAck(playerId, frame.Session.SnapshotSequence))).Count == 0,
        "A gateway nem fogadta el a snapshot ACK-ot.");

    gateway.Disconnect("connection-2");
    Assert(!session.IsHumanControlled(companion.Id) && gateway.CreateReplicationMessages(snapshot).Count == 0,
        "Disconnect után nem állt vissza az NPC-vezérlés vagy megmaradt a címzett kapcsolat.");
}

static async Task SignalRServerStartsAndStops()
{
    var (session, _, _) = CreateSession();
    var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
    var gateway = new CoopHostGateway(session, new SessionHandshakeService(session, "1.0.0", hash),
        new SessionReplicationPublisher());
    await using var server = await CoopSignalRServer.StartAsync(gateway, "http://127.0.0.1:0");
}

static async Task SignalRClientRunsLanProtocolFlow()
{
    var (session, leader, companion) = CreateSession();
    var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
    var publisher = new SessionReplicationPublisher();
    var gateway = new CoopHostGateway(session, new SessionHandshakeService(session, "1.0.0", hash), publisher);
    await using var server = await CoopSignalRServer.StartAsync(gateway, "http://127.0.0.1:0");
    var address = server.Addresses.Single();
    Assert(!address.EndsWith(":0", StringComparison.Ordinal),
        "A Kestrel nem publikálta a dinamikusan választott portot.");

    await using var client = new CoopSignalRClient(address, "1.0.0", hash, "LAN vendég");
    var receivedSnapshot = new TaskCompletionSource<SessionSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
    client.SnapshotChanged += snapshot => receivedSnapshot.TrySetResult(snapshot);
    var protocolErrors = new List<CoopProtocolError>();
    client.ProtocolErrorReceived += protocolErrors.Add;
    var hello = await client.ConnectAsync();
    Assert(hello is { Accepted: true, PlayerId: { } } && client.State == CoopClientConnectionState.Connected,
        "A valódi SignalR kliens handshake-je sikertelen.");

    var control = await client.RequestCharacterControlAsync(companion.Id);
    Assert(control.Accepted && session.IsHumanControlled(companion.Id),
        "A SignalR kliens nem tudta átvenni az NPC irányítását.");

    var maze = new Maze(7, 7);
    maze.Carve(maze.Entrance);
    var fog = new FogOfWar(7, 7, 0);
    fog.RevealFrom(maze, maze.Entrance);
    var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "SignalR pálya",
        new Dictionary<CharacterId, Position>
        {
            [leader.Id] = maze.Entrance,
            [companion.Id] = new Position(3, 2)
        }, World: WorldSnapshotProjector.Create(maze, fog)));
    await server.PublishSnapshotAsync(snapshot);
    var applied = await receivedSnapshot.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert(applied.SnapshotSequence == snapshot.SnapshotSequence && client.CurrentSnapshot?.World is not null,
        "A SignalR kliens nem alkalmazta a host teljes snapshotját.");

    var move = new MoveCharacterCommand(client.PlayerId!.Value, client.NextCommandId(), companion.Id,
        Direction.Right);
    await client.SendCommandAsync(move);
    Assert(session.TryReadCommand(out var accepted) && accepted == move,
        "A SignalR kliens commandja nem jutott el a host session queue-jáig.");
    Assert(protocolErrors.Count == 0, "A hibamentes SignalR folyamat közben protokollhiba érkezett.");
}

static async Task InMemoryTransportRunsProtocolFlow()
{
    var (session, leader, companion) = CreateSession();
    var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
    var handshake = new SessionHandshakeService(session, "1.0.0", hash);
    var (host, client) = InMemoryCoopTransport.CreatePair();

    await client.SendAsync(CoopProtocolJson.Encode(new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég")));
    var hello = (ClientHello)CoopProtocolJson.Decode(await host.ReceiveAsync());
    var serverHello = handshake.Handle(hello);
    await host.SendAsync(CoopProtocolJson.Encode(serverHello));
    var accepted = (ServerHello)CoopProtocolJson.Decode(await client.ReceiveAsync());
    Assert(accepted is { Accepted: true, PlayerId: { } }, "Az in-memory handshake sikertelen.");
    var remote = accepted.PlayerId!.Value;
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);

    var move = new MoveCharacterCommand(remote, 1, companion.Id, Direction.Right);
    await client.SendAsync(CoopProtocolJson.Encode(move));
    var decodedMove = (MoveCharacterCommand)CoopProtocolJson.Decode(await host.ReceiveAsync());
    session.Submit(decodedMove);
    Assert(session.TryReadCommand(out var acceptedCommand) && acceptedCommand == move,
        "Az in-memory transporton érkezett commandot nem fogadta el a session.");

    var maze = new Maze(7, 7);
    maze.Carve(maze.Entrance);
    var fog = new FogOfWar(7, 7, 0);
    fog.RevealFrom(maze, maze.Entrance);
    var positions = new Dictionary<CharacterId, Position>
    {
        [leader.Id] = maze.Entrance,
        [companion.Id] = new Position(3, 2)
    };
    var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "Wire pálya", positions,
        World: WorldSnapshotProjector.Create(maze, fog)));
    var publisher = new SessionReplicationPublisher();
    await host.SendAsync(CoopProtocolJson.Encode(publisher.CreateFrame(remote, snapshot)));
    var frame = (SessionReplicationFrame)CoopProtocolJson.Decode(await client.ReceiveAsync());
    Assert(frame.Kind == SessionReplicationFrameKind.FullSnapshot && frame.Session.World is not null,
        "Az első replikációs frame nem jutott át az in-memory transporton.");
    await client.SendAsync(CoopProtocolJson.Encode(new SnapshotAck(remote, frame.Session.SnapshotSequence)));
    var ack = (SnapshotAck)CoopProtocolJson.Decode(await host.ReceiveAsync());
    Assert(publisher.TryAcknowledge(ack.PlayerId, ack.SnapshotSequence, out var ackError), ackError);
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
