namespace KaoszRubin.Domain.Characters;

public static class CharacterColors
{
    public static IReadOnlyList<ConsoleColor> Selectable { get; } =
    [
        ConsoleColor.Cyan, ConsoleColor.Green, ConsoleColor.Yellow, ConsoleColor.Magenta,
        ConsoleColor.Red, ConsoleColor.Blue, ConsoleColor.White, ConsoleColor.DarkCyan,
        ConsoleColor.DarkGreen, ConsoleColor.DarkYellow, ConsoleColor.DarkMagenta, ConsoleColor.DarkRed
    ];
    public static IReadOnlyList<ConsoleColor> WorldNpcSelectable { get; } = Selectable
        .Where(color => color != ConsoleColor.White).ToArray();

    public static string NameOf(ConsoleColor color) => color switch
    {
        ConsoleColor.Cyan => "cián", ConsoleColor.Green => "zöld", ConsoleColor.Yellow => "sárga",
        ConsoleColor.Magenta => "magenta", ConsoleColor.Red => "piros", ConsoleColor.Blue => "kék",
        ConsoleColor.White => "fehér", ConsoleColor.DarkCyan => "sötétcián", ConsoleColor.DarkGreen => "sötétzöld",
        ConsoleColor.DarkYellow => "arany", ConsoleColor.DarkMagenta => "lila", ConsoleColor.DarkRed => "bordó",
        _ => color.ToString()
    };
}
