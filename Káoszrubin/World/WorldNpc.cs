using System.Text;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain;

namespace KaoszRubin.World;

public enum NpcDisposition { Friendly, Neutral, Hostile }
public enum WorldNpcState { Available, Declined, Following }
public enum WorldNpcInteractionResult { Leave, Join, Continue }

/// <summary>A pályán megszólítható, még nem partitagnak számító karakter.</summary>
public sealed class WorldNpc(Position position, string definitionId, LiveCharacter character,
    NpcDisposition disposition, bool recruitable, bool isQuestNpc, string dialogue,
    WorldNpcState state = WorldNpcState.Available, int friendliness = 5,
    NpcWorldBehavior behavior = NpcWorldBehavior.Guarded,
    string? storyId = null, string storyStateId = "INITIAL") : WorldObject(position)
{
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
}
