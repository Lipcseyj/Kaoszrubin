using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain;

namespace KaoszRubin.Application;

public sealed partial class Game
{
    private sealed record NpcChestOrder(TreasureChest Chest, PartyMemberAvatar Opener,
        bool ReassembleFormation, Position? ReturnPosition = null);

    private NpcChestOrder? _npcChestOrder;
    private bool _reassembleAfterChestBattle;
    private Maze? _chestAssignmentMaze;
    private readonly Dictionary<TreasureChest, CharacterId> _chestOpenerAssignments = [];

    private void TryOrderNpcToOpenChest()
    {
        if (_npcChestOrder is { } active)
        {
            AnnouncePartyCommand($"{active.Opener.Character.Name} már úton van a ládához.", ConsoleColor.Cyan);
            return;
        }
        if (_formation.State == PartyFormationState.Assembling)
        {
            AnnouncePartyCommand("Várd meg, amíg az alakzat összeáll vagy oszlasd fel.", ConsoleColor.DarkYellow);
            return;
        }
        if (!ReferenceEquals(_chestAssignmentMaze, _maze))
        {
            _chestOpenerAssignments.Clear();
            _chestAssignmentMaze = _maze;
        }

        var directions = new[] { _leaderFacing }.Concat(Directions.Where(direction => direction != _leaderFacing));
        var chest = directions.Select(direction => _maze.GetTreasureChestAt(_player.Position + direction))
            .FirstOrDefault(candidate => candidate is not null &&
                (candidate.Definition is null || !candidate.IsOpened || candidate.GoldAmount > 0 ||
                 candidate.RemainingItems.Count > 0));
        if (chest is null)
        {
            AnnouncePartyCommand("A vezér mellett nincs kincsesláda.", ConsoleColor.DarkYellow);
            return;
        }

        var npcIds = _session.CharacterControls
            .Where(control => control.ControllerKind == CharacterControllerKind.Npc)
            .Select(control => control.CharacterId).ToHashSet();
        var candidates = _maze.PartyMembers
            .Where(member => !member.IsTemporaryFollower && member.Character.IsAlive &&
                             npcIds.Contains(member.Character.Id) &&
                             NpcChestOpeningController.FindPath(_maze, member.Position, chest.Position,
                                 _player.Position) is { Count: > 0 })
            .ToArray();
        if (candidates.Length == 0)
        {
            AnnouncePartyCommand("Egyetlen NPC társ sem tud eljutni a ládához.", ConsoleColor.DarkYellow);
            return;
        }

        PartyMemberAvatar opener;
        if (_chestOpenerAssignments.TryGetValue(chest, out var assignedId))
        {
            var assigned = candidates.FirstOrDefault(member => member.Character.Id == assignedId);
            if (assigned is null)
            {
                AnnouncePartyCommand("A korábban kisorsolt társ most nem tud eljutni a ládához.",
                    ConsoleColor.DarkYellow);
                return;
            }
            opener = assigned;
        }
        else
        {
            opener = NpcChestOpeningController.ChooseOpener(candidates, _random);
            _chestOpenerAssignments[chest] = opener.Character.Id;
        }

        var reassemble = _formation.State == PartyFormationState.Locked;
        if (reassemble)
        {
            _formation = PartyFormationRules.WithState(_formation, PartyFormationState.Disbanded);
            _renderer.CharacterSheet.SetFormationStatus(_formation);
            _session.SetFormationMovementLocked(false);
        }
        _npcChestOrder = new NpcChestOrder(chest, opener, reassemble);
        _nextPartyMoves[opener] = DateTime.UtcNow;
        AnnouncePartyCommand($"Nyissátok ki! {opener.Character.Name} a ládához indul.", ConsoleColor.Cyan);
    }

    private bool AdvanceNpcChestOrder(DateTime now)
    {
        var order = _npcChestOrder!;
        if (!ReferenceEquals(_chestAssignmentMaze, _maze))
        {
            _npcChestOrder = null;
            _chestOpenerAssignments.Clear();
            _chestAssignmentMaze = _maze;
            return true;
        }
        if (!order.Opener.Character.IsAlive || !_maze.PartyMembers.Contains(order.Opener))
        {
            FinishNpcChestOrder("A ládanyitási utasítás megszakadt.");
            return true;
        }
        if (_nextPartyMoves.GetValueOrDefault(order.Opener) > now) return false;
        if (order.ReturnPosition is { } returnPosition)
        {
            var previousChestPosition = order.Opener.Position;
            if (!_maze.TryMovePartyMember(order.Opener, returnPosition, _player.Position))
            {
                FinishNpcChestOrder("A ládanyitó társ nem tudott visszalépni az alakzatba.");
                return true;
            }
            RegisterTerrainExplorationStep(order.Opener.Character, returnPosition);
            var returnReveal = RevealFor(order.Opener.Character, returnPosition, advanceEnemyMemory: true);
            _renderer.DrawPartyMemberMovement(_maze, _fogOfWar, previousChestPosition, returnPosition,
                returnReveal, _player.Position);
            CheckBossDiscoveryAt(returnReveal, order.Opener.Character);
            PlayCharacterStepSound(order.Opener.Character);
            TriggerTrapAt(order.Opener.Character, returnPosition);
            FinishNpcChestOrder(null);
            return true;
        }
        if (_maze.GetTreasureChestAt(order.Chest.Position) != order.Chest)
        {
            FinishNpcChestOrder("A ládanyitási utasítás megszakadt.");
            return true;
        }
        var path = NpcChestOpeningController.FindPath(_maze, order.Opener.Position,
            order.Chest.Position, _player.Position);
        if (path is null || path.Count == 0)
        {
            FinishNpcChestOrder("A kisorsolt társ nem tud eljutni a ládához.");
            return true;
        }

        var destination = path[0];
        if (!CanEnterTrap(order.Opener.Character, destination))
        {
            FinishNpcChestOrder("A társ útját csapda állította meg; a láda érintetlen maradt.");
            return true;
        }
        var previous = order.Opener.Position;
        var displaced = _maze.GetPartyMemberAt(destination);
        var moved = displaced is not null
            ? _maze.TrySwapPartyMembers(order.Opener, displaced, _player.Position)
            : _maze.TryMovePartyMember(order.Opener, destination, _player.Position,
                allowTreasureChest: destination == order.Chest.Position);
        if (!moved)
        {
            FinishNpcChestOrder("A társ útja elzáródott; a láda érintetlen maradt.");
            return true;
        }

        RegisterTerrainExplorationStep(order.Opener.Character, destination);
        var revealed = RevealFor(order.Opener.Character, destination, advanceEnemyMemory: true);
        _renderer.DrawPartyMemberMovement(_maze, _fogOfWar, previous, destination, revealed, _player.Position);
        CheckBossDiscoveryAt(revealed, order.Opener.Character);
        ScheduleNextPartyMove(order.Opener, now);
        if (displaced is not null)
        {
            RegisterTerrainExplorationStep(displaced.Character, previous);
            var displacedRevealed = RevealFor(displaced.Character, previous, advanceEnemyMemory: true);
            _renderer.DrawPartyMemberMovement(_maze, _fogOfWar, destination, previous,
                displacedRevealed, _player.Position);
            CheckBossDiscoveryAt(displacedRevealed, displaced.Character);
            ScheduleNextPartyMove(displaced, now);
            TriggerTrapAt(displaced.Character, previous);
        }
        PlayCharacterStepSound(order.Opener.Character);
        if (destination == order.Chest.Position)
        {
            CollectTreasureChest(order.Opener.Character, destination, shareLootWithParty: false);
            _chestOpenerAssignments.Remove(order.Chest);
            if (_maze.GetTreasureChestAt(destination) == order.Chest)
                _npcChestOrder = order with { ReturnPosition = previous };
        }
        TriggerTrapAt(order.Opener.Character, destination);
        if (destination == order.Chest.Position && _npcChestOrder?.ReturnPosition is null)
            FinishNpcChestOrder(null);
        else if (CanActivelyAttack(order.Opener))
            TryResolveAdjacentNpcBattle(order.Opener);
        return true;
    }

    private void InterruptNpcChestOrderForBattle()
    {
        if (_npcChestOrder is not { } order) return;
        _reassembleAfterChestBattle = order.ReassembleFormation;
        _npcChestOrder = null;
    }

    private void FinishNpcChestOrder(string? message)
    {
        var reassemble = _npcChestOrder?.ReassembleFormation == true;
        _npcChestOrder = null;
        if (message is not null) AnnouncePartyCommand(message, ConsoleColor.DarkYellow);
        if (reassemble && _formation.State == PartyFormationState.Disbanded) ToggleFormation();
    }
}
