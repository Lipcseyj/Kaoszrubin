namespace KaoszRubin.Domain.Quests;

/// <summary>
/// Meghatározza, hogy egy quest a sikeres teljesítése után
/// újra elérhetővé válhat-e.
/// </summary>
public enum QuestRepeatPolicy
{
    Once = 0,
    Repeatable
}