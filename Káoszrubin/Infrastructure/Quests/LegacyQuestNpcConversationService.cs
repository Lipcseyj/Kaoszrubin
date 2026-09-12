using KaoszRubin.Application.Quests;
using KaoszRubin.Domain.Quests;
using KaoszRubin.World;

namespace KaoszRubin.Infrastructure.Quests;

/// <summary>
/// Az új QuestManager beszélgetés API-ját összeköti
/// a jelenlegi Game NPC-interakciós kódjával.
/// </summary>
public sealed class LegacyQuestNpcConversationService
    : IQuestNpcConversationService
{
    private readonly MazeQuestWorldContext _world;
    private readonly Action<WorldNpc> _startConversation;

    public LegacyQuestNpcConversationService(MazeQuestWorldContext world, Action<WorldNpc> startConversation)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(startConversation);

        _world = world;
        _startConversation = startConversation;
    }

    public void StartConversation(
        QuestNpcId npcId,
        QuestNpcInstanceId instanceId = default)
    {
        var npc =
            _world.ResolveNpc(
                npcId,
                instanceId);

        if (npc is null)
        {
            throw new InvalidOperationException(
                $"A(z) '{npcId}' NPC jelenleg nem található " +
                "a játékvilágban.");
        }

        if (!npc.CanStartConversation)
        {
            throw new InvalidOperationException(
                $"A(z) '{npcId}' NPC-vel jelenleg " +
                "nem indítható beszélgetés.");
        }

        _startConversation(npc);
    }
}