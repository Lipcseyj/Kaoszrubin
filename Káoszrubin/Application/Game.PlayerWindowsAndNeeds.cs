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
    private void UpdatePlayerBlockingWindowState(PlayerId playerId, CharacterId characterId, PlayerWindowKind kind,
        Guid windowId, bool isOpen)
    {
        // A karakterlap a térkép melletti, valós idejű panel; önmagában nem blokkolja a közös játékot.
        if (!PlayerWindowKindRules.PausesGame(kind)) return;
        var characterName = CharacterRoster.Party.Members
            .FirstOrDefault(character => character.Id == characterId)?.Name ?? "Egy játékos";
        if (isOpen)
        {
            var wasPaused = _openPlayerWindows.Count > 0;
            if (_openPlayerWindows.TryGetValue(playerId, out var current) && current.WindowId == windowId)
            {
                if (current.Kind == kind && current.CharacterId == characterId) return;
                _openPlayerWindows[playerId] = new PlayerWindowStateSnapshot(playerId, characterId,
                    characterName, kind, windowId);
                RefreshCoopWindowStatus();
                RequestCoopSnapshotPublish();
                return;
            }

            _openPlayerWindows[playerId] = new PlayerWindowStateSnapshot(playerId, characterId,
                characterName, kind, windowId);
            RefreshCoopWindowStatus();
            if (!wasPaused) _playerWindowPauseStartedUtc = DateTime.UtcNow;
            var otherCharacters = _session.CharacterControls
                .Where(control => control.AssignedPlayerId is { } assigned && assigned != playerId &&
                                  control.ConnectionState == PlayerConnectionState.Connected)
                .Select(control => control.CharacterId)
                .Distinct()
                .ToArray();
            if (otherCharacters.Length > 0)
                PlaySessionSound(SoundEffect.Waiting, otherCharacters);
            RequestCoopSnapshotPublish();
            return;
        }

        if (!_openPlayerWindows.TryGetValue(playerId, out var open) || open.WindowId != windowId) return;
        _openPlayerWindows.Remove(playerId);
        RefreshCoopWindowStatus();
        var resumedMessage = $"▶ {characterName} bezárta: {PlayerWindowTitle(open.Kind)}.";
        RecordSessionActivity(SessionActivityKind.System, resumedMessage, ConsoleColor.Green);
        if (playerId != _session.HostPlayerId)
            _renderer.DrawInventoryMessage(resumedMessage, ConsoleColor.Green);
        RequestCoopSnapshotPublish();
        CompletePlayerWindowPauseIfPossible();
    }

    private string? CurrentHostCoopWindowStatus()
    {
        if (_activeCoopHost is null) return null;
        ProcessSessionCommands();
        PruneDisconnectedPlayerWindows();
        TryPublishScheduledCoopSnapshot(DateTime.UtcNow);
        var remote = _openPlayerWindows.Values.FirstOrDefault(window =>
            window.PlayerId != _session.HostPlayerId);
        return remote is null
            ? null
            : $"{remote.CharacterName} {PlayerWindowActivity(remote.Kind)}; a közös játék szünetel.";
    }

    private void RefreshCoopWindowStatus()
    {
        CoopWindowStatusBanner.Refresh(CurrentHostCoopWindowStatus);
    }

    private bool PruneDisconnectedPlayerWindows()
    {
        var connectedPlayers = _session.CharacterControls
            .Where(control =>
                control.AssignedPlayerId is not null &&
                control.ConnectionState == PlayerConnectionState.Connected)
            .Select(control => control.AssignedPlayerId!.Value)
            .Append(_session.HostPlayerId)
            .ToHashSet();

        var removed = false;

        foreach (var playerId in _openPlayerWindows.Keys
                     .Where(playerId => !connectedPlayers.Contains(playerId))
                     .ToArray())
        {
            _openPlayerWindows.Remove(playerId);
            removed = true;
        }

        if (!removed)
            return false;

        RequestCoopSnapshotPublish();
        CompletePlayerWindowPauseIfPossible();

        return true;
    }

    private void CompletePlayerWindowPauseIfPossible()
    {
        if (_openPlayerWindows.Count > 0 || _playerWindowPauseStartedUtc is not { } pauseStarted) return;

        var pauseDuration = DateTime.UtcNow - pauseStarted;
        _playerWindowPauseStartedUtc = null;
        if (!_battleStarted) ShiftExplorationSchedules(pauseDuration);
    }

    private static string PlayerWindowTitle(PlayerWindowKind kind) => kind switch
    {
        PlayerWindowKind.Help => "súgó",
        PlayerWindowKind.Settings => "beállítások",
        PlayerWindowKind.QuestJournal => "küldetésnapló",
        PlayerWindowKind.Inventory => "felszerelés",
        PlayerWindowKind.CharacterDetails => "részletes karakterinformáció",
        PlayerWindowKind.SpellInfo => "varázslatinformáció",
        _ => "személyes ablak"
    };

    private static string PlayerWindowActivity(PlayerWindowKind kind) => kind switch
    {
        PlayerWindowKind.Help => "a súgót olvassa",
        PlayerWindowKind.Settings => "a beállításokat kezeli",
        PlayerWindowKind.QuestJournal => "a küldetésnaplót böngészi",
        PlayerWindowKind.Inventory => "a felszerelését rendezi",
        PlayerWindowKind.CharacterDetails => "a részletes karakterinformációkat olvassa",
        PlayerWindowKind.SpellInfo => "a varázslatait böngészi",
        _ => "egy személyes ablakot használ"
    };

    private void DrainNeeds()
    {
        var followers = _maze.PartyMembers.Where(member => member.IsTemporaryFollower)
            .Select(member => member.Character);
        var characters = CharacterRoster.Party.Members.Concat(followers).Distinct();
        _sustenanceService.DrainNeeds(characters, IsAutonomousNpc, LogNewZeroNeed, TryNpcUseConsumables);
        _renderer.RefreshCharacterSheet(PartyLeader);
    }

    private int DrainNeedsAfterBattle(LiveCharacter character, int monsterTier) =>
        _sustenanceService.DrainNeedsAfterBattle(character, monsterTier, IsAutonomousNpc, LogNewZeroNeed);

    private void DrainNeedsAfterTeamBattle(LiveCharacter character, int cycles) =>
        _sustenanceService.DrainNeedsAfterTeamBattle(character, cycles, IsAutonomousNpc, LogNewZeroNeed);

    private bool IsAutonomousNpc(LiveCharacter character) =>
        character != PartyLeader && !_session.IsHumanControlled(character.Id) &&
        CharacterRoster.Party.Members.Contains(character) &&
        _maze.PartyMembers.Any(member => member.Character == character);

    private void TryNpcUseConsumables(LiveCharacter character)
    {
        if (!character.IsAlive || !IsAutonomousNpc(character)) return;
        if (character.HasStatus(CharacterStatusIds.Hungry))
            TryNpcConsumeNeedItems(character, ConsumableEffect.Food, NpcComplaintKind.Hunger);
        else ClearNpcShortage(character, NpcComplaintKind.Hunger);
        if (character.HasStatus(CharacterStatusIds.Thirsty))
            TryNpcConsumeNeedItems(character, ConsumableEffect.Water, NpcComplaintKind.Thirst);
        else ClearNpcShortage(character, NpcComplaintKind.Thirst);
        if (character.CurrentVitality < character.MaximumVitality)
            TryNpcConsumeHealingPotions(character);
        if (character.CurrentVitality * 2 >= character.MaximumVitality || HasHealingPotion(character))
            ClearNpcShortage(character, NpcComplaintKind.Injured);
        character.SynchronizeNeedStatuses(_gameData.GetStatus(CharacterStatusIds.Hungry),
            _gameData.GetStatus(CharacterStatusIds.Thirsty));
    }

    private void TryNpcConsumeNeedItems(LiveCharacter character, ConsumableEffect effect, NpcComplaintKind kind)
    {
        var desiredServings = _random.Next(1, 4);
        var consumed = new List<string>();
        for (var serving = 0; serving < desiredServings; serving++)
        {
            var current = effect == ConsumableEffect.Food ? character.FoodLevel : character.WaterLevel;
            if (current >= 100) break;
            var candidates = BackpackConsumables(character, effect)
                .Where(entry => Math.Max(0, current + entry.Item.EffectValue - 100) <= 15)
                .ToArray();
            if (candidates.Length == 0) break;
            var selected = candidates[_random.Next(candidates.Length)];
            if (!character.RemoveOneInventoryItem(InventorySlotKind.Backpack, selected.Index)) break;
            if (effect == ConsumableEffect.Food) character.RestoreFood(selected.Item.EffectValue);
            else if (string.Equals(selected.Item.Id, MiscItemIds.HerbalTea, StringComparison.OrdinalIgnoreCase))
                UseHerbalTea(character, selected.Item.EffectValue);
            else if (IsInitiativeDrink(selected.Item)) UseInitiativeDrink(character, selected.Item);
            else character.RestoreWater(selected.Item.EffectValue);
            consumed.Add(selected.Item.Name);
        }
        if (consumed.Count > 0)
        {
            ClearNpcShortage(character, kind);
            var level = effect == ConsumableEffect.Food ? character.FoodLevel : character.WaterLevel;
            var action = effect == ConsumableEffect.Food ? "evett" : "ivott";
            LogNpcAutomation(character, $"{character.Name} {action}: {string.Join(", ", consumed)}. " +
                $"{(effect == ConsumableEffect.Food ? "🍖" : "💧")} {level}/100.", ConsoleColor.Cyan);
            return;
        }
        RegisterNpcShortage(character, kind);
    }

    private bool TryNpcConsumeHealingPotions(LiveCharacter character)
    {
        var desiredServings = _random.Next(1, 4);
        var consumed = new List<string>();
        for (var serving = 0; serving < desiredServings; serving++)
        {
            var missingVitality = character.MaximumVitality - character.CurrentVitality;
            if (missingVitality <= 0) break;
            var candidates = BackpackConsumables(character, ConsumableEffect.Heal)
                .Where(entry => Math.Max(0, character.PreviewVitalityRecovery(entry.Item.EffectValue) - missingVitality) <= 15)
                .ToArray();
            if (candidates.Length == 0) break;
            var selected = candidates[_random.Next(candidates.Length)];
            if (!character.RemoveOneInventoryItem(InventorySlotKind.Backpack, selected.Index)) break;
            character.RestoreVitality(selected.Item.EffectValue);
            consumed.Add(selected.Item.Name);
        }
        if (consumed.Count > 0)
        {
            ClearNpcShortage(character, NpcComplaintKind.Injured);
            LogNpcAutomation(character, $"{character.Name} gyógyitalt ivott: {string.Join(", ", consumed)}. " +
                $"❤️ {character.CurrentVitality}/{character.MaximumVitality}.", ConsoleColor.Green);
        }
        else if (character.CurrentVitality * 2 < character.MaximumVitality && !HasHealingPotion(character))
            RegisterNpcShortage(character, NpcComplaintKind.Injured);
        return consumed.Count > 0;
    }

    private static IEnumerable<(int Index, MiscItemDefinition Item)> BackpackConsumables(
        LiveCharacter character, ConsumableEffect effect) =>
        PartySustenanceService.BackpackConsumables(character, effect);

    private static bool HasHealingPotion(LiveCharacter character) =>
        PartySustenanceService.HasHealingPotion(character);

    private void LogNewZeroNeed(LiveCharacter character, NpcComplaintKind kind, int previous, int current)
    {
        if (previous <= 0 || current > 0) return;
        ScheduleNpcComplaint(character, kind, DateTime.UtcNow);
    }

    private bool ProcessNpcComplaints(DateTime now)
    {
        var stateChanged = false;
        foreach (var character in _maze.PartyMembers.Select(member => member.Character).Distinct()
                     .Where(IsAutonomousNpc))
        {
            stateChanged |= ProcessNpcComplaint(character, NpcComplaintKind.Hunger, character.FoodLevel == 0, now);
            stateChanged |= ProcessNpcComplaint(character, NpcComplaintKind.Thirst, character.WaterLevel == 0, now);
            stateChanged |= ProcessNpcComplaint(character, NpcComplaintKind.Injured,
                character.CurrentVitality * 2 < character.MaximumVitality && !HasHealingPotion(character), now);
        }
        return stateChanged;
    }

    private bool ProcessNpcSelfCare(DateTime now)
    {
        var stateChanged = false;
        foreach (var character in _maze.PartyMembers.Select(member => member.Character).Distinct()
                     .Where(IsAutonomousNpc))
        {
            if (character.IsAlive && character.CurrentVitality < character.MaximumVitality)
                stateChanged |= TryNpcConsumeHealingPotions(character);
            if (character.CurrentVitality * 2 >= character.MaximumVitality || HasHealingPotion(character))
                ClearNpcShortage(character, NpcComplaintKind.Injured);
        }
        return ProcessNpcComplaints(now) || stateChanged;
    }

    private bool ProcessNpcComplaint(LiveCharacter character, NpcComplaintKind kind, bool active, DateTime now)
    {
        var key = (character.Id, kind);
        if (!active)
        {
            _nextNpcComplaints.Remove(key);
            return false;
        }
        if (!_nextNpcComplaints.TryGetValue(key, out var next))
        {
            ScheduleNpcComplaint(character, kind, now);
            return false;
        }
        if (now < next) return false;
        LogScheduledPartyComment(character, kind);
        ScheduleNpcComplaint(character, kind, now);
        return true;
    }

    private void ScheduleNpcComplaint(LiveCharacter character, NpcComplaintKind kind, DateTime from) =>
        _nextNpcComplaints[(character.Id, kind)] = from + TimeSpan.FromSeconds(_random.Next(120, 181));

    private void RegisterNpcShortage(LiveCharacter character, NpcComplaintKind kind)
    {
        if (!_reportedNpcShortages.Add((character.Id, kind))) return;
        ScheduleNpcComplaint(character, kind, DateTime.UtcNow);
    }

    private void ClearNpcShortage(LiveCharacter character, NpcComplaintKind kind)
    {
        _reportedNpcShortages.Remove((character.Id, kind));
        _nextNpcComplaints.Remove((character.Id, kind));
    }

    private void LogNpcAutomation(LiveCharacter character, string message, ConsoleColor color)
    {
        _renderer.DrawInventoryMessage(message, color);
        RecordSessionActivity(SessionActivityKind.System, message, color);
    }

    private void TryLogPartyComments(string situationId)
    {
        foreach (var selection in PartyCommentarySelector.Select(_gameData, situationId,
                     CharacterRoster.Party.Members, _random))
            LogPartyComment(selection.Speaker, selection.Remark.Text);
        var follower = _maze.PartyMembers.FirstOrDefault(member => member.IsTemporaryFollower &&
            member.Character.IsAlive && _gameData.GetTemporaryFollowerRemarks(situationId, member.Character).Count > 0);
        if (follower is null || !PartyCommentarySelector.ShouldComment(_random.Next(100))) return;
        var remarks = _gameData.GetTemporaryFollowerRemarks(situationId, follower.Character);
        LogPartyComment(follower.Character, remarks[_random.Next(remarks.Count)].Text);
    }

    private void LogScheduledPartyComment(LiveCharacter character, NpcComplaintKind kind)
    {
        var situationId = kind switch
        {
            NpcComplaintKind.Hunger => PartySituationIds.Hungry,
            NpcComplaintKind.Thirst => PartySituationIds.Thirsty,
            _ => PartySituationIds.Injured
        };
        PartyCommentSelection? selection;
        if (_maze.PartyMembers.Any(member => member.IsTemporaryFollower && member.Character == character) &&
            _gameData.GetTemporaryFollowerRemarks(situationId, character) is { Count: > 0 } followerRemarks)
            selection = new PartyCommentSelection(character, followerRemarks[_random.Next(followerRemarks.Count)]);
        else selection = PartyCommentarySelector.SelectFor(_gameData, situationId, character, _random);
        if (selection is null) return;
        var level = kind switch
        {
            NpcComplaintKind.Hunger => character.FoodLevel.ToString(),
            NpcComplaintKind.Thirst => character.WaterLevel.ToString(),
            _ => $"{character.CurrentVitality}/{character.MaximumVitality}"
        };
        LogPartyComment(character, selection.Remark.Text, level);
    }

    private void LogPartyComment(LiveCharacter speaker, string comment, string? level = null) =>
        _sessionEventService.LogPartyComment(speaker, comment, level);

    private void PresentBattleEntries(IEnumerable<BattleLogEntry> entries)
    {
        var sourceEntries = entries.ToArray();
        var materialized = sourceEntries.SelectMany(entry =>
            new[] { entry }.Concat((entry.FollowUps ?? []).Select(notice =>
                new BattleLogEntry(notice.Message, notice.Kind)))).ToArray();
        if (_locationId == DeveloperBattleTestLocationId && _activeTeamBattle is { } loggedBattle)
            _developerBattleLog.AppendBattleEntries(loggedBattle, materialized);
        if (_activeTeamBattle is not null && !_isQuickTeamBattle)
        {
            foreach (var entry in sourceEntries)
            {
                _lastBattleActionDetails = entry.Details ?? new BattleActionDetails(Guid.NewGuid(),
                    _activeTeamBattle.CurrentCharacter?.Name ?? _activeTeamBattle.CurrentEnemy?.Name ?? "Akció",
                    "", [], [entry.Message]);
                _renderer.CharacterSheet.DrawBattleDetails(_lastBattleActionDetails);
            }
        }
        _sessionEventService.PresentBattleEntries(
            materialized,
            _isQuickTeamBattle,
            entry => _renderer.DrawBattleRound(entry),
            _ => _renderer.CharacterSheet.RefreshBattleStatusRows(),
            PartyLeader.Id,
            _ => _quickBattleSuppressedEntryCount++);
    }

    private void RecordSessionActivity(SessionActivityKind kind, string message, ConsoleColor color,
        IReadOnlyCollection<CharacterId>? listeners = null)
    {
        _sessionEventService.RecordSessionActivity(kind, message, color, listeners);
        if (_locationId == DeveloperBattleTestLocationId && kind == SessionActivityKind.Battle)
            _developerBattleLog.Append("BATTLE-ACTIVITY", message);
    }

    private void PlayCharacterStepSound(LiveCharacter character) =>
        _sessionEventService.PlayCharacterStepSound(character, PartyLeader.Id);

    private void PlayBattleVictorySound() =>
        _sessionEventService.PlayBattleVictorySound(PartyLeader.Id);

    private void PlaySessionSound(SoundEffect effect, IReadOnlyCollection<CharacterId>? listeners = null) =>
        _sessionEventService.PlaySessionSound(effect, listeners, PartyLeader.Id);

    private void ApplyAudioSettings()
    {
        _backgroundMusic.ApplySettings();
        _soundEffects.ApplySettings();
    }

    private void RecordSessionSound(SoundEffect effect, IReadOnlyList<CharacterId>? listenerCharacterIds) =>
        _sessionEventService.RecordSessionSound(effect, listenerCharacterIds);

    private static ConsoleColor BattleEntryColor(BattleLogKind kind) => SessionEventService.BattleEntryColor(kind);
}
