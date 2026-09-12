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

    public QuestManager(
        QuestCatalog catalog,
        QuestStateStore stateStore,
        QuestAvailabilityService availability,
        QuestProgressEngine progressEngine)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(progressEngine);

        _catalog = catalog;
        _stateStore = stateStore;
        _availability = availability;
        _progressEngine = progressEngine;
    }

    // ------------------------------------------------------------
    // Quest lekérdezések
    // ------------------------------------------------------------

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
    public QuestHandle GetQuest(
        QuestId questId,
        QuestNpcInstanceId giverInstanceId)
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
    public IReadOnlyList<QuestHandle> GetQuestsForNpc(
        QuestNpcId npcId,
        QuestNpcInstanceId instanceId = default)
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
    /// Egy NPC jelenleg elérhető questjei.
    /// </summary>
    public IReadOnlyList<QuestHandle> GetAvailableQuestsForNpc(
        QuestNpcId npcId,
        QuestNpcInstanceId instanceId = default)
    {
        return _availability
            .GetAvailableForNpc(
                npcId,
                instanceId)
            .Select(CreateHandle)
            .ToArray();
    }

    // ------------------------------------------------------------
    // Aktiválás
    // ------------------------------------------------------------

    public QuestHandle Activate(
        QuestId questId,
        QuestNpcInstanceId giverInstanceId = default)
    {
        var state = _availability.Activate(
            questId,
            giverInstanceId);

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
    /// Aktiválja az NPC összes jelenleg elérhető questjét.
    /// </summary>
    public IReadOnlyList<QuestHandle> ActivateAvailableForNpc(
        QuestNpcId npcId,
        QuestNpcInstanceId instanceId = default)
    {
        var states = _availability
            .ActivateAvailableForNpc(
                npcId,
                instanceId);

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

    // ------------------------------------------------------------
    // Gameplay események
    // ------------------------------------------------------------

    public IReadOnlyList<QuestProgressChange> RegisterKill(
        Enemy defeatedEnemy)
    {
        ArgumentNullException.ThrowIfNull(defeatedEnemy);

        return _progressEngine.Process(
            new EnemyKilledEvent(defeatedEnemy));
    }

    public IReadOnlyList<QuestProgressChange>
        RegisterInventoryChanged(
            IItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return _progressEngine.Process(
            new InventoryItemCountChangedEvent(item));
    }

    public IReadOnlyList<QuestProgressChange>
        RegisterTrapDisarmed()
    {
        return _progressEngine.Process(
            new TrapDisarmedEvent());
    }

    public IReadOnlyList<QuestProgressChange>
        RegisterChestOpened()
    {
        return _progressEngine.Process(
            new ChestOpenedEvent());
    }

    public IReadOnlyList<QuestProgressChange>
        RegisterLocationReached(
            QuestLocation location)
    {
        return _progressEngine.Process(
            new LocationReachedEvent(location));
    }

    /// <summary>
    /// Minden aktív collect quest progressét újraszámolja
    /// az aktuális party inventoryból.
    /// </summary>
    public IReadOnlyList<QuestProgressChange>
        SynchronizeCollectQuests()
    {
        return _progressEngine
            .SynchronizeCollectObjectives();
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