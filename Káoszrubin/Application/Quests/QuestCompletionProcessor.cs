using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

/// <summary>
/// A questek biztonságos lezárását kezeli.
///
/// Feladata:
/// - a teljesíthetőség ellenőrzése,
/// - collect objective esetén a tárgyak elvétele,
/// - a runtime state Completed állapotba helyezése.
///
/// Nem kezel UI-t, XP-kiosztást vagy perkablakokat.
/// </summary>
public sealed class QuestCompletionProcessor
{
    private readonly QuestCatalog _catalog;
    private readonly QuestStateStore _stateStore;
    private readonly QuestProgressEngine _progressEngine;
    private readonly IQuestWorldContext _world;
    private readonly QuestRewardService _rewardService;

    public QuestCompletionProcessor(
        QuestCatalog catalog,
        QuestStateStore stateStore,
        QuestProgressEngine progressEngine,
        IQuestWorldContext world,
        QuestRewardService rewardService)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(progressEngine);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(rewardService);

        _catalog = catalog;
        _stateStore = stateStore;
        _progressEngine = progressEngine;
        _world = world;
        _rewardService = rewardService;
    }

    /// <summary>
    /// Megmondja, hogy a quest jelenleg leadható-e.
    ///
    /// Collect quest esetén előtte újraszámolja a progresst
    /// az aktuális inventory alapján.
    /// </summary>
    public bool CanComplete(
        QuestId questId,
        QuestNpcInstanceId giverInstanceId = default)
    {
        var definition = _catalog.Get(questId);

        SynchronizeIfCollect(definition);

        if (!_stateStore.TryGet(
                questId,
                giverInstanceId,
                out var state))
        {
            return false;
        }

        return state.State ==
               QuestState.ReadyToTurnIn;
    }

    /// <summary>
    /// Lezár egy leadásra kész questet.
    /// Hibát dob, ha a quest nem teljesíthető.
    /// </summary>
    public QuestCompletionResult Complete(
        QuestId questId,
        QuestNpcInstanceId giverInstanceId = default)
    {
        var definition =
            _catalog.Get(questId);

        SynchronizeIfCollect(definition);

        var state =
            _stateStore.Get(
                questId,
                giverInstanceId);

        if (state.State != QuestState.ReadyToTurnIn)
        {
            throw new InvalidOperationException(
                $"A(z) '{questId}' quest nem adható le. " +
                $"Aktuális állapot: {state.State}, " +
                $"progress: {state.Progress}/" +
                $"{definition.Objective.RequiredCount}.");
        }

        ConsumeObjectiveItemsIfNeeded(definition);

        var rewards =
            _rewardService.Grant(
                definition);

        state.Complete();

        return new QuestCompletionResult(
            QuestId: definition.Id,
            GiverInstanceId:
                state.GiverInstanceId,
            Title:
                definition.Title,
            CompletionCount:
                state.CompletionCount,
            Rewards:
                rewards);
    }

    /// <summary>
    /// Visszaadja a jelenleg leadásra kész questeket.
    ///
    /// Előtte minden collect questet szinkronizál
    /// az aktuális inventoryval.
    /// </summary>
    public IReadOnlyList<QuestRuntimeState>
        GetPendingCompletions()
    {
        _progressEngine
            .SynchronizeCollectObjectives();

        return _stateStore
            .GetReadyToTurnIn();
    }

    private void SynchronizeIfCollect(
        QuestDefinition definition)
    {
        if (definition.Objective
            is QuestObjective.CollectItem)
        {
            _progressEngine
                .SynchronizeCollectObjectives();
        }
    }

    private void ConsumeObjectiveItemsIfNeeded(
        QuestDefinition definition)
    {
        if (definition.Objective
            is not QuestObjective.CollectItem collect)
        {
            return;
        }

        var consumed =
            _world.TryConsumePartyItem(
                collect.Item,
                collect.RequiredCount);

        if (!consumed)
        {
            // Az inventory akár a confirm ablak megnyitása
            // és a tényleges leadás között is változhatott.
            _progressEngine
                .SynchronizeCollectObjectives();

            throw new InvalidOperationException(
                $"A(z) '{definition.Id}' collect quest " +
                "már nem teljesíthető, mert nincs elegendő " +
                $"'{collect.Item.Name}' a parti inventoryjában.");
        }
    }
}