namespace MazeGame;

internal static class GameInput
{
    public static bool TryGetDirection(ConsoleKey key, out Direction direction)
    {
        direction = key switch
        {
            ConsoleKey.UpArrow => Direction.Up,
            ConsoleKey.DownArrow => Direction.Down,
            ConsoleKey.LeftArrow => Direction.Left,
            ConsoleKey.RightArrow => Direction.Right,
            _ => default
        };
        return key is ConsoleKey.UpArrow or ConsoleKey.DownArrow or ConsoleKey.LeftArrow or ConsoleKey.RightArrow;
    }

    public static bool IsSaveGameShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.F9;

    public static bool IsHelpShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.F1 && (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0;

    public static bool TryGetQuickSpellIndex(ConsoleKeyInfo keyInfo, out int slotIndex)
    {
        slotIndex = keyInfo.Key switch
        {
            ConsoleKey.F1 => 0,
            ConsoleKey.F2 => 1,
            ConsoleKey.F3 => 2,
            ConsoleKey.F4 => 3,
            ConsoleKey.F5 => 4,
            ConsoleKey.F6 => 5,
            ConsoleKey.F7 => 6,
            ConsoleKey.F8 => 7,
            _ => -1
        };
        return slotIndex >= 0 && (keyInfo.Modifiers & ConsoleModifiers.Shift) == 0;
    }

    public static bool IsRevealMapShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.U &&
        HasControlShift(keyInfo);

    public static bool IsNewMazeShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.R &&
        HasControlShift(keyInfo);

    public static bool IsTeleportToExitShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.E &&
        HasControlShift(keyInfo);

    public static bool IsLevelUpShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.S &&
        HasControlShift(keyInfo);

    public static bool IsFillPartyShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.Y &&
        HasControlShift(keyInfo);

    public static bool IsAddLevelOnePartyMemberShortcut(ConsoleKeyInfo keyInfo) =>
        (keyInfo.Key is ConsoleKey.Oem102 or ConsoleKey.Oem8 || keyInfo.KeyChar is 'í' or 'Í') &&
        HasControlShift(keyInfo);

    public static bool IsDeveloperPhasingShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.I &&
        HasControlShift(keyInfo);

    public static bool IsGrantPartyExperienceShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.X &&
        HasControlShift(keyInfo);

    private static bool HasControlShift(ConsoleKeyInfo keyInfo) =>
        (keyInfo.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Shift)) ==
        (ConsoleModifiers.Control | ConsoleModifiers.Shift);
}