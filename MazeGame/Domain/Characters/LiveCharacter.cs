using MazeGame.Domain.Combat;
using MazeGame.Domain.Inventory;
using MazeGame.Domain.Magic;

namespace MazeGame.Domain.Characters;

/// <summary>Egy játék közben létező karakter saját, konkrét értékekkel.</summary>
public sealed class LiveCharacter
{
    public const int MaximumNameLength = 13;
    private readonly WeaponDefinition?[] _weaponSlots = new WeaponDefinition?[2];
    private readonly MagicItemDefinition?[] _magicItems = new MagicItemDefinition?[MaximumMagicItemCount];
    private readonly int[] _magicItemCharges = new int[MaximumMagicItemCount];
    private readonly IItemDefinition?[] _backpack = new IItemDefinition?[MaximumBackpackItemCount];
    private readonly int[] _backpackItemCharges = new int[MaximumBackpackItemCount];
    private readonly List<PerkDefinition> _perks = [];
    private readonly List<ClassFeatureUpgradeDefinition> _classFeatureUpgrades = [];
    private readonly List<StatusDefinition> _statuses = [];
    private readonly List<SpellDefinition> _knownSpells = [];
    private readonly List<SpellDefinition> _memorizedSpells = [];
    private readonly SpellDefinition?[] _quickSpells = new SpellDefinition?[MaximumQuickSpellCount];
    private readonly List<ActiveSpellEffect> _activeSpellEffects = [];
    private int _explorationStepsTowardSpellAction;
    private readonly Dictionary<string, int?> _statusDurations = new(StringComparer.OrdinalIgnoreCase);
    private int _maximumVitality;
    private int _maximumMana;
    private int _divineSpellCycle;
    public LiveCharacter(string name, RaceDefinition race, CharacterClassDefinition characterClass, PrimaryAbilities abilities,
        int maximumVitality, int maximumMana, int vitalityBonus, int manaBonus, ConsoleColor color = ConsoleColor.Cyan,
        CharacterId? id = null)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaximumNameLength)
            throw new ArgumentException($"A karakternév 1 és {MaximumNameLength} karakter közötti lehet.", nameof(name));
        Id = id ?? CharacterId.New();
        Name = name;
        Color = color;
        Race = race;
        CharacterClass = characterClass;
        Abilities = abilities;
        _maximumVitality = maximumVitality;
        CurrentVitality = maximumVitality;
        _maximumMana = maximumMana;
        CurrentMana = maximumMana;
        VitalityBonus = vitalityBonus;
        ManaBonus = manaBonus;
    }

    public CharacterId Id { get; }
    public string Name { get; }
    public ConsoleColor Color { get; }
    public NpcBehavior? NpcBehavior { get; private set; }
    public RaceDefinition Race { get; }
    public CharacterClassDefinition CharacterClass { get; }
    public PrimaryAbilities Abilities { get; private set; }
    public int MaximumVitality => ApplyMaximumResourceModifier(_maximumVitality, status => status.MaximumVitalityPercent);
    public int UnmodifiedMaximumVitality => _maximumVitality;
    public int CurrentVitality { get; private set; }
    public int MaximumMana => ApplyMaximumResourceModifier(_maximumMana, status => status.MaximumManaPercent);
    public int UnmodifiedMaximumMana => _maximumMana;
    public int CurrentMana { get; private set; }
    public int VitalityBonus { get; }
    public int ManaBonus { get; }
    public bool UsesMana => CharacterClass.UsesMana;
    public bool IsAlive => CurrentVitality > 0;
    public int FoodLevel { get; private set; } = 100;
    public int WaterLevel { get; private set; } = 100;
    public int Gold { get; private set; }
    public int Level { get; private set; } = 1;
    public int Experience { get; private set; }
    public int AbilityIncreasesClaimed { get; private set; }
    public int DivineSpellCycle => _divineSpellCycle;
    public bool WasResurrectedThisLevel { get; private set; }
    public bool WasRelentlessUsedThisLevel { get; private set; }
    public bool KnightRetaliationReady { get; private set; }
    public string? SpecializationId { get; private set; }
    public ClassSpecializationDefinition? Specialization => ClassSpecializations.Find(SpecializationId);
    public IReadOnlyList<WeaponDefinition?> WeaponSlots => _weaponSlots;
    public ArmorDefinition? Armor { get; private set; }
    public IReadOnlyList<MagicItemDefinition?> MagicItems => _magicItems;
    public IReadOnlyList<int> MagicItemCharges => _magicItemCharges;
    public IReadOnlyList<IItemDefinition?> Backpack => _backpack;
    public IReadOnlyList<PerkDefinition> Perks => _perks;
    public IReadOnlyList<ClassFeatureUpgradeDefinition> ClassFeatureUpgrades => _classFeatureUpgrades;
    public IReadOnlyList<StatusDefinition> Statuses => _statuses;
    public IReadOnlyList<SpellDefinition> KnownSpells => _knownSpells;
    public IReadOnlyList<SpellDefinition> MemorizedSpells => _memorizedSpells;
    public IReadOnlyList<SpellDefinition?> QuickSpells => _quickSpells;
    public IReadOnlyList<ActiveSpellEffect> ActiveSpellEffects => _activeSpellEffects;
    public long InventoryRevision { get; private set; }
    public int ExplorationStepsTowardSpellAction => _explorationStepsTowardSpellAction;
    public bool IsSpellcaster => SpellcastingRules.TryGetSchool(CharacterClass.Id, out _);
    public bool CanCastSpells => IsAlive && SpellcastingRules.HasRequiredFocus(this);
    public bool HasClassFeatureUpgrade(string id) => _classFeatureUpgrades.Any(upgrade =>
        string.Equals(upgrade.Id, id, StringComparison.OrdinalIgnoreCase));

    public bool ChooseClassFeatureUpgrade(string id)
    {
        var upgrade = global::MazeGame.Domain.Characters.ClassFeatureUpgrades.Find(id);
        if (upgrade is null || upgrade.CharacterClassId != CharacterClass.Id ||
            _classFeatureUpgrades.Count >= 2 || HasClassFeatureUpgrade(id)) return false;
        _classFeatureUpgrades.Add(upgrade);
        return true;
    }

    public bool TryIncreaseAbility(string abilityId)
    {
        var increased = abilityId switch
        {
            "STR" when Abilities.Strength < 13 => Abilities with { Strength = Abilities.Strength + 1 },
            "DEX" when Abilities.Dexterity < 13 => Abilities with { Dexterity = Abilities.Dexterity + 1 },
            "HEA" when Abilities.Health < 13 => Abilities with { Health = Abilities.Health + 1 },
            "INT" when Abilities.Intelligence < 13 => Abilities with { Intelligence = Abilities.Intelligence + 1 },
            _ => (PrimaryAbilities?)null
        };
        if (increased is null) return false;
        Abilities = increased.Value;
        AbilityIncreasesClaimed++;
        return true;
    }

    public void RestoreAbilityIncreasesClaimed(int claimed) => AbilityIncreasesClaimed = Math.Max(0, claimed);
    public void ClaimUnspendableAbilityIncrease() => AbilityIncreasesClaimed++;
    public void ApplyAbilityResourceIncrease(int vitality, int mana)
    {
        var vitalityIncrease = Math.Max(0, vitality);
        var manaIncrease = UsesMana ? Math.Max(0, mana) : 0;
        _maximumVitality += vitalityIncrease;
        CurrentVitality = Math.Min(MaximumVitality, CurrentVitality + vitalityIncrease);
        _maximumMana += manaIncrease;
        CurrentMana = Math.Min(MaximumMana, CurrentMana + manaIncrease);
    }

    public void ReadyKnightRetaliation() => KnightRetaliationReady = true;
    public bool ConsumeKnightRetaliation()
    {
        var ready = KnightRetaliationReady;
        KnightRetaliationReady = false;
        return ready;
    }
    public void RestoreKnightRetaliation(bool ready) => KnightRetaliationReady = ready;
    public int MemorizationCapacity => SpellcastingRules.MemorizationCapacity(this);
    public const int MaximumMagicItemCount = 3;
    public const int MaximumBackpackItemCount = 10;
    public const int MaximumQuickSpellCount = 8;

    public void SetNpcBehavior(NpcBehavior? behavior) => NpcBehavior = behavior;

    public bool LearnSpell(SpellDefinition spell)
    {
        if (!SpellcastingRules.TryGetSchool(CharacterClass.Id, out var school) || spell.School != school ||
            spell.Level > SpellcastingRules.MaximumSpellLevel(Level) ||
            _knownSpells.Any(known => string.Equals(known.Id, spell.Id, StringComparison.OrdinalIgnoreCase))) return false;
        _knownSpells.Add(spell);
        return true;
    }

    public bool SetMemorizedSpells(IEnumerable<SpellDefinition> spells)
    {
        var selected = spells.DistinctBy(spell => spell.Id, StringComparer.OrdinalIgnoreCase).ToList();
        if (selected.Count > MemorizationCapacity || selected.Any(spell =>
                _knownSpells.All(known => !string.Equals(known.Id, spell.Id, StringComparison.OrdinalIgnoreCase)))) return false;
        _memorizedSpells.Clear();
        _memorizedSpells.AddRange(selected);
        for (var index = 0; index < _quickSpells.Length; index++)
            if (_quickSpells[index] is { } assigned &&
                selected.All(spell => !string.Equals(spell.Id, assigned.Id, StringComparison.OrdinalIgnoreCase)))
                _quickSpells[index] = null;
        foreach (var spell in selected.Where(spell => _quickSpells.All(assigned =>
                     !string.Equals(assigned?.Id, spell.Id, StringComparison.OrdinalIgnoreCase))))
        {
            var empty = Array.FindIndex(_quickSpells, assigned => assigned is null);
            if (empty < 0) break;
            _quickSpells[empty] = spell;
        }
        return true;
    }

    public bool AssignQuickSpell(int slotIndex, SpellDefinition? spell)
    {
        if (slotIndex < 0 || slotIndex >= MaximumQuickSpellCount) return false;
        if (spell is not null && _memorizedSpells.All(memorized =>
                !string.Equals(memorized.Id, spell.Id, StringComparison.OrdinalIgnoreCase))) return false;
        if (spell is not null)
            for (var index = 0; index < _quickSpells.Length; index++)
                if (index != slotIndex && string.Equals(_quickSpells[index]?.Id, spell.Id, StringComparison.OrdinalIgnoreCase))
                    _quickSpells[index] = null;
        _quickSpells[slotIndex] = spell;
        return true;
    }

    public void ApplySpellEffect(ActiveSpellEffect effect)
    {
        _activeSpellEffects.RemoveAll(existing => existing.Type == effect.Type &&
            string.Equals(existing.SourceSpellId, effect.SourceSpellId, StringComparison.OrdinalIgnoreCase));
        _activeSpellEffects.Add(effect);
    }

    public void RestoreSpellEffect(ActiveSpellEffect effect) => ApplySpellEffect(effect);
    public bool HasSpellEffect(ActiveSpellEffectType type) => _activeSpellEffects.Any(effect => effect.Type == type);
    public int SpellEffectValue(ActiveSpellEffectType type) => _activeSpellEffects
        .Where(effect => effect.Type == type).Sum(effect => effect.Value);
    public int RemoveSpellEffects(Func<ActiveSpellEffect, bool>? predicate = null) =>
        _activeSpellEffects.RemoveAll(effect => predicate?.Invoke(effect) ?? true);
    public ActiveSpellEffect? TakeSpellEffect(ActiveSpellEffectType type)
    {
        var index = _activeSpellEffects.FindIndex(effect => effect.Type == type);
        if (index < 0) return null;
        var effect = _activeSpellEffects[index];
        _activeSpellEffects.RemoveAt(index);
        return effect;
    }
    public void BreakInvisibility() => _activeSpellEffects.RemoveAll(effect => effect.Type == ActiveSpellEffectType.Invisibility);
    public void BreakSanctuary() => _activeSpellEffects.RemoveAll(effect => effect.Type == ActiveSpellEffectType.Sanctuary);
    public void AdvanceSpellEffects()
    {
        for (var index = _activeSpellEffects.Count - 1; index >= 0; index--)
        {
            var effect = _activeSpellEffects[index];
            if (effect.RemainingActions <= 0) continue;
            var remaining = effect.RemainingActions - 1;
            if (remaining == 0) _activeSpellEffects.RemoveAt(index);
            else _activeSpellEffects[index] = effect with { RemainingActions = remaining };
        }
    }

    public void RegisterExplorationStep()
    {
        _explorationStepsTowardSpellAction++;
        if (_explorationStepsTowardSpellAction < 10) return;
        _explorationStepsTowardSpellAction = 0;
        AdvanceSpellEffects();
    }

    public void RestoreExplorationStepsTowardSpellAction(int steps) =>
        _explorationStepsTowardSpellAction = Math.Clamp(steps, 0, 9);

    public void ClearTemporarySpellEffects() => _activeSpellEffects.Clear();

    public bool NextDivineSpellTriggersJudgment(SpellDefinition spell) =>
        spell.School == SpellSchool.Divine && HasPerk(PerkIds.PriestDivineJudgment) && _divineSpellCycle == 4;

    public bool RecordDivineSpellCast(SpellDefinition spell)
    {
        if (spell.School != SpellSchool.Divine || !HasPerk(PerkIds.PriestDivineJudgment)) return false;
        var empowered = _divineSpellCycle == 4;
        _divineSpellCycle = (_divineSpellCycle + 1) % 5;
        return empowered;
    }

    public void RestoreDivineSpellCycle(int cycle) => _divineSpellCycle = Math.Clamp(cycle, 0, 4);
    public void MarkResurrectedThisLevel() => WasResurrectedThisLevel = true;
    public void ResetLevelResurrection() => WasResurrectedThisLevel = false;
    public void RestoreLevelResurrection(bool used) => WasResurrectedThisLevel = used;
    public void ResetLevelRelentless() => WasRelentlessUsedThisLevel = false;
    public void MarkRelentlessUsedThisLevel() => WasRelentlessUsedThisLevel = true;
    public void RestoreLevelRelentless(bool used) => WasRelentlessUsedThisLevel = used;

    public bool ChooseSpecialization(string specializationId)
    {
        if (SpecializationId is not null) return false;
        var specialization = ClassSpecializations.Find(specializationId);
        if (specialization is null || !string.Equals(specialization.CharacterClassId, CharacterClass.Id,
                StringComparison.OrdinalIgnoreCase)) return false;
        SpecializationId = specialization.Id;
        return true;
    }

    public void RestoreSpecialization(string? specializationId)
    {
        if (specializationId is null) return;
        if (!ChooseSpecialization(specializationId))
            throw new InvalidDataException($"Érvénytelen osztályspecializáció: {specializationId}.");
    }

    public bool EquipWeapon(int slotIndex, WeaponDefinition? weapon) =>
        SetInventoryItem(InventorySlotKind.Weapon, slotIndex, weapon);

    public bool EquipArmor(ArmorDefinition? armor) => SetInventoryItem(InventorySlotKind.Armor, 0, armor);

    public bool AddMagicItem(MagicItemDefinition item)
    {
        var index = Array.FindIndex(_magicItems, existing => existing is null);
        if (index < 0) return false;
        return SetInventoryItem(InventorySlotKind.MagicItem, index, item);
    }

    public bool AddToBackpack(IItemDefinition item)
    {
        var index = Array.FindIndex(_backpack, existing => existing is null);
        if (index < 0) return false;
        return SetInventoryItem(InventorySlotKind.Backpack, index, item);
    }

    public bool RemoveFromBackpack(string itemId)
    {
        var index = Array.FindIndex(_backpack, item => item is not null && string.Equals(item.Id, itemId, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;
        return SetInventoryItem(InventorySlotKind.Backpack, index, null);
    }

    public IItemDefinition? GetInventoryItem(InventorySlotKind kind, int index) => kind switch
    {
        InventorySlotKind.Weapon when index is >= 0 and < 2 => _weaponSlots[index],
        InventorySlotKind.Armor when index == 0 => Armor,
        InventorySlotKind.MagicItem when index is >= 0 and < MaximumMagicItemCount => _magicItems[index],
        InventorySlotKind.Backpack when index is >= 0 and < MaximumBackpackItemCount => _backpack[index],
        _ => null
    };

    public int GetInventoryItemCharges(InventorySlotKind kind, int index) => kind switch
    {
        InventorySlotKind.MagicItem when index is >= 0 and < MaximumMagicItemCount => _magicItemCharges[index],
        InventorySlotKind.Backpack when index is >= 0 and < MaximumBackpackItemCount => _backpackItemCharges[index],
        _ => 0
    };

    public static bool CanPlaceInventoryItem(InventorySlotKind kind, IItemDefinition item) => kind switch
    {
        InventorySlotKind.Weapon => item.Category == ItemCategory.Weapon,
        InventorySlotKind.Armor => item.Category == ItemCategory.Armor,
        InventorySlotKind.MagicItem => item.Category == ItemCategory.MagicItem,
        InventorySlotKind.Backpack => true,
        _ => false
    };

    public bool CanApplyInventoryChanges(params InventorySlotChange[] changes)
    {
        var weapons = (WeaponDefinition?[])_weaponSlots.Clone();
        var armor = Armor;
        var magicItems = (MagicItemDefinition?[])_magicItems.Clone();
        foreach (var change in changes)
        {
            if (!IsValidSpellcastingFocusChange(change)) return false;
            if (change.Item is not null && !CanPlaceInventoryItem(change.Kind, change.Item)) return false;
            switch (change.Kind)
            {
                case InventorySlotKind.Weapon when change.Index is >= 0 and < 2:
                    weapons[change.Index] = (WeaponDefinition?)change.Item;
                    break;
                case InventorySlotKind.Armor when change.Index == 0:
                    armor = (ArmorDefinition?)change.Item;
                    break;
                case InventorySlotKind.MagicItem when change.Index is >= 0 and < MaximumMagicItemCount:
                    magicItems[change.Index] = (MagicItemDefinition?)change.Item;
                    break;
                case InventorySlotKind.Backpack when change.Index is >= 0 and < MaximumBackpackItemCount:
                    break;
                default:
                    return false;
            }
        }

        if (weapons.Any(weapon => weapon is not null &&
                !weapon.CanBeEquippedBy(CharacterClass.Id, Abilities.Strength))) return false;
        if (armor is not null && !armor.CanBeEquippedBy(CharacterClass.Id)) return false;
        if (magicItems.Any(item => item is not null && !item.CanBeEquippedBy(CharacterClass.Id))) return false;
        if (weapons[1]?.IsTwoHanded == true) return false;
        return weapons[0]?.IsTwoHanded != true || weapons[1] is null;
    }

    private bool IsValidSpellcastingFocusChange(InventorySlotChange change)
    {
        var existing = GetInventoryItem(change.Kind, change.Index);
        if (SpellcastingRules.IsSpellcastingFocus(existing) &&
            !string.Equals(existing!.Id, change.Item?.Id, StringComparison.OrdinalIgnoreCase)) return false;
        if (!SpellcastingRules.IsSpellcastingFocus(change.Item)) return true;
        return change.Kind == InventorySlotKind.Backpack && change.Index == 0 &&
            string.Equals(SpellcastingRules.RequiredFocusItemId(CharacterClass.Id), change.Item!.Id,
                StringComparison.OrdinalIgnoreCase);
    }

    public bool SetInventoryItem(InventorySlotKind kind, int index, IItemDefinition? item)
    {
        var change = new InventorySlotChange(kind, index, item);
        if (!CanApplyInventoryChanges(change)) return false;
        ApplyInventoryChangesUnchecked(change);
        InventoryRevision++;
        return true;
    }

    public void ApplyInventoryChanges(params InventorySlotChange[] changes)
    {
        if (!CanApplyInventoryChanges(changes)) throw new InvalidOperationException("A felszerelésváltozás nem engedélyezett.");
        foreach (var change in changes) ApplyInventoryChangesUnchecked(change);
        if (changes.Length > 0) InventoryRevision++;
    }

    private void ApplyInventoryChangesUnchecked(InventorySlotChange change)
    {
        var (kind, index, item, _) = change;
        switch (kind)
        {
            case InventorySlotKind.Weapon when index is >= 0 and < 2:
                _weaponSlots[index] = (WeaponDefinition?)item;
                break;
            case InventorySlotKind.Armor when index == 0:
                Armor = (ArmorDefinition?)item;
                break;
            case InventorySlotKind.MagicItem when index is >= 0 and < MaximumMagicItemCount:
                _magicItems[index] = (MagicItemDefinition?)item;
                _magicItemCharges[index] = InitialCharges(item, change.Charges);
                break;
            case InventorySlotKind.Backpack when index is >= 0 and < MaximumBackpackItemCount:
                _backpack[index] = item;
                _backpackItemCharges[index] = InitialCharges(item, change.Charges);
                break;
        }
    }

    private static int InitialCharges(IItemDefinition? item, int? charges) => item is MagicItemDefinition magic &&
        magic.Kind is MagicItemKind.Wand or MagicItemKind.Scroll
            ? Math.Clamp(charges ?? magic.MaximumCharges, 0, magic.MaximumCharges)
            : 0;

    public bool ConsumeMagicItemCharge(int slotIndex)
    {
        if (slotIndex is < 0 or >= MaximumMagicItemCount || _magicItems[slotIndex] is not { } item ||
            item.Kind is not (MagicItemKind.Wand or MagicItemKind.Scroll) || _magicItemCharges[slotIndex] <= 0) return false;
        _magicItemCharges[slotIndex]--;
        if (item.Kind == MagicItemKind.Scroll)
            ApplyInventoryChangesUnchecked(new InventorySlotChange(InventorySlotKind.MagicItem, slotIndex, null));
        InventoryRevision++;
        return true;
    }

    public bool AddPerk(PerkDefinition perk)
    {
        if (!string.Equals(perk.CharacterClassId, CharacterClass.Id, StringComparison.OrdinalIgnoreCase) ||
            _perks.Any(existing => existing.Tier == perk.Tier)) return false;
        _perks.Add(perk);
        return true;
    }

    public bool HasPerk(string perkId) => _perks.Any(perk =>
        string.Equals(perk.Id, perkId, StringComparison.OrdinalIgnoreCase));

    public int GetMagicItemBonus(MagicItemEffect effect) => _magicItems
        .Where(item => item?.Effect == effect)
        .Sum(item => item!.EffectValue);

    /// <summary>Egyszer, közvetlenül a tehetség kiválasztásakor alkalmazandó erőforrásbónusz.</summary>
    public void ApplyPerkAcquisitionBonus(PerkDefinition perk)
    {
        var vitality = perk.Id switch
        {
            PerkIds.FighterRobustness => 10,
            PerkIds.BarbarianThickSkin => 8,
            PerkIds.BarbarianPrimalStrength => 20,
            PerkIds.KnightInvincible => 15,
            _ => 0
        };
        var mana = perk.Id switch
        {
            PerkIds.PriestFaithSource => 12,
            PerkIds.MageManaReserve => 15,
            PerkIds.MageArchmage => 25,
            _ => 0
        };
        _maximumVitality += vitality;
        CurrentVitality += vitality;
        if (UsesMana)
        {
            _maximumMana += mana;
            CurrentMana += mana;
        }
        CurrentVitality = Math.Min(CurrentVitality, MaximumVitality);
        CurrentMana = Math.Min(CurrentMana, MaximumMana);
    }

    public bool AddStatus(StatusDefinition status)
    {
        var existing = _statuses.FirstOrDefault(candidate => string.Equals(candidate.Id, status.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is null) _statuses.Add(status);
        _statusDurations[status.Id] = status.DefaultDuration;
        CurrentVitality = Math.Min(CurrentVitality, MaximumVitality);
        CurrentMana = Math.Min(CurrentMana, MaximumMana);
        return true;
    }

    public bool RemoveStatus(string statusId)
    {
        _statusDurations.Remove(statusId);
        return _statuses.RemoveAll(status => string.Equals(status.Id, statusId, StringComparison.OrdinalIgnoreCase)) > 0;
    }

    public bool HasStatus(string statusId) => _statuses.Any(status =>
        string.Equals(status.Id, statusId, StringComparison.OrdinalIgnoreCase));

    public int? GetStatusDuration(string statusId) => _statusDurations.GetValueOrDefault(statusId);

    public void RestoreStatus(StatusDefinition status, int? remainingActivations)
    {
        AddStatus(status);
        _statusDurations[status.Id] = remainingActivations ?? status.DefaultDuration;
    }

    public IReadOnlyList<StatusTickResult> ApplyTurnEndStatusEffects(Random random)
    {
        var results = new List<StatusTickResult>();
        foreach (var status in _statuses.Where(status => status.PeriodicDamageMaximum > 0).ToList())
        {
            var damage = random.Next(status.PeriodicDamageMinimum, status.PeriodicDamageMaximum + 1);
            ReceiveDamage(damage);
            var remaining = _statusDurations.GetValueOrDefault(status.Id);
            var expired = remaining is > 0 && remaining.Value - 1 <= 0;
            if (remaining is > 0) _statusDurations[status.Id] = remaining.Value - 1;
            if (expired) RemoveStatus(status.Id);
            results.Add(new StatusTickResult(status.Name, status.Icon, damage, expired));
        }
        return results;
    }

    public BattleStartStatusResult ApplyBattleStartStatusEffects()
    {
        var vitalityLoss = 0;
        var manaLoss = 0;
        foreach (var status in _statuses)
        {
            var multiplier = NeedIsZero(status) ? status.ZeroNeedMultiplier : 1;
            if (status.BattleStartVitalityLossPercent > 0 &&
                (!string.Equals(status.Id, CharacterStatusIds.Hungry, StringComparison.OrdinalIgnoreCase) || FoodLevel == 0))
                vitalityLoss += Math.Max(1, MaximumVitality * status.BattleStartVitalityLossPercent * multiplier / 100);
            if (UsesMana && status.BattleStartManaLossPercent > 0)
                manaLoss += Math.Max(1, MaximumMana * status.BattleStartManaLossPercent * multiplier / 100);
        }
        vitalityLoss = Math.Min(CurrentVitality, vitalityLoss);
        manaLoss = Math.Min(CurrentMana, manaLoss);
        ReceiveDamage(vitalityLoss);
        SpendMana(manaLoss);
        return new BattleStartStatusResult(vitalityLoss, manaLoss);
    }

    public int StatusInitiativePenalty => StatusPenalty(status => status.InitiativePenalty);
    public int StatusHitPenalty => StatusPenalty(status => status.HitPenalty);
    public int StatusPhysicalDamagePenalty => StatusPenalty(status => status.PhysicalDamagePenalty);

    private int StatusPenalty(Func<StatusDefinition, int> selector) => _statuses.Sum(status =>
        selector(status) * (NeedIsZero(status) ? status.ZeroNeedMultiplier : 1));

    private bool NeedIsZero(StatusDefinition status) =>
        string.Equals(status.Id, CharacterStatusIds.Hungry, StringComparison.OrdinalIgnoreCase) ? FoodLevel == 0 :
        string.Equals(status.Id, CharacterStatusIds.Thirsty, StringComparison.OrdinalIgnoreCase) && WaterLevel == 0;

    private int ApplyMaximumResourceModifier(int baseValue, Func<StatusDefinition, int> selector)
    {
        var result = baseValue;
        foreach (var percentage in _statuses.Select(selector).Where(value => value != 100))
            result = result * Math.Max(0, percentage) / 100;
        return Math.Max(baseValue > 0 ? 1 : 0, result);
    }

    public void SynchronizeNeedStatuses(StatusDefinition hungry, StatusDefinition thirsty)
    {
        SetStatusActive(hungry, FoodLevel <= 30);
        SetStatusActive(thirsty, WaterLevel <= 30);
    }

    private void SetStatusActive(StatusDefinition status, bool active)
    {
        if (active) AddStatus(status);
        else RemoveStatus(status.Id);
    }

    public void ReceiveDamage(int amount) => CurrentVitality = Math.Max(0, CurrentVitality - Math.Max(0, amount));
    public void RestoreVitality(int amount)
    {
        var adjusted = PreviewVitalityRecovery(amount);
        CurrentVitality = Math.Min(MaximumVitality, CurrentVitality + adjusted);
    }
    public int PreviewVitalityRecovery(int amount) =>
        ApplyRecoveryModifier(amount, status => status.VitalityRecoveryPercent);
    public bool SpendMana(int amount)
    {
        if (amount < 0 || CurrentMana < amount) return false;
        CurrentMana -= amount;
        return true;
    }
    public void RestoreMana(int amount)
    {
        var adjusted = ApplyRecoveryModifier(amount, status => status.ManaRecoveryPercent);
        CurrentMana = Math.Min(MaximumMana, CurrentMana + adjusted);
    }

    private int ApplyRecoveryModifier(int amount, Func<StatusDefinition, int> selector)
    {
        var adjusted = Math.Max(0, amount);
        foreach (var percentage in _statuses.Select(selector).Where(value => value != 100))
            adjusted = adjusted * Math.Max(0, percentage) / 100;
        return amount > 0 && adjusted == 0 ? 1 : adjusted;
    }

    public void ConsumeFood(int amount) => FoodLevel = Math.Max(0, FoodLevel - Math.Max(0, amount));
    public void ConsumeWater(int amount) => WaterLevel = Math.Max(0, WaterLevel - Math.Max(0, amount));
    public void RestoreFood(int amount) => FoodLevel = Math.Min(100, FoodLevel + Math.Max(0, amount));
    public void RestoreWater(int amount) => WaterLevel = Math.Min(100, WaterLevel + Math.Max(0, amount));
    public void AddGold(int amount) => Gold += Math.Max(0, amount);
    public bool SpendGold(int amount)
    {
        if (amount < 0 || Gold < amount) return false;
        Gold -= amount;
        return true;
    }
    public void SetGold(int gold) => Gold = Math.Max(0, gold);

    public LevelUpResult AddExperience(int amount, IReadOnlyDictionary<int, int> experienceByLevel,
        ValueRange vitalityGrowth, ValueRange manaGrowth, Random random)
    {
        var awardedExperience = Math.Max(0, amount);
        Experience += awardedExperience;
        var previousLevel = Level;
        var bonuses = new List<LevelUpBonus>();
        while (experienceByLevel.ContainsKey(Level + 1) && Experience >= GetRequiredExperience(Level + 1, experienceByLevel))
        {
            Level++;
            var vitality = random.Next(vitalityGrowth.Minimum, vitalityGrowth.Maximum + 1);
            var mana = UsesMana
                ? CharacterClassRules.AdjustManaGrowth(CharacterClass.Id,
                    random.Next(manaGrowth.Minimum, manaGrowth.Maximum + 1))
                : 0;
            _maximumVitality += vitality;
            CurrentVitality += vitality;
            _maximumMana += mana;
            CurrentMana += mana;
            bonuses.Add(new LevelUpBonus(Level, vitality, mana));
        }
        CurrentVitality = Math.Min(CurrentVitality, MaximumVitality);
        CurrentMana = Math.Min(CurrentMana, MaximumMana);
        return new LevelUpResult(awardedExperience, previousLevel, Level, bonuses);
    }

    public int? GetNextLevelExperience(IReadOnlyDictionary<int, int> experienceByLevel) =>
        experienceByLevel.ContainsKey(Level + 1) ? GetRequiredExperience(Level + 1, experienceByLevel) : null;

    public int GetExperienceNeededForNextLevel(IReadOnlyDictionary<int, int> experienceByLevel) =>
        Math.Max(0, (GetNextLevelExperience(experienceByLevel) ?? Experience) - Experience);

    public void SetProgress(int level, int experience)
    {
        Level = Math.Max(1, level);
        Experience = Math.Max(0, experience);
    }

    private int GetRequiredExperience(int targetLevel, IReadOnlyDictionary<int, int> experienceByLevel) =>
        (int)Math.Ceiling(experienceByLevel[targetLevel] * CharacterClass.ExperienceModifier);

    public void SetNeedLevels(int foodLevel, int waterLevel)
    {
        FoodLevel = Math.Clamp(foodLevel, 0, 100);
        WaterLevel = Math.Clamp(waterLevel, 0, 100);
    }

    public void SetCurrentResources(int vitality, int mana)
    {
        CurrentVitality = Math.Clamp(vitality, 0, MaximumVitality);
        CurrentMana = Math.Clamp(mana, 0, MaximumMana);
    }

    public void ApplySavedLevelGrowth(int vitalityIncrease, int manaIncrease)
    {
        _maximumVitality += Math.Max(0, vitalityIncrease);
        _maximumMana += UsesMana ? Math.Max(0, manaIncrease) : 0;
    }
}

public readonly record struct InventorySlotChange(InventorySlotKind Kind, int Index, IItemDefinition? Item, int? Charges = null);

public sealed record LevelUpBonus(int Level, int Vitality, int Mana);

public sealed record LevelUpResult(int GainedExperience, int PreviousLevel, int CurrentLevel, IReadOnlyList<LevelUpBonus> Bonuses)
{
    public bool LeveledUp => CurrentLevel > PreviousLevel;
    public int VitalityGained => Bonuses.Sum(bonus => bonus.Vitality);
    public int ManaGained => Bonuses.Sum(bonus => bonus.Mana);
}
