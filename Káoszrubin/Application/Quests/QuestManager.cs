using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

/// <summary>
/// A quest-rendszer központi publikus belépési pontja.
///
/// A játék többi része lehetőség szerint kizárólag
/// ezen az osztályon keresztül dolgozik questekkel.
/// </summary>
public sealed class QuestManager
{
    private readonly QuestCatalog _catalog;
    private readonly QuestStateStore _stateStore;
    private readonly QuestAvailabilityService _availability;
    private readonly QuestProgressEngine _progressEngine;
    private readonly QuestCompletionProcessor _completionProcessor;
    private readonly IQuestNpcConversationService _conversationService;

    /// <summary>
    /// A futásidejű módosítás utáni szinkron projekciós értesítés.
    /// A feliratkozó csak olvassa a handle-t; nem indít questműveletet vagy UI-t.
    /// </summary>
    public event Action<QuestHandle>? QuestChanged;

    public QuestManager(
        QuestCatalog catalog,
        QuestStateStore stateStore,
        QuestAvailabilityService availability,
        QuestProgressEngine progressEngine,
        QuestCompletionProcessor completionProcessor,
        IQuestNpcConversationService conversationService)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(progressEngine);
        ArgumentNullException.ThrowIfNull(completionProcessor);
        ArgumentNullException.ThrowIfNull(conversationService);

        _catalog = catalog;
        _stateStore = stateStore;
        _availability = availability;
        _progressEngine = progressEngine;
        _completionProcessor = completionProcessor;
        _conversationService = conversationService;
        _progressEngine.ProgressChanged += change => QuestChanged?.Invoke(
            CreateHandle(change.QuestId, change.GiverInstanceId));

        Roderic = new RodericNpcApi(this);

        Elira = new EliraNpcApi(this);
    }


    // ------------------------------------------------------------
    // Egyedi Npc támogatás
    // ------------------------------------------------------------

    public RodericNpcApi Roderic { get; }

    public EliraNpcApi Elira { get; }

    // ------------------------------------------------------------
    // NEM egyedi Npc támogatás
    // ------------------------------------------------------------

    public QuestNpcHandle For(QuestNpcId npcId, QuestNpcInstanceId instanceId = default)
    {
        return new QuestNpcHandle(
            this,
            npcId,
            instanceId);
    }

    // ------------------------------------------------------------
    // Quest lekérdezések
    // ------------------------------------------------------------

    public IReadOnlyList<QuestHandle> GetActiveQuestsForNpc(QuestNpcId npcId, QuestNpcInstanceId instanceId = default)
    {
        return GetQuestsForNpc(
                npcId,
                instanceId)
            .Where(quest =>
                quest.IsInProgress)
            .ToArray();
    }

    /// <summary>
    /// Lekér egy globális questet.
    ///
    /// PerNpcInstance quest esetén a másik overloadot kell használni.
    /// </summary>
    public QuestHandle GetQuest(QuestId questId)
    {
        return CreateHandle(
            questId,
            QuestNpcInstanceId.None);
    }

    /// <summary>
    /// Lekér egy konkrét NPC-példányhoz tartozó questet.
    /// </summary>
    public QuestHandle GetQuest(QuestId questId, QuestNpcInstanceId giverInstanceId)
    {
        return CreateHandle(
            questId,
            giverInstanceId);
    }

    /// <summary>
    /// Az összes jelenleg folyamatban lévő quest.
    ///
    /// Az Active és ReadyToTurnIn állapotokat egyaránt tartalmazza.
    /// </summary>
    public IReadOnlyList<QuestHandle> GetActiveQuests()
    {
        return _stateStore
            .GetInProgress()
            .Select(CreateHandle)
            .ToArray();
    }

    /// <summary>
    /// Az összes leadásra kész quest.
    /// </summary>
    public IReadOnlyList<QuestHandle> GetReadyToTurnInQuests()
    {
        return _stateStore
            .GetReadyToTurnIn()
            .Select(CreateHandle)
            .ToArray();
    }

    /// <summary>
    /// Egy konkrét NPC-példányhoz tartozó questek.
    /// </summary>
    public IReadOnlyList<QuestHandle> GetQuestsForNpc(QuestNpcId npcId, QuestNpcInstanceId instanceId = default)
    {
        var definitions =
            _catalog.GetByGiver(npcId);

        return definitions
            .Select(definition =>
                CreateHandle(
                    definition.Id,
                    instanceId))
            .ToArray();
    }

    /// <summary>
    /// Egy NPC jelenleg általánosan felajánlható questjei (Story küldetések nélkül).
    /// </summary>
    public IReadOnlyList<QuestHandle> GetAvailableQuestsForNpc(QuestNpcId npcId, QuestNpcInstanceId instanceId = default)
    {
        return _availability
            .GetAvailableForNpc(
                npcId,
                instanceId)
            .Select(CreateHandle)
            .ToArray();
    }

    public bool CanComplete(QuestId questId, QuestNpcInstanceId giverInstanceId = default)
    {
        return _completionProcessor.CanComplete(
            questId,
            giverInstanceId);
    }

    public QuestCompletionResult Complete(QuestId questId, QuestNpcInstanceId giverInstanceId = default)
    {
        var result = _completionProcessor.Complete(
            questId,
            giverInstanceId);
        QuestChanged?.Invoke(CreateHandle(questId, giverInstanceId));
        // A fogyasztás és a jutalom más aktív collect küldetést is érinthet.
        _progressEngine.SynchronizeCollectObjectives();
        return result;
    }

    public IReadOnlyList<QuestHandle> GetPendingQuestCompletions()
    {
        return _completionProcessor
            .GetPendingCompletions()
            .Select(CreateHandle)
            .ToArray();
    }

    // ------------------------------------------------------------
    // Aktiválás
    // ------------------------------------------------------------

    public QuestHandle Activate(QuestId questId, QuestNpcInstanceId giverInstanceId = default)
    {
        var state = _availability.Activate(
            questId,
            giverInstanceId);
        QuestChanged?.Invoke(CreateHandle(state));
        _progressEngine.SynchronizeExplorationObjective(state);

        // Collect quest esetén azonnal vegyük figyelembe
        // a már meglévő inventory tartalmát.
        if (_catalog.Get(questId).Objective
            is QuestObjective.CollectItem)
        {
            _progressEngine
                .SynchronizeCollectObjectives();
        }

        return CreateHandle(state);
    }

    /// <summary>
    /// Aktiválja az NPC összes jelenleg általánosan felajánlható questjét.
    /// </summary>
    public IReadOnlyList<QuestHandle> ActivateAvailableForNpc(QuestNpcId npcId, QuestNpcInstanceId instanceId = default)
    {
        var states = _availability
            .ActivateAvailableForNpc(
                npcId,
                instanceId);

        foreach (var state in states)
        {
            QuestChanged?.Invoke(CreateHandle(state));
            _progressEngine.SynchronizeExplorationObjective(state);
        }

        if (states.Any(state =>
                _catalog.Get(state.QuestId).Objective
                    is QuestObjective.CollectItem))
        {
            _progressEngine
                .SynchronizeCollectObjectives();
        }

        return states
            .Select(CreateHandle)
            .ToArray();
    }

    public QuestHandle Abandon(QuestId questId, QuestNpcInstanceId giverInstanceId = default)
    {
        var state =
            _stateStore.Get(
                questId,
                giverInstanceId);

        state.Abandon();
        QuestChanged?.Invoke(CreateHandle(state));

        return CreateHandle(state);
    }

    // ------------------------------------------------------------
    // Gameplay események
    // ------------------------------------------------------------

    public IReadOnlyList<QuestProgressChange> RegisterKill(Enemy defeatedEnemy)
    {
        ArgumentNullException.ThrowIfNull(defeatedEnemy);

        return _progressEngine.Process(
            new EnemyKilledEvent(defeatedEnemy));
    }

    public IReadOnlyList<QuestProgressChange> RegisterInventoryChanged(IItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return _progressEngine.Process(
            new InventoryItemCountChangedEvent(item));
    }

    public IReadOnlyList<QuestProgressChange> RegisterTrapDisarmed()
    {
        return _progressEngine.Process(
            new TrapDisarmedEvent());
    }

    public IReadOnlyList<QuestProgressChange> RegisterChestOpened()
    {
        return _progressEngine.Process(
            new ChestOpenedEvent());
    }

    public IReadOnlyList<QuestProgressChange> RegisterLocationReached(QuestLocation location)
    {
        return _progressEngine.Process(
            new LocationReachedEvent(location));
    }

    public IReadOnlyList<QuestProgressChange> RegisterLocationDiscovered(QuestLocation location) =>
        _progressEngine.Process(new LocationDiscoveredEvent(location));

    public IReadOnlyList<QuestProgressChange> RegisterNpcReachedLocation(
        QuestNpcId npcId, QuestNpcInstanceId instanceId, QuestLocation location)
    {
        if (npcId == QuestNpcId.None) throw new ArgumentException("A kísérő NPC azonosítója kötelező.", nameof(npcId));
        if (instanceId.IsNone) throw new ArgumentException("A kísérő példányazonosítója kötelező.", nameof(instanceId));
        return _progressEngine.Process(new NpcReachedLocationEvent(npcId, instanceId, location));
    }

    /// <summary>
    /// Minden aktív collect quest progressét újraszámolja
    /// az aktuális party inventoryból.
    /// </summary>
    public IReadOnlyList<QuestProgressChange> SynchronizeCollectQuests()
    {
        return _progressEngine
            .SynchronizeCollectObjectives();
    }

    internal void StartConversation(QuestNpcId npcId, QuestNpcInstanceId instanceId = default)
    {
        _conversationService.StartConversation(
            npcId,
            instanceId);
    }

    // ------------------------------------------------------------
    // Belső handle létrehozás
    // ------------------------------------------------------------

    private QuestHandle CreateHandle(
        QuestId questId,
        QuestNpcInstanceId instanceId)
    {
        var definition =
            _catalog.Get(questId);

        var state =
            _stateStore.GetOrCreate(
                questId,
                instanceId);

        return new QuestHandle(
            this,
            definition,
            state);
    }

    private QuestHandle CreateHandle(
        QuestRuntimeState state)
    {
        return new QuestHandle(
            this,
            _catalog.Get(state.QuestId),
            state);
    }
}
