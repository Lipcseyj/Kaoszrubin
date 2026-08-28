using MazeGame.Data;
using MazeGame.Domain.Magic;
using MazeGame.Domain.Inventory;

namespace MazeGame.Domain.Characters;

/// <summary>A varázslatok megtanulásának és memorizálásának kasztszabályai.</summary>
public static class SpellcastingRules
{
    public const string MageSpellbookItemId = "T021";
    public const string PriestHolySymbolItemId = "T022";
    public const string LegacyHolySymbolItemId = "M003";
    public const string LegacyApprenticeWandItemId = "M004";

    public static bool TryGetSchool(string characterClassId, out SpellSchool school)
    {
        switch (characterClassId.ToUpperInvariant())
        {
            case CharacterClassIds.Pap: school = SpellSchool.Divine; return true;
            case CharacterClassIds.Lovag: school = SpellSchool.Divine; return true;
            case CharacterClassIds.Mágus: school = SpellSchool.Arcane; return true;
            default: school = default; return false;
        }
    }

    public static string? RequiredFocusItemId(string characterClassId) => characterClassId.ToUpperInvariant() switch
    {
        CharacterClassIds.Pap => PriestHolySymbolItemId,
        CharacterClassIds.Lovag => PriestHolySymbolItemId,
        CharacterClassIds.Mágus => MageSpellbookItemId,
        _ => null
    };

    public static int StartingSpellCount(string characterClassId) => characterClassId.ToUpperInvariant() switch
    {
        CharacterClassIds.Pap => 3,
        CharacterClassIds.Lovag => 0,
        CharacterClassIds.Mágus => 3,
        _ => 0
    };

    public static bool CanUseCastingItem(LiveCharacter character, MagicItemDefinition item, SpellDefinition spell) =>
        item.Kind == MagicItemKind.Wand || item.Kind == MagicItemKind.Scroll && spell.School switch
        {
            SpellSchool.Arcane => character.CharacterClass.Id == CharacterClassIds.Mágus,
            SpellSchool.Divine => character.CharacterClass.Id is CharacterClassIds.Pap or CharacterClassIds.Lovag or CharacterClassIds.Mágus,
            _ => false
        };

    public static bool IsSpellcastingFocus(IItemDefinition? item) => item is not null &&
        IsSpellcastingFocusId(item.Id);

    public static bool IsSpellcastingFocusId(string itemId) =>
        string.Equals(itemId, MageSpellbookItemId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(itemId, PriestHolySymbolItemId, StringComparison.OrdinalIgnoreCase);

    public static bool IsLegacyStartingFocus(IItemDefinition item) =>
        IsLegacyStartingFocusId(item.Id);

    public static bool IsLegacyStartingFocusId(string itemIdOrName) =>
        string.Equals(itemIdOrName, LegacyHolySymbolItemId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(itemIdOrName, LegacyApprenticeWandItemId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(itemIdOrName, "Szent szimbólum", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(itemIdOrName, "Tanonc pálcája", StringComparison.OrdinalIgnoreCase);

    public static bool IsRestrictedFromTradingAndGeneration(IItemDefinition item) =>
        IsSpellcastingFocus(item) || IsLegacyStartingFocus(item);

    public static bool HasRequiredFocus(LiveCharacter character) =>
        RequiredFocusItemId(character.CharacterClass.Id) is { } requiredId &&
        character.Backpack[0] is { } focus && string.Equals(focus.Id, requiredId, StringComparison.OrdinalIgnoreCase);

    public static bool GiveRequiredFocus(LiveCharacter character, GameDataCatalog gameData) =>
        RequiredFocusItemId(character.CharacterClass.Id) is not { } focusId ||
        character.SetInventoryItem(InventorySlotKind.Backpack, 0, gameData.GetItem(focusId));

    public static int MemorizationCapacity(LiveCharacter character) =>
        TryGetSchool(character.CharacterClass.Id, out _)
            ? 2 + character.EffectiveAbilities.Intelligence / 3 + character.Level / 5
            : 0;

    public static int MaximumSpellLevel(int characterLevel) => characterLevel switch
    {
        >= 20 => 5,
        >= 15 => 4,
        >= 10 => 3,
        >= 5 => 2,
        _ => 1
    };

    public static int EffectiveManaCost(LiveCharacter character, SpellDefinition spell) =>
        character.NextDivineSpellTriggersJudgment(spell)
            ? 0
            : Math.Max(1, spell.ManaCost - (character.HasPerk(PerkIds.MageArchmage) ? 2 : 0));

    public static IReadOnlyList<SpellDefinition> AvailableUnknownSpells(
        LiveCharacter character, GameDataCatalog gameData, int atCharacterLevel)
    {
        if (!TryGetSchool(character.CharacterClass.Id, out var school)) return [];
        var maximumLevel = MaximumSpellLevel(atCharacterLevel);
        return gameData.Spells.Where(spell => spell.School == school)
            .Where(spell => spell.Level <= maximumLevel &&
                character.KnownSpells.All(known => !string.Equals(known.Id, spell.Id, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(spell => spell.Level).ThenBy(spell => spell.Name).ToList();
    }

    public static void GiveAutomaticStartingSpells(LiveCharacter character, GameDataCatalog gameData, Random random)
    {
        // Only full casters (Priest and Mage) receive starting spellbook memorized spells.
        if (!TryGetSchool(character.CharacterClass.Id, out var school) || character.KnownSpells.Count > 0) return;
        if (!string.Equals(character.CharacterClass.Id, CharacterClassIds.Pap, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(character.CharacterClass.Id, CharacterClassIds.Mágus, StringComparison.OrdinalIgnoreCase)) return;

        var spells = gameData.GetSpells(school, 1).OrderBy(_ => random.Next()).Take(StartingSpellCount(character.CharacterClass.Id)).ToList();
        foreach (var spell in spells) character.LearnSpell(spell);
        character.SetMemorizedSpells(spells);
    }

    public static void LearnAutomaticSpells(LiveCharacter character, GameDataCatalog gameData,
        IEnumerable<LevelUpBonus> levelUps, Random random)
    {
        // Knights learn spells on a custom schedule: level 2, level 5, then every +3 levels thereafter (8,11,14...).
        foreach (var levelUp in levelUps)
        {
            var lvl = levelUp.Level;
            if (string.Equals(character.CharacterClass.Id, CharacterClassIds.Lovag, StringComparison.OrdinalIgnoreCase))
            {
                var shouldLearn = lvl == 2 || (lvl >= 5 && (lvl - 5) % 3 == 0);
                if (!shouldLearn) continue;
            }

            var choices = AvailableUnknownSpells(character, gameData, levelUp.Level);
            if (choices.Count > 0) character.LearnSpell(choices[random.Next(choices.Count)]);
        }
    }
}
