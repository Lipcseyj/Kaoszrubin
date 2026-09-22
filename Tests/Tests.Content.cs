internal static partial class Program
{
    static void CreatureQuotesLoadAndResolveForMainMenu()
    {
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory,
            CsvGameDataLoader.GameDataFileName));

        Assert(catalog.CreatureQuotes.Count == 82 &&
               catalog.CreatureQuotes.Count(quote => quote.Kind == CreatureQuoteKind.CharacterClass) == 6 &&
               catalog.CreatureQuotes.Count(quote => quote.Kind == CreatureQuoteKind.Enemy) == 76,
            "A #Lény mondatok szekció nem minden osztály- és szörnymondatot olvasott be.");
        Assert(catalog.CreatureQuotes.Single(quote => quote.Id == "CS001").CreatureId == "C001" &&
               catalog.CreatureQuotes.Single(quote => quote.Id == "ES001").CreatureId == "E001" &&
               catalog.CreatureQuotes.All(quote => quote.Quotes.Count == 3 &&
                   quote.Quotes.All(text => !string.IsNullOrWhiteSpace(text))),
            "A CSxxx/ESxxx hivatkozás vagy a három lénymondat feloldása hibás.");
        var creature = MainMenuCreaturePanel.ChooseCreature(catalog, new Random(2161));
        Assert(creature is not null && creature.Portrait.Lines.Count == 5 &&
               creature.Quotes.Quotes.Contains(creature.Quotes.Quotes[0]),
            "A főmenü nem tudott a betöltött mondatokhoz portréval rendelkező lényt választani.");
    }

    static void RaceTraitsAreLoadedFromData()
    {
        var dataPath = Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName);
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
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        foreach (var school in Enum.GetValues<SpellSchool>())
            for (var level = 1; level <= 5; level++)
                Assert(catalog.GetSpells(school, level).Count == (level <= 3 ? 6 : 5),
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
        var invalid = command with
        {
            CommandId = 2,
            ExpectedInventoryRevision = source.InventoryRevision,
            BackpackIndex = nonConsumableIndex
        };
        Assert(!InventoryDistributionService.TryExecute(party, invalid, out _, out error) &&
               error.Contains("elfogyasztható", StringComparison.OrdinalIgnoreCase),
            "A szétosztás elfogadott egy nem elfogyasztható tárgyat.");
    }

    static void ConsumableStackHalfTransfersToFollower()
    {
        var source = CreateCharacter("Átadó");
        var follower = CreateCharacter("Követő");
        var ration = new MiscItemDefinition("I-FOLLOWER-GIFT", "Útravaló", "Teszt", 1,
            ConsumableEffect.Food, 25);
        for (var index = 0; index < 5; index++)
            Assert(source.AddToBackpack(ration), "A követőnek szánt tesztköteg nem fért el.");
        Assert(follower.AddToBackpack(ration), "A követő célstackje nem hozható létre.");
        var command = new GiveFollowerStackCommand(PlayerId.New(), 1, source.Id, source.InventoryRevision, 0,
            follower.Id, follower.InventoryRevision);
        Assert(FollowerStackTransferService.TryExecute(source, follower, command, out var result, out var error), error);
        Assert(source.GetInventoryItemQuantity(InventorySlotKind.Backpack, 0) == 3 &&
               follower.GetInventoryItemQuantity(InventorySlotKind.Backpack, 0) == 3 &&
               result.TransferredQuantity == 2 && result.RemainingQuantity == 3,
            "Az ötdarabos köteg kisebb fele nem 3/2 arányban került a követőhöz vagy nem stackelődött.");

        var trinket = new MiscItemDefinition("I-FOLLOWER-NONCONSUMABLE", "Dísztárgy", "Teszt", 1);
        Assert(source.AddToBackpack(trinket) && source.AddToBackpack(trinket),
            "A nem fogyasztható tesztköteg nem fért el.");
        var trinketIndex = Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount)
            .Single(index => source.GetInventoryItem(InventorySlotKind.Backpack, index)?.Id == trinket.Id);
        var invalid = command with
        {
            CommandId = 2,
            ExpectedInventoryRevision = source.InventoryRevision,
            BackpackIndex = trinketIndex,
            ExpectedFollowerInventoryRevision = follower.InventoryRevision
        };
        Assert(!FollowerStackTransferService.TryExecute(source, follower, invalid, out _, out error) &&
               error.Contains("elfogyasztható", StringComparison.OrdinalIgnoreCase),
            "A követőnek átadás elfogadott egy nem fogyasztható tárgyat.");
    }

    static void ClassResourceGrowthLoadsFromCsv()
    {
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        Assert(catalog.GetCharacterResourceGrowth(CharacterClassIds.Barbár).AdjustVitality(5) == 10 &&
               catalog.GetCharacterResourceGrowth(CharacterClassIds.Harcos).AdjustVitality(5) == 8 &&
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
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        Assert(catalog.Npcs.Count == 21 && catalog.NpcEncounters.Count == 29 &&
               Enumerable.Range(1, MazeLevelConfigurations.FinalLevel).All(level =>
                   catalog.NpcEncounters.Any(encounter => encounter.MazeLevel == level)),
            "Az NPC-definíciók vagy valamelyik pálya találkozása hiányzik.");
        Assert(catalog.NpcDialogues.Count == 69,
            $"Az NPC-párbeszédek száma hibás: várt 69, tényleges {catalog.NpcDialogues.Count}.");
        Assert(catalog.NpcStoryChoices.Count == 67,
            $"Az NPC történeti választások száma hibás: várt 67, tényleges {catalog.NpcStoryChoices.Count}.");
        Assert(catalog.Quests.Count == 41,
            $"Az NPC-küldetések száma hibás: várt 41, tényleges {catalog.Quests.Count}.");
        foreach (var (type, expected) in new[]
        {
        (typeof(QuestObjective.CollectItem), 8), (typeof(QuestObjective.KillEnemy), 18), (typeof(QuestObjective.KillEnemyWithTraits), 1),
        (typeof(QuestObjective.ExploreLocation), 5), (typeof(QuestObjective.DisarmTraps), 3), (typeof(QuestObjective.OpenChests), 4), (typeof(QuestObjective.EscortNpc), 1), (typeof(QuestObjective.OpenQuestChest), 1)
    })
        {
            var actual = catalog.Quests.All.Count(quest => quest.Objective.GetType() == type);
            Assert(actual == expected, $"A(z) {type} küldetések száma hibás: várt {expected}, tényleges {actual}.");
        }
        Assert(catalog.GetNpc("NPC001") is { Disposition: NpcDisposition.Neutral, Unique: false } &&
               catalog.Quests.GetByGiver(QuestNpcId.MonsterHunter).Any(quest =>
                   quest is { Objective: QuestObjective.KillEnemy { Enemy.Id: "E003" }, ExperienceReward: 1170 }) &&
               catalog.Quests.GetByGiver(QuestNpcId.WanderingHerbalist).Any(quest => quest is
               { Id: QuestId.HerbalistHealingSupplies, FixedRewardItem.Id: "T018", FixedRewardItemCount: 2, RandomRewardCount: 0 }) &&
               catalog.GetNpc("NPC020") is { Unique: true, Recruitable: true, RaceId: "R003" } &&
               catalog.GetNpc("NPC021") is { Unique: true, RaceId: "R001", StoryId: "RODERIC_OATH" } &&
               catalog.NpcEncounters.Single(encounter => encounter.NpcId == "NPC021").QuestRoomId == "RODERIC_MEETING" &&
               catalog.GetNpcStoryChoices("RODERIC_OATH", "INITIAL") is
                   [{ FriendlinessChange: 2 }, { FriendlinessChange: 0 }, { FriendlinessChange: -3 }] &&
               catalog.Quests.GetByGiver(QuestNpcId.EliraSilverbranch).Select(quest => quest.Objective.GetType()).ToHashSet().SetEquals(
                   [typeof(QuestObjective.EscortNpc), typeof(QuestObjective.CollectItem), typeof(QuestObjective.KillEnemy)]) &&
               catalog.Quests.All.All(quest => quest.RandomRewardCount > 0 || quest.FixedRewardItemCount > 0 ||
                   quest.Objective is QuestObjective.OpenQuestChest && quest.ExperienceReward > 0),
            "A semleges nem egyedi NPC vagy a hozzá kapcsolt küldetés hibás.");
    }

    static void NpcLifecycleHasNoQuestState()
    {
        var npc = new WorldNpc(new Position(1, 1), "NPC002", CreateCharacter("Küldetésadó"),
            NpcDisposition.Neutral, false, true, "Próba");
        Assert(typeof(WorldNpc).GetProperty("Quests") is null && typeof(WorldNpc).GetProperty("QuestIds") is null &&
               new[] { "ActivateQuest", "AddQuestProgress", "CompleteQuest", "AbandonQuest", "RestoreQuests" }
                   .All(name => typeof(WorldNpc).GetMethod(name) is null),
            "A WorldNpc továbbra is párhuzamos questállapotot vagy mutátorokat kínál.");
        npc.AdjustFriendliness(20);
        npc.SetStoryState("TEST_STATE");
        npc.BeginFollowing();
        npc.AdvanceConversation();
        var follower = new PartyMemberAvatar(new Position(1, 1), npc.Character, npc);
        follower.MoveTo(new Position(2, 1));
        Assert(npc.Friendliness == 10 && npc.State == WorldNpcState.Following && !npc.CanStartConversation &&
               npc.ConversationStage == 1 && npc.StoryStateId == "TEST_STATE" && follower.IsTemporaryFollower &&
               npc.Position == follower.Position,
            "Az egyedi NPC viszonya, követőállapota vagy párbeszédtiltása hibás.");
        follower.MakePermanent();
        Assert(!follower.IsTemporaryFollower,
            "Az ideiglenes követő nem alakítható végleges partitaggá.");
    }

    static void AdHocFollowerConversationsAreConfigured()
    {
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        foreach (var storyId in new[] { "ELIRA_RESCUE", "RODERIC_OATH" })
        {
            for (var index = 1; index <= 5; index++)
            {
                var startState = $"ADHOC_{index}_START";
                var followupState = $"ADHOC_{index}_FOLLOWUP";
                var start = catalog.GetNpcStoryChoices(storyId, startState);
                var followup = catalog.GetNpcStoryChoices(storyId, followupState);
                Assert(start.Count == 2 && start.All(choice => choice.ContinueConversation &&
                           choice.NextStateId == followupState) &&
                       followup.Count == 2 && followup.All(choice => !choice.ContinueConversation),
                    $"A(z) {storyId}/{startState} ad-hoc beszélgetésszál szerkezete hibás.");
            }
        }

        var snapshot = new AdHocConversationSnapshot(Guid.NewGuid(), "Roderic", "Ember", "Lovag",
            ["Roderic: Az esküm még köt."], "Bízol bennünk?", ["Igen.", "Még nem."]);
        var window = AdHocConversationWindow.Build(snapshot);
        Assert(window.Any(line => line.Text.Contains("1) Igen.", StringComparison.Ordinal)) &&
               window.Any(line => line.Text.Contains("host választja", StringComparison.OrdinalIgnoreCase)),
            "A vendég ad-hoc párbeszédablaka nem mutatja a két választ vagy a host vezérlését.");
    }

    static void RodericInsigniaGuardiansAreConfigured()
    {
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var encounter = MazeLevelConfigurations.Get(5).QuestRoomEnemyEncounters.Single(value => value.RoomId == "RODERIC_INSIGNIA");
        Assert(encounter is
        {
            RoomId: "RODERIC_INSIGNIA", EnemyId: "E052", Count: 3,
            GuaranteedItemId: "T026"
        } &&
               catalog.GetItem(MiscItemIds.FallenKnightInsignia).BasePrice == 1 &&
               SpellcastingRules.IsRestrictedFromTradingAndGeneration(
                   catalog.GetItem(MiscItemIds.FallenKnightInsignia)),
            "A jelvényes terem, a Skeleton Knight vagy a questtárgy adatai hibásak.");

        var maze = new Maze(7, 7);
        maze.Carve(new Position(3, 3));
        var enemy = new ConfiguredEnemy(new Position(3, 3), catalog.GetEnemy(MonsterIds.CsontvázLovag));
        enemy.ConfigureGuaranteedLoot([MiscItemIds.FallenKnightInsignia]);
        maze.AddEnemy(enemy);
        maze.ReplaceEnemyWithCorpse(enemy);
        var corpse = maze.Corpses.OfType<MonsterCorpse>().Single();
        Assert(corpse.GuaranteedLootIds.SequenceEqual([MiscItemIds.FallenKnightInsignia]),
            "A példányhoz kötött jelvény nem került át a Skeleton Knight tetemére.");
        var restored = JsonSerializer.Deserialize<CorpseSaveData>(JsonSerializer.Serialize(new CorpseSaveData(
            corpse.Position, corpse.FormerName, null, corpse.EnemyDefinitionId, corpse.IsSearched,
            corpse.GuaranteedLootIds.ToList())));
        Assert(restored?.GuaranteedLootIds?.SequenceEqual([MiscItemIds.FallenKnightInsignia]) == true,
            "A garantált jelvény nem élte túl a mentési JSON-körutat.");
    }

    static void RodericMalrecQuestLocationIsConfigured()
    {
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var configuration = QuestLocationConfigurations.Get(QuestLocationConfigurations.RodericMalrec);
        var malrecEncounter = configuration.QuestRoomEnemyEncounters.Single(value =>
            value.EnemyId == MonsterIds.SirMalrec);
        var guards = configuration.QuestRoomEnemyEncounters.Single(value =>
            value.EnemyId == MonsterIds.CsontvázLovag);
        var quest = catalog.Quests.Get(QuestId.RodericOathbreakerKnight);
        var proofQuest = catalog.Quests.Get(QuestId.RodericTheDeadAreNotPrey);
        Assert(configuration.Level == 5 && configuration.BossRoomIds.SequenceEqual(["MALREC_CHAMBER"]) &&
               malrecEncounter is { RoomId: "MALREC_CHAMBER", EnemyId: "E053", Count: 1 } &&
               guards is
               {
                   RoomId: "MALREC_CHAMBER", EnemyId: "E052", Count: 4,
                   GuaranteedItemId: null
               } &&
               catalog.GetEnemy(MonsterIds.SirMalrec) is
               {
                   Rank: EnemyRank.MiniBoss, IsBoss: false, HitPoints: 420,
                   Armor: { Minimum: 6, Maximum: 12 }
               } &&
               quest is { Objective: QuestObjective.KillEnemy { Enemy.Id: "E053" }, ActivationRequirement: QuestActivationRequirement.StoryStateEquals { RequiredState: QuestStoryState.MalrecApproach } },
            "Sir Malrec rangja, küldetése vagy az 5-ös nehézségű küldetéshelyszíne hibás.");
        Assert(catalog.GetQuestChest(new("RODERIC_ORDER_RELICS")).Items.Any(item => item.Item.Id == "T027" && item.Quantity == 1) &&
               proofQuest is { Objective: QuestObjective.KillEnemyWithTraits { RequiredTraits: EnemyTraits.Undead, Count: 8 } } &&
               catalog.GetNpcStoryChoices("RODERIC_OATH", "PROOF_OFFER").Single() is
               {
                   Action: NpcStoryAction.ActivateQuest, ActionParameter: "NPCQ040",
                   NextStateId: "PROOF_ACTIVE"
               } &&
               catalog.GetItemDefinition(MiscItemIds.SilverOathSeal).Id == MiscItemIds.SilverOathSeal &&
               catalog.GetItem(MiscItemIds.SilverOathSeal).BasePrice == 1 &&
               SpellcastingRules.IsRestrictedFromTradingAndGeneration(
                   catalog.GetItem(MiscItemIds.SilverOathSeal)),
            "Az Ezüst Eskü nagypecsétje nem történeti jutalomtárgyként szerepel.");

        var initialChoices = catalog.GetNpcStoryChoices("RODERIC_OATH", "INITIAL");
        var trustedChoices = catalog.GetNpcStoryChoices("RODERIC_OATH", "TRUSTED");
        var finalChoices = catalog.GetNpcStoryChoices("RODERIC_OATH", "MALREC_DEFEATED");
        var lowTrustVerdict = catalog.GetNpcStoryChoices("RODERIC_OATH", "JOIN_VERDICT", 7);
        var highTrustVerdict = catalog.GetNpcStoryChoices("RODERIC_OATH", "JOIN_VERDICT", 8);
        Assert(initialChoices.Count == 3 && initialChoices.All(value => value.ContinueConversation) &&
               trustedChoices.Single() is { NextStateId: "RELICS_ACTIVE", Action: NpcStoryAction.ActivateQuest, ActionParameter: "NPCQ041" } &&
               catalog.GetNpcStoryChoices("RODERIC_OATH", "MALREC_STORY").All(value =>
                   value.NextStateId == "MALREC_READY" && !value.ContinueConversation) &&
               catalog.GetNpcStoryChoices("RODERIC_OATH", "CACHE_BLOCKED").Count == 0 &&
               finalChoices.Count == 3 && finalChoices.All(value => value.ContinueConversation) &&
               catalog.GetNpcStoryChoices("RODERIC_OATH", "SECOND_CHANCE").Count == 2 &&
               catalog.GetNpcStoryChoices("RODERIC_OATH", "SECOND_CHANCE").Any(value =>
                   value.NextStateId == "OATH_BROKEN" && value.FriendlinessChange == -3) &&
               lowTrustVerdict.Single() is
               {
                   NextStateId: "JOIN_REFUSED",
                   MaximumFriendliness: 7
               } &&
               highTrustVerdict.Single() is
               {
                   NextStateId: "JOIN_ACCEPTED",
                   MinimumFriendliness: 8, Action: NpcStoryAction.RequestPermanentJoin
               },
            "Roderic többforduló párbeszédgráfja vagy második csatlakozási esélye hibás.");

        var maze = new MazeGenerator(configuration.CreateGenerationSettings(new Random(17)), [], [])
            .Create(55, 31);
        Assert(maze.Rooms.SingleOrDefault(room => room.ContentId == "MALREC_CHAMBER") is
        { Purpose: RoomPurpose.Boss },
            "Sir Malrec szobája nem elkülönített boss roomként jött létre.");

        var suspended = new GameSaveData { MazeLevel = 5, LocationId = "CAMPAIGN_05" };
        var active = new GameSaveData
        {
            MazeLevel = 5,
            LocationKind = AdventureLocationKind.Quest,
            LocationId = QuestLocationConfigurations.RodericMalrec,
            DifficultyLevel = 5,
            SuspendedCampaign = suspended
        };
        var restored = JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(active));
        Assert(restored is
        {
            LocationKind: AdventureLocationKind.Quest, DifficultyLevel: 5,
            SuspendedCampaign.LocationId: "CAMPAIGN_05"
        },
            "A küldetéshelyszín vagy a felfüggesztett kampánypálya nem menthető.");
    }

    static void QuestRoomsReserveTheirContent()
    {
        var settings = new MazeGenerationSettings
        {
            RoomCount = 8,
            MinimumRoomSize = 4,
            MaximumRoomSize = 6,
            TreasureChestCount = 20,
            QuestRoomIds = ["RODERIC_MEETING", "RODERIC_INSIGNIA"]
        };
        var maze = new MazeGenerator(settings, [], []).Create(55, 31);
        var questRooms = maze.Rooms.Where(room => room.Purpose == RoomPurpose.Quest).ToArray();
        Assert(questRooms.Length == 2 && questRooms.Select(room => room.ContentId).ToHashSet().SetEquals(
                   ["RODERIC_MEETING", "RODERIC_INSIGNIA"]),
            "A két Roderic-quest room nem jött létre stabil tartalomazonosítóval.");
        Assert(questRooms.All(room => maze.TreasureChests.All(chest => !room.Contains(chest.Position)) &&
                                      maze.Enemies.All(enemy => !room.Contains(enemy.Position))),
            "Véletlen kincs vagy ellenfél került egy quest roomba.");
        var restored = JsonSerializer.Deserialize<Room>(JsonSerializer.Serialize(questRooms[0]));
        Assert(restored is { Purpose: RoomPurpose.Quest, ContentId: not null },
            "A quest room szerepe vagy azonosítója nem menthető.");
    }

    static void RodericUsesDefinedCharacterBuild()
    {
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var first = new UniqueNpcCharacterFactory(catalog).Create(catalog.GetNpc("NPC021"));
        var repeated = new UniqueNpcCharacterFactory(catalog).Create(catalog.GetNpc("NPC021"));
        var scaled = new UniqueNpcCharacterFactory(catalog).Create(catalog.GetNpc("NPC021"), 9);
        var veteran = new UniqueNpcCharacterFactory(catalog).Create(catalog.GetNpc("NPC021"), 15);
        Assert(first.Name == "Sir Roderic" && first.Level == 7 && scaled.Level == 9 &&
               first.Color == ConsoleColor.DarkCyan && first.NpcBehavior == NpcBehavior.Aggressive &&
               first.Abilities == new PrimaryAbilities(12, 5, 9, 6) &&
               scaled.Abilities.Strength == 13 && veteran.HasPerk(PerkIds.KnightHolyOath) &&
               veteran.HasClassFeatureUpgrade(ClassFeatureUpgrades.KnightRetaliation) &&
               first.Perks.Select(perk => perk.Id).SequenceEqual(["PERK-C003-1B", PerkIds.RodericOathblade]) &&
               first.WeaponProficiencyRankFor(WeaponFamilies.Sword) == WeaponProficiencyRank.Master &&
               first.WeaponSlots[0] is
               {
                   Id: CharacterBoundItemRules.RodericGreatswordId,
                   Rarity: ItemRarity.Magic, MagicPower: 1, BaseWeaponId: "W009"
               } &&
               first.WeaponSlots[1] is null &&
               first.Armor is
               {
                   Id: CharacterBoundItemRules.RodericPlateArmorId,
                   Rarity: ItemRarity.Magic, MagicPower: 1, BaseArmorId: "A006"
               } &&
               first.Color != ConsoleColor.White,
            "Roderic karakterlapja nem a CSV-ben rögzített buildet használja.");
        Assert(first.Abilities == repeated.Abilities && first.MaximumVitality == repeated.MaximumVitality &&
               first.WeaponSlots.Select(item => item?.Id).SequenceEqual(repeated.WeaponSlots.Select(item => item?.Id)),
            "Roderic újbóli létrehozása nem determinisztikus.");

        var outsider = CreateCharacter("Ereklyepróba");
        Assert(!outsider.SetInventoryItem(InventorySlotKind.Backpack, 0, first.WeaponSlots[0]) &&
               !outsider.SetInventoryItem(InventorySlotKind.Armor, 0, first.Armor) &&
               SpellcastingRules.IsRestrictedFromTradingAndGeneration(first.WeaponSlots[0]!) &&
               SpellcastingRules.IsRestrictedFromTradingAndGeneration(first.Armor!),
            "Roderic családi ereklyéit más karakter használhatja vagy kereskedelmi lootként kaphatja.");

        var recipient = CreateCharacter("Ellátmány");
        var bundle = new[]
        {
        new InventoryBundleEntry(catalog.GetItem("T012"), 4),
        new InventoryBundleEntry(catalog.GetItem("T004"), 2),
        new InventoryBundleEntry(catalog.GetItem("T006"), 2),
        new InventoryBundleEntry(catalog.GetItem("T002"), 4)
    };
        Assert(InventoryBundleGrantService.TryGrant([recipient], bundle, out var lacking) && lacking.Count == 0 &&
               CountBackpack(recipient, "T012") == 4 && CountBackpack(recipient, "T004") == 2 &&
               CountBackpack(recipient, "T006") == 2 && CountBackpack(recipient, "T002") == 4,
            "Az Ezüst Eskü vésztartaléka nem a kért mennyiséget osztotta ki.");

        var fullRecipient = CreateCharacter("Telezsák");
        for (var index = 0; index < LiveCharacter.MaximumBackpackItemCount; index++)
            Assert(fullRecipient.AddToBackpack(new MiscItemDefinition($"FULL-{index}", $"Tárgy {index}", "Teszt", 1)),
                "A tele hátizsákos előfeltétel nem jött létre.");
        Assert(!InventoryBundleGrantService.TryGrant([recipient, fullRecipient], bundle, out lacking) &&
               lacking.SequenceEqual([fullRecipient.Name]) && CountBackpack(recipient, "T012") == 4,
            "A vésztartalék részlegesen kiosztódott annak ellenére hogy egy partitag hátizsákja tele volt.");

        static int CountBackpack(LiveCharacter character, string itemId) =>
            Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount)
                .Where(index => string.Equals(character.Backpack[index]?.Id, itemId, StringComparison.OrdinalIgnoreCase))
                .Sum(index => character.GetInventoryItemQuantity(InventorySlotKind.Backpack, index));
    }

    static void PartyRemarksLoadFromCsv()
    {
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var combinations = catalog.Races.SelectMany(race => catalog.CharacterClasses.Select(characterClass =>
            (RaceId: race.Id, ClassId: characterClass.Id))).ToArray();

        Assert(catalog.PartySituations.Count == 9 && catalog.PartyRemarks.Count == 729 &&
               combinations.All(pair => catalog.PartyRemarks.Count(remark =>
                   remark.SituationId == PartySituationIds.Thirsty && remark.RaceId == pair.RaceId &&
                   remark.CharacterClassId == pair.ClassId) ==
                   (pair.RaceId == "R002" && pair.ClassId == CharacterClassIds.Harcos ? 6 : 3)),
            "A kilenc szituáció vagy a faj–osztály páronkénti három szomjúsági megjegyzés hiányzik.");
        var dwarfFighterRemarks = catalog.PartyRemarks.Where(remark => remark.RaceId == "R002" &&
            remark.CharacterClassId == CharacterClassIds.Harcos).ToArray();
        Assert(catalog.PartySituations.All(situation => dwarfFighterRemarks.Count(remark =>
                   remark.SituationId == situation.Id) == 6) &&
               new[] { "üllő", "tárna", "szakáll", "pöröly", "lakoma", "győzel" }.All(topic =>
                   dwarfFighterRemarks.Any(remark => remark.Text.Contains(topic, StringComparison.OrdinalIgnoreCase))),
            "A törpe harcosok megduplázott vagy tematikus megjegyzései hiányoznak.");
        Assert(catalog.PartyRemarks.Count(remark => remark.CharacterName == "Sir Roderic" &&
                   remark.TemporaryFollower) == 27 &&
               catalog.PartyRemarks.Count(remark => remark.CharacterName == "Sir Roderic" &&
                   !remark.TemporaryFollower) == 27 &&
               catalog.PartyRemarks.Count(remark => remark.CharacterName == "Sir Roderic" &&
                   remark.Text.Contains("Ezüst Eskü", StringComparison.Ordinal)) >= 18,
            "Roderic követői vagy végleges partitagként használt egyedi megjegyzései hiányoznak.");
        Assert(catalog.PartyRemarks.Single(remark => remark.Id == "PM001").Text.Contains(
                   "Maradjatok mögöttem, felmérem", StringComparison.Ordinal) &&
               catalog.PartyRemarks.Single(remark => remark.Id == "PM648").Text.Contains(
                   "a víz még nagyobb", StringComparison.Ordinal),
            "Az idézőjeles, vesszőt tartalmazó megjegyzésszöveg csonkolódott.");
    }

    static void PartyRemarkProbabilitiesFollowRules()
    {
        Assert(PartyCommentarySelector.ShouldComment(0) && PartyCommentarySelector.ShouldComment(39) &&
               !PartyCommentarySelector.ShouldComment(40) && !PartyCommentarySelector.ShouldComment(99),
            "A szituációs kommentár esélye nem pontosan 40 százalék.");
        Assert(PartyCommentarySelector.SpeakerCount(4, 0) == 1 &&
               PartyCommentarySelector.SpeakerCount(4, 49) == 1 &&
               PartyCommentarySelector.SpeakerCount(4, 59) == 1 &&
               PartyCommentarySelector.SpeakerCount(4, 60) == 2 &&
               PartyCommentarySelector.SpeakerCount(4, 79) == 2 &&
               PartyCommentarySelector.SpeakerCount(4, 80) == 3 &&
               PartyCommentarySelector.SpeakerCount(2, 99) == 2,
            "A beszélők 60/20/20 százalékos eloszlása vagy partilétszám-korlátja hibás.");
        var dwarf = new LiveCharacter("Törpe", new RaceDefinition("R002", "Törpe", PrimaryAbilities.Zero),
            new CharacterClassDefinition(CharacterClassIds.Harcos, "Harcos", PrimaryAbilities.Zero, false, 1.0),
            new PrimaryAbilities(5, 5, 5, 5), 20, 0, 1, 0);
        Assert(PartyCommentarySelector.SpeakerWeight(PartySituationIds.Resting, dwarf) == 120 &&
               PartyCommentarySelector.SpeakerWeight(PartySituationIds.EnemySpotted, dwarf) == 140 &&
               PartyCommentarySelector.SpeakerWeight(PartySituationIds.BattleStarted, dwarf) == 140 &&
               PartyCommentarySelector.SpeakerWeight(PartySituationIds.BattleWon, dwarf) == 140 &&
               PartyCommentarySelector.SpeakerWeight(PartySituationIds.PartyMemberDied, dwarf) == 140 &&
               PartyCommentarySelector.SpeakerWeight(PartySituationIds.BattleStarted,
                   CreateCharacter("Ember")) == 100,
            "A törpe beszélők 20/40 százalékos súlytöbblete hibás.");
        var speaker = CreateCharacter("Kommentelő");
        Assert(PartyCommentarySelector.Format(speaker, "Próba.") == "[Kommentelő] Próba." &&
               PartyCommentarySelector.Format(speaker, "Éhes vagyok.", "17") ==
               "[Kommentelő](17) Éhes vagyok.",
            "A parti megjegyzésének név- vagy állapotszint-formátuma hibás.");
    }

    static void WorldNpcGenerationExcludesWhiteColor()
    {
        Assert(CharacterColors.Selectable.Contains(ConsoleColor.White) &&
               !CharacterColors.WorldNpcSelectable.Contains(ConsoleColor.White) &&
               CharacterColors.WorldNpcSelectable.Count == CharacterColors.Selectable.Count - 1,
            "A world-NPC színpaletta nem pontosan a fehér karakterszínt zárja ki.");

        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var generator = new RandomCharacterGenerator(catalog, new Random(7281));
        var characterClass = catalog.GetCharacterClass(CharacterClassIds.Harcos);
        var recruits = Enumerable.Range(0, 30)
            .Select(index => generator.GenerateWorldNpc(characterClass, 5, [$"WorldNpc{index}"]))
            .ToArray();
        Assert(recruits.All(recruit => recruit.Color != ConsoleColor.White),
            "A world-NPC generátor fehér karakterszínt választott.");
    }

    static void GeneratedCharacterEquipmentProfilesAreLevelBounded()
    {
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory,
            CsvGameDataLoader.GameDataFileName));
        var characterClass = catalog.GetCharacterClass(CharacterClassIds.Harcos);
        var generator = new RandomCharacterGenerator(catalog, new Random(8142));
        var lowLevelNpcs = Enumerable.Range(0, 20)
            .Select(index => generator.GenerateWorldNpc(characterClass, 4, [$"LowNpc{index}"],
                RandomCharacterGenerator.EquipmentOptions.Scaled(includeMagicItems: false,
                    addSupplies: false)))
            .ToArray();
        var highLevelNpcs = Enumerable.Range(0, 30)
            .Select(index => generator.GenerateWorldNpc(characterClass, 15, [$"HighNpc{index}"],
                RandomCharacterGenerator.EquipmentOptions.Scaled(includeMagicItems: false,
                    addSupplies: false)))
            .ToArray();

        static IEnumerable<IItemDefinition> WornEquipment(LiveCharacter character) =>
            character.WeaponSlots.Where(item => item is not null).Cast<IItemDefinition>()
                .Concat(character.Armor is null ? [] : [character.Armor]);

        Assert(lowLevelNpcs.SelectMany(WornEquipment).All(item => item.MagicPower == 0),
            "A 4. szintű, skálázott világ-NPC varázstierű felszerelést kapott.");
        var highLevelPowers = highLevelNpcs.SelectMany(WornEquipment).Select(item => item.MagicPower).ToArray();
        Assert(highLevelPowers.All(power => power is >= 2 and <= 3) &&
               highLevelPowers.Contains(2) && highLevelPowers.Contains(3),
            "A 15. szintű világ-NPC felszerelése nem a szinthez illő, változó 2–3. tierből készült.");

        var fixedTierNpc = generator.GenerateWorldNpc(characterClass, 20, ["FixedTierNpc"],
            RandomCharacterGenerator.EquipmentOptions.AtTier(
                RandomCharacterGenerator.EquipmentTier.LesserMagic,
                includeMagicItems: false, addSupplies: false));
        Assert(WornEquipment(fixedTierNpc).All(item => item.MagicPower <= 1),
            "A fix felszerelési tier fölötti tárgy került a generált NPC-re.");

        characterClass = catalog.GetCharacterClass(CharacterClassIds.Harcos);
        var c1 = generator.GenerateMercenary(characterClass, 6, []);

        characterClass = catalog.GetCharacterClass(CharacterClassIds.Lovag);
        var c2 = generator.GenerateMercenary(characterClass, 9, []);

        characterClass = catalog.GetCharacterClass(CharacterClassIds.Tolvaj);
        var c3 = generator.GenerateMercenary(characterClass, 12, []);

        characterClass = catalog.GetCharacterClass(CharacterClassIds.Mágus);
        var c4 = generator.GenerateMercenary(characterClass, 15, []);

        characterClass = catalog.GetCharacterClass(CharacterClassIds.Mágus);
        var c5 = generator.GenerateMercenary(characterClass, 22, []);

        characterClass = catalog.GetCharacterClass(CharacterClassIds.Harcos);
        var n1 = generator.GenerateWorldNpc(characterClass, 6, []);

        characterClass = catalog.GetCharacterClass(CharacterClassIds.Lovag);
        var n2 = generator.GenerateWorldNpc(characterClass, 9, []);

        characterClass = catalog.GetCharacterClass(CharacterClassIds.Tolvaj);
        var n3 = generator.GenerateWorldNpc(characterClass, 12, []);

        characterClass = catalog.GetCharacterClass(CharacterClassIds.Mágus);
        var n4 = generator.GenerateWorldNpc(characterClass, 15, []);

        characterClass = catalog.GetCharacterClass(CharacterClassIds.Mágus);
        var n5 = generator.GenerateWorldNpc(characterClass, 22, []);

    }

    static void LateInnRecruitsAreLowerLevelAndStillCostGold()
    {
        Assert(!RecruitmentRules.UsesLowerLevelCandidates(4) &&
               RecruitmentRules.UsesLowerLevelCandidates(5),
            "A zsoldosszint-váltás nem az 5. pálya utáni fogadóban történik.");
        Assert(RecruitmentRules.LowerRecruitLevel(10, 25) == 7 &&
               RecruitmentRules.LowerRecruitLevel(10, 50) == 5 &&
               RecruitmentRules.LowerRecruitLevel(2, 25) == 1,
            "A késői zsoldos nem 25–50%-kal, legalább egy teljes szinttel marad el a vezértől.");
        Assert(RecruitmentRules.Price(7, 10, 4, 100) == 0 &&
               RecruitmentRules.Price(7, 10, 5, 100) == 1400 &&
               RecruitmentRules.Price(7, 10, 5, 50) == 700 &&
               RecruitmentRules.Price(7, 10, 5, 150) == 2100,
            "A korai ingyenes és a késői, szintenként 200 aranyas zsoldosárazás nem a 50–150%-os képletet használja.");

        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory,
            CsvGameDataLoader.GameDataFileName));
        var generator = new RandomCharacterGenerator(catalog, new Random(5050));
        var characterClass = catalog.GetCharacterClass(CharacterClassIds.Harcos);
        var recruits = Enumerable.Range(0, 30)
            .Select(index => generator.GenerateMercenary(characterClass, 10, [$"LateRecruit{index}"], 5))
            .ToArray();
        Assert(recruits.All(recruit => recruit.Level is >= 5 and <= 7),
            "Az 5. pálya utáni generátor nem 25–50%-kal gyengébb zsoldost készített.");
    }

    static void TemporaryFollowerKeepsWorldNpcMapColors()
    {
        var character = CreateCharacter("Elira");
        var npc = new WorldNpc(new Position(1, 1), "NPC020", character,
            NpcDisposition.Neutral, true, true, "Próba");
        var follower = new PartyMemberAvatar(npc.Position, character, npc);

        Assert(follower.ForegroundColor == ConsoleColor.White && follower.BackgroundColor == character.Color,
            "Az ideiglenes követő nem a world-NPC inverz térképszíneit kapta.");
        follower.MakePermanent();
        Assert(follower.ForegroundColor == character.Color && follower.BackgroundColor == ConsoleColor.Black,
            "A végleges partitaggá vált követő nem kapta vissza a normál térképszíneit.");
    }

    static void NpcDialogueWrapsInsideRecruitmentWindow()
    {
        var dialogue = "„A csontok emlékeznek azokra akik felébresztették őket. " +
                       "Segítsetek újra elcsendesíteni a sírokat.”";
        var lines = MessageTextLayout.Wrap(dialogue, ConsoleRenderer.WorldNpcRecruitmentTextWidth).ToArray();

        Assert(lines.Length > 1 && lines.All(line =>
                   line.Length <= ConsoleRenderer.WorldNpcRecruitmentTextWidth) &&
               string.Join(' ', lines).Replace("  ", " ", StringComparison.Ordinal) == dialogue,
            "A hosszú NPC-párbeszéd kilóg a találkozási ablakból vagy szöveg veszett el.");
    }

    static void QuestJournalBuildsSharedHistory()
    {
        var entries = new QuestJournalEntrySnapshot[]
        {
        new(new(KaoszRubin.Domain.Quests.QuestId.EliraRescue), "Folyamatban", "Tedd meg.", "Elira", QuestJournalStatus.Active, 2, 4, 240),
        new(new(KaoszRubin.Domain.Quests.QuestId.EliraTornBandage), "Befejezve", "Megtetted.", "Elira", QuestJournalStatus.Completed, 1, 1, 420),
        new(new(KaoszRubin.Domain.Quests.QuestId.EliraOnOurTrail), "Feladva", "Nem folytatod.", "Elira", QuestJournalStatus.Abandoned, 1, 3, 180)
        };
        var lines = QuestJournalWindow.Build(entries);
        var restoration = QuestJournalWindow.CalculateRestorationRegion(entries, 0, 200, 50);
        var readOnlyRestoration = QuestJournalWindow.CalculateRestorationRegion(entries, 0, 200, 50,
            allowAbandon: false);
        Assert(WindowFrameConfiguration.For(FramedWindow.QuestOffer) == WindowFrameStyle.Stone &&
               WindowFrameConfiguration.For(FramedWindow.QuestJournal) == WindowFrameStyle.Scroll2 &&
               restoration.Width == QuestJournalWindow.Width && restoration.Height < 50 &&
               restoration.Height == readOnlyRestoration.Height + 1 &&
               restoration.Left > 0 && restoration.Left + restoration.Width < 200 &&
               lines.Any(line => line.Text.Contains("Folyamatban — 2/4  Elira (240 XP)", StringComparison.Ordinal)) &&
               lines.Any(line => line.Text.Contains("Befejezve — Elira (+420 XP)", StringComparison.Ordinal)) &&
               lines.Any(line => line.Text.Contains("× Feladva — Elira", StringComparison.Ordinal)),
            "A küldetésnapló kerete vagy aktív/teljesített/feladott tartalma hibás.");
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
        var longDescriptionSpell = spell with
        {
            Description = "Egy próbaként használt varázslat, amely szélesebb karakterlapon kevesebb töréssel jelenjen meg."
        };
        var defaultInfoLines = SpellInfoPanel.Build("Rubin", CharacterClassIds.Mágus, 6,
            new SpellInfoSnapshot("Kristálygömb", 3, [longDescriptionSpell]), 0);
        var wideInfoLines = SpellInfoPanel.Build("Rubin", CharacterClassIds.Mágus, 6,
            new SpellInfoSnapshot("Kristálygömb", 3, [longDescriptionSpell]), 0, width: 40);
        Assert(wideInfoLines.Count(line => line.Row is >= 30 and < 35 && !string.IsNullOrWhiteSpace(line.Text)) <=
               defaultInfoLines.Count(line => line.Row is >= 30 and < 35 && !string.IsNullOrWhiteSpace(line.Text)),
            "A széles varázslatinformációs panel nem használja ki az extra jobb oldali helyet a leírás tördelésénél.");

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
        var dataPath = Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName);
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
        var thiefTactics = new[]
        {
        new BattleTacticOptionSnapshot(BattleActionKind.ThiefAmbush, "🗡️ Orvtámadás", "első találat ×2", 60),
        new BattleTacticOptionSnapshot(BattleActionKind.ThiefObserve, "👁️ Megfigyelés", "+2 találat", 70),
        new BattleTacticOptionSnapshot(BattleActionKind.ThiefPoison, "☠️ Mérgezett penge", "+1–4 sebzés", 60)
    };
        Assert(BattlePromptText.Tactic(CharacterClassIds.Harcos, tactics).Contains("65%", StringComparison.Ordinal) &&
               BattlePromptText.Tactic(CharacterClassIds.Tolvaj, thiefTactics).Contains("Megfigyelés 70%", StringComparison.Ordinal) &&
               BattleCommandPanel.Format(thiefTactics.Select(option => option.Action), true, "testthief", thiefTactics)
                   .Contains("3: ☠️ Mérgezett penge", StringComparison.Ordinal) &&
               BattleCommandPanel.DisplayWidth("🗡️ Orvtámadás | 👁️ Megfigyelés") == 30 &&
               BattlePromptText.EnemyTurn == "Space — ellenfél köre" &&
               BattlePromptText.PlayerAction(true, true).Contains("halottűzés", StringComparison.Ordinal),
            "A közös harci prompt elvesztette a taktikai esélyt vagy valamelyik vezérlést.");
    }

    static void AbilityMagicItemsAreUniversalAndCapped()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
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
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var invalid = source.Replace("#Képességek", "#Elgépelt képességek", StringComparison.Ordinal);
        AssertCsvLoadFails(invalid, "Ismeretlen fejezetcím", "sorában");
    }

    static void MissingRequiredCsvFieldIsRejectedWithLineNumber()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var separator = source.Contains("R001;Ember;Adaptable", StringComparison.Ordinal) ? ';' : ',';
        var invalid = source.Replace($"R001{separator}Ember{separator}Adaptable", $"R001{separator}Ember", StringComparison.Ordinal);
        Assert(invalid != source, "A kötelező mezőt törlő teszt nem találta a módosítandó CSV-sort.");
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
}
