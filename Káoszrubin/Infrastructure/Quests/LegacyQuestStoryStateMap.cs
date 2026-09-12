using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Infrastructure.Quests;

/// <summary>
/// Legacy NPC story-state azonosítók typed quest story-state
/// értékekké alakítása.
/// </summary>
public static class LegacyQuestStoryStateMap
{
    public static QuestStoryState ToQuestStoryState(
        string legacyState)
    {
        if (TryToQuestStoryState(
                legacyState,
                out var state))
        {
            return state;
        }

        throw new InvalidDataException(
            $"Ismeretlen legacy NPC story state: '{legacyState}'.");
    }

    public static bool TryToQuestStoryState(string? legacyState, out QuestStoryState state)
    {
        if (string.IsNullOrWhiteSpace(legacyState))
        {
            state = QuestStoryState.None;
            return false;
        }

        state = legacyState.ToUpperInvariant() switch
        {
            "1" => QuestStoryState.State1,
            "2" => QuestStoryState.State2,
            "3" => QuestStoryState.State3,
            "TRUSTED" => QuestStoryState.Trusted,
            "MALREC_FIGHT" => QuestStoryState.MalrecFight,
            _ => QuestStoryState.None
        };

        return state != QuestStoryState.None;
    }
}