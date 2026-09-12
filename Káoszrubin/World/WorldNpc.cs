using System.Text;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain;

namespace KaoszRubin.World;

public enum NpcDisposition { Friendly, Neutral, Hostile }
public enum WorldNpcState { Available, Declined, Following }
[Obsolete("Legacy quest state. Használd a Domain.Quests típusokat.")]
public enum NpcQuestState { Offered, Active, Completed, Abandoned }
public enum WorldNpcInteractionResult { Leave, Join, Continue }
[Obsolete("Legacy quest state. Használd a Domain.Quests típusokat.")]
public sealed record NpcQuestProgress(string QuestId, NpcQuestState State = NpcQuestState.Offered, int Progress = 0);

/// <summary>A pályán megszólítható, még nem partitagnak számító karakter.</summary>
public sealed class WorldNpc(Position position, string definitionId, LiveCharacter character,
    NpcDisposition disposition, bool recruitable, bool isQuestNpc, string dialogue,
    WorldNpcState state = WorldNpcState.Available, int friendliness = 5,
    NpcWorldBehavior behavior = NpcWorldBehavior.Guarded, IReadOnlyList<string>? questIds = null,
    string? storyId = null, string storyStateId = "INITIAL") : WorldObject(position)
{
    private readonly Dictionary<string, NpcQuestProgress> _quests = (questIds ?? [])
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToDictionary(id => id, id => new NpcQuestProgress(id), StringComparer.OrdinalIgnoreCase);
    public string DefinitionId { get; } = definitionId;
    public LiveCharacter Character { get; } = character;
    public NpcDisposition Disposition { get; } = disposition;
    public bool Recruitable { get; } = recruitable;
    public bool IsQuestNpc { get; } = isQuestNpc;
    public string Dialogue { get; } = dialogue;
    public WorldNpcState State { get; private set; } = state;
    public bool CanStartConversation => State != WorldNpcState.Following;
    public int Friendliness { get; private set; } = Math.Clamp(friendliness, 0, 10);
    public NpcWorldBehavior Behavior { get; } = behavior;
    public string? StoryId { get; } = storyId;
    public string StoryStateId { get; private set; } = string.IsNullOrWhiteSpace(storyStateId) ? "INITIAL" : storyStateId;
    public int ConversationStage { get; private set; }
    [Obsolete("Legacy quest state. Használd a QuestManager / QuestNpcHandle rendszert.")]
    public IReadOnlyList<string> QuestIds => _quests.Keys.ToArray();
    [Obsolete("Legacy quest state. Használd a QuestManager / QuestNpcHandle rendszert.")]
    public IReadOnlyList<NpcQuestProgress> Quests => _quests.Values.ToArray();
    public override Rune Symbol { get; } = Rune.GetRuneAt(character.CharacterClass.Name.ToUpperInvariant(), 0);

    public void Decline() => State = WorldNpcState.Declined;
    public void BeginFollowing() => State = WorldNpcState.Following;
    public void MoveTo(Position position) => SetPosition(position);
    public void AdjustFriendliness(int amount) => Friendliness = Math.Clamp(Friendliness + amount, 0, 10);
    public void AdvanceConversation() => ConversationStage++;
    public void RestoreConversationStage(int stage) => ConversationStage = Math.Max(0, stage);
    public void SetStoryState(string storyStateId)
    {
        if (string.IsNullOrWhiteSpace(storyStateId)) throw new ArgumentException("A történeti állapot nem lehet üres.", nameof(storyStateId));
        StoryStateId = storyStateId;
    }
    [Obsolete("Legacy quest state. Használd a QuestManager-t.")]
    public bool ActivateQuest(string questId)
    {
        if (!_quests.TryGetValue(questId, out var quest) || quest.State != NpcQuestState.Offered) return false;
        _quests[questId] = quest with { State = NpcQuestState.Active };
        return true;
    }
    [Obsolete("Legacy quest state. Használd a QuestManager-t.")]
    public bool AddQuestProgress(string questId, int amount, int requiredCount)
    {
        if (!_quests.TryGetValue(questId, out var quest) || quest.State != NpcQuestState.Active) return false;
        _quests[questId] = quest with { Progress = Math.Clamp(quest.Progress + Math.Max(0, amount), 0, requiredCount) };
        return true;
    }
    [Obsolete("Legacy quest state. Használd a QuestManager-t.")]
    public bool CompleteQuest(string questId)
    {
        if (!_quests.TryGetValue(questId, out var quest) || quest.State != NpcQuestState.Active) return false;
        _quests[questId] = quest with { State = NpcQuestState.Completed };
        return true;
    }
    [Obsolete("Legacy quest state. Használd a QuestManager-t.")]
    public bool AbandonQuest(string questId)
    {
        if (!_quests.TryGetValue(questId, out var quest) || quest.State != NpcQuestState.Active) return false;
        _quests[questId] = quest with { State = NpcQuestState.Abandoned };
        return true;
    }
    [Obsolete("Legacy quest state. Használd a QuestManager-t.")]
    public void RestoreQuests(IEnumerable<NpcQuestProgress> quests)
    {
        foreach (var quest in quests)
            if (_quests.ContainsKey(quest.QuestId)) _quests[quest.QuestId] = quest;
    }
}
