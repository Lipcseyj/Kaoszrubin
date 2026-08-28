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
    public IReadOnlyList<MonsterAbilityDefinition> MonsterAbilities { get; init; } = [];
    public IReadOnlyList<StrengthHitBonusDefinition> StrengthHitBonuses { get; init; } = [];
    public IReadOnlyList<MonsterLootDefinition> MonsterLoot { get; init; } = [];
    public LootRules LootRules { get; init; } = new(10, 40, 10, 130, 1, 10, 3);
    public DoorAttemptRules DoorAttemptRules { get; init; } = new(1, 2, 1, 2);
    public IReadOnlyList<WeaponTypeDefinition> WeaponTypes { get; init; } = [];
    public IReadOnlyList<WeaponDefinition> Weapons { get; init; } = [];
    public IReadOnlyList<ArmorDefinition> Armors { get; init; } = [];
    public IReadOnlyList<AbilityDefinition> Abilities { get; init; } = [];
    public IReadOnlyList<MiscItemDefinition> Items { get; init; } = [];
    public IReadOnlyList<MagicItemDefinition> MagicItems { get; init; } = [];
    public IReadOnlyList<SpellDefinition> Spells { get; init; } = [];
    public IReadOnlyList<SpellEffectDefinition> SpellEffects { get; init; } = [];
    public IReadOnlyList<PerkDefinition> Perks { get; init; } = [];
    public IReadOnlyList<StatusDefinition> Statuses { get; init; } = [];
    public IReadOnlyList<CharacterNameDefinition> CharacterNames { get; init; } = [];
    public IReadOnlyList<string> InnNames { get; init; } = [];
    public IReadOnlyList<InnRumorDefinition> InnRumors { get; init; } = [];
    public IReadOnlyList<TrapDefinition> Traps { get; init; } = [];
    public IReadOnlyDictionary<string, StartingEquipmentDefinition> StartingEquipmentByClass { get; init; } = new Dictionary<string, StartingEquipmentDefinition>();
    public IReadOnlyDictionary<int, int> MinimumVitalityByHealth { get; init; } = new Dictionary<int, int>();
    public IReadOnlyDictionary<int, int> MinimumManaByIntelligence { get; init; } = new Dictionary<int, int>();
    public IReadOnlyDictionary<int, int> ExperienceByLevel { get; init; } = new Dictionary<int, int>();
    public IReadOnlyDictionary<int, ValueRange> VitalityGrowthByHealth { get; init; } = new Dictionary<int, ValueRange>();
    public IReadOnlyDictionary<int, ValueRange> ManaGrowthByIntelligence { get; init; } = new Dictionary<int, ValueRange>();
    public int BaseLevelCompletionExperience { get; init; }

    public EnemyDefinition GetEnemy(string id) => FindById(Enemies, id, "ellenfél");
    public MonsterAbilityDefinition GetMonsterAbility(string id) => FindById(MonsterAbilities, id, "szörnyképesség");
    public MonsterLootDefinition? GetMonsterLoot(string enemyId) => MonsterLoot.FirstOrDefault(loot =>
        string.Equals(loot.EnemyId, enemyId, StringComparison.OrdinalIgnoreCase));
    public WeaponTypeDefinition GetWeaponType(string id) => FindById(WeaponTypes, id, "fegyvertípus");
    public RaceDefinition GetRace(string id) => FindById(Races, id, "faj");
    public CharacterClassDefinition GetCharacterClass(string id) => FindById(CharacterClasses, id, "osztály");
    public WeaponDefinition GetWeapon(string id) => FindById(Weapons, id, "fegyver");
    public ArmorDefinition GetArmor(string id) => FindById(Armors, id, "páncél");
    public MagicItemDefinition GetMagicItem(string id) => FindById(MagicItems, id, "varázstárgy");
    public SpellDefinition GetSpell(string id) => FindById(Spells, id, "varázslat");
    public IReadOnlyList<SpellDefinition> GetSpells(SpellSchool school, int level) => Spells
        .Where(spell => spell.School == school && spell.Level == level).ToList();
    public IReadOnlyList<SpellEffectDefinition> GetSpellEffects(string spellId) => SpellEffects
        .Where(effect => string.Equals(effect.SpellId, spellId, StringComparison.OrdinalIgnoreCase))
        .OrderBy(effect => effect.Order).ToList();
    public MiscItemDefinition GetItem(string id) => FindById(Items, id, "tárgy");
    public PerkDefinition GetPerk(string id) => FindById(Perks, id, "tehetség");
    public StatusDefinition GetStatus(string id) => FindById(Statuses, id, "állapot");
    public TrapDefinition GetTrap(string id) => FindById(Traps, id, "csapda");
    public IReadOnlyList<CharacterNameDefinition> GetCharacterNames(string characterClassId) => CharacterNames.Where(name =>
        string.Equals(name.CharacterClassId, characterClassId, StringComparison.OrdinalIgnoreCase)).ToList();
    public IReadOnlyList<PerkDefinition> GetPerkChoices(string characterClassId, int tier)
    {
        var choices = Perks.Where(perk =>
            string.Equals(perk.CharacterClassId, characterClassId, StringComparison.OrdinalIgnoreCase) && perk.Tier == tier).ToList();
        if (choices.Count != 2)
            throw new InvalidOperationException($"A(z) '{characterClassId}' osztály {tier}. tehetségfokozatához pontosan két tehetség szükséges az adatok.csv fájlban.");
        return choices;
    }
    public StartingEquipmentDefinition? GetStartingEquipment(string characterClassId) =>
        StartingEquipmentByClass.TryGetValue(characterClassId, out var equipment) ? equipment : null;

    public int GetMinimumVitality(int health) => GetThresholdValue(MinimumVitalityByHealth, health, "egészség");
    public int GetMinimumMana(int intelligence) => GetThresholdValue(MinimumManaByIntelligence, intelligence, "intelligencia");
    public ValueRange GetVitalityGrowth(int health) => GetRangeThresholdValue(VitalityGrowthByHealth, health, "egészség");
    public ValueRange GetManaGrowth(int intelligence) => GetRangeThresholdValue(ManaGrowthByIntelligence, intelligence, "intelligencia");

    private static int GetThresholdValue(IReadOnlyDictionary<int, int> values, int ability, string abilityName)
    {
        var matchingValue = values.Where(pair => pair.Key <= ability).OrderByDescending(pair => pair.Key).Select(pair => (int?)pair.Value).FirstOrDefault();
        return matchingValue ?? throw new InvalidOperationException($"Nincs {abilityName} értékhez tartozó minimum az adatok.csv fájlban.");
    }

    private static ValueRange GetRangeThresholdValue(IReadOnlyDictionary<int, ValueRange> values, int ability, string abilityName) =>
        values.Where(pair => pair.Key <= ability).OrderByDescending(pair => pair.Key).Select(pair => pair.Value).FirstOrDefault()
        ?? throw new InvalidOperationException($"Nincs {abilityName} értékhez tartozó szintlépési növekedés az adatok.csv fájlban.");

    private static T FindById<T>(IReadOnlyList<T> definitions, string id, string typeName) where T : IGameDefinition =>
        definitions.FirstOrDefault(definition => string.Equals(definition.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"A(z) '{id}' azonosítójú {typeName} nem található az adatok.csv fájlban.");
}
