using System.Globalization;
using System.Text;
using MazeGame.Domain.Characters;
using MazeGame.Domain.Combat;
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
        var weapons = new List<WeaponDefinition>();
        var armors = new List<ArmorDefinition>();
        var abilities = new List<AbilityDefinition>();
        var magicItems = new List<MagicItemDefinition>();
        var spells = new List<SpellDefinition>();
        var raceBonuses = new Dictionary<string, PrimaryAbilities>(StringComparer.OrdinalIgnoreCase);
        var classMinimums = new Dictionary<string, PrimaryAbilities>(StringComparer.OrdinalIgnoreCase);
        var minimumVitalityByHealth = new Dictionary<int, int>();
        var minimumManaByIntelligence = new Dictionary<int, int>();
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
            AddDefinition(section, cells, races, characterClasses, enemies, weapons, armors, abilities, magicItems, spells,
                raceBonuses, classMinimums, minimumVitalityByHealth, minimumManaByIntelligence);
        }

        return new GameDataCatalog
        {
            Races = races.Select(race => new RaceDefinition(race.Name, raceBonuses.GetValueOrDefault(race.Name, PrimaryAbilities.Zero))).ToList(),
            CharacterClasses = characterClasses.Select(characterClass => new CharacterClassDefinition(characterClass.Name, classMinimums.GetValueOrDefault(characterClass.Name, PrimaryAbilities.Zero))).ToList(),
            Enemies = enemies,
            Weapons = weapons,
            Armors = armors,
            Abilities = abilities,
            MagicItems = magicItems,
            Spells = spells,
            MinimumVitalityByHealth = minimumVitalityByHealth,
            MinimumManaByIntelligence = minimumManaByIntelligence
        };
    }

    private static void AddDefinition(DataSection section, string[] cells,
        ICollection<RaceDefinition> races, ICollection<CharacterClassDefinition> characterClasses,
        ICollection<EnemyDefinition> enemies, ICollection<WeaponDefinition> weapons,
        ICollection<ArmorDefinition> armors, ICollection<AbilityDefinition> abilities,
        ICollection<MagicItemDefinition> magicItems, ICollection<SpellDefinition> spells,
        IDictionary<string, PrimaryAbilities> raceBonuses, IDictionary<string, PrimaryAbilities> classMinimums,
        IDictionary<int, int> minimumVitalityByHealth, IDictionary<int, int> minimumManaByIntelligence)
    {
        var name = Cell(cells, 0);
        if (string.IsNullOrWhiteSpace(name)) return;

        switch (section)
        {
            case DataSection.Races:
                foreach (var race in cells.Where(cell => !string.IsNullOrWhiteSpace(cell))) races.Add(new RaceDefinition(race, PrimaryAbilities.Zero));
                break;
            case DataSection.CharacterClasses:
                foreach (var characterClass in cells.Where(cell => !string.IsNullOrWhiteSpace(cell))) characterClasses.Add(new CharacterClassDefinition(characterClass, PrimaryAbilities.Zero));
                break;
            case DataSection.Enemies:
                enemies.Add(new EnemyDefinition(name, Integer(cells, 1), Integer(cells, 2), Integer(cells, 3), Integer(cells, 4)));
                break;
            case DataSection.Weapons:
                weapons.Add(new WeaponDefinition(name, EmptyAsNull(Cell(cells, 1)), Integer(cells, 2)));
                break;
            case DataSection.Armors:
                armors.Add(new ArmorDefinition(name, Integer(cells, 1)));
                break;
            case DataSection.Abilities:
                abilities.Add(new AbilityDefinition(name));
                break;
            case DataSection.MagicItems:
                magicItems.Add(new MagicItemDefinition(name));
                break;
            case DataSection.ArcaneSpells:
                spells.Add(new SpellDefinition(name, SpellSchool.Arcane));
                break;
            case DataSection.DivineSpells:
                spells.Add(new SpellDefinition(name, SpellSchool.Divine));
                break;
            case DataSection.RaceAbilityBonuses:
                raceBonuses[name] = PrimaryAbilitiesFrom(cells);
                break;
            case DataSection.ClassAbilityMinimums:
                classMinimums[name] = PrimaryAbilitiesFrom(cells);
                break;
            case DataSection.VitalityByHealth:
                AddAbilityThreshold(cells, minimumVitalityByHealth);
                break;
            case DataSection.ManaByIntelligence:
                AddAbilityThreshold(cells, minimumManaByIntelligence);
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

    private static bool IsHeaderRow(string value) => Normalize(value) is "nev" or "faj" or "osztaly";
    private static string Cell(string[] cells, int index) => index < cells.Length ? cells[index] : string.Empty;
    private static string? EmptyAsNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static int? Integer(string[] cells, int index) => int.TryParse(Cell(cells, index), CultureInfo.InvariantCulture, out var value) ? value : null;
    private static PrimaryAbilities PrimaryAbilitiesFrom(string[] cells) => new(Integer(cells, 1) ?? 0, Integer(cells, 2) ?? 0, Integer(cells, 3) ?? 0, Integer(cells, 4) ?? 0);

    private static void AddAbilityThreshold(string[] cells, IDictionary<int, int> thresholds)
    {
        if (Integer(cells, 0) is not { } ability || Integer(cells, 1) is not { } resource) return;
        thresholds[ability] = resource;
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
        "fegyverek" => DataSection.Weapons,
        "pancelok" => DataSection.Armors,
        "kepessegek" => DataSection.Abilities,
        "varazstargyak" => DataSection.MagicItems,
        "varazslatok" => DataSection.ArcaneSpells,
        "papi varazslatok" => DataSection.DivineSpells,
        "faji kepessegbonuszok" => DataSection.RaceAbilityBonuses,
        "osztaly kepessegminimumok" => DataSection.ClassAbilityMinimums,
        "egeszseg altal adott eletero minimum" => DataSection.VitalityByHealth,
        "intelligencia altal adott manna minimum" => DataSection.ManaByIntelligence,
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
        Weapons,
        Armors,
        Abilities,
        MagicItems,
        ArcaneSpells,
        DivineSpells,
        RaceAbilityBonuses,
        ClassAbilityMinimums,
        VitalityByHealth,
        ManaByIntelligence
    }
}
