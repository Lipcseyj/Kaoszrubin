using System.ComponentModel;
using System.Runtime.InteropServices;

namespace KaoszRubin.UI;

/// <summary>Saves console cells (including their colors) with one native buffer read.</summary>
public sealed class BackgroundContentRestorer : IDisposable
{
    private readonly nint _output;
    private readonly Cell[] _cells;
    private readonly Coord _size;
    private readonly Rectangle _region;
    private readonly (int Left, int Top) _cursor;
    private readonly ConsoleColor _foreground = Console.ForegroundColor;
    private readonly ConsoleColor _background = Console.BackgroundColor;
    private readonly Action? _invalidateColors;
    private bool _disposed;

    public BackgroundContentRestorer(int left, int top, int width, int height, Action? invalidateColors = null)
    {
        _invalidateColors = invalidateColors;
        _cursor = Console.GetCursorPosition();
        left = Math.Clamp(left, 0, Console.BufferWidth - 1);
        top = Math.Clamp(top, 0, Console.BufferHeight - 1);
        width = Math.Min(width, Console.BufferWidth - left);
        height = Math.Min(height, Console.BufferHeight - top);
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        _size = new Coord { X = checked((short)width), Y = checked((short)height) };
        _region = new Rectangle { Left = (short)left, Top = (short)top,
            Right = checked((short)(left + width - 1)), Bottom = checked((short)(top + height - 1)) };
        _cells = new Cell[width * height];
        _output = GetStdHandle(-11);
        var region = _region;
        if (!ReadConsoleOutputW(_output, _cells, _size, default, ref region))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Nem sikerült menteni az ablak hátterét.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var region = _region;
        try
        {
            if (!WriteConsoleOutputW(_output, _cells, _size, default, ref region))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Nem sikerült visszaállítani az ablak hátterét.");
        }
        finally
        {
            Console.ForegroundColor = _foreground;
            Console.BackgroundColor = _background;
            Console.SetCursorPosition(Math.Min(_cursor.Left, Console.BufferWidth - 1),
                Math.Min(_cursor.Top, Console.BufferHeight - 1));
            _invalidateColors?.Invoke();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Coord { public short X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle { public short Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Explicit, Size = 4)]
    private struct Cell
    {
        [FieldOffset(0)] public ushort Character;
        [FieldOffset(2)] public ushort Attributes;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetStdHandle(int handle);
    [DllImport("kernel32.dll", EntryPoint = "ReadConsoleOutputW", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadConsoleOutputW(nint handle, [Out] Cell[] buffer, Coord size, Coord origin, ref Rectangle region);
    [DllImport("kernel32.dll", EntryPoint = "WriteConsoleOutputW", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteConsoleOutputW(nint handle, Cell[] buffer, Coord size, Coord origin, ref Rectangle region);
}
