using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

public sealed record QuestJournalNotifications(IReadOnlyList<QuestJournalEntrySnapshot> Offers,
    IReadOnlyList<QuestJournalEntrySnapshot> Completions);

/// <summary>A vendég egy kapcsolati munkamenetében futásonként egyszer jelez. Az első kép történetét nem játssza vissza.</summary>
public sealed class QuestJournalNotificationTracker
{
    private readonly HashSet<QuestKey> _known = [];
    private readonly HashSet<QuestKey> _completed = [];
    private bool _initialized;

    public QuestJournalNotifications Observe(IReadOnlyList<QuestJournalEntrySnapshot> quests)
    {
        var offers = new List<QuestJournalEntrySnapshot>();
        var completions = new List<QuestJournalEntrySnapshot>();
        foreach (var quest in quests)
        {
            if (_known.Add(quest.Key) && _initialized && quest.Status == QuestJournalStatus.Active)
                offers.Add(quest);
            if (quest.Status == QuestJournalStatus.Completed && _completed.Add(quest.Key) && _initialized)
                completions.Add(quest);
        }
        _initialized = true;
        return new(offers, completions);
    }
}
