using System.Diagnostics;
using System.Text;
using KaoszRubin.Application;
using KaoszRubin.Combat;
using KaoszRubin.Data;
using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;
using KaoszRubin.UI;

namespace KaoszRubin;

public sealed record SpellCastSelection(SpellDefinition Spell, LiveCharacter Caster,
    MagicItemDefinition? CastingItem = null, int? CastingItemSlotIndex = null);
public sealed record UniqueNpcConversationResult(int FriendlinessChange, bool FollowRequested, int ChoiceIndex);

public sealed class ConsoleRenderer
{
    public const int PlayfieldWidth = 170;
    public const int PlayfieldHeight = 44;
    public const int MessageLogLineCount = 7;
    public const int MessageLogBufferLineCount = MessageLogLineCount * 3;
    public const int ScreenRowCount = PlayfieldHeight + MessageLogLineCount + 1;
    public static string MoneyIcon { get; } = IsWindows11OrLater() ? "🪙" : "💰";
    public static string WandIcon { get; } = IsWindows11OrLater() ? "🪄" : "✨";
    public static string DamageReductionIcon { get; } = IsWindows11OrLater() ? "🪨" : "💥🛡️";
    private const int RightBorderX = PlayfieldWidth;
    private const int BottomBorderY = PlayfieldHeight;
    private static readonly Rune FogSymbol = new('█');
    private const int MessageLineCount = MessageLogLineCount;
    private const int MessageWidth = 164;
    private const int PicturePanelHeight = 5;
    private const int PicturePanelBottom = BottomBorderY + MessageLineCount;
    private const int PicturePanelTop = PicturePanelBottom - PicturePanelHeight - 1;
    private const int CenteredFrameHorizontalPadding = 2;
    private const int FrameBorderWidth = 2;
    private const int MinimumCenteredFrameTop = 1;
    private const int GameOverFrameWidth = 96;
    private const int GameOverFrameHeight = 11;
    private const int GameOverMinimumTop = 2;
    internal const int LevelCompletionFrameWidth = 112;
    internal const int InnMenuFrameWidth = 90;
    private const int InnMenuFirstOptionLabelLine = 5;
    private const int InnMenuFirstOptionDescriptionLine = 6;
    private const int InnMenuOptionLineStride = 2;
    private const int InnMenuFrameBaseLineCount = 7;
    private const int InnConfirmationFrameWidth = 90;
    private const int InnRestUnavailableFrameWidth = 90;
    internal const int InnMarketFrameWidth = 110;
    private const int InnRecruitmentFrameWidth = 100;
    private const int InnMarketPageSize = 22;
    private const int InnMarketTextWidth = 104;
    private const int InnMarketSelectedItemDetailLine = 28;
    private const int InnMarketFrameLineCount = 31;
    private const int InnRecruitmentFirstCandidateLine = 4;
    private const int InnRecruitmentDetailStartOffset = 5;
    private const int InnRecruitmentFrameBaseLineCount = 10;
    private const int InnReplacementFrameWidth = 90;
    private const int InnReplacementFirstMemberLine = 4;
    private const int InnReplacementFrameBaseLineCount = 7;
    internal const int InnRumorFrameWidth = 108;
    private const int InnRumorTextWidth = 100;
    internal const int WorldNpcRecruitmentFrameWidth = 72;
    public const int WorldNpcRecruitmentTextWidth = WorldNpcRecruitmentFrameWidth -
                                                      CenteredFrameHorizontalPadding * FrameBorderWidth;
    private const int DetailNextLineOffset = 1;
    private const int DetailSecondLineOffset = 2;
    private const int TruncationEllipsisReserve = 1;
    private const int FirstItemNumber = 1;
    private const int SecondItemNumber = 2;
    private const int SpellCastingOverlayFrameWidth = 76;
    private const int MaximumVisibleSpellCount = 12;
    private const int RightSheetX = 172;
    private const int RightSheetWidth = 27;
    private const int RightSheetBattleHintLine = 42;
    private const int SpellInfoKnownSpellRows = 20;
    private const int SpellInfoKnownSpellStartLine = 5;
    private const int SpellInfoSelectedSpellHeadingLine = 26;
    private const int SpellInfoSelectedSpellNameLine = 27;
    private const int SpellInfoSelectedSpellSummaryLine = 28;
    private const int SpellInfoSelectedSpellStateLine = 29;
    private const int SpellInfoDescriptionStartLine = 30;
    private const int SpellInfoDescriptionRows = 5;
    private const int SpellInfoDescriptionWidth = RightSheetWidth;
    private const int SpellInfoLevelsHeadingLine = 36;
    private const int SpellInfoNextUnlockLine = 43;
    private const int SpellInfoControlsLine = 45;
    private const int SpellInfoCloseControlsLine = 46;
    private const int SpellLevelCount = 5;
    private const int PaladinSpellLevelCount = 2;
    private const int FirstSpellUnlockLevel = 1;
    private const int SecondSpellUnlockLevel = 5;
    private const int ThirdSpellUnlockLevel = 10;
    private const int FourthSpellUnlockLevel = 15;
    private const int FifthSpellUnlockLevel = 20;
    private const int SecondPaladinSpellUnlockLevel = 8;
    private const int UnavailableSpellUnlockLevel = 99;
    private const int CharacterSheetPerkRows = 2;
    private const int CharacterSheetHeaderLine = 1;
    private const int CharacterSheetRaceClassLine = 1;
    private const int CharacterSheetFirstPerkLine = 2;
    private const int CharacterSheetSecondPerkLine = 3;
    private const int CharacterSheetStatusLine = 8;
    private const int CharacterSheetLevelLine = 5;
    private const int CharacterSheetExperienceLine = 6;
    private const int CharacterSheetStrengthLine = 7;
    private const int CharacterSheetDexterityLine = 8;
    private const int CharacterSheetHealthLine = 9;
    private const int CharacterSheetIntelligenceLine = 10;
    private const int CharacterSheetVitalityLine = 5;
    private const int CharacterSheetFoodLine = 13;
    private const int CharacterSheetWaterLine = 14;
    private const int CharacterSheetGoldLine = 15;
    private const int CharacterSheetWeaponsHeadingLine = 17;
    private const int CharacterSheetFirstWeaponLine = 18;
    private const int CharacterSheetSecondWeaponLine = 19;
    private const int CharacterSheetArmorLine = 20;
    private const int CharacterSheetMagicItemsHeadingLine = 22;
    private const int CharacterSheetMagicItemsStartLine = 23;
    private const int CharacterSheetBackpackHeadingLine = 26;
    private const int CharacterSheetBackpackStartLine = 27;
    private const int CharacterSheetPartyMembersStartLine = 41;
    private const int CharacterSheetMaximumMagicItems = 3;
    private const int CharacterSheetBackpackSlots = 12;
    private const int CharacterSheetPartyMemberRows = 4;
    private const int CharacterSheetReservedMessageLine = 39;
    private const int CharacterSheetControlsLine = 40;
    private const int ResourceIconStep = 10;
    private const int PortraitInteriorWidth = 25;
    private const int MessagePanelLeft = 2;
    private const int FirstMessageLineOffset = 1;
    private readonly Queue<MessageLogLine> _messageLog = new();
    private int _messageLogScrollOffset;
    private readonly GameDataCatalog _gameData;
    private readonly Party _party;
    private readonly Func<IReadOnlyList<LiveCharacter>> _temporaryFollowers;
    private int _mazeLevel;
    private int _goldenKeyCount;
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

    public ConsoleRenderer(GameDataCatalog gameData, Party party,
        Func<IReadOnlyList<LiveCharacter>>? temporaryFollowers = null)
    {
        _gameData = gameData;
        _party = party;
        _temporaryFollowers = temporaryFollowers ?? (() => []);
    }

    private static bool IsWindows11OrLater()
    {
        var version = Environment.OSVersion.Version;
        return OperatingSystem.IsWindows() && version.Major >= 10 && version.Build >= 22000;
    }

    public void SetGoldenKeyCount(int count) => _goldenKeyCount = Math.Clamp(count, 0, MonsterIds.Bosses.Count);

    public bool DrawThiefKeyChoice(LiveCharacter thief, Maze maze, FogOfWar fogOfWar, Position playerPosition)
    {
        _spellCastingOverlaySnapshot = null;
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("🔐  KULCSRA ZÁRT AJTÓ", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray),
            ($"{thief.Name} tolvajnál van egy használható kulcs.", ConsoleColor.Cyan),
            ("Felhasználod? A kulcs nyitás közben eltörik.", ConsoleColor.White),
            (string.Empty, ConsoleColor.Gray),
            ("I / Y / Enter: kulcs használata", ConsoleColor.Green),
            ("N / Esc: zárnyitási próba", ConsoleColor.DarkYellow)
        };
        DrawSpellCastingOverlay(SpellCastingOverlayFrameWidth, lines, maze, fogOfWar, playerPosition);
        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.I or ConsoleKey.Y or ConsoleKey.Enter)
            {
                RestoreSpellCastingOverlay();
                return true;
            }
            if (key is ConsoleKey.N or ConsoleKey.Escape)
            {
                RestoreSpellCastingOverlay();
                return false;
            }
        }
    }

    public bool DrawDoorSmashChoice(LiveCharacter leader, LiveCharacter thief, Maze maze,
        FogOfWar fogOfWar, Position playerPosition)
    {
        _spellCastingOverlaySnapshot = null;
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("🔨  A ZÁR ELLENÁLLT", ConsoleColor.Red),
            (string.Empty, ConsoleColor.Gray),
            ($"{thief.Name} nem tudta kinyitni az ajtót.", ConsoleColor.Yellow),
            ($"Megpróbálja {leader.Name} erővel betörni?", ConsoleColor.White),
            (string.Empty, ConsoleColor.Gray),
            ("I / Y / Enter: betörési kísérlet", ConsoleColor.Green),
            ("N / Esc: az ajtó maradjon zárva", ConsoleColor.DarkYellow)
        };
        DrawSpellCastingOverlay(SpellCastingOverlayFrameWidth, lines, maze, fogOfWar, playerPosition);
        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.I or ConsoleKey.Y or ConsoleKey.Enter)
            {
                RestoreSpellCastingOverlay();
                return true;
            }
            if (key is ConsoleKey.N or ConsoleKey.Escape)
            {
                RestoreSpellCastingOverlay();
                return false;
            }
        }
    }

    public void DrawBossIntroduction(EnemyDefinition boss, string chapterTitle, IReadOnlyList<string> speech,
        Maze maze, FogOfWar fogOfWar, Position playerPosition)
    {
        _spellCastingOverlaySnapshot = null;
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("⚔️👑  BOSS KÖZELEG  👑⚔️", ConsoleColor.Red),
            (chapterTitle, ConsoleColor.Magenta),
            ($"{boss.Appearance}  {boss.Name}", ConsoleColor.Yellow),
            ($"Erősség: {boss.StrengthTier}/5     Jutalom: 🔑 Aranykulcs", ConsoleColor.Cyan),
            ("────────────────────────────────────────────────────────────────────────────────────────────────────", ConsoleColor.DarkMagenta),
            (string.Empty, ConsoleColor.Gray),
        };
        foreach (var paragraph in speech)
        {
            lines.AddRange(WrapText(paragraph, InnRumorTextWidth - 2)
                .Select(line => ($"„{line}”", ConsoleColor.White)));
            lines.Add((string.Empty, ConsoleColor.Gray));
        }
        lines.Add(("Nyomj Entert a folytatáshoz...", ConsoleColor.Green));
        DrawSpellCastingOverlay(InnRumorFrameWidth, lines, maze, fogOfWar, playerPosition);
        while (Console.ReadKey(intercept: true).Key != ConsoleKey.Enter) { }
        RestoreSpellCastingOverlay();
    }

    public void DrawStoryOverlay(string title, string subtitle, IReadOnlyList<string> paragraphs,
        Maze maze, FogOfWar fogOfWar, Position playerPosition)
    {
        ShowStoryOverlay(title, subtitle, paragraphs, maze, fogOfWar, playerPosition);
        while (Console.ReadKey(intercept: true).Key != ConsoleKey.Enter) { }
        CloseStoryOverlay();
    }

    public void ShowStoryOverlay(string title, string subtitle, IReadOnlyList<string> paragraphs,
        Maze maze, FogOfWar fogOfWar, Position playerPosition,
        NarrativeKind kind = NarrativeKind.CampaignIntroduction, BossPresentationSnapshot? boss = null)
    {
        _spellCastingOverlaySnapshot = null;
        var lines = NarrativeWindow.Build(title, subtitle, paragraphs,
            "❖  Nyomj Entert a történet folytatásához...  ❖", kind: kind, boss: boss);
        DrawSpellCastingOverlay(NarrativeWindow.Width, lines, maze, fogOfWar, playerPosition,
            FramedWindow.Storyline);
    }

    public void CloseStoryOverlay() => RestoreSpellCastingOverlay();

    /// <summary>
    /// Inicializálja a teljes képernyős állapotot: törli a konzolt, kirajzolja a pályát,
    /// keretet, karakterlapot és a játékos pozícióját.
    /// </summary>
    public void DrawInitialState(Maze maze, Player player, FogOfWar fogOfWar, int mazeLevel)
    {
        ResetColorCache();
        _messageLog.Clear();
        _messageLogScrollOffset = 0;
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
        if (hasWon) DrawBattleMessage("Elérted a kijáratot! Nyomj Entert a fogadóba lépéshez.");
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
            ? $"{MoneyIcon}🎉 FŐNYEREMÉNY! +{goldAmount} arany (esély {jackpotChance}%, ×{rewardMultiplier})!"
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
        WriteSheetLine(RightSheetBattleHintLine, "Space: tovább | saját kör: V/F1-F8", ConsoleColor.DarkYellow);
    }

    /// <summary>
    /// Csata eredményének megjelenítése: visszaáll a nem-csata állapotra és kiírja az összegző üzenetet.
    /// </summary>
    public void DrawBattleResult(BattleResult result, Enemy enemy, string? message = null)
    {
        _battleActive = false;
        _battleEnemy = null;
        DrawPicturePanel();
        WriteSheetLine(RightSheetBattleHintLine, string.Empty, ConsoleColor.DarkCyan);
        DrawBattleMessage(message ?? FormatBattleResultMessage(result, enemy));
    }

    public static string FormatBattleResultMessage(BattleResult result, Enemy enemy) => result.PlayerWon
        ? $"GYŐZELEM 🏆: {enemy.Name} elesett."
        : $"Elestél {result.Rounds} kör után. {result.Events.LastOrDefault() ?? string.Empty}";

    public static string FormatBattleVictorySummary(BattleResult result, Enemy enemy, int vitalityLost,
        int manaLost, IEnumerable<string> gainedStatusIcons, int needLoss)
    {
        var vitality = vitalityLost > 0 ? $"❤️-{vitalityLost}" : string.Empty;
        var mana = manaLost > 0 ? $"🔷-{manaLost}" : string.Empty;
        var statuses = string.Concat(gainedStatusIcons.Where(icon => !string.IsNullOrWhiteSpace(icon)));
        return $"GYŐZELEM 🏆: {enemy.Name} elesett. ⌛{result.Rounds}{vitality}{mana}{statuses}" +
               $"🍖-{needLoss}💧-{needLoss}";
    }

    public static string FormatAutoBattleVictorySummary(BattleResult result, string fighterName, Enemy enemy,
        int vitalityLost, int manaLost, IEnumerable<string> gainedStatusIcons, int needLoss,
        int spellsCast, int experience)
    {
        var vitality = vitalityLost > 0 ? $"❤️-{vitalityLost}" : string.Empty;
        var mana = manaLost > 0 ? $"🔷-{manaLost}" : string.Empty;
        var statuses = string.Concat(gainedStatusIcons.Where(icon => !string.IsNullOrWhiteSpace(icon)));
        var spells = spellsCast > 0 ? $"📜{spellsCast}" : string.Empty;
        return $"AUTOCSATA 🏆: {fighterName} → {enemy.Name} elesett. ⌛{result.Rounds}{vitality}{mana}{statuses}" +
               $"🍖-{needLoss}💧-{needLoss}{spells}✨{experience}XP";
    }

    public static string FormatAutoBattleDefeatSummary(BattleResult result, string fighterName, Enemy enemy,
        int vitalityLost, int manaLost, IEnumerable<string> gainedStatusIcons, int needLoss, int spellsCast)
    {
        var mana = manaLost > 0 ? $"🔷-{manaLost}" : string.Empty;
        var statuses = string.Concat(gainedStatusIcons.Where(icon => !string.IsNullOrWhiteSpace(icon)));
        var spells = spellsCast > 0 ? $"📜{spellsCast}" : string.Empty;
        return $"AUTOCSATA 💀: {fighterName} elesett → {enemy.Name}. ⌛{result.Rounds}❤️-{vitalityLost}{mana}{statuses}" +
               $"🍖-{needLoss}💧-{needLoss}{spells}👹{enemy.CurrentHitPoints}HP";
    }

    /// <summary>
    /// Játék vége képernyő: középre rajzolt keret és szöveg, majd várakozás billentyűleütésre.
    /// </summary>
    public void DrawGameOver(string characterName)
    {
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
        DrawGameOverFrame(lines);
    }

    public static void DrawCoopGuestGameOver(string characterName) => DrawGameOverFrame(
    [
        "💀  JÁTÉK VÉGE  💀",
        string.Empty,
        $"{characterName}, elestél a labirintus mélyén.",
        "A kalandod ezen a ponton véget ért.",
        "A host csapata nélküled folytathatja az utat.",
        "Új coop játékhoz válassz vagy készíts egy élő karaktert,",
        "majd csatlakozz újra a host címére.",
        string.Empty,
        "Nyomj meg egy billentyűt a főmenühöz."
    ]);

    public void DrawCompanionDeath(string characterName)
    {
        DrawGameOverFrame(
        [
            "💀  EGY TÁRS ELBUKOTT  💀",
            string.Empty,
            $"{characterName} elhalálozott a labirintus mélyén.",
            "A vendég a Game Over képernyő után visszatér a főmenübe.",
            "Ha újra együtt játszanátok, válasszon vagy készítsen egy élő karaktert,",
            "majd csatlakozzon újra ugyanerre a host címre.",
            "A megmaradt csapattal addig is folytathatod a kalandot.",
            string.Empty,
            "Nyomj meg egy billentyűt a folytatáshoz."
        ]);
        ResetColorCache();
    }

    private static void DrawGameOverFrame(IReadOnlyList<string> lines)
    {
        Console.ResetColor();
        Console.Clear();

        var left = Math.Max(0, (Console.WindowWidth - GameOverFrameWidth) / FrameBorderWidth);
        var top = Math.Max(GameOverMinimumTop, (Console.WindowHeight - (lines.Count + 2)) / FrameBorderWidth);

        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.BackgroundColor = ConsoleColor.Black;
        WriteAt(left, top, "╔" + new string('═', GameOverFrameWidth - FrameBorderWidth) + "╗");
        for (var index = 0; index < lines.Count; index++)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.BackgroundColor = ConsoleColor.Black;
            WriteAt(left, top + index + 1, "║");
            Console.ForegroundColor = index == 0 ? ConsoleColor.Red :
                index == lines.Count - 1 ? ConsoleColor.Yellow : ConsoleColor.Gray;
            WriteAt(left + CenteredFrameHorizontalPadding, top + index + 1,
                lines[index].PadRight(GameOverFrameWidth - CenteredFrameHorizontalPadding * FrameBorderWidth));
            Console.ForegroundColor = ConsoleColor.DarkRed;
            WriteAt(left + GameOverFrameWidth - 1, top + index + 1, "║");
        }
        Console.ForegroundColor = ConsoleColor.DarkRed;
        WriteAt(left, top + lines.Count + 1, "╚" + new string('═', GameOverFrameWidth - FrameBorderWidth) + "╝");
        Console.ReadKey(intercept: true);
        Console.ResetColor();
    }

    public void DrawLevelCompletionScreen(LevelCompletionSnapshot completion)
    {
        ResetColorCache();
        Console.Clear();
        DrawCenteredFrame(LevelCompletionFrameWidth, BuildLevelCompletionLines(completion), FramedWindow.Inn);
    }

    internal static IReadOnlyList<(string Text, ConsoleColor Color)> BuildLevelCompletionLines(
        LevelCompletionSnapshot completion)
    {
        var reward = checked(completion.BaseExperience * completion.CompletedLevel);
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("🏆✨  PÁLYA TELJESÍTVE!  ✨🏆", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray),
            ($"🚪 A parti kijutott a(z) {completion.CompletedLevel}. labirintusszintről.", ConsoleColor.Green),
            ($"📜 Teljesítési XP: {completion.BaseExperience} × {completion.CompletedLevel} = {reward} XP minden túlélő partitag számára", ConsoleColor.Cyan),
            (string.Empty, ConsoleColor.Gray),
            ("🏰🍺  PIHENŐ A FOGADÓBAN  🍲🛏️", ConsoleColor.DarkYellow)
        };
        foreach (var character in completion.Survivors)
        {
            var levelText = character.CurrentLevel > character.PreviousLevel
                ? $"  ⭐ L{character.PreviousLevel}→L{character.CurrentLevel}"
                : $"  L{character.CurrentLevel}";
            var manaText = character.UsesMana
                ? $"  🔷 {character.CurrentMana}/{character.MaximumMana} manna"
                : string.Empty;
            lines.Add(($"✨ {character.Name,-13} +{character.GainedExperience} XP{levelText}  ❤️ {character.CurrentVitality}/{character.MaximumVitality} HP{manaText}", character.Color));
        }
        if (completion.FallenCharacters.Count > 0)
        {
            lines.Add((string.Empty, ConsoleColor.Gray));
            lines.Add(("💀  A fogadóig nem jutottak el — végleg elvesztek:", ConsoleColor.Red));
            foreach (var fallen in completion.FallenCharacters)
                lines.Add(($"† {fallen.Name} ({fallen.CharacterClassName})", ConsoleColor.DarkRed));
        }
        lines.AddRange([
            (string.Empty, ConsoleColor.Gray),
            (" A fogadó kereskedői már várnak a portékáikkal...", ConsoleColor.Magenta),
            (string.Empty, ConsoleColor.Gray),
            ("Nyomj Entert vagy Space-t a fogadó megnyitásához! ➡️", ConsoleColor.Yellow)
        ]);
        return lines;
    }

    internal static IReadOnlyList<(string Text, ConsoleColor Color)> BuildInnMenuLines(int partyCount,
        int partyGold, IReadOnlyList<InnMenuOptionSnapshot> options, int selectedIndex, string artisanNotice,
        bool disableLeaderOnly, string innName = "Vándorcsillag", int mazeLevel = 1)
    {
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ($"🏰🍺  {innName} fogadója, {mazeLevel}. szint  🍺🏰", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray),
            ($"Parti: {partyCount}/{Party.MaximumSize} fő     {MoneyIcon} Arany: {partyGold}", ConsoleColor.Cyan),
            (ClipMarketText(artisanNotice, InnMenuFrameWidth - 6), ConsoleColor.DarkYellow),
            (string.Empty, ConsoleColor.Gray)
        };
        for (var index = 0; index < options.Count; index++)
        {
            var selected = index == selectedIndex;
            var disabled = disableLeaderOnly && options[index].LeaderOnly;
            lines.Add(($"{(selected ? "▶" : " ")} {options[index].Label}", disabled
                ? ConsoleColor.DarkGray : selected ? ConsoleColor.Yellow : ConsoleColor.Gray));
            lines.Add(($"     {options[index].Description}", disabled
                ? ConsoleColor.DarkGray : selected ? ConsoleColor.White : ConsoleColor.DarkGray));
        }
        lines.Add((string.Empty, ConsoleColor.Gray));
        lines.Add((disableLeaderOnly ? "↑/↓ választás   Enter belépés   Tab karakterlap   Szürke: csak a leader"
            : "↑/↓ választás   Enter belépés   Tab karakterlap", ConsoleColor.Green));
        return lines;
    }

    internal static IReadOnlyList<(string Text, ConsoleColor Color)> BuildInnVendorLines(
        InnVendorSnapshot vendor, InnMarketMode mode,
        IReadOnlyList<(InventoryItemSnapshot Item, int Price, string OwnerName)> sellOffers,
        int selectedIndex, int partyGold, int freeBackpackSlots, string message)
    {
        var buying = vendor.Kind != InnVendorKind.Market || mode == InnMarketMode.Buy;
        var entryCount = buying ? vendor.Offers.Count : sellOffers.Count;
        var pageStart = InnMarketPageStart(entryCount, selectedIndex);
        var title = vendor.Kind switch
        {
            InnVendorKind.Blacksmith => "🏰🍺  🔨 KOVÁCSMESTER  ✨",
            InnVendorKind.Armorer => "🏰🍺  🛡️ PÁNCÉLMÍVES  ✨",
            InnVendorKind.WanderingMage => "🏰🍺  🧙 VÁNDORMÁGUS PORTÉKÁI  ✨",
            _ => "🏰🍺  A VÁNDORCSILLAG FOGADÓ KERESKEDŐJE  🛒✨"
        };
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            (title, ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray)
        };
        var usesMarketLayout = vendor.Kind is InnVendorKind.Market or InnVendorKind.Witcher;
        if (usesMarketLayout)
            lines.Add((buying ? "◀  [ VÁSÁRLÁS ]     ELADÁS  ▶" : "◀    VÁSÁRLÁS     [ ELADÁS ]  ▶",
                ConsoleColor.Cyan));
        else
            lines.Add(("Csak vásárlás — a kínálat és az árak a fogadóba érkezéskor rögzültek.",
                ConsoleColor.DarkYellow));
        lines.Add(($"{MoneyIcon} Közös arany: {partyGold}     🎒 Szabad hátizsákhely: {freeBackpackSlots}",
            ConsoleColor.Green));
        lines.Add((new string('─', 92), ConsoleColor.DarkMagenta));
        for (var row = 0; row < InnMarketPageSize; row++)
        {
            var index = pageStart + row;
            if (index >= entryCount) { lines.Add((string.Empty, ConsoleColor.Gray)); continue; }
            var selected = index == selectedIndex;
            if (buying)
            {
                var offer = vendor.Offers[index];
                lines.Add(($"{(selected ? "▶" : " ")} {ItemCategoryIcon(offer.Item.Category)} {offer.Item.Name,-24} alapár {offer.Item.BasePrice,5}   fogadói ár {offer.Price,5} {MoneyIcon}",
                    selected ? ConsoleColor.White : ItemRarityColor(offer.Item.Rarity)));
            }
            else
            {
                var offer = sellOffers[index];
                var itemName = offer.Item.Name + (offer.Item.Quantity > 1 ? $" ×{offer.Item.Quantity}" : string.Empty);
                lines.Add(($"{(selected ? "▶" : " ")} {ItemCategoryIcon(offer.Item.Category)} {itemName,-22} {offer.OwnerName,-13} ajánlat {offer.Price,5} {MoneyIcon}",
                    selected ? ConsoleColor.White : ItemRarityColor(offer.Item.Rarity)));
            }
        }
        var selectedItem = entryCount == 0 ? null : buying
            ? vendor.Offers[selectedIndex].Item
            : sellOffers[selectedIndex].Item;
        lines.Add((new string('─', 92), ConsoleColor.DarkMagenta));
        lines.Add((selectedItem is null
            ? buying ? "Nincs több megvásárolható portéka." : "Nincs eladható tárgy a hátizsákodban."
            : ClipMarketText($"ℹ️ {selectedItem.Description}", InnMarketTextWidth), ConsoleColor.DarkCyan));
        lines.Add((ClipMarketText(message, InnMarketTextWidth), ConsoleColor.Magenta));
        lines.Add((usesMarketLayout
            ? "↑/↓ választás   ←/→ vétel–eladás   Enter üzlet   Tab karakterlap   Esc vissza"
            : "↑/↓ választás   Enter vásárlás   Tab karakterlap   Esc vissza", ConsoleColor.White));
        return lines;
    }

    internal static IReadOnlyList<(string Text, ConsoleColor Color)> BuildWanderingMageMenuLines(int partyGold,
        IReadOnlyList<(string Label, string Description, bool Disabled)> options, int selectedIndex, string message)
    {
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("🧙✨  A VÁNDORMÁGUS  ✨🧙", ConsoleColor.Magenta),
            ($"{MoneyIcon} Közös arany: {partyGold}", ConsoleColor.Green),
            (ClipMarketText(message, InnMenuFrameWidth - 6), ConsoleColor.Cyan),
            (string.Empty, ConsoleColor.Gray)
        };
        for (var index = 0; index < options.Count; index++)
        {
            var selected = index == selectedIndex;
            var disabled = options[index].Disabled;
            lines.Add(($"{(selected ? "▶" : " ")} {options[index].Label}", disabled
                ? ConsoleColor.DarkGray : selected ? ConsoleColor.Yellow : ConsoleColor.Gray));
            lines.Add(($"     {options[index].Description}", disabled
                ? ConsoleColor.DarkGray : selected ? ConsoleColor.White : ConsoleColor.DarkGray));
        }
        lines.Add((string.Empty, ConsoleColor.Gray));
        lines.Add(("↑/↓ választás   Enter belépés   Esc vissza", ConsoleColor.Green));
        return lines;
    }

    internal static IReadOnlyList<(string Text, ConsoleColor Color)> BuildInnRumorLines(
        InnRumorSnapshot rumor, int selectedIndex, int rumorCount, string? notice = null)
    {
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("🏰🍺  PLETYKÁK A VÁNDORCSILLAG FOGADÓBAN  👂📜", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray),
            (rumor.Title, rumor.Color),
            (new string('─', 92), ConsoleColor.DarkMagenta)
        };
        foreach (var paragraph in rumor.Lines)
        {
            foreach (var line in WrapText(paragraph, InnRumorTextWidth)) lines.Add((line, ConsoleColor.Gray));
            lines.Add((string.Empty, ConsoleColor.Gray));
        }
        lines.Add((string.IsNullOrWhiteSpace(notice) ? string.Empty : ClipMarketText(notice, InnMarketTextWidth),
            ConsoleColor.Yellow));
        lines.Add((new string('─', 92), ConsoleColor.DarkMagenta));
        lines.Add(($"Pletyka {selectedIndex + 1}/{rumorCount}   ←/→ vagy N: lapozás   Enter/Esc: vissza a fogadóba",
            ConsoleColor.White));
        return lines;
    }

    private static string ItemCategoryIcon(ItemCategory category) => category switch
    {
        ItemCategory.Weapon => "⚔️",
        ItemCategory.Armor => "🛡️",
        ItemCategory.MagicItem => "🔮",
        _ => "📦"
    };

    public void DrawInnMenuScreen(LiveCharacter leader, int partyCount, int selectedIndex,
        IReadOnlyList<InnMenuOptionSnapshot> options, string artisanNotice, string innName, int mazeLevel)
    {
        ResetColorCache();
        Console.Clear();
        DrawCenteredFrame(InnMenuFrameWidth, BuildInnMenuLines(partyCount, leader.Gold, options,
            selectedIndex, artisanNotice, disableLeaderOnly: false, innName, mazeLevel), FramedWindow.Inn);
    }

    public void DrawInnCharacterSheet(LiveCharacter character)
    {
        ResetColorCache();
        Console.Clear();
        DrawFrame();
        if (_displayedCharacter is null || !SheetCharacters().Contains(_displayedCharacter))
            _displayedCharacter = character;
        DrawCharacterSheet(_displayedCharacter);
        SetCharacterSheetFocused(true);
        DrawBattleMessage("Fogadói karakterlap — Tab: vissza a fogadóba | ↑/↓: választás | ←/→: karakter");
    }

    public void UpdateInnMenuSelection(IReadOnlyList<InnMenuOptionSnapshot> options,
        int previousIndex, int selectedIndex)
    {
        var updates = new List<(int Index, string Text, ConsoleColor Color)>();
        foreach (var index in new[] { previousIndex, selectedIndex }.Distinct())
        {
            var selected = index == selectedIndex;
            updates.Add((InnMenuFirstOptionLabelLine + index * InnMenuOptionLineStride, $"{(selected ? "▶" : " ")} {options[index].Label}",
                selected ? ConsoleColor.Yellow : ConsoleColor.Gray));
            updates.Add((InnMenuFirstOptionDescriptionLine + index * InnMenuOptionLineStride, $"     {options[index].Description}",
                selected ? ConsoleColor.White : ConsoleColor.DarkGray));
        }
        UpdateCenteredFrameLines(InnMenuFrameWidth, InnMenuFrameBaseLineCount + options.Count * InnMenuOptionLineStride,
            updates, FramedWindow.Inn);
    }

    public bool ConfirmInnSecretStashAccess(int cost)
    {
        ResetColorCache();
        Console.Clear();
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("🗝️🛒  A KERESKEDŐ TITKOS RAKTÁRA  🛒🗝️", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray),
            ($"A kereskedő {cost} aranyért hajlandó megmutatni a pult mögötti készletét.", ConsoleColor.Cyan),
            ("A belépődíj nem jár vissza. Megfizeted?", ConsoleColor.DarkYellow),
            (string.Empty, ConsoleColor.Gray),
            ("Enter: igen   Esc: mégsem", ConsoleColor.Green)
        };
        DrawCenteredFrame(InnConfirmationFrameWidth, lines, FramedWindow.Inn);
        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.Enter) return true;
            if (key == ConsoleKey.Escape) return false;
        }
    }

    public void DrawInnRestUnavailableScreen()
    {
        ResetColorCache();
        Console.Clear();
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("🛏️  PIHENÉS  🛏️", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray),
            ("A parti már kipihente magát ebben a fogadóban.", ConsoleColor.Red),
            ("A küldetés sürgető — nincs idő újra ledőlni, tovább kell indulni!", ConsoleColor.DarkYellow),
            (string.Empty, ConsoleColor.Gray),
            ("Nyomj Entert a folytatáshoz...", ConsoleColor.Yellow)
        };
        DrawCenteredFrame(InnRestUnavailableFrameWidth, lines, FramedWindow.Inn);
        while (Console.ReadKey(intercept: true).Key != ConsoleKey.Enter) { }
    }

    public void DrawInnMarketScreen(LiveCharacter leader, InnMarketMode mode,
        IReadOnlyList<InnStockOffer> stock, IReadOnlyList<InnSellOffer> sellOffers,
        int selectedIndex, int freeBackpackSlots, string message)
    {
        ResetColorCache();
        Console.Clear();
        var vendor = new InnVendorSnapshot(InnVendorKind.Market, "Kereskedő", stock.Select((offer, index) =>
            new InnOfferSnapshot(index, ToInventoryItemSnapshot(offer.Item), offer.Price)).ToArray());
        var sales = sellOffers.Select(offer => (ToInventoryItemSnapshot(offer.Item) with
            {
                Quantity = offer.Owner.GetInventoryItemQuantity(InventorySlotKind.Backpack, offer.BackpackIndex)
            }, offer.Price, offer.Owner.Name)).ToArray();
        DrawCenteredFrame(InnMarketFrameWidth, BuildInnVendorLines(vendor, mode, sales, selectedIndex,
            leader.Gold, freeBackpackSlots, message), FramedWindow.Inn);
    }

    public void DrawInnSecretStashScreen(LiveCharacter leader, IReadOnlyList<InnStockOffer> stock,
        int selectedIndex, int freeBackpackSlots, string message)
    {
        ResetColorCache();
        Console.Clear();
        var entryCount = stock.Count;
        var pageStart = InnMarketPageStart(entryCount, selectedIndex);
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("🗝️🛒  A KERESKEDŐ TITKOS RAKTÁRA  🛒🗝️", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray),
            ("Fejlettebb, ritkább árucikkek — csak a beavatottaknak, borsos áron.", ConsoleColor.DarkYellow),
            ($"{MoneyIcon} {leader.Name} aranya: {leader.Gold}     🎒 Szabad parti-hátizsákhely: {freeBackpackSlots}", ConsoleColor.Green),
            ("────────────────────────────────────────────────────────────────────────────────────────────", ConsoleColor.DarkMagenta)
        };
        for (var row = 0; row < InnMarketPageSize; row++)
        {
            var index = pageStart + row;
            if (index >= entryCount) { lines.Add((string.Empty, ConsoleColor.Gray)); continue; }
            var selected = index == selectedIndex;
            var offer = stock[index];
            lines.Add(($"{(selected ? "▶" : " ")} {ItemCategoryIcon(offer.Item)} {offer.Item.Name,-24} alapár {offer.Item.BasePrice,5}   fogadói ár {offer.Price,5} {MoneyIcon}",
                selected ? ConsoleColor.White : ItemRarityColor(offer.Item.Rarity)));
        }
        var selectedItem = entryCount == 0 ? null : stock[selectedIndex].Item;
        lines.Add(("────────────────────────────────────────────────────────────────────────────────────────────", ConsoleColor.DarkMagenta));
        lines.Add((selectedItem is null ? "Nincs több megvásárolható portéka." : ClipMarketText($"ℹ️ {selectedItem.Description}", InnMarketTextWidth), ConsoleColor.DarkCyan));
        lines.Add((ClipMarketText(message, InnMarketTextWidth), ConsoleColor.Magenta));
        lines.Add(("↑/↓ választás   Enter vásárlás   Esc vissza a fogadóba", ConsoleColor.White));
        DrawCenteredFrame(InnMarketFrameWidth, lines, FramedWindow.Inn);
    }

    public void DrawInnSpecialistScreen(string title, LiveCharacter leader, IReadOnlyList<InnStockOffer> stock,
        int selectedIndex, int freeBackpackSlots, string message)
    {
        ResetColorCache();
        Console.Clear();
        var kind = title.Contains("KOVÁCS", StringComparison.OrdinalIgnoreCase) ? InnVendorKind.Blacksmith
            : title.Contains("PÁNCÉL", StringComparison.OrdinalIgnoreCase) ? InnVendorKind.Armorer
            : InnVendorKind.WanderingMage;
        var vendor = new InnVendorSnapshot(kind, title, stock.Select((offer, index) =>
            new InnOfferSnapshot(index, ToInventoryItemSnapshot(offer.Item), offer.Price)).ToArray());
        DrawCenteredFrame(InnMarketFrameWidth, BuildInnVendorLines(vendor, InnMarketMode.Buy, [], selectedIndex,
            leader.Gold, freeBackpackSlots, message), FramedWindow.Inn);
    }

    private static InventoryItemSnapshot ToInventoryItemSnapshot(IItemDefinition item) => new(item.Id, item.Name,
        item.Category, item.Rarity, item is MagicItemDefinition magic ? magic.MaximumCharges : 0,
        item is MagicItemDefinition magicItem ? magicItem.MaximumCharges : 0,
        item is WeaponDefinition { IsTwoHanded: true }, item.Description, item.BasePrice, item.MagicPower);

    public void DrawWanderingMageMenu(LiveCharacter leader, IReadOnlyList<(string Label, string Description)> options,
        int selectedIndex, string message)
    {
        ResetColorCache();
        Console.Clear();
        DrawCenteredFrame(InnMenuFrameWidth, BuildWanderingMageMenuLines(leader.Gold,
            options.Select(option => (option.Label, option.Description, Disabled: false)).ToArray(),
            selectedIndex, message), FramedWindow.Inn);
    }

    public void DrawWandRechargeScreen(LiveCharacter leader,
        IReadOnlyList<(string Owner, string Item, int Price, int Charges)> wands, int selectedIndex, string message)
    {
        ResetColorCache();
        Console.Clear();
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ($"{WandIcon}✨  VARÁZSPÁLCA-FELTÖLTÉS  ✨{WandIcon}", ConsoleColor.Magenta),
            ($"{MoneyIcon} {leader.Name} aranya: {leader.Gold}", ConsoleColor.Green),
            ("A teljes feltöltés díja a pálca eredeti árának kétharmada.", ConsoleColor.DarkYellow),
            (string.Empty, ConsoleColor.Gray)
        };
        if (wands.Count == 0) lines.Add(("Nincs teljesen kiürült varázspálca a partinál.", ConsoleColor.DarkGray));
        else
            for (var index = 0; index < wands.Count; index++)
            {
                var wand = wands[index];
                var selected = index == selectedIndex;
                lines.Add(($"{(selected ? "▶" : " ")} {wand.Owner,-13}  {wand.Item,-28} → {wand.Charges} töltet   {wand.Price} {MoneyIcon}",
                    selected ? ConsoleColor.White : ConsoleColor.Cyan));
            }
        lines.Add((string.Empty, ConsoleColor.Gray));
        lines.Add((ClipMarketText(message, InnMarketFrameWidth - 6), ConsoleColor.Magenta));
        lines.Add(("↑/↓ választás   Enter feltöltés   Esc vissza", ConsoleColor.Green));
        DrawCenteredFrame(InnMenuFrameWidth, lines, FramedWindow.Inn);
    }

    public void UpdateInnMarketSelection(InnMarketMode mode, IReadOnlyList<InnStockOffer> stock,
        IReadOnlyList<InnSellOffer> sellOffers, int previousIndex, int selectedIndex)
    {
        var entries = mode == InnMarketMode.Buy ? stock.Count : sellOffers.Count;
        var previousPageStart = InnMarketPageStart(entries, previousIndex);
        var pageStart = InnMarketPageStart(entries, selectedIndex);
        var updates = new List<(int Index, string Text, ConsoleColor Color)>();
        var visibleIndices = previousPageStart == pageStart
            ? new[] { previousIndex, selectedIndex }.Distinct()
            : Enumerable.Range(pageStart, Math.Min(InnMarketPageSize, entries - pageStart));
        foreach (var index in visibleIndices)
        {
            var selected = index == selectedIndex;
            var text = mode == InnMarketMode.Buy
                ? InnStockLine(stock[index], selected)
                : InnSellLine(sellOffers[index], selected);
            var color = selected ? ConsoleColor.White : ItemRarityColor(mode == InnMarketMode.Buy
                ? stock[index].Item.Rarity : sellOffers[index].Item.Rarity);
            updates.Add((InnMenuFirstOptionLabelLine + index - pageStart, text, color));
        }
        if (previousPageStart != pageStart)
        {
            for (var row = entries - pageStart; row < InnMarketPageSize; row++)
                updates.Add((InnMenuFirstOptionLabelLine + row, string.Empty, ConsoleColor.Gray));
        }
        var selectedItem = mode == InnMarketMode.Buy ? stock[selectedIndex].Item : sellOffers[selectedIndex].Item;
        updates.Add((InnMarketSelectedItemDetailLine, ClipMarketText($"ℹ️ {selectedItem.Description}", InnMarketTextWidth), ConsoleColor.DarkCyan));
        UpdateCenteredFrameLines(InnMarketFrameWidth, InnMarketFrameLineCount, updates, FramedWindow.Inn);
    }

    public void UpdateInnBuyOnlySelection(IReadOnlyList<InnStockOffer> stock, int previousIndex, int selectedIndex)
    {
        var previousPageStart = InnMarketPageStart(stock.Count, previousIndex);
        var pageStart = InnMarketPageStart(stock.Count, selectedIndex);
        var updates = new List<(int Index, string Text, ConsoleColor Color)>();
        var visibleIndices = previousPageStart == pageStart
            ? new[] { previousIndex, selectedIndex }.Distinct()
            : Enumerable.Range(pageStart, Math.Min(InnMarketPageSize, stock.Count - pageStart));
        foreach (var index in visibleIndices)
        {
            var selected = index == selectedIndex;
            updates.Add((InnMenuFirstOptionLabelLine + index - pageStart, InnStockLine(stock[index], selected),
                selected ? ConsoleColor.White : ItemRarityColor(stock[index].Item.Rarity)));
        }
        if (previousPageStart != pageStart)
        {
            for (var row = stock.Count - pageStart; row < InnMarketPageSize; row++)
                updates.Add((InnMenuFirstOptionLabelLine + row, string.Empty, ConsoleColor.Gray));
        }
        updates.Add((InnMarketSelectedItemDetailLine, ClipMarketText($"ℹ️ {stock[selectedIndex].Item.Description}", InnMarketTextWidth), ConsoleColor.DarkCyan));
        UpdateCenteredFrameLines(InnMarketFrameWidth, InnMarketFrameLineCount, updates, FramedWindow.Inn);
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
            ($"Parti: {party.Count}/{Party.MaximumSize} fő     {MoneyIcon} Arany: {leaderGold}", ConsoleColor.Cyan),
            ("────────────────────────────────────────────────────────────────────────────────────────────", ConsoleColor.DarkMagenta)
        };
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var selected = index == selectedIndex;
            var mana = candidate.UsesMana ? $"  MP {candidate.MaximumMana}" : string.Empty;
            var price = prices[candidate] == 0 ? "INGYEN" : $"{prices[candidate]} {MoneyIcon}";
            lines.Add(($"{(selected ? "▶" : " ")} {candidate.Name,-13}  {candidate.Race.Name,-10} {candidate.CharacterClass.Name,-10} L{candidate.Level,2}  HP {candidate.MaximumVitality}{mana}  {price}",
                selected ? ConsoleColor.White : candidate.Color));
        }
        var shown = candidates[selectedIndex];
        var weaponNames = shown.WeaponSlots.Where(item => item is not null).Select(item => item!.Name).ToList();
        var weapons = weaponNames.Count == 0 ? "nincs" : string.Join(", ", weaponNames);
        lines.Add(("────────────────────────────────────────────────────────────────────────────────────────────", ConsoleColor.DarkMagenta));
        lines.Add(($"Erő {shown.EffectiveAbilities.Strength}  Ügy {shown.EffectiveAbilities.Dexterity}  Egész {shown.EffectiveAbilities.Health}  Int {shown.EffectiveAbilities.Intelligence}", ConsoleColor.Cyan));
        lines.Add(($"Fegyver: {weapons}  |  Páncél: {shown.Armor?.Name ?? "nincs"}", ConsoleColor.Gray));
        lines.Add(($"Hátizsák: {string.Join(", ", shown.Backpack.Where(item => item is not null).Select(item => item!.Name))}", ConsoleColor.DarkCyan));
        lines.Add((ClipMarketText(message, InnMarketTextWidth), ConsoleColor.Magenta));
        lines.Add((party.Count >= Party.MaximumSize
            ? "↑/↓ választás   Enter felvétel és társ lecserélése   Esc vissza a fogadóba"
            : "↑/↓ választás   Enter felvétel   Esc vissza a fogadóba", ConsoleColor.White));
        DrawCenteredFrame(InnRecruitmentFrameWidth, lines, FramedWindow.Inn);
    }

    public void DrawInnReplacementScreen(LiveCharacter recruit, IReadOnlyList<LiveCharacter> replaceable,
        int selectedIndex, string? notice = null)
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
        lines.Add((string.IsNullOrWhiteSpace(notice) ? string.Empty : ClipMarketText(notice, InnMarketTextWidth),
            ConsoleColor.Yellow));
        lines.Add((string.Empty, ConsoleColor.Gray));
        lines.Add(("↑/↓ választás   Enter végleges csere   Esc mégse", ConsoleColor.White));
        DrawCenteredFrame(InnReplacementFrameWidth, lines, FramedWindow.Inn);
    }

    public void UpdateInnRecruitmentSelection(IReadOnlyList<LiveCharacter> candidates,
        IReadOnlyDictionary<LiveCharacter, int> prices, int previousIndex, int selectedIndex)
    {
        var updates = new List<(int Index, string Text, ConsoleColor Color)>();
        foreach (var index in new[] { previousIndex, selectedIndex }.Distinct())
        {
            var candidate = candidates[index];
            var selected = index == selectedIndex;
            updates.Add((InnRecruitmentFirstCandidateLine + index, InnRecruitLine(candidate, prices[candidate], selected),
                selected ? ConsoleColor.White : candidate.Color));
        }
        var shown = candidates[selectedIndex];
        var weaponNames = shown.WeaponSlots.Where(item => item is not null).Select(item => item!.Name).ToList();
        var weapons = weaponNames.Count == 0 ? "nincs" : string.Join(", ", weaponNames);
        var detailStart = InnRecruitmentDetailStartOffset + candidates.Count;
        updates.Add((detailStart, $"Erő {shown.EffectiveAbilities.Strength}  Ügy {shown.EffectiveAbilities.Dexterity}  Egész {shown.EffectiveAbilities.Health}  Int {shown.EffectiveAbilities.Intelligence}", ConsoleColor.Cyan));
        updates.Add((detailStart + DetailNextLineOffset, $"Fegyver: {weapons}  |  Páncél: {shown.Armor?.Name ?? "nincs"}", ConsoleColor.Gray));
        updates.Add((detailStart + DetailSecondLineOffset, $"Hátizsák: {string.Join(", ", shown.Backpack.Where(item => item is not null).Select(item => item!.Name))}", ConsoleColor.DarkCyan));
        UpdateCenteredFrameLines(InnRecruitmentFrameWidth, InnRecruitmentFrameBaseLineCount + candidates.Count,
            updates, FramedWindow.Inn);
    }

    public void UpdateInnReplacementSelection(IReadOnlyList<LiveCharacter> replaceable,
        int previousIndex, int selectedIndex)
    {
        var updates = new List<(int Index, string Text, ConsoleColor Color)>();
        foreach (var index in new[] { previousIndex, selectedIndex }.Distinct())
        {
            var member = replaceable[index];
            var selected = index == selectedIndex;
            updates.Add((InnReplacementFirstMemberLine + index, InnReplacementLine(member, selected), selected ? ConsoleColor.White : member.Color));
        }
        UpdateCenteredFrameLines(InnReplacementFrameWidth, InnReplacementFrameBaseLineCount + replaceable.Count,
            updates, FramedWindow.Inn);
    }

    public void DrawInnRumorScreen(InnRumor rumor, int selectedIndex, int rumorCount, string? notice = null)
    {
        ResetColorCache();
        Console.Clear();
        DrawCenteredFrame(InnRumorFrameWidth, BuildInnRumorLines(
            new InnRumorSnapshot(rumor.Title, rumor.Lines, rumor.Color), selectedIndex, rumorCount, notice),
            FramedWindow.Inn);
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

    private static string ItemCategoryIcon(IItemDefinition item) => ItemCategoryIcon(item.Category);

    private static ConsoleColor ItemRarityColor(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Magic => ConsoleColor.Cyan,
        ItemRarity.Legendary => ConsoleColor.Yellow,
        _ => ConsoleColor.Gray
    };

    private static string ClipMarketText(string text, int maximumLength) =>
        text.Length <= maximumLength ? text : text[..Math.Max(TruncationEllipsisReserve, maximumLength - TruncationEllipsisReserve)] + "…";
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

    public ClassSpecializationDefinition DrawSpecializationChoice(LiveCharacter character,
        IReadOnlyList<ClassSpecializationDefinition> choices)
    {
        if (choices.Count == 0) throw new ArgumentException("Nincs választható specializáció.", nameof(choices));
        var selectedIndex = 0;
        while (true)
        {
            ResetColorCache();
            Console.Clear();
            var lines = LevelUpWindow.BuildChoice(LevelUpPromptKind.SpecializationChoice,
                [new($"{character.Name} — {character.CharacterClass.Name}", ConsoleColor.Cyan),
                 new("Ez a választás végleges.", ConsoleColor.Red)],
                choices.Select(choice => new LevelUpChoiceSnapshot(choice.Id, choice.Name, choice.Description)).ToArray(),
                selectedIndex);
            DrawCenteredFrame(LevelUpWindow.ChoiceWidth(LevelUpPromptKind.SpecializationChoice), lines,
                FramedWindow.LevelUpChoice);
            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.UpArrow:
                case ConsoleKey.LeftArrow:
                    selectedIndex = (selectedIndex - 1 + choices.Count) % choices.Count;
                    break;
                case ConsoleKey.DownArrow:
                case ConsoleKey.RightArrow:
                    selectedIndex = (selectedIndex + 1) % choices.Count;
                    break;
                case ConsoleKey.Enter:
                    return choices[selectedIndex];
            }
        }
    }

    public ClassFeatureUpgradeDefinition DrawClassFeatureUpgradeChoice(LiveCharacter character,
        IReadOnlyList<ClassFeatureUpgradeDefinition> choices, int milestone)
    {
        if (choices.Count == 0) throw new ArgumentException("Nincs választható osztályképesség-fejlesztés.", nameof(choices));
        var selectedIndex = 0;
        while (true)
        {
            ResetColorCache();
            Console.Clear();
            var lines = LevelUpWindow.BuildChoice(LevelUpPromptKind.ClassFeatureChoice,
                [new($"{character.Name} — {character.CharacterClass.Name} — {milestone}. szint", ConsoleColor.Cyan),
                 new("A választás végleges; a 20. szinten egy másik fejlesztés választható.", ConsoleColor.Red)],
                choices.Select(choice => new LevelUpChoiceSnapshot(choice.Id, choice.Name, choice.Description)).ToArray(),
                selectedIndex);
            DrawCenteredFrame(LevelUpWindow.ChoiceWidth(LevelUpPromptKind.ClassFeatureChoice), lines,
                FramedWindow.LevelUpChoice);
            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.UpArrow:
                case ConsoleKey.LeftArrow:
                    selectedIndex = (selectedIndex - 1 + choices.Count) % choices.Count;
                    break;
                case ConsoleKey.DownArrow:
                case ConsoleKey.RightArrow:
                    selectedIndex = (selectedIndex + 1) % choices.Count;
                    break;
                case ConsoleKey.Enter:
                    return choices[selectedIndex];
            }
        }
    }

    public string DrawAbilityIncreaseChoice(LiveCharacter character,
        IReadOnlyList<(string Id, string Name, string Description)> choices, int milestone)
    {
        if (choices.Count == 0) throw new ArgumentException("Nincs növelhető képesség.", nameof(choices));
        var selectedIndex = 0;
        while (true)
        {
            ResetColorCache();
            Console.Clear();
            var lines = LevelUpWindow.BuildChoice(LevelUpPromptKind.AbilityChoice,
                [new($"{character.Name} — {milestone}. szint", ConsoleColor.Cyan),
                 new("Növelj meg egy képességet 1 ponttal! Maximum: 13.", ConsoleColor.Green)],
                choices.Select(choice => new LevelUpChoiceSnapshot(choice.Id, choice.Name, choice.Description)).ToArray(),
                selectedIndex);
            DrawCenteredFrame(LevelUpWindow.ChoiceWidth(LevelUpPromptKind.AbilityChoice), lines,
                FramedWindow.LevelUpChoice);
            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.UpArrow:
                case ConsoleKey.LeftArrow:
                    selectedIndex = (selectedIndex - 1 + choices.Count) % choices.Count;
                    break;
                case ConsoleKey.DownArrow:
                case ConsoleKey.RightArrow:
                    selectedIndex = (selectedIndex + 1) % choices.Count;
                    break;
                case ConsoleKey.Enter:
                    return choices[selectedIndex].Id;
            }
        }
    }

    public string DrawWeaponProficiencyChoice(LiveCharacter character,
        IReadOnlyList<(string Id, string Name, string Description)> choices, int milestone)
    {
        if (choices.Count == 0) throw new ArgumentException("Nincs választható fegyverjártasság.", nameof(choices));
        var selectedIndex = 0;
        while (true)
        {
            ResetColorCache();
            Console.Clear();
            var lines = LevelUpWindow.BuildChoice(LevelUpPromptKind.WeaponProficiencyChoice,
                [new($"{character.Name} — {(milestone == 1 ? "karakteralkotás" : $"{milestone}. szint")}", ConsoleColor.Cyan),
                 new("Legfeljebb két fegyvercsalád tanulható; egy család Jártas, majd Mester lehet.", ConsoleColor.Green)],
                choices.Select(choice => new LevelUpChoiceSnapshot(choice.Id, choice.Name, choice.Description)).ToArray(),
                selectedIndex);
            DrawCenteredFrame(LevelUpWindow.ChoiceWidth(LevelUpPromptKind.WeaponProficiencyChoice), lines,
                FramedWindow.LevelUpChoice);
            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.UpArrow:
                case ConsoleKey.LeftArrow:
                    selectedIndex = (selectedIndex - 1 + choices.Count) % choices.Count;
                    break;
                case ConsoleKey.DownArrow:
                case ConsoleKey.RightArrow:
                    selectedIndex = (selectedIndex + 1) % choices.Count;
                    break;
                case ConsoleKey.Enter:
                    return choices[selectedIndex].Id;
            }
        }
    }

    public SpellDefinition DrawSpellLearningScreen(LiveCharacter character,
        IReadOnlyList<SpellDefinition> choices, int learnedNumber, int learnedTotal)
    {
        var selectedIndex = 0;
        while (true)
        {
            ResetColorCache();
            Console.Clear();
            var projected = choices.Select(spell => new LevelUpChoiceSnapshot(spell.Id,
                $"{spell.Level}. szint — {spell.Name}", spell.Description)).ToArray();
            var lines = MagicProgressionWindow.BuildLearning(character.Name,
                $"{learnedNumber}/{learnedTotal}. új varázslat", projected, selectedIndex);
            DrawCenteredFrame(MagicProgressionWindow.LearningWidth, lines, FramedWindow.SpellLearning);
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
            var projected = SpellInfoSnapshotProjector.Create(character).KnownSpells;
            var lines = MagicProgressionWindow.BuildPreparation(character.Name, selected.Count,
                character.MemorizationCapacity, projected, selected, cursor);
            DrawCenteredFrame(MagicProgressionWindow.PreparationWidth, lines, FramedWindow.SpellPreparation);
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

    public SpellCastSelection? DrawSpellCastingScreen(IReadOnlyList<LiveCharacter> casters, int casterIndex, bool inCombat,
        Maze maze, FogOfWar fogOfWar, Func<LiveCharacter, Position> casterPosition, Action? showHelp = null)
    {
        _spellCastingOverlaySnapshot = null;
        while (true)
        {
            var character = casters[casterIndex];
            var playerPosition = casterPosition(character);
            var spells = character.MemorizedSpells
                .Where(spell => inCombat ? spell.CanUseInCombat : spell.CanUseDuringExploration)
                .Select(spell => (Spell: spell, CastingItem: (MagicItemDefinition?)null, SlotIndex: (int?)null))
                .Concat(character.MagicItems.Select((item, index) => (Item: item, Index: index))
                    .Where(entry => entry.Item?.Kind is MagicItemKind.Scroll or MagicItemKind.Wand &&
                        entry.Item.SpellId is not null && character.MagicItemCharges[entry.Index] > 0)
                    .Select(entry => (Spell: _gameData.GetSpell(entry.Item!.SpellId!), CastingItem: (MagicItemDefinition?)entry.Item, SlotIndex: (int?)entry.Index))
                    .Where(entry => SpellcastingRules.CanUseCastingItem(character, entry.CastingItem!, entry.Spell))
                    .Where(entry => inCombat ? entry.Spell.CanUseInCombat : entry.Spell.CanUseDuringExploration))
                .OrderBy(entry => entry.Spell.Level).ThenBy(entry => entry.Spell.Name).ThenBy(entry => entry.CastingItem is not null)
                .ToList();
            var selectedIndex = 0;
            var switchDirection = 0;
            while (true)
            {
                var firstVisibleIndex = spells.Count == 0 ? 0 : Math.Clamp(
                    selectedIndex - SpellSelectorWindow.PageSize / FrameBorderWidth, 0,
                    Math.Max(0, spells.Count - SpellSelectorWindow.PageSize));
                var projected = spells.Select(spell =>
                {
                    var quickIndex = character.QuickSpells.ToList().FindIndex(candidate =>
                        string.Equals(candidate?.Id, spell.Spell.Id, StringComparison.OrdinalIgnoreCase));
                    var quick = spell.CastingItem?.Kind switch
                    {
                        MagicItemKind.Scroll => "📜",
                        MagicItemKind.Wand => $"{WandIcon}{character.MagicItemCharges[spell.SlotIndex!.Value]}",
                        _ => quickIndex >= 0 ? $"F{quickIndex + 1}" : "--"
                    };
                    var manaCost = spell.CastingItem is null
                        ? SpellcastingRules.EffectiveManaCost(character, spell.Spell)
                        : 0;
                    return new SpellSelectorOption(spell.Spell.Name, spell.Spell.Level, manaCost,
                        spell.Spell.TargetType, quick, character.CurrentMana >= manaCost);
                }).ToArray();
                var lines = SpellSelectorWindow.Build(character.Name, character.CurrentMana,
                    character.MaximumMana, inCombat, projected, selectedIndex, firstVisibleIndex,
                    casterIndex, casters.Count);
                DrawSpellCastingOverlay(SpellSelectorWindow.Width, lines, maze, fogOfWar, playerPosition,
                    FramedWindow.SpellSelector);
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.F1 && (key.Modifiers & ConsoleModifiers.Shift) != 0)
                {
                    showHelp?.Invoke();
                    _spellCastingOverlaySnapshot = null;
                    continue;
                }
                switch (key.Key)
                {
                    case ConsoleKey.UpArrow when spells.Count > 0: selectedIndex = (selectedIndex - 1 + spells.Count) % spells.Count; break;
                    case ConsoleKey.DownArrow when spells.Count > 0: selectedIndex = (selectedIndex + 1) % spells.Count; break;
                    case ConsoleKey.Enter when spells.Count > 0:
                        return new SpellCastSelection(spells[selectedIndex].Spell,
                        character, spells[selectedIndex].CastingItem, spells[selectedIndex].SlotIndex);
                    case ConsoleKey.Escape: return null;
                    case ConsoleKey.LeftArrow when casters.Count > 1:
                        casterIndex = (casterIndex - 1 + casters.Count) % casters.Count;
                        switchDirection = -1;
                        break;
                    case ConsoleKey.RightArrow when casters.Count > 1:
                        casterIndex = (casterIndex + 1) % casters.Count;
                        switchDirection = 1;
                        break;
                }
                if (switchDirection != 0)
                {
                    RestoreSpellCastingOverlay();
                    break;
                }
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
        Maze maze, FogOfWar fogOfWar, Position playerPosition, FramedWindow? framedWindow = null)
    {
        var frameHeight = lines.Count + FrameBorderWidth;
        var left = Math.Max(0, (PlayfieldWidth - frameWidth) / FrameBorderWidth);
        var top = Math.Max(MinimumCenteredFrameTop, (PlayfieldHeight - frameHeight) / FrameBorderWidth);
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

        var style = framedWindow is { } window
            ? WindowFrameConfiguration.For(window)
            : WindowFrameStyle.Double;
        SetColors(ConsoleColor.Magenta, ConsoleColor.Black);
        WriteAt(left, top, WindowFrameCatalog.Horizontal(style, frameWidth));
        var contentWidth = frameWidth - CenteredFrameHorizontalPadding * FrameBorderWidth;
        for (var index = 0; index < lines.Count; index++)
        {
            var sides = WindowFrameCatalog.Sides(style, index, lines.Count);
            SetColors(ConsoleColor.Magenta, ConsoleColor.Black);
            WriteAt(left, top + index + 1, sides.Left);
            SetColors(ConsoleColor.Gray, ConsoleColor.Black);
            WriteAt(left + sides.Left.Length, top + index + 1,
                new string(' ', frameWidth - sides.Left.Length - sides.Right.Length));
            SetColors(lines[index].Color, ConsoleColor.Black);
            var text = lines[index].Text.Length <= contentWidth
                ? lines[index].Text
                : lines[index].Text[..contentWidth];
            WriteAt(left + CenteredFrameHorizontalPadding, top + index + 1, text.PadRight(contentWidth));
            SetColors(ConsoleColor.Magenta, ConsoleColor.Black);
            WriteAt(left + frameWidth - sides.Right.Length, top + index + 1, sides.Right);
        }
        SetColors(ConsoleColor.Magenta, ConsoleColor.Black);
        WriteAt(left, top + lines.Count + 1, WindowFrameCatalog.Horizontal(style, frameWidth, bottom: true));
    }

    /// <summary>
    /// Kirajzol egy középre igazított összegző keretet a szintlépéshez.
    /// </summary>
    private void DrawLevelUpSummary(LiveCharacter character, LevelUpResult result, bool hasPerkOffer)
    {
        ResetColorCache();
        Console.Clear();
        var lines = LevelUpWindow.BuildSummary(character.Name, result.PreviousLevel, result.CurrentLevel,
            result.Bonuses.Select(bonus => new LevelUpBonusSnapshot(bonus.Level, bonus.Vitality, bonus.Mana)).ToArray(),
            result.VitalityGained, result.ManaGained, character.UsesMana, character.CurrentVitality,
            character.MaximumVitality, character.CurrentMana, character.MaximumMana,
            hasPerkOffer
                ? "🌠 Új TEHETSÉG ébred benned! Nyomj meg egy billentyűt... 🌠"
                : "🌟 Nyomj meg egy billentyűt a kaland folytatásához! 🌟");

        DrawCenteredFrame(LevelUpWindow.Width, lines, FramedWindow.LevelUp);
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
            var lines = LevelUpWindow.BuildChoice(LevelUpPromptKind.PerkChoice,
                [new($"{character.Name} — {character.CharacterClass.Name} — {offer.Tier}. fokozat", ConsoleColor.Cyan),
                 new($"A tehetség a {offer.TriggerLevel}. szint elérésekor vált elérhetővé.", ConsoleColor.DarkCyan),
                 new("A nem választott tehetség végleg elveszik ennél a karakternél.", ConsoleColor.Red)],
                offer.Choices.Select(choice => new LevelUpChoiceSnapshot(choice.Id, choice.Name, choice.Description)).ToArray(),
                selectedIndex);
            DrawCenteredFrame(LevelUpWindow.ChoiceWidth(LevelUpPromptKind.PerkChoice), lines,
                FramedWindow.LevelUpChoice);

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
    private void DrawCenteredFrame(int frameWidth, IReadOnlyList<(string Text, ConsoleColor Color)> lines,
        FramedWindow? framedWindow = null)
    {
        var left = Math.Max(0, (Console.WindowWidth - frameWidth) / FrameBorderWidth);
        var style = framedWindow is { } window
            ? WindowFrameConfiguration.For(window)
            : WindowFrameStyle.Double;
        var topAdornment = WindowFrameCatalog.Adornment(style, frameWidth);
        var bottomAdornment = WindowFrameCatalog.Adornment(style, frameWidth, bottom: true);
        var adornmentRows = topAdornment is null ? 0 : 2;
        var top = Math.Max(MinimumCenteredFrameTop,
            (Console.WindowHeight - lines.Count - FrameBorderWidth - adornmentRows) / FrameBorderWidth);
        var contentPadding = WindowFrameCatalog.ContentPadding(style);
        var contentWidth = frameWidth - contentPadding * FrameBorderWidth;
        var frameTop = topAdornment is null ? top : top + 1;
        var borderColor = style == WindowFrameStyle.Sword ? ConsoleColor.Yellow : ConsoleColor.Magenta;
        SetColors(borderColor, ConsoleColor.Black);
        if (topAdornment is not null) WriteAt(left, top, topAdornment);
        WriteAt(left, frameTop, WindowFrameCatalog.Horizontal(style, frameWidth));
        for (var index = 0; index < lines.Count; index++)
        {
            var sides = WindowFrameCatalog.Sides(style, index, lines.Count);
            SetColors(borderColor, ConsoleColor.Black);
            WriteAt(left, frameTop + index + 1, sides.Left);
            SetColors(ConsoleColor.Gray, ConsoleColor.Black);
            WriteAt(left + sides.Left.Length, frameTop + index + 1,
                new string(' ', frameWidth - sides.Left.Length - sides.Right.Length));
            SetColors(lines[index].Color, ConsoleColor.Black);
            var text = lines[index].Text.Length <= contentWidth
                ? lines[index].Text
                : lines[index].Text[..contentWidth];
            WriteAt(left + contentPadding, frameTop + index + 1, text.PadRight(contentWidth));
            SetColors(borderColor, ConsoleColor.Black);
            WriteAt(left + frameWidth - sides.Right.Length, frameTop + index + 1, sides.Right);
        }
        SetColors(borderColor, ConsoleColor.Black);
        var frameBottom = frameTop + lines.Count + 1;
        WriteAt(left, frameBottom, WindowFrameCatalog.Horizontal(style, frameWidth, bottom: true));
        if (bottomAdornment is not null) WriteAt(left, frameBottom + 1, bottomAdornment);
    }

    public WorldNpcInteractionResult DrawWorldNpcRecruitment(WorldNpc npc,
        IReadOnlyList<NpcQuestDefinition> questDefinitions)
    {
        ResetColorCache();
        Console.Clear();
        var mana = npc.Character.UsesMana ? $"   🔷 {npc.Character.CurrentMana}/{npc.Character.MaximumMana}" : string.Empty;
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            (npc.Disposition == NpcDisposition.Friendly ? "🤝 BARÁTSÁGOS TALÁLKOZÁS" : "👁 SEMLEGES TALÁLKOZÁS", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray),
            ($"{npc.Character.Name} — {npc.Character.Race.Name} {npc.Character.CharacterClass.Name}", npc.Character.Color),
            ($"Szint {npc.Character.Level}   ❤️ {npc.Character.CurrentVitality}/{npc.Character.MaximumVitality}{mana}", ConsoleColor.Gray),
            ($"Viszony: {npc.Friendliness}/10   Viselkedés: {NpcBehaviorName(npc.Behavior)}", ConsoleColor.Cyan),
            (npc.QuestIds.Count > 0 ? $"📜 Küldetések: {npc.QuestIds.Count}" : string.Empty, ConsoleColor.DarkYellow),
            (string.Empty, ConsoleColor.Gray),
        };
        lines.AddRange(MessageTextLayout.Wrap($"„{npc.Dialogue}”", WorldNpcRecruitmentTextWidth)
            .Select(line => (line, ConsoleColor.White)));
        foreach (var quest in questDefinitions)
        {
            var progress = npc.Quests.FirstOrDefault(value =>
                string.Equals(value.QuestId, quest.Id, StringComparison.OrdinalIgnoreCase));
            var status = progress?.State switch
            {
                NpcQuestState.Active => $"{progress.Progress}/{quest.RequiredCount}",
                NpcQuestState.Completed => "teljesítve",
                _ => "új"
            };
            lines.Add(($"  📜 {quest.Title} [{status}]", progress?.State == NpcQuestState.Completed
                ? ConsoleColor.Green : ConsoleColor.DarkYellow));
        }
        lines.Add((string.Empty, ConsoleColor.Gray));
        var actions = npc.CanJoin ? "Enter: csatlakozzon ingyen   " : string.Empty;
        if (npc.Disposition == NpcDisposition.Neutral) actions += "T: továbbhaladás   ";
        actions += npc.CanJoin ? "Esc: most nem" : "Esc: távozás";
        lines.Add((actions, ConsoleColor.Yellow));
        DrawCenteredFrame(WorldNpcRecruitmentFrameWidth, lines, FramedWindow.Inn);
        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.Enter && npc.CanJoin) return WorldNpcInteractionResult.Join;
            if (key == ConsoleKey.T && npc.Disposition == NpcDisposition.Neutral)
                return WorldNpcInteractionResult.Continue;
            if (key == ConsoleKey.Escape) return WorldNpcInteractionResult.Leave;
        }
    }

    public UniqueNpcConversationResult DrawUniqueNpcConversation(WorldNpc npc)
    {
        if (npc.ConversationStage >= 3)
        {
            DrawCenteredFrame(78,
            [
                ("🌿 ELIRA EZÜSTÁG", ConsoleColor.Yellow),
                ($"Elf Tolvaj   Viszony: {npc.Friendliness}/10", ConsoleColor.Cyan),
                (string.Empty, ConsoleColor.Gray),
                ("„Most már a kijáratra figyeljünk. A többit odakint eldöntjük.”", ConsoleColor.White),
                (string.Empty, ConsoleColor.Gray),
                ("Enter: tovább", ConsoleColor.Yellow)
            ], FramedWindow.Inn);
            while (Console.ReadKey(intercept: true).Key != ConsoleKey.Enter) { }
            return new UniqueNpcConversationResult(0, false, -1);
        }
        var (prompt, answers) = npc.ConversationStage switch
        {
            0 => ("Elira a falnak támaszkodik. A bokáján friss kötés sötétlik.", new[]
            {
                ("1) Segítünk. Senkit nem hagyunk itt.", 2, true),
                ("2) Velünk jöhetsz ha hasznossá teszed magad.", 1, true),
                ("3) A saját bajodból egyedül is kimászhatsz.", -2, false)
            }),
            1 => ("Miért kockáztatnátok értem bármit is?", new[]
            {
                ("1) Mert senki sem érdemli meg hogy itt pusztuljon.", 2, true),
                ("2) Egy jó felderítő mindannyiunk túlélési esélyét növeli.", 1, true),
                ("3) Ne értsd félre. Csak szemmel akarunk tartani.", -1, true)
            }),
            _ => ("Ha kijutunk innen mi lesz velünk odakint?", new[]
            {
                ("1) Nem a származás hanem a tettek tesznek társsá.", 2, false),
                ("2) Az elfeknek össze kell tartaniuk.", 2, false),
                ("3) Ne várj többet egy ideiglenes szövetségnél.", -2, false)
            })
        };
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("🌿 ELIRA EZÜSTÁG", ConsoleColor.Yellow),
            ($"Elf Tolvaj   Viszony: {npc.Friendliness}/10", ConsoleColor.Cyan),
            (string.Empty, ConsoleColor.Gray),
            ($"„{prompt}”", ConsoleColor.White),
            (string.Empty, ConsoleColor.Gray)
        };
        lines.AddRange(answers.Select(answer => (answer.Item1, ConsoleColor.Yellow)));
        DrawCenteredFrame(78, lines, FramedWindow.Inn);
        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            var index = key switch
            {
                ConsoleKey.D1 or ConsoleKey.NumPad1 => 0,
                ConsoleKey.D2 or ConsoleKey.NumPad2 => 1,
                ConsoleKey.D3 or ConsoleKey.NumPad3 => 2,
                _ => -1
            };
            if (index >= 0) return new UniqueNpcConversationResult(answers[index].Item2, answers[index].Item3, index);
        }
    }

    public void DrawUniqueNpcIntroduction(WorldNpc npc)
    {
        var title = $"⚜ {npc.Character.Name.ToUpperInvariant()}";
        DrawCenteredFrame(78,
        [
            (title, ConsoleColor.Yellow),
            ($"{npc.Character.Race.Name} {npc.Character.CharacterClass.Name}   Viszony: {npc.Friendliness}/10",
                ConsoleColor.Cyan),
            (string.Empty, ConsoleColor.Gray),
            ($"„{npc.Dialogue}”", ConsoleColor.White),
            (string.Empty, ConsoleColor.Gray),
            ("Enter: tovább", ConsoleColor.Yellow)
        ], FramedWindow.Inn);
        while (Console.ReadKey(intercept: true).Key != ConsoleKey.Enter) { }
    }

    public int DrawUniqueNpcStoryChoice(WorldNpc npc, string prompt, IReadOnlyList<string> choices)
    {
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ($"⚜ {npc.Character.Name.ToUpperInvariant()}", ConsoleColor.Yellow),
            ($"{npc.Character.Race.Name} {npc.Character.CharacterClass.Name}   Viszony: {npc.Friendliness}/10",
                ConsoleColor.Cyan),
            (string.Empty, ConsoleColor.Gray),
            ($"„{prompt}”", ConsoleColor.White),
            (string.Empty, ConsoleColor.Gray)
        };
        lines.AddRange(choices.Select((choice, index) => ($"{index + 1}) {choice}", ConsoleColor.Yellow)));
        DrawCenteredFrame(88, lines, FramedWindow.Inn);
        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            var index = key >= ConsoleKey.D1 && key <= ConsoleKey.D9 ? key - ConsoleKey.D1 :
                key >= ConsoleKey.NumPad1 && key <= ConsoleKey.NumPad9 ? key - ConsoleKey.NumPad1 : -1;
            if (index >= 0 && index < choices.Count) return index;
        }
    }

    public void DrawGenericUniqueNpcQuestOffer(WorldNpc npc, IReadOnlyList<NpcQuestDefinition> quests)
    {
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ($"📜 {npc.Character.Name.ToUpperInvariant()} KÉRÉSE", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray)
        };
        foreach (var quest in quests)
        {
            lines.Add(($"📜 {quest.Title} ({quest.ExperienceReward} XP)", ConsoleColor.Cyan));
            lines.Add(($"   {quest.Description}", ConsoleColor.Gray));
        }
        lines.Add((string.Empty, ConsoleColor.Gray));
        lines.Add(("Enter: tovább", ConsoleColor.Yellow));
        DrawCenteredFrame(88, lines, FramedWindow.QuestOffer);
        while (Console.ReadKey(intercept: true).Key != ConsoleKey.Enter) { }
    }

    public void DrawUniqueNpcQuestOffer(WorldNpc npc, IReadOnlyList<NpcQuestDefinition> quests)
    {
        ResetColorCache();
        Console.Clear();
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("🌿 ELIRA KÉRÉSE", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray),
            ("„Köszönöm. Már azt hittem, a labirintusban maradok örökre.”", ConsoleColor.White),
            ("„Ha valóban kijuttattok, három dologban kérem a segítségeteket:”", ConsoleColor.White),
            (string.Empty, ConsoleColor.Gray)
        };
        foreach (var quest in quests)
        {
            lines.Add(($"📜 {quest.Title} ({quest.ExperienceReward} XP)", ConsoleColor.Cyan));
            lines.Add(($"   {quest.Description}", ConsoleColor.Gray));
        }
        lines.Add((string.Empty, ConsoleColor.Gray));
        lines.Add(("„Nem felejtem el, amit értem tesztek.”", ConsoleColor.White));
        lines.Add((string.Empty, ConsoleColor.Gray));
        lines.Add(("Enter: tovább", ConsoleColor.Yellow));
        DrawCenteredFrame(88, lines, FramedWindow.QuestOffer);
        while (Console.ReadKey(intercept: true).Key != ConsoleKey.Enter) { }
    }

    public bool ConfirmUniqueNpcPermanentJoin(WorldNpc npc, bool partyHasRoom)
    {
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("🤝 EGY ÚJ ÚT KEZDETE", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray),
            ($"{npc.Character.Name} viszonya: {npc.Friendliness}/10", ConsoleColor.Cyan),
            (partyHasRoom
                ? "„Nem csak a kijáratig akarok veletek tartani. Befogadtok?”"
                : $"{npc.Character.Name} csatlakozna de a négyfős parti megtelt.", ConsoleColor.White),
            (string.Empty, ConsoleColor.Gray),
            (partyHasRoom ? "Enter: végleges csatlakozás   Esc: búcsú" : "Enter: búcsú", ConsoleColor.Yellow)
        };
        DrawCenteredFrame(78, lines, FramedWindow.Inn);
        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.Enter) return partyHasRoom;
            if (key == ConsoleKey.Escape && partyHasRoom) return false;
        }
    }

    private static string NpcBehaviorName(NpcWorldBehavior behavior) => behavior switch
    {
        NpcWorldBehavior.Friendly => "barátságos",
        NpcWorldBehavior.Guarded => "óvatos",
        NpcWorldBehavior.Opportunistic => "érdekvezérelt",
        NpcWorldBehavior.Aggressive => "agresszív",
        _ => behavior.ToString()
    };

    public void DrawRestSummaryScreen(PartyRestSnapshot rest, string footer, ConsoleColor footerColor)
    {
        ResetColorCache();
        Console.Clear();
        DrawCenteredFrame(RestSummaryWindow.Width, RestSummaryWindow.Build(rest, footer, footerColor),
            FramedWindow.Inn);
    }

    private static int InnMarketPageStart(int entryCount, int selectedIndex)
    {
        return entryCount == 0 ? 0 : Math.Clamp(selectedIndex - InnMarketPageSize / FrameBorderWidth, 0, Math.Max(0, entryCount - InnMarketPageSize));
    }

    private string InnStockLine(InnStockOffer offer, bool selected) =>
        $"{(selected ? "▶" : " ")} {ItemCategoryIcon(offer.Item)} {offer.Item.Name,-24} alapár {offer.Item.BasePrice,5}   fogadói ár {offer.Price,5} {MoneyIcon}";

    private string InnSellLine(InnSellOffer offer, bool selected)
    {
        var quantity = offer.Owner.GetInventoryItemQuantity(InventorySlotKind.Backpack, offer.BackpackIndex);
        var itemName = offer.Item.Name + (quantity > 1 ? $" ×{quantity}" : string.Empty);
        return $"{(selected ? "▶" : " ")} {ItemCategoryIcon(offer.Item)} {itemName,-22} {offer.Owner.Name,-13} ajánlat {offer.Price,5} {MoneyIcon}";
    }

    private static string InnRecruitLine(LiveCharacter candidate, int price, bool selected)
    {
        var mana = candidate.UsesMana ? $"  MP {candidate.MaximumMana}" : string.Empty;
        var priceText = price == 0 ? "INGYEN" : $"{price} {MoneyIcon}";
        return $"{(selected ? "▶" : " ")} {candidate.Name,-13}  {candidate.Race.Name,-10} {candidate.CharacterClass.Name,-10} L{candidate.Level,2}  HP {candidate.MaximumVitality}{mana}  {priceText}";
    }

    private static string InnReplacementLine(LiveCharacter member, bool selected) =>
        $"{(selected ? "▶" : " ")} {member.Name,-13} {member.CharacterClass.Name,-10} L{member.Level,2}  HP {member.CurrentVitality}/{member.MaximumVitality}";

    private void UpdateCenteredFrameLines(int frameWidth, int lineCount,
        IEnumerable<(int Index, string Text, ConsoleColor Color)> updates, FramedWindow? framedWindow = null)
    {
        var style = framedWindow is { } window
            ? WindowFrameConfiguration.For(window)
            : WindowFrameStyle.Double;
        var contentPadding = WindowFrameCatalog.ContentPadding(style);
        var contentWidth = frameWidth - contentPadding * FrameBorderWidth;
        var left = Math.Max(0, (Console.WindowWidth - frameWidth) / FrameBorderWidth);
        var top = Math.Max(MinimumCenteredFrameTop, (Console.WindowHeight - lineCount - FrameBorderWidth) / FrameBorderWidth);
        foreach (var (index, text, color) in updates)
        {
            SetColors(color, ConsoleColor.Black);
            WriteAt(left + contentPadding, top + index + 1, new string(' ', contentWidth));
            WriteAt(left + contentPadding, top + index + 1, text);
        }
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
        var characterToDraw = _displayedCharacter is not null && SheetCharacters().Contains(_displayedCharacter)
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
        var characters = SheetCharacters();
        if (_displayedCharacter is null || direction == 0 || characters.Count == 0) return;
        if (_activeSheetSelection is { } active) _lastSheetSelections[_displayedCharacter] = active;
        var currentIndex = characters.IndexOf(_displayedCharacter);
        if (currentIndex < 0) currentIndex = 0;
        var nextIndex = (currentIndex + direction + characters.Count) % characters.Count;
        _displayedCharacter = characters[nextIndex];
        _activeSheetSelection = _lastSheetSelections.GetValueOrDefault(_displayedCharacter);
        DrawCharacterSheet(_displayedCharacter);
    }

    public void DrawSpellInfoPage(LiveCharacter character, int selectedIndex)
    {
        var spells = character.KnownSpells.OrderBy(spell => spell.Level).ThenBy(spell => spell.Name).ToList();
        selectedIndex = spells.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, spells.Count - 1);
        _spellInfoCharacter = character;
        _selectedSpellInfoIndex = selectedIndex;
        for (var line = CharacterSheetHeaderLine; line <= PicturePanelBottom; line++) WriteSheetLine(line, string.Empty, ConsoleColor.Gray);
        var info = SpellInfoSnapshotProjector.Create(character);
        foreach (var line in SpellInfoPanel.Build(character.Name, character.CharacterClass.Id, character.Level,
                     info, selectedIndex, _characterSheetFocused))
            WriteSheetLine(line.Row, line.Text, line.Color, line.Background);
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
        return _party.Members.ElementAtOrDefault(selection.Index);
    }

    public void RefreshAfterPartyMemberRemoved(LiveCharacter removedCharacter, LiveCharacter leader)
    {
        _lastSheetSelections.Remove(removedCharacter);
        if (_displayedCharacter == removedCharacter) _displayedCharacter = leader;
        if (_displayedCharacter is null || !_party.Members.Contains(_displayedCharacter)) _displayedCharacter = leader;
        _activeSheetSelection = _lastSheetSelections.GetValueOrDefault(_displayedCharacter);
        DrawCharacterSheet(_displayedCharacter);
    }

    public void RefreshInventoryRows()
    {
        if (_displayedCharacter is not null) DrawSelectableCharacterSheetRows(_displayedCharacter);
    }

    /// <summary>Csata közben csak az állapot-, HP- és mannasorokat frissíti.</summary>
    public void RefreshBattleStatusRows()
    {
        if (_displayedCharacter is null) return;
        DrawBattleStatusRows(_displayedCharacter);
        DrawPartyStatusRows(_displayedCharacter);
    }

    public void DrawInventoryMessage(string message, ConsoleColor color = ConsoleColor.Cyan) => DrawBattleMessage(message, color);
    public void DrawNpcBattleSummary(string message, ConsoleColor color) => DrawBattleMessage(message, color);

    public void ScrollMessageLog(bool towardOlderMessages)
    {
        var maximumOffset = Math.Max(0, _messageLog.Count - MessageLineCount);
        _messageLogScrollOffset = towardOlderMessages
            ? Math.Min(maximumOffset, _messageLogScrollOffset + MessageLineCount)
            : Math.Max(0, _messageLogScrollOffset - MessageLineCount);
        RenderMessageLog();
    }

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
        var panelLines = CharacterSheetPanel.Build(character, _gameData.ExperienceByLevel, _mazeLevel,
            _goldenKeyCount, MonsterIds.Bosses.Count, character == _party.Leader,
            IsTemporaryFollower(character));
        DrawCharacterSheetHeader(character);
        foreach (var line in panelLines.Where(line => line.Row != CharacterSheetHeaderLine && line.InventorySlot is null))
            if (line.Row == CharacterSheetVitalityLine)
                DrawCharacterResourceLine(line.Row, CharacterSheetPanel.BuildResourceLine(character));
            else
                WriteCharacterSheetPanelLine(line);
        DrawSelectableCharacterSheetRows(character);
        WriteSheetLine(CharacterSheetReservedMessageLine, string.Empty, ConsoleColor.DarkGray);
        WriteSheetLine(CharacterSheetControlsLine, string.Empty, ConsoleColor.DarkGray);
        DrawPicturePanel();
    }

    private void WriteCharacterSheetPanelLine(CharacterSheetPanelLine line)
    {
        if (string.IsNullOrEmpty(line.ColoredSuffix))
            WriteSheetLine(line.Row, line.Text, line.Color, line.Background);
        else
            WriteSheetLine(line.Row, line.Text, line.Color, line.Background,
                line.ColoredSuffix, line.ColoredSuffixColor);
    }

    private void DrawBattleStatusRows(LiveCharacter character)
    {
        var statusIcons = character.Statuses.Select(status => status.Icon)
            .Concat(character.ActiveSpellEffects.Select(effect => effect.Type switch
            {
                ActiveSpellEffectType.Invisibility => "👻",
                ActiveSpellEffectType.DefenseBonus => "🛡️",
                ActiveSpellEffectType.PhysicalReduction => DamageReductionIcon,
                ActiveSpellEffectType.BleedingImmunity => "🩸🚫",
                ActiveSpellEffectType.HitBonus => "🎯",
                ActiveSpellEffectType.DamageBonus => "⚔️✨",
                ActiveSpellEffectType.InitiativeBonus => "⚡",
                ActiveSpellEffectType.ProtectionFromEvil => "✝️🛡️",
                ActiveSpellEffectType.GuardianAngel => "👼",
                ActiveSpellEffectType.Sanctuary => "⛪",
                ActiveSpellEffectType.VisionBonus when effect.Value < 0 => "🌑",
                ActiveSpellEffectType.VisionBonus => "🔆",
                _ => "✨"
            })).ToList();
        WriteSheetLine(CharacterSheetStatusLine, statusIcons.Count == 0
                ? "Áll: nincs"
                : $"Áll: {string.Join(' ', statusIcons)}",
            statusIcons.Count > 0 ? ConsoleColor.Magenta : ConsoleColor.DarkGray);
        DrawCharacterResourceLine(CharacterSheetVitalityLine, CharacterSheetPanel.BuildResourceLine(character));
    }

    private void DrawCharacterSheetHeader(LiveCharacter character) => WriteSheetLine(
        CharacterSheetHeaderLine, "KARAKTERLAP", ConsoleColor.Yellow,
        _characterSheetFocused ? ConsoleColor.Green : ConsoleColor.Black,
        " - " + character.Name, character.Color);

    private void DrawSelectableCharacterSheetRows(LiveCharacter character)
    {
        var entries = BuildSheetSelections(character);
        if (_activeSheetSelection is null || entries.All(entry => entry.Key != _activeSheetSelection))
            _activeSheetSelection = entries.FirstOrDefault()?.Key;
        foreach (var line in CharacterSheetPanel.Build(character, _gameData.ExperienceByLevel, _mazeLevel,
                     _goldenKeyCount, MonsterIds.Bosses.Count, character == _party.Leader,
                     IsTemporaryFollower(character))
                 .Where(line => line.InventorySlot is not null))
        {
            var slot = line.InventorySlot!.Value;
            var kind = slot.Kind switch
            {
                InventorySlotKind.Weapon => SheetSelectionKind.Weapon,
                InventorySlotKind.Armor => SheetSelectionKind.Armor,
                InventorySlotKind.MagicItem => SheetSelectionKind.MagicItem,
                InventorySlotKind.Backpack => SheetSelectionKind.Backpack,
                _ => throw new ArgumentOutOfRangeException()
            };
            WriteSheetLine(line.Row, line.Text, line.Color,
                SelectionBackground(new SheetSelectionKey(kind, slot.Index)));
        }
        DrawPartyStatusRows(character);
    }

    private void DrawPartyStatusRows(LiveCharacter displayedCharacter)
    {
        var partyMembers = _party.Members.Take(CharacterSheetPartyMemberRows).ToList();
        for (var index = 0; index < CharacterSheetPartyMemberRows; index++)
        {
            var row = CharacterSheetPartyMembersStartLine + index;
            if (index >= partyMembers.Count)
            {
                WriteSheetLine(row, string.Empty, ConsoleColor.DarkGray);
                continue;
            }
            var member = partyMembers[index];
            DrawPartyStatusLine(row, CharacterSheetPanel.BuildPartyStatus(member,
                member == displayedCharacter, member == _party.Leader),
                SelectionBackground(new(SheetSelectionKind.PartyMember, index)));
        }
    }

    private List<SheetSelectionEntry> BuildSheetSelections(LiveCharacter character)
    {
        var entries = new List<SheetSelectionEntry>();
        if (IsTemporaryFollower(character)) return entries;
        for (var index = 0; index < character.WeaponSlots.Count; index++)
            entries.Add(new(new(SheetSelectionKind.Weapon, index)));
        entries.Add(new(new(SheetSelectionKind.Armor, 0)));
        for (var index = 0; index < character.MagicItems.Count; index++) entries.Add(new(new(SheetSelectionKind.MagicItem, index)));
        for (var index = 0; index < character.Backpack.Count; index++) entries.Add(new(new(SheetSelectionKind.Backpack, index)));
        var partyMemberCount = Math.Min(CharacterSheetPartyMemberRows, _party.Members.Count);
        for (var index = 0; index < partyMemberCount; index++) entries.Add(new(new(SheetSelectionKind.PartyMember, index)));
        return entries;
    }

    private List<LiveCharacter> SheetCharacters() => _party.Members
        .Concat(_temporaryFollowers())
        .Distinct()
        .ToList();

    private bool IsTemporaryFollower(LiveCharacter character) =>
        !_party.Members.Contains(character) && _temporaryFollowers().Contains(character);

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
        while (_messageLog.Count > MessageLogBufferLineCount) _messageLog.Dequeue();
        _messageLogScrollOffset = 0;
        RenderMessageLog();
    }

    private void RenderMessageLog()
    {
        var messages = _messageLog.ToArray();
        var start = Math.Max(0, messages.Length - MessageLineCount - _messageLogScrollOffset);
        var end = messages.Length - _messageLogScrollOffset;
        for (var index = 0; index < MessageLineCount; index++)
        {
            var messageIndex = start + index;
            var messageLine = messageIndex < messages.Length && messageIndex < end
                ? messages[messageIndex]
                : new MessageLogLine(string.Empty, ConsoleColor.Gray);
            SetColors(messageLine.Color, ConsoleColor.Black);
            var text = messageLine.Text;
            WriteAt(MessagePanelLeft, BottomBorderY + FirstMessageLineOffset + index, text.PadRight(MessageWidth));
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
    private static string ResourceIcons(string icon, int level) => string.Concat(Enumerable.Repeat(icon, level / ResourceIconStep));

    private void DrawPartyStatusLine(int y, PartyStatusLine status, ConsoleColor background)
    {
        WriteSheetLine(y, string.Empty, ConsoleColor.Gray, background);
        var x = RightSheetX;
        if (status.InvertedNameStart >= 0)
        {
            var prefix = status.Identity[..status.InvertedNameStart];
            var name = status.Identity[status.InvertedNameStart..];
            SetColors(status.IdentityColor, background);
            WriteAt(x, y, prefix);
            x += prefix.Length;
            SetColors(ConsoleColor.Black, status.IdentityColor);
            WriteAt(x, y, name);
            x += name.Length;
        }
        else
        {
            SetColors(status.IdentityColor, background);
            WriteAt(x, y, status.Identity);
            x += status.Identity.Length;
        }
        foreach (var (text, color) in new[]
                 {
                     (status.Vitality, status.VitalityColor),
                     (status.Mana, status.ManaColor)
                 })
        {
            SetColors(color, background);
            WriteAt(x, y, text);
            x += text.Length;
        }
    }

    private void DrawCharacterResourceLine(int y, CharacterResourceLine resources)
    {
        WriteSheetLine(y, string.Empty, ConsoleColor.Gray, ConsoleColor.Black);
        var x = RightSheetX;
        foreach (var (text, color) in new[]
                 {
                     (resources.Vitality, resources.VitalityColor),
                     (resources.Mana, resources.ManaColor)
                 })
        {
            SetColors(color, ConsoleColor.Black);
            WriteAt(x, y, text);
            x += text.Length;
        }
    }

    private static string FormatCompactList(string label, IEnumerable<string> values)
    {
        var names = values.ToList();
        if (names.Count == 0) return $"{label}: nincs";
        var prefix = $"{label}: ";
        var separatorsWidth = (names.Count - FirstItemNumber) * FrameBorderWidth;
        var availablePerName = Math.Max(FirstItemNumber, (RightSheetWidth - prefix.Length - separatorsWidth) / names.Count);
        var shortenedNames = names.Select(name => name.Length <= availablePerName ? name : name[..availablePerName]);
        return prefix + string.Join(", ", shortenedNames);
    }

    private static IReadOnlyList<string> FormatCompactListRows(string label, IEnumerable<string> values, int rowCount)
    {
        var names = values.ToList();
        if (names.Count == 0) return [$"{label}: nincs", .. Enumerable.Repeat(string.Empty, rowCount - FirstItemNumber)];

        var rows = new List<string>(rowCount);
        var namesPerRow = (int)Math.Ceiling(names.Count / (double)rowCount);
        for (var row = 0; row < rowCount; row++)
        {
            var rowNames = names.Skip(row * namesPerRow).Take(namesPerRow).ToList();
            if (rowNames.Count == 0) { rows.Add(string.Empty); continue; }
            var prefix = row == 0 ? $"{label}: " : new string(' ', label.Length + FrameBorderWidth);
            var separatorsWidth = (rowNames.Count - FirstItemNumber) * FrameBorderWidth;
            var availablePerName = Math.Max(FirstItemNumber, (RightSheetWidth - prefix.Length - separatorsWidth) / rowNames.Count);
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
        var style = WindowFrameConfiguration.For(FramedWindow.CreaturePortrait);
        WriteSheetLine(PicturePanelTop, WindowFrameCatalog.Horizontal(style, RightSheetWidth), ConsoleColor.DarkCyan);
        for (var index = 0; index < PicturePanelHeight; index++)
        {
            var line = index < portrait.Lines.Count ? portrait.Lines[index] : string.Empty;
            var sides = WindowFrameCatalog.Sides(style, index, PicturePanelHeight);
            var interiorWidth = RightSheetWidth - sides.Left.Length - sides.Right.Length;
            WriteSheetLine(PicturePanelTop + index + FirstMessageLineOffset,
                sides.Left + CenterPanelText(line, portrait.CanvasWidth, interiorWidth) + sides.Right, color);
        }
        WriteSheetLine(PicturePanelBottom, WindowFrameCatalog.Horizontal(style, RightSheetWidth, bottom: true),
            ConsoleColor.DarkCyan);
    }

    private static string CenterPanelText(string text, int canvasWidth, int interiorWidth = PortraitInteriorWidth)
    {
        var canvas = text.PadRight(canvasWidth);
        var leftPadding = Math.Max(0, (interiorWidth - canvasWidth) / FrameBorderWidth);
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
        var clippedText = text.Length <= RightSheetWidth ? text : text[..RightSheetWidth];
        SetColors(foregroundColor, backgroundColor);
        // Itt történik a tényleges kiírás: balra igazított, maximum 'maximumWidth' karakter,
        // és X=172 lesz (a jobb oldali karakterlap kezdő X pozíciója).
        WriteAt(RightSheetX, y, clippedText.PadRight(RightSheetWidth));
    }

    /// <summary>
    /// Két szöveget ír ki egymás mellé a jobb oldali karakterlapra, két külön színnel.
    /// A teljes sor hossza nem haladja meg a maximum 27 karaktert — ha szükséges,
    /// levágja a szövegeket úgy, hogy mindkét rész látható maradjon lehetőleg.
    /// </summary>
    private void WriteSheetLine(int y, string leftText, ConsoleColor leftColor, ConsoleColor leftColorBg, string rightText, ConsoleColor rightColor)
    {
        // Alap felosztás: fele-fele, de dinamikusan kiegészítjük ha az egyik rövidebb
        var leftMax = RightSheetWidth / FrameBorderWidth;
        var rightMax = RightSheetWidth - leftMax;

        string leftClipped;
        string rightClipped;

        if (leftText.Length <= leftMax)
        {
            leftClipped = leftText;
            var remaining = RightSheetWidth - leftClipped.Length;
            rightClipped = rightText.Length <= remaining ? rightText : rightText[..remaining];
        }
        else if (rightText.Length <= rightMax)
        {
            rightClipped = rightText;
            var remaining = RightSheetWidth - rightClipped.Length;
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
        WriteAt(RightSheetX, y, leftPadded);

        SetColors(rightColor, ConsoleColor.Black);
        var secondX = RightSheetX + leftPadded.Length;
        var remainingWidth = RightSheetWidth - leftPadded.Length;
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
            return fogOfWar.EnemyMemoryAt(position) is { IsSoundCue: true }
                ? new MapCellVisual(new Rune('?'), ConsoleColor.DarkYellow, ConsoleColor.Black)
                : new MapCellVisual(FogSymbol, ConsoleColor.Black, ConsoleColor.Black);
        if (maze.GetTrapAt(position) is { State: not TrapState.Hidden } trap)
            return new MapCellVisual(trap.Symbol, trap.State == TrapState.Detected
                ? ConsoleColor.Yellow : ConsoleColor.DarkGray, ConsoleColor.Black);
        if (maze.GetWorldNpcAt(position) is { } npc)
            return new MapCellVisual(npc.Symbol, ConsoleColor.White, npc.Character.Color);
        var mapObject = maze.GetObjectAt(position);
        if (mapObject is PartyMemberAvatar partyMember)
            return new MapCellVisual(partyMember.Symbol, partyMember.ForegroundColor, partyMember.BackgroundColor);
        if (mapObject is Enemy enemy && !fogOfWar.IsEnemyVisible(enemy.Id, enemy.Position))
            return fogOfWar.EnemyMemoryAt(position) is { } hiddenMemory
                ? new MapCellVisual(new Rune('?'), hiddenMemory.IsSoundCue ? ConsoleColor.DarkYellow : ConsoleColor.DarkGray,
                    ConsoleColor.Black)
                : new MapCellVisual(maze.Tiles[position.X, position.Y], GetTerrainForegroundColor(maze, position),
                    ConsoleColor.Black);
        if (mapObject is null && fogOfWar.EnemyMemoryAt(position) is { } memory)
            return new MapCellVisual(new Rune('?'), memory.IsSoundCue ? ConsoleColor.DarkYellow : ConsoleColor.DarkGray,
                ConsoleColor.Black);
        return new MapCellVisual(mapObject?.Symbol ?? maze.Tiles[position.X, position.Y],
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
        int bufferWidth;
        int bufferHeight;
        try
        {
            bufferWidth = Console.BufferWidth;
            bufferHeight = Console.BufferHeight;
        }
        catch (IOException)
        {
            return;
        }
        if (x < 0 || y < 0 || x >= bufferWidth || y >= bufferHeight) return;
        if (text.Length > bufferWidth - x) text = text[..(bufferWidth - x)];
        Console.SetCursorPosition(x, y);
        Console.Write(text);
    }

    /// <summary>
    /// Visszaadja az adott mező előtérszínét az ott lévő objektum alapján
    /// (kincsesládához sárga, ellenséghez piros, stb.), vagy a tile alapértelmezettét.
    /// </summary>
    private static ConsoleColor GetForegroundColor(Maze maze, Position position)
    {
        if (maze.GetTrapAt(position) is { State: not TrapState.Hidden } trap)
            return trap.State == TrapState.Detected ? ConsoleColor.Yellow : ConsoleColor.DarkGray;
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
        if (mapObject is WorldNpc) return ConsoleColor.White;
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

    private static ConsoleColor GetTerrainForegroundColor(Maze maze, Position position) =>
        maze.Tiles[position.X, position.Y] switch
        {
            var tile when tile == maze.WallRune => maze.WallColor,
            var tile when tile == Maze.ExitMarker => ConsoleColor.Green,
            _ => ConsoleColor.Black
        };

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
