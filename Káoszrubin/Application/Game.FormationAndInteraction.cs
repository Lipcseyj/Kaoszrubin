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
    private void EditFormation()
    {
        NormalizeFormation();
        var result = RunHostWindow("Alakzatszerkesztő",
            "Várunk a vezető alakzati döntéseire…",
            () => FormationEditor.Edit(
                CharacterRoster.Party.Members.Where(member => member.IsAlive).ToArray(),
                _formation, _npcSpellcasterTactics, CaptureSharedWindowPresentation));
        _formation = PartyFormationRules.WithSlots(_formation, result.Slots);
        _npcSpellcasterTactics.Clear();
        foreach (var pair in result.SpellcasterTactics) _npcSpellcasterTactics[pair.Key] = pair.Value.Normalize();
        _renderer.CharacterSheet.SetFormationStatus(_formation);
        _session.SetFormationMovementLocked(false);
        AnnouncePartyCommand("Az alakzat sorrendje elmentve. A terkepen A-val rendelheted el az osszeallast.",
            ConsoleColor.Cyan);
    }

    private bool NormalizeFormation()
    {
        var previous = _formation;
        _formation = PartyFormationController.Normalize(_formation,
            CharacterRoster.Party.Members.Where(member => member.IsAlive).Select(member => member.Id),
            PartyLeader.Id, out var transitionedToAssembling);
        if (transitionedToAssembling)
        {
            _session.SetFormationMovementLocked(false);
            _formationObstacleReported = false;
        }
        _renderer.CharacterSheet.SetFormationStatus(_formation);
        return _formation != previous;
    }

    private void ToggleFormation()
    {
        NormalizeFormation();
        if (_formation.State != PartyFormationState.Disbanded)
        {
            _formation = PartyFormationRules.WithState(_formation, PartyFormationState.Disbanded);
            _renderer.CharacterSheet.SetFormationStatus(_formation);
            _session.SetFormationMovementLocked(false);
            _formationObstacleReported = false;
            AnnouncePartyCommand("Az alakzat feloszlott; minden partitag ujra egyenileg mozoghat.", ConsoleColor.Gray);
            return;
        }
        _formation = _formation with
        {
            Facing = _leaderFacing,
            State = PartyFormationState.Assembling,
            Layout = PartyFormationLayout.Block
        };
        _renderer.CharacterSheet.SetFormationStatus(_formation);
        _partyHoldingPosition = false;
        _partyRegrouping = false;
        _partyAttackMode = false;
        _partyScatterUntil = null;
        _formationObstacleReported = false;
        foreach (var member in _maze.PartyMembers) _nextPartyMoves[member] = DateTime.UtcNow;
        AnnouncePartyCommand("ALAKZAT: a partitagok elfoglaljak a beallitott 2x2-es helyuket.", ConsoleColor.Cyan);
    }

    private void RotateFormation(bool clockwise)
    {
        if (_formation.State != PartyFormationState.Locked)
        {
            _renderer.DrawDeveloperMessage("Fordulni csak teljesen osszeallt alakzattal lehet.");
            return;
        }
        if (!CanControlledCharacterMove(PartyLeader)) return;
        var rotated = PartyFormationController.RotateInPlace(_formation, clockwise);
        if (_formation.Layout == PartyFormationLayout.SingleFile)
        {
            _formation = rotated;
            _leaderFacing = rotated.Facing;
            _renderer.CharacterSheet.SetFormationStatus(_formation);
            ScheduleFormationMove();
            AnnouncePartyCommand(clockwise ? "Az alakzat jobbra fordult." : "Az alakzat balra fordult.",
                ConsoleColor.Cyan);
            return;
        }
        var positions = PartyFormationController.PositionsInSameFootprint(_formation, PartyLeader.Id,
            _player.Position, rotated.Facing);
        if (!TryPlanFormationPlacement(positions, rotated, out var followerMoves))
        {
            _renderer.DrawDeveloperMessage("Az alakzat a sajat teruleten sem tud 90 fokot fordulni.");
            return;
        }
        ApplyFormationPositions(positions, rotated, followerMoves);
        _leaderFacing = rotated.Facing;
        ScheduleFormationMove();
        AnnouncePartyCommand(clockwise ? "Az alakzat jobbra fordult." : "Az alakzat balra fordult.",
            ConsoleColor.Cyan);
    }

    private void MoveLockedFormation(Direction direction, bool preserveFormationFacing)
    {
        var leaderDestination = _player.Position + direction;
        var travelFormation = preserveFormationFacing
            ? _formation
            : PartyFormationController.FaceInPlace(_formation, direction);
        if (_formation.Layout == PartyFormationLayout.SingleFile)
        {
            var blockFormation = travelFormation with { Layout = PartyFormationLayout.Block };
            var blockPositions = PartyFormationController.Positions(blockFormation, PartyLeader.Id,
                leaderDestination);
            if (TryPlanFormationPlacement(blockPositions, blockFormation, out var reformFollowerMoves))
            {
                ApplyFormationPositions(blockPositions, blockFormation, reformFollowerMoves);
                CompleteFormationTravelMove(direction, preserveFormationFacing);
                AnnouncePartyCommand("A szukuleten tul az alakzat automatikusan visszaallt 2x2-es rendbe.",
                    ConsoleColor.Green);
                return;
            }

            var current = CurrentFormationPositions();
            var shifted = PartyFormationController.SingleFileDestinations(travelFormation, current,
                PartyLeader.Id, leaderDestination);
            if (TryMoveFormationTo(shifted, travelFormation, direction, preserveFormationFacing)) return;

            var alignedPositions = PartyFormationController.Positions(travelFormation, PartyLeader.Id,
                leaderDestination);
            TryMoveFormationTo(alignedPositions, travelFormation, direction, preserveFormationFacing);
            return;
        }

        var blockCurrent = preserveFormationFacing
            ? CurrentFormationPositions()
            : PartyFormationController.PositionsInSameFootprint(_formation, PartyLeader.Id,
                _player.Position, travelFormation.Facing);
        var blockDestinations = blockCurrent.ToDictionary(pair => pair.Key, pair => pair.Value + direction);
        if (blockDestinations.Values.All(_maze.IsWalkable))
        {
            TryMoveFormationTo(blockDestinations, travelFormation, direction, preserveFormationFacing);
            return;
        }

        var singleFileFormation = travelFormation with { Layout = PartyFormationLayout.SingleFile };
        var singleFilePositions = PartyFormationController.SingleFileDestinations(singleFileFormation,
            CurrentFormationPositions(), PartyLeader.Id, leaderDestination);
        if (!PartyFormationController.IsSingleFilePassage(blockDestinations, singleFilePositions, _maze) ||
            !TryMoveFormationTo(singleFilePositions, singleFileFormation, direction, preserveFormationFacing)) return;
        AnnouncePartyCommand("Az egymezos szukuletben az alakzat ideiglenesen libasorra valt.",
            ConsoleColor.Cyan);
    }

    private bool TryMoveFormationTo(IReadOnlyDictionary<CharacterId, Position> destinations,
        PartyFormationSnapshot formation, Direction movementDirection, bool preserveFormationFacing)
    {
        var enemyEntry = destinations.Select(pair => (pair.Key, Enemy: _maze.GetEnemyAt(pair.Value)))
            .FirstOrDefault(entry => entry.Enemy is not null);
        if (enemyEntry.Enemy is { } enemy)
        {
            var avatar = FormationAvatar(enemyEntry.Key);
            if (avatar is null) StartBattle(enemy);
            else StartBattle(avatar, enemy);
            return true;
        }
        if (!TryPlanFormationPlacement(destinations, formation, out var followerMoves)) return false;
        ApplyFormationPositions(destinations, formation, followerMoves);
        CompleteFormationTravelMove(movementDirection, preserveFormationFacing);
        return true;
    }

    private void CompleteFormationTravelMove(Direction direction, bool preserveFormationFacing)
    {
        if (!preserveFormationFacing) _leaderFacing = direction;
        if (_leaderTrail[^1] != _player.Position) _leaderTrail.Add(_player.Position);
        if (_leaderTrail.Count > 256) _leaderTrail.RemoveRange(0, _leaderTrail.Count - 256);
        ScheduleFormationMove();
    }

    private IReadOnlyDictionary<CharacterId, Position> CurrentFormationPositions()
    {
        var positions = new Dictionary<CharacterId, Position>
        {
            [PartyLeader.Id] = _player.Position
        };
        foreach (var id in _formation.Slots.Where(id => id is not null).Select(id => id!.Value)
                     .Where(id => id != PartyLeader.Id))
            if (FormationAvatar(id) is { } avatar)
                positions[id] = avatar.Position;
        return positions;
    }

    private bool TryPlanFormationPlacement(IReadOnlyDictionary<CharacterId, Position> positions,
        PartyFormationSnapshot formation,
        out IReadOnlyDictionary<PartyMemberAvatar, Position> followerMoves)
    {
        followerMoves = new Dictionary<PartyMemberAvatar, Position>();
        var followers = _maze.PartyMembers.Where(member => member.IsTemporaryFollower && member.Character.IsAlive)
            .ToArray();
        if (!PartyFormationController.CanFormationOccupy(positions, _maze, FormationAvatar,
                avatar => followers.Contains(avatar))) return false;

        var reserved = positions.Values.ToHashSet();
        var currentlyOccupiedByFormation = positions.Keys.Select(FormationAvatar)
            .Where(avatar => avatar is not null).Select(avatar => avatar!.Position).ToHashSet();
        var used = new HashSet<Position>();
        var escortPositions = formation.Layout == PartyFormationLayout.SingleFile
            ? PartyFormationController.SingleFileEscortPositions(formation, positions, PartyLeader.Id)
            : PartyFormationController.EscortPositions(positions, formation.Facing);
        var planned = new Dictionary<PartyMemberAvatar, Position>();
        foreach (var follower in followers)
        {
            var conflictsWithFormation = reserved.Contains(follower.Position);
            if (!conflictsWithFormation && escortPositions.Contains(follower.Position))
            {
                used.Add(follower.Position);
                continue;
            }

            var candidates = escortPositions
                .Concat(Enum.GetValues<Direction>().Select(direction => follower.Position + direction))
                .Where(position => !reserved.Contains(position) && !used.Contains(position) &&
                                   position != _player.Position && !currentlyOccupiedByFormation.Contains(position))
                .Where(position => IsAvailableFollowerEscortPosition(follower, position))
                .OrderBy(position => Manhattan(follower.Position, position))
                .ToArray();
            Position? destination = candidates
                .Where(position => Manhattan(follower.Position, position) <= 1)
                .Where(position => CanEnterTrap(follower.Character, position))
                .Select(position => (Position?)position)
                .FirstOrDefault();
            if (destination is null)
            {
                if (conflictsWithFormation) return false;
                used.Add(follower.Position);
                continue;
            }
            planned[follower] = destination.Value;
            used.Add(destination.Value);
        }
        followerMoves = planned;
        return true;
    }

    private bool IsAvailableFollowerEscortPosition(PartyMemberAvatar follower, Position position)
    {
        if (!_maze.IsWalkable(position) || _maze.GetEnemyAt(position) is not null) return false;
        var occupant = _maze.GetObjectAt(position);
        return occupant is null or GroundItemPile or Corpse || occupant == follower ||
               Maze.IsPassableNeutralNpc(occupant);
    }

    private void ApplyFormationPositions(IReadOnlyDictionary<CharacterId, Position> positions,
        PartyFormationSnapshot formation, IReadOnlyDictionary<PartyMemberAvatar, Position> followerMoves)
    {
        foreach (var (follower, destination) in followerMoves)
            ApplyFollowerEscortMove(follower, destination);
        var previousLeader = _player.Position;
        var previousMembers = positions.Keys.Where(id => id != PartyLeader.Id)
            .Select(id => (Avatar: FormationAvatar(id), Destination: positions[id]))
            .Where(entry => entry.Avatar is not null)
            .Select(entry => (Avatar: entry.Avatar!, Previous: entry.Avatar!.Position, entry.Destination)).ToArray();
        _player.TeleportTo(positions[PartyLeader.Id]);
        foreach (var entry in previousMembers) entry.Avatar.MoveTo(entry.Destination);
        _formation = formation;
        _renderer.CharacterSheet.SetFormationStatus(_formation);

        PartyLeader.RegisterExplorationStep();
        var leaderRevealed = RevealFor(PartyLeader, _player.Position, advanceEnemyMemory: true);
        var memberReveals = new List<Position>();
        foreach (var entry in previousMembers)
        {
            entry.Avatar.Character.RegisterExplorationStep();
            memberReveals.AddRange(RevealFor(entry.Avatar.Character, entry.Destination,
                advanceEnemyMemory: true));
        }
        _renderer.DrawFormationMovement(_maze, _fogOfWar,
            [previousLeader, .. previousMembers.Select(entry => entry.Previous)],
            [_player.Position, .. previousMembers.Select(entry => entry.Destination)],
            [.. leaderRevealed, .. memberReveals],
            _player.Position,
            _player.Position == _maze.Exit && previousLeader != _maze.Exit &&
            _dungeonLevel.ActiveArea == _dungeonLevel.Areas[^1]);
        if (_maze.GetPassageAt(_player.Position) is not null)
            _renderer.DrawInventoryMessage("⇄ Átjáró a szint másik területére. Enter: átkelés.", ConsoleColor.Cyan);

        PlayCharacterStepSound(PartyLeader);
        CollectTreasureChest(PartyLeader, _player.Position, shareLootWithParty: true);
        TriggerTrapAt(PartyLeader, _player.Position);
        foreach (var entry in previousMembers)
        {
            CollectTreasureChest(entry.Avatar.Character, entry.Destination, shareLootWithParty: false);
            TriggerTrapAt(entry.Avatar.Character, entry.Destination);
        }
        CheckBossDiscoveryAt(leaderRevealed, PartyLeader);
    }

    private void ScheduleFormationMove()
    {
        var delay = PartyFormationController.CalculateMoveDelay(CharacterRoster.Party.Members,
            ControlledMoveDelayMilliseconds);
        var next = DateTime.UtcNow + TimeSpan.FromMilliseconds(delay);
        foreach (var member in CharacterRoster.Party.Members) _nextControlledMoves[member.Id] = next;
    }

    private PartyMemberAvatar? FormationAvatar(CharacterId id) =>
        _maze.PartyMembers.FirstOrDefault(member => !member.IsTemporaryFollower && member.Character.Id == id);

    private void ExecuteCharacterAction(CharacterActionCommand command)
    {
        var character = CharacterRoster.Party.Members.FirstOrDefault(candidate => candidate.Id == command.CharacterId);
        var position = character is null ? null : GetCharacterWorldPosition(character);
        if (character is null || position is null || !character.IsAlive) return;
        var isLeader = character == PartyLeader;
        var isSenderLeader = command.SenderId == _session.HostPlayerId;
        var doorContext = ResolveDoorInteraction(character, position.Value, command.Action,
            command.TargetDoorPosition);
        var keyOwners = DoorKeyOwners(character);
        switch (command.Action)
        {
            case CharacterAction.OpenDoor:
                _doorInteractions.TryOpenAdjacentDoor(_maze, _fogOfWar, doorContext.Origin, _player.Position,
                    character, allowPartyAssistanceAndPrompts: isLeader, doorContext.Target, command.UseKey,
                    command.KeyOwnerCharacterId, keyOwners, isSenderLeader);
                break;
            case CharacterAction.CloseOrLockDoor:
                _doorInteractions.TryCloseOrLockAdjacentDoor(_maze, _fogOfWar, doorContext.Origin, _player.Position,
                    character, allowPartyAssistanceAndPrompts: isLeader, doorContext.Target, command.UseKey,
                    command.KeyOwnerCharacterId, keyOwners, isSenderLeader);
                break;
            case CharacterAction.SearchCurrentPosition:
                if (!TryDisarmAdjacentTrap(character, position.Value))
                    TrySearchCurrentCell(character, position.Value, shareLootWithParty: isLeader, isSenderLeader: isSenderLeader);
                break;
        }
    }

    private IReadOnlyList<Position> AdjacentDoorPositions(LiveCharacter character, Position position,
        bool includeFormation) =>
        DoorInteractionOrigins(character, position, includeFormation)
            .SelectMany(origin => Enum.GetValues<Direction>().Select(direction => origin + direction))
            .Where(candidate => _maze.GetDoorAt(candidate) is not null)
            .Distinct()
            .ToArray();

    private IReadOnlyList<Position> DoorInteractionOrigins(LiveCharacter character, Position position,
        bool includeFormation)
    {
        if (!includeFormation) return [position];
        var partyPositions = CharacterRoster.Party.Members.Where(member => member.IsAlive)
            .Select(member => (member.Id, Position: GetCharacterWorldPosition(member)))
            .Where(entry => entry.Position is not null)
            .ToDictionary(entry => entry.Id, entry => entry.Position!.Value);
        return PartyFormationRules.InteractionOrigins(_formation, character.Id, position, partyPositions);
    }

    private IReadOnlyList<LiveCharacter> DoorKeyOwners(LiveCharacter character)
    {
        if (_formation.State != PartyFormationState.Locked || !_formation.Slots.Contains(character.Id))
            return [character];
        return _formation.Slots.Where(id => id is not null)
            .Select(id => CharacterRoster.Party.Members.FirstOrDefault(member => member.Id == id!.Value))
            .Where(member => member is { IsAlive: true })
            .Cast<LiveCharacter>()
            .ToArray();
    }

    private (Position Origin, Position? Target) ResolveDoorInteraction(LiveCharacter character,
        Position actorPosition, CharacterAction action, Position? requestedTarget)
    {
        var includeFormation = action is CharacterAction.OpenDoor or CharacterAction.CloseOrLockDoor;
        var candidates = AdjacentDoorPositions(character, actorPosition, includeFormation);
        var target = requestedTarget ?? (candidates.Count == 1 ? candidates[0] : null);
        if (target is not { } targetPosition || !candidates.Contains(targetPosition))
            return (actorPosition, requestedTarget);
        var origin = DoorInteractionOrigins(character, actorPosition, includeFormation)
            .First(position => Manhattan(position, targetPosition) == 1);
        return (origin, targetPosition);
    }

    private (bool? UseKey, CharacterId? KeyOwnerCharacterId) GetLocalThiefKeyChoice(
        CharacterAction action, Position? targetDoorPosition)
    {
        var keyOwner = DoorKeyOwners(PartyLeader).FirstOrDefault(DoorInteractionRules.HasKey);
        if (!CharacterClassRules.IsThief(PartyLeader.CharacterClass.Id) ||
            keyOwner is null ||
            targetDoorPosition is not { } target || _maze.GetDoorAt(target) is not { } door || door.IsQuestSealed ||
            action switch
            {
                CharacterAction.OpenDoor => door.State != DoorState.Locked,
                CharacterAction.CloseOrLockDoor => door.State != DoorState.Closed,
                _ => true
            }) return (null, null);

        _renderer.DrawDoorMessage(
            $"🔑 {keyOwner.Name} kulcsát használjuk? I/Y/Enter: igen | N/Esc: nem, jöjjön a tolvajpróba",
            ConsoleColor.Yellow);
        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.I or ConsoleKey.Y or ConsoleKey.Enter) return (true, keyOwner.Id);
            if (key is ConsoleKey.N or ConsoleKey.Escape) return (false, null);
        }
    }

    private Position? SelectDoorTarget(IReadOnlyList<Position> doors, CharacterAction action)
    {
        var selected = 0;
        Position? previous = null;
        while (true)
        {
            var current = doors[selected];
            var verb = action == CharacterAction.OpenDoor ? "nyitás" : "bezárás/zárás";
            _renderer.DrawSpellTargetCursor(_maze, _fogOfWar, previous, current, true,
                $"Ajtó kiválasztása ({verb}): nyilak/Tab, Enter: kész, Esc: mégse");
            previous = current;
            RequestCoopSnapshotPublish();
            while (!Console.KeyAvailable)
            {
                ProcessSessionCommands();
                TryPublishScheduledCoopSnapshot(DateTime.UtcNow);
                Thread.Sleep(20);
            }
            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.Escape)
            {
                _renderer.FinishSpellTargeting(_maze, _fogOfWar, _player.Position);
                return null;
            }
            if (key == ConsoleKey.Enter)
            {
                _renderer.FinishSpellTargeting(_maze, _fogOfWar, _player.Position);
                return current;
            }
            if (key == ConsoleKey.Tab)
                selected = (selected + 1) % doors.Count;
            else if (TryGetDirection(key, out var direction))
            {
                var directionalDoor = _player.Position + direction;
                var index = doors.ToList().IndexOf(directionalDoor);
                if (index >= 0) selected = index;
            }
        }
    }

    private void ActivateExit()
    {
        if (_maze.GetPassageAt(_player.Position) is { } passage)
        {
            ActivatePassage(passage);
            return;
        }
        if (_player.Position != _maze.Exit || _dungeonLevel.ActiveArea != _dungeonLevel.Areas[^1]) return;
        if (_locationKind == AdventureLocationKind.Quest)
        {
            _renderer.DrawInventoryMessage(
                "A sírkápolnából csak Roderic vezethet vissza. Előbb győzzétek le Sir Malrecet.",
                ConsoleColor.DarkYellow);
            return;
        }
        if (_isReturnExpedition)
        {
            ReturnFromExpeditionToInn();
            return;
        }
        if (_maze.PartyMembers.FirstOrDefault(member => member.IsTemporaryFollower) is { } escort &&
            Manhattan(escort.Position, _player.Position) > MazeQuestWorldContext.ExitEscortMaximumDistance)
        {
            _renderer.DrawInventoryMessage($"🌿 {escort.Character.Name} túl messze van a kijárattól. Várjátok meg vagy hívjátok magatokhoz Gyülekező paranccsal.",
                ConsoleColor.Yellow);
            return;
        }
        ResolveTemporaryFollowerAtExit();
        if (_mazeLevel == MazeLevelConfigurations.FinalLevel)
        {
            CompleteCampaign();
            return;
        }
        var completedLevel = _mazeLevel;
        PlaySessionSound(SoundEffect.LevelComplete);
        _backgroundMusic.EnterInn();
        _session.SetPhase(GameSessionPhase.Inn);
        var expeditionReason = ReturnExpeditionReason(completedLevel);
        var departure = _innController.Run(completedLevel, expeditionReason);
        if (departure == InnController.DepartureChoice.ReturnExpedition)
        {
            BeginReturnExpedition();
            return;
        }
        _activeInnDeparture = new InnDepartureSnapshot("A csapat szedelőzködik, és elhagyjátok a fogadót.");
        _session.SetPhase(GameSessionPhase.Paused);
        RequestCoopSnapshotPublish();
        CarryPersistentTemporaryFollowers();
        _mazeLevel++;
        StartNewMaze();
    }

    private void ActivatePassage(MazePassage passage)
    {
        if (_battleStarted || !_dungeonLevel.IsMultiArea) return;
        var sourceArea = _dungeonLevel.ActiveArea;
        var destinationArea = _dungeonLevel.GetArea(passage.DestinationAreaId);
        var distantMember = _maze.PartyMembers.FirstOrDefault(member => member.Character.IsAlive &&
            Manhattan(member.Position, _player.Position) > 4);
        if (distantMember is not null)
        {
            _renderer.DrawInventoryMessage(
                $"⇄ {distantMember.Character.Name} túl messze van az átjárótól. Előbb gyűljön össze a parti.",
                ConsoleColor.Yellow);
            return;
        }
        var travelers = _maze.PartyMembers.Where(member => member.Character.IsAlive)
            .Select(member => (member.Character, member.TemporaryFollower)).ToArray();
        var now = DateTime.UtcNow;
        sourceArea.EnemyMoveDelays.Clear();
        foreach (var enemy in sourceArea.Maze.Enemies)
            sourceArea.EnemyMoveDelays[enemy] = _nextEnemyMoves.TryGetValue(enemy, out var scheduled)
                ? scheduled > now ? scheduled - now : TimeSpan.Zero
                : EnemyMoveInterval(enemy);
        sourceArea.PausedAtUtc = now;

        foreach (var member in sourceArea.Maze.PartyMembers.ToArray()) sourceArea.Maze.RemovePartyMember(member);
        _dungeonLevel.Activate(destinationArea.Id);
        ShiftPausedHordeTimers(destinationArea, now, remainPaused: false);
        _maze = destinationArea.Maze;
        _fogOfWar = destinationArea.FogOfWar;
        _player.TeleportTo(passage.DestinationPosition);
        _leaderTrail.Clear();
        _leaderTrail.Add(_player.Position);
        _nextPartyMoves.Clear();
        var positions = FindNearbyFreePositions(_player.Position).Take(travelers.Length).ToArray();
        if (positions.Length < travelers.Length)
            throw new InvalidOperationException("Az átjáró túloldalán nincs elég hely a teljes partinak.");
        for (var index = 0; index < travelers.Length; index++)
        {
            var avatar = new PartyMemberAvatar(positions[index], travelers[index].Character,
                travelers[index].TemporaryFollower);
            _maze.AddPartyMember(avatar);
            ScheduleNextPartyMove(avatar, now);
        }

        _nextEnemyMoves.Clear();
        foreach (var enemy in _maze.Enemies)
            _nextEnemyMoves[enemy] = now + destinationArea.EnemyMoveDelays.GetValueOrDefault(enemy,
                EnemyMoveInterval(enemy));
        RefreshNextEnemyActionUtc();
        _formation = PartyFormationRules.WithState(_formation, PartyFormationState.Disbanded);
        _renderer.CharacterSheet.SetFormationStatus(_formation);
        _session.SetFormationMovementLocked(false);
        _spottedEnemyIds.Clear();
        _spottedChestIds.Clear();
        RevealFor(PartyLeader, _player.Position);
        foreach (var member in _maze.PartyMembers) RevealFor(member.Character, member.Position);
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        var areaIndex = _dungeonLevel.Areas.ToList().IndexOf(destinationArea) + 1;
        var message = $"⇄ Átjártatok a szint {areaIndex}/{_dungeonLevel.Areas.Count}. területére.";
        _renderer.DrawInventoryMessage(message, ConsoleColor.Cyan);
        RecordSessionActivity(SessionActivityKind.System, message, ConsoleColor.Cyan);
        ForceCoopSnapshotPublish();
    }

    private static void ShiftPausedHordeTimers(DungeonArea area, DateTime now, bool remainPaused)
    {
        if (area.PausedAtUtc is { } pausedAt)
        {
            var pause = now > pausedAt ? now - pausedAt : TimeSpan.Zero;
            foreach (var enemy in area.Maze.Enemies) enemy.ShiftHordeCamp(pause);
        }
        area.PausedAtUtc = remainPaused ? now : null;
    }

    private string? ReturnExpeditionReason(int completedLevel)
    {
        var quest = _questManager.GetActiveQuests().FirstOrDefault(candidate =>
            _questWorldContext.ResolveNpc(candidate.Giver, candidate.GiverInstanceId) is { } npc &&
            _maze.WorldNpcs.Contains(npc));
        if (quest is not null)
            return $"Aktív küldetés maradt hátra: {quest.Title}. " + Environment.NewLine + " A régi kijárat visszahoz ugyanebbe a fogadóba.";
        return _random.Next(100) < 35
            ? "Egy fogadói szóbeszéd új nyomot jelzett az előző pályán. " + Environment.NewLine + "A régi kijárat visszahoz ugyanebbe a fogadóba."
            : null;
    }

    private void BeginReturnExpedition()
    {
        _isReturnExpedition = true;
        _session.SetPhase(GameSessionPhase.Exploration);
        _session.SynchronizeParty();
        foreach (var area in _dungeonLevel.Areas)
        {
            foreach (var boss in area.Maze.Enemies.Where(enemy => enemy.Definition.IsBoss).ToArray())
            {
                area.Maze.RemoveEnemy(boss);
                area.EnemyMoveDelays.Remove(boss);
                _nextEnemyMoves.Remove(boss);
            }
        }
        ReplenishExpeditionEnemies();
        var returningParty = _maze.PartyMembers.Where(member => member.Character.IsAlive)
            .Select(member => (member.Character, member.TemporaryFollower)).ToList();
        foreach (var member in _maze.PartyMembers.ToArray()) _maze.RemovePartyMember(member);
        _dungeonLevel.ActiveArea.PausedAtUtc = DateTime.UtcNow;
        var firstArea = _dungeonLevel.Areas[0];
        _dungeonLevel.Activate(firstArea.Id);
        ShiftPausedHordeTimers(firstArea, DateTime.UtcNow, remainPaused: false);
        _maze = firstArea.Maze;
        _fogOfWar = firstArea.FogOfWar;
        RepositionPartyAtEntrance(returningParty);
        foreach (var character in CharacterRoster.Party.Members.Where(character => character.IsAlive))
        {
            character.ConsumeFood(ReturnExpeditionRules.TravelNeedCost);
            character.ConsumeWater(ReturnExpeditionRules.TravelNeedCost);
            character.SynchronizeNeedStatuses(_gameData.GetStatus(CharacterStatusIds.Hungry),
                _gameData.GetStatus(CharacterStatusIds.Thirsty));
        }
        _spottedEnemyIds.Clear();
        _spottedChestIds.Clear();
        _battleStarted = false;
        _hasRestedThisLevel = true;
        InitializeEnemyMoveSchedule(DateTime.UtcNow);
        RevealFor(PartyLeader, _player.Position);
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        var message = "🗺️ Visszatérő expedíció: a felderített térkép megmaradt, a vidéket csak kisebb szörnyjárőrök népesítették be újra. 🍖-3 💧-3";
        _renderer.DrawInventoryMessage(message, ConsoleColor.Cyan);
        RecordSessionActivity(SessionActivityKind.System, message, ConsoleColor.Cyan);
        _backgroundMusic.SynchronizeMazeLevel(_mazeLevel, IsLevelExitDiscovered());
        _activeInnDeparture = null;
        RequestCoopSnapshotPublish();
    }

    private void ReturnFromExpeditionToInn()
    {
        _isReturnExpedition = false;
        PlaySessionSound(SoundEffect.LevelComplete);
        _backgroundMusic.EnterInn();
        _session.SetPhase(GameSessionPhase.Inn);
        _innController.Run(_mazeLevel, resume: true);
        _activeInnDeparture = new InnDepartureSnapshot("A csapat szedelőzködik, és elhagyjátok a fogadót.");
        _session.SetPhase(GameSessionPhase.Paused);
        RequestCoopSnapshotPublish();
        CarryPersistentTemporaryFollowers();
        _mazeLevel++;
        StartNewMaze();
    }

    private void CaptureExpeditionEnemyTemplates()
    {
        _levelEnemyTemplates.Clear();
        if (_dungeonLevel is null)
        {
            DungeonExpeditionCoordinator.CaptureExpeditionEnemyTemplates(_levelEnemyTemplates, _maze, _gameData);
            return;
        }
        foreach (var area in _dungeonLevel.Areas)
            DungeonExpeditionCoordinator.CaptureExpeditionEnemyTemplates(_levelEnemyTemplates, area.Maze, _gameData,
                area.Id, clear: false);
    }

    private void ReplenishExpeditionEnemies()
    {
        if (_dungeonLevel is null)
        {
            _expeditionCoordinator.ReplenishExpeditionEnemies(_levelEnemyTemplates, _maze);
            return;
        }
        foreach (var area in _dungeonLevel.Areas)
            _expeditionCoordinator.ReplenishExpeditionEnemies(_levelEnemyTemplates, area.Maze, area.Id);
    }

    private Position? FindExpeditionSpawnPosition(Position preferred) =>
        DungeonExpeditionCoordinator.FindExpeditionSpawnPosition(_maze, preferred);

    private void RepositionPartyAtEntrance(
        IEnumerable<(LiveCharacter Character, WorldNpc? TemporaryFollower)>? returningParty = null)
    {
        var oldAvatars = returningParty?.ToList() ?? _maze.PartyMembers.Where(member => member.Character.IsAlive)
            .Select(member => (member.Character, member.TemporaryFollower)).ToList();
        foreach (var member in _maze.PartyMembers.ToArray()) _maze.RemovePartyMember(member);
        _player.TeleportTo(_maze.Entrance);
        _leaderTrail.Clear();
        _leaderTrail.Add(_player.Position);
        _nextPartyMoves.Clear();
        foreach (var character in CharacterRoster.Party.Members.Where(character => character != PartyLeader &&
                     character.IsAlive && oldAvatars.All(entry => entry.Character != character)))
            oldAvatars.Add((character, null));
        var positions = FindNearbyFreePositions(_player.Position).Take(oldAvatars.Count).ToArray();
        for (var index = 0; index < Math.Min(oldAvatars.Count, positions.Length); index++)
        {
            var avatar = new PartyMemberAvatar(positions[index], oldAvatars[index].Character,
                oldAvatars[index].TemporaryFollower);
            _maze.AddPartyMember(avatar);
            _nextPartyMoves[avatar] = DateTime.UtcNow;
        }
    }

    private void ResolveTemporaryFollowerAtExit()
    {
        var avatar = _maze.PartyMembers.FirstOrDefault(member => member.TemporaryFollower is not null);
        if (avatar?.TemporaryFollower is not { } follower) return;
        if (string.Equals(follower.StoryId, RodericStoryId, StringComparison.OrdinalIgnoreCase)) return;
        ProcessQuestProgressChanges(_questManager.RegisterNpcReachedLocation(
            LegacyNpcIdMap.ToQuestNpcId(follower.DefinitionId),
            _questWorldContext.GetInstanceId(follower), QuestLocation.Exit));
        ProcessNpcQuests(follower);
        if (string.Equals(follower.StoryId, EliraStoryId, StringComparison.OrdinalIgnoreCase) &&
            !_questManager.Elira.CanResolveDeparture) return;
        follower.AdjustFriendliness(2);

        if (follower.Friendliness >= 10)
        {
            var hasRoom = CharacterRoster.Party.Members.Count < Party.MaximumSize;
            switch (_renderer.ChooseUniqueNpcDeparture(follower, hasRoom))
            {
                case UniqueNpcDepartureChoice.JoinParty when CharacterRoster.Party.Add(follower.Character):
                    follower.Character.SetNpcJoinOrigin(_mazeLevel, "A pálya kijáratánál csatlakozott");
                    avatar.MakePermanent();
                    _renderer.DrawInventoryMessage($"🤝 {follower.Character.Name} végleg csatlakozott a partihoz.", ConsoleColor.Green);
                    return;
                case UniqueNpcDepartureChoice.RemainFollower:
                    _renderer.DrawInventoryMessage($"🌿 {follower.Character.Name} egyelőre követőként marad veletek.", ConsoleColor.Cyan);
                    return;
                case UniqueNpcDepartureChoice.WaitAtInn:
                    _maze.RemovePartyMember(avatar);
                    _nextPartyMoves.Remove(avatar);
                    _eliraWaitingAtInn = follower.Character;
                    _eliraInnVisitsRemaining = 3;
                    _renderer.DrawInventoryMessage($"🍺 {follower.Character.Name} a következő három fogadólátogatáskor vár rátok.", ConsoleColor.Yellow);
                    return;
            }
        }

        _maze.RemovePartyMember(avatar);
        _nextPartyMoves.Remove(avatar);
        CharacterRoster.Remove(follower.Character);
        _renderer.DrawInventoryMessage(follower.Friendliness >= 10
            ? $"🌿 {follower.Character.Name} hálásan elbúcsúzott."
            : $"🌿 {follower.Character.Name} kijutott de még nem bízik eléggé a végleges csatlakozáshoz ({follower.Friendliness}/10).",
            ConsoleColor.Cyan);
    }

    private void DropSelectedInventoryItem()
    {
        var slot = _renderer.CharacterSheet.GetSelectedInventorySlot();
        if (slot is null) { _renderer.DrawInventoryMessage("Itt nincs ledobható tárgy.", ConsoleColor.DarkYellow); return; }
        var item = slot.Value.Character.GetInventoryItem(slot.Value.Kind, slot.Value.Index);
        if (item is null) { _renderer.DrawInventoryMessage("A kijelölt hely üres.", ConsoleColor.DarkYellow); return; }
        if (SpellcastingRules.IsSpellcastingFocus(item))
        { _renderer.DrawInventoryMessage($"A(z) {item.Name} a karakterhez kötött varázsfókusz, ezért nem dobható el.", ConsoleColor.Red); return; }
        if (CharacterBoundItemRules.IsBound(item))
        { _renderer.DrawInventoryMessage($"A(z) {item.Name} családi ereklye, ezért nem dobható el.", ConsoleColor.Red); return; }
        var commandId = _localCommandId + 1;
        if (!_session.Submit(new DropInventoryItemCommand(_session.HostPlayerId, commandId,
                slot.Value.Character.Id, slot.Value.Character.InventoryRevision, slot.Value.Kind, slot.Value.Index))) return;
        _localCommandId = commandId;
    }

    private bool TrySearchCurrentCell(LiveCharacter character, Position position, bool shareLootWithParty, bool isSenderLeader)
    {
        if (_maze.GetTreasureChestAt(position)?.Definition is not null)
        {
            CollectTreasureChest(character, position, shareLootWithParty);
            return true;
        }
        var corpses = _maze.GetCorpsesAt(position);
        var pile = _maze.GetGroundItemPileAt(position);
        if (corpses.Count == 0 && pile is null)
        {
            var nothingFoundMessage = "🔎 A keresés nem hozott eredményt.";
            if (isSenderLeader) _renderer.DrawInventoryMessage(nothingFoundMessage, ConsoleColor.Yellow);
            RecordSessionActivity(SessionActivityKind.System, nothingFoundMessage, ConsoleColor.Yellow, [character.Id]);
            return false;
        }
        var unsearched = _maze.GetUnsearchedMonsterCorpsesAt(position);

        var messages = new List<string>();
        var hasSuccessfulCorpseLoot = false;
        foreach (var monsterCorpse in unsearched)
        {
            monsterCorpse.MarkSearched();
            var corpseMessages = new List<string>();
            hasSuccessfulCorpseLoot |= SearchMonsterCorpse(monsterCorpse, character, position, shareLootWithParty,
                corpseMessages);
            messages.Add($"† {monsterCorpse.FormerName}: {string.Join(", ", corpseMessages)}");
        }
        if (unsearched.Count == 0 && pile is null && corpses.All(corpse => corpse is MonsterCorpse))
            messages.Add("🔎 A tetemeket már átkutattátok, új zsákmány nem maradt.");
        if (unsearched.Count == 0 && corpses.Any(corpse => corpse is PartyMemberCorpse))
            messages.Add("🔎 Az elesett társ testén nincs elvehető zsákmány");
        else if (unsearched.Count == 0 && corpses.Any(corpse => corpse is not MonsterCorpse))
            messages.Add("🔎 Ez a régi tetem már nem tartalmaz azonosítható zsákmányt");

        PickUpGroundItems(character, position, shareLootWithParty, messages);
        _renderer.RefreshCharacterSheet(PartyLeader);
        _renderer.DrawMapCellsChanged(_maze, _fogOfWar, _player.Position, [position]);

        string[] resultMessages = messages.Count == 0
            ? ["🔎 A keresés nem hozott eredményt."]
            : messages.Select(message => $"🔎 {character.Name} » Zsákmány: {message}.").ToArray();

        var visibleListeners = hasSuccessfulCorpseLoot ? _humanMemberIds : [character.Id];

        foreach (var resultMessage in resultMessages)
        {
            if (isSenderLeader || hasSuccessfulCorpseLoot)
                _renderer.DrawInventoryMessage(resultMessage, ConsoleColor.Yellow);
            RecordSessionActivity(SessionActivityKind.System, resultMessage, ConsoleColor.Yellow, visibleListeners);
        }
        return true;
    }

    private bool SearchMonsterCorpse(MonsterCorpse corpse, LiveCharacter character, Position position,
        bool shareLootWithParty, ICollection<string> messages)
    {
        var enemy = _gameData.GetEnemy(corpse.EnemyDefinitionId);
        var rules = _gameData.LootRules;
        var keyChance = _isReturnExpedition ? 0 : AdjustedSearchChance(character, rules.KeyChancePercent);
        var goldChance = AdjustedSearchChance(character, rules.GoldChancePercent);
        var equipmentDefinition = _gameData.GetMonsterLoot(enemy.Id);
        var equipmentChance = equipmentDefinition is null
            ? 0
            : AdjustedSearchChance(character, equipmentDefinition.EquipmentChancePercent);
        var carriedWeaponChance = corpse.CarriedWeaponIds.Count == 0
            ? 0
            : AdjustedSearchChance(character, rules.CarriedWeaponChancePercent);
        messages.Add($"esélyek: 🔑 {keyChance}%, {ConsoleRenderer.MoneyIcon} {goldChance}%" +
                     (carriedWeaponChance == 0 ? string.Empty : $", ⚔ saját fegyver {carriedWeaponChance}%") +
                     (equipmentDefinition is null ? string.Empty : $", 🎁 {equipmentChance}%"));

        var foundItems = corpse.GuaranteedLootIds.Select(_gameData.GetItem).Cast<IItemDefinition>().ToList();
        var foundGold = false;
        if (_random.Next(100) < keyChance) foundItems.Add(_gameData.GetItem(MiscItemIds.Key));
        if (_random.Next(100) < goldChance)
        {
            var maximumGold = Math.Max(1, enemy.StrengthTier * rules.GoldPerStrengthTier);
            var gold = _random.Next(1, maximumGold + 1);
            PartyLeader.AddGold(gold);
            foundGold = true;
            messages.Add($"{ConsoleRenderer.MoneyIcon} {gold} arany");
        }
        if (_lootService.RollCarriedWeapon(corpse.CarriedWeaponIds, carriedWeaponChance) is { } carriedWeapon)
            foundItems.Add(carriedWeapon);
        else if (equipmentDefinition is not null && _random.Next(100) < equipmentChance &&
                 RollEquipmentLoot(equipmentDefinition) is { } equipment)
            foundItems.Add(equipment);

        foreach (var item in foundItems)
        {
            var identification = RollLootItemState(item);
            if (TryStoreSearchedLoot(character, item, shareLootWithParty, out var owner, identification.State))
            {
                messages.Add($"{ItemIdentificationRules.DisplayName(item, identification.State.IsIdentified)} → {owner} hátizsákja" +
                             FormatMageIdentification(identification));
            }
            else
            {
                _maze.DropItem(position, item, state: identification.State);
                messages.Add($"{ItemIdentificationRules.DisplayName(item, identification.State.IsIdentified)} a földön maradt (a hátizsákok tele vannak)" +
                             FormatMageIdentification(identification));
            }
        }
        if (foundItems.Count == 0 && messages.All(message => !message.StartsWith(ConsoleRenderer.MoneyIcon, StringComparison.Ordinal)))
            messages.Add("a tetemnél nem találtál zsákmányt");
        return foundGold || foundItems.Count > 0;
    }

    private int AdjustedSearchChance(LiveCharacter character, int baseChance) =>
        _lootService.AdjustedSearchChance(character, baseChance);

    private IItemDefinition? RollEquipmentLoot(MonsterLootDefinition loot) =>
        _lootService.RollEquipmentLoot(loot);

    private IItemDefinition? RollMasterThiefChestLoot(LiveCharacter character) =>
        _lootService.RollMasterThiefChestLoot(character, AllTradableItems());

    private bool TryStoreLootInParty(IItemDefinition item, out string ownerName) =>
        LootAndInventoryService.TryStoreLootInParty(item, PartyLeader, CharacterRoster.Party.Members, out ownerName);
}
