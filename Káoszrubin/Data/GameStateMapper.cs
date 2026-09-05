using System.Text;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Data;

internal sealed class GameStateMapper
{
    private const int VisionRange = 5;
    private readonly GameDataCatalog _gameData;
    private readonly CharacterRoster _characterRoster;
    private readonly LiveCharacter _selectedCharacter;

    public GameStateMapper(GameDataCatalog gameData, CharacterRoster characterRoster,
        LiveCharacter selectedCharacter)
    {
        _gameData = gameData;
        _characterRoster = characterRoster;
        _selectedCharacter = selectedCharacter;
    }

    public GameSaveData Create(int mazeLevel, Maze maze, Player player, FogOfWar fogOfWar,
        Direction leaderFacing, IReadOnlyList<Position> leaderTrail, bool partyHoldingPosition,
        bool partyRegrouping, bool partyAttackMode, bool hasRestedThisLevel, DateTime? partyScatterUntil,
        DateTime nextNeedsDrain,
        IReadOnlyDictionary<Enemy, DateTime> nextEnemyMoves, IReadOnlyCollection<string> collectedBossKeyIds,
        IReadOnlyCollection<string> seenBossIds)
    {
        var now = DateTime.UtcNow;
        var mazeData = new MazeSaveData
        {
            Width = maze.Width,
            Height = maze.Height,
            WallCodePoint = maze.WallRune.Value,
            WallColor = maze.WallColor,
            LevelName = maze.LevelName,
            Exit = maze.Exit,
            StartingRoom = maze.StartingRoom,
            Rooms = maze.Rooms.Where(room => room != maze.StartingRoom).ToList(),
            Doors = maze.Doors.Select(door => new DoorSaveData(door.Position, door.State)).ToList(),
            Chests = maze.TreasureChests.Select(chest => new ChestSaveData(chest.Position, chest.GoldAmount)).ToList(),
            Enemies = maze.Enemies.Select(enemy => new EnemySaveData(enemy.Position, enemy.Definition.Id,
                enemy.CurrentHitPoints, enemy.MovementProfile, enemy.PatrolDirection, enemy.PursuitState,
                Math.Max(0, (int)(nextEnemyMoves.GetValueOrDefault(enemy, now) - now).TotalMilliseconds),
                enemy.GroupId, enemy.GroupRole, enemy.ActiveSpellEffects.ToList(),
                enemy.PursuitTargetCharacterId, enemy.PursuitMemoryRemainingMoves,
                enemy.GuaranteedLootIds.ToList(), enemy.Alertness, enemy.SearchRole,
                enemy.HomePosition, enemy.LastKnownTargetPosition, enemy.ReactionDelayMovesRemaining,
                enemy.SearchMovesRemaining, enemy.ReturnDelayMovesRemaining,
                enemy.Definition.Weapon?.Id)).ToList(),
            Corpses = maze.Corpses.Select(corpse => new CorpseSaveData(corpse.Position, corpse.FormerName,
                corpse is PartyMemberCorpse partyCorpse ? CharacterIndex(partyCorpse.Character) : null,
                (corpse as MonsterCorpse)?.EnemyDefinitionId, (corpse as MonsterCorpse)?.IsSearched ?? false,
                (corpse as MonsterCorpse)?.GuaranteedLootIds.ToList(),
                (corpse as MonsterCorpse)?.CarriedWeaponIds.ToList())).ToList(),
            PartyAvatars = maze.PartyMembers.Select(member => new PartyAvatarSaveData(member.Position,
                CharacterIndex(member.Character), member.TemporaryFollower is { } follower
                    ? SaveWorldNpc(follower) : null)).ToList(),
            Npcs = maze.WorldNpcs.Select(SaveWorldNpc).ToList(),
            GroundPiles = maze.GroundItemPiles.Select(pile => new GroundPileSaveData(pile.Position,
                pile.Entries.Select(entry => new SavedItemReference(entry.Item.Category.ToString(), entry.Item.Id,
                    entry.Charges)).ToList())).ToList(),
            Traps = maze.Traps.Select(trap => new TrapSaveData(trap.Position, trap.Definition.Id, trap.State,
                trap.DetectionAttempted, trap.FailedDisarmAttempts)).ToList()
        };
        for (var y = 0; y < maze.Height; y++)
        for (var x = 0; x < maze.Width; x++) mazeData.TileCodePoints.Add(maze.Tiles[x, y].Value);

        return new GameSaveData
        {
            MainCharacterName = _selectedCharacter.Name,
            MazeLevel = mazeLevel,
            CollectedBossKeyIds = collectedBossKeyIds.ToList(),
            SeenBossIds = seenBossIds.ToList(),
            PlayerPosition = player.Position,
            LeaderFacing = leaderFacing,
            LeaderTrail = leaderTrail.ToList(),
            PartyHoldingPosition = partyHoldingPosition,
            PartyRegrouping = partyRegrouping,
            PartyAttackMode = partyAttackMode,
            HasRestedThisLevel = hasRestedThisLevel,
            ScatterRemainingMilliseconds = partyScatterUntil is { } scatter
                ? Math.Max(0, (int)(scatter - now).TotalMilliseconds) : 0,
            NeedsDrainRemainingMilliseconds = Math.Max(0, (int)(nextNeedsDrain - now).TotalMilliseconds),
            EnemyMoveRemainingMilliseconds = maze.Enemies.Count == 0 ? 0 : maze.Enemies.Min(enemy =>
                Math.Max(0, (int)(nextEnemyMoves.GetValueOrDefault(enemy, now) - now).TotalMilliseconds)),
            Maze = mazeData,
            Fog = new FogSaveData
            {
                RevealedPositions = fogOfWar.GetRevealedPositions().ToList(),
#if DEBUG
                DeveloperRevealActive = fogOfWar.IsDeveloperRevealActive
#else
                DeveloperRevealActive = false
#endif
            }
        };
    }

    public RestoredGameState Restore(GameSaveData state)
    {
        if (state.Maze.TileCodePoints.Count != state.Maze.Width * state.Maze.Height)
            throw new InvalidOperationException("A mentett térképrács mérete érvénytelen.");

        var now = DateTime.UtcNow;
        var mazeLevel = Math.Max(1, state.MazeLevel);
        var wallRune = state.Maze.WallCodePoint > 0
            ? new Rune(state.Maze.WallCodePoint)
            : Maze.Wall;
        var maze = new Maze(state.Maze.Width, state.Maze.Height, wallRune,
            state.Maze.WallColor, state.Maze.LevelName);
        var tileIndex = 0;
        for (var y = 0; y < maze.Height; y++)
        for (var x = 0; x < maze.Width; x++)
            maze.SetTile(new Position(x, y), new Rune(state.Maze.TileCodePoints[tileIndex++]));
        if (state.Maze.StartingRoom is { } startingRoom) maze.SetStartingRoom(startingRoom);
        foreach (var room in state.Maze.Rooms) maze.AddRoom(room);
        foreach (var door in state.Maze.Doors) maze.PlaceDoor(door.Position, door.State);
        maze.PlaceExit(state.Maze.Exit);
        foreach (var chest in state.Maze.Chests) maze.AddTreasureChest(new TreasureChest(chest.Position, chest.GoldAmount));

        var nextEnemyMoves = new Dictionary<Enemy, DateTime>();
        foreach (var savedEnemy in state.Maze.Enemies)
        {
            var enemy = new ConfiguredEnemy(savedEnemy.Position, _gameData.GetEnemy(savedEnemy.DefinitionId),
                selectedWeaponId: savedEnemy.SelectedWeaponId);
            enemy.SetCurrentHitPoints(savedEnemy.CurrentHitPoints);
            enemy.ConfigureMovement(savedEnemy.MovementProfile, savedEnemy.PatrolDirection, savedEnemy.PursuitState,
                savedEnemy.PursuitTargetCharacterId, savedEnemy.PursuitMemoryRemainingMoves);
            enemy.ConfigureAwareness(savedEnemy.Alertness, savedEnemy.HomePosition,
                savedEnemy.SearchRole, savedEnemy.LastKnownTargetPosition,
                savedEnemy.ReactionDelayMovesRemaining, savedEnemy.SearchMovesRemaining,
                savedEnemy.ReturnDelayMovesRemaining);
            enemy.ConfigureGroup(savedEnemy.GroupId, savedEnemy.GroupRole);
            enemy.ConfigureGuaranteedLoot(savedEnemy.GuaranteedLootIds ?? []);
            foreach (var effect in savedEnemy.ActiveSpellEffects ?? []) enemy.RestoreSpellEffect(effect);
            maze.AddEnemy(enemy);
            var remaining = savedEnemy.NextMoveRemainingMilliseconds >= 0
                ? savedEnemy.NextMoveRemainingMilliseconds
                : Math.Max(0, state.EnemyMoveRemainingMilliseconds);
            nextEnemyMoves[enemy] = now + TimeSpan.FromMilliseconds(remaining);
        }
        foreach (var avatar in state.Maze.PartyAvatars)
            if (avatar.CharacterIndex >= 0 && avatar.CharacterIndex < _characterRoster.Characters.Count)
            {
                WorldNpc? follower = avatar.TemporaryFollower is { } savedFollower
                    ? RestoreWorldNpc(savedFollower) : null;
                maze.AddPartyMember(new PartyMemberAvatar(avatar.Position,
                    _characterRoster.Characters[avatar.CharacterIndex], follower));
            }
        foreach (var npc in state.Maze.Npcs ?? [])
            if (npc.CharacterIndex >= 0 && npc.CharacterIndex < _characterRoster.Characters.Count)
            {
                maze.AddWorldNpc(RestoreWorldNpc(npc));
            }
        foreach (var corpse in state.Maze.Corpses)
        {
            var restored = corpse.PartyCharacterIndex is >= 0 and var characterIndex && characterIndex < _characterRoster.Characters.Count
                ? new PartyMemberCorpse(corpse.Position, _characterRoster.Characters[characterIndex])
                : corpse.EnemyDefinitionId is { Length: > 0 } enemyDefinitionId
                    ? new MonsterCorpse(corpse.Position, corpse.FormerName, enemyDefinitionId, corpse.IsSearched,
                        corpse.GuaranteedLootIds, corpse.CarriedWeaponIds)
                    : new Corpse(corpse.Position, corpse.FormerName);
            maze.AddCorpse(restored);
        }
        foreach (var pile in state.Maze.GroundPiles)
            foreach (var item in pile.Items) maze.DropItem(pile.Position, ResolveSavedItem(item), item.Charges);
        foreach (var trap in state.Maze.Traps)
            maze.AddTrap(new MazeTrap(trap.Position, _gameData.GetTrap(trap.DefinitionId), trap.State,
                trap.DetectionAttempted, trap.FailedDisarmAttempts));

        var player = new Player(state.PlayerPosition, _selectedCharacter);
        var fogOfWar = new FogOfWar(maze.Width, maze.Height, VisionRange);
#if DEBUG
        fogOfWar.Restore(state.Fog.RevealedPositions, state.Fog.DeveloperRevealActive);
#else
        fogOfWar.Restore(state.Fog.RevealedPositions, developerRevealActive: false);
#endif

        return new RestoredGameState(
            mazeLevel,
            maze,
            player,
            fogOfWar,
            state.LeaderFacing,
            state.LeaderTrail.Count > 0 ? state.LeaderTrail.ToList() : [state.PlayerPosition],
            state.PartyHoldingPosition,
            state.PartyRegrouping,
            state.PartyAttackMode,
            state.HasRestedThisLevel,
            state.ScatterRemainingMilliseconds > 0 ? now + TimeSpan.FromMilliseconds(state.ScatterRemainingMilliseconds) : null,
            now + TimeSpan.FromMilliseconds(Math.Max(0, state.NeedsDrainRemainingMilliseconds)),
            nextEnemyMoves);
    }

    private int CharacterIndex(LiveCharacter character) => Enumerable.Range(0, _characterRoster.Characters.Count)
        .First(index => _characterRoster.Characters[index] == character);

    private WorldNpcSaveData SaveWorldNpc(WorldNpc npc) => new(npc.Position, npc.DefinitionId,
        CharacterIndex(npc.Character), npc.Disposition, npc.Recruitable, npc.IsQuestNpc,
        npc.Dialogue, npc.State, npc.Friendliness, npc.Behavior, npc.QuestIds.ToList(),
        npc.Quests.ToList(), npc.ConversationStage, npc.StoryId, npc.StoryStateId);

    private WorldNpc RestoreWorldNpc(WorldNpcSaveData saved)
    {
        var restored = new WorldNpc(saved.Position, saved.DefinitionId,
            _characterRoster.Characters[saved.CharacterIndex], saved.Disposition, saved.Recruitable,
            saved.IsQuestNpc, saved.Dialogue, saved.State, saved.Friendliness, saved.Behavior, saved.QuestIds,
            saved.StoryId, saved.StoryStateId);
        restored.RestoreQuests(saved.Quests ?? []);
        restored.RestoreConversationStage(saved.ConversationStage);
        return restored;
    }

    private IItemDefinition ResolveSavedItem(SavedItemReference item) => item.Category switch
    {
        nameof(ItemCategory.Weapon) => _gameData.GetWeapon(item.Id),
        nameof(ItemCategory.Armor) => _gameData.GetArmor(item.Id),
        nameof(ItemCategory.MagicItem) => _gameData.GetMagicItem(item.Id),
        nameof(ItemCategory.Miscellaneous) => _gameData.GetItem(item.Id),
        _ => throw new InvalidOperationException($"Ismeretlen mentett tárgykategória: {item.Category}")
    };
}

internal sealed record RestoredGameState(int MazeLevel, Maze Maze, Player Player, FogOfWar FogOfWar,
    Direction LeaderFacing, IReadOnlyList<Position> LeaderTrail, bool PartyHoldingPosition,
    bool PartyRegrouping, bool PartyAttackMode, bool HasRestedThisLevel, DateTime? PartyScatterUntil,
    DateTime NextNeedsDrain,
    IReadOnlyDictionary<Enemy, DateTime> NextEnemyMoves);
