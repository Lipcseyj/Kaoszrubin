using KaoszRubin.Combat;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Application;

public sealed class SessionEventService
{
    private readonly ConsoleRenderer _renderer;
    private readonly SoundEffects _soundEffects;
    private readonly Random _random;
    private readonly Queue<SessionActivitySnapshot> _sessionActivities = new();
    private readonly Queue<SessionSoundSnapshot> _sessionSounds = new();
    private long _sessionActivitySequence;
    private long _sessionSoundSequence;

    public SessionEventService(ConsoleRenderer renderer, SoundEffects soundEffects, Random random)
    {
        _renderer = renderer;
        _soundEffects = soundEffects;
        _random = random;
    }

    public IReadOnlyList<SessionActivitySnapshot> Activities => _sessionActivities.ToArray();
    public IReadOnlyList<SessionSoundSnapshot> Sounds => _sessionSounds.ToArray();

    public void LogPartyComment(LiveCharacter speaker, string comment, string? level = null)
    {
        var message = PartyCommentarySelector.Format(speaker, comment, level);
        _renderer.DrawInventoryMessage(message, speaker.Color);
        RecordSessionActivity(SessionActivityKind.System, message, speaker.Color);
    }

    public void PlayBattleRoundSound(BattleLogEntry entry, BattleState? activeBattleState, CharacterId selectedCharacterId)
    {
        if (entry.Kind is not (BattleLogKind.PlayerAttack or BattleLogKind.EnemyAttack or BattleLogKind.CriticalHit)) return;
        var battle = activeBattleState;
        var listeners = battle is not null
            ? new[] { battle.PlayerCharacterId, selectedCharacterId }.Distinct().ToArray()
            : [selectedCharacterId];
        var missed = entry.Message.Contains("💨", StringComparison.Ordinal);
        var enemyHitPlayer = !missed && battle is not null &&
            entry.Message.Contains($"{battle.Enemy.Name} → {battle.Player.Name}", StringComparison.Ordinal);
        if (enemyHitPlayer && battle is not null)
            PlaySessionSound(SoundEffect.PlayerGotHit, [battle.PlayerCharacterId], selectedCharacterId);
        else
            PlaySessionSound(missed ? SoundEffect.Miss : SoundEffect.Hit, listeners, selectedCharacterId);
    }

    public void PresentBattleEntries(IEnumerable<BattleLogEntry> entries, bool isQuickTeamBattle,
        Action<BattleLogEntry> drawBattleRound, Action<BattleLogEntry> refreshBattleStatus,
        BattleState? activeBattleState, CharacterId selectedCharacterId, Action<int>? incrementQuickBattleSuppressedEntryCount = null)
    {
        foreach (var entry in entries)
        {
            if (isQuickTeamBattle)
            {
                incrementQuickBattleSuppressedEntryCount?.Invoke(1);
                RecordSessionActivity(SessionActivityKind.Battle, entry.Message, BattleEntryColor(entry.Kind));
                continue;
            }
            drawBattleRound(entry);
            RecordSessionActivity(SessionActivityKind.Battle, entry.Message, BattleEntryColor(entry.Kind));
            PlayBattleRoundSound(entry, activeBattleState, selectedCharacterId);
            refreshBattleStatus(entry);
        }
    }

    public void RecordSessionActivity(SessionActivityKind kind, string message, ConsoleColor color,
        IReadOnlyCollection<CharacterId>? listeners = null)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        _sessionActivities.Enqueue(new SessionActivitySnapshot(++_sessionActivitySequence, kind, message, color,
            listeners?.Distinct().ToArray()));
        while (_sessionActivities.Count > 24) _sessionActivities.Dequeue();
    }

    public void PlayCharacterStepSound(LiveCharacter character, CharacterId selectedCharacterId)
    {
        switch (_random.Next(20))
        {
            case 0: PlaySessionSound(SoundEffect.Step1, [character.Id], selectedCharacterId); break;
            case 1: PlaySessionSound(SoundEffect.Step2, [character.Id], selectedCharacterId); break;
            case 2: PlaySessionSound(SoundEffect.Step3, [character.Id], selectedCharacterId); break;
            case 3: PlaySessionSound(SoundEffect.Step4, [character.Id], selectedCharacterId); break;
            case 4: PlaySessionSound(SoundEffect.Step5, [character.Id], selectedCharacterId); break;
        }
    }

    public void PlayBattleVictorySound(CharacterId selectedCharacterId) =>
        PlaySessionSound(_random.Next(2) == 0 ? SoundEffect.Victory : SoundEffect.Victory2, selectedCharacterId: selectedCharacterId);

    public void PlaySessionSound(SoundEffect effect, IReadOnlyCollection<CharacterId>? listeners = null,
        CharacterId? selectedCharacterId = null)
    {
        var listenerIds = listeners?.Distinct().ToArray();
        RecordSessionSound(effect, listenerIds);
        if (selectedCharacterId is not null && listenerIds is not null && !listenerIds.Contains(selectedCharacterId.Value)) return;
        _soundEffects.Play(effect);
    }

    public void RecordSessionSound(SoundEffect effect, IReadOnlyList<CharacterId>? listenerCharacterIds)
    {
        _sessionSounds.Enqueue(new SessionSoundSnapshot(++_sessionSoundSequence, effect, listenerCharacterIds));
        while (_sessionSounds.Count > 48) _sessionSounds.Dequeue();
    }

    public static ConsoleColor BattleEntryColor(BattleLogKind kind) => kind switch
    {
        BattleLogKind.PlayerAttack => ConsoleColor.Green,
        BattleLogKind.EnemyAttack => ConsoleColor.Red,
        BattleLogKind.CriticalHit => ConsoleColor.Yellow,
        _ => ConsoleColor.Gray
    };
}
