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
    private bool TryStartAdHocFollowerConversation(DateTime now)
    {
        if (_session.Phase != GameSessionPhase.Exploration || _characterSheetFocused ||
            _activeTeamBattle is not null || _activeNarrative is not null ||
            _adHocConversationMazeLevel == _mazeLevel ||
            now - _lastAdHocConversationUtc < TimeSpan.FromHours(1) ||
            _maze.Enemies.Any(enemy => _fogOfWar.IsEnemyVisible(enemy.Id, enemy.Position)) ||
            _random.Next(100) >= 15) return false;

        var candidates = GetAdHocConversationCandidates().OrderBy(_ => _random.Next()).ToArray();
        foreach (var npc in candidates)
        {
            var starts = Enumerable.Range(1, 5).Select(index => $"ADHOC_{index}_START")
                .Where(state => !_usedAdHocConversationIds.Contains(AdHocConversationId(npc, state)) &&
                                _gameData.GetNpcStoryChoices(npc.StoryId!, state, npc.Friendliness).Count == 2)
                .OrderBy(_ => _random.Next()).ToArray();
            if (starts.Length == 0) continue;
            var state = starts[0];
            _usedAdHocConversationIds.Add(AdHocConversationId(npc, state));
            _lastAdHocConversationUtc = now;
            _adHocConversationMazeLevel = _mazeLevel;
            RunAdHocFollowerConversation(npc, state);
            return true;
        }
        return false;
    }

    private IReadOnlyList<WorldNpc> GetAdHocConversationCandidates() =>
        _storyConversationCoordinator.GetAdHocConversationCandidates(_maze, _player, CharacterRoster, PartyLeader);

    private static bool IsAdHocConversationStory(string? storyId) =>
        StoryConversationCoordinator.IsAdHocConversationStory(storyId);

    private static string AdHocConversationId(WorldNpc npc, string startState) =>
        StoryConversationCoordinator.AdHocConversationId(npc, startState);

    private void RunAdHocFollowerConversation(WorldNpc npc, string startState)
    {
        var previousPhase = _session.Phase;
        var conversationId = Guid.NewGuid();
        var transcript = new List<string>();
        var state = startState;
        _session.SetPhase(GameSessionPhase.Paused);
        try
        {
            while (true)
            {
                var choices = _gameData.GetNpcStoryChoices(npc.StoryId!, state, npc.Friendliness);
                if (choices.Count != 2) return;
                _activeAdHocConversation = new AdHocConversationSnapshot(conversationId, npc.Character.Name,
                    npc.Character.Race.Name, npc.Character.CharacterClass.Name, transcript.ToArray(),
                    choices[0].Prompt, choices.Select(choice => choice.Text).ToArray());
                RequestCoopSnapshotPublish();
                var index = _renderer.DrawUniqueNpcStoryChoice(npc, choices[0].Prompt,
                    choices.Select(choice => choice.Text).ToArray(), transcript);
                var selected = choices[index];
                transcript.Add($"Te: {selected.Text}");
                transcript.AddRange(selected.Response.Split('|',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                if (selected.ContinueConversation)
                {
                    state = selected.NextStateId;
                    continue;
                }
                _activeAdHocConversation = new AdHocConversationSnapshot(conversationId, npc.Character.Name,
                    npc.Character.Race.Name, npc.Character.CharacterClass.Name, transcript.ToArray(), string.Empty, []);
                RequestCoopSnapshotPublish();
                _renderer.DrawUniqueNpcStoryResponse(npc, transcript);
                return;
            }
        }
        finally
        {
            _activeAdHocConversation = null;
            _session.SetPhase(previousPhase);
            RequestCoopSnapshotPublish();
            _renderer.CharacterSheet.SetCharacterSheetFocused(_characterSheetFocused);
        }
    }


    private bool MoveEnemies(DateTime now)
    {
        var stateChanged = false;
        var dueEnemies = new List<Enemy>();
        _nextEnemyActionUtc = DateTime.MaxValue;
        foreach (var enemy in _maze.Enemies)
        {
            var scheduled = _nextEnemyMoves.GetValueOrDefault(enemy);
            if (scheduled <= now) dueEnemies.Add(enemy);
            else if (scheduled < _nextEnemyActionUtc) _nextEnemyActionUtc = scheduled;
        }
        Shuffle(dueEnemies);
        foreach (var enemy in dueEnemies)
        {
            ScheduleNextEnemyMove(enemy, now);
            var previousEffectCount = enemy.ActiveSpellEffects.Count;
            var spellTick = enemy.AdvanceSpellEffects(_random);
            if (enemy.ActiveSpellEffects.Count != previousEffectCount) stateChanged = true;
            if (spellTick.Damage > 0)
            {
                stateChanged = true;
                var spellNotes = new List<string>();
                ApplyExplorationSpellDamage(PartyLeader, enemy, spellTick.Damage, spellNotes);
                _renderer.DrawInventoryMessage(string.Join("; ", spellTick.Notes.Concat(spellNotes)), ConsoleColor.Magenta);
                if (enemy.CurrentHitPoints <= 0) continue;
            }
            if (spellTick.SkipAction) continue;
            var visibleTarget = FindVisibleEnemyTarget(enemy);
            var detectedTarget = visibleTarget ?? FindSensedEnemyTarget(enemy);
            if (detectedTarget is not null)
                AlertEnemyGroup(enemy, detectedTarget.Value.Character.Id, detectedTarget.Value.Position);
            if (enemy.ConsumeReactionDelay()) continue;

            Direction? direction;
            if (enemy.PursuitState == EnemyPursuitState.Pursuing)
            {
                if (detectedTarget is null && !enemy.TryRememberPursuitTarget())
                {
                    BeginEnemySearch(enemy);
                    direction = EnemySearchOrReturnDirection(enemy);
                }
                else
                {
                    var targetPosition = detectedTarget?.Position ?? enemy.LastKnownTargetPosition;
                    if (detectedTarget is null && targetPosition == enemy.Position &&
                        enemy.LastKnownTargetDirection is { } rememberedDirection)
                    {
                        var predictedTarget = enemy.Position + rememberedDirection;
                        if (_maze.IsWalkable(predictedTarget) && predictedTarget != _maze.Entrance &&
                            predictedTarget != _maze.Exit)
                        {
                            enemy.AdvancePredictedTarget(predictedTarget);
                            targetPosition = predictedTarget;
                        }
                    }
                    direction = targetPosition is { } target && target != enemy.Position
                        ? FindEnemyStepToward(enemy, target)
                        : null;
                    if (direction is null && detectedTarget is null && enemy.RegisterPursuitPathFailure())
                    {
                        BeginEnemySearch(enemy);
                        direction = EnemySearchOrReturnDirection(enemy);
                    }
                }
            }
            else if (enemy.SearchRole != EnemySearchRole.None)
                direction = EnemySearchOrReturnDirection(enemy);
            else
                direction = enemy.Alertness == EnemyAlertness.Sleeping ? null : enemy.MovementProfile switch
                {
                    EnemyMovementProfile.Stationary => null,
                    EnemyMovementProfile.Patrol => enemy.PatrolDirection,
                    _ => Directions[_random.Next(Directions.Length)]
                };
            if (direction is null) continue;
            if (TryMoveEnemy(enemy, direction.Value))
            {
                stateChanged = true;
                if (enemy.PursuitState == EnemyPursuitState.Pursuing)
                    enemy.ResetPursuitPathFailures();
                if (_battleStarted) return true;
                if (enemy.SearchRole == EnemySearchRole.Scout)
                    foreach (var scout in EnemyGroup(enemy).Where(member =>
                                 member.SearchRole == EnemySearchRole.Scout &&
                                 member.SearchAnchorPosition == enemy.SearchAnchorPosition))
                        scout.RecordSearchVisit(enemy.Position);
                else if (enemy.SearchRole == EnemySearchRole.Returning &&
                         Manhattan(enemy.Position, enemy.HomePosition) <= 1)
                    enemy.CompleteReturn();
                continue;
            }
            if (enemy.PursuitState != EnemyPursuitState.Pursuing && enemy.SearchRole == EnemySearchRole.None &&
                enemy.MovementProfile == EnemyMovementProfile.Patrol)
            {
                enemy.ReversePatrolDirection();
                if (TryMoveEnemy(enemy, enemy.PatrolDirection))
                {
                    stateChanged = true;
                    if (_battleStarted) return true;
                }
            }
            else if (enemy.PursuitState == EnemyPursuitState.Pursuing &&
                     enemy.RegisterPursuitPathFailure())
                BeginEnemySearch(enemy);
        }
        return stateChanged;
    }

    private void Shuffle<T>(IList<T> values)
    {
        for (var index = values.Count - 1; index > 0; index--)
        {
            var other = _random.Next(index + 1);
            (values[index], values[other]) = (values[other], values[index]);
        }
    }

    private (LiveCharacter Character, Position Position)? FindVisibleEnemyTarget(Enemy enemy)
    {
        return EnemyTargeting.ChooseNearestVisible(enemy.Position, LivingPartyWithPositions().ToArray(),
            position => FogOfWar.CanSee(_maze, enemy.Position, position, enemy.EffectiveVisionRange), _random,
            enemy.PursuitTargetCharacterId);
    }

    private (LiveCharacter Character, Position Position)? FindSensedEnemyTarget(Enemy enemy)
    {
        return EnemyTargeting.ChooseNearestSensed(enemy.Position, LivingPartyWithPositions().ToArray(),
            enemy.Definition.TrackingSense,
            position => EnemyDistanceMap(position).TryGetValue(enemy.Position, out var distance) ? distance : null,
            _random, enemy.PursuitTargetCharacterId);
    }

    private void AlertEnemyGroup(Enemy observer, CharacterId targetCharacterId, Position targetPosition)
    {
        foreach (var enemy in EnemyGroup(observer))
        {
            var memoryMoves = _random.Next(Enemy.MinimumPursuitMemoryMoves,
                Enemy.MaximumPursuitMemoryMoves + 1);
            if (enemy.PursuitState == EnemyPursuitState.Pursuing &&
                enemy.PursuitTargetCharacterId == targetCharacterId &&
                enemy.SearchRole == EnemySearchRole.None)
            {
                enemy.RefreshKnownTarget(targetPosition, memoryMoves);
                continue;
            }
            var reactionDelay = enemy.Alertness switch
            {
                EnemyAlertness.Sleeping => _random.Next(4, 9),
                EnemyAlertness.Drowsy => _random.Next(2, 5),
                _ => 0
            };
            enemy.BeginPursuit(targetCharacterId, targetPosition, reactionDelay, memoryMoves);
        }
    }

    private IReadOnlyList<Enemy> EnemyGroup(Enemy member) => member.GroupId is null
        ? [member]
        : _maze.Enemies.Where(enemy => string.Equals(enemy.GroupId, member.GroupId,
            StringComparison.Ordinal)).ToList();

    private void BeginEnemySearch(Enemy observer)
    {
        if (observer.SearchRole != EnemySearchRole.None) return;
        EnemySearchCoordinator.BeginCoordinatedSearch(EnemyGroup(observer), observer, _random);
    }

    private Direction? EnemySearchOrReturnDirection(Enemy enemy)
    {
        if (enemy.SearchRole == EnemySearchRole.Returning)
        {
            if (enemy.ConsumeReturnDelay()) return null;
            if (Manhattan(enemy.Position, enemy.HomePosition) <= 1)
            {
                enemy.CompleteReturn();
                return null;
            }
            return FindEnemyStepToward(enemy, enemy.HomePosition);
        }
        if (enemy.SearchRole is not (EnemySearchRole.Scout or EnemySearchRole.Guarding)) return null;
        if (!enemy.ConsumeSearchMove())
        {
            enemy.BeginReturn(0);
            return EnemySearchOrReturnDirection(enemy);
        }

        var anchor = enemy.SearchAnchorPosition ?? enemy.Position;
        if (enemy.SearchRole == EnemySearchRole.Guarding)
            return Manhattan(enemy.Position, anchor) > Enemy.SearchGuardRadius
                ? FindEnemyStepToward(enemy, anchor)
                : null;

        if (Manhattan(enemy.Position, anchor) > Enemy.SearchCohesionRadius)
            return FindEnemyStepToward(enemy, anchor);

        var selected = EnemySearchNavigator.ChooseScoutDirection(enemy.Position, anchor,
            enemy.PatrolDirection, Enemy.SearchCohesionRadius, enemy.SearchVisitedPositions,
            Directions, position => CanEnemySearchTraverse(enemy, position), _random);
        if (selected is null)
        {
            enemy.BeginReturn(0);
            return EnemySearchOrReturnDirection(enemy);
        }
        enemy.RememberTravelDirection(selected.Value);
        return selected.Value;
    }

    private bool CanEnemySearchTraverse(Enemy enemy, Position position)
    {
        if (!_maze.IsWalkable(position) || position == _maze.Entrance || position == _maze.Exit) return false;
        if (position == _player.Position || _maze.GetPartyMemberAt(position) is not null) return true;
        var occupant = _maze.GetObjectAt(position);
        return occupant is null or GroundItemPile or Corpse || Maze.IsPassableNeutralNpc(occupant);
    }

    private void InitializeEnemyMoveSchedule(DateTime from)
    {
        _nextEnemyMoves.Clear();
        _nextEnemyActionUtc = DateTime.MaxValue;
        foreach (var enemy in _maze.Enemies) ScheduleNextEnemyMove(enemy, from);
    }

    private void ScheduleNextEnemyMove(Enemy enemy, DateTime from)
    {
        var scheduled = from + EnemyMoveInterval(enemy);
        _nextEnemyMoves[enemy] = scheduled;
        if (scheduled < _nextEnemyActionUtc) _nextEnemyActionUtc = scheduled;
    }

    private void RefreshNextEnemyActionUtc() =>
        _nextEnemyActionUtc = _nextEnemyMoves.Count == 0 ? DateTime.MaxValue : _nextEnemyMoves.Values.Min();

    private static TimeSpan EnemyMoveInterval(Enemy enemy)
    {
        var speed = Math.Max(1, enemy.EffectiveSpeed);
        return TimeSpan.FromMilliseconds((double)ZombieMoveIntervalMilliseconds * ZombieSpeed / speed);
    }

    private bool TryMoveEnemy(Enemy enemy, Direction direction)
    {
        var previousPosition = enemy.Position;
        var destination = previousPosition + direction;
        if (_maze.GetPartyMemberAt(destination) is { } encounteredMember)
        {
            StartBattle(encounteredMember, enemy, enemyStrikesFirst: true);
            return true;
        }
        if (destination == _player.Position)
        {
            StartBattle(enemy, enemyStrikesFirst: true);
            return true;
        }
        if (!_maze.TryMoveEnemy(enemy, destination)) return false;
        RevealFor(PartyLeader, _player.Position);
        _renderer.DrawEnemyMovement(_maze, _fogOfWar, previousPosition, enemy.Position, _player.Position);
        return true;
    }

    private Direction? FindEnemyStepToward(Enemy enemy, Position target)
    {
        var distances = EnemyDistanceMap(target);
        if (distances.TryGetValue(enemy.Position, out var currentDistance))
        {
            foreach (var direction in Directions)
            {
                var next = enemy.Position + direction;
                if (distances.GetValueOrDefault(next, int.MaxValue) >= currentDistance ||
                    !CanEnemyPathThrough(next, target)) continue;
                return direction;
            }
        }

        // Mozgó ellenfél vagy más dinamikus objektum elállhatja a közös térkép legjobb lépését.
        // Ilyenkor a régi, pontos akadálykezelésű keresés kerül elő, ezért a gyorsítás nem változtat
        // a zsúfolt folyosók átjárhatósági szabályain.
        return FindEnemyStepTowardDynamic(enemy, target);
    }

    private IReadOnlyDictionary<Position, int> EnemyDistanceMap(Position target)
    {
        if (_enemyDistanceMapMazeId != _maze.Id ||
            _enemyDistanceMapNavigationRevision != _maze.NavigationRevision)
        {
            _enemyDistanceMaps.Clear();
            _enemyDistanceMapMazeId = _maze.Id;
            _enemyDistanceMapNavigationRevision = _maze.NavigationRevision;
        }
        if (_enemyDistanceMaps.TryGetValue(target, out var cached)) return cached;
        if (_enemyDistanceMaps.Count >= 16) _enemyDistanceMaps.Clear();

        var distances = new Dictionary<Position, int> { [target] = 0 };
        var queue = new Queue<Position>();
        queue.Enqueue(target);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var nextDistance = distances[current] + 1;
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (distances.ContainsKey(next) || !CanEnemyDistanceMapTraverse(next, target)) continue;
                distances[next] = nextDistance;
                queue.Enqueue(next);
            }
        }
        _enemyDistanceMaps[target] = distances;
        return distances;
    }

    private bool CanEnemyDistanceMapTraverse(Position position, Position target) =>
        _maze.IsWalkable(position) &&
        (position == target || position != _maze.Entrance && position != _maze.Exit);

    private Direction? FindEnemyStepTowardDynamic(Enemy enemy, Position target)
    {
        var queue = new Queue<Position>();
        var previous = new Dictionary<Position, Position>();
        queue.Enqueue(enemy.Position);
        previous[enemy.Position] = enemy.Position;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == target) break;
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (previous.ContainsKey(next) || !CanEnemyPathThrough(next, target)) continue;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }
        if (!previous.ContainsKey(target)) return null;
        var step = target;
        while (previous[step] != enemy.Position) step = previous[step];
        return Directions.First(direction => enemy.Position + direction == step);
    }

    private bool CanEnemyPathThrough(Position position, Position target)
    {
        if (position == target) return _maze.IsWalkable(position);
        if (!_maze.IsWalkable(position) || position == _maze.Entrance || position == _maze.Exit) return false;
        var occupant = _maze.GetObjectAt(position);
        return occupant is null or GroundItemPile or Corpse or PartyMemberAvatar ||
               Maze.IsPassableNeutralNpc(occupant);
    }

    private bool MovePartyMembers(DateTime now)
    {
        var stateChanged = NormalizeFormation();
        if (_formation.State == PartyFormationState.Assembling)
        {
            return AdvanceFormationAssembly(now);
        }
        if (_partyScatterUntil is { } scatterUntil && now >= scatterUntil)
        {
            _partyScatterUntil = null;
            _renderer.DrawDeveloperMessage(_partyHoldingPosition
                ? "A szétszóródás véget ért; a parti ismét helyben marad."
                : "A szétszóródás véget ért; a parti folytatja korábbi viselkedését.");
        }
        var isScattering = _partyScatterUntil is not null;
        if (_partyRegrouping) isScattering = false;
        if (_partyHoldingPosition && !isScattering && !_partyRegrouping) return false;
        foreach (var member in PartyMembersInFreeMovementOrder())
        {
            if (_formation.State == PartyFormationState.Locked && !member.IsTemporaryFollower &&
                _formation.Slots.Contains(member.Character.Id)) continue;
            if (_session.IsHumanControlled(member.Character.Id)) continue;
            if (_nextPartyMoves.GetValueOrDefault(member) > now) continue;
            ScheduleNextPartyMove(member, now);
            // Allow NPCs to cast simple exploration spells (heals/cures) before moving
            if (TryNpcCastExplorationSpell(member)) stateChanged = true;
            if (_formation.State == PartyFormationState.Locked && member.IsTemporaryFollower)
            {
                if (TryResolveAdjacentNpcBattle(member))
                {
                    stateChanged = true;
                    if (_battleStarted) return true;
                    continue;
                }
                if (MoveFollowerWithLockedFormation(member)) stateChanged = true;
                continue;
            }
            if (isScattering)
            {
                if (MovePartyMemberAwayFromLeader(member)) stateChanged = true;
                continue;
            }
            if (_partyRegrouping)
            {
                if (MovePartyMemberTowardLeader(member)) stateChanged = true;
                continue;
            }
            if (CanActivelyAttack(member) && TryResolveAdjacentNpcBattle(member))
            {
                stateChanged = true;
                if (_battleStarted) return true;
                continue;
            }
            var previous = member.Position;
            var next = ChoosePartyMemberStep(member);
            if (next is null || !CanEnterTrap(member.Character, next.Value) ||
                !_maze.TryMovePartyMember(member, next.Value, _player.Position)) continue;
            member.Character.RegisterExplorationStep();
            var newlyRevealed = RevealFor(member.Character, member.Position, advanceEnemyMemory: true);
            _renderer.DrawPartyMemberMovement(_maze, _fogOfWar, previous, member.Position, newlyRevealed, _player.Position);
            CheckBossDiscoveryAt(newlyRevealed, member.Character);
            TriggerTrapAt(member.Character, member.Position);
            stateChanged = true;
            if (CanActivelyAttack(member) && TryResolveAdjacentNpcBattle(member))
            {
                stateChanged = true;
                if (_battleStarted) return true;
            }
        }
        return stateChanged;
    }

    private bool ShouldProcessPartyMembers(DateTime now)
    {
        if (_formation.State == PartyFormationState.Assembling) return true;
        if (_partyScatterUntil is { } scatterUntil && now >= scatterUntil) return true;
        var isScattering = _partyScatterUntil is not null && !_partyRegrouping;
        if (_partyHoldingPosition && !isScattering && !_partyRegrouping) return false;

        foreach (var member in _maze.PartyMembers)
        {
            if (_formation.State == PartyFormationState.Locked && !member.IsTemporaryFollower &&
                _formation.Slots.Contains(member.Character.Id)) continue;
            if (_session.IsHumanControlled(member.Character.Id)) continue;
            if (_nextPartyMoves.GetValueOrDefault(member) <= now) return true;
        }
        return false;
    }

    private bool AdvanceFormationAssembly(DateTime now)
    {
        var targets = PartyFormationController.Positions(_formation, PartyLeader.Id, _player.Position);
        var result = PartyFormationAssemblyCoordinator.Advance(
            now,
            _maze,
            _player,
            PartyLeader.Id,
            targets,
            _maze.PartyMembers,
            _nextPartyMoves,
            FindNextFormationAssemblyStep,
            (member, targetPosition) => CanEnterTrap(member.Character, targetPosition),
            (member, position) => _maze.GetPartyMemberAt(position),
            (member, nextPosition) => _maze.TryMovePartyMember(member, nextPosition, _player.Position),
            (member, blockingFriend, position) => _maze.TrySwapPartyMembers(member, blockingFriend, _player.Position),
            RegisterFormationAssemblyMove,
            ScheduleNextPartyMove,
            (member, nextPosition) => _maze.GetEnemyAt(nextPosition),
            (member, enemy) => StartBattle(member, enemy),
            FormationAvatar);

        if (result.BattleStarted) return true;
        if (result.AllInPlace)
        {
            _formation = PartyFormationRules.WithState(_formation, PartyFormationState.Locked);
            _renderer.CharacterSheet.SetFormationStatus(_formation);
            _session.SetFormationMovementLocked(true);
            _formationObstacleReported = false;
            AnnouncePartyCommand("Az alakzat osszeallt. Csak a vezer mozgathatja; Ctrl+bal/jobb: fordulas.",
                ConsoleColor.Green);
            return true;
        }
        if (!result.MadeProgress && !_formationObstacleReported && result.ObstacleReported)
        {
            _formationObstacleReported = true;
            _renderer.DrawDeveloperMessage("Az alakzat meg nem tud osszeallni: egy kijelolt hely nem erheto el.");
        }
        return result.MadeProgress;
    }

    private void RegisterFormationAssemblyMove(PartyMemberAvatar member, Position previous)
    {
        member.Character.RegisterExplorationStep();
        var newlyRevealed = RevealFor(member.Character, member.Position, advanceEnemyMemory: true);
        _renderer.DrawPartyMemberMovement(_maze, _fogOfWar, previous, member.Position, newlyRevealed,
            _player.Position);
        CheckBossDiscoveryAt(newlyRevealed, member.Character);
        TriggerTrapAt(member.Character, member.Position);
    }

    private Position? FindNextFormationAssemblyStep(PartyMemberAvatar member, Position target,
        IReadOnlyDictionary<CharacterId, Position> formationTargets) =>
        PartyMovementController.FindNextFormationAssemblyStep(member, target, formationTargets, _maze, _player);

    private bool CanFormationAssemblyTraverse(PartyMemberAvatar member, Position position,
        IReadOnlyDictionary<CharacterId, Position> formationTargets) =>
        PartyMovementController.CanFormationAssemblyTraverse(member, position, formationTargets, _maze, _player);

    private void TogglePartyHoldPosition()
    {
        _partyCommandState = _partyCommandController.ToggleHoldPosition(_partyCommandState);
        _partyHoldingPosition = _partyCommandState.HoldingPosition;
        _partyRegrouping = _partyCommandState.Regrouping;
        _partyAttackMode = _partyCommandState.AttackMode;
        _partyScatterUntil = _partyCommandState.ScatterUntil;
        if (!_partyHoldingPosition)
            foreach (var member in _maze.PartyMembers) _nextPartyMoves[member] = DateTime.UtcNow;
        AnnouncePartyCommand(_partyHoldingPosition
            ? "✋ MEGÁLLJ: minden NPC társ azonnal tartja a helyét; a Támadás és Gyülekező kikapcsolt."
            : "✋ A Megállj parancs kikapcsolt; az NPC társak folytatják saját viselkedésüket.",
            _partyHoldingPosition ? ConsoleColor.Yellow : ConsoleColor.Gray);
    }

    private void TogglePartyRegrouping()
    {
        _partyCommandState = _partyCommandController.ToggleRegrouping(_partyCommandState);
        _partyHoldingPosition = _partyCommandState.HoldingPosition;
        _partyRegrouping = _partyCommandState.Regrouping;
        _partyAttackMode = _partyCommandState.AttackMode;
        _partyScatterUntil = _partyCommandState.ScatterUntil;
        foreach (var member in _maze.PartyMembers) _nextPartyMoves[member] = DateTime.UtcNow;
        AnnouncePartyCommand(_partyRegrouping
            ? "🛡️ GYÜLEKEZŐ: minden NPC társ harc keresése nélkül a vezér mellé zárkózik és ott marad; a Támadás és Megállj kikapcsolt."
            : "🛡️ A Gyülekező kikapcsolt; az NPC társak folytatják saját viselkedésüket.",
            _partyRegrouping ? ConsoleColor.Cyan : ConsoleColor.Gray);
    }

    private void TogglePartyAttackMode()
    {
        _partyCommandState = _partyCommandController.ToggleAttackMode(_partyCommandState);
        _partyHoldingPosition = _partyCommandState.HoldingPosition;
        _partyRegrouping = _partyCommandState.Regrouping;
        _partyAttackMode = _partyCommandState.AttackMode;
        _partyScatterUntil = _partyCommandState.ScatterUntil;
        foreach (var member in _maze.PartyMembers) _nextPartyMoves[member] = DateTime.UtcNow;
        AnnouncePartyCommand(_partyAttackMode
            ? "⚔️ TÁMADÁS: minden NPC társ agresszívan keresi és támadja az ellenfeleket a parancs kikapcsolásáig."
            : "⚔️ A Támadás kikapcsolt; az NPC társak visszatértek saját viselkedésükhöz.",
            _partyAttackMode ? ConsoleColor.Red : ConsoleColor.Gray);
    }

    private void AnnouncePartyCommand(string message, ConsoleColor color)
    {
        _renderer.DrawDeveloperMessage(message);
        RecordSessionActivity(SessionActivityKind.System, message, color);
    }

    private void ScatterPartyTemporarily()
    {
        _partyCommandState = _partyCommandController.ScatterTemporarily(_partyCommandState, DateTime.UtcNow);
        _partyHoldingPosition = _partyCommandState.HoldingPosition;
        _partyRegrouping = _partyCommandState.Regrouping;
        _partyAttackMode = _partyCommandState.AttackMode;
        _partyScatterUntil = _partyCommandState.ScatterUntil;
        foreach (var member in _maze.PartyMembers)
            _nextPartyMoves[member] = DateTime.UtcNow + TimeSpan.FromMilliseconds(_random.Next(0, 100));
        AnnouncePartyCommand("Partiparancs: szétszóródás 10 másodpercig; a Támadás, Gyülekező és Megállj kikapcsolt.", ConsoleColor.Magenta);
    }

    private bool MovePartyMemberTowardLeader(PartyMemberAvatar member)
    {
        if (Manhattan(member.Position, _player.Position) <= 1) return false;
        var next = FindNextStep(member, FreeNeighborsOf(_player.Position))
                   ?? FollowLeaderTrail(member, minimumLag: 1);
        if (next is null) return false;
        var previous = member.Position;
        if (!CanEnterTrap(member.Character, next.Value) ||
            !_maze.TryMovePartyMember(member, next.Value, _player.Position)) return false;
        member.Character.RegisterExplorationStep();
        var newlyRevealed = RevealFor(member.Character, member.Position, advanceEnemyMemory: true);
        _renderer.DrawPartyMemberMovement(_maze, _fogOfWar, previous, member.Position, newlyRevealed,
            _player.Position);
        CheckBossDiscoveryAt(newlyRevealed, member.Character);
        TriggerTrapAt(member.Character, member.Position);
        return true;
    }

    private bool MoveFollowerWithLockedFormation(PartyMemberAvatar follower)
    {
        var formationPositions = CurrentFormationPositions();
        var escortCandidates = _formation.Layout == PartyFormationLayout.SingleFile
            ? PartyFormationController.SingleFileEscortPositions(_formation, formationPositions,
                PartyLeader.Id)
            : PartyFormationController.EscortPositions(formationPositions, _formation.Facing);
        var escortPositions = escortCandidates
            .Where(position => position == follower.Position || CanPartyTraverse(follower, position))
            .ToArray();
        if (escortPositions.Contains(follower.Position)) return false;
        var next = FindNextStep(follower, escortPositions);
        if (next is null || !CanEnterTrap(follower.Character, next.Value)) return false;
        var previous = follower.Position;
        if (!_maze.TryMovePartyMember(follower, next.Value, _player.Position)) return false;
        ApplyFollowerEscortMoveEffects(follower, next.Value, previous);
        return true;
    }

    private void ApplyFollowerEscortMove(PartyMemberAvatar follower, Position destination)
    {
        if (follower.Position == destination) return;
        var previous = follower.Position;
        follower.MoveTo(destination);
        ApplyFollowerEscortMoveEffects(follower, destination, previous);
    }

    private void ApplyFollowerEscortMoveEffects(PartyMemberAvatar follower, Position destination,
        Position previous)
    {
        follower.Character.RegisterExplorationStep();
        var newlyRevealed = RevealFor(follower.Character, destination, advanceEnemyMemory: true);
        _renderer.DrawPartyMemberMovement(_maze, _fogOfWar, previous, destination, newlyRevealed,
            _player.Position);
        CheckBossDiscoveryAt(newlyRevealed, follower.Character);
        TriggerTrapAt(follower.Character, destination);
    }

    private bool MovePartyMemberAwayFromLeader(PartyMemberAvatar member)
    {
        if (Manhattan(member.Position, _player.Position) >= 10) return false;
        var target = FindReachablePositions(member, 12)
            .Where(entry => Manhattan(entry.Position, _player.Position) <= 10)
            .OrderByDescending(entry => Manhattan(entry.Position, _player.Position))
            .ThenBy(entry => entry.Distance)
            .FirstOrDefault();
        if (target == default) return false;
        var next = FindNextStep(member, [target.Position]);
        if (next is null) return false;
        var previous = member.Position;
        if (!CanEnterTrap(member.Character, next.Value) ||
            !_maze.TryMovePartyMember(member, next.Value, _player.Position)) return false;
        member.Character.RegisterExplorationStep();
        var newlyRevealed = RevealFor(member.Character, member.Position, advanceEnemyMemory: true);
        _renderer.DrawPartyMemberMovement(_maze, _fogOfWar, previous, member.Position, newlyRevealed, _player.Position);
        CheckBossDiscoveryAt(newlyRevealed, member.Character);
        TriggerTrapAt(member.Character, member.Position);
        return true;
    }

    private bool CanActivelyAttack(PartyMemberAvatar member) =>
        _partyAiController.CanActivelyAttack(_partyAttackMode, member);

    private bool TryResolveAdjacentNpcBattle(PartyMemberAvatar member) =>
        _partyAiController.TryResolveAdjacentNpcBattle(_maze, member, (avatar, enemy) => StartBattle(avatar, enemy));

    private bool IsQuestCriticalRoderic(PartyMemberAvatar member) =>
        member.TemporaryFollower is { } follower &&
        string.Equals(follower.StoryId, RodericStoryId, StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(follower.StoryStateId, "JOINED", StringComparison.OrdinalIgnoreCase);

    private void ScheduleNextPartyMove(PartyMemberAvatar member, DateTime from) =>
        _partyAiController.ScheduleNextPartyMove(member, from, _player, _nextPartyMoves);

    private bool CanControlledCharacterMove(LiveCharacter character) =>
        _partyAiController.CanControlledCharacterMove(character, _nextControlledMoves);

    private void ScheduleNextControlledMove(LiveCharacter character) =>
        _partyAiController.ScheduleNextControlledMove(character, _nextControlledMoves);

    private Position? ChoosePartyMemberStep(PartyMemberAvatar member)
    {
        var effectiveMember = _partyAttackMode && member.Character.NpcBehavior != NpcBehavior.Aggressive
            ? new PartyMemberAvatar(member.Position, member.Character, member.TemporaryFollower)
            : member;
        if (_partyAttackMode && member.Character.NpcBehavior != NpcBehavior.Aggressive)
        {
            var original = member.Character.NpcBehavior;
            member.Character.SetNpcBehavior(NpcBehavior.Aggressive);
            var step = PartyMovementController.ChoosePartyMemberStep(member, _maze, _player, _leaderFacing,
                _leaderTrail, CurrentLevelVisionModifier, FreeMovementFollowIndex(member));
            member.Character.SetNpcBehavior(original);
            return step;
        }
        return PartyMovementController.ChoosePartyMemberStep(member, _maze, _player, _leaderFacing,
            _leaderTrail, CurrentLevelVisionModifier, FreeMovementFollowIndex(member));
    }

    private Position? FollowLeaderTrail(PartyMemberAvatar member, int minimumLag) =>
        PartyMovementController.FollowLeaderTrail(member, minimumLag, _maze, _player, _leaderTrail,
            FreeMovementFollowIndex(member));

    private IReadOnlyList<PartyMemberAvatar> PartyMembersInFreeMovementOrder()
    {
        var members = _maze.PartyMembers.ToArray();
        var permanentMembers = members.Where(member => !member.IsTemporaryFollower).ToArray();
        var followOrder = PartyFormationRules.FollowOrder(_formation, PartyLeader.Id,
            permanentMembers.Select(member => member.Character.Id));
        var priorities = followOrder.Select((id, index) => (id, index))
            .ToDictionary(entry => entry.id, entry => entry.index);
        return members.Select((member, originalIndex) => (member, originalIndex))
            .OrderBy(entry => entry.member.IsTemporaryFollower ? 1 : 0)
            .ThenBy(entry => priorities.GetValueOrDefault(entry.member.Character.Id, int.MaxValue))
            .ThenBy(entry => entry.originalIndex)
            .Select(entry => entry.member)
            .ToArray();
    }

    private int FreeMovementFollowIndex(PartyMemberAvatar member) =>
        PartyMembersInFreeMovementOrder().ToList().IndexOf(member);

    private Position? ChooseForwardStep(PartyMemberAvatar member, int maximumLeaderDistance, int maximumSearchDistance, bool avoidNarrowFront) =>
        PartyMovementController.ChooseForwardStep(member, maximumLeaderDistance, maximumSearchDistance, avoidNarrowFront, _maze, _player, _leaderFacing);

    private Position? FindNextStep(PartyMemberAvatar member, IEnumerable<Position> targetPositions) =>
        PartyMovementController.FindNextStep(member, targetPositions, _maze, _player);

    private IReadOnlyList<(Position Position, int Distance)> FindReachablePositions(PartyMemberAvatar member, int maximumDistance) =>
        PartyMovementController.FindReachablePositions(member, maximumDistance, _maze, _player);

    private IEnumerable<Position> FreeNeighborsOf(Position origin) =>
        PartyMovementController.FreeNeighborsOf(_maze, _player, origin);

    private bool CanPartyTraverse(PartyMemberAvatar member, Position position) =>
        PartyMovementController.CanPartyTraverse(member, position, _maze, _player);

    private int CountWalkableNeighbors(Position position) =>
        PartyMovementController.CountWalkableNeighbors(position, _maze);

    private bool IsAheadOfLeader(Position position) =>
        PartyMovementController.IsAheadOfLeader(position, _player.Position, _leaderFacing);

    private static int Manhattan(Position first, Position second) => PartyMovementController.Manhattan(first, second);
    private static (int X, int Y) DirectionOffset(Direction direction) => PartyMovementController.DirectionOffset(direction);
}
