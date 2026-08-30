using KaoszRubin;
using KaoszRubin.Application;
using KaoszRubin.Combat;
using KaoszRubin.Data;
using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;
using KaoszRubin.Transport.SignalR;
using KaoszRubin.UI;
using System.Text.Json;
using System.Text;

var tests = new (string Name, Action Run)[]
{
    ("A host mozgási parancsa átmegy", HostMovementIsAccepted),
    ("A vendég átvehet egy NPC-t", RemotePlayerCanTakeNpcControl),
    ("A vendég saját karakterrel beléphet a host partijába", RemotePlayerCanJoinOwnCharacter),
    ("Az emberi vendég ráléphet a kincsesláda mezőjére", RemotePlayerCanStepOntoTreasureChest),
    ("A vendég visszaveheti a coop mentésben foglalt karakterét", RemotePlayerCanReclaimSavedCharacter),
    ("A vendég saját ajtó- és keresési akciót küldhet", RemotePlayerCanIssueCharacterAction),
    ("A vendég térképről varázslási parancsot küldhet", RemotePlayerCanCastExplorationSpell),
    ("A vendég saját karakterével fogadói vásárlást küldhet", RemotePlayerCanPurchaseAtInn),
    ("A vendég saját hátizsákjából fogadói eladást küldhet", RemotePlayerCanSellAtInn),
    ("A vendég nyugtázhatja a közös történeti ablakot", RemotePlayerCanAcknowledgeNarrative),
    ("A vendég nyugtázhatja a közös pályaképet", RemotePlayerCanAcknowledgeLevelImage),
    ("A vendég nyugtázhatja a közös pihenési összegzőt", RemotePlayerCanAcknowledgeRest),
    ("A vendég elküldheti a saját memorizált varázslatait", RemotePlayerCanPrepareSpells),
    ("A vendég válaszolhat a saját szintlépési promptjára", RemotePlayerCanResolveLevelUpPrompt),
    ("A vendég nem adhat leader-parancsot", RemotePlayerCannotIssueLeaderAction),
    ("A host és a vendég közös billentyűkiosztást használ", HostAndGuestUseSharedInputBindings),
    ("A faji tulajdonságokat az adatfájl tölti be", RaceTraitsAreLoadedFromData),
    ("A mágus első szintjén a Fényvarázslat a hatodik varázslat", SpellSchoolsIncludeMageLightSpell),
    ("A varázsmemória osztályonként eltérően fejlődik", SpellMemorizationCapacityUsesClassFormula),
    ("A kasztok CSV-ből módosítják a HP- és mannanövekedést", ClassResourceGrowthLoadsFromCsv),
    ("Az NPC-k és első küldetéseik CSV-ből töltődnek", NpcDefinitionsLoadFromCsv),
    ("A közös küldetésnapló elkülöníti az aktív és teljesített küldetéseket", QuestJournalBuildsSharedHistory),
    ("Az ismeretlen CSV-fejezet sorszámos hibát ad", UnknownCsvSectionIsRejectedWithLineNumber),
    ("A hiányzó kötelező CSV-mező sorszámos hibát ad", MissingRequiredCsvFieldIsRejectedWithLineNumber),
    ("Az alkalmazkodó ember választott képességbónuszt kap", AdaptableRaceGainsChosenAbility),
    ("A duplikált parancs elutasításra kerül", DuplicateCommandIsRejected),
    ("Harc közben nem futhat felfedezési parancs", ExplorationCommandIsRejectedDuringBattle),
    ("A CharacterId mentés után is stabil", CharacterIdSurvivesSerialization),
    ("A régi játékmentések az aktuális formátumra migrálódnak", LegacyGameSavesMigrateToCurrentVersion),
    ("Az ismeretlen és verzió nélküli játékmentések elutasításra kerülnek", InvalidGameSaveVersionsAreRejected),
    ("Az osztályspecializáció mentés után is megmarad", ClassSpecializationSurvivesSerialization),
    ("A 10. és 20. szintű osztályfejlesztések mentődnek és megjelennek", ClassFeatureUpgradesPersistAndAppearOnSheet),
    ("A képességpontok 13-nál megállnak és mentődnek", AbilityIncreasesAreCappedAndPersisted),
    ("A fegyverjártasság két családra korlátozott, hat és menthető", WeaponProficienciesAreLimitedEffectiveAndPersisted),
    ("Disconnectkor AI veszi át, reconnectkor visszakapja", DisconnectAndReconnectRestoreControl),
    ("A léptethető csata egy hívásra egy akciót futtat", BattleAdvanceRunsOneAction),
    ("A harcos és a tolvaj csatakezdő taktikát választ", PhysicalClassesChooseBattleTactic),
    ("A harcos taktikai találati esélyei a valódi képletet követik", FighterTacticHitChancesUseCombatFormula),
    ("Az ellenséges kezdeményezés az első saját körig késlelteti a taktikát", EnemyInitiativeDelaysTacticPrompt),
    ("A kezdeményezési napló előjeles 1d2 dobást mutat", InitiativeLogShowsSignedDie),
    ("Az éhség és szomjúság hatásai láthatók a harci naplóban", NeedStatusEffectsAreVisible),
    ("A harci találat kiemeli a sebzést és a megmaradt HP-t", BattleHitHighlightsDamageAndHealth),
    ("A győzelmi üzenet nem ismétli meg az utolsó támadást", VictoryMessageIsConcise),
    ("A győzelmi összegzés egyetlen kompakt sor", VictorySummaryIsCompact),
    ("A barbár öt sebzés után Dühbe gurul", BarbarianRageTriggersAfterFiveDamage),
    ("A lovagi közbelépés kivédi a társ találatát és harmadolva átveszi", KnightProtectionTransfersThirdOfFirstHit),
    ("A csata megvárhatja a játékos hálózati akcióját", BattleCanWaitForPlayerAction),
    ("A támogatás a fő akció előtt lezárhatja a csatát", SupportCanFinishBattleBeforePlayerAction),
    ("A régi Resolve API az állapotgépet hajtja", ResolveUsesStateMachineAdapter),
    ("A Resolve támogatói győzelemnél nem kér fölösleges akciót", ResolveSkipsActionAfterSupportVictory),
    ("Csak az aktív BattleId és TurnId parancsa fogadható el", BattleCommandRequiresCurrentPrompt),
    ("Az ellenfél köre külön Space-paranccsal léptethető", EnemyTurnAdvanceCommandIsAccepted),
    ("A távoli harci promptot csak a karakter gazdája oldhatja fel", RemoteBattlePromptRequiresCharacterOwner),
    ("A varázslat command csak szemantikus választást hordoz", SpellBattleCommandIsAccepted),
    ("Hiányos varázslat command nem juthat át", MalformedSpellBattleCommandIsRejected),
    ("A promptban nem engedélyezett harci akció elutasításra kerül", DisallowedBattleActionIsRejected),
    ("A csatakezdő taktikai parancs átjut a session-validáción", BattleTacticCommandIsAccepted),
    ("A session snapshot JSON-on körbeírható", SessionSnapshotRoundTripsThroughJson),
    ("A session-aktivitás karakterhez címezhető", SessionActivityCanTargetCharacter),
    ("A fogadó snapshotja közös pletykákat továbbít", InnSnapshotCarriesSharedRumors),
    ("A fogadónevek és hangulatpletykák CSV-ből töltődnek", InnNamesAndRumorsLoadFromCsv),
    ("A snapshot csak az aktív harci promptot fogadja el", SnapshotRequiresCurrentBattlePrompt),
    ("A world snapshot nem szivárogtat rejtett entitást", WorldSnapshotOnlyContainsRevealedState),
    ("A rejtett csapda nem szivárog ki, a felfedezett pedig replikálódik", TrapVisibilityFollowsDiscoveryState),
    ("A csapdakészlet és darabszám a labirintusszinttel nehezedik", TrapConfigurationScalesByMazeLevel),
    ("A karakter kasztja, faja és átmeneti hatásai módosítják a látótávot", CharacterVisionRangeUsesClassRaceAndEffects),
    ("A szörnyek látótávja CSV-ből érkezik", EnemyVisionRangesLoadFromCsv),
    ("A felfedés változó látótávot és látóvonalat használ", FogRevealUsesVariableRangeAndLineOfSight),
    ("Az üldözési memória három elvesztett látási lépésig tart", PursuitMemoryLastsThreeMoves),
    ("A pályanevekből szabályos képfájlnév készül", LevelImageFileNamesAreNormalized),
    ("Az ellenség a legközelebbi látható csapattagot célozza", EnemyTargetsNearestVisiblePartyMember),
    ("A mozgó world entity azonosítója stabil", WorldEntityIdSurvivesMovement),
    ("A world delta minden lényeges változást leír", WorldDeltaCapturesChanges),
    ("Eltérő pályák között nem készülhet delta", WorldDeltaRejectsDifferentWorld),
    ("A publisher teljes snapshot után ACK-alapú deltát küld", ReplicationPublisherUsesAcknowledgedBaseline),
    ("Ismeretlen ACK teljes resyncet kényszerít", UnknownReplicationAckForcesResync),
    ("Pályaváltáskor a publisher teljes snapshotra vált", ReplicationPublisherUsesFullSnapshotForNewWorld),
    ("A kliens store teljes snapshotot és régi baseline-ról érkező deltát alkalmaz", ClientStoreAppliesReplicationFrames),
    ("Hiányzó delta-baseline esetén a kliens resyncet kér", ClientStoreRequestsResyncForMissingBaseline),
    ("Az inventory snapshot explicit slotokat és revíziót tartalmaz", InventorySnapshotHasSlotsAndRevision),
    ("A hátizsák 12 helyes és kilences kötegeket képez", BackpackStacksIdenticalItemsUpToNine),
    ("A host és a vendég ugyanazt a karakterlap-layoutot használja", CharacterSheetLayoutIsShared),
    ("A karakterlap külön színezi az alacsony HP-t és a mannát", CharacterSheetColorsHealthAndManaSeparately),
    ("A host és a vendég közös varázslat-UI modelleket használ", SpellUiModelsAreShared),
    ("A host és a vendég közös pihenési összegzőt használ", RestSummaryUiIsShared),
    ("A vendég tárgyvizsgálata nem vágja le a sebzésértéket", GuestItemInspectionKeepsDamageValue),
    ("A boss-ablak és a harci promptok közös UI-modellt használnak", BossAndBattlePromptsAreShared),
    ("A kompakt party státusz HP-t és manát százalékosan mutat", CompactPartyStatusShowsResources),
    ("Az ablakkeret-katalógus méretezhető és konfigurálható", WindowFrameCatalogIsResizableAndConfigured),
    ("A vendég snapshot kasztbetűt és karakterszínt őriz", GuestAvatarUsesClassGlyphAndCharacterColor),
    ("A vendég nem rajzol újra puszta snapshot-sorszám változásra", GuestRedrawIgnoresReplicationSequences),
    ("A vendég a teljes party inventory read modeljét megkapja", ReplicationPublisherSharesPartyInventories),
    ("Az inventory transfer atomi és megőrzi a töltetet", InventoryTransferIsAtomicAndPreservesCharges),
    ("A hátizsákköteg felezése atomi és tele hátizsáknál figyelmeztet", InventoryStackSplitIsAtomicAndRequiresSpace),
    ("Az elfogyasztható köteg egyenletesen és veszteségmentesen oszlik szét", ConsumableStackDistributesEvenly),
    ("A képességnövelő varázstárgyak minden kasztnál 13-ig hatnak", AbilityMagicItemsAreUniversalAndCapped),
    ("Az elavult inventory-revízió elutasításra kerül", StaleInventoryRevisionIsRejected),
    ("A vendég hátizsákok között mozgathat, felszerelést nem", RemoteInventoryTransferCanCrossBackpacksOnly),
    ("A használat, eldobás és pickup command alakja validált", InventoryActionCommandsAreValidated),
    ("Nem fogyasztható tárgy használata elutasításra kerül", NonConsumableUseIsRejected),
    ("A földi loot megőrzi a töltetet és revíziózott", GroundPilePreservesChargesAndRevision),
    ("A katalógus fingerprint determinisztikus", CatalogFingerprintIsDeterministic),
    ("A handshake verziót és katalógushasht ellenőriz", HandshakeValidatesProtocolAndCatalog),
    ("A reconnect-token ugyanazt a PlayerId-t állítja vissza", HandshakeReconnectRestoresPlayer),
    ("A JSON wire codec allowlistelt commandot ír körbe", ProtocolCodecRoundTripsCommand),
    ("A host gateway kapcsolathoz köti a PlayerId-t", HostGatewayBindsAuthenticatedPlayer),
    ("A host gateway kezeli a control-, replikáció- és disconnect-folyamot", HostGatewayRunsConnectionLifecycle),
    ("A hálózati lifecycle és a szimulációs esemény nem deadlockol", GatewayAndSimulationDoNotDeadlock),
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
           GameInputBindings.LeaderAction(ConsoleKey.Enter, false) is null &&
           GameInputBindings.LeaderAction(ConsoleKey.Enter, true) == LeaderAction.ActivateExit,
        "A leader-only billentyűkiosztás hibás.");
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

static void LegacyGameSavesMigrateToCurrentVersion()
{
    foreach (var version in new[] { 1, 2, 3 })
    {
        var state = new GameSaveData { Version = version, MazeLevel = 6 };
        var migrated = GameSaveFormat.MigrateToCurrent(state);
        Assert(ReferenceEquals(state, migrated) && migrated.Version == GameSaveFormat.CurrentVersion &&
               migrated.MazeLevel == 6,
            $"A(z) {version}. mentésverzió migrációja hibás vagy megváltoztatta a pályaszintet.");
    }
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
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, "adatok.csv"));
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
        rageLogs.AddRange(system.Advance(state).Entries.Select(entry => entry.Message));
    Assert(rageLogs.Any(log => Enumerable.Range(5, 6).Any(bonus =>
            log.Contains($"🔥 Düh +{bonus}", StringComparison.Ordinal))),
        "A barbár Düh támadása nem kapott 5–10 közötti sebzésbónuszt.");
}

static void ClassFeatureUpgradesPersistAndAppearOnSheet()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, "adatok.csv"));
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
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, "adatok.csv"));
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
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, "adatok.csv"));
    Assert(WeaponProficiencyProgression.MilestonesFor(CharacterClassIds.Harcos).SequenceEqual(new[] { 1, 7, 17, 27 }) &&
           WeaponProficiencyProgression.MilestonesFor(CharacterClassIds.Tolvaj).SequenceEqual(new[] { 7, 17 }),
        "A harci és nem harci osztályok fegyverjártassági mérföldkövei hibásak.");
    var fighter = CreateCharacter("Fegyvermester", characterClassId: CharacterClassIds.Harcos);
    var sword = data.GetWeapon("W004");
    Assert(WeaponFamilies.ForWeapon(sword) == WeaponFamilies.Sword &&
           WeaponFamilies.ForWeapon(data.GetWeapon("LW004")) == WeaponFamilies.Sword &&
           WeaponFamilies.All.Count == 6,
        "A normál vagy legendás fegyver családbesorolása hibás.");
    Assert(fighter.EquipWeapon(0, sword), "A tesztkarakter nem tudta felszerelni a hosszú kardot.");
    var system = CreateBattleSystem(2201);
    var enemy = CreateEnemy(100, 1, speed: 8);
    var before = system.EstimatePlayerHitChance(fighter, enemy, BattleTactic.FighterDefensive);
    Assert(fighter.TryAdvanceWeaponProficiency(WeaponFamilies.Sword), "A Kard Jártas fok nem volt választható.");
    var after = system.EstimatePlayerHitChance(fighter, enemy, BattleTactic.FighterDefensive);
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
    Assert(lines.Single(line => line.Row == 10).Text.Contains("⚔️M", StringComparison.Ordinal) &&
           lines.Single(line => line.Row == 10).Text.Contains("🛡️M", StringComparison.Ordinal) &&
           lines.Single(line => line.Row == 5).Text.Contains("❤️", StringComparison.Ordinal) &&
           !lines.Single(line => line.Row == 5).Text.Contains("🔷", StringComparison.Ordinal),
        "A fegyverjártasság- vagy az összevont erőforrássor hibás a karakterlapon.");
    var inspection = ItemInspectionFormatter.Format(sword, data, weaponProficiencies:
        restored.WeaponProficiencies.ToDictionary(proficiency => proficiency.FamilyId,
            proficiency => (int)proficiency.Rank));
    Assert(inspection.Text.Contains("Kard", StringComparison.Ordinal) &&
           inspection.Text.Contains("Mester", StringComparison.Ordinal),
        "A fegyver részletes nézete nem mutatja a családot és a jártassági fokot.");
}

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
    Assert(protectedEntries.Any(entry => entry.Message.Contains("Őrszem közbelépett", StringComparison.Ordinal)),
        "A lovagi közbelépés nem került a harci eseménynaplóba.");
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
    Assert(events.OfType<GameCommandRejectedEvent>().Any(rejected => rejected.PlayerId == remote &&
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
        LevelImage = new LevelImageSnapshot(Guid.NewGuid(), "Tesztlabirintus", "teszt.png",
            [session.HostPlayerId]),
        InnDeparture = new InnDepartureSnapshot("A csapat elhagyja a fogadót.")
    };
    var json = JsonSerializer.Serialize(snapshot);
    var restored = JsonSerializer.Deserialize<SessionSnapshot>(json);
    Assert(restored is not null && restored.ProtocolVersion == SessionProtocol.Version &&
           restored.Phase == GameSessionPhase.Exploration && restored.Party.Count == 2 &&
           restored.Activities is [{ Kind: SessionActivityKind.Spell }] &&
           restored.LevelImage is { FileName: "teszt.png", AcknowledgedPlayerIds.Count: 1 } &&
           restored.InnDeparture is { Message: "A csapat elhagyja a fogadót." } &&
           restored.Sounds is [{ Sequence: 1, Effect: SoundEffect.OffensiveSpell,
               ListenerCharacterIds: [{ } listener] }] && listener == companion.Id &&
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

static void InnNamesAndRumorsLoadFromCsv()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, "adatok.csv"));
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

static void TrapConfigurationScalesByMazeLevel()
{
    var first = MazeLevelConfigurations.Get(1);
    var middle = MazeLevelConfigurations.Get(10);
    var final = MazeLevelConfigurations.Get(MazeLevelConfigurations.FinalLevel);
    Assert(first.TrapCount == new IntRange(2, 5) && first.TrapIds.SequenceEqual(["TR001"]),
        "Az első szint csapdakonfigurációja nem kezdőbarát.");
    Assert(middle.TrapCount == new IntRange(3, 6) && middle.TrapIds.Contains("TR005") &&
           middle.TrapIds.Contains("TR008") && !middle.TrapIds.Contains("TR006"),
        "A középső szintek csapdakonfigurációja nem megfelelően nehezedik.");
    Assert(final.TrapCount == new IntRange(4, 8) && final.TrapIds.Contains("TR007") &&
           !final.TrapIds.Contains("TR001"),
        "A végső szintek nem a legnehezebb csapdakészletet használják.");
    Assert(first.VisionModifier == 0 && MazeLevelConfigurations.Get(5).VisionModifier == -1 &&
           MazeLevelConfigurations.Get(9).VisionModifier == -2,
        "Az extra sötét pályák látótávmódosítója hibás.");
}

static void LevelImageFileNamesAreNormalized()
{
    Assert(ImageViewer.FileNameForLevel("Patkányjáratok") == "patkanyjaratok.png" &&
           ImageViewer.FileNameForLevel("A holtak katakombái") == "aholtakkatakombai.png" &&
           ImageViewer.FileNameForLevel("A démoni sík: Vértrónus") == "ademonisikvertronus.png" &&
           ImageViewer.FileNameForLevel("A Káoszrubin rejtekhelye") == "akaoszrubinrejtekhelye.png",
        "A pályakép fájlneve nem kisbetűs, egybeírt és ékezetmentes.");
}

static void TrapVisibilityFollowsDiscoveryState()
{
    var maze = new Maze(7, 7);
    var position = new Position(3, 2);
    maze.Carve(position);
    var definition = new TrapDefinition("TR-TEST", "Tesztcsapda", new Rune('⌄'), TrapEffect.Damage,
        1, 7, 7, 3, 7, 0, 25, 75, "Teszt.");
    var trap = new MazeTrap(position, definition);
    maze.AddTrap(trap);
    var fog = new FogOfWar(maze.Width, maze.Height, 2);
    fog.RevealFrom(maze, maze.Entrance);

    var hidden = WorldSnapshotProjector.Create(maze, fog).RevealedCells.Single(cell => cell.Position == position);
    Assert(hidden.TileCodePoint == Maze.Floor.Value,
        "A rejtett csapda kiszivárgott a coop world snapshotba.");
    trap.Detect();
    var detected = WorldSnapshotProjector.Create(maze, fog).RevealedCells.Single(cell => cell.Position == position);
    Assert(detected.TileCodePoint == definition.Symbol.Value && detected.ForegroundColor == ConsoleColor.Yellow,
        "A felfedezett csapda nem jelent meg a coop world snapshotban.");
    trap.Disarm();
    var disarmed = WorldSnapshotProjector.Create(maze, fog).RevealedCells.Single(cell => cell.Position == position);
    Assert(disarmed.TileCodePoint == new Rune('·').Value && disarmed.ForegroundColor == ConsoleColor.DarkGray,
        "A hatástalanított csapda állapota nem replikálódott.");
}

static void EnemyTargetsNearestVisiblePartyMember()
{
    var host = CreateCharacter("Host");
    var guest = CreateCharacter("Vendég");
    var npc = CreateCharacter("NPC");
    var candidates = new[]
    {
        (host, new Position(8, 8)),
        (guest, new Position(3, 2)),
        (npc, new Position(4, 2))
    };
    var target = EnemyTargeting.ChooseNearestVisible(new Position(2, 2), candidates,
        position => position != new Position(3, 2), new Random(1));
    Assert(target?.Character == npc,
        "Az ellenség nem a legközelebbi látható NPC-/vendégpozíciót választotta a host helyett.");

    target = EnemyTargeting.ChooseNearestVisible(new Position(2, 2), candidates,
        _ => true, new Random(1));
    Assert(target?.Character == guest,
        "Az ellenség figyelmen kívül hagyta a hostnál közelebbi vendégkaraktert.");
}

static void FighterTacticHitChancesUseCombatFormula()
{
    var system = CreateBattleSystem(1701);
    var fighter = CreateCharacter("Harcos", characterClassId: CharacterClassIds.Harcos);
    var enemy = CreateEnemy(100, 1, speed: 8);
    var precise = system.EstimatePlayerHitChance(fighter, enemy, BattleTactic.FighterPrecise);
    var powerful = system.EstimatePlayerHitChance(fighter, enemy, BattleTactic.FighterPowerful);
    var defensive = system.EstimatePlayerHitChance(fighter, enemy, BattleTactic.FighterDefensive);
    Assert(precise == defensive + 10 && defensive == powerful + 5,
        $"A taktikai módosítók nem +2/0/-1 arányban változtatják az esélyt: {precise}/{defensive}/{powerful}%.");

    var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
    var fighterClass = new CharacterClassDefinition(CharacterClassIds.Harcos, "Harcos", PrimaryAbilities.Zero,
        false, 1.0);
    var nearlyCertain = new LiveCharacter("Biztos", race, fighterClass,
        new PrimaryAbilities(5, 100, 5, 5), 20, 0, 1, 0);
    var nearlyImpossible = new LiveCharacter("Esélytelen", race, fighterClass,
        new PrimaryAbilities(5, -100, 5, 5), 20, 0, 1, 0);
    Assert(system.EstimatePlayerHitChance(nearlyCertain, enemy, BattleTactic.FighterPrecise) == 95,
        "A természetes 1 nem korlátozza 95%-ra a találati esélyt.");
    Assert(system.EstimatePlayerHitChance(nearlyImpossible, enemy, BattleTactic.FighterPowerful) == 5,
        "A természetes 20 nem biztosít legalább 5% találati esélyt.");
}

static void EnemyInitiativeDelaysTacticPrompt()
{
    var system = CreateBattleSystem(710);
    var fighter = CreateCharacter("Harcos", vitality: 100, characterClassId: CharacterClassIds.Harcos);
    var state = system.StartBattle(fighter, CreateEnemy(100, 1, speed: 100)).State;
    Assert(state.IsOpeningEnemyTurn && state.RequiresTacticSelection && !state.IsAwaitingTacticSelection,
        "Az ellenséges nyitókör előtt a rendszer már taktikai inputot várt.");
    system.Advance(state);
    Assert(state.IsPlayerTurn && state.IsAwaitingTacticSelection,
        "Az első ellenséges támadás után nem jelent meg az első saját kör taktikai választása.");
}

static void InitiativeLogShowsSignedDie()
{
    var started = CreateBattleSystem(711).StartBattle(CreateCharacter("Kezdeményező"),
        CreateEnemy(100, 1));
    var message = started.Entries.Single(entry => entry.Message.StartsWith("Kezdeményezés:",
        StringComparison.Ordinal)).Message;
    Assert(message.Split("±1d2(", StringSplitOptions.None).Length == 3 &&
           !message.Contains(" +1d2(", StringComparison.Ordinal),
        "A kezdeményezési napló nem mindkét félnél ±1d2 formában mutatja a dobást.");
}

static void NeedStatusEffectsAreVisible()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, "adatok.csv"));
    var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
    var priestClass = new CharacterClassDefinition(CharacterClassIds.Pap, "Pap", PrimaryAbilities.Zero, true, 1.0);
    var player = new LiveCharacter("Éhező", race, priestClass, new PrimaryAbilities(5, 5, 5, 5),
        200, 100, 1, 1);
    player.ConsumeFood(100);
    player.ConsumeWater(100);
    player.AddStatus(data.GetStatus(CharacterStatusIds.Hungry));
    player.AddStatus(data.GetStatus(CharacterStatusIds.Thirsty));
    player.AddStatus(data.GetStatus(CharacterStatusIds.Diseased));
    player.ReceiveDamage(100);

    Assert(player.PreviewVitalityRecovery(100) == 37,
        "Az éhség és betegség kombinált gyógyításcsökkentése hibás.");
    var started = CreateBattleSystem(713).StartBattle(player, CreateEnemy(1000, 1));
    var startLog = string.Join(" ", started.Entries.Select(entry => entry.Message));
    Assert(startLog.Contains("🍖 nulla élelem: ❤️ -", StringComparison.Ordinal) &&
           startLog.Contains("💧 szomjúság: 🔷 -", StringComparison.Ordinal) &&
           startLog.Contains("💧 szomjúság 6", StringComparison.Ordinal),
        "A csatakezdő szükséglethatások vagy a szomjúság kezdeményezés-büntetése nem látható.");

    var attackLogs = new List<string>();
    for (var index = 0; index < 100 && !started.State.IsCompleted; index++)
    {
        var entry = CreateBattleSystem(713 + index).Advance(started.State).Entries.Single().Message;
        if (entry.Contains("Éhező támadja", StringComparison.Ordinal)) attackLogs.Add(entry);
        if (attackLogs.Any(log => log.Contains("🍖 éhség -2 fizikai sebzés", StringComparison.Ordinal))) break;
    }
    Assert(attackLogs.Any(log => log.Contains("💧 szomjúság -2 találat", StringComparison.Ordinal)) &&
           attackLogs.Any(log => log.Contains("🍖 éhség -2 fizikai sebzés", StringComparison.Ordinal)),
        "A találat- vagy fizikai sebzésbüntetés oka nem látható a támadás naplójában.");
}

static void BattleHitHighlightsDamageAndHealth()
{
    var system = CreateBattleSystem(712);
    var player = CreateCharacter("Sebző", vitality: 500, characterClassId: CharacterClassIds.Pap);
    var state = system.StartBattle(player, CreateEnemy(1000, 1)).State;
    string? playerHit = null;
    string? enemyHit = null;
    for (var index = 0; index < 100 && (playerHit is null || enemyHit is null) && !state.IsCompleted; index++)
    {
        var entry = system.Advance(state).Entries.Single().Message;
        if (entry.Contains("Sebző támadja", StringComparison.Ordinal) && entry.Contains("→ 🎯", StringComparison.Ordinal))
            playerHit = entry;
        if (entry.Contains("támadja Sebző", StringComparison.Ordinal) && entry.Contains("→ 🎯", StringComparison.Ordinal))
            enemyHit = entry;
    }
    Assert(playerHit?.Contains("= 💥 ", StringComparison.Ordinal) == true &&
           playerHit.Contains("Tesztellenfél ❤️ ", StringComparison.Ordinal) &&
           enemyHit?.Contains("= 💥 ", StringComparison.Ordinal) == true &&
           enemyHit.Contains("Sebző ❤️ ", StringComparison.Ordinal),
        "A sikeres támadásból hiányzik a sebzés- vagy a megmaradt HP ikonja.");
}

static void VictoryMessageIsConcise()
{
    var enemy = CreateEnemy(1, 1);
    var result = new BattleResult(true, 7, ["7. kör — hosszú és redundáns utolsó támadás."]);
    Assert(ConsoleRenderer.FormatBattleResultMessage(result, enemy) ==
           "GYŐZELEM 🏆: Tesztellenfél elesett.",
        "A győzelmi üzenet továbbra is megismétli az utolsó támadás részleteit.");
}

static void SnapshotRequiresCurrentBattlePrompt()
{
    var (session, leader, _) = CreateSession();
    var battleId = BattleId.New();
    session.SetBattlePrompt(battleId, 2, leader.Id,
        [BattleActionKind.PhysicalAttack, BattleActionKind.TurnUndead]);
    var battle = new BattleSnapshot(battleId, 2, 1, true, leader.Id,
        new SessionEnemySnapshot("E-TEST", "Tesztellenfél", new Position(3, 2), 8, 10),
        [BattleActionKind.PhysicalAttack, BattleActionKind.TurnUndead],
        [new BattleSpellOption("S-TEST", "Tesztvarázs", 1, 3, SpellTargetType.Enemy, 5, 0,
            null, null, 0, 0, [new Position(3, 2)])],
        [new BattleTacticOptionSnapshot(BattleActionKind.FighterPrecise, "🎯 Pontos", "sebzés ×0,75", 55)]);
    var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "Tesztlabirintus",
        new Dictionary<CharacterId, Position> { [leader.Id] = new Position(2, 2) }, battle));
    Assert(snapshot.Battle?.BattleId == battleId && snapshot.Battle.AllowedActions.Count == 2 &&
           snapshot.Battle.SpellOptions?.Single().ValidTargets.Single() == new Position(3, 2) &&
           snapshot.Battle.TacticOptions?.Single().HitChancePercent == 55,
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
    var visibleEnemy = CreateEnemyAt(new Position(3, 2), "E-VISIBLE", "r");
    var hiddenEnemy = CreateEnemyAt(new Position(4, 5), "E-HIDDEN");
    var visibleNpc = new WorldNpc(new Position(1, 1), "NPC-VISIBLE", CreateCharacter("Segítő"),
        NpcDisposition.Friendly, true, false, "Veletek tartok.");
    var hiddenNpc = new WorldNpc(new Position(5, 4), "NPC-HIDDEN", CreateCharacter("Rejtett"),
        NpcDisposition.Neutral, false, true, "Titok.");
    foreach (var position in new[] { visibleEnemy.Position, hiddenEnemy.Position, new Position(1, 2),
                 visibleNpc.Position, hiddenNpc.Position })
        maze.Carve(position);
    maze.AddEnemy(visibleEnemy);
    maze.AddEnemy(hiddenEnemy);
    maze.AddTreasureChest(new TreasureChest(new Position(1, 2), 99));
    maze.AddWorldNpc(visibleNpc);
    maze.AddWorldNpc(hiddenNpc);
    maze.PlaceDoor(new Position(2, 3), DoorState.Closed);
    var fog = new FogOfWar(maze.Width, maze.Height, 1);
    fog.RevealFrom(maze, maze.Entrance);
    fog.ToggleDeveloperReveal();

    var world = WorldSnapshotProjector.Create(maze, fog);
    Assert(world.Enemies.Count == 1 && world.Enemies[0].EntityId == visibleEnemy.Id,
        "A world snapshot rejtett ellenfelet is publikált, vagy kihagyta a láthatót.");
    Assert(world.Chests.Count == 1 && world.Doors.Count == 1,
        "A felfedett statikus entitások hiányoznak a world snapshotból.");
    Assert(world.Npcs?.Single().EntityId == visibleNpc.Id && world.Npcs.Single().Name == "Segítő",
        "A world snapshot rejtett NPC-t publikált, vagy kihagyta a láthatót.");
    Assert(world.Chests.Single().SymbolCodePoint == new Rune('▣').Value &&
           world.Chests.Single().ForegroundColor == ConsoleColor.Yellow,
        "A world snapshot nem őrizte meg a láda hostoldali megjelenését.");
    Assert(world.Exit is null && world.RevealedCells.All(cell => fog.IsRevealed(cell.Position)),
        "A world snapshot rejtett kijáratot vagy cellát publikált.");
    Assert(world.RevealedCells.Single(cell => cell.Position == new Position(2, 1)).ForegroundColor == maze.WallColor,
        "A world snapshot nem őrizte meg a host falszínét.");
    Assert(world.Doors.Single().ForegroundColor == ConsoleColor.DarkYellow &&
           world.Doors.Single().SymbolCodePoint == new Rune('╬').Value,
        "A world snapshot nem őrizte meg a host ajtószínét vagy ajtójelét.");
    Assert(world.Enemies.Single().Color != ConsoleColor.Red,
        "A world snapshot a host erősségfüggő színe helyett fix kliensszínt adott az ellenfélnek.");
    Assert(world.Enemies.Single().SymbolCodePoint == new Rune('r').Value,
        "A world snapshot nem őrizte meg az ellenfél katalógusban megadott jelét.");
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
    Assert(first.Revision == 1 && first.Slots.Count == 18 &&
           first.Slots.Single(slot => slot.Kind == InventorySlotKind.Backpack && slot.Index == 0)
               .Item?.DefinitionId == ration.Id,
        "Az inventory snapshot slotjai vagy revíziója hibás.");
    Assert(character.SetInventoryItem(InventorySlotKind.Backpack, 0, null), "A teszttárgy nem távolítható el.");
    var second = InventorySnapshotProjector.Create(character);
    Assert(second.Revision == first.Revision + 1 &&
           second.Slots.Single(slot => slot.Kind == InventorySlotKind.Backpack && slot.Index == 0).Item is null,
        "Az inventory mutáció nem növelte pontosan egyszer a revíziót.");
}

static void VictorySummaryIsCompact()
{
    var enemy = CreateEnemy(1, 1);
    var result = new BattleResult(true, 4, []);
    Assert(ConsoleRenderer.FormatBattleVictorySummary(result, enemy, 11, 8, ["🤒"], 4) ==
           "GYŐZELEM 🏆: Tesztellenfél elesett. ⌛4❤️-11🔷-8🤒🍖-4💧-4",
        "A győzelmi összegzés nem a várt kompakt formátumot használja.");
    Assert(ConsoleRenderer.FormatBattleVictorySummary(result, enemy, 0, 0, [], 2) ==
           "GYŐZELEM 🏆: Tesztellenfél elesett. ⌛4🍖-2💧-2",
        "A nulla HP-/manaveszteséget nem szabad megjeleníteni.");
    Assert(ConsoleRenderer.FormatAutoBattleVictorySummary(result, "Borin", enemy, 11, 8, ["🤒"], 4, 2, 120) ==
           "AUTOCSATA 🏆: Borin → Tesztellenfél elesett. ⌛4❤️-11🔷-8🤒🍖-4💧-4📜2✨120XP",
        "A győztes autocsata összegzése nem kompakt vagy nem nevezi meg a harcoló társat.");
    Assert(ConsoleRenderer.FormatAutoBattleDefeatSummary(result with { PlayerWon = false }, "Borin", enemy,
               20, 3, [], 4, 0) ==
           "AUTOCSATA 💀: Borin elesett → Tesztellenfél. ⌛4❤️-20🔷-3🍖-4💧-4👹1HP",
        "A vesztes autocsata összegzése nem kompakt vagy nem mutatja az ellenfél megmaradt HP-ját.");
}

static void BackpackStacksIdenticalItemsUpToNine()
{
    var character = CreateCharacter("Kötegteszt");
    var ration = new MiscItemDefinition("I-STACK", "Útravaló", "Tesztélelem", 2,
        ConsumableEffect.Food, 10);
    for (var count = 0; count < 10; count++)
        Assert(character.AddToBackpack(ration), "Az azonos tárgy nem fért be a hátizsákba.");
    Assert(character.Backpack.Count == 12 &&
           character.GetInventoryItemQuantity(InventorySlotKind.Backpack, 0) == 9 &&
           character.GetInventoryItemQuantity(InventorySlotKind.Backpack, 1) == 1,
        "A hátizsák nem kilences kötegre és új slotra bontotta a tíz azonos tárgyat.");
    Assert(character.RemoveOneInventoryItem(InventorySlotKind.Backpack, 0) &&
           character.GetInventoryItemQuantity(InventorySlotKind.Backpack, 0) == 8,
        "Egy tárgy elvétele nem pontosan eggyel csökkentette a köteget.");
    var snapshot = InventorySnapshotProjector.Create(character);
    Assert(snapshot.Slots.Single(slot => slot.Kind == InventorySlotKind.Backpack && slot.Index == 0)
               .Item?.Quantity == 8,
        "A coop inventory snapshot nem továbbította a köteg darabszámát.");
}

static void CharacterSheetLayoutIsShared()
{
    var character = CreateCharacter("Közös lap");
    var experienceByLevel = new Dictionary<int, int> { [2] = 100 };
    var inventory = InventorySnapshotProjector.Create(character);
    var snapshot = new SessionCharacterSnapshot(character.Id, character.Name, character.Race.Id,
        character.CharacterClass.Id, character.Level, character.CurrentVitality, character.MaximumVitality,
        character.CurrentMana, character.MaximumMana, character.FoodLevel, character.WaterLevel, character.Gold,
        character.IsAlive, null, [], inventory,
        CharacterSheetSnapshotProjector.Create(character, experienceByLevel));

    var hostLines = CharacterSheetPanel.Build(character, experienceByLevel, 3, 1, 4);
    var guestLines = CharacterSheetPanel.Build(snapshot, 3, 1, 4);
    Assert(hostLines.SequenceEqual(guestLines),
        "A doménkarakterből és a hálózati snapshotból felépített karakterlap eltér.");
    var abilityLine = hostLines.Single(line => line.Row == 4);
    Assert(abilityLine.Text.Contains("💖", StringComparison.Ordinal) &&
           abilityLine.Text.EndsWith("👁️", StringComparison.Ordinal) && abilityLine.ColoredSuffix == "5" &&
           abilityLine.ColoredSuffixColor == ConsoleColor.White &&
           abilityLine.Text.Length + abilityLine.ColoredSuffix.Length <= CharacterSheetPanel.Width,
        $"A karakterlap képességsora nem mutatja szabályosan a látótávot: '{abilityLine.Text}{abilityLine.ColoredSuffix}'.");
    var restored = JsonSerializer.Deserialize<SessionCharacterSnapshot>(JsonSerializer.Serialize(snapshot));
    Assert(restored is not null && CharacterSheetPanel.Build(restored, 3, 1, 4).SequenceEqual(hostLines),
        "A közös karakterlap read modelje nem élte túl a JSON wire-körutat.");
    var hostLeaderLine = CharacterSheetPanel.Build(character, experienceByLevel, 3, 1, 4, true)
        .Single(line => line.Row == 2);
    var guestLeaderLine = CharacterSheetPanel.Build(snapshot, 3, 1, 4, true)
        .Single(line => line.Row == 2);
    Assert(hostLeaderLine == guestLeaderLine && hostLeaderLine.Text.Contains("👑 VEZÉR", StringComparison.Ordinal) &&
           hostLeaderLine.Color == ConsoleColor.Yellow,
        "A host és a vendég karakterlapján nem azonos a feltűnő vezérjelzés.");
    var followerSnapshot = snapshot with { IsTemporaryFollower = true };
    var hostFollowerLine = CharacterSheetPanel.Build(character, experienceByLevel, 3, 1, 4,
        isTemporaryFollower: true).Single(line => line.Row == 2);
    var guestFollowerLine = CharacterSheetPanel.Build(followerSnapshot, 3, 1, 4)
        .Single(line => line.Row == 2);
    Assert(hostFollowerLine == guestFollowerLine &&
           hostFollowerLine.Text.Contains("👤 KÖVETŐ", StringComparison.Ordinal) &&
           hostFollowerLine.Color == ConsoleColor.Black &&
           hostFollowerLine.Background == ConsoleColor.Yellow,
        "A host és a vendég karakterlapján nem azonos a követőjelzés szövege vagy színe.");
}

static void GuestAvatarUsesClassGlyphAndCharacterColor()
{
    Assert(CharacterSheetPanel.CharacterClassGlyph(CharacterClassIds.Harcos) == "H" &&
           CharacterSheetPanel.CharacterClassGlyph(CharacterClassIds.Barbár) == "B" &&
           CharacterSheetPanel.CharacterClassGlyph(CharacterClassIds.Lovag) == "L" &&
           CharacterSheetPanel.CharacterClassGlyph(CharacterClassIds.Tolvaj) == "T" &&
           CharacterSheetPanel.CharacterClassGlyph(CharacterClassIds.Pap) == "P" &&
           CharacterSheetPanel.CharacterClassGlyph(CharacterClassIds.Mágus) == "M",
        "A kasztazonosítók nem a megfelelő térképi betűre képződnek.");
    var (session, leader, companion) = CreateSession();
    var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "Színteszt",
        new Dictionary<CharacterId, Position>
        {
            [leader.Id] = new Position(1, 1),
            [companion.Id] = new Position(2, 1)
        }));
    Assert(snapshot.Party.Single(character => character.CharacterId == leader.Id).Color == leader.Color &&
           snapshot.Party.Single(character => character.CharacterId == companion.Id).Color == companion.Color,
        "A session snapshot nem őrizte meg a karakterhez rendelt konzolszínt.");
}

static void GuestRedrawIgnoresReplicationSequences()
{
    var (session, leader, companion) = CreateSession();
    var positions = new Dictionary<CharacterId, Position>
    {
        [leader.Id] = new Position(1, 1),
        [companion.Id] = new Position(2, 1)
    };
    var first = session.CreateSnapshot(new SessionSnapshotContext(1, "Render", positions));
    var sequenceOnly = first with
    {
        SnapshotSequence = first.SnapshotSequence + 10,
        LastEventSequence = first.LastEventSequence + 5
    };
    var changed = first with { MazeLevel = first.MazeLevel + 1 };
    Assert(CoopGuestRenderFingerprint.Compute(first) == CoopGuestRenderFingerprint.Compute(sequenceOnly),
        "A render fingerprint puszta replikációs sorszámra megváltozott.");
    Assert(CoopGuestRenderFingerprint.Compute(first) != CoopGuestRenderFingerprint.Compute(changed),
        "A render fingerprint valódi látható állapotváltozást nem érzékelt.");
}

static void ReplicationPublisherSharesPartyInventories()
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
    var experienceByLevel = new Dictionary<int, int> { [2] = 100 };
    snapshot = snapshot with
    {
        Party = snapshot.Party.Select(character => character with
        {
            CharacterSheet = CharacterSheetSnapshotProjector.Create(
                character.CharacterId == leader.Id ? leader : companion, experienceByLevel)
        }).ToArray()
    };
    var publisher = new SessionReplicationPublisher();
    var hostFrame = publisher.CreateFrame(session.HostPlayerId, snapshot);
    var guestFrame = publisher.CreateFrame(remote, snapshot);

    Assert(hostFrame.Session.Party.All(character => character.Inventory is not null),
        "A host nem kapta meg a teljes parti inventory read modelt.");
    Assert(guestFrame.Session.Party.All(character => character.Inventory is not null),
        "A vendég nem kapta meg a party hátizsákok közötti mozgatáshoz szükséges inventory read modelleket.");
    Assert(guestFrame.Session.Party.All(character => character.CharacterSheet is not null),
        "A vendég nem kapta meg a lapozható party-karakterlapokat.");
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

static void RemoteInventoryTransferCanCrossBackpacksOnly()
{
    var (session, leader, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
    companion.AddToBackpack(new MiscItemDefinition("I-REMOTE", "Vendégtárgy", "Teszt", 1));
    var command = new InventoryTransferCommand(remote, 1, companion.Id, companion.InventoryRevision,
        InventorySlotKind.Backpack, 0, leader.Id, leader.InventoryRevision, InventorySlotKind.Backpack, 1);
    session.Submit(command);
    Assert(session.TryReadCommand(out var accepted) && accepted == command,
        "A vendég nem mozgathatott tárgyat a saját és a host hátizsákja között.");
    var weapon = new WeaponDefinition("W-REMOTE", "Vendégfegyver", "Kard", new ValueRange(1, 4), 0, false,
        new HashSet<string> { companion.CharacterClass.Id }, "Teszt", 1);
    Assert(companion.SetInventoryItem(InventorySlotKind.Weapon, 0, weapon),
        "A vendég felszereléskorlátozási tesztje nem készíthető elő.");
    var forbidden = new InventoryTransferCommand(remote, 2, companion.Id, companion.InventoryRevision,
        InventorySlotKind.Weapon, 0, leader.Id, leader.InventoryRevision, InventorySlotKind.Backpack, 2);
    session.Submit(forbidden);
    Assert(!session.TryReadCommand(out _),
        "A vendég másik karakterhez felszerelést is mozgathatott.");
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
           accepted.PlayerId is not null && accepted.ReconnectToken?.Length == 64 &&
           accepted.AvailableCharacters?.Single().Name == "Companion",
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
    var characterAction = new CharacterActionCommand(PlayerId.New(), 8, CharacterId.New(),
        CharacterAction.CloseOrLockDoor, new Position(7, 9), UseKey: false);
    Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(characterAction)) is CharacterActionCommand decodedAction &&
           decodedAction == characterAction && decodedAction.UseKey == false,
        "A JSON wire codec megváltoztatta a karakterhez kötött akciót.");
    var attackOrder = new LeaderActionCommand(PlayerId.New(), 9, CharacterId.New(),
        LeaderAction.ToggleAttackMode);
    Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(attackOrder)) is LeaderActionCommand decodedOrder &&
           decodedOrder == attackOrder,
        "A JSON wire codec megváltoztatta a Támadás leader-parancsot.");
    var sale = new InnSaleCommand(PlayerId.New(), 10, CharacterId.New(), 4, 7, 2);
    Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(sale)) is InnSaleCommand decodedSale &&
           decodedSale == sale, "A JSON wire codec megváltoztatta a fogadói eladást.");
    var helpVisibility = new SetHelpVisibilityCommand(PlayerId.New(), 11, CharacterId.New(), true);
    Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(helpVisibility)) is SetHelpVisibilityCommand decodedHelp &&
           decodedHelp == helpVisibility, "A JSON wire codec megváltoztatta a súgó láthatósági parancsát.");
    var distribution = new DistributeInventoryStackCommand(PlayerId.New(), 12, CharacterId.New(), 4, 2);
    Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(distribution)) is
               DistributeInventoryStackCommand decodedDistribution && decodedDistribution == distribution,
        "A JSON wire codec megváltoztatta az inventory-szétosztási parancsot.");
    var imageAcknowledgement = new AcknowledgeLevelImageCommand(PlayerId.New(), 13, CharacterId.New(),
        Guid.NewGuid());
    Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(imageAcknowledgement)) is
               AcknowledgeLevelImageCommand decodedImageAcknowledgement &&
           decodedImageAcknowledgement == imageAcknowledgement,
        "A JSON wire codec megváltoztatta a pályakép-nyugtázást.");
    var characterState = new CharacterStateSync(PlayerId.New(), CharacterId.New(), "character-json",
        CharacterSyncReason.CharacterDied);
    Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(characterState)) is CharacterStateSync decodedState &&
           decodedState == characterState, "A JSON wire codec megváltoztatta a karakter-visszaszinkronizálást.");
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

static void GatewayAndSimulationDoNotDeadlock()
{
    var (session, _, companion) = CreateSession();
    var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
    var gateway = new CoopHostGateway(session, new SessionHandshakeService(session, "1.0.0", hash),
        new SessionReplicationPublisher());
    var hello = (ServerHello)CoopProtocolJson.Decode(gateway.HandleIncoming("deadlock-connection",
        CoopProtocolJson.Encode(new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég")))
        .Single().WireMessage);
    var playerId = hello.PlayerId!.Value;
    gateway.HandleIncoming("deadlock-connection",
        CoopProtocolJson.Encode(new CharacterControlRequest(playerId, companion.Id)));

    var simulation = Task.Run(() =>
    {
        for (var index = 1; index <= 200; index++)
        {
            gateway.HandleIncoming("deadlock-connection", CoopProtocolJson.Encode(
                new MoveCharacterCommand(playerId, index, companion.Id, Direction.Right)));
            session.TryReadCommand(out _);
            gateway.HandleIncoming("deadlock-connection", CoopProtocolJson.Encode(
                new MoveCharacterCommand(playerId, index, companion.Id, Direction.Left)));
            session.TryReadCommand(out _);
        }
    });
    var lifecycle = Task.Run(() =>
    {
        for (var index = 0; index < 200; index++)
            gateway.HandleIncoming("deadlock-connection",
                CoopProtocolJson.Encode(new CharacterControlRequest(playerId, companion.Id)));
    });
    Assert(Task.WaitAll([simulation, lifecycle], TimeSpan.FromSeconds(5)),
        "A session-event és a gateway lifecycle egymás zárolására várt.");
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
    var commandRejected = new TaskCompletionSource<GameCommandRejectedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
    client.CommandRejected += rejected => commandRejected.TrySetResult(rejected);
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
    GameCommand? accepted = null;
    for (var attempt = 0; attempt < 100 && accepted is null; attempt++)
    {
        if (session.TryReadCommand(out var queued)) accepted = queued;
        else await Task.Delay(10);
    }
    Assert(accepted == move,
        "A SignalR kliens commandja nem jutott el a host session queue-jáig.");
    await client.SendCommandAsync(move);
    var duplicateAccepted = false;
    for (var attempt = 0; attempt < 100 && !commandRejected.Task.IsCompleted; attempt++)
    {
        duplicateAccepted |= session.TryReadCommand(out _);
        if (!commandRejected.Task.IsCompleted) await Task.Delay(10);
    }
    Assert(!duplicateAccepted, "A host session elfogadta a duplikált hálózati commandot.");
    var rejectionSnapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "SignalR pálya",
        new Dictionary<CharacterId, Position>
        {
            [leader.Id] = maze.Entrance,
            [companion.Id] = new Position(3, 2)
        }, World: WorldSnapshotProjector.Create(maze, fog)));
    await server.PublishSnapshotAsync(rejectionSnapshot);
    Assert((await commandRejected.Task.WaitAsync(TimeSpan.FromSeconds(5))).CommandId == move.CommandId,
        "A szimulációs szál command-elutasítása nem jutott vissza a SignalR klienshez.");
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

static LiveCharacter CreateCharacter(string name, int vitality = 20,
    string characterClassId = CharacterClassIds.Harcos)
{
    var abilities = new PrimaryAbilities(5, 5, 5, 5);
    var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
    var characterClass = new CharacterClassDefinition(characterClassId, characterClassId, PrimaryAbilities.Zero, false, 1.0);
    return new LiveCharacter(name, race, characterClass, abilities, vitality, 0, 1, 0);
}

static void CompactPartyStatusShowsResources()
{
    var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
    var mageClass = new CharacterClassDefinition(CharacterClassIds.Mágus, "Mágus", PrimaryAbilities.Zero,
        true, 1.0);
    var mage = new LiveCharacter("Hosszúnevű", race, mageClass, new PrimaryAbilities(5, 5, 5, 5),
        40, 20, 1, 0);
    mage.SetCurrentResources(10, 12);
    var status = CharacterSheetPanel.BuildPartyStatus(mage, true, isLeader: true);
    Assert(status.Text.Length <= CharacterSheetPanel.Width, "A party státusz túllóg a jobb panelen.");
    Assert(status.Identity.Contains("👑", StringComparison.Ordinal),
        "A leader koronája hiányzik a party státuszból.");
    Assert(status.Text.Contains("❤️25%", StringComparison.Ordinal) &&
           status.Text.Contains("🔷60%", StringComparison.Ordinal),
        "A party státusz nem százalékosan mutatja a HP-t és a manát.");
    Assert(status.VitalityColor == ConsoleColor.Red && status.ManaColor == ConsoleColor.Cyan,
        "A party státusz erőforrásszínei nem követik a százalékos küszöböket.");

    var fighter = CreateCharacter("Hosszú Harcos", vitality: 40,
        characterClassId: CharacterClassIds.Harcos);
    var fighterStatus = CharacterSheetPanel.BuildPartyStatus(fighter, false);
    Assert(string.IsNullOrEmpty(fighterStatus.Mana) &&
           !fighterStatus.Text.Contains("🔷", StringComparison.Ordinal) &&
           fighterStatus.Identity.Contains("Hosszú Harcos", StringComparison.Ordinal),
        "A manát nem használó karakter party státusza helyet foglal a manna számára.");
}

static void WindowFrameCatalogIsResizableAndConfigured()
{
    Assert(ConsoleRenderer.MessageLogLineCount == 7 && ConsoleRenderer.MessageLogBufferLineCount == 21 &&
           ConsoleRenderer.ScreenRowCount == 52,
        "Az 1080p-s fő játékfelület nem 7 látható és 21 tárolt logsorral, összesen 52 sorra van méretezve.");
    foreach (var style in Enum.GetValues<WindowFrameStyle>())
    {
        Assert(WindowFrameCatalog.Horizontal(style, 52).Length == 52,
            $"A(z) {style} keret felső sora nem tartja a kért szélességet.");
        Assert(WindowFrameCatalog.Horizontal(style, 27, bottom: true).Length == 27,
            $"A(z) {style} keret alsó sora nem tartja a kért szélességet.");
    }
    Assert(WindowFrameCatalog.Horizontal(WindowFrameStyle.Scroll2, 6) == "╭≈≈≈≈╮" &&
           WindowFrameCatalog.Horizontal(WindowFrameStyle.Scroll2, 6, bottom: true) == "╰≈≈≈≈╯" &&
           WindowFrameCatalog.Sides(WindowFrameStyle.Scroll2, 0, 3) == new WindowFrameRow(" )", "( ") &&
           WindowFrameCatalog.Sides(WindowFrameStyle.Scroll2, 1, 3) == new WindowFrameRow("( ", " )"),
        "A scroll2 keretsablon nem az előírt váltakozó tekercsformát adja.");
    Assert(WindowFrameCatalog.Adornment(WindowFrameStyle.Sword, 12) == "   ▲    ▲   " &&
           WindowFrameCatalog.Horizontal(WindowFrameStyle.Sword, 12) == "═══╪════╪═══" &&
           WindowFrameCatalog.Sides(WindowFrameStyle.Sword, 0, 3) == new WindowFrameRow("   │", "│   ") &&
           WindowFrameCatalog.Adornment(WindowFrameStyle.Sword, 12, bottom: true) == "   ▼    ▼   ",
        "A sword keretsablon kardjai és függőleges élei nem igazodnak egymáshoz.");
    Assert(WindowFrameCatalog.Horizontal(WindowFrameStyle.Magic2, 37) ==
           "· ✦ ─────── ◆ ───────── ◆ ─────── ✦ ·" &&
           WindowFrameCatalog.Horizontal(WindowFrameStyle.Magic2, 37, bottom: true) ==
           "· ✦ ─────── ◆ ───────── ◆ ─────── ✦ ·" &&
           WindowFrameCatalog.Sides(WindowFrameStyle.Magic2, 0, 3) == new WindowFrameRow("│", "│"),
        "A magic2 keretsablon nem az előírt szimmetrikus mágikus díszsort adja.");
    Assert(WindowFrameConfiguration.For(FramedWindow.MainMenu) == WindowFrameStyle.Ruby &&
           WindowFrameConfiguration.For(FramedWindow.Help) == WindowFrameStyle.Ruby &&
           WindowFrameConfiguration.For(FramedWindow.SpellSelector) == WindowFrameStyle.Magic &&
           WindowFrameConfiguration.For(FramedWindow.CreaturePortrait) == WindowFrameStyle.Stone &&
           WindowFrameConfiguration.For(FramedWindow.Storyline) == WindowFrameStyle.Stone &&
           WindowFrameConfiguration.For(FramedWindow.LevelUp) == WindowFrameStyle.Scroll2 &&
           WindowFrameConfiguration.For(FramedWindow.LevelUpChoice) == WindowFrameStyle.Sword &&
           WindowFrameConfiguration.For(FramedWindow.SpellLearning) == WindowFrameStyle.Magic2 &&
           WindowFrameConfiguration.For(FramedWindow.SpellPreparation) == WindowFrameStyle.Magic2 &&
           WindowFrameConfiguration.For(FramedWindow.Inn) == WindowFrameStyle.Ruby,
        "Az első körös ablak–keret alapbeállítások hibásak.");
}

static void RaceTraitsAreLoadedFromData()
{
    var dataPath = Path.Combine(AppContext.BaseDirectory, "adatok.csv");
    var catalog = CsvGameDataLoader.Load(dataPath);
    Assert(catalog.GetRace("R001").HasTrait(RaceTraits.Adaptable), "Az ember Alkalmazkodó tulajdonsága hiányzik.");
    Assert(catalog.GetRace("R002").HasTrait(RaceTraits.Resilient), "A törp Rendíthetetlen tulajdonsága hiányzik.");
    Assert(catalog.GetRace("R003").HasTrait(RaceTraits.KeenSenses), "Az elf Éles érzékek tulajdonsága hiányzik.");
    Assert(catalog.GetRace("R004").HasTrait(RaceTraits.Relentless), "A félork Könyörtelen tulajdonsága hiányzik.");
}

static void CharacterSheetColorsHealthAndManaSeparately()
{
    var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
    var mageClass = new CharacterClassDefinition(CharacterClassIds.Mágus, "Mágus", PrimaryAbilities.Zero,
        true, 1.0);
    var mage = new LiveCharacter("Színpróba", race, mageClass, new PrimaryAbilities(5, 5, 5, 8),
        20, 20, 1, 1);

    var full = CharacterSheetPanel.BuildResourceLine(mage);
    Assert(full.VitalityColor == ConsoleColor.Green && full.ManaColor == ConsoleColor.Cyan,
        "A teljes HP vagy a manna színe hibás.");
    mage.SetCurrentResources(10, 10);
    Assert(CharacterSheetPanel.BuildResourceLine(mage).VitalityColor == ConsoleColor.Green,
        "A pontosan fél HP tévesen piros.");
    mage.SetCurrentResources(9, 10);
    var low = CharacterSheetPanel.BuildResourceLine(mage);
    Assert(low.VitalityColor == ConsoleColor.Red && low.ManaColor == ConsoleColor.Cyan,
        "A fél HP alatti érték nem piros, vagy a manna nem maradt külön színű.");
}

static void SpellSchoolsIncludeMageLightSpell()
{
    var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, "adatok.csv"));
    foreach (var school in Enum.GetValues<SpellSchool>())
        for (var level = 1; level <= 5; level++)
            Assert(catalog.GetSpells(school, level).Count ==
                   (school == SpellSchool.Arcane && level == 1 ? 6 : 5),
                $"A(z) {school} iskola {level}. szintjén hibás a varázslatok száma.");

    var light = catalog.GetSpell("S026");
    Assert(light.Name == "Fényvarázslat" && light.Level == 1 &&
           light.UsageMode == SpellUsageMode.Exploration && light.TargetType == SpellTargetType.Self &&
           catalog.GetSpellEffects(light.Id).Single() is
               { Type: SpellEffectType.VisionBonus, Value: 2, Duration: 12 },
        "A Fényvarázslat adatai vagy látótávhatása hibás.");

    var creation = catalog.GetSpell("P021");
    Assert(creation.Name == "Étel és ital teremtése" && creation.Level == 1 &&
           creation.UsageMode == SpellUsageMode.Exploration &&
           catalog.GetSpellEffects(creation.Id).Single() is { Type: SpellEffectType.RestoreNeeds, Value: 35 },
        "Az első szintű Étel és ital teremtése varázslat adatai vagy hatása hibás.");
}

static void SpellMemorizationCapacityUsesClassFormula()
{
    static LiveCharacter Caster(string classId, int level)
    {
        var race = new RaceDefinition("R-MEM", "Teszt", PrimaryAbilities.Zero);
        var characterClass = new CharacterClassDefinition(classId, classId, PrimaryAbilities.Zero, true, 1.0);
        var character = new LiveCharacter("Memória", race, characterClass,
            new PrimaryAbilities(5, 5, 5, 8), 20, 20, 1, 1);
        character.SetProgress(level, 0);
        return character;
    }

    Assert(Caster(CharacterClassIds.Mágus, 1).MemorizationCapacity == 4 &&
           Caster(CharacterClassIds.Mágus, 30).MemorizationCapacity == 10,
        "A mágus memóriaképlete hibás.");
    Assert(Caster(CharacterClassIds.Pap, 1).MemorizationCapacity == 4 &&
           Caster(CharacterClassIds.Pap, 30).MemorizationCapacity == 10,
        "A pap memóriaképlete hibás.");
    Assert(Caster(CharacterClassIds.Lovag, 1).MemorizationCapacity == 2 &&
           Caster(CharacterClassIds.Lovag, 10).MemorizationCapacity == 3 &&
           Caster(CharacterClassIds.Lovag, 20).MemorizationCapacity == 4 &&
           Caster(CharacterClassIds.Lovag, 30).MemorizationCapacity == 4,
        "A lovag memóriaképlete vagy négyhelyes korlátja hibás.");
}

static void InventoryStackSplitIsAtomicAndRequiresSpace()
{
    var party = new Party();
    var character = CreateCharacter("StackSplit");
    party.SetLeader(character);
    var item = new MiscItemDefinition("I-STACK", "Dobókés", "Teszt", 1);
    for (var index = 0; index < 5; index++)
        Assert(character.AddToBackpack(item), "A tesztköteg nem fért a hátizsákba.");
    var revision = character.InventoryRevision;
    var command = new SplitInventoryStackCommand(PlayerId.New(), 1, character.Id, revision, 0);

    Assert(InventoryStackService.TryExecute(party, command, out var result, out var error), error);
    Assert(character.GetInventoryItemQuantity(InventorySlotKind.Backpack, 0) == 3 &&
           character.GetInventoryItemQuantity(InventorySlotKind.Backpack, result.DestinationIndex) == 2 &&
           character.InventoryRevision == revision + 1,
        "Az 5 darabos köteg nem atomi 3+2 kötegre vált szét.");

    for (var index = 0; index < LiveCharacter.MaximumBackpackItemCount; index++)
    {
        if (character.GetInventoryItem(InventorySlotKind.Backpack, index) is not null) continue;
        Assert(character.SetInventoryItem(InventorySlotKind.Backpack, index,
            new MiscItemDefinition($"I-FILL-{index}", $"Töltelék {index}", "Teszt", 1)),
            "A tele hátizsák tesztjének előkészítése sikertelen.");
    }
    var fullCommand = command with { CommandId = 2, ExpectedInventoryRevision = character.InventoryRevision };
    Assert(!InventoryStackService.TryExecute(party, fullCommand, out _, out error) &&
           error.Contains("nincs üres hely", StringComparison.OrdinalIgnoreCase),
        "A tele hátizsák felezése nem adott egyértelmű figyelmeztetést.");
}

static void ConsumableStackDistributesEvenly()
{
    var party = new Party();
    var source = CreateCharacter("Forrás");
    var second = CreateCharacter("Második");
    var third = CreateCharacter("Harmadik");
    party.SetLeader(source);
    party.Add(second);
    party.Add(third);
    var ration = new MiscItemDefinition("I-DISTRIBUTE", "Útravaló", "Teszt", 1,
        ConsumableEffect.Food, 25);
    for (var index = 0; index < 9; index++)
        Assert(source.AddToBackpack(ration), "A szétosztási teszt forráskötege nem fért el.");
    var sourceRevision = source.InventoryRevision;
    var secondRevision = second.InventoryRevision;
    var thirdRevision = third.InventoryRevision;
    var command = new DistributeInventoryStackCommand(PlayerId.New(), 1, source.Id, sourceRevision, 0);

    Assert(InventoryDistributionService.TryExecute(party, command, out var result, out var error), error);
    Assert(source.GetInventoryItemQuantity(InventorySlotKind.Backpack, 0) == 3 &&
           second.GetInventoryItemQuantity(InventorySlotKind.Backpack, 0) == 3 &&
           third.GetInventoryItemQuantity(InventorySlotKind.Backpack, 0) == 3 &&
           result.DistributedQuantity == 6 && result.RemainingSourceQuantity == 3 &&
           source.InventoryRevision == sourceRevision + 1 &&
           second.InventoryRevision == secondRevision + 1 && third.InventoryRevision == thirdRevision + 1,
        "A kilences fogyóeszközköteg nem 3–3–3 arányban, atomi revíziónöveléssel oszlott szét.");

    var nonConsumable = new MiscItemDefinition("I-NONDISTRIBUTE", "Dísztárgy", "Teszt", 1);
    Assert(source.AddToBackpack(nonConsumable), "A nem fogyasztható teszttárgy nem fért el.");
    var nonConsumableIndex = Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount)
        .Single(index => source.GetInventoryItem(InventorySlotKind.Backpack, index)?.Id == nonConsumable.Id);
    var invalid = command with { CommandId = 2, ExpectedInventoryRevision = source.InventoryRevision,
        BackpackIndex = nonConsumableIndex };
    Assert(!InventoryDistributionService.TryExecute(party, invalid, out _, out error) &&
           error.Contains("elfogyasztható", StringComparison.OrdinalIgnoreCase),
        "A szétosztás elfogadott egy nem elfogyasztható tárgyat.");
}

static void ClassResourceGrowthLoadsFromCsv()
{
    var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, "adatok.csv"));
    Assert(catalog.GetCharacterResourceGrowth(CharacterClassIds.Barbár).AdjustVitality(5) == 7 &&
           catalog.GetCharacterResourceGrowth(CharacterClassIds.Harcos).AdjustVitality(5) == 6 &&
           catalog.GetCharacterResourceGrowth(CharacterClassIds.Tolvaj).AdjustVitality(5) == 5 &&
           catalog.GetCharacterResourceGrowth(CharacterClassIds.Mágus).AdjustVitality(1) == 1,
        "A barbár/harcos/tolvaj/mágus HP-növekedési módosítója hibás.");
    Assert(catalog.GetCharacterResourceGrowth(CharacterClassIds.Mágus).AdjustMana(5) == 6 &&
           catalog.GetCharacterResourceGrowth(CharacterClassIds.Pap).AdjustMana(5) == 5 &&
           catalog.GetCharacterResourceGrowth(CharacterClassIds.Lovag).AdjustMana(5) == 3,
        "A mágus/pap/lovag mannanövekedési módosítója hibás.");
}

static void NpcDefinitionsLoadFromCsv()
{
    var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, "adatok.csv"));
    Assert(catalog.Npcs.Count == 20 && catalog.NpcEncounters.Count == 28 &&
           Enumerable.Range(1, MazeLevelConfigurations.FinalLevel).All(level =>
               catalog.NpcEncounters.Any(encounter => encounter.MazeLevel == level)),
        "Az NPC-definíciók vagy valamelyik pálya találkozása hiányzik.");
    Assert(catalog.NpcDialogues.Count == 72 && catalog.NpcQuests.Count == 36 &&
           catalog.NpcQuests.Count(quest => quest.Type == NpcQuestType.Collect) == 7 &&
           catalog.NpcQuests.Count(quest => quest.Type == NpcQuestType.Kill) == 16 &&
           catalog.NpcQuests.Count(quest => quest.Type == NpcQuestType.Explore) == 5 &&
           catalog.NpcQuests.Count(quest => quest.Type == NpcQuestType.Disarm) == 3 &&
           catalog.NpcQuests.Count(quest => quest.Type == NpcQuestType.OpenChest) == 4 &&
           catalog.NpcQuests.Count(quest => quest.Type == NpcQuestType.Escort) == 1,
        "Az NPC-párbeszédek vagy a küldetéstípusok hibásan töltődtek.");
    Assert(catalog.GetNpc("NPC001") is { Disposition: NpcDisposition.Neutral, Unique: false } &&
           catalog.GetNpcQuests("NPC002").Any(quest =>
               quest is { TargetId: "E003", ExperienceReward: 260 }) &&
           catalog.GetNpcQuests("NPC001").Any(quest => quest is
               { Id: "NPCQ001", RewardItemId: "T018", RewardItemCount: 2, RandomRewardCount: 0 }) &&
           catalog.GetNpc("NPC020") is { Unique: true, Recruitable: true, RaceId: "R003" } &&
           catalog.GetNpcQuests("NPC020").Select(quest => quest.Type).ToHashSet().SetEquals(
               [NpcQuestType.Escort, NpcQuestType.Collect, NpcQuestType.Kill]) &&
           catalog.NpcQuests.All(quest => quest.RandomRewardCount > 0 || quest.RewardItemCount > 0),
        "A semleges nem egyedi NPC vagy a hozzá kapcsolt küldetés hibás.");

    var npc = new WorldNpc(new Position(1, 1), "NPC002", CreateCharacter("Küldetésadó"),
        NpcDisposition.Neutral, false, true, "Próba", questIds: ["NPCQ002"]);
    Assert(npc.ActivateQuest("NPCQ002") && npc.AddQuestProgress("NPCQ002", 3, 4) &&
           npc.Quests.Single() is { State: NpcQuestState.Active, Progress: 3 } &&
           npc.AddQuestProgress("NPCQ002", 1, 4) && npc.CompleteQuest("NPCQ002") &&
           npc.Quests.Single().State == NpcQuestState.Completed,
        "Az NPC-küldetés felvétele, haladása vagy egyszeri lezárása hibás.");

    npc.AdjustFriendliness(20);
    npc.BeginFollowing();
    npc.AdvanceConversation();
    var follower = new PartyMemberAvatar(new Position(1, 1), npc.Character, npc);
    follower.MoveTo(new Position(2, 1));
    Assert(npc.Friendliness == 10 && npc.State == WorldNpcState.Following &&
           npc.ConversationStage == 1 && follower.IsTemporaryFollower && npc.Position == follower.Position,
        "Az egyedi NPC viszonya vagy az ideiglenes követő pozíciója hibás.");
    follower.MakePermanent();
    Assert(!follower.IsTemporaryFollower,
        "Az ideiglenes követő nem alakítható végleges partitaggá.");
}

static void QuestJournalBuildsSharedHistory()
{
    var entries = new QuestJournalEntrySnapshot[]
    {
        new("Q-A", "Folyamatban", "Tedd meg.", "Elira", QuestJournalStatus.Active, 2, 4, 240),
        new("Q-B", "Befejezve", "Megtetted.", "Elira", QuestJournalStatus.Completed, 1, 1, 420)
    };
    var lines = QuestJournalWindow.Build(entries);
    Assert(WindowFrameConfiguration.For(FramedWindow.QuestOffer) == WindowFrameStyle.Stone &&
           WindowFrameConfiguration.For(FramedWindow.QuestJournal) == WindowFrameStyle.Scroll2 &&
           lines.Any(line => line.Text.Contains("Folyamatban — 2/4  Elira (240 XP)", StringComparison.Ordinal)) &&
           lines.Any(line => line.Text.Contains("Befejezve — Elira (+420 XP)", StringComparison.Ordinal)),
        "A küldetésnapló kerete vagy aktív/teljesített tartalma hibás.");
}

static void SpellUiModelsAreShared()
{
    var spell = new KnownSpellSnapshot("spell-test", "Próbaláng", 2, 7, SpellTargetType.Enemy,
        "Egy próbaként használt varázslat.", true, 0);
    var infoLines = SpellInfoPanel.Build("Rubin", CharacterClassIds.Mágus, 6,
        new SpellInfoSnapshot("Kristálygömb", 3, [spell]), 0);
    Assert(infoLines.Any(line => line.Row == 5 && line.Text.Contains("[M][F1]", StringComparison.Ordinal)) &&
           infoLines.Any(line => line.Row == 43 && line.Text == "Következő feloldás: L10") &&
           infoLines.Single(line => line.Row == 5).Background == ConsoleColor.DarkCyan,
        "A közös varázslatinformációs panel elvesztette a gyorshelyet, feloldást vagy kijelölést.");

    var selectorLines = SpellSelectorWindow.Build("Rubin", 5, 12, true,
        [new SpellSelectorOption("Próbaláng", 2, 7, SpellTargetType.Enemy, "F1", false)], 0, 0);
    Assert(selectorLines[0].Text == "⚔️ HARCI VARÁZSLÁS" &&
           selectorLines.Any(line => line.Text.Contains("[F1] L2", StringComparison.Ordinal) &&
                                     line.Color == ConsoleColor.DarkRed),
        "A közös varázslatválasztó elvesztette a harci címet, gyorshelyet vagy mannafigyelmeztetést.");
}

static void RestSummaryUiIsShared()
{
    var characterId = CharacterId.New();
    var rest = new PartyRestSnapshot(Guid.NewGuid(), false,
        [new CharacterRestSnapshot(characterId, "Rubin", ConsoleColor.Cyan,
            7, 12, 28, 35, 20, 20, true, ["🤒 betegség", "🩸 vérzés"])], []);
    var lines = RestSummaryWindow.Build(rest, "❖  Nyomj Entert a folytatáshoz...  ❖");
    Assert(WindowFrameConfiguration.For(FramedWindow.Inn) == WindowFrameStyle.Ruby &&
           lines.Any(line => line.Text.Contains("❤️ Rubin", StringComparison.Ordinal) &&
                             line.Text.Contains("+7", StringComparison.Ordinal) &&
                             line.Text.Contains("🔷+12", StringComparison.Ordinal)) &&
           lines.Any(line => line.Text.Contains("🤒 betegség", StringComparison.Ordinal) &&
                             line.Text.Contains("🩸 vérzés", StringComparison.Ordinal)),
        "A közös Ruby pihenési összegzőből hiányzik a HP, manna vagy megszűnt állapot.");
}

static void GuestItemInspectionKeepsDamageValue()
{
    var dataPath = Path.Combine(AppContext.BaseDirectory, "adatok.csv");
    var data = CsvGameDataLoader.Load(dataPath);
    var weapon = data.Weapons.First(candidate => candidate.Damage is not null);
    var inspection = ItemInspectionFormatter.Format(weapon, data);
    var lines = MessageTextLayout.Wrap(inspection.Text, 48).ToArray();
    Assert(lines.All(line => line.Length <= 48) &&
           string.Join(' ', lines).Contains($"sebzés: {weapon.Damage}", StringComparison.Ordinal),
        "A vendég tényleges panelszélességű tördelése levágta a fegyver sebzésértékét.");
}

static void BossAndBattlePromptsAreShared()
{
    var boss = new BossPresentationSnapshot("Káoszúr", "🐉 Fekete sárkány", 5, "🔑 Aranykulcs");
    var lines = NarrativeWindow.Build("BOSS KÖZELEG", "X. fejezet", ["Nincs menekvés."],
        "❖  Tovább  ❖", kind: NarrativeKind.BossIntroduction, boss: boss);
    Assert(lines[0] == ("⚔️👑  BOSS KÖZELEG  👑⚔️", ConsoleColor.Red) &&
           lines.Any(line => line.Text.Contains("Káoszúr", StringComparison.Ordinal)) &&
           lines.Any(line => line.Text.Contains("Erősség: 5/5", StringComparison.Ordinal) &&
                             line.Text.Contains("Aranykulcs", StringComparison.Ordinal)),
        "A közös boss-ablak elvesztette a boss azonosságát, erősségét vagy jutalmát.");

    var tactics = new[]
    {
        new BattleTacticOptionSnapshot(BattleActionKind.FighterPrecise, "🎯 Pontos", "sebzés ×0,75", 65)
    };
    Assert(BattlePromptText.Tactic(CharacterClassIds.Harcos, tactics).Contains("65%", StringComparison.Ordinal) &&
           BattlePromptText.EnemyTurn == "Space — ellenfél köre" &&
           BattlePromptText.PlayerAction(true, true).Contains("halottűzés", StringComparison.Ordinal),
        "A közös harci prompt elvesztette a taktikai esélyt vagy valamelyik vezérlést.");
}

static void AbilityMagicItemsAreUniversalAndCapped()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, "adatok.csv"));
    var expected = new Dictionary<string, (MagicItemEffect Effect, int Value, int Price)>
    {
        ["M017"] = (MagicItemEffect.Strength, 1, 1200),
        ["M018"] = (MagicItemEffect.Strength, 2, 3000),
        ["M019"] = (MagicItemEffect.Dexterity, 1, 1200),
        ["M020"] = (MagicItemEffect.Dexterity, 2, 3000),
        ["M021"] = (MagicItemEffect.Health, 1, 1200),
        ["M022"] = (MagicItemEffect.Health, 2, 3000),
        ["M023"] = (MagicItemEffect.Intelligence, 1, 1200),
        ["M024"] = (MagicItemEffect.Intelligence, 2, 3000)
    };
    foreach (var (id, definition) in expected)
    {
        var item = data.GetMagicItem(id);
        Assert(item.Effect == definition.Effect && item.EffectValue == definition.Value &&
               item.BasePrice == definition.Price &&
               data.CharacterClasses.All(characterClass => item.CanBeEquippedBy(characterClass.Id)),
            $"A(z) {id} képességtárgy adatai vagy kasztengedélyei hibásak.");
    }

    var race = data.GetRace("R001");
    var characterClass = data.CharacterClasses.First();
    var character = new LiveCharacter("Ékszerteszt", race, characterClass,
        new PrimaryAbilities(12, 12, 12, 12), 100, 100, 1, 1);
    Assert(character.AddMagicItem(data.GetMagicItem("M018")) &&
           character.AddMagicItem(data.GetMagicItem("M019")) &&
           character.AddMagicItem(data.GetMagicItem("M022")),
        "A képességtárgyak nem voltak felszerelhetők.");
    Assert(character.EffectiveAbilities == new PrimaryAbilities(13, 13, 13, 12) &&
           character.Abilities == new PrimaryAbilities(12, 12, 12, 12),
        "A felszerelt képességbónusz átlépte a 13-at vagy módosította az alapértéket.");
    var snapshot = CharacterSheetSnapshotProjector.Create(character, data.ExperienceByLevel);
    Assert(snapshot.Abilities == character.EffectiveAbilities,
        "A karakterlap és a coop snapshot nem az effektív képességeket mutatja.");
    Assert(character.SetInventoryItem(InventorySlotKind.MagicItem, 0, null) &&
           character.EffectiveAbilities.Strength == 12 && character.Abilities.Strength == 12,
        "A varázstárgy levétele után nem szűnt meg a képességbónusz.");
}

static void UnknownCsvSectionIsRejectedWithLineNumber()
{
    var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "adatok.csv"));
    var invalid = source.Replace("#Képességek", "#Elgépelt képességek", StringComparison.Ordinal);
    AssertCsvLoadFails(invalid, "Ismeretlen fejezetcím", "sorában");
}

static void MissingRequiredCsvFieldIsRejectedWithLineNumber()
{
    var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "adatok.csv"));
    var invalid = source.Replace("R001,Ember,Adaptable", "R001,Ember", StringComparison.Ordinal);
    AssertCsvLoadFails(invalid, "Tulajdonság", "sorában");
}

static void AssertCsvLoadFails(string content, params string[] expectedMessageParts)
{
    var path = Path.Combine(Path.GetTempPath(), $"kaoszrubin-invalid-{Guid.NewGuid():N}.csv");
    try
    {
        File.WriteAllText(path, content, new UTF8Encoding(false));
        try
        {
            CsvGameDataLoader.Load(path);
            throw new InvalidOperationException("A hibás CSV betöltése nem dobott kivételt.");
        }
        catch (InvalidDataException exception)
        {
            Assert(expectedMessageParts.All(part => exception.Message.Contains(part,
                    StringComparison.OrdinalIgnoreCase)),
                $"A CSV-hibaüzenet nem elég részletes: {exception.Message}");
        }
    }
    finally
    {
        if (File.Exists(path)) File.Delete(path);
    }
}

static void AdaptableRaceGainsChosenAbility()
{
    var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero, RaceTraits.Adaptable);
    var characterClass = new CharacterClassDefinition("C001", "Harcos", PrimaryAbilities.Zero, false, 1.0);
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, "adatok.csv"));
    var character = LiveCharacterFactory.Create("Ember", race, characterClass,
        new PrimaryAbilities(5, 5, 5, 5), 1, 1, data, ConsoleColor.Cyan,
        new PrimaryAbilities(0, 0, 0, 1));
    Assert(character.Abilities == new PrimaryAbilities(5, 5, 5, 6),
        "Az Alkalmazkodó tulajdonság nem a kiválasztott képességre adta a +1-et.");
    Assert(PerkProgressionRules.TriggerLevel(race, 1) == 4 &&
           PerkProgressionRules.TriggerLevel(race, 2) == 15,
        "Az Alkalmazkodó ember tehetségszintjei hibásak.");
}

static void CharacterVisionRangeUsesClassRaceAndEffects()
{
    var abilities = new PrimaryAbilities(5, 5, 5, 5);
    var human = new RaceDefinition("R-HUMAN", "Ember", PrimaryAbilities.Zero);
    var elf = new RaceDefinition("R-ELF", "Elf", PrimaryAbilities.Zero, RaceTraits.KeenSenses);
    var fighterClass = new CharacterClassDefinition(CharacterClassIds.Harcos, "Harcos", PrimaryAbilities.Zero,
        false, 1.0);
    var thiefClass = new CharacterClassDefinition(CharacterClassIds.Tolvaj, "Tolvaj", PrimaryAbilities.Zero,
        false, 1.0);
    var fighter = new LiveCharacter("Harcos", human, fighterClass, abilities, 20, 0, 1, 0);
    var thief = new LiveCharacter("Tolvaj", human, thiefClass, abilities, 20, 0, 1, 0);
    var elfThief = new LiveCharacter("Elf tolvaj", elf, thiefClass, abilities, 20, 0, 1, 0);
    Assert(CharacterClassRules.VisionRange(fighter) == 5 &&
           CharacterClassRules.VisionRange(thief) == 7 &&
           CharacterClassRules.VisionRange(elfThief) == 8,
        "A karakter 5/7/8-as alap-, tolvaj- vagy elf látótávja hibás.");

    var darkLevelLine = CharacterSheetPanel.Build(fighter, new Dictionary<int, int> { [2] = 100 },
        9, 0, 12).Single(line => line.Row == 4);
    Assert(CharacterClassRules.VisionRange(fighter, -2) == 3 && darkLevelLine.ColoredSuffix == "3" &&
           darkLevelLine.ColoredSuffixColor == ConsoleColor.Red,
        "Az extra sötét pálya nem csökkenti vagy nem pirosítja a látótávot.");

    fighter.ApplySpellEffect(new ActiveSpellEffect("LIGHT", ActiveSpellEffectType.VisionBonus, 2, 12, Beneficial: true));
    Assert(CharacterClassRules.NaturalVisionRange(fighter) == 5 &&
           CharacterClassRules.VisionRange(fighter) == 7,
        "A pozitív látótávhatás nem különül el a természetes látótávtól.");
    var increasedLine = CharacterSheetPanel.Build(fighter, new Dictionary<int, int> { [2] = 100 },
        1, 0, 12).Single(line => line.Row == 4);
    Assert(increasedLine.ColoredSuffix == "7" && increasedLine.ColoredSuffixColor == ConsoleColor.Green,
        "A növelt látótáv száma nem zöld a karakterlapon.");

    fighter.ApplySpellEffect(new ActiveSpellEffect("DARKNESS", ActiveSpellEffectType.VisionBonus, -4, 12));
    var decreasedLine = CharacterSheetPanel.Build(fighter, new Dictionary<int, int> { [2] = 100 },
        1, 0, 12).Single(line => line.Row == 4);
    Assert(CharacterClassRules.VisionRange(fighter) == 3 && decreasedLine.ColoredSuffix == "3" &&
           decreasedLine.ColoredSuffixColor == ConsoleColor.Red,
        "A csökkentett látótáv értéke vagy piros kijelzése hibás.");
}

static void EnemyVisionRangesLoadFromCsv()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, "adatok.csv"));
    Assert(data.GetEnemy("E001").VisionRange == 3 && data.GetEnemy("E003").VisionRange == 4 &&
           data.GetEnemy("E019").VisionRange == 7 && data.GetEnemy("E050").VisionRange == 8,
        "A patkány, goblin, vámpír vagy káoszsárkány CSV-látótávja hibás.");
}

static void FogRevealUsesVariableRangeAndLineOfSight()
{
    var maze = new Maze(12, 5);
    for (var x = 2; x <= 10; x++) maze.Carve(new Position(x, 2));
    var origin = maze.Entrance;
    var normalFog = new FogOfWar(maze.Width, maze.Height, 5);
    normalFog.RevealFrom(maze, origin, 5);
    var far = new Position(9, 2);
    Assert(!normalFog.IsRevealed(far), "Az ötrácsos látótáv túl messzire fedett fel.");

    var scoutFog = new FogOfWar(maze.Width, maze.Height, 5);
    scoutFog.RevealFrom(maze, origin, 8);
    Assert(scoutFog.IsRevealed(far), "A nyolcrácsos látótáv nem fedte fel a távoli folyosót.");

    maze.PlaceDoor(new Position(4, 2), DoorState.Closed);
    var blockedFog = new FogOfWar(maze.Width, maze.Height, 5);
    blockedFog.RevealFrom(maze, origin, 8);
    Assert(!blockedFog.IsRevealed(new Position(5, 2)), "A zárt ajtó mögé átlátott a felfedés.");
}

static void PursuitMemoryLastsThreeMoves()
{
    var enemy = CreateEnemy(10, 1);
    var target = CharacterId.New();
    enemy.ResolvePursuit(true, target);
    Assert(enemy.PursuitMemoryRemainingMoves == 3 && enemy.TryRememberPursuitTarget() &&
           enemy.TryRememberPursuitTarget() && enemy.TryRememberPursuitTarget() &&
           !enemy.TryRememberPursuitTarget() && enemy.PursuitMemoryRemainingMoves == 0,
        "Az ellenfél üldözési memóriája nem pontosan három lépésig tart.");

    var saved = new EnemySaveData(enemy.Position, enemy.Definition.Id, enemy.CurrentHitPoints,
        PursuitTargetCharacterId: target, PursuitMemoryRemainingMoves: 2);
    var restored = JsonSerializer.Deserialize<EnemySaveData>(JsonSerializer.Serialize(saved));
    Assert(restored?.PursuitMemoryRemainingMoves == 2,
        "Az üldözési memória nem élte túl a mentési JSON-körutat.");
}

static BattleSystem CreateBattleSystem(int seed) => new(new Random(seed),
    Array.Empty<MonsterAbilityDefinition>(), Array.Empty<StatusDefinition>(),
    Array.Empty<StrengthHitBonusDefinition>());

static ConfiguredEnemy CreateEnemy(int hitPoints, int strength, int speed = 1) => new(new Position(1, 1),
    new EnemyDefinition("E-TEST", "Tesztellenfél", "e", strength, hitPoints, 0, speed,
        1, 1, Array.Empty<string>()));

static ConfiguredEnemy CreateEnemyAt(Position position, string id, string appearance = "e") => new(position,
    new EnemyDefinition(id, "Tesztellenfél", appearance, 1, 10, 0, 1,
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
