using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

public sealed class EliraNpcApi
    : QuestNpcHandle
{
    internal EliraNpcApi(
        QuestManager manager)
        : base(
            manager,
            QuestNpcId.EliraSilverbranch)
    {
        Quests =
            new EliraQuestApi(manager);
    }

    public EliraQuestApi Quests { get; }
}