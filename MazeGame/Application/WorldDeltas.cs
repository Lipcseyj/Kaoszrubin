namespace MazeGame.Application;

/// <summary>Két azonos pályához tartozó world snapshot közötti, sorrendhelyes változáscsomag.</summary>
public sealed record WorldDelta(long FromSnapshotSequence, long ToSnapshotSequence,
    Position? RevealedEntrance, Position? RevealedExit,
    IReadOnlyList<WorldCellSnapshot> RevealedOrChangedCells,
    IReadOnlyList<WorldDoorSnapshot> DoorUpserts, IReadOnlyList<Position> RemovedDoorPositions,
    IReadOnlyList<WorldEnemySnapshot> EnemyUpserts, IReadOnlyList<WorldChestSnapshot> ChestUpserts,
    IReadOnlyList<WorldCorpseSnapshot> CorpseUpserts, IReadOnlyList<WorldGroundPileSnapshot> GroundPileUpserts,
    IReadOnlyList<WorldEntityId> RemovedEntityIds)
{
    public bool IsEmpty => RevealedEntrance is null && RevealedExit is null &&
        RevealedOrChangedCells.Count == 0 && DoorUpserts.Count == 0 && RemovedDoorPositions.Count == 0 &&
        EnemyUpserts.Count == 0 && ChestUpserts.Count == 0 && CorpseUpserts.Count == 0 &&
        GroundPileUpserts.Count == 0 && RemovedEntityIds.Count == 0;
}

public static class WorldDeltaProjector
{
    public static WorldDelta Create(SessionSnapshot previous, SessionSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        if (previous.World is null || current.World is null)
            throw new ArgumentException("World delta csak world résszel rendelkező session snapshotokból készíthető.");
        if (previous.MazeLevel != current.MazeLevel || previous.LevelName != current.LevelName)
            throw new ArgumentException("Pályaváltáskor teljes snapshot szükséges, delta nem készíthető.", nameof(current));
        return Create(previous.SnapshotSequence, previous.World, current.SnapshotSequence, current.World);
    }

    public static WorldDelta Create(long fromSnapshotSequence, WorldSnapshot previous,
        long toSnapshotSequence, WorldSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        if (fromSnapshotSequence <= 0 || toSnapshotSequence <= fromSnapshotSequence)
            throw new ArgumentOutOfRangeException(nameof(toSnapshotSequence),
                "A delta snapshot-sorszámainak pozitívnak és szigorúan növekvőnek kell lenniük.");
        if (previous.WorldId != current.WorldId || previous.Width != current.Width || previous.Height != current.Height)
            throw new ArgumentException("Eltérő pályák között teljes snapshot szükséges, delta nem készíthető.", nameof(current));

        var previousCells = previous.RevealedCells.ToDictionary(cell => cell.Position);
        var changedCells = current.RevealedCells.Where(cell =>
            !previousCells.TryGetValue(cell.Position, out var old) || old.TileCodePoint != cell.TileCodePoint).ToArray();
        var doorChanges = Upserts(previous.Doors, current.Doors, door => door.Position).ToArray();
        var currentDoorPositions = current.Doors.Select(door => door.Position).ToHashSet();
        var removedDoors = previous.Doors.Select(door => door.Position)
            .Where(position => !currentDoorPositions.Contains(position)).ToArray();

        var enemyChanges = Upserts(previous.Enemies, current.Enemies, enemy => enemy.EntityId, EnemyEquals).ToArray();
        var chestChanges = Upserts(previous.Chests, current.Chests, chest => chest.EntityId).ToArray();
        var corpseChanges = Upserts(previous.Corpses, current.Corpses, corpse => corpse.EntityId).ToArray();
        var pileChanges = Upserts(previous.GroundPiles, current.GroundPiles, pile => pile.EntityId,
            GroundPileEquals).ToArray();
        var previousEntities = EntityIds(previous).ToHashSet();
        var currentEntities = EntityIds(current).ToHashSet();

        return new WorldDelta(fromSnapshotSequence, toSnapshotSequence,
            previous.Entrance is null ? current.Entrance : null,
            previous.Exit is null ? current.Exit : null,
            changedCells, doorChanges, removedDoors, enemyChanges, chestChanges, corpseChanges, pileChanges,
            previousEntities.Where(id => !currentEntities.Contains(id)).ToArray());
    }

    private static IEnumerable<T> Upserts<T, TKey>(IEnumerable<T> previous, IEnumerable<T> current,
        Func<T, TKey> key, Func<T, T, bool>? equals = null) where TKey : notnull
    {
        equals ??= EqualityComparer<T>.Default.Equals;
        var previousById = previous.ToDictionary(key);
        return current.Where(item => !previousById.TryGetValue(key(item), out var old) || !equals(old, item));
    }

    private static IEnumerable<WorldEntityId> EntityIds(WorldSnapshot snapshot) =>
        snapshot.Enemies.Select(enemy => enemy.EntityId)
            .Concat(snapshot.Chests.Select(chest => chest.EntityId))
            .Concat(snapshot.Corpses.Select(corpse => corpse.EntityId))
            .Concat(snapshot.GroundPiles.Select(pile => pile.EntityId));

    private static bool EnemyEquals(WorldEnemySnapshot first, WorldEnemySnapshot second) =>
        first.EntityId == second.EntityId && first.DefinitionId == second.DefinitionId && first.Name == second.Name &&
        first.Position == second.Position && first.CurrentHitPoints == second.CurrentHitPoints &&
        first.MaximumHitPoints == second.MaximumHitPoints && first.GroupId == second.GroupId &&
        first.GroupRole == second.GroupRole && first.ActiveEffectTypes.SequenceEqual(second.ActiveEffectTypes) &&
        first.Color == second.Color && first.SymbolCodePoint == second.SymbolCodePoint;

    private static bool GroundPileEquals(WorldGroundPileSnapshot first, WorldGroundPileSnapshot second) =>
        first.EntityId == second.EntityId && first.Position == second.Position && first.Revision == second.Revision &&
        first.Items.SequenceEqual(second.Items) && first.SymbolCodePoint == second.SymbolCodePoint &&
        first.ForegroundColor == second.ForegroundColor && first.BackgroundColor == second.BackgroundColor;
}
