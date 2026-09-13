using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Application.Quests;

/// <summary>
/// A lezárt játékmeneti művelet után a teljes parti inventoryját egyezteti.
/// Így egy több karakter közötti átadás köztes állapotai nem okoznak hamis visszaesést.
/// A tagságváltozás is készletváltozásnak számít.
/// </summary>
public sealed class QuestInventorySynchronizer(QuestManager manager)
{
    private (LiveCharacter Character, long Revision)[] _previous = [];

    public IReadOnlyList<QuestProgressChange> Synchronize(IEnumerable<LiveCharacter> party)
    {
        var current = party.Select(character => (character, character.InventoryRevision)).ToArray();
        if (_previous.SequenceEqual(current)) return [];
        var changes = manager.SynchronizeCollectQuests();
        _previous = current;
        return changes;
    }
}
