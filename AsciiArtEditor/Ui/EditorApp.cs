using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using AsciiArtEditor.Services;

namespace AsciiArtEditor.Ui;

public sealed class EditorApp
{
    private const int PortraitWidth = 17;
    private const int PortraitHeight = 5;
    private const int PaletteCellWidth = 4;
    private const int StdInputHandle = -10;
    private const uint EnableMouseInput = 0x0010;
    private const uint EnableQuickEditMode = 0x0040;
    private const uint EnableExtendedFlags = 0x0080;
    private const ushort KeyEventType = 0x0001;
    private const ushort MouseEventType = 0x0002;
    private const uint LeftButtonPressed = 0x0001;
    private const uint RightButtonPressed = 0x0002;
    private const uint MouseMoved = 0x0001;

    private readonly string _sourcePath;
    private readonly AsciiPortraitSource _source;
    private readonly Dictionary<string, string> _portraits;
    private readonly string[] _palette;
    private (PortraitDictionary Dictionary, PortraitSourceEntry Entry)[] _portraitEntries = [];
    private int _portraitIndex;
    private PortraitDictionary? _selectedDictionary;
    private string? _selectedKeyExpression;
    private string? _proposedKeyExpression;
    private int _canvasWidth = PortraitWidth;
    private int _canvasHeight = PortraitHeight;
    private string[,] _cells = CreateCanvas(PortraitWidth, PortraitHeight);
    private int _cursorX;
    private int _cursorY;
    private int _mouseHoverX = -1;
    private int _mouseHoverY = -1;
    private string _brush = " ";
    private int _paletteIndex;
    private int _palettePage;
    private string _status = "";

    private bool IsPortraitMode => _canvasWidth == PortraitWidth && _canvasHeight == PortraitHeight;
    private string ModeName => IsPortraitMode ? "Portrait" : "Art";

    public EditorApp(string sourcePath, AsciiPortraitSource source, Dictionary<string, string> portraits)
    {
        _sourcePath = sourcePath;
        _source = source;
        _portraits = portraits;
        var sourceContent = File.ReadAllText(sourcePath);
        _portraitEntries = Enum.GetValues<PortraitDictionary>()
            .SelectMany(dictionary => _source.ParseDictionary(sourceContent, dictionary)
                .Select(entry => (dictionary, entry)))
            .ToArray();
        _palette = PortraitPalette.Collect(_portraitEntries.Select(item => item.Entry.Content))
            .ToArray();
        _paletteIndex = Array.IndexOf(_palette, "█");
        if (_paletteIndex < 0)
            _paletteIndex = 0;
        _brush = _palette[_paletteIndex];
    }

    public void Run()
    {
        if (_portraitEntries.Length > 0)
            SelectPortrait(0, redraw: false);
        else
            StartNewPortrait();

        var originalCursorVisible = Console.CursorVisible;
        var inputHandle = IntPtr.Zero;
        uint originalInputMode = 0;
        var inputModeChanged = false;
        var mouseEnabled = false;

        try
        {
            Console.CursorVisible = false;
            if (OperatingSystem.IsWindows() && !Console.IsInputRedirected)
            {
                inputHandle = GetStdHandle(StdInputHandle);
                if (inputHandle != IntPtr.Zero && inputHandle != new IntPtr(-1) && GetConsoleMode(inputHandle, out originalInputMode))
                {
                    var editorMode = (originalInputMode | EnableMouseInput | EnableExtendedFlags) & ~EnableQuickEditMode;
                    inputModeChanged = SetConsoleMode(inputHandle, editorMode);
                    mouseEnabled = inputModeChanged;
                }
            }

            if (mouseEnabled)
            {
                DrawAll(clear: true);
                ReadWindowsInput(inputHandle);
            }
            else
            {
                _status = "Mouse unavailable; use arrows, P, Space/D, E, Delete, S, C. ";
                RunKeyboardInput();
            }
        }
        finally
        {
            if (inputModeChanged)
                SetConsoleMode(inputHandle, originalInputMode);

            Console.CursorVisible = originalCursorVisible;
            Console.ResetColor();
            Console.Clear();
        }
    }

    private void ReadWindowsInput(IntPtr inputHandle)
    {
        while (true)
        {
            if (!ReadConsoleInput(inputHandle, out var record, 1, out var eventsRead) || eventsRead == 0)
            {
                _status = "Could not read console input. Press a key to retry; Esc quits.";
                DrawAll();
                RunKeyboardInput();
                return;
            }

            if (record.EventType == KeyEventType)
            {
                if (record.KeyDown == 0)
                    continue;

                if (record.VirtualKeyCode == (ushort)ConsoleKey.Escape || record.UnicodeChar is 'q' or 'Q')
                    return;

                var handled = HandleKey(new ConsoleKeyInfo(
                    record.UnicodeChar,
                    (ConsoleKey)record.VirtualKeyCode,
                    (record.ControlKeyState & 0x0010) != 0,
                    (record.ControlKeyState & 0x0003) != 0,
                    (record.ControlKeyState & 0x000C) != 0));
                if (handled)
                    DrawKeyChanges((ConsoleKey)record.VirtualKeyCode);
                continue;
            }

            if (record.EventType == MouseEventType)
            {
                var mouse = record.MouseEvent;
                var leftButtonDown = (mouse.ButtonState & LeftButtonPressed) != 0;
                var rightButtonDown = (mouse.ButtonState & RightButtonPressed) != 0;
                if (leftButtonDown || rightButtonDown || (mouse.EventFlags & MouseMoved) != 0)
                    HandleMouse(mouse.Position.X, mouse.Position.Y, leftButtonDown, rightButtonDown);

                continue;
            }
        }
    }

    private void RunKeyboardInput()
    {
        DrawAll(clear: true);
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q)
                return;

            if (HandleKey(key))
                DrawKeyChanges(key.Key);
        }
    }

    private void DrawKeyChanges(ConsoleKey key)
    {
        var layout = CalculateLayout();
        switch (key)
        {
            case ConsoleKey.LeftArrow:
            case ConsoleKey.RightArrow:
            case ConsoleKey.UpArrow:
            case ConsoleKey.DownArrow:
                DrawCanvasPanel(layout);
                DrawStatus(layout);
                break;
            case ConsoleKey.Spacebar:
            case ConsoleKey.D:
            case ConsoleKey.E:
                DrawCanvasCell(layout, _cursorX, _cursorY);
                DrawStatus(layout);
                break;
            case ConsoleKey.P:
                DrawPalettePanel(layout);
                DrawStatus(layout);
                break;
            case ConsoleKey.PageUp:
            case ConsoleKey.PageDown:
                DrawPalettePanel(layout);
                break;
            case ConsoleKey.C:
            case ConsoleKey.N:
                DrawAll();
                break;
            case ConsoleKey.Delete:
                DrawCanvasPanel(layout);
                DrawStatus(layout);
                break;
            case ConsoleKey.S:
                DrawAll();
                break;
        }
    }

    private void Load(string content)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        _canvasHeight = Math.Clamp(lines.Length, 1, 40);
        _cells = CreateCanvas(_canvasWidth, _canvasHeight);
        for (var y = 0; y < Math.Min(_canvasHeight, lines.Length); y++)
        {
            for (var x = 0; x < Math.Min(_canvasWidth, lines[y].Length); x++)
                _cells[x, y] = lines[y][x].ToString();
        }
    }

    private bool HandleKey(ConsoleKeyInfo key)
    {
        if ((key.Modifiers & ConsoleModifiers.Shift) != 0)
        {
            if (key.Key == ConsoleKey.LeftArrow)
            {
                SwitchPortrait(-1);
                return false;
            }

            if (key.Key == ConsoleKey.RightArrow)
            {
                SwitchPortrait(1);
                return false;
            }
        }

        switch (key.Key)
        {
            case ConsoleKey.LeftArrow:
                _cursorX = Math.Max(0, _cursorX - 1);
                break;
            case ConsoleKey.RightArrow:
                _cursorX = Math.Min(_canvasWidth - 1, _cursorX + 1);
                break;
            case ConsoleKey.UpArrow:
                _cursorY = Math.Max(0, _cursorY - 1);
                break;
            case ConsoleKey.DownArrow:
                _cursorY = Math.Min(_canvasHeight - 1, _cursorY + 1);
                break;
            case ConsoleKey.Spacebar:
            case ConsoleKey.D:
                DrawAtCursor();
                break;
            case ConsoleKey.E:
                _cells[_cursorX, _cursorY] = " ";
                break;
            case ConsoleKey.P:
                SelectPaletteGlyph((_paletteIndex + 1) % _palette.Length);
                break;
            case ConsoleKey.S:
                SaveCurrent();
                break;
            case ConsoleKey.C:
                ChangeCanvasSize();
                break;
            case ConsoleKey.N:
                StartNewPortrait();
                break;
            case ConsoleKey.Delete:
                ClearCanvas();
                break;
            case ConsoleKey.PageUp:
                _palettePage = Math.Max(0, _palettePage - 1);
                break;
            case ConsoleKey.PageDown:
                _palettePage = Math.Min(CalculateLayout().PalettePageCount - 1, _palettePage + 1);
                break;
        }

        return true;
    }

    private void SwitchPortrait(int direction)
    {
        if (_portraitEntries.Length == 0)
            return;

        var index = (_portraitIndex + direction + _portraitEntries.Length) % _portraitEntries.Length;
        var previousLayout = CalculateLayout();
        SelectPortrait(index, redraw: false);
        ClearCanvasPanel(previousLayout);
        var layout = CalculateLayout();
        DrawCanvasPanel(layout);
        DrawStatus(layout);
    }

    private void SelectPortrait(int index, bool redraw)
    {
        _portraitIndex = index;
        var portrait = _portraitEntries[index];
        _selectedDictionary = portrait.Dictionary;
        _selectedKeyExpression = portrait.Entry.KeyExpression;
        _proposedKeyExpression = null;
        Load(portrait.Entry.Content);
        _status = $"Portrait {_portraitIndex + 1}/{_portraitEntries.Length}: {_selectedKeyExpression}";
        if (redraw)
        {
            var layout = CalculateLayout();
            DrawCanvasPanel(layout);
            DrawStatus(layout);
        }
    }

    private void StartNewPortrait()
    {
        _selectedDictionary = null;
        _selectedKeyExpression = null;
        _proposedKeyExpression = null;
        _canvasWidth = PortraitWidth;
        _canvasHeight = PortraitHeight;
        _cells = CreateCanvas(_canvasWidth, _canvasHeight);
        _cursorX = 0;
        _cursorY = 0;
        _status = "New portrait. Save with S and choose the target dictionary and key. ";
    }

    private void ClearCanvas()
    {
        for (var y = 0; y < _canvasHeight; y++)
            for (var x = 0; x < _canvasWidth; x++)
                _cells[x, y] = " ";

        _status = "Canvas cleared.";
    }

    private void HandleMouse(int x, int y, bool leftButtonDown, bool rightButtonDown)
    {
        var layout = CalculateLayout();
        var canvasLeft = layout.CanvasFrameX + 1;
        var canvasTop = layout.CanvasFrameY + 1;
        var isOverCanvas = x >= canvasLeft && x < canvasLeft + _canvasWidth &&
                           y >= canvasTop && y < canvasTop + _canvasHeight;
        var newHoverX = isOverCanvas ? x - canvasLeft : -1;
        var newHoverY = isOverCanvas ? y - canvasTop : -1;
        if (newHoverX != _mouseHoverX || newHoverY != _mouseHoverY)
        {
            var previousHoverX = _mouseHoverX;
            var previousHoverY = _mouseHoverY;
            _mouseHoverX = newHoverX;
            _mouseHoverY = newHoverY;
            DrawCanvasCell(layout, previousHoverX, previousHoverY);
            DrawCanvasCell(layout, _mouseHoverX, _mouseHoverY);
        }

        if (!leftButtonDown && !rightButtonDown)
            return;

        if (isOverCanvas)
        {
            var oldCursorX = _cursorX;
            var oldCursorY = _cursorY;
            var newCursorX = newHoverX;
            var newCursorY = newHoverY;
            var newCell = rightButtonDown ? " " : _brush;
            var cellChanged = _cells[newCursorX, newCursorY] != newCell;
            if (oldCursorX == newCursorX && oldCursorY == newCursorY && !cellChanged)
                return;

            _cursorX = newCursorX;
            _cursorY = newCursorY;
            _cells[_cursorX, _cursorY] = newCell;
            DrawCanvasCell(layout, oldCursorX, oldCursorY);
            if (oldCursorX != newCursorX || oldCursorY != newCursorY)
                DrawCanvasCell(layout, newCursorX, newCursorY);
            DrawStatus(layout);
            return;
        }

        var paletteColumn = (x - layout.PaletteItemsLeft) / PaletteCellWidth;
        var paletteRow = y - layout.PaletteItemsTop;
        if (x >= layout.PaletteItemsLeft && paletteColumn < layout.PaletteColumns &&
            paletteRow >= 0 && paletteRow < layout.PaletteRows)
        {
            var index = _palettePage * layout.PalettePageSize + paletteRow * layout.PaletteColumns + paletteColumn;
            if (index >= _palette.Length || index == _paletteIndex)
                return;

            var previousIndex = _paletteIndex;
            SelectPaletteGlyph(index);
            DrawPaletteTile(layout, previousIndex);
            DrawPaletteTile(layout, index);
            DrawStatus(layout);
        }
    }

    private void SelectPaletteGlyph(int index)
    {
        _paletteIndex = index;
        _brush = _palette[index];
        _palettePage = _paletteIndex / CalculateLayout().PalettePageSize;
        _status = $"Brush selected: '{_brush}'";
    }

    private void DrawAtCursor() => _cells[_cursorX, _cursorY] = _brush;

    private void DrawCanvasCell(EditorLayout layout, int x, int y)
    {
        var screenX = layout.CanvasFrameX + 1 + x;
        var screenY = layout.CanvasFrameY + 1 + y;
        if (x < 0 || y < 0 || x >= _canvasWidth || y >= _canvasHeight ||
            screenX < 0 || screenX >= layout.Width - 1 || screenY < 0 || screenY >= layout.Height - 1)
            return;

        Console.SetCursorPosition(screenX, screenY);
        if (_mouseHoverX == x && _mouseHoverY == y)
            Console.BackgroundColor = ConsoleColor.DarkCyan;
        else if (_cursorX == x && _cursorY == y)
            Console.BackgroundColor = ConsoleColor.DarkBlue;
        Console.Write(_cells[x, y]);
        Console.ResetColor();
    }

    private void DrawPaletteTile(EditorLayout layout, int index)
    {
        var pageStart = _palettePage * layout.PalettePageSize;
        var localIndex = index - pageStart;
        if (index < 0 || index >= _palette.Length || localIndex < 0 || localIndex >= layout.PalettePageSize)
            return;

        var row = localIndex / layout.PaletteColumns;
        var column = localIndex % layout.PaletteColumns;
        var x = layout.PaletteItemsLeft + column * PaletteCellWidth;
        var y = layout.PaletteItemsTop + row;
        if (x + PaletteCellWidth >= layout.Width - 1 || y >= layout.Height - 1)
            return;

        Console.SetCursorPosition(x, y);
        Console.BackgroundColor = index == _paletteIndex ? ConsoleColor.DarkGreen : ConsoleColor.Black;
        Console.Write($" {_palette[index]}  ");
        Console.ResetColor();
    }

    private void ChangeCanvasSize()
    {
        var previousLayout = CalculateLayout();
        Prompt("New width: ");
        if (!int.TryParse(Console.ReadLine(), out var width))
            return;
        Prompt("New height: ");
        if (!int.TryParse(Console.ReadLine(), out var height))
            return;

        width = Math.Clamp(width, 1, 80);
        height = Math.Clamp(height, 1, 40);
        if (width == _canvasWidth && height == _canvasHeight)
            return;

        var resized = CreateCanvas(width, height);
        for (var y = 0; y < Math.Min(height, _canvasHeight); y++)
            for (var x = 0; x < Math.Min(width, _canvasWidth); x++)
                resized[x, y] = _cells[x, y];

        ClearCanvasPanel(previousLayout);
        _canvasWidth = width;
        _canvasHeight = height;
        _cells = resized;
        _cursorX = Math.Min(_cursorX, width - 1);
        _cursorY = Math.Min(_cursorY, height - 1);
    }

    private void SaveCurrent()
    {
        if (!IsPortraitMode)
        {
            SaveArt();
            return;
        }

        var dictionary = _selectedDictionary;
        var keyExpression = _selectedKeyExpression;
        if (dictionary is null || keyExpression is null)
        {
            Prompt("Target dictionary: C=character, E=enemy: ");
            dictionary = Console.ReadLine()?.Trim().ToUpperInvariant() switch
            {
                "C" or "CHARACTER" or "CHARACTERCLASSES" => PortraitDictionary.CharacterClasses,
                "E" or "ENEMY" or "ENEMIES" => PortraitDictionary.Enemies,
                _ => null
            };
            if (dictionary is null)
            {
                _status = "Save canceled: choose C or E for the target dictionary.";
                return;
            }

            Prompt($"C# key expression [{_proposedKeyExpression}]: ");
            var typedKey = Console.ReadLine()?.Trim();
            keyExpression = string.IsNullOrEmpty(typedKey) ? _proposedKeyExpression : typedKey;
            if (string.IsNullOrEmpty(keyExpression))
            {
                _status = "Save canceled: a key expression is required.";
                return;
            }
        }

        var result = _source.SavePortraitInFile(_sourcePath, dictionary.Value, keyExpression, GetCanvasContent());
        if (!result.Success)
        {
            _status = $"Save failed: {result.Error}";
            return;
        }

        _selectedDictionary = dictionary;
        _selectedKeyExpression = keyExpression;
        _proposedKeyExpression = null;
        RefreshPortraitEntries();
        _portraitIndex = Array.FindIndex(_portraitEntries, item =>
            item.Dictionary == dictionary &&
            string.Equals(item.Entry.KeyExpression, keyExpression, StringComparison.Ordinal));
        _status = result.Inserted ? "Portrait inserted." : "Portrait updated.";
    }

    private void SaveArt()
    {
        Prompt("Art file name: ");
        var path = Console.ReadLine();
        if (string.IsNullOrEmpty(path))
        {
            _status = "Art save canceled: a file name is required.";
            return;
        }

        try
        {
            File.WriteAllText(path, GetCanvasContent());
            _status = $"Art saved to {path}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            _status = $"Art save failed: {ex.Message}";
        }
    }

    private string GetCanvasContent()
    {
        var lines = new string[_canvasHeight];
        for (var y = 0; y < _canvasHeight; y++)
        {
            var line = "";
            for (var x = 0; x < _canvasWidth; x++)
                line += _cells[x, y];
            lines[y] = line.TrimEnd();
        }

        return string.Join('\n', lines);
    }

    private void RefreshPortraitEntries()
    {
        var sourceContent = File.ReadAllText(_sourcePath);
        _portraitEntries = Enum.GetValues<PortraitDictionary>()
            .SelectMany(dictionary => _source.ParseDictionary(sourceContent, dictionary)
                .Select(entry => (dictionary, entry)))
            .ToArray();

        _portraits.Clear();
        foreach (var item in _portraitEntries)
            _portraits[item.Entry.KeyExpression] = item.Entry.Content;
    }

    private void Prompt(string prompt)
    {
        DrawAll();
        Console.CursorVisible = true;
        Console.ResetColor();
        var promptY = Math.Max(0, Console.WindowHeight - 2);
        var writableWidth = Math.Max(0, Console.WindowWidth - 1);
        Console.SetCursorPosition(0, promptY);
        Console.Write(new string(' ', writableWidth));
        Console.SetCursorPosition(0, promptY);
        Console.Write(prompt);
    }

    private void DrawAll(bool clear = false)
    {
        var layout = CalculateLayout();
        if (layout.Width <= 0 || layout.Height <= 0)
            return;

        _palettePage = Math.Clamp(_palettePage, 0, layout.PalettePageCount - 1);

        Console.CursorVisible = false;
        Console.ResetColor();
        if (clear)
            Console.Clear();
        WriteAt(2, 0, "ASCII PORTRAIT EDITOR", layout.Width - 4);
        WriteAt(2, 1, "Arrows move | Shift+Left/Right switch portrait | Space/D draw | E erase | Delete clear | P glyph | PgUp/PgDn palette | S save | C resize | N new | Esc/Q quit",
            layout.Width - 4);

        for (var y = 2; y < layout.Height - 2; y++)
            WriteAt(layout.SplitX, y, "│", 1);

        DrawCanvasPanel(layout);
        DrawPalettePanel(layout);
        DrawStatus(layout);

        if (layout.Width < _canvasWidth + 5 || layout.Height < _canvasHeight + 8)
            WriteAt(2, Math.Min(2, layout.Height - 1), "Enlarge the terminal to see both framed panels.", layout.Width - 4);
    }

    private void DrawCanvasPanel(EditorLayout layout)
    {
        DrawFrame(layout.CanvasFrameX, layout.CanvasFrameY, layout.CanvasFrameWidth, layout.CanvasFrameHeight);
        WriteAt(layout.CanvasFrameX + 2, layout.CanvasFrameY, " Canvas ", layout.CanvasFrameWidth - 4);
        for (var y = 0; y < _canvasHeight; y++)
        {
            for (var x = 0; x < _canvasWidth; x++)
            {
                var screenX = layout.CanvasFrameX + 1 + x;
                var screenY = layout.CanvasFrameY + 1 + y;
                if (screenX < 0 || screenX >= layout.Width - 1 || screenY < 0 || screenY >= layout.Height - 1)
                    continue;

                Console.SetCursorPosition(screenX, screenY);
                if (_cursorX == x && _cursorY == y)
                    Console.BackgroundColor = ConsoleColor.DarkBlue;
                Console.Write(_cells[x, y]);
                Console.ResetColor();
            }
        }
    }

    private void DrawPalettePanel(EditorLayout layout)
    {
        DrawFrame(layout.PaletteFrameX, layout.PaletteFrameY, layout.PaletteFrameWidth, layout.PaletteFrameHeight);
        var pageCount = layout.PalettePageCount;
        WriteAt(layout.PaletteFrameX + 2, layout.PaletteFrameY,
            $" Palette {_palettePage + 1}/{pageCount} ", layout.PaletteFrameWidth - 4);
        for (var row = 0; row < layout.PaletteRows; row++)
        {
            var y = layout.PaletteItemsTop + row;
            for (var column = 0; column < layout.PaletteColumns; column++)
            {
                var index = _palettePage * layout.PalettePageSize + row * layout.PaletteColumns + column;
                var x = layout.PaletteItemsLeft + column * PaletteCellWidth;
                if (x + PaletteCellWidth >= layout.Width - 1 || y >= layout.Height - 1)
                    continue;

                if (index >= _palette.Length)
                {
                    WriteAt(x, y, new string(' ', PaletteCellWidth), PaletteCellWidth);
                    continue;
                }

                Console.SetCursorPosition(x, y);
                Console.BackgroundColor = index == _paletteIndex ? ConsoleColor.DarkGreen : ConsoleColor.Black;
                Console.Write($" {_palette[index]}  ");
                Console.ResetColor();
            }
        }
    }

    private static void ClearCanvasPanel(EditorLayout layout)
    {
        var blank = new string(' ', Math.Max(0, layout.CanvasFrameWidth));
        for (var row = 0; row < layout.CanvasFrameHeight; row++)
            WriteAt(layout.CanvasFrameX, layout.CanvasFrameY + row, blank, layout.CanvasFrameWidth);
    }

    private void DrawStatus(EditorLayout layout)
    {
        var statusY = Math.Max(2, layout.Height - 2);
        var maximumWidth = Math.Max(0, layout.Width - 4);
        var status = $"Mode: {ModeName}    Brush: '{_brush}'    Position: ({_cursorX},{_cursorY})    {_status}";
        WriteAt(2, statusY, status.PadRight(maximumWidth), maximumWidth);
    }

    private EditorLayout CalculateLayout()
    {
        var width = Console.WindowWidth;
        var height = Console.WindowHeight;
        var splitX = Math.Clamp(width * 2 / 3, 1, Math.Max(1, width - 1));
        var canvasFrameWidth = _canvasWidth + 2;
        var canvasFrameHeight = _canvasHeight + 2;
        var canvasFrameX = Math.Max(1, (splitX - canvasFrameWidth) / 2);
        var canvasFrameY = Math.Max(3, (height - canvasFrameHeight) / 2);
        var paletteFrameX = Math.Min(width - 1, splitX + 1);
        var paletteFrameWidth = Math.Max(3, width - paletteFrameX - 1);
        var paletteFrameY = 2;
        var paletteFrameHeight = Math.Max(3, height - 4);
        var paletteColumns = Math.Max(1, (paletteFrameWidth - 2) / PaletteCellWidth);
        var paletteRows = Math.Max(1, paletteFrameHeight - 4);
        var palettePageSize = paletteColumns * paletteRows;

        return new EditorLayout(width, height, splitX,
            canvasFrameX, canvasFrameY, canvasFrameWidth, canvasFrameHeight,
            paletteFrameX, paletteFrameY, paletteFrameWidth, paletteFrameHeight,
            paletteColumns, paletteRows, palettePageSize, _palette.Length);
    }

    private static void DrawFrame(int x, int y, int width, int height)
    {
        if (width < 2 || height < 2)
            return;

        WriteAt(x, y, $"┌{new string('─', Math.Max(0, width - 2))}┐", width);
        WriteAt(x, y + height - 1, $"└{new string('─', Math.Max(0, width - 2))}┘", width);
        for (var row = 1; row < height - 1; row++)
        {
            WriteAt(x, y + row, "│", 1);
            WriteAt(x + width - 1, y + row, "│", 1);
        }
    }

    private static void WriteAt(int x, int y, string text, int maximumWidth)
    {
        var windowWidth = Console.WindowWidth;
        var windowHeight = Console.WindowHeight;
        if (x < 0 || y < 0 || x >= windowWidth - 1 || y >= windowHeight - 1 || maximumWidth <= 0)
            return;

        var availableWidth = Math.Min(maximumWidth, windowWidth - 1 - x);
        var visible = text.Length > availableWidth ? text[..availableWidth] : text;
        Console.SetCursorPosition(x, y);
        Console.Write(visible);
    }

    private readonly record struct EditorLayout(
        int Width,
        int Height,
        int SplitX,
        int CanvasFrameX,
        int CanvasFrameY,
        int CanvasFrameWidth,
        int CanvasFrameHeight,
        int PaletteFrameX,
        int PaletteFrameY,
        int PaletteFrameWidth,
        int PaletteFrameHeight,
        int PaletteColumns,
        int PaletteRows,
        int PalettePageSize,
        int PaletteLength)
    {
        public int PaletteItemsLeft => PaletteFrameX + 1;
        public int PaletteItemsTop => PaletteFrameY + 3;
        public int PalettePageCount => Math.Max(1, (PaletteLength + PalettePageSize - 1) / PalettePageSize);
    }

    private static string[,] CreateCanvas(int width, int height)
    {
        var cells = new string[width, height];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                cells[x, y] = " ";
        return cells;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int standardHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(IntPtr consoleHandle, out uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleMode(IntPtr consoleHandle, uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadConsoleInput(IntPtr consoleInput, out InputRecord buffer, uint length, out uint eventsRead);

    [StructLayout(LayoutKind.Sequential)]
    private struct Coord
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseEventRecord
    {
        public Coord Position;
        public uint ButtonState;
        public uint ControlKeyState;
        public uint EventFlags;
    }

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode, Size = 20)]
    private struct InputRecord
    {
        [FieldOffset(0)]
        public ushort EventType;

        [FieldOffset(4)]
        public int KeyDown;

        [FieldOffset(8)]
        public ushort RepeatCount;

        [FieldOffset(10)]
        public ushort VirtualKeyCode;

        [FieldOffset(12)]
        public ushort VirtualScanCode;

        [FieldOffset(14)]
        [MarshalAs(UnmanagedType.U2)]
        public char UnicodeChar;

        [FieldOffset(16)]
        public uint ControlKeyState;

        [FieldOffset(4)]
        public MouseEventRecord MouseEvent;
    }
}