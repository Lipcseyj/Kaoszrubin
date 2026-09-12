using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

/// <summary>
/// Az aktuális játék futásidejű questállapotainak központi tárolója.
/// </summary>
public sealed class QuestStateStore
{
    private readonly QuestCatalog _catalog;
    private readonly Dictionary<QuestStateKey, QuestRuntimeState> _states = [];

    public IReadOnlyCollection<QuestRuntimeState> All =>
        _states.Values;

    public QuestStateStore(QuestCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _catalog = catalog;
    }

    /// <summary>
    /// Visszaadja a quest meglévő állapotát, vagy létrehozza azt.
    /// </summary>
    public QuestRuntimeState GetOrCreate(
        QuestId questId,
        QuestNpcInstanceId giverInstanceId = default,
        QuestState initialState = QuestState.Locked)
    {
        var key = CreateKey(
            questId,
            giverInstanceId);

        if (_states.TryGetValue(key, out var state))
            return state;

        state = new QuestRuntimeState(
            questId,
            key.GiverInstanceId,
            initialState);

        _states.Add(key, state);

        return state;
    }

    /// <summary>
    /// Visszaadja egy már létező quest futásidejű állapotát.
    /// Hibát dob, ha az még nem létezik.
    /// </summary>
    public QuestRuntimeState Get(
        QuestId questId,
        QuestNpcInstanceId giverInstanceId = default)
    {
        var key = CreateKey(
            questId,
            giverInstanceId);

        return _states.TryGetValue(key, out var state)
            ? state
            : throw new KeyNotFoundException(
                $"A(z) '{questId}' questhez nem található futásidejű állapot.");
    }

    /// <summary>
    /// Megpróbálja lekérni egy quest futásidejű állapotát.
    /// </summary>
    public bool TryGet(
        QuestId questId,
        QuestNpcInstanceId giverInstanceId,
        out QuestRuntimeState state)
    {
        var key = CreateKey(
            questId,
            giverInstanceId);

        return _states.TryGetValue(
            key,
            out state!);
    }

    /// <summary>
    /// Visszaadja egy adott quest összes futásidejű példányát.
    /// PerNpcInstance quest esetén ebből több is lehet.
    /// </summary>
    public IReadOnlyList<QuestRuntimeState> GetByQuest(
        QuestId questId)
    {
        if (questId == QuestId.None)
            throw new ArgumentException(
                "QuestId.None nem használható.",
                nameof(questId));

        return _states
            .Where(pair => pair.Key.QuestId == questId)
            .Select(pair => pair.Value)
            .ToArray();
    }

    /// <summary>
    /// Visszaadja az adott NPC-példányhoz tartozó
    /// összes runtime questállapotot.
    /// </summary>
    public IReadOnlyList<QuestRuntimeState> GetByNpcInstance(
        QuestNpcInstanceId instanceId)
    {
        if (instanceId.IsNone)
            throw new ArgumentException(
                "QuestNpcInstanceId.None nem használható.",
                nameof(instanceId));

        return _states
            .Where(pair =>
                pair.Key.GiverInstanceId == instanceId)
            .Select(pair => pair.Value)
            .ToArray();
    }

    /// <summary>
    /// Visszaadja az összes aktív questállapotot.
    /// </summary>
    public IReadOnlyList<QuestRuntimeState> GetActive() =>
        _states.Values
            .Where(state =>
                state.State == QuestState.Active)
            .ToArray();

    /// <summary>
    /// Visszaadja az összes leadásra kész questállapotot.
    /// </summary>
    public IReadOnlyList<QuestRuntimeState> GetReadyToTurnIn() =>
        _states.Values
            .Where(state =>
                state.State == QuestState.ReadyToTurnIn)
            .ToArray();

    public IReadOnlyList<QuestRuntimeState> GetInProgress() =>
    _states.Values
        .Where(state =>
            state.State is
                QuestState.Active or
                QuestState.ReadyToTurnIn)
        .ToArray();

    private QuestStateKey CreateKey(
        QuestId questId,
        QuestNpcInstanceId giverInstanceId)
    {
        var definition = _catalog.Get(questId);

        return definition.Scope switch
        {
            QuestScope.Global =>
                new QuestStateKey(
                    questId,
                    QuestNpcInstanceId.None),

            QuestScope.PerNpcInstance
                when !giverInstanceId.IsNone =>
                new QuestStateKey(
                    questId,
                    giverInstanceId),

            QuestScope.PerNpcInstance =>
                throw new InvalidOperationException(
                    $"A(z) '{questId}' quest NPC-példányhoz kötött, " +
                    "ezért QuestNpcInstanceId megadása kötelező."),

            _ => throw new ArgumentOutOfRangeException(
                nameof(definition.Scope),
                definition.Scope,
                "Ismeretlen quest scope.")
        };
    }

    private readonly record struct QuestStateKey(
        QuestId QuestId,
        QuestNpcInstanceId GiverInstanceId);
}