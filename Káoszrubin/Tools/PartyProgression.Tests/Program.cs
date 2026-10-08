using System.Reflection;
using System.Text.Json;
using KaoszRubin.Application;
using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.UI;
using KaoszRubin.World;

var tests = new (string Name, Action Run)[]
{
    ("4/5/6 kapacitás és idempotens mérföldkövek", CapacityThresholds),
    ("A küszöbök közös konfigurációból hangolhatók", ConfigurableThresholds),
    ("Kikapcsolt feloldás mellett négy hely marad", DisabledExpansion),
    ("Azonos karakterazonosító nem foglalhat két helyet", DuplicateIds),
    ("Visszatöltés minden tagot egyszer és kapacitáson belül tart meg", RestoreMembers),
    ("Halál, eltávolítás és vezetőváltás megtartja a haladást", MonotonicProgression),
    ("Támogatás csak sikeres normál felvételkor fogy", RecruitmentGrants),
    ("Párhuzamos támogatásfelhasználás egyszer sikerül", ConcurrentGrant),
    ("Párhuzamos coop belépés nem tölti túl a partit", ConcurrentCoop),
    ("Társ cseréje megőrzi a vezetőt és az összes többi tagot", CompanionReplacement),
    ("Régi mentésben az 5/8 határ helyesen migrálódik", LegacyMigration),
    ("Questmentés a felfüggesztett kampányból migrálódik", SuspendedMigration),
    ("Új mentés magas pályája nem jelent teljesítést", NewSaveDoesNotInferProgression),
    ("Régi pillanatkép nem adja vissza az elhasznált jutalmat", SuspendedProgressionMerge),
    ("Hat tag felszerelése és jutalomállapota menthető", SaveRoundTrip),
    ("A host kapacitása és jutalomállapota a snapshotba kerül", SessionRoundTrip),
    ("A fogadó fejlécében az aktuális kapacitás látszik", InnCapacityDisplay),
    ("A pályajutalom a kapacitás növelése előtt jár", CompletionRewardOrder),
    ("A 2x2 régi slotbeosztása migrációkor változatlan", LegacyFormation)
};
var failures = 0;
foreach (var (name, run) in tests.Concat(FormationTests.Cases).Concat(RecruitmentTests.Cases))
{
    try { run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception error) { failures++; Console.WriteLine($"FAIL {name}: {error}"); }
}
var totalTests = tests.Length + FormationTests.Cases.Count() + RecruitmentTests.Cases.Count();
Console.WriteLine($"{totalTests - failures}/{totalTests} teszt sikeres.");
return failures == 0 ? 0 : 1;

static PartyCapacityRules EnabledRules() => new() { ExpandedPartyEnabled = true };
static LiveCharacter Character(string name, CharacterId? id = null) => new(name,
    new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero),
    new CharacterClassDefinition("C001", "Harcos", PrimaryAbilities.Zero, false, 1),
    new PrimaryAbilities(10, 10, 10, 10), 20, 0, 0, 0, id: id);
static Party PartyOfSize(int count, int completed = 0)
{
    var party = new Party(EnabledRules());
    party.SetLeader(Character("Vezető"));
    if (completed > 0) party.RecordCampaignLevelCompletion(completed);
    for (var index = 1; index < count; index++) Check(party.Add(Character($"Társ{index}")), "Nem fért be a teszttárs.");
    return party;
}
static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
static GameDataCatalog Catalog() => CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, "Data", "game-data.csv"));
static void CapacityThresholds()
{
    var party = PartyOfSize(4);
    Check(party.IsFull && !party.Add(Character("Ötödik")), "Új kampányban több mint négy tag fért be.");
    party.RecordCampaignLevelCompletion(4);
    Check(party.Capacity == 4, "Korai ötödik hely.");
    party.RecordCampaignLevelCompletion(5);
    Check(party.Capacity == 5 && party.Add(Character("Ötödik")), "Az ötödik hely nem nyílt meg.");
    party.RecordCampaignLevelCompletion(7);
    Check(party.Capacity == 5 && !party.Add(Character("Hatodik")), "Korai hatodik hely.");
    party.RecordCampaignLevelCompletion(8);
    Check(party.Capacity == 6 && party.Add(Character("Hatodik")) && !party.Add(Character("Hetedik")), "Hibás hatos korlát.");
    party.RecordCampaignLevelCompletion(5);
    party.RecordCampaignLevelCompletion(8);
    Check(party.Capacity == 6 && party.AvailableRecruitmentGrants.Count == 2, "Ismételt lezárás módosította a jutalmakat.");
}
static void ConfigurableThresholds()
{
    var party = new Party(EnabledRules() with { FifthMemberCompletedLevel = 6, SixthMemberCompletedLevel = 9 });
    party.RecordCampaignLevelCompletion(5);
    Check(party.Capacity == 4, "A hangolt első küszöb nem érvényesült.");
    party.RecordCampaignLevelCompletion(6);
    Check(party.Capacity == 5, "A hangolt ötödik hely nem nyílt meg.");
    party.RecordCampaignLevelCompletion(9);
    Check(party.Capacity == 6, "A hangolt hatodik hely nem nyílt meg.");
}
static void DisabledExpansion()
{
    var party = new Party(new PartyCapacityRules { ExpandedPartyEnabled = false });
    party.RecordCampaignLevelCompletion(8);
    Check(Party.MaximumSize == 6 && party.Capacity == 4 && party.UnlockedCapacity == 6 &&
        party.AvailableRecruitmentGrants.Count == 0 && party.PendingUnlockPresentations.Count == 0,
        "Az első ütem véletlenül bekapcsolta a bővítést.");
    party.Restore(Character("Host"), Enumerable.Range(0, 6).Select(index => Character($"Társ{index}")));
    Check(party.Members.Count == 4, "Kikapcsolt feloldással a visszatöltés túl nagy csapatot adott.");
}
static void DuplicateIds()
{
    var party = PartyOfSize(1);
    Check(!party.Add(Character("Másolat", party.Leader!.Id)), "Két azonos karakterazonosító bekerült.");
}
static void RestoreMembers()
{
    var party = PartyOfSize(6, 8);
    var original = party.Members.ToArray();
    party.Restore(original[0], original.Concat([Character("Másolat", original[1].Id), Character("Hetedik")]));
    Check(party.Members.SequenceEqual(original), "A visszatöltés elvesztett vagy duplikált tagot.");
    party.Restore(original[0], party.Members);
    Check(party.Members.Count == 6, "Önmagából visszatöltve elveszett a lista.");
}
static void MonotonicProgression()
{
    var party = PartyOfSize(4, 8);
    var oldLeader = party.Leader!;
    oldLeader.SetCurrentResources(0, 0);
    party.Remove(oldLeader);
    party.SetLeader(party.Members[0]);
    party.Clear();
    Check(party.Capacity == 6, "Karakterváltozás visszavonta a kampányhaladást.");
    party.StartNewCampaign();
    Check(party.Capacity == 4 && party.UnlockedCapacity == 4, "Az új kampány nem négy helyről indult.");
}
static void RecruitmentGrants()
{
    var party = PartyOfSize(4, 5);
    var special = Character("Roderic");
    special.SetSourceNpcDefinitionId("NPC021");
    Check(!party.TryAddWithRecruitmentGrant(special, PartyExpansionMilestone.FifthMember), "Egyedi NPC támogatást használt.");
    Check(!party.TryAddWithRecruitmentGrant(Character("Másolat", party.Leader!.Id), PartyExpansionMilestone.FifthMember), "Duplikáció beváltotta a támogatást.");
    Check(party.AvailableRecruitmentGrants.Count == 1, "Sikertelen felvétel támogatást fogyasztott.");
    Check(party.Add(Character("Ötödik")), "Az ötödik fizetős társ nem fért be.");
    Check(!party.TryAddWithRecruitmentGrant(Character("Teli"), PartyExpansionMilestone.FifthMember) &&
        party.AvailableRecruitmentGrants.Count == 1, "A tele parti elhasználta a támogatást.");
    party.Remove(party.Members[^1]);
    Check(party.TryAddWithRecruitmentGrant(Character("Ingyenes"), PartyExpansionMilestone.FifthMember), "A támogatás nem használható.");
    party.Remove(party.Members[^1]);
    party.RecordCampaignLevelCompletion(5);
    Check(!party.TryAddWithRecruitmentGrant(Character("Ismét"), PartyExpansionMilestone.FifthMember), "Kétszer felhasználható támogatás.");
    party.RecordCampaignLevelCompletion(8);
    Check(party.AvailableRecruitmentGrants.SequenceEqual([PartyExpansionMilestone.SixthMember]), "A támogatások nem függetlenek.");
    Check(party.TryMarkUnlockPresented(PartyExpansionMilestone.FifthMember) &&
        !party.TryMarkUnlockPresented(PartyExpansionMilestone.FifthMember), "Ismétlődő bemutatás.");
}
static void ConcurrentGrant()
{
    var party = PartyOfSize(4, 8);
    var success = 0;
    Parallel.For(0, 24, index =>
    {
        if (party.TryAddWithRecruitmentGrant(Character($"Zsoldos{index}"), PartyExpansionMilestone.FifthMember))
            Interlocked.Increment(ref success);
    });
    Check(success == 1 && party.Members.Count == 5 && party.AvailableRecruitmentGrants.Count == 1,
        "Párhuzamos beváltás kétszer sikerült.");
}
static void ConcurrentCoop()
{
    foreach (var (count, completed) in new[] { (4, 0), (5, 5), (6, 8) })
    {
        var party = PartyOfSize(count - 1, completed);
        var session = new GameSession(party, party.Leader!);
        var players = Enumerable.Range(0, 16).Select(_ => session.RegisterRemotePlayer()).ToArray();
        var success = 0;
        Parallel.For(0, players.Length, index =>
        {
            if (session.TryJoinRemoteCharacter(players[index], Character($"Vendég{index}"), out _))
                Interlocked.Increment(ref success);
        });
        Check(success == 1 && party.Members.Count == count, "A coop átlépte a kapacitást vagy eltávolított egy NPC-t.");
    }
}
static void CompanionReplacement()
{
    var party = PartyOfSize(4);
    var original = party.Members;
    var recruit = Character("Új társ");
    Check(!party.TryReplaceCompanion(original[0], recruit), "A vezető lecserélhető volt.");
    Check(!party.TryReplaceCompanion(original[1], Character("Duplikált", original[2].Id)), "Duplikált társ csere útján belépett.");
    Check(party.TryReplaceCompanion(original[1], recruit) && party.Leader == original[0] &&
        party.Members.Count == 4 && party.Members.Contains(original[2]) && party.Members.Contains(original[3]),
        "A csere elvesztett más tagot.");
}
static void LegacyMigration()
{
    foreach (var (level, expected) in new[] { (1, 4), (5, 4), (6, 5), (8, 5), (9, 6), (22, 6) })
    {
        var save = GameSaveFormat.MigrateToCurrent(new GameSaveData { Version = 35, MazeLevel = level });
        var party = new Party(EnabledRules());
        party.MergeCampaignProgression(save.PartyCampaignProgression);
        Check(save.Version == GameSaveFormat.CurrentVersion && party.Capacity == expected, $"Hibás migráció a(z) {level}. pályán.");
        var before = JsonSerializer.Serialize(save.PartyCampaignProgression);
        GameSaveFormat.MigrateToCurrent(save);
        Check(JsonSerializer.Serialize(save.PartyCampaignProgression) == before, "A migráció nem idempotens.");
    }
    var shifted = GameSaveFormat.MigrateToCurrent(new GameSaveData { Version = 33, MazeLevel = 8 });
    Check(shifted.MazeLevel == 9 && shifted.PartyCampaignProgression.HighestCompletedCampaignLevel == 8,
        "A beszúrt kampánypálya és a kapacitás migrációja hibás sorrendű.");
}
static void SuspendedMigration()
{
    var save = GameSaveFormat.MigrateToCurrent(new GameSaveData
    {
        Version = 35, MazeLevel = 50, DifficultyLevel = 50, LocationKind = AdventureLocationKind.Quest,
        SuspendedCampaign = new() { Version = 35, MazeLevel = 6 }
    });
    Check(save.PartyCampaignProgression.HighestCompletedCampaignLevel == 5 &&
        save.SuspendedCampaign!.PartyCampaignProgression.HighestCompletedCampaignLevel == 5,
        "A quest nehézségéből származott a feloldás.");
    var orphanQuest = GameSaveFormat.MigrateToCurrent(new GameSaveData
        { Version = 35, MazeLevel = 50, LocationKind = AdventureLocationKind.Quest });
    Check(orphanQuest.PartyCampaignProgression.HighestCompletedCampaignLevel == 0,
        "Kampánypillanatkép nélkül a quest új helyet adott.");
}
static void NewSaveDoesNotInferProgression()
{
    foreach (var kind in Enum.GetValues<AdventureLocationKind>())
    {
        var save = GameSaveFormat.MigrateToCurrent(new GameSaveData { MazeLevel = 22, DifficultyLevel = 50, LocationKind = kind });
        Check(save.PartyCampaignProgression.HighestCompletedCampaignLevel == 0, "Új mentésben a pályaszám feloldást adott.");
    }
}
static void SuspendedProgressionMerge()
{
    var party = PartyOfSize(1, 8);
    Check(party.TryAddWithRecruitmentGrant(Character("Társ"), PartyExpansionMilestone.FifthMember), "Előkészítés sikertelen.");
    party.TryMarkUnlockPresented(PartyExpansionMilestone.FifthMember);
    var old = new PartyCampaignProgressionSnapshot(4);
    party.MergeCampaignProgression(old);
    Check(party.Capacity == 6 && !party.AvailableRecruitmentGrants.Contains(PartyExpansionMilestone.FifthMember) &&
        !party.PendingUnlockPresentations.Contains(PartyExpansionMilestone.FifthMember), "Régi pillanatkép visszavonta a haladást.");
    var save = GameSaveFormat.MigrateToCurrent(new GameSaveData
    {
        LocationKind = AdventureLocationKind.Quest, PartyCampaignProgression = party.CampaignProgression,
        SuspendedCampaign = new() { PartyCampaignProgression = old }
    });
    Check(save.SuspendedCampaign!.PartyCampaignProgression.HighestCompletedCampaignLevel == 8 &&
        save.SuspendedCampaign.PartyCampaignProgression.ConsumedRecruitmentGrants!.Contains(PartyExpansionMilestone.FifthMember),
        "A felfüggesztett kampány nem kapta meg a közös állapotot.");
}
static void SaveRoundTrip()
{
    var catalog = Catalog();
    var roster = new CharacterRoster(EnabledRules());
    var leader = Character("Mentett");
    roster.Add(leader); roster.Select(leader);
    roster.Party.RecordCampaignLevelCompletion(8);
    var weapon = catalog.GetWeapon("W001");
    for (var index = 1; index < 6; index++)
    {
        var member = Character($"Társ{index}");
        member.SetInventoryItem(InventorySlotKind.Weapon, 0, weapon);
        member.AddGold(index * 10);
        roster.Add(member);
        Check(index == 1 ? roster.Party.TryAddWithRecruitmentGrant(member, PartyExpansionMilestone.FifthMember)
            : roster.Party.Add(member), "Mentési teszttárs nem fért be.");
    }
    roster.Party.TryMarkUnlockPresented(PartyExpansionMilestone.FifthMember);
    var directory = Path.Combine(Path.GetTempPath(), $"party-progression-{Guid.NewGuid():N}");
    try
    {
        var characterService = new CharacterSaveService(Path.Combine(directory, "characters.json"), catalog, EnabledRules());
        var saveService = new GameSaveService(directory, characterService);
        var savedPath = saveService.Save(new GameSaveData { MainCharacterName = leader.Name, MazeLevel = 9 }, roster);
        var loaded = saveService.Load(savedPath);
        Check(loaded.Roster.Party.Members.Select(member => member.Id).SequenceEqual(roster.Party.Members.Select(member => member.Id)) &&
            loaded.Roster.Party.Members.Skip(1).All(member => member.GetInventoryItem(InventorySlotKind.Weapon, 0)?.Id == weapon.Id) &&
            loaded.Roster.Party.Members.Select(member => member.Gold).SequenceEqual(roster.Party.Members.Select(member => member.Gold)),
            "Tag, felszerelés vagy arany veszett el mentéskor.");
        Check(loaded.Roster.Party.Capacity == 6 && loaded.Roster.Party.AvailableRecruitmentGrants.SequenceEqual([PartyExpansionMilestone.SixthMember]) &&
            loaded.Roster.Party.PendingUnlockPresentations.SequenceEqual([PartyExpansionMilestone.SixthMember]), "A jutalomállapot visszaállítása hibás.");
        var migratedJson = JsonSerializer.Serialize(loaded.State);
        Check(GameSaveFormat.MigrateToCurrent(JsonSerializer.Deserialize<GameSaveData>(migratedJson)!).Version == GameSaveFormat.CurrentVersion,
            "Az új mentés JSON roundtripja hibás.");
    }
    finally
    {
        Check(Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase),
            "A tesztkönyvtár a kijelölt ideiglenes gyökéren kívülre mutat.");
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
static void SessionRoundTrip()
{
    var party = PartyOfSize(5, 8);
    party.TryMarkUnlockPresented(PartyExpansionMilestone.FifthMember);
    var session = new GameSession(party, party.Leader!);
    var snapshot = session.CreateSnapshot(new(9, "Teszt", new Dictionary<CharacterId, Position>()));
    var restored = JsonSerializer.Deserialize<SessionSnapshot>(JsonSerializer.Serialize(snapshot))!;
    Check(restored.ProtocolVersion == SessionProtocol.Version && restored.PartyCapacity == 6 && restored.Party.Count == 5 &&
        restored.PartyCampaignProgression!.HighestCompletedCampaignLevel == 8 &&
        restored.PartyCampaignProgression.PresentedUnlocks!.Contains(PartyExpansionMilestone.FifthMember),
        "A kapacitás vagy kampányállapot hiányzik a replikációból.");
}
static void InnCapacityDisplay()
{
    foreach (var capacity in new[] { 4, 5, 6 })
    {
        var lines = ConsoleRenderer.BuildInnMenuLines(4, 10, [], 0, "", false, partyCapacity: capacity);
        Check(lines.Any(line => line.Text.Contains($"Parti: 4/{capacity}")), "A fogadó technikai plafont mutat.");
    }
}
static void CompletionRewardOrder()
{
    var catalog = Catalog();
    var roster = new CharacterRoster(EnabledRules());
    var leader = Character("Jutalmazott"); roster.Add(leader); roster.Select(leader);
    var fallen = Character("Elesett"); roster.Add(fallen); roster.Party.Add(fallen); fallen.SetCurrentResources(0, 0);
    var awarded = new List<CharacterId>();
    var controller = new InnController(catalog, roster, leader, new ConsoleRenderer(catalog, roster.Party),
        _ => { }, new Random(1), (character, amount) =>
        {
            Check(roster.Party.Capacity == 4, "A feloldás a pályajutalom előtt megtörtént.");
            Check(amount == catalog.BaseLevelCompletionExperience * 5, "A pályajutalom hígult.");
            awarded.Add(character.Id);
            return new(amount, character.Level, character.Level, []);
        }, (_, _) => { }, () => { });
    var complete = typeof(InnController).GetMethod("CompleteLevelAtInn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    complete.Invoke(controller, [5]);
    Check(awarded.SequenceEqual([leader.Id]) && !roster.Characters.Contains(fallen) && roster.Party.Capacity == 5,
        "A túlélők jutalma vagy a lezárási sorrend hibás.");
}
static void LegacyFormation()
{
    var original = new PartyFormationSnapshot(CharacterId.New(), CharacterId.New(), CharacterId.New(), CharacterId.New(),
        Direction.Left, PartyFormationState.Locked);
    var save = GameSaveFormat.MigrateToCurrent(new GameSaveData { Version = 35, MazeLevel = 9, Formation = original });
    Check(save.Formation == original, "A régi négy slot migrációkor megváltozott.");
}
