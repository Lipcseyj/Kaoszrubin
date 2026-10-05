using System.Reflection;
using KaoszRubin.Domain.Combat;
using KaoszRubin.World;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KaoszRubin.MapEditor;

/// <summary>Csak az ismert deklaratív encounter-kifejezéseket olvassa; forráskódot nem futtat.</summary>
internal sealed class EncounterDraft
{
    public const string Custom = "Egyedi csoport";
    public static readonly IReadOnlyDictionary<string, MethodInfo> Factories = typeof(Encounters)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Where(method => method.ReturnType == typeof(EnemyEncounterConfiguration))
        .ToDictionary(method => method.Name);
    public static readonly IReadOnlyDictionary<string, string> Monsters = typeof(MonsterIds)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .ToDictionary(field => field.Name, field => (string)field.GetRawConstantValue()!);
    private static readonly ConstructorInfo Constructor = typeof(EnemyEncounterConfiguration).GetConstructors()
        .Single(constructor => constructor.GetParameters().Length == 10);

    public string Kind { get; }
    public Dictionary<string, object?> Arguments { get; } = [];
    public Dictionary<string, object?> Overrides { get; } = [];
    public ParameterInfo[] Parameters => Kind == Custom ? Constructor.GetParameters() : Factories[Kind].GetParameters();

    public EncounterDraft(string kind)
    {
        if (kind != Custom && !Factories.ContainsKey(kind)) throw new FormatException("Ismeretlen találkozástípus.");
        Kind = kind;
        foreach (var parameter in Parameters)
            Arguments[parameter.Name!] = parameter.HasDefaultValue ? DefaultFor(parameter) :
                parameter.ParameterType == typeof(string) ? MonsterIds.Goblin :
                parameter.ParameterType == typeof(Amount) ? Amount.Few :
                parameter.ParameterType == typeof(IntRange) ? new IntRange(1, 2) :
                parameter.ParameterType == typeof(IReadOnlyList<EnemyGroupMemberConfiguration>)
                    ? new EnemyGroupMemberConfiguration[] { new(MonsterIds.Goblin, new(1, 2)) } :
                Activator.CreateInstance(parameter.ParameterType);
    }

    private static object? DefaultFor(ParameterInfo parameter)
    {
        var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
        return parameter.DefaultValue is { } value && type.IsEnum ? Enum.ToObject(type, value) : parameter.DefaultValue;
    }

    public EnemyEncounterConfiguration Configuration()
    {
        var args = Parameters.Select(parameter => Arguments[parameter.Name!]).ToArray();
        var configuration = (EnemyEncounterConfiguration)(Kind == Custom
            ? Constructor.Invoke(args) : Factories[Kind].Invoke(null, args))!;
        foreach (var (name, value) in Overrides)
            typeof(EnemyEncounterConfiguration).GetProperty(name)!.SetValue(configuration, value);
        return configuration;
    }

    public string Expression()
    {
        var args = Parameters.Where(p => !p.HasDefaultValue || !Equals(Arguments[p.Name!], DefaultFor(p)))
            .Select(p => p.Name + ": " + FormatValue(Arguments[p.Name!], p.ParameterType,
                p.ParameterType == typeof(string) && p.Name != nameof(EnemyEncounterConfiguration.AreaId)));
        var expression = (Kind == Custom ? "new EnemyEncounterConfiguration" : "Encounters." + Kind) +
                         "(" + string.Join(", ", args) + ")";
        if (Overrides.Count > 0)
            expression += " with { " + string.Join(", ", Overrides.Select(entry => entry.Key + " = " +
                FormatValue(entry.Value, typeof(EnemyEncounterConfiguration).GetProperty(entry.Key)!.PropertyType))) + " }";
        return expression;
    }

    public static EncounterDraft Parse(string text)
    {
        var syntax = SyntaxFactory.ParseExpression(text);
        if (syntax.ContainsDiagnostics) throw new FormatException("Hibás C# találkozáskifejezés.");
        return Read(syntax);
    }

    private static EncounterDraft Read(ExpressionSyntax syntax)
    {
        if (syntax is ParenthesizedExpressionSyntax parenthesized) return Read(parenthesized.Expression);
        if (syntax is WithExpressionSyntax with)
        {
            var result = Read(with.Expression);
            foreach (var item in with.Initializer.Expressions)
            {
                if (item is not AssignmentExpressionSyntax assignment || !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) ||
                    assignment.Left is not IdentifierNameSyntax name)
                    throw new FormatException("Nem támogatott találkozás-felülírás.");
                var property = typeof(EnemyEncounterConfiguration).GetProperty(name.Identifier.ValueText)
                    ?? throw new FormatException("Ismeretlen találkozásmező: " + name);
                result.Overrides[property.Name] = ReadValue(assignment.Right, property.PropertyType);
            }
            return result;
        }
        if (syntax is InvocationExpressionSyntax call && call.Expression is MemberAccessExpressionSyntax member &&
            member.Expression.ToString() == nameof(Encounters) && Factories.ContainsKey(member.Name.ToString()))
        {
            var result = new EncounterDraft(member.Name.ToString());
            ReadArguments(call.ArgumentList, result.Parameters, result.Arguments);
            return result;
        }
        var arguments = CreationArguments(syntax, nameof(EnemyEncounterConfiguration));
        var custom = new EncounterDraft(Custom);
        ReadArguments(arguments, custom.Parameters, custom.Arguments);
        return custom;
    }

    private static void ReadArguments(ArgumentListSyntax arguments, ParameterInfo[] parameters,
        Dictionary<string, object?> values)
    {
        var seen = new HashSet<string>();
        for (var i = 0; i < arguments.Arguments.Count; i++)
        {
            var argument = arguments.Arguments[i];
            var parameter = argument.NameColon is { } named
                ? parameters.SingleOrDefault(p => p.Name == named.Name.Identifier.ValueText)
                : parameters.ElementAtOrDefault(i);
            if (parameter is null || !seen.Add(parameter.Name!) || argument.RefKindKeyword.RawKind != 0)
                throw new FormatException("Ismeretlen vagy ismételt találkozásparaméter.");
            values[parameter.Name!] = ReadValue(argument.Expression, parameter.ParameterType);
        }
        if (parameters.Any(p => !p.HasDefaultValue && !seen.Contains(p.Name!)))
            throw new FormatException("Hiányzó kötelező találkozásparaméter.");
    }

    private static ArgumentListSyntax CreationArguments(ExpressionSyntax syntax, string type) => syntax switch
    {
        ImplicitObjectCreationExpressionSyntax creation when creation.Initializer is null => creation.ArgumentList,
        ObjectCreationExpressionSyntax creation when creation.Type.ToString() == type && creation.Initializer is null =>
            creation.ArgumentList ?? throw new FormatException("Hiányzó konstruktorparaméterek."),
        _ => throw new FormatException("Ez a kifejezés nem alakítható át veszteségmentesen űrlappá: " + syntax)
    };

    private static object? ReadValue(ExpressionSyntax expression, Type type)
    {
        if (expression is ParenthesizedExpressionSyntax parenthesized) return ReadValue(parenthesized.Expression, type);
        if (expression.IsKind(SyntaxKind.NullLiteralExpression) && (!type.IsValueType || Nullable.GetUnderlyingType(type) is not null))
            return null;
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string))
        {
            if (expression is LiteralExpressionSyntax literal && literal.Token.Value is string text) return text;
            if (expression is MemberAccessExpressionSyntax id && id.Expression.ToString() == nameof(MonsterIds) &&
                Monsters.TryGetValue(id.Name.ToString(), out var monster)) return monster;
        }
        if (type == typeof(int))
        {
            if (expression is LiteralExpressionSyntax integer && integer.Token.Value is int number) return number;
            if (int.TryParse(expression.ToString(), out var signed)) return signed;
        }
        if (type.IsEnum)
        {
            if (expression is BinaryExpressionSyntax flags && flags.IsKind(SyntaxKind.BitwiseOrExpression) &&
                type.IsDefined(typeof(FlagsAttribute), false))
                return Enum.ToObject(type, Convert.ToInt64(ReadValue(flags.Left, type)) | Convert.ToInt64(ReadValue(flags.Right, type)));
            if (expression is MemberAccessExpressionSyntax member && member.Expression.ToString() == type.Name &&
                Enum.TryParse(type, member.Name.ToString(), out var value) && Enum.IsDefined(type, value!)) return value;
        }
        if (type == typeof(IntRange))
        {
            if (expression is InvocationExpressionSyntax invocation && invocation.ArgumentList.Arguments.Count == 0 &&
                invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Range" } range)
                return ((Amount)ReadValue(range.Expression, typeof(Amount))!).Range();
            var args = CreationArguments(expression, nameof(IntRange));
            var values = new Dictionary<string, object?>();
            ReadArguments(args, typeof(IntRange).GetConstructors().Single(c => c.GetParameters().Length == 2).GetParameters(), values);
            return new IntRange((int)values["Minimum"]!, (int)values["Maximum"]!);
        }
        if (type == typeof(IReadOnlyList<EnemyGroupMemberConfiguration>) && expression is CollectionExpressionSyntax collection)
        {
            var constructor = typeof(EnemyGroupMemberConfiguration).GetConstructors().Single(c => c.GetParameters().Length == 3);
            return collection.Elements.Select(element =>
            {
                if (element is not ExpressionElementSyntax item) throw new FormatException("A csoporttag-lista nem tartalmazhat kiterjesztést.");
                var values = new Dictionary<string, object?> { ["Role"] = EnemyGroupRole.Member };
                ReadArguments(CreationArguments(item.Expression, nameof(EnemyGroupMemberConfiguration)), constructor.GetParameters(), values);
                return new EnemyGroupMemberConfiguration((string)values["EnemyId"]!, (IntRange)values["Count"]!, (EnemyGroupRole)values["Role"]!);
            }).ToArray();
        }
        throw new FormatException("Nem támogatott érték: " + expression);
    }

    private static string FormatValue(object? value, Type type, bool monster = false)
    {
        if (value is null) return "null";
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (value is string text)
        {
            var field = monster ? Monsters.FirstOrDefault(entry => entry.Value == text).Key : null;
            return field is null ? Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(text, true) : "MonsterIds." + field;
        }
        if (value is IntRange range) return $"new IntRange({range.Minimum}, {range.Maximum})";
        if (value is IReadOnlyList<EnemyGroupMemberConfiguration> members)
            return "[" + string.Join(", ", members.Select(member => "new(" + FormatValue(member.EnemyId, typeof(string), true) +
                ", " + FormatValue(member.Count, typeof(IntRange)) + ", EnemyGroupRole." + member.Role + ")")) + "]";
        if (type.IsEnum) return string.Join(" | ", value.ToString()!.Split(", ").Select(name => type.Name + "." + name));
        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!;
    }
}

internal sealed record EncounterEditorContext(bool Forest, bool RoomList, int GuaranteedScreens,
    IReadOnlyList<string> AreaIds, IReadOnlyList<RoomKind> RoomKinds, IReadOnlySet<string> MonsterIds,
    IReadOnlyDictionary<string, IReadOnlyList<RoomKind>>? AreaRoomKinds = null)
{
    public IReadOnlyList<RoomKind> KindsFor(object? target) => !RoomList ? [] :
        target is string area && AreaRoomKinds?.TryGetValue(area, out var kinds) == true ? kinds :
        target is int screen && screen > 0 && screen <= AreaIds.Count &&
        AreaRoomKinds?.TryGetValue(AreaIds[screen - 1], out var screenKinds) == true ? screenKinds : RoomKinds;

    public void Validate(EnemyEncounterConfiguration configuration)
    {
        if (configuration.GroupCount is null || configuration.Members is null ||
            configuration.GroupCount.Minimum < 0 || configuration.GroupCount.Maximum < configuration.GroupCount.Minimum ||
            configuration.GroupCount.Maximum == int.MaxValue || configuration.Members.Count == 0 ||
            configuration.Members.Any(member => member.Count is null || !MonsterIds.Contains(member.EnemyId) || member.Count.Minimum < 1 ||
                member.Count.Maximum < member.Count.Minimum || member.Count.Maximum == int.MaxValue))
            throw new InvalidDataException("Válassz létező ellenfelet és érvényes darabszámtartományokat.");
        if (configuration.ScreenNumber is { } screen && (screen < 1 || screen > GuaranteedScreens))
            throw new InvalidDataException($"Biztosan létező képernyők: 1–{GuaranteedScreens}.");
        if (configuration.AreaId is { } area && !AreaIds.Contains(area))
            throw new InvalidDataException("A célterület nem létezik az aktuális gráfban: " + area);
        if (configuration.AreaId is not null && configuration.ScreenNumber is not null)
            throw new InvalidDataException("Területazonosító és képernyőszám közül egyet válassz.");
        if (configuration.TargetRoomKind is { } kind && !KindsFor((object?)configuration.AreaId ?? configuration.ScreenNumber).Contains(kind))
            throw new InvalidDataException("Ez a szobatípus ezen a helyen nem választható.");
        if (configuration.TargetRoomKind is not null && configuration.TargetTerrainTags != TerrainTag.None)
            throw new InvalidDataException("A terepi elhelyezés nem szobatípus szerint történik; válassz bármely szobatípust vagy töröld a terepi célzást.");
        if (!Forest && (configuration.TargetTerrainTags != TerrainTag.None || configuration.Posture == EnemyEncounterPosture.Ambush))
            throw new InvalidDataException("Terepi rajtaütéshez erdős pálya szükséges.");
        if (configuration.Posture == EnemyEncounterPosture.Ambush &&
            (configuration.TargetTerrainTags == TerrainTag.None || configuration.TriggerDistance < 1))
            throw new InvalidDataException("A rajtaütéshez válassz terepet és pozitív aktiválási távolságot.");
    }
}
