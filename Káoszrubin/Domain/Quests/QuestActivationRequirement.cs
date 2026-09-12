namespace KaoszRubin.Domain.Quests;

/// <summary>
/// Egy quest aktiválásához szükséges feltétel.
/// </summary>
public abstract record QuestActivationRequirement
{
    /// <summary>
    /// A questadó NPC-nek egy meghatározott történetállapotban kell lennie.
    /// </summary>
    public sealed record StoryStateEquals(
        QuestStoryState RequiredState)
        : QuestActivationRequirement;
}