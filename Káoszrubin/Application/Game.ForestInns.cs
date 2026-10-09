using KaoszRubin.Audio;
using KaoszRubin.Domain.Characters;
using KaoszRubin.World;

namespace KaoszRubin.Application;

public sealed partial class Game
{
    private void EnterForestInn(ForestInn inn)
    {
        if (!ForestInnPlacement.CanEnter(inn, _player.Position, _maze.PartyMembers, _battleStarted, out var reason))
        {
            _renderer.DrawInventoryMessage(reason!, ConsoleColor.DarkYellow);
            return;
        }
        if (!inn.TryVisit()) return;
        var enteredAt = DateTime.UtcNow;
        _session.SetPhase(GameSessionPhase.Paused);
        try
        {
            _innController.RunForestStop(_difficultyLevel, inn, () =>
            {
                _backgroundMusic.EnterInn();
                _session.SetPhase(GameSessionPhase.Inn);
                ForceCoopSnapshotPublish();
            });
        }
        finally
        {
            var pause = DateTime.UtcNow - enteredAt;
            ShiftExplorationSchedules(pause);
            foreach (var enemy in _maze.Enemies) enemy.ShiftHordeCamp(pause);
            _session.SynchronizeParty();
            foreach (var member in _maze.PartyMembers.Where(member => !member.IsTemporaryFollower &&
                !CharacterRoster.Party.Members.Contains(member.Character)).ToArray())
                _maze.RemovePartyMember(member);
            PlacePartyMembersNear(_player.Position);
            NormalizeFormation();
            _formation = PartyFormationRules.WithState(_formation, PartyFormationState.Disbanded);
            _renderer.CharacterSheet.SetFormationStatus(_formation);
            _session.SetFormationMovementLocked(false);
            _session.SetPhase(GameSessionPhase.Exploration);
            _backgroundMusic.SynchronizeMazeLevel(_difficultyLevel, IsLevelExitDiscovered());
            RevealFor(PartyLeader, _player.Position);
            _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
            var message = $"♨ Elhagytátok a(z) {inn.Name} fogadót. Ugyanitt folytatjátok az erdei utat.";
            _renderer.DrawInventoryMessage(message, ConsoleColor.Cyan);
            RecordSessionActivity(SessionActivityKind.System, message, ConsoleColor.Cyan);
            ForceCoopSnapshotPublish();
        }
    }

    private void ShowForestInnHint()
    {
        if (_maze.GetForestInnAt(_player.Position) is not { } inn) return;
        _renderer.DrawInventoryMessage(
            $"♨ {inn.Name}. Enter: betérés a fogadóba; gyűljön össze a parti.",
            ConsoleColor.Yellow);
    }
}
