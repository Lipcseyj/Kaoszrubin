using KaoszRubin.World;
using System.ComponentModel;

namespace KaoszRubin.MapEditor;

internal sealed class ForestGraphCanvas : Control
{
    private const int GridSize = 100;
    private const int NodeWidth = 150;
    private const int NodeHeight = 54;
    private const int OriginX = 220;
    private const int OriginY = 160;
    private Point _dragOffset;
    private ForestAreaDefinition? _dragged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<ForestAreaDefinition> Areas { get; set; } = [];
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<ForestAreaConnectionDefinition> Connections { get; set; } = [];
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ForestAreaDefinition? SelectedArea { get; set; }
    public event Action<ForestAreaDefinition?>? SelectionChanged;
    public event Action<ForestAreaDefinition, AreaCoordinate>? AreaMoved;

    public ForestGraphCanvas()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(25, 34, 28);
        Dock = DockStyle.Fill;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var gridPen = new Pen(Color.FromArgb(42, 60, 47));
        for (var x = GridSize / 2; x < Width; x += GridSize) e.Graphics.DrawLine(gridPen, x, 0, x, Height);
        for (var y = GridSize / 2; y < Height; y += GridSize) e.Graphics.DrawLine(gridPen, 0, y, Width, y);
        var byId = Areas.ToDictionary(area => area.Id, StringComparer.Ordinal);
        using var edgePen = new Pen(Color.SandyBrown, 4);
        foreach (var connection in Connections)
        {
            if (!byId.TryGetValue(connection.FirstAreaId, out var first) ||
                !byId.TryGetValue(connection.SecondAreaId, out var second)) continue;
            e.Graphics.DrawLine(edgePen, Center(first), Center(second));
        }
        foreach (var area in Areas)
        {
            var rectangle = NodeBounds(area);
            using var fill = new SolidBrush(area == SelectedArea ? Color.DarkOliveGreen : Color.FromArgb(49, 73, 54));
            using var border = new Pen(area == SelectedArea ? Color.Gold : Color.DarkSeaGreen, area == SelectedArea ? 3 : 2);
            e.Graphics.FillRoundedRectangle(fill, rectangle, 10);
            e.Graphics.DrawRoundedRectangle(border, rectangle, 10);
            TextRenderer.DrawText(e.Graphics, area.Name, Font, rectangle, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            var template = new Rectangle(rectangle.X + 4, rectangle.Bottom - 18, rectangle.Width - 8, 16);
            TextRenderer.DrawText(e.Graphics, area.TemplateId, Font, template, Color.LightGreen,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        SelectedArea = Areas.LastOrDefault(area => NodeBounds(area).Contains(e.Location));
        _dragged = SelectedArea;
        if (_dragged is not null)
        {
            var bounds = NodeBounds(_dragged);
            _dragOffset = new Point(e.X - bounds.X, e.Y - bounds.Y);
        }
        SelectionChanged?.Invoke(SelectedArea);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_dragged is not null)
        {
            var coordinate = new AreaCoordinate(
                (int)Math.Round((e.X - _dragOffset.X - OriginX) / (double)GridSize),
                (int)Math.Round((e.Y - _dragOffset.Y - OriginY) / (double)GridSize));
            AreaMoved?.Invoke(_dragged, coordinate);
        }
        _dragged = null;
    }

    private static Rectangle NodeBounds(ForestAreaDefinition area) => new(
        OriginX + area.Coordinate.X * GridSize,
        OriginY + area.Coordinate.Y * GridSize, NodeWidth, NodeHeight);

    private static Point Center(ForestAreaDefinition area)
    {
        var bounds = NodeBounds(area);
        return new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    }
}

internal static class RoundedGraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius) =>
        graphics.FillPath(brush, Path(bounds, radius));

    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle bounds, int radius) =>
        graphics.DrawPath(pen, Path(bounds, radius));

    private static System.Drawing.Drawing2D.GraphicsPath Path(Rectangle bounds, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
