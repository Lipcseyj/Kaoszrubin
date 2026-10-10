using System.Text.Json.Serialization;
using KaoszRubin.Combat;

namespace KaoszRubin.Application;

/// <summary>A kampány naptára. Az első nap reggel nyolckor kezdődik.</summary>
public sealed record GameTimeSnapshot(long TotalMinutes = 8 * 60,
    long? LastInnRestCompletedMinutes = null, long? LastForestFeastDay = null)
{
    [JsonIgnore] public long Day => TotalMinutes / (24 * 60) + 1;
    [JsonIgnore] public int Hour => (int)(TotalMinutes / 60 % 24);
    [JsonIgnore] public int Minute => (int)(TotalMinutes % 60);
    [JsonIgnore] public bool IsDaytime => Hour >= 6 && Hour < 18;
    [JsonIgnore] public string DayNightIcon => IsDaytime ? "☀️" : "🌙";
    [JsonIgnore] public string DisplayText => $"{Day}.nap {Hour:00}:{Minute:00} {DayNightIcon}";
}

/// <summary>A host által vezetett játékidő; valódi faliórát és felhasználói várakozást nem számol.</summary>
public sealed class GameTimeClock
{
    public const int MinutesPerRound = 1;
    public const int RestHours = 8;
    private long _totalMinutes = 8 * 60;
    private BattleId? _battleId;
    private int _countedBattleRounds;
    private Guid? _lastRestId;
    private long? _lastInnRestCompletedMinutes;
    private long? _lastForestFeastDay;

    public GameTimeSnapshot Snapshot => new(_totalMinutes, _lastInnRestCompletedMinutes, _lastForestFeastDay);

    public void Restore(GameTimeSnapshot? snapshot)
    {
        _totalMinutes = Math.Max(0, snapshot?.TotalMinutes ?? 8 * 60);
        _lastInnRestCompletedMinutes = snapshot?.LastInnRestCompletedMinutes;
        _lastForestFeastDay = snapshot?.LastForestFeastDay;
        _battleId = null;
        _countedBattleRounds = 0;
        _lastRestId = null;
    }

    public void AdvanceExplorationRound() => AdvanceMinutes(MinutesPerRound);

    /// <summary>Egy harci kör a résztvevők számától és a döntések idejétől függetlenül egyszer számít.</summary>
    public void AccountBattleRounds(BattleId battleId, int currentRound)
    {
        if (currentRound < 1) throw new ArgumentOutOfRangeException(nameof(currentRound));
        if (_battleId != battleId)
        {
            _battleId = battleId;
            _countedBattleRounds = 0;
        }
        if (currentRound <= _countedBattleRounds) return;
        AdvanceMinutes((long)(currentRound - _countedBattleRounds) * MinutesPerRound);
        _countedBattleRounds = currentRound;
    }

    public void AdvanceRest(Guid restId, bool atInn = false)
    {
        if (_lastRestId == restId) return;
        AdvanceMinutes(RestHours * 60);
        _lastRestId = restId;
        if (atInn) _lastInnRestCompletedMinutes = _totalMinutes;
    }

    public void AdvanceFeast(bool atForestInn)
    {
        var feastDay = Snapshot.Day;
        AdvanceMinutes(60);
        if (atForestInn) _lastForestFeastDay = feastDay;
    }

    private void AdvanceMinutes(long minutes) => _totalMinutes = checked(_totalMinutes + minutes);
}
