using KaoszRubin.Application.Quests;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Quests;
using KaoszRubin.World;

namespace KaoszRubin.Infrastructure.Quests;

/// <summary>
/// A quest-rendszer és a konkrét Maze/WorldNpc infrastruktúra közötti adapter.
/// </summary>
public sealed class MazeQuestWorldContext : IQuestWorldContext
{
    private readonly Func<Maze> _getMaze;
    private readonly Func<IItemDefinition, int> _countPartyItem;
    private readonly QuestNpcInstanceRegistry _instanceRegistry;
    private readonly Func<IItemDefinition, int, bool> _tryConsumePartyItem;
    public MazeQuestWorldContext(
        Func<Maze> getMaze,
        Func<IItemDefinition, int> countPartyItem,
        Func<IItemDefinition, int, bool> tryConsumePartyItem,
        QuestNpcInstanceRegistry instanceRegistry)
    {
        ArgumentNullException.ThrowIfNull(getMaze);
        ArgumentNullException.ThrowIfNull(countPartyItem);
        ArgumentNullException.ThrowIfNull(tryConsumePartyItem);
        ArgumentNullException.ThrowIfNull(instanceRegistry);

        _getMaze = getMaze;
        _countPartyItem = countPartyItem;
        _tryConsumePartyItem = tryConsumePartyItem;
        _instanceRegistry = instanceRegistry;
    }

    public QuestNpcInstanceId GetInstanceId(
        WorldNpc npc)
    {
        ArgumentNullException.ThrowIfNull(npc);

        EnsureQuestNpc(npc);

        return _instanceRegistry.GetOrCreate(npc);
    }

    public bool IsNpcParticipatingInCombat(
        QuestNpcId npcId,
        QuestNpcInstanceId instanceId,
        Enemy defeatedEnemy,
        int maximumDistance)
    {
        ArgumentNullException.ThrowIfNull(defeatedEnemy);

        if (maximumDistance < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumDistance));
        }

        var npc =
            FindNpc(
                npcId,
                instanceId);

        if (npc is null)
            return false;

        var avatar = FindTemporaryFollowerAvatar(npc);

        if (avatar is null ||
            !avatar.Character.IsAlive)
        {
            return false;
        }

        return Manhattan(
                avatar.Position,
                defeatedEnemy.Position)
            <= maximumDistance;
    }

    public bool IsNpcAliveAndFollowing(
        QuestNpcId npcId,
        QuestNpcInstanceId instanceId)
    {
        var npc =
            FindNpc(
                npcId,
                instanceId);

        if (npc is null)
            return false;

        var avatar =
            FindTemporaryFollowerAvatar(npc);

        return avatar is not null &&
               avatar.Character.IsAlive &&
               npc.State == WorldNpcState.Following;
    }

    public QuestStoryState GetNpcStoryState(
        QuestNpcId npcId,
        QuestNpcInstanceId instanceId)
    {
        var npc =
            FindNpc(
                npcId,
                instanceId);

        if (npc is null)
            return QuestStoryState.None;

        return LegacyQuestStoryStateMap.TryToQuestStoryState(
            npc.StoryStateId,
            out var state)
            ? state
            : QuestStoryState.None;
    }

    public int CountPartyItem(
        IItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return _countPartyItem(item);
    }

    public bool TryConsumePartyItem(
    IItemDefinition item,
    int amount)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        return _tryConsumePartyItem(
            item,
            amount);
    }

    private WorldNpc? FindNpc(
        QuestNpcId npcId,
        QuestNpcInstanceId instanceId)
    {
        if (npcId == QuestNpcId.None)
        {
            throw new ArgumentException(
                "QuestNpcId.None nem használható NPC kereséséhez.",
                nameof(npcId));
        }

        var matching = EnumerateQuestNpcs()
            .Where(npc =>
                GetQuestNpcId(npc) == npcId)
            .ToArray();

        if (!instanceId.IsNone)
        {
            return matching.FirstOrDefault(
                npc =>
                    _instanceRegistry.GetOrCreate(npc)
                    == instanceId);
        }

        return matching.Length switch
        {
            0 => null,

            1 => matching[0],

            _ => throw new InvalidOperationException(
                $"A(z) '{npcId}' NPC-ből több példány is jelen van, " +
                "ezért QuestNpcInstanceId megadása szükséges.")
        };
    }

    private PartyMemberAvatar? FindTemporaryFollowerAvatar(
        WorldNpc npc)
    {
        return _getMaze()
            .PartyMembers
            .FirstOrDefault(member =>
                ReferenceEquals(
                    member.TemporaryFollower,
                    npc));
    }

    private IEnumerable<WorldNpc> EnumerateQuestNpcs()
    {
        var maze = _getMaze();

        return (IEnumerable<WorldNpc>)maze.WorldNpcs
            .Concat(
                maze.PartyMembers
                    .Where(member =>
                        member.TemporaryFollower is not null)
                    .Select(member =>
                        member.TemporaryFollower!))
            .Where(npc => npc.IsQuestNpc)
            .Distinct(ReferenceEqualityComparer.Instance);
    }

    private static QuestNpcId GetQuestNpcId(
        WorldNpc npc)
    {
        EnsureQuestNpc(npc);

        return LegacyNpcIdMap.ToQuestNpcId(
            npc.DefinitionId);
    }

    private static void EnsureQuestNpc(
        WorldNpc npc)
    {
        if (!npc.IsQuestNpc)
        {
            throw new InvalidOperationException(
                $"A(z) '{npc.DefinitionId}' WorldNpc nem quest NPC.");
        }
    }

    private static int Manhattan(
        Position first,
        Position second)
    {
        return Math.Abs(first.X - second.X) +
               Math.Abs(first.Y - second.Y);
    }
}