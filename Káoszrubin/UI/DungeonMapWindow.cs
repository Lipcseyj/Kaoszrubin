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

    public static void ShowStandalone(DungeonMapSnapshot map)
    {
        Console.Clear();
        var lines = Build(map);
        var width = lines.Max(line => line.Text.Length);
        var left = Math.Max(0, (Console.WindowWidth - width) / 2);
        var top = Math.Max(0, (Console.WindowHeight - lines.Count) / 2);
        for (var index = 0; index < lines.Count; index++)
        {
            Console.ForegroundColor = lines[index].Color;
            Console.SetCursorPosition(left, top + index);
            Console.Write(lines[index].Text);
        }
        while (Console.ReadKey(intercept: true).Key is not (ConsoleKey.Escape or ConsoleKey.Enter)) { }
        Console.ResetColor();
    }
}
