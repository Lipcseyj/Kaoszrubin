namespace KaoszRubin.Domain.Quests;

/// <summary>
/// A quest-rendszer számára jelentős NPC történetállapotok.
/// A legacy string értékek ezen a domain-határon túl nem használhatók.
/// </summary>
public enum QuestStoryState
{
    None = 0,

    State1,
    State2,
    State3,

    Trusted,

    // A Sir Malrec harc speciális Roderic-állapota.
    MalrecFight,
    ProofActive,
    InsigniasActive,
    Following,
    MalrecApproach
}
