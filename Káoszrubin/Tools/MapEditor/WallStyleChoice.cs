using KaoszRubin.World;

namespace KaoszRubin.MapEditor;

/// <summary>Csak ismert katalógushivatkozásokat értelmez; egyedi C# kifejezést változatlanul megőriz.</summary>
internal sealed record WallStyleChoice(string Expression, DungeonWallStyle? Style)
{
    public static IReadOnlyList<WallStyleChoice> Choices { get; } =
    [
        new("null", null),
        .. DungeonWallStyles.All.OrderBy(style => style.Name, StringComparer.CurrentCulture).Select(style => new WallStyleChoice($"DungeonWallStyles.{style.Id}", style))
    ];

    public static WallStyleChoice FromExpression(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression) || expression.Trim() == "null")
            return Choices[0];
        var trimmed = expression.Trim();
        return Choices.FirstOrDefault(choice => choice.Expression == trimmed) ?? new(trimmed, null);
    }

    public override string ToString() => Style?.ToString() ??
        (Expression == "null" ? "Egyedi fal / erdei terep" : $"Egyedi kifejezés: {Expression}");
}
