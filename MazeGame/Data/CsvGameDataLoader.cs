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
        var raceBonuses = new Dictionary<string, PrimaryAbilities>(StringComparer.OrdinalIgnoreCase);
        var classMinimums = new Dictionary<string, PrimaryAbilities>(StringComparer.OrdinalIgnoreCase);
        var minimumVitalityByHealth = new Dictionary<int, int>();
        var minimumManaByIntelligence = new Dictionary<int, int>();
        var experienceByLevel = new Dictionary<int, int>();
        var vitalityGrowthByHealth = new Dictionary<int, ValueRange>();
        var manaGrowthByIntelligence = new Dictionary<int, ValueRange>();
        var startingEquipmentByClass = new Dictionary<string, StartingEquipmentDefinition>(StringComparer.OrdinalIgnoreCase);
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
            AddDefinition(section, cells, races, characterClasses, enemies, weaponTypes, weapons, armors, abilities, items, magicItems, spells,
                raceBonuses, classMinimums, minimumVitalityByHealth, minimumManaByIntelligence, experienceByLevel,
                vitalityGrowthByHealth, manaGrowthByIntelligence, startingEquipmentByClass);
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
            Weapons = weapons,
            Armors = armors,
            Abilities = abilities,
            Items = items,
            MagicItems = magicItems,
            Spells = spells,
            MinimumVitalityByHealth = minimumVitalityByHealth,
            MinimumManaByIntelligence = minimumManaByIntelligence,
            ExperienceByLevel = experienceByLevel,
            VitalityGrowthByHealth = vitalityGrowthByHealth,
            ManaGrowthByIntelligence = manaGrowthByIntelligence,
            StartingEquipmentByClass = startingEquipmentByClass
        };
    }

    private static void AddDefinition(DataSection section, string[] cells,
        ICollection<RaceDefinition> races, ICollection<CharacterClassDefinition> characterClasses,
        ICollection<EnemyDefinition> enemies, ICollection<WeaponTypeDefinition> weaponTypes, ICollection<WeaponDefinition> weapons,
        ICollection<ArmorDefinition> armors, ICollection<AbilityDefinition> abilities, ICollection<MiscItemDefinition> items,
        ICollection<MagicItemDefinition> magicItems, ICollection<SpellDefinition> spells,
        IDictionary<string, PrimaryAbilities> raceBonuses, IDictionary<string, PrimaryAbilities> classMinimums,
        IDictionary<int, int> minimumVitalityByHealth, IDictionary<int, int> minimumManaByIntelligence, IDictionary<int, int> experienceByLevel,
        IDictionary<int, ValueRange> vitalityGrowthByHealth, IDictionary<int, ValueRange> manaGrowthByIntelligence,
        IDictionary<string, StartingEquipmentDefinition> startingEquipmentByClass)
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
                weapons.Add(new WeaponDefinition(id, name, EmptyAsNull(Cell(cells, 2)), ValueRangeFrom(cells, 3)));
                break;
            case DataSection.Armors:
                armors.Add(new ArmorDefinition(id, name, ValueRangeFrom(cells, 2)));
                break;
            case DataSection.Abilities:
                abilities.Add(new AbilityDefinition(id, name));
                break;
            case DataSection.Items:
                items.Add(new MiscItemDefinition(id, name));
                break;
            case DataSection.MagicItems:
                magicItems.Add(new MagicItemDefinition(id, name));
                break;
            case DataSection.ArcaneSpells:
                spells.Add(new SpellDefinition(id, name, SpellSchool.Arcane));
                break;
            case DataSection.DivineSpells:
                spells.Add(new SpellDefinition(id, name, SpellSchool.Divine));
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
        }
    }

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
        "faji kepessegbonuszok" => DataSection.RaceAbilityBonuses,
        "osztaly kepessegminimumok" => DataSection.ClassAbilityMinimums,
        "osztaly kezdofelszereles" => DataSection.StartingEquipment,
        "egeszseg altal adott eletero minimum" => DataSection.VitalityByHealth,
        "intelligencia altal adott manna minimum" => DataSection.ManaByIntelligence,
        "szintlepesek" => DataSection.LevelExperience,
        "szintlepes eletero novekedes" => DataSection.VitalityGrowth,
        "szintlepes manna novekedes" => DataSection.ManaGrowth,
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
        RaceAbilityBonuses,
        ClassAbilityMinimums,
        StartingEquipment,
        VitalityByHealth,
        ManaByIntelligence,
        LevelExperience,
        VitalityGrowth,
        ManaGrowth
    }
}
