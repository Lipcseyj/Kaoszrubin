namespace KaoszRubin.Application.Quests;

/// <summary>A lezárt küldetés után elérhető beszélgetési állapot. A jutalmat a quest API kezeli.</summary>
public static class RodericStoryProgression
{
    public static string? NextState(string state, RodericQuestApi quests) => state switch
    {
        "PROOF_ACTIVE" when quests.FightingOnTheSameSide.IsCompleted => "PROOF_COMPLETE",
        "INSIGNIAS_ACTIVE" when quests.FallenComradesInsignia.IsCompleted => "CONFESSION",
        "FOLLOWING" when quests.PatriarchsShadows.IsCompleted => "TRUSTED",
        "RELICS_ACTIVE" when quests.OrderRelics.IsCompleted => "RELICS_COMPLETE",
        "MALREC_FIGHT" when quests.OathbreakerKnight.IsCompleted => "MALREC_DEFEATED",
        _ => null
    };
}
