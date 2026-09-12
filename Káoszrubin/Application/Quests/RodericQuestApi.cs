using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

public sealed class RodericQuestApi
{
    private readonly QuestManager _manager;

    internal RodericQuestApi(
        QuestManager manager)
    {
        _manager = manager;
    }

    public QuestHandle FallenComradesInsignia =>
        _manager.GetQuest(
            QuestId.RodericFallenComradesInsignia);

    public QuestHandle SharedBladeTrial =>
        _manager.GetQuest(
            QuestId.RodericSharedBladeTrial);

    public QuestHandle OathbreakerKnight =>
        _manager.GetQuest(
            QuestId.RodericOathbreakerKnight);

    public QuestHandle TheDeadAreNotPrey =>
        _manager.GetQuest(
            QuestId.RodericTheDeadAreNotPrey);
}