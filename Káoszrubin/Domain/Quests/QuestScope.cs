namespace KaoszRubin.Domain.Quests;

/// <summary>
/// Meghatározza, hogy egy quest állapota az egész játékhoz,
/// vagy egy konkrét NPC-példányhoz tartozik.
/// </summary>
public enum QuestScope
{
    Global = 0,
    PerNpcInstance
}