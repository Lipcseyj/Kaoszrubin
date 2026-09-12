using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

/// <summary>
/// Egy questadó NPC kényelmes publikus API-ja.
/// </summary>
public class QuestNpcHandle
{
    protected QuestManager Manager { get; }

    public QuestNpcId Id { get; }

    public QuestNpcInstanceId InstanceId { get; }

    internal QuestNpcHandle(
        QuestManager manager,
        QuestNpcId id,
        QuestNpcInstanceId instanceId = default)
    {
        ArgumentNullException.ThrowIfNull(manager);

        if (id == QuestNpcId.None)
        {
            throw new ArgumentException(
                "QuestNpcId.None nem használható.",
                nameof(id));
        }

        Manager = manager;
        Id = id;
        InstanceId = instanceId;
    }

    public IReadOnlyList<QuestHandle> GetQuests()
    {
        return Manager.GetQuestsForNpc(
            Id,
            InstanceId);
    }

    public bool AreAllQuestsResolved =>
        GetQuests()
            .All(quest => quest.IsResolved);

    public bool HasUnresolvedQuests =>
        !AreAllQuestsResolved;

    public IReadOnlyList<QuestHandle> GetActiveQuests()
    {
        return Manager.GetActiveQuestsForNpc(
            Id,
            InstanceId);
    }

    public IReadOnlyList<QuestHandle> GetAvailableQuests()
    {
        return Manager.GetAvailableQuestsForNpc(
            Id,
            InstanceId);
    }

    public QuestHandle GetQuest(
        QuestId questId)
    {
        var quest =
            Manager.GetQuest(
                questId,
                InstanceId);

        if (quest.Giver != Id)
        {
            throw new InvalidOperationException(
                $"A(z) '{questId}' quest nem a(z) " +
                $"'{Id}' NPC-hez tartozik.");
        }

        return quest;
    }

    public IReadOnlyList<QuestHandle>
        ActivateAvailableQuests()
    {
        return Manager.ActivateAvailableForNpc(
            Id,
            InstanceId);
    }

    public void StartConversation()
    {
        Manager.StartConversation(
            Id,
            InstanceId);
    }

    public override string ToString() =>
        InstanceId.IsNone
            ? Id.ToString()
            : $"{Id} [{InstanceId}]";
}