namespace MazeGame.Domain.Characters;

/// <summary>Az aktív karakterből és legfeljebb három társából álló kalandozócsapat.</summary>
public sealed class Party
{
    public const int MaximumSize = 4;
    private readonly object _gate = new();
    private readonly List<LiveCharacter> _members = [];
    public IReadOnlyList<LiveCharacter> Members { get { lock (_gate) return _members.ToArray(); } }
    public LiveCharacter? Leader { get { lock (_gate) return _members.FirstOrDefault(); } }

    public void SetLeader(LiveCharacter leader)
    {
        lock (_gate)
        {
            _members.Clear();
            _members.Add(leader);
        }
    }

    public bool Add(LiveCharacter character)
    {
        lock (_gate)
        {
            if (_members.Count >= MaximumSize || _members.Contains(character)) return false;
            _members.Add(character);
            return true;
        }
    }

    public bool Remove(LiveCharacter character) { lock (_gate) return _members.Remove(character); }

    public void Restore(LiveCharacter leader, IEnumerable<LiveCharacter> members)
    {
        lock (_gate)
        {
            _members.Clear();
            _members.Add(leader);
            foreach (var member in members.Where(member => member != leader).Take(MaximumSize - 1))
                _members.Add(member);
        }
    }

    public void Clear() { lock (_gate) _members.Clear(); }
}
