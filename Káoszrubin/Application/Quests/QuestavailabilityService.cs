using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

/// <summary>
/// A questek elérhetőségének és aktiválhatóságának
/// központi szabályait kezeli.
/// </summary>
public sealed class QuestAvailabilityService
{
    private readonly QuestCatalog _catalog;
    private readonly QuestStateStore _stateStore;
    private readonly IQuestWorldContext _world;

    public QuestAvailabilityService(
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
    /// Újraértékeli az adott NPC valamennyi questjének
    /// Locked/Available állapotát.
    ///
    /// Már aktív, teljesíthető, teljesített vagy elbukott
    /// quest állapotát nem módosítja.
    /// </summary>
    public IReadOnlyList<QuestRuntimeState> RefreshForNpc(
        QuestNpcId npcId,
        QuestNpcInstanceId instanceId = default)
    {
        if (npcId == QuestNpcId.None)
            throw new ArgumentException(
                "QuestNpcId.None nem használható.",
                nameof(npcId));

        var results = new List<QuestRuntimeState>();

        foreach (var definition in _catalog.GetByGiver(npcId))
        {
            var state = _stateStore.GetOrCreate(
                definition.Id,
                instanceId);

            RefreshAvailability(
                definition,
                state,
                instanceId);

            results.Add(state);
        }

        return results;
    }

    /// <summary>
    /// Visszaadja az adott NPC jelenleg általánosan felajánlható questjeit.
    /// A Story típusú küldetést csak konkrét Activate hívás indíthatja el.
    /// Előtte automatikusan frissíti az availability állapotukat.
    /// </summary>
    public IReadOnlyList<QuestRuntimeState> GetAvailableForNpc(
        QuestNpcId npcId,
        QuestNpcInstanceId instanceId = default)
    {
        return RefreshForNpc(
                npcId,
                instanceId)
            .Where(state =>
                state.State == QuestState.Available &&
                _catalog.Get(state.QuestId).ActivationKind == QuestActivationKind.Offered)
            .ToArray();
    }

    /// <summary>
    /// Megmondja, hogy egy konkrét quest jelenleg aktiválható-e.
    /// </summary>
    public bool CanActivate(
        QuestId questId,
        QuestNpcInstanceId giverInstanceId = default)
    {
        var definition = _catalog.Get(questId);

        var state = _stateStore.GetOrCreate(
            questId,
            giverInstanceId);

        RefreshAvailability(
            definition,
            state,
            giverInstanceId);

        return state.State == QuestState.Available;
    }

    /// <summary>
    /// Aktivál egy elérhető questet.
    /// </summary>
    public QuestRuntimeState Activate(
        QuestId questId,
        QuestNpcInstanceId giverInstanceId = default)
    {
        var definition = _catalog.Get(questId);

        var state = _stateStore.GetOrCreate(
            questId,
            giverInstanceId);

        RefreshAvailability(
            definition,
            state,
            giverInstanceId);

        if (state.State != QuestState.Available)
        {
            throw new InvalidOperationException(
                $"A(z) '{questId}' quest jelenleg nem aktiválható. " +
                $"Aktuális állapot: {state.State}.");
        }

        state.Activate();

        return state;
    }

    /// <summary>
    /// Aktiválja az adott NPC összes jelenleg elérhető questjét.
    ///
    /// Ez felel meg a legacy viselkedésnek, ahol az Offered
    /// questek az NPC-interakció során Active állapotba kerültek.
    /// </summary>
    public IReadOnlyList<QuestRuntimeState> ActivateAvailableForNpc(
        QuestNpcId npcId,
        QuestNpcInstanceId instanceId = default)
    {
        var available = GetAvailableForNpc(
            npcId,
            instanceId);

        foreach (var state in available)
            state.Activate();

        return available;
    }

    internal void RefreshRestoredStates()
    {
        foreach (var state in _stateStore.All)
            RefreshAvailability(_catalog.Get(state.QuestId), state, state.GiverInstanceId);
    }

    private void RefreshAvailability(
        QuestDefinition definition,
        QuestRuntimeState state,
        QuestNpcInstanceId giverInstanceId)
    {
        // Az activation requirement csak a felvételt kapuzza.
        // Már futó vagy lezárt questet nem befolyásol.
        if (state.State is
            QuestState.Active or
            QuestState.ReadyToTurnIn or
            QuestState.Completed or
            QuestState.Failed)
        {
            return;
        }

        var available = MeetsActivationRequirement(
            definition,
            giverInstanceId);

        state.SetState(
            available
                ? QuestState.Available
                : QuestState.Locked);
    }

    private bool MeetsActivationRequirement(
        QuestDefinition definition,
        QuestNpcInstanceId giverInstanceId)
    {
        return definition.ActivationRequirement switch
        {
            null => true,

            QuestActivationRequirement.StoryStateEquals requirement =>
                _world.GetNpcStoryState(
                    definition.Giver,
                    giverInstanceId)
                == requirement.RequiredState,

            _ => throw new ArgumentOutOfRangeException(
                nameof(definition.ActivationRequirement),
                definition.ActivationRequirement,
                "Ismeretlen quest aktiválási feltétel.")
        };
    }
}
