using System.Globalization;
using System.Text;
using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin.Data;

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
        var doorAttemptRuleValues = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
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
        var innNames = new List<string>();
        var innRumors = new List<InnRumorDefinition>();
        var traps = new List<TrapDefinition>();
        var npcs = new List<NpcDefinition>();
        var uniqueNpcCharacters = new List<UniqueNpcCharacterDefinition>();
        var npcEncounters = new List<NpcEncounterDefinition>();
        var npcDialogues = new List<NpcDialogueDefinition>();
        var npcStoryChoices = new List<NpcStoryChoiceDefinition>();
        var npcQuests = new List<NpcQuestDefinition>();
        var partySituations = new List<PartySituationDefinition>();
        var partyRemarks = new List<PartyRemarkDefinition>();
        var itemUpgrades = new List<ItemUpgradeDefinition>();
        var raceBonuses = new Dictionary<string, PrimaryAbilities>(StringComparer.OrdinalIgnoreCase);
        var classMinimums = new Dictionary<string, PrimaryAbilities>(StringComparer.OrdinalIgnoreCase);
        var minimumVitalityByHealth = new Dictionary<int, int>();
        var minimumManaByIntelligence = new Dictionary<int, int>();
        var experienceByLevel = new Dictionary<int, int>();
        var vitalityGrowthByHealth = new Dictionary<int, ValueRange>();
        var manaGrowthByIntelligence = new Dictionary<int, ValueRange>();
        var startingEquipmentByClass = new Dictionary<string, StartingEquipmentDefinition>(StringComparer.OrdinalIgnoreCase);
        var characterResourceGrowthByClass = new Dictionary<string, CharacterResourceGrowthDefinition>(StringComparer.OrdinalIgnoreCase);
        int? baseLevelCompletionExperience = null;
        var section = DataSection.None;

        var sourceLines = ReadLinesWithFallbackEncoding(filePath).ToArray();
        for (var lineIndex = 0; lineIndex < sourceLines.Length; lineIndex++)
        {
            var rawLine = sourceLines[lineIndex];
            var lineNumber = lineIndex + 1;
            var cells = ParseCsvLine(rawLine);
            if (cells.All(string.IsNullOrEmpty)) continue;

            if (TryReadSection(cells, lineNumber, out var parsedSection))
            {
                section = parsedSection;
                continue;
            }

            if (IsHeaderRow(cells[0])) continue;
            if (section == DataSection.None)
                throw new InvalidDataException($"Az adatok.csv {lineNumber}. sora nem tartozik ismert fejezethez: '{rawLine}'.");
            try
            {
                AddDefinition(section, cells, races, characterClasses, enemies, monsterAbilities, strengthHitBonuses,
                    monsterLoot, lootRuleValues, doorAttemptRuleValues, weaponTypes, weapons, armors, abilities, items, magicItems, spells, spellEffects, perks, statuses, characterNames, innNames, innRumors, traps,
                    npcs, uniqueNpcCharacters, npcEncounters, npcDialogues, npcStoryChoices, npcQuests,
                    partySituations, partyRemarks, itemUpgrades,
                    raceBonuses, classMinimums, minimumVitalityByHealth, minimumManaByIntelligence, experienceByLevel,
                    vitalityGrowthByHealth, manaGrowthByIntelligence, startingEquipmentByClass,
                    characterResourceGrowthByClass, ref baseLevelCompletionExperience);
            }
            catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or ArgumentException)
            {
                throw new InvalidDataException($"Hiba az adatok.csv {lineNumber}. sorában, a(z) '{section}' fejezetben: {exception.Message}", exception);
            }
        }

        ValidateRequiredCoreData(races, characterClasses, enemies, abilities, raceBonuses, classMinimums,
            startingEquipmentByClass, minimumVitalityByHealth, minimumManaByIntelligence, experienceByLevel,
            vitalityGrowthByHealth, manaGrowthByIntelligence);
        ValidateCharacterResourceGrowth(characterClasses, characterResourceGrowthByClass);
        if (innNames.Count == 0)
            throw new InvalidDataException("A #Fogadónevek fejezetnek legalább egy nevet kell tartalmaznia az adatok.csv fájlban.");
        if (innRumors.Count == 0)
            throw new InvalidDataException("A #Pletykák fejezetnek legalább egy pletykát kell tartalmaznia az adatok.csv fájlban.");
        if (traps.Count == 0)
            throw new InvalidDataException("A #Csapdák fejezetnek legalább egy csapdát kell tartalmaznia az adatok.csv fájlban.");
        ValidateUniqueIds(
            ("Fajok", races.Select(value => value.Id)),
            ("Osztályok", characterClasses.Select(value => value.Id)),
            ("Ellenségek", enemies.Select(value => value.Id)),
            ("Szörnyképességek", monsterAbilities.Select(value => value.Id)),
            ("Fegyvertípus", weaponTypes.Select(value => value.Id)),
            ("Fegyverek", weapons.Select(value => value.Id)),
            ("Páncélok", armors.Select(value => value.Id)),
            ("Képességek", abilities.Select(value => value.Id)),
            ("Tárgyak", items.Select(value => value.Id)),
            ("Varázstárgyak", magicItems.Select(value => value.Id)),
            ("Varázslatok", spells.Select(value => value.Id)),
            ("Tehetségek", perks.Select(value => value.Id)),
            ("Állapotok", statuses.Select(value => value.Id)),
            ("Karakternevek", characterNames.Select(value => value.Id)),
            ("Pletykák", innRumors.Select(value => value.Id)),
            ("Csapdák", traps.Select(value => value.Id)),
            ("NPC-k", npcs.Select(value => value.Id)),
            ("Egyedi NPC karakterlapok", uniqueNpcCharacters.Select(value => value.Id)),
            ("NPC találkozások", npcEncounters.Select(value => value.Id)),
            ("NPC párbeszédek", npcDialogues.Select(value => value.Id)),
            ("NPC történeti választások", npcStoryChoices.Select(value => value.Id)),
            ("NPC küldetések", npcQuests.Select(value => value.Id)),
            ("Szituációk", partySituations.Select(value => value.Id)),
            ("Parti megjegyzések", partyRemarks.Select(value => value.Id)),
            ("Tárgybővítések", itemUpgrades.Select(value => value.Id)));
        ValidateSpells(spells);
        ValidateSpellEffects(spells, spellEffects);
        ValidateMagicItems(magicItems, spells);
        ValidateEnemies(enemies, monsterAbilities);
        ValidateStatuses(statuses);
        ValidateStrengthHitBonuses(characterClasses, strengthHitBonuses);
        ValidateMonsterLoot(enemies, monsterLoot);
        ValidateTrapConfigurations(traps);
        ValidateQuestRoomEncounters(enemies, items);
        ValidateNpcData(npcs, uniqueNpcCharacters, npcEncounters, npcDialogues, npcStoryChoices, npcQuests,
            races, characterClasses,
            enemies, monsterAbilities, items, weapons, armors, magicItems, perks);
        ValidatePartyRemarks(partySituations, partyRemarks, races, characterClasses);
        var lootRules = CreateLootRules(lootRuleValues);
        var doorAttemptRules = CreateDoorAttemptRules(doorAttemptRuleValues);

        return new GameDataCatalog
        {
            Races = races.Select(race => race with
            {
                AbilityBonuses = raceBonuses.GetValueOrDefault(race.Id, PrimaryAbilities.Zero)
            }).ToList(),
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
            DoorAttemptRules = doorAttemptRules,
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
            InnNames = innNames,
            InnRumors = innRumors,
            Traps = traps,
            Npcs = npcs,
            UniqueNpcCharacters = uniqueNpcCharacters,
            NpcEncounters = npcEncounters,
            NpcDialogues = npcDialogues,
            NpcStoryChoices = npcStoryChoices,
            NpcQuests = npcQuests,
            PartySituations = partySituations,
            PartyRemarks = partyRemarks,
            MinimumVitalityByHealth = minimumVitalityByHealth,
            MinimumManaByIntelligence = minimumManaByIntelligence,
            ExperienceByLevel = experienceByLevel,
            VitalityGrowthByHealth = vitalityGrowthByHealth,
            ManaGrowthByIntelligence = manaGrowthByIntelligence,
            StartingEquipmentByClass = startingEquipmentByClass,
            CharacterResourceGrowthByClass = characterResourceGrowthByClass,
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
        IDictionary<string, int> doorAttemptRuleValues,
        ICollection<WeaponTypeDefinition> weaponTypes, ICollection<WeaponDefinition> weapons,
        ICollection<ArmorDefinition> armors, ICollection<AbilityDefinition> abilities, ICollection<MiscItemDefinition> items,
        ICollection<MagicItemDefinition> magicItems, ICollection<SpellDefinition> spells,
        ICollection<SpellEffectDefinition> spellEffects, ICollection<PerkDefinition> perks,
        ICollection<StatusDefinition> statuses, ICollection<CharacterNameDefinition> characterNames,
        ICollection<string> innNames, ICollection<InnRumorDefinition> innRumors, ICollection<TrapDefinition> traps,
        ICollection<NpcDefinition> npcs, ICollection<UniqueNpcCharacterDefinition> uniqueNpcCharacters,
        ICollection<NpcEncounterDefinition> npcEncounters,
        ICollection<NpcDialogueDefinition> npcDialogues, ICollection<NpcStoryChoiceDefinition> npcStoryChoices,
        ICollection<NpcQuestDefinition> npcQuests,
        ICollection<PartySituationDefinition> partySituations,
        ICollection<PartyRemarkDefinition> partyRemarks,
        ICollection<ItemUpgradeDefinition> itemUpgrades,
        IDictionary<string, PrimaryAbilities> raceBonuses, IDictionary<string, PrimaryAbilities> classMinimums,
        IDictionary<int, int> minimumVitalityByHealth, IDictionary<int, int> minimumManaByIntelligence, IDictionary<int, int> experienceByLevel,
        IDictionary<int, ValueRange> vitalityGrowthByHealth, IDictionary<int, ValueRange> manaGrowthByIntelligence,
        IDictionary<string, StartingEquipmentDefinition> startingEquipmentByClass,
        IDictionary<string, CharacterResourceGrowthDefinition> characterResourceGrowthByClass,
        ref int? baseLevelCompletionExperience)
    {
        var id = Cell(cells, 0);
        if (string.IsNullOrWhiteSpace(id)) return;
        var name = Cell(cells, 1);

        switch (section)
        {
            case DataSection.Races:
                races.Add(new RaceDefinition(id, name, PrimaryAbilities.Zero, ParseRaceTraits(Cell(cells, 2))));
                break;
            case DataSection.CharacterClasses:
                characterClasses.Add(new CharacterClassDefinition(id, name, PrimaryAbilities.Zero, CharacterClassRules.UsesMana(id), Double(cells, 2) ?? 1));
                break;
            case DataSection.Enemies:
                enemies.Add(new EnemyDefinition(id, name, Cell(cells, 2), Integer(cells, 3), Integer(cells, 4),
                    Integer(cells, 5), Integer(cells, 6), Integer(cells, 7) ?? 0, Integer(cells, 8) ?? 1,
                    cells.Skip(9).Take(2).Where(abilityId => !string.IsNullOrWhiteSpace(abilityId)).ToList(),
                    MonsterIds.Bosses.Contains(id), Integer(cells, 11) ?? 5,
                    Math.Clamp(Integer(cells, 12) ?? 0, 0, 3), Math.Clamp(Integer(cells, 13) ?? 2, 0, 4),
                    MonsterIds.Bosses.Contains(id) ? EnemyRank.Boss :
                    MonsterIds.MiniBosses.Contains(id) ? EnemyRank.MiniBoss : EnemyRank.Normal,
                    IsYes(cells, 14)));
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
                    RequiredWeaponStrength(cells, 4, id), IsYes(cells, 5),
                    AllowedClasses(cells, (CharacterClassIds.Harcos, null), (CharacterClassIds.Barbár, null), (CharacterClassIds.Lovag, null),
                        (CharacterClassIds.Tolvaj, 6), (CharacterClassIds.Pap, 7), (CharacterClassIds.Mágus, 8)),
                    Cell(cells, 9), RequiredPrice(cells, 10, id), ParseRarity(cells, 11),
                    EmptyAsNull(Cell(cells, 12)), Integer(cells, 13) ?? 0,
                    PositiveWeight(cells, 14, id, "fegyver")));
                break;
            case DataSection.Armors:
                armors.Add(new ArmorDefinition(id, name, ValueRangeFrom(cells, 2),
                    AllowedClasses(cells, (CharacterClassIds.Harcos, null), (CharacterClassIds.Lovag, null), (CharacterClassIds.Barbár, 3),
                        (CharacterClassIds.Tolvaj, 4), (CharacterClassIds.Pap, 5), (CharacterClassIds.Mágus, 6)), Cell(cells, 7), RequiredPrice(cells, 8, id),
                    ParseRarity(cells, 9), EmptyAsNull(Cell(cells, 10)), Integer(cells, 11) ?? 0,
                    PositiveWeight(cells, 12, id, "páncél")));
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
                    Integer(cells, 8) ?? 0, MagicItemAllowedClasses(cells, 9, ParseMagicItemKind(cells, 2), EmptyAsNull(Cell(cells, 6))), Cell(cells, 10), Integer(cells, 11) ?? 0));
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
            case DataSection.DoorAttemptRules:
                doorAttemptRuleValues[id] = Integer(cells, 1) ??
                    throw new InvalidOperationException($"A(z) '{id}' ajtópróba-paraméter értéke egész szám legyen.");
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
            case DataSection.Npcs:
                npcs.Add(new NpcDefinition(id, name, Cell(cells, 2),
                    EnumValue<NpcDisposition>(cells, 3), EnumValue<NpcWorldBehavior>(cells, 4),
                    IsYes(cells, 5), IsYes(cells, 6), EmptyAsNull(Cell(cells, 7)), EmptyAsNull(Cell(cells, 8))));
                break;
            case DataSection.UniqueNpcCharacters:
                uniqueNpcCharacters.Add(new UniqueNpcCharacterDefinition(id,
                    Math.Max(1, Integer(cells, 1) ?? 1),
                    new PrimaryAbilities(Integer(cells, 2) ?? 1, Integer(cells, 3) ?? 1,
                        Integer(cells, 4) ?? 1, Integer(cells, 5) ?? 1),
                    AdaptableAbilityBonus(Cell(cells, 6)),
                    Math.Clamp(Integer(cells, 7) ?? 1, 1, 15),
                    Math.Clamp(Integer(cells, 8) ?? 1, 1, 15),
                    EnumValue<ConsoleColor>(cells, 9), EnumValue<NpcBehavior>(cells, 10),
                    EmptyAsNull(Cell(cells, 11)), SplitIds(Cell(cells, 12)),
                    EmptyAsNull(Cell(cells, 13)), EmptyAsNull(Cell(cells, 14)),
                    EmptyAsNull(Cell(cells, 15)), SplitIds(Cell(cells, 16)), SplitIds(Cell(cells, 17)),
                    SplitIds(Cell(cells, 18))));
                break;
            case DataSection.CharacterResourceGrowth:
                characterResourceGrowthByClass[id] = new CharacterResourceGrowthDefinition(id,
                    Integer(cells, 1) ?? 0, Integer(cells, 2) ?? 0,
                    Integer(cells, 3) ?? 100);
                break;
            case DataSection.NpcEncounters:
                npcEncounters.Add(new NpcEncounterDefinition(id, Cell(cells, 1),
                    Integer(cells, 2) ?? 1, Integer(cells, 3) ?? 6, Integer(cells, 4) ?? 14,
                    EmptyAsNull(Cell(cells, 5))));
                break;
            case DataSection.NpcDialogues:
                npcDialogues.Add(new NpcDialogueDefinition(id, Cell(cells, 1),
                    Math.Clamp(Integer(cells, 2) ?? 0, 0, 10), Math.Clamp(Integer(cells, 3) ?? 10, 0, 10),
                    Cell(cells, 4)));
                break;
            case DataSection.NpcQuests:
                npcQuests.Add(new NpcQuestDefinition(id, Cell(cells, 1), EnumValue<NpcQuestType>(cells, 2),
                    Cell(cells, 3), Math.Max(1, Integer(cells, 4) ?? 1),
                    Math.Max(0, Integer(cells, 5) ?? 0), Cell(cells, 6), Cell(cells, 7),
                    EmptyAsNull(Cell(cells, 8)), Math.Max(0, Integer(cells, 9) ?? 0),
                    Math.Clamp(Integer(cells, 10) ?? 1, 0, 5), EmptyAsNull(Cell(cells, 11))));
                break;
            case DataSection.NpcStoryChoices:
                npcStoryChoices.Add(new NpcStoryChoiceDefinition(id, Cell(cells, 1), Cell(cells, 2),
                    Cell(cells, 3), Math.Max(1, Integer(cells, 4) ?? 1), Cell(cells, 5),
                    Integer(cells, 6) ?? 0, Cell(cells, 7), Cell(cells, 8),
                    EnumValue<NpcStoryAction>(cells, 9), EmptyAsNull(Cell(cells, 10)), IsYes(cells, 11),
                    Math.Clamp(Integer(cells, 12) ?? 0, 0, 10),
                    Math.Clamp(Integer(cells, 13) ?? 10, 0, 10)));
                break;
            case DataSection.PartySituations:
                partySituations.Add(new PartySituationDefinition(id, name));
                break;
            case DataSection.PartyRemarks:
                partyRemarks.Add(new PartyRemarkDefinition(id, Cell(cells, 1), Cell(cells, 2),
                    Cell(cells, 3), Cell(cells, 4), EmptyAsNull(Cell(cells, 5)), IsYes(cells, 6)));
                break;
            case DataSection.InnNames:
                if (string.IsNullOrWhiteSpace(name))
                    throw new InvalidOperationException("A fogadó neve nem lehet üres.");
                if (innNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"A(z) '{name}' fogadónév duplikált.");
                innNames.Add(name);
                break;
            case DataSection.InnRumors:
                var rumorText = string.Join(", ", cells.Skip(1)).Trim();
                if (string.IsNullOrWhiteSpace(rumorText))
                    throw new InvalidOperationException("A pletyka szövege nem lehet üres.");
                innRumors.Add(new InnRumorDefinition(id, rumorText));
                break;
            case DataSection.Traps:
                var symbolText = Cell(cells, 2);
                if (string.IsNullOrWhiteSpace(symbolText))
                    throw new InvalidOperationException($"A(z) '{id}' csapda jele hiányzik.");
                var minimumDamage = Integer(cells, 7) ?? 0;
                var maximumDamage = Integer(cells, 8) ?? 0;
                if (minimumDamage < 0 || maximumDamage < minimumDamage)
                    throw new InvalidOperationException($"A(z) '{id}' csapda sebzéstartománya érvénytelen.");
                var detectionExperience = Integer(cells, 10) ?? -1;
                var disarmExperience = Integer(cells, 11) ?? -1;
                if (detectionExperience < 0 || disarmExperience < 0)
                    throw new InvalidOperationException($"A(z) '{id}' csapda XP-jutalma hiányzik vagy érvénytelen.");
                traps.Add(new TrapDefinition(id, name, Rune.GetRuneAt(symbolText, 0),
                    Enum.TryParse<TrapEffect>(Cell(cells, 3), true, out var trapEffect) ? trapEffect
                        : throw new InvalidOperationException($"A(z) '{id}' csapda hatása ismeretlen."),
                    Math.Max(1, Integer(cells, 4) ?? 1), Math.Max(1, Integer(cells, 5) ?? 1),
                    Math.Max(1, Integer(cells, 6) ?? 1), minimumDamage, maximumDamage,
                    Math.Clamp(Integer(cells, 9) ?? 0, 0, 100), detectionExperience, disarmExperience,
                    Cell(cells, 12)));
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

    private static int PositiveWeight(string[] cells, int index, string id, string itemType)
    {
        if (string.IsNullOrWhiteSpace(Cell(cells, index))) return 1;
        return Integer(cells, index) is > 0 and var weight
            ? weight
            : throw new InvalidOperationException($"A(z) '{id}' {itemType} súlya pozitív egész szám legyen.");
    }

    private static int RequiredWeaponStrength(string[] cells, int index, string id) =>
        Integer(cells, index) is >= 1 and <= 13 and var strength
            ? strength
            : throw new InvalidOperationException(
                $"A(z) '{id}' fegyver MinimumErő értékének 1 és 13 közé kell esnie.");

    private static ItemRarity ParseRarity(string[] cells, int index) => Normalize(Cell(cells, index)) switch
    {
        "" or "sima" or "normal" => ItemRarity.Normal,
        "varazs" or "magic" => ItemRarity.Magic,
        "legendas" or "legendary" => ItemRarity.Legendary,
        _ => throw new InvalidOperationException($"Ismeretlen tárgyritkaság: '{Cell(cells, index)}'.")
    };

    private static ItemRarity RequiredItemRarity(string[] cells, int index, string id, string fieldName)
    {
        var normalized = Normalize(Cell(cells, index));
        if (normalized is not ("sima" or "normal" or "varazs" or "magic" or "legendas" or "legendary"))
            throw new InvalidOperationException($"A(z) '{id}' {fieldName} mezője ismeretlen: '{Cell(cells, index)}'.");
        return ParseRarity(cells, index);
    }

    private static ConsumableEffect ParseConsumableEffect(string[] cells, int index)
    {
        var value = Cell(cells, index);
        if (string.IsNullOrWhiteSpace(value)) return ConsumableEffect.None;
        return Enum.TryParse<ConsumableEffect>(value, true, out var effect)
            ? effect
            : throw new InvalidOperationException($"Ismeretlen fogyaszthatótárgy-hatás: '{value}'.");
    }

    private static void ValidateTrapConfigurations(IReadOnlyCollection<TrapDefinition> traps)
    {
        var knownIds = traps.Select(trap => trap.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var level = 1; level <= MazeLevelConfigurations.FinalLevel; level++)
        {
            var configuration = MazeLevelConfigurations.Get(level);
            if (configuration.TrapCount.Minimum < 0 ||
                configuration.TrapCount.Maximum < configuration.TrapCount.Minimum)
                throw new InvalidOperationException($"A(z) {level}. szint csapdadarabszáma érvénytelen.");
            var unknown = configuration.TrapIds.FirstOrDefault(id => !knownIds.Contains(id));
            if (unknown is not null)
                throw new InvalidOperationException(
                    $"A(z) {level}. szint ismeretlen csapdára hivatkozik: '{unknown}'.");
        }
    }

    private static void ValidateQuestRoomEncounters(IReadOnlyCollection<EnemyDefinition> enemies,
        IReadOnlyCollection<MiscItemDefinition> items)
    {
        var enemyIds = enemies.Select(value => value.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var itemIds = items.Select(value => value.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var level = 1; level <= MazeLevelConfigurations.FinalLevel; level++)
            Validate(MazeLevelConfigurations.Get(level), $"{level}. pálya");
        foreach (var configuration in QuestLocationConfigurations.All)
            Validate(configuration, $"'{configuration.Name}' küldetéshelyszín");

        void Validate(MazeLevelConfiguration configuration, string location)
        {
            foreach (var encounter in configuration.QuestRoomEnemyEncounters)
            if (!configuration.QuestRoomIds.Concat(configuration.BossRoomIds).Contains(encounter.RoomId,
                    StringComparer.OrdinalIgnoreCase) || !enemyIds.Contains(encounter.EnemyId) || encounter.Count < 1 ||
                encounter.GuaranteedItemId is { } itemId && !itemIds.Contains(itemId))
                throw new InvalidDataException($"A(z) {location} quest room ellenfél-konfigurációja érvénytelen.");
        }
    }

    private static void ValidateNpcData(IReadOnlyCollection<NpcDefinition> npcs,
        IReadOnlyCollection<UniqueNpcCharacterDefinition> uniqueNpcCharacters,
        IReadOnlyCollection<NpcEncounterDefinition> encounters,
        IReadOnlyCollection<NpcDialogueDefinition> dialogues,
        IReadOnlyCollection<NpcStoryChoiceDefinition> storyChoices,
        IReadOnlyCollection<NpcQuestDefinition> quests,
        IReadOnlyCollection<RaceDefinition> races, IReadOnlyCollection<CharacterClassDefinition> classes,
        IReadOnlyCollection<EnemyDefinition> enemies, IReadOnlyCollection<MonsterAbilityDefinition> monsterAbilities,
        IReadOnlyCollection<MiscItemDefinition> items, IReadOnlyCollection<WeaponDefinition> weapons,
        IReadOnlyCollection<ArmorDefinition> armors, IReadOnlyCollection<MagicItemDefinition> magicItems,
        IReadOnlyCollection<PerkDefinition> perks)
    {
        var npcIds = npcs.Select(npc => npc.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var storyIds = npcs.Where(npc => npc.StoryId is not null).Select(npc => npc.StoryId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var classIds = classes.Select(characterClass => characterClass.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var raceIds = races.Select(race => race.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var npc in npcs)
        {
            if (!classIds.Contains(npc.CharacterClassId))
                throw new InvalidDataException($"A(z) '{npc.Id}' NPC ismeretlen kasztra hivatkozik: '{npc.CharacterClassId}'.");
            if (npc.RaceId is { } raceId && !raceIds.Contains(raceId))
                throw new InvalidDataException($"A(z) '{npc.Id}' NPC ismeretlen fajra hivatkozik: '{raceId}'.");
        }
        var buildWeaponIds = weapons.Select(value => value.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var buildArmorIds = armors.Select(value => value.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var buildMagicItemIds = magicItems.Select(value => value.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var buildItemIds = items.Select(value => value.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var buildPerkIds = perks.Select(value => value.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var build in uniqueNpcCharacters)
        {
            var npc = npcs.FirstOrDefault(value => string.Equals(value.Id, build.NpcId, StringComparison.OrdinalIgnoreCase));
            if (npc is null || !npc.Unique || npc.RaceId is null)
                throw new InvalidDataException($"A(z) '{build.NpcId}' karakterlaphoz fajt tartalmazó egyedi NPC szükséges.");
            if (build.Color == ConsoleColor.White)
                throw new InvalidDataException($"A(z) '{build.NpcId}' egyedi world-NPC karakterszíne nem lehet fehér.");
            if (build.FirstWeaponId is { } first && !buildWeaponIds.Contains(first) ||
                build.SecondWeaponId is { } second && !buildWeaponIds.Contains(second) ||
                build.ArmorId is { } armor && !buildArmorIds.Contains(armor) ||
                build.MagicItemIds.Any(value => !buildMagicItemIds.Contains(value)) ||
                build.BackpackItemIds.Any(value => !buildItemIds.Contains(value)) ||
                build.PerkIds.Any(value => !buildPerkIds.Contains(value)) ||
                build.WeaponProficiencyIds.Any(value => WeaponFamilies.Find(value) is null))
                throw new InvalidDataException($"A(z) '{build.NpcId}' egyedi karakterlap ismeretlen felszerelésre vagy tehetségre hivatkozik.");
        }
        var missingBuild = npcs.FirstOrDefault(npc => npc.Unique &&
            uniqueNpcCharacters.All(build => !string.Equals(build.NpcId, npc.Id, StringComparison.OrdinalIgnoreCase)));
        if (missingBuild is not null)
            throw new InvalidDataException($"A(z) '{missingBuild.Id}' egyedi NPC-hez hiányzik a #Egyedi NPC karakterlap bejegyzés.");
        foreach (var reference in encounters.Select(value => (value.Id, value.NpcId))
                     .Concat(dialogues.Select(value => (value.Id, value.NpcId)))
                     .Concat(quests.Select(value => (value.Id, value.NpcId))))
            if (!npcIds.Contains(reference.NpcId))
                throw new InvalidDataException($"A(z) '{reference.Id}' bejegyzés ismeretlen NPC-re hivatkozik: '{reference.NpcId}'.");
        foreach (var encounter in encounters)
        {
            if (encounter.MazeLevel is < 1 or > MazeLevelConfigurations.FinalLevel ||
                encounter.MinimumDistance < 1 || encounter.MaximumDistance < encounter.MinimumDistance)
                throw new InvalidDataException($"A(z) '{encounter.Id}' NPC-találkozás pálya- vagy távolságadata érvénytelen.");
            if (encounter.QuestRoomId is { } questRoomId && !MazeLevelConfigurations.Get(encounter.MazeLevel)
                    .QuestRoomIds.Contains(questRoomId, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException($"A(z) '{encounter.Id}' NPC-találkozás ismeretlen quest roomra hivatkozik: '{questRoomId}'.");
        }
        foreach (var dialogue in dialogues)
            if (dialogue.MinimumFriendliness > dialogue.MaximumFriendliness)
                throw new InvalidDataException($"A(z) '{dialogue.Id}' NPC-párbeszéd viszonytartománya érvénytelen.");
        foreach (var choice in storyChoices)
            if (!storyIds.Contains(choice.StoryId) || string.IsNullOrWhiteSpace(choice.StateId) ||
                string.IsNullOrWhiteSpace(choice.Prompt) || string.IsNullOrWhiteSpace(choice.Text) ||
                string.IsNullOrWhiteSpace(choice.NextStateId) ||
                choice.MinimumFriendliness > choice.MaximumFriendliness)
                throw new InvalidDataException($"A(z) '{choice.Id}' történeti választás érvénytelen.");
        var questIds = quests.Select(quest => quest.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var choice in storyChoices)
        {
            if (choice.ContinueConversation && !storyChoices.Any(candidate =>
                    string.Equals(candidate.StoryId, choice.StoryId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(candidate.StateId, choice.NextStateId, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException(
                    $"A(z) '{choice.Id}' folytatódó történeti választása hiányzó csomópontra mutat: '{choice.NextStateId}'.");
            if (choice.Action == NpcStoryAction.ActivateQuest &&
                (choice.ActionParameter is null || !questIds.Contains(choice.ActionParameter)) ||
                choice.Action == NpcStoryAction.TravelToLocation &&
                !string.Equals(choice.ActionParameter, QuestLocationConfigurations.RodericMalrec,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"A(z) '{choice.Id}' történeti hatása érvénytelen.");
        }
        var enemyIds = enemies.Select(enemy => enemy.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var itemIds = items.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rewardItemIds = itemIds.Concat(weapons.Select(item => item.Id)).Concat(armors.Select(item => item.Id))
            .Concat(magicItems.Select(item => item.Id)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var quest in quests)
        {
            var targetIsValid = quest.Type switch
            {
                NpcQuestType.Kill => enemyIds.Contains(quest.TargetId),
                NpcQuestType.KillWithFollower => monsterAbilities.Any(ability =>
                    string.Equals(ability.Id, quest.TargetId, StringComparison.OrdinalIgnoreCase)),
                NpcQuestType.Collect => itemIds.Contains(quest.TargetId),
                NpcQuestType.Explore => string.Equals(quest.TargetId, "EXIT", StringComparison.OrdinalIgnoreCase),
                NpcQuestType.Escort => string.Equals(quest.TargetId, "EXIT", StringComparison.OrdinalIgnoreCase),
                NpcQuestType.Disarm or NpcQuestType.OpenChest =>
                    string.Equals(quest.TargetId, "ANY", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
            if (!targetIsValid)
                throw new InvalidDataException($"A(z) '{quest.Id}' küldetés célpontja nem található: '{quest.TargetId}'.");
            if (quest.RewardItemId is { } rewardItemId && !rewardItemIds.Contains(rewardItemId))
                throw new InvalidDataException($"A(z) '{quest.Id}' küldetés jutalomtárgya nem található: '{rewardItemId}'.");
            if (quest.RewardItemId is null && quest.RewardItemCount != 0)
                throw new InvalidDataException($"A(z) '{quest.Id}' küldetés jutalomdarabszámához nincs tárgy megadva.");
        }
    }

    private static void ValidatePartyRemarks(IReadOnlyCollection<PartySituationDefinition> situations,
        IReadOnlyCollection<PartyRemarkDefinition> remarks, IReadOnlyCollection<RaceDefinition> races,
        IReadOnlyCollection<CharacterClassDefinition> classes)
    {
        var situationIds = situations.Select(value => value.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var raceIds = races.Select(value => value.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var classIds = classes.Select(value => value.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[]
                 {
                     PartySituationIds.EnemySpotted, PartySituationIds.BattleStarted, PartySituationIds.BattleWon,
                     PartySituationIds.PartyMemberDied, PartySituationIds.Hungry, PartySituationIds.Injured,
                     PartySituationIds.TreasureChestFound, PartySituationIds.Resting, PartySituationIds.Thirsty
                 })
            if (!situationIds.Contains(required))
                throw new InvalidDataException($"A #Szituációk fejezetből hiányzik a(z) '{required}' szituáció.");
        foreach (var remark in remarks)
        {
            if (!situationIds.Contains(remark.SituationId))
                throw new InvalidDataException($"A(z) '{remark.Id}' megjegyzés ismeretlen szituációra hivatkozik.");
            if (remark.CharacterName is null &&
                (!raceIds.Contains(remark.RaceId) || !classIds.Contains(remark.CharacterClassId)))
                throw new InvalidDataException($"A(z) '{remark.Id}' megjegyzés ismeretlen fajra vagy osztályra hivatkozik.");
            if (remark.CharacterName is not null &&
                (string.IsNullOrWhiteSpace(remark.CharacterName) || remark.RaceId.Length > 0 ||
                 remark.CharacterClassId.Length > 0))
                throw new InvalidDataException($"A(z) '{remark.Id}' egyedi megjegyzés karakterneve vagy üres faj-/osztálymezője hibás.");
            if (string.IsNullOrWhiteSpace(remark.Text))
                throw new InvalidDataException($"A(z) '{remark.Id}' megjegyzés szövege nem lehet üres.");
        }
        foreach (var situation in situations)
        foreach (var race in races)
        foreach (var characterClass in classes)
            if (!remarks.Any(remark =>
                    remark.CharacterName is null &&
                    string.Equals(remark.SituationId, situation.Id, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(remark.RaceId, race.Id, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(remark.CharacterClassId, characterClass.Id, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"A(z) '{situation.Id}' szituációhoz nincs megjegyzés a(z) " +
                                               $"'{race.Id}' faj és '{characterClass.Id}' osztály párosához.");
    }

    private static void ValidateCharacterResourceGrowth(
        IReadOnlyCollection<CharacterClassDefinition> characterClasses,
        IReadOnlyDictionary<string, CharacterResourceGrowthDefinition> growthByClass)
    {
        foreach (var characterClass in characterClasses)
            if (!growthByClass.ContainsKey(characterClass.Id))
                throw new InvalidDataException(
                    $"A #Osztály erőforrás-növekedés fejezetből hiányzik a(z) '{characterClass.Id}' osztály.");
        foreach (var growth in growthByClass.Values)
        {
            if (growth.ManaPercentage is < 0 or > 200)
                throw new InvalidDataException($"A(z) '{growth.Id}' osztály mannaszázaléka 0 és 200 közé kell essen.");
            if (growth.VitalityModifier is < -20 or > 20 || growth.ManaModifier is < -20 or > 20)
                throw new InvalidDataException($"A(z) '{growth.Id}' osztály erőforrás-módosítója -20 és 20 közé kell essen.");
        }
    }

    private static void ValidateRequiredCoreData(IReadOnlyCollection<RaceDefinition> races,
        IReadOnlyCollection<CharacterClassDefinition> characterClasses,
        IReadOnlyCollection<EnemyDefinition> enemies, IReadOnlyCollection<AbilityDefinition> abilities,
        IReadOnlyDictionary<string, PrimaryAbilities> raceBonuses,
        IReadOnlyDictionary<string, PrimaryAbilities> classMinimums,
        IReadOnlyDictionary<string, StartingEquipmentDefinition> startingEquipmentByClass,
        IReadOnlyDictionary<int, int> minimumVitalityByHealth,
        IReadOnlyDictionary<int, int> minimumManaByIntelligence,
        IReadOnlyDictionary<int, int> experienceByLevel,
        IReadOnlyDictionary<int, ValueRange> vitalityGrowthByHealth,
        IReadOnlyDictionary<int, ValueRange> manaGrowthByIntelligence)
    {
        if (races.Count == 0) throw new InvalidDataException("A #Fajok fejezet üres vagy hiányzik.");
        if (characterClasses.Count == 0) throw new InvalidDataException("Az #Osztályok fejezet üres vagy hiányzik.");
        if (enemies.Count == 0) throw new InvalidDataException("Az #Ellenségek fejezet üres vagy hiányzik.");
        if (abilities.Count == 0) throw new InvalidDataException("A #Képességek fejezet üres vagy hiányzik.");

        ValidateReferences("#Faji képességbónuszok", races.Select(race => race.Id), raceBonuses.Keys);
        ValidateReferences("#Osztály képességminimumok", characterClasses.Select(value => value.Id), classMinimums.Keys);
        ValidateReferences("#Osztály kezdőfelszerelés", characterClasses.Select(value => value.Id),
            startingEquipmentByClass.Keys);
        ValidateThresholds("#Egészség által adott életerő minimum", minimumVitalityByHealth.Keys, 1, 13);
        ValidateThresholds("#Intelligencia által adott manna minimum", minimumManaByIntelligence.Keys, 1, 13);
        ValidateThresholds("#Szintlépés életerő növekedés", vitalityGrowthByHealth.Keys, 1, 13);
        ValidateThresholds("#Szintlépés manna növekedés", manaGrowthByIntelligence.Keys, 1, 13);
        ValidateThresholds("#Szintlépések", experienceByLevel.Keys, 1, 30);
    }

    private static void ValidateReferences(string section, IEnumerable<string> expectedIds,
        IEnumerable<string> actualIds)
    {
        var expected = expectedIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actual = actualIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = expected.Except(actual, StringComparer.OrdinalIgnoreCase).Order().ToArray();
        var unknown = actual.Except(expected, StringComparer.OrdinalIgnoreCase).Order().ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException($"A(z) {section} fejezetből hiányzó azonosítók: {string.Join(", ", missing)}.");
        if (unknown.Length > 0)
            throw new InvalidDataException($"A(z) {section} fejezet ismeretlen azonosítói: {string.Join(", ", unknown)}.");
    }

    private static void ValidateThresholds(string section, IEnumerable<int> actualKeys, int minimum, int maximum)
    {
        var expected = Enumerable.Range(minimum, maximum - minimum + 1).ToHashSet();
        var actual = actualKeys.ToHashSet();
        var missing = expected.Except(actual).Order().ToArray();
        var extra = actual.Except(expected).Order().ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException($"A(z) {section} fejezetből hiányzó értékek: {string.Join(", ", missing)}.");
        if (extra.Length > 0)
            throw new InvalidDataException($"A(z) {section} fejezet tartományon kívüli értékei: {string.Join(", ", extra)}.");
    }

    private static void ValidateUniqueIds(params (string Section, IEnumerable<string> Ids)[] catalogs)
    {
        foreach (var (section, ids) in catalogs)
        {
            var duplicates = ids.Where(id => !string.IsNullOrWhiteSpace(id))
                .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1).Select(group => group.Key).Order().ToArray();
            if (duplicates.Length > 0)
                throw new InvalidDataException($"A(z) #{section} fejezetben ismétlődő azonosítók vannak: {string.Join(", ", duplicates)}.");
        }
    }

    private static MagicItemKind ParseMagicItemKind(string[] cells, int index) => Normalize(Cell(cells, index)) switch
    {
        "amulett" => MagicItemKind.Amulet,
        "varazspalca" => MagicItemKind.Wand,
        "varazstekercs" => MagicItemKind.Scroll,
        "gyuru" or "varazsgyuru" => MagicItemKind.Ring,
        _ => throw new InvalidOperationException($"Ismeretlen varázstárgytípus: '{Cell(cells, index)}'.")
    };

    private static MagicItemEffect ParseMagicItemEffect(string[] cells, int index)
    {
        var value = Cell(cells, index);
        if (string.IsNullOrWhiteSpace(value)) return MagicItemEffect.None;
        return Enum.TryParse<MagicItemEffect>(value, true, out var effect)
            ? effect
            : throw new InvalidOperationException($"Ismeretlen varázstárgyhatás: '{value}'.");
    }

    private static IReadOnlySet<string> MagicItemAllowedClasses(string[] cells, int mageOnlyIndex, MagicItemKind kind, string? spellId) => kind switch
    {
        MagicItemKind.Wand => new HashSet<string>([CharacterClassIds.Harcos, CharacterClassIds.Barbár, CharacterClassIds.Lovag,
            CharacterClassIds.Tolvaj, CharacterClassIds.Pap, CharacterClassIds.Mágus], StringComparer.OrdinalIgnoreCase),
        MagicItemKind.Scroll when spellId?.StartsWith('P') == true =>
            new HashSet<string>([CharacterClassIds.Pap, CharacterClassIds.Lovag, CharacterClassIds.Mágus], StringComparer.OrdinalIgnoreCase),
        MagicItemKind.Scroll => new HashSet<string>([CharacterClassIds.Mágus], StringComparer.OrdinalIgnoreCase),
        _ when IsYes(cells, mageOnlyIndex) => new HashSet<string>([CharacterClassIds.Mágus], StringComparer.OrdinalIgnoreCase),
        _ => new HashSet<string>([CharacterClassIds.Harcos, CharacterClassIds.Barbár, CharacterClassIds.Lovag,
            CharacterClassIds.Tolvaj, CharacterClassIds.Pap, CharacterClassIds.Mágus], StringComparer.OrdinalIgnoreCase)
    };

    private static void ValidateMagicItems(IEnumerable<MagicItemDefinition> magicItems, IReadOnlyCollection<SpellDefinition> spells)
    {
        var spellsById = spells.ToDictionary(spell => spell.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var item in magicItems)
        {
            if (item.SpellId is { } spellId && !spellsById.ContainsKey(spellId))
                throw new InvalidOperationException($"A(z) '{item.Id}' varázstárgy ismeretlen varázslatra hivatkozik: '{spellId}'.");
            if (item.Kind == MagicItemKind.Wand && (item.SpellId is null || item.MaximumCharges <= 1))
                throw new InvalidOperationException($"A(z) '{item.Id}' varázspálcának több töltetű varázslatot kell tartalmaznia.");
            if (item.Kind == MagicItemKind.Scroll && (item.SpellId is null || item.MaximumCharges != 1))
                throw new InvalidOperationException($"A(z) '{item.Id}' varázstekercsnek egy töltetűnek és varázslathoz kötöttnek kell lennie.");
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
            var expectedSchoolCount = school == SpellSchool.Arcane ? 26 : 25;
            if (schoolSpells.Count != expectedSchoolCount)
                throw new InvalidOperationException($"A(z) {school} iskolához pontosan {expectedSchoolCount} varázslat szükséges; jelenleg {schoolSpells.Count} található.");
            for (var level = 1; level <= 5; level++)
            {
                var count = schoolSpells.Count(spell => spell.Level == level);
                var expectedLevelCount = school == SpellSchool.Arcane && level == 1 ? 6 : 5;
                if (count != expectedLevelCount)
                    throw new InvalidOperationException($"A(z) {school} iskola {level}. szintjén pontosan {expectedLevelCount} varázslat szükséges; jelenleg {count} található.");
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
        foreach (var spell in spells)
            if (!effects.Any(effect => string.Equals(effect.SpellId, spell.Id, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"A(z) '{spell.Id}' varázslathoz legalább egy #Varázshatások sor szükséges.");
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

    private static RaceTraits ParseRaceTraits(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException("A faj Tulajdonság mezője nem lehet üres.");
        var result = RaceTraits.None;
        foreach (var name in value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<RaceTraits>(name, true, out var trait) || trait == RaceTraits.None)
                throw new InvalidDataException($"Ismeretlen faji tulajdonság: {name}.");
            result |= trait;
        }
        return result;
    }

    private static MonsterAbilityEffect ParseMonsterAbilityEffect(string[] cells, int index) =>
        Enum.TryParse<MonsterAbilityEffect>(Cell(cells, index), true, out var effect)
            ? effect
            : throw new InvalidOperationException($"Ismeretlen szörnyképesség-hatás: '{Cell(cells, index)}'.");

    private static void ValidateEnemies(IEnumerable<EnemyDefinition> enemies,
        IReadOnlyCollection<MonsterAbilityDefinition> monsterAbilities)
    {
        var abilityIds = monsterAbilities.Select(ability => ability.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var enemy in enemies)
        {
            if (enemy.StrengthTier is < 1 or > 5)
                throw new InvalidOperationException($"A(z) '{enemy.Id}' szörny erősségi szintje csak 1 és 5 közötti lehet.");
            if (enemy.VisionRange is < 1 or > 8)
                throw new InvalidOperationException($"A(z) '{enemy.Id}' szörny látótávja csak 1 és 8 közötti lehet.");
            if (enemy.AbilityIds.Count > 2)
                throw new InvalidOperationException($"A(z) '{enemy.Id}' szörny legfeljebb két képességgel rendelkezhet.");
            if (enemy.CanSleep && enemy.AbilityIds.Contains(MonsterAbilityIds.Undead,
                    StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException($"A(z) '{enemy.Id}' élőholt szörny nem lehet alvásképes.");
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
            Required("IntelligenciaPontBónusz"), Required("LádaFőnyereményEsély"),
            Required("LádaFőnyereménySzorzó"));
        if (rules.KeyChancePercent is < 0 or > 100 || rules.GoldChancePercent is < 0 or > 100 ||
            rules.GoldPerStrengthTier <= 0 || rules.ThiefChanceMultiplierPercent <= 0 ||
            rules.IntelligenceChanceBonusPerPoint < 0 || rules.ChestJackpotChancePercent is < 0 or > 100 ||
            rules.ChestJackpotMultiplier < 1)
            throw new InvalidOperationException("A #Zsákmány paraméterek értékei érvénytelenek.");
        return rules;
    }

    private static DoorAttemptRules CreateDoorAttemptRules(IReadOnlyDictionary<string, int> values)
    {
        int Required(string id) => values.TryGetValue(id, out var value)
            ? value
            : throw new InvalidOperationException($"Hiányzó #Ajtópróba paraméterek érték: '{id}'.");
        var rules = new DoorAttemptRules(Required("ÉlelemMinimum"), Required("ÉlelemMaximum"),
            Required("VízMinimum"), Required("VízMaximum"));
        if (rules.FoodMinimum < 0 || rules.FoodMaximum < rules.FoodMinimum ||
            rules.WaterMinimum < 0 || rules.WaterMaximum < rules.WaterMinimum)
            throw new InvalidOperationException("A #Ajtópróba paraméterek értékei érvénytelenek.");
        return rules;
    }

    private static IReadOnlyList<WeaponDefinition> CreateUpgradedWeapons(
        IReadOnlyCollection<WeaponDefinition> weapons, IReadOnlyCollection<ItemUpgradeDefinition> upgrades)
    {
        var result = weapons.ToList();
        foreach (var weapon in weapons.Where(weapon => weapon.Rarity == ItemRarity.Normal && weapon.Id != "W005"))
        foreach (var upgrade in upgrades)
            result.Add(weapon with
            {
                Id = $"{weapon.Id}-{upgrade.Id}",
                Name = weapon.Name + " " + upgrade.NameSuffix,
                Damage = Increase(weapon.Damage, upgrade.CombatBonus),
                Description = $"{weapon.Description} Mágikus {upgrade.NameSuffix} változat.",
                BasePrice = Math.Max(1, (int)Math.Ceiling(weapon.BasePrice * upgrade.PriceMultiplier) + 500),
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
                BasePrice = Math.Max(1, (int)Math.Ceiling(armor.BasePrice * upgrade.PriceMultiplier) +
                    (armor.Id is "A001" or "A002" ? upgrade.MagicPower * 500 : 500)),
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

    private static string[] ParseCsvLine(string line)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    cell.Append('"');
                    index++;
                }
                else quoted = !quoted;
                continue;
            }
            if (character == ',' && !quoted)
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
                continue;
            }
            cell.Append(character);
        }
        if (quoted) throw new InvalidDataException("Lezáratlan idézőjeles CSV-mező.");
        cells.Add(cell.ToString().Trim());
        return cells.ToArray();
    }

    private static bool IsHeaderRow(string value) => Normalize(value) is "id" or "npcid" or "fajid" or "osztalyid" or
        "szornyid" or "szituacioid" or "egeszseg" or "intelligencia" or "szint";
    private static string Cell(string[] cells, int index) => index < cells.Length ? cells[index] : string.Empty;
    private static string? EmptyAsNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static IReadOnlyList<string> SplitIds(string value) => value.Split('|',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static PrimaryAbilities AdaptableAbilityBonus(string abilityId) => abilityId.Trim().ToUpperInvariant() switch
    {
        "" => PrimaryAbilities.Zero,
        "STR" => new PrimaryAbilities(1, 0, 0, 0),
        "DEX" => new PrimaryAbilities(0, 1, 0, 0),
        "HEA" => new PrimaryAbilities(0, 0, 1, 0),
        "INT" => new PrimaryAbilities(0, 0, 0, 1),
        _ => throw new InvalidOperationException($"Ismeretlen alkalmazkodó képesség: '{abilityId}'.")
    };
    private static int? Integer(string[] cells, int index) => int.TryParse(Cell(cells, index), CultureInfo.InvariantCulture, out var value) ? value : null;
    private static T EnumValue<T>(string[] cells, int index) where T : struct, Enum =>
        Enum.TryParse<T>(Cell(cells, index), ignoreCase: true, out var value) ? value :
            throw new InvalidDataException($"Érvénytelen {typeof(T).Name} érték: '{Cell(cells, index)}'.");
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

    private static bool TryReadSection(string[] cells, int lineNumber, out DataSection section)
    {
        var sectionCell = cells[0];
        if (sectionCell.StartsWith('#'))
        {
            section = ParseSection(sectionCell[1..]);
            if (section == DataSection.None)
                throw new InvalidDataException($"Ismeretlen fejezetcím az adatok.csv {lineNumber}. sorában: '{sectionCell}'.");
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
        "fogadonevek" => DataSection.InnNames,
        "pletykak" => DataSection.InnRumors,
        "csapdak" => DataSection.Traps,
        "npc-k" => DataSection.Npcs,
        "egyedi npc karakterlap" => DataSection.UniqueNpcCharacters,
        "npc talalkozasok" => DataSection.NpcEncounters,
        "npc parbeszedek" => DataSection.NpcDialogues,
        "npc torteneti valasztasok" => DataSection.NpcStoryChoices,
        "npc kuldetesek" => DataSection.NpcQuests,
        "szituaciok" => DataSection.PartySituations,
        "parti megjegyzesek" => DataSection.PartyRemarks,
        "faji kepessegbonuszok" => DataSection.RaceAbilityBonuses,
        "osztaly kepessegminimumok" => DataSection.ClassAbilityMinimums,
        "ero talalati bonusz" => DataSection.StrengthHitBonuses,
        "szorny zsakmany" => DataSection.MonsterLoot,
        "zsakmany parameterek" => DataSection.LootRules,
        "ajtoproba parameterek" => DataSection.DoorAttemptRules,
        "osztaly kezdofelszereles" => DataSection.StartingEquipment,
        "osztaly eroforras-novekedes" => DataSection.CharacterResourceGrowth,
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
        InnNames,
        InnRumors,
        Traps,
        Npcs,
        UniqueNpcCharacters,
        NpcEncounters,
        NpcDialogues,
        NpcStoryChoices,
        NpcQuests,
        PartySituations,
        PartyRemarks,
        RaceAbilityBonuses,
        ClassAbilityMinimums,
        StrengthHitBonuses,
        MonsterLoot,
        LootRules,
        DoorAttemptRules,
        StartingEquipment,
        CharacterResourceGrowth,
        VitalityByHealth,
        ManaByIntelligence,
        LevelExperience,
        VitalityGrowth,
        ManaGrowth,
        LevelCompletionExperience,
        ItemUpgrades
    }
}
