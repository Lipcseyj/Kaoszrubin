using System.Text;
using MazeGame.Combat;
using MazeGame.Data;
using MazeGame.Domain.Characters;
using MazeGame.Domain.Inventory;
using MazeGame.Domain.Magic;

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
    private Enemy? _battleEnemy;
    private LiveCharacter? _displayedCharacter;
    private bool _characterSheetFocused;
    private LiveCharacter? _spellInfoCharacter;
    private int _selectedSpellInfoIndex;
    private List<MapCellSnapshot>? _spellCastingOverlaySnapshot;
    private SheetSelectionKey? _activeSheetSelection;
    private readonly Dictionary<LiveCharacter, SheetSelectionKey> _lastSheetSelections = [];
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
        _battleEnemy = null;
        _spellInfoCharacter = null;
        _spellCastingOverlaySnapshot = null;
        Console.Clear();
        DrawPlayfield(maze, fogOfWar);
        DrawFrame();
        RefreshCharacterSheet(player.Character);
        DrawBattleMessage($"{maze.LevelName} — találd meg a kijáratot: ⌂");
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

    public void DrawPartyMemberMovement(Maze maze, FogOfWar fogOfWar, Position previousPosition,
        Position currentPosition, IReadOnlyList<Position> newlyRevealed, Position playerPosition)
    {
        foreach (var position in newlyRevealed) DrawMapCell(maze, fogOfWar, position);
        if (previousPosition != playerPosition) DrawMapCell(maze, fogOfWar, previousPosition);
        if (currentPosition != playerPosition) DrawMapCell(maze, fogOfWar, currentPosition);
        DrawPlayer(playerPosition);
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
        _battleEnemy = enemy;
        DrawPicturePanel();
        DrawBattleMessage($"Csata kezdődik! Ellenfél: {enemy.Name}");
    }
    /// <summary>Kincs felvétele esetén rövid üzenet a battle/message panelre.</summary>
    public void DrawTreasureCollected(int goldAmount, bool jackpot, int jackpotChance, int rewardMultiplier) =>
        DrawBattleMessage(jackpot
            ? $"💰🎉 FŐNYEREMÉNY! +{goldAmount} arany (esély {jackpotChance}%, ×{rewardMultiplier})!"
            : $"Kincsesláda: +{goldAmount} arany!", jackpot ? ConsoleColor.Magenta : ConsoleColor.Yellow);
    /// <summary>Tapasztalati pont szerzés és esetleges szintlépés megjelenítése.</summary>
    public void DrawExperienceGained(LevelUpResult result) => DrawBattleMessage(
        result.LeveledUp
            ? $"+{result.GainedExperience} XP! Szintlépés: {result.PreviousLevel} → {result.CurrentLevel}."
            : $"+{result.GainedExperience} XP.",
        result.LeveledUp ? ConsoleColor.Magenta : ConsoleColor.Cyan);

    public void DrawExperienceDistribution(string distribution, bool anyLevelUp) =>
        DrawBattleMessage($"XP elosztás: {distribution}.", anyLevelUp ? ConsoleColor.Magenta : ConsoleColor.Cyan);

    /// <summary>
    /// Csataround naplóbejegyzés megjelenítése. A napló színezése a bejegyzés típusától függ.
    /// </summary>
    public void DrawBattleRound(BattleLogEntry entry)
    {
        var color = entry.Kind switch
        {
            BattleLogKind.PlayerAttack => ConsoleColor.Green,
            BattleLogKind.EnemyAttack => ConsoleColor.Red,
            BattleLogKind.CriticalHit => ConsoleColor.Yellow,
            _ => ConsoleColor.Cyan
        };
        DrawBattleMessage(entry.Message, color);
        // A jobb oldali karakterlapon megjelenített sor: információ a vezérlésről.
        WriteSheetLine(42, "Space: tovább | saját kör: V/F1-F8", ConsoleColor.DarkYellow);
    }

    /// <summary>
    /// Csata eredményének megjelenítése: visszaáll a nem-csata állapotra és kiírja az összegző üzenetet.
    /// </summary>
    public void DrawBattleResult(BattleResult result, Enemy enemy)
    {
        _battleActive = false;
        _battleEnemy = null;
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

    public void DrawLevelCompletionScreen(int completedLevel, int baseExperience,
        IReadOnlyList<Game.LevelCompletionResult> results, IReadOnlyList<LiveCharacter> fallenCharacters)
    {
        ResetColorCache();
        Console.Clear();
        var reward = checked(baseExperience * completedLevel);
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("🏆✨  PÁLYA TELJESÍTVE!  ✨🏆", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray),
            ($"🚪 A parti kijutott a(z) {completedLevel}. labirintusszintről.", ConsoleColor.Green),
            ($"📜 Teljesítési XP: {baseExperience} × {completedLevel} = {reward} XP minden túlélő partitag számára", ConsoleColor.Cyan),
            (string.Empty, ConsoleColor.Gray),
            ("🏰🍺  PIHENŐ A FOGADÓBAN  🍲🛏️", ConsoleColor.DarkYellow)
        };
        foreach (var result in results)
        {
            var character = result.Character;
            var levelText = result.Experience.LeveledUp
                ? $"  ⭐ L{result.Experience.PreviousLevel}→L{result.Experience.CurrentLevel}"
                : $"  L{character.Level}";
            var manaText = character.UsesMana ? $"  🔷 {character.CurrentMana}/{character.MaximumMana} manna" : string.Empty;
            lines.Add(($"✨ {character.Name,-13} +{result.Experience.GainedExperience} XP{levelText}  ❤️ {character.CurrentVitality}/{character.MaximumVitality} HP{manaText}", character.Color));
        }
        if (fallenCharacters.Count > 0)
        {
            lines.Add((string.Empty, ConsoleColor.Gray));
            lines.Add(("💀  A fogadóig nem jutottak el — végleg elvesztek:", ConsoleColor.Red));
            foreach (var fallen in fallenCharacters)
                lines.Add(($"† {fallen.Name} ({fallen.CharacterClass.Name})", ConsoleColor.DarkRed));
        }
        lines.AddRange([
            (string.Empty, ConsoleColor.Gray),
            ("💤 A túlélők kipihenték sérüléseiket: minden HP és manna feltöltve.", ConsoleColor.Green),
            ("🛒 A fogadó kereskedői már várnak a portékáikkal...", ConsoleColor.Magenta),
            (string.Empty, ConsoleColor.Gray),
            ("Nyomj Entert vagy Space-t a kereskedéshez! ➡️", ConsoleColor.Yellow)
        ]);
        DrawCenteredFrame(112, lines);
        while (Console.ReadKey(intercept: true).Key is not (ConsoleKey.Enter or ConsoleKey.Spacebar)) { }
    }

    public void DrawInnMarketScreen(LiveCharacter leader, InnMarketMode mode,
        IReadOnlyList<InnStockOffer> stock, IReadOnlyList<InnSellOffer> sellOffers,
        int selectedIndex, int freeBackpackSlots, string message)
    {
        ResetColorCache();
        Console.Clear();
        var buying = mode == InnMarketMode.Buy;
        var entryCount = buying ? stock.Count : sellOffers.Count;
        const int pageSize = 12;
        var pageStart = entryCount == 0 ? 0 : Math.Clamp(selectedIndex - pageSize / 2, 0, Math.Max(0, entryCount - pageSize));
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("🏰🍺  A VÁNDORCSILLAG FOGADÓ KERESKEDŐJE  🛒✨", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray),
            (buying ? "◀  [ VÁSÁRLÁS ]     ELADÁS  ▶" : "◀    VÁSÁRLÁS     [ ELADÁS ]  ▶", ConsoleColor.Cyan),
            ($"💰 {leader.Name} aranya: {leader.Gold}     🎒 Szabad parti-hátizsákhely: {freeBackpackSlots}", ConsoleColor.Green),
            ("────────────────────────────────────────────────────────────────────────────────────────────", ConsoleColor.DarkMagenta)
        };

        for (var row = 0; row < pageSize; row++)
        {
            var index = pageStart + row;
            if (index >= entryCount) { lines.Add((string.Empty, ConsoleColor.Gray)); continue; }
            var selected = index == selectedIndex;
            if (buying)
            {
                var offer = stock[index];
                lines.Add(($"{(selected ? "▶" : " ")} {ItemCategoryIcon(offer.Item)} {offer.Item.Name,-24} alapár {offer.Item.BasePrice,5}   fogadói ár {offer.Price,5} 💰",
                    selected ? ConsoleColor.White : ItemRarityColor(offer.Item.Rarity)));
            }
            else
            {
                var offer = sellOffers[index];
                lines.Add(($"{(selected ? "▶" : " ")} {ItemCategoryIcon(offer.Item)} {offer.Item.Name,-22} {offer.Owner.Name,-13} ajánlat {offer.Price,5} 💰",
                    selected ? ConsoleColor.White : ItemRarityColor(offer.Item.Rarity)));
            }
        }

        var selectedItem = entryCount == 0 ? null : buying ? stock[selectedIndex].Item : sellOffers[selectedIndex].Item;
        lines.Add(("────────────────────────────────────────────────────────────────────────────────────────────", ConsoleColor.DarkMagenta));
        lines.Add((selectedItem is null ? (buying ? "Nincs több megvásárolható portéka." : "Nincs eladható tárgy a hátizsákokban.")
            : ClipMarketText($"ℹ️ {selectedItem.Description}", 94), ConsoleColor.DarkCyan));
        lines.Add((ClipMarketText(message, 94), ConsoleColor.Magenta));
        lines.Add(("↑/↓ választás   ←/→ vétel–eladás   Enter üzlet   Esc tovább a toborzáshoz", ConsoleColor.White));
        DrawCenteredFrame(100, lines);
    }

    public void DrawInnRecruitmentScreen(IReadOnlyList<LiveCharacter> candidates,
        IReadOnlyDictionary<LiveCharacter, int> prices, int selectedIndex,
        IReadOnlyList<LiveCharacter> party, int leaderGold, string message)
    {
        ResetColorCache();
        Console.Clear();
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("🏰🍺  A VÁNDORCSILLAG FOGADÓ ZSOLDOSAI  ⚔️✨", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray),
            ($"Parti: {party.Count}/{Party.MaximumSize} fő     💰 Arany: {leaderGold}", ConsoleColor.Cyan),
            ("────────────────────────────────────────────────────────────────────────────────────────────", ConsoleColor.DarkMagenta)
        };
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var selected = index == selectedIndex;
            var mana = candidate.UsesMana ? $"  MP {candidate.MaximumMana}" : string.Empty;
            var price = prices[candidate] == 0 ? "INGYEN" : $"{prices[candidate]} 💰";
            lines.Add(($"{(selected ? "▶" : " ")} {candidate.Name,-13}  {candidate.Race.Name,-10} {candidate.CharacterClass.Name,-10} L{candidate.Level,2}  HP {candidate.MaximumVitality}{mana}  {price}",
                selected ? ConsoleColor.White : candidate.Color));
        }
        var shown = candidates[selectedIndex];
        var weaponNames = shown.WeaponSlots.Where(item => item is not null).Select(item => item!.Name).ToList();
        var weapons = weaponNames.Count == 0 ? "nincs" : string.Join(", ", weaponNames);
        lines.Add(("────────────────────────────────────────────────────────────────────────────────────────────", ConsoleColor.DarkMagenta));
        lines.Add(($"Erő {shown.Abilities.Strength}  Ügy {shown.Abilities.Dexterity}  Egész {shown.Abilities.Health}  Int {shown.Abilities.Intelligence}", ConsoleColor.Cyan));
        lines.Add(($"Fegyver: {weapons}  |  Páncél: {shown.Armor?.Name ?? "nincs"}", ConsoleColor.Gray));
        lines.Add(($"Hátizsák: {string.Join(", ", shown.Backpack.Where(item => item is not null).Select(item => item!.Name))}", ConsoleColor.DarkCyan));
        lines.Add((ClipMarketText(message, 94), ConsoleColor.Magenta));
        lines.Add((party.Count >= Party.MaximumSize
            ? "↑/↓ választás   Enter felvétel és társ lecserélése   Esc tovább a pletykákhoz"
            : "↑/↓ választás   Enter felvétel   Esc tovább a pletykákhoz", ConsoleColor.White));
        DrawCenteredFrame(100, lines);
    }

    public void DrawInnReplacementScreen(LiveCharacter recruit, IReadOnlyList<LiveCharacter> replaceable,
        int selectedIndex)
    {
        ResetColorCache();
        Console.Clear();
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("⚠️  A PARTI MEGTELT  ⚠️", ConsoleColor.Yellow),
            ($"{recruit.Name} ({recruit.CharacterClass.Name}, L{recruit.Level}) csak egy régi társ helyére léphet.", ConsoleColor.Cyan),
            ("A lecserélt karakter végleg elveszik.", ConsoleColor.Red),
            (string.Empty, ConsoleColor.Gray)
        };
        for (var index = 0; index < replaceable.Count; index++)
        {
            var member = replaceable[index];
            lines.Add(($"{(index == selectedIndex ? "▶" : " ")} {member.Name,-13} {member.CharacterClass.Name,-10} L{member.Level,2}  HP {member.CurrentVitality}/{member.MaximumVitality}",
                index == selectedIndex ? ConsoleColor.White : member.Color));
        }
        lines.Add((string.Empty, ConsoleColor.Gray));
        lines.Add(("↑/↓ választás   Enter végleges csere   Esc mégse", ConsoleColor.White));
        DrawCenteredFrame(90, lines);
    }

    public void DrawInnRumorScreen(InnRumor rumor, int refreshesRemaining)
    {
        ResetColorCache();
        Console.Clear();
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("🏰🍺  PLETYKÁK A VÁNDORCSILLAG FOGADÓBAN  👂📜", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray),
            (rumor.Title, rumor.Color),
            ("────────────────────────────────────────────────────────────────────────────────────────────", ConsoleColor.DarkMagenta)
        };
        foreach (var paragraph in rumor.Lines)
        {
            foreach (var line in WrapText(paragraph, 100)) lines.Add((line, ConsoleColor.Gray));
            lines.Add((string.Empty, ConsoleColor.Gray));
        }
        lines.Add(("────────────────────────────────────────────────────────────────────────────────────────────", ConsoleColor.DarkMagenta));
        lines.Add((refreshesRemaining > 0
            ? $"N: új pletyka ({refreshesRemaining} maradt)   Enter/Esc: indulás a következő pályára"
            : "Nincs több új pletyka.   Enter/Esc: indulás a következő pályára", ConsoleColor.White));
        DrawCenteredFrame(108, lines);
    }

    private static IEnumerable<string> WrapText(string text, int maximumWidth)
    {
        var remaining = text;
        while (remaining.Length > maximumWidth)
        {
            var splitAt = remaining.LastIndexOf(' ', maximumWidth);
            if (splitAt <= 0) splitAt = maximumWidth;
            yield return remaining[..splitAt];
            remaining = remaining[splitAt..].TrimStart();
        }
        yield return remaining;
    }

    private static string ItemCategoryIcon(IItemDefinition item) => item.Category switch
    {
        ItemCategory.Weapon => "⚔️",
        ItemCategory.Armor => "🛡️",
        ItemCategory.MagicItem => "🔮",
        _ => "📦"
    };

    private static ConsoleColor ItemRarityColor(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Magic => ConsoleColor.Cyan,
        ItemRarity.Legendary => ConsoleColor.Yellow,
        _ => ConsoleColor.Gray
    };

    private static string ClipMarketText(string text, int maximumLength) =>
        text.Length <= maximumLength ? text : text[..Math.Max(1, maximumLength - 1)] + "…";
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

    public SpellDefinition DrawSpellLearningScreen(LiveCharacter character,
        IReadOnlyList<SpellDefinition> choices, int learnedNumber, int learnedTotal)
    {
        var selectedIndex = 0;
        while (true)
        {
            ResetColorCache();
            Console.Clear();
            var lines = new List<(string Text, ConsoleColor Color)>
            {
                ("📖  ÚJ VARÁZSLAT TANULÁSA", ConsoleColor.Magenta),
                (string.Empty, ConsoleColor.Gray),
                ($"{character.Name} — {learnedNumber}/{learnedTotal}. új varázslat", ConsoleColor.Cyan),
                ("Fel/le: választás     Enter: megtanulás", ConsoleColor.Green),
                (string.Empty, ConsoleColor.Gray)
            };
            lines.AddRange(choices.Select((spell, index) =>
                ($"{(index == selectedIndex ? "▶" : " ")}  {spell.Level}. szint — {spell.Name}",
                    index == selectedIndex ? ConsoleColor.Yellow : ConsoleColor.Gray)));
            DrawCenteredFrame(88, lines);
            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.UpArrow: selectedIndex = (selectedIndex - 1 + choices.Count) % choices.Count; break;
                case ConsoleKey.DownArrow: selectedIndex = (selectedIndex + 1) % choices.Count; break;
                case ConsoleKey.Enter: return choices[selectedIndex];
            }
        }
    }

    public IReadOnlyList<SpellDefinition> DrawSpellPreparationScreen(LiveCharacter character)
    {
        var spells = character.KnownSpells.OrderBy(spell => spell.Level).ThenBy(spell => spell.Name).ToList();
        if (spells.Count == 0) return [];
        var selected = character.MemorizedSpells.Select(spell => spell.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cursor = 0;
        while (true)
        {
            ResetColorCache();
            Console.Clear();
            var lines = new List<(string Text, ConsoleColor Color)>
            {
                ("🧠✨  VARÁZSLATOK MEMORIZÁLÁSA", ConsoleColor.Magenta),
                (string.Empty, ConsoleColor.Gray),
                ($"{character.Name} — kapacitás: {selected.Count}/{character.MemorizationCapacity}", ConsoleColor.Cyan),
                ("Fel/le: mozgás   Space: ki/be   Enter: kész", ConsoleColor.Green),
                (string.Empty, ConsoleColor.Gray)
            };
            lines.AddRange(spells.Select((spell, index) =>
                ($"{(index == cursor ? "▶" : " ")} [{(selected.Contains(spell.Id) ? "X" : " ")}]  {spell.Level}. szint — {spell.Name}",
                    index == cursor ? ConsoleColor.Yellow : ConsoleColor.Gray)));
            DrawCenteredFrame(92, lines);
            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.UpArrow: cursor = (cursor - 1 + spells.Count) % spells.Count; break;
                case ConsoleKey.DownArrow: cursor = (cursor + 1) % spells.Count; break;
                case ConsoleKey.Spacebar:
                    if (!selected.Remove(spells[cursor].Id) && selected.Count < character.MemorizationCapacity)
                        selected.Add(spells[cursor].Id);
                    break;
                case ConsoleKey.Enter:
                    return spells.Where(spell => selected.Contains(spell.Id)).ToList();
            }
        }
    }

    public SpellDefinition? DrawSpellCastingScreen(LiveCharacter character, bool inCombat, Maze maze,
        FogOfWar fogOfWar, Position playerPosition, Action? showHelp = null)
    {
        var spells = character.MemorizedSpells
            .Where(spell => inCombat ? spell.CanUseInCombat : spell.CanUseDuringExploration)
            .OrderBy(spell => spell.Level).ThenBy(spell => spell.Name).ToList();
        if (spells.Count == 0) return null;
        _spellCastingOverlaySnapshot = null;
        var selectedIndex = 0;
        while (true)
        {
            const int maximumVisibleSpellCount = 12;
            var firstVisibleIndex = Math.Clamp(selectedIndex - maximumVisibleSpellCount / 2, 0,
                Math.Max(0, spells.Count - maximumVisibleSpellCount));
            var visibleSpells = spells.Skip(firstVisibleIndex).Take(maximumVisibleSpellCount).ToList();
            var lines = new List<(string Text, ConsoleColor Color)>
            {
                (inCombat ? "⚔️ HARCI VARÁZSLÁS" : "🔮 VARÁZSLÁS", ConsoleColor.Magenta),
                ($"{character.Name}  ◆ {character.CurrentMana}/{character.MaximumMana} manna", ConsoleColor.Cyan),
                ("↑↓ választ  Enter célzás  Esc bezár", ConsoleColor.Green),
                ("────────────────────────────────────────────────────────────────────", ConsoleColor.DarkMagenta)
            };
            lines.AddRange(visibleSpells.Select((spell, visibleIndex) =>
            {
                var index = firstVisibleIndex + visibleIndex;
                var quickIndex = character.QuickSpells.ToList().FindIndex(candidate =>
                    string.Equals(candidate?.Id, spell.Id, StringComparison.OrdinalIgnoreCase));
                var quick = quickIndex >= 0 ? $"F{quickIndex + 1}" : "--";
                var manaCost = SpellcastingRules.EffectiveManaCost(character, spell);
                var text = $"{(index == selectedIndex ? "▶" : " ")} [{quick}] L{spell.Level}  {spell.Name,-24} {manaCost}M  {SpellTargetName(spell.TargetType)}";
                var color = character.CurrentMana < manaCost ? ConsoleColor.DarkRed :
                    index == selectedIndex ? ConsoleColor.Yellow : ConsoleColor.Gray;
                return (text, color);
            }));
            if (spells.Count > maximumVisibleSpellCount)
                lines.Add(($"{firstVisibleIndex + 1}–{firstVisibleIndex + visibleSpells.Count} / {spells.Count}", ConsoleColor.DarkCyan));
            DrawSpellCastingOverlay(76, lines, maze, fogOfWar, playerPosition);
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.F1 && (key.Modifiers & ConsoleModifiers.Shift) != 0)
            {
                showHelp?.Invoke();
                _spellCastingOverlaySnapshot = null;
                continue;
            }
            switch (key.Key)
            {
                case ConsoleKey.UpArrow: selectedIndex = (selectedIndex - 1 + spells.Count) % spells.Count; break;
                case ConsoleKey.DownArrow: selectedIndex = (selectedIndex + 1) % spells.Count; break;
                case ConsoleKey.Enter: return spells[selectedIndex];
                case ConsoleKey.Escape: return null;
            }
        }
    }

    public void RestoreSpellCastingOverlay()
    {
        if (_spellCastingOverlaySnapshot is null) return;
        foreach (var cell in _spellCastingOverlaySnapshot)
        {
            Console.SetCursorPosition(cell.Position.X, cell.Position.Y);
            WriteRuneWithColor(cell.Rune, cell.ForegroundColor, cell.BackgroundColor);
        }
        _spellCastingOverlaySnapshot = null;
    }

    private void DrawSpellCastingOverlay(int frameWidth, IReadOnlyList<(string Text, ConsoleColor Color)> lines,
        Maze maze, FogOfWar fogOfWar, Position playerPosition)
    {
        const int horizontalPadding = 2;
        var frameHeight = lines.Count + 2;
        var left = Math.Max(0, (PlayfieldWidth - frameWidth) / 2);
        var top = Math.Max(1, (PlayfieldHeight - frameHeight) / 2);
        if (_spellCastingOverlaySnapshot is null)
        {
            _spellCastingOverlaySnapshot = [];
            for (var y = top; y < top + frameHeight; y++)
            for (var x = left; x < left + frameWidth; x++)
            {
                var position = new Position(x, y);
                var visual = GetMapCellVisual(maze, fogOfWar, position, playerPosition);
                _spellCastingOverlaySnapshot.Add(new MapCellSnapshot(position, visual.Rune,
                    visual.ForegroundColor, visual.BackgroundColor));
            }
        }

        SetColors(ConsoleColor.Magenta, ConsoleColor.Black);
        WriteAt(left, top, "╔" + new string('═', frameWidth - 2) + "╗");
        var contentWidth = frameWidth - horizontalPadding * 2;
        for (var index = 0; index < lines.Count; index++)
        {
            SetColors(ConsoleColor.Magenta, ConsoleColor.Black);
            WriteAt(left, top + index + 1, "║");
            SetColors(ConsoleColor.Gray, ConsoleColor.Black);
            WriteAt(left + 1, top + index + 1, new string(' ', frameWidth - 2));
            SetColors(lines[index].Color, ConsoleColor.Black);
            var text = lines[index].Text.Length <= contentWidth
                ? lines[index].Text
                : lines[index].Text[..contentWidth];
            WriteAt(left + horizontalPadding, top + index + 1, text.PadRight(contentWidth));
            SetColors(ConsoleColor.Magenta, ConsoleColor.Black);
            WriteAt(left + frameWidth - 1, top + index + 1, "║");
        }
        SetColors(ConsoleColor.Magenta, ConsoleColor.Black);
        WriteAt(left, top + lines.Count + 1, "╚" + new string('═', frameWidth - 2) + "╝");
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
        if (_spellInfoCharacter is not null)
        {
            DrawSpellInfoPage(_spellInfoCharacter, _selectedSpellInfoIndex);
            return;
        }
        var characterToDraw = _displayedCharacter is not null && _party.Members.Contains(_displayedCharacter)
            ? _displayedCharacter
            : character;
        DrawCharacterSheet(characterToDraw);
    }

    public void SetCharacterSheetFocused(bool focused)
    {
        _characterSheetFocused = focused;
        if (_displayedCharacter is null) return;
        DrawCharacterSheetHeader(_displayedCharacter);
        DrawSelectableCharacterSheetRows(_displayedCharacter);
    }

    public void MoveCharacterSheetSelection(int direction)
    {
        if (_displayedCharacter is null || direction == 0) return;
        var entries = BuildSheetSelections(_displayedCharacter);
        if (entries.Count == 0) return;
        var currentIndex = _activeSheetSelection is { } active
            ? entries.FindIndex(entry => entry.Key == active)
            : -1;
        if (currentIndex < 0) currentIndex = direction > 0 ? -1 : 0;
        var nextIndex = (currentIndex + direction + entries.Count) % entries.Count;
        _activeSheetSelection = entries[nextIndex].Key;
        _lastSheetSelections[_displayedCharacter] = entries[nextIndex].Key;
        DrawSelectableCharacterSheetRows(_displayedCharacter);
    }

    public void MoveDisplayedPartyMember(int direction)
    {
        if (_displayedCharacter is null || direction == 0 || _party.Members.Count == 0) return;
        if (_activeSheetSelection is { } active) _lastSheetSelections[_displayedCharacter] = active;
        var currentIndex = Enumerable.Range(0, _party.Members.Count)
            .FirstOrDefault(index => _party.Members[index] == _displayedCharacter);
        var nextIndex = (currentIndex + direction + _party.Members.Count) % _party.Members.Count;
        _displayedCharacter = _party.Members[nextIndex];
        _activeSheetSelection = _lastSheetSelections.GetValueOrDefault(_displayedCharacter);
        DrawCharacterSheet(_displayedCharacter);
    }

    public void DrawSpellInfoPage(LiveCharacter character, int selectedIndex)
    {
        var spells = character.KnownSpells.OrderBy(spell => spell.Level).ThenBy(spell => spell.Name).ToList();
        selectedIndex = spells.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, spells.Count - 1);
        _spellInfoCharacter = character;
        _selectedSpellInfoIndex = selectedIndex;
        for (var line = 0; line <= PicturePanelBottom; line++) WriteSheetLine(line, string.Empty, ConsoleColor.Gray);

        WriteSheetLine(0, $"VARÁZSLATOK - {character.Name}", ConsoleColor.Yellow,
            _characterSheetFocused ? ConsoleColor.Green : ConsoleColor.Black);
        WriteSheetLine(1, SpellcastingRules.HasRequiredFocus(character)
            ? $"Fókusz: {character.Backpack[0]!.Name}"
            : "Fókusz: HIÁNYZIK", SpellcastingRules.HasRequiredFocus(character) ? ConsoleColor.Cyan : ConsoleColor.Red);
        WriteSheetLine(2, $"Memória: {character.MemorizedSpells.Count}/{character.MemorizationCapacity}", ConsoleColor.Magenta);
        WriteSheetLine(3, "[M] memorizált  [F#] gyors", ConsoleColor.DarkCyan);
        WriteSheetLine(4, "ISMERT VARÁZSLATOK", ConsoleColor.White);

        for (var index = 0; index < 20; index++)
        {
            if (index >= spells.Count) { WriteSheetLine(5 + index, string.Empty, ConsoleColor.Gray); continue; }
            var spell = spells[index];
            var memorized = character.MemorizedSpells.Any(candidate =>
                string.Equals(candidate.Id, spell.Id, StringComparison.OrdinalIgnoreCase));
            var quickIndex = character.QuickSpells.ToList().FindIndex(candidate =>
                string.Equals(candidate?.Id, spell.Id, StringComparison.OrdinalIgnoreCase));
            var marker = index == selectedIndex ? ">" : " ";
            WriteSheetLine(5 + index, $"{marker}[{(memorized ? "M" : " ")}]{(quickIndex >= 0 ? $"[F{quickIndex + 1}]" : "    ")} {spell.Level}. {spell.Name}",
                index == selectedIndex ? ConsoleColor.Yellow : memorized ? ConsoleColor.Cyan : ConsoleColor.Gray,
                index == selectedIndex ? ConsoleColor.DarkCyan : ConsoleColor.Black);
        }

        if (spells.Count > 0)
        {
            var selected = spells[selectedIndex];
            WriteSheetLine(26, "KIJELÖLT VARÁZSLAT", ConsoleColor.White);
            WriteSheetLine(27, selected.Name, ConsoleColor.Yellow);
            var manaCost = SpellcastingRules.EffectiveManaCost(character, selected);
            WriteSheetLine(28, $"Szint: {selected.Level} | Manna: {manaCost} | Cél: {SpellTargetName(selected.TargetType)}", ConsoleColor.Blue);
            var quickIndex = character.QuickSpells.ToList().FindIndex(spell => spell?.Id == selected.Id);
            WriteSheetLine(29, character.MemorizedSpells.Any(spell => spell.Id == selected.Id)
                ? $"Állapot: memorizált{(quickIndex >= 0 ? $", F{quickIndex + 1}" : string.Empty)}"
                : "Állapot: csak ismert", ConsoleColor.Magenta);
            var descriptionLines = WrapText(selected.Description, 27).Take(5).ToList();
            for (var index = 0; index < 5; index++)
                WriteSheetLine(30 + index, index < descriptionLines.Count ? descriptionLines[index] : string.Empty, ConsoleColor.Gray);
        }

        WriteSheetLine(36, "VARÁZSLATSZINTEK", ConsoleColor.White);
        var unlockLevels = new[] { 1, 5, 10, 15, 20 };
        for (var spellLevel = 1; spellLevel <= 5; spellLevel++)
        {
            var requiredLevel = unlockLevels[spellLevel - 1];
            var unlocked = character.Level >= requiredLevel;
            WriteSheetLine(36 + spellLevel,
                $"{spellLevel}. szint: L{requiredLevel} {(unlocked ? "feloldva" : $"még {requiredLevel - character.Level}")}",
                unlocked ? ConsoleColor.Green : ConsoleColor.DarkYellow);
        }
        var nextUnlock = unlockLevels.FirstOrDefault(level => level > character.Level);
        WriteSheetLine(43, nextUnlock == 0 ? "Minden szint feloldva." : $"Következő feloldás: L{nextUnlock}", ConsoleColor.Cyan);
        WriteSheetLine(45, "Fel/le: böngészés | F1-F8: gyorshely", ConsoleColor.Green);
        WriteSheetLine(46, "Enter: elsütés | Esc: vissza", ConsoleColor.DarkYellow);
    }

    public bool IsSpellInfoPageOpen => _spellInfoCharacter is not null;

    public LiveCharacter? SpellInfoCharacter => _spellInfoCharacter;

    public SpellDefinition? GetSelectedSpellInfo()
    {
        if (_spellInfoCharacter is null) return null;
        var spells = _spellInfoCharacter.KnownSpells.OrderBy(spell => spell.Level).ThenBy(spell => spell.Name).ToList();
        return spells.ElementAtOrDefault(_selectedSpellInfoIndex);
    }

    public void RefreshSpellInfoPage()
    {
        if (_spellInfoCharacter is not null) DrawSpellInfoPage(_spellInfoCharacter, _selectedSpellInfoIndex);
    }

    public void MoveSpellInfoSelection(int direction)
    {
        if (_spellInfoCharacter is null || direction == 0 || _spellInfoCharacter.KnownSpells.Count == 0) return;
        _selectedSpellInfoIndex = (_selectedSpellInfoIndex + direction + _spellInfoCharacter.KnownSpells.Count) %
                                  _spellInfoCharacter.KnownSpells.Count;
        DrawSpellInfoPage(_spellInfoCharacter, _selectedSpellInfoIndex);
    }

    public void CloseSpellInfoPage()
    {
        if (_spellInfoCharacter is null) return;
        var character = _spellInfoCharacter;
        _spellInfoCharacter = null;
        DrawCharacterSheet(character);
    }

    public InventorySlotReference? GetSelectedInventorySlot()
    {
        if (_displayedCharacter is null || _activeSheetSelection is not { } selection) return null;
        var kind = selection.Kind switch
        {
            SheetSelectionKind.Weapon => InventorySlotKind.Weapon,
            SheetSelectionKind.Armor => InventorySlotKind.Armor,
            SheetSelectionKind.MagicItem => InventorySlotKind.MagicItem,
            SheetSelectionKind.Backpack => InventorySlotKind.Backpack,
            _ => (InventorySlotKind?)null
        };
        return kind is { } inventoryKind
            ? new InventorySlotReference(_displayedCharacter, inventoryKind, selection.Index)
            : null;
    }

    public LiveCharacter? GetSelectedPartyMember()
    {
        if (_activeSheetSelection is not { Kind: SheetSelectionKind.PartyMember } selection) return null;
        return _party.Members.Skip(1).ElementAtOrDefault(selection.Index);
    }

    public void RefreshInventoryRows()
    {
        if (_displayedCharacter is not null) DrawSelectableCharacterSheetRows(_displayedCharacter);
    }

    /// <summary>Csata közben csak az állapot-, HP- és mannasorokat frissíti.</summary>
    public void RefreshBattleStatusRows()
    {
        if (_displayedCharacter is not null) DrawBattleStatusRows(_displayedCharacter);
    }

    public void DrawInventoryMessage(string message, ConsoleColor color = ConsoleColor.Cyan) => DrawBattleMessage(message, color);
    public void DrawNpcBattleSummary(string message, ConsoleColor color) => DrawBattleMessage(message, color);

    public void DrawSpellTargetCursor(Maze maze, FogOfWar fogOfWar, Position? previousPosition,
        Position position, bool valid, string prompt)
    {
        if (previousPosition is { } previous) DrawMapCell(maze, fogOfWar, previous);
        Console.SetCursorPosition(position.X, position.Y);
        WriteRuneWithColor(new Rune('╳'), valid ? ConsoleColor.Green : ConsoleColor.Red, ConsoleColor.DarkBlue);
        DrawBattleMessage(prompt, valid ? ConsoleColor.Cyan : ConsoleColor.DarkYellow);
    }

    public void FinishSpellTargeting(Maze maze, FogOfWar fogOfWar, Position playerPosition) =>
        DrawMapVisibilityChanged(maze, fogOfWar, playerPosition);

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
        DrawCharacterSheetHeader(character);
        WriteSheetLine(1, $"{character.Race.Name} {character.CharacterClass.Name}", ConsoleColor.White);
        var perkLines = FormatCompactListRows("Teh", character.Perks.Select(perk => perk.Name), 2);
        WriteSheetLine(2, perkLines[0], ConsoleColor.Magenta);
        WriteSheetLine(3, perkLines[1], ConsoleColor.Magenta);
        DrawBattleStatusRows(character);
        WriteSheetLine(5, $"Labirintus: {_mazeLevel}", ConsoleColor.Green);
        WriteSheetLine(6, FormatExperience(character), ConsoleColor.Cyan);
        WriteSheetLine(7, $"Erő: {character.Abilities.Strength}", ConsoleColor.Red);
        WriteSheetLine(8, $"Ügy: {character.Abilities.Dexterity}", ConsoleColor.Green);
        WriteSheetLine(9, $"Egs: {character.Abilities.Health}", ConsoleColor.DarkYellow);
        WriteSheetLine(10, $"Int: {character.Abilities.Intelligence}", ConsoleColor.Magenta);
        WriteSheetLine(13, $"É: {ResourceIcons("🍖", character.FoodLevel)}", ConsoleColor.Yellow);
        WriteSheetLine(14, $"V: {ResourceIcons("💧", character.WaterLevel)}", ConsoleColor.Cyan);
        WriteSheetLine(15, $"Arany: {character.Gold} 💰", ConsoleColor.Yellow);
        WriteSheetLine(17, "FEGYVEREK", ConsoleColor.Yellow);
        WriteSheetLine(22, $"VARÁZSTÁRGYAK {character.MagicItems.Count(item => item is not null)}/3", ConsoleColor.Magenta);
        WriteSheetLine(26, $"HÁTIZSÁK {character.Backpack.Count(item => item is not null)}/10", ConsoleColor.DarkCyan);
        DrawSelectableCharacterSheetRows(character);
        WriteSheetLine(41, string.Empty, ConsoleColor.DarkGray);
        WriteSheetLine(42, string.Empty, ConsoleColor.DarkGray);
        DrawPicturePanel();
    }

    private void DrawBattleStatusRows(LiveCharacter character)
    {
        var statusIcons = character.Statuses.Select(status => status.Icon)
            .Concat(character.ActiveSpellEffects.Select(effect => effect.Type switch
            {
                ActiveSpellEffectType.Invisibility => "👻",
                ActiveSpellEffectType.DefenseBonus => "🛡️",
                ActiveSpellEffectType.PhysicalReduction => "🪨",
                ActiveSpellEffectType.BleedingImmunity => "🩸🚫",
                _ => "✨"
            })).ToList();
        WriteSheetLine(4, statusIcons.Count == 0
                ? "Áll: nincs"
                : $"Áll: {string.Join(' ', statusIcons)}",
            statusIcons.Count > 0 ? ConsoleColor.Magenta : ConsoleColor.DarkGray);
        WriteSheetLine(11, $"HP: {character.CurrentVitality}/{character.MaximumVitality}", ConsoleColor.Red);
        WriteSheetLine(12, character.UsesMana
            ? $"Manna: {character.CurrentMana}/{character.MaximumMana}"
            : "Manna: nincs", ConsoleColor.Blue);
    }

    private void DrawCharacterSheetHeader(LiveCharacter character) => WriteSheetLine(
        0, "KARAKTERLAP", ConsoleColor.Yellow,
        _characterSheetFocused ? ConsoleColor.Green : ConsoleColor.Black,
        " - " + character.Name, character.Color);

    private void DrawSelectableCharacterSheetRows(LiveCharacter character)
    {
        var entries = BuildSheetSelections(character);
        if (_activeSheetSelection is null || entries.All(entry => entry.Key != _activeSheetSelection))
            _activeSheetSelection = entries.FirstOrDefault()?.Key;

        WriteSheetLine(18, $"1: {ItemName(character.WeaponSlots[0])}", ConsoleColor.Gray, SelectionBackground(new(SheetSelectionKind.Weapon, 0)));
        var secondWeaponText = character.WeaponSlots[0]?.IsTwoHanded == true
            ? "2: ⛔ kétkezes fegyver"
            : $"2: {ItemName(character.WeaponSlots[1])}";
        WriteSheetLine(19, secondWeaponText, character.WeaponSlots[0]?.IsTwoHanded == true ? ConsoleColor.DarkGray : ConsoleColor.Gray,
            SelectionBackground(new(SheetSelectionKind.Weapon, 1)));
        WriteSheetLine(20, $"Páncél: {ItemName(character.Armor)}", ConsoleColor.DarkYellow, SelectionBackground(new(SheetSelectionKind.Armor, 0)));
        for (var index = 0; index < 3; index++)
            WriteSheetLine(23 + index, $"{index + 1}: {ItemName(index < character.MagicItems.Count ? character.MagicItems[index] : null)}",
                ConsoleColor.Gray, SelectionBackground(new(SheetSelectionKind.MagicItem, index)));
        for (var index = 0; index < 10; index++)
            WriteSheetLine(27 + index, $"{index + 1}: {ItemName(index < character.Backpack.Count ? character.Backpack[index] : null)}",
                ConsoleColor.Gray, SelectionBackground(new(SheetSelectionKind.Backpack, index)));
        var companions = _party.Members.Skip(1).Take(3).ToList();
        for (var index = 0; index < 3; index++)
            WriteSheetLine(38 + index, index < companions.Count ? FormatPartyMember(companions[index], companions[index] == character) : string.Empty,
                index < companions.Count ? companions[index].Color : ConsoleColor.DarkGray,
                index < companions.Count ? SelectionBackground(new(SheetSelectionKind.PartyMember, index)) : ConsoleColor.Black);
    }

    private List<SheetSelectionEntry> BuildSheetSelections(LiveCharacter character)
    {
        var entries = new List<SheetSelectionEntry>();
        for (var index = 0; index < character.WeaponSlots.Count; index++)
            entries.Add(new(new(SheetSelectionKind.Weapon, index)));
        entries.Add(new(new(SheetSelectionKind.Armor, 0)));
        for (var index = 0; index < character.MagicItems.Count; index++) entries.Add(new(new(SheetSelectionKind.MagicItem, index)));
        for (var index = 0; index < character.Backpack.Count; index++) entries.Add(new(new(SheetSelectionKind.Backpack, index)));
        var companionCount = Math.Min(3, Math.Max(0, _party.Members.Count - 1));
        for (var index = 0; index < companionCount; index++) entries.Add(new(new(SheetSelectionKind.PartyMember, index)));
        return entries;
    }

    private ConsoleColor SelectionBackground(SheetSelectionKey key) =>
        _activeSheetSelection == key ? ConsoleColor.DarkCyan : ConsoleColor.Black;

    private enum SheetSelectionKind { Weapon, Armor, MagicItem, Backpack, PartyMember }
    private readonly record struct SheetSelectionKey(SheetSelectionKind Kind, int Index);
    private sealed record SheetSelectionEntry(SheetSelectionKey Key);

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

    private static string FormatPartyMember(LiveCharacter character, bool isDisplayed)
    {
        const int maximumWidth = 27;
        var marker = isDisplayed ? "▶ " : "  ";
        var classInitial = character.CharacterClass.Name.EnumerateRunes().First().ToString().ToUpperInvariant();
        var suffix = character.IsAlive
            ? $" L{character.Level} {character.CurrentVitality}/{character.MaximumVitality}"
            : $" L{character.Level} 💀";
        var maximumNameLength = Math.Max(1, maximumWidth - marker.Length - classInitial.Length - 1 - suffix.Length);
        var name = character.Name[..Math.Min(character.Name.Length, maximumNameLength)];
        return $"{marker}{classInitial} {name}{suffix}";
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

    private static IReadOnlyList<string> FormatCompactListRows(string label, IEnumerable<string> values, int rowCount)
    {
        var names = values.ToList();
        if (names.Count == 0) return [$"{label}: nincs", .. Enumerable.Repeat(string.Empty, rowCount - 1)];

        var rows = new List<string>(rowCount);
        var namesPerRow = (int)Math.Ceiling(names.Count / (double)rowCount);
        for (var row = 0; row < rowCount; row++)
        {
            var rowNames = names.Skip(row * namesPerRow).Take(namesPerRow).ToList();
            if (rowNames.Count == 0) { rows.Add(string.Empty); continue; }
            var prefix = row == 0 ? $"{label}: " : new string(' ', label.Length + 2);
            var separatorsWidth = (rowNames.Count - 1) * 2;
            var availablePerName = Math.Max(1, (27 - prefix.Length - separatorsWidth) / rowNames.Count);
            var shortenedNames = rowNames.Select(name => name.Length <= availablePerName ? name : name[..availablePerName]);
            rows.Add(prefix + string.Join(", ", shortenedNames));
        }
        return rows;
    }

    /// <summary>
    /// A jobb oldali kép-panel (ASCII portré) kirajzolása. A PicturePanelTop-ról indul,
    /// és a WriteSheetLine metódussal írja ki a keretet és a képsorokat.
    /// </summary>
    private void DrawPicturePanel()
    {
        var portrait = _battleActive && _battleEnemy is not null
            ? AsciiPortraits.ForEnemy(_battleEnemy.Definition.Id)
            : AsciiPortraits.ForCharacterClass(_displayedCharacter?.CharacterClass.Id ?? "");
        var color = _battleActive && _battleEnemy is not null
            ? _battleEnemy.Definition.StrengthTier switch
            {
                1 => ConsoleColor.Green,
                2 => ConsoleColor.Yellow,
                3 => ConsoleColor.DarkYellow,
                4 => ConsoleColor.Red,
                _ => ConsoleColor.Magenta
            }
            : _displayedCharacter?.Color ?? ConsoleColor.Cyan;
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
        => WriteSheetLine(y, text, foregroundColor, ConsoleColor.Black);

    private void WriteSheetLine(int y, string text, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
    {
        const int maximumWidth = 27;
        var clippedText = text.Length <= maximumWidth ? text : text[..maximumWidth];
        SetColors(foregroundColor, backgroundColor);
        // Itt történik a tényleges kiírás: balra igazított, maximum 'maximumWidth' karakter,
        // és X=172 lesz (a jobb oldali karakterlap kezdő X pozíciója).
        WriteAt(172, y, clippedText.PadRight(maximumWidth));
    }

    /// <summary>
    /// Két szöveget ír ki egymás mellé a jobb oldali karakterlapra, két külön színnel.
    /// A teljes sor hossza nem haladja meg a maximum 27 karaktert — ha szükséges,
    /// levágja a szövegeket úgy, hogy mindkét rész látható maradjon lehetőleg.
    /// </summary>
    private void WriteSheetLine(int y, string leftText, ConsoleColor leftColor, ConsoleColor leftColorBg, string rightText, ConsoleColor rightColor)
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
        SetColors(leftColor, leftColorBg);
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
    private void DrawMapRune(Maze maze, FogOfWar fogOfWar, Position position)
    {
        var visual = GetMapCellVisual(maze, fogOfWar, position, null);
        WriteRuneWithColor(visual.Rune, visual.ForegroundColor, visual.BackgroundColor);
    }

    private MapCellVisual GetMapCellVisual(Maze maze, FogOfWar fogOfWar, Position position, Position? playerPosition)
    {
        if (playerPosition == position)
        {
            var character = _party.Leader ?? throw new InvalidOperationException("A főkarakter rajzolása előtt a partit inicializálni kell.");
            return new MapCellVisual(Rune.GetRuneAt(character.CharacterClass.Name.ToUpperInvariant(), 0),
                character.Color, ConsoleColor.Black);
        }
        if (!fogOfWar.IsVisible(position))
            return new MapCellVisual(FogSymbol, ConsoleColor.DarkGray, ConsoleColor.DarkBlue);
        return new MapCellVisual(maze.GetObjectAt(position)?.Symbol ?? maze.Tiles[position.X, position.Y],
            GetForegroundColor(maze, position), ConsoleColor.Black);
    }

    public static string SpellTargetName(SpellTargetType targetType) => targetType switch
    {
        SpellTargetType.Self => "önmaga",
        SpellTargetType.Party => "parti",
        SpellTargetType.PartyMember => "partitag",
        SpellTargetType.Enemy => "ellenség",
        SpellTargetType.Corpse => "elesett társ",
        SpellTargetType.Cell => "mező",
        SpellTargetType.Area => "terület",
        SpellTargetType.Direction => "irány",
        _ => "célpont"
    };

    /// <summary>
    /// Játékos karakterének kirajzolása: fix szimbólum és szín a kurzor aktuális pozíciójára.
    /// </summary>
    private void DrawPlayer(Position position)
    {
        var character = _party.Leader ?? throw new InvalidOperationException("A főkarakter rajzolása előtt a partit inicializálni kell.");
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
        if (mapObject is Enemy enemy) return enemy.Definition.StrengthTier switch
        {
            1 => ConsoleColor.Green,
            2 => ConsoleColor.Yellow,
            3 => ConsoleColor.DarkYellow,
            4 => ConsoleColor.Red,
            5 => ConsoleColor.Magenta,
            _ => ConsoleColor.Gray
        };
        if (mapObject is Corpse) return ConsoleColor.DarkRed;
        if (mapObject is GroundItemPile) return ConsoleColor.Cyan;
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
            var tile when tile == maze.WallRune => maze.WallColor,
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

    private readonly record struct MapCellVisual(Rune Rune, ConsoleColor ForegroundColor, ConsoleColor BackgroundColor);
    private readonly record struct MapCellSnapshot(Position Position, Rune Rune, ConsoleColor ForegroundColor, ConsoleColor BackgroundColor);
}
