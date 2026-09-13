using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Domain.Quests;

/// <summary>
/// A quest-rendszer számára releváns játékbeli események alaptípusa.
/// </summary>
public abstract record QuestEvent;

/// <summary>
/// Egy ellenfél vereséget szenvedett.
/// </summary>
public sealed record EnemyKilledEvent(
    Enemy Enemy)
    : QuestEvent;

/// <summary>
/// Egy adott tárgy mennyisége megváltozott a parti inventoryjában.
/// Nem tartalmaz deltát, mert a collect quest progressét mindig
/// az inventory aktuális állapotából számítjuk újra.
/// </summary>
public sealed record InventoryItemCountChangedEvent(
    IItemDefinition Item)
    : QuestEvent;

/// <summary>
/// A parti sikeresen hatástalanított egy csapdát.
/// </summary>
public sealed record TrapDisarmedEvent
    : QuestEvent;
// Ha később lesznek konkrét csapda-típusok, akkor a TrapDefinition paramétert lehetne hozzáadni. Hasonlóan a chesteknél is.
//public sealed record TrapDisarmedEvent(
//    TrapDefinition Trap)
//    : QuestEvent;

/// <summary>
/// A parti sikeresen kinyitott egy kincsesládát.
/// </summary>
public sealed record ChestOpenedEvent
    : QuestEvent;

/// <summary>
/// A parti elért egy quest szempontjából jelentős helyet.
/// </summary>
public sealed record LocationReachedEvent(
    QuestLocation Location)
    : QuestEvent;

/// <summary>A hely láthatóvá vált; kísérő célba érését nem jelenti.</summary>
public sealed record LocationDiscoveredEvent(QuestLocation Location) : QuestEvent;

/// <summary>A konkrét kísérővel a parti végrehajtja a helyszín elérését.</summary>
public sealed record NpcReachedLocationEvent(
    QuestNpcId NpcId, QuestNpcInstanceId InstanceId, QuestLocation Location) : QuestEvent;
