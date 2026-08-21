using MazeGame.Domain.Combat;
using MazeGame.Domain.Inventory;
using MazeGame.Domain.Magic;

namespace MazeGame.Domain.Characters;

/// <summary>Egy játék közben létező karakter saját, konkrét értékekkel.</summary>
public sealed class LiveCharacter
{
    private readonly WeaponDefinition?[] _weaponSlots = new WeaponDefinition?[2];
    private readonly List<MagicItemDefinition> _magicItems = [];
    private readonly List<IItemDefinition> _backpack = [];
    public LiveCharacter(string name, RaceDefinition race, CharacterClassDefinition characterClass, PrimaryAbilities abilities, int maximumVitality, int maximumMana, int vitalityBonus, int manaBonus)
    {
        Name = name;
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
    public RaceDefinition Race { get; }
    public CharacterClassDefinition CharacterClass { get; }
    public PrimaryAbilities Abilities { get; }
    public int MaximumVitality { get; }
    public int CurrentVitality { get; private set; }
    public int MaximumMana { get; }
    public int CurrentMana { get; private set; }
    public int VitalityBonus { get; }
    public int ManaBonus { get; }
    public bool UsesMana => CharacterClass.UsesMana;
    public IReadOnlyList<WeaponDefinition?> WeaponSlots => _weaponSlots;
    public ArmorDefinition? Armor { get; private set; }
    public IReadOnlyList<MagicItemDefinition> MagicItems => _magicItems;
    public IReadOnlyList<IItemDefinition> Backpack => _backpack;
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

    public void ReceiveDamage(int amount) => CurrentVitality = Math.Max(0, CurrentVitality - Math.Max(0, amount));
    public void RestoreVitality(int amount) => CurrentVitality = Math.Min(MaximumVitality, CurrentVitality + Math.Max(0, amount));
    public bool SpendMana(int amount)
    {
        if (amount < 0 || CurrentMana < amount) return false;
        CurrentMana -= amount;
        return true;
    }
    public void RestoreMana(int amount) => CurrentMana = Math.Min(MaximumMana, CurrentMana + Math.Max(0, amount));
}
