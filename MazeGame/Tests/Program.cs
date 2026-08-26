using MazeGame;
using MazeGame.Application;
using MazeGame.Combat;
using MazeGame.Data;
using MazeGame.Domain.Characters;
using MazeGame.Domain.Combat;

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
    ("A Resolve támogatói győzelemnél nem kér fölösleges akciót", ResolveSkipsActionAfterSupportVictory)
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
