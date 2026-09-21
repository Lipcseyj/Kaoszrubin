namespace KaoszRubin.Domain.Magic;

public enum EnemySpellcastingStyle
{
    Artillery,
    Controller,
    Support,
    BattleMage,
    Necromancer
}

/// <summary>Adatvezérelt varázshasználói beállítás egy ellenségtípushoz.</summary>
public sealed record EnemySpellcasterProfile(
    string EnemyId,
    IReadOnlyList<string> SpellIds,
    int MaximumMana,
    int Intelligence,
    int ManaReservePercent,
    int CastingChancePercent,
    EnemySpellcastingStyle Style,
    int SpellCooldownRounds = 1);
