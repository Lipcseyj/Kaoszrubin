using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin.Data;

public sealed partial class RandomCharacterGenerator(GameDataCatalog gameData, Random random)
{
    private const int AbilityPointTotal = 25;
    private readonly GameDataCatalog _gameData = gameData;
    private readonly Random _random = random;

    private LiveCharacter CreateLevelOneCore(IReadOnlyCollection<string> usedNames)
    {
        for (var attempt = 0; attempt < 2_000; attempt++)
        {
            var race = _gameData.Races[_random.Next(_gameData.Races.Count)];
            var adaptableAbilityBonus = RandomAdaptableAbilityBonus(race);
            var rolledAbilities = RollAbilities();
            var finalAbilities = (rolledAbilities + race.AbilityBonuses + adaptableAbilityBonus).Clamp(1, 13);
            var eligibleClasses = _gameData.CharacterClasses.Where(candidate => finalAbilities.MeetsMinimum(candidate.MinimumAbilities)).ToList();
            if (eligibleClasses.Count == 0) continue;
            var characterClass = eligibleClasses[_random.Next(eligibleClasses.Count)];
            var name = ChooseName(characterClass.Id, usedNames);
            var character = LiveCharacterFactory.Create(name, race, characterClass, rolledAbilities,
                _random.Next(1, 16), _random.Next(1, 16), _gameData,
                RandomCharacterColor(), adaptableAbilityBonus);
            character.SetNpcBehavior(BehaviorFor(characterClass.Id));
            AddRandomWeaponProficiencies(character);
            SpellcastingRules.GiveAutomaticStartingSpells(character, _gameData, _random);
            return character;
        }
        throw new InvalidOperationException("A jelenlegi játékadatokból nem generálható véletlen partitárs.");
    }

    private LiveCharacter CreateLevelOneCore(CharacterClassDefinition characterClass, IReadOnlyCollection<string> usedNames)
    {
        for (var attempt = 0; attempt < 2_000; attempt++)
        {
            var race = _gameData.Races[_random.Next(_gameData.Races.Count)];
            var adaptableAbilityBonus = RandomAdaptableAbilityBonus(race);
            var rolledAbilities = RollAbilities();
            var finalAbilities = (rolledAbilities + race.AbilityBonuses + adaptableAbilityBonus).Clamp(1, 13);
            if (!finalAbilities.MeetsMinimum(characterClass.MinimumAbilities)) continue;
            var character = LiveCharacterFactory.Create(ChooseName(characterClass.Id, usedNames), race, characterClass,
                rolledAbilities, _random.Next(1, 16), _random.Next(1, 16), _gameData,
                RandomCharacterColor(), adaptableAbilityBonus);
            character.SetNpcBehavior(BehaviorFor(characterClass.Id));
            AddRandomWeaponProficiencies(character);
            SpellcastingRules.GiveAutomaticStartingSpells(character, _gameData, _random);
            return character;
        }
        throw new InvalidOperationException($"A(z) {characterClass.Name} osztályhoz nem sikerült fejlesztői karaktert generálni.");
    }

    private LiveCharacter GenerateNpcCore(CharacterClassDefinition characterClass, int targetLevel,
        IReadOnlyCollection<string> usedNames, bool allowWhiteColor, EquipmentOptions equipment)
    {
        for (var attempt = 0; attempt < 2_000; attempt++)
        {
            var race = _gameData.Races[_random.Next(_gameData.Races.Count)];
            var adaptableAbilityBonus = RandomAdaptableAbilityBonus(race);
            var rolledAbilities = RollAbilities();
            var finalAbilities = (rolledAbilities + race.AbilityBonuses + adaptableAbilityBonus).Clamp(1, 13);
            if (!finalAbilities.MeetsMinimum(characterClass.MinimumAbilities)) continue;

            var character = LiveCharacterFactory.Create(ChooseName(characterClass.Id, usedNames), race,
                characterClass, rolledAbilities, _random.Next(1, 16), _random.Next(1, 16), _gameData,
                RandomCharacterColor(allowWhiteColor), adaptableAbilityBonus);
            InitializeGeneratedCharacter(character, targetLevel);
            ApplyEquipment(character, equipment);
            return character;
        }

        throw new InvalidOperationException($"A(z) {characterClass.Name} osztályhoz nem sikerült érvényes NPC-t generálni.");
    }

    private void InitializeGeneratedCharacter(LiveCharacter character, int targetLevel)
    {
        character.SetNpcBehavior(BehaviorFor(character.CharacterClass.Id));
        SpellcastingRules.GiveAutomaticStartingSpells(character, _gameData, _random);
        RaiseToLevel(character, Math.Max(1, targetLevel));
        CompleteGeneratedProgression(character);
    }

    private void CompleteGeneratedProgression(LiveCharacter character)
    {
        AddRandomPerks(character);
        AddRandomTacticalDisciplines(character);
        AddRandomWeaponProficiencies(character);
    }

    private NpcBehavior BehaviorFor(string characterClassId) => characterClassId.ToUpperInvariant() switch
    {
        CharacterClassIds.Harcos => _random.Next(2) == 0 ? NpcBehavior.Defensive : NpcBehavior.Aggressive,
        CharacterClassIds.Barbár => NpcBehavior.Aggressive,
        CharacterClassIds.Lovag => NpcBehavior.Defensive,
        CharacterClassIds.Tolvaj => NpcBehavior.Scout,
        CharacterClassIds.Pap or CharacterClassIds.Mágus => NpcBehavior.Cautious,
        _ => NpcBehavior.Defensive
    };

    private ConsoleColor RandomCharacterColor(bool allowWhiteColor = false)
    {
        var colors = allowWhiteColor ? CharacterColors.Selectable : CharacterColors.WorldNpcSelectable;
        return colors[_random.Next(colors.Count)];
    }

    private string ChooseName(string characterClassId, IReadOnlyCollection<string> usedNames)
    {
        var names = _gameData.GetCharacterNames(characterClassId);
        var unused = names.Where(candidate => !usedNames.Contains(candidate.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        var candidates = unused.Count > 0 ? unused : names;
        if (candidates.Count == 0) throw new InvalidOperationException("A véletlen karakter osztályához nincs név az adatfájlban.");
        return candidates[_random.Next(candidates.Count)].Name;
    }

    private void RaiseToRandomLevel(LiveCharacter character)
    {
        var targetLevel = _random.Next(2, 31);
        RaiseToLevel(character, targetLevel);
    }

    internal void RaiseToLevel(LiveCharacter character, int targetLevel)
    {
        while (character.Level < targetLevel)
        {
            var needed = character.GetExperienceNeededForNextLevel(_gameData.ExperienceByLevel);
            if (needed <= 0) break;
            var result = character.AddExperience(needed, _gameData.ExperienceByLevel,
                _gameData.GetVitalityGrowth(character.Abilities.Health),
                _gameData.GetManaGrowth(character.Abilities.Intelligence),
                _gameData.GetCharacterResourceGrowth(character.CharacterClass.Id), _random);
            SpellcastingRules.LearnAutomaticSpells(character, _gameData, result.Bonuses, _random);
        }
    }

    private void ApplyEquipment(LiveCharacter character, EquipmentOptions options)
    {
        if (options.Selection == EquipmentSelection.KeepStartingEquipment)
        {
            EnsureEquippedWeaponAmmunition(character);
            if (options.AddSupplies) FillRecruitBackpack(character);
            return;
        }
        if (options.Selection == EquipmentSelection.UnrestrictedRandom)
        {
            FillRandomEquipment(character, options.AllowLegendary);
            return;
        }

        var maximumTier = options.Selection == EquipmentSelection.ScaleWithLevel
            ? MaximumConfiguredUpgradePower(character.Level)
            : (int)options.Tier;
        ApplyConfiguredWeapons(character, maximumTier);
        ApplyConfiguredArmor(character, maximumTier);
        EnsureEquippedWeaponAmmunition(character);

        if (options.IncludeMagicItems) FillScaledMagicItems(character, maximumTier, options.TierVariance);
        if (options.AddSupplies) FillRecruitBackpack(character);
    }

    private int MaximumConfiguredUpgradePower(int level)
    {
        var powers = from rule in _gameData.CharacterGenerationUpgrades
            where rule.Includes(level)
            join upgrade in _gameData.ItemUpgrades on rule.UpgradeId.ToUpperInvariant()
                equals upgrade.Id.ToUpperInvariant()
            select upgrade.MagicPower;
        return powers.DefaultIfEmpty(0).Max();
    }

    private void ApplyConfiguredWeapons(LiveCharacter character, int maximumMagicPower)
    {
        var slotKinds = character.WeaponSlots.Select(weapon => weapon is null
            ? WeaponSlotGenerationKind.Empty
            : WeaponFamilies.ForWeapon(weapon) == WeaponFamilies.Shield
                ? WeaponSlotGenerationKind.Shield
                : WeaponSlotGenerationKind.Attack).ToArray();
        for (var slot = 0; slot < character.WeaponSlots.Count; slot++) character.EquipWeapon(slot, null);

        for (var slot = 0; slot < slotKinds.Length; slot++)
        {
            if (slotKinds[slot] == WeaponSlotGenerationKind.Empty) continue;
            var wantsShield = slotKinds[slot] == WeaponSlotGenerationKind.Shield;
            var candidates = _gameData.Weapons.Where(weapon =>
                    weapon.Rarity == ItemRarity.Normal && weapon.BaseWeaponId is null &&
                    weapon.CanBeEquippedBy(character.CharacterClass.Id, character.Abilities.Strength) &&
                    (WeaponFamilies.ForWeapon(weapon) == WeaponFamilies.Shield) == wantsShield &&
                    _gameData.CharacterGenerationEquipmentByItemId.TryGetValue(weapon.Id, out var rule) &&
                    rule.Includes(character.Level))
                .Where(weapon => slot != 1 || DualWieldingRules.CanEquipOffhand(
                    character, character.WeaponSlots[0], weapon)).ToList();
            if (candidates.Count == 0) continue;

            var proficient = candidates.Where(weapon =>
                character.WeaponProficiencyRankFor(WeaponFamilies.ForWeapon(weapon)) is not null).ToList();
            var selectedBase = SelectWeightedByLevel(proficient.Count > 0 ? proficient : candidates,
                character.Level);
            if (selectedBase is null) continue;
            character.EquipWeapon(slot, SelectMagicVariant(selectedBase, character.Level, maximumMagicPower));

            // A kétkezes főfegyver természetesen megszünteti a mellékkéz generálását.
            if (slot == 0 && selectedBase.IsTwoHanded && slotKinds.Length > 1)
                slotKinds[1] = WeaponSlotGenerationKind.Empty;
        }
    }

    private void ApplyConfiguredArmor(LiveCharacter character, int maximumMagicPower)
    {
        if (character.Armor is null) return;
        var candidates = _gameData.Armors.Where(armor =>
            armor.Rarity == ItemRarity.Normal && armor.BaseArmorId is null &&
            armor.CanBeEquippedBy(character.CharacterClass.Id) &&
            _gameData.CharacterGenerationEquipmentByItemId.TryGetValue(armor.Id, out var rule) &&
            rule.Includes(character.Level)).ToList();
        var selectedBase = SelectWeightedByLevel(candidates, character.Level);
        if (selectedBase is not null)
            character.EquipArmor(SelectMagicVariant(selectedBase, character.Level, maximumMagicPower));
    }

    private T? SelectWeightedByLevel<T>(IReadOnlyList<T> candidates, int level) where T : class, IItemDefinition
    {
        if (candidates.Count == 0) return null;
        var weighted = candidates.Select(candidate => new
        {
            Item = candidate,
            Weight = _gameData.CharacterGenerationEquipmentByItemId[candidate.Id].SelectionWeight(level)
        }).Where(candidate => candidate.Weight > 0).ToArray();
        var totalWeight = weighted.Sum(candidate => candidate.Weight);
        if (totalWeight <= 0) return null;
        var roll = _random.Next(totalWeight);
        foreach (var candidate in weighted)
        {
            if (roll < candidate.Weight) return candidate.Item;
            roll -= candidate.Weight;
        }
        return weighted[^1].Item;
    }

    private WeaponDefinition SelectMagicVariant(WeaponDefinition baseWeapon, int level, int maximumMagicPower)
    {
        var upgrade = RollMagicUpgrade(level, maximumMagicPower);
        if (upgrade is null) return baseWeapon;
        var generatedId = $"{baseWeapon.Id}-{upgrade.Id}";
        return _gameData.Weapons.FirstOrDefault(weapon =>
            string.Equals(weapon.Id, generatedId, StringComparison.OrdinalIgnoreCase)) ?? baseWeapon;
    }

    private ArmorDefinition SelectMagicVariant(ArmorDefinition baseArmor, int level, int maximumMagicPower)
    {
        var upgrade = RollMagicUpgrade(level, maximumMagicPower);
        if (upgrade is null) return baseArmor;
        var generatedId = $"{baseArmor.Id}-{upgrade.Id}";
        return _gameData.Armors.FirstOrDefault(armor =>
            string.Equals(armor.Id, generatedId, StringComparison.OrdinalIgnoreCase)) ?? baseArmor;
    }

    private ItemUpgradeDefinition? RollMagicUpgrade(int level, int maximumMagicPower)
    {
        var choices = from rule in _gameData.CharacterGenerationUpgrades
            where rule.Includes(level)
            join upgrade in _gameData.ItemUpgrades on rule.UpgradeId.ToUpperInvariant()
                equals upgrade.Id.ToUpperInvariant()
            where upgrade.MagicPower <= maximumMagicPower
            orderby upgrade.MagicPower descending
            select (Rule: rule, Upgrade: upgrade);
        foreach (var choice in choices)
            if (_random.Next(100) < choice.Rule.ChancePercent(level))
                return choice.Upgrade;
        return null;
    }

    private enum WeaponSlotGenerationKind { Empty, Attack, Shield }

    private void FillScaledMagicItems(LiveCharacter character, int maximumTier, int variance)
    {
        for (var index = 0; index < LiveCharacter.MaximumMagicItemCount; index++)
            character.SetInventoryItem(InventorySlotKind.MagicItem, index, null);
        if (maximumTier == 0) return;

        var minimumTier = Math.Max(1, maximumTier - variance);
        var candidates = _gameData.MagicItems.Where(item =>
                !SpellcastingRules.IsRestrictedFromTradingAndGeneration(item) &&
                item.Rarity != ItemRarity.Legendary && item.MagicPower >= minimumTier &&
                item.MagicPower <= maximumTier && item.CanBeEquippedBy(character.CharacterClass.Id))
            .OrderBy(_ => _random.Next()).ToList();
        var minimumCount = maximumTier >= 2 ? 1 : 0;
        var maximumCount = Math.Min(maximumTier, LiveCharacter.MaximumMagicItemCount);
        var count = _random.Next(minimumCount, maximumCount + 1);
        foreach (var item in candidates.Take(count)) character.AddMagicItem(item);
    }

    private void FillRecruitBackpack(LiveCharacter character)
    {
        for (var index = 0; index < LiveCharacter.MaximumBackpackItemCount; index++)
            character.SetInventoryItem(InventorySlotKind.Backpack, index, null);
        var preferredPotion = character.UsesMana ? ConsumableEffect.RestoreMana : ConsumableEffect.Heal;
        var supplies = _gameData.Items.Where(item => item.Effect == preferredPotion &&
            !SpellcastingRules.IsRestrictedFromTradingAndGeneration(item)).ToList();
        var itemCount = _random.Next(1, 4);
        for (var index = 0; index < itemCount && supplies.Count > 0; index++)
            character.AddToBackpack(supplies[_random.Next(supplies.Count)]);
    }

    private void AddRandomPerks(LiveCharacter character)
    {
        for (var tier = 1; tier <= 3; tier++)
        {
            var milestone = PerkProgressionRules.TriggerLevel(character.Race, tier);
            if (character.Level < milestone) continue;
            var choices = _gameData.GetPerkChoices(character.CharacterClass.Id, tier);
            var perk = choices[_random.Next(choices.Count)];
            if (character.AddPerk(perk)) character.ApplyPerkAcquisitionBonus(perk);
            if (tier == 1 && character.SpecializationId is null)
            {
                var specializations = ClassSpecializations.ForClass(character.CharacterClass.Id);
                if (specializations.Count > 0)
                    character.ChooseSpecialization(specializations[_random.Next(specializations.Count)].Id);
            }
        }
    }

    private void AddRandomTacticalDisciplines(LiveCharacter character)
    {
        var earned = TacticalDisciplineProgression.EarnedChoices(character.Level);
        while (character.TacticalDisciplines.Count < earned)
        {
            var choices = TacticalDisciplines.All
                .Where(discipline => !character.HasTacticalDiscipline(discipline.Id)).ToArray();
            if (choices.Length == 0) break;
            character.ChooseTacticalDiscipline(choices[_random.Next(choices.Length)].Id);
        }
    }

    private void FillRandomEquipment(LiveCharacter character, bool allowLegendary)
    {
        allowLegendary = allowLegendary && _random.NextDouble() < 0.02;
        var maximumMagicPower = (int)EquipmentTier.Masterwork;
        var usableWeapons = _gameData.Weapons.Where(weapon =>
            weapon.CanBeEquippedBy(character.CharacterClass.Id, character.Abilities.Strength) &&
            IsEquipmentTierAvailable(weapon, maximumMagicPower, allowLegendary)).ToList();
        var dualWeapons = character.HasTacticalDiscipline(TacticalDisciplines.DualWield)
            ? usableWeapons.Where(weapon => !weapon.IsTwoHanded &&
                WeaponFamilies.ForWeapon(weapon) is WeaponFamilies.Dagger or WeaponFamilies.Sword &&
                character.WeaponProficiencyRankFor(WeaponFamilies.ForWeapon(weapon)) is not null).ToList()
            : [];
        if (dualWeapons.Count > 0)
        {
            character.EquipWeapon(0, dualWeapons[_random.Next(dualWeapons.Count)]);
            character.EquipWeapon(1, dualWeapons[_random.Next(dualWeapons.Count)]);
        }
        else if (usableWeapons.Count > 0)
        {
            var firstWeapon = usableWeapons[_random.Next(usableWeapons.Count)];
            character.EquipWeapon(0, firstWeapon);
            var usableSecondWeapons = usableWeapons.Where(weapon => !weapon.IsTwoHanded).ToList();
            if (!firstWeapon.IsTwoHanded && usableSecondWeapons.Count > 0)
                character.EquipWeapon(1, usableSecondWeapons[_random.Next(usableSecondWeapons.Count)]);
        }
        EnsureEquippedWeaponAmmunition(character);
        var usableArmors = _gameData.Armors.Where(armor => armor.CanBeEquippedBy(character.CharacterClass.Id) &&
            IsEquipmentTierAvailable(armor, maximumMagicPower, allowLegendary)).ToList();
        if (usableArmors.Count > 0) character.EquipArmor(usableArmors[_random.Next(usableArmors.Count)]);
        var magicItemCount = _random.Next(1, LiveCharacter.MaximumMagicItemCount + 1);
        foreach (var item in _gameData.MagicItems.Where(item => !SpellcastingRules.IsRestrictedFromTradingAndGeneration(item) &&
                         item.CanBeEquippedBy(character.CharacterClass.Id) &&
                         IsEquipmentTierAvailable(item, maximumMagicPower, allowLegendary))
                     .OrderBy(_ => _random.Next()).Take(magicItemCount)) character.AddMagicItem(item);

        var allItems = _gameData.Items.Cast<IItemDefinition>()
            .Concat(_gameData.Weapons).Concat(_gameData.Armors).Concat(_gameData.MagicItems)
            .Where(item => !SpellcastingRules.IsRestrictedFromTradingAndGeneration(item) &&
                           IsEquipmentTierAvailable(item, maximumMagicPower, allowLegendary)).ToList();
        var targetCount = _random.Next(3, LiveCharacter.MaximumBackpackItemCount + 1);
        while (character.Backpack.Count(item => item is not null) < targetCount)
            character.AddToBackpack(allItems[_random.Next(allItems.Count)]);
    }

    private void EnsureEquippedWeaponAmmunition(LiveCharacter character)
    {
        foreach (var ammunitionId in character.WeaponSlots
                     .OfType<WeaponDefinition>()
                     .Where(weapon => weapon.UsesAmmunition && weapon.AmmunitionItemId is not null)
                     .Select(weapon => weapon.AmmunitionItemId!)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var ammunition = _gameData.GetItem(ammunitionId);
            int Count() => Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount)
                .Where(index => string.Equals(
                    character.GetInventoryItem(InventorySlotKind.Backpack, index)?.Id,
                    ammunitionId, StringComparison.OrdinalIgnoreCase))
                .Sum(index => character.GetInventoryItemQuantity(InventorySlotKind.Backpack, index));
            while (Count() < 12 && character.AddToBackpack(ammunition))
            {
            }
        }
    }

    private void EquipDevelopmentMagicItems(LiveCharacter character)
    {
        for (var index = 0; index < LiveCharacter.MaximumMagicItemCount; index++)
            character.SetInventoryItem(InventorySlotKind.MagicItem, index, null);

        var wand = RandomMagicItem(character, item => item.Kind == MagicItemKind.Wand);
        var usableScrolls = _gameData.MagicItems.Where(item => item.Kind == MagicItemKind.Scroll &&
            item.CanBeEquippedBy(character.CharacterClass.Id) && item.SpellId is not null &&
            SpellcastingRules.CanUseCastingItem(character, item, _gameData.GetSpell(item.SpellId))).ToList();
        var scrollOrSecondWand = usableScrolls.Count > 0
            ? usableScrolls[_random.Next(usableScrolls.Count)]
            : RandomMagicItem(character, item => item.Kind == MagicItemKind.Wand);
        var passive = RandomMagicItem(character, item => item.Kind is MagicItemKind.Ring or MagicItemKind.Amulet);

        character.AddMagicItem(wand);
        character.AddMagicItem(scrollOrSecondWand);
        character.AddMagicItem(passive);
    }

    private void GiveDevelopmentKey(LiveCharacter character)
    {
        for (var index = 0; index < LiveCharacter.MaximumBackpackItemCount; index++)
            if (string.Equals(character.Backpack[index]?.Id, MiscItemIds.Key,
                    StringComparison.OrdinalIgnoreCase))
                character.SetInventoryItem(InventorySlotKind.Backpack, index, null);

        var key = _gameData.GetItem(MiscItemIds.Key);
        if (character.AddToBackpack(key)) return;
        character.SetInventoryItem(InventorySlotKind.Backpack, LiveCharacter.MaximumBackpackItemCount - 1, null);
        character.AddToBackpack(key);
    }

    private MagicItemDefinition RandomMagicItem(LiveCharacter character, Func<MagicItemDefinition, bool> predicate)
    {
        var candidates = _gameData.MagicItems.Where(item => predicate(item) &&
            item.CanBeEquippedBy(character.CharacterClass.Id) &&
            !SpellcastingRules.IsRestrictedFromTradingAndGeneration(item)).ToList();
        if (candidates.Count == 0)
            throw new InvalidOperationException($"Nincs megfelelő fejlesztői varázstárgy a(z) {character.CharacterClass.Name} osztályhoz.");
        return candidates[_random.Next(candidates.Count)];
    }

    private static bool IsEquipmentTierAvailable(IItemDefinition item, int maximumMagicPower, bool allowLegendary) =>
        item.Rarity switch
        {
            ItemRarity.Legendary => allowLegendary,
            ItemRarity.Magic => item.MagicPower <= maximumMagicPower,
            _ => true
        };

    private PrimaryAbilities RollAbilities()
    {
        var values = new[] { 1, 1, 1, 1 };
        var pointTotal = RollAbilityPointTotal();
        for (var remaining = pointTotal - values.Sum(); remaining > 0; remaining--)
        {
            var available = Enumerable.Range(0, values.Length).Where(index => values[index] < 10).ToArray();
            values[available[_random.Next(available.Length)]]++;
        }
        return new PrimaryAbilities(values[0], values[1], values[2], values[3]);
    }

    private void AddRandomWeaponProficiencies(LiveCharacter character)
    {
        var desiredAdvances = WeaponProficiencyProgression.EarnedAdvances(
            character.CharacterClass.Id, character.Level);
        // A távolsági családok tudatos játékosi szakosodások; a véletlen NPC-generálás
        // nem írja felül velük a korábbi közelharci szerepköröket.
        var families = WeaponFamilies.AvailableFor(character.CharacterClass.Id, _gameData.Weapons)
            .Where(family => family.Id is not WeaponFamilies.Bow and not WeaponFamilies.Crossbow)
            .ToArray();
        while (character.WeaponProficiencyAdvances < desiredAdvances)
        {
            var choices = families.Where(family =>
                character.WeaponProficiencyRankFor(family.Id) != WeaponProficiencyRank.Master &&
                (character.WeaponProficiencies.Count < 2 || character.WeaponProficiencyRankFor(family.Id) is not null))
                .ToArray();
            if (choices.Length == 0) break;
            character.TryAdvanceWeaponProficiency(choices[_random.Next(choices.Length)].Id);
        }
    }

    private PrimaryAbilities RandomAdaptableAbilityBonus(RaceDefinition race)
    {
        if (!race.HasTrait(RaceTraits.Adaptable)) return PrimaryAbilities.Zero;
        return _random.Next(4) switch
        {
            0 => new PrimaryAbilities(1, 0, 0, 0),
            1 => new PrimaryAbilities(0, 1, 0, 0),
            2 => new PrimaryAbilities(0, 0, 1, 0),
            _ => new PrimaryAbilities(0, 0, 0, 1)
        };
    }

    private int RollAbilityPointTotal()
    {
        var roll = _random.Next(100);
        return AbilityPointTotal + (roll switch
        {
            < 15 => 0,
            < 65 => 1,
            < 90 => 2,
            _ => 3
        });
    }
}
