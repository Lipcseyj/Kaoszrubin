using MazeGame.Domain.Characters;
using MazeGame.Combat;

namespace MazeGame.Application;

/// <summary>A kliens által ismert pályarész teljes képe. Rejtett cellát vagy entitást nem tartalmaz.</summary>
public sealed record WorldSnapshot(WorldId WorldId, int Width, int Height, Position? Entrance, Position? Exit,
    IReadOnlyList<WorldCellSnapshot> RevealedCells, IReadOnlyList<WorldDoorSnapshot> Doors,
    IReadOnlyList<WorldEnemySnapshot> Enemies, IReadOnlyList<WorldChestSnapshot> Chests,
    IReadOnlyList<WorldCorpseSnapshot> Corpses, IReadOnlyList<WorldGroundPileSnapshot> GroundPiles);

public sealed record WorldCellSnapshot(Position Position, int TileCodePoint);

public sealed record WorldDoorSnapshot(Position Position, DoorState State);

public sealed record WorldEnemySnapshot(WorldEntityId EntityId, string DefinitionId, string Name,
    Position Position, int CurrentHitPoints, int MaximumHitPoints, string? GroupId,
    EnemyGroupRole GroupRole, IReadOnlyList<string> ActiveEffectTypes);

public sealed record WorldChestSnapshot(WorldEntityId EntityId, Position Position);

public sealed record WorldCorpseSnapshot(WorldEntityId EntityId, Position Position, string FormerName,
    CharacterId? PartyCharacterId, string? EnemyDefinitionId, bool IsSearched);

public sealed record WorldGroundPileSnapshot(WorldEntityId EntityId, Position Position, long Revision,
    IReadOnlyList<WorldItemSnapshot> Items);

public sealed record WorldItemSnapshot(string Category, string DefinitionId, string Name, int Charges,
    int MaximumCharges);

public static class WorldSnapshotProjector
{
    public static WorldSnapshot Create(Maze maze, FogOfWar fogOfWar, BattleState? activeBattle = null)
    {
        ArgumentNullException.ThrowIfNull(maze);
        ArgumentNullException.ThrowIfNull(fogOfWar);
        var visible = new HashSet<Position>();
        var cells = new List<WorldCellSnapshot>();
        for (var y = 0; y < maze.Height; y++)
        for (var x = 0; x < maze.Width; x++)
        {
            var position = new Position(x, y);
            // A host fejlesztői teljes-felfedése lokális segédeszköz; távoli kliensnek csak ténylegesen felfedett adat mehet.
            if (!fogOfWar.IsRevealed(position)) continue;
            visible.Add(position);
            cells.Add(new WorldCellSnapshot(position, maze.Tiles[x, y].Value));
        }
        bool IsVisible(Position position) => visible.Contains(position);

        var doors = maze.Doors.Where(door => IsVisible(door.Position))
            .Select(door => new WorldDoorSnapshot(door.Position, door.State)).ToArray();
        var enemies = maze.Enemies.Where(enemy => IsVisible(enemy.Position)).Select(enemy =>
        {
            var hitPoints = activeBattle is { IsCompleted: false } battle && battle.Enemy == enemy
                ? battle.CurrentEnemyHitPoints
                : enemy.CurrentHitPoints;
            return new WorldEnemySnapshot(enemy.Id, enemy.Definition.Id, enemy.Name, enemy.Position, hitPoints,
                enemy.Definition.HitPoints ?? hitPoints, enemy.GroupId, enemy.GroupRole,
                enemy.ActiveSpellEffects.Select(effect => effect.Type.ToString()).ToArray());
        }).ToArray();
        var chests = maze.TreasureChests.Where(chest => IsVisible(chest.Position))
            .Select(chest => new WorldChestSnapshot(chest.Id, chest.Position)).ToArray();
        var corpses = maze.Corpses.Where(corpse => IsVisible(corpse.Position)).Select(corpse =>
            new WorldCorpseSnapshot(corpse.Id, corpse.Position, corpse.FormerName,
                (corpse as PartyMemberCorpse)?.Character.Id, (corpse as MonsterCorpse)?.EnemyDefinitionId,
                (corpse as MonsterCorpse)?.IsSearched ?? false)).ToArray();
        var groundPiles = maze.GroundItemPiles.Where(pile => IsVisible(pile.Position)).Select(pile =>
            new WorldGroundPileSnapshot(pile.Id, pile.Position, pile.Revision, pile.Entries.Select(entry =>
                new WorldItemSnapshot(entry.Item.Category.ToString(), entry.Item.Id, entry.Item.Name, entry.Charges,
                    entry.Item is Domain.Magic.MagicItemDefinition magic ? magic.MaximumCharges : 0)).ToArray())).ToArray();

        return new WorldSnapshot(maze.Id, maze.Width, maze.Height,
            IsVisible(maze.Entrance) ? maze.Entrance : null,
            IsVisible(maze.Exit) ? maze.Exit : null,
            cells, doors, enemies, chests, corpses, groundPiles);
    }
}
