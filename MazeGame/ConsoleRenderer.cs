using System.Text;
using MazeGame.Combat;
using MazeGame.Data;
using MazeGame.Domain.Characters;
using MazeGame.Domain.Inventory;

namespace MazeGame;

public sealed class ConsoleRenderer
{
    public const int PlayfieldWidth = 170;
    public const int PlayfieldHeight = 44;
    private const int RightBorderX = PlayfieldWidth;
    private const int BottomBorderY = PlayfieldHeight;
    private static readonly Rune FogSymbol = new('░');
    private static readonly Rune PlayerSymbol = new('☻');
    private const int MessageLineCount = 5;
    private const int MessageWidth = 164;
    private const int PicturePanelTop = 39;
    private const int PicturePanelBottom = BottomBorderY + MessageLineCount;
    private readonly Queue<MessageLogLine> _messageLog = new();
    private readonly GameDataCatalog _gameData;
    private int _mazeLevel;
    private bool _battleActive;
    private LiveCharacter? _displayedCharacter;
    private ConsoleColor? _currentForegroundColor;
    private ConsoleColor? _currentBackgroundColor;

    public ConsoleRenderer(GameDataCatalog gameData) => _gameData = gameData;

    public void DrawInitialState(Maze maze, Player player, FogOfWar fogOfWar, int mazeLevel)
    {
        ResetColorCache();
        _messageLog.Clear();
        _mazeLevel = mazeLevel;
        _battleActive = false;
        Console.Clear();
        DrawPlayfield(maze, fogOfWar);
        DrawFrame();
        RefreshCharacterSheet(player.Character);
        DrawBattleMessage("Találd meg a kijáratot: ⌂");
        DrawPlayer(player.Position);
    }

    public void DrawMovement(Maze maze, FogOfWar fogOfWar, Position previousPosition, Position currentPosition, IReadOnlyList<Position> newlyRevealed, bool hasWon)
    {
        foreach (var position in newlyRevealed) DrawMapCell(maze, fogOfWar, position);
        DrawMapCell(maze, fogOfWar, previousPosition);
        DrawPlayer(currentPosition);
        if (hasWon) DrawBattleMessage("Célba értél! R: új labirintus, Esc: kilépés.");
    }

    public void DrawEnemyMovement(Maze maze, FogOfWar fogOfWar, Position previousPosition, Position currentPosition, Position playerPosition)
    {
        if (previousPosition != playerPosition) DrawMapCell(maze, fogOfWar, previousPosition);
        if (currentPosition != playerPosition) DrawMapCell(maze, fogOfWar, currentPosition);
    }

    public void DrawMapVisibilityChanged(Maze maze, FogOfWar fogOfWar, Position playerPosition)
    {
        for (var y = 0; y < maze.Height; y++)
        for (var x = 0; x < maze.Width; x++) DrawMapCell(maze, fogOfWar, new Position(x, y));
        DrawPlayer(playerPosition);
    }

    public void DrawBattleStarted(Enemy enemy)
    {
        _battleActive = true;
        DrawPicturePanel();
        DrawBattleMessage($"Csata kezdődik! Ellenfél: {enemy.Name}");
    }
    public void DrawTreasureCollected(int goldAmount) => DrawBattleMessage($"Kincsesláda: +{goldAmount} arany!", ConsoleColor.Yellow);
    public void DrawExperienceGained(LevelUpResult result) => DrawBattleMessage(
        result.LeveledUp
            ? $"+{result.GainedExperience} XP! Szintlépés: {result.PreviousLevel} → {result.CurrentLevel}."
            : $"+{result.GainedExperience} XP.",
        result.LeveledUp ? ConsoleColor.Magenta : ConsoleColor.Cyan);
    public void DrawBattleRound(BattleLogEntry entry)
    {
        var color = entry.Kind switch
        {
            BattleLogKind.PlayerAttack => ConsoleColor.Green,
            BattleLogKind.EnemyAttack => ConsoleColor.Red,
            _ => ConsoleColor.Cyan
        };
        DrawBattleMessage(entry.Message, color);
        WriteSheetLine(38, "Szóköz: következő kör", ConsoleColor.DarkYellow);
    }
    public void DrawBattleResult(BattleResult result, Enemy enemy)
    {
        _battleActive = false;
        DrawPicturePanel();
        if (_displayedCharacter is not null)
            WriteSheetLine(38, $"XP szorzó: {_displayedCharacter.CharacterClass.ExperienceModifier:0.00}×", ConsoleColor.DarkCyan);
        var lastEvent = result.Events.LastOrDefault() ?? "";
        DrawBattleMessage(result.PlayerWon
            ? $"Győzelem {result.Rounds} kör után! {lastEvent}"
            : $"Elestél {result.Rounds} kör után. {lastEvent}");
    }

    public void DrawGameOver(string characterName)
    {
        ResetColorCache();
        Console.Clear();

        const int frameWidth = 96;
        var left = Math.Max(0, (Console.WindowWidth - frameWidth) / 2);
        var top = Math.Max(2, (Console.WindowHeight - 11) / 2);
        var lines = new[]
        {
            "💀  JÁTÉK VÉGE  💀",
            string.Empty,
            $"{characterName}, elestél a labirintus mélyén.",
            "A szörnyek tovább kísértenek a sötét folyosókon,",
            "amíg újabb bátor hősök nem érkeznek, hogy kihívják őket.",
            "👻 Talán a következő hős te leszel... ⚔️",
            string.Empty,
            "Nyomj meg egy billentyűt a főmenühöz."
        };

        SetColors(ConsoleColor.DarkRed, ConsoleColor.Black);
        WriteAt(left, top, "╔" + new string('═', frameWidth - 2) + "╗");
        for (var index = 0; index < lines.Length; index++)
        {
            SetColors(ConsoleColor.DarkRed, ConsoleColor.Black);
            WriteAt(left, top + index + 1, "║");
            SetColors(index == 0 ? ConsoleColor.Red : index == lines.Length - 1 ? ConsoleColor.Yellow : ConsoleColor.Gray, ConsoleColor.Black);
            WriteAt(left + 2, top + index + 1, lines[index].PadRight(frameWidth - 4));
            SetColors(ConsoleColor.DarkRed, ConsoleColor.Black);
            WriteAt(left + frameWidth - 1, top + index + 1, "║");
        }
        SetColors(ConsoleColor.DarkRed, ConsoleColor.Black);
        WriteAt(left, top + lines.Length + 1, "╚" + new string('═', frameWidth - 2) + "╝");
        Console.ReadKey(intercept: true);
    }
    public void DrawDeveloperMessage(string message) => DrawBattleMessage(message);

    public void DrawMapCellAfterBattle(Maze maze, FogOfWar fogOfWar, Position battlePosition, Position playerPosition)
    {
        if (battlePosition != playerPosition) DrawMapCell(maze, fogOfWar, battlePosition);
        DrawPlayer(playerPosition);
    }

    /// <summary>Csak a jobb oldali karakterlapot rajzolja újra, a játéktér érintése nélkül.</summary>
    public void RefreshCharacterSheet(LiveCharacter character)
    {
        DrawCharacterSheet(character);
    }

    private void DrawPlayfield(Maze maze, FogOfWar fogOfWar)
    {
        for (var y = 0; y < maze.Height; y++)
        {
            Console.SetCursorPosition(0, y);
            for (var x = 0; x < maze.Width; x++) DrawMapRune(maze, fogOfWar, new Position(x, y));
        }
    }

    private void DrawFrame()
    {
        SetColors(ConsoleColor.DarkCyan, ConsoleColor.Black);
        for (var y = 0; y <= PicturePanelBottom; y++) WriteAt(RightBorderX, y, "│");
        Console.SetCursorPosition(0, BottomBorderY);
        Console.Write(new string('─', PlayfieldWidth));
        Console.Write('┤');
    }

    private void DrawCharacterSheet(LiveCharacter character)
    {
        _displayedCharacter = character;
        WriteSheetLine(2, "KARAKTERLAP", ConsoleColor.Yellow);
        WriteSheetLine(3, character.Name, ConsoleColor.Cyan);
        WriteSheetLine(4, $"{character.Race.Name} {character.CharacterClass.Name}", ConsoleColor.White);
        WriteSheetLine(5, $"Labirintus: {_mazeLevel}", ConsoleColor.Green);
        WriteSheetLine(6, FormatExperience(character), ConsoleColor.Cyan);
        WriteSheetLine(7, $"Erő: {character.Abilities.Strength}", ConsoleColor.Red);
        WriteSheetLine(8, $"Ügy: {character.Abilities.Dexterity}", ConsoleColor.Green);
        WriteSheetLine(9, $"Egs: {character.Abilities.Health}", ConsoleColor.DarkYellow);
        WriteSheetLine(10, $"Int: {character.Abilities.Intelligence}", ConsoleColor.Magenta);
        WriteSheetLine(11, $"HP: {character.CurrentVitality}/{character.MaximumVitality}", ConsoleColor.Red);
        WriteSheetLine(12, character.UsesMana ? $"Manna: {character.CurrentMana}/{character.MaximumMana}" : "Manna: nincs", ConsoleColor.Blue);
        WriteSheetLine(13, $"É: {ResourceIcons("🍖", character.FoodLevel)}", ConsoleColor.Yellow);
        WriteSheetLine(14, $"V: {ResourceIcons("💧", character.WaterLevel)}", ConsoleColor.Cyan);
        WriteSheetLine(15, $"Arany: {character.Gold} 🪙", ConsoleColor.Yellow);
        WriteSheetLine(16, "FEGYVEREK", ConsoleColor.Yellow);
        WriteSheetLine(17, $"1: {ItemName(character.WeaponSlots[0])}", ConsoleColor.Gray);
        WriteSheetLine(18, $"2: {ItemName(character.WeaponSlots[1])}", ConsoleColor.Gray);
        WriteSheetLine(19, $"Páncél: {ItemName(character.Armor)}", ConsoleColor.DarkYellow);
        WriteSheetLine(21, $"VARÁZSTÁRGYAK {character.MagicItems.Count}/3", ConsoleColor.Magenta);
        for (var index = 0; index < 3; index++)
            WriteSheetLine(22 + index, $"{index + 1}: {ItemName(index < character.MagicItems.Count ? character.MagicItems[index] : null)}", ConsoleColor.Gray);
        WriteSheetLine(26, $"HÁTIZSÁK {character.Backpack.Count}/10", ConsoleColor.DarkCyan);
        for (var index = 0; index < 10; index++)
            WriteSheetLine(27 + index, $"{index + 1}: {ItemName(index < character.Backpack.Count ? character.Backpack[index] : null)}", ConsoleColor.Gray);
        WriteSheetLine(37, character.IsAlive ? "Állapot: élő" : "Állapot: halott", character.IsAlive ? ConsoleColor.Green : ConsoleColor.Red);
        WriteSheetLine(38, $"XP szorzó: {character.CharacterClass.ExperienceModifier:0.00}×", ConsoleColor.DarkCyan);
        DrawPicturePanel();
    }

    private void DrawBattleMessage(string message, ConsoleColor color = ConsoleColor.Gray)
    {
        foreach (var line in WrapMessage(message)) _messageLog.Enqueue(new MessageLogLine(line, color));
        while (_messageLog.Count > MessageLineCount) _messageLog.Dequeue();

        var messages = _messageLog.ToArray();
        for (var index = 0; index < MessageLineCount; index++)
        {
            var messageLine = index < messages.Length ? messages[index] : new MessageLogLine(string.Empty, ConsoleColor.Gray);
            SetColors(messageLine.Color, ConsoleColor.Black);
            var text = messageLine.Text;
            WriteAt(2, BottomBorderY + 1 + index, text.PadRight(MessageWidth));
        }
    }

    private static IEnumerable<string> WrapMessage(string message)
    {
        while (message.Length > MessageWidth)
        {
            var splitAt = message.LastIndexOf(' ', MessageWidth);
            if (splitAt <= 0) splitAt = MessageWidth;
            yield return message[..splitAt];
            message = message[splitAt..].TrimStart();
        }

        yield return message;
    }

    private sealed record MessageLogLine(string Text, ConsoleColor Color);

    private static string ItemName(IItemDefinition? item) => item?.Name ?? "üres";
    private string FormatExperience(LiveCharacter character) => character.GetNextLevelExperience(_gameData.ExperienceByLevel) is { } next
        ? $"Szint: {character.Level}  XP: {character.Experience}/{next}"
        : $"Szint: {character.Level}  XP: MAX";
    private static string ResourceIcons(string icon, int level) => string.Concat(Enumerable.Repeat(icon, level / 10));

    private void DrawPicturePanel()
    {
        var kind = _battleActive ? AsciiPortraitKind.Skeleton : AsciiPortraitKind.Warrior;
        var color = _battleActive ? ConsoleColor.Red : ConsoleColor.Cyan;
        var portrait = AsciiPortraits.Get(kind);
        WriteSheetLine(PicturePanelTop, "┌────────── KÉP ──────────┐", ConsoleColor.DarkCyan);
        for (var index = 0; index < portrait.Lines.Count; index++)
            WriteSheetLine(PicturePanelTop + index + 1, $"│{CenterPanelText(portrait.Lines[index], portrait.CanvasWidth)}│", color);
        WriteSheetLine(PicturePanelBottom, "└─────────────────────────┘", ConsoleColor.DarkCyan);
    }

    private static string CenterPanelText(string text, int canvasWidth)
    {
        const int interiorWidth = 25;
        var canvas = text.PadRight(canvasWidth);
        var leftPadding = Math.Max(0, (interiorWidth - canvasWidth) / 2);
        return (new string(' ', leftPadding) + canvas).PadRight(interiorWidth);
    }

    private void WriteSheetLine(int y, string text, ConsoleColor foregroundColor)
    {
        const int maximumWidth = 27;
        var clippedText = text.Length <= maximumWidth ? text : text[..maximumWidth];
        SetColors(foregroundColor, ConsoleColor.Black);
        WriteAt(172, y, clippedText.PadRight(maximumWidth));
    }

    private void DrawMapCell(Maze maze, FogOfWar fogOfWar, Position position)
    {
        Console.SetCursorPosition(position.X, position.Y);
        DrawMapRune(maze, fogOfWar, position);
    }

    private void DrawMapRune(Maze maze, FogOfWar fogOfWar, Position position) =>
        WriteRuneWithColor(
            fogOfWar.IsVisible(position) ? maze.GetObjectAt(position)?.Symbol ?? maze.Tiles[position.X, position.Y] : FogSymbol,
            fogOfWar.IsVisible(position) ? GetForegroundColor(maze, position) : ConsoleColor.Black,
            fogOfWar.IsVisible(position) ? ConsoleColor.Black : ConsoleColor.DarkBlue);

    private void DrawPlayer(Position position)
    {
        Console.SetCursorPosition(position.X, position.Y);
        WriteRuneWithColor(PlayerSymbol, ConsoleColor.Cyan, ConsoleColor.Black);
    }

    private static void WriteAt(int x, int y, string text)
    {
        Console.SetCursorPosition(x, y);
        Console.Write(text);
    }

    private static ConsoleColor GetForegroundColor(Maze maze, Position position)
    {
        var mapObject = maze.GetObjectAt(position);
        if (mapObject is TreasureChest) return ConsoleColor.Yellow;
        if (mapObject is Enemy) return ConsoleColor.Red;
        if (mapObject is Corpse) return ConsoleColor.DarkRed;

        return maze.Tiles[position.X, position.Y] switch
        {
            var tile when tile == Maze.Wall => ConsoleColor.DarkGray,
            var tile when tile == Maze.Door => ConsoleColor.DarkYellow,
            var tile when tile == Maze.ExitMarker => ConsoleColor.Green,
            _ => ConsoleColor.Black
        };
    }

    private void WriteRuneWithColor(Rune rune, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
    {
        SetColors(foregroundColor, backgroundColor);
        Console.Write(rune.ToString());
    }

    private void SetColors(ConsoleColor foregroundColor, ConsoleColor backgroundColor)
    {
        if (_currentForegroundColor != foregroundColor)
        {
            Console.ForegroundColor = foregroundColor;
            _currentForegroundColor = foregroundColor;
        }

        if (_currentBackgroundColor != backgroundColor)
        {
            Console.BackgroundColor = backgroundColor;
            _currentBackgroundColor = backgroundColor;
        }
    }

    private void ResetColorCache()
    {
        Console.ResetColor();
        _currentForegroundColor = null;
        _currentBackgroundColor = null;
    }
}
