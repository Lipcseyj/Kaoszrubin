namespace MazeGame.Domain.Characters;

/// <summary>Egy játék közben létező karakter saját, konkrét értékekkel.</summary>
public sealed class LiveCharacter
{
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
