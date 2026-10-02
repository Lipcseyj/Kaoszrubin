using System.Text;

namespace KaoszRubin.UI;

internal readonly record struct PlayfieldCellVisual(
    Rune Rune,
    ConsoleColor ForegroundColor,
    ConsoleColor BackgroundColor);

internal interface IPlayfieldConsoleOutput
{
    void SetCursorPosition(int left, int top);
    void SetColors(ConsoleColor foregroundColor, ConsoleColor backgroundColor);
    void Write(ReadOnlySpan<char> value);
}

internal static class PlayfieldConsoleWriter
{
    internal static void WriteRow(
        int row,
        ReadOnlySpan<PlayfieldCellVisual> cells,
        Span<char> textBuffer,
        IPlayfieldConsoleOutput output)
    {
        if (cells.IsEmpty) return;

        output.SetCursorPosition(0, row);
        var runStart = 0;
        var textLength = 0;
        var foregroundColor = cells[0].ForegroundColor;
        var backgroundColor = cells[0].BackgroundColor;

        foreach (var cell in cells)
        {
            if (cell.ForegroundColor != foregroundColor || cell.BackgroundColor != backgroundColor)
            {
                output.SetColors(foregroundColor, backgroundColor);
                output.Write(textBuffer[runStart..textLength]);
                runStart = textLength;
                foregroundColor = cell.ForegroundColor;
                backgroundColor = cell.BackgroundColor;
            }

            textLength += cell.Rune.EncodeToUtf16(textBuffer[textLength..]);
        }

        output.SetColors(foregroundColor, backgroundColor);
        output.Write(textBuffer[runStart..textLength]);
    }
}
