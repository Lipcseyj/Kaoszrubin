using KaoszRubin.Data;

namespace KaoszRubin.UI;

internal sealed record MainMenuCreature(CreatureQuoteDefinition Quotes, string Name,
    AsciiPortrait Portrait, ConsoleColor Color);

internal static class MainMenuCreaturePanel
{
    public const int Top = 29;
    public const int Height = 13;

    public static MainMenuCreature? ChooseCreature(GameDataCatalog gameData, Random random)
    {
        if (gameData.CreatureQuotes.Count == 0) return null;
        var quote = gameData.CreatureQuotes[random.Next(gameData.CreatureQuotes.Count)];
        if (quote.Kind == CreatureQuoteKind.CharacterClass)
        {
            var characterClass = gameData.GetCharacterClass(quote.CreatureId);
            return new MainMenuCreature(quote, characterClass.Name,
                AsciiPortraits.ForCharacterClass(characterClass.Id), ConsoleColor.Cyan);
        }

        var enemy = gameData.GetEnemy(quote.CreatureId);
        var color = enemy.StrengthTier switch
        {
            <= 1 => ConsoleColor.Green,
            2 => ConsoleColor.Yellow,
            3 => ConsoleColor.DarkYellow,
            4 => ConsoleColor.Red,
            _ => ConsoleColor.Magenta
        };
        return new MainMenuCreature(quote, enemy.Name, AsciiPortraits.ForEnemy(enemy.Id), color);
    }

    public static void Draw(MainMenuCreature creature, Random random, int left, int width)
    {
        if (left >= Console.WindowWidth || Top >= Console.WindowHeight) return;
        width = Math.Min(width, Console.WindowWidth - left);
        var height = Math.Min(Height, Console.WindowHeight - Top);
        if (width < 10 || height < 4) return;

        var style = WindowFrameConfiguration.For(FramedWindow.CreaturePortrait);
        Console.SetCursorPosition(left, Top);
        Console.ForegroundColor = ConsoleColor.DarkMagenta;
        Console.Write(WindowFrameCatalog.Horizontal(style, width));
        for (var row = 0; row < height - 2; row++)
        {
            var sides = WindowFrameCatalog.Sides(style, row, height - 2);
            Console.SetCursorPosition(left, Top + row + 1);
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.Write(sides.Left);
            Console.Write(new string(' ', Math.Max(0, width - sides.Left.Length - sides.Right.Length)));
            Console.Write(sides.Right);
        }
        Console.SetCursorPosition(left, Top + height - 1);
        Console.Write(WindowFrameCatalog.Horizontal(style, width, bottom: true));

        var interiorLeft = left + 3;
        var interiorWidth = Math.Max(1, width - 6);
        var quote = creature.Quotes.Quotes[random.Next(creature.Quotes.Quotes.Count)];
        var quoteLines = MessageTextLayout.Wrap($"„{quote}”", interiorWidth).Take(5).ToArray();
        for (var index = 0; index < quoteLines.Length && Top + 1 + index < Top + height - 1; index++)
            WriteAt(interiorLeft, Top + 1 + index, quoteLines[index], ConsoleColor.Gray, interiorWidth);

        WriteAt(interiorLeft, Top + 6, $"— {creature.Name}", creature.Color, interiorWidth);
        var portraitTop = Top + height - 1 - creature.Portrait.Lines.Count;
        for (var index = 0; index < creature.Portrait.Lines.Count; index++)
        {
            var line = creature.Portrait.Lines[index];
            var padding = Math.Max(0, (interiorWidth - creature.Portrait.CanvasWidth) / 2);
            WriteAt(interiorLeft + padding, portraitTop + index,
                line.PadRight(creature.Portrait.CanvasWidth), creature.Color, creature.Portrait.CanvasWidth);
        }
        Console.ResetColor();
    }

    private static void WriteAt(int left, int top, string text, ConsoleColor color, int width)
    {
        if (top < 0 || top >= Console.WindowHeight || left >= Console.WindowWidth) return;
        Console.SetCursorPosition(left, top);
        Console.ForegroundColor = color;
        Console.BackgroundColor = ConsoleColor.Black;
        Console.Write(text.Length <= width ? text.PadRight(width) : text[..width]);
    }
}
