using KaoszRubin.Application;
using KaoszRubin.Application.Quests;
using KaoszRubin.Combat;
using KaoszRubin.Data;
using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;
using KaoszRubin.Domain.Quests;
using KaoszRubin.Infrastructure;
using KaoszRubin.Infrastructure.Quests;
using KaoszRubin.UI;
using System.Runtime;
using System.Security.Cryptography.Xml;
using static KaoszRubin.UI.GameInput;
using MainMenu = KaoszRubin.UI.MainMenu;

namespace KaoszRubin.Application;

public sealed partial class Game
{
    private void TeleportPartyToSelectedPosition()
    {
        var companions = _maze.PartyMembers.Where(member => member.Character.IsAlive).ToArray();
        var target = RunHostWindow<Position?>("Fejlesztői teleport",
            "A vezető teleportálási célpontot választ az aktuális pályán.", () =>
            {
                var cursor = _player.Position;
                Position? previous = null;
                var wasRevealed = _fogOfWar.IsDeveloperRevealActive;
                if (!wasRevealed) _fogOfWar.ToggleDeveloperReveal();
                _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
                try
                {
                    while (true)
                    {
                        var valid = DeveloperPartyTeleport.FindDestinations(_maze, cursor, companions).Count > 0;
                        _renderer.DrawSpellTargetCursor(_maze, _fogOfWar, previous, cursor, valid,
                            $"Teleport ({cursor.X}, {cursor.Y}) | Nyilak: célpont | Enter: teleport | Esc: mégse" +
                            (valid ? string.Empty : " | Nincs szabad hely a csapatnak"));
                        previous = cursor;
                        var key = Console.ReadKey(intercept: true);
                        if (key.Key == ConsoleKey.Escape) return null;
                        if (key.Key == ConsoleKey.Enter && valid) return cursor;
                        if (TryGetDirection(key.Key, out var direction) && _maze.IsInside(cursor + direction))
                            cursor += direction;
                    }
                }
                finally
                {
                    if (!wasRevealed) _fogOfWar.ToggleDeveloperReveal();
                    _renderer.FinishSpellTargeting(_maze, _fogOfWar, _player.Position);
                }
            });
        if (target is null) return;
        var positions = DeveloperPartyTeleport.FindDestinations(_maze, target.Value, companions);
        if (positions.Count != companions.Length + 1) return;
        _player.TeleportTo(positions[0]);
        for (var index = 0; index < companions.Length; index++)
        {
            companions[index].MoveTo(positions[index + 1]);
            ScheduleNextPartyMove(companions[index], DateTime.UtcNow);
        }
        _leaderTrail.Clear();
        _leaderTrail.Add(_player.Position);
        _formation = PartyFormationRules.WithState(_formation, PartyFormationState.Disbanded);
        _partyHoldingPosition = false;
        _partyRegrouping = false;
        _partyAttackMode = false;
        _partyScatterUntil = DateTime.MinValue;
        RevealFor(PartyLeader, _player.Position);
        foreach (var member in companions) RevealFor(member.Character, member.Position);
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        _renderer.DrawDeveloperMessage($"Fejlesztői teleport: vezér és {companions.Length} társ → ({target.Value.X}, {target.Value.Y}).");
        ForceCoopSnapshotPublish();
    }

    private void TeleportLeaderNearExit()
    {
        Position? destination = Directions
            .Select(direction => _maze.Exit + direction)
            .Where(position => _maze.IsWalkable(position) && _maze.GetObjectAt(position) is null)
            .OrderBy(position => Manhattan(position, _player.Position))
            .Select(position => (Position?)position)
            .FirstOrDefault();
        if (destination is null)
        {
            _renderer.DrawDeveloperMessage("Fejlesztői mód: nincs üres járható mező a kijárat mellett.");
            return;
        }

        _player.TeleportTo(destination.Value);
        _leaderTrail.Clear();
        _leaderTrail.Add(destination.Value);
        RevealFor(PartyLeader, destination.Value);
        // A fejlesztői teleport közvetlenül is jelzi a kijárat elérését; ne függjön
        // attól, hogy az általános látómező-frissítés új cellának számította-e a kijáratot.
        _backgroundMusic.MarkExitDiscovered();
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, destination.Value);
        _renderer.DrawDeveloperMessage("Fejlesztői mód: a partyvezér a kijárat mellé teleportált.");
    }

    private void TeleportLeaderToNextUniqueNpc()
    {
        var targets = _gameData.NpcEncounters
            .Where(encounter => _gameData.GetNpc(encounter.NpcId).Unique)
            .GroupBy(encounter => encounter.NpcId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(encounter => encounter.MazeLevel).First())
            .OrderBy(encounter => encounter.MazeLevel)
            .ThenBy(encounter => encounter.NpcId, StringComparer.OrdinalIgnoreCase)
            .Select(encounter => new DeveloperUniqueNpcTarget(_gameData.GetNpc(encounter.NpcId),
                encounter.MazeLevel))
            .ToArray();
        if (targets.Length == 0)
        {
            _renderer.DrawDeveloperMessage("Fejlesztői mód: nincs pályához rendelt egyedi NPC.");
            return;
        }

        _lastDeveloperUniqueNpcIndex = (_lastDeveloperUniqueNpcIndex + 1) % targets.Length;
        var target = targets[_lastDeveloperUniqueNpcIndex];
        if (!TryFindUniqueNpcPosition(target.Definition, out var npcPosition))
        {
            RemoveStaleUniqueNpcCharacter(target.Definition);
            _mazeLevel = target.MazeLevel;
            StartNewMaze(showLevelImage: false);
            if (!TryFindUniqueNpcPosition(target.Definition, out npcPosition))
            {
                _renderer.DrawDeveloperMessage($"Fejlesztői mód: {target.Definition.Name} nem helyezhető el a(z) " +
                    $"{target.MazeLevel}. pályán.");
                return;
            }
        }

        Position? destination = Directions
            .Select(direction => npcPosition + direction)
            .Where(IsFreeDeveloperTeleportDestination)
            .OrderBy(position => Manhattan(position, _player.Position))
            .Select(position => (Position?)position)
            .FirstOrDefault();
        destination ??= FindNearbyFreePositions(npcPosition)
            .Where(position => _maze.GetTrapAt(position) is null && _maze.GetDoorAt(position) is null)
            .Select(position => (Position?)position)
            .FirstOrDefault();
        if (destination is null)
        {
            _renderer.DrawDeveloperMessage($"Fejlesztői mód: nincs szabad mező {target.Definition.Name} mellett.");
            return;
        }

        _player.TeleportTo(destination.Value);
        _leaderTrail.Clear();
        _leaderTrail.Add(destination.Value);
        RevealFor(PartyLeader, destination.Value);
        RevealFor(PartyLeader, npcPosition);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, destination.Value);
        _renderer.DrawDeveloperMessage($"Fejlesztői mód: egyedi NPC " +
            $"{_lastDeveloperUniqueNpcIndex + 1}/{targets.Length} — {target.Definition.Name}, " +
            $"{_mazeLevel}. pálya.");
        RequestCoopSnapshotPublish();
    }

    private bool TryFindUniqueNpcPosition(NpcDefinition definition, out Position position)
    {
        var worldNpc = _maze.WorldNpcs.FirstOrDefault(npc =>
            string.Equals(npc.DefinitionId, definition.Id, StringComparison.OrdinalIgnoreCase));
        if (worldNpc is not null)
        {
            position = worldNpc.Position;
            return true;
        }

        var avatar = _maze.PartyMembers.FirstOrDefault(member =>
            string.Equals(member.TemporaryFollower?.DefinitionId, definition.Id,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(member.Character.Name, definition.Name, StringComparison.OrdinalIgnoreCase));
        if (avatar is not null)
        {
            position = avatar.Position;
            return true;
        }

        if (string.Equals(PartyLeader.Name, definition.Name, StringComparison.OrdinalIgnoreCase))
        {
            position = _player.Position;
            return true;
        }

        position = default;
        return false;
    }

    private void RemoveStaleUniqueNpcCharacter(NpcDefinition definition)
    {
        var partyMembers = CharacterRoster.Party.Members.ToHashSet();
        foreach (var character in CharacterRoster.Characters.Where(character =>
                     !partyMembers.Contains(character) &&
                     string.Equals(character.Name, definition.Name, StringComparison.OrdinalIgnoreCase)).ToArray())
            CharacterRoster.Remove(character);
    }

    private bool IsFreeDeveloperTeleportDestination(Position position) =>
        _maze.IsWalkable(position) && _maze.GetObjectAt(position) is null &&
        _maze.GetTrapAt(position) is null && _maze.GetDoorAt(position) is null;

    private void ToggleDeveloperPhasing()
    {
        _developerPhasing = !_developerPhasing;
        _renderer.DrawDeveloperMessage(_developerPhasing
            ? "Fejlesztői mód: fal-áthaladás engedélyezve."
            : "Fejlesztői mód: fal-áthaladás letiltva.");
    }

    private void StartDeveloperBattleTest()
    {
        if (_session.ConnectedRemoteCharacterCount > 0)
        {
            _renderer.DrawDeveloperMessage(
                "Fejlesztői mód: a tesztparti csak csatlakoztatott vendégek nélkül hozható létre.");
            return;
        }

        var maximumLevel = Math.Max(PartyLeader.Level,
            _gameData.ExperienceByLevel.Keys.DefaultIfEmpty(PartyLeader.Level).Max());
        var options = _renderer.DrawDeveloperBattleTestSetup(PartyLeader.Level, maximumLevel,
            _maze, _fogOfWar, _player.Position);
        if (options is null) return;

        var generator = new RandomCharacterGenerator(_gameData, _random);
        foreach (var previousCompanion in _developerBattleTestCompanions.ToArray())
            CharacterRoster.Remove(previousCompanion);
        _developerBattleTestCompanions.Clear();
        generator.PrepareForCombatTest(PartyLeader, options.PartyLevel);
        foreach (var status in PartyLeader.Statuses.ToArray())
            PartyLeader.RemoveStatus(status.Id);
        PartyLeader.RemoveSpellEffects();
        PartyLeader.RestoreVitality(Math.Max(0,
            PartyLeader.MaximumVitality - PartyLeader.CurrentVitality));
        PartyLeader.RestoreMana(Math.Max(0,
            PartyLeader.MaximumMana - PartyLeader.CurrentMana));

        var companions = new List<LiveCharacter>();
        foreach (var classId in new[] { CharacterClassIds.Mágus, CharacterClassIds.Pap, CharacterClassIds.Lovag })
        {
            var companion = generator.CreateCombatTestCharacter(_gameData.GetCharacterClass(classId),
                options.PartyLevel, CharacterRoster.Characters.Concat(companions)
                    .Select(character => character.Name).ToArray());
            CharacterRoster.Add(companion);
            companions.Add(companion);
            _developerBattleTestCompanions.Add(companion);
        }
        CharacterRoster.Party.Restore(PartyLeader, companions);
        _session.SetPhase(GameSessionPhase.Exploration);
        _session.SynchronizeParty();

        var scenario = DeveloperBattleTestScenarioBuilder.Create(MazeWidth, MazeHeight, options,
            _gameData.Enemies, _random, maximumLevel);
        _locationKind = AdventureLocationKind.Campaign;
        _locationId = DeveloperBattleTestLocationId;
        _mazeLevel = options.PartyLevel;
        _difficultyLevel = options.PartyLevel;
        _suspendedCampaignState = null;
        _pendingRodericExpedition = false;
        _pendingRodericReturn = false;
        _temporaryFollowersEnteringNextMaze.Clear();
        _hasRestedThisLevel = false;
        _spottedEnemyIds.Clear();
        _spottedChestIds.Clear();
        _npcSpellcasterTactics.Clear();
        _activeTeamBattle = null;
        _battleStarted = false;
        _gameOver = false;
        _isQuickTeamBattle = false;
        _partyHoldingPosition = false;
        _partyRegrouping = false;
        _partyAttackMode = false;
        _partyCommandState = new PartyCommandState(false, false, false, null);
        _partyScatterUntil = null;
        _leaderFacing = Direction.Up;
        _maze = scenario.Maze;
        _player = new Player(scenario.LeaderPosition, PartyLeader);
        _leaderTrail.Clear();
        _leaderTrail.Add(_player.Position);
        _nextPartyMoves.Clear();
        PlacePartyMembersNear(_player.Position);
        CaptureExpeditionEnemyTemplates();

        _formation = PartyFormationRules.CreateDefault(
            CharacterRoster.Party.Members.Select(member => member.Id), PartyLeader.Id);
        _formation = PartyFormationRules.WithState(_formation, PartyFormationState.Disbanded);
        _renderer.CharacterSheet.SetFormationStatus(_formation);
        _session.SetFormationMovementLocked(false);
        _fogOfWar = new FogOfWar(_maze.Width, _maze.Height, CharacterClassRules.BaseVisionRange);
        RevealFor(PartyLeader, _player.Position);
        foreach (var member in _maze.PartyMembers) RevealFor(member.Character, member.Position);
        _fogOfWar.ToggleDeveloperReveal();
        _developerBattleLog.BeginScenario(options, scenario, CharacterRoster.Party.Members, _gameData,
            NpcTacticsFor);
        InitializeEnemyMoveSchedule(DateTime.UtcNow);
        _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _difficultyLevel);
        _renderer.RefreshCharacterSheet(PartyLeader);
        var partySummary = string.Join("; ", CharacterRoster.Party.Members.Select(character =>
            $"{character.Name} ({character.CharacterClass.Name}, L{character.Level}" +
            (character.IsSpellcaster
                ? $", {character.MemorizedSpells.Count} memorizált varázslat)"
                : ")")));
        var logPath = _developerBattleLog.FilePath ?? "nem sikerült létrehozni";
        _renderer.DrawDeveloperMessage(
            $"Tesztpálya kész: {options.EnemyGroupCount}×{options.EnemiesPerGroup} ellenfél, " +
            $"{options.EnemyGroupCount} jelölőláda. Ctrl+Shift+U: köd visszakapcsolása. {partySummary}");
        _renderer.DrawDeveloperMessage($"Harci tesztnapló: {logPath}");
        _backgroundMusic.SynchronizeMazeLevel(_mazeLevel, exitDiscovered: false);
        RequestCoopSnapshotPublish();
        LogMazeAccessibilityCheck();
    }

    private sealed record HeldInventoryItem(IItemDefinition Item, InventorySlotReference Source, long SourceRevision);
    private sealed record DeveloperUniqueNpcTarget(NpcDefinition Definition, int MazeLevel);
    private sealed record NpcTeamSpellPlan(SpellDefinition Spell, Position Target, Enemy? Enemy, bool Offensive);
    private sealed record NpcOffensiveSpellCandidate(SpellDefinition Spell, Position CastingPosition,
        Position TargetPosition, Enemy PrimaryTarget, NpcSpellTacticalClassification Classification,
        int TargetCount, NpcSpellPlanEvaluation Evaluation, bool ReadyToCast, int MovementDistance);

    private void FillPartyForDevelopment(IReadOnlyList<string> characterClassIds, string setName)
    {
        if (CharacterRoster.Party.Members.Count >= Party.MaximumSize)
        {
            _renderer.DrawDeveloperMessage("Fejlesztői mód: a parti már teljes (4/4). ");
            return;
        }

        var generator = new RandomCharacterGenerator(_gameData, _random);
        var added = new List<LiveCharacter>();
        foreach (var characterClassId in characterClassIds)
        {
            if (CharacterRoster.Party.Members.Count >= Party.MaximumSize) break;
            var member = generator.CreateDevelopmentCharacter(_gameData.GetCharacterClass(characterClassId),
                CharacterRoster.Characters.Select(character => character.Name).ToList());
            CharacterRoster.Add(member);
            CharacterRoster.Party.Add(member);
            added.Add(member);
        }
        PlacePartyMembersNear(_player.Position);
        foreach (var member in _maze.PartyMembers) RevealFor(member.Character, member.Position);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        _renderer.RefreshCharacterSheet(PartyLeader);
        _renderer.DrawDeveloperMessage($"Fejlesztői mód: {setName} osztályszett hozzáadva: " +
            string.Join(", ", added.Select(member => $"{member.Name} ({member.CharacterClass.Name})")) + ".");
    }

    private void AddLevelOnePartyMemberForDevelopment()
    {
        if (CharacterRoster.Party.Members.Count >= Party.MaximumSize)
        {
            _renderer.DrawDeveloperMessage("Fejlesztői mód: a parti már teljes (4/4). ");
            return;
        }

        var generator = new RandomCharacterGenerator(_gameData, _random);
        var member = generator.CreateLevelOne(CharacterRoster.Characters.Select(character => character.Name).ToList());
        CharacterRoster.Add(member);
        CharacterRoster.Party.Add(member);
        PlacePartyMembersNear(_player.Position);
        foreach (var avatar in _maze.PartyMembers) RevealFor(avatar.Character, avatar.Position);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        _renderer.RefreshCharacterSheet(PartyLeader);
        _renderer.DrawDeveloperMessage($"Fejlesztői mód: {member.Name} ({member.CharacterClass.Name}) 1. szinten csatlakozott. Profil: {NpcBehaviorName(member.NpcBehavior)}.");
    }

    private void PlacePartyMembersNear(Position origin)
    {
        var alreadyPlaced = _maze.PartyMembers.Select(member => member.Character).ToHashSet();
        var companions = CharacterRoster.Party.Members.Where(member => member != PartyLeader && member.IsAlive && !alreadyPlaced.Contains(member)).ToList();
        if (companions.Count == 0) return;

        var positions = FindNearbyFreePositions(origin).Take(companions.Count).ToList();
        for (var index = 0; index < Math.Min(companions.Count, positions.Count); index++)
        {
            if (companions[index].NpcBehavior is null) companions[index].SetNpcBehavior(NpcBehavior.Defensive);
            var avatar = new PartyMemberAvatar(positions[index], companions[index]);
            _maze.AddPartyMember(avatar);
            _nextPartyMoves[avatar] = DateTime.UtcNow + TimeSpan.FromMilliseconds(_random.Next(80, MaximumPartyMoveDelayMilliseconds + 1));
        }
    }

    private IEnumerable<Position> FindNearbyFreePositions(Position origin)
    {
        var yielded = new HashSet<Position>();
        if (_maze.StartingRoom is { } startingRoom && startingRoom.Contains(origin))
        {
            foreach (var position in startingRoom.InteriorPositions()
                         .Where(position => position != origin && _maze.GetObjectAt(position) is null && !IsStartingRoomDoorApproach(startingRoom, position))
                         .OrderByDescending(position => Math.Abs(position.X - origin.X) + Math.Abs(position.Y - origin.Y)))
            {
                yielded.Add(position);
                yield return position;
            }
        }

        var visited = new HashSet<Position> { origin };
        var queue = new Queue<Position>();
        queue.Enqueue(origin);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (!visited.Add(next) || !_maze.IsWalkable(next)) continue;
                queue.Enqueue(next);
                if (!yielded.Contains(next) && next != _maze.Entrance && next != _maze.Exit && next != _player.Position && _maze.GetObjectAt(next) is null)
                {
                    yielded.Add(next);
                    yield return next;
                }
            }
        }
    }

    private bool IsStartingRoomDoorApproach(Room room, Position position)
    {
        var rightBoundary = new Position(room.TopLeft.X + room.Width, position.Y);
        if (position.X == room.TopLeft.X + room.Width - 1 && _maze.IsInside(rightBoundary) &&
            _maze.GetDoorAt(rightBoundary) is not null) return true;

        var bottomBoundary = new Position(position.X, room.TopLeft.Y + room.Height);
        return position.Y == room.TopLeft.Y + room.Height - 1 && _maze.IsInside(bottomBoundary) &&
            _maze.GetDoorAt(bottomBoundary) is not null;
    }
}
