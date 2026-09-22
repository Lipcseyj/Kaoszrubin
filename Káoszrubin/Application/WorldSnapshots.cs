using KaoszRubin.Domain.Characters;
using KaoszRubin.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Quests;
using KaoszRubin.Infrastructure.Quests;
using System.Text.Json.Serialization;

namespace KaoszRubin.Application;

/// <summary>A kliens által ismert pályarész teljes képe. Rejtett cellát vagy entitást nem tartalmaz.</summary>
public sealed record WorldSnapshot(WorldId WorldId, int Width, int Height, Position? Entrance, Position? Exit,
    IReadOnlyList<WorldCellSnapshot> RevealedCells, IReadOnlyList<WorldDoorSnapshot> Doors,
    IReadOnlyList<WorldEnemySnapshot> Enemies, IReadOnlyList<WorldChestSnapshot> Chests,
    IReadOnlyList<WorldCorpseSnapshot> Corpses, IReadOnlyList<WorldGroundPileSnapshot> GroundPiles,
    IReadOnlyList<WorldNpcSnapshot>? Npcs = null,
    IReadOnlyList<WorldLastKnownEnemySnapshot>? LastKnownEnemies = null);

public sealed record WorldCellSnapshot(Position Position, int TileCodePoint,
    ConsoleColor ForegroundColor = ConsoleColor.Black, ConsoleColor BackgroundColor = ConsoleColor.Black);

public sealed record WorldDoorSnapshot(Position Position, DoorState State, int SymbolCodePoint = '▥',
    ConsoleColor ForegroundColor = ConsoleColor.Gray, ConsoleColor BackgroundColor = ConsoleColor.Black,
    bool IsQuestSealed = false);

public sealed record WorldEnemySnapshot(WorldEntityId EntityId, string DefinitionId, string Name,
    Position Position, int CurrentHitPoints, int MaximumHitPoints, string? GroupId,
    EnemyGroupRole GroupRole, IReadOnlyList<string> ActiveEffectTypes, ConsoleColor Color = ConsoleColor.Red,
    int SymbolCodePoint = 'e', ConsoleColor BackgroundColor = ConsoleColor.Black, int BossTier = 0);

public sealed record WorldLastKnownEnemySnapshot(WorldEntityId EntityId, Position Position,
    int RemainingPartyMoves, bool IsSoundCue = false);

public sealed record WorldChestSnapshot(WorldEntityId EntityId, Position Position, int SymbolCodePoint = '▣',
    ConsoleColor ForegroundColor = ConsoleColor.Yellow, ConsoleColor BackgroundColor = ConsoleColor.Black,
    string? DefinitionId = null, string? Name = null, bool IsOpened = false, int RemainingItemCount = 0);

public sealed record WorldCorpseSnapshot(WorldEntityId EntityId, Position Position, string FormerName,
    CharacterId? PartyCharacterId, string? EnemyDefinitionId, bool IsSearched, int SymbolCodePoint = '†',
    ConsoleColor ForegroundColor = ConsoleColor.DarkRed, ConsoleColor BackgroundColor = ConsoleColor.Black);

public sealed record WorldGroundPileSnapshot(WorldEntityId EntityId, Position Position, long Revision,
    IReadOnlyList<WorldItemSnapshot> Items, int SymbolCodePoint = '◆',
    ConsoleColor ForegroundColor = ConsoleColor.Cyan, ConsoleColor BackgroundColor = ConsoleColor.Black);

public sealed record WorldItemSnapshot(string Category, string DefinitionId, string Name, int Charges,
    int MaximumCharges, Guid InstanceId = default, bool IsIdentified = true,
    int MaximumDurability = 0, int DurabilityDamage = 0);

public sealed record WorldNpcSnapshot(WorldEntityId EntityId, string DefinitionId, string Name,
    Position Position, string Disposition, bool Recruitable, bool IsQuestNpc, int SymbolCodePoint,
    ConsoleColor ForegroundColor = ConsoleColor.White, ConsoleColor BackgroundColor = ConsoleColor.Black,
    int Friendliness = 5, string Behavior = "Guarded", int QuestInstanceId = 0,
    IReadOnlyList<WorldQuestSnapshot>? Quests = null);

/// <summary>A host típusos állapotából másolt questfutás; a kliens nem futtat questműveleteket.</summary>
public sealed record WorldQuestSnapshot(
    [property: JsonRequired, JsonConverter(typeof(QuestKeyJsonConverter))] QuestKey Key,
    [property: JsonConverter(typeof(JsonStringEnumConverter<QuestState>))] QuestState State,
    int Progress, int RequiredCount, int CompletionCount);

public sealed record WorldNpcQuestData(int InstanceId, IReadOnlyList<WorldQuestSnapshot> Quests);

public static class WorldSnapshotProjector
{
    public static WorldSnapshot Create(Maze maze, FogOfWar fogOfWar,
        IReadOnlySet<WorldEntityId>? forcedVisibleEnemies = null,
        Func<WorldNpc, WorldNpcQuestData>? projectQuests = null)
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
            if (maze.GetPassageAt(position) is not null) tile = MazePassage.Symbol;
            var shownTrap = maze.GetTrapAt(position) is { State: not TrapState.Hidden } trap ? trap : null;
            if (shownTrap is not null) tile = shownTrap.Symbol;
            var color = shownTrap is not null
                ? shownTrap.State == TrapState.Detected ? ConsoleColor.Yellow : ConsoleColor.DarkGray
                : tile == maze.WallRune
                ? maze.WallColor
                : maze.GetPassageAt(position) is not null ? ConsoleColor.Cyan
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
                }, IsQuestSealed: door.IsQuestSealed)).ToArray();
        var enemies = maze.Enemies.Where(enemy => fogOfWar.IsEnemyVisible(enemy.Id, enemy.Position) ||
                                                  forcedVisibleEnemies?.Contains(enemy.Id) == true).Select(enemy =>
        {
            var hitPoints = enemy.CurrentHitPoints;
            return new WorldEnemySnapshot(enemy.Id, enemy.Definition.Id, enemy.Name, enemy.Position, hitPoints,
                enemy.MaximumHitPoints, enemy.GroupId, enemy.GroupRole,
                enemy.ActiveSpellEffects.Select(effect => effect.Type.ToString()).ToArray(),
                enemy.BossTier > 0 ? ConsoleColor.Black : enemy.Definition.StrengthTier switch
                {
                    1 => ConsoleColor.Green,
                    2 => ConsoleColor.Yellow,
                    3 => ConsoleColor.DarkYellow,
                    4 => ConsoleColor.Red,
                    5 => ConsoleColor.Magenta,
                    _ => ConsoleColor.Gray
                }, enemy.Symbol.Value,
                enemy.BossTier > 0 ? BossTierRules.Background(enemy.BossTier) : ConsoleColor.Black,
                enemy.BossTier);
        }).ToArray();
        var chests = maze.TreasureChests.Where(chest => IsVisible(chest.Position))
            .Select(chest => new WorldChestSnapshot(chest.Id, chest.Position, chest.Symbol.Value,
                DefinitionId: chest.Definition?.Id.Value, Name: chest.Definition?.Name,
                IsOpened: chest.IsOpened, RemainingItemCount: chest.RemainingItems.Sum(item => item.Quantity))).ToArray();
        var corpses = maze.Corpses.Where(corpse => IsVisible(corpse.Position)).Select(corpse =>
            new WorldCorpseSnapshot(corpse.Id, corpse.Position, corpse.FormerName,
                (corpse as PartyMemberCorpse)?.Character.Id, (corpse as MonsterCorpse)?.EnemyDefinitionId,
                (corpse as MonsterCorpse)?.IsSearched ?? false, corpse.Symbol.Value)).ToArray();
        var groundPiles = maze.GroundItemPiles.Where(pile => IsVisible(pile.Position)).Select(pile =>
            new WorldGroundPileSnapshot(pile.Id, pile.Position, pile.Revision, pile.Entries.Select(entry =>
                new WorldItemSnapshot(entry.Item.Category.ToString(),
                    entry.State.IsIdentified ? entry.Item.Id : string.Empty,
                    Domain.Inventory.ItemIdentificationRules.DisplayName(entry.Item, entry.State.IsIdentified),
                    entry.State.IsIdentified ? entry.Charges : 0,
                    entry.State.IsIdentified && entry.Item is Domain.Magic.MagicItemDefinition magic
                        ? magic.MaximumCharges : 0,
                    entry.State.InstanceId, entry.State.IsIdentified,
                    EquipmentDurabilityRules.MaximumDurability(entry.Item),
                    Math.Max(0, entry.State.DurabilityDamage))).ToArray(),
                pile.Symbol.Value)).ToArray();
        var worldNpcs = maze.WorldNpcs.Select(npc => (Npc: npc, npc.Position)).Concat(maze.PartyMembers
            .Where(member => member.TemporaryFollower is not null)
            .Select(member => (Npc: member.TemporaryFollower!, member.Position)));
        var npcs = worldNpcs.Where(entry => IsVisible(entry.Position)).Select(entry =>
        {
            var npc = entry.Npc;
            var quests = npc.IsQuestNpc
                ? projectQuests?.Invoke(npc) ?? throw new InvalidOperationException("A quest NPC projekciójához típusos állapotforrás szükséges.")
                : new WorldNpcQuestData(0, []);
            return new WorldNpcSnapshot(npc.Id, npc.DefinitionId, npc.Character.Name, entry.Position,
                npc.Disposition.ToString(), npc.Recruitable, npc.IsQuestNpc, npc.Symbol.Value,
                ConsoleColor.White, npc.Character.Color, npc.Friendliness, npc.Behavior.ToString(),
                quests.InstanceId, quests.Quests);
        }).ToArray();

        return new WorldSnapshot(maze.Id, maze.Width, maze.Height,
            IsVisible(maze.Entrance) ? maze.Entrance : null,
            IsVisible(maze.Exit) ? maze.Exit : null,
            cells, doors, enemies, chests, corpses, groundPiles, npcs,
            fogOfWar.EnemyMemories.Select(memory => new WorldLastKnownEnemySnapshot(memory.Key,
                memory.Value.Position, memory.Value.RemainingPartyMoves, memory.Value.IsSoundCue)).ToArray());
    }
}
