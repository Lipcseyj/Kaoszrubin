using KaoszRubin.Domain.Quests;
using KaoszRubin.Infrastructure.Quests;
using KaoszRubin.World;

namespace KaoszRubin.Application.Quests;

public sealed record QuestFastTravelOption(QuestKey Key, QuestNpcInstanceId GiverInstanceId,
    string QuestTitle, string QuestGiverName, int NeedCost);

/// <summary>A jelenlegi világban elérhető, leadható futások célzott felkeresése.</summary>
public sealed class QuestTravelService(QuestManager manager, MazeQuestWorldContext world)
{
    public IReadOnlyList<QuestFastTravelOption> BuildOptions(IEnumerable<WorldNpc> destinations,
        Func<WorldNpc, int?> findDistance)
    {
        var options = new List<QuestFastTravelOption>();
        foreach (var npc in destinations.Where(npc => npc.IsQuestNpc && npc.Character.IsAlive))
        {
            var instance = world.GetInstanceId(npc);
            foreach (var quest in manager.GetActiveQuests().Where(quest =>
                ReferenceEquals(world.ResolveNpc(quest.Giver, quest.GiverInstanceId), npc)))
            {
                if (!quest.CanComplete || findDistance(npc) is not { } distance) continue;
                options.Add(new(quest.Key, instance, quest.Title, npc.Character.Name,
                    Math.Clamp((distance * 2 + 19) / 20, 1, 15)));
            }
        }
        return options;
    }

    /// <summary>Végrehajtás előtt újra ellenőrzi a questet, a konkrét NPC-t és az aktuális útvonalat/költséget.</summary>
    public bool TryResolve(QuestFastTravelOption selection, IEnumerable<WorldNpc> destinations,
        Func<WorldNpc, int?> findDistance, out QuestFastTravelOption option, out WorldNpc npc)
    {
        var current = destinations.ToArray();
        option = BuildOptions(current, findDistance).FirstOrDefault(candidate => candidate.Key == selection.Key &&
            candidate.GiverInstanceId == selection.GiverInstanceId)!;
        var giver = option?.GiverInstanceId;
        npc = option is null ? null! : current.Single(candidate => candidate.IsQuestNpc &&
            world.GetInstanceId(candidate) == giver);
        return option is not null;
    }
}
