using MazeGame.Domain.Characters;
using MazeGame.Combat;

namespace MazeGame.Application;

/// <summary>A kliens által ismert pályarész teljes képe. Rejtett cellát vagy entitást nem tartalmaz.</summary>
public sealed record WorldSnapshot(WorldId WorldId, int Width, int Height, Position? Entrance, Position? Exit,
    IReadOnlyList<WorldCellSnapshot> RevealedCells, IReadOnlyList<WorldDoorSnapshot> Doors,
    IReadOnlyList<WorldEnemySnapshot> Enemies, IReadOnlyList<WorldChestSnapshot> Chests,
    IReadOnlyList<WorldCorpseSnapshot> Corpses, IReadOnlyList<WorldGroundPileSnapshot> GroundPiles,
    IReadOnlyList<WorldNpcSnapshot>? Npcs = null);

public sealed record WorldCellSnapshot(Position Position, int TileCodePoint,
    ConsoleColor ForegroundColor = ConsoleColor.Black, ConsoleColor BackgroundColor = ConsoleColor.Black);

public sealed record WorldDoorSnapshot(Position Position, DoorState State, int SymbolCodePoint = '▥',
    ConsoleColor ForegroundColor = ConsoleColor.Gray, ConsoleColor BackgroundColor = ConsoleColor.Black);

public sealed record WorldEnemySnapshot(WorldEntityId EntityId, string DefinitionId, string Name,
    Position Position, int CurrentHitPoints, int MaximumHitPoints, string? GroupId,
    EnemyGroupRole GroupRole, IReadOnlyList<string> ActiveEffectTypes, ConsoleColor Color = ConsoleColor.Red,
    int SymbolCodePoint = 'e');

public sealed record WorldChestSnapshot(WorldEntityId EntityId, Position Position, int SymbolCodePoint = '▣',
    ConsoleColor ForegroundColor = ConsoleColor.Yellow, ConsoleColor BackgroundColor = ConsoleColor.Black);

public sealed record WorldCorpseSnapshot(WorldEntityId EntityId, Position Position, string FormerName,
    CharacterId? PartyCharacterId, string? EnemyDefinitionId, bool IsSearched, int SymbolCodePoint = '†',
    ConsoleColor ForegroundColor = ConsoleColor.DarkRed, ConsoleColor BackgroundColor = ConsoleColor.Black);

public sealed record WorldGroundPileSnapshot(WorldEntityId EntityId, Position Position, long Revision,
    IReadOnlyList<WorldItemSnapshot> Items, int SymbolCodePoint = '◆',
    ConsoleColor ForegroundColor = ConsoleColor.Cyan, ConsoleColor BackgroundColor = ConsoleColor.Black);

public sealed record WorldItemSnapshot(string Category, string DefinitionId, string Name, int Charges,
    int MaximumCharges);

public sealed record WorldNpcSnapshot(WorldEntityId EntityId, string DefinitionId, string Name,
    Position Position, string Disposition, bool Recruitable, bool IsQuestNpc, int SymbolCodePoint,
    ConsoleColor ForegroundColor = ConsoleColor.White, ConsoleColor BackgroundColor = ConsoleColor.Black);

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
            var tile = maze.Tiles[x, y];
            var shownTrap = maze.GetTrapAt(position) is { State: not TrapState.Hidden } trap ? trap : null;
            if (shownTrap is not null) tile = shownTrap.Symbol;
            var color = shownTrap is not null
                ? shownTrap.State == TrapState.Detected ? ConsoleColor.Yellow : ConsoleColor.DarkGray
                : tile == maze.WallRune
                ? maze.WallColor
                : tile == Maze.ExitMarker ? ConsoleColor.Green : ConsoleColor.Black;
            cells.Add(new WorldCellSnapshot(position, tile.Value, color));
        }
        bool IsVisible(Position position) => visible.Contains(position);

        var doors = maze.Doors.Where(door => IsVisible(door.Position))
            .Select(door => new WorldDoorSnapshot(door.Position, door.State, door.Symbol.Value,
                door.State switch
                {
                    DoorState.Locked => ConsoleColor.Red,
                    DoorState.Open => ConsoleColor.DarkGreen,
                    DoorState.Closed => ConsoleColor.DarkYellow,
                    DoorState.Smashed => ConsoleColor.DarkGray,
                    _ => ConsoleColor.Gray
                })).ToArray();
        var enemies = maze.Enemies.Where(enemy => IsVisible(enemy.Position)).Select(enemy =>
        {
            var hitPoints = activeBattle is { IsCompleted: false } battle && battle.Enemy == enemy
                ? battle.CurrentEnemyHitPoints
                : enemy.CurrentHitPoints;
            return new WorldEnemySnapshot(enemy.Id, enemy.Definition.Id, enemy.Name, enemy.Position, hitPoints,
                enemy.Definition.HitPoints ?? hitPoints, enemy.GroupId, enemy.GroupRole,
                enemy.ActiveSpellEffects.Select(effect => effect.Type.ToString()).ToArray(),
                enemy.Definition.StrengthTier switch
                {
                    1 => ConsoleColor.Green,
                    2 => ConsoleColor.Yellow,
                    3 => ConsoleColor.DarkYellow,
                    4 => ConsoleColor.Red,
                    5 => ConsoleColor.Magenta,
                    _ => ConsoleColor.Gray
                }, enemy.Symbol.Value);
        }).ToArray();
        var chests = maze.TreasureChests.Where(chest => IsVisible(chest.Position))
            .Select(chest => new WorldChestSnapshot(chest.Id, chest.Position, chest.Symbol.Value)).ToArray();
        var corpses = maze.Corpses.Where(corpse => IsVisible(corpse.Position)).Select(corpse =>
            new WorldCorpseSnapshot(corpse.Id, corpse.Position, corpse.FormerName,
                (corpse as PartyMemberCorpse)?.Character.Id, (corpse as MonsterCorpse)?.EnemyDefinitionId,
                (corpse as MonsterCorpse)?.IsSearched ?? false, corpse.Symbol.Value)).ToArray();
        var groundPiles = maze.GroundItemPiles.Where(pile => IsVisible(pile.Position)).Select(pile =>
            new WorldGroundPileSnapshot(pile.Id, pile.Position, pile.Revision, pile.Entries.Select(entry =>
                new WorldItemSnapshot(entry.Item.Category.ToString(), entry.Item.Id, entry.Item.Name, entry.Charges,
                    entry.Item is Domain.Magic.MagicItemDefinition magic ? magic.MaximumCharges : 0)).ToArray(),
                pile.Symbol.Value)).ToArray();
        var npcs = maze.WorldNpcs.Where(npc => IsVisible(npc.Position)).Select(npc =>
            new WorldNpcSnapshot(npc.Id, npc.DefinitionId, npc.Character.Name, npc.Position,
                npc.Disposition.ToString(), npc.Recruitable, npc.IsQuestNpc, npc.Symbol.Value,
                ConsoleColor.White, npc.Character.Color)).ToArray();

        return new WorldSnapshot(maze.Id, maze.Width, maze.Height,
            IsVisible(maze.Entrance) ? maze.Entrance : null,
            IsVisible(maze.Exit) ? maze.Exit : null,
            cells, doors, enemies, chests, corpses, groundPiles, npcs);
    }
}
