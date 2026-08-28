using MazeGame.Domain;
using System.Text;

namespace MazeGame;

public enum TrapState { Hidden, Detected, Disarmed, Triggered }

/// <summary>Egy pályára lerakott csapda és annak futam közbeni állapota.</summary>
public sealed class MazeTrap : WorldObject
{
    public MazeTrap(Position position, TrapDefinition definition, TrapState state = TrapState.Hidden,
        bool detectionAttempted = false, int failedDisarmAttempts = 0) : base(position)
    {
        Definition = definition;
        State = state;
        DetectionAttempted = detectionAttempted;
        FailedDisarmAttempts = Math.Max(0, failedDisarmAttempts);
    }

    public TrapDefinition Definition { get; }
    public TrapState State { get; private set; }
    public bool DetectionAttempted { get; private set; }
    public int FailedDisarmAttempts { get; private set; }
    public bool IsActive => State is TrapState.Hidden or TrapState.Detected;
    public override Rune Symbol => State == TrapState.Disarmed ? new Rune('·') : Definition.Symbol;

    public void MarkDetectionAttempted() => DetectionAttempted = true;
    public void Detect() { DetectionAttempted = true; State = TrapState.Detected; }
    public void RecordFailedDisarm() => FailedDisarmAttempts++;
    public void Disarm() => State = TrapState.Disarmed;
    public void Trigger() => State = TrapState.Triggered;
}
