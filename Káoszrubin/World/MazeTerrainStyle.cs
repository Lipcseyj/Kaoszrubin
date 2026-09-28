using System.Text;

namespace KaoszRubin.World;

/// <summary>
/// Egy térképrúna játékmeneti és megjelenítési tulajdonságai. A stílus pályánként regisztrált,
/// ezért ugyanaz a tereptípus különböző pályákon más színt kaphat.
/// </summary>
public sealed record MazeTerrainStyle(
    string Id,
    Rune Rune,
    ConsoleColor ForegroundColor,
    ConsoleColor BackgroundColor,
    bool Walkable,
    bool BlocksSight);
