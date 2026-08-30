namespace KaoszRubin.Domain;

/// <summary>A kulcs nélküli, kulcsra zárt ajtónyitási próbák szükségletköltsége.</summary>
public sealed record DoorAttemptRules(int FoodMinimum, int FoodMaximum, int WaterMinimum, int WaterMaximum);
