using MazeGame.Data;
using MazeGame.Domain.Magic;

namespace MazeGame.Domain.Characters;

/// <summary>A varázslatok megtanulásának és memorizálásának kasztszabályai.</summary>
public static class SpellcastingRules
{
    public const int StartingSpellCount = 3;

    public static bool TryGetSchool(string characterClassId, out SpellSchool school)
    {
        switch (characterClassId.ToUpperInvariant())
        {
            case "C005": school = SpellSchool.Divine; return true;
            case "C006": school = SpellSchool.Arcane; return true;
            default: school = default; return false;
        }
    }

    public static int MemorizationCapacity(LiveCharacter character) =>
        TryGetSchool(character.CharacterClass.Id, out _)
            ? 2 + character.Abilities.Intelligence / 3 + character.Level / 5
            : 0;

    public static int MaximumSpellLevel(int characterLevel) => characterLevel switch
    {
        >= 20 => 5,
        >= 15 => 4,
        >= 10 => 3,
        >= 5 => 2,
        _ => 1
    };

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
        if (!TryGetSchool(character.CharacterClass.Id, out var school) || character.KnownSpells.Count > 0) return;
        var spells = gameData.GetSpells(school, 1).OrderBy(_ => random.Next()).Take(StartingSpellCount).ToList();
        foreach (var spell in spells) character.LearnSpell(spell);
        character.SetMemorizedSpells(spells);
    }

    public static void LearnAutomaticSpells(LiveCharacter character, GameDataCatalog gameData,
        IEnumerable<LevelUpBonus> levelUps, Random random)
    {
        foreach (var levelUp in levelUps)
        {
            var choices = AvailableUnknownSpells(character, gameData, levelUp.Level);
            if (choices.Count > 0) character.LearnSpell(choices[random.Next(choices.Count)]);
        }
    }
}
