internal static partial class Program
{
    static void MagicItemIdentificationStatePersists()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var race = data.Races[0];
        var characterClass = data.CharacterClasses.First(value => value.Id == CharacterClassIds.Harcos);
        var character = new LiveCharacter("Azonosító", race, characterClass,
            new PrimaryAbilities(8, 8, 8, 8), 30, 0, 0, 0);
        var item = data.MagicItems.First(value => value.Rarity == ItemRarity.Magic);
        var instanceId = Guid.NewGuid();
        var curse = data.ItemCurses.First(value => value.CanAffect(item));
        var statefulItem = new InventoryItemInstanceState(instanceId, false, curse.Id, curse.Effect,
            curse.Value, curse.Strength);
        Assert(character.AddToBackpack(item, identified: false, instanceId, statefulItem),
            "Az azonosítatlan tárgy nem került a hátizsákba.");

        var hidden = InventorySnapshotProjector.Create(character).Slots.Single(slot =>
            slot.Kind == InventorySlotKind.Backpack && slot.Index == 0).Item!;
        Assert(!hidden.IsIdentified && hidden.InstanceId == instanceId && hidden.DefinitionId.Length == 0 &&
               hidden.Name.Contains("Ismeretlen", StringComparison.OrdinalIgnoreCase) && hidden.BasePrice == 0 &&
               hidden.MagicPower == 0 && hidden.MaximumCharges == 0,
            "A snapshot kiszivárogtatta az azonosítatlan tárgy tulajdonságait.");

        var charges = character.GetInventoryItemCharges(InventorySlotKind.Backpack, 0);
        var state = character.GetInventoryItemState(InventorySlotKind.Backpack, 0);
        character.ApplyInventoryChanges(
            new InventorySlotChange(InventorySlotKind.Backpack, 0, null),
            new InventorySlotChange(InventorySlotKind.Backpack, 1, item, charges, 1, state));
        Assert(character.GetInventoryItemState(InventorySlotKind.Backpack, 1)?.InstanceId == instanceId &&
               !character.IsInventoryItemIdentified(InventorySlotKind.Backpack, 1),
            "A mozgatás nem őrizte meg a tárgypéldány állapotát.");

        var saves = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-identification-save.json"), data);
        var restored = saves.DeserializeCharacter(saves.SerializeCharacter(character));
        Assert(restored.GetInventoryItemState(InventorySlotKind.Backpack, 1)?.InstanceId == instanceId &&
               !restored.IsInventoryItemIdentified(InventorySlotKind.Backpack, 1) &&
               restored.GetInventoryItemState(InventorySlotKind.Backpack, 1)?.CurseId == curse.Id,
            "A mentés nem őrizte meg az azonosítási állapotot.");
        Assert(restored.IdentifyInventoryItem(InventorySlotKind.Backpack, 1),
            "A tárgy nem volt azonosítható.");
        var revealed = InventorySnapshotProjector.Create(restored).Slots.Single(slot =>
            slot.Kind == InventorySlotKind.Backpack && slot.Index == 1).Item!;
        Assert(revealed.IsIdentified && revealed.DefinitionId == item.Id &&
               revealed.Name.StartsWith(item.Name, StringComparison.Ordinal) &&
               revealed.CurseId == curse.Id && revealed.InstanceId == instanceId,
            "Az azonosítás nem fedte fel a valódi tárgyat vagy lecserélte a példányazonosítót.");
    }

    static void EquipmentDurabilityDataAndStatePersist()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var dagger = data.GetWeapon("W001");
        var greatsword = data.GetWeapon("W009");
        var naturalWeapon = data.GetWeapon("WN001");
        var clothArmor = data.GetArmor("A001");
        var plateArmor = data.GetArmor("A006");
        Assert(dagger.MaximumDurability == 80 && greatsword.MaximumDurability == 120 &&
               naturalWeapon.MaximumDurability == 0 && clothArmor.MaximumDurability == 90 &&
               plateArmor.MaximumDurability == 150,
            "A felszerelések alap-tartóssága nem a súlyuk és típusuk szerinti adatból érkezik.");
        Assert(data.GetWeapon("LW014").MaximumDurability == 0,
            "A soha meg nem repedő Tölgykirály pajzsa kopó tárggyá vált.");

        var original = InventoryItemInstanceState.Create();
        var worn = EquipmentDurabilityRules.ApplyWear(dagger, original, 35);
        var broken = EquipmentDurabilityRules.ApplyWear(dagger, worn, 1000);
        var repaired = EquipmentDurabilityRules.Repair(dagger, broken, 20);
        Assert(worn.DurabilityDamage == 35 && EquipmentDurabilityRules.CurrentDurability(dagger, worn) == 45 &&
               broken.DurabilityDamage == dagger.MaximumDurability &&
               EquipmentDurabilityRules.CurrentDurability(dagger, broken) == 0 &&
               EquipmentDurabilityRules.CurrentDurability(dagger, repaired) == 20,
            "A kopás, törés vagy javítás nem marad a tartóssági határok között.");
        Assert(EquipmentDurabilityRules.ApplyWear(naturalWeapon, original, 10) == original,
            "A természetes szörnyfegyver példánykopást kapott.");

        var character = CreateCharacter("Kopásteszt");
        Assert(character.SetInventoryItem(InventorySlotKind.Weapon, 0, dagger, null, 1, worn),
            "A kopott tesztfegyvert nem lehetett felszerelni.");
        var saves = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-durability-save.json"), data);
        var restored = saves.DeserializeCharacter(saves.SerializeCharacter(character));
        var restoredState = restored.GetInventoryItemState(InventorySlotKind.Weapon, 0);
        Assert(restoredState?.DurabilityDamage == 35 &&
               InventorySnapshotProjector.Create(restored).Slots.Single(slot =>
                   slot.Kind == InventorySlotKind.Weapon && slot.Index == 0).Item is
               { MaximumDurability: 80, DurabilityDamage: 35 },
            "A fegyver kopása nem élte túl a mentési vagy coop-pillanatkép körutat.");

        var legacy = GameSaveFormat.MigrateToCurrent(new GameSaveData { Version = 20 });
        Assert(legacy.Version == GameSaveFormat.CurrentVersion,
            "A kopás előtti játékmentés nem migrálódott az aktuális formátumra.");
    }

    static void EquipmentDurabilityIsVisible()
    {
        Assert(EquipmentDurabilityRules.Condition(100, 49) == EquipmentCondition.Intact &&
               EquipmentDurabilityRules.Condition(100, 50) == EquipmentCondition.Worn &&
               EquipmentDurabilityRules.Condition(100, 75) == EquipmentCondition.Damaged &&
               EquipmentDurabilityRules.Condition(100, 100) == EquipmentCondition.Broken &&
               EquipmentDurabilityRules.Condition(120, 119) == EquipmentCondition.Damaged &&
               EquipmentDurabilityRules.Condition(0, 0) == EquipmentCondition.NotApplicable,
            "A tartóssági állapotok határértékei nem 51–100 / 26–50 / 1–25 / 0 százaléknál vannak.");

        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var dagger = data.GetWeapon("W001");
        var wornState = InventoryItemInstanceState.Create() with { DurabilityDamage = 40 };
        var inspection = ItemInspectionFormatter.Format(dagger, data, instanceState: wornState);
        Assert(inspection.Text.Contains("Tartósság: 40/80 (50%)", StringComparison.Ordinal) &&
               inspection.Text.Contains("kopott", StringComparison.OrdinalIgnoreCase),
            "A tárgyvizsgálat nem mutatja a kopott fegyver pontos tartósságát és állapotát.");

        var character = CreateCharacter("Állapotjelző");
        Assert(character.SetInventoryItem(InventorySlotKind.Weapon, 0, dagger, null, 1, wornState),
            "A kopott tesztfegyvert nem lehetett felszerelni.");
        var inventory = InventorySnapshotProjector.Create(character);
        var sheet = CharacterSheetSnapshotProjector.Create(character, data.ExperienceByLevel, 0);
        var snapshot = new SessionCharacterSnapshot(character.Id, character.Name, character.Race.Id,
            character.CharacterClass.Id, character.Level, character.CurrentVitality, character.MaximumVitality,
            character.CurrentMana, character.MaximumMana, character.FoodLevel, character.WaterLevel, character.Gold,
            character.IsAlive, null, [], inventory, sheet, character.Color);
        var detailsLine = CharacterDetailsWindow.Build(snapshot, data)
            .Single(line => line.Text.Contains($"Fegyver 1: {dagger.Name}", StringComparison.Ordinal));
        Assert(detailsLine.Text.Contains("Tartósság: 40/80 (50%)", StringComparison.Ordinal) &&
               detailsLine.Color == ConsoleColor.Yellow,
            "A részletes karakterinfó nem mutatja vagy nem színezi a kopott felszerelést.");
        var compactLine = CharacterSheetPanel.Build(snapshot, 0, 0, 0)
            .Single(line => line.Row == 18);
        Assert(!compactLine.Text.Contains("Tartósság", StringComparison.Ordinal) &&
               !compactLine.Text.Contains("🟡", StringComparison.Ordinal) &&
               compactLine.ColoredTextStart == "1: ".Length &&
               compactLine.ColoredTextColor == ConsoleColor.Yellow,
            "A kompakt karakterlap nem helytakarékosan, a kopott fegyver nevét sárgítva jelez.");

        var unidentified = new InventoryItemSnapshot(string.Empty, "❓ Azonosítatlan mágikus fegyver",
            ItemCategory.Weapon, ItemRarity.Magic, 0, 0, Description: "erős mágikus aura",
            IsIdentified: false, MaximumDurability: 120, DurabilityDamage: 90);
        Assert(ItemInspectionFormatter.FormatUnidentified(unidentified).Text.Contains(
                "Tartósság: 30/120 (25%)", StringComparison.Ordinal),
            "Az azonosítatlan felszerelés szemmel látható fizikai állapota rejtve maradt.");
    }

    static void CombatAppliesEquipmentWear()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var attacker = CreateCharacter("Koptató", 1000);
        var weapon = data.GetWeapon("W001");
        Assert(attacker.EquipWeapon(0, weapon), "A kopási teszt fegyvere nem volt felszerelhető.");
        var enemy = CreateEnemy(10000, 1);
        var attackSystem = CreateBattleSystem(17);
        var attackerRuntime = attackSystem.PrepareCharacter(attacker).Runtime;
        var observedWeaponHits = 0;
        for (var attempt = 0; attempt < 40 && observedWeaponHits < 3; attempt++)
        {
            var before = attacker.GetInventoryItemState(InventorySlotKind.Weapon, 0)!.Value.DurabilityDamage;
            var enemyVitalityBefore = enemy.CurrentHitPoints;
            var entry = attackSystem.ResolveCharacterAttack(attacker, attackerRuntime, enemy,
                finishAction: false);
            var after = attacker.GetInventoryItemState(InventorySlotKind.Weapon, 0)!.Value.DurabilityDamage;
            var hit = enemy.CurrentHitPoints < enemyVitalityBefore;
            var expectedWear = hit ? entry.Kind == BattleLogKind.CriticalHit ? 2 : 1 : 0;
            Assert(after - before == expectedWear,
                "A sikeres, kritikus vagy elhibázott fegyvertámadás nem a megfelelő kopást okozta.");
            if (!hit) continue;
            observedWeaponHits++;
            Assert(entry.Details?.Calculation.Any(line => line.Contains("Fegyverkopás", StringComparison.Ordinal)) == true,
                "A fegyverkopás nem került be a csatarészletek közé.");
        }
        Assert(observedWeaponHits >= 3 && attacker.InventoryRevision > 1,
            "Nem sikerült több fegyverkopást megfigyelni, vagy az inventory revízió nem változott.");

        Assert(attacker.SetInventoryItem(InventorySlotKind.Weapon, 0, weapon, null, 1,
                InventoryItemInstanceState.Create() with { DurabilityDamage = weapon.MaximumDurability - 1 }),
            "A majdnem törött tesztfegyvert nem lehetett felszerelni.");
        BattleLogEntry? weaponBreak = null;
        for (var attempt = 0; attempt < 40 && weaponBreak is null; attempt++)
        {
            var entry = attackSystem.ResolveCharacterAttack(attacker, attackerRuntime, enemy,
                finishAction: false);
            if (entry.FollowUps?.Any(notice => notice.Message.Contains("eltört", StringComparison.OrdinalIgnoreCase)) == true)
                weaponBreak = entry;
        }
        Assert(weaponBreak?.FollowUps?.Any(notice =>
                   notice.Message.Contains(attacker.Name, StringComparison.Ordinal) &&
                   notice.Message.Contains(weapon.Name, StringComparison.Ordinal) &&
                   notice.Message.Contains("nem használható", StringComparison.OrdinalIgnoreCase)) == true,
            "A fegyver törése nem adott külön, következményt is leíró csatalog-üzenetet.");

        var defender = CreateCharacter("Vértvizsgáló", 1000);
        var armor = data.GetArmor("A001");
        var shield = data.GetWeapon("W014");
        Assert(defender.EquipWeapon(1, shield) && defender.EquipArmor(armor),
            "A kopási teszt páncélja vagy pajzsa nem volt felszerelhető.");
        var armoredEnemy = CreateEnemy(10000, 5);
        var defenseSystem = CreateBattleSystem(29);
        var defenderRuntime = defenseSystem.PrepareCharacter(defender).Runtime;
        var observedArmorHits = 0;
        for (var attempt = 0; attempt < 40 && observedArmorHits < 3; attempt++)
        {
            var armorBefore = defender.GetInventoryItemState(InventorySlotKind.Armor, 0)!.Value.DurabilityDamage;
            var shieldBefore = defender.GetInventoryItemState(InventorySlotKind.Weapon, 1)!.Value.DurabilityDamage;
            var resolution = defenseSystem.ResolveEnemyActionDetailed(armoredEnemy, defender, defenderRuntime);
            var armorAfter = defender.GetInventoryItemState(InventorySlotKind.Armor, 0)!.Value.DurabilityDamage;
            var shieldAfter = defender.GetInventoryItemState(InventorySlotKind.Weapon, 1)!.Value.DurabilityDamage;
            var expectedWear = resolution.Hit ? resolution.Entry.Kind == BattleLogKind.CriticalHit ? 2 : 1 : 0;
            var criticalBlock = resolution.Entry.ShieldBlocks?.Any(block => block.IsCriticalBlock) == true;
            var expectedShieldWear = criticalBlock
                ? resolution.Entry.Kind == BattleLogKind.CriticalHit ? 3 : 2
                : expectedWear;
            Assert(armorAfter - armorBefore == expectedWear && shieldAfter - shieldBefore == expectedShieldWear,
                "A fizikai találat vagy a kritikus blokk nem a megfelelő mértékben koptatta a páncélt és pajzsot.");
            if (!resolution.Hit) continue;
            observedArmorHits++;
            Assert(resolution.Entry.ShieldBlocks is { Count: 1 } &&
                   resolution.Entry.ShieldBlocks[0].Attempted,
                "A pajzs blokkpróbája nem strukturált harci eredményként érkezett meg.");
            Assert(resolution.Entry.Details?.Calculation.Any(line => line.Contains("Páncélkopás", StringComparison.Ordinal)) == true &&
                   resolution.Entry.Details.Calculation.Any(line => line.Contains("Pajzskopás", StringComparison.Ordinal)),
                "A páncél- vagy pajzskopás nem került be a csatarészletek közé.");
        }
        Assert(observedArmorHits >= 3, "Nem sikerült több páncélt érő találatot megfigyelni.");

        var breakingDefender = CreateCharacter("Törő vértes", 1000);
        Assert(breakingDefender.SetInventoryItem(InventorySlotKind.Armor, 0, armor, null, 1,
                   InventoryItemInstanceState.Create() with { DurabilityDamage = armor.MaximumDurability - 1 }) &&
               breakingDefender.SetInventoryItem(InventorySlotKind.Weapon, 1, shield, null, 1,
                   InventoryItemInstanceState.Create() with { DurabilityDamage = shield.MaximumDurability - 1 }),
            "A majdnem törött páncélt vagy pajzsot nem lehetett felszerelni.");
        var breakingEnemyWeapon = weapon with { Id = "W-BREAK-NOTICE", Damage = new ValueRange(1, 1) };
        var breakingEnemy = new ConfiguredEnemy(new Position(1, 1),
            CreateEnemy(10000, 5, speed: 100).Definition);
        var breakingSystem = CreateBattleSystem(31);
        var breakingRuntime = breakingSystem.PrepareCharacter(breakingDefender).Runtime;
        var defensiveBreakNotices = new List<BattleLogNotice>();
        for (var attempt = 0; attempt < 40 && defensiveBreakNotices.Count < 2; attempt++)
        {
            var resolution = breakingSystem.ResolveEnemyActionDetailed(
                breakingEnemy, breakingDefender, breakingRuntime, breakingEnemyWeapon);
            if (resolution.Entry.FollowUps is { } followUps) defensiveBreakNotices.AddRange(followUps);
        }
        Assert(defensiveBreakNotices.Count(notice =>
                   notice.Message.Contains("eltört", StringComparison.OrdinalIgnoreCase) &&
                   notice.Message.Contains("nem ad védelmet", StringComparison.OrdinalIgnoreCase)) == 2,
            "A páncél és a pajzs törése nem adott külön, következményt is leíró csatalog-üzenetet. " +
            string.Join(" | ", defensiveBreakNotices.Select(notice => notice.Message)));

        var indestructible = shield with { Id = "W-INDESTRUCTIBLE-TEST", MaximumDurability = 0 };
        Assert(defender.SetInventoryItem(InventorySlotKind.Weapon, 1, indestructible, null, 1),
            "A törhetetlen pajzsot nem lehetett felszerelni.");
        Assert(!defender.ApplyInventoryItemWear(InventorySlotKind.Weapon, 1, EquipmentWearCause.Attack, 100).Changed &&
               defender.GetInventoryItemState(InventorySlotKind.Weapon, 1)!.Value.DurabilityDamage == 0,
            "A nulla maximális tartósságú, törhetetlen felszerelés kopást kapott.");
    }

    static void AcidAndChaosCauseSpecialEquipmentWear()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var armor = data.GetArmor("A001");
        var shield = data.GetWeapon("W014");
        var weapon = data.GetWeapon("W001");

        (EnemyAttackResolution Resolution, int ArmorWear, int ShieldWear, int WeaponWear) ResolveHit(
            DamageType damageType, int seed)
        {
            var defender = CreateCharacter($"{damageType.Name()} célpont", 1000);
            Assert(defender.EquipWeapon(0, weapon) && defender.EquipWeapon(1, shield) && defender.EquipArmor(armor),
                "A különleges kopási teszt felszerelése nem volt feladható.");
            var attackWeapon = data.GetWeapon("WN003") with
            {
                Id = $"W-{damageType}-WEAR-TEST",
                Damage = new ValueRange(1, 1),
                DamageType = damageType
            };
            var enemy = new ConfiguredEnemy(new Position(1, 1),
                CreateEnemy(10000, 5, speed: 100).Definition);
            var system = CreateBattleSystem(seed);
            var runtime = system.PrepareCharacter(defender).Runtime;
            for (var attempt = 0; attempt < 40; attempt++)
            {
                var resolution = system.ResolveEnemyActionDetailed(enemy, defender, runtime, attackWeapon);
                if (!resolution.Hit) continue;
                return (resolution,
                    defender.GetInventoryItemState(InventorySlotKind.Armor, 0)!.Value.DurabilityDamage,
                    defender.GetInventoryItemState(InventorySlotKind.Weapon, 1)!.Value.DurabilityDamage,
                    defender.GetInventoryItemState(InventorySlotKind.Weapon, 0)!.Value.DurabilityDamage);
            }
            throw new InvalidOperationException("A különleges kopási próba negyven támadásból sem talált.");
        }

        var acid = ResolveHit(DamageType.Acid, 71);
        var expectedAcidWear = acid.Resolution.Entry.Kind == BattleLogKind.CriticalHit ? 4 : 2;
        Assert(acid.ArmorWear == expectedAcidWear && acid.ShieldWear == expectedAcidWear && acid.WeaponWear == 0 &&
               acid.Resolution.Entry.Details?.Calculation.Any(line =>
                   line.Contains("Savmarás", StringComparison.Ordinal)) == true,
            "A sav nem kétszeres alapkopással marta a páncélt és a pajzsot.");

        var chaos = ResolveHit(DamageType.Chaos, 73);
        var chaosWear = chaos.ArmorWear + chaos.ShieldWear + chaos.WeaponWear;
        var chaosMaximum = chaos.Resolution.Entry.Kind == BattleLogKind.CriticalHit ? 6 : 3;
        Assert(chaosWear >= 1 && chaosWear <= chaosMaximum &&
               new[] { chaos.ArmorWear, chaos.ShieldWear, chaos.WeaponWear }.Count(value => value > 0) == 1 &&
               chaos.Resolution.Entry.Details?.Calculation.Any(line =>
                   line.Contains("Káoszmarás", StringComparison.Ordinal)) == true,
            "A káoszsebzés nem egyetlen véletlen aktív felszerelést koptatott 1–3 ponttal.");
    }

    static void EquipmentLootStartsWithRandomWear()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var weapon = data.GetWeapon("W001");
        var damages = Enumerable.Range(1, 40)
            .Select(seed => ItemIdentificationRules.CreateLootState(weapon, data.ItemCurses, new Random(seed), 0,
                data.LootRules.MinimumEquipmentDurabilityPercent,
                data.LootRules.MaximumEquipmentDurabilityPercent).DurabilityDamage)
            .ToArray();
        var maximumDamage = weapon.MaximumDurability -
                            (int)Math.Ceiling(weapon.MaximumDurability *
                                data.LootRules.MinimumEquipmentDurabilityPercent / 100d);
        Assert(data.LootRules.MinimumEquipmentDurabilityPercent == 25 &&
               data.LootRules.MaximumEquipmentDurabilityPercent == 100 &&
               damages.Distinct().Count() > 1 && damages.All(damage => damage >= 0 && damage <= maximumDamage) &&
               damages.All(damage => damage < weapon.MaximumDurability),
            "A zsákmánykopás nem a konfigurált 25–100%-os megmaradt tartósságból sorsolódott.");

        var indestructible = data.GetWeapon("LW014");
        Assert(ItemIdentificationRules.CreateLootState(indestructible, data.ItemCurses, new Random(1), 0,
                   25, 100).DurabilityDamage == 0,
            "A törhetetlen zsákmány véletlen kopást kapott.");
    }

    static void WornEquipmentSellsForLess()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CharacterClassIds.Harcos };
        var weapon = new WeaponDefinition("W-SELL-WEAR", "Kopott kard", "WT001", new ValueRange(2, 4), 1,
            false, allowed, "", 1000, MaximumDurability: 100);
        var intact = InventoryItemInstanceState.Create();
        var worn = intact with { DurabilityDamage = 40 };
        var broken = intact with { DurabilityDamage = 100 };
        var indestructible = weapon with { Id = "W-SELL-FOREVER", MaximumDurability = 0 };
        Assert(EquipmentDurabilityRules.DepreciatedSellPrice(weapon, intact, 500) == 500 &&
               EquipmentDurabilityRules.DepreciatedSellPrice(weapon, worn, 500) == 300 &&
               EquipmentDurabilityRules.DepreciatedSellPrice(weapon, broken, 500) == 1 &&
               EquipmentDurabilityRules.DepreciatedSellPrice(indestructible, worn, 500) == 500,
            "A kereskedői ár nem a megmaradt tartósság arányát követi.");
    }

    static void UpgradesAndClassPerksImproveDurability()
    {
        var data = CsvGameDataLoader.Load(
            Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));

        Assert(data.GetWeapon("W001-PLUS1").MaximumDurability == 92 &&
               data.GetWeapon("W001-PLUS2").MaximumDurability == 104 &&
               data.GetWeapon("W001-PLUS3").MaximumDurability == 120 &&
               data.GetArmor("A001-PLUS1").MaximumDurability == 104 &&
               data.GetArmor("A001-PLUS2").MaximumDurability == 117 &&
               data.GetArmor("A001-PLUS3").MaximumDurability == 135,
            "A +1/+2/+3 tárgybővítés nem 15/30/50%-kal növelte a tartósságot.");

        const int attempts = 1000;

        var fighterWear = 0;

        for (var i = 0; i < attempts; i++)
        {
            var fighter = CreateCharacter(
                $"Fegyvrmest{i}",
                characterClassId: CharacterClassIds.Harcos);

            var weapon = data.GetWeapon("W001");

            Assert(
                fighter.AddPerk(data.GetPerk(PerkIds.FighterWeaponMaster)) &&
                fighter.EquipWeapon(0, weapon),
                "A Fegyvermester kopási próbája nem volt előkészíthető.");

            fighter.ApplyInventoryItemWear(
                InventorySlotKind.Weapon,
                0,
                EquipmentWearCause.Attack,
                1);

            fighterWear += fighter
                               .GetInventoryItemState(InventorySlotKind.Weapon, 0)?
                               .DurabilityDamage ?? 0;
        }

        Assert(
            fighterWear is >= 400 and <= 600,
            $"A Fegyvermester 50%-os kopásellenállása nem megfelelő. " +
            $"1000 próbából {fighterWear} kopás történt.");

        var armorWear = 0;
        var shieldWear = 0;

        for (var i = 0; i < attempts; i++)
        {
            var knight = CreateCharacter(
                $"Páncélmest{i}",
                characterClassId: CharacterClassIds.Lovag);

            var armor = data.GetArmor("A003");
            var shield = data.GetWeapon("W014");

            Assert(
                knight.AddPerk(data.GetPerk(PerkIds.KnightArmorMaster)) &&
                knight.EquipArmor(armor) &&
                knight.EquipWeapon(1, shield),
                "A Páncélmester kopási próbája nem volt előkészíthető.");

            knight.ApplyInventoryItemWear(
                InventorySlotKind.Armor,
                0,
                EquipmentWearCause.BeingAttacked,
                1);

            knight.ApplyInventoryItemWear(
                InventorySlotKind.Weapon,
                1,
                EquipmentWearCause.BeingAttacked,
                1);

            armorWear += knight
                             .GetInventoryItemState(InventorySlotKind.Armor, 0)?
                             .DurabilityDamage ?? 0;

            shieldWear += knight
                              .GetInventoryItemState(InventorySlotKind.Weapon, 1)?
                              .DurabilityDamage ?? 0;
        }

        Assert(
            armorWear is >= 400 and <= 600,
            $"A Páncélmester 50%-os páncél-kopásellenállása nem megfelelő. " +
            $"1000 próbából {armorWear} kopás történt.");

        Assert(
            shieldWear is >= 400 and <= 600,
            $"A Páncélmester 50%-os pajzs-kopásellenállása nem megfelelő. " +
            $"1000 próbából {shieldWear} kopás történt.");
    }

    static void EquipmentWearCauseMatchesEquipmentType()
    {
        var data = CsvGameDataLoader.Load(
            Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));

        var weapon = data.GetWeapon("W001");
        var shield = data.GetWeapon("W014");
        var armor = data.GetArmor("A003");

        Assert(
            EquipmentDurabilityRules.CanWearFrom(
                weapon,
                InventorySlotKind.Weapon,
                EquipmentWearCause.Attack),
            "A normál fegyvernek támadáskor kopnia kell.");

        Assert(
            !EquipmentDurabilityRules.CanWearFrom(
                weapon,
                InventorySlotKind.Weapon,
                EquipmentWearCause.BeingAttacked),
            "A normál fegyvernek védekezéskor nem szabad kopnia.");

        Assert(
            !EquipmentDurabilityRules.CanWearFrom(
                shield,
                InventorySlotKind.Weapon,
                EquipmentWearCause.Attack),
            "A pajzsnak támadáskor nem szabad kopnia.");

        Assert(
            EquipmentDurabilityRules.CanWearFrom(
                shield,
                InventorySlotKind.Weapon,
                EquipmentWearCause.BeingAttacked),
            "A pajzsnak védekezéskor kopnia kell.");

        Assert(
            EquipmentDurabilityRules.CanWearFrom(
                armor,
                InventorySlotKind.Armor,
                EquipmentWearCause.BeingAttacked),
            "A páncélnak védekezéskor kopnia kell.");
    }

    static void LegendaryDurabilityAndFieldRepairKit()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var repairKit = data.GetItem(MiscItemIds.RepairKit);
        Assert(repairKit is { Effect: ConsumableEffect.RepairEquipment, EffectValue: 50 } &&
               Math.Abs(repairKit.Weight - 0.8) < 0.001,
            "A javítókészlet adatai nem töltődtek be.");
        Assert(Math.Abs(data.GetArmor("LA018").Weight - 3) < 0.001 &&
               Math.Abs(data.GetArmor("LA019").Weight - 0.5) < 0.001 &&
               Math.Abs(data.GetArmor("LA020").Weight - 7) < 0.001,
            "A három legendás páncél javított súlya nem töltődött be.");
        Assert(data.GetWeapon("LW001").MaximumDurability == 200 &&
               data.GetWeapon("LW020").MaximumDurability == 300 &&
               data.GetWeapon("LW014").MaximumDurability == 0 &&
               data.GetArmor("LA001").MaximumDurability == 160 &&
               data.GetArmor("LA020").MaximumDurability == 340 &&
               data.Weapons.Where(item => item.Rarity == ItemRarity.Legendary && item.Id != "LW014")
                   .All(item => item.MaximumDurability >= 170) &&
               data.Armors.Where(item => item.Rarity == ItemRarity.Legendary)
                   .All(item => item.MaximumDurability >= 160),
            "A legendás felszerelések nem kapták meg az egyedi, emelt tartósságukat.");

        var character = CreateCharacter("Terepi javító");
        var weapon = data.GetWeapon("W001") with { Id = "W-FIELD-REPAIR", MaximumDurability = 200 };
        var state = InventoryItemInstanceState.Create() with { DurabilityDamage = 190 };
        Assert(character.SetInventoryItem(InventorySlotKind.Weapon, 0, weapon, null, 1, state),
            "A terepi javítás tesztfegyvere nem volt felszerelhető.");
        var first = character.RepairInventoryItemLimited(InventorySlotKind.Weapon, 0, 50, 75);
        var second = character.RepairInventoryItemLimited(InventorySlotKind.Weapon, 0, 50, 75);
        var capped = character.RepairInventoryItemLimited(InventorySlotKind.Weapon, 0, 50, 75);
        var blocked = character.RepairInventoryItemLimited(InventorySlotKind.Weapon, 0, 50, 75);
        var repairedState = character.GetInventoryItemState(InventorySlotKind.Weapon, 0)!.Value;
        Assert(first is { Changed: true, PreviousDurability: 10, CurrentDurability: 60 } &&
               second is { Changed: true, PreviousDurability: 60, CurrentDurability: 110 } &&
               capped is { Changed: true, PreviousDurability: 110, CurrentDurability: 150 } &&
               !blocked.Changed && repairedState.DurabilityDamage == 50 && repairedState.InstanceId == state.InstanceId,
            "A javítókészlet nem 50 pontos részjavítást vagy nem 75%-os terepi korlátot alkalmazott.");
    }

    static void DamagedAndBrokenEquipmentAffectsCombat()
    {
        Assert(EquipmentDurabilityRules.WeaponHitPenalty(EquipmentCondition.Worn) == 1 &&
               EquipmentDurabilityRules.WeaponHitPenalty(EquipmentCondition.Damaged) == 2 &&
               EquipmentDurabilityRules.WeaponDamagePenalty(EquipmentCondition.Worn) == 1 &&
               EquipmentDurabilityRules.WeaponDamagePenalty(EquipmentCondition.Damaged) == 2 &&
               EquipmentDurabilityRules.ScaleDefense(9, EquipmentCondition.Worn) == 7 &&
               EquipmentDurabilityRules.ScaleDefense(9, EquipmentCondition.Damaged) == 5 &&
               EquipmentDurabilityRules.ScaleDefense(-3, EquipmentCondition.Intact) == -3 &&
               EquipmentDurabilityRules.ScaleDefense(-3, EquipmentCondition.Damaged) == -2 &&
               EquipmentDurabilityRules.ScaleDefense(9, EquipmentCondition.Broken) == 0,
            "A kopott, sérült vagy törött felszerelés alapvető harci módosítói hibásak.");

        var data = CsvGameDataLoader.Load(
            Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        CharacterClassIds.Harcos
    };

        var weapon = data.GetWeapon("W001") with
        {
            Id = "W-CONDITION-TEST",
            Damage = new ValueRange(5, 5),
            MaximumDurability = 10000,
            AllowedClassIds = allowed
        };

        (int Damage, BattleLogEntry? FirstHit) AttackSeries(int durabilityDamage)
        {
            var character = CreateCharacter("Kopott támadó", 1000);

            Assert(character.SetInventoryItem(
                    InventorySlotKind.Weapon,
                    0,
                    weapon,
                    null,
                    1,
                    InventoryItemInstanceState.Create() with
                    {
                        DurabilityDamage = durabilityDamage
                    }),
                "Az állapotteszt fegyvere nem volt felszerelhető.");

            var system = CreateBattleSystem(41);
            var runtime = system.PrepareCharacter(character).Runtime;
            var enemy = CreateEnemy(100000, 1);

            BattleLogEntry? firstHit = null;

            for (var attack = 0; attack < 100; attack++)
            {
                var before = enemy.CurrentHitPoints;

                var entry = system.ResolveCharacterAttack(
                    character,
                    runtime,
                    enemy,
                    finishAction: false);

                if (enemy.CurrentHitPoints < before && firstHit is null)
                    firstHit = entry;
            }

            return (100000 - enemy.CurrentHitPoints, firstHit);
        }

        // 100% tartósság → Intact
        var intactAttack = AttackSeries(0);

        // 50% tartósság → Worn
        var wornAttack = AttackSeries(5000);

        // 25% tartósság → Damaged
        var damagedAttack = AttackSeries(7500);

        Assert(
            wornAttack.Damage < intactAttack.Damage &&
            damagedAttack.Damage < wornAttack.Damage,
            "A kopott és sérült fegyver nem okozott fokozatosan nagyobb harci hátrányt.");

        Assert(
            damagedAttack.FirstHit?.Details?.Calculation.Any(line =>
                line.Contains("Sérült fegyver", StringComparison.OrdinalIgnoreCase)) == true,
            "A harci napló nem jelzi a sérült fegyver hátrányát.");

        var brokenAttacker = CreateCharacter("Töröttkezű");

        Assert(brokenAttacker.SetInventoryItem(
                InventorySlotKind.Weapon,
                0,
                weapon,
                null,
                1,
                InventoryItemInstanceState.Create() with
                {
                    DurabilityDamage = weapon.MaximumDurability
                }),
            "A törött tesztfegyvert nem lehetett felszerelve tárolni.");

        Assert(
            brokenAttacker.WeaponSlots[0] == weapon &&
            brokenAttacker.AttackWeapon is null &&
            !brokenAttacker.IsInventoryItemOperational(InventorySlotKind.Weapon, 0),
            "A törött fegyver eltűnt a slotból vagy továbbra is használható maradt.");

        Assert(
            ItemInspectionFormatter.Format(
                    weapon,
                    data,
                    instanceState: brokenAttacker.GetInventoryItemState(
                        InventorySlotKind.Weapon, 0))
                .Text
                .Contains("nem használható fegyverként",
                    StringComparison.OrdinalIgnoreCase),
            "A tárgyvizsgálat nem magyarázza el a törött fegyver következményét.");

        var armor = data.GetArmor("A001") with
        {
            Id = "A-CONDITION-TEST",
            Defense = new ValueRange(10, 10),
            MaximumDurability = 10000,
            AllowedClassIds = allowed
        };

        int DamageReceived(int durabilityDamage)
        {
            var defender = CreateCharacter("Kopott védő", 100000);

            Assert(defender.SetInventoryItem(
                    InventorySlotKind.Armor,
                    0,
                    armor,
                    null,
                    1,
                    InventoryItemInstanceState.Create() with
                    {
                        DurabilityDamage = durabilityDamage
                    }),
                "Az állapotteszt páncélja nem volt felszerelhető.");

            var enemyWeapon = weapon with
            {
                Id = "W-ENEMY-CONDITION",
                Damage = new ValueRange(20, 20)
            };

            var enemyDefinition = CreateEnemy(1000, 5).Definition;

            var enemy = new ConfiguredEnemy(
                new Position(1, 1),
                enemyDefinition);

            var system = CreateBattleSystem(67);
            var runtime = system.PrepareCharacter(defender).Runtime;

            for (var attack = 0; attack < 100; attack++)
            {
                system.ResolveEnemyAction(
                    enemy,
                    defender,
                    runtime,
                    enemyWeapon);
            }

            return 100000 - defender.CurrentVitality;
        }

        var intactArmorDamage = DamageReceived(0);
        var wornArmorDamage = DamageReceived(5000);
        var damagedArmorDamage = DamageReceived(7500);
        var brokenArmorDamage = DamageReceived(10000);

        Assert(
            intactArmorDamage < wornArmorDamage &&
            wornArmorDamage < damagedArmorDamage &&
            damagedArmorDamage < brokenArmorDamage,
            "A kopott, sérült és törött páncél nem fokozatosan csökkenő védelemmel működött.");

        Assert(
            ItemInspectionFormatter.Format(
                    armor,
                    data,
                    instanceState: InventoryItemInstanceState.Create() with
                    {
                        DurabilityDamage = 7500
                    })
                .Text
                .Contains("védelem 50%-a", StringComparison.OrdinalIgnoreCase),
            "A tárgyvizsgálat nem magyarázza el a sérült páncél következményét.");
    }

    static void EquipmentRepairRestoresDurability()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CharacterClassIds.Harcos };
        var normal = new WeaponDefinition("W-REPAIR-N", "Javítandó kard", "WT001", new ValueRange(2, 4), 1,
            false, allowed, "", 1000, Rarity: ItemRarity.Normal, MaximumDurability: 100);
        var magic = normal with { Id = "W-REPAIR-M", Rarity = ItemRarity.Magic };
        var legendary = normal with { Id = "W-REPAIR-L", Rarity = ItemRarity.Legendary };
        var state = new InventoryItemInstanceState(Guid.NewGuid(), false, "CURSE-TEST",
            ItemCurseEffect.HitPenalty, 1, 2, true, CharacterId.New(), DurabilityDamage: 60);
        Assert(EquipmentDurabilityRules.FullRepairCost(normal, state) == 150 &&
               EquipmentDurabilityRules.FullRepairCost(magic, state) == 210 &&
               EquipmentDurabilityRules.FullRepairCost(legendary, state) == 300 &&
               EquipmentDurabilityRules.FullRepairCost(normal, state with { DurabilityDamage = 0 }) == 0,
            "A teljes javítás díja nem a hiányzó tartósság 25/35/50%-os árhányadát követi.");

        var character = CreateCharacter("Javítás");
        var boundState = state with { BoundCharacterId = character.Id };
        Assert(character.SetInventoryItem(InventorySlotKind.Weapon, 0, magic, null, 1, boundState),
            "A javítási tesztfegyvert nem lehetett felszerelni.");
        var revisionBefore = character.InventoryRevision;
        Assert(character.RepairInventoryItemFully(InventorySlotKind.Weapon, 0),
            "A kopott felszerelés teljes javítása sikertelen volt.");
        var repaired = character.GetInventoryItemState(InventorySlotKind.Weapon, 0)!.Value;
        Assert(repaired.DurabilityDamage == 0 && repaired.InstanceId == boundState.InstanceId &&
               repaired.IsIdentified == boundState.IsIdentified && repaired.CurseId == boundState.CurseId &&
               repaired.IsCurseActivated && repaired.BoundCharacterId == character.Id &&
               character.InventoryRevision == revisionBefore + 1,
            "A javítás lecserélte a példányt, elvesztette az azonosítás/átok állapotát vagy nem frissített revíziót.");
        Assert(!character.RepairInventoryItemFully(InventorySlotKind.Weapon, 0),
            "A teljesen ép felszerelést ismét meg lehetett javítani.");

        var repairItem = new InventoryItemSnapshot(magic.Id, $"{character.Name}: {magic.Name}",
            ItemCategory.Weapon, magic.Rarity, 0, 0, Description: "Teljes javítás: 40/100 → 100/100 tartósság.",
            BasePrice: magic.BasePrice, MaximumDurability: 100, DurabilityDamage: 60);
        var repairVendor = new InnVendorSnapshot(InnVendorKind.BlacksmithRepair, "Javítóműhely",
            [new InnOfferSnapshot(0, repairItem, 210)]);
        var lines = ConsoleRenderer.BuildInnVendorLines(repairVendor, InnMarketMode.Buy, [], 0,
            500, 0, "Válassz javítást.", "Tesztfogadó");
        Assert(lines.Any(line => line.Text.Contains("FEGYVERJAVÍTÁS", StringComparison.Ordinal)) &&
               lines.Any(line => line.Text.Contains("40/100 → 100/100", StringComparison.Ordinal)) &&
               lines.Any(line => line.Text.Contains("Enter javítás", StringComparison.Ordinal)) &&
               lines.Any(line => line.Text.Contains("210", StringComparison.Ordinal)),
            "A közös host/vendég javítóképernyő nem mutatja az állapotváltozást, árat vagy vezérlést.");
    }

    static void MageIdentifiesFreshMagicLoot()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var race = data.Races[0];
        var mageClass = data.CharacterClasses.First(value => value.Id == CharacterClassIds.Mágus);
        var weakerMage = new LiveCharacter("Tanonc", race, mageClass, new PrimaryAbilities(5, 5, 5, 7),
            20, 30, 0, 0);
        var strongerMage = new LiveCharacter("Tudós", race, mageClass, new PrimaryAbilities(5, 5, 5, 12),
            20, 30, 0, 0);
        var fighter = CreateCharacter("Harcos");
        var item = data.MagicItems.Where(value => value.Rarity == ItemRarity.Magic)
            .OrderBy(value => value.MagicPower).First();
        var state = InventoryItemInstanceState.Create(identified: false);

        var result = ItemIdentificationRules.AttemptByBestMage(item, state,
            [fighter, weakerMage, strongerMage], new Random(1));

        Assert(result.Attempted && result.Mage == strongerMage,
            "Nem a legmagasabb effektív Intelligenciájú élő Mágus végezte a próbát.");
        Assert(result.ChancePercent == ItemIdentificationRules.MageIdentificationChance(strongerMage, item) &&
               result.Succeeded == (result.Roll <= result.ChancePercent) &&
               result.State.IsIdentified == result.Succeeded && result.State.InstanceId == state.InstanceId,
            "A mágusi azonosítás nem a dokumentált esély vagy dobás szerint módosította a példányállapotot.");

        var withoutMage = ItemIdentificationRules.AttemptByBestMage(item, state, [fighter], new Random(1));
        Assert(!withoutMage.Attempted && !withoutMage.State.IsIdentified,
            "Mágus nélkül is történt automatikus tárgyazonosítás.");
    }

    static void CursedItemsActivateBindAndApplyEffects()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        Assert(data.ItemCurses.Count == 8 && data.ItemCurses.Select(curse => curse.Effect).Distinct().Count() == 8,
            "A nyolc adatvezérelt átok nem töltődött be.");
        var character = CreateCharacter("Átokpróba", characterClassId: CharacterClassIds.Mágus);
        var item = new MagicItemDefinition("MI-CURSE", "Próbagyűrű", MagicItemKind.Ring, ItemRarity.Magic,
            1000, 0, null, MagicItemEffect.None, 0, new HashSet<string> { CharacterClassIds.Mágus },
            "Átokpróba", 3);
        var curse = data.ItemCurses.First(value => value.Effect == ItemCurseEffect.ManaCost);
        Assert(ItemIdentificationRules.CreateLootState(item, data.ItemCurses, new Random(1), 100).HasCurse,
            "A garantált átokdobás nem rendelt kompatibilis átkot a varázstárgyhoz.");
        var state = new InventoryItemInstanceState(Guid.NewGuid(), false, curse.Id, curse.Effect,
            curse.Value, curse.Strength);
        Assert(character.AddToBackpack(item, identified: false, state.InstanceId, state),
            "Az átkozott példány nem került a hátizsákba.");
        var party = new Party();
        party.SetLeader(character);
        var command = new InventoryTransferCommand(PlayerId.New(), 1, character.Id, character.InventoryRevision,
            InventorySlotKind.Backpack, 0, character.Id, character.InventoryRevision,
            InventorySlotKind.MagicItem, 0);
        Assert(InventoryTransferService.TryExecute(party, command, out var result, out var error), error);
        var activated = character.GetInventoryItemState(InventorySlotKind.MagicItem, 0);
        Assert(activated is { IsCurseActivated: true, HasCurse: true } &&
               activated.Value.BoundCharacterId == character.Id && result.CurseActivations?.Count == 1,
            "A felszerelt átok nem aktiválódott vagy nem kötődött a viselőhöz.");

        var remove = new InventoryTransferCommand(PlayerId.New(), 2, character.Id, character.InventoryRevision,
            InventorySlotKind.MagicItem, 0, character.Id, character.InventoryRevision,
            InventorySlotKind.Backpack, 1);
        Assert(!InventoryTransferService.TryExecute(party, remove, out _, out _),
            "Az aktív átkozott tárgy levehető volt.");
        Assert(character.GetActiveCurseValue(ItemCurseEffect.ManaCost) == curse.Value,
            "Az aktív átok értéke nem került be a karakter szabályaiba.");
        var spell = data.Spells.First(value => value.ManaCost > 0);
        Assert(SpellcastingRules.EffectiveManaCost(character, spell) == spell.ManaCost + curse.Value,
            "A Manafaló nem növelte a varázslat mannaköltségét.");

        Assert(character.IdentifyInventoryItem(InventorySlotKind.MagicItem, 0),
            "Az aktív átkozott tárgy nem volt azonosítható.");
        var snapshot = InventorySnapshotProjector.Create(character).Slots.Single(slot =>
            slot.Kind == InventorySlotKind.MagicItem && slot.Index == 0).Item!;
        Assert(snapshot.CurseId == curse.Id && snapshot.IsCurseActivated && snapshot.Name.Contains('☠'),
            "Az azonosítás nem fedte fel az aktív átkot.");
    }

    static void ItemCursePurificationIsPermanent()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var character = CreateCharacter("Tisztító", characterClassId: CharacterClassIds.Mágus);
        var item = new MagicItemDefinition("MI-PURIFY", "Próbagyűrű", MagicItemKind.Ring, ItemRarity.Magic,
            1000, 0, null, MagicItemEffect.None, 0, new HashSet<string> { CharacterClassIds.Mágus },
            "Átoktörési próba", 3);
        var weak = data.ItemCurses.First(curse => curse.Strength == 1 && curse.CanAffect(item));
        var strong = data.ItemCurses.First(curse => curse.Strength == 3 && curse.CanAffect(item));
        var weakState = new InventoryItemInstanceState(Guid.NewGuid(), true, weak.Id, weak.Effect,
            weak.Value, weak.Strength);
        var strongState = new InventoryItemInstanceState(Guid.NewGuid(), true, strong.Id, strong.Effect,
            strong.Value, strong.Strength);
        Assert(character.SetInventoryItem(InventorySlotKind.MagicItem, 0, item, 0, 1, weakState) &&
               character.SetInventoryItem(InventorySlotKind.MagicItem, 1, item, 0, 1, strongState),
            "Az átkozott próbatárgyak nem voltak felszerelhetők.");

        Assert(character.PurifyStrongestActiveCurse()?.Id == item.Id,
            "Az Átoktörés nem a legerősebb aktív tárgyátkot választotta.");
        var purified = character.GetInventoryItemState(InventorySlotKind.MagicItem, 1);
        Assert(purified is { IsPurified: true, IsCurseActivated: false, BoundCharacterId: null } &&
               !purified.Value.HasCurse && purified.Value.CurseId == strong.Id,
            "A megtisztítás nem őrizte meg az átok előéletét vagy nem oldotta fel a kötést.");
        Assert(character.SetInventoryItem(InventorySlotKind.MagicItem, 1, null, 0, 0),
            "A megtisztított tárgy továbbra sem volt levehető.");
        Assert(character.HasActiveCurse && character.PurifyInventoryItem(InventorySlotKind.MagicItem, 0) &&
               !character.HasActiveCurse,
            "A Vándormágus-jellegű célzott megtisztítás nem szüntette meg az aktív hátrányt.");
        Assert(ItemIdentificationRules.CurseRemovalPrice(item, weakState) == 50 + 120 + 75 + 100,
            "A Vándormágus átoktörési díja nem a dokumentált képletet követi.");
        Assert(data.GetSpellEffects("P027").Any(effect => effect.Type == SpellEffectType.BreakItemCurse),
            "Az Átoktörés varázslathoz nincs tárgyátok-tisztítás rendelve.");
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
        Assert(!status.Identity.Contains("👑", StringComparison.Ordinal) && status.InvertedNameStart >= 0,
            "A vezér neve nem inverz jelölést kapott a party státuszban.");
        Assert(status.Text.Contains("❤️10", StringComparison.Ordinal) &&
               status.Text.Contains("🔷12", StringComparison.Ordinal),
            "A party státusz rosszul mutatja a HP-t és a manát.");
        Assert(status.VitalityColor == ConsoleColor.Red && status.ManaColor == ConsoleColor.Cyan,
            "A party státusz erőforrásszínei nem követik a százalékos küszöböket.");

        var fighter = CreateCharacter("Hosszú Harcos", vitality: 40,
            characterClassId: CharacterClassIds.Harcos);
        var fighterStatus = CharacterSheetPanel.BuildPartyStatus(fighter, false);
        Assert(string.IsNullOrEmpty(fighterStatus.Mana) &&
               !fighterStatus.Text.Contains("🔷", StringComparison.Ordinal) &&
               !fighterStatus.Identity.Contains("👑", StringComparison.Ordinal),
            "A manát nem használó karakter party státusza helyet foglal a manna számára.");
    }

    static void WindowFrameCatalogIsResizableAndConfigured()
    {
        Assert(ConsoleRenderer.MessageLogLineCountForMonitorHeight(1080) == 7 &&
               ConsoleRenderer.MessageLogLineCountForMonitorHeight(1199) == 7 &&
               ConsoleRenderer.MessageLogLineCountForMonitorHeight(1200) == 11 &&
               ConsoleRenderer.MessageLogLineCountForMonitorHeight(2160) == 11 &&
               ConsoleRenderer.MessageLogLineCountForWindowHeight(
                   ConsoleRenderer.ScreenRowCountForMessageLogLineCount(ConsoleRenderer.StandardMessageLogLineCount) - 1) == 7 &&
               ConsoleRenderer.MessageLogLineCountForWindowHeight(
                   ConsoleRenderer.ScreenRowCountForMessageLogLineCount(ConsoleRenderer.StandardMessageLogLineCount)) == ConsoleRenderer.StandardMessageLogLineCount &&
               ConsoleRenderer.MessageLogLineCountForWindowHeight(
                   ConsoleRenderer.ScreenRowCountForMessageLogLineCount(ConsoleRenderer.StandardMessageLogLineCount) + 3) == ConsoleRenderer.StandardMessageLogLineCount + 3 &&
               ConsoleRenderer.MessageLogBufferLineCount == ConsoleRenderer.MessageLogLineCount * 3 &&
               ConsoleRenderer.ScreenRowCount == ConsoleRenderer.PlayfieldHeight +
                   ConsoleRenderer.MessageLogLineCount + 1,
            "A fő játékfelület 1200p-s négy extra logsora vagy a hozzá igazodó magassága hibás.");
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
}
