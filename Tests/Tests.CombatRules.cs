internal static partial class Program
{
    static void BattleCommandsAreValidated()
    {
        var (session, leader, _) = CreateSession();
        var battleId = BattleId.New();
        session.SetBattlePrompt(battleId, 1, leader.Id, [BattleActionKind.Move]);
        var move = new BattleActionCommand(session.HostPlayerId, 1, leader.Id, battleId, 1,
            BattleActionKind.Move, Target: new Position(4, 3));
        Assert(session.Submit(move) && session.TryReadCommand(out var acceptedMove) && acceptedMove == move,
            "A szemantikus harci mozgási parancsot elutasította a session.");

        session.SetBattlePrompt(battleId, 2, leader.Id, [BattleActionKind.UseItem]);
        var use = new BattleActionCommand(session.HostPlayerId, 2, leader.Id, battleId, 2,
            BattleActionKind.UseItem, BackpackIndex: 3);
        Assert(session.Submit(use) && session.TryReadCommand(out var acceptedUse) && acceptedUse == use,
            "A harci tárgyhasználati parancsot elutasította a session.");

        var enemyId = WorldEntityId.New();
        session.SetBattlePrompt(battleId, 3, leader.Id,
            [BattleActionKind.SelectTarget, BattleActionKind.Retreat]);
        var select = new BattleActionCommand(session.HostPlayerId, 3, leader.Id, battleId, 3,
            BattleActionKind.SelectTarget, TargetEnemyId: enemyId);
        Assert(session.Submit(select) && session.TryReadCommand(out var acceptedSelect) && acceptedSelect == select,
            "A célpontváltó parancsot elutasította a session.");
        var retreat = new BattleActionCommand(session.HostPlayerId, 4, leader.Id, battleId, 3,
            BattleActionKind.Retreat);
        Assert(session.Submit(retreat) && session.TryReadCommand(out var acceptedRetreat) && acceptedRetreat == retreat,
            "A visszavonulási parancsot elutasította a session.");

        session.SetBattlePrompt(battleId, 4, leader.Id,
            [BattleActionKind.MoveFormation, BattleActionKind.SwapToRear, BattleActionKind.PrepareRearLeft,
            BattleActionKind.PrepareRearRight, BattleActionKind.Pass]);
        var formationMove = new BattleActionCommand(session.HostPlayerId, 5, leader.Id, battleId, 4,
            BattleActionKind.MoveFormation, Target: new Position(5, 3));
        Assert(session.Submit(formationMove) && session.TryReadCommand(out var acceptedFormationMove) &&
               acceptedFormationMove == formationMove,
            "Az alakzatmozgatási parancsot elutasította a session.");
        var swap = new BattleActionCommand(session.HostPlayerId, 6, leader.Id, battleId, 4,
            BattleActionKind.SwapToRear);
        Assert(session.Submit(swap) && session.TryReadCommand(out var acceptedSwap) && acceptedSwap == swap,
            "A Hátra! parancsot elutasította a session.");
        var pass = new BattleActionCommand(session.HostPlayerId, 7, leader.Id, battleId, 4,
            BattleActionKind.Pass);
        Assert(session.Submit(pass) && session.TryReadCommand(out var acceptedPass) && acceptedPass == pass,
            "A passz parancsot elutasította a session.");
        var prepareLeft = new BattleActionCommand(session.HostPlayerId, 8, leader.Id, battleId, 4,
            BattleActionKind.PrepareRearLeft);
        var prepareRight = prepareLeft with { CommandId = 9, Action = BattleActionKind.PrepareRearRight };
        Assert(session.Submit(prepareLeft) && session.TryReadCommand(out var acceptedPrepareLeft) &&
               acceptedPrepareLeft == prepareLeft && session.Submit(prepareRight) &&
               session.TryReadCommand(out var acceptedPrepareRight) && acceptedPrepareRight == prepareRight,
            "A két hátsó felkészítő parancsot elutasította a session.");
        var malformedPass = pass with { CommandId = 10, Target = new Position(9, 9) };
        session.Submit(malformedPass);
        Assert(!session.TryReadCommand(out _),
            "A célponttal meghamisított passz parancs átjutott a session-validáción.");
    }

    static void EquipmentWeightAffectsMobility()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CharacterClassIds.Harcos };
        var race = new RaceDefinition("R-TEST", "Ember", PrimaryAbilities.Zero);
        var fighterClass = new CharacterClassDefinition(CharacterClassIds.Harcos, "Harcos",
            PrimaryAbilities.Zero, false, 1.0);
        var character = new LiveCharacter("Teherpróba", race, fighterClass,
            new PrimaryAbilities(8, 7, 5, 5), 30, 0, 1, 0);
        var light = CharacterMobilityRules.Evaluate(character);
        var weapon = new WeaponDefinition("W-HEAVY", "Nehéz fegyver", "WT001", new ValueRange(2, 4),
            1, false, allowed, "", 1, Weight: 10);
        var shield = weapon with
        {
            Id = "W-SHIELD",
            Name = "Nehéz pajzs",
            WeaponTypeId = "WT003",
            FamilyId = WeaponFamilies.Shield
        };
        var reserve = weapon with { Id = "W-RESERVE", Name = "Nehéz tartalékfegyver" };
        var armor = new ArmorDefinition("A-HEAVY", "Nehéz vért", new ValueRange(2, 4), allowed,
            "", 1, Weight: 14);
        var magicItem = new MagicItemDefinition("M-HEAVY", "Nehéz amulett", MagicItemKind.Amulet,
            ItemRarity.Normal, 1, 0, null, MagicItemEffect.None, 0, allowed, "", 0, Weight: 6);
        Assert(character.EquipWeapon(0, weapon) && character.EquipWeapon(1, shield) &&
               character.EquipWeapon(2, reserve) && character.EquipArmor(armor) &&
               character.AddMagicItem(magicItem),
            "A tesztfelszerelés nem volt felvehető.");
        var heavy = CharacterMobilityRules.Evaluate(character);
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));

        Assert(light.Encumbrance == EncumbranceLevel.Light && heavy.EquippedWeight == 50 &&
               heavy.CarriedWeight == 50 && heavy.CarryingCapacity == 44 &&
               heavy.CombatCarryingCapacity == 33 &&
               heavy.Encumbrance == EncumbranceLevel.Heavy &&
               heavy.InitiativeBase < light.InitiativeBase &&
               heavy.CombatMovementAllowance < light.CombatMovementAllowance &&
               data.GetWeapon("W001").Weight == 1 && data.GetWeapon("W009").Weight == 7 &&
               data.GetArmor("A006").Weight == 12 && Math.Abs(data.GetItem("T001").Weight - 0.2) < 0.001 &&
               Math.Abs(data.GetMagicItem("M001").Weight - 0.1) < 0.001,
            "A súlyadatok vagy a leterheltségi mozgásprofil hibás.");
    }

    static void MobilityPreviewIsVisible()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var race = new RaceDefinition("R-TEST", "Ember", PrimaryAbilities.Zero);
        var fighterClass = new CharacterClassDefinition(CharacterClassIds.Harcos, "Harcos",
            PrimaryAbilities.Zero, false, 1.0);
        var character = new LiveCharacter("Előnézet", race, fighterClass,
            new PrimaryAbilities(8, 7, 5, 5), 30, 0, 1, 0);
        Assert(character.EquipWeapon(0, data.GetWeapon("W004")) &&
               character.EquipArmor(data.GetArmor("A003")) &&
               character.AddToBackpack(data.GetArmor("A006")),
            "A mobilitási előnézet tesztfelszerelése nem volt előkészíthető.");
        var supplies = new MiscItemDefinition("I-WEIGHT", "Teherpróba", "Teszt", 1, Weight: 2);
        Assert(character.AddToBackpack(supplies) && character.AddToBackpack(supplies) &&
               character.AddToBackpack(supplies), "A többdarabos súlyteszt nem volt előkészíthető.");

        var snapshot = new SessionCharacterSnapshot(character.Id, character.Name, character.Race.Id,
            character.CharacterClass.Id, character.Level, character.CurrentVitality, character.MaximumVitality,
            character.CurrentMana, character.MaximumMana, character.FoodLevel, character.WaterLevel,
            character.Gold, character.IsAlive, null, [], InventorySnapshotProjector.Create(character),
            CharacterSheetSnapshotProjector.Create(character, data.ExperienceByLevel));
        var panel = CharacterSheetPanel.Build(snapshot, 1, 0, 12);
        var armorIndex = character.Backpack.ToList().FindIndex(item => item?.Id == "A006");
        var inspection = ItemInspectionFormatter.Format(data.GetArmor("A006"), data,
            mobilityContext: new ItemInspectionMobilityContext(snapshot, InventorySlotKind.Backpack, armorIndex));

        var loadLine = panel.Single(line => line.Row == 17);
        Assert(loadLine.Text + loadLine.ColoredSuffix == "FEGYVEREK ⚔ ⚖ 9/33  ⚡ 8" &&
               panel.Single(line => line.Row == 21).InventorySlot?.Kind == InventorySlotKind.Armor &&
               panel.Single(line => line.Row == 26).Text == $"HÁTIZSÁK 2/12 ⚖ {27.0:F1}/44" &&
               snapshot.CharacterSheet!.CarriedWeight == 27 &&
               snapshot.CharacterSheet.ExplorationMovementAllowance == 3 &&
               inspection.Text.Contains("súly: 12", StringComparison.Ordinal) &&
               inspection.Text.Contains("⚔ ⚖ 9 → 15", StringComparison.Ordinal) &&
               inspection.Text.Contains("Könnyű → Könnyű", StringComparison.Ordinal) &&
               inspection.Text.Contains("👣 4 → 4", StringComparison.Ordinal) &&
               inspection.Text.Contains("⚡ 8 → 8", StringComparison.Ordinal),
            $"A kompakt harci terhelés vagy a felszerelési előnézet hibás. Sor='{loadLine.Text + loadLine.ColoredSuffix}', " +
            $"hátizsák='{panel.Single(line => line.Row == 26).Text}', súly={snapshot.CharacterSheet!.CarriedWeight}, " +
            $"mozgás={snapshot.CharacterSheet.ExplorationMovementAllowance}, vizsgálat='{inspection.Text}'.");
    }

    static BattleSystem CreateBattleSystem(int seed) => new(new Random(seed),
        Array.Empty<MonsterAbilityDefinition>(), Array.Empty<StatusDefinition>(),
        Array.Empty<StrengthHitBonusDefinition>());

    static BattleSystem CreateMaximumBattleSystem() => new(new MaximumRandom(),
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

    static void TurnUndeadRefreshesAfterTenRounds()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var random = new Random(1);
        var system = CreateBattleSystem(1805);
        var actions = new BattleActionCoordinator(data, system, new SpellExecutionService(data, random), random);
        var tactical = new TacticalBattleCoordinator(data, system, new Random(1805));
        var priest = CreateCharacter("Pap", characterClassId: CharacterClassIds.Pap);
        var knight = CreateCharacter("Lovag", characterClassId: CharacterClassIds.Lovag);
        var position = new Position(3, 3);
        var definition = CreateEnemy(1000, 5).Definition with { Traits = EnemyTraits.Undead };
        var undead = new ConfiguredEnemy(new Position(3, 1), definition);
        BattleCharacterParticipant Participant(LiveCharacter character, Position cell) => new(character, cell,
            TacticalParticipantKind.PartyMember, 10, 3, 1, system.PrepareCharacter(character).Runtime);
        var battle = new BattleEncounter(position,
            [Participant(priest, position), Participant(knight, new Position(4, 3))],
            [new BattleEnemyParticipant(undead, 5, 2, 1)], priest.Id, undead.Id);
        battle.Turns.StartTurns();
        var cooldowns = new Dictionary<LiveCharacter, int>();
        bool Available(LiveCharacter character) => tactical.GetAllowedBattleActions(battle, character,
            undead, priest, battle.PositionOf(character), false, cooldowns).Contains(BattleActionKind.TurnUndead);
        void AdvanceTo(int round)
        {
            while (battle.Turns.Cycle < round) battle.Turns.AdvanceTurn();
        }

        Assert(Available(priest) && Available(knight), "Az első körben a képesség nem elérhető mindkét karakternek.");
        var failed = actions.ResolveTurnUndead(priest, undead, position, battle.Turns.Cycle, cooldowns);
        Assert(failed.Kind == BattleLogKind.Information && !Available(priest) && Available(knight),
            "A sikertelen halottűzés nem indított saját lehűlést, vagy a másik karaktert is letiltotta.");
        battle.Turns.RepeatCurrentTurn();
        Assert(!Available(priest), "Az extra akció túl korán megújította a képességet.");
        var blocked = false;
        try { actions.ResolveTurnUndead(priest, undead, position, battle.Turns.Cycle, cooldowns); }
        catch (InvalidOperationException) { blocked = true; }
        Assert(blocked, "A közvetlen végrehajtás átengedte az ismételt használatot ugyanabban a körben.");

        AdvanceTo(4);
        actions.ResolveTurnUndead(knight, undead, battle.PositionOf(knight), battle.Turns.Cycle, cooldowns);
        for (var round = 4; round <= 10; round++)
        {
            AdvanceTo(round);
            Assert(!Available(priest) && !Available(knight), "A halottűzés tíz kör eltelte előtt újult meg.");
        }
        AdvanceTo(11);
        Assert(Available(priest) && !Available(knight), "A 11. körben nem a megfelelő karakter képessége újult meg.");
        actions.ResolveTurnUndead(priest, undead, position, battle.Turns.Cycle, cooldowns);
        AdvanceTo(13);
        Assert(!Available(knight), "A 4. körben használt képesség már a 13. körben elérhető lett.");
        AdvanceTo(14);
        Assert(Available(knight) && !Available(priest), "A késleltetett első használat nem a 14. körre újult meg.");
        AdvanceTo(21);
        Assert(Available(priest), "A második tízkörös újrahasználati idő nem járt le.");
        Assert(BattleActionCoordinator.IsTurnUndeadReady(priest, 1, new Dictionary<LiveCharacter, int>()),
            "Egy új csata üres használati állapota nem az első körtől elérhető.");
    }

    static void TurnUndeadHasTwoCellRange()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var origin = new Position(5, 5);
        var definition = CreateEnemy(100, 3).Definition with { Traits = EnemyTraits.Undead };
        foreach (var classId in new[] { CharacterClassIds.Pap, CharacterClassIds.Lovag })
        {
            var character = CreateCharacter("Elűző", characterClassId: classId);
            foreach (var position in new[] { new Position(3, 3), new Position(5, 3), new Position(7, 5), new Position(7, 7) })
                Assert(BattleActionCoordinator.CanTurnUndead(character, new ConfiguredEnemy(position, definition), origin),
                    "A kétmezős, átlósan is érvényes hatótáv túl rövid.");
            var far = new ConfiguredEnemy(new Position(8, 5), definition);
            Assert(!BattleActionCoordinator.CanTurnUndead(character, far, origin), "A képesség három mezőre is hatott.");
            var near = new ConfiguredEnemy(new Position(7, 5), definition);
            var secondNear = new ConfiguredEnemy(new Position(5, 3), definition);
            var alive = CreateEnemyAt(new Position(5, 4), "E-NOT-UNDEAD");
            var system = CreateBattleSystem(1811);
            var battle = new BattleEncounter(origin,
                [new BattleCharacterParticipant(character, origin, TacticalParticipantKind.PartyMember,
                10, 3, 1, system.PrepareCharacter(character).Runtime)],
                new[] { near, secondNear, far, alive }.Select(enemy => new BattleEnemyParticipant(enemy, 5, 2, 1)),
                character.Id, near.Id);
            Assert(TacticalBattleCoordinator.TurnUndeadTargets(battle, character, origin).SequenceEqual([near, secondNear]),
                "Az alakzat nélküli célpontlista hibás vagy túl távoli élőholtat is tartalmaz.");
            battle.Turns.StartTurns();
            var tactical = new TacticalBattleCoordinator(data, system, new Random(1811));
            var openingActions = tactical.GetAllowedBattleActions(battle, character, alive, character, origin,
                false, new Dictionary<LiveCharacter, int>());
            Assert(openingActions.Contains(BattleActionKind.SelectTarget),
                "A nyitókör nem engedi a több, akcióspecifikusan érvényes célpont közötti váltást.");
            var snapshot = new BattleSnapshot(battle.Id, 1, 1, true, character.Id,
                new SessionEnemySnapshot(alive.Definition.Id, alive.Name, alive.Position,
                    alive.CurrentHitPoints, alive.CurrentHitPoints, alive.Id),
                [BattleActionKind.PhysicalAttack, BattleActionKind.ShieldBash, BattleActionKind.TurnUndead,
                BattleActionKind.SelectTarget],
                TurnUndeadTargetEnemyId: near.Id,
                ActionTargets:
                [
                    new BattleActionTargetsSnapshot(BattleActionKind.PhysicalAttack, [alive.Id]),
                new BattleActionTargetsSnapshot(BattleActionKind.ShieldBash, [alive.Id]),
                new BattleActionTargetsSnapshot(BattleActionKind.TurnUndead, [near.Id, secondNear.Id])
                ]);
            var restored = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!;
            Assert(restored.ActionTargets?.Single(option => option.Action == BattleActionKind.TurnUndead)
                       .EnemyIds.SequenceEqual([near.Id, secondNear.Id]) == true &&
                   CoopGuestScreen.TargetForAction(restored, BattleActionKind.TurnUndead) == near.Id &&
                   CoopGuestScreen.TargetForAction(restored, BattleActionKind.ShieldBash) == alive.Id,
                "A coop akciónkénti célpontlista elveszett, vagy a guest nem az adott akció érvényes célpontját választja.");
            near.ReceiveSpellDamage(near.CurrentHitPoints);
            secondNear.ReceiveSpellDamage(secondNear.CurrentHitPoints);
            Assert(!TacticalBattleCoordinator.TurnUndeadTargets(battle, character, origin).Any(),
                "A legyőzött élőholt elűzhető maradt.");
        }
        var warrior = CreateCharacter("Harcos");
        Assert(!BattleActionCoordinator.CanTurnUndead(warrior, new ConfiguredEnemy(new Position(5, 4), definition), origin),
            "A halottűzés jogosulatlan kaszt számára is elérhető lett.");
    }

    static void RearPriestCanTurnFrontEngagedUndead()
    {
        var system = CreateBattleSystem(1706);
        var front = CreateCharacter("Első sor", characterClassId: CharacterClassIds.Harcos);
        var priest = CreateCharacter("Hátsó pap", characterClassId: CharacterClassIds.Pap);
        var undead = CreateEnemyAt(new Position(3, 2), "E-REAR-UNDEAD");
        var undeadDefinition = undead.Definition with { Traits = EnemyTraits.Undead };
        undead = new ConfiguredEnemy(new Position(3, 2), undeadDefinition);
        var frontPreparation = system.PrepareCharacter(front);
        var priestPreparation = system.PrepareCharacter(priest);
        var formation = new PartyFormationSnapshot(front.Id, null, priest.Id, null,
            Direction.Up, PartyFormationState.Locked);
        var battle = new BattleEncounter(new Position(3, 3),
            [
                new BattleCharacterParticipant(front, new Position(3, 3), TacticalParticipantKind.PartyMember,
                frontPreparation.Initiative, 3, 1, frontPreparation.Runtime),
            new BattleCharacterParticipant(priest, new Position(3, 4), TacticalParticipantKind.PartyMember,
                priestPreparation.Initiative, 3, 1, priestPreparation.Runtime)
            ],
            [new BattleEnemyParticipant(undead, 5, 2, 1)], front.Id, undead.Id, formation: formation);
        battle.Engage(front, undead);
        battle.Turns.StartTurns();
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var coordinator = new TacticalBattleCoordinator(data, system, new Random(1706));
        var actions = coordinator.GetAllowedBattleActions(battle, priest, undead, priest,
            new Position(3, 4), false, new Dictionary<LiveCharacter, int>());

        Assert(TacticalBattleCoordinator.ReachableEnemies(battle, priest, new Position(3, 4)).Count() == 0 &&
               battle.RearFormationEngagedEnemies(priest).SequenceEqual([undead]) &&
               BattleActionCoordinator.CanTurnUndead(priest, undead) &&
               actions.Contains(BattleActionKind.TurnUndead),
            "A fegyverével nem támadó hátsó pap nem érte el Halottűzéssel az első sor által lekötött élőholtat.");
    }

    static void SpellcasterRetreatDistanceIsCapped()
    {
        var ground = CreateEnemyAt(new Position(1, 1), "E-GROUND");
        var flyingDefinition = ground.Definition with { Id = "E-FLYING", Traits = EnemyTraits.Flying };
        var flying = new ConfiguredEnemy(new Position(2, 2), flyingDefinition);
        Assert(TacticalBattleCoordinator.PreferredSpellcasterRetreatDistance([ground]) == 6 &&
               TacticalBattleCoordinator.PreferredSpellcasterRetreatDistance([ground, flying]) == 8,
            "A hátráló varázshasználó 6/8 mezős biztonsági távolsága hibás.");
    }

    static void TacticalAttackArcsUseEnemyFacing()
    {
        var system = CreateBattleSystem(1801);
        var attackers = new[]
        {
        (Character: CreateCharacter("Bal elöl"), Position: new Position(2, 4), Arc: TacticalAttackArc.Front),
        (Character: CreateCharacter("Elöl"), Position: new Position(3, 4), Arc: TacticalAttackArc.Front),
        (Character: CreateCharacter("Jobb elöl"), Position: new Position(4, 4), Arc: TacticalAttackArc.Front),
        (Character: CreateCharacter("Balról"), Position: new Position(2, 3), Arc: TacticalAttackArc.Flank),
        (Character: CreateCharacter("Jobbról"), Position: new Position(4, 3), Arc: TacticalAttackArc.Flank),
        (Character: CreateCharacter("Bal hátul"), Position: new Position(2, 2), Arc: TacticalAttackArc.Rear),
        (Character: CreateCharacter("Hátul"), Position: new Position(3, 2), Arc: TacticalAttackArc.Rear),
        (Character: CreateCharacter("Jobb hátul"), Position: new Position(4, 2), Arc: TacticalAttackArc.Rear)
    };
        var enemy = CreateEnemyAt(new Position(3, 3), "E-FACING");
        var participants = attackers.Select(attacker =>
        {
            var preparation = system.PrepareCharacter(attacker.Character);
            return new BattleCharacterParticipant(attacker.Character, attacker.Position,
                TacticalParticipantKind.PartyMember, preparation.Initiative, 3, 1, preparation.Runtime);
        }).ToArray();
        var front = attackers[1].Character;
        var battle = new BattleEncounter(new Position(3, 3),
            participants,
            [new BattleEnemyParticipant(enemy, 5, 2, 1)], front.Id, enemy.Id);

        foreach (var attacker in attackers)
        {
            var advantage = TacticalBattleCoordinator.AttackAdvantage(battle, attacker.Character, enemy);
            var expectedBonus = attacker.Arc switch
            {
                TacticalAttackArc.Flank => 1,
                TacticalAttackArc.Rear => 2,
                _ => 0
            };
            Assert(advantage.Arc == attacker.Arc && advantage.HitBonus == expectedBonus,
                $"A(z) {attacker.Character.Name} pozíció {attacker.Arc} ívének felismerése hibás.");
        }

        var flank = attackers[4].Character;
        battle.FaceEnemyToward(enemy, flank);
        Assert(TacticalBattleCoordinator.AttackAdvantage(battle, flank, enemy).Arc == TacticalAttackArc.Front,
            "Az ellenfél nem fordult az új célpont felé.");
    }

    static void ThiefCanBackstabFromRearFormation()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var system = CreateBattleSystem(1802);
        var front = CreateCharacter("Fedező", characterClassId: CharacterClassIds.Harcos);
        var thief = CreateCharacter("Orvtámadó", characterClassId: CharacterClassIds.Tolvaj);
        Assert(thief.EquipWeapon(0, data.GetWeapon("W001")), "A tolvaj nem tudta felszerelni a tőrt.");
        var enemy = CreateEnemyAt(new Position(3, 2), "E-REAR-THIEF");
        var frontPreparation = system.PrepareCharacter(front);
        var thiefPreparation = system.PrepareCharacter(thief);
        var formation = new PartyFormationSnapshot(front.Id, null, thief.Id, null,
            Direction.Up, PartyFormationState.Locked);
        var battle = new BattleEncounter(new Position(3, 3),
            [new BattleCharacterParticipant(front, new Position(3, 3), TacticalParticipantKind.PartyMember,
             frontPreparation.Initiative, 3, 1, frontPreparation.Runtime),
         new BattleCharacterParticipant(thief, new Position(3, 4), TacticalParticipantKind.PartyMember,
             thiefPreparation.Initiative, 3, 1, thiefPreparation.Runtime)],
            [new BattleEnemyParticipant(enemy, 5, 2, 1)], front.Id, enemy.Id, formation: formation);
        battle.Engage(front, enemy);
        Assert(battle.RearFormationEnemiesInReach(thief).Count == 0,
            "A képesség nélküli hátsó sori tolvaj elérte az ellenfelet.");
        Assert(thiefPreparation.Runtime.TryChooseTactic(thief, BattleTactic.ThiefAmbush) &&
               battle.RearFormationEnemiesInReach(thief).SequenceEqual([enemy]),
            "Az Orvtámadás nem nyitotta meg a hátsó sori tőrtámadást.");
        var entry = system.ResolveCharacterAttack(thief, thiefPreparation.Runtime, enemy,
            tacticalBackstab: true);
        Assert(entry.Details?.Calculation.Any(line => line.Contains("Hátbatámadás: Orvtámadás",
                   StringComparison.Ordinal)) == true,
            "A hátsó sori tőrtámadás nem aktiválta az Orvtámadás sebzésszorzóját.");
    }

    static void MultilineInnRumorStaysInsideFrame()
    {
        var rumor = new InnRumorSnapshot("🗺️ Nyom az előző pályáról",
            ["Egy zilált vándor új mozgásról beszél a már elhagyott járatokban.\r\n" +
         "Nem a teljes szörnyhorda tért vissza, de valami érdemes lehet még odalent."],
            ConsoleColor.Yellow);
        var method = typeof(ConsoleRenderer).GetMethod("BuildInnRumorLines",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var lines = (IReadOnlyList<(string Text, ConsoleColor Color)>)method.Invoke(
            null, [rumor, 2, 5, "Tesztfogadó", null])!;

        Assert(lines.Any(line => line.Text.StartsWith("Egy zilált vándor", StringComparison.Ordinal)) &&
               lines.Any(line => line.Text.StartsWith("Nem a teljes szörnyhorda", StringComparison.Ordinal)) &&
               lines.All(line => !line.Text.Contains('\r') && !line.Text.Contains('\n') &&
                                 line.Text.Length <= 104),
            "A beágyazott sortörés kijutott a keret rajzolásához átadott sorból.");
    }

    static void DevelopmentWeaponsRespectCapacity()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var character = CreateCharacter("Tesztcsomag");
        var granted = DevelopmentWeaponGrantService.Grant(character, data.Weapons, new Random(42));
        Assert(granted.Count == 6 && granted.Select(weapon => weapon.Id).Distinct().Count() == 6 &&
            granted.Count(weapon => weapon.Rarity == ItemRarity.Normal && !weapon.IsTwoHanded) == 2 &&
            granted.Count(weapon => weapon.Rarity == ItemRarity.Normal && weapon.IsTwoHanded) == 2 &&
            granted.Count(weapon => weapon.Rarity == ItemRarity.Magic) == 1 &&
            granted.Count(weapon => weapon.Rarity == ItemRarity.Legendary) == 1 &&
            granted.All(weapon => weapon.WeaponTypeId != "WT003" && !SpellcastingRules.IsRestrictedFromTradingAndGeneration(weapon)),
            "A fejlesztői csomag összetétele hibás.");
        Assert(character.Backpack.Count(item => item is WeaponDefinition) == 6 && character.WeaponSlots.All(item => item is null),
            "A csomag nem a hátizsákba került.");
        var limited = CreateCharacter("Kevéshely");
        for (var index = 0; index < LiveCharacter.MaximumBackpackItemCount - 1; index++)
            limited.SetInventoryItem(InventorySlotKind.Backpack, index, data.GetItem("T001"));
        var partial = DevelopmentWeaponGrantService.Grant(limited, data.Weapons, new Random(42));
        Assert(partial.Count == 1 && limited.Backpack.Take(11).All(item => item?.Id == "T001"),
            "A részleges csomag felülírta a hátizsák tartalmát.");
        var full = CreateCharacter("Telthátizsák");
        for (var index = 0; index < LiveCharacter.MaximumBackpackItemCount; index++)
            full.SetInventoryItem(InventorySlotKind.Backpack, index, data.GetItem("T001"));
        var revision = full.InventoryRevision;
        Assert(DevelopmentWeaponGrantService.Grant(full, data.Weapons, new Random(42)).Count == 0 && full.InventoryRevision == revision,
            "A telt hátizsák módosult a sikertelen kiosztástól.");
    }

    static void DeveloperBattleTestScenarioHasRequestedLayout()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var options = new DeveloperBattleTestOptions(12, 6, 12);
        var scenario = DeveloperBattleTestScenarioBuilder.Create(ConsoleRenderer.PlayfieldWidth,
            ConsoleRenderer.PlayfieldHeight, options, data.Enemies, new Random(4201), 30);

        Assert(scenario.EnemyGroups.Count == 6 && scenario.EnemyGroups.All(group => group.Count == 12) &&
               scenario.Maze.Enemies.Count == 72 && scenario.GroupMarkers.Count == 6 &&
               scenario.Maze.TreasureChests.Count == 6,
            "A tesztpálya nem a kért számú csoportot, ellenfelet vagy jelölőládát készítette.");
        Assert(scenario.EnemyGroups.Select(group => group[0].GroupId).Distinct().Count() == 6 &&
               scenario.EnemyGroups.All(group => group.Select(enemy => enemy.GroupId).Distinct().Count() == 1),
            "Az ellenfelek csoportazonosítói összekeveredtek.");
        Assert(scenario.EnemyGroups.SelectMany(group => group).All(enemy =>
            enemy.Position.Y < scenario.LeaderPosition.Y &&
            Math.Max(Math.Abs(enemy.Position.X - scenario.LeaderPosition.X),
                Math.Abs(enemy.Position.Y - scenario.LeaderPosition.Y)) is >=
                    DeveloperBattleTestScenarioBuilder.MinimumEnemyDistance and <=
                    DeveloperBattleTestScenarioBuilder.MaximumEnemyDistance),
            $"Egy ellenfél nem a felső térfélen vagy nem " +
            $"{DeveloperBattleTestScenarioBuilder.MinimumEnemyDistance}–" +
            $"{DeveloperBattleTestScenarioBuilder.MaximumEnemyDistance} mezős távolságban áll.");
        Assert(scenario.EnemyGroups.Zip(scenario.GroupMarkers).All(pair => pair.First.Any(enemy =>
            Math.Abs(enemy.Position.X - pair.Second.Position.X) +
            Math.Abs(enemy.Position.Y - pair.Second.Position.Y) == 1)),
            "Egy jelölőláda nem a saját ellenségcsoportja mellett áll.");

        var corridor = scenario.CorridorTopLeft;
        for (var offset = 0; offset < DeveloperBattleTestScenarioBuilder.CorridorLength; offset++)
        {
            Assert(scenario.Maze.IsWalkable(new Position(corridor.X, corridor.Y + offset)) &&
                   scenario.Maze.IsWalkable(new Position(corridor.X + 1, corridor.Y + offset)) &&
                   scenario.Maze.BlocksSight(new Position(corridor.X - 1, corridor.Y + offset)) &&
                   scenario.Maze.BlocksSight(new Position(corridor.X + 2, corridor.Y + offset)),
                "A középső 2×8-as folyosó járható szélessége vagy oldalfala hibás.");
        }
    }

    static void LoadedDeveloperBattleCreatesRecoveryLog()
    {
        var system = CreateBattleSystem(4210);
        var character = CreateCharacter("Loghős");
        var enemy = CreateNpcSpellTestEnemy("LOG-ENEMY", 30, 2, new Position(2, 1));
        var preparation = system.PrepareCharacter(character);
        var battle = new BattleEncounter(new Position(1, 1),
            [new BattleCharacterParticipant(character, new Position(1, 1), TacticalParticipantKind.PartyMember,
            preparation.Initiative, 3, 1, preparation.Runtime)],
            [new BattleEnemyParticipant(enemy, 5, 2, 1)], character.Id, enemy.Id);
        battle.Turns.StartTurns();

        var log = new DeveloperBattleLog();
        log.BeginBattle(battle);
        log.CompleteBattle(battle, "defeat");
        var path = log.FilePath;
        Assert(path is not null && File.Exists(path),
            "A scenario-inicializálás nélkül indított tesztcsata nem hozott létre naplófájlt.");
        using var stream = new FileStream(path!, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var text = reader.ReadToEnd();
        Assert(text.Contains("[LOG-START] reason=battle-recovery", StringComparison.Ordinal) &&
               text.Contains("[SCENARIO-RECOVERY]", StringComparison.Ordinal) &&
               text.Contains("[BATTLE-START]", StringComparison.Ordinal) &&
               text.Contains("[BATTLE-END]", StringComparison.Ordinal) &&
               text.Contains("outcome=defeat", StringComparison.Ordinal),
            "A helyreállított csatalogból hiányzik a kezdet, a csataállapot vagy a vereségi lezárás.");
        File.Delete(path!);
    }

    static void CombatTestCharactersMatchRequestedLevelAndSpells()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var generator = new RandomCharacterGenerator(data, new Random(4202));
        var characters = new[] { CharacterClassIds.Mágus, CharacterClassIds.Pap, CharacterClassIds.Lovag }
            .Select(classId => generator.GenerateCombatTestCharacter(data.GetCharacterClass(classId), 12, []))
            .ToArray();

        Assert(characters.All(character => character.Level == 12 && character.IsAlive &&
                                         character.CurrentVitality == character.MaximumVitality &&
                                         character.CurrentMana == character.MaximumMana &&
                                         SpellcastingRules.HasRequiredFocus(character)),
            "A teszt-NPC szintje, erőforrása vagy varázsfókusza hibás.");
        Assert(characters.All(character => character.MemorizedSpells.Count ==
                                          Math.Min(character.KnownSpells.Count, character.MemorizationCapacity) &&
                                          character.MemorizedSpells.All(spell =>
                                              spell.Level <= SpellcastingRules.MaximumSpellLevel(character.CharacterClass.Id, character.Level) &&
                                              character.KnownSpells.Any(known => known.Id == spell.Id))),
            "A teszt-NPC nem az ismert, szintjén elérhető varázslataiból memorizált.");
        Assert(characters.All(character => character.ActiveWeapons.Any(weapon => weapon is not null)),
            "Egy teszt-NPC nem kapta meg a kaszt alapfegyverzetét.");
    }

    static void KnightBattleWeaponSwapCommandIsAccepted()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var knight = new LiveCharacter("Tesztlovag", data.GetRace("R001"),
            data.GetCharacterClass(CharacterClassIds.Lovag), new PrimaryAbilities(8, 5, 5, 5), 40, 0, 1, 0);
        Assert(knight.EquipWeapon(0, data.GetWeapon("W004")) &&
               knight.EquipWeapon(1, data.GetWeapon("W014")) &&
               knight.EquipWeapon(2, data.GetWeapon("W010")), "A kard–pajzs–csatabárd felszerelés nem állítható be.");
        var party = new Party(); party.SetLeader(knight);
        var session = new GameSession(party, knight);
        var system = CreateBattleSystem(42);
        var preparation = system.PrepareCharacter(knight);
        var rat = new ConfiguredEnemy(new(3, 2), data.GetEnemy("E001"));
        var battle = new BattleEncounter(new(3, 3),
            [new BattleCharacterParticipant(knight, new(3, 3), TacticalParticipantKind.PartyMember,
            preparation.Initiative, 3, 1, preparation.Runtime)],
            [new BattleEnemyParticipant(rat, 1, 1, 1)], knight.Id, rat.Id);
        var coordinator = new TacticalBattleCoordinator(data, system, new Random(42));
        var allowed = coordinator.GetAllowedBattleActions(battle, knight, rat, knight, new(3, 3), false,
            new Dictionary<LiveCharacter, int>());
        Assert(allowed.Contains(BattleActionKind.SwapWeapon), "A panel nem kínálja fel a fegyvercserét.");
        var battleId = BattleId.New();
        session.SetBattlePrompt(battleId, 1, knight.Id, allowed);
        var swap = new BattleActionCommand(session.HostPlayerId, 1, knight.Id, battleId, 1, BattleActionKind.SwapWeapon);
        Assert(session.Submit(swap) && session.TryReadCommand(out var accepted) && accepted == swap,
            "A panelen felkínált fegyvercsere-parancsot elutasította a session.");
        Assert(knight.TrySwapReserveWeapon() && knight.WeaponSlots[0]?.Id == "W010" &&
               knight.WeaponSlots[1]?.Id == "W014" && knight.WeaponSlots[2]?.Id == "W004",
            "A kard és csatabárd cseréje nem őrizte meg a pajzsot.");
        session.Submit(swap with { CommandId = 2, Target = new(9, 9) });
        Assert(!session.TryReadCommand(out _), "A célpontot tartalmazó hibás fegyvercsere átjutott.");
        session.SetBattlePrompt(battleId, 2, knight.Id, [BattleActionKind.Pass]);
        session.Submit(swap with { CommandId = 3, TurnId = 2 });
        Assert(!session.TryReadCommand(out _), "A fel nem kínált fegyvercsere átjutott.");
    }

    static void WeaponSweepRequiresMutualAdjacency()
    {
        var (battle, front, _, primary) = CreateFormationEncounter();
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var weapon = data.GetWeapon("W009") with { MinimumStrength = 1 };
        Assert(front.EquipWeapon(0, weapon), "A pallos nem szerelhető fel.");
        var opposite = CreateEnemyAt(new Position(3, 4), "E-OPPOSITE");
        var distant = CreateEnemyAt(new Position(3, 1), "E-DISTANT");
        var side = CreateEnemyAt(new Position(4, 2), "E-SIDE");
        foreach (var enemy in new[] { opposite, distant, side })
            Assert(battle.TryAddEnemy(new(enemy, 1, 1, 1)), "Nem csatlakozott a célpont.");
        Assert(TacticalBattleCoordinator.SweepTargets(battle, front, new(3, 3), primary)
            .SequenceEqual([primary, side]), "Távoli vagy átellenes célpont bekerült a csapásba.");
        side.SetCurrentHitPoints(0);
        Assert(TacticalBattleCoordinator.SweepTargets(battle, front, new(3, 3), primary).Count == 1,
            "A halott célpontot vagy az átellenes ellenfelet elérte a csapás.");
        Assert(front.EquipWeapon(0, data.GetWeapon("W004")), "A kard nem szerelhető fel.");
        side.SetCurrentHitPoints(10);
        Assert(TacticalBattleCoordinator.SweepTargets(battle, front, new(3, 3), primary).Count == 1,
            "Az egycélpontos kard több ellenfelet ért el.");
    }

    static void TacticalWeaponMasteriesHaveDistinctRoles()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var fighter = CreateCharacter("Taktikus", characterClassId: CharacterClassIds.Harcos);
        var system = CreateBattleSystem(7);
        var runtime = system.PrepareCharacter(fighter).Runtime;
        Assert(runtime.TryChooseTactic(fighter, BattleTactic.FighterPowerful) &&
               TacticalBattleCoordinator.SweepDamagePercent(fighter, runtime, true) == 100,
            "Az Erőteljes állás nem ad teljes erejű söprést.");

        var mage = CreateCharacter("Botharcos", characterClassId: CharacterClassIds.Mágus);
        var staff = data.GetWeapon("W018") with { MinimumStrength = 1 };
        Assert(mage.EquipWeapon(0, staff) && mage.TryAdvanceWeaponProficiency(WeaponFamilies.Staff) &&
               mage.TryAdvanceWeaponProficiency(WeaponFamilies.Staff), "A botmester tesztfelszerelése hibás.");
        var withoutStaff = CreateCharacter("Fókusz", characterClassId: CharacterClassIds.Mágus);
        Assert(SpellcastingRules.CombatFailureChance(mage, true) ==
               Math.Max(0, SpellcastingRules.CombatFailureChance(withoutStaff, true) - 10),
            "A botmester nem csökkenti tíz százalékponttal a harci varázskudarcot.");

        var swordMaster = CreateCharacter("Kardőr", characterClassId: CharacterClassIds.Harcos);
        var protectedAlly = CreateCharacter("Védett", characterClassId: CharacterClassIds.Harcos);
        Assert(swordMaster.EquipWeapon(0, data.GetWeapon("W004")) &&
               swordMaster.TryAdvanceWeaponProficiency(WeaponFamilies.Sword) &&
               swordMaster.TryAdvanceWeaponProficiency(WeaponFamilies.Sword),
            "A kardmester tesztfelszerelése hibás.");
        var swordPreparation = system.PrepareCharacter(swordMaster);
        var allyPreparation = system.PrepareCharacter(protectedAlly);
        var guardEnemy = CreateEnemyAt(new Position(2, 1), "SWORD-GUARD");
        var guardBattle = new BattleEncounter(new Position(1, 1),
            [new BattleCharacterParticipant(swordMaster, new Position(1, 1), TacticalParticipantKind.PartyMember,
             swordPreparation.Initiative, 3, 1, swordPreparation.Runtime),
         new BattleCharacterParticipant(protectedAlly, new Position(1, 2), TacticalParticipantKind.PartyMember,
             allyPreparation.Initiative, 3, 1, allyPreparation.Runtime)],
            [new BattleEnemyParticipant(guardEnemy, 1, 2, 1)], protectedAlly.Id, guardEnemy.Id);
        Assert(TacticalBattleCoordinator.AlliedGuardDefense(guardBattle, protectedAlly,
                   candidate => guardBattle.PositionOf(candidate)) == 1,
            "A kardmester nem adott +1 fedezetet a szomszédos társának.");

        var milestones = CharacterProgressionService.UpcomingMilestones(fighter);
        Assert(milestones.Any(text => text.Contains("képességpont", StringComparison.OrdinalIgnoreCase)) &&
               milestones.Any(text => text.Contains("tehetség", StringComparison.OrdinalIgnoreCase)),
            "A szintlépési előnézetből hiányzik a következő fejlődés.");
    }

    static void ShieldTierDrivesBlockAndBash()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var wood = data.GetWeapon("W014");
        var iron = data.GetWeapon("W015");
        var steel = data.GetWeapon("W016");
        var legendary = data.GetWeapon("LW013");
        Assert(wood.ShieldTier == 1 && iron.ShieldTier == 2 && steel.ShieldTier == 3 &&
               legendary.ShieldTier == 4,
            "A pajzsok CSV-s tierbesorolása hibás.");
        var damagedSnapshot = new ShieldDefenseSnapshot(legendary, WeaponProficiencyRank.Master, true,
            EquipmentCondition.Damaged);
        var guaranteedBlock = ShieldRules.ResolveCriticalBlock(
            new ShieldDefenseSnapshot(steel, WeaponProficiencyRank.Trained), DamageType.Slashing, 17);
        var elementalAttempt = ShieldRules.ResolveCriticalBlock(
            new ShieldDefenseSnapshot(legendary), DamageType.Fire, 20);
        Assert(ShieldRules.CriticalBlockRating(wood) == 1 &&
               ShieldRules.CriticalBlockRating(steel, WeaponProficiencyRank.Trained) == 4 &&
               ShieldRules.CriticalBlockRating(legendary, WeaponProficiencyRank.Master, true) == 4 &&
               ShieldRules.StaggerWeightBonus(wood) == 2 &&
               ShieldRules.StaggerWeightBonus(steel) == 3 &&
               ShieldRules.StaggerWeightBonus(data.GetWeapon("W032")) == 4 &&
               damagedSnapshot.BlockRating == 1 && guaranteedBlock is
               { Attempted: true, Roll: 17, BlockRating: 4, IsCriticalBlock: true } &&
               elementalAttempt == ShieldBlockResult.NotAttempted,
            "A pajzstier, jártasság vagy Pajzsfal nem a közös 5–20 százalékos blokkszabályt használja.");

        var fighter = CreateCharacter("Pajzslökő", characterClassId: CharacterClassIds.Harcos);
        Assert(fighter.EquipWeapon(1, steel with { MinimumStrength = 1 }) &&
               fighter.TryAdvanceWeaponProficiency(WeaponFamilies.Shield) &&
               fighter.TryAdvanceWeaponProficiency(WeaponFamilies.Shield),
            "A pajzslökés tesztkaraktere nem állítható elő.");
        var enemy = CreateEnemy(100, 5);
        var system = CreateBattleSystem(2401);
        var wearBefore = fighter.GetInventoryItemState(InventorySlotKind.Weapon, 1)!.Value.DurabilityDamage;
        var bash = system.ResolvePlayerShieldBash(fighter, enemy, fighter.WeaponSlots[1]!);
        var wearAfter = fighter.GetInventoryItemState(InventorySlotKind.Weapon, 1)!.Value.DurabilityDamage;
        Assert(bash.ShieldPower == 5 && bash.ShieldWeightBonus == 3 &&
               bash.AttackTotal == bash.AttackerRoll + bash.StrengthPressure + 5 + 3 &&
               bash.DefenseTotal == bash.DefenderRoll + bash.DefenderStability + bash.DefenderShieldBonus &&
               (bash.Outcome == MonsterStrengthContestOutcome.Resisted) == (bash.Damage == 0) &&
               wearAfter == wearBefore + 1,
            "A pajzslökés nem a tiert és jártasságot használta, vagy nem koptatta a pajzsot.");
    }

    static void DefensiveInterventionsReachBattleLogs()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var shieldBearer = CreateCharacter("Pajzshordozó", vitality: 1000);
        Assert(shieldBearer.EquipWeapon(1, data.GetWeapon("LW013") with { MinimumStrength = 1 }),
            "A kritikus blokk tesztpajzsa nem volt felszerelhető.");
        var blockSystem = CreateMaximumBattleSystem();
        var blockRuntime = blockSystem.PrepareCharacter(shieldBearer).Runtime;
        var blockedEntry = blockSystem.ResolveEnemyActionDetailed(
            CreateEnemy(1000, 20), shieldBearer, blockRuntime).Entry;
        Assert(blockedEntry.Message.Contains("🛡️ Pajzshordozó PAJZSBLOKK", StringComparison.Ordinal) &&
               blockedEntry.Details?.Calculation.Any(line =>
                   line.Contains("KRITIKUS BLOKK", StringComparison.Ordinal)) == true,
            $"A kritikus pajzsblokk nem jelent meg egyszerre az alsó és a részletes harci naplóban. " +
            $"Napló='{blockedEntry.Message}', blokkok='{string.Join(",", blockedEntry.ShieldBlocks ?? [])}', " +
            $"részletek='{string.Join(" | ", blockedEntry.Details?.Calculation ?? [])}'.");

        var protectedCharacter = CreateCharacter("Védett", vitality: 1000, characterClassId: CharacterClassIds.Pap);
        var protector = CreateCharacter("Őrszem", vitality: 1000, characterClassId: CharacterClassIds.Lovag);
        var protectionSystem = CreateMaximumBattleSystem();
        var protectionRuntime = protectionSystem.PrepareCharacter(protectedCharacter).Runtime;
        protectionSystem.SetKnightProtection(protectionRuntime, protector);
        var protectedEntry = protectionSystem.ResolveEnemyActionDetailed(
            CreateEnemy(1000, 20), protectedCharacter, protectionRuntime).Entry;
        Assert(protectedEntry is not null &&
               protectedEntry.Details?.Calculation.Any(line =>
                   line.Contains("🛡️ Őrszem közbelépett", StringComparison.Ordinal) &&
                   line.Contains("teljes", StringComparison.OrdinalIgnoreCase)) == true,
            "A lovagi közbelépés alsó naplóüzenete nem jutott el a részletes harci naplóba.");
    }

    static void ShieldCsvValidationRejectsInvalidDefinitions()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var shieldLine = source.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.StartsWith("W014;", StringComparison.Ordinal));
        var fields = shieldLine.Split(';');
        fields[^1] = "0";
        var missingTier = source.Replace(shieldLine, string.Join(';', fields), StringComparison.Ordinal);
        AssertCsvLoadFails(missingTier, "W014", "PajzsTier", "1 és 4");

        fields = shieldLine.Split(';');
        fields[^3] = "SWORD";
        var mismatchedFamily = source.Replace(shieldLine, string.Join(';', fields), StringComparison.Ordinal);
        AssertCsvLoadFails(mismatchedFamily, "W014", "nincs összhangban");
    }

    static void WeaponFamiliesUseDistinctAttackPatterns()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));

        IReadOnlyList<Enemy> Targets(string weaponId, Position secondaryPosition)
        {
            var character = CreateCharacter($"Minta-{weaponId}", characterClassId: CharacterClassIds.Harcos);
            Assert(character.EquipWeapon(0, data.GetWeapon(weaponId) with { MinimumStrength = 1 }),
                $"A(z) {weaponId} tesztfegyver nem szerelhető fel.");
            var primary = CreateEnemyAt(new Position(3, 2), $"{weaponId}-PRIMARY");
            var secondary = CreateEnemyAt(secondaryPosition, $"{weaponId}-SECONDARY");
            var decoyPosition = weaponId == "W011" ? new Position(4, 2) : new Position(3, 4);
            var decoy = CreateEnemyAt(decoyPosition, $"{weaponId}-DECOY");
            var preparation = CreateBattleSystem(1803).PrepareCharacter(character);
            var battle = new BattleEncounter(new Position(3, 3),
                [new BattleCharacterParticipant(character, new Position(3, 3), TacticalParticipantKind.PartyMember,
                preparation.Initiative, 3, 1, preparation.Runtime)],
                [new BattleEnemyParticipant(primary, 3, 2, 1), new BattleEnemyParticipant(secondary, 2, 2, 1),
             new BattleEnemyParticipant(decoy, 1, 2, 1)], character.Id, primary.Id);
            return TacticalBattleCoordinator.SweepTargets(battle, character, new Position(3, 3), primary);
        }

        var polearmTargets = Targets("W011", new Position(3, 1));
        Assert(polearmTargets.Count == 2 && polearmTargets[1].Position == new Position(3, 1) &&
               TacticalBattleCoordinator.AttackPattern(data.GetWeapon("W011")) == WeaponAttackPattern.Line,
            "A szálfegyver nem egyenes vonalban érte el a cél mögötti mezőt.");
        var axeTargets = Targets("W017", new Position(4, 2));
        Assert(axeTargets.Count == 2 &&
               TacticalBattleCoordinator.AttackPattern(data.GetWeapon("W017")) == WeaponAttackPattern.Arc,
            "A nagybalta nem ívesen söpört.");
        var hammerTargets = Targets("W013", new Position(4, 2));
        Assert(hammerTargets.Count == 2 &&
               TacticalBattleCoordinator.AttackPattern(data.GetWeapon("W013")) == WeaponAttackPattern.Compact,
            "A kétkezes pöröly nem kis összefüggő területen hatott.");

        var sentinel = CreateCharacter("Feltartóztató", characterClassId: CharacterClassIds.Harcos);
        Assert(sentinel.EquipWeapon(0, data.GetWeapon("W011") with { MinimumStrength = 1 }) &&
               sentinel.TryAdvanceWeaponProficiency(WeaponFamilies.Polearm) &&
               sentinel.TryAdvanceWeaponProficiency(WeaponFamilies.Polearm),
            "A szálfegyver-mester tesztkarakter nem állítható elő.");
        var approaching = CreateEnemyAt(new Position(3, 1), "INTERCEPTED");
        var sentinelPreparation = CreateBattleSystem(1805).PrepareCharacter(sentinel);
        var sentinelBattle = new BattleEncounter(new Position(3, 3),
            [new BattleCharacterParticipant(sentinel, new Position(3, 3), TacticalParticipantKind.PartyMember,
            sentinelPreparation.Initiative, 3, 1, sentinelPreparation.Runtime)],
            [new BattleEnemyParticipant(approaching, 1, 2, 1)], sentinel.Id, approaching.Id);
        Assert(TacticalBattleCoordinator.PolearmMasterControlling(sentinelBattle, new Position(3, 2)) == sentinel,
            "A szálfegyver-mester nem tartotta ellenőrzés alatt a belépő mezőt.");

        var stateEnemy = CreateEnemyAt(new Position(1, 1), "TACTICAL-STATE");
        var stateCharacter = CreateCharacter("Állapotteszt");
        var statePreparation = CreateBattleSystem(1804).PrepareCharacter(stateCharacter);
        var stateBattle = new BattleEncounter(new Position(1, 2),
            [new BattleCharacterParticipant(stateCharacter, new Position(1, 2), TacticalParticipantKind.PartyMember,
            statePreparation.Initiative, 3, 1, statePreparation.Runtime)],
            [new BattleEnemyParticipant(stateEnemy, 1, 2, 1)], stateCharacter.Id, stateEnemy.Id);
        Assert(stateBattle.ApplyArmorShred(stateEnemy, 2) && stateBattle.EnemyArmorPenalty(stateEnemy) == 2 &&
               !stateBattle.ApplyArmorShred(stateEnemy, 1) &&
               stateBattle.StaggerEnemy(stateEnemy, StaggerSeverity.Normal) && stateBattle.IsEnemyStaggered(stateEnemy),
            "A páncélrepesztés vagy az ellenfél megingásának harci állapota hibás.");
        var enemyResolution = stateBattle.PrepareStaggerAction(CombatantId.ForEnemy(stateEnemy.Id), () => 46);
        Assert(enemyResolution is
        {
            Severity: StaggerSeverity.Normal, BlocksMovement: true,
            BlocksOffensiveActions: false
        } &&
               stateBattle.IsMovementBlocked(CombatantId.ForEnemy(stateEnemy.Id)) &&
               !stateBattle.AreOffensiveActionsBlocked(CombatantId.ForEnemy(stateEnemy.Id)),
            "Az ellenfél nem ugyanazt a megingási állapotgépet használja, mint a parti.");
    }

    static void DualWieldingRequiresDisciplineAndProficiencies()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var shieldBearer = CreateCharacter("Pajzsos", characterClassId: CharacterClassIds.Harcos);
        Assert(shieldBearer.EquipWeapon(0, data.GetWeapon("W004")) &&
               shieldBearer.EquipWeapon(1, data.GetWeapon("W014")) &&
               !DualWieldingRules.TryGetWeapons(shieldBearer, out _, out _),
            "A rendszer a pajzsot támadó mellékkéz-fegyvernek tekintette.");

        var fighter = CreateCharacter("Kétpengés", characterClassId: CharacterClassIds.Harcos);
        Assert(fighter.EquipWeapon(0, data.GetWeapon("W001")) &&
               fighter.TryAdvanceWeaponProficiency(WeaponFamilies.Dagger) &&
               !fighter.EquipWeapon(1, data.GetWeapon("W001")),
            "A mellékkéz képesség nélkül elfogadta a második tőrt.");
        Assert(fighter.ChooseTacticalDiscipline(TacticalDisciplines.DualWield) &&
               fighter.EquipWeapon(1, data.GetWeapon("W001")) &&
               DualWieldingRules.TryGetWeapons(fighter, out var main, out var offhand) &&
               main?.Id == "W001" && offhand?.Id == "W001",
            "A két tőr a diszciplína és a jártasság után sem aktiválódott.");

        Assert(fighter.TryAdvanceWeaponProficiency(WeaponFamilies.Sword) &&
               fighter.EquipWeapon(0, data.GetWeapon("W004")) &&
               DualWieldingRules.TryGetWeapons(fighter, out _, out _),
            "A Jártas kard–tőr páros nem aktiválódott.");
        Assert(!fighter.EquipWeapon(1, data.GetWeapon("W005")) &&
               fighter.WeaponSlots[1]?.Id == "W001",
            "A mellékkéz elfogadta a nem támogatott zúzófegyvert.");

        Assert(fighter.EquipWeapon(1, data.GetWeapon("W001")), "A mellékkéz tőre nem szerelhető vissza.");
        var system = CreateBattleSystem(1806);
        var runtime = system.PrepareCharacter(fighter).Runtime;
        BattleLogEntry? entry = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            entry = system.ResolveCharacterAttack(fighter, runtime, CreateEnemy(100, 0), finishAction: false,
                damagePercent: DualWieldingRules.OffhandDamagePercent, attackWeapon: fighter.WeaponSlots[1],
                allowTriggeredExtraAttacks: false, allowAmbush: false, damageScaleName: "Mellékkéz");
            if (entry.Details?.Calculation.Any(line => line.Contains("Fegyver alapsebzése: tőr") &&
                                                       line.Contains("tőr", StringComparison.OrdinalIgnoreCase)) == true)
                break;
        }
        Assert(entry?.Details?.Calculation.Any(line => line.Contains("Fegyver alapsebzése: tőr") ||
                                                       line.Contains("Mellékkéz")) == true,
            "A külön mellékkéz-támadás nem a második fegyvert vagy a 60%-os skálázást használta.");
    }

    static void ElvenDaggersGainPairedDamage()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var elvenDagger = data.GetWeapon(DualWieldingRules.ElvenDaggerId);
        Assert(elvenDagger.WeaponTypeId == "WT002" && elvenDagger.Damage == new ValueRange(3, 6) &&
               elvenDagger.DamageType == DamageType.Slashing &&
               WeaponFamilies.ForWeapon(elvenDagger) == WeaponFamilies.Dagger,
            "Az elf tőr nem a megadott ügyességi, vágó Tőr-profilt kapta.");

        var fighter = CreateCharacter("Elfkések", characterClassId: CharacterClassIds.Harcos);
        Assert(fighter.TryAdvanceWeaponProficiency(WeaponFamilies.Dagger) &&
               fighter.ChooseTacticalDiscipline(TacticalDisciplines.DualWield) &&
               fighter.EquipWeapon(0, elvenDagger) && fighter.EquipWeapon(1, elvenDagger) &&
               DualWieldingRules.HasPairedElvenDaggers(fighter),
            "A páros elf tőr nem szerelhető fel aktív kétfegyveres harccal.");
        var system = CreateBattleSystem(1807);
        var runtime = system.PrepareCharacter(fighter).Runtime;
        BattleLogEntry? entry = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            entry = system.ResolveCharacterAttack(fighter, runtime, CreateEnemy(100, 0), finishAction: false,
                attackWeapon: elvenDagger, allowTriggeredExtraAttacks: false, allowAmbush: false);
            if (entry.Details?.Calculation.Any(line => line.Contains("Páros elf tőr", StringComparison.OrdinalIgnoreCase)) == true)
                break;
        }
        Assert(entry?.Details?.Calculation.Any(line => line.Contains("Páros elf tőr", StringComparison.OrdinalIgnoreCase)) == true,
            "A két elf tőrrel végrehajtott találat nem kapott +1 sebzést.");
    }

    static void TacticalDisciplinesProgressAndPersist()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var character = CreateCharacter("Diszciplína", characterClassId: CharacterClassIds.Harcos);
        var firstMilestones = CharacterProgressionService.PendingTacticalDisciplineMilestones(character,
            new LevelUpResult(0, 7, 8, [])).ToArray();
        Assert(firstMilestones.SequenceEqual(new[] { 8 }) &&
               CharacterProgressionService.TacticalDisciplineChoices(character).Count == 4,
            "A 8. szint nem nyitotta meg az első diszciplínát.");
        Assert(character.ChooseTacticalDiscipline(TacticalDisciplines.Skirmisher) &&
               !character.ChooseTacticalDiscipline(TacticalDisciplines.Skirmisher),
            "Ugyanaz a diszciplína többször kiválasztható.");
        var secondMilestones = CharacterProgressionService.PendingTacticalDisciplineMilestones(character,
            new LevelUpResult(0, 17, 18, [])).ToArray();
        Assert(secondMilestones.SequenceEqual(new[] { 18 }) &&
               character.ChooseTacticalDiscipline(TacticalDisciplines.Guardian) &&
               !character.ChooseTacticalDiscipline(TacticalDisciplines.Finisher),
            "A 18. szint vagy a kétdiszciplínás korlát hibás.");

        var baseCharacter = CreateCharacter("Alap", characterClassId: CharacterClassIds.Harcos);
        var baseInitiative = CreateBattleSystem(91).PrepareCharacter(baseCharacter).Initiative;
        var disciplineInitiative = CreateBattleSystem(91).PrepareCharacter(character).Initiative;
        Assert(disciplineInitiative == baseInitiative + 2,
            "A Portyázó nem adott +2 harci kezdeményezést.");

        var finisher = CreateCharacter("Kivégző", characterClassId: CharacterClassIds.Harcos);
        var woundedEnemy = CreateEnemy(20, 1, speed: 8);
        woundedEnemy.SetCurrentHitPoints(10);
        var chanceWithoutDiscipline = CreateBattleSystem(17)
            .EstimateCharacterHitChance(baseCharacter, woundedEnemy, BattleTactic.FighterPrecise);
        Assert(finisher.ChooseTacticalDiscipline(TacticalDisciplines.Finisher),
            "A Kivégző diszciplína nem választható.");
        var chanceWithDiscipline = CreateBattleSystem(17)
            .EstimateCharacterHitChance(finisher, woundedEnemy, BattleTactic.FighterPrecise);
        Assert(chanceWithDiscipline == chanceWithoutDiscipline + 10,
            "A Kivégző nem adott +2, azaz 10 százalékpontnyi találati előnyt a sebesült célpont ellen.");

        var protectedAlly = CreateCharacter("Védett", characterClassId: CharacterClassIds.Harcos);
        var guardian = CreateCharacter("Őrszem", characterClassId: CharacterClassIds.Harcos);
        Assert(guardian.ChooseTacticalDiscipline(TacticalDisciplines.Guardian),
            "A Bajtársi őrség nem választható.");
        var protectedPreparation = CreateBattleSystem(22).PrepareCharacter(protectedAlly);
        var guardianPreparation = CreateBattleSystem(23).PrepareCharacter(guardian);
        var guardEnemy = CreateEnemy(20, 2);
        var guardBattle = new BattleEncounter(new(1, 1),
            [new BattleCharacterParticipant(protectedAlly, new(1, 1), TacticalParticipantKind.PartyMember,
             protectedPreparation.Initiative, 3, 1, protectedPreparation.Runtime),
         new BattleCharacterParticipant(guardian, new(1, 2), TacticalParticipantKind.PartyMember,
             guardianPreparation.Initiative, 3, 1, guardianPreparation.Runtime)],
            [new BattleEnemyParticipant(guardEnemy, 1, 2, 1)], protectedAlly.Id, guardEnemy.Id);
        Assert(TacticalBattleCoordinator.AlliedGuardDefense(guardBattle, protectedAlly,
                   candidate => candidate == protectedAlly ? new(1, 1) : new(1, 2)) == 1,
            "A Bajtársi őrség nem adott fedezetet a szomszédos társnak.");

        var service = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-discipline-save.json"), data);
        var restored = service.DeserializeCharacter(service.SerializeCharacter(character));
        Assert(restored.TacticalDisciplines.Select(discipline => discipline.Id).SequenceEqual(
                new[] { TacticalDisciplines.Skirmisher, TacticalDisciplines.Guardian }) &&
               CharacterSheetSnapshotProjector.Create(restored, data.ExperienceByLevel).ClassFeatureUpgradeNames!
                   .Any(name => name.Contains("diszciplína", StringComparison.OrdinalIgnoreCase)),
            "A diszciplínák elvesztek a mentésben vagy nem jelennek meg a karakterlapon.");
    }

    static void ProgressionRetrainingPreservesAdvances()
    {
        var character = CreateCharacter("Átképzett", characterClassId: CharacterClassIds.Harcos);
        Assert(character.ChooseClassFeatureUpgrade(ClassFeatureUpgrades.FighterPrecise) &&
               character.ChooseClassFeatureUpgrade(ClassFeatureUpgrades.FighterDefensive) &&
               character.ChooseTacticalDiscipline(TacticalDisciplines.Finisher) &&
               character.ChooseTacticalDiscipline(TacticalDisciplines.Guardian) &&
               character.TryAdvanceWeaponProficiency(WeaponFamilies.Sword) &&
               character.TryAdvanceWeaponProficiency(WeaponFamilies.Sword) &&
               character.TryAdvanceWeaponProficiency(WeaponFamilies.Shield),
            "Az átképzési teszt fejlődése nem állítható elő.");

        var classCount = character.ClassFeatureUpgrades.Count;
        var disciplineCount = character.TacticalDisciplines.Count;
        var weaponAdvances = character.WeaponProficiencyAdvances;
        character.ResetClassFeatureUpgrades();
        Assert(character.ChooseClassFeatureUpgrade(ClassFeatureUpgrades.FighterPowerful) &&
               character.ChooseClassFeatureUpgrade(ClassFeatureUpgrades.FighterDefensive) &&
               character.ClassFeatureUpgrades.Count == classCount &&
               character.TacticalDisciplines.Count == disciplineCount &&
               character.WeaponProficiencyAdvances == weaponAdvances,
            "Az osztályképesség-átképzés más csoportot módosított vagy lépést vesztett.");

        character.ResetWeaponProficiencies();
        Assert(character.TryAdvanceWeaponProficiency(WeaponFamilies.Axe) &&
               character.TryAdvanceWeaponProficiency(WeaponFamilies.Axe) &&
               character.TryAdvanceWeaponProficiency(WeaponFamilies.Blunt) &&
               character.WeaponProficiencyAdvances == weaponAdvances,
            "A fegyverjártasság-átképzés nem őrizte meg a lépések számát.");
        Assert(ProgressionRetrainingRules.Cost(character,
                   ProgressionRetrainingKind.WeaponProficiencies) == 300 &&
               ProgressionRetrainingRules.Cost(character,
                   ProgressionRetrainingKind.ClassFeatures) == 300,
            "Az átképzés minimumdíja hibás.");
    }

    static void ReserveWeaponIsPassiveAndPersistent()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var character = CreateCharacter("Tartalék");
        Assert(character.EquipWeapon(0, data.GetWeapon("W001")), "Hiányzik a tőr.");
        var weight = CharacterMobilityRules.Evaluate(character).EquippedWeight;
        Assert(character.EquipWeapon(2, data.GetWeapon("W004")), "Hiányzik a tartalék kard.");
        Assert(character.AttackWeapon?.Id == "W001" && character.ActiveWeapons.Count() == 2 &&
            CharacterMobilityRules.Evaluate(character).EquippedWeight == weight + 3 &&
            CharacterMobilityRules.Evaluate(character).CarriedWeight == weight + 3,
            "A tartalék aktívvá vált vagy hibásan számít a súlyba.");
        var revision = character.InventoryRevision;
        Assert(character.TrySwapReserveWeapon() && character.InventoryRevision == revision + 1 &&
            character.AttackWeapon?.Id == "W004" && character.WeaponSlots[2]?.Id == "W001", "Hibás csere.");
        var roster = new CharacterRoster(); roster.Add(character); roster.Select(character);
        var saves = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-reserve.json"), data);
        var restored = saves.Deserialize(saves.Serialize(roster)).SelectedCharacter!;
        Assert(restored.WeaponSlots.Select(value => value?.Id).SequenceEqual(character.WeaponSlots.Select(value => value?.Id)),
            "A tartalék fegyver elveszett a mentésben.");
    }

    static void NpcSwapsBrokenWeaponForOperationalReserve()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var character = CreateCharacter("Fegyverváltó");
        var brokenWeapon = data.GetWeapon("W001");
        var reserve = data.GetWeapon("W004");
        var reserveState = InventoryItemInstanceState.Create() with { DurabilityDamage = 7 };
        Assert(character.SetInventoryItem(InventorySlotKind.Weapon, 0, brokenWeapon, null, 1,
                   InventoryItemInstanceState.Create() with { DurabilityDamage = brokenWeapon.MaximumDurability }) &&
               character.SetInventoryItem(InventorySlotKind.Weapon, 2, reserve, null, 1, reserveState),
            "Az NPC fegyvercsere-tesztje nem tudta előkészíteni a felszerelést.");
        Assert(TacticalBattleCoordinator.ShouldNpcSwapToReserveWeapon(character),
            "Az NPC nem ismerte fel, hogy az eltört aktív fegyverét le kell cserélnie.");
        Assert(character.TrySwapReserveWeapon() && character.AttackWeapon?.Id == reserve.Id &&
               character.GetInventoryItemState(InventorySlotKind.Weapon, 0)?.DurabilityDamage == 7 &&
               character.WeaponSlots[2]?.Id == brokenWeapon.Id,
            "Az NPC tartalékfegyver-cseréje nem őrizte meg a tárgyállapotokat.");
        Assert(!TacticalBattleCoordinator.ShouldNpcSwapToReserveWeapon(character),
            "Az NPC működő aktív fegyver mellett is újabb tartalékcserét kezdeményezne.");

        Assert(character.SetInventoryItem(InventorySlotKind.Weapon, 0, brokenWeapon, null, 1,
                   InventoryItemInstanceState.Create() with { DurabilityDamage = brokenWeapon.MaximumDurability }) &&
               character.SetInventoryItem(InventorySlotKind.Weapon, 2, reserve, null, 1,
                   InventoryItemInstanceState.Create() with { DurabilityDamage = reserve.MaximumDurability }),
            "A törött tartalékfegyveres esetet nem sikerült előkészíteni.");
        Assert(!TacticalBattleCoordinator.ShouldNpcSwapToReserveWeapon(character),
            "Az NPC törött tartalékfegyvert próbálna kézbe venni.");
    }

    static void ReserveTwoHandedSwapStowsShield()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var character = CreateCharacter("Pajzsos");
        Assert(character.EquipWeapon(0, data.GetWeapon("W004")) &&
            character.EquipWeapon(1, data.GetWeapon("W014")) && character.EquipWeapon(2, data.GetWeapon("W011")), "Hibás előkészítés.");
        for (var index = 0; index < LiveCharacter.MaximumBackpackItemCount; index++)
            character.SetInventoryItem(InventorySlotKind.Backpack, index, data.GetItem("T001"));
        var revision = character.InventoryRevision;
        Assert(!character.TrySwapReserveWeapon() && character.InventoryRevision == revision && character.WeaponSlots[1]?.Id == "W014",
            "Telt hátizsáknál részleges csere történt.");
        character.SetInventoryItem(InventorySlotKind.Backpack, 3, null);
        Assert(character.TrySwapReserveWeapon() && character.WeaponSlots[1] is null &&
            character.Backpack[3]?.Id == "W014" && character.WeaponSlots[2]?.Id == "W004", "A pajzs nem került biztonságba.");
    }

    static void PhysicalDamageUsesTypesAndWeapons()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        int PlayerDamage(DamageType type, DamageResistance? resistance = null)
        {
            var total = 0;
            for (var seed = 0; seed < 30; seed++)
            {
                var system = CreateBattleSystem(seed);
                var character = CreateCharacter("Támadó", 1000);
                character.EquipWeapon(0, data.GetWeapon("W005") with { Damage = new(20, 20), DamageType = type });
                var enemy = new ConfiguredEnemy(new(1, 1), new("E-TYPE", "Cél", "e", 1, 1000, 8, 1, 1, 1, [],
                    Resistances: resistance ?? new(4, 0, -4)));
                system.ResolveCharacterAttack(character, system.PrepareCharacter(character).Runtime, enemy);
                total += 1000 - enemy.CurrentHitPoints;
            }
            return total;
        }
        Assert(PlayerDamage(DamageType.Bludgeoning) > PlayerDamage(DamageType.Piercing) &&
            PlayerDamage(DamageType.Piercing) > PlayerDamage(DamageType.Slashing), "A szörny típusvédelme nem számít.");
        Assert(PlayerDamage(DamageType.Acid, new(Acid: -4)) > PlayerDamage(DamageType.Acid, new(Acid: 4)) &&
               Enum.GetValues<DamageType>().Select(type => type.Name()).SequenceEqual(
                   ["vágás", "szúrás", "zúzás", "tűz", "sav", "nekrotikus", "káosz"]),
            "Az elemi és természetfeletti sebzéstípusok vagy ellenállásaik hibásak.");
        int EnemyDamage(int weaponDamage, DamageResistance resistance)
        {
            var total = 0;
            for (var seed = 0; seed < 30; seed++)
            {
                var system = CreateBattleSystem(seed);
                var target = CreateCharacter("Védő", 1000);
                target.EquipArmor(data.GetArmor("A002") with { Defense = new(5, 5), Resistances = resistance });
                var weapon = data.GetWeapon("W005") with { Damage = new(weaponDamage, weaponDamage) };
                var enemy = new ConfiguredEnemy(new(1, 1), data.GetEnemy("E003") with
                {
                    WeaponIds = [weapon.Id],
                    Weapons = [weapon]
                });
                system.ResolveEnemyAction(enemy, target, system.PrepareCharacter(target).Runtime);
                total += 1000 - target.CurrentVitality;
            }
            return total;
        }
        Assert(EnemyDamage(20, new()) > EnemyDamage(5, new()), "Az azonos erejű szörny fegyvere nem módosítja a sebzést.");
        Assert(EnemyDamage(20, new(0, 0, -3)) > EnemyDamage(20, new(0, 0, 3)), "A páncél típusvédelme nem számít.");
    }

    static void WeaponCsvPropertiesAreInherited()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        Assert(data.Enemies.All(enemy => enemy.Weapons is { Count: > 0 } &&
                enemy.Weapons.Select(weapon => weapon.Id).SequenceEqual(enemy.WeaponIds ?? [])),
            "Fegyver nélküli vagy hibás fegyverlistájú szörny.");
        Assert(data.Enemies.All(enemy => enemy.Id is not ("E054" or "E055" or "E056" or "E057" or "E058" or "E059" or "E060")),
            "A régi goblin- vagy orkvariáns megmaradt.");
        var goblin = data.GetEnemy("E003");
        var firstGoblin = new ConfiguredEnemy(new(1, 1), goblin, new Random(7));
        var selectedGoblinWeapon = firstGoblin.EquippedWeapon ??
                                   throw new InvalidOperationException("A goblin nem választott fegyvert.");
        var sameGoblin = new ConfiguredEnemy(new(1, 1), goblin, new Random(99),
            new EnemyEquipmentSelection(selectedGoblinWeapon.Id, null));
        Assert(goblin.Name == "Goblin" && goblin.ChoosesWeapon && goblin.WeaponIds!.Count == 3 &&
               firstGoblin.LongName.Contains(selectedGoblinWeapon.Name, StringComparison.Ordinal) &&
               sameGoblin.EquippedWeapon?.Id == selectedGoblinWeapon.Id &&
               ReferenceEquals(firstGoblin.Definition, goblin) && ReferenceEquals(sameGoblin.Definition, goblin),
            "A goblin példány nem választott és nem őrzött meg megjelenített fegyvert.");
        var savedGoblin = JsonSerializer.Deserialize<EnemySaveData>(JsonSerializer.Serialize(new EnemySaveData(
            firstGoblin.Position, firstGoblin.Definition.Id, firstGoblin.CurrentHitPoints,
            SelectedWeaponId: selectedGoblinWeapon.Id)))!;
        var restoredGoblin = ConfiguredEnemy.RestoreLegacy(savedGoblin.Position, goblin,
            savedGoblin.SelectedWeaponId, new Random(99));
        Assert(restoredGoblin.EquippedWeapon?.Id == selectedGoblinWeapon.Id,
            "A példány fegyverválasztása nem élte túl a mentést.");
        var oneHandedGoblinWeapon = goblin.Weapons!.First(weapon => !weapon.IsTwoHanded);
        var goblinShield = goblin.ShieldOption ?? throw new InvalidOperationException("A goblinnak nincs pajzsopciója.");
        var shieldedGoblin = new ConfiguredEnemy(new(1, 1), goblin, new Random(1),
            equipment: new EnemyEquipmentSelection(oneHandedGoblinWeapon.Id, goblinShield.Id));
        var shieldlessGoblin = new ConfiguredEnemy(new(1, 1), goblin, new Random(1),
            equipment: new EnemyEquipmentSelection(oneHandedGoblinWeapon.Id, null));
        var savedEquipment = JsonSerializer.Deserialize<EnemyEquipmentSaveData>(JsonSerializer.Serialize(
            new EnemyEquipmentSaveData(shieldedGoblin.EquippedWeapon?.Id, shieldedGoblin.EquippedShield?.Id)))!;
        var reloadedShieldedGoblin = new ConfiguredEnemy(new(1, 1), goblin, new Random(99),
            equipment: new EnemyEquipmentSelection(savedEquipment.WeaponId, savedEquipment.ShieldId));
        Assert(shieldedGoblin.EquippedShield?.Id == goblinShield.Id &&
               shieldlessGoblin.EquippedShield is null &&
               reloadedShieldedGoblin.EquippedWeapon?.Id == oneHandedGoblinWeapon.Id &&
               reloadedShieldedGoblin.EquippedShield?.Id == goblinShield.Id &&
               shieldedGoblin.AttackWeapons.All(weapon => !weapon.IsTwoHanded),
            "A példány fegyver- vagy pajzsállapota nem maradt stabil mentés után.");
        var zombie = data.GetEnemy("E006");
        var zombieWeaponIds = zombie.WeaponIds ?? [];
        var zombieEnemy = new ConfiguredEnemy(new(1, 1), zombie, new Random(4));
        Assert(zombie.ChoosesWeapon && zombieWeaponIds.SequenceEqual(["WN003", "W005"]) &&
               zombieEnemy.EquippedWeapon is { } zombieWeapon &&
               zombieWeaponIds.Contains(zombieWeapon.Id) && zombieEnemy.LongName.Contains(zombieWeapon.Name, StringComparison.Ordinal),
            "A zombi nem választ egyszer az ököl és a bunkó közül.");
        var armedZombie = new ConfiguredEnemy(new(1, 1), zombie,
            equipment: new EnemyEquipmentSelection("W005", null));
        var unarmedZombie = new ConfiguredEnemy(new(1, 1), zombie,
            equipment: new EnemyEquipmentSelection("WN003", null));
        var minotaurCorpseMaze = new Maze(7, 7);
        var minotaurForLoot = new ConfiguredEnemy(new(3, 3), data.GetEnemy("E014"));
        minotaurCorpseMaze.Carve(minotaurForLoot.Position);
        minotaurCorpseMaze.AddEnemy(minotaurForLoot);
        minotaurCorpseMaze.ReplaceEnemyWithCorpse(minotaurForLoot);
        var minotaurCorpse = minotaurCorpseMaze.Corpses.OfType<MonsterCorpse>().Single();
        var lootService = new LootAndInventoryService(data, new Random(1));
        Assert(data.LootRules.CarriedWeaponChancePercent == 30 &&
               armedZombie.CarriedWeaponIds.SequenceEqual(["W005"]) &&
               unarmedZombie.CarriedWeaponIds.Count == 0 &&
               minotaurCorpse.CarriedWeaponIds.SequenceEqual(["W017"]) &&
               lootService.RollCarriedWeapon(minotaurCorpse.CarriedWeaponIds, 100)?.Id == "W017" &&
               lootService.RollCarriedWeapon(["WN009"], 100) is null,
            "A humanoid saját fegyverének emelt zsákmányesélye vagy a természetes fegyver kizárása hibás.");
        var minotaur = data.GetEnemy("E014");
        var minotaurWeapons = minotaur.Weapons ?? [];
        Assert(!minotaur.ChoosesWeapon && (minotaur.WeaponIds ?? []).SequenceEqual(["W017", "WN009"]) &&
               minotaurWeapons.Any(weapon => weapon.IsMonsterOnly) &&
               minotaurWeapons.Any(weapon => !weapon.IsMonsterOnly),
            "A minotaurusz vegyes nagybalta–szarvöklelés listája hibás.");
        var dragon = data.GetEnemy("E021");
        var dragonWeaponIds = dragon.WeaponIds ?? [];
        var dragonWeapons = dragon.Weapons ?? [];
        Assert(!dragon.ChoosesWeapon && dragonWeaponIds.SequenceEqual(["WN004", "WN005", "WN006"]) &&
               dragonWeapons.Select(weapon => weapon.Name).SequenceEqual(["sárkányfogak", "farokcsapás", "tüzes lehelet"]),
            "A sárkány természetes támadáslistája hibás.");
        var dragonEnemy = new ConfiguredEnemy(new(1, 1), dragon, new Random(11));
        var dragonSystem = CreateBattleSystem(11);
        var usedWeapons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var attack = 0; attack < 100 && usedWeapons.Count < 3; attack++)
        {
            if (dragonSystem.SelectEnemyAttackWeapon(dragonEnemy) is { } weapon)
                usedWeapons.Add(weapon.Id);
        }
        Assert(usedWeapons.SetEquals(dragonWeaponIds),
            "A nem választó sárkány nem használta véletlenszerűen mindhárom támadását.");
        var lich = data.GetEnemy("E022");
        var lichEnemy = new ConfiguredEnemy(new(1, 1), lich);
        var lichStaff = data.GetWeapon("W021");
        Assert((lich.WeaponIds ?? []).SequenceEqual(["W021"]) &&
               dragonSystem.SelectEnemyAttackWeapon(lichEnemy)?.Id == lichStaff.Id &&
               lichStaff is { DamageType: DamageType.Necrotic, CanAttackFromRear: true, IsMonsterOnly: true } &&
               !lichStaff.CanBeEquippedBy(CharacterClassIds.Mágus, 13) &&
               SpellcastingRules.IsRestrictedFromTradingAndGeneration(lichStaff),
            "A lich varázsbotja nem szörnykizárólagos nekrotikus távolsági fegyverként működik.");
        var breath = data.GetWeapon("WN006");
        var chaosBreath = data.GetWeapon("WN017");
        var targetSystem = CreateBattleSystem(21);
        var breathTargets = Enumerable.Range(0, 4).Select(index => CreateCharacter($"Leheletcél {index}", 100)).ToArray();
        var targetPositions = new[] { new Position(3, 2), new Position(4, 3), new Position(3, 4), new Position(2, 3) };
        var targetPreparations = breathTargets.Select(targetSystem.PrepareCharacter).ToArray();
        var breathEnemy = new ConfiguredEnemy(new(3, 3), data.GetEnemy("E050"));
        var breathBattle = new BattleEncounter(new(3, 3), breathTargets.Select((character, index) =>
                new BattleCharacterParticipant(character, targetPositions[index], TacticalParticipantKind.PartyMember,
                    targetPreparations[index].Initiative, 3, 1, targetPreparations[index].Runtime)),
            [new BattleEnemyParticipant(breathEnemy, 5, 2, 1)], breathTargets[0].Id, breathEnemy.Id);
        Assert(TacticalBattleCoordinator.EnemyAttackTargets(breathBattle, breathEnemy, breath,
                   character => targetPositions[Array.IndexOf(breathTargets, character)]).Count == 3 &&
               TacticalBattleCoordinator.EnemyAttackTargets(breathBattle, breathEnemy, chaosBreath,
                   character => targetPositions[Array.IndexOf(breathTargets, character)]).Count == 4,
            "A leheletek CSV szerinti többcélú támadása nem érvényesül.");
        Assert(data.GetWeapon("W009-PLUS1").MaximumTargets == 2 &&
            data.GetWeapon("W011-PLUS1").CanAttackFromRear && data.GetArmor("A003-PLUS1").Resistances == data.GetArmor("A003").Resistances,
            "A mágikus változat elvesztette a tulajdonságokat.");
        var (battle, front, rear, enemy) = CreateFormationEncounter();
        rear.EquipWeapon(0, data.GetWeapon("W011") with { Id = "CUSTOM-POLEARM", BaseWeaponId = null });
        battle.Engage(front, enemy);
        Assert(battle.RearFormationEnemiesInReach(rear).Count == 1, "Új azonosítóval nem működik a hátsó sor.");
        rear.EquipWeapon(0, data.GetWeapon("W001")); rear.EquipWeapon(2, data.GetWeapon("W011"));
        Assert(battle.RearFormationEnemiesInReach(rear).Count == 0, "A tartalék szálfegyver hátsó soros támadást adott.");
        Assert(SpellcastingRules.IsRestrictedFromTradingAndGeneration(data.GetWeapon("WN001")) &&
            !data.GetWeapon("WN001").CanBeEquippedBy(CharacterClassIds.Harcos, 13) &&
            SpellcastingRules.IsRestrictedFromTradingAndGeneration(data.GetWeapon("W019")) &&
            SpellcastingRules.IsRestrictedFromTradingAndGeneration(data.GetWeapon("W020")) &&
            !data.GetWeapon("W019").CanBeEquippedBy(CharacterClassIds.Harcos, 13) &&
            WeaponFamilies.Find(WeaponFamilies.Staff) is not null,
            "A természetes vagy nulla árú szörnyfegyver felszerelhető/árulható, vagy hiányzik a botcsalád.");
        var legendaryArmors = data.Armors.Where(armor => armor.Rarity == ItemRarity.Legendary).ToArray();
        int StrongDistinctBaseTypes(Func<DamageResistance, int> resistance) => legendaryArmors
            .Where(armor => armor.Resistances is { } values && resistance(values) >= 4)
            .Select(armor => armor.BaseArmorId).Where(id => id is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count();
        Assert(StrongDistinctBaseTypes(values => values.Fire) >= 2 &&
               StrongDistinctBaseTypes(values => values.Acid) >= 2 &&
               StrongDistinctBaseTypes(values => values.Necrotic) >= 2 &&
               StrongDistinctBaseTypes(values => values.Chaos) >= 2,
            "Nincs mind a négy új sebzéstípushoz két erős, eltérő alaptípusú legendás páncél.");
    }

}

file sealed class MaximumRandom : Random
{
    public override int Next() => int.MaxValue;
    public override int Next(int maxValue) => maxValue <= 0 ? 0 : maxValue - 1;
    public override int Next(int minValue, int maxValue) => maxValue <= minValue ? minValue : maxValue - 1;
    public override double NextDouble() => 0.9999999999999999d;
    protected override double Sample() => 0.9999999999999999d;
}
