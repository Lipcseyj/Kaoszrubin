using MazeGame.Domain.Characters;
using MazeGame.Domain.Magic;

namespace MazeGame.Application;

/// <summary>A karakterlap megjelenítéséhez szükséges, doménobjektumoktól leválasztott adatok.</summary>
public sealed record CharacterSheetSnapshot(string RaceName, string CharacterClassName, int Experience,
    int? NextLevelExperience, PrimaryAbilities Abilities, IReadOnlyList<string> PerkNames,
    IReadOnlyList<string> StatusIcons, bool UsesMana, ConsoleColor Color);

public static class CharacterSheetSnapshotProjector
{
    public static CharacterSheetSnapshot Create(LiveCharacter character,
        IReadOnlyDictionary<int, int> experienceByLevel)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(experienceByLevel);
        var icons = character.Statuses.Select(status => status.Icon)
            .Concat(character.ActiveSpellEffects.Select(effect => effect.Type switch
            {
                ActiveSpellEffectType.Invisibility => "👻",
                ActiveSpellEffectType.DefenseBonus => "🛡️",
                ActiveSpellEffectType.PhysicalReduction => "🪨",
                ActiveSpellEffectType.BleedingImmunity => "🩸🚫",
                ActiveSpellEffectType.HitBonus => "🎯",
                ActiveSpellEffectType.DamageBonus => "⚔️✨",
                ActiveSpellEffectType.InitiativeBonus => "⚡",
                ActiveSpellEffectType.ProtectionFromEvil => "✝️🛡️",
                ActiveSpellEffectType.GuardianAngel => "👼",
                ActiveSpellEffectType.Sanctuary => "⛪",
                _ => "✨"
            })).ToArray();
        return new CharacterSheetSnapshot(character.Race.Name, character.CharacterClass.Name,
            character.Experience, character.GetNextLevelExperience(experienceByLevel), character.Abilities,
            character.Perks.Select(perk => perk.Name).ToArray(), icons, character.UsesMana, character.Color);
    }
}
