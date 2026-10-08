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
        ("Élesben az 5. és 8. teljesített főpálya oldja fel az ötödik és hatodik helyet", CurrentRules),
        ("A harmadik ütemben tárolt és a régi mentésből migrált hatos jogosultság azonnal érvényesül", LegacyEntitlement),
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
        ("A 6–8. kampánypálya generált területein elhelyezhető az ötös menetoszlop", CampaignLayouts),
        ("A 8. főpálya jutalma után nyílik a hatodik hely, történet és ingyenes felvétel", SixthCompletion),
        ("A két halasztott támogatás külön fogy a két új hely feltöltésekor", TwoDeferredGrants),
        ("A hatos parti mentése megőrzi a széles formát és a két elköltött támogatást", SixthSave),
        ("A vendég hat tagot, széles formát és a második támogatás fogadói állapotát kapja", SixthReplication),
        ("Az F öt és hat taggal körbejárja a két nagy formát, az üres hely megmarad", WideEditor),
        ("Teli hatos parti és elavult ajánlat nem használhatja újra a második támogatást", SixthFullParty),
        ("A 9–10. kampánypálya generált területein mindkét nagy alakzat elhelyezhető", SixthCampaignLayouts),
        ("A támogatott toborzás teljes leírása tördelve elfér a fogadói menüben", MenuDescriptionLayout),
        ("Toborzás után a panel lapozás nélkül megjeleníti az új partitagot", RecruitmentPanelRefresh),
        ("A fogadó alapellátása a feloldott kapacitással nő, a prémiumkészlet változatlan", SupplyScaling),
        ("A tesztgenerátor 4/5/6 tagot és a választott járható induló alakzatot készíti", DeveloperPartyLayouts),
        ("A tesztparti nem old fel kampányjutalmat, és normál visszaállításkor megszűnik az eltérés", DeveloperCapacity),
        ("A harci tesztparti mentése hat tagot őriz meg, a normál kampány korlátja megmarad", DeveloperSave),
        ("Az érvénytelen tesztlétszám és túl kicsi alakzat elutasítható", DeveloperOptions)
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
        party.RecordCampaignLevelCompletion(7);
        Check(party.Capacity == 5 &&
            FormationEditor.AvailableShapes(party.Capacity, 5).SequenceEqual([PartyFormationShape.Column2x3]) &&
            !party.TryMarkUnlockPresented(PartyExpansionMilestone.SixthMember), "Korai hatodik hely vagy széles forma.");
        party.RecordCampaignLevelCompletion(8);
        Check(party.Capacity == 6 && party.UnlockedCapacity == 6 &&
            party.AvailableRecruitmentGrants.SequenceEqual([PartyExpansionMilestone.FifthMember, PartyExpansionMilestone.SixthMember]) &&
            party.PendingUnlockPresentations.Count == 2, "A hatodik hely vagy második támogatás nem nyílt meg.");
        Check(FormationEditor.AvailableShapes(party.Capacity, 5).SequenceEqual([PartyFormationShape.Column2x3, PartyFormationShape.Wide3x2]),
            "A széles forma a feloldás után sem érhető el.");
        party.RecordCampaignLevelCompletion(22);
        party.RecordCampaignLevelCompletion(4);
        Check(party.Capacity == 6 && party.AvailableRecruitmentGrants.Count == 2, "Ismételt teljesítés módosította a jutalmakat.");
    }
    public static void LegacyEntitlement()
    {
        var migrated = GameSaveFormat.MigrateToCurrent(new GameSaveData { Version = 35, MazeLevel = 9 });
        var party = new Party(PartyCapacityRules.Current with { MaximumEnabledCapacity = 5 });
        party.MergeCampaignProgression(migrated.PartyCampaignProgression);
        Check(party.Capacity == 5 && party.UnlockedCapacity == 6, "A régi jogosultság elveszett.");
        party.TryMarkUnlockPresented(PartyExpansionMilestone.FifthMember);
        var future = new Party();
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
    private static (InnController Inn, CharacterRoster Roster) FifthParty()
    {
        var (inn, roster) = Fixture();
        var offer = inn.RecruitmentOffers()[0];
        Check(inn.TryRecruit(offer.CharacterId, inn.Revision, null, out _), "Az ötödik társ nem csatlakozott.");
        roster.Party.TryMarkUnlockPresented(PartyExpansionMilestone.FifthMember);
        roster.Party.RecordCampaignLevelCompletion(7);
        return (inn, roster);
    }

    private static (InnController Inn, CharacterRoster Roster) SixthInn()
    {
        var (inn, roster) = FifthParty();
        Invoke(inn, "CompleteLevelAtInn", 8);
        inn.InitializeRecruitment(8);
        return (inn, roster);
    }

    public static void SixthCompletion()
    {
        var (inn, roster) = FifthParty();
        Check(roster.Party.Capacity == 5 && roster.Party.AvailableRecruitmentGrants.Count == 0,
            "A 8. pálya lezárása előtt feloldódott a hatodik hely.");
        var outcome = Invoke(inn, "CompleteLevelAtInn", 8);
        var completion = (LevelCompletionSnapshot)Invoke(inn, "CreateLevelCompletionSnapshot", 8, outcome);
        var expansion = completion.PartyExpansions!.Single();
        Check(completion.Survivors.Count == 5 &&
            completion.Survivors.All(value => value.GainedExperience == Catalog.BaseLevelCompletionExperience * 8) &&
            expansion.Milestone == PartyExpansionMilestone.SixthMember && roster.Party.Capacity == 6,
            "Hibás pályajutalom, feloldás vagy ismételt történet.");
        var lines = ConsoleRenderer.BuildLevelCompletionLines(completion);
        Check(lines.Any(line => line.Text.Contains("Aurelios")) && lines.Any(line => line.Text.Contains("6 fős")) &&
            lines.Any(line => line.Text.Contains("3×2")) && lines.Any(line => line.Text.Contains("F-fel")) &&
            lines.FindIndex(line => line.Text.Contains("Teljesítési XP")) < lines.FindIndex(line => line.Text.Contains("Új partihely")),
            "A hatodik hely története vagy a formaváltás magyarázata hiányzik.");
        Check(roster.Party.TryMarkUnlockPresented(PartyExpansionMilestone.SixthMember) &&
            !roster.Party.TryMarkUnlockPresented(PartyExpansionMilestone.SixthMember), "A történet többször elfogadható.");
        inn.InitializeRecruitment(8);
        var offers = inn.RecruitmentOffers();
        Check(offers.Count == 3 && offers.All(offer => offer.Price == 0 &&
            offer.RecruitmentGrant == PartyExpansionMilestone.SixthMember), "A második támogatás nem használható.");
        var candidate = Candidates(inn)[0]; var xp = candidate.Experience; var gold = roster.Party.Leader!.Gold;
        Check(inn.TryRecruit(candidate.Id, inn.Revision, null, out _) && candidate.Experience == xp &&
            roster.Party.Members.Count == 6 && roster.Party.Leader.Gold == gold &&
            roster.Party.AvailableRecruitmentGrants.Count == 0, "A hatodik társ hibás áron vagy visszamenőleges XP-vel csatlakozott.");
        Invoke(inn, "CompleteLevelAtInn", 8);
        Check(PartyExpansionPresentation.Pending(roster.Party).Count == 0 && roster.Party.AvailableRecruitmentGrants.Count == 0,
            "Ismételt teljesítés visszaadta a történetet vagy a támogatást.");
    }

    public static void TwoDeferredGrants()
    {
        var (inn, roster) = Fixture(completed: 8);
        var offers = inn.RecruitmentOffers();
        Check(roster.Party.AvailableRecruitmentGrants.Count == 2 &&
            offers.All(offer => offer.RecruitmentGrant == PartyExpansionMilestone.FifthMember), "Hibás támogatássorrend.");
        Check(inn.TryRecruit(offers[0].CharacterId, inn.Revision, null, out _), "Az első halasztott támogatás nem használható.");
        Check(!inn.TryRecruit(offers[1].CharacterId, inn.Revision - 1, null, out _) &&
            roster.Party.AvailableRecruitmentGrants.SequenceEqual([PartyExpansionMilestone.SixthMember]),
            "Elavult ajánlat elköltötte a második támogatást.");
        var next = Controller(roster, 294); next.InitializeRecruitment(9);
        Check(next.RecruitmentOffers().Count == 3 &&
            next.RecruitmentOffers().All(offer => offer.RecruitmentGrant == PartyExpansionMilestone.SixthMember),
            "A halasztott második támogatás elveszett.");
        Check(next.TryRecruit(next.RecruitmentOffers()[0].CharacterId, next.Revision, null, out _) &&
            roster.Party.Members.Count == 6 && roster.Party.AvailableRecruitmentGrants.Count == 0 &&
            roster.Party.CampaignProgression.ConsumedRecruitmentGrants!.Count == 2,
            "A két támogatás nem külön fogyott el.");
    }

    public static void SixthSave()
    {
        var (inn, roster) = SixthInn();
        var deferred = roster.Party.CampaignProgression;
        roster.Party.TryMarkUnlockPresented(PartyExpansionMilestone.SixthMember);
        Check(inn.TryRecruit(inn.RecruitmentOffers()[0].CharacterId, inn.Revision, null, out _), "Hatodik tag hiányzik.");
        var formation = PartyFormationRules.WithShape(
            PartyFormationRules.CreateDefault(roster.Party.Members.Select(member => member.Id), roster.Party.Leader!.Id, Direction.Left),
            PartyFormationShape.Wide3x2);
        var directory = Path.Combine(Path.GetTempPath(), $"party-sixth-{Guid.NewGuid():N}");
        try
        {
            var characters = new CharacterSaveService(Path.Combine(directory, "characters.json"), Catalog);
            var saves = new GameSaveService(directory, characters);
            var path = saves.Save(new GameSaveData { MainCharacterName = roster.Party.Leader.Name, MazeLevel = 9,
                Formation = formation }, roster);
            var loaded = saves.Load(path);
            loaded.Roster.Party.MergeCampaignProgression(deferred);
            Check(loaded.Roster.Party.Capacity == 6 && loaded.Roster.Party.Members.Select(member => member.Id)
                .SequenceEqual(roster.Party.Members.Select(member => member.Id)) &&
                loaded.Roster.Party.PendingUnlockPresentations.Count == 0 && loaded.Roster.Party.AvailableRecruitmentGrants.Count == 0 &&
                loaded.State.Formation!.Shape == PartyFormationShape.Wide3x2 && loaded.State.Formation.Facing == Direction.Left &&
                loaded.State.Formation.Slots.SequenceEqual(formation.Slots), "Hatodik tag, széles forma vagy egyszeri jutalom elveszett.");
        }
        finally
        {
            Check(Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase),
                "A tesztkönyvtár az ideiglenes gyökéren kívülre mutat.");
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    public static void SixthReplication()
    {
        var (inn, roster) = SixthInn();
        var before = JsonSerializer.Deserialize<InnSnapshot>(JsonSerializer.Serialize(inn.CreateSnapshot()))!;
        Check(before.PartyCapacity == 6 && before.PartyCount == 5 &&
            before.RecruitmentGrants!.SequenceEqual([PartyExpansionMilestone.SixthMember]) &&
            before.Recruits!.All(offer => offer.RecruitmentGrant == PartyExpansionMilestone.SixthMember),
            "A vendég nem látja a második támogatást vagy az új korlátot.");
        Check(inn.TryRecruit(before.Recruits![0].CharacterId, before.Revision, null, out _), "Replikált ajánlat nem vehető fel.");
        var formation = PartyFormationRules.WithShape(
            PartyFormationRules.CreateDefault(roster.Party.Members.Select(member => member.Id), roster.Party.Leader!.Id),
            PartyFormationShape.Wide3x2);
        var positions = PartyFormationRules.Positions(formation, roster.Party.Leader.Id, new(8, 8));
        var snapshot = new GameSession(roster.Party, roster.Party.Leader).CreateSnapshot(new(9, "Haditábor", positions))
            with { Formation = formation, Inn = inn.CreateSnapshot() };
        var restored = JsonSerializer.Deserialize<SessionSnapshot>(JsonSerializer.Serialize(snapshot))!;
        Check(restored.PartyCapacity == 6 && restored.Party.Count == 6 && restored.Party.All(member => member.Position is not null) &&
            restored.Formation!.Shape == PartyFormationShape.Wide3x2 && restored.Formation.Slots.SequenceEqual(formation.Slots) &&
            restored.Inn!.RecruitmentGrants!.Count == 0 && restored.Inn.PartyCount == 6,
            "A vendégből hiányzik a hatodik tag, az alakzat vagy a felvétel eredménye.");
    }

    public static void WideEditor()
    {
        foreach (var count in new[] { 5, 6 })
        {
            var members = Catalog.CharacterClasses.Take(count).Select((value, index) => Character($"Széles{index}", 8, value.Id)).ToArray();
            var column = PartyFormationRules.CreateDefault(members.Select(member => member.Id), members[0].Id, Direction.Down);
            var wide = FormationEditor.CycleShape(column, PartyCapacityRules.Current.Capacity(8), count);
            Check(wide.Shape == PartyFormationShape.Wide3x2 && wide.Facing == Direction.Down &&
                wide.Slots.OfType<CharacterId>().ToHashSet().SetEquals(members.Select(member => member.Id)) &&
                wide.Slots.Count(id => id is null) == 6 - count && FormationEditor.ShapeChangeHint(6, count) == string.Empty,
                "F-fel nem választható a széles forma, vagy elveszett tag/üres hely.");
            if (count == 5)
            {
                var slots = wide.Slots.ToArray();
                var gap = Array.IndexOf(slots, null);
                (slots[gap], slots[1]) = (slots[1], slots[gap]);
                wide = PartyFormationRules.WithSlots(wide, slots);
                Check(PartyFormationRules.Normalize(wide, members.Select(member => member.Id), members[0].Id).Slots.SequenceEqual(slots),
                    "A széles forma szándékos üres helye elmozdult.");
            }
            Check(FormationEditor.CycleShape(wide, 6, count).Shape == PartyFormationShape.Column2x3,
                "Az F nem vált vissza a menetoszlopra.");
        }
    }

    public static void SixthFullParty()
    {
        var (inn, roster) = SixthInn();
        var offer = inn.RecruitmentOffers()[0]; var revision = inn.Revision;
        Check(inn.TryRecruit(offer.CharacterId, revision, null, out _), "Nem telt meg a hatos parti.");
        var gold = roster.Party.Leader!.Gold;
        Check(!inn.TryRecruit(offer.CharacterId, revision, null, out _) &&
            !inn.TryRecruit(inn.RecruitmentOffers()[0].CharacterId, inn.Revision, null, out _) &&
            roster.Party.Members.Count == 6 && roster.Party.Leader.Gold == gold &&
            roster.Party.AvailableRecruitmentGrants.Count == 0, "Hetedik tag vagy ismételt ingyenes felvétel sikerült.");
    }

    public static void MenuDescriptionLayout()
    {
        var (inn, _) = SixthInn();
        var description = (string)Invoke(inn, "RecruitmentStatus");
        var options = new InnMenuOptionSnapshot[]
        {
            new(InnMenuOptionKind.Recruit, "Zsoldosok toborzása — támogatással", description),
            new(InnMenuOptionKind.Leave, "Indulás", "A parti elhagyja a fogadót.")
        };
        foreach (var guest in new[] { false, true })
        {
            var lines = ConsoleRenderer.BuildInnMenuLines(5, 2000, options, 0, "", guest, partyCapacity: 6);
            var descriptionLines = lines.Where(line => line.Text.StartsWith("     ")).TakeWhile(line =>
                !line.Text.Contains("A parti elhagyja")).ToArray();
            Check(descriptionLines.Length >= 2 && descriptionLines.All(line =>
                BattleCommandPanel.DisplayWidth(line.Text) <= ConsoleRenderer.InnMenuFrameWidth -
                    2 * WindowFrameCatalog.ContentPadding(WindowFrameConfiguration.For(FramedWindow.Inn))),
                "A leírás kilóg a helyi vagy vendégmenüből.");
            Check(string.Join(" ", descriptionLines.Select(line => line.Text.Trim())) == description,
                "A tördelés elhagyta a támogatás vagy halasztás leírását.");
            var split = typeof(ConsoleRenderer).GetMethod("SplitMenuDescriptionLines", BindingFlags.NonPublic | BindingFlags.Static)!;
            var rows = (string[])split.Invoke(null, [description])!;
            Check(rows.SequenceEqual(descriptionLines.Select(line => line.Text.TrimStart())),
                "A részleges kijelölésfrissítés más sorokat használ.");
        }
    }

    public static void RecruitmentPanelRefresh()
    {
        foreach (var supported in new[] { true, false })
        {
            var (inn, roster) = SixthInn();
            var renderer = (ConsoleRenderer)inn.GetType().GetField("_renderer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(inn)!;
            if (!supported)
            {
                var dummy = Character("Korábbi támogatás");
                Check(roster.Party.TryAddWithRecruitmentGrant(dummy, PartyExpansionMilestone.SixthMember), "Hiányzó támogatás.");
                roster.Party.Remove(dummy);
                roster.Party.Leader!.AddGold(100_000);
            }
            var output = new StringWriter();
            var original = Console.Out;
            try
            {
                Console.SetOut(output);
                SetField(renderer.CharacterSheet, "_displayedCharacter", roster.Party.Members[1]);
                Invoke(renderer.CharacterSheet, "DrawPartyStatusRows", roster.Party.Members[1]);
                var displayed = renderer.CharacterSheet.DisplayedCharacter;
                var offer = inn.RecruitmentOffers()[0];
                Check(inn.TryRecruit(offer.CharacterId, inn.Revision, null, out _), "A tesztfelvétel sikertelen.");
                var rows = (System.Collections.IDictionary)renderer.CharacterSheet.GetType()
                    .GetField("_lastPartyStatusRows", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(renderer.CharacterSheet)!;
                var last = rows[ConsoleRenderer.CharacterSheetRenderer.CharacterSheetPartyMembersStartLine + 5]!;
                var status = (PartyStatusLine)last.GetType().GetProperty("Status")!.GetValue(last)!;
                var recruit = roster.Party.Members.Single(member => member.Id == offer.CharacterId);
                Check(status.Identity == CharacterSheetPanel.BuildPartyStatus(recruit, false, false).Identity &&
                    renderer.CharacterSheet.DisplayedCharacter == displayed,
                    "Az új társ státuszsora nem frissült azonnal, vagy megváltozott a megjelenített karakter.");
            }
            finally { Console.SetOut(original); output.Dispose(); }
        }
    }

    private static Dictionary<string, int> StockCounts(InnController inn, string method, params object[] arguments)
    {
        var stock = (System.Collections.IEnumerable)inn.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(value => value.Name == method && value.GetParameters().Length == arguments.Length).Invoke(inn, arguments)!;
        return stock.Cast<object>().GroupBy(offer => ((IItemDefinition)offer.GetType().GetProperty("Item")!.GetValue(offer)!).Id)
            .ToDictionary(group => group.Key, group => group.Sum(offer =>
                (int)offer.GetType().GetProperty("StockCount")!.GetValue(offer)!));
    }

    public static void SupplyScaling()
    {
        var baselineRoster = Fixture(completed: 4).Roster;
        var baselineMarket = StockCounts(Controller(baselineRoster, 471), "CreateMerchantStock", 8);
        var baselineMedicine = StockCounts(Controller(baselineRoster, 471), "CreateWitcherStock", 8);
        var baselinePremium = StockCounts(Controller(baselineRoster, 471), "CreateMerchantStock", 8, 8, 1.0, true, true, true);
        foreach (var (completed, capacity) in new[] { (5, 5), (8, 6) })
        {
            var roster = Fixture(completed: completed).Roster; // Four actual members, unlocked capacity determines supply.
            var market = StockCounts(Controller(roster, 471), "CreateMerchantStock", 8);
            foreach (var (id, count) in new[] { ("T001", 4), ("T002", 8), ("T004", 2), ("T005", 2) })
                Check(market[id] - baselineMarket[id] == (count * capacity + 3) / 4 - count, "Hibás élelem/víz szorzó.");
            Check(baselineMarket.Where(pair => pair.Key is not ("T001" or "T002" or "T004" or "T005"))
                .All(pair => market.GetValueOrDefault(pair.Key) == pair.Value), "Felszerelés vagy ritka készlet is megsokszorozódott.");
            var medicines = StockCounts(Controller(roster, 471), "CreateWitcherStock", 8);
            foreach (var pair in baselineMedicine)
                Check(medicines[pair.Key] == (pair.Key is "T011" or "T012" or "T014" or "T018" or "T019"
                    ? (pair.Value * capacity + 3) / 4 : pair.Value), "Hibás alapgyógyszer- vagy prémiumital-mennyiség.");
            var premium = StockCounts(Controller(roster, 471), "CreateMerchantStock", 8, 8, 1.0, true, true, true);
            Check(premium.Count == baselinePremium.Count && baselinePremium.All(pair => premium[pair.Key] == pair.Value),
                "A titkos raktár készlete is nőtt.");
        }
    }

    public static void DeveloperPartyLayouts()
    {
        foreach (var leaderClass in Catalog.CharacterClasses)
        foreach (var size in new[] { 4, 5, 6 })
        foreach (var shape in Enum.GetValues<PartyFormationShape>().Where(shape => size == 4 || shape != PartyFormationShape.Block2x2))
        {
            var leader = Character($"Teszt{size}{shape}{leaderClass.Id}", 8, leaderClass.Id);
            var options = new DeveloperBattleTestOptions(8, 2, 3, size, shape);
            var companions = DeveloperBattleTestPartyBuilder.CreateCompanions(Catalog, options, leader, [leader.Name],
                new Random(options.RandomSeed));
            var members = companions.Prepend(leader).ToArray();
            Check(members.Length == size && members.Select(member => member.CharacterClass.Id).Distinct().Count() == size &&
                companions.All(member => member.Level == 8 && member.ActiveWeapons.Any(weapon => weapon is not null)),
                "A tesztparti létszáma, kasztja vagy szinthez igazított felszerelése hibás.");
            var scenario = DeveloperBattleTestScenarioBuilder.Create(ConsoleRenderer.PlayfieldWidth, ConsoleRenderer.PlayfieldHeight,
                options, Catalog.Enemies, new Random(options.RandomSeed), 30);
            var formation = PartyFormationRules.WithShape(PartyFormationRules.CreateDefault(members.Select(member => member.Id),
                leader.Id, Direction.Up), shape);
            var positions = PartyFormationRules.Positions(formation, leader.Id, scenario.LeaderPosition);
            Check(positions.Count == size && positions.Values.Distinct().Count() == size &&
                positions.Values.All(position => scenario.Maze.Rooms.Single().Contains(position) &&
                    scenario.Maze.IsWalkable(position) && scenario.Maze.GetObjectAt(position) is null),
                "Az induló tesztalakzat falba vagy foglalt mezőre kerül.");
            var baseline = DeveloperBattleTestScenarioBuilder.Create(ConsoleRenderer.PlayfieldWidth, ConsoleRenderer.PlayfieldHeight,
                new(8, 2, 3), Catalog.Enemies, new Random(options.RandomSeed), 30);
            Check(scenario.Maze.Enemies.Select(enemy => (enemy.Position, enemy.Name, enemy.CurrentHitPoints))
                .SequenceEqual(baseline.Maze.Enemies.Select(enemy => (enemy.Position, enemy.Name, enemy.CurrentHitPoints))),
                "Azonos mag mellett a parti létszáma megváltoztatta az ellenfeleket.");
        }
    }

    public static void DeveloperCapacity()
    {
        var leader = Character("Tesztvezér");
        var companions = Catalog.CharacterClasses.Where(value => value.Id != leader.CharacterClass.Id)
            .Select(value => Character(value.Id, 8, value.Id)).ToArray();
        var party = new Party(); party.SetLeader(leader);
        party.RestoreForDeveloperTest(leader, companions, 6);
        Check(party.Capacity == 6 && party.Members.Count == 6 && party.UnlockedCapacity == 4 &&
            party.CampaignProgression.HighestCompletedCampaignLevel == 0 && party.AvailableRecruitmentGrants.Count == 0 &&
            !party.Add(Character("Hetedik")), "A tesztparti kampányjutalmat adott vagy túlcsordult.");
        party.Restore(leader, companions);
        Check(party.Capacity == 4 && party.Members.Count == 4, "Normál visszaállításkor megmaradt a tesztkapacitás.");
        party.RestoreForDeveloperTest(leader, companions.Take(3), 4);
        party.StartNewCampaign(); party.RecordCampaignLevelCompletion(8);
        Check(party.Capacity == 6, "Új kampányban megmaradt a négyfős tesztkapacitás.");
    }

    public static void DeveloperSave()
    {
        var roster = new CharacterRoster(); var leader = Character("Mentett teszt"); roster.Add(leader); roster.Select(leader);
        var options = new DeveloperBattleTestOptions(8, 2, 3, 6, PartyFormationShape.Wide3x2);
        var companions = DeveloperBattleTestPartyBuilder.CreateCompanions(Catalog, options, leader, [leader.Name], new(42));
        foreach (var member in companions) roster.Add(member);
        roster.Party.RestoreForDeveloperTest(leader, companions, 6);
        var directory = Path.Combine(Path.GetTempPath(), $"party-dev-{Guid.NewGuid():N}");
        try
        {
            var characters = new CharacterSaveService(Path.Combine(directory, "characters.json"), Catalog);
            var saves = new GameSaveService(directory, characters);
            var path = saves.Save(new GameSaveData { MainCharacterName = leader.Name, MazeLevel = 8,
                LocationId = DeveloperBattleTestScenarioBuilder.LocationId }, roster);
            var loaded = saves.Load(path);
            Check(loaded.Roster.Party.Members.Count == 6 && loaded.Roster.Party.Capacity == 6 &&
                loaded.Roster.Party.UnlockedCapacity == 4 && loaded.Roster.Party.AvailableRecruitmentGrants.Count == 0,
                "Tesztmentésből elvesztek tagok vagy kampányjutalom keletkezett.");
            var normal = characters.Deserialize(characters.Serialize(roster), roster.Party.CampaignProgression);
            Check(normal.Party.Members.Count == 4 && normal.Party.Capacity == 4, "Normál karakterbetöltés megkerülte a kampánykorlátot.");
        }
        finally
        {
            Check(Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase),
                "A tesztkönyvtár az ideiglenes gyökéren kívülre mutat.");
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    public static void DeveloperOptions()
    {
        foreach (var options in new DeveloperBattleTestOptions[] { new(8, 2, 3, 3), new(8, 2, 3, 7),
            new(8, 2, 3, 6, PartyFormationShape.Block2x2), new(8, 2, 3, 5, (PartyFormationShape)123), new(8, 2, 3, RandomSeed: -1) })
        {
            var rejected = false;
            try { options.Validate(30); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Érvénytelen tesztbeállítás elfogadva.");
        }
    }

    public static void CampaignLayouts() => CheckCampaignLayouts(6, 8, 5, [PartyFormationShape.Column2x3]);
    public static void SixthCampaignLayouts() => CheckCampaignLayouts(9, 10, 6,
        [PartyFormationShape.Column2x3, PartyFormationShape.Wide3x2]);

    private static void CheckCampaignLayouts(int firstLevel, int lastLevel, int memberCount, PartyFormationShape[] shapes)
    {
        var members = Catalog.CharacterClasses.Take(memberCount).Select((value, index) => Character($"Kampány{index}", 8, value.Id)).ToArray();
        var generate = typeof(Game).GetMethod("GenerateDungeonLevel", BindingFlags.NonPublic | BindingFlags.Instance)!;
        for (var level = firstLevel; level <= lastLevel; level++)
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
            foreach (var shape in shapes)
            {
                var formation = PartyFormationRules.WithShape(
                    PartyFormationRules.CreateDefault(members.Select(value => value.Id), members[0].Id), shape);
                var maze = area.Maze;
                var room = maze.StartingRoom!;
                var canPlace = false;
                for (var y = room.TopLeft.Y; y < room.TopLeft.Y + room.Height && !canPlace; y++)
                for (var x = room.TopLeft.X; x < room.TopLeft.X + room.Width && !canPlace; x++)
                foreach (var facing in Enum.GetValues<Direction>())
                {
                    var positions = PartyFormationRules.Positions(formation with { Facing = facing }, members[0].Id, new(x, y));
                    if (!positions.Values.All(maze.IsWalkable) || positions.Values.Any(position => maze.GetObjectAt(position) is not null)) continue;
                    Check(positions.Count == memberCount && positions.Values.Distinct().Count() == memberCount, "Ütköző kampánybelépés.");
                    Check(positions.Values.All(position => TacticalDistance.IsWithin(positions[members[0].Id], position)), "A harmadik sor kimarad a harci sugárból.");
                    canPlace = true; break;
                }
                Check(canPlace, $"{level}. pálya / {area.Id}: nincs hely a {memberCount} fős {shape} induló alakzatnak.");
            }
        }
    }
}

internal static class RecruitmentTestListExtensions
{
    public static int FindIndex<T>(this IReadOnlyList<T> values, Func<T, bool> predicate) =>
        Enumerable.Range(0, values.Count).FirstOrDefault(index => predicate(values[index]), -1);
}