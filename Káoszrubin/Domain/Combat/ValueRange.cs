namespace KaoszRubin.Domain.Combat;

/// <summary>CSV-ben például <c>3-8</c> alakban megadott zárt értéktartomány.</summary>
public sealed record ValueRange(int Minimum, int Maximum)
{
    public override string ToString() => $"{Minimum}-{Maximum}";
}
