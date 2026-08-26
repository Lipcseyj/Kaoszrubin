using MazeGame;
using MazeGame.Application;
using MazeGame.Data;
using MazeGame.Domain.Characters;

var tests = new (string Name, Action Run)[]
{
    ("A host mozgási parancsa átmegy", HostMovementIsAccepted),
    ("A vendég átvehet egy NPC-t", RemotePlayerCanTakeNpcControl),
    ("A vendég nem adhat leader-parancsot", RemotePlayerCannotIssueLeaderAction),
    ("A duplikált parancs elutasításra kerül", DuplicateCommandIsRejected),
    ("Harc közben nem futhat felfedezési parancs", ExplorationCommandIsRejectedDuringBattle),
    ("A CharacterId mentés után is stabil", CharacterIdSurvivesSerialization),
    ("Disconnectkor AI veszi át, reconnectkor visszakapja", DisconnectAndReconnectRestoreControl)
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

static (GameSession Session, LiveCharacter Leader, LiveCharacter Companion) CreateSession()
{
    var party = new Party();
    var leader = CreateCharacter("Leader");
    var companion = CreateCharacter("Companion");
    party.SetLeader(leader);
    party.Add(companion);
    return (new GameSession(party, leader), leader, companion);
}

static LiveCharacter CreateCharacter(string name)
{
    var abilities = new PrimaryAbilities(5, 5, 5, 5);
    var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
    var characterClass = new CharacterClassDefinition("C001", "Harcos", PrimaryAbilities.Zero, false, 1.0);
    return new LiveCharacter(name, race, characterClass, abilities, 20, 0, 1, 0);
}

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
