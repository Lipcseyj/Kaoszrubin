using System.Globalization;
using System.Text;
using MazeGame.Domain.Characters;
using MazeGame.Domain.Combat;
using MazeGame.Domain.Inventory;
using MazeGame.Domain.Magic;

namespace MazeGame.Data;

/// <summary>A szekciókra tagolt adatok.csv betöltője.</summary>
public static class CsvGameDataLoader
{
    public static GameDataCatalog Load(string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("Az adatok.csv nem található.", filePath);

        var races = new List<RaceDefinition>();
        var characterClasses = new List<CharacterClassDefinition>();
        var enemies = new List<EnemyDefinition>();
        var monsterAbilities = new List<MonsterAbilityDefinition>();
        var strengthHitBonuses = new List<StrengthHitBonusDefinition>();
        var monsterLoot = new List<MonsterLootDefinition>();
        var lootRuleValues = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var weaponTypes = new List<WeaponTypeDefinition>();
        var weapons = new List<WeaponDefinition>();
        var armors = new List<ArmorDefinition>();
        var abilities = new List<AbilityDefinition>();
        var items = new List<MiscItemDefinition>();
        var magicItems = new List<MagicItemDefinition>();
        var spells = new List<SpellDefinition>();
        var spellEffects = new List<SpellEffectDefinition>();
        var perks = new List<PerkDefinition>();
        var statuses = new List<StatusDefinition>();
        var characterNames = new List<CharacterNameDefinition>();
        var itemUpgrades = new List<ItemUpgradeDefinition>();
        var raceBonuses = new Dictionary<string, PrimaryAbilities>(StringComparer.OrdinalIgnoreCase);
        var classMinimums = new Dictionary<string, PrimaryAbilities>(StringComparer.OrdinalIgnoreCase);
        var minimumVitalityByHealth = new Dictionary<int, int>();
        var minimumManaByIntelligence = new Dictionary<int, int>();
        var experienceByLevel = new Dictionary<int, int>();
        var vitalityGrowthByHealth = new Dictionary<int, ValueRange>();
        var manaGrowthByIntelligence = new Dictionary<int, ValueRange>();
        var startingEquipmentByClass = new Dictionary<string, StartingEquipmentDefinition>(StringComparer.OrdinalIgnoreCase);
        int? baseLevelCompletionExperience = null;
        var section = DataSection.None;

        foreach (var rawLine in ReadLinesWithFallbackEncoding(filePath))
        {
            var cells = rawLine.Split(',').Select(cell => cell.Trim()).ToArray();
            if (cells.All(string.IsNullOrEmpty)) continue;

            if (TryReadSection(cells, out var parsedSection))
            {
                section = parsedSection;
                continue;
            }

            if (IsHeaderRow(cells[0])) continue;
            AddDefinition(section, cells, races, characterClasses, enemies, monsterAbilities, strengthHitBonuses,
                monsterLoot, lootRuleValues, weaponTypes, weapons, armors, abilities, items, magicItems, spells, spellEffects, perks, statuses, characterNames, itemUpgrades,
                raceBonuses, classMinimums, minimumVitalityByHealth, minimumManaByIntelligence, experienceByLevel,
                vitalityGrowthByHealth, manaGrowthByIntelligence, startingEquipmentByClass, ref baseLevelCompletionExperience);
        }

        ValidateSpells(spells);
        ValidateSpellEffects(spells, spellEffects);
        ValidateMagicItems(magicItems, spells);
        ValidateEnemies(enemies, monsterAbilities);
        ValidateStatuses(statuses);
        ValidateStrengthHitBonuses(characterClasses, strengthHitBonuses);
        ValidateMonsterLoot(enemies, monsterLoot);
        var lootRules = CreateLootRules(lootRuleValues);

        return new GameDataCatalog
        {
            Races = races.Select(race => new RaceDefinition(race.Id, race.Name, raceBonuses.GetValueOrDefault(race.Id, PrimaryAbilities.Zero))).ToList(),
            CharacterClasses = characterClasses.Select(characterClass => new CharacterClassDefinition(
                characterClass.Id,
                characterClass.Name,
                classMinimums.GetValueOrDefault(characterClass.Id, PrimaryAbilities.Zero),
                CharacterClassRules.UsesMana(characterClass.Id),
                characterClass.ExperienceModifier)).ToList(),
            Enemies = enemies,
            MonsterAbilities = monsterAbilities,
            StrengthHitBonuses = strengthHitBonuses,
            MonsterLoot = monsterLoot,
            LootRules = lootRules,
            WeaponTypes = weaponTypes,
            Weapons = CreateUpgradedWeapons(weapons, itemUpgrades),
            Armors = CreateUpgradedArmors(armors, itemUpgrades),
            Abilities = abilities,
            Items = items,
            MagicItems = magicItems,
            Spells = spells,
            SpellEffects = spellEffects,
            Perks = perks,
            Statuses = statuses,
            CharacterNames = characterNames,
            MinimumVitalityByHealth = minimumVitalityByHealth,
            MinimumManaByIntelligence = minimumManaByIntelligence,
            ExperienceByLevel = experienceByLevel,
            VitalityGrowthByHealth = vitalityGrowthByHealth,
            ManaGrowthByIntelligence = manaGrowthByIntelligence,
            StartingEquipmentByClass = startingEquipmentByClass,
            BaseLevelCompletionExperience = baseLevelCompletionExperience is >= 0
                ? baseLevelCompletionExperience.Value
                : throw new InvalidOperationException("A #Base XP pálya végén értékének nemnegatív egész számnak kell lennie az adatok.csv fájlban.")
        };
    }

    private static void AddDefinition(DataSection section, string[] cells,
        ICollection<RaceDefinition> races, ICollection<CharacterClassDefinition> characterClasses,
        ICollection<EnemyDefinition> enemies, ICollection<MonsterAbilityDefinition> monsterAbilities,
        ICollection<StrengthHitBonusDefinition> strengthHitBonuses,
        ICollection<MonsterLootDefinition> monsterLoot, IDictionary<string, int> lootRuleValues,
        ICollection<WeaponTypeDefinition> weaponTypes, ICollection<WeaponDefinition> weapons,
        ICollection<ArmorDefinition> armors, ICollection<AbilityDefinition> abilities, ICollection<MiscItemDefinition> items,
        ICollection<MagicItemDefinition> magicItems, ICollection<SpellDefinition> spells,
        ICollection<SpellEffectDefinition> spellEffects, ICollection<PerkDefinition> perks,
        ICollection<StatusDefinition> statuses, ICollection<CharacterNameDefinition> characterNames, ICollection<ItemUpgradeDefinition> itemUpgrades,
        IDictionary<string, PrimaryAbilities> raceBonuses, IDictionary<string, PrimaryAbilities> classMinimums,
        IDictionary<int, int> minimumVitalityByHealth, IDictionary<int, int> minimumManaByIntelligence, IDictionary<int, int> experienceByLevel,
        IDictionary<int, ValueRange> vitalityGrowthByHealth, IDictionary<int, ValueRange> manaGrowthByIntelligence,
        IDictionary<string, StartingEquipmentDefinition> startingEquipmentByClass,
        ref int? baseLevelCompletionExperience)
    {
        var id = Cell(cells, 0);
        if (string.IsNullOrWhiteSpace(id)) return;
        var name = Cell(cells, 1);

        switch (section)
        {
            case DataSection.Races:
                races.Add(new RaceDefinition(id, name, PrimaryAbilities.Zero));
                break;
            case DataSection.CharacterClasses:
                characterClasses.Add(new CharacterClassDefinition(id, name, PrimaryAbilities.Zero, CharacterClassRules.UsesMana(id), Double(cells, 2) ?? 1));
                break;
            case DataSection.Enemies:
                enemies.Add(new EnemyDefinition(id, name, Cell(cells, 2), Integer(cells, 3), Integer(cells, 4),
                    Integer(cells, 5), Integer(cells, 6), Integer(cells, 7) ?? 0, Integer(cells, 8) ?? 1,
                    cells.Skip(9).Take(2).Where(abilityId => !string.IsNullOrWhiteSpace(abilityId)).ToList()));
                break;
            case DataSection.MonsterAbilities:
                monsterAbilities.Add(new MonsterAbilityDefinition(id, name, ParseMonsterAbilityEffect(cells, 2),
                    Math.Clamp(Integer(cells, 3) ?? 0, 0, 100), Integer(cells, 4) ?? 0, Cell(cells, 5)));
                break;
            case DataSection.WeaponTypes:
                weaponTypes.Add(new WeaponTypeDefinition(id, name));
                break;
            case DataSection.Weapons:
                weapons.Add(new WeaponDefinition(id, name, EmptyAsNull(Cell(cells, 2)), ValueRangeFrom(cells, 3),
                    IsYes(cells, 4), AllowedClasses(cells, ("C001", null), ("C002", null), ("C003", null),
                        ("C004", 5), ("C005", 6), ("C006", 7)), Cell(cells, 8), RequiredPrice(cells, 9, id),
                    ParseRarity(cells, 10), EmptyAsNull(Cell(cells, 11)), Integer(cells, 12) ?? 0));
                break;
            case DataSection.Armors:
                armors.Add(new ArmorDefinition(id, name, ValueRangeFrom(cells, 2),
                    AllowedClasses(cells, ("C001", null), ("C003", null), ("C002", 3),
                        ("C004", 4), ("C005", 5), ("C006", 6)), Cell(cells, 7), RequiredPrice(cells, 8, id),
                    ParseRarity(cells, 9), EmptyAsNull(Cell(cells, 10)), Integer(cells, 11) ?? 0));
                break;
            case DataSection.Abilities:
                abilities.Add(new AbilityDefinition(id, name));
                break;
            case DataSection.Items:
                items.Add(new MiscItemDefinition(id, name, Cell(cells, 2), RequiredPrice(cells, 3, id),
                    ParseConsumableEffect(cells, 4), Integer(cells, 5) ?? 0));
                break;
            case DataSection.MagicItems:
                magicItems.Add(new MagicItemDefinition(id, name,
                    ParseMagicItemKind(cells, 2), ParseRarity(cells, 3), RequiredPrice(cells, 4, id),
                    Integer(cells, 5) ?? 0, EmptyAsNull(Cell(cells, 6)), ParseMagicItemEffect(cells, 7),
                    Integer(cells, 8) ?? 0, MagicItemAllowedClasses(cells, 9), Cell(cells, 10), Integer(cells, 11) ?? 0));
                break;
            case DataSection.ArcaneSpells:
                spells.Add(new SpellDefinition(id, name, SpellSchool.Arcane, RequiredSpellLevel(cells, id),
                    RequiredSpellManaCost(cells, id), RequiredSpellDescription(cells, id),
                    RequiredSpellTargetType(cells, id), RequiredNonNegativeInteger(cells, 6, id, "hatótáv"),
                    RequiredNonNegativeInteger(cells, 7, id, "terület"), IsYes(cells, 8),
                    RequiredSpellUsageMode(cells, id)));
                break;
            case DataSection.StrengthHitBonuses:
                strengthHitBonuses.Add(new StrengthHitBonusDefinition(id, Integer(cells, 1) ?? 0,
                    Integer(cells, 2) ?? 0));
                break;
            case DataSection.MonsterLoot:
                monsterLoot.Add(new MonsterLootDefinition(id, Integer(cells, 1) ?? 0,
                    IsYes(cells, 2), IsYes(cells, 3), IsYes(cells, 4),
                    RequiredItemRarity(cells, 5, id, "minimumritkaság"),
                    RequiredItemRarity(cells, 6, id, "maximumritkaság"),
                    Integer(cells, 7) ?? 0, Integer(cells, 8) ?? 0));
                break;
            case DataSection.LootRules:
                lootRuleValues[id] = Integer(cells, 1) ??
                    throw new InvalidOperationException($"A(z) '{id}' zsákmányparaméter értéke egész szám legyen.");
                break;
            case DataSection.DivineSpells:
                spells.Add(new SpellDefinition(id, name, SpellSchool.Divine, RequiredSpellLevel(cells, id),
                    RequiredSpellManaCost(cells, id), RequiredSpellDescription(cells, id),
                    RequiredSpellTargetType(cells, id), RequiredNonNegativeInteger(cells, 6, id, "hatótáv"),
                    RequiredNonNegativeInteger(cells, 7, id, "terület"), IsYes(cells, 8),
                    RequiredSpellUsageMode(cells, id)));
                break;
            case DataSection.SpellEffects:
                spellEffects.Add(new SpellEffectDefinition(id, Cell(cells, 1), Integer(cells, 2) ?? 0,
                    ParseRequiredEnum<SpellEffectType>(cells, 3, id, "hatástípus"), ParseDice(cells, 4, id),
                    Double(cells, 5) ?? 0, Integer(cells, 6) ?? 0, Integer(cells, 7) ?? 0,
                    Integer(cells, 8) ?? 0, Math.Clamp(Integer(cells, 9) ?? 100, 0, 100),
                    ParseRequiredEnum<SpellResolution>(cells, 10, id, "ellenpróba"),
                    EmptyAsNull(Cell(cells, 11)), Cell(cells, 12)));
                break;
            case DataSection.Perks:
                if (Integer(cells, 4) is { } tier)
                    perks.Add(new PerkDefinition(id, name, Cell(cells, 2), Cell(cells, 3), tier));
                break;
            case DataSection.Statuses:
                statuses.Add(new StatusDefinition(
                    id, name, Cell(cells, 2), Integer(cells, 3) is > 0 and var duration ? duration : null,
                    Integer(cells, 4) ?? 0, Integer(cells, 5) ?? 0,
                    Integer(cells, 6) ?? 0, Integer(cells, 7) ?? 0, Integer(cells, 8) ?? 0,
                    Integer(cells, 9) ?? 100, Integer(cells, 10) ?? 100,
                    Integer(cells, 11) ?? 100, Integer(cells, 12) ?? 100,
                    Integer(cells, 13) ?? 0, Integer(cells, 14) ?? 0,
                    Math.Max(1, Integer(cells, 15) ?? 1), Cell(cells, 16)));
                break;
            case DataSection.CharacterNames:
                characterNames.Add(new CharacterNameDefinition(id, name, Cell(cells, 2)));
                break;
            case DataSection.RaceAbilityBonuses:
                raceBonuses[id] = PrimaryAbilitiesFrom(cells);
                break;
            case DataSection.ClassAbilityMinimums:
                classMinimums[id] = PrimaryAbilitiesFrom(cells);
                break;
            case DataSection.VitalityByHealth:
                AddAbilityThreshold(cells, minimumVitalityByHealth);
                break;
            case DataSection.ManaByIntelligence:
                AddAbilityThreshold(cells, minimumManaByIntelligence);
                break;
            case DataSection.LevelExperience:
                AddAbilityThreshold(cells, experienceByLevel);
                break;
            case DataSection.VitalityGrowth:
                AddGrowthRange(cells, vitalityGrowthByHealth);
                break;
            case DataSection.ManaGrowth:
                AddGrowthRange(cells, manaGrowthByIntelligence);
                break;
            case DataSection.StartingEquipment:
                startingEquipmentByClass[id] = new StartingEquipmentDefinition(
                    id,
                    EmptyAsNull(Cell(cells, 1)),
                    EmptyAsNull(Cell(cells, 2)),
                    EmptyAsNull(Cell(cells, 3)),
                    EmptyAsNull(Cell(cells, 4)),
                    cells.Skip(5).Where(item => !string.IsNullOrWhiteSpace(item)).ToList());
                break;
            case DataSection.LevelCompletionExperience:
                baseLevelCompletionExperience = Integer(cells, 0);
                break;
            case DataSection.ItemUpgrades:
                itemUpgrades.Add(new ItemUpgradeDefinition(id, Cell(cells, 1), Integer(cells, 2) ?? 0,
                    Double(cells, 3) ?? 1, Integer(cells, 4) ?? 0));
                break;
        }
    }

    private static bool IsYes(string[] cells, int index) =>
        string.Equals(Cell(cells, index), "igen", StringComparison.OrdinalIgnoreCase);

    private static void ValidateStatuses(IEnumerable<StatusDefinition> statuses)
    {
        foreach (var status in statuses)
        {
            if (string.IsNullOrWhiteSpace(status.Icon))
                throw new InvalidOperationException($"A(z) {status.Id} állapot emoji mezője nem lehet üres.");
            if (status.PeriodicDamageMinimum < 0 || status.PeriodicDamageMaximum < status.PeriodicDamageMinimum)
                throw new InvalidOperationException($"A(z) {status.Id} állapot körsebzés-tartománya érvénytelen.");
            var percentages = new[] { status.MaximumVitalityPercent, status.MaximumManaPercent,
                status.VitalityRecoveryPercent, status.ManaRecoveryPercent };
            if (percentages.Any(value => value is < 0 or > 100))
                throw new InvalidOperationException($"A(z) {status.Id} állapot százalékos erőforrásértékeinek 0 és 100 közé kell esniük.");
        }
    }

    private static int RequiredPrice(string[] cells, int index, string id) => Integer(cells, index) is > 0 and var price
        ? price
        : throw new InvalidOperationException($"A(z) '{id}' tárgy ára hiányzik vagy nem pozitív az adatok.csv fájlban.");

    private static ItemRarity ParseRarity(string[] cells, int index) => Normalize(Cell(cells, index)) switch
    {
        "varazs" or "magic" => ItemRarity.Magic,
        "legendas" or "legendary" => ItemRarity.Legendary,
        _ => ItemRarity.Normal
    };

    private static ItemRarity RequiredItemRarity(string[] cells, int index, string id, string fieldName)
    {
        var normalized = Normalize(Cell(cells, index));
        if (normalized is not ("sima" or "normal" or "varazs" or "magic" or "legendas" or "legendary"))
            throw new InvalidOperationException($"A(z) '{id}' {fieldName} mezője ismeretlen: '{Cell(cells, index)}'.");
        return ParseRarity(cells, index);
    }

    private static ConsumableEffect ParseConsumableEffect(string[] cells, int index) =>
        Enum.TryParse<ConsumableEffect>(Cell(cells, index), true, out var effect) ? effect : ConsumableEffect.None;

    private static MagicItemKind ParseMagicItemKind(string[] cells, int index) => Normalize(Cell(cells, index)) switch
    {
        "amulett" => MagicItemKind.Amulet,
        "varazspalca" => MagicItemKind.Wand,
        "varazstekercs" => MagicItemKind.Scroll,
        _ => MagicItemKind.Ring
    };

    private static MagicItemEffect ParseMagicItemEffect(string[] cells, int index) =>
        Enum.TryParse<MagicItemEffect>(Cell(cells, index), true, out var effect) ? effect : MagicItemEffect.None;

    private static IReadOnlySet<string> MagicItemAllowedClasses(string[] cells, int mageOnlyIndex) =>
        IsYes(cells, mageOnlyIndex)
            ? new HashSet<string>(["C006"], StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(["C001", "C002", "C003", "C004", "C005", "C006"], StringComparer.OrdinalIgnoreCase);

    private static void ValidateMagicItems(IEnumerable<MagicItemDefinition> magicItems, IReadOnlyCollection<SpellDefinition> spells)
    {
        var spellsById = spells.ToDictionary(spell => spell.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var item in magicItems)
        {
            if (item.SpellId is { } spellId && !spellsById.ContainsKey(spellId))
                throw new InvalidOperationException($"A(z) '{item.Id}' varázstárgy ismeretlen varázslatra hivatkozik: '{spellId}'.");
            if (item.Kind == MagicItemKind.Wand && (item.SpellId is null || item.MaximumCharges <= 1 || spellsById[item.SpellId].School != SpellSchool.Arcane))
                throw new InvalidOperationException($"A(z) '{item.Id}' varázspálcának több töltetű mágusvarázslatot kell tartalmaznia.");
            if (item.Kind == MagicItemKind.Scroll && (item.SpellId is null || item.MaximumCharges != 1 ||
                item.AllowedClassIds.Count != 1 || !item.AllowedClassIds.Contains("C006")))
                throw new InvalidOperationException($"A(z) '{item.Id}' varázstekercsnek egy töltetűnek és csak mágus által használhatónak kell lennie.");
            if (item.Kind is MagicItemKind.Ring or MagicItemKind.Amulet &&
                (item.SpellId is not null || item.MaximumCharges != 0 || item.Effect == MagicItemEffect.None))
                throw new InvalidOperationException($"A(z) '{item.Id}' gyűrűnek vagy amulettnek passzív hatással és töltet nélkül kell rendelkeznie.");
        }
    }

    private static int RequiredSpellLevel(string[] cells, string id) => Integer(cells, 2) is >= 1 and <= 5 and var level
        ? level
        : throw new InvalidOperationException($"A(z) '{id}' varázslat szintjének 1 és 5 közé kell esnie.");

    private static int RequiredSpellManaCost(string[] cells, string id) => Integer(cells, 3) is > 0 and var manaCost
        ? manaCost
        : throw new InvalidOperationException($"A(z) '{id}' varázslat mannaköltségének pozitív egésznek kell lennie.");

    private static string RequiredSpellDescription(string[] cells, string id) =>
        !string.IsNullOrWhiteSpace(Cell(cells, 4))
            ? Cell(cells, 4)
            : throw new InvalidOperationException($"A(z) '{id}' varázslathoz leírás szükséges.");

    private static SpellTargetType RequiredSpellTargetType(string[] cells, string id) =>
        Enum.TryParse<SpellTargetType>(Cell(cells, 5), true, out var targetType)
            ? targetType
            : throw new InvalidOperationException($"A(z) '{id}' varázslat célzása ismeretlen: '{Cell(cells, 5)}'.");

    private static SpellUsageMode RequiredSpellUsageMode(string[] cells, string id) =>
        Enum.TryParse<SpellUsageMode>(Cell(cells, 9), true, out var usageMode)
            ? usageMode
            : throw new InvalidOperationException($"A(z) '{id}' varázslat használati módja ismeretlen: '{Cell(cells, 9)}'.");

    private static int RequiredNonNegativeInteger(string[] cells, int index, string id, string fieldName) =>
        Integer(cells, index) is >= 0 and var value
            ? value
            : throw new InvalidOperationException($"A(z) '{id}' varázslat {fieldName} mezője nemnegatív egész legyen.");

    private static void ValidateSpells(IReadOnlyCollection<SpellDefinition> spells)
    {
        foreach (var school in Enum.GetValues<SpellSchool>())
        {
            var schoolSpells = spells.Where(spell => spell.School == school).ToList();
            if (schoolSpells.Count != 20)
                throw new InvalidOperationException($"A(z) {school} iskolához pontosan 20 varázslat szükséges; jelenleg {schoolSpells.Count} található.");
            for (var level = 1; level <= 5; level++)
            {
                var count = schoolSpells.Count(spell => spell.Level == level);
                if (count != 4)
                    throw new InvalidOperationException($"A(z) {school} iskola {level}. szintjén pontosan 4 varázslat szükséges; jelenleg {count} található.");
            }
        }
    }

    private static void ValidateSpellEffects(IReadOnlyCollection<SpellDefinition> spells,
        IReadOnlyCollection<SpellEffectDefinition> effects)
    {
        var spellIds = spells.Select(spell => spell.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var effect in effects)
        {
            if (!spellIds.Contains(effect.SpellId))
                throw new InvalidOperationException($"A(z) '{effect.Id}' hatás ismeretlen varázslatra hivatkozik: '{effect.SpellId}'.");
            if (effect.Order <= 0 || effect.Duration < 0)
                throw new InvalidOperationException($"A(z) '{effect.Id}' hatás sorrendje legyen pozitív és időtartama nemnegatív.");
        }
        foreach (var spell in spells.Where(spell => spell.School == SpellSchool.Arcane))
            if (!effects.Any(effect => string.Equals(effect.SpellId, spell.Id, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"A(z) '{spell.Id}' mágusvarázslathoz legalább egy #Varázshatások sor szükséges.");
    }

    private static T ParseRequiredEnum<T>(string[] cells, int index, string id, string fieldName) where T : struct, Enum =>
        Enum.TryParse<T>(Cell(cells, index), true, out var value)
            ? value
            : throw new InvalidOperationException($"A(z) '{id}' {fieldName} mezője ismeretlen: '{Cell(cells, index)}'.");

    private static DiceExpression? ParseDice(string[] cells, int index, string id)
    {
        var value = Cell(cells, index);
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DiceExpression.TryParse(value, out var dice)
            ? dice
            : throw new InvalidOperationException($"A(z) '{id}' kockaképlete hibás: '{value}'.");
    }

    private static MonsterAbilityEffect ParseMonsterAbilityEffect(string[] cells, int index) =>
        Enum.TryParse<MonsterAbilityEffect>(Cell(cells, index), true, out var effect) ? effect : MonsterAbilityEffect.Trait;

    private static void ValidateEnemies(IEnumerable<EnemyDefinition> enemies,
        IReadOnlyCollection<MonsterAbilityDefinition> monsterAbilities)
    {
        var abilityIds = monsterAbilities.Select(ability => ability.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var enemy in enemies)
        {
            if (enemy.StrengthTier is < 1 or > 5)
                throw new InvalidOperationException($"A(z) '{enemy.Id}' szörny erősségi szintje csak 1 és 5 közötti lehet.");
            if (enemy.AbilityIds.Count > 2)
                throw new InvalidOperationException($"A(z) '{enemy.Id}' szörny legfeljebb két képességgel rendelkezhet.");
            foreach (var abilityId in enemy.AbilityIds.Where(abilityId => !abilityIds.Contains(abilityId)))
                throw new InvalidOperationException($"A(z) '{enemy.Id}' szörny ismeretlen képességre hivatkozik: '{abilityId}'.");
        }
    }

    private static void ValidateStrengthHitBonuses(IEnumerable<CharacterClassDefinition> characterClasses,
        IReadOnlyCollection<StrengthHitBonusDefinition> bonuses)
    {
        var classIds = characterClasses.Select(characterClass => characterClass.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var bonus in bonuses)
        {
            if (!classIds.Contains(bonus.CharacterClassId))
                throw new InvalidOperationException($"Az Erő-találati bónusz ismeretlen osztályra hivatkozik: '{bonus.CharacterClassId}'.");
            if (bonus.MinimumStrength is < 1 or > 13 || bonus.Bonus <= 0)
                throw new InvalidOperationException($"A(z) '{bonus.CharacterClassId}' Erő-találati küszöbe 1–13, bónusza pozitív legyen.");
        }
        if (bonuses.GroupBy(bonus => (bonus.CharacterClassId.ToUpperInvariant(), bonus.MinimumStrength))
            .Any(group => group.Count() > 1))
            throw new InvalidOperationException("Egy osztályhoz ugyanaz az Erő-találati küszöb csak egyszer szerepelhet.");
    }

    private static void ValidateMonsterLoot(IEnumerable<EnemyDefinition> enemies,
        IReadOnlyCollection<MonsterLootDefinition> lootDefinitions)
    {
        var enemyIds = enemies.Select(enemy => enemy.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var loot in lootDefinitions)
        {
            if (!enemyIds.Contains(loot.EnemyId))
                throw new InvalidOperationException($"A szörnyzsákmány ismeretlen ellenfélre hivatkozik: '{loot.EnemyId}'.");
            if (loot.EquipmentChancePercent is < 0 or > 100 || loot.MaximumMagicPower < 0 ||
                loot.MaximumBasePrice <= 0 || loot.MinimumRarity > loot.MaximumRarity)
                throw new InvalidOperationException($"A(z) '{loot.EnemyId}' szörnyzsákmány-korlátai érvénytelenek.");
            if (!loot.CanDropWeapon && !loot.CanDropArmor && !loot.CanDropMagicItem)
                throw new InvalidOperationException($"A(z) '{loot.EnemyId}' zsákmánysora egyetlen tárgykategóriát sem engedélyez.");
        }
        if (lootDefinitions.GroupBy(loot => loot.EnemyId, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Egy szörnyhöz csak egy #Szörny zsákmány sor tartozhat.");
    }

    private static LootRules CreateLootRules(IReadOnlyDictionary<string, int> values)
    {
        int Required(string id) => values.TryGetValue(id, out var value)
            ? value
            : throw new InvalidOperationException($"Hiányzó #Zsákmány paraméterek érték: '{id}'.");
        var rules = new LootRules(Required("KulcsEsély"), Required("AranyEsély"),
            Required("AranyTierSzorzó"), Required("TolvajEsélySzorzó"),
            Required("IntelligenciaPontBónusz"));
        if (rules.KeyChancePercent is < 0 or > 100 || rules.GoldChancePercent is < 0 or > 100 ||
            rules.GoldPerStrengthTier <= 0 || rules.ThiefChanceMultiplierPercent <= 0 ||
            rules.IntelligenceChanceBonusPerPoint < 0)
            throw new InvalidOperationException("A #Zsákmány paraméterek értékei érvénytelenek.");
        return rules;
    }

    private static IReadOnlyList<WeaponDefinition> CreateUpgradedWeapons(
        IReadOnlyCollection<WeaponDefinition> weapons, IReadOnlyCollection<ItemUpgradeDefinition> upgrades)
    {
        var result = weapons.ToList();
        foreach (var weapon in weapons.Where(weapon => weapon.Rarity == ItemRarity.Normal))
        foreach (var upgrade in upgrades)
            result.Add(weapon with
            {
                Id = $"{weapon.Id}-{upgrade.Id}",
                Name = weapon.Name + " " + upgrade.NameSuffix,
                Damage = Increase(weapon.Damage, upgrade.CombatBonus),
                Description = $"{weapon.Description} Mágikus {upgrade.NameSuffix} változat.",
                BasePrice = Math.Max(1, (int)Math.Ceiling(weapon.BasePrice * upgrade.PriceMultiplier)),
                Rarity = ItemRarity.Magic,
                BaseWeaponId = weapon.Id,
                MagicPower = upgrade.MagicPower
            });
        return result;
    }

    private static IReadOnlyList<ArmorDefinition> CreateUpgradedArmors(
        IReadOnlyCollection<ArmorDefinition> armors, IReadOnlyCollection<ItemUpgradeDefinition> upgrades)
    {
        var result = armors.ToList();
        foreach (var armor in armors.Where(armor => armor.Rarity == ItemRarity.Normal))
        foreach (var upgrade in upgrades)
            result.Add(armor with
            {
                Id = $"{armor.Id}-{upgrade.Id}",
                Name = armor.Name + " " + upgrade.NameSuffix,
                Defense = Increase(armor.Defense, upgrade.CombatBonus),
                Description = $"{armor.Description} Mágikus {upgrade.NameSuffix} változat.",
                BasePrice = Math.Max(1, (int)Math.Ceiling(armor.BasePrice * upgrade.PriceMultiplier)),
                Rarity = ItemRarity.Magic,
                BaseArmorId = armor.Id,
                MagicPower = upgrade.MagicPower
            });
        return result;
    }

    private static ValueRange? Increase(ValueRange? range, int amount) => range is null
        ? null
        : new ValueRange(range.Minimum + amount, range.Maximum + amount);

    private static IReadOnlySet<string> AllowedClasses(string[] cells,
        params (string ClassId, int? FlagIndex)[] classRules) => classRules
        .Where(rule => rule.FlagIndex is null || IsYes(cells, rule.FlagIndex.Value))
        .Select(rule => rule.ClassId)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> ReadLinesWithFallbackEncoding(string filePath)
    {
        try
        {
            return File.ReadAllLines(filePath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return File.ReadAllLines(filePath, Encoding.GetEncoding(1250));
        }
    }

    private static bool IsHeaderRow(string value) => Normalize(value) is "id" or "fajid" or "osztalyid" or
        "szornyid" or "egeszseg" or "intelligencia" or "szint";
    private static string Cell(string[] cells, int index) => index < cells.Length ? cells[index] : string.Empty;
    private static string? EmptyAsNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static int? Integer(string[] cells, int index) => int.TryParse(Cell(cells, index), CultureInfo.InvariantCulture, out var value) ? value : null;
    private static double? Double(string[] cells, int index) => double.TryParse(Cell(cells, index), CultureInfo.InvariantCulture, out var value) ? value : null;
    private static ValueRange? ValueRangeFrom(string[] cells, int index)
    {
        var parts = Cell(cells, index).Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2
            && int.TryParse(parts[0], CultureInfo.InvariantCulture, out var minimum)
            && int.TryParse(parts[1], CultureInfo.InvariantCulture, out var maximum)
            && minimum <= maximum
            ? new ValueRange(minimum, maximum)
            : null;
    }
    private static PrimaryAbilities PrimaryAbilitiesFrom(string[] cells) => new(Integer(cells, 1) ?? 0, Integer(cells, 2) ?? 0, Integer(cells, 3) ?? 0, Integer(cells, 4) ?? 0);

    private static void AddAbilityThreshold(string[] cells, IDictionary<int, int> thresholds)
    {
        if (Integer(cells, 0) is not { } ability || Integer(cells, 1) is not { } resource) return;
        thresholds[ability] = resource;
    }

    private static void AddGrowthRange(string[] cells, IDictionary<int, ValueRange> ranges)
    {
        if (Integer(cells, 0) is not { } ability || ValueRangeFrom(cells, 1) is not { } range) return;
        ranges[ability] = range;
    }

    private static bool TryReadSection(string[] cells, out DataSection section)
    {
        var sectionCell = cells[0];
        if (sectionCell.StartsWith('#'))
        {
            section = ParseSection(sectionCell[1..]);
            return true;
        }

        // Átmeneti kompatibilitás a jelenlegi, # nélküli CSV-hez.
        // Új vagy bővített adatoknál a # előtagos szekciócím a kötelező forma.
        if (!cells.Skip(1).All(string.IsNullOrEmpty))
        {
            section = DataSection.None;
            return false;
        }

        section = ParseSection(sectionCell);
        return section != DataSection.None;
    }

    private static DataSection ParseSection(string sectionName) => Normalize(sectionName) switch
    {
        "fajok" => DataSection.Races,
        "osztalyok" => DataSection.CharacterClasses,
        "ellensegek" => DataSection.Enemies,
        "szornykepessegek" => DataSection.MonsterAbilities,
        "fegyvertipus" => DataSection.WeaponTypes,
        "fegyverek" => DataSection.Weapons,
        "pancelok" => DataSection.Armors,
        "kepessegek" => DataSection.Abilities,
        "targyak" => DataSection.Items,
        "varazstargyak" => DataSection.MagicItems,
        "varazslatok" => DataSection.ArcaneSpells,
        "papi varazslatok" => DataSection.DivineSpells,
        "varazshatasok" => DataSection.SpellEffects,
        "tehetsegek" => DataSection.Perks,
        "allapotok" => DataSection.Statuses,
        "karakternevek" => DataSection.CharacterNames,
        "faji kepessegbonuszok" => DataSection.RaceAbilityBonuses,
        "osztaly kepessegminimumok" => DataSection.ClassAbilityMinimums,
        "ero talalati bonusz" => DataSection.StrengthHitBonuses,
        "szorny zsakmany" => DataSection.MonsterLoot,
        "zsakmany parameterek" => DataSection.LootRules,
        "osztaly kezdofelszereles" => DataSection.StartingEquipment,
        "egeszseg altal adott eletero minimum" => DataSection.VitalityByHealth,
        "intelligencia altal adott manna minimum" => DataSection.ManaByIntelligence,
        "szintlepesek" => DataSection.LevelExperience,
        "szintlepes eletero novekedes" => DataSection.VitalityGrowth,
        "szintlepes manna novekedes" => DataSection.ManaGrowth,
        "base xp palya vegen" => DataSection.LevelCompletionExperience,
        "targybovitesek" => DataSection.ItemUpgrades,
        _ => DataSection.None
    };

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        return string.Concat(decomposed.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant();
    }

    private enum DataSection
    {
        None,
        Races,
        CharacterClasses,
        Enemies,
        MonsterAbilities,
        WeaponTypes,
        Weapons,
        Armors,
        Abilities,
        Items,
        MagicItems,
        ArcaneSpells,
        DivineSpells,
        SpellEffects,
        Perks,
        Statuses,
        CharacterNames,
        RaceAbilityBonuses,
        ClassAbilityMinimums,
        StrengthHitBonuses,
        MonsterLoot,
        LootRules,
        StartingEquipment,
        VitalityByHealth,
        ManaByIntelligence,
        LevelExperience,
        VitalityGrowth,
        ManaGrowth,
        LevelCompletionExperience,
        ItemUpgrades
    }
}
