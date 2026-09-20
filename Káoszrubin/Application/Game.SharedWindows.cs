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
    private void ExecuteRestAcknowledgement(AcknowledgeRestCommand command)
    {
        if (_latestRestNotice?.RestId != command.RestId)
        {
            _session.RejectExecutedCommand(command, "Ez a pihenési összegző már nem aktív.");
            return;
        }
        AcknowledgeRest(command.SenderId, command.CharacterId);
    }

    private void ExecuteSharedWindowAcknowledgement(AcknowledgeSharedWindowCommand command)
    {
        if (_activeSharedWindow is null || _activeSharedWindow.WindowId != command.WindowId ||
            _activeSharedWindow.Revision != command.Revision)
        {
            _session.RejectExecutedCommand(command, "Ez a közös ablak vagy annak oldala már nem aktív.");
            return;
        }
        if (!_sharedWindowAcknowledgements.Add(command.SenderId)) return;
        var characterName = CharacterRoster.Party.Members
            .FirstOrDefault(character => character.Id == command.CharacterId)?.Name ?? "Egy játékos";
        RecordSessionActivity(SessionActivityKind.System,
            $"✓ {characterName} elolvasta a közös ablakot.", ConsoleColor.DarkCyan,
            _session.CharacterControls.Where(control => control.AssignedPlayerId != command.SenderId &&
                                                        control.AssignedPlayerId is not null)
                .Select(control => control.CharacterId).ToArray());
        RequestCoopSnapshotPublish();
    }

    private void AcknowledgeRest(PlayerId playerId, CharacterId characterId)
    {
        if (!_restAcknowledgements.Add(playerId)) return;
        if (_session.ConnectedHumanPlayerIds.Any(other => other != playerId))
            PlaySessionSound(SoundEffect.Waiting, [characterId]);
        var characterName = CharacterRoster.Party.Members
            .FirstOrDefault(character => character.Id == characterId)?.Name ?? "Egy játékos";
        var message = $"✓ {characterName} bezárta a pihenési összegzőt.";
        var otherCharacters = _session.CharacterControls
            .Where(control => control.AssignedPlayerId != playerId && control.AssignedPlayerId is not null)
            .Select(control => control.CharacterId).ToArray();
        RecordSessionActivity(SessionActivityKind.System, message, ConsoleColor.DarkCyan, otherCharacters);
        if (playerId != _session.HostPlayerId) _hostRestAcknowledgementMessages.Add(message);
    }

    private void ExecuteAssignQuickSpell(AssignQuickSpellCommand command)
    {
        var character = CharacterRoster.Party.Members.FirstOrDefault(member => member.Id == command.CharacterId);
        var spell = character?.KnownSpells.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, command.SpellId, StringComparison.OrdinalIgnoreCase));
        if (character is null || spell is null || !character.AssignQuickSpell(command.QuickSlot, spell))
            _session.RejectExecutedCommand(command, "Csak memorizált varázslat tehető gyorshelyre.");
    }

    private void ExecuteSpellPreparation(PrepareSpellsCommand command)
    {
        if (_activeSpellPreparation is null || _activeSpellPreparation.PromptId != command.PromptId ||
            _activeSpellPreparation.CharacterId != command.CharacterId)
        {
            _session.RejectExecutedCommand(command, "Ez a memorizálási kérés már nem aktív.");
            return;
        }
        var character = CharacterRoster.Party.Members.FirstOrDefault(member => member.Id == command.CharacterId);
        var ids = command.SpellIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var spells = character?.KnownSpells.Where(spell => ids.Contains(spell.Id, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (character is null || spells is null || spells.Length != ids.Length || !character.SetMemorizedSpells(spells))
        {
            _session.RejectExecutedCommand(command, "A választott varázslatlista nem memorizálható.");
            return;
        }
        _spellPreparationCompleted = true;
    }

    private void ExecuteLevelUpPrompt(ResolveLevelUpPromptCommand command)
    {
        if (_activeLevelUpPrompt is null || _activeLevelUpPrompt.PromptId != command.PromptId ||
            _activeLevelUpPrompt.CharacterId != command.CharacterId ||
            (_activeLevelUpPrompt.Kind != LevelUpPromptKind.Summary &&
             _activeLevelUpPrompt.Choices.All(choice => choice.Id != command.ChoiceId)))
        {
            _session.RejectExecutedCommand(command, "Ez a szintlépési választás már nem aktív vagy nem érvényes.");
            return;
        }
        _levelUpResponse = command.ChoiceId;
        _levelUpPromptCompleted = true;
    }

    private void ShowSynchronizedNarrative(NarrativeKind kind, string title, string subtitle,
        IReadOnlyList<string> paragraphs, BossPresentationSnapshot? boss = null)
    {
        var previousPhase = _session.Phase;
        _narrativeAcknowledgements.Clear();
        _activeNarrative = new NarrativeSnapshot(Guid.NewGuid(), kind, title, subtitle, paragraphs, [], boss);
        _session.SetPhase(GameSessionPhase.Paused);
        _renderer.ShowStoryOverlay(title, subtitle, paragraphs, _maze, _fogOfWar, _player.Position, kind, boss);
        RequestCoopSnapshotPublish();
        while (true)
        {
            ProcessSessionCommands();
            if (Console.KeyAvailable && Console.ReadKey(intercept: true).Key == ConsoleKey.Enter)
            {
                _narrativeAcknowledgements.Add(_session.HostPlayerId);
                if (_session.ConnectedHumanPlayerIds.Any(player => player != _session.HostPlayerId))
                    PlaySessionSound(SoundEffect.Waiting, [PartyLeader.Id]);
            }
            var required = _session.ConnectedHumanPlayerIds;
            if (required.All(_narrativeAcknowledgements.Contains)) break;
            TryPublishScheduledCoopSnapshot(DateTime.UtcNow);
            Thread.Sleep(20);
        }
        _renderer.CloseStoryOverlay();
        _activeNarrative = null;
        _narrativeAcknowledgements.Clear();
        _session.SetPhase(previousPhase);
        RequestCoopSnapshotPublish();
    }

    private void ShowSynchronizedLevelImage(string fileName, string path)
    {
        var previousPhase = _session.Phase;
        _levelImageAcknowledgements.Clear();
        _activeLevelImage = new LevelImageSnapshot(Guid.NewGuid(), _maze.LevelName, fileName, []);
        _session.SetPhase(GameSessionPhase.Paused);
        RequestCoopSnapshotPublish();

        if (!ImageViewer.Show(path))
            _renderer.DrawDeveloperMessage($"Pályakép még nem található: {fileName}");
        AcknowledgeLevelImage(_session.HostPlayerId, PartyLeader.Id);
        RequestCoopSnapshotPublish();

        while (true)
        {
            ProcessSessionCommands();
            if (_session.ConnectedHumanPlayerIds.All(_levelImageAcknowledgements.Contains)) break;
            TryPublishScheduledCoopSnapshot(DateTime.UtcNow);
            Thread.Sleep(20);
        }

        _activeLevelImage = null;
        _levelImageAcknowledgements.Clear();
        _session.SetPhase(previousPhase);
        RequestCoopSnapshotPublish();
    }

    private void AcknowledgeLevelImage(PlayerId playerId, CharacterId characterId)
    {
        if (!_levelImageAcknowledgements.Add(playerId)) return;
        var characterName = CharacterRoster.Party.Members
            .FirstOrDefault(character => character.Id == characterId)?.Name ?? "Egy játékos";
        var message = $"👤 {characterName} készen áll a játékra.";
        var otherCharacters = _session.CharacterControls
            .Where(control => control.AssignedPlayerId != playerId && control.AssignedPlayerId is not null &&
                              control.ConnectionState == PlayerConnectionState.Connected)
            .Select(control => control.CharacterId).ToArray();
        RecordSessionActivity(SessionActivityKind.System, message, ConsoleColor.Green, otherCharacters);
        if (playerId != _session.HostPlayerId)
            _renderer.DrawInventoryMessage(message, ConsoleColor.Green);
    }

    private void ShowSynchronizedRest(PartyRestSnapshot rest)
    {
        var previousPhase = _session.Phase;
        _restAcknowledgements.Clear();
        _hostRestAcknowledgementMessages.Clear();
        _latestRestNotice = rest;
        _session.SetPhase(GameSessionPhase.Paused);
        DrawRestSummaryForHost();
        var renderedAcknowledgementCount = _restAcknowledgements.Count;
        RequestCoopSnapshotPublish();
        while (true)
        {
            ProcessSessionCommands();
            if (Console.KeyAvailable && Console.ReadKey(intercept: true).Key == ConsoleKey.Enter)
                AcknowledgeRest(_session.HostPlayerId, PartyLeader.Id);
            var required = _session.ConnectedHumanPlayerIds;
            if (required.All(_restAcknowledgements.Contains)) break;
            if (_restAcknowledgements.Count != renderedAcknowledgementCount)
            {
                DrawRestSummaryForHost();
                renderedAcknowledgementCount = _restAcknowledgements.Count;
            }
            TryPublishScheduledCoopSnapshot(DateTime.UtcNow);
            Thread.Sleep(20);
        }
        _latestRestNotice = null;
        _restAcknowledgements.Clear();
        _session.SetPhase(previousPhase);
        RequestCoopSnapshotPublish();
        foreach (var message in _hostRestAcknowledgementMessages)
            _renderer.DrawInventoryMessage(message, ConsoleColor.DarkCyan);
        _hostRestAcknowledgementMessages.Clear();
    }

    private void DrawRestSummaryForHost()
    {
        if (_latestRestNotice is not { } rest) return;
        var acknowledged = _restAcknowledgements.Contains(_session.HostPlayerId);
        _renderer.DrawRestSummaryScreen(rest,
            acknowledged ? "❖  Várakozás a másik játékosra…  ❖" : "❖  Nyomj Entert a folytatáshoz...  ❖",
            acknowledged ? ConsoleColor.DarkCyan : ConsoleColor.Green);
    }

    private void ContinueDisconnectedRemoteBattleAsNpc()
    {
        if (_activeTeamBattle is { IsCompleted: false } teamBattle &&
            teamBattle.CurrentCharacter is { } teamCharacter &&
            !_session.IsHumanControlled(teamCharacter.Id))
        {
            ContinueTeamBattle();
            return;
        }
        return;
    }

    private void ExecuteLeaderAction(LeaderAction action)
    {
        switch (action)
        {
            case LeaderAction.ToggleFormation:
                ToggleFormation();
                break;
            case LeaderAction.RotateFormationLeft:
                RotateFormation(clockwise: false);
                break;
            case LeaderAction.RotateFormationRight:
                RotateFormation(clockwise: true);
                break;
            case LeaderAction.ToggleRegrouping:
                TogglePartyRegrouping();
                break;
            case LeaderAction.ToggleHoldPosition:
                TogglePartyHoldPosition();
                break;
            case LeaderAction.ScatterParty:
                ScatterPartyTemporarily();
                break;
            case LeaderAction.ToggleAttackMode:
                TogglePartyAttackMode();
                break;
            case LeaderAction.Rest:
                TryRestParty();
                break;
            case LeaderAction.ActivateExit:
                ActivateExit();
                break;
        }
    }

    private void RunHostPersonalWindow(PlayerWindowKind kind, Action action) =>
        RunHostPersonalWindow(kind, () => { action(); return true; });

    private T RunHostPersonalWindow<T>(PlayerWindowKind kind, Func<T> action)
    {
        if (_activeCoopHost is null) return action();
        var hadPrevious = _openPlayerWindows.TryGetValue(_session.HostPlayerId, out var previous);
        var windowId = hadPrevious ? previous!.WindowId : Guid.NewGuid();
        var previousCaptureSharedWindow = _captureSharedWindow;
        _captureSharedWindow = false;
        UpdatePlayerBlockingWindowState(_session.HostPlayerId, PartyLeader.Id, kind, windowId, true);
        ForceCoopSnapshotPublish();
        try
        {
            return action();
        }
        finally
        {
            _captureSharedWindow = previousCaptureSharedWindow;
            if (hadPrevious)
                UpdatePlayerBlockingWindowState(previous!.PlayerId, previous.CharacterId, previous.Kind,
                    previous.WindowId, true);
            else
                UpdatePlayerBlockingWindowState(_session.HostPlayerId, PartyLeader.Id, kind, windowId, false);
            ForceCoopSnapshotPublish();
        }
    }

    private void CloseHostSpellInfoWindow()
    {
        if (_activeCoopHost is null || _hostSpellInfoWindowId is not { } windowId) return;
        UpdatePlayerBlockingWindowState(_session.HostPlayerId, PartyLeader.Id,
            PlayerWindowKind.SpellInfo, windowId, false);
        _hostSpellInfoWindowId = null;
    }

    private void RunHostWindow(string title, string message, Action action) =>
        RunHostWindow(title, message, () => { action(); return true; });

    private T RunHostWindow<T>(string title, string message, Func<T> action)
    {
        var previousPhase = _session.Phase;
        var previousTitle = _leaderDecisionTitle;
        var previousMessage = _leaderDecisionMessage;
        var previousCaptureSharedWindow = _captureSharedWindow;
        var previousSharedWindowId = _sharedWindowId;
        var previousSharedWindowRevision = _sharedWindowRevision;
        var previousSharedWindow = _activeSharedWindow;
        var previousSharedWindowAcknowledgements = _sharedWindowAcknowledgements.ToArray();
        _captureSharedWindow = true;
        _sharedWindowId = Guid.NewGuid();
        _sharedWindowRevision = 1;
        _sharedWindowAcknowledgements.Clear();
        _activeSharedWindow = new ReplicatedWindowSnapshot(_sharedWindowId.Value, _sharedWindowRevision,
            title, 68, null, []);
        _leaderDecisionTitle = title;
        _leaderDecisionMessage = message;
        _session.SetPhase(GameSessionPhase.Paused);
        var remoteListeners = _session.CharacterControls
            .Where(control => control.AssignedPlayerId is { } playerId &&
                              playerId != _session.HostPlayerId &&
                              control.ConnectionState == PlayerConnectionState.Connected)
            .Select(control => control.CharacterId)
            .Distinct()
            .ToArray();
        if (remoteListeners.Length > 0)
            PlaySessionSound(SoundEffect.Waiting, remoteListeners);
        ForceCoopSnapshotPublish();
        try
        {
            var result = action();
            if (_activeCoopHost is not null && _activeSharedWindow is not null)
            {
                _sharedWindowAcknowledgements.Add(_session.HostPlayerId);
                ForceCoopSnapshotPublish();
                WaitForSharedWindowAcknowledgements();
            }
            return result;
        }
        finally
        {
            _captureSharedWindow = previousCaptureSharedWindow;
            _sharedWindowId = previousSharedWindowId;
            _sharedWindowRevision = previousSharedWindowRevision;
            _activeSharedWindow = previousSharedWindow;
            _sharedWindowAcknowledgements.Clear();
            _sharedWindowAcknowledgements.UnionWith(previousSharedWindowAcknowledgements);
            _leaderDecisionTitle = previousTitle;
            _leaderDecisionMessage = previousMessage;
            _session.SetPhase(previousPhase);
            if (previousPhase == GameSessionPhase.Battle && _activeTeamBattle is not { IsCompleted: false })
                MarkCoopSnapshotDirty();
            else
                ForceCoopSnapshotPublish();
        }
    }

    private void CaptureSharedWindowPresentation(int width,
        IReadOnlyList<(string Text, ConsoleColor Color)> lines, FramedWindow? frame)
    {
        if (!_captureSharedWindow || _sharedWindowId is not { } windowId) return;
        var projectedLines = lines.Select(line => new ReplicatedWindowLineSnapshot(line.Text, line.Color)).ToArray();
        var frameName = frame?.ToString();
        var changed = _activeSharedWindow is not { } current || current.Width != width ||
                      !string.Equals(current.Frame, frameName, StringComparison.Ordinal) ||
                      !current.Lines.SequenceEqual(projectedLines);
        if (!changed) return;
        _sharedWindowRevision++;
        _sharedWindowAcknowledgements.Clear();
        _activeSharedWindow = new ReplicatedWindowSnapshot(windowId, _sharedWindowRevision,
            _leaderDecisionTitle ?? "Közös ablak", width, frameName, projectedLines);
        ForceCoopSnapshotPublish();
    }

    private void WaitForSharedWindowAcknowledgements()
    {
        while (_activeSharedWindow is not null)
        {
            ProcessSessionCommands();
            if (PruneDisconnectedPlayerWindows()) RefreshCoopWindowStatus();
            var required = _session.ConnectedHumanPlayerIds;
            if (required.All(_sharedWindowAcknowledgements.Contains)) return;
            TryPublishScheduledCoopSnapshot(DateTime.UtcNow);
            Thread.Sleep(20);
        }
    }

    private void ForceCoopSnapshotPublish()
    {
        SynchronizeInventoryQuests();
        MarkCoopSnapshotDirty();
        if (_activeCoopHost is null || !_activeCoopHost.TryPublish(CreateSessionSnapshot())) return;
        _coopSnapshotDirty = false;
        _nextCoopSnapshotHeartbeatUtc = DateTime.UtcNow + CoopSnapshotHeartbeatInterval;
    }
}
