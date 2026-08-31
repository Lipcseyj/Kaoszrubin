using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin.Application;

/// <summary>A karakterlap megjelenítéséhez szükséges, doménobjektumoktól leválasztott adatok.</summary>
public sealed record CharacterSheetSnapshot(string RaceName, string CharacterClassName, int Experience,
    int? NextLevelExperience, PrimaryAbilities Abilities, IReadOnlyList<string> PerkNames,
    IReadOnlyList<string> StatusIcons, bool UsesMana, ConsoleColor Color,
    IReadOnlyList<string>? ClassFeatureUpgradeNames = null,
    IReadOnlyList<string>? WeaponProficiencyNames = null,
    IReadOnlyDictionary<string, int>? WeaponProficiencyRanks = null,
    int VisionRange = CharacterClassRules.BaseVisionRange,
    int NaturalVisionRange = CharacterClassRules.BaseVisionRange,
    IReadOnlyList<VisionModifierSnapshot>? VisionModifiers = null,
    int HearingRange = 4, int DetectionBonus = 0,
    IReadOnlyList<string>? DetailedWeaponProficiencyNames = null);

public sealed record VisionModifierSnapshot(string Name, int Value);
public sealed record MonsterKillSnapshot(string EnemyDefinitionId, int Count);
public sealed record CharacterHistorySnapshot(IReadOnlyList<MonsterKillSnapshot> MonsterKills,
    int? NpcJoinedMazeLevel = null, string? NpcJoinedLocation = null, string? NpcBehavior = null);

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
        IReadOnlyDictionary<int, int> experienceByLevel, int environmentVisionModifier = 0)
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
                ActiveSpellEffectType.VisionBonus when effect.Value < 0 => "🌑",
                ActiveSpellEffectType.VisionBonus => "🔆",
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
                proficiency => (int)proficiency.Rank, StringComparer.OrdinalIgnoreCase),
            CharacterClassRules.VisionRange(character, environmentVisionModifier),
            CharacterClassRules.NaturalVisionRange(character), BuildVisionModifiers(character, environmentVisionModifier),
            CharacterClassRules.HearingRange(character), CharacterClassRules.DetectionBonus(character),
            character.WeaponProficiencies.Select(FormatDetailedWeaponProficiency).ToArray());
    }

    private static IReadOnlyList<VisionModifierSnapshot> BuildVisionModifiers(LiveCharacter character,
        int environmentVisionModifier)
    {
        var result = new List<VisionModifierSnapshot> { new("Alap látótáv", CharacterClassRules.BaseVisionRange) };
        if (CharacterClassRules.IsThief(character.CharacterClass.Id)) result.Add(new("Tolvaj osztály", 2));
        if (character.Race.HasTrait(RaceTraits.KeenSenses)) result.Add(new("Éles érzékek", 1));
        result.AddRange(character.ActiveSpellEffects
            .Where(effect => effect.Type == ActiveSpellEffectType.VisionBonus)
            .Select(effect => new VisionModifierSnapshot(effect.SourceSpellId, effect.Value)));
        if (environmentVisionModifier != 0) result.Add(new("Pálya/környezet", environmentVisionModifier));
        return result;
    }

    private static string FormatDetailedWeaponProficiency(WeaponProficiencyState proficiency)
    {
        var family = WeaponFamilies.Find(proficiency.FamilyId)!;
        return proficiency.Rank == WeaponProficiencyRank.Master
            ? $"{family.Icon} {family.Name} — Mesterfok: {family.TrainedDescription} {family.MasterDescription}"
            : $"{family.Icon} {family.Name} — Jártas: {family.TrainedDescription}";
    }
}
