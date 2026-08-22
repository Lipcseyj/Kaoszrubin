using MazeGame.Domain.Combat;
using MazeGame.Domain.Inventory;
using MazeGame.Domain.Magic;

namespace MazeGame.Domain.Characters;

/// <summary>Egy játék közben létező karakter saját, konkrét értékekkel.</summary>
public sealed class LiveCharacter
{
    public const int MaximumNameLength = 13;
    private readonly WeaponDefinition?[] _weaponSlots = new WeaponDefinition?[2];
    private readonly List<MagicItemDefinition> _magicItems = [];
    private readonly List<IItemDefinition> _backpack = [];
    private readonly List<PerkDefinition> _perks = [];
    private readonly List<StatusDefinition> _statuses = [];
    public LiveCharacter(string name, RaceDefinition race, CharacterClassDefinition characterClass, PrimaryAbilities abilities, int maximumVitality, int maximumMana, int vitalityBonus, int manaBonus, ConsoleColor color = ConsoleColor.Cyan)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaximumNameLength)
            throw new ArgumentException($"A karakternév 1 és {MaximumNameLength} karakter közötti lehet.", nameof(name));
        Name = name;
        Color = color;
        Race = race;
        CharacterClass = characterClass;
        Abilities = abilities;
        MaximumVitality = maximumVitality;
        CurrentVitality = maximumVitality;
        MaximumMana = maximumMana;
        CurrentMana = maximumMana;
        VitalityBonus = vitalityBonus;
        ManaBonus = manaBonus;
    }

    public string Name { get; }
    public ConsoleColor Color { get; }
    public RaceDefinition Race { get; }
    public CharacterClassDefinition CharacterClass { get; }
    public PrimaryAbilities Abilities { get; }
    public int MaximumVitality { get; private set; }
    public int CurrentVitality { get; private set; }
    public int MaximumMana { get; private set; }
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
    public IReadOnlyList<WeaponDefinition?> WeaponSlots => _weaponSlots;
    public ArmorDefinition? Armor { get; private set; }
    public IReadOnlyList<MagicItemDefinition> MagicItems => _magicItems;
    public IReadOnlyList<IItemDefinition> Backpack => _backpack;
    public IReadOnlyList<PerkDefinition> Perks => _perks;
    public IReadOnlyList<StatusDefinition> Statuses => _statuses;
    public const int MaximumMagicItemCount = 3;
    public const int MaximumBackpackItemCount = 10;

    public void EquipWeapon(int slotIndex, WeaponDefinition? weapon)
    {
        if (slotIndex is < 0 or >= 2) throw new ArgumentOutOfRangeException(nameof(slotIndex));
        _weaponSlots[slotIndex] = weapon;
    }

    public void EquipArmor(ArmorDefinition? armor) => Armor = armor;

    public bool AddMagicItem(MagicItemDefinition item)
    {
        if (_magicItems.Count >= MaximumMagicItemCount) return false;
        _magicItems.Add(item);
        return true;
    }

    public bool AddToBackpack(IItemDefinition item)
    {
        if (_backpack.Count >= MaximumBackpackItemCount) return false;
        _backpack.Add(item);
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
        MaximumVitality += vitality;
        CurrentVitality += vitality;
        if (UsesMana)
        {
            MaximumMana += mana;
            CurrentMana += mana;
        }
    }

    public bool AddStatus(StatusDefinition status)
    {
        if (_statuses.Any(existing => string.Equals(existing.Id, status.Id, StringComparison.OrdinalIgnoreCase))) return false;
        _statuses.Add(status);
        return true;
    }

    public bool RemoveStatus(string statusId) => _statuses.RemoveAll(status =>
        string.Equals(status.Id, statusId, StringComparison.OrdinalIgnoreCase)) > 0;

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
    public void RestoreVitality(int amount) => CurrentVitality = Math.Min(MaximumVitality, CurrentVitality + Math.Max(0, amount));
    public bool SpendMana(int amount)
    {
        if (amount < 0 || CurrentMana < amount) return false;
        CurrentMana -= amount;
        return true;
    }
    public void RestoreMana(int amount) => CurrentMana = Math.Min(MaximumMana, CurrentMana + Math.Max(0, amount));

    public void ConsumeFood(int amount) => FoodLevel = Math.Max(0, FoodLevel - Math.Max(0, amount));
    public void ConsumeWater(int amount) => WaterLevel = Math.Max(0, WaterLevel - Math.Max(0, amount));
    public void AddGold(int amount) => Gold += Math.Max(0, amount);
    public void SetGold(int gold) => Gold = Math.Max(0, gold);

    public LevelUpResult AddExperience(int amount, IReadOnlyDictionary<int, int> experienceByLevel,
        ValueRange vitalityGrowth, ValueRange manaGrowth, Random random)
    {
        Experience += Math.Max(0, amount);
        var previousLevel = Level;
        var bonuses = new List<LevelUpBonus>();
        while (experienceByLevel.ContainsKey(Level + 1) && Experience >= GetRequiredExperience(Level + 1, experienceByLevel))
        {
            Level++;
            var vitality = random.Next(vitalityGrowth.Minimum, vitalityGrowth.Maximum + 1);
            var mana = UsesMana ? random.Next(manaGrowth.Minimum, manaGrowth.Maximum + 1) : 0;
            MaximumVitality += vitality;
            CurrentVitality += vitality;
            MaximumMana += mana;
            CurrentMana += mana;
            bonuses.Add(new LevelUpBonus(Level, vitality, mana));
        }
        return new LevelUpResult(amount, previousLevel, Level, bonuses);
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
        MaximumVitality += Math.Max(0, vitalityIncrease);
        MaximumMana += UsesMana ? Math.Max(0, manaIncrease) : 0;
    }
}

public sealed record LevelUpBonus(int Level, int Vitality, int Mana);

public sealed record LevelUpResult(int GainedExperience, int PreviousLevel, int CurrentLevel, IReadOnlyList<LevelUpBonus> Bonuses)
{
    public bool LeveledUp => CurrentLevel > PreviousLevel;
    public int VitalityGained => Bonuses.Sum(bonus => bonus.Vitality);
    public int ManaGained => Bonuses.Sum(bonus => bonus.Mana);
}
