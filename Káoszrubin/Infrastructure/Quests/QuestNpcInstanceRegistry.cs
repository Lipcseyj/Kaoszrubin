using KaoszRubin.Domain.Quests;
using KaoszRubin.World;

namespace KaoszRubin.Infrastructure.Quests;

//Most runtime közben ez már stabil.
//A mentés/betöltéshez később ténylegesen el kell mentenünk az instance ID-t is, de ezért került bele már most a Restore().

/// <summary>
/// Stabil QuestNpcInstanceId értéket rendel az aktuális játék
/// WorldNpc példányaihoz.
///
/// Ugyanaz a WorldNpc objektum mindig ugyanazt az instance ID-t kapja,
/// függetlenül attól, hogy WorldNpc-ként vagy temporary followerré
/// válva van jelen.
/// </summary>
public sealed class QuestNpcInstanceRegistry
{
    private readonly Dictionary<WorldNpc, QuestNpcInstanceId> _ids =
        new(ReferenceEqualityComparer.Instance);

    private int _nextId = 1;

    public QuestNpcInstanceId GetOrCreate(
        WorldNpc npc)
    {
        ArgumentNullException.ThrowIfNull(npc);

        if (_ids.TryGetValue(npc, out var existing))
            return existing;

        var id =
            new QuestNpcInstanceId(_nextId++);

        _ids.Add(npc, id);

        return id;
    }

    /// <summary>
    /// Mentés visszatöltésekor lehetővé teszi egy korábbi
    /// instance ID visszaállítását ugyanahhoz az NPC-hez.
    /// </summary>
    public void Restore(
        WorldNpc npc,
        QuestNpcInstanceId instanceId)
    {
        ArgumentNullException.ThrowIfNull(npc);

        if (instanceId.IsNone)
            throw new ArgumentException(
                "QuestNpcInstanceId.None nem állítható vissza.",
                nameof(instanceId));

        if (_ids.TryGetValue(npc, out var existing))
        {
            if (existing != instanceId)
            {
                throw new InvalidOperationException(
                    $"Az NPC már más instance ID-val rendelkezik: " +
                    $"{existing}.");
            }

            return;
        }

        if (_ids.Values.Contains(instanceId))
        {
            throw new InvalidOperationException(
                $"A(z) '{instanceId}' quest NPC instance ID " +
                "már használatban van.");
        }

        _ids.Add(
            npc,
            instanceId);

        _nextId =
            Math.Max(
                _nextId,
                instanceId.Value + 1);
    }
}