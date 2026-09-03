namespace KaoszRubin.Application;

public sealed record PartyCommandState(
    bool HoldingPosition,
    bool Regrouping,
    bool AttackMode,
    DateTime? ScatterUntil);

public sealed class PartyCommandController
{
    private readonly Random _random;

    public PartyCommandController(Random random)
    {
        _random = random;
    }

    public PartyCommandState ToggleHoldPosition(PartyCommandState state)
    {
        var holding = !state.HoldingPosition;
        return state with
        {
            HoldingPosition = holding,
            Regrouping = holding ? false : state.Regrouping,
            AttackMode = holding ? false : state.AttackMode,
            ScatterUntil = holding ? null : state.ScatterUntil
        };
    }

    public PartyCommandState ToggleRegrouping(PartyCommandState state)
    {
        var regrouping = !state.Regrouping;
        return state with
        {
            HoldingPosition = regrouping ? false : state.HoldingPosition,
            Regrouping = regrouping,
            AttackMode = regrouping ? false : state.AttackMode,
            ScatterUntil = regrouping ? null : state.ScatterUntil
        };
    }

    public PartyCommandState ToggleAttackMode(PartyCommandState state)
    {
        var attackMode = !state.AttackMode;
        return state with
        {
            HoldingPosition = attackMode ? false : state.HoldingPosition,
            Regrouping = attackMode ? false : state.Regrouping,
            AttackMode = attackMode,
            ScatterUntil = attackMode ? null : state.ScatterUntil
        };
    }

    public PartyCommandState ScatterTemporarily(PartyCommandState state, DateTime now)
    {
        return state with
        {
            HoldingPosition = false,
            Regrouping = false,
            AttackMode = false,
            ScatterUntil = now + TimeSpan.FromSeconds(10)
        };
    }

    public PartyCommandState ClearScatterWindow(PartyCommandState state, DateTime now)
    {
        if (state.ScatterUntil is not { } scatterUntil || now < scatterUntil) return state;
        return state with { ScatterUntil = null };
    }

    public PartyCommandState RefreshPartyMovement(PartyCommandState state, DateTime now)
    {
        var scatterUntil = state.ScatterUntil;
        if (scatterUntil is not null && now >= scatterUntil)
            return state with { ScatterUntil = null };
        return state;
    }

    public PartyCommandState ApplyPartyMovementDelay(PartyCommandState state)
    {
        return state with
        {
            ScatterUntil = state.ScatterUntil is null
                ? null
                : state.ScatterUntil + TimeSpan.FromMilliseconds(_random.Next(0, 100))
        };
    }
}
