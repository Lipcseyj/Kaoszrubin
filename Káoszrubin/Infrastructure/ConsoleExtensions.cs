using System;
using System.Collections.Generic;
using System.Text;

namespace Kaoszrubin.Infrastructure;

public static class ConsoleExtensions
{
    /// <summary>
    /// Beolvas egy sort a konzolról legfeljebb a megadott számú karakterrel.
    /// </summary>
    public static string ReadLine(int maxLength)
    {
        if (maxLength < 1)
            throw new ArgumentOutOfRangeException(
                nameof(maxLength),
                "A maximális karakterszám legalább 1 legyen.");

        var text = new List<char>();
        var cursorIndex = 0;

        var startLeft = Console.CursorLeft;
        var startTop = Console.CursorTop;

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    return new string(text.ToArray());

                case ConsoleKey.Backspace:
                    if (cursorIndex > 0)
                    {
                        text.RemoveAt(cursorIndex - 1);
                        cursorIndex--;

                        Redraw(
                            text,
                            cursorIndex,
                            startLeft,
                            startTop,
                            maxLength);
                    }

                    break;

                case ConsoleKey.Delete:
                    if (cursorIndex < text.Count)
                    {
                        text.RemoveAt(cursorIndex);

                        Redraw(
                            text,
                            cursorIndex,
                            startLeft,
                            startTop,
                            maxLength);
                    }

                    break;

                case ConsoleKey.LeftArrow:
                    if (cursorIndex > 0)
                    {
                        cursorIndex--;
                        SetCursorPosition(
                            startLeft,
                            startTop,
                            cursorIndex);
                    }

                    break;

                case ConsoleKey.RightArrow:
                    if (cursorIndex < text.Count)
                    {
                        cursorIndex++;
                        SetCursorPosition(
                            startLeft,
                            startTop,
                            cursorIndex);
                    }

                    break;

                case ConsoleKey.Home:
                    cursorIndex = 0;

                    SetCursorPosition(
                        startLeft,
                        startTop,
                        cursorIndex);

                    break;

                case ConsoleKey.End:
                    cursorIndex = text.Count;

                    SetCursorPosition(
                        startLeft,
                        startTop,
                        cursorIndex);

                    break;

                default:
                    if (!char.IsControl(key.KeyChar) &&
                        text.Count < maxLength)
                    {
                        text.Insert(
                            cursorIndex,
                            key.KeyChar);

                        cursorIndex++;

                        Redraw(
                            text,
                            cursorIndex,
                            startLeft,
                            startTop,
                            maxLength);
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Beolvas egy sort a konzolról legfeljebb a megadott számú karakterrel és az eredményt rögtön trimmeli.
    /// </summary>
    public static string ReadLine(int maxLength, bool trim)
    {
        var result =
            ReadLine(maxLength);

        return trim
            ? result.Trim()
            : result;
    }

    private static void Redraw(
        IReadOnlyList<char> text,
        int cursorIndex,
        int startLeft,
        int startTop,
        int maxLength)
    {
        Console.SetCursorPosition(
            startLeft,
            startTop);

        Console.Write(
            new string(text.ToArray()));

        // Kitöröljük az esetleg korábban hosszabb szöveg maradékát.
        var remaining =
            maxLength - text.Count;

        if (remaining > 0)
            Console.Write(
                new string(' ', remaining));

        SetCursorPosition(
            startLeft,
            startTop,
            cursorIndex);
    }

    private static void SetCursorPosition(
        int startLeft,
        int startTop,
        int characterIndex)
    {
        Console.SetCursorPosition(
            startLeft + characterIndex,
            startTop);
    }
}
