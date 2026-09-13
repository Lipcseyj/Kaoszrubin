using KaoszRubin.Application;
using KaoszRubin.Application.Quests;
using KaoszRubin.World;

namespace KaoszRubin.Infrastructure.Quests;

/// <summary>A látható questadó összes futását a managerből másolja, aktiválás és jutalmazás nélkül.</summary>
public sealed class QuestWorldSnapshotProjector(QuestManager manager, MazeQuestWorldContext world)
{
    public WorldNpcQuestData Create(WorldNpc npc)
    {
        var instance = world.GetInstanceId(npc);
        var quests = manager.For(LegacyNpcIdMap.ToQuestNpcId(npc.DefinitionId), instance).GetQuests();
        return new(instance.Value, quests.OrderBy(quest => quest.Id).Select(quest =>
            new WorldQuestSnapshot(quest.Key, quest.State, quest.Progress, quest.RequiredCount, quest.CompletionCount)).ToArray());
    }
}
