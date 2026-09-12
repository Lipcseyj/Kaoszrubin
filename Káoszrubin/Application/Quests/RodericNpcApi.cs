using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

public sealed class RodericNpcApi
    : QuestNpcHandle
{
    internal RodericNpcApi(
        QuestManager manager)
        : base(
            manager,
            QuestNpcId.SirRoderic)
    {
        Quests =
            new RodericQuestApi(manager);
    }

    public RodericQuestApi Quests { get; }
}