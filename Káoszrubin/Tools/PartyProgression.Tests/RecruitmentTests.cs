using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using KaoszRubin.Application;
using KaoszRubin.Combat;
using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.UI;
using KaoszRubin.World;

internal static class RecruitmentTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("A harmadik ütem élesben öt helyet old fel, hatot még nem", CurrentRules),
        ("Régi mentés jogosultsága megmarad a hatodik hely bekapcsolásáig", LegacyEntitlement),
        ("Támogatott zsoldos pontos szinttel, felszereléssel és ellátmánnyal készül", SupportedCharacters),
        ("A három támogatott jelöltből legalább egy hiányzó kasztú", CandidatePool),
        ("Az ingyenes felvétel egyszer fogyaszt támogatást és nem költ aranyat", FreeRecruitment),
        ("Elavult, érvénytelen és inaktív felvétel nem módosít állapotot", RejectedRecruitment),
        ("Teli parti megtartja a támogatást, társcsere normál áron működik", FullPartyReplacement),
        ("A halasztott támogatás következő fogadóban új három jelöltet ad", DeferredRecruitment),
        ("Egyedi és visszavett társ nem használja a normál zsoldos támogatását", SpecialRecruitment),
        ("A feloldási történet csak egyszer, a jutalmazás után jelenik meg", UnlockPresentation),
        ("A felvett ötödik tag az előző pálya XP-jét nem kapja meg", CompletionBeforeRecruitment),
        ("A fogadói jelöltek és a támogatás hostállapota replikálható", InnReplication),
        ("Mentés-visszatöltés megtartja a halasztott és elhasznált támogatást", SavedRecruitment),
        ("Az ingyenes ajánlat és a támogatás egyértelmű a toborzási képernyőn", RecruitmentDisplay),
        ("A 6–8. kampánypálya generált területein elhelyezhető az ötös menetoszlop", CampaignLayouts)
    ];

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static readonly Lazy<GameDataCatalog> Data = new(() => CsvGameDataLoader.Load(
        Path.Combine(AppContext.BaseDirectory, "Data", "game-data.csv")));
    private static GameDataCatalog Catalog => Data.Value;
    private static void SetField(object owner, string name, object? value) => owner.GetType()
        .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(owner, value);
    private static object Invoke(object owner, string method, params object?[] arguments) => owner.GetType()
        .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(owner, arguments)!;
    private static LiveCharacter Character(string name, int level = 8, string classId = "C001")
    {
        var character = new RandomCharacterGenerator(Catalog, new Random(name.Aggregate(17, (hash, letter) => unchecked(hash * 31 + letter))))
            .GenerateCombatTestCharacter(Catalog.CharacterClasses.First(value => value.Id == classId), level, []);
        return character;
    }
    private static (InnController Inn, CharacterRoster Roster) Fixture(int members = 4, int completed = 5, int seed = 103,
        LiveCharacter? special = null, int? specialPrice = null)
    {
        var roster = new CharacterRoster();
        var leader = Character("Host"); roster.Add(leader); roster.Select(leader);
        roster.Party.RecordCampaignLevelCompletion(completed);
        for (var index = 1; index < members; index++)
        { var member = Character($"Társ{index}"); roster.Add(member); Check(roster.Party.Add(member), "Nem fért be a teszttárs."); }
        var inn = Controller(roster, seed, special, specialPrice);
        inn.InitializeRecruitment(completed);
        return (inn, roster);
    }
    private static InnController Controller(CharacterRoster roster, int seed = 103,
        LiveCharacter? special = null, int? specialPrice = null)
    {
        var inn = new InnController(Catalog, roster, roster.Party.Leader!, new ConsoleRenderer(Catalog, roster.Party),
            _ => { }, new Random(seed), (character, amount) => new(amount, character.Level, character.Level, []),
            (_, _) => { }, () => { }, specialRecruitCandidates: () => special is null ? [] : [special],
            specialRecruitmentPrice: (character, _) => ReferenceEquals(character, special) ? specialPrice : null);
        SetField(inn, "_active", true); SetField(inn, "_innName", "Próbafogadó");
        return inn;
    }
    private static LiveCharacter[] Candidates(InnController inn) => ((List<LiveCharacter>)inn.GetType()
        .GetField("_recruitCandidates", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(inn)!).ToArray();

    public static void CurrentRules()
    {
        var party = new Party();
        party.RecordCampaignLevelCompletion(4); Check(party.Capacity == 4, "Korai feloldás.");
        party.RecordCampaignLevelCompletion(5); Check(party.Capacity == 5, "Az ötödik hely nem nyílt meg.");
        party.RecordCampaignLevelCompletion(22);
        Check(party.Capacity == 5 && party.UnlockedCapacity == 6 &&
            party.AvailableRecruitmentGrants.SequenceEqual([PartyExpansionMilestone.FifthMember]) &&
            !party.TryMarkUnlockPresented(PartyExpansionMilestone.SixthMember), "A negyedik ütem idő előtt bekapcsolt.");
        Check(FormationEditor.AvailableShapes(party.Capacity, 5).SequenceEqual([PartyFormationShape.Column2x3]), "A széles forma idő előtt elérhető.");
    }
    public static void LegacyEntitlement()
    {
        var migrated = GameSaveFormat.MigrateToCurrent(new GameSaveData { Version = 35, MazeLevel = 9 });
        var party = new Party(); party.MergeCampaignProgression(migrated.PartyCampaignProgression);
        Check(party.Capacity == 5 && party.UnlockedCapacity == 6, "A régi jogosultság elveszett.");
        party.TryMarkUnlockPresented(PartyExpansionMilestone.FifthMember);
        var future = new Party(new PartyCapacityRules { ExpandedPartyEnabled = true });
        future.MergeCampaignProgression(party.CampaignProgression);
        Check(future.Capacity == 6 && future.PendingUnlockPresentations.SequenceEqual([PartyExpansionMilestone.SixthMember]),
            "A későbbi hatodik helyhez újra kellene teljesíteni a mérföldkövet.");
    }
    public static void SupportedCharacters()
    {
        var generator = new RandomCharacterGenerator(Catalog, new Random(174));
        foreach (var level in new[] { 1, 8, 15 })
        foreach (var characterClass in Catalog.CharacterClasses)
        {
            var character = generator.GenerateSupportedMercenary(characterClass, level, []);
            Check(character.Level == Math.Max(1, level - 1) && character.SourceNpcDefinitionId is null && character.IsAlive &&
                character.ActiveWeapons.Any(weapon => weapon is not null) && character.NpcBehavior is not null,
                $"Hibás szint vagy felszerelés: {characterClass.Name}.");
            var effects = character.Backpack.OfType<MiscItemDefinition>().Select(item => item.Effect).ToHashSet();
            Check(effects.Contains(ConsumableEffect.Food) && effects.Contains(ConsumableEffect.Water) &&
                effects.Contains(character.UsesMana ? ConsumableEffect.RestoreMana : ConsumableEffect.Heal) &&
                character.FoodLevel == 100 && character.WaterLevel == 100, "Hiányzó kezdő ellátmány.");
            if (character.IsSpellcaster)
                Check(character.CanCastSpells, $"Hiányzó fókusz: {characterClass.Name}, L{character.Level}.");
            if (characterClass.Id is CharacterClassIds.Pap or CharacterClassIds.Mágus)
                Check(character.KnownSpells.Count > 0, "A Pap vagy Mágus nem kapott kezdő varázslatot.");
        }
    }
    public static void CandidatePool()
    {
        for (var seed = 1; seed <= 12; seed++)
        {
            var (inn, roster) = Fixture(seed: seed);
            var candidates = Candidates(inn);
            var first = inn.RecruitmentOffers(); var revision = inn.Revision;
            Check(candidates.Length == 3 && candidates.Select(value => value.CharacterClass.Id).Distinct().Count() == 3 &&
                candidates.Any(candidate => roster.Party.Members.All(member => member.CharacterClass.Id != candidate.CharacterClass.Id)),
                "Nincs három eltérő kaszt vagy hiányzó kasztú jelölt.");
            Check(candidates.All(candidate => candidate.Level == roster.Party.Leader!.Level - 1) &&
                first.All(offer => offer.Price == 0 && offer.RecruitmentGrant == PartyExpansionMilestone.FifthMember), "Hibás támogatott ajánlat.");
            Check(inn.RecruitmentOffers().SequenceEqual(first) && inn.CreateSnapshot()!.Recruits!.SequenceEqual(first) && inn.Revision == revision,
                "A megtekintés újrasorsolta a jelölteket.");
        }
    }
    public static void FreeRecruitment()
    {
        var (inn, roster) = Fixture(); var leader = roster.Party.Leader!;
        leader.SpendGold(leader.Gold); var offer = inn.RecruitmentOffers()[0];
        var oldRevision = inn.Revision;
        Check(inn.TryRecruit(offer.CharacterId, oldRevision, null, out _) && leader.Gold == 0 && roster.Party.Members.Count == 5 &&
            roster.Characters.Any(member => member.Id == offer.CharacterId) && roster.Party.AvailableRecruitmentGrants.Count == 0,
            "Az ingyenes csatlakozás hibás.");
        Check(!inn.TryRecruit(offer.CharacterId, oldRevision, null, out _) && roster.Party.Members.Count == 5 &&
            inn.RecruitmentOffers().All(value => value.Price > 0 && value.RecruitmentGrant is null), "A támogatás ismét felhasználható.");
        Check(roster.Party.CampaignProgression.ConsumedRecruitmentGrants!.SequenceEqual([PartyExpansionMilestone.FifthMember]), "Hibás támogatás fogyott.");
    }
    public static void RejectedRecruitment()
    {
        var (inn, roster) = Fixture(); var offer = inn.RecruitmentOffers()[0];
        var members = roster.Party.Members.ToArray(); var gold = roster.Party.Leader!.Gold;
        var revision = inn.Revision; var offers = inn.RecruitmentOffers();
        Check(!inn.TryRecruit(offer.CharacterId, revision - 1, null, out _) &&
            !inn.TryRecruit(CharacterId.New(), revision, null, out _) &&
            !inn.TryRecruit(offer.CharacterId, revision, roster.Party.Leader.Id, out _), "Érvénytelen felvétel elfogadva.");
        SetField(inn, "_active", false);
        Check(!inn.TryRecruit(offer.CharacterId, revision, null, out _), "Fogadón kívül lehet toborozni.");
        Check(roster.Party.Members.SequenceEqual(members) && roster.Party.Leader.Gold == gold && inn.Revision == revision &&
            inn.RecruitmentOffers().SequenceEqual(offers) && roster.Party.AvailableRecruitmentGrants.Count == 1, "Sikertelen felvétel állapotot módosított.");
    }
    public static void FullPartyReplacement()
    {
        var (inn, roster) = Fixture(members: 5); var offer = inn.RecruitmentOffers()[0];
        var leader = roster.Party.Leader!; leader.SpendGold(leader.Gold);
        Check(offer.RecruitmentGrant is null && offer.Price > 0 && !inn.TryRecruit(offer.CharacterId, inn.Revision, null, out _) &&
            roster.Party.AvailableRecruitmentGrants.Count == 1, "Teli parti ingyenes felvételt engedett.");
        leader.AddGold(10_000); var gold = leader.Gold; var replaced = roster.Party.Members[1];
        Check(inn.TryRecruit(offer.CharacterId, inn.Revision, replaced.Id, out _) && leader.Gold == gold - offer.Price &&
            roster.Party.Members.Count == 5 && !roster.Characters.Contains(replaced) && roster.Party.AvailableRecruitmentGrants.Count == 1,
            "A normál csere támogatást használt vagy rossz összeget fizetett.");
        roster.Remove(roster.Party.Members[1]);
        Check(inn.RecruitmentOffers().All(value => value.RecruitmentGrant == PartyExpansionMilestone.FifthMember), "A felszabadult hely nem használhatja a támogatást.");
    }
    public static void DeferredRecruitment()
    {
        var (inn, roster) = Fixture(); var initial = inn.RecruitmentOffers();
        var next = Controller(roster, 902); next.InitializeRecruitment(6);
        Check(roster.Party.AvailableRecruitmentGrants.Count == 1 && next.RecruitmentOffers().Count == 3 &&
            !next.RecruitmentOffers().Any(value => initial.Any(old => old.CharacterId == value.CharacterId)), "Elveszett a halasztott támogatás.");
        var offer = next.RecruitmentOffers()[1];
        Check(next.TryRecruit(offer.CharacterId, next.Revision, null, out _), "A következő fogadóban nem használható a támogatás.");
        var subsequent = Controller(roster, 203); subsequent.InitializeRecruitment(7);
        Check(subsequent.RecruitmentOffers().All(value => value.RecruitmentGrant is null && value.Price > 0), "Az újabb fogadó visszaadta a támogatást.");
    }
    public static void SpecialRecruitment()
    {
        var special = Character("Történeti"); special.SetSourceNpcDefinitionId("NPC020");
        var (inn, roster) = Fixture(special: special, specialPrice: 4242);
        var offer = inn.RecruitmentOffers().Single(value => value.CharacterId == special.Id);
        Check(offer.Price == 4242 && offer.RecruitmentGrant is null, "Egyedi NPC támogatott ajánlatot kapott.");
        roster.Party.Leader!.AddGold(4242);
        var gold = roster.Party.Leader.Gold;
        Check(inn.TryRecruit(special.Id, inn.Revision, null, out _) && roster.Party.Leader.Gold == gold - 4242 &&
            roster.Party.AvailableRecruitmentGrants.Count == 1, "Egyedi felvétel elfogyasztotta a támogatást.");
        var dismissed = Character("Visszavett");
        var (returnInn, _) = Fixture(special: dismissed, specialPrice: 3200);
        var returned = returnInn.RecruitmentOffers().Single(value => value.CharacterId == dismissed.Id);
        Check(returned.Price == 3200 && returned.RecruitmentGrant is null, "Visszavett társ megkerülte a saját árát.");
    }
    public static void UnlockPresentation()
    {
        var roster = new CharacterRoster(); var leader = Character("Bemutatás"); roster.Add(leader); roster.Select(leader);
        Check(PartyExpansionPresentation.Pending(roster.Party).Count == 0, "Idő előtti történet.");
        var inn = Controller(roster);
        var outcome = Invoke(inn, "CompleteLevelAtInn", 5);
        var completion = (LevelCompletionSnapshot)Invoke(inn, "CreateLevelCompletionSnapshot", 5, outcome);
        Check(completion.PartyExpansions!.Single().Milestone == PartyExpansionMilestone.FifthMember && completion.Survivors.Count == 1,
            "Hiányzó vagy ismételt feloldás.");
        var lines = ConsoleRenderer.BuildLevelCompletionLines(completion);
        Check(lines.Any(line => line.Text.Contains("Aurelios")) && lines.Any(line => line.Text.Contains("legfeljebb 5 fővel")) &&
            lines.Any(line => line.Text.Contains("támogatás")) && lines.FindIndex(line => line.Text.Contains("Teljesítési XP")) <
            lines.FindIndex(line => line.Text.Contains("Új partihely")), "A feloldás nem a jutalomképernyő után következik.");
        Check(roster.Party.TryMarkUnlockPresented(PartyExpansionMilestone.FifthMember) &&
            !roster.Party.TryMarkUnlockPresented(PartyExpansionMilestone.FifthMember) && PartyExpansionPresentation.Pending(roster.Party).Count == 0,
            "A történet többször bemutatható.");
    }
    public static void CompletionBeforeRecruitment()
    {
        var (inn, roster) = Fixture(completed: 4);
        var outcome = Invoke(inn, "CompleteLevelAtInn", 5);
        var completion = (LevelCompletionSnapshot)Invoke(inn, "CreateLevelCompletionSnapshot", 5, outcome);
        inn.InitializeRecruitment(5); var candidate = Candidates(inn)[0]; var xp = candidate.Experience;
        Check(inn.TryRecruit(candidate.Id, inn.Revision, null, out _) && candidate.Experience == xp && completion.Survivors.Count == 4 &&
            completion.Survivors.All(value => value.GainedExperience == Catalog.BaseLevelCompletionExperience * 5), "Az ötödik társ utólag pályajutalmat kapott.");
    }
    public static void InnReplication()
    {
        var (inn, roster) = Fixture(); var snapshot = inn.CreateSnapshot()!;
        var restored = JsonSerializer.Deserialize<InnSnapshot>(JsonSerializer.Serialize(snapshot))!;
        Check(restored.PartyCapacity == 5 && restored.Recruits!.SequenceEqual(snapshot.Recruits!) &&
            restored.RecruitmentGrants!.SequenceEqual([PartyExpansionMilestone.FifthMember]), "A vendég nem ugyanazt az ajánlatot kapja.");
        var offer = snapshot.Recruits![0]; inn.TryRecruit(offer.CharacterId, snapshot.Revision, null, out _);
        var updated = inn.CreateSnapshot()!;
        Check(updated.Revision > snapshot.Revision && updated.PartyCount == 5 && updated.RecruitmentGrants!.Count == 0 &&
            updated.Recruits!.Count == 2 && updated.Transactions.Last().Kind == InnTransactionKind.Recruitment &&
            updated.Transactions.Last().Price == 0, "A sikeres felvétel nem került a host pillanatképébe.");
        var partySnapshot = new GameSession(roster.Party, roster.Party.Leader!).CreateSnapshot(new(6, "Erdő", new Dictionary<CharacterId, Position>()));
        Check(partySnapshot.Party.Count == 5 && partySnapshot.PartyCapacity == 5, "A vendégből kimaradt az ötödik tag.");
    }
    public static void SavedRecruitment()
    {
        var (inn, roster) = Fixture();
        roster.Party.TryMarkUnlockPresented(PartyExpansionMilestone.FifthMember);
        var serializer = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-party-test.json"), Catalog);
        var state = new GameSaveData { MazeLevel = 6, PartyCampaignProgression = roster.Party.CampaignProgression };
        var restoredState = JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(state))!;
        var restored = serializer.Deserialize(serializer.Serialize(roster), restoredState.PartyCampaignProgression);
        Check(restored.Party.Capacity == 5 && restored.Party.Members.Count == 4 && restored.Party.PendingUnlockPresentations.Count == 0 &&
            restored.Party.AvailableRecruitmentGrants.SequenceEqual([PartyExpansionMilestone.FifthMember]), "A halasztott támogatás vagy a bemutatás elveszett.");
        var next = Controller(restored); next.InitializeRecruitment(6);
        var candidate = next.RecruitmentOffers()[0];
        Check(next.TryRecruit(candidate.CharacterId, next.Revision, null, out _), "Visszatöltés után nem vehető fel a támogatott társ.");
        state = new GameSaveData { MazeLevel = 6, PartyCampaignProgression = restored.Party.CampaignProgression };
        var consumed = serializer.Deserialize(serializer.Serialize(restored), state.PartyCampaignProgression);
        consumed.Party.MergeCampaignProgression(restoredState.PartyCampaignProgression);
        Check(consumed.Party.Members.Count == 5 && consumed.Party.Capacity == 5 && consumed.Party.AvailableRecruitmentGrants.Count == 0 &&
            consumed.Party.Members.Any(member => member.Id == candidate.CharacterId), "A régi pillanatkép újra használható támogatást adott vagy elvesztette az ötödik tagot.");
        var after = Controller(consumed); after.InitializeRecruitment(7);
        Check(after.RecruitmentOffers().All(offer => offer.RecruitmentGrant is null), "A következő fogadó visszaadta az elköltött támogatást.");
    }
    public static void RecruitmentDisplay()
    {
        var (inn, roster) = Fixture(); var candidates = Candidates(inn); var offers = inn.RecruitmentOffers();
        var lines = ConsoleRenderer.BuildInnRecruitmentLines(candidates, candidates.ToDictionary(value => value, _ => 0), 0,
            roster.Party.Members, 0, "Esc: a támogatás megmarad", "Próbafogadó", roster.Party.Capacity,
            candidates.Select(value => value.Id).ToHashSet(), 1);
        Check(lines.Any(line => line.Text.Contains("4/5")) && lines.Count(line => line.Text.Contains("TÁMOGATÁSSAL INGYEN")) == 3 &&
            lines.Any(line => line.Text.Contains("1 támogatás")) && lines.Any(line => line.Text.Contains("Esc")), "A kedvezmény nincs világosan megkülönböztetve.");
    }
    public static void CampaignLayouts()
    {
        var members = Catalog.CharacterClasses.Take(5).Select((value, index) => Character($"Kampány{index}", 8, value.Id)).ToArray();
        var formation = PartyFormationRules.CreateDefault(members.Select(value => value.Id), members[0].Id);
        var generate = typeof(Game).GetMethod("GenerateDungeonLevel", BindingFlags.NonPublic | BindingFlags.Instance)!;
        for (var level = 6; level <= 8; level++)
        for (var seed = 1; seed <= 2; seed++)
        {
            var game = (Game)RuntimeHelpers.GetUninitializedObject(typeof(Game));
            SetField(game, "_gameData", Catalog); SetField(game, "_random", new Random(level * 100 + seed));
            SetField(game, "_difficultyLevel", level);
            var configuration = MazeLevelConfigurations.Get(level);
            var layout = ForestLevelGraphOverrideBridge.Apply(configuration,
                new FileForestLevelGraphSource(Path.Combine(AppContext.BaseDirectory, "ForestLevelGraphs")));
            var dungeon = (DungeonLevel)generate.Invoke(game, [configuration, layout])!;
            foreach (var area in dungeon.Areas)
            {
                var maze = area.Maze;
                var room = maze.StartingRoom!;
                var canPlace = false;
                for (var y = room.TopLeft.Y; y < room.TopLeft.Y + room.Height && !canPlace; y++)
                for (var x = room.TopLeft.X; x < room.TopLeft.X + room.Width && !canPlace; x++)
                foreach (var facing in Enum.GetValues<Direction>())
                {
                    var positions = PartyFormationRules.Positions(formation with { Facing = facing }, members[0].Id, new(x, y));
                    if (!positions.Values.All(maze.IsWalkable) || positions.Values.Any(position => maze.GetObjectAt(position) is not null)) continue;
                    Check(positions.Count == 5 && positions.Values.Distinct().Count() == 5, "Ütköző kampánybelépés.");
                    Check(positions.Values.All(position => TacticalDistance.IsWithin(positions[members[0].Id], position)), "A harmadik sor kimarad a harci sugárból.");
                    canPlace = true; break;
                }
                Check(canPlace, $"{level}. pálya / {area.Id}: nincs hely az ötfős induló alakzatnak.");
            }
        }
    }
}

internal static class RecruitmentTestListExtensions
{
    public static int FindIndex<T>(this IReadOnlyList<T> values, Func<T, bool> predicate) =>
        Enumerable.Range(0, values.Count).FirstOrDefault(index => predicate(values[index]), -1);
}