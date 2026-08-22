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
    private const int MessageLineCount = 5;
    private const int MessageWidth = 164;
    private const int PicturePanelHeight = 5;
    private const int PicturePanelBottom = BottomBorderY + MessageLineCount;
    private const int PicturePanelTop = PicturePanelBottom - PicturePanelHeight - 1;
    private readonly Queue<MessageLogLine> _messageLog = new();
    private readonly GameDataCatalog _gameData;
    private readonly Party _party;
    private int _mazeLevel;
    private bool _battleActive;
    private LiveCharacter? _displayedCharacter;
    private ConsoleColor? _currentForegroundColor;
    private ConsoleColor? _currentBackgroundColor;

    public ConsoleRenderer(GameDataCatalog gameData, Party party)
    {
        _gameData = gameData;
        _party = party;
    }

    /// <summary>
    /// Inicializálja a teljes képernyős állapotot: törli a konzolt, kirajzolja a pályát,
    /// keretet, karakterlapot és a játékos pozícióját.
    /// </summary>
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

    /// <summary>
    /// Rajzolja a játékos mozgását: frissíti az újonnan felfedett mezőket,
    /// az előző pozíciót, és a jelenlegi játékos pozíciót.
    /// </summary>
    public void DrawMovement(Maze maze, FogOfWar fogOfWar, Position previousPosition, Position currentPosition, IReadOnlyList<Position> newlyRevealed, bool hasWon)
    {
        foreach (var position in newlyRevealed) DrawMapCell(maze, fogOfWar, position);
        DrawMapCell(maze, fogOfWar, previousPosition);
        DrawPlayer(currentPosition);
        if (hasWon) DrawBattleMessage("Célba értél! R: új labirintus, Esc: kilépés.");
    }

    /// <summary>
    /// Ellenség mozgásának kirajzolása: frissíti a korábbi és az aktuális mezőt,
    /// kivéve ha azok a játékos pozíciója (mivel a játékos karakterét külön kezeljük).
    /// </summary>
    public void DrawEnemyMovement(Maze maze, FogOfWar fogOfWar, Position previousPosition, Position currentPosition, Position playerPosition)
    {
        if (previousPosition != playerPosition) DrawMapCell(maze, fogOfWar, previousPosition);
        if (currentPosition != playerPosition) DrawMapCell(maze, fogOfWar, currentPosition);
    }

    /// <summary>
    /// A teljes térképet újrarajzolja a jelenlegi láthatósági állapot alapján.
    /// Hasznos, ha a látótér jelentősen megváltozott (pl. fényforrások).
    /// </summary>
    public void DrawMapVisibilityChanged(Maze maze, FogOfWar fogOfWar, Position playerPosition)
    {
        for (var y = 0; y < maze.Height; y++)
        for (var x = 0; x < maze.Width; x++) DrawMapCell(maze, fogOfWar, new Position(x, y));
        DrawPlayer(playerPosition);
    }

    /// <summary>
    /// Csata kezdetét jelző megjelenítés: kapcsolja a csata állapotát és kirajzolja a kép-panelt.
    /// </summary>
    public void DrawBattleStarted(Enemy enemy)
    {
        _battleActive = true;
        DrawPicturePanel();
        DrawBattleMessage($"Csata kezdődik! Ellenfél: {enemy.Name}");
    }
    /// <summary>Kincs felvétele esetén rövid üzenet a battle/message panelre.</summary>
    public void DrawTreasureCollected(int goldAmount) => DrawBattleMessage($"Kincsesláda: +{goldAmount} arany!", ConsoleColor.Yellow);
    /// <summary>Tapasztalati pont szerzés és esetleges szintlépés megjelenítése.</summary>
    public void DrawExperienceGained(LevelUpResult result) => DrawBattleMessage(
        result.LeveledUp
            ? $"+{result.GainedExperience} XP! Szintlépés: {result.PreviousLevel} → {result.CurrentLevel}."
            : $"+{result.GainedExperience} XP.",
        result.LeveledUp ? ConsoleColor.Magenta : ConsoleColor.Cyan);

    /// <summary>
    /// Csataround naplóbejegyzés megjelenítése. A napló színezése a bejegyzés típusától függ.
    /// </summary>
    public void DrawBattleRound(BattleLogEntry entry)
    {
        var color = entry.Kind switch
        {
            BattleLogKind.PlayerAttack => ConsoleColor.Green,
            BattleLogKind.EnemyAttack => ConsoleColor.Red,
            _ => ConsoleColor.Cyan
        };
        DrawBattleMessage(entry.Message, color);
        // A jobb oldali karakterlapon megjelenített sor: információ a vezérlésről.
        WriteSheetLine(42, "Szóköz: következő kör", ConsoleColor.DarkYellow);
    }

    /// <summary>
    /// Csata eredményének megjelenítése: visszaáll a nem-csata állapotra és kiírja az összegző üzenetet.
    /// </summary>
    public void DrawBattleResult(BattleResult result, Enemy enemy)
    {
        _battleActive = false;
        DrawPicturePanel();
        WriteSheetLine(42, string.Empty, ConsoleColor.DarkCyan);
        var lastEvent = result.Events.LastOrDefault() ?? "";
        DrawBattleMessage(result.PlayerWon
            ? $"Győzelem {result.Rounds} kör után! {lastEvent}"
            : $"Elestél {result.Rounds} kör után. {lastEvent}");
    }

    /// <summary>
    /// Játék vége képernyő: középre rajzolt keret és szöveg, majd várakozás billentyűleütésre.
    /// </summary>
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
    /// <summary>Fejlesztői üzenetek gyors megjelenítésére szolgál (battle message panelre).</summary>
    public void DrawDeveloperMessage(string message) => DrawBattleMessage(message);
    public void DrawDoorMessage(string message, ConsoleColor color = ConsoleColor.DarkYellow) => DrawBattleMessage(message, color);

    /// <summary>
    /// Szintlépés képernyő: összegzi a kapott bónuszokat, és ha vannak tehetség-ajánlatok,
    /// megjeleníti őket választásra.
    /// </summary>
    public IReadOnlyList<PerkDefinition> DrawLevelUpScreen(LiveCharacter character, LevelUpResult result, IReadOnlyList<PerkOffer> perkOffers)
    {
        var selectedPerks = new List<PerkDefinition>();
        DrawLevelUpSummary(character, result, perkOffers.Count > 0);
        if (perkOffers.Count == 0)
        {
            Console.ReadKey(intercept: true);
            return selectedPerks;
        }

        foreach (var offer in perkOffers)
            selectedPerks.Add(DrawPerkChoice(character, offer));
        return selectedPerks;
    }

    /// <summary>
    /// Kirajzol egy középre igazított összegző keretet a szintlépéshez.
    /// </summary>
    private void DrawLevelUpSummary(LiveCharacter character, LevelUpResult result, bool hasPerkOffer)
    {
        ResetColorCache();
        Console.Clear();
        const int frameWidth = 88;
        var detailLines = result.Bonuses.Select(bonus => character.UsesMana
            ? $"⭐ {bonus.Level}. szint:  ❤️ +{bonus.Vitality} HP     🔷 +{bonus.Mana} manna"
            : $"⭐ {bonus.Level}. szint:  ❤️ +{bonus.Vitality} HP").ToList();
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("✨🏆✨  SZINTLÉPÉS!  ✨🏆✨", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray),
            ($"⚔️  {character.Name} új ereje felébredt!", ConsoleColor.Cyan),
            ($"📜  {result.PreviousLevel}. szint  ➜  {result.CurrentLevel}. szint", ConsoleColor.Magenta),
            (string.Empty, ConsoleColor.Gray)
        };
        lines.AddRange(detailLines.Select(line => (line, ConsoleColor.Green)));
        lines.Add((string.Empty, ConsoleColor.Gray));
        lines.Add((character.UsesMana
            ? $"💖 Összes növekedés: +{result.VitalityGained} HP   💠 +{result.ManaGained} manna"
            : $"💖 Összes növekedés: +{result.VitalityGained} HP", ConsoleColor.White));
        lines.Add(($"🛡️  Jelenlegi értékek: {character.CurrentVitality}/{character.MaximumVitality} HP" +
            (character.UsesMana ? $"   {character.CurrentMana}/{character.MaximumMana} manna" : string.Empty), ConsoleColor.Cyan));
        lines.Add((string.Empty, ConsoleColor.Gray));
        lines.Add((hasPerkOffer
            ? "🌠 Új TEHETSÉG ébred benned! Nyomj meg egy billentyűt... 🌠"
            : "🌟 Nyomj meg egy billentyűt a kaland folytatásához! 🌟", ConsoleColor.Yellow));

        DrawCenteredFrame(frameWidth, lines);
        if (hasPerkOffer) Console.ReadKey(intercept: true);
    }

    /// <summary>
    /// Két tehetség közül választó képernyő: bal/jobb vagy fel/le billentyűkkel választ,
    /// Enterrel véglegesít. Visszaadja a kiválasztott tehetséget.
    /// </summary>
    private PerkDefinition DrawPerkChoice(LiveCharacter character, PerkOffer offer)
    {
        var selectedIndex = 0;
        while (true)
        {
            ResetColorCache();
            Console.Clear();
            const int frameWidth = 112;
            var first = offer.Choices[0];
            var second = offer.Choices[1];
            var lines = new List<(string Text, ConsoleColor Color)>
            {
                ("🌟⚔️🌟  TEHETSÉGVÁLASZTÁS  🌟⚔️🌟", ConsoleColor.Yellow),
                (string.Empty, ConsoleColor.Gray),
                ($"{character.Name} — {character.CharacterClass.Name} — {offer.Tier}. fokozat", ConsoleColor.Cyan),
                ($"A tehetség a {offer.TriggerLevel}. szint elérésekor vált elérhetővé.", ConsoleColor.DarkCyan),
                ("A nem választott tehetség végleg elveszik ennél a karakternél.", ConsoleColor.Red),
                (string.Empty, ConsoleColor.Gray),
                ($"{(selectedIndex == 0 ? "▶" : " ")}  🟥 {first.Name}", selectedIndex == 0 ? ConsoleColor.Yellow : ConsoleColor.Gray),
                ($"     {first.Description}", selectedIndex == 0 ? ConsoleColor.White : ConsoleColor.DarkGray),
                (string.Empty, ConsoleColor.Gray),
                ($"{(selectedIndex == 1 ? "▶" : " ")}  🟦 {second.Name}", selectedIndex == 1 ? ConsoleColor.Yellow : ConsoleColor.Gray),
                ($"     {second.Description}", selectedIndex == 1 ? ConsoleColor.White : ConsoleColor.DarkGray),
                (string.Empty, ConsoleColor.Gray),
                ("⬅️  Bal/jobb vagy fel/le: választás     ✅ Enter: véglegesítés", ConsoleColor.Green)
            };
            DrawCenteredFrame(frameWidth, lines);

            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.LeftArrow:
                case ConsoleKey.UpArrow:
                    selectedIndex = 0;
                    break;
                case ConsoleKey.RightArrow:
                case ConsoleKey.DownArrow:
                    selectedIndex = 1;
                    break;
                case ConsoleKey.Enter:
                    return offer.Choices[selectedIndex];
            }
        }
    }

    /// <summary>
    /// Rajzol egy középre igazított keretet megadott szélességgel és sorokkal.
    /// Belső szöveg- és színbeállításokat kezel.
    /// </summary>
    private void DrawCenteredFrame(int frameWidth, IReadOnlyList<(string Text, ConsoleColor Color)> lines)
    {
        const int horizontalPadding = 2;
        var contentWidth = frameWidth - horizontalPadding * 2;

        var left = Math.Max(0, (Console.WindowWidth - frameWidth) / 2);
        var top = Math.Max(1, (Console.WindowHeight - lines.Count - 2) / 2);
        SetColors(ConsoleColor.Magenta, ConsoleColor.Black);
        WriteAt(left, top, "╔" + new string('═', frameWidth - 2) + "╗");
        for (var index = 0; index < lines.Count; index++)
        {
            SetColors(ConsoleColor.Magenta, ConsoleColor.Black);
            WriteAt(left, top + index + 1, "║");
            SetColors(lines[index].Color, ConsoleColor.Black);
            var text = lines[index].Text;
            WriteAt(left + horizontalPadding, top + index + 1, text.PadRight(Math.Max(0, contentWidth - text.Length)));
            SetColors(ConsoleColor.Magenta, ConsoleColor.Black);
            WriteAt(left + frameWidth - 1, top + index + 1, "║");
        }
        SetColors(ConsoleColor.Magenta, ConsoleColor.Black);
        WriteAt(left, top + lines.Count + 1, "╚" + new string('═', frameWidth - 2) + "╝");
    }

    /// <summary>
    /// Csata után frissíti a csatatér mezőjét (ha nem a játékos mezője volt) és a játékost.
    /// </summary>
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

    /// <summary>
    /// Kirajzolja a teljes játéktér rácsát a megadott labirintus alapján.
    /// </summary>
    private void DrawPlayfield(Maze maze, FogOfWar fogOfWar)
    {
        for (var y = 0; y < maze.Height; y++)
        {
            Console.SetCursorPosition(0, y);
            for (var x = 0; x < maze.Width; x++) DrawMapRune(maze, fogOfWar, new Position(x, y));
        }
    }

    /// <summary>
    /// A jobb oldali függőleges keret és alja/határainek kirajzolása a teljes ablakhoz.
    /// </summary>
    private void DrawFrame()
    {
        SetColors(ConsoleColor.DarkCyan, ConsoleColor.Black);
        for (var y = 0; y <= PicturePanelBottom; y++) WriteAt(RightBorderX, y, "│");
        Console.SetCursorPosition(0, BottomBorderY);
        Console.Write(new string('─', PlayfieldWidth));
        Console.Write('┤');
    }

    /// <summary>
    /// Teljes karakterlap rajzolása a jobb oldali panelre. Minden sor a WriteSheetLine segítségével kerül oda.
    /// </summary>
    private void DrawCharacterSheet(LiveCharacter character)
    {
        _displayedCharacter = character;
        WriteSheetLine(1, "KARAKTERLAP - ", ConsoleColor.Yellow, character.Name, character.Color);
        WriteSheetLine(2, $"{character.Race.Name} {character.CharacterClass.Name}", ConsoleColor.White);
        WriteSheetLine(3, FormatCompactList("Teh", character.Perks.Select(perk => perk.Name)), ConsoleColor.Magenta);
        WriteSheetLine(4, FormatCompactList("Áll", character.Statuses.Select(status => status.Name)), character.Statuses.Count > 0 ? ConsoleColor.Red : ConsoleColor.DarkGray);
        WriteSheetLine(6, $"Labirintus: {_mazeLevel}", ConsoleColor.Green);
        WriteSheetLine(7, FormatExperience(character), ConsoleColor.Cyan);
        WriteSheetLine(8, $"Erő: {character.Abilities.Strength}", ConsoleColor.Red);
        WriteSheetLine(9, $"Ügy: {character.Abilities.Dexterity}", ConsoleColor.Green);
        WriteSheetLine(10, $"Egs: {character.Abilities.Health}", ConsoleColor.DarkYellow);
        WriteSheetLine(11, $"Int: {character.Abilities.Intelligence}", ConsoleColor.Magenta);
        WriteSheetLine(12, $"HP: {character.CurrentVitality}/{character.MaximumVitality}", ConsoleColor.Red);
        WriteSheetLine(13, character.UsesMana ? $"Manna: {character.CurrentMana}/{character.MaximumMana}" : "Manna: nincs", ConsoleColor.Blue);
        WriteSheetLine(14, $"É: {ResourceIcons("🍖", character.FoodLevel)}", ConsoleColor.Yellow);
        WriteSheetLine(15, $"V: {ResourceIcons("💧", character.WaterLevel)}", ConsoleColor.Cyan);
        WriteSheetLine(16, $"Arany: {character.Gold} 🪙", ConsoleColor.Yellow);
        WriteSheetLine(18, "FEGYVEREK", ConsoleColor.Yellow);
        WriteSheetLine(19, $"1: {ItemName(character.WeaponSlots[0])}", ConsoleColor.Gray);
        WriteSheetLine(20, $"2: {ItemName(character.WeaponSlots[1])}", ConsoleColor.Gray);
        WriteSheetLine(21, $"Páncél: {ItemName(character.Armor)}", ConsoleColor.DarkYellow);
        WriteSheetLine(23, $"VARÁZSTÁRGYAK {character.MagicItems.Count}/3", ConsoleColor.Magenta);
        for (var index = 0; index < 3; index++)
            WriteSheetLine(24 + index, $"{index + 1}: {ItemName(index < character.MagicItems.Count ? character.MagicItems[index] : null)}", ConsoleColor.Gray);
        WriteSheetLine(27, $"HÁTIZSÁK {character.Backpack.Count}/10", ConsoleColor.DarkCyan);
        for (var index = 0; index < 10; index++)
            WriteSheetLine(28 + index, $"{index + 1}: {ItemName(index < character.Backpack.Count ? character.Backpack[index] : null)}", ConsoleColor.Gray);
        var companions = _party.Members.Where(member => member != character).Take(3).ToList();
        for (var index = 0; index < 3; index++)
            WriteSheetLine(39 + index, index < companions.Count ? FormatPartyMember(companions[index]) : string.Empty,
                index < companions.Count ? companions[index].Color : ConsoleColor.DarkGray);
        WriteSheetLine(42, string.Empty, ConsoleColor.DarkGray);
        DrawPicturePanel();
    }

    /// <summary>
    /// Battle/message panelre egy új bejegyzést ír: a hosszú üzeneteket megtöri, és
    /// az utolsó N (MessageLineCount) bejegyzést jeleníti meg a képernyő alsó részén.
    /// </summary>
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

    /// <summary>
    /// Segéd: megtöri a hosszú üzenetet MessageWidth szélességre szóközök mentén.
    /// </summary>
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

    private static string FormatPartyMember(LiveCharacter character)
    {
        const int maximumWidth = 27;
        var classInitial = character.CharacterClass.Name.EnumerateRunes().First().ToString().ToUpperInvariant();
        var suffix = $" L{character.Level} {character.CurrentVitality}/{character.MaximumVitality}";
        var maximumNameLength = Math.Max(1, maximumWidth - classInitial.Length - 1 - suffix.Length);
        var name = character.Name[..Math.Min(character.Name.Length, maximumNameLength)];
        return $"{classInitial} {name}{suffix}";
    }

    private static string FormatCompactList(string label, IEnumerable<string> values)
    {
        const int maximumWidth = 27;
        var names = values.ToList();
        if (names.Count == 0) return $"{label}: nincs";
        var prefix = $"{label}: ";
        var separatorsWidth = (names.Count - 1) * 2;
        var availablePerName = Math.Max(1, (maximumWidth - prefix.Length - separatorsWidth) / names.Count);
        var shortenedNames = names.Select(name => name.Length <= availablePerName ? name : name[..availablePerName]);
        return prefix + string.Join(", ", shortenedNames);
    }

    /// <summary>
    /// A jobb oldali kép-panel (ASCII portré) kirajzolása. A PicturePanelTop-ról indul,
    /// és a WriteSheetLine metódussal írja ki a keretet és a képsorokat.
    /// </summary>
    private void DrawPicturePanel()
    {
        var kind = _battleActive ? AsciiPortraitKind.Skeleton : AsciiPortraitKind.Warrior;
        var color = _battleActive ? ConsoleColor.Red : ConsoleColor.Cyan;
        var portrait = AsciiPortraits.Get(kind);
        WriteSheetLine(PicturePanelTop, "┌────────── KÉP ──────────┐", ConsoleColor.DarkCyan);
        for (var index = 0; index < PicturePanelHeight; index++)
        {
            var line = index < portrait.Lines.Count ? portrait.Lines[index] : string.Empty;
            WriteSheetLine(PicturePanelTop + index + 1, $"│{CenterPanelText(line, portrait.CanvasWidth)}│", color);
        }
        WriteSheetLine(PicturePanelBottom, "└─────────────────────────┘", ConsoleColor.DarkCyan);
    }

    private static string CenterPanelText(string text, int canvasWidth)
    {
        const int interiorWidth = 25;
        var canvas = text.PadRight(canvasWidth);
        var leftPadding = Math.Max(0, (interiorWidth - canvasWidth) / 2);
        return (new string(' ', leftPadding) + canvas).PadRight(interiorWidth);
    }

    /// <summary>
    /// A jobb oldali karakterpanel egy sorába ír. Fontos: a tényleges X koordináta
    /// konstansan 172, és a maximális szélesség 27 karakter (azaz a jobb panel fixelt).
    /// A metódus beállítja a színeket, levágja a túl hosszú szöveget és jobbra/padra ír.
    /// </summary>
    private void WriteSheetLine(int y, string text, ConsoleColor foregroundColor)
    {
        const int maximumWidth = 27;
        var clippedText = text.Length <= maximumWidth ? text : text[..maximumWidth];
        SetColors(foregroundColor, ConsoleColor.Black);
        // Itt történik a tényleges kiírás: balra igazított, maximum 'maximumWidth' karakter,
        // és X=172 lesz (a jobb oldali karakterlap kezdő X pozíciója).
        WriteAt(172, y, clippedText.PadRight(maximumWidth));
    }

    /// <summary>
    /// Két szöveget ír ki egymás mellé a jobb oldali karakterlapra, két külön színnel.
    /// A teljes sor hossza nem haladja meg a maximum 27 karaktert — ha szükséges,
    /// levágja a szövegeket úgy, hogy mindkét rész látható maradjon lehetőleg.
    /// </summary>
    private void WriteSheetLine(int y, string leftText, ConsoleColor leftColor, string rightText, ConsoleColor rightColor)
    {
        const int maximumWidth = 27;

        // Alap felosztás: fele-fele, de dinamikusan kiegészítjük ha az egyik rövidebb
        var leftMax = maximumWidth / 2; // 13
        var rightMax = maximumWidth - leftMax; // 14

        string leftClipped;
        string rightClipped;

        if (leftText.Length <= leftMax)
        {
            leftClipped = leftText;
            var remaining = maximumWidth - leftClipped.Length;
            rightClipped = rightText.Length <= remaining ? rightText : rightText[..remaining];
        }
        else if (rightText.Length <= rightMax)
        {
            rightClipped = rightText;
            var remaining = maximumWidth - rightClipped.Length;
            leftClipped = leftText.Length <= remaining ? leftText : leftText[..remaining];
        }
        else
        {
            leftClipped = leftText[..leftMax];
            rightClipped = rightText.Length <= rightMax ? rightText : rightText[..rightMax];
        }

        // Kiírás: először a bal oldali rész, majd a jobb oldali közvetlenül utána
        SetColors(leftColor, ConsoleColor.Black);
        var leftPadded = leftClipped.PadRight(leftClipped.Length);
        WriteAt(172, y, leftPadded);

        SetColors(rightColor, ConsoleColor.Black);
        var secondX = 172 + leftPadded.Length;
        var remainingWidth = maximumWidth - leftPadded.Length;
        var rightPadded = rightClipped.PadRight(remainingWidth);
        WriteAt(secondX, y, rightPadded);
    }

    /// <summary>
    /// Megadott mező kirajzolása a kurzor mozgatásával, majd a megfelelő Rune kiírásával.
    /// </summary>
    private void DrawMapCell(Maze maze, FogOfWar fogOfWar, Position position)
    {
        Console.SetCursorPosition(position.X, position.Y);
        DrawMapRune(maze, fogOfWar, position);
    }

    /// <summary>
    /// Kiírja a mezőre vonatkozó Rune-t a megfelelő színekkel, figyelve a köd/láthatóság állapotára.
    /// </summary>
    private void DrawMapRune(Maze maze, FogOfWar fogOfWar, Position position) =>
        WriteRuneWithColor(
            fogOfWar.IsVisible(position) ? maze.GetObjectAt(position)?.Symbol ?? maze.Tiles[position.X, position.Y] : FogSymbol,
            fogOfWar.IsVisible(position) ? GetForegroundColor(maze, position) : ConsoleColor.DarkGray,
            fogOfWar.IsVisible(position) ? ConsoleColor.Black : ConsoleColor.DarkBlue);

    /// <summary>
    /// Játékos karakterének kirajzolása: fix szimbólum és szín a kurzor aktuális pozíciójára.
    /// </summary>
    private void DrawPlayer(Position position)
    {
        var character = _displayedCharacter ?? throw new InvalidOperationException("A főkarakter rajzolása előtt a karakterlapot inicializálni kell.");
        Console.SetCursorPosition(position.X, position.Y);
        var symbol = Rune.GetRuneAt(character.CharacterClass.Name.ToUpperInvariant(), 0);
        WriteRuneWithColor(symbol, character.Color, ConsoleColor.Black);
    }

    /// <summary>
    /// Egyszerű segéd: kiír egy tetszőleges szöveget adott X,Y koordinátára a konzolon.
    /// </summary>
    private static void WriteAt(int x, int y, string text)
    {
        Console.SetCursorPosition(x, y);
        Console.Write(text);
    }

    /// <summary>
    /// Visszaadja az adott mező előtérszínét az ott lévő objektum alapján
    /// (kincsesládához sárga, ellenséghez piros, stb.), vagy a tile alapértelmezettét.
    /// </summary>
    private static ConsoleColor GetForegroundColor(Maze maze, Position position)
    {
        var mapObject = maze.GetObjectAt(position);
        if (mapObject is TreasureChest) return ConsoleColor.Yellow;
        if (mapObject is Enemy) return ConsoleColor.Red;
        if (mapObject is Corpse) return ConsoleColor.DarkRed;
        if (mapObject is PartyMemberAvatar partyMember) return partyMember.Character.Color;
        if (maze.GetDoorAt(position) is { } door) return door.State switch
        {
            DoorState.Locked => ConsoleColor.Red,
            DoorState.Open => ConsoleColor.DarkGreen,
            DoorState.Closed => ConsoleColor.DarkYellow,
            DoorState.Smashed => ConsoleColor.DarkGray,
            _ => ConsoleColor.Gray
        };

        return maze.Tiles[position.X, position.Y] switch
        {
            var tile when tile == Maze.Wall => ConsoleColor.DarkGray,
            var tile when tile == Maze.ExitMarker => ConsoleColor.Green,
            _ => ConsoleColor.Black
        };
    }

    /// <summary>
    /// Egy Rune kiírása: beállítja a kívánt előtér- és háttérszínt és kiírja a Rune stringjét.
    /// </summary>
    private void WriteRuneWithColor(Rune rune, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
    {
        SetColors(foregroundColor, backgroundColor);
        Console.Write(rune.ToString());
    }

    /// <summary>
    /// Színkezelő: csak akkor állítja át Console.ForegroundColor/BackgroundColor értékét,
    /// ha azok eltérnek a cache-elt értékektől, így minimalizálva a felesleges rendszerhívásokat.
    /// </summary>
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

    /// <summary>Reseteli a konzol színeket és törli a cache-elt színértékeket.</summary>
    private void ResetColorCache()
    {
        Console.ResetColor();
        _currentForegroundColor = null;
        _currentBackgroundColor = null;
    }
}
