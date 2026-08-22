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
        var weaponTypes = new List<WeaponTypeDefinition>();
        var weapons = new List<WeaponDefinition>();
        var armors = new List<ArmorDefinition>();
        var abilities = new List<AbilityDefinition>();
        var items = new List<MiscItemDefinition>();
        var magicItems = new List<MagicItemDefinition>();
        var spells = new List<SpellDefinition>();
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
            AddDefinition(section, cells, races, characterClasses, enemies, weaponTypes, weapons, armors, abilities, items, magicItems, spells, perks, statuses, characterNames, itemUpgrades,
                raceBonuses, classMinimums, minimumVitalityByHealth, minimumManaByIntelligence, experienceByLevel,
                vitalityGrowthByHealth, manaGrowthByIntelligence, startingEquipmentByClass, ref baseLevelCompletionExperience);
        }

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
            WeaponTypes = weaponTypes,
            Weapons = CreateUpgradedWeapons(weapons, itemUpgrades),
            Armors = CreateUpgradedArmors(armors, itemUpgrades),
            Abilities = abilities,
            Items = items,
            MagicItems = magicItems,
            Spells = spells,
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
        ICollection<EnemyDefinition> enemies, ICollection<WeaponTypeDefinition> weaponTypes, ICollection<WeaponDefinition> weapons,
        ICollection<ArmorDefinition> armors, ICollection<AbilityDefinition> abilities, ICollection<MiscItemDefinition> items,
        ICollection<MagicItemDefinition> magicItems, ICollection<SpellDefinition> spells, ICollection<PerkDefinition> perks,
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
                enemies.Add(new EnemyDefinition(id, name, Cell(cells, 2), Integer(cells, 3), Integer(cells, 4), Integer(cells, 5), Integer(cells, 6), Integer(cells, 7) ?? 0));
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
                magicItems.Add(new MagicItemDefinition(id, name, RequiredPrice(cells, 2, id)));
                break;
            case DataSection.ArcaneSpells:
                spells.Add(new SpellDefinition(id, name, SpellSchool.Arcane));
                break;
            case DataSection.DivineSpells:
                spells.Add(new SpellDefinition(id, name, SpellSchool.Divine));
                break;
            case DataSection.Perks:
                if (Integer(cells, 4) is { } tier)
                    perks.Add(new PerkDefinition(id, name, Cell(cells, 2), Cell(cells, 3), tier));
                break;
            case DataSection.Statuses:
                statuses.Add(new StatusDefinition(id, name, Cell(cells, 2)));
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

    private static int RequiredPrice(string[] cells, int index, string id) => Integer(cells, index) is > 0 and var price
        ? price
        : throw new InvalidOperationException($"A(z) '{id}' tárgy ára hiányzik vagy nem pozitív az adatok.csv fájlban.");

    private static ItemRarity ParseRarity(string[] cells, int index) => Normalize(Cell(cells, index)) switch
    {
        "varazs" => ItemRarity.Magic,
        "legendas" => ItemRarity.Legendary,
        _ => ItemRarity.Normal
    };

    private static ConsumableEffect ParseConsumableEffect(string[] cells, int index) =>
        Enum.TryParse<ConsumableEffect>(Cell(cells, index), true, out var effect) ? effect : ConsumableEffect.None;

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

    private static bool IsHeaderRow(string value) => Normalize(value) is "id" or "fajid" or "osztalyid" or "egeszseg" or "intelligencia" or "szint";
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
        "fegyvertipus" => DataSection.WeaponTypes,
        "fegyverek" => DataSection.Weapons,
        "pancelok" => DataSection.Armors,
        "kepessegek" => DataSection.Abilities,
        "targyak" => DataSection.Items,
        "varazstargyak" => DataSection.MagicItems,
        "varazslatok" => DataSection.ArcaneSpells,
        "papi varazslatok" => DataSection.DivineSpells,
        "tehetsegek" => DataSection.Perks,
        "allapotok" => DataSection.Statuses,
        "karakternevek" => DataSection.CharacterNames,
        "faji kepessegbonuszok" => DataSection.RaceAbilityBonuses,
        "osztaly kepessegminimumok" => DataSection.ClassAbilityMinimums,
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
        WeaponTypes,
        Weapons,
        Armors,
        Abilities,
        Items,
        MagicItems,
        ArcaneSpells,
        DivineSpells,
        Perks,
        Statuses,
        CharacterNames,
        RaceAbilityBonuses,
        ClassAbilityMinimums,
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
