using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

/// <summary>
/// A játékbeli quest-eseményeket aktív questek
/// előrehaladásává alakítja.
/// </summary>
public sealed class QuestProgressEngine
{
    private readonly QuestCatalog _catalog;
    private readonly QuestStateStore _stateStore;
    private readonly IQuestWorldContext _world;

    public QuestProgressEngine(
        QuestCatalog catalog,
        QuestStateStore stateStore,
        IQuestWorldContext world)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(world);

        _catalog = catalog;
        _stateStore = stateStore;
        _world = world;
    }

    /// <summary>
    /// Feldolgoz egy quest szempontjából releváns játékbeli eseményt.
    /// Csak az aktív questeket vizsgálja.
    /// </summary>
    public IReadOnlyList<QuestProgressChange> Process(
        QuestEvent questEvent)
    {
        ArgumentNullException.ThrowIfNull(questEvent);

        if (questEvent is InventoryItemCountChangedEvent inventoryChanged)
        {
            return SynchronizeCollectObjectives(
                inventoryChanged.Item);
        }

        var changes = new List<QuestProgressChange>();

        foreach (var state in _stateStore.GetActive().ToArray())
        {
            var definition = _catalog.Get(state.QuestId);

            var amount = GetProgressAmount(
                definition,
                state,
                questEvent);

            if (amount <= 0)
                continue;

            var previousProgress = state.Progress;
            var previousState = state.State;

            state.AddProgress(
                amount,
                definition.Objective.RequiredCount);

            if (state.Progress == previousProgress &&
                state.State == previousState)
            {
                continue;
            }

            changes.Add(CreateChange(
                state,
                previousProgress,
                previousState));
        }

        return changes;
    }

    public IReadOnlyList<QuestProgressChange> SynchronizeCollectObjectives()
    {
        return SynchronizeCollectObjectives(
            changedItem: null);
    }

    private IReadOnlyList<QuestProgressChange> SynchronizeCollectObjectives(IItemDefinition? changedItem)
    {
        var changes = new List<QuestProgressChange>();

        foreach (var state in _stateStore.GetInProgress())
        {
            var definition = _catalog.Get(state.QuestId);

            if (definition.Objective is not
                QuestObjective.CollectItem objective)
            {
                continue;
            }

            if (changedItem is not null &&
                !Equals(objective.Item, changedItem))
            {
                continue;
            }

            var currentAmount =
                _world.CountPartyItem(objective.Item);

            var previousProgress = state.Progress;
            var previousState = state.State;

            state.SynchronizeProgress(
                currentAmount,
                objective.RequiredCount);

            if (state.Progress == previousProgress &&
                state.State == previousState)
            {
                continue;
            }

            changes.Add(CreateChange(
                state,
                previousProgress,
                previousState));
        }

        return changes;
    }

    private static QuestProgressChange CreateChange(
    QuestRuntimeState state,
    int previousProgress,
    QuestState previousState)
    {
        return new QuestProgressChange(
            state.QuestId,
            state.GiverInstanceId,
            previousProgress,
            state.Progress,
            previousState,
            state.State);
    }

    private int GetProgressAmount(
        QuestDefinition quest,
        QuestRuntimeState state,
        QuestEvent questEvent)
    {
        return (quest.Objective, questEvent) switch
        {
            (
                QuestObjective.KillEnemy objective,
                EnemyKilledEvent occurred
            ) => MatchKillEnemy(
                objective,
                state,
                occurred),

            (
                QuestObjective.KillEnemyWithTraits objective,
                EnemyKilledEvent occurred
            ) => MatchKillEnemyWithTraits(
                objective,
                state,
                occurred),

            (
                QuestObjective.ExploreLocation objective,
                LocationReachedEvent occurred
            ) => MatchExploreLocation(
                objective,
                occurred),

            (
                QuestObjective.DisarmTraps,
                TrapDisarmedEvent
            ) => 1,

            (
                QuestObjective.OpenChests,
                ChestOpenedEvent
            ) => 1,

            (
                QuestObjective.EscortNpc objective,
                LocationReachedEvent occurred
            ) => MatchEscort(
                objective,
                state,
                occurred),

            _ => 0
        };
    }

    private int MatchKillEnemy(
        QuestObjective.KillEnemy objective,
        QuestRuntimeState state,
        EnemyKilledEvent occurred)
    {
        if (!Equals(
                objective.Enemy,
                occurred.Enemy.Definition))
        {
            return 0;
        }

        return RequiredFollowerParticipated(
            objective.RequiredFollower,
            state,
            occurred.Enemy) ? 1 : 0;
    }

    private int MatchKillEnemyWithTraits(
        QuestObjective.KillEnemyWithTraits objective,
        QuestRuntimeState state,
        EnemyKilledEvent occurred)
    {
        if (!HasAllTraits(occurred.Enemy.Definition, objective.RequiredTraits))
        {
            return 0;
        }

        return RequiredFollowerParticipated(
            objective.RequiredFollower,
            state,
            occurred.Enemy) ? 1 : 0;
    }

    private static bool HasAllTraits(
    EnemyDefinition enemy,
    EnemyTraits requiredTraits)
    {
        return (enemy.Traits & requiredTraits) == requiredTraits;
    }

    private static int MatchExploreLocation(
        QuestObjective.ExploreLocation objective,
        LocationReachedEvent occurred)
    {
        return objective.Location == occurred.Location
            ? 1
            : 0;
    }

    private int MatchEscort(
        QuestObjective.EscortNpc objective,
        QuestRuntimeState state,
        LocationReachedEvent occurred)
    {
        if (objective.Destination != occurred.Location)
            return 0;

        var instanceId = ResolveNpcInstanceId(
            objective.Npc,
            state);

        return _world.IsNpcAliveAndFollowing(
            objective.Npc,
            instanceId)
            ? 1
            : 0;
    }

    private bool RequiredFollowerParticipated(
        QuestNpcId? requiredFollower,
        QuestRuntimeState state,
        Enemy defeatedEnemy)
    {
        if (requiredFollower is null)
            return true;

        var instanceId = ResolveNpcInstanceId(
            requiredFollower.Value,
            state);

        return _world.IsNpcParticipatingInCombat(
            requiredFollower.Value,
            instanceId,
            defeatedEnemy);
    }

    private QuestNpcInstanceId ResolveNpcInstanceId(
        QuestNpcId npcId,
        QuestRuntimeState state)
    {
        var quest = _catalog.Get(state.QuestId);

        // Ha a követelmény éppen a questadó NPC-re vonatkozik,
        // PerNpcInstance quest esetén tudjuk a konkrét példányát.
        if (npcId == quest.Giver &&
            !state.GiverInstanceId.IsNone)
        {
            return state.GiverInstanceId;
        }

        // Unique NPC-knél (Roderic, Elira) a world context
        // QuestNpcId alapján is fel tudja majd oldani az egyetlen példányt.
        return QuestNpcInstanceId.None;
    }
}