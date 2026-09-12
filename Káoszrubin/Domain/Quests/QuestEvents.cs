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
/// A parti egy adott tárgyból meghatározott mennyiséget szerzett.
/// </summary>
public sealed record ItemObtainedEvent
    : QuestEvent
{
    public IItemDefinition Item { get; }
    public int Amount { get; }

    public ItemObtainedEvent(
        IItemDefinition item,
        int amount = 1)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (amount <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "A megszerzett tárgy mennyiségének pozitívnak kell lennie.");

        Item = item;
        Amount = amount;
    }
}

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