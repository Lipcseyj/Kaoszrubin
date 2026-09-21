namespace KaoszRubin.Application;

/// <summary>
/// Egyetlen harci parancsot tart függőben addig, amíg a feldolgozása vissza nem igazolódik.
/// Ezzel a gyors billentyűismétlés nem tud több parancsot ugyanahhoz a prompthoz sorba állítani.
/// </summary>
public sealed class BattleCommandGate
{
    private readonly object _sync = new();
    private long? _commandId;
    private long _snapshotSequence;

    public bool IsPending
    {
        get
        {
            lock (_sync) return _commandId is not null;
        }
    }

    public bool TryBegin(long commandId, long snapshotSequence = 0)
    {
        lock (_sync)
        {
            if (_commandId is not null) return false;
            _commandId = commandId;
            _snapshotSequence = snapshotSequence;
            return true;
        }
    }

    public bool Complete(long commandId)
    {
        lock (_sync)
        {
            if (_commandId != commandId) return false;
            Clear();
            return true;
        }
    }

    public bool CompleteAfterSnapshot(long snapshotSequence)
    {
        lock (_sync)
        {
            if (_commandId is null || snapshotSequence <= _snapshotSequence) return false;
            Clear();
            return true;
        }
    }

    public void Reset()
    {
        lock (_sync) Clear();
    }

    private void Clear()
    {
        _commandId = null;
        _snapshotSequence = 0;
    }
}
