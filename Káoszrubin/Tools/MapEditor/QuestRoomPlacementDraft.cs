using KaoszRubin.World;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KaoszRubin.MapEditor;

/// <summary>Csak literális szobacélokat olvas, C# kód futtatása nélkül.</summary>
internal static class QuestRoomPlacementDraft
{
    public static QuestRoomPlacementConfiguration Parse(string expression)
    {
        var syntax = SyntaxFactory.ParseExpression(expression);
        var arguments = syntax switch
        {
            ImplicitObjectCreationExpressionSyntax { Initializer: null } creation => creation.ArgumentList.Arguments,
            ObjectCreationExpressionSyntax { Initializer: null, ArgumentList: { } list } creation
                when creation.Type.ToString() == nameof(QuestRoomPlacementConfiguration) => list.Arguments,
            _ => throw new FormatException("A szobacél new(ScreenNumber: 2) vagy new(AreaId: \"AREA_ID\") alakú legyen.")
        };
        if (syntax.ContainsDiagnostics || arguments.Count > 2)
            throw new FormatException("Hibás küldetésszoba-elhelyezési kifejezés.");
        int? screen = null;
        string? areaId = null;
        var used = new HashSet<string>();
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            var name = argument.NameColon?.Name.Identifier.ValueText ??
                (index == 0 ? "ScreenNumber" : "AreaId");
            if (!used.Add(name)) throw new FormatException("Ismétlődő szobacél-paraméter.");
            switch (name, argument.Expression)
            {
                case ("ScreenNumber", LiteralExpressionSyntax literal) when literal.Token.Value is int value:
                    screen = value;
                    break;
                case ("AreaId", LiteralExpressionSyntax literal) when literal.IsKind(SyntaxKind.StringLiteralExpression):
                    areaId = literal.Token.ValueText;
                    break;
                case ("ScreenNumber" or "AreaId", LiteralExpressionSyntax literal)
                    when literal.IsKind(SyntaxKind.NullLiteralExpression):
                    break;
                default:
                    throw new FormatException("A képernyőszám egész szám, az AreaId idézőjeles szöveg lehet.");
            }
        }
        var result = new QuestRoomPlacementConfiguration(screen, areaId);
        Validate(result);
        return result;
    }

    public static string Expression(QuestRoomPlacementConfiguration placement)
    {
        Validate(placement);
        var arguments = new List<string>();
        if (placement.ScreenNumber is { } screen) arguments.Add($"ScreenNumber: {screen}");
        if (placement.AreaId is { } areaId)
            arguments.Add("AreaId: " + SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(areaId)).ToFullString());
        return "new(" + string.Join(", ", arguments) + ")";
    }

    private static void Validate(QuestRoomPlacementConfiguration placement)
    {
        if (placement.ScreenNumber is <= 0 || placement.AreaId is not null && string.IsNullOrWhiteSpace(placement.AreaId))
            throw new FormatException("A képernyőszám legalább 1, az AreaId nem üres szöveg legyen.");
    }
}
