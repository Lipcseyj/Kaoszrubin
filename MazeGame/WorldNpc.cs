using System.Text;
using MazeGame.Domain.Characters;

namespace MazeGame;

public enum NpcDisposition { Friendly, Neutral, Hostile }
public enum WorldNpcState { Available, Declined }

/// <summary>A pályán megszólítható, még nem partitagnak számító karakter.</summary>
public sealed class WorldNpc(Position position, string definitionId, LiveCharacter character,
    NpcDisposition disposition, bool recruitable, bool isQuestNpc, string dialogue,
    WorldNpcState state = WorldNpcState.Available) : WorldObject(position)
{
    public string DefinitionId { get; } = definitionId;
    public LiveCharacter Character { get; } = character;
    public NpcDisposition Disposition { get; } = disposition;
    public bool Recruitable { get; } = recruitable;
    public bool IsQuestNpc { get; } = isQuestNpc;
    public string Dialogue { get; } = dialogue;
    public WorldNpcState State { get; private set; } = state;
    public override Rune Symbol { get; } = Rune.GetRuneAt(character.CharacterClass.Name.ToUpperInvariant(), 0);

    public void Decline() => State = WorldNpcState.Declined;
}
