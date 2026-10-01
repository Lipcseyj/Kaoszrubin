using KaoszRubin.World;

namespace KaoszRubin.UI;

public static class DungeonMapWindow
{
    private const int NodeWidth = 18;
    private const int HorizontalStep = 22;
    private const int VerticalStep = 3;

    public static IReadOnlyList<(string Text, ConsoleColor Color)> Build(DungeonMapSnapshot map)
    {
        if (map.Nodes.Count == 0)
            return [("Még egyetlen területet sem fedeztetek fel.", ConsoleColor.DarkGray)];
        var minX = map.Nodes.Min(node => node.Coordinate.X);
        var minY = map.Nodes.Min(node => node.Coordinate.Y);
        var maxX = map.Nodes.Max(node => node.Coordinate.X);
        var maxY = map.Nodes.Max(node => node.Coordinate.Y);
        var width = (maxX - minX) * HorizontalStep + NodeWidth;
        var height = (maxY - minY) * VerticalStep + 1;
        var canvas = Enumerable.Range(0, height).Select(_ => Enumerable.Repeat(' ', width).ToArray()).ToArray();
        var byId = map.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);

        foreach (var edge in map.Edges)
        {
            if (!byId.TryGetValue(edge.FirstAreaId, out var first) ||
                !byId.TryGetValue(edge.SecondAreaId, out var second)) continue;
            var firstX = (first.Coordinate.X - minX) * HorizontalStep + NodeWidth / 2;
            var firstY = (first.Coordinate.Y - minY) * VerticalStep;
            var secondX = (second.Coordinate.X - minX) * HorizontalStep + NodeWidth / 2;
            var secondY = (second.Coordinate.Y - minY) * VerticalStep;
            if (firstY == secondY)
                for (var x = Math.Min(firstX, secondX) + NodeWidth / 2;
                     x <= Math.Max(firstX, secondX) - NodeWidth / 2; x++) canvas[firstY][x] = '─';
            else
                for (var y = Math.Min(firstY, secondY) + 1; y < Math.Max(firstY, secondY); y++)
                    canvas[y][firstX] = '│';
        }

        foreach (var node in map.Nodes)
        {
            var x = (node.Coordinate.X - minX) * HorizontalStep;
            var y = (node.Coordinate.Y - minY) * VerticalStep;
            var marker = node.IsCurrent ? '◆' : node.IsVisited ? '●' : '○';
            var name = node.Name.Length > NodeWidth - 4 ? node.Name[..(NodeWidth - 5)] + "…" : node.Name;
            var label = $"[{marker} {name}]".PadRight(NodeWidth);
            for (var index = 0; index < Math.Min(NodeWidth, label.Length); index++) canvas[y][x + index] = label[index];
        }

        var result = new List<(string Text, ConsoleColor Color)>
        {
            ("FELFEDEZETT ERDŐVIDÉK", ConsoleColor.Yellow),
            ("◆ jelenlegi   ● meglátogatott   ○ ismert átjáró", ConsoleColor.DarkCyan),
            (string.Empty, ConsoleColor.Gray)
        };
        result.AddRange(canvas.Select(row => (new string(row).TrimEnd(), ConsoleColor.Cyan)));
        result.Add((string.Empty, ConsoleColor.Gray));
        result.Add(("ESC / ENTER – vissza", ConsoleColor.DarkGray));
        return result;
    }

    public static void Show(DungeonMapSnapshot map, Func<string?>? coopStatusProvider = null)
    {
        var lines = Build(map);
        var maximumWidth = Math.Max(20, Console.WindowWidth - 4);
        var minimumWidth = Math.Min(48, maximumWidth);
        var width = Math.Clamp(lines.Max(line => BattleCommandPanel.DisplayWidth(line.Text)) + 6,
            minimumWidth, maximumWidth);
        var height = Math.Min(lines.Count + 2, Math.Max(4, Console.WindowHeight - 4));
        var left = Math.Max(0, (Console.WindowWidth - width) / 2);
        var top = Math.Max(0, (Console.WindowHeight - height) / 2);
        var style = WindowFrameConfiguration.For(FramedWindow.DungeonMap);
        using var background = new BackgroundContentRestorer(left, top, width, height);
        DrawFrame(left, top, width, height, style);
        var contentWidth = Math.Max(0, width - 6);
        var availableRows = height - 2;
        var visibleLines = lines.Count <= availableRows
            ? lines
            : lines.Take(Math.Max(0, availableRows - 1)).Append(lines[^1]).ToArray();
        for (var index = 0; index < visibleLines.Count; index++)
        {
            Console.SetCursorPosition(left + 3, top + index + 1);
            Console.ForegroundColor = visibleLines[index].Color;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Write(BattleCommandPanel.FitToDisplayWidth(visibleLines[index].Text, contentWidth));
        }
        while (CoopWindowStatusBanner.ReadKey(coopStatusProvider).Key is not (ConsoleKey.Escape or ConsoleKey.Enter)) { }
        Console.ResetColor();
    }

    private static void DrawFrame(int left, int top, int width, int height, WindowFrameStyle style)
    {
        Write(left, top, WindowFrameCatalog.Horizontal(style, width), ConsoleColor.Magenta, width);
        var interiorRows = height - 2;
        for (var row = 0; row < interiorRows; row++)
        {
            var sides = WindowFrameCatalog.Sides(style, row, interiorRows);
            Write(left, top + row + 1, sides.Left, ConsoleColor.Magenta, sides.Left.Length);
            Write(left + sides.Left.Length, top + row + 1, string.Empty, ConsoleColor.Gray,
                width - sides.Left.Length - sides.Right.Length);
            Write(left + width - sides.Right.Length, top + row + 1, sides.Right,
                ConsoleColor.Magenta, sides.Right.Length);
        }
        Write(left, top + height - 1, WindowFrameCatalog.Horizontal(style, width, bottom: true),
            ConsoleColor.Magenta, width);
    }

    private static void Write(int x, int y, string text, ConsoleColor color, int width)
    {
        if (width <= 0 || x < 0 || y < 0 || x >= Console.WindowWidth || y >= Console.WindowHeight) return;
        var fitted = BattleCommandPanel.FitToDisplayWidth(text, Math.Min(width, Console.WindowWidth - x));
        Console.SetCursorPosition(x, y);
        Console.ForegroundColor = color;
        Console.BackgroundColor = ConsoleColor.Black;
        Console.Write(fitted);
    }
}
