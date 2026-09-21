using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Data;

/// <summary>
/// A véletlen tesztkarakterek, fogadói zsoldosok és világ-NPC-k közös generáló felülete.
/// A szerepkör szerinti metódusnevek szándékosan különülnek el: a tesztgenerálás segédeszközöket
/// is adhat, míg a játékvilágban megjelenő karakterek szintarányos felszerelést kapnak.
/// </summary>
public sealed partial class RandomCharacterGenerator
{
    /// <summary>A fix felszerelési szintekhez tartozó legnagyobb megengedett varázserő.</summary>
    public enum EquipmentTier
    {
        Basic = 0,
        LesserMagic = 1,
        GreaterMagic = 2,
        Masterwork = 3
    }

    /// <summary>Meghatározza, hogyan válasszon felszerelést a generátor.</summary>
    public enum EquipmentSelection
    {
        KeepStartingEquipment,
        FixedTier,
        ScaleWithLevel,
        UnrestrictedRandom
    }

    /// <summary>
    /// Felszerelés-generálási beállítások. A <see cref="Scaled"/> való NPC-khez és zsoldosokhoz,
    /// a <see cref="Starting"/> és <see cref="Random"/> elsősorban reprodukálható tesztekhez való.
    /// </summary>
    public sealed record EquipmentOptions(
        EquipmentSelection Selection,
        EquipmentTier Tier = EquipmentTier.Basic,
        int TierVariance = 1,
        bool IncludeMagicItems = true,
        bool AddSupplies = true,
        bool AllowLegendary = false)
    {
        /// <summary>Megtartja a kaszt alapfegyverzetét.</summary>
        public static EquipmentOptions Starting { get; } = new(
            EquipmentSelection.KeepStartingEquipment, AddSupplies: false, IncludeMagicItems: false);

        /// <summary>
        /// A karakter szintjéből számít felső felszerelési tiert, azon belül pedig véletlenül
        /// egy legfeljebb <paramref name="tierVariance"/> fokkal gyengébb minőséget választ.
        /// </summary>
        public static EquipmentOptions Scaled(int tierVariance = 1, bool includeMagicItems = true,
            bool addSupplies = true) => new(EquipmentSelection.ScaleWithLevel,
            TierVariance: ValidateVariance(tierVariance), IncludeMagicItems: includeMagicItems,
            AddSupplies: addSupplies);

        /// <summary>Fix felszerelési tierből, opcionális lefelé irányuló szórással választ.</summary>
        public static EquipmentOptions AtTier(EquipmentTier tier, int tierVariance = 0,
            bool includeMagicItems = true, bool addSupplies = true) => new(
            EquipmentSelection.FixedTier, tier, ValidateVariance(tierVariance), includeMagicItems,
            addSupplies);

        /// <summary>Korlátlanul véletlen, fejlesztői tartalompróbákhoz való felszerelés.</summary>
        public static EquipmentOptions Random(bool allowLegendary = true) => new(
            EquipmentSelection.UnrestrictedRandom, EquipmentTier.Masterwork, 3, true, true,
            allowLegendary);

        private static int ValidateVariance(int value) => value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "A tierszórás nem lehet negatív.");
    }

    /// <summary>
    /// Harci tesztkaraktert készít pontos szinten. Alapértelmezésben megtartja a kaszt
    /// alapfelszerelését, így a harci szabálytesztek nem függnek véletlen tárgyaktól.
    /// </summary>
    public LiveCharacter GenerateCombatTestCharacter(CharacterClassDefinition characterClass, int targetLevel,
        IReadOnlyCollection<string> usedNames, EquipmentOptions? equipment = null)
    {
        var character = CreateLevelOneCore(characterClass, usedNames);
        PrepareExistingCharacterForTest(character, targetLevel);
        ApplyEquipment(character, equipment ?? EquipmentOptions.Starting);
        return character;
    }

    /// <summary>
    /// Fejlesztői kézi próbához készít 2–30. szintű karaktert, korlátlanul véletlen
    /// felszereléssel, fejlesztői varázstárgyakkal és kulccsal.
    /// </summary>
    public LiveCharacter GenerateDevelopmentCharacter(CharacterClassDefinition characterClass,
        IReadOnlyCollection<string> usedNames)
    {
        var character = CreateLevelOneCore(characterClass, usedNames);
        RaiseToRandomLevel(character);
        CompleteGeneratedProgression(character);
        ApplyEquipment(character, EquipmentOptions.Random());
        EquipDevelopmentMagicItems(character);
        GiveDevelopmentKey(character);
        return character;
    }

    /// <summary>Első szintű, véletlen kasztú fejlesztői tesztkaraktert készít.</summary>
    public LiveCharacter GenerateLevelOneTestCharacter(IReadOnlyCollection<string> usedNames) =>
        CreateLevelOneCore(usedNames);

    /// <summary>
    /// Fogadói zsoldost készít. Az 5. pálya utáni szintkorlátozást a
    /// <see cref="RecruitmentRules"/> alkalmazza; a felszerelés alapból a kapott szinthez skálázódik.
    /// </summary>
    public LiveCharacter GenerateMercenary(CharacterClassDefinition characterClass, int leaderLevel,
        IReadOnlyCollection<string> usedNames, int completedLevel = 0, bool allowWhiteColor = true,
        EquipmentOptions? equipment = null)
    {
        var maximumLevel = Math.Max(1, _gameData.ExperienceByLevel.Keys.DefaultIfEmpty(1).Max());
        var targetLevel = RecruitmentRules.UsesLowerLevelCandidates(completedLevel)
            ? RecruitmentRules.LowerRecruitLevel(leaderLevel, _random.Next(10, 41))
            : Math.Clamp(leaderLevel + _random.Next(-3, 4), 1, maximumLevel);
        return GenerateNpcCore(characterClass, targetLevel, usedNames, allowWhiteColor,
            equipment ?? EquipmentOptions.Scaled());
    }

    /// <summary>
    /// Világ-NPC-t készít pontos szinten. A karakter színe alapból nem lehet fehér, felszerelése
    /// pedig a szintjének megfelelő felső tier alatt enyhén véletlen minőségű.
    /// </summary>
    public LiveCharacter GenerateWorldNpc(CharacterClassDefinition characterClass, int targetLevel,
        IReadOnlyCollection<string> usedNames, EquipmentOptions? equipment = null,
        bool allowWhiteColor = false) => GenerateNpcCore(characterClass, targetLevel, usedNames,
        allowWhiteColor, equipment ?? EquipmentOptions.Scaled());

    /// <summary>
    /// Adott nevű és fajú világ-NPC-t készít pontos szinten. A CSV-ben teljes karakterlappal
    /// definiált egyedi NPC-ket továbbra is a <see cref="UniqueNpcCharacterFactory"/> készíti.
    /// </summary>
    public LiveCharacter GenerateUniqueWorldNpc(string name, RaceDefinition race,
        CharacterClassDefinition characterClass, int targetLevel, EquipmentOptions? equipment = null)
    {
        for (var attempt = 0; attempt < 2_000; attempt++)
        {
            var adaptableAbilityBonus = RandomAdaptableAbilityBonus(race);
            var rolledAbilities = RollAbilities();
            var finalAbilities = (rolledAbilities + race.AbilityBonuses + adaptableAbilityBonus).Clamp(1, 13);
            if (!finalAbilities.MeetsMinimum(characterClass.MinimumAbilities)) continue;
            var character = LiveCharacterFactory.Create(name, race, characterClass, rolledAbilities,
                _random.Next(1, 16), _random.Next(1, 16), _gameData,
                RandomCharacterColor(), adaptableAbilityBonus);
            InitializeGeneratedCharacter(character, targetLevel);
            ApplyEquipment(character, equipment ?? EquipmentOptions.Scaled());
            return character;
        }
        throw new InvalidOperationException($"A(z) {name} egyedi NPC nem generálható a megadott fajjal és kaszttal.");
    }

    /// <summary>Meglévő karaktert készít fel fejlesztői harci tesztre a megadott szinten.</summary>
    public void PrepareExistingCharacterForTest(LiveCharacter character, int targetLevel)
    {
        if (targetLevel < character.Level)
            throw new ArgumentOutOfRangeException(nameof(targetLevel),
                "A fejlesztői teszt nem csökkentheti egy karakter szintjét.");
        RaiseToLevel(character, targetLevel);
        CompleteGeneratedProgression(character);
        if (character.IsSpellcaster)
            character.SetMemorizedSpells(character.KnownSpells.OrderBy(_ => _random.Next())
                .Take(character.MemorizationCapacity));
        character.RestoreVitality(Math.Max(0, character.MaximumVitality - character.CurrentVitality));
        character.RestoreMana(Math.Max(0, character.MaximumMana - character.CurrentMana));
    }
}
