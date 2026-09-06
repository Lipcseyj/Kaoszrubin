namespace KaoszRubin.Domain.Characters;

public enum SpellcasterManaFallback { Retreat, SelfBuffAndMelee }

public sealed record NpcSpellcasterCombatProfile(int OffensiveSpellsPerBattle, int MinimumEnemyStrength,
    int FullOffenseEnemyStrength, SpellcasterManaFallback ManaFallback)
{
    public NpcSpellcasterCombatProfile Normalize() => this with
    {
        OffensiveSpellsPerBattle = Math.Clamp(OffensiveSpellsPerBattle, 0, 20),
        MinimumEnemyStrength = Math.Clamp(MinimumEnemyStrength, 0, 100),
        FullOffenseEnemyStrength = Math.Clamp(FullOffenseEnemyStrength,
            Math.Clamp(MinimumEnemyStrength, 0, 100), 100)
    };
}

public sealed record NpcSpellcasterTactics(
    int OffensiveSpellsPerBattle = 2,
    int MinimumEnemyStrength = 8,
    int FullOffenseEnemyStrength = 16,
    SpellcasterManaFallback ManaFallback = SpellcasterManaFallback.Retreat,
    NpcSpellcasterCombatProfile? UnholyProfile = null)
{
    public static NpcSpellcasterTactics Default { get; } = new();

    public static NpcSpellcasterTactics DefaultFor(string characterClassId) => characterClassId switch
    {
        CharacterClassIds.Lovag => new(0, 20, 40, SpellcasterManaFallback.SelfBuffAndMelee),
        CharacterClassIds.Pap => new(1, 15, 30, SpellcasterManaFallback.SelfBuffAndMelee,
            new NpcSpellcasterCombatProfile(5, 5, 15, SpellcasterManaFallback.SelfBuffAndMelee)),
        _ => Default
    };

    public NpcSpellcasterCombatProfile StandardProfile =>
        new(OffensiveSpellsPerBattle, MinimumEnemyStrength, FullOffenseEnemyStrength, ManaFallback);

    public NpcSpellcasterCombatProfile EffectiveProfile(bool hasUnholyEnemy) =>
        (hasUnholyEnemy ? UnholyProfile : null)?.Normalize() ?? StandardProfile.Normalize();

    public NpcSpellcasterTactics Normalize() => this with
    {
        OffensiveSpellsPerBattle = Math.Clamp(OffensiveSpellsPerBattle, 0, 20),
        MinimumEnemyStrength = Math.Clamp(MinimumEnemyStrength, 0, 100),
        FullOffenseEnemyStrength = Math.Clamp(FullOffenseEnemyStrength,
            Math.Clamp(MinimumEnemyStrength, 0, 100), 100),
        UnholyProfile = UnholyProfile?.Normalize()
    };
}

public sealed record NpcSpellcasterTacticsEntry(CharacterId CharacterId, NpcSpellcasterTactics Tactics);
