using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

public sealed class EliraQuestApi
{
    private readonly QuestManager _manager;

    internal EliraQuestApi(
        QuestManager manager)
    {
        _manager = manager;
    }

    public QuestHandle Rescue =>
        _manager.GetQuest(
            QuestId.EliraRescue);

    public QuestHandle TornBandage =>
        _manager.GetQuest(
            QuestId.EliraTornBandage);

    public QuestHandle OnOurTrail =>
        _manager.GetQuest(
            QuestId.EliraOnOurTrail);
}