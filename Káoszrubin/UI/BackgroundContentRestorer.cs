using System.Runtime.InteropServices;

namespace KaoszRubin.UI;

/// <summary>Saves console cells (including their colors) with one native buffer read.</summary>
public sealed class BackgroundContentRestorer : IDisposable
{
    private readonly nint _output;
    private readonly Cell[] _cells = [];
    private readonly Coord _size;
    private readonly Rectangle _region;
    private readonly (int Left, int Top) _cursor;
    private readonly ConsoleColor _foreground = Console.ForegroundColor;
    private readonly ConsoleColor _background = Console.BackgroundColor;
    private readonly Action? _invalidateColors;
    private readonly bool _captured;
    private bool _disposed;

    public BackgroundContentRestorer(int left, int top, int width, int height, Action? invalidateColors = null)
    {
        _invalidateColors = invalidateColors;
        try
        {
            _cursor = Console.GetCursorPosition();
            var bufferWidth = Console.BufferWidth;
            var bufferHeight = Console.BufferHeight;
            if (bufferWidth <= 0 || bufferHeight <= 0) return;
            left = Math.Clamp(left, 0, bufferWidth - 1);
            top = Math.Clamp(top, 0, bufferHeight - 1);
            width = Math.Min(width, bufferWidth - left);
            height = Math.Min(height, bufferHeight - top);
            if (width <= 0 || height <= 0 || width > short.MaxValue || height > short.MaxValue) return;
            _size = new Coord { X = (short)width, Y = (short)height };
            _region = new Rectangle { Left = (short)left, Top = (short)top,
                Right = (short)(left + width - 1), Bottom = (short)(top + height - 1) };
            _cells = new Cell[width * height];
            _output = GetStdHandle(-11);
            var region = _region;
            _captured = ReadConsoleOutputW(_output, _cells, _size, default, ref region);
        }
        catch (Exception exception) when (TerminalViewport.IsTransientConsoleException(exception))
        {
            // Átméretezés alatt az overlay háttérmentése egyszerűen kimarad.
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_captured && Console.BufferWidth > _region.Right && Console.BufferHeight > _region.Bottom)
            {
                var region = _region;
                WriteConsoleOutputW(_output, _cells, _size, default, ref region);
            }
        }
        catch (Exception exception) when (TerminalViewport.IsTransientConsoleException(exception))
        {
            // Méretváltás után a fő képernyő teljes újrarajzolást végez.
        }
        finally
        {
            try
            {
                Console.ForegroundColor = _foreground;
                Console.BackgroundColor = _background;
                Console.SetCursorPosition(Math.Min(_cursor.Left, Math.Max(0, Console.BufferWidth - 1)),
                    Math.Min(_cursor.Top, Math.Max(0, Console.BufferHeight - 1)));
            }
            catch (Exception exception) when (TerminalViewport.IsTransientConsoleException(exception))
            {
            }
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
