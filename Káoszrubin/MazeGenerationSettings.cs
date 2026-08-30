namespace KaoszRubin;

/// <summary>A labirintus alakját meghatározó, könnyen hangolható beállítások.</summary>
public sealed class MazeGenerationSettings
{
    /// <summary>A két cella széles folyosószakaszok aránya 0 és 1 között.</summary>
    public double DoubleWidthCorridorChance { get; init; } = 0.80;
    public int RoomCount { get; init; } = 5;
    public int MinimumRoomSize { get; init; } = 2;
    public int MaximumRoomSize { get; init; } = 6;
    public int TreasureChestCount { get; init; } = 5;
    public IntRange TreasureGoldRange { get; init; } = new(0, 0);
    public System.Text.Rune WallRune { get; init; } = new('█');
    public ConsoleColor WallColor { get; init; } = ConsoleColor.DarkGray;
    public string LevelName { get; init; } = "Labirintus";
}
