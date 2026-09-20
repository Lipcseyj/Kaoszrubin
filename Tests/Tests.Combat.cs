internal static partial class Program
{
    static void TacticalDistanceUsesConsoleAspectRatio()
    {
        var origin = new Position(10, 10);
        Assert(TacticalDistance.Between(origin, new Position(20, 10)) == 5 &&
               TacticalDistance.Between(origin, new Position(10, 15)) == 5 &&
               TacticalDistance.IsWithin(origin, new Position(18, 11), 5) &&
               !TacticalDistance.IsWithin(origin, new Position(20, 11), 5),
            "A taktikai távolság nem azonos léptékben kezeli a vízszintes és függőleges irányt.");
    }

    static void DiagonalEnemyIsMeleeAdjacent()
    {
        var origin = new Position(10, 10);
        Assert(TacticalDistance.IsMeleeAdjacent(origin, new Position(9, 9)) &&
               TacticalDistance.IsMeleeAdjacent(origin, new Position(11, 11)) &&
               TacticalDistance.IsMeleeAdjacent(origin, new Position(10, 11)) &&
               !TacticalDistance.IsMeleeAdjacent(origin, origin) &&
               !TacticalDistance.IsMeleeAdjacent(origin, new Position(12, 10)),
            "A közelharci szomszédság nem pontosan a nyolc környező mezőt fogadja el.");
    }

    static void TacticalBattleStateOrdersEligibleParticipants()
    {
        var first = new TacticalBattleParticipant(new CombatantId("character:first"), BattleSide.Friendly,
            TacticalParticipantKind.PartyMember, new Position(10, 10), 8, 3);
        var second = new TacticalBattleParticipant(new CombatantId("enemy:first"), BattleSide.Hostile,
            TacticalParticipantKind.Enemy, new Position(11, 10), 6, 2);
        var late = new TacticalBattleParticipant(new CombatantId("character:late"), BattleSide.Friendly,
            TacticalParticipantKind.PartyMember, new Position(20, 10), 12, 5, EligibleFromCycle: 2);
        var state = new TacticalBattleState(BattleId.New(), new Position(10, 10), [first, second, late],
            openingOrder: [second.Id, first.Id]);

        Assert(state.InitiativeOrder.Select(value => value.Id).SequenceEqual([first.Id, second.Id]) &&
               state.IsInsideBattleArea(new Position(20, 10)),
            "A nyitószakaszban nem csak az azonnal jogosult résztvevők kerültek sorra.");
        Assert(state.StartTurns().Id == second.Id && state.AdvanceTurn().Id == first.Id &&
               state.AdvanceTurn().Id == late.Id,
            "A nyitó ütésváltást nem azonnal követte a teljes kezdeményezési sor.");
        Assert(state.InitiativeOrder.Select(value => value.Id).SequenceEqual([late.Id, first.Id, second.Id]),
            "A harmadik körben nem lépett be vagy nem kezdeményezés szerint rendeződött a távoli résztvevő.");
    }

    static void BattleOpeningOrderUsesInitiative()
    {
        var system = CreateBattleSystem(1720);
        var slower = CreateCharacter("Lassabb");
        var fasterEnemy = CreateEnemyAt(new Position(2, 1), "OPENING-FAST-ENEMY");
        var preparation = system.PrepareCharacter(slower);
        var normal = new BattleEncounter(new Position(1, 1),
            [new BattleCharacterParticipant(slower, new Position(1, 1), TacticalParticipantKind.PartyMember,
            4, 3, 1, preparation.Runtime)],
            [new BattleEnemyParticipant(fasterEnemy, 9, 3, 1)], slower.Id, fasterEnemy.Id);
        Assert(normal.OpeningOrder.SequenceEqual(
                [CombatantId.ForEnemy(fasterEnemy.Id), CombatantId.ForCharacter(slower.Id)]) &&
               normal.Turns.StartTurns().Id == CombatantId.ForEnemy(fasterEnemy.Id),
            "Normál találkozáskor nem a magasabb kezdeményezésű fél kezdte a nyitó ütésváltást.");

        var faster = CreateCharacter("Gyorsabb");
        var ambusher = CreateEnemyAt(new Position(4, 3), "OPENING-AMBUSHER");
        var ambushPreparation = system.PrepareCharacter(faster);
        var ambush = new BattleEncounter(new Position(3, 3),
            [new BattleCharacterParticipant(faster, new Position(3, 3), TacticalParticipantKind.PartyMember,
            20, 3, 1, ambushPreparation.Runtime)],
            [new BattleEnemyParticipant(ambusher, 1, 3, 1)], faster.Id, ambusher.Id,
            enemyStrikesFirst: true);
        Assert(ambush.OpeningOrder.SequenceEqual(
                [CombatantId.ForEnemy(ambusher.Id), CombatantId.ForCharacter(faster.Id)]) &&
               ambush.Turns.StartTurns().Id == CombatantId.ForEnemy(ambusher.Id),
            "Az ellenséges rajtaütés nem őrizte meg a szörny nyitó elsőbbségét.");
    }

    static void FirstStrikeUsesSeparateOpeningInitiative()
    {
        var normalCharacter = CreateCharacter("Normál");
        var firstStrikeCharacter = CreateCharacter("Elsőcsapás");
        Assert(firstStrikeCharacter.AddPerk(new PerkDefinition(PerkIds.FighterFirstStrike, "Első csapás",
                "Teszt", CharacterClassIds.Harcos, 1)),
            "A tesztkarakter nem kapta meg az Első csapás tehetséget.");

        var normalPreparation = CreateBattleSystem(1721).PrepareCharacter(normalCharacter);
        var firstStrikePreparation = CreateBattleSystem(1721).PrepareCharacter(firstStrikeCharacter);
        Assert(firstStrikePreparation.Initiative == normalPreparation.Initiative + 2 &&
               firstStrikePreparation.OpeningInitiative == normalPreparation.OpeningInitiative + 10 &&
               firstStrikePreparation.OpeningInitiative == firstStrikePreparation.Initiative + 8,
            "Az Első csapás nem ugyanarra a dobásra adta a nyitó +10 és a rendes +2 bónuszt.");

        var enemy = CreateEnemyAt(new Position(2, 1), "FIRST-STRIKE-ENEMY");
        var enemyInitiative = firstStrikePreparation.Initiative + 5;
        var encounter = new BattleEncounter(new Position(1, 1),
            [new BattleCharacterParticipant(firstStrikeCharacter, new Position(1, 1),
            TacticalParticipantKind.PartyMember, firstStrikePreparation.Initiative, 3, 1,
            firstStrikePreparation.Runtime, firstStrikePreparation.OpeningInitiative)],
            [new BattleEnemyParticipant(enemy, enemyInitiative, 3, 1)], firstStrikeCharacter.Id, enemy.Id);
        var characterId = CombatantId.ForCharacter(firstStrikeCharacter.Id);
        var enemyId = CombatantId.ForEnemy(enemy.Id);

        Assert(encounter.OpeningOrder.SequenceEqual([characterId, enemyId]) &&
               encounter.Turns.StartTurns().Id == characterId &&
               encounter.Turns.AdvanceTurn().Id == enemyId &&
               encounter.Turns.AdvanceTurn().Id == enemyId,
            "Az Első csapás nyitó bónusza nem csak az első ütésváltást rendezte át.");
    }

    static void SpellEffectsReorderInitiativeAtCycleBoundary()
    {
        var system = CreateBattleSystem(1722);
        var character = CreateCharacter("Gyorsított");
        var enemy = CreateEnemy(20, 2, speed: 7);
        var preparation = system.PrepareCharacter(character);
        var encounter = new BattleEncounter(new Position(1, 1),
            [new BattleCharacterParticipant(character, new Position(1, 1), TacticalParticipantKind.PartyMember,
            5, 3, 1, preparation.Runtime)],
            [new BattleEnemyParticipant(enemy, 7, 3, 1)], character.Id, enemy.Id);
        var characterId = CombatantId.ForCharacter(character.Id);
        var enemyId = CombatantId.ForEnemy(enemy.Id);

        Assert(encounter.Turns.StartTurns().Id == enemyId,
            "A teszt kezdeti kezdeményezési sorrendje hibás.");
        character.ApplySpellEffect(new ActiveSpellEffect("HASTE-TEST", ActiveSpellEffectType.InitiativeBonus,
            5, 3, Beneficial: true));
        enemy.ApplySpellEffect(new ActiveSpellEffect("SLOW-TEST", ActiveSpellEffectType.SpeedPenalty, 3, 3));
        Assert(encounter.Turns.CurrentParticipant?.CurrentInitiative == 7,
            "A varázshatás kör közben megváltoztatta az aktuális sorrendet.");

        encounter.AdvanceTurn();
        var secondCycleFirst = encounter.AdvanceTurn();
        Assert(encounter.Turns.Cycle == 2 && secondCycleFirst.Id == characterId &&
               encounter.Turns.Find(characterId)?.CurrentInitiative == 10 &&
               encounter.Turns.Find(enemyId)?.CurrentInitiative == 4 &&
               encounter.InitiativeChangesAtCycleStart.Count == 2,
            "A gyorsítás és lassítás nem a következő kör kezdeményezését rendezte át.");

        character.RemoveSpellEffects(effect => effect.SourceSpellId == "HASTE-TEST");
        enemy.RemoveSpellEffects(effect => effect.SourceSpellId == "SLOW-TEST");
        encounter.AdvanceTurn();
        var thirdCycleFirst = encounter.AdvanceTurn();
        Assert(encounter.Turns.Cycle == 3 && thirdCycleFirst.Id == enemyId &&
               encounter.Turns.Find(characterId)?.CurrentInitiative == 5 &&
               encounter.Turns.Find(enemyId)?.CurrentInitiative == 7 &&
               encounter.InitiativeChangesAtCycleStart.Count == 2,
            "A lejárt gyorsítás és lassítás nem állította vissza a következő kör sorrendjét.");

        encounter.AdvanceTurn();
        encounter.AdvanceTurn();
        Assert(encounter.Turns.Cycle == 4 && encounter.InitiativeChangesAtCycleStart.Count == 0,
            "A rendszer változatlan kezdeményezés mellett is körönkénti naplóeseményt készítene.");
    }

    static void StatusPenaltyReordersInitiativeAtCycleBoundary()
    {
        var system = CreateBattleSystem(1723);
        var character = CreateCharacter("Rémült");
        var enemy = CreateEnemy(20, 2, speed: 7);
        var preparation = system.PrepareCharacter(character);
        var encounter = new BattleEncounter(new Position(1, 1),
            [new BattleCharacterParticipant(character, new Position(1, 1), TacticalParticipantKind.PartyMember,
            8, 3, 1, preparation.Runtime)],
            [new BattleEnemyParticipant(enemy, 7, 3, 1)], character.Id, enemy.Id);
        var characterId = CombatantId.ForCharacter(character.Id);
        var enemyId = CombatantId.ForEnemy(enemy.Id);
        var fear = new StatusDefinition("FEAR-TEST", "Rettegés", "😱", 2,
            0, 0, 0, 2, 0, 100, 100, 100, 100, 0, 0, 1, "Teszt");

        Assert(encounter.Turns.StartTurns().Id == characterId,
            "A státuszteszt kezdeti kezdeményezési sorrendje hibás.");
        character.AddStatus(fear);
        encounter.AdvanceTurn();
        var penalizedCycleFirst = encounter.AdvanceTurn();
        Assert(encounter.Turns.Cycle == 2 && penalizedCycleFirst.Id == enemyId &&
               encounter.Turns.Find(characterId)?.CurrentInitiative == 6 &&
               encounter.InitiativeChangesAtCycleStart is [{ PreviousInitiative: 8, CurrentInitiative: 6 }],
            "Az időzített kezdeményezés-büntetés nem rendezte át a következő kört.");

        character.RemoveStatus(fear.Id);
        encounter.AdvanceTurn();
        var restoredCycleFirst = encounter.AdvanceTurn();
        Assert(encounter.Turns.Cycle == 3 && restoredCycleFirst.Id == characterId &&
               encounter.Turns.Find(characterId)?.CurrentInitiative == 8 &&
               encounter.InitiativeChangesAtCycleStart is [{ PreviousInitiative: 6, CurrentInitiative: 8 }],
            "A megszűnt kezdeményezés-büntetés nem állította vissza a következő kör sorrendjét.");
    }

    static void InitiativeTiesRemainStable()
    {
        var first = new TacticalBattleParticipant(new CombatantId("character:a"), BattleSide.Friendly,
            TacticalParticipantKind.PartyMember, new Position(1, 1), 5, 3);
        var second = new TacticalBattleParticipant(new CombatantId("character:b"), BattleSide.Friendly,
            TacticalParticipantKind.PartyMember, new Position(2, 1), 5, 3);
        var state = new TacticalBattleState(BattleId.New(), new Position(1, 1), [second, first]);

        Assert(state.InitiativeOrder.Select(participant => participant.Id).SequenceEqual([first.Id, second.Id]) &&
               state.StartTurns().Id == first.Id && state.AdvanceTurn().Id == second.Id &&
               state.AdvanceTurn().Id == first.Id &&
               state.InitiativeOrder.Select(participant => participant.Id).SequenceEqual([first.Id, second.Id]),
            "Az azonos kezdeményezésű résztvevők sorrendje megváltozott a körhatáron.");
    }

    static void TacticalArrivalRequiresWalkableRoute()
    {
        var origin = new Position(0, 0);
        var closed = new Position(2, 0);
        bool IsCorridorOpen(Position position) => position.Y == 0 && position.X is >= 0 and <= 4 && position != closed;
        bool IsOpenCorridor(Position position) => position.Y == 0 && position.X is >= 0 and <= 4;
        bool HasArrived(Position position) => position == new Position(4, 0);

        Assert(!TacticalArrivalRules.CanReachWithin(origin, 6, IsCorridorOpen, HasArrived) &&
               TacticalArrivalRules.CanReachWithin(origin, 6, IsOpenCorridor, HasArrived),
            "A taktikai érkezés nem különítette el a zárt és a járható útvonalat.");
    }

    static void BattleDetectsInactiveSide()
    {
        var system = CreateBattleSystem(1710);
        var character = CreateCharacter("Aktivitás");
        var enemy = CreateEnemy(20, 2);
        var preparation = system.PrepareCharacter(character);
        var encounter = new BattleEncounter(new Position(1, 1),
            [new BattleCharacterParticipant(character, new Position(1, 2), TacticalParticipantKind.PartyMember,
            preparation.Initiative, 3, 1, preparation.Runtime)],
            [new BattleEnemyParticipant(enemy, 5, 2, 1)], character.Id, enemy.Id);
        encounter.Turns.StartTurns();
        void CompleteCycle()
        {
            var cycle = encounter.Turns.Cycle;
            do { encounter.AdvanceTurn(); } while (encounter.Turns.Cycle == cycle);
        }
        for (var cycle = 1; cycle < BattleEncounter.InactiveCycleLimit; cycle++)
        {
            encounter.RecordAttack(BattleSide.Friendly);
            CompleteCycle();
            Assert(encounter.InactiveSidesLastCompletedCycle.Count == 0,
                "A rendszer az inaktivitási küszöb előtt lezárná a csatát.");
        }

        encounter.RecordAttack(BattleSide.Friendly);
        CompleteCycle();

        Assert(encounter.InactiveSidesLastCompletedCycle.SetEquals([BattleSide.Hostile]),
            "A rendszer nem azonosította a küszöbig mozdulatlan és támadás nélküli oldalt.");
    }

    static void EncounterThreatAssessmentRecognizesSafeFight()
    {
        var party = Enumerable.Range(0, 4).Select(index => CreateCharacter($"Hős{index}", 30)).ToArray();
        var weakEnemy = new EnemyDefinition("E-WEAK", "Gyenge ellenfél", "e", 1, 2, 0, 1,
            1, 1, []);
        var boss = weakEnemy with
        {
            HitPoints = 100,
            Strength = 12,
            Armor = 8,
            Speed = 8,
            StrengthTier = 10,
            Rank = EnemyRank.Boss
        };
        var safe = EncounterThreatEvaluator.Assess(party, [weakEnemy]);
        var dangerous = EncounterThreatEvaluator.Assess(party, [boss]);
        Assert(safe.IsOverwhelminglySafe && safe.HostileToFriendlyRatio <= 0.25 &&
               !dangerous.IsOverwhelminglySafe && dangerous.HostilePower > safe.HostilePower,
            "A fenyegetésbecslés nem különíti el a jelentéktelen ellenfelet a bosstól.");
    }

    static void QuickCombatAllowsUpToThreeSafeEnemies()
    {
        var party = Enumerable.Range(0, 4).Select(index => CreateCharacter($"Gyorshős{index}", 30)).ToArray();
        var weak = new EnemyDefinition("E-QUICK", "Jelentéktelen ellenfél", "e", 1, 2, 0, 1,
            1, 1, []);
        var giantRat = new EnemyDefinition("E001", "Óriáspatkány", "r", 2, 20, 0, 5,
            100, 1, ["MA003"]);
        var safe = QuickCombatRules.Assess(party, [weak]);
        var safeGroup = QuickCombatRules.Assess(party, [weak, weak, weak]);
        var giantRatGroup = QuickCombatRules.Assess(party, [giantRat, giantRat, giantRat]);
        var fragileParty = party.ToArray();
        fragileParty[0].ReceiveDamage(29);
        var injuredGroup = QuickCombatRules.Assess(fragileParty, [giantRat, giantRat, giantRat]);

        Assert(safe.IsEligible && safeGroup.IsEligible && giantRatGroup.IsEligible && injuredGroup.IsEligible &&
               safeGroup.PredictedInjuryRatio <= QuickCombatRules.MaximumPredictedInjuryRatio,
            "Az egy-három jelentéktelen ellenfélből álló csoport egy sérült csapattal sem lett gyorsharcra alkalmas.");
        Assert(!QuickCombatRules.Assess(party, [weak, weak, weak, weak]).IsEligible &&
               !QuickCombatRules.Assess(party, [weak with { Rank = EnemyRank.Elite }]).IsEligible &&
               !QuickCombatRules.Assess(party, [weak], hasAvailableReinforcements: true).IsEligible &&
               !QuickCombatRules.Assess(party, [weak], hasActiveFormation: true).IsEligible &&
               !QuickCombatRules.Assess(party, [weak], isQuestImportant: true).IsEligible &&
               !QuickCombatRules.Assess(party, [weak], enemyStrikesFirst: true).IsEligible,
            "A gyorsharc valamelyik taktikai vagy halálkockázatos helyzetet tévesen átengedte.");
    }

    static void QuickCombatSettingPersists()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kaoszrubin-settings-{Guid.NewGuid():N}.json");
        try
        {
            var service = new GameSettingsService(path);
            service.Settings.QuickCombat = QuickCombatMode.Automatic;
            service.Save();
            var loaded = new GameSettingsService(path);
            var invalid = new GameSettings
            {
                QuickCombat = (QuickCombatMode)999,
                MusicVolumePercent = 150,
                SoundEffectsVolumePercent = -10
            };
            invalid.Normalize();

            Assert(loaded.Settings.QuickCombat == QuickCombatMode.Automatic &&
                   invalid.QuickCombat == QuickCombatMode.Ask && invalid.MusicVolumePercent == 100 &&
                   invalid.SoundEffectsVolumePercent == 0,
                "A gyorsharc módja nem maradt meg vagy az érvénytelen beállítás nem normalizálódott.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    static void BattleDetailsPanelPagesCalculation()
    {
        var details = new BattleActionDetails(Guid.NewGuid(), "Grok", "Kobold",
            ["🎯 1/1 találat", "💥 Sebzés: 12", "🎲 Kritikus: 15% — nem"],
            Enumerable.Range(1, 12).Select(index => $"🎯 {index}. módosító: +{index}").ToArray());
        var first = BattleDetailsPanel.Build(details, 0);
        var pages = BattleDetailsPanel.PageCount(details);
        var last = BattleDetailsPanel.Build(details, pages - 1);

        Assert(first.Count == BattleDetailsPanel.Height && pages > 1 &&
               first.Any(line => line.Text.Contains("15%")) &&
               first[0].Text.StartsWith("├") && first[0].Text.Length == BattleDetailsPanel.ExtendedWidth &&
               first[0].ExtendsToDivider && first[^1].ExtendsToDivider &&
               first[^1].Text.EndsWith("┤") && first[^1].Text.Contains("−/+") &&
               first[^1].Segments?.Any(segment => segment.Text == "−/+" &&
                   segment.Color == ConsoleColor.Yellow) == true &&
               last[^1].Text.Contains($"{pages}/{pages}"),
            "A csatarészlet panel mérete, kritikus esélye vagy lapozása hibás.");
        var wide = BattleDetailsPanel.Build(details, 0, 40);
        Assert(wide[0].Text.Length == 42 && wide[^1].Text.Length == 42,
            "A széles csatarészlet panel nem használja ki az extra jobb oldali helyet.");
    }

    static void QuickCombatSummaryListsKillsAndExperience()
    {
        var iskra = CharacterId.New();
        var yorgrim = CharacterId.New();
        BattleKill[] kills =
        [
            new(iskra, "Iskra", "E001", "Óriáspatkány", 100),
        new(iskra, "Iskra", "E001", "Óriáspatkány", 100),
        new(yorgrim, "Yorgrim", "E002", "Kobold", 150)
        ];

        var summary = ConsoleRenderer.FormatQuickBattleKillSummary(kills);
        Assert(summary == "Szerzett XP: 350. Iskra legyőzött 2 ellenfelet: 2× Óriáspatkány; " +
               "Yorgrim legyőzött 1 ellenfelet: 1× Kobold.",
            "A gyorsharc összesítője nem a tényleges ölőket, ellenféltípusokat és XP-t írta ki.");
    }

    static void BattleSummaryListsResourceUse()
    {
        var summary = ConsoleRenderer.FormatBattleResourceSummary(
        [
            new BattleCharacterResult("Iskra", 0, 0, false, ["🤒"], 0),
        new BattleCharacterResult("Yorgrim", 0, 0, false, ["🤒"], 0),
        new BattleCharacterResult("Fürge", 0, 0, false, [], 0),
        new BattleCharacterResult("Pál", 0, 20, false, [], 3)
        ], 7);
        Assert(summary == "Iskra: ❤️-0 🤒; Yorgrim: ❤️-0 🤒; Fürge: ❤️-0; " +
               "Pál: ❤️-0🔷-20 ✨3 Mindenki 🍖-7 💧-7",
            "A harc erőforrás-összesítője nem személyenként és tömör emoji-formában jelenik meg.");

        BattleKill[] kills =
        [
            new(CharacterId.New(), "Iskra", "E001", "Óriáspatkány", 100),
        new(CharacterId.New(), "Pál", "E001", "Óriáspatkány", 100),
        new(CharacterId.New(), "Yorgrim", "E001", "Óriáspatkány", 100)
        ];
        var victorySummary = ConsoleRenderer.FormatBattleVictorySummary(true, 7, 17, kills);
        Assert(victorySummary ==
               "🏆🤖 HARC GYŐZELEM — ⌛7 🕧17 ☠ 3 🎖 300. " +
               "Iskra ☠ 1: 1× Óriáspatkány; Pál ☠ 1: 1× Óriáspatkány; Yorgrim ☠ 1: 1× Óriáspatkány.",
            $"Az autoharc győzelmi sora hibás: {victorySummary}");

        var retreatSummary = ConsoleRenderer.FormatBattleRetreatSummary(7, 17, kills);
        Assert(retreatSummary == "🏃 HARC VISSZAVONULÁS — ⌛7 🕧17 ☠ 3 🎖 300 XP.",
            $"A visszavonulási összefoglaló nem jelzi a megtartott öléseket és XP-t: {retreatSummary}");

        var (encounter, front, _, _) = CreateFormationEncounter();
        var poisoned = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName))
            .GetStatus(CharacterStatusIds.Poisoned);
        front.AddStatus(poisoned);
        encounter.CaptureNewStatuses();
        front.RemoveStatus(poisoned.Id);
        encounter.RecordSpellCast(front);
        encounter.RecordSpellCast(front);
        var recorded = encounter.ResultFor(front);
        Assert(recorded.GainedStatusIcons.SequenceEqual([poisoned.Icon]) && recorded.SpellsCast == 2,
            "A csata közben megszűnt állapot vagy a karakter varázslatszáma elveszett az összesítőből.");
    }

    static void BattleAttackUsesExistingCombatRules()
    {
        var system = CreateBattleSystem(1701);
        var fighter = CreateCharacter("Harcos", 30);
        var preparation = system.PrepareCharacter(fighter);
        Assert(preparation.Runtime.TryChooseTactic(fighter, BattleTactic.FighterPrecise),
            "A harcos nem tudta kiválasztani a meglévő pontos taktikát.");
        var enemy = CreateEnemy(30, 2, 1);
        system.BeginCharacterTurn(fighter);
        var entry = system.ResolveCharacterAttack(fighter, preparation.Runtime, enemy);
        Assert(entry.Message.Contains(fighter.Name, StringComparison.Ordinal) &&
               entry.Message.Contains(enemy.Name, StringComparison.Ordinal) &&
               !entry.Message.Contains("1. akció", StringComparison.Ordinal) && enemy.CurrentHitPoints <= 30 &&
               entry.Details is { } details &&
               details.Calculation.Any(line => line.StartsWith("🎯")) &&
               details.Calculation.Any(line => line.StartsWith("💥")),
            "A harci támadás nem a meglévő találat/sebzés naplóformátumot és HP-kezelést használja.");
    }

    static void BattleEngagementLastsUntilEnemyDeath()
    {
        var system = CreateBattleSystem(1702);
        var character = CreateCharacter("Lekötött hős");
        var enemy = CreateEnemy(20, 2);
        var preparation = system.PrepareCharacter(character);
        var encounter = new BattleEncounter(new Position(1, 1),
            [new BattleCharacterParticipant(character, new Position(1, 2), TacticalParticipantKind.PartyMember,
            preparation.Initiative, 3, 1, preparation.Runtime)],
            [new BattleEnemyParticipant(enemy, 5, 2, 1)], character.Id, enemy.Id);
        encounter.Engage(character, enemy);
        Assert(encounter.IsEngaged(character) && encounter.IsEngaged(enemy),
            "A közelharci páros nem került lekötött állapotba.");
        enemy.SetCurrentHitPoints(0);
        Assert(!encounter.IsEngaged(character),
            "A karaktert a legyőzött ellenfél továbbra is lekötve tartja.");
    }

    static void BattleFormationProtectsRearRow()
    {
        var (encounter, front, rear, _) = CreateFormationEncounter();
        Assert(encounter.HasActiveFormation && encounter.IsFrontRow(front) && encounter.IsRearRow(rear) &&
               encounter.RearPartnerOf(front) == rear && encounter.FrontPartnerOf(rear) == front,
            "A harc nem őrizte meg az alakzat sorait és oszloppárját.");
        Assert(encounter.IsProtectedRearTarget(rear, new Position(3, 2)) &&
               !encounter.IsProtectedRearTarget(rear, new Position(2, 4)) &&
               !encounter.IsProtectedRearTarget(rear, new Position(3, 5)),
            "Az első sor nem csak az alakzat eleje felől védi a hátsó társat.");
    }

    static void GuestDrawsOnlyLivingPartyAvatars()
    {
        var character = CreateCharacter("Snapshot hős");
        var living = new SessionCharacterSnapshot(character.Id, character.Name, character.Race.Id,
            character.CharacterClass.Id, character.Level, character.CurrentVitality, character.MaximumVitality,
            character.CurrentMana, character.MaximumMana, character.FoodLevel, character.WaterLevel, character.Gold,
            true, new Position(3, 4), [], null);

        Assert(CoopGuestScreen.ShouldDrawPartyAvatar(living),
            "Az élő, pozícióval rendelkező karakter avatárja eltűnt a guest térképről.");
        Assert(!CoopGuestScreen.ShouldDrawPartyAvatar(living with { IsAlive = false }),
            "Az elesett karaktert a guest továbbra is élő avatárként rajzolná.");
        Assert(!CoopGuestScreen.ShouldDrawPartyAvatar(living with { Position = null }),
            "A világpozíció nélküli karaktert a guest megpróbálná kirajzolni.");
    }

    static void MonsterStrengthCreatesTacticalPressure()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var attacker = CreateEnemy(100, 13);
        var plainDefender = CreateCharacter("Támasz nélkül", 100);
        var bracedDefender = CreateCharacter("Pajzsos", 100);
        Assert(bracedDefender.EquipWeapon(0, data.GetWeapon("W004")) &&
               bracedDefender.EquipWeapon(1, data.GetWeapon("W014")),
            "A pajzsos Erőpróba-teszt felszerelése sikertelen.");
        var plainSystem = CreateBattleSystem(1712);
        var bracedSystem = CreateBattleSystem(1712);
        var plainRuntime = plainSystem.PrepareCharacter(plainDefender).Runtime;
        var bracedRuntime = bracedSystem.PrepareCharacter(bracedDefender).Runtime;
        Assert(bracedRuntime.TryChooseTactic(bracedDefender, BattleTactic.FighterDefensive),
            "A védekező állás nem volt kiválasztható az Erőpróba tesztjében.");
        var plain = plainSystem.ResolveMonsterStrengthContest(attacker, plainDefender, plainRuntime);
        var braced = bracedSystem.ResolveMonsterStrengthContest(attacker, bracedDefender, bracedRuntime);
        Assert(plain.Roll == braced.Roll && plain.ResistanceRoll == braced.ResistanceRoll &&
               plain.Total == braced.Total && braced.StrengthPressure == (braced.Strength + 1) / 2 &&
               braced.ShieldBonus == 2 && braced.DefensiveBonus == 2 &&
               braced.Resistance == plain.Resistance + 4,
            "A pajzs vagy a védekező állás nem növelte helyesen az Erőpróba ellenállását.");
        Assert(plain.Outcome == (plain.Margin >= 5 ? MonsterStrengthContestOutcome.Push :
                   plain.Margin >= 1 ? MonsterStrengthContestOutcome.Stagger : MonsterStrengthContestOutcome.Resisted),
            "Az Erőpróba különbsége nem a megfelelő taktikai hatást választotta.");

        var race = new RaceDefinition("R-STRENGTH", "Ember", PrimaryAbilities.Zero);
        var fighterClass = new CharacterClassDefinition(CharacterClassIds.Harcos, "Harcos",
            PrimaryAbilities.Zero, false, 1.0);
        var sturdyDefender = new LiveCharacter("Szívós", race, fighterClass,
            new PrimaryAbilities(5, 5, 10, 5), 100, 0, 1, 0);
        Assert(sturdyDefender.EquipWeapon(0, data.GetWeapon("W004")) &&
               sturdyDefender.EquipWeapon(1, data.GetWeapon("W014")),
            "A szívós pajzsos tesztkarakter felszerelése sikertelen.");
        var sturdyRuntime = CreateBattleSystem(0).PrepareCharacter(sturdyDefender).Runtime;
        var kobold = CreateEnemy(30, 3);
        Assert(Enumerable.Range(0, 200).All(seed =>
                CreateBattleSystem(seed).ResolveMonsterStrengthContest(kobold, sturdyDefender, sturdyRuntime).Outcome ==
                MonsterStrengthContestOutcome.Resisted),
            "A 3-as Erővel rendelkező kobold szerencsével megtántoríthatta a 10-es Egészségű pajzsost.");

        var ordinaryDefender = CreateCharacter("Átlagos", 100);
        var ordinaryRuntime = CreateBattleSystem(0).PrepareCharacter(ordinaryDefender).Runtime;
        var strongMonster = CreateEnemy(100, 16);
        var strongOutcomes = Enumerable.Range(0, 200).Select(seed =>
            CreateBattleSystem(seed).ResolveMonsterStrengthContest(strongMonster, ordinaryDefender,
                ordinaryRuntime).Outcome).ToArray();
        Assert(strongOutcomes.Contains(MonsterStrengthContestOutcome.Resisted) &&
               strongOutcomes.Any(outcome => outcome is MonsterStrengthContestOutcome.Stagger or
                   MonsterStrengthContestOutcome.Push),
            "A 16-os szörnyerő próbája garantálttá vagy hatástalanná vált az átlagos célpont ellen.");

        var resistedDetails = BattleSystem.DescribeMonsterStrengthContest("Ogre", "Harcos", braced,
            MonsterStrengthContestOutcome.Resisted);
        var resistedLog = BattleSystem.MonsterStrengthCombatLogMessage("Harcos",
            MonsterStrengthContestOutcome.Resisted);
        var staggerLog = BattleSystem.MonsterStrengthCombatLogMessage("Harcos",
            MonsterStrengthContestOutcome.Stagger);
        var pushLog = BattleSystem.MonsterStrengthCombatLogMessage("Harcos",
            MonsterStrengthContestOutcome.Push);
        Assert(resistedLog is null && resistedDetails.Summary.Any(line => line.Contains("Erőpróba")) &&
               resistedDetails.Calculation.Any(line => line.Contains("Erőhatás")),
            "Az ellenállt Erőpróba nem csak a csatarészletek paneljére került.");
        Assert(staggerLog is not null && pushLog is not null &&
               !staggerLog.Contains("Erőpróba") && !pushLog.Contains("Erőpróba"),
            "A tényleges Erőhatás naplóbejegyzése még mindig kiírja a próba részleteit.");

        var character = CreateCharacter("Tántorgó", characterClassId: CharacterClassIds.Barbár);
        var enemy = CreateEnemyAt(new Position(8, 8), "E-STRENGTH");
        var system = CreateBattleSystem(1713);
        var preparation = system.PrepareCharacter(character);
        var encounter = new BattleEncounter(new Position(3, 3),
            [new BattleCharacterParticipant(character, new Position(3, 3), TacticalParticipantKind.PartyMember,
            100, 3, 1, preparation.Runtime)],
            [new BattleEnemyParticipant(enemy, 5, 2, 1)], character.Id, enemy.Id);
        encounter.Turns.StartTurns();
        Assert(encounter.TryBeginStrengthContest(enemy) && !encounter.TryBeginStrengthContest(enemy),
            "Ugyanaz a szörny egy körben többször kezdhetett Erőpróbát.");
        Assert(StaggerRules.Resolve(StaggerSeverity.Light, 25).BlocksOffensiveActions &&
               !StaggerRules.Resolve(StaggerSeverity.Light, 26).BlocksOffensiveActions &&
               StaggerRules.Resolve(StaggerSeverity.Normal, 45).BlocksOffensiveActions &&
               !StaggerRules.Resolve(StaggerSeverity.Normal, 46).BlocksOffensiveActions &&
               StaggerRules.Resolve(StaggerSeverity.Heavy, 70).BlocksOffensiveActions &&
               !StaggerRules.Resolve(StaggerSeverity.Heavy, 71).BlocksOffensiveActions,
            "A könnyű, normál vagy súlyos megingás 25/45/70%-os határa hibás.");
        Assert(encounter.StaggerCharacter(character, StaggerSeverity.Light) &&
               encounter.StaggerCharacter(character, StaggerSeverity.Heavy),
            "A karakter megingása nem jött létre, vagy nem erősödött fel.");
        Assert(encounter.StaggerFor(CombatantId.ForCharacter(character.Id)) is
        {
            Severity: StaggerSeverity.Heavy, IsResolved: false, BlocksMovement: true,
            BlocksOffensiveActions: null
        },
            "A függő megingás megjelenítési pillanatképe nem őrzi a fokozatot vagy a még ismeretlen kimenetelt.");
        var staggerRolls = 0;
        var firstResolution = encounter.PrepareStaggerAction(CombatantId.ForCharacter(character.Id), () =>
        {
            staggerRolls++;
            return 70;
        });
        var repeatedResolution = encounter.PrepareStaggerAction(CombatantId.ForCharacter(character.Id), () =>
        {
            staggerRolls++;
            return 100;
        });
        Assert(firstResolution is
        {
            Severity: StaggerSeverity.Heavy, BlocksMovement: true,
            BlocksOffensiveActions: true
        } && repeatedResolution == firstResolution && staggerRolls == 1,
            "A megingás nem a legerősebb fokozattal, vagy egy akcióban többször dobott.");
        Assert(encounter.StaggerFor(CombatantId.ForCharacter(character.Id)) is
        { Severity: StaggerSeverity.Heavy, IsResolved: true, BlocksOffensiveActions: true },
            "A feloldott megingás megjelenítési pillanatképe nem tartalmazza az akcióvesztést.");
        var combatCondition = new CombatConditionSnapshot(CombatConditionKind.Staggered,
            CombatConditionPresentation.StaggerName, CombatConditionPresentation.StaggerIcon,
            StaggerSeverity.Heavy, true, true, true);
        var participantSnapshot = new TacticalBattleParticipantSnapshot(
            CombatantId.ForCharacter(character.Id), character.Name, BattleSide.Friendly,
            TacticalParticipantKind.PartyMember, encounter.PositionOf(character), 100, 3, 1,
            TacticalParticipantState.Active, character.CurrentVitality, character.MaximumVitality, true,
            Conditions: [combatCondition]);
        Assert(CoopGuestScreen.IsStaggered(participantSnapshot),
            "A coop kliens nem ismeri fel a snapshot megingási állapotát.");
        var statusLine = CharacterSheetPanel.Build(character, data.ExperienceByLevel, 1, 0, 0,
                combatStatusIcons: [CombatConditionPresentation.StaggerIcon])
            .Single(line => line.Row == 8);
        Assert(statusLine.Text.Contains(CombatConditionPresentation.StaggerIcon) &&
               character.Statuses.All(status => status.Icon != CombatConditionPresentation.StaggerIcon),
            "A megingás nem az ideiglenes állapotsoron látszik, vagy bekerült a tartós karakterstátuszok közé.");
        var coordinator = new TacticalBattleCoordinator(data, system, new Random(1713));
        var actions = coordinator.GetAllowedBattleActions(encounter, character, enemy, character,
            encounter.PositionOf(character), false, new Dictionary<LiveCharacter, int>());
        Assert(!actions.Contains(BattleActionKind.Move) &&
               !actions.Contains(BattleActionKind.PhysicalAttack) &&
               !actions.Contains(BattleActionKind.ShieldBash) &&
               !actions.Contains(BattleActionKind.CastSpell) &&
               !actions.Contains(BattleActionKind.TurnUndead) &&
               actions.Contains(BattleActionKind.Pass),
            "A megingott karakter mozgási vagy támadó akciói nem a közös szabály szerint tiltódtak.");
        encounter.GrantExtraActions(1);
        encounter.AdvanceTurn();
        Assert(encounter.CurrentCharacter == character && !encounter.IsCharacterStaggered(character) &&
               !encounter.AreOffensiveActionsBlocked(CombatantId.ForCharacter(character.Id)),
            "A soron kívüli extra akció örökölte az előző akció megingását.");
        encounter.AdvanceTurn();
        encounter.AdvanceTurn();
        Assert(!encounter.IsCharacterStaggered(character) && encounter.TryBeginStrengthContest(enemy),
            "A megingás nem az akció végén múlt el, vagy az Erőpróba nem újult meg körváltáskor.");
    }

    static void RearCombatPreparationIsLeaderControlled()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var system = CreateBattleSystem(1710);
        var leader = CreateCharacter("Vezér", characterClassId: CharacterClassIds.Barbár);
        var rearLeft = CreateCharacter("Bal hátul", characterClassId: CharacterClassIds.Mágus);
        var rearRight = CreateCharacter("Jobb hátul", characterClassId: CharacterClassIds.Pap);
        var enemy = CreateEnemyAt(new Position(3, 2), "E-PREPARE");
        BattleCharacterParticipant Participant(LiveCharacter member, Position position)
        {
            var prepared = system.PrepareCharacter(member);
            return new BattleCharacterParticipant(member, position, TacticalParticipantKind.PartyMember,
                prepared.Initiative, 3, 1, prepared.Runtime);
        }

        var formation = new PartyFormationSnapshot(leader.Id, null, rearLeft.Id, rearRight.Id,
            Direction.Up, PartyFormationState.Locked);
        var encounter = new BattleEncounter(new Position(3, 3),
            [Participant(leader, new Position(3, 3)), Participant(rearLeft, new Position(3, 4)),
            Participant(rearRight, new Position(4, 4))],
            [new BattleEnemyParticipant(enemy, 5, 2, 1)], leader.Id, enemy.Id, formation: formation);
        var coordinator = new TacticalBattleCoordinator(data, system, new Random(1710));
        var actions = coordinator.GetAllowedBattleActions(encounter, leader, enemy, leader,
            new Position(3, 3), false, new Dictionary<LiveCharacter, int>());
        Assert(actions.Contains(BattleActionKind.PrepareRearLeft) &&
               actions.Contains(BattleActionKind.PrepareRearRight),
            "A vezér nem kapta meg mindkét hátsó alakzathely felkészítő akcióját.");

        Assert(encounter.TryOrderRearCombatPreparation(FormationSlot.RearLeft, out var ordered) &&
               ordered == rearLeft && encounter.ShouldPrioritizeRearSelfBuff(rearLeft) &&
               !encounter.ShouldPrioritizeRearSelfBuff(rearRight),
            "A bal és jobb hátsó felkészítési utasítás nem maradt elkülönítve.");

        var selfBuff = new SpellDefinition("TEST-SELF-BUFF", "Próbavédelem", SpellSchool.Arcane, 1, 1, "",
            SpellTargetType.Self, 0, 0, false, SpellUsageMode.Combat);
        var defense = new SpellEffectDefinition("TEST-DEFENSE", selfBuff.Id, 1, SpellEffectType.DefenseBonus,
            null, 1, 0, 0, 0, 100, SpellResolution.Auto, null, "");
        var casterPosition = encounter.PositionOf(rearLeft);
        Assert(coordinator.ChooseNpcBuffTarget(encounter, rearLeft, casterPosition, selfBuff, [defense],
                   [leader, rearLeft, rearRight], encounter.PositionOf, (_, _, _, _, _) => true,
                   allowSelfBuff: false) is null &&
               coordinator.ChooseNpcBuffTarget(encounter, rearLeft, casterPosition, selfBuff, [defense],
                   [leader, rearLeft, rearRight], encounter.PositionOf, (_, _, _, _, _) => true) == casterPosition,
            "A hátsó sori önbuff tiltása vagy vezetői engedélyezése nem működik.");

        Assert(encounter.TrySwapToRear(leader, out _, out _, out _, out _) &&
               !encounter.ShouldPrioritizeRearSelfBuff(rearLeft),
            "Az előresorolt tag megtartotta a csak hátsó sorban érvényes felkészítési utasítást.");
        var panel = BattleCommandPanel.Format([BattleActionKind.PrepareRearLeft, BattleActionKind.PrepareRearRight], true, "testactor");
        Assert(panel.Contains("B: bal hátul", StringComparison.Ordinal) &&
               panel.Contains("J: jobb hátul", StringComparison.Ordinal),
            "A két hátsó felkészítő parancs nem jelent meg külön a csatapanelen.");
        var roundSegments = BattleCommandPanel.WithRound(7,
            BattleCommandPanel.FormatWithHighlighting([BattleActionKind.PhysicalAttack]));
        Assert(string.Concat(roundSegments.Select(segment => segment.Text)).StartsWith("7. KÖR ",
                StringComparison.Ordinal) && roundSegments[0].Color == ConsoleColor.Cyan,
            "A közös csatapanel-formázó nem őrizte meg a körszámot vagy annak színét.");
    }

    static void BattleAiHealingPotionAvoidsWaste()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var system = CreateBattleSystem(1711);
        var character = CreateCharacter("Sebesült", vitality: 200, characterClassId: CharacterClassIds.Barbár);
        Assert(character.AddToBackpack(data.GetItem("T011")) &&
               character.AddToBackpack(data.GetItem("T012")) &&
               character.AddToBackpack(data.GetItem("T013")),
            "A gyógyital-választási teszt készlete nem fért el a hátizsákban.");
        character.SetCurrentResources(95, 0);
        var enemy = CreateEnemyAt(new Position(8, 8), "E-POTION");
        var prepared = system.PrepareCharacter(character);
        var encounter = new BattleEncounter(new Position(3, 3),
            [new BattleCharacterParticipant(character, new Position(3, 3), TacticalParticipantKind.PartyMember,
            prepared.Initiative, 3, 1, prepared.Runtime)],
            [new BattleEnemyParticipant(enemy, 5, 2, 1)], character.Id, enemy.Id);

        Assert(TacticalBattleCoordinator.ChooseNpcHealingPotionIndex(encounter, character, allowedWaste: 0) == 1,
            "A normál harci AI olyan gyógyitalt választott, amely HP-t pazarolna.");
        Assert(TacticalBattleCoordinator.ChooseNpcHealingPotionIndex(encounter, character, allowedWaste: 15) == 2,
            "A felkészített hátsó tag nem a legerősebb, legfeljebb 15 HP-t pazarló gyógyitalt választotta.");
        character.SetCurrentResources(196, 0);
        Assert(TacticalBattleCoordinator.ChooseNpcHealingPotionIndex(encounter, character, allowedWaste: 15) is null,
            "Az AI a 15 HP-s pazarlási határt meghaladó gyógyitalt választott.");
    }

    static void BattleSingleFileHasNoRearProtection()
    {
        var (encounter, front, rear, enemy) = CreateFormationEncounter(PartyFormationLayout.SingleFile);
        encounter.Engage(front, enemy);
        Assert(encounter.HasActiveFormation && !encounter.HasProtectiveFormation &&
               !encounter.IsProtectedRearTarget(rear, new Position(3, 2)) &&
               encounter.RearFormationEngagedEnemies(rear).Count == 0 &&
               !encounter.TrySwapToRear(front, out _, out _, out _, out _) &&
               encounter.FormationDestinations(Direction.Up).Count == 2,
            "A libasor felbomlott, vagy tévesen megkapta a 2×2-es alakzat harci előnyeit.");
    }

    static void BattleItemUseRequiresFreeRearPosition()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var combatItemIds = data.Items.Where(item => item.UsableInCombat).Select(item => item.Id).ToArray();
        Assert(combatItemIds.SequenceEqual(["T011", "T012", "T013", "T014", "T015", "T016"]),
            "Nem kizárólag a gyógy- és varázsitalok használhatók harcban a CSV szerint.");

        var (encounter, front, rear, enemy) = CreateFormationEncounter();
        var healingPotion = data.GetItem("T011");
        var food = data.GetItem("T004");
        Assert(!encounter.CanUseItem(front, healingPotion) && encounter.CanUseItem(rear, healingPotion) &&
               !encounter.CanUseItem(rear, food),
            "Az alakzat első és hátsó sorának tárgyhasználati szabálya hibás.");
        encounter.Engage(rear, enemy);
        Assert(!encounter.CanUseItem(rear, healingPotion),
            "A lekötött hátsó sori karakter tárgyat használhatott.");
        Assert(data.GetMagicItem("M004").Kind == MagicItemKind.Wand &&
               data.GetMagicItem("M002").Kind == MagicItemKind.Scroll,
            "A pálca vagy tekercs kikerült a külön varázslási tárgykategóriából.");
    }

    static void BattleRearPolearmReachUsesFrontEngagement()
    {
        var (encounter, front, rear, enemy) = CreateFormationEncounter();
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        Assert(rear.EquipWeapon(0, data.GetWeapon("W011")),
            "A hátsó sori tesztkarakter nem tudta felszerelni a szálfegyvert.");
        encounter.Engage(front, enemy);

        Assert(encounter.RearFormationEnemiesInReach(rear).SequenceEqual([enemy]),
            "A hátsó sori szálfegyver nem érte el az előtte álló társ lekötött ellenfelét.");
    }

    static void BattleSwapToRearTransfersEngagements()
    {
        var (encounter, front, rear, enemy) = CreateFormationEncounter();
        encounter.Engage(front, enemy);

        Assert(encounter.TrySwapToRear(front, out var swappedRear, out var oldFrontPosition,
                   out var oldRearPosition, out var transferred) && swappedRear == rear && transferred == 1,
            "A Hátra! nem hajtotta végre az első és hátsó társ helycseréjét.");
        Assert(encounter.FormationSlotFor(front) == FormationSlot.RearLeft &&
               encounter.FormationSlotFor(rear) == FormationSlot.FrontLeft &&
               encounter.Turns.Find(CombatantId.ForCharacter(front.Id))?.Position == oldRearPosition &&
               encounter.Turns.Find(CombatantId.ForCharacter(rear.Id))?.Position == oldFrontPosition &&
               !encounter.IsEngaged(front) && encounter.IsEngaged(rear),
            "A Hátra! nem cserélte fel atomian a slotokat, pozíciókat és lekötéseket.");
    }

    static void BattleFormationMovementPreservesEngagements()
    {
        var (encounter, front, _, enemy) = CreateFormationEncounter();
        encounter.Engage(front, enemy);
        var sideways = encounter.FormationDestinations(Direction.Right);
        var backward = encounter.FormationDestinations(Direction.Down);

        Assert(sideways.Count == 2 && encounter.PreservesEngagements(sideways) &&
               !encounter.PreservesEngagements(backward),
            "Az alakzatmozgás nem a fennálló közelharci lekötés megtartását követeli meg.");
    }

    static (BattleEncounter Encounter, LiveCharacter Front, LiveCharacter Rear, ConfiguredEnemy Enemy)
        CreateFormationEncounter(PartyFormationLayout layout = PartyFormationLayout.Block)
    {
        var system = CreateBattleSystem(1705);
        var front = CreateCharacter("Első sor");
        var rear = CreateCharacter("Hátsó sor");
        var enemy = CreateEnemyAt(new Position(3, 2), "E-FORMATION");
        var frontPreparation = system.PrepareCharacter(front);
        var rearPreparation = system.PrepareCharacter(rear);
        var formation = new PartyFormationSnapshot(front.Id, null, rear.Id, null,
            Direction.Up, PartyFormationState.Locked, layout);
        var encounter = new BattleEncounter(new Position(3, 3),
            [
                new BattleCharacterParticipant(front, new Position(3, 3), TacticalParticipantKind.PartyMember,
                frontPreparation.Initiative, 3, 1, frontPreparation.Runtime),
            new BattleCharacterParticipant(rear, new Position(3, 4), TacticalParticipantKind.PartyMember,
                rearPreparation.Initiative, 3, 1, rearPreparation.Runtime)
            ],
            [new BattleEnemyParticipant(enemy, 5, 2, 1)], front.Id, enemy.Id, formation: formation);
        return (encounter, front, rear, enemy);
    }

    static void BattleTargetCanBeChanged()
    {
        var system = CreateBattleSystem(1703);
        var character = CreateCharacter("Célpontváltó");
        var firstEnemy = CreateEnemy(20, 2);
        var secondEnemy = CreateEnemy(20, 2);
        var preparation = system.PrepareCharacter(character);
        var encounter = new BattleEncounter(new Position(1, 1),
            [new BattleCharacterParticipant(character, new Position(1, 2), TacticalParticipantKind.PartyMember,
            preparation.Initiative, 3, 1, preparation.Runtime)],
            [new BattleEnemyParticipant(firstEnemy, 5, 2, 1), new BattleEnemyParticipant(secondEnemy, 4, 2, 2)],
            character.Id, firstEnemy.Id);
        encounter.Turns.StartTurns();
        var turnId = encounter.Turns.TurnId;
        Assert(encounter.TrySelectTarget(secondEnemy) && encounter.SelectedTargetEnemy() == secondEnemy &&
               encounter.Turns.TurnId == turnId,
            "A célpontváltás előreléptette a körsorrendet vagy nem őrizte meg a célpontot.");
    }

    static void NpcSpellcastingPolicyPreservesMana()
    {
        var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
        var mageClass = new CharacterClassDefinition(CharacterClassIds.Mágus, "Mágus", PrimaryAbilities.Zero,
            true, 1.0);
        var mage = new LiveCharacter("Taktikus", race, mageClass, new PrimaryAbilities(5, 5, 5, 5),
            40, 20, 1, 0);
        mage.SetCurrentResources(14, 8);

        Assert(NpcSpellcastingPolicy.NeedsHealing(mage) && !NpcSpellcastingPolicy.IsEmergency(mage),
            "Az NPC gyógyítási küszöbe nem 35 százalék.");
        Assert(NpcSpellcastingPolicy.CanSpendMana(mage, 4) &&
               !NpcSpellcastingPolicy.CanSpendMana(mage, 5) &&
               NpcSpellcastingPolicy.CanSpendMana(mage, 5, emergency: true),
            "Az NPC nem tartja meg a 20 százalékos mannatartalékot, vagy vészhelyzetben sem oldja fel.");
        mage.SetCurrentResources(4, 8);
        Assert(NpcSpellcastingPolicy.IsEmergency(mage),
            "Az NPC nem ismeri fel a 10 százalékos gyógyítási vészhelyzetet.");

        var bolt = new SpellDefinition("TEST-BOLT", "Próbalövedék", SpellSchool.Arcane, 1, 2, "",
            SpellTargetType.Enemy, 6, 0, true, SpellUsageMode.Combat);
        var blast = bolt with { Id = "TEST-BLAST", AreaRadius = 1 };
        var damage = new SpellEffectDefinition("TEST-DAMAGE", bolt.Id, 1, SpellEffectType.Damage,
            new DiceExpression(1, 4), 0, 0, 0, 0, 100, SpellResolution.Auto, null, "");
        var chain = damage with { Id = "TEST-CHAIN", Type = SpellEffectType.ChainDamage };
        Assert(NpcSpellcastingPolicy.IsSingleTargetOffensive(bolt, [damage]) &&
               !NpcSpellcastingPolicy.IsSingleTargetOffensive(blast, [damage]) &&
               !NpcSpellcastingPolicy.IsSingleTargetOffensive(bolt, [damage, chain]) &&
               NpcSpellcastingPolicy.ActiveTypeFor(SpellEffectType.DefenseBonus) ==
               ActiveSpellEffectType.DefenseBonus,
            "Az NPC támadó- vagy buffvarázslat-besorolása hibás.");
    }

    static void BreakCurseRequiresUsefulPartyTarget()
    {
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory,
            CsvGameDataLoader.GameDataFileName));
        var spell = catalog.GetSpell("P027");
        var effects = catalog.GetSpellEffects(spell.Id);
        var service = new SpellExecutionService(catalog, new Random(1802));
        var ally = CreateCharacter("Tiszta");
        var classification = NpcSpellTacticalClassifier.Classify(spell, effects);

        Assert(spell.TargetType == SpellTargetType.PartyMember &&
               !classification.IsOffensive &&
               classification.AttackPattern == NpcSpellAttackPattern.None &&
               classification.Roles.HasFlag(NpcSpellTacticalRole.Cleanse),
            "Az Átoktörés célpontja vagy taktikai tisztító besorolása hibás.");
        Assert(!service.CanAffectCharacter(spell, ally),
            "Az Átoktörés tiszta csapattársat is érvényes célpontnak tekintett.");

        ally.ApplySpellEffect(new ActiveSpellEffect("TEST-CURSE", ActiveSpellEffectType.HitBonus,
            -2, 3, Beneficial: false));
        Assert(service.CanAffectCharacter(spell, ally),
            "Az Átoktörés nem ismerte fel a káros varázshatással sújtott csapattársat.");
        ally.RemoveSpellEffects(active => !active.Beneficial);
        Assert(!service.CanAffectCharacter(spell, ally),
            "Az Átoktörés a megtisztítás után továbbra is elsüthető maradt ugyanarra a csapattársra.");
    }

    static void CleansingHealRequiresRemovableStatus()
    {
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory,
            CsvGameDataLoader.GameDataFileName));
        var effects = catalog.GetSpellEffects("P014");
        var ally = CreateCharacter("Tiszta", vitality: 40);
        ally.ReceiveDamage(30);

        Assert(NpcSpellcastingPolicy.NeedsHealing(ally) &&
               !NpcSpellcastingPolicy.NeedsCleansing(ally, effects),
            "A Megtisztítás a pusztán sérült, de tiszta csapattársat is tisztítandó célpontnak tekintette.");

        ally.AddStatus(catalog.GetStatus(CharacterStatusIds.Poisoned));
        Assert(NpcSpellcastingPolicy.NeedsCleansing(ally, effects),
            "A Megtisztítás nem ismerte fel a mérgezett csapattársat.");
        ally.RemoveStatus(CharacterStatusIds.Poisoned);
        Assert(!NpcSpellcastingPolicy.NeedsCleansing(ally, effects),
            "A Megtisztítás a méreg levétele után is indokoltnak látszik.");
    }

    static void EngagedSupportSpellcastingIsThrottled()
    {
        Assert(NpcSpellcastingPolicy.UsesEngagedSpellCadence(CharacterClassIds.Pap) &&
               NpcSpellcastingPolicy.UsesEngagedSpellCadence(CharacterClassIds.Lovag) &&
               !NpcSpellcastingPolicy.UsesEngagedSpellCadence(CharacterClassIds.Mágus),
            "A lekötött varázslási ritkítás nem pontosan a papra és a lovagra vonatkozik.");
        Assert(!NpcSpellcastingPolicy.CanCastWhileEngaged(1, urgent: false) &&
               !NpcSpellcastingPolicy.CanCastWhileEngaged(2, urgent: false) &&
               NpcSpellcastingPolicy.CanCastWhileEngaged(3, urgent: false),
            "A lekötött pap vagy lovag rutinvarázslása nem minden harmadik csatakörre korlátozott.");
        Assert(NpcSpellcastingPolicy.CanCastWhileEngaged(1, urgent: true),
            "A lekötött pap vagy lovag sürgős gyógyítását vagy tisztítását is letiltotta a ritkítás.");
    }

    static void NpcOffensiveSpellStrengthThresholdsAreInclusive()
    {
        var tactics = NpcSpellcasterTactics.Default.StandardProfile;
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory,
            CsvGameDataLoader.GameDataFileName));
        var koboldStrength = catalog.GetEnemy("E002").Strength ?? 1;

        Assert(koboldStrength == 3 &&
               NpcSpellPlanningPolicy.EnemyStrength(Enumerable.Repeat(koboldStrength, 3)) == 9 &&
               NpcSpellPlanningPolicy.EnemyStrength([0, -2, 3]) == 5,
            "Az ellenség-összerő nem a résztvevők tényleges Erő tulajdonságát összegzi (3 koboldnak 9-et kell adnia).");
        Assert(!NpcSpellPlanningPolicy.ShouldCastOffensively(tactics, 7, offensiveSpellsCast: 0) &&
               NpcSpellPlanningPolicy.ShouldCastOffensively(tactics, 8, offensiveSpellsCast: 0),
            "A mágus alsó, 8-as összerőhatára nem inkluzív.");
        Assert(!NpcSpellPlanningPolicy.ShouldCastOffensively(tactics, 8, offensiveSpellsCast: 2) &&
               NpcSpellPlanningPolicy.ShouldCastOffensively(tactics, 16, offensiveSpellsCast: 2),
            "A mágus teljes támadásra váltó, 16-os összerőhatára nem inkluzív, vagy nem kezeli a kvótát.");
    }

    static void NpcSelfBuffUsesValueManaAndChance()
    {
        SpellEffectDefinition Buff(string id, SpellEffectType type, int value, int duration) =>
            new(id, "SELF-BUFF", 1, type, null, 0, 0, value, duration, 100,
                SpellResolution.Auto, null, "");

        var weakExpensive = NpcSpellcastingPolicy.BuffCastChancePercent(
            manaCost: 8, currentMana: 10, enemyStrength: 2, enemyCount: 1, beneficiaryCount: 1,
            casterIsEngaged: false, casterIsFrontRow: false,
            [Buff("WEAK", SpellEffectType.DefenseBonus, 1, 2)]);
        var strongEfficient = NpcSpellcastingPolicy.BuffCastChancePercent(
            manaCost: 2, currentMana: 20, enemyStrength: 20, enemyCount: 3, beneficiaryCount: 1,
            casterIsEngaged: true, casterIsFrontRow: true,
            [Buff("STRONG", SpellEffectType.DefenseBonus, 8, 5)]);
        var partyValue = NpcSpellcastingPolicy.BuffCastChancePercent(
            manaCost: 2, currentMana: 20, enemyStrength: 20, enemyCount: 3, beneficiaryCount: 4,
            casterIsEngaged: false, casterIsFrontRow: false,
            [Buff("PARTY-HIT", SpellEffectType.HitBonus, 2, 5),
         Buff("PARTY-DAMAGE", SpellEffectType.DamageBonus, 2, 5)]);
        var selfValue = NpcSpellcastingPolicy.BuffCastChancePercent(
            manaCost: 2, currentMana: 20, enemyStrength: 20, enemyCount: 3, beneficiaryCount: 1,
            casterIsEngaged: false, casterIsFrontRow: false,
            [Buff("SELF-HIT", SpellEffectType.HitBonus, 2, 5),
         Buff("SELF-DAMAGE", SpellEffectType.DamageBonus, 2, 5)]);

        Assert(weakExpensive == 0 && strongEfficient is >= 20 and <= 85 && partyValue > selfValue,
            "Az önbuff pontozása nem veti össze a hatást, a fenyegetést, a csapathasznot és a mannaköltséget.");
        Assert(NpcSpellcastingPolicy.ShouldCastBuff(strongEfficient, strongEfficient - 1) &&
               !NpcSpellcastingPolicy.ShouldCastBuff(strongEfficient, strongEfficient) &&
               !NpcSpellcastingPolicy.ShouldCastBuff(0, 0),
            "Az önbuff valószínűségi döntése nem a kiszámított esélyt használja.");
    }

    static void NpcSpellUtilityValuesTargetsAndOverkill()
    {
        var caster = CreateNpcSpellTestCaster();
        var spell = new SpellDefinition("TEST-UTILITY", "Próbavillám", SpellSchool.Arcane, 1, 6, "",
            SpellTargetType.Enemy, 8, 0, true, SpellUsageMode.Combat);
        var damage = new SpellEffectDefinition("FX-TEST-UTILITY", spell.Id, 1, SpellEffectType.Damage,
            new DiceExpression(2, 6), 1, 1, 0, 0, 100, SpellResolution.Auto, null, "");
        var targets = new[]
        {
        CreateNpcSpellTestEnemy("UTILITY-1", 40, 2, new Position(2, 2)),
        CreateNpcSpellTestEnemy("UTILITY-2", 40, 2, new Position(3, 2)),
        CreateNpcSpellTestEnemy("UTILITY-3", 40, 2, new Position(4, 2))
    };
        var single = NpcSpellPlanEvaluator.Evaluate(caster, spell, [damage],
            [new NpcSpellPlanTarget(targets[0])], spell.ManaCost);
        var area = NpcSpellPlanEvaluator.Evaluate(caster, spell with
        { Id = "TEST-AREA", TargetType = SpellTargetType.Area, AreaRadius = 1, ManaCost = 8 }, [damage],
            targets.Select(enemy => new NpcSpellPlanTarget(enemy)).ToArray(), 8);

        var weak = CreateNpcSpellTestEnemy("UTILITY-WEAK", 5, 1, new Position(2, 3));
        var cheapEffect = damage with
        {
            Id = "FX-TEST-CHEAP",
            Dice = new DiceExpression(1, 8),
            IntelligenceMultiplier = 0,
            LevelMultiplier = 0,
            Value = 2
        };
        var wastefulEffect = damage with
        {
            Id = "FX-TEST-WASTEFUL",
            Dice = new DiceExpression(20, 10),
            IntelligenceMultiplier = 0,
            LevelMultiplier = 0,
            Value = 0
        };
        var cheap = NpcSpellPlanEvaluator.Evaluate(caster, spell, [cheapEffect],
            [new NpcSpellPlanTarget(weak)], 1);
        var wasteful = NpcSpellPlanEvaluator.Evaluate(caster, spell with { ManaCost = 30 }, [wastefulEffect],
            [new NpcSpellPlanTarget(weak)], 30);

        Assert(area.Utility > single.Utility * 2 && area.UsefulDamage == single.UsefulDamage * 3,
            "A többcélú varázslat nem kapta meg a csoportsebzés hasznát.");
        Assert(cheap.Utility > wasteful.Utility && wasteful.Overkill > 100,
            "A pontozás nem büntette a gyenge célpontra pazarolt nagy varázslatot.");
    }

    static void NpcSpellMemoryBalancesVarietyAndUtility()
    {
        NpcOffensiveSpellMemory[] memories =
        [
            new("MAGIC-MISSILE", NpcSpellPlanComplexity.Simple, NpcSpellAttackPattern.SingleTarget, 1),
        new("MAGIC-MISSILE", NpcSpellPlanComplexity.Simple, NpcSpellAttackPattern.SingleTarget, 2)
        ];
        var repeated = NpcSpellPlanningPolicy.AdjustUtilityForMemory(memories, "MAGIC-MISSILE",
            NpcSpellPlanComplexity.Simple, NpcSpellAttackPattern.SingleTarget, 100);
        var unusedSimple = NpcSpellPlanningPolicy.AdjustUtilityForMemory(memories, "FROST-BOLT",
            NpcSpellPlanComplexity.Simple, NpcSpellAttackPattern.SingleTarget, 100);
        var unusedComplex = NpcSpellPlanningPolicy.AdjustUtilityForMemory(memories, "FIREBALL",
            NpcSpellPlanComplexity.Complex, NpcSpellAttackPattern.Area, 100);
        var clearlySuperiorRepeat = NpcSpellPlanningPolicy.AdjustUtilityForMemory(memories, "MAGIC-MISSILE",
            NpcSpellPlanComplexity.Simple, NpcSpellAttackPattern.SingleTarget, 250);
        var weakNovelty = NpcSpellPlanningPolicy.AdjustUtilityForMemory(memories, "FIREBALL",
            NpcSpellPlanComplexity.Complex, NpcSpellAttackPattern.Area, 40);

        Assert(unusedComplex > unusedSimple && unusedSimple > repeated,
            "A memória nem jutalmazza az egyszerű–összetett vagy repertoárváltást.");
        Assert(clearlySuperiorRepeat > weakNovelty,
            "A változatossági bónusz felülírt egy lényegesen jobb ismételt varázslatot.");
    }

    static void NpcSpellPlanRetentionIsStable()
    {
        Assert(NpcSpellPlanningPolicy.ShouldRetainPlan(100, 91, 100),
            "A 12 százalékon belüli aktív tervet nem tartotta meg a hiszterézis.");
        Assert(!NpcSpellPlanningPolicy.ShouldRetainPlan(100, 87, 100),
            "A megtartási tartományon kívüli tervet is megtartotta a hiszterézis.");
        Assert(!NpcSpellPlanningPolicy.ShouldRetainPlan(100, 50, 52),
            "Az eredeti hasznának 55 százaléka alá esett terv nem omlott össze.");
    }

    static void NpcSpellPlanInvalidationCoversFailureModes()
    {
        Assert(NpcSpellPlanningPolicy.CanReevaluatePlan(NpcSpellPlanStatus.SeekingPosition,
                targetIsAlive: true, spellIsAvailable: true, canSpendMana: true),
            "Egy megvalósítható, pozíciót kereső tervet érvénytelenített.");
        Assert(!NpcSpellPlanningPolicy.CanReevaluatePlan(NpcSpellPlanStatus.SeekingPosition,
                targetIsAlive: false, spellIsAvailable: true, canSpendMana: true) &&
               !NpcSpellPlanningPolicy.CanReevaluatePlan(NpcSpellPlanStatus.SeekingPosition,
                   targetIsAlive: true, spellIsAvailable: false, canSpendMana: true) &&
               !NpcSpellPlanningPolicy.CanReevaluatePlan(NpcSpellPlanStatus.SeekingPosition,
                   targetIsAlive: true, spellIsAvailable: true, canSpendMana: false) &&
               !NpcSpellPlanningPolicy.CanReevaluatePlan(NpcSpellPlanStatus.Failed,
                   targetIsAlive: true, spellIsAvailable: true, canSpendMana: true),
            "A halott célpont, elveszett varázslat, elfogyott mana vagy sikertelen terv nem érvénytelenítette a tervet.");
    }

    static void NpcSpellPlanMovementHonorsClassAndBattleState()
    {
        Assert(NpcSpellPlanningPolicy.CanMoveForPlan(isKnight: false, hasActiveFormation: false,
                isEngaged: false, isStaggered: false),
            "A szabad mágus nem kereshet tüzelőállást.");
        Assert(!NpcSpellPlanningPolicy.CanMoveForPlan(isKnight: true, hasActiveFormation: false,
                isEngaged: false, isStaggered: false) &&
               !NpcSpellPlanningPolicy.CanMoveForPlan(isKnight: false, hasActiveFormation: true,
                   isEngaged: false, isStaggered: false) &&
               !NpcSpellPlanningPolicy.CanMoveForPlan(isKnight: false, hasActiveFormation: false,
                   isEngaged: true, isStaggered: false) &&
               !NpcSpellPlanningPolicy.CanMoveForPlan(isKnight: false, hasActiveFormation: false,
                   isEngaged: false, isStaggered: true),
            "A lovag, alakzat, lekötés vagy tántorodás nem tiltotta le a tervhez mozgást.");
    }

    static void NpcSpellPositionPenaltyIncludesTravelAndDanger()
    {
        var current = NpcSpellPlanningPolicy.PositionPenalty(0, 3, 0);
        var distant = NpcSpellPlanningPolicy.PositionPenalty(4, 3, 0);
        var dangerous = NpcSpellPlanningPolicy.PositionPenalty(4, 3, 3);
        Assert(current == 0 && distant == 7 && dangerous == 13,
            $"A mozgási vagy veszélybüntetés hibás: {current}/{distant}/{dangerous}.");
    }

    static void NpcCasterPrefersFullSafeCastingMove()
    {
        Assert(NpcSpellPlanningPolicy.CanPreferSaferFullCastingMove(3, 4, 1, 100, 94),
            "A közeli ellenfélnél nem választható a közel azonos értékű, teljes mozgásnyi tüzelőállás.");
        Assert(!NpcSpellPlanningPolicy.CanPreferSaferFullCastingMove(6, 4, 1, 100, 100) &&
               !NpcSpellPlanningPolicy.CanPreferSaferFullCastingMove(3, 4, 0, 100, 100) &&
               !NpcSpellPlanningPolicy.CanPreferSaferFullCastingMove(3, 4, 1, 100, 80),
            "A teljes mozgás preferenciája biztonságos távolságban, mozgás nélkül vagy nagy hasznosságvesztéssel is aktiválódott.");
    }

    static void BattleStoresNpcSpellMemory()
    {
        var system = CreateBattleSystem(1801);
        var caster = CreateCharacter("Memóriamágus", characterClassId: CharacterClassIds.Mágus);
        var enemy = CreateNpcSpellTestEnemy("MEMORY-TARGET", 30, 2, new Position(2, 1));
        var preparation = system.PrepareCharacter(caster);
        var battle = new BattleEncounter(new Position(1, 1),
            [new BattleCharacterParticipant(caster, new Position(1, 1), TacticalParticipantKind.PartyMember,
            preparation.Initiative, 3, 1, preparation.Runtime)],
            [new BattleEnemyParticipant(enemy, 5, 2, 1)], caster.Id, enemy.Id);
        var first = new NpcSpellPlan(Guid.NewGuid(), "SPELL-SIMPLE", enemy.Id, enemy.Position,
            new Position(1, 1), NpcSpellPlanComplexity.Simple, NpcSpellAttackPattern.SingleTarget,
            NpcSpellTacticalRole.Damage, 1, 1, 20, NpcSpellPlanStatus.ReadyToCast);
        var second = first with
        {
            Id = Guid.NewGuid(),
            SpellId = "SPELL-AREA",
            Complexity = NpcSpellPlanComplexity.Complex,
            AttackPattern = NpcSpellAttackPattern.Area,
            ExpectedTargetCount = 3
        };
        battle.RecordNpcOffensiveSpellMemory(caster, first);
        battle.RecordNpcOffensiveSpellMemory(caster, second);
        var memories = battle.NpcOffensiveSpellMemoriesFor(caster);

        Assert(memories.Count == 2 && memories[0].SpellId == "SPELL-SIMPLE" &&
               memories[1].SpellId == "SPELL-AREA" &&
               memories.Select(memory => memory.Complexity).SequenceEqual(
                   [NpcSpellPlanComplexity.Simple, NpcSpellPlanComplexity.Complex]),
            "A csatamemória elvesztette a varázslatok sorrendjét vagy tervtípusát.");
    }

    static LiveCharacter CreateNpcSpellTestCaster()
    {
        var race = new RaceDefinition("R-NPC-SPELL", "Ember", PrimaryAbilities.Zero);
        var mageClass = new CharacterClassDefinition(CharacterClassIds.Mágus, "Mágus", PrimaryAbilities.Zero,
            true, 1.0);
        return new LiveCharacter("Tervmágus", race, mageClass, new PrimaryAbilities(5, 5, 5, 8),
            40, 100, 1, 0);
    }

    static ConfiguredEnemy CreateNpcSpellTestEnemy(string id, int hitPoints, int strengthTier,
        Position position) => new(position, new EnemyDefinition(id, id, "e", strengthTier, hitPoints,
            0, 1, 1, strengthTier, []));

    static void EngagementAdjustsSpellFailureChance()
    {
        var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
        var mageClass = new CharacterClassDefinition(CharacterClassIds.Mágus, "Mágus", PrimaryAbilities.Zero,
            true, 1.0);
        var mage = new LiveCharacter("Lekötött", race, mageClass, new PrimaryAbilities(5, 5, 5, 5),
            30, 30, 1, 0);
        Assert(SpellcastingRules.CombatFailureChance(mage, engaged: false) == 0 &&
               SpellcastingRules.CombatFailureChance(mage, engaged: true) == 35,
            "A szabad varázslásnak hibakockázata van, vagy a lekötött varázslás képlete hibás.");
    }

    static void BattleReinforcementJoinsNextCycle()
    {
        var system = CreateBattleSystem(1704);
        var character = CreateCharacter("Erősítéspróba");
        var enemy = CreateEnemy(20, 2);
        var reinforcement = CreateEnemy(20, 2);
        var preparation = system.PrepareCharacter(character);
        var encounter = new BattleEncounter(new Position(1, 1),
            [new BattleCharacterParticipant(character, new Position(1, 2), TacticalParticipantKind.PartyMember,
            preparation.Initiative, 3, 1, preparation.Runtime)],
            [new BattleEnemyParticipant(enemy, 5, 2, 1)], character.Id, enemy.Id);
        encounter.Turns.StartTurns();
        Assert(encounter.TryAddEnemy(new BattleEnemyParticipant(reinforcement, 99, 3, 2)) &&
               !encounter.Turns.InitiativeOrder.Any(participant =>
                   participant.Id == CombatantId.ForEnemy(reinforcement.Id)),
            "Az erősítés már a nyitó ütésváltásba bekerült.");
        encounter.AdvanceTurn();
        var next = encounter.AdvanceTurn();
        Assert(encounter.Turns.Cycle == 2 && next.Id == CombatantId.ForEnemy(reinforcement.Id),
            "Az erősítés nem a következő kör kezdeményezési sorrendjébe került.");
    }
}
