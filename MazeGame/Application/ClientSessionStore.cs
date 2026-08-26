namespace MazeGame.Application;

public enum ClientFrameApplyStatus
{
    Applied,
    Ignored,
    ResyncRequired,
    Rejected
}

/// <summary>Az alkalmazás eredménye és a hostnak visszaküldendő ACK vagy resync-kérés.</summary>
public sealed record ClientFrameApplyResult(ClientFrameApplyStatus Status, object? Response = null,
    string? Error = null);

/// <summary>
/// Transportfüggetlen kliensoldali session read model. A teljes snapshotot eltárolja, a deltát kizárólag
/// annak deklarált baseline-jára alkalmazza, majd egy új, teljes és renderelhető snapshotot publikál.
/// </summary>
public sealed class ClientSessionStore
{
    private const int MaximumRetainedSnapshots = 16;
    private readonly object _gate = new();
    private readonly PlayerId _localPlayerId;
    private readonly SortedDictionary<long, SessionSnapshot> _snapshots = [];
    private SessionSnapshot? _currentSnapshot;

    public ClientSessionStore(PlayerId localPlayerId)
    {
        if (localPlayerId.Value == Guid.Empty) throw new ArgumentException("A helyi PlayerId érvénytelen.", nameof(localPlayerId));
        _localPlayerId = localPlayerId;
    }

    public SessionSnapshot? CurrentSnapshot
    {
        get { lock (_gate) return _currentSnapshot; }
    }

    public event Action<SessionSnapshot>? SnapshotChanged;

    public ClientFrameApplyResult Apply(SessionReplicationFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        SessionSnapshot? changedSnapshot = null;
        ClientFrameApplyResult result;
        lock (_gate)
        {
            result = ApplyLocked(frame, out changedSnapshot);
        }
        if (changedSnapshot is not null) SnapshotChanged?.Invoke(changedSnapshot);
        return result;
    }

    private ClientFrameApplyResult ApplyLocked(SessionReplicationFrame frame,
        out SessionSnapshot? changedSnapshot)
    {
        changedSnapshot = null;
        if (frame.RecipientPlayerId != _localPlayerId)
            return Reject("A replikációs frame másik játékosnak szól.");
        if (frame.Session.ProtocolVersion != SessionProtocol.Version)
            return Reject("A snapshot protokollverziója nem támogatott.");
        if (frame.Session.SnapshotSequence <= 0)
            return RequestResync("A snapshot-sorszám érvénytelen.");
        if (string.IsNullOrWhiteSpace(frame.Session.LevelName) || frame.Session.Party is null ||
            frame.Session.CharacterControls is null)
            return RequestResync("A session snapshot kötelező mezői hiányoznak.");
        if (_currentSnapshot is { } current && frame.Session.SnapshotSequence <= current.SnapshotSequence)
            return new ClientFrameApplyResult(ClientFrameApplyStatus.Ignored);

        SessionSnapshot next;
        try
        {
            next = frame.Kind switch
            {
                SessionReplicationFrameKind.FullSnapshot => ApplyFull(frame),
                SessionReplicationFrameKind.Delta => ApplyDelta(frame),
                _ => throw new ArgumentException("Ismeretlen replikációs frame-típus.")
            };
        }
        catch (ArgumentException exception)
        {
            return RequestResync(exception.Message);
        }

        _currentSnapshot = next;
        _snapshots[next.SnapshotSequence] = next;
        while (_snapshots.Count > MaximumRetainedSnapshots)
            _snapshots.Remove(_snapshots.Keys.First());
        changedSnapshot = next;
        return new ClientFrameApplyResult(ClientFrameApplyStatus.Applied,
            new SnapshotAck(_localPlayerId, next.SnapshotSequence));
    }

    private SessionSnapshot ApplyFull(SessionReplicationFrame frame)
    {
        if (frame.BaseSnapshotSequence is not null || frame.WorldDelta is not null || frame.Session.World is null)
            throw new ArgumentException("A teljes frame alakja érvénytelen.");
        ValidateWorld(frame.Session.World);
        _snapshots.Clear();
        return frame.Session;
    }

    private SessionSnapshot ApplyDelta(SessionReplicationFrame frame)
    {
        if (frame.BaseSnapshotSequence is not { } baseSequence || frame.WorldDelta is not { } delta ||
            frame.Session.World is not null)
            throw new ArgumentException("A delta frame alakja érvénytelen.");
        if (baseSequence != delta.FromSnapshotSequence ||
            frame.Session.SnapshotSequence != delta.ToSnapshotSequence ||
            delta.ToSnapshotSequence <= delta.FromSnapshotSequence)
            throw new ArgumentException("A delta sorszámai nem alkotnak érvényes baseline-láncot.");
        ValidateDelta(delta);
        if (!_snapshots.TryGetValue(baseSequence, out var baseline) || baseline.World is null)
            throw new ArgumentException("A delta baseline-ja már nem érhető el a kliensen.");
        if (baseline.MazeLevel != frame.Session.MazeLevel || baseline.LevelName != frame.Session.LevelName)
            throw new ArgumentException("Pályaváltás nem alkalmazható delta frame-ként.");

        var world = WorldDeltaReducer.Apply(baseline.World, delta);
        return frame.Session with { World = world };
    }

    private ClientFrameApplyResult RequestResync(string error) => new(ClientFrameApplyStatus.ResyncRequired,
        new SnapshotResyncRequest(_localPlayerId), error);

    private static ClientFrameApplyResult Reject(string error) =>
        new(ClientFrameApplyStatus.Rejected, null, error);

    private static void ValidateWorld(WorldSnapshot world)
    {
        if (world.Width <= 0 || world.Height <= 0)
            throw new ArgumentException("A world snapshot mérete érvénytelen.");
        if (world.WorldId.Value == Guid.Empty)
            throw new ArgumentException("A world snapshot azonosítója érvénytelen.");
        if (world.RevealedCells is null || world.Doors is null || world.Enemies is null ||
            world.Chests is null || world.Corpses is null || world.GroundPiles is null)
            throw new ArgumentException("A world snapshot egyik kötelező gyűjteménye hiányzik.");
    }

    private static void ValidateDelta(WorldDelta delta)
    {
        if (delta.RevealedOrChangedCells is null || delta.DoorUpserts is null ||
            delta.RemovedDoorPositions is null || delta.EnemyUpserts is null || delta.ChestUpserts is null ||
            delta.CorpseUpserts is null || delta.GroundPileUpserts is null || delta.RemovedEntityIds is null)
            throw new ArgumentException("A world delta egyik kötelező gyűjteménye hiányzik.");
    }
}

/// <summary>Egy world deltát új, változtathatatlan kliensoldali world snapshotra vetít.</summary>
public static class WorldDeltaReducer
{
    public static WorldSnapshot Apply(WorldSnapshot baseline, WorldDelta delta)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(delta);

        var cells = baseline.RevealedCells.ToDictionary(cell => cell.Position);
        foreach (var cell in delta.RevealedOrChangedCells) cells[cell.Position] = cell;

        var doors = baseline.Doors.ToDictionary(door => door.Position);
        foreach (var position in delta.RemovedDoorPositions) doors.Remove(position);
        foreach (var door in delta.DoorUpserts) doors[door.Position] = door;

        var enemies = baseline.Enemies.ToDictionary(entity => entity.EntityId);
        var chests = baseline.Chests.ToDictionary(entity => entity.EntityId);
        var corpses = baseline.Corpses.ToDictionary(entity => entity.EntityId);
        var piles = baseline.GroundPiles.ToDictionary(entity => entity.EntityId);
        foreach (var id in delta.RemovedEntityIds) RemoveEntity(id, enemies, chests, corpses, piles);
        foreach (var enemy in delta.EnemyUpserts)
        {
            RemoveEntity(enemy.EntityId, enemies, chests, corpses, piles);
            enemies[enemy.EntityId] = enemy;
        }
        foreach (var chest in delta.ChestUpserts)
        {
            RemoveEntity(chest.EntityId, enemies, chests, corpses, piles);
            chests[chest.EntityId] = chest;
        }
        foreach (var corpse in delta.CorpseUpserts)
        {
            RemoveEntity(corpse.EntityId, enemies, chests, corpses, piles);
            corpses[corpse.EntityId] = corpse;
        }
        foreach (var pile in delta.GroundPileUpserts)
        {
            RemoveEntity(pile.EntityId, enemies, chests, corpses, piles);
            piles[pile.EntityId] = pile;
        }

        return baseline with
        {
            Entrance = delta.RevealedEntrance ?? baseline.Entrance,
            Exit = delta.RevealedExit ?? baseline.Exit,
            RevealedCells = cells.Values.OrderBy(cell => cell.Position.Y).ThenBy(cell => cell.Position.X).ToArray(),
            Doors = doors.Values.OrderBy(door => door.Position.Y).ThenBy(door => door.Position.X).ToArray(),
            Enemies = enemies.Values.OrderBy(entity => entity.Position.Y).ThenBy(entity => entity.Position.X)
                .ThenBy(entity => entity.EntityId.Value).ToArray(),
            Chests = chests.Values.OrderBy(entity => entity.Position.Y).ThenBy(entity => entity.Position.X)
                .ThenBy(entity => entity.EntityId.Value).ToArray(),
            Corpses = corpses.Values.OrderBy(entity => entity.Position.Y).ThenBy(entity => entity.Position.X)
                .ThenBy(entity => entity.EntityId.Value).ToArray(),
            GroundPiles = piles.Values.OrderBy(entity => entity.Position.Y).ThenBy(entity => entity.Position.X)
                .ThenBy(entity => entity.EntityId.Value).ToArray()
        };
    }

    private static void RemoveEntity(WorldEntityId id,
        Dictionary<WorldEntityId, WorldEnemySnapshot> enemies,
        Dictionary<WorldEntityId, WorldChestSnapshot> chests,
        Dictionary<WorldEntityId, WorldCorpseSnapshot> corpses,
        Dictionary<WorldEntityId, WorldGroundPileSnapshot> piles)
    {
        enemies.Remove(id);
        chests.Remove(id);
        corpses.Remove(id);
        piles.Remove(id);
    }
}
