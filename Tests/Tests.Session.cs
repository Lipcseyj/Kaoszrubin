internal static partial class Program
{
    static void BattleCommandGateIgnoresBufferedInput()
    {
        var gate = new BattleCommandGate();

        Assert(gate.TryBegin(41, 10), "Az első harci parancsot be kell engedni.");
        Assert(gate.IsPending, "A parancsnak a feldolgozásig függőben kell maradnia.");
        Assert(!gate.TryBegin(42, 10), "A gyorsan ismételt parancsot figyelmen kívül kell hagyni.");
        Assert(!gate.Complete(42), "Másik parancs visszajelzése nem oldhatja fel a reteszt.");
        Assert(!gate.CompleteAfterSnapshot(10), "A kiinduló snapshot nem igazolja a feldolgozást.");
        Assert(gate.CompleteAfterSnapshot(11), "Az új snapshotnak fel kell oldania a vendég reteszét.");
        Assert(gate.TryBegin(43), "A feldolgozás utáni új parancsot be kell engedni.");
        Assert(gate.Complete(43) && !gate.IsPending,
            "A megfelelő parancsvisszajelzésnek fel kell oldania a reteszt.");
    }

    static void HostMovementIsAccepted()
    {
        var (session, leader, _) = CreateSession();
        Assert(session.Submit(new MoveCharacterCommand(session.HostPlayerId, 1, leader.Id, Direction.Right)),
            "A parancs nem került a sorba.");
        Assert(session.TryReadCommand(out var command) && command is MoveCharacterCommand,
            "A host érvényes mozgási parancsa nem olvasható ki.");
    }

    static void CharacterColorCanBeChangedFromPalette()
    {
        var character = CreateCharacter("Színes");
        Assert(character.ChangeColor(ConsoleColor.Magenta) && character.Color == ConsoleColor.Magenta &&
               !character.ChangeColor(ConsoleColor.Black) && character.Color == ConsoleColor.Magenta,
            "A karakter elfogadott tiltott színt, vagy nem tartotta meg a kiválasztott színt.");
        Assert(CharacterColorPalette.Move(0, ConsoleKey.RightArrow) == 1 &&
               CharacterColorPalette.Move(0, ConsoleKey.LeftArrow) == CharacterColors.Selectable.Count - 1 &&
               CharacterColorPalette.Move(0, ConsoleKey.DownArrow) == CharacterColorPalette.Columns,
            "A 4 oszlopos színpaletta nyilas navigációja hibás.");

        var (session, _, companion) = CreateSession();
        var remote = session.RegisterRemotePlayer();
        Assert(session.TryAssignRemoteControl(remote, companion.Id, out var error), error);
        var valid = new ChangeCharacterColorCommand(remote, 1, companion.Id, ConsoleColor.Yellow);
        Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(valid)) is ChangeCharacterColorCommand
               { Color: ConsoleColor.Yellow },
            "A karakterszín-parancs nem éli túl a coop wire-körutat.");
        session.Submit(valid);
        Assert(session.TryReadCommand(out var accepted) && accepted == valid,
            "A vendég saját, választható karakterszínét elutasította a session.");
        session.Submit(new ChangeCharacterColorCommand(remote, 2, companion.Id, ConsoleColor.Black));
        Assert(!session.TryReadCommand(out _),
            "A session elfogadta a palettán nem szereplő karakterszínt.");
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

    static void RemotePlayerCanJoinOwnCharacter()
    {
        var leader = CreateCharacter("Host");
        var party = new Party();
        party.SetLeader(leader);
        var session = new GameSession(party, leader);
        var joined = CreateCharacter("Vendég");
        LiveCharacter? registered = null;
        var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
        var gateway = new CoopHostGateway(session, new SessionHandshakeService(session, "1.0.0", hash),
            new SessionReplicationPublisher(), _ => joined, character => registered = character);
        var helloMessages = gateway.HandleIncoming("guest", CoopProtocolJson.Encode(
            new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég")));
        var hello = (ServerHello)CoopProtocolJson.Decode(helloMessages.Single().WireMessage);
        var responseMessages = gateway.HandleIncoming("guest", CoopProtocolJson.Encode(
            new JoinCharacterRequest(hello.PlayerId!.Value, "character-data")));
        var response = (CharacterControlResult)CoopProtocolJson.Decode(responseMessages.Single().WireMessage);

        Assert(response.Accepted && response.CharacterId == joined.Id && registered == joined &&
               party.Members.Contains(joined) && session.IsHumanControlled(joined.Id),
            "A host nem vette fel és nem rendelte a távoli játékoshoz a kliens karakterét.");
    }

    static void RemotePlayerCanReclaimSavedCharacter()
    {
        var (session, _, companion) = CreateSession();
        var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
        var registered = false;
        var gateway = new CoopHostGateway(session, new SessionHandshakeService(session, "1.0.0", hash),
            new SessionReplicationPublisher(), _ => companion, _ => registered = true, companion.Id);
        var helloMessage = gateway.HandleIncoming("saved-guest", CoopProtocolJson.Encode(
            new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég"))).Single();
        var hello = (ServerHello)CoopProtocolJson.Decode(helloMessage.WireMessage);
        var resultMessage = gateway.HandleIncoming("saved-guest", CoopProtocolJson.Encode(
            new JoinCharacterRequest(hello.PlayerId!.Value, "saved-character"))).Single();
        var result = (CharacterControlResult)CoopProtocolJson.Decode(resultMessage.WireMessage);
        Assert(result.Accepted && result.CharacterId == companion.Id && session.IsHumanControlled(companion.Id) &&
               !registered, "A mentett vendégslot nem a host meglévő karakterpéldányához lett rendelve.");
        Assert(gateway.QueueCharacterState(companion.Id, "authoritative-character", CharacterSyncReason.CharacterDied),
            "A host nem tudta sorba állítani a vendég célzott halálállapotát.");
        var sync = (CharacterStateSync)CoopProtocolJson.Decode(gateway.DrainPendingMessages().Single().WireMessage);
        Assert(sync.PlayerId == hello.PlayerId && sync.CharacterId == companion.Id &&
               sync.CharacterData == "authoritative-character" && sync.Reason == CharacterSyncReason.CharacterDied,
            "A karakter-visszaszinkronizálás nem a megfelelő vendéghez került.");
    }

    static void RemotePlayerCanIssueCharacterAction()
    {
        var (session, _, companion) = CreateSession();
        var remote = session.RegisterRemotePlayer();
        Assert(session.TryAssignRemoteControl(remote, companion.Id, out var error), error);
        var command = new CharacterActionCommand(remote, 1, companion.Id, CharacterAction.OpenDoor,
            new Position(4, 3));
        session.Submit(command);
        Assert(session.TryReadCommand(out var accepted) && accepted == command,
            "A session elutasította a vendég saját karakterhez kötött ajtóakcióját.");
    }

    static void RemotePlayerCanCastExplorationSpell()
    {
        var (session, _, companion) = CreateSession();
        var remote = session.RegisterRemotePlayer();
        Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
        var command = new CastExplorationSpellCommand(remote, 1, companion.Id, "S-TEST", null,
            new Position(3, 2));
        session.Submit(command);
        Assert(session.TryReadCommand(out var accepted) && accepted == command,
            "A session nem fogadta el a vendég saját, térképi varázslási parancsát.");
    }

    static void RemotePlayerCanPurchaseAtInn()
    {
        var (session, _, companion) = CreateSession();
        var remote = session.RegisterRemotePlayer();
        Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
        session.SetPhase(GameSessionPhase.Inn);
        var command = new InnPurchaseCommand(remote, 1, companion.Id, 3, InnVendorKind.Market, 0);
        session.Submit(command);
        Assert(session.TryReadCommand(out var accepted) && accepted == command,
            "A session elutasította a vendég saját fogadói vásárlását.");
    }

    static void RemotePlayerCanSellAtInn()
    {
        var (session, _, companion) = CreateSession();
        var remote = session.RegisterRemotePlayer();
        Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
        companion.AddToBackpack(new MiscItemDefinition("I-SELL", "Eladó tárgy", "Teszt", 10));
        session.SetPhase(GameSessionPhase.Inn);
        var command = new InnSaleCommand(remote, 1, companion.Id, 3, companion.InventoryRevision, 0);
        Assert(session.Submit(command) && session.TryReadCommand(out var accepted) && accepted == command,
            "A session elutasította a vendég saját fogadói eladását.");
    }

    static void RemotePlayerCanAcknowledgeNarrative()
    {
        var (session, _, companion) = CreateSession();
        var remote = session.RegisterRemotePlayer();
        Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
        session.SetPhase(GameSessionPhase.Paused);
        var command = new AcknowledgeNarrativeCommand(remote, 1, companion.Id, Guid.NewGuid());
        session.Submit(command);
        Assert(session.TryReadCommand(out var accepted) && accepted == command,
            "A session elutasította a vendég történeti nyugtázását.");
    }

    static void RemotePlayerCanPrepareSpells()
    {
        var (session, _, companion) = CreateSession();
        var remote = session.RegisterRemotePlayer();
        Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
        session.SetPhase(GameSessionPhase.Paused);
        var command = new PrepareSpellsCommand(remote, 1, companion.Id, Guid.NewGuid(), ["S001", "S002"]);
        session.Submit(command);
        Assert(session.TryReadCommand(out var accepted) && accepted == command,
            "A session elutasította a vendég memorizálási választását.");
    }

    static void RemotePlayerCanResolveLevelUpPrompt()
    {
        var (session, _, companion) = CreateSession();
        var remote = session.RegisterRemotePlayer();
        Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
        session.SetPhase(GameSessionPhase.Paused);
        var command = new ResolveLevelUpPromptCommand(remote, 1, companion.Id, Guid.NewGuid(), "PERK-TEST");
        session.Submit(command);
        Assert(session.TryReadCommand(out var accepted) && accepted == command,
            "A session elutasította a vendég szintlépési választását.");
    }

    static void HostAndGuestUseSharedInputBindings()
    {
        Assert(GameInputBindings.IsCharacterSheetToggle(ConsoleKey.Tab), "A Tab nem vált karakterlapfókuszt.");
        Assert(GameInputBindings.InventoryAction(ConsoleKey.Enter) == InventoryInputAction.Use &&
               GameInputBindings.InventoryAction(ConsoleKey.D) == InventoryInputAction.Drop &&
               GameInputBindings.InventoryAction(ConsoleKey.Spacebar) == InventoryInputAction.MoveItem &&
               GameInputBindings.InventoryAction(ConsoleKey.F) == InventoryInputAction.SplitStack &&
               GameInputBindings.InventoryAction(ConsoleKey.S) == InventoryInputAction.DistributeStack &&
               GameInputBindings.InventoryAction(ConsoleKey.K) == InventoryInputAction.GiveFollowerStack &&
               GameInputBindings.InventoryAction(ConsoleKey.I) == InventoryInputAction.Inspect,
            "Az inventory közös billentyűkiosztása eltér a host vezérlésétől.");
        Assert(GameInputBindings.CharacterAction(ConsoleKey.N) == CharacterAction.OpenDoor &&
               GameInputBindings.CharacterAction(ConsoleKey.Z) == CharacterAction.CloseOrLockDoor &&
               GameInputBindings.CharacterAction(ConsoleKey.K) == CharacterAction.SearchCurrentPosition,
            "Az N/Z/K karakterakciók nincsenek a közös keymapben.");
        Assert(GameInputBindings.LeaderAction(ConsoleKey.P, false) == LeaderAction.Rest &&
               GameInputBindings.LeaderAction(ConsoleKey.G, false) == LeaderAction.ToggleRegrouping &&
               GameInputBindings.LeaderAction(ConsoleKey.H, false) == LeaderAction.ToggleHoldPosition &&
               GameInputBindings.LeaderAction(ConsoleKey.T, false) == LeaderAction.ToggleAttackMode &&
               GameInputBindings.LeaderAction(ConsoleKey.C, false) == LeaderAction.OrderNpcThiefToDisarmTrap &&
               GameInputBindings.LeaderAction(ConsoleKey.Enter, false) is null &&
               GameInputBindings.LeaderAction(ConsoleKey.Enter, true) == LeaderAction.ActivateExit,
            "A leader-only billentyűkiosztás hibás.");
        Assert(GameInputBindings.PreserveFormationFacing(ConsoleModifiers.Shift) &&
               GameInputBindings.PreserveFormationFacing(ConsoleModifiers.Shift | ConsoleModifiers.Alt) &&
               !GameInputBindings.PreserveFormationFacing(ConsoleModifiers.Control),
            "A Shift+nyíl alakzati oldalazás módosítója nem közös a host és a vendég között.");
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
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var roster = new CharacterRoster();
        var character = CreateCharacter("Persistent");
        roster.Add(character);
        roster.Select(character);
        var service = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-character-save.json"), data);
        var restored = service.Deserialize(service.Serialize(roster));
        Assert(restored.SelectedCharacter?.Id == character.Id, "A karakter stabil azonosítója megváltozott mentéskor.");
    }

    static void CharacterHistorySurvivesSerialization()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var character = CreateCharacter("Krónikás");
        character.SetNpcBehavior(NpcBehavior.Defensive);
        character.SetNpcJoinOrigin(4, "A Kormos Griff");
        character.RecordMonsterKill(data.Enemies[0].Id, 3);
        var service = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-history-save.json"), data);
        var restored = service.DeserializeCharacter(service.SerializeCharacter(character));
        Assert(restored.NpcJoinedMazeLevel == 4 && restored.NpcJoinedLocation == "A Kormos Griff" &&
               restored.MonsterKills.GetValueOrDefault(data.Enemies[0].Id) == 3,
            "A karakter történeti adatai nem élték túl a mentési körutat.");
    }

    static void LegacyGameSavesMigrateToCurrentVersion()
    {
        foreach (var version in new[] { 1, 2, 3, 18 })
        {
            var state = new GameSaveData { Version = version, MazeLevel = 6 };
            var migrated = GameSaveFormat.MigrateToCurrent(state);
            Assert(ReferenceEquals(state, migrated) && migrated.Version == GameSaveFormat.CurrentVersion &&
                   migrated.MazeLevel == 6,
                $"A(z) {version}. mentésverzió migrációja hibás vagy megváltoztatta a pályaszintet.");
        }

        var current = new GameSaveData
        {
            UsedAdHocConversationIds = ["ELIRA_RESCUE:ADHOC_1_START"],
            LastAdHocConversationUtc = new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero),
            AdHocConversationMazeLevel = 4,
            EliraInnCharacterIndex = 2,
            EliraInnVisitsRemaining = 3,
            Formation = new PartyFormationSnapshot(CharacterId.New(), null, null, null,
                Direction.Left, PartyFormationState.Locked, PartyFormationLayout.SingleFile)
        };
        var restored = JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(current));
        Assert(restored is
        {
            UsedAdHocConversationIds: ["ELIRA_RESCUE:ADHOC_1_START"],
            AdHocConversationMazeLevel: 4
        } &&
               restored.LastAdHocConversationUtc == current.LastAdHocConversationUtc &&
               restored.EliraInnCharacterIndex == 2 && restored.EliraInnVisitsRemaining == 3 &&
               restored.Formation is { State: PartyFormationState.Locked, Layout: PartyFormationLayout.SingleFile },
            "Az egyszer már elindított ad-hoc párbeszéd vagy a korlátozásai elvesztek mentéskor.");

        var old = GameSaveFormat.MigrateToCurrent(new GameSaveData { Version = 13 });
        Assert(old.Formation is { State: PartyFormationState.Disbanded },
            "A 13-as mentés nem kapott biztonságosan feloszlatott alap-alakzatot.");
    }

    static void SaveEditorOverwritesWithBackup()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"kaoszrubin-save-editor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
            var characterService = new CharacterSaveService(Path.Combine(directory, "characters.json"), data);
            var saveService = new GameSaveService(directory, characterService);
            var roster = new CharacterRoster();
            var leader = CreateCharacter("Szerkesztett");
            leader.SetGold(125);
            roster.Add(leader);
            roster.Select(leader);
            roster.Party.SetLeader(leader);
            var path = saveService.Save(new GameSaveData { MainCharacterName = leader.Name }, roster);
            var loaded = saveService.Load(path);
            loaded.Roster.SelectedCharacter!.SetGold(999);
            Assert(InventoryBundleGrantService.TryGrant([loaded.Roster.SelectedCharacter],
                    [new InventoryBundleEntry(data.GetItem("T001"), 9),
                 new InventoryBundleEntry(data.GetItem("T002"), 9)], out _),
                "A szerkesztő tesztellátmánya nem fért el.");
            var backup = saveService.Overwrite(loaded);
            var restored = saveService.Load(path);
            Assert(File.Exists(backup) && backup.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) &&
                   restored.State.Version == GameSaveFormat.CurrentVersion &&
                   restored.Roster.SelectedCharacter is { Gold: 999 } selected &&
                   CountBackpack(selected, "T001") == 9 && CountBackpack(selected, "T002") == 9,
                "A szerkesztett mentés, a két kilences köteg vagy a biztonsági másolat hibás.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        static int CountBackpack(LiveCharacter character, string itemId) =>
            Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount)
                .Where(index => string.Equals(character.Backpack[index]?.Id, itemId,
                    StringComparison.OrdinalIgnoreCase))
                .Sum(index => character.GetInventoryItemQuantity(InventorySlotKind.Backpack, index));
    }

    static void RemotePlayerCanAcknowledgeLevelImage()
    {
        var (session, _, companion) = CreateSession();
        var remote = session.RegisterRemotePlayer();
        Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
        session.SetPhase(GameSessionPhase.Paused);
        var command = new AcknowledgeLevelImageCommand(remote, 1, companion.Id, Guid.NewGuid());
        session.Submit(command);
        Assert(session.TryReadCommand(out var accepted) && accepted == command,
            "A session elutasította a vendég pályakép-nyugtázását.");
    }

    static void InvalidGameSaveVersionsAreRejected()
    {
        try
        {
            GameSaveFormat.MigrateToCurrent(new GameSaveData { Version = GameSaveFormat.CurrentVersion + 1 });
            throw new InvalidOperationException("A jövőbeli mentésverzió betöltődött.");
        }
        catch (InvalidOperationException exception)
        {
            Assert(exception.Message.Contains("Nem támogatott mentésverzió", StringComparison.Ordinal),
                $"A jövőbeli mentésverzió hibaüzenete pontatlan: {exception.Message}");
        }

        try
        {
            JsonSerializer.Deserialize<GameSaveData>("{}");
            throw new InvalidOperationException("A verzió nélküli mentés betöltődött.");
        }
        catch (JsonException)
        {
        }
    }

    static void ClassSpecializationSurvivesSerialization()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var race = data.GetRace("R003");
        var mageClass = data.CharacterClasses.Single(characterClass => characterClass.Id == CharacterClassIds.Mágus);
        var character = new LiveCharacter("Specialista", race, mageClass,
            new PrimaryAbilities(1, 3, 3, 10), 30, 60, 1, 1);
        Assert(character.ChooseSpecialization(ClassSpecializations.MageIllusionist),
            "A mágus nem tudta kiválasztani az Illuzionista specializációt.");
        var roster = new CharacterRoster();
        roster.Add(character);
        var service = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-specialization-save.json"), data);
        var restored = service.Deserialize(service.Serialize(roster)).Characters.Single();
        Assert(restored.SpecializationId == ClassSpecializations.MageIllusionist,
            "A specializáció elveszett a mentési kör után.");
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

#if false // A megszüntetett párbaj-állapotgép tesztjei; a harci lefedettség váltja fel őket.
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

static void PhysicalClassesChooseBattleTactic()
{
    var system = CreateBattleSystem(71);
    var fighter = CreateCharacter("Harcos", characterClassId: CharacterClassIds.Harcos);
    var fighterState = system.StartBattle(fighter, CreateEnemy(100, 1)).State;
    Assert(fighterState.RequiresTacticSelection, "A harcos nem kapott csatakezdő állásválasztást.");
    Assert(!fighterState.TryChooseTactic(BattleTactic.ThiefPoison), "A harcos elfogadott egy tolvaj taktikát.");
    Assert(fighterState.TryChooseTactic(BattleTactic.FighterDefensive) && !fighterState.RequiresTacticSelection,
        "A harcos érvényes állása nem oldotta fel a választást.");

    var thief = CreateCharacter("Tolvaj", characterClassId: CharacterClassIds.Tolvaj);
    var thiefState = system.StartBattle(thief, CreateEnemy(100, 1)).State;
    Assert(thiefState.RequiresTacticSelection && thiefState.TryChooseTactic(BattleTactic.ThiefObserve),
        "A tolvaj nem tudta kiválasztani a csatakezdő megközelítését.");
}

static void BarbarianRageTriggersAfterFiveDamage()
{
    var system = CreateBattleSystem(72);
    var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
    var barbarianClass = new CharacterClassDefinition(CharacterClassIds.Barbár, "Barbár",
        PrimaryAbilities.Zero, false, 1.0);
    var barbarian = new LiveCharacter("Barbár", race, barbarianClass,
        new PrimaryAbilities(5, 100, 5, 5), 500, 0, 1, 0);
    var state = system.StartBattle(barbarian, CreateEnemy(1000, 20)).State;
    for (var step = 0; step < 100 && !state.IsBarbarianRaging; step++)
        system.Advance(state);
    Assert(state.IsBarbarianRaging, "A barbár legalább 5 tényleges sebzés után sem került Dühbe.");
    var rageLogs = new List<string>();
    for (var step = 0; step < 6 && state.IsBarbarianRaging; step++)
        rageLogs.AddRange(system.Advance(state).Entries.SelectMany(entry =>
            entry.Details?.Calculation ?? [entry.Message]));
    Assert(rageLogs.Any(log => Enumerable.Range(5, 6).Any(bonus =>
            log.Contains($"🔥 Düh +{bonus}", StringComparison.Ordinal))),
        "A barbár Düh támadása nem kapott 5–10 közötti sebzésbónuszt.");
}

#endif

    static void ClassFeatureUpgradesPersistAndAppearOnSheet()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        foreach (var characterClass in data.CharacterClasses)
            Assert(ClassFeatureUpgrades.ForClass(characterClass.Id).Count == 3,
                $"{characterClass.Name} nem pontosan három osztályfejlesztést kapott.");

        var race = data.GetRace("R001");
        var fighterClass = data.CharacterClasses.Single(characterClass => characterClass.Id == CharacterClassIds.Harcos);
        var character = new LiveCharacter("Fejlesztett", race, fighterClass,
            new PrimaryAbilities(7, 8, 9, 10), 40, 0, 1, 0);
        Assert(character.ChooseClassFeatureUpgrade(ClassFeatureUpgrades.FighterPrecise) &&
               character.ChooseClassFeatureUpgrade(ClassFeatureUpgrades.FighterDefensive) &&
               !character.ChooseClassFeatureUpgrade(ClassFeatureUpgrades.FighterPowerful) &&
               !character.ChooseClassFeatureUpgrade(ClassFeatureUpgrades.BarbarianWildRage),
            "Az osztályfejlesztések darabszám- vagy osztálykorlátozása hibás.");

        var service = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-upgrade-save.json"), data);
        var restored = service.DeserializeCharacter(service.SerializeCharacter(character));
        Assert(restored.ClassFeatureUpgrades.Select(upgrade => upgrade.Id).SequenceEqual(
                new[] { ClassFeatureUpgrades.FighterPrecise, ClassFeatureUpgrades.FighterDefensive }),
            "Az osztályfejlesztések elvesztek a mentési kör után.");

        var lines = CharacterSheetPanel.Build(restored, data.ExperienceByLevel, 1, 0, 12);
        Assert(lines.Single(line => line.Row == 4).Text.Contains("💪7", StringComparison.Ordinal) &&
               lines.Single(line => line.Row == 4).Text.Contains("💖9", StringComparison.Ordinal) &&
               lines.Single(line => line.Row == 4).Text.Contains("🧠10", StringComparison.Ordinal) &&
               lines.Single(line => line.Row == 13).Text == "OSZTÁLYFEJLESZTÉSEK" &&
               lines.Single(line => line.Row == 14).Text.Contains("Kimért pontosság", StringComparison.Ordinal) &&
               lines.Single(line => line.Row == 15).Text.Contains("Áthatolhatatlan", StringComparison.Ordinal),
            "A tömör képességsor vagy az osztályfejlesztések karakterlap-blokkja hibás.");
    }

    static void AbilityIncreasesAreCappedAndPersisted()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var character = new LiveCharacter("Képességes", data.GetRace("R001"),
            data.CharacterClasses.Single(characterClass => characterClass.Id == CharacterClassIds.Harcos),
            new PrimaryAbilities(12, 13, 8, 9), data.GetMinimumVitality(8) + 1, 0, 1, 0);
        Assert(character.TryIncreaseAbility("STR") && character.Abilities.Strength == 13,
            "Az Erő képességpontja nem növelte 13-ra az értéket.");
        Assert(!character.TryIncreaseAbility("STR") && !character.TryIncreaseAbility("DEX") &&
               character.Abilities.Strength == 13 && character.Abilities.Dexterity == 13,
            "A képességpont túllépte a 13-as maximumot.");
        var oldVitalityBase = data.GetMinimumVitality(character.Abilities.Health);
        Assert(character.TryIncreaseAbility("HEA") && character.AbilityIncreasesClaimed == 2,
            "A képességpontok elköltött számlálója hibás.");
        character.ApplyAbilityResourceIncrease(data.GetMinimumVitality(character.Abilities.Health) - oldVitalityBase, 0);

        var service = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-ability-save.json"), data);
        var restored = service.DeserializeCharacter(service.SerializeCharacter(character));
        Assert(restored.Abilities == character.Abilities && restored.AbilityIncreasesClaimed == 2 &&
               restored.MaximumVitality == character.MaximumVitality,
            "A képességnövelések vagy az elköltött pontok száma elveszett a mentési kör után.");
    }

    static void WeaponProficienciesAreLimitedEffectiveAndPersisted()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        Assert(WeaponProficiencyProgression.MilestonesFor(CharacterClassIds.Harcos).SequenceEqual(new[] { 1, 7, 17, 27 }) &&
               WeaponProficiencyProgression.MilestonesFor(CharacterClassIds.Tolvaj).SequenceEqual(new[] { 7, 17 }),
            "A harci és nem harci osztályok fegyverjártassági mérföldkövei hibásak.");
        var fighter = CreateCharacter("Fegyvermester", characterClassId: CharacterClassIds.Harcos);
        var sword = data.GetWeapon("W004");
        Assert(WeaponFamilies.ForWeapon(sword) == WeaponFamilies.Sword &&
               WeaponFamilies.ForWeapon(data.GetWeapon("LW004")) == WeaponFamilies.Sword &&
               WeaponFamilies.ForWeapon(data.GetWeapon("W039")) == WeaponFamilies.Bow &&
               WeaponFamilies.ForWeapon(data.GetWeapon("W042")) == WeaponFamilies.Crossbow &&
               WeaponFamilies.All.Count == 9,
            "A normál vagy legendás fegyver családbesorolása hibás.");
        Assert(fighter.EquipWeapon(0, sword), "A tesztkarakter nem tudta felszerelni a hosszú kardot.");
        var system = CreateBattleSystem(2201);
        var enemy = CreateEnemy(100, 1, speed: 8);
        var before = system.EstimateCharacterHitChance(fighter, enemy, BattleTactic.FighterDefensive);
        Assert(fighter.TryAdvanceWeaponProficiency(WeaponFamilies.Sword), "A Kard Jártas fok nem volt választható.");
        var after = system.EstimateCharacterHitChance(fighter, enemy, BattleTactic.FighterDefensive);
        Assert(after == before + 5, "A Kard Jártas fok nem adott +1, azaz 5 százalékpont találati esélyt.");
        Assert(fighter.TryAdvanceWeaponProficiency(WeaponFamilies.Sword) &&
               fighter.TryAdvanceWeaponProficiency(WeaponFamilies.Shield) &&
               !fighter.TryAdvanceWeaponProficiency(WeaponFamilies.Dagger) &&
               fighter.TryAdvanceWeaponProficiency(WeaponFamilies.Shield) &&
               fighter.WeaponProficiencyAdvances == 4,
            "A két családos vagy kétfokozatú fegyverjártassági korlát hibás.");

        var service = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-proficiency-save.json"), data);
        var restored = service.DeserializeCharacter(service.SerializeCharacter(fighter));
        Assert(restored.WeaponProficiencyRankFor(WeaponFamilies.Sword) == WeaponProficiencyRank.Master &&
               restored.WeaponProficiencyRankFor(WeaponFamilies.Shield) == WeaponProficiencyRank.Master,
            "A fegyverjártasságok elvesztek a mentési kör után.");
        var lines = CharacterSheetPanel.Build(restored, data.ExperienceByLevel, 1, 0, 12);
        var resources = CharacterSheetPanel.BuildResourceLine(restored);
        Assert(lines.Single(line => line.Row == 10).Text.Contains("⚔️M", StringComparison.Ordinal) &&
               lines.Single(line => line.Row == 10).Text.Contains("🛡️M", StringComparison.Ordinal) &&
               resources.Vitality.Contains("❤️", StringComparison.Ordinal) &&
               string.IsNullOrEmpty(resources.Mana),
            "A fegyverjártasság- vagy az összevont erőforrássor hibás a karakterlapon.");
        var inspection = ItemInspectionFormatter.Format(sword, data, weaponProficiencies:
            restored.WeaponProficiencies.ToDictionary(proficiency => proficiency.FamilyId,
                proficiency => (int)proficiency.Rank));
        Assert(inspection.Text.Contains("Kard", StringComparison.Ordinal) &&
               inspection.Text.Contains("Mester", StringComparison.Ordinal),
            "A fegyver részletes nézete nem mutatja a családot és a jártassági fokot.");
    }

#if false // A megszüntetett párbaj-állapotgép tesztjei.
static void KnightProtectionTransfersThirdOfFirstHit()
{
    var unprotectedSystem = CreateBattleSystem(73);
    var protectedSystem = CreateBattleSystem(73);
    var unprotected = CreateCharacter("Védtelen", vitality: 500, characterClassId: CharacterClassIds.Pap);
    var protectedCharacter = CreateCharacter("Védett", vitality: 500, characterClassId: CharacterClassIds.Pap);
    var protector = CreateCharacter("Őrszem", vitality: 500, characterClassId: CharacterClassIds.Lovag);
    var unprotectedState = unprotectedSystem.StartBattle(unprotected, CreateEnemy(1000, 20)).State;
    var protectedState = protectedSystem.StartBattle(protectedCharacter, CreateEnemy(1000, 20)).State;
    protectedState.SetKnightProtection(protector);
    var protectedEntries = new List<BattleLogEntry>();
    for (var step = 0; step < 100 && unprotected.CurrentVitality == 500; step++)
    {
        unprotectedSystem.Advance(unprotectedState);
        protectedEntries.AddRange(protectedSystem.Advance(protectedState).Entries);
    }
    var fullDamage = 500 - unprotected.CurrentVitality;
    var protectedDamage = 500 - protectedCharacter.CurrentVitality;
    var protectorDamage = 500 - protector.CurrentVitality;
    Assert(fullDamage > 0 && protectedDamage == 0 && protectorDamage == (fullDamage + 2) / 3,
        $"A lovagi védelem hibásan osztotta el a sebzést: társ {protectedDamage}, lovag {protectorDamage}, eredeti {fullDamage}.");
    var protection = protectedEntries.FirstOrDefault(entry =>
        entry.Message.Contains("🛡️ Őrszem közbelépett", StringComparison.Ordinal));
    Assert(protection is not null &&
           !protection.Message.Contains("teljes", StringComparison.OrdinalIgnoreCase) &&
           protection.Details?.Calculation.Any(line =>
               line.Contains("teljes", StringComparison.OrdinalIgnoreCase) &&
               line.Contains("harmada", StringComparison.OrdinalIgnoreCase)) == true,
        "A lovagi közbelépés rövid naplója vagy részletes paneladata hibás.");
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

#endif

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

    static void BattlePromptIsIdempotent()
    {
        var (session, leader, _) = CreateSession();
        var events = CollectEvents(session);
        var battleId = BattleId.New();
        Assert(session.SetBattlePrompt(battleId, 3, leader.Id,
                   [BattleActionKind.PhysicalAttack, BattleActionKind.Pass]),
            "Az első harci prompt nem került beállításra.");
        Assert(!session.SetBattlePrompt(battleId, 3, leader.Id,
                   [BattleActionKind.Pass, BattleActionKind.PhysicalAttack]) &&
               events.OfType<BattlePromptEvent>().Count() == 1,
            "Az azonos tartalmú, eltérő sorrendű prompt ismét eseményt publikált.");
        Assert(session.SetBattlePrompt(battleId, 3, leader.Id,
                   [BattleActionKind.PhysicalAttack, BattleActionKind.Retreat]) &&
               events.OfType<BattlePromptEvent>().Count() == 2,
            "Az azonos kör tartalmilag megváltozott promptja nem publikálódott újra.");
    }

    static void RemoteBattlePromptRequiresCharacterOwner()
    {
        var (session, leader, companion) = CreateSession();
        var events = CollectEvents(session);
        var remote = session.RegisterRemotePlayer();
        Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
        var battleId = BattleId.New();
        session.SetBattlePrompt(battleId, 1, companion.Id, [BattleActionKind.PhysicalAttack]);
        session.Submit(new BattleActionCommand(session.HostPlayerId, 1, companion.Id, battleId, 1,
            BattleActionKind.PhysicalAttack));
        Assert(!session.TryReadCommand(out _), "A host feloldhatta a távoli karakter harci promptját.");
        session.Submit(new BattleActionCommand(remote, 1, companion.Id, battleId, 1,
            BattleActionKind.PhysicalAttack));
        Assert(session.TryReadCommand(out var accepted) && accepted.SenderId == remote,
            "A távoli karakter gazdájának érvényes harci akcióját elutasította a session.");
        session.RejectExecutedCommand(accepted, "Szemantikai próbahiba.");
        Assert(events.OfType<GameCommandRejectedEvent>().Any(rejected => rejected.RecipientPlayerId == remote &&
                rejected.CommandId == accepted.CommandId && rejected.Reason == "Szemantikai próbahiba."),
            "A végrehajtási réteg szemantikai elutasítása nem került vissza a parancs gazdájához.");

        companion.ReceiveDamage(companion.CurrentVitality);
        session.ReleaseCharacterControl(companion.Id);
        Assert(!session.IsHumanControlled(companion.Id) && session.GetAvailableRemoteCharacters()
                .All(option => option.CharacterId != companion.Id),
            "A halott távoli karakter vezérlése nem szűnt meg, vagy újra kiválasztható maradt.");
        Assert(session.TryReconnectPlayer(remote),
            "A karakterét elvesztő megfigyelő reconnect-tokenje nem maradt érvényes.");
        session.EndBattle(battleId);
    }

    static void EnemyTurnAdvanceCommandIsAccepted()
    {
        var (session, leader, _) = CreateSession();
        var battleId = BattleId.New();
        session.SetBattlePrompt(battleId, 4, leader.Id, [BattleActionKind.AdvanceEnemyTurn]);
        Assert(session.Submit(new BattleActionCommand(session.HostPlayerId, 1, leader.Id, battleId, 4,
                BattleActionKind.AdvanceEnemyTurn)),
            "Az ellenfél körét léptető Space-parancsot elutasította a session.");
        Assert(session.TryReadCommand(out var command) && command is BattleActionCommand
        { Action: BattleActionKind.AdvanceEnemyTurn },
            "Az ellenfél körét léptető parancs nem került a végrehajtási sorba.");
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

    static void BattleTacticCommandIsAccepted()
    {
        var (session, leader, _) = CreateSession();
        var battleId = BattleId.New();
        session.SetBattlePrompt(battleId, 1, leader.Id,
            [BattleActionKind.FighterPrecise, BattleActionKind.FighterPowerful, BattleActionKind.FighterDefensive]);
        Assert(session.Submit(new BattleActionCommand(session.HostPlayerId, 1, leader.Id, battleId, 1,
                BattleActionKind.FighterPowerful)),
            "A session elutasította az engedélyezett harcos taktikát.");
        Assert(session.TryReadCommand(out var command) && command is BattleActionCommand
        { Action: BattleActionKind.FighterPowerful }, "A taktikai parancs nem került a feldolgozási sorba.");
    }
    static void SessionSnapshotRoundTripsThroughJson()
    {
        var (session, leader, companion) = CreateSession();
        leader.SetGold(777);
        companion.SetGold(999);
        var positions = new Dictionary<CharacterId, Position>
        {
            [leader.Id] = new Position(2, 2),
            [companion.Id] = new Position(3, 2)
        };
        var snapshot = session.CreateSnapshot(new SessionSnapshotContext(4, "Tesztlabirintus", positions)) with
        {
            Activities = [new SessionActivitySnapshot(1, SessionActivityKind.Spell,
            "A host térképi varázslatot használt.", ConsoleColor.Magenta)],
            Sounds = [new SessionSoundSnapshot(1, SoundEffect.OffensiveSpell, [companion.Id])],
            SpellImpacts = [new SessionSpellImpactSnapshot(1, WorldId.New(), "S001",
                new Position(4, 3), [new Position(4, 3), new Position(5, 3)])],
            LevelImage = new LevelImageSnapshot(Guid.NewGuid(), "Tesztlabirintus", "teszt.png",
                [session.HostPlayerId]),
            InnDeparture = new InnDepartureSnapshot("A csapat elhagyja a fogadót."),
            AdHocConversation = new AdHocConversationSnapshot(Guid.NewGuid(), "Elira", "Elf", "Tolvaj",
                ["Elira: Emlékszem az erdőre."], "Hiányzik az otthonod?", ["Igen.", "Beszélj másról."]),
            Formation = PartyFormationRules.CreateDefault([leader.Id, companion.Id], leader.Id,
                Direction.Down, PartyFormationState.Locked),
            LeaderDecisionMessage = "Várunk a vezető döntéseire…",
            LeaderDecisionTitle = "Alakzatszerkesztő",
            OpenPlayerWindows = [new PlayerWindowStateSnapshot(session.HostPlayerId, leader.Id, leader.Name,
            PlayerWindowKind.QuestJournal, Guid.NewGuid())],
            SharedWindow = new ReplicatedWindowSnapshot(Guid.NewGuid(), 3, "Közös próba", 64,
                FramedWindow.Storyline.ToString(),
                [new ReplicatedWindowLineSnapshot("A host által rajzolt közös tartalom.", ConsoleColor.Cyan)],
                [session.HostPlayerId])
        };
        var json = JsonSerializer.Serialize(snapshot);
        var restored = JsonSerializer.Deserialize<SessionSnapshot>(json);
        Assert(restored is not null && restored.ProtocolVersion == SessionProtocol.Version &&
               restored.Phase == GameSessionPhase.Exploration && restored.Party.Count == 2 &&
               restored.Activities is [{ Kind: SessionActivityKind.Spell }] &&
               restored.LevelImage is { FileName: "teszt.png", AcknowledgedPlayerIds.Count: 1 } &&
               restored.InnDeparture is { Message: "A csapat elhagyja a fogadót." } &&
               restored.AdHocConversation is { CharacterName: "Elira", Choices.Count: 2 } &&
               restored.Formation is { Facing: Direction.Down, State: PartyFormationState.Locked } &&
               restored.LeaderDecisionMessage == "Várunk a vezető döntéseire…" &&
               restored.LeaderDecisionTitle == "Alakzatszerkesztő" &&
               restored.OpenPlayerWindows is [{ Kind: PlayerWindowKind.QuestJournal }] &&
               restored.SharedWindow is
               {
                   Revision: 3, Title: "Közös próba", Width: 64,
                   Frame: nameof(FramedWindow.Storyline), Lines: [{ Color: ConsoleColor.Cyan }],
                   AcknowledgedPlayerIds.Count: 1
               } &&
               restored.Sounds is [
               {
                   Sequence: 1, Effect: SoundEffect.OffensiveSpell,
                   ListenerCharacterIds: [{ } listener]
               }] && listener == companion.Id &&
               restored.SpellImpacts is [{ Sequence: 1, SpellId: "S001", Cells.Count: 2 }] &&
               restored.PartyGold == 777 && restored.Party.All(character => character.Gold == 777) &&
               restored.Sounds[0].IsAudibleTo(companion.Id) && !restored.Sounds[0].IsAudibleTo(leader.Id) &&
               restored.Party.Single(character => character.CharacterId == companion.Id).Position == new Position(3, 2),
            "A session snapshot JSON round-trip közben megváltozott.");
        var next = session.CreateSnapshot(new SessionSnapshotContext(4, "Tesztlabirintus", positions));
        Assert(next.SnapshotSequence == snapshot.SnapshotSequence + 1,
            "A publikált snapshot sorszáma nem monoton nő.");
    }

    static void RemotePlayerCanStepOntoTreasureChest()
    {
        var maze = new Maze(7, 7);
        var start = new Position(2, 3);
        var chestPosition = new Position(3, 3);
        maze.Carve(start);
        maze.Carve(chestPosition);
        var member = new PartyMemberAvatar(start, CreateCharacter("Vendég"));
        var chest = new TreasureChest(chestPosition, 25);
        maze.AddPartyMember(member);
        maze.AddTreasureChest(chest);

        Assert(!maze.TryMovePartyMember(member, chestPosition, maze.Entrance),
            "Az NPC partitárs önállóan felvehetne kincsesládát.");
        Assert(maze.TryMovePartyMember(member, chestPosition, maze.Entrance, allowTreasureChest: true) &&
               member.Position == chestPosition && maze.GetTreasureChestAt(chestPosition) == chest,
            "Az ember által vezérelt vendéget a láda mezője blokkolta.");

        var npcMaze = new Maze(7, 7);
        var npcStart = new Position(2, 3);
        var npcPosition = new Position(3, 3);
        npcMaze.Carve(npcStart);
        npcMaze.Carve(npcPosition);
        var guest = new PartyMemberAvatar(npcStart, CreateCharacter("Vendég"));
        npcMaze.AddPartyMember(guest);
        npcMaze.AddWorldNpc(new WorldNpc(npcPosition, "NPC-FRIENDLY-PASSABLE", CreateCharacter("Barátságos"),
            NpcDisposition.Friendly, false, false, "Utad engedem."));
        Assert(!npcMaze.TryMovePartyMember(guest, npcPosition, npcMaze.Entrance) &&
               npcMaze.TryMovePartyMember(guest, npcPosition, npcMaze.Entrance, allowWorldNpc: true),
            "Az ember által vezérelt vendég nem tud áthaladni a barátságos NPC avatárján.");

        Assert(CoopGuestScreen.LocalPlayerWindowStatusText(PlayerWindowKind.QuestJournal) ==
               "küldetésnapló megnyitva",
            "A vendég saját küldetésnapló-bannerének szövege hiányzó alanyra utal.");
    }

    static void GuestSeesOtherPlayersBlockingWindows()
    {
        var (session, leader, companion) = CreateSession();
        var remote = session.RegisterRemotePlayer();
        Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
        var maze = new Maze(7, 7);
        maze.Carve(maze.Entrance);
        var fog = new FogOfWar(7, 7, 0);
        fog.RevealFrom(maze, maze.Entrance);
        var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "Várakozási próba",
            new Dictionary<CharacterId, Position>
            {
                [leader.Id] = maze.Entrance,
                [companion.Id] = maze.Entrance
            }, World: WorldSnapshotProjector.Create(maze, fog))) with
        {
            Phase = GameSessionPhase.Paused,
            LevelUpPrompt = new LevelUpPromptSnapshot(Guid.NewGuid(), leader.Id, leader.Name,
                LevelUpPromptKind.PerkChoice, 1, 2, 5, 0, [], "Válassz tehetséget.")
        };
        var personalized = new SessionReplicationPublisher().CreateFrame(remote, snapshot).Session;
        Assert(personalized.LevelUpPrompt is { CharacterId: var levelUpCharacterId } &&
               levelUpCharacterId == leader.Id &&
               personalized.LeaderDecisionTitle == $"Szintlépés — {leader.Name}" &&
               personalized.LeaderDecisionMessage?.Contains(leader.Name, StringComparison.Ordinal) == true,
            "A más karakter szintlépési ablaka nem maradt látható read-only replikaként.");
    }

    static void PartyMemberCanBeRestoredOnEntranceOrExit()
    {
        var maze = new Maze(7, 7);
        maze.Carve(maze.Entrance);
        maze.Carve(maze.Exit);

        var entranceMember = new PartyMemberAvatar(maze.Entrance, CreateCharacter("Bejárati társ"));
        var exitMember = new PartyMemberAvatar(maze.Exit, CreateCharacter("Kijárati társ"));
        maze.AddPartyMember(entranceMember);
        maze.AddPartyMember(exitMember);

        Assert(maze.GetPartyMemberAt(maze.Entrance) == entranceMember &&
               maze.GetPartyMemberAt(maze.Exit) == exitMember,
            "A mentésből visszaállított partitársat a bejárat vagy a kijárat elutasította.");
    }

    static void SessionActivityCanTargetCharacter()
    {
        var first = new CharacterId(Guid.NewGuid());
        var second = new CharacterId(Guid.NewGuid());
        var targeted = new SessionActivitySnapshot(1, SessionActivityKind.System, "Keresési eredmény",
            ConsoleColor.Yellow, [first]);
        var shared = new SessionActivitySnapshot(2, SessionActivityKind.Battle, "Közös esemény", ConsoleColor.Red);
        Assert(targeted.IsVisibleTo(first) && !targeted.IsVisibleTo(second) &&
               shared.IsVisibleTo(first) && shared.IsVisibleTo(second),
            "A karakterhez címzett session-aktivitás láthatósága hibás.");
    }

    static void InnSnapshotCarriesSharedRumors()
    {
        var completionId = Guid.NewGuid();
        var snapshot = new InnSnapshot(3, 120, [],
            [new InnRumorSnapshot("Úti hír", ["Ugyanazt hallja a host és a vendég."], ConsoleColor.Yellow)],
            [new InnTransactionSnapshot(1, InnTransactionKind.Purchase, "Vendég", "Kard", 50, "Vendég")],
            [new InnSellPriceSnapshot("W-TEST", 25)],
            [new InnMenuOptionSnapshot(InnMenuOptionKind.Rest, "Pihenés", "Közös pihenés", LeaderOnly: true),
         new InnMenuOptionSnapshot(InnMenuOptionKind.Market, "Kereskedő", "Vétel és eladás", InnVendorKind.Market)],
            "A kovácsmester ma jelen van.", 2, 7,
            new LevelCompletionSnapshot(completionId, 2, 100,
                [new LevelCompletionCharacterSnapshot("Host", ConsoleColor.Green, 200, 1, 2, 12, 15, 0, 0, false)],
                [new LevelCompletionFallenSnapshot("Elesett", "Harcos")]), "A Törött Kard", 2);
        var restored = JsonSerializer.Deserialize<InnSnapshot>(JsonSerializer.Serialize(snapshot));
        Assert(restored is { Rumors.Count: 1 } && restored.Rumors[0].Title == "Úti hír" &&
               restored.Rumors[0].Lines.SequenceEqual(snapshot.Rumors[0].Lines) &&
               restored.Rumors[0].Color == ConsoleColor.Yellow && restored.Transactions is [{ ActorName: "Vendég" }] &&
               restored.SellPrices is [{ Price: 25 }] && restored.MenuOptions is [{ LeaderOnly: true }, ..] &&
               restored.MenuOptions[1].Vendor == InnVendorKind.Market && restored.PartyCount == 2 &&
               restored.PartyFreeBackpackSlots == 7 && restored.LevelCompletion?.CompletionId == completionId &&
               restored.LevelCompletion.FallenCharacters is [{ Name: "Elesett" }] &&
               restored.InnName == "A Törött Kard" && restored.MazeLevel == 2,
            "A fogadó közös menü- vagy pályavégi állapota nem maradt meg a snapshot JSON round-trip során.");
    }

    static void NeutralWorldNpcIsPassable()
    {
        static (Maze Maze, Position Start, Position NpcPosition) CreateMazeWithNeutralNpc()
        {
            var maze = new Maze(7, 7);
            var start = new Position(2, 3);
            var npcPosition = new Position(3, 3);
            maze.Carve(start);
            maze.Carve(npcPosition);
            maze.AddWorldNpc(new WorldNpc(npcPosition, "NPC-PASSABLE", CreateCharacter("Semleges"),
                NpcDisposition.Neutral, false, false, "Utad engedem."));
            return (maze, start, npcPosition);
        }

        var enemySetup = CreateMazeWithNeutralNpc();
        var enemy = CreateEnemyAt(enemySetup.Start, "PASSABLE-ENEMY");
        enemySetup.Maze.AddEnemy(enemy);
        Assert(enemySetup.Maze.TryMoveEnemy(enemy, enemySetup.NpcPosition) &&
               enemy.Position == enemySetup.NpcPosition,
            "A szörnyet blokkolta a semleges NPC.");

        var partySetup = CreateMazeWithNeutralNpc();
        var member = new PartyMemberAvatar(partySetup.Start, CreateCharacter("Mozgó NPC"));
        partySetup.Maze.AddPartyMember(member);
        Assert(partySetup.Maze.TryMovePartyMember(member, partySetup.NpcPosition, partySetup.Maze.Entrance) &&
               member.Position == partySetup.NpcPosition,
            "A mozgó partitársat blokkolta a semleges NPC.");
    }

    static void ReturnExpeditionPopulationIsLimited()
    {
        Assert(ReturnExpeditionRules.TargetNormalEnemyCount(10) == 3 &&
               ReturnExpeditionRules.TargetNormalEnemyCount(11) == 4 &&
               ReturnExpeditionRules.TargetNormalEnemyCount(1) == 1 &&
               ReturnExpeditionRules.AdditionalEnemiesNeeded(10, 1) == 2 &&
               ReturnExpeditionRules.AdditionalEnemiesNeeded(10, 4) == 0,
            "A visszatérő expedíció nem az eredeti normál szörnyállomány 30%-ára tölt vissza.");
    }

    static void RemotePlayerCanAcknowledgeRest()
    {
        var (session, _, companion) = CreateSession();
        var remote = session.RegisterRemotePlayer();
        Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
        session.SetPhase(GameSessionPhase.Paused);
        var command = new AcknowledgeRestCommand(remote, 1, companion.Id, Guid.NewGuid());
        session.Submit(command);
        Assert(session.TryReadCommand(out var accepted) && accepted == command,
            "A session elutasította a vendég pihenési nyugtázását.");
        Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(command)) is AcknowledgeRestCommand decoded &&
               decoded == command, "A pihenési nyugtázás nem írható körbe a hálózati protokollon.");
    }

    static void SideInventoryDoesNotPauseCoop()
    {
        Assert(!PlayerWindowKindRules.PausesGame(PlayerWindowKind.Inventory) &&
               PlayerWindowKindRules.PausesGame(PlayerWindowKind.Help) &&
               PlayerWindowKindRules.PausesGame(PlayerWindowKind.QuestJournal) &&
               PlayerWindowKindRules.PausesGame(PlayerWindowKind.CharacterDetails) &&
               PlayerWindowKindRules.PausesGame(PlayerWindowKind.SpellInfo),
            "A térkép melletti inventory vagy valamely térképtakaró személyes ablak szüneteltetése hibás.");
    }

    static void RemotePlayerCanAcknowledgeSharedWindow()
    {
        var (session, _, companion) = CreateSession();
        var remote = session.RegisterRemotePlayer();
        Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
        session.SetPhase(GameSessionPhase.Paused);
        var command = new AcknowledgeSharedWindowCommand(remote, 1, companion.Id, Guid.NewGuid(), 4);
        Assert(session.Submit(command) && session.TryReadCommand(out var accepted) && accepted == command,
            "A session elutasította a vendég közösablak-nyugtázását.");
        Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(command)) is
                   AcknowledgeSharedWindowCommand decoded && decoded == command,
            "A közösablak-nyugtázás nem írható körbe a hálózati protokollon.");
    }

    static void InnNamesAndRumorsLoadFromCsv()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        Assert(data.InnNames.Count == 26 && data.InnNames.Contains("A Törött Kard") &&
               data.InnNames.Contains("A Vándor Pihenője"),
            "A fogadónév-halmaz nem megfelelően töltődött be a CSV-ből.");
        Assert(data.InnRumors.Count == 50 &&
               data.InnRumors.Single(rumor => rumor.Id == "PL001").Name.Contains(
                   "Aki válaszol neki, azt többé nem látják.", StringComparison.Ordinal),
            "A hangulatpletykák vagy a szövegükben lévő vesszők nem megfelelően töltődtek be a CSV-ből.");
        Assert(data.Traps.Count == 8 && data.GetTrap("TR001").Effect == TrapEffect.Damage &&
               data.GetTrap("TR001").DetectionExperience == 25 &&
               data.GetTrap("TR001").DisarmExperience == 75 &&
               data.GetTrap("TR002").Effect == TrapEffect.Poison && data.GetTrap("TR003").Effect == TrapEffect.Alert &&
               data.GetTrap("TR007").MinimumLevel == 18 && data.GetTrap("TR007").DisarmDifficulty == 15 &&
               data.GetTrap("TR007").DetectionExperience == 200 &&
               data.GetTrap("TR007").DisarmExperience == 600 &&
               data.GetTrap("TR008").Effect == TrapEffect.Darkness &&
               data.GetItem(MiscItemIds.Torch) is { Effect: ConsumableEffect.Vision, EffectValue: 2 },
            "A csapdadefiníciók nem megfelelően töltődtek be a CSV-ből.");
    }
}
