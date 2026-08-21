using MazeGame.Domain;
using MazeGame.Domain.Characters;
using MazeGame.Domain.Combat;
using MazeGame.Domain.Magic;
using MazeGame.Domain.Inventory;

namespace MazeGame.Data;

/// <summary>A játék definícióinak központi, csak olvasható gyűjteménye.</summary>
public sealed class GameDataCatalog
{
    public IReadOnlyList<RaceDefinition> Races { get; init; } = [];
    public IReadOnlyList<CharacterClassDefinition> CharacterClasses { get; init; } = [];
    public IReadOnlyList<EnemyDefinition> Enemies { get; init; } = [];
    public IReadOnlyList<WeaponTypeDefinition> WeaponTypes { get; init; } = [];
    public IReadOnlyList<WeaponDefinition> Weapons { get; init; } = [];
    public IReadOnlyList<ArmorDefinition> Armors { get; init; } = [];
    public IReadOnlyList<AbilityDefinition> Abilities { get; init; } = [];
    public IReadOnlyList<MiscItemDefinition> Items { get; init; } = [];
    public IReadOnlyList<MagicItemDefinition> MagicItems { get; init; } = [];
    public IReadOnlyList<SpellDefinition> Spells { get; init; } = [];
    public IReadOnlyDictionary<string, StartingEquipmentDefinition> StartingEquipmentByClass { get; init; } = new Dictionary<string, StartingEquipmentDefinition>();
    public IReadOnlyDictionary<int, int> MinimumVitalityByHealth { get; init; } = new Dictionary<int, int>();
    public IReadOnlyDictionary<int, int> MinimumManaByIntelligence { get; init; } = new Dictionary<int, int>();

    public EnemyDefinition GetEnemy(string id) => FindById(Enemies, id, "ellenfél");
    public WeaponTypeDefinition GetWeaponType(string id) => FindById(WeaponTypes, id, "fegyvertípus");
    public RaceDefinition GetRace(string id) => FindById(Races, id, "faj");
    public CharacterClassDefinition GetCharacterClass(string id) => FindById(CharacterClasses, id, "osztály");
    public WeaponDefinition GetWeapon(string id) => FindById(Weapons, id, "fegyver");
    public ArmorDefinition GetArmor(string id) => FindById(Armors, id, "páncél");
    public MagicItemDefinition GetMagicItem(string id) => FindById(MagicItems, id, "varázstárgy");
    public MiscItemDefinition GetItem(string id) => FindById(Items, id, "tárgy");
    public StartingEquipmentDefinition? GetStartingEquipment(string characterClassId) =>
        StartingEquipmentByClass.TryGetValue(characterClassId, out var equipment) ? equipment : null;

    public int GetMinimumVitality(int health) => GetThresholdValue(MinimumVitalityByHealth, health, "egészség");
    public int GetMinimumMana(int intelligence) => GetThresholdValue(MinimumManaByIntelligence, intelligence, "intelligencia");

    private static int GetThresholdValue(IReadOnlyDictionary<int, int> values, int ability, string abilityName)
    {
        var matchingValue = values.Where(pair => pair.Key <= ability).OrderByDescending(pair => pair.Key).Select(pair => (int?)pair.Value).FirstOrDefault();
        return matchingValue ?? throw new InvalidOperationException($"Nincs {abilityName} értékhez tartozó minimum az adatok.csv fájlban.");
    }

    private static T FindById<T>(IReadOnlyList<T> definitions, string id, string typeName) where T : IGameDefinition =>
        definitions.FirstOrDefault(definition => string.Equals(definition.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"A(z) '{id}' azonosítójú {typeName} nem található az adatok.csv fájlban.");
}
