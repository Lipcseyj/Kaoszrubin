using System.Text;
using System.ComponentModel;
using System.Globalization;

namespace KaoszRubin.World;

/// <summary>
/// Egy térképrúna megjelenítési és alapvető járhatósági tulajdonságai. A stílus pályánként regisztrált,
/// ezért ugyanaz a tereptípus különböző pályákon más színt kaphat.
/// </summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed record MazeTerrainStyle(
    string Id,
    [property: TypeConverter(typeof(RuneTypeConverter))] Rune Rune,
    ConsoleColor ForegroundColor,
    ConsoleColor BackgroundColor,
    bool Walkable,
    bool BlocksSight);

public sealed class RuneTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string);
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(string);
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        var text = value.ToString();
        if (string.IsNullOrEmpty(text)) throw new FormatException("Egy Unicode karakter szükséges.");
        return Rune.GetRuneAt(text, 0);
    }
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value,
        Type destinationType) => destinationType == typeof(string) && value is Rune rune
        ? rune.ToString() : base.ConvertTo(context, culture, value, destinationType);
}
