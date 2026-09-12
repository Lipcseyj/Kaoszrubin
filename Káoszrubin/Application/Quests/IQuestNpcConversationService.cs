using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

/// <summary>
/// Questadó NPC-vel való beszélgetés elindításának absztrakciója.
///
/// A QuestManager nem tudja, hogyan működik a Game,
/// a renderer vagy a konkrét NPC-interakciós UI.
/// </summary>
public interface IQuestNpcConversationService
{
    void StartConversation(QuestNpcId npcId, QuestNpcInstanceId instanceId = default);
}