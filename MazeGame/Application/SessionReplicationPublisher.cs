namespace MazeGame.Application;

public enum SessionReplicationFrameKind
{
    FullSnapshot,
    Delta
}

/// <summary>A transportnak átadható host → kliens replikációs üzenet.</summary>
public sealed record SessionReplicationFrame(SessionReplicationFrameKind Kind, PlayerId RecipientPlayerId,
    long? BaseSnapshotSequence, SessionSnapshot Session, WorldDelta? WorldDelta);

/// <summary>
/// Kliensenként követi az utolsó nyugtázott baseline-t. Nem végez I/O-t; a SignalR/WebSocket réteg csak
/// továbbítja a létrehozott frame-et, majd visszaadja az ACK-ot vagy resync-kérést.
/// </summary>
public sealed class SessionReplicationPublisher
{
    private const int MaximumPendingSnapshotsPerClient = 8;
    private readonly object _gate = new();
    private readonly Dictionary<PlayerId, ClientReplicationState> _clients = [];

    public SessionReplicationFrame CreateFrame(PlayerId recipientPlayerId, SessionSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.ProtocolVersion != SessionProtocol.Version)
            throw new ArgumentException("A snapshot protokollverziója nem támogatott.", nameof(current));
        if (current.World is null)
            throw new ArgumentException("Publikáláshoz teljes world résszel rendelkező snapshot szükséges.", nameof(current));
        current = Personalize(current, recipientPlayerId);

        lock (_gate)
        {
            var client = GetOrCreateClient(recipientPlayerId);
            if (client.AcknowledgedSnapshot is { } acknowledged &&
                current.SnapshotSequence <= acknowledged.SnapshotSequence)
                throw new InvalidOperationException("Nyugtázott baseline-nál nem újabb snapshot nem publikálható.");
            SessionReplicationFrame frame;
            if (client.ForceFullSnapshot || client.AcknowledgedSnapshot is null)
            {
                frame = new SessionReplicationFrame(SessionReplicationFrameKind.FullSnapshot, recipientPlayerId,
                    null, current, null);
                client.ForceFullSnapshot = false;
            }
            else
            {
                try
                {
                    var delta = WorldDeltaProjector.Create(client.AcknowledgedSnapshot, current);
                    frame = new SessionReplicationFrame(SessionReplicationFrameKind.Delta, recipientPlayerId,
                        client.AcknowledgedSnapshot.SnapshotSequence, current with { World = null }, delta);
                }
                catch (ArgumentException)
                {
                    frame = new SessionReplicationFrame(SessionReplicationFrameKind.FullSnapshot, recipientPlayerId,
                        null, current, null);
                }
            }
            RememberSentSnapshot(client, current);
            return frame;
        }
    }

    public bool TryAcknowledge(PlayerId playerId, long snapshotSequence, out string error)
    {
        lock (_gate)
        {
            if (!_clients.TryGetValue(playerId, out var client))
                return Fail("A klienshez még nem tartozik replikációs állapot.", out error);
            if (client.AcknowledgedSnapshot is { } acknowledged && snapshotSequence <= acknowledged.SnapshotSequence)
                return Fail("A snapshot ACK lejárt vagy ismételt.", out error);
            if (!client.SentSnapshots.TryGetValue(snapshotSequence, out var snapshot))
            {
                client.ForceFullSnapshot = true;
                return Fail("Az ACK ismeretlen snapshotra hivatkozik; teljes resync szükséges.", out error);
            }

            client.AcknowledgedSnapshot = snapshot;
            foreach (var oldSequence in client.SentSnapshots.Keys.Where(sequence => sequence <= snapshotSequence).ToArray())
                client.SentSnapshots.Remove(oldSequence);
            error = string.Empty;
            return true;
        }
    }

    public void RequestFullSnapshot(PlayerId playerId)
    {
        lock (_gate)
        {
            var client = GetOrCreateClient(playerId);
            client.AcknowledgedSnapshot = null;
            client.SentSnapshots.Clear();
            client.ForceFullSnapshot = true;
        }
    }

    public void RemoveClient(PlayerId playerId)
    {
        lock (_gate) _clients.Remove(playerId);
    }

    private ClientReplicationState GetOrCreateClient(PlayerId playerId)
    {
        if (_clients.TryGetValue(playerId, out var client)) return client;
        client = new ClientReplicationState();
        _clients[playerId] = client;
        return client;
    }

    private static void RememberSentSnapshot(ClientReplicationState client, SessionSnapshot snapshot)
    {
        client.SentSnapshots[snapshot.SnapshotSequence] = snapshot;
        while (client.SentSnapshots.Count > MaximumPendingSnapshotsPerClient)
            client.SentSnapshots.Remove(client.SentSnapshots.Keys.First());
    }

    private static SessionSnapshot Personalize(SessionSnapshot snapshot, PlayerId recipientPlayerId)
    {
        if (recipientPlayerId == snapshot.HostPlayerId) return snapshot;
        var controlledCharacters = snapshot.CharacterControls
            .Where(control => control.AssignedPlayerId == recipientPlayerId &&
                              control.ConnectionState == PlayerConnectionState.Connected)
            .Select(control => control.CharacterId).ToHashSet();
        return snapshot with
        {
            Party = snapshot.Party.Select(character => controlledCharacters.Contains(character.CharacterId)
                ? character
                : character with { Inventory = null, CharacterSheet = null, ExplorationSpellOptions = null,
                    SpellInfo = null }).ToArray()
        };
    }

    private static bool Fail(string reason, out string error)
    {
        error = reason;
        return false;
    }

    private sealed class ClientReplicationState
    {
        public SessionSnapshot? AcknowledgedSnapshot { get; set; }
        public SortedDictionary<long, SessionSnapshot> SentSnapshots { get; } = [];
        public bool ForceFullSnapshot { get; set; } = true;
    }
}
