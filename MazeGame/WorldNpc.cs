using System.Text;
using MazeGame.Domain.Characters;
using MazeGame.Domain;

namespace MazeGame;

public enum NpcDisposition { Friendly, Neutral, Hostile }
public enum WorldNpcState { Available, Declined, Following }
public enum NpcQuestState { Offered, Active, Completed }
public sealed record NpcQuestProgress(string QuestId, NpcQuestState State = NpcQuestState.Offered, int Progress = 0);

/// <summary>A pályán megszólítható, még nem partitagnak számító karakter.</summary>
public sealed class WorldNpc(Position position, string definitionId, LiveCharacter character,
    NpcDisposition disposition, bool recruitable, bool isQuestNpc, string dialogue,
    WorldNpcState state = WorldNpcState.Available, int friendliness = 5,
    NpcWorldBehavior behavior = NpcWorldBehavior.Guarded, IReadOnlyList<string>? questIds = null) : WorldObject(position)
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
    public int Friendliness { get; private set; } = Math.Clamp(friendliness, 0, 10);
    public NpcWorldBehavior Behavior { get; } = behavior;
    public int ConversationStage { get; private set; }
    public IReadOnlyList<string> QuestIds => _quests.Keys.ToArray();
    public IReadOnlyList<NpcQuestProgress> Quests => _quests.Values.ToArray();
    public bool CanJoin => Recruitable && _quests.Values.All(quest => quest.State == NpcQuestState.Completed);
    public override Rune Symbol { get; } = Rune.GetRuneAt(character.CharacterClass.Name.ToUpperInvariant(), 0);

    public void Decline() => State = WorldNpcState.Declined;
    public void BeginFollowing() => State = WorldNpcState.Following;
    public void MoveTo(Position position) => Position = position;
    public void AdjustFriendliness(int amount) => Friendliness = Math.Clamp(Friendliness + amount, 0, 10);
    public void AdvanceConversation() => ConversationStage++;
    public void RestoreConversationStage(int stage) => ConversationStage = Math.Max(0, stage);
    public bool ActivateQuest(string questId)
    {
        if (!_quests.TryGetValue(questId, out var quest) || quest.State != NpcQuestState.Offered) return false;
        _quests[questId] = quest with { State = NpcQuestState.Active };
        return true;
    }
    public bool AddQuestProgress(string questId, int amount, int requiredCount)
    {
        if (!_quests.TryGetValue(questId, out var quest) || quest.State != NpcQuestState.Active) return false;
        _quests[questId] = quest with { Progress = Math.Clamp(quest.Progress + Math.Max(0, amount), 0, requiredCount) };
        return true;
    }
    public bool CompleteQuest(string questId)
    {
        if (!_quests.TryGetValue(questId, out var quest) || quest.State != NpcQuestState.Active) return false;
        _quests[questId] = quest with { State = NpcQuestState.Completed };
        return true;
    }
    public void RestoreQuests(IEnumerable<NpcQuestProgress> quests)
    {
        foreach (var quest in quests)
            if (_quests.ContainsKey(quest.QuestId)) _quests[quest.QuestId] = quest;
    }
}
