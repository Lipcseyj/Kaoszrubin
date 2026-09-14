using System.Text;
using KaoszRubin.Domain.Quests;

namespace KaoszRubin.World;

public enum DoorState { Locked, Open, Closed, Smashed }

/// <summary>Állapottal rendelkező ajtó; a bezúzott állapot végleges.</summary>
public sealed class MazeDoor
{
    public Position Position { get; }
    public DoorState State { get; private set; }
    public QuestKey? RequiredQuest { get; }
    public bool QuestAccessGranted { get; private set; }
    public bool IsQuestSealed => RequiredQuest is not null && !QuestAccessGranted;

    public MazeDoor(Position position, DoorState state, QuestKey? requiredQuest = null, bool questAccessGranted = false)
    {
        if (requiredQuest is { } key && (!Enum.IsDefined(key.QuestId) || key.QuestId == QuestId.None) ||
            requiredQuest is null && questAccessGranted ||
            requiredQuest is not null && !questAccessGranted && state is DoorState.Open or DoorState.Smashed)
            throw new ArgumentException("Érvénytelen questajtó-állapot.");
        Position = position;
        State = state;
        RequiredQuest = requiredQuest;
        QuestAccessGranted = questAccessGranted;
    }

    // A host questellenőrzése után egyszer megszerzett hozzáférés nem vonható vissza.
    public void GrantQuestAccess() { if (RequiredQuest is not null) QuestAccessGranted = true; }
    public bool IsWalkable => State is DoorState.Open or DoorState.Smashed;
    public bool BlocksSight => State is DoorState.Locked or DoorState.Closed;
    public Rune Symbol => State switch
    {
        DoorState.Locked => new Rune('╫'),
        DoorState.Open => new Rune('╱'),
        DoorState.Closed => new Rune('╬'),
        DoorState.Smashed => new Rune('▒'),
        _ => new Rune('?')
    };

    public bool TrySetState(DoorState state)
    {
        if (IsQuestSealed && state is DoorState.Open or DoorState.Smashed) return false;
        if (State == DoorState.Smashed && state != DoorState.Smashed) return false;
        State = state;
        return true;
    }
}
