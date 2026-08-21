using MazeGame.Domain.Characters;
using MazeGame.Domain.Combat;
using MazeGame.Domain.Magic;

namespace MazeGame.Data;

/// <summary>A játék definícióinak központi, csak olvasható gyűjteménye.</summary>
public sealed class GameDataCatalog
{
    public IReadOnlyList<RaceDefinition> Races { get; init; } = [];
    public IReadOnlyList<CharacterClassDefinition> CharacterClasses { get; init; } = [];
    public IReadOnlyList<EnemyDefinition> Enemies { get; init; } = [];
    public IReadOnlyList<WeaponDefinition> Weapons { get; init; } = [];
    public IReadOnlyList<ArmorDefinition> Armors { get; init; } = [];
    public IReadOnlyList<AbilityDefinition> Abilities { get; init; } = [];
    public IReadOnlyList<MagicItemDefinition> MagicItems { get; init; } = [];
    public IReadOnlyList<SpellDefinition> Spells { get; init; } = [];
    public IReadOnlyDictionary<int, int> MinimumVitalityByHealth { get; init; } = new Dictionary<int, int>();
    public IReadOnlyDictionary<int, int> MinimumManaByIntelligence { get; init; } = new Dictionary<int, int>();

    public EnemyDefinition GetEnemy(string name) =>
        Enemies.FirstOrDefault(enemy => string.Equals(enemy.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"A(z) '{name}' nevű ellenfél nem található az adatok.csv fájlban.");

    public RaceDefinition GetRace(string name) => FindByName(Races, name, "faj");
    public CharacterClassDefinition GetCharacterClass(string name) => FindByName(CharacterClasses, name, "osztály");
    public WeaponDefinition GetWeapon(string name) => FindByName(Weapons, name, "fegyver");
    public ArmorDefinition GetArmor(string name) => FindByName(Armors, name, "páncél");
    public MagicItemDefinition GetMagicItem(string name) => FindByName(MagicItems, name, "varázstárgy");

    public int GetMinimumVitality(int health) => GetThresholdValue(MinimumVitalityByHealth, health, "egészség");
    public int GetMinimumMana(int intelligence) => GetThresholdValue(MinimumManaByIntelligence, intelligence, "intelligencia");

    private static int GetThresholdValue(IReadOnlyDictionary<int, int> values, int ability, string abilityName)
    {
        var matchingValue = values.Where(pair => pair.Key <= ability).OrderByDescending(pair => pair.Key).Select(pair => (int?)pair.Value).FirstOrDefault();
        return matchingValue ?? throw new InvalidOperationException($"Nincs {abilityName} értékhez tartozó minimum az adatok.csv fájlban.");
    }

    private static T FindByName<T>(IReadOnlyList<T> definitions, string name, string typeName) where T : class
    {
        var nameProperty = typeof(T).GetProperty("Name")!;
        return definitions.FirstOrDefault(definition => string.Equals((string?)nameProperty.GetValue(definition), name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"A(z) '{name}' nevű {typeName} nem található az adatok.csv fájlban.");
    }
}
