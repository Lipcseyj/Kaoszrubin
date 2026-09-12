using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

/// <summary>
/// Egy konkrét quest kényelmes, biztonságos publikus nézete.
///
/// Összekapcsolja a változatlan QuestDefinitiont
/// az aktuális QuestRuntimeState-tel.
/// </summary>
public sealed class QuestHandle
{
    private readonly QuestManager _manager;
    private readonly QuestDefinition _definition;
    private readonly QuestRuntimeState _state;

    internal QuestHandle(
        QuestManager manager,
        QuestDefinition definition,
        QuestRuntimeState state)
    {
        _manager = manager;
        _definition = definition;
        _state = state;
    }

    public QuestId Id =>
        _definition.Id;

    public QuestNpcId Giver =>
        _definition.Giver;

    public QuestNpcInstanceId GiverInstanceId =>
        _state.GiverInstanceId;

    /// <summary>
    /// Kényelmi alias a quest címére.
    /// </summary>
    public string Name =>
        _definition.Title;

    public string Title =>
        _definition.Title;

    public string Description =>
        _definition.Description;

    public QuestObjective Objective =>
        _definition.Objective;

    public int ExperienceReward =>
        _definition.ExperienceReward;

    public QuestScope Scope =>
        _definition.Scope;

    public QuestRepeatPolicy RepeatPolicy =>
        _definition.RepeatPolicy;

    public QuestState State =>
        _state.State;

    public int Progress =>
        _state.Progress;

    public int RequiredCount =>
        _definition.Objective.RequiredCount;

    public int CompletionCount =>
        _state.CompletionCount;

    public bool IsLocked =>
        State == QuestState.Locked;

    public bool IsAvailable =>
        State == QuestState.Available;

    public bool IsActive =>
        State == QuestState.Active;

    public bool IsReadyToTurnIn =>
        State == QuestState.ReadyToTurnIn;

    public bool IsCompleted =>
        State == QuestState.Completed;

    public bool IsFailed =>
        State == QuestState.Failed;

    public bool CanComplete =>
        _manager.CanComplete(
            Id,
            GiverInstanceId);

    public bool IsInProgress =>
        State is
            QuestState.Active or
            QuestState.ReadyToTurnIn;

    public bool IsResolved =>
        State is
            QuestState.Completed or
            QuestState.Failed;

    public IItemDefinition? FixedRewardItem =>
    _definition.FixedRewardItem;

    public int FixedRewardItemCount =>
        _definition.FixedRewardItemCount;

    public int RandomRewardCount =>
        _definition.RandomRewardCount;

    public QuestCompletionResult Complete()
    {
        return _manager.Complete(
            Id,
            GiverInstanceId);
    }

    /// <summary>
    /// Aktiválja a questet a QuestManager szabályain keresztül.
    /// </summary>
    public void Activate()
    {
        _manager.Activate(
            Id,
            GiverInstanceId);
    }

    public override string ToString() =>
        $"{Title} [{State}] {Progress}/{RequiredCount}";
}