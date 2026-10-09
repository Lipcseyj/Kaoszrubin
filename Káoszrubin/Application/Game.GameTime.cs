namespace KaoszRubin.Application;

public sealed partial class Game
{
    private void RefreshGameTime()
    {
        _renderer?.SetGameTime(GameTime.Snapshot);
        MarkCoopSnapshotDirty();
    }

    private int RemainingExplorationRoundMilliseconds(DateTime now) =>
        _nextExplorationStatusTickUtc == DateTime.MaxValue ? _pausedExplorationRoundMilliseconds :
        ExplorationRoundRemainingMilliseconds(_nextExplorationStatusTickUtc, now);

    private void PauseExplorationRound()
    {
        _pausedExplorationRoundMilliseconds = RemainingExplorationRoundMilliseconds(DateTime.UtcNow);
        _nextExplorationStatusTickUtc = DateTime.MaxValue;
    }

    private void ResumeExplorationRound() =>
        _nextExplorationStatusTickUtc = DateTime.UtcNow +
            TimeSpan.FromMilliseconds(_pausedExplorationRoundMilliseconds);

    internal static int ExplorationRoundRemainingMilliseconds(DateTime nextRoundUtc, DateTime now)
    {
        if (nextRoundUtc == DateTime.MinValue || nextRoundUtc == DateTime.MaxValue) return 30_000;
        return (int)Math.Clamp(Math.Ceiling((nextRoundUtc - now).TotalMilliseconds), 0, 30_000);
    }
}
