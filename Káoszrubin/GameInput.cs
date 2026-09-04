namespace KaoszRubin;

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

    public static bool IsSettingsShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.F2 && (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0;

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

#if DEBUG
    public static bool IsRevealMapShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.U &&
        HasControlShift(keyInfo);

    public static bool IsNewMazeShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.R &&
        HasControlShift(keyInfo);

    public static bool IsTeleportToExitShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.E &&
        HasControlShift(keyInfo);

    public static bool IsTeleportToNextUniqueNpcShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.N &&
        HasControlAlt(keyInfo);

    public static bool IsLevelUpShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.S &&
        HasControlAlt(keyInfo);
    
    public static bool IsLevelUpPartyShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.W &&
        HasControlAlt(keyInfo);

    public static bool IsFillPartySetYShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.Y &&
        HasControlShift(keyInfo);

    public static bool IsFillPartySetXShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.X &&
        HasControlAlt(keyInfo);

    public static bool IsAddLevelOnePartyMemberShortcut(ConsoleKeyInfo keyInfo) =>
        (keyInfo.Key is ConsoleKey.Oem102 or ConsoleKey.Oem8 || keyInfo.KeyChar is 'í' or 'Í') &&
        HasControlShift(keyInfo);

    public static bool IsDeveloperPhasingShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.I &&
        HasControlShift(keyInfo);

    private static bool HasControlShift(ConsoleKeyInfo keyInfo) =>
        (keyInfo.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Shift)) ==
        (ConsoleModifiers.Control | ConsoleModifiers.Shift);

    private static bool HasControlAlt(ConsoleKeyInfo keyInfo) =>
        (keyInfo.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) ==
        (ConsoleModifiers.Control | ConsoleModifiers.Alt);
#endif
}
