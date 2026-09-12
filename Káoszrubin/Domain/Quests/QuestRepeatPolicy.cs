namespace KaoszRubin.Domain.Quests;

/// <summary>
/// Meghatározza, hogy egy küldetés milyen gyakorisággal teljesíthető.
/// </summary>
public enum QuestRepeatPolicy
{
    OncePerGame = 0,
    OncePerNpcInstance,
    Repeatable
}