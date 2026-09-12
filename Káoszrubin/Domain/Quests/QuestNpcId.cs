namespace KaoszRubin.Domain.Quests;

/// <summary>
/// A küldetésrendszerben részt vevő NPC-definíciók
/// erősen tipizált azonosítója.
///
/// Nem egy konkrét világpéldányt azonosít, hanem az NPC típusát.
/// </summary>
public enum QuestNpcId
{
    None = 0,

    WanderingHerbalist,
    MonsterHunter,
    TreasureHunter,
    WanderingBard,
    RatHunter,
    KoboldFugitive,
    WoundedBorderGuard,
    SpiritSeerHermit,
    OrcDeserter,
    CaveAlchemist,
    PrisonerScout,
    GraveKeeper,
    DragonResearcher,
    SwampFerryman,
    CrystalEngineer,
    NightRefugee,
    UnchainedDemonHunter,
    ChaosPilgrim,
    GiantHunter,

    EliraSilverbranch,
    SirRoderic
}