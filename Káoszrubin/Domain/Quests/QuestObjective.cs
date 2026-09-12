using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Domain.Quests;

/// <summary>
/// Egy küldetés teljesítéséhez szükséges cél.
/// Az objective típusa egyben meghatározza azt is,
/// hogy milyen játékbeli esemény képes előrehaladást okozni.
/// </summary>
public abstract record QuestObjective(int RequiredCount)
{
    /// <summary>
    /// Egy konkrét tárgy összegyűjtése.
    /// </summary>
    public sealed record CollectItem(
        IItemDefinition Item,
        int Count)
        : QuestObjective(Count);

    /// <summary>
    /// Ellenfelek legyőzése.
    /// Opcionálisan egy meghatározott NPC-nek is a csapatban kell lennie.
    /// </summary>public sealed record KillEnemy(
    public sealed record KillEnemy(
     EnemyDefinition Enemy,
     int Count,
     QuestNpcId? RequiredFollower = null)
     : QuestObjective(Count);

    /// <summary>
    /// Egy ellenfél-kategóriába tartozó ellenfelek legyőzése.
    /// Opcionálisan egy meghatározott NPC-nek is a csapatban kell lennie.
    /// </summary>
    public sealed record KillEnemyWithTraits(
        EnemyTraits RequiredTraits,
        int Count,
        QuestNpcId? RequiredFollower = null)
        : QuestObjective(Count);

    /// <summary>
    /// Egy meghatározott hely felfedezése.
    /// </summary>
    public sealed record ExploreLocation(
        QuestLocation Location)
        : QuestObjective(1);

    /// <summary>
    /// Tetszőleges csapdák hatástalanítása.
    /// </summary>
    public sealed record DisarmTraps(
        int Count)
        : QuestObjective(Count);

    /// <summary>
    /// Tetszőleges kincsesládák kinyitása.
    /// </summary>
    public sealed record OpenChests(
        int Count)
        : QuestObjective(Count);

    /// <summary>
    /// Egy quest NPC élve eljuttatása egy meghatározott helyre.
    /// </summary>
    public sealed record EscortNpc(
        QuestNpcId Npc,
        QuestLocation Destination)
        : QuestObjective(1);
}

/// <summary>
/// Questek által használható, erősen tipizált speciális helyek.
/// </summary>
public enum QuestLocation
{
    Exit
}