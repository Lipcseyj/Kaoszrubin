using MazeGame.Domain.Characters;
using MazeGame.Domain.Magic;

namespace MazeGame.Application;

/// <summary>A karakterlap megjelenítéséhez szükséges, doménobjektumoktól leválasztott adatok.</summary>
public sealed record CharacterSheetSnapshot(string RaceName, string CharacterClassName, int Experience,
    int? NextLevelExperience, PrimaryAbilities Abilities, IReadOnlyList<string> PerkNames,
    IReadOnlyList<string> StatusIcons, bool UsesMana, ConsoleColor Color,
    IReadOnlyList<string>? ClassFeatureUpgradeNames = null,
    IReadOnlyList<string>? WeaponProficiencyNames = null,
    IReadOnlyDictionary<string, int>? WeaponProficiencyRanks = null);

public static class SpellInfoSnapshotProjector
{
    public static SpellInfoSnapshot Create(LiveCharacter character) => new(
        character.Backpack.FirstOrDefault(item => SpellcastingRules.IsSpellcastingFocus(item))?.Name ?? "HIÁNYZIK",
        character.MemorizationCapacity,
        character.KnownSpells.OrderBy(spell => spell.Level).ThenBy(spell => spell.Name).Select(spell =>
            new KnownSpellSnapshot(spell.Id, spell.Name, spell.Level,
                SpellcastingRules.EffectiveManaCost(character, spell), spell.TargetType, spell.Description,
                character.MemorizedSpells.Any(candidate => candidate.Id == spell.Id),
                character.QuickSpells.ToList().FindIndex(candidate => candidate?.Id == spell.Id) is var index && index >= 0
                    ? index : null)).ToArray());
}

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
            character.Experience, character.GetNextLevelExperience(experienceByLevel), character.EffectiveAbilities,
            character.Perks.Select(perk => perk.Name)
                .Concat(character.Specialization is { } specialization ? [$"{specialization.Name} specializáció"] : [])
                .ToArray(), icons, character.UsesMana, character.Color,
            character.ClassFeatureUpgrades.Select(upgrade => upgrade.Name).ToArray(),
            character.WeaponProficiencies.Select(proficiency =>
            {
                var family = WeaponFamilies.Find(proficiency.FamilyId)!;
                return $"{family.Icon}{(proficiency.Rank == WeaponProficiencyRank.Master ? "M" : "J")}";
            }).ToArray(),
            character.WeaponProficiencies.ToDictionary(proficiency => proficiency.FamilyId,
                proficiency => (int)proficiency.Rank, StringComparer.OrdinalIgnoreCase));
    }
}
