using KaoszRubin.Application;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.UI;

/// <summary>Egy inventory-hely típusa és a típuson belüli, nullától induló indexe.</summary>
public readonly record struct InventorySlotAddress(InventorySlotKind Kind, int Index);

/// <summary>Egy karakterlap-sor szövege, pozíciója és megjelenítési adatai.</summary>
/// <param name="Row">A sor nullától induló konzolpozíciója.</param>
/// <param name="Text">A sor alapértelmezett szövege.</param>
/// <param name="Color">Az alapértelmezett előtérszín.</param>
/// <param name="InventorySlot">A sorhoz tartozó inventory-hely, ha van.</param>
/// <param name="Background">A sor háttérszíne.</param>
/// <param name="ColoredSuffix">Az alapszöveg után külön színnel megjelenített szöveg.</param>
/// <param name="ColoredSuffixColor">A kiegészítő szöveg előtérszíne.</param>
/// <param name="ExtendsToDivider">Jelzi, hogy a sor a kibővített, elválasztóig tartó területet használja.</param>
/// <param name="Segments">A szegmentált megjelenítéshez használható színezett szövegrészek.</param>
/// <param name="ColoredTextStart">A más színű szövegvég kezdőindexe a Text értékében; -1 esetén nincs színváltás.</param>
/// <param name="ColoredTextColor">A Text színezett végének előtérszíne.</param>
public sealed record CharacterSheetPanelLine(int Row, string Text, ConsoleColor Color,
    InventorySlotAddress? InventorySlot = null, ConsoleColor Background = ConsoleColor.Black,
    string ColoredSuffix = "", ConsoleColor ColoredSuffixColor = ConsoleColor.White,
    bool ExtendsToDivider = false, IReadOnlyList<TextSegment>? Segments = null,
    int ColoredTextStart = -1, ConsoleColor ColoredTextColor = ConsoleColor.White);

/// <summary>A parti-státuszsor külön színezhető azonosító-, életerő- és mánarészei.</summary>
/// <param name="Identity">A kijelölés, osztályjel, név és szint szövege.</param>
/// <param name="IdentityColor">Az azonosító rész előtérszíne.</param>
/// <param name="Vitality">Az életerő vagy a halott állapot szövege.</param>
/// <param name="VitalityColor">Az életerő rész előtérszíne.</param>
/// <param name="Mana">A mánaszöveg; mána nélküli karakter esetén üres.</param>
/// <param name="ManaColor">A mána rész előtérszíne.</param>
/// <param name="InvertedNameStart">Az Identity inverz színű részének kezdőindexe; -1 esetén nincs inverz rész.</param>
public sealed record PartyStatusLine(string Identity, ConsoleColor IdentityColor,
    string Vitality, ConsoleColor VitalityColor, string Mana, ConsoleColor ManaColor,
    int InvertedNameStart = -1)
{
    /// <summary>A külön színezhető részek összefűzött, formázatlan szövege.</summary>
    public string Text => Identity + Vitality + Mana;
}

/// <summary>Az aktuális és maximális életerő, valamint az opcionális mána külön színezhető szövege.</summary>
/// <param name="Vitality">Az életerő szövege.</param>
/// <param name="VitalityColor">Az életerő előtérszíne.</param>
/// <param name="Mana">A mána szövege; mána nélküli karakter esetén üres.</param>
/// <param name="ManaColor">A mána előtérszíne.</param>
public sealed record CharacterResourceLine(string Vitality, ConsoleColor VitalityColor,
    string Mana, ConsoleColor ManaColor)
{
    /// <summary>Az életerő és mána összefűzött, formázatlan szövege.</summary>
    public string Text => Vitality + Mana;
}

/// <summary>A host és a vendég azonos karakterlap- és inventory-sorelrendezése.</summary>
public static class CharacterSheetPanel
{
    /// <summary>A karakterlap alapértelmezett és minimális formázási szélessége.</summary>
    public const int Width = 27;
    private const int PartyStatusNameColumnWidth = 13;
    private const int PartyStatusLevelColumnWidth = 2;
    private const int PartyStatusHpColumnWidth = 6;
    private const int ResourceIconStep = 10;

    /// <summary>Legalább Width hosszúságú, szóközökkel kitöltött sort készít.</summary>
    public static string BlankLineForWidth(int width) => new(' ', Math.Max(Width, width));
    /// <summary>Az osztályjelek alapértelmezett avatarkészletét megadó, betöltött játékbeállítások.</summary>
    public static readonly GameSettings? _gameSettings = null;

    static CharacterSheetPanel()
    {
            _gameSettings = new GameSettingsService().Settings;
    }

    /// <summary>
    /// A karakterosztály azonosítóját egy rövid, egybetűs glyph-re alakítja,
    /// amely a parti-státuszsor elején jelenik meg.
    /// Rúnát vagy betűt használ a választott avatarkészlettől függően.
    /// Ismeretlen osztály vagy elérhetetlen alapbeállítás esetén "?" jelet ad vissza.
    /// </summary>
    /// <param name="characterClassId">A karakterosztály azonosítója.</param>
    /// <param name="partyAvatar">A használandó avatarkészlet; null esetén a betöltött beállítás érvényes.</param>
    public static string CharacterClassGlyph(string characterClassId, PartyAvatarSet? partyAvatar = null)
    {
        if (partyAvatar == null)
        {
            if (_gameSettings == null)
            {
                return "?";
            }
            else
            {
                partyAvatar = _gameSettings.PartyAvatars;
            }
        }

        return partyAvatar == PartyAvatarSet.Runes ?
            characterClassId switch
            {
                CharacterClassIds.Harcos => "ᚺ",
                CharacterClassIds.Barbár => "ᛒ",
                CharacterClassIds.Lovag => "ᛚ",
                CharacterClassIds.Tolvaj => "ᛏ",
                CharacterClassIds.Pap => "ᛈ",
                CharacterClassIds.Mágus => "ᛗ",
                _ => "?"
            } :
            characterClassId switch
            {
                CharacterClassIds.Harcos => "H",
                CharacterClassIds.Barbár => "B",
                CharacterClassIds.Lovag => "L",
                CharacterClassIds.Tolvaj => "T",
                CharacterClassIds.Pap => "P",
                CharacterClassIds.Mágus => "M",
                _ => "?"
            };
    }

    /// <summary>A megadott avatarkészlet osztályjelét adja vissza a térképi megjelenítéshez.</summary>
    public static string PartyAvatarGlyph(string characterClassId, PartyAvatarSet avatarSet) =>
        CharacterClassGlyph(characterClassId, avatarSet);

    /// <summary>A karakter aranyát a karakterlap 9. sorára formázza.</summary>
    public static CharacterSheetPanelLine BuildGoldLine(LiveCharacter character, int goldenKeyCount = 0,
        int bossCount = 0, int width = Width) =>
        BuildGoldLine(character.Gold, goldenKeyCount, bossCount, width);

    private static CharacterSheetPanelLine BuildGoldLine(int gold, int goldenKeyCount, int bossCount, int width)
    {
        var money = $"Arany: {gold} {ConsoleRenderer.MoneyIcon}";
        var keys = $"🔑 {goldenKeyCount}/{bossCount}";
        var effectiveWidth = Math.Max(Width, width);
        var column = 18;
        if (BattleCommandPanel.DisplayWidth(money) >= column &&
            BattleCommandPanel.DisplayWidth(money) + 1 + BattleCommandPanel.DisplayWidth(keys) > effectiveWidth)
            money = $"Arany: {gold}";
        column = Math.Max(column, BattleCommandPanel.DisplayWidth(money) + 1);
        return new CharacterSheetPanelLine(9,
            money + new string(' ', column - BattleCommandPanel.DisplayWidth(money)) + keys,
            ConsoleColor.Yellow);
    }

    /// <summary>
    /// Élő karakterből (domain objektumból) készít rövid parti-státusz sort.
    /// A metódus a szükséges mezőket kinyeri, majd a közös, belső összeállító
    /// metódusnak adja át a formázáshoz és színezéshez.
    /// </summary>
    public static PartyStatusLine BuildPartyStatus(LiveCharacter character, bool isDisplayed, bool isLeader = false,
        int width = Width) =>
        BuildPartyStatus(character.Name, character.CharacterClass.Id, character.Level, character.CurrentVitality,
            character.MaximumVitality, character.CurrentMana, character.MaximumMana, character.IsAlive,
            character.UsesMana, character.Color, isDisplayed, isLeader, width);

    /// <summary>
    /// Session snapshotból készít rövid parti-státusz sort.
    /// Funkciója megegyezik az élő karakteres overloaddal, de hálózati/snapshot
    /// adatszerkezetből dolgozik ugyanarra a megjelenítési modellre.
    /// </summary>
    public static PartyStatusLine BuildPartyStatus(SessionCharacterSnapshot character, bool isDisplayed,
        bool isLeader = false, int width = Width) =>
        BuildPartyStatus(character.Name, character.CharacterClassId, character.Level, character.CurrentVitality,
            character.MaximumVitality, character.CurrentMana, character.MaximumMana, character.IsAlive,
            character.CharacterSheet?.UsesMana == true, character.Color, isDisplayed, isLeader, width);

    /// <summary>
    /// Élő karakter aktuális életerő/mána állapotából készít egy erőforrás-sort.
    /// A tényleges formázást és színlogikát a belső overload végzi.
    /// </summary>
    public static CharacterResourceLine BuildResourceLine(LiveCharacter character) =>
        BuildResourceLine(character.CurrentVitality, character.MaximumVitality,
            character.CurrentMana, character.MaximumMana, character.UsesMana);

    /// <summary>
    /// Snapshot karakterből készít erőforrás-sort (életerő és opcionális mána).
    /// A mana megjelenítését a karakterlap-projekció UsesMana jelzője határozza meg.
    /// </summary>
    public static CharacterResourceLine BuildResourceLine(SessionCharacterSnapshot character) =>
        BuildResourceLine(character.CurrentVitality, character.MaximumVitality,
            character.CurrentMana, character.MaximumMana, character.CharacterSheet?.UsesMana == true);

    /// <summary>
    /// A numerikus erőforrásértékekből állítja elő a megjelenítendő szöveget és színeket.
    /// Az életerő színe kritikus tartományban pirosra vált, a mána pedig szürkített,
    /// ha nem használ mágiát a karakter vagy elfogyott a mána.
    /// </summary>
    private static CharacterResourceLine BuildResourceLine(int currentVitality, int maximumVitality,
        int currentMana, int maximumMana, bool usesMana)
    {
        var vitality = $"❤️{currentVitality}/{maximumVitality}";
        var mana = usesMana ? $"  🔷{currentMana}/{maximumMana}" : string.Empty;
        return new CharacterResourceLine(vitality,
            maximumVitality > 0 && currentVitality * 2 < maximumVitality
                ? ConsoleColor.Red
                : ConsoleColor.Green,
            mana, !usesMana || currentMana <= 0 ? ConsoleColor.DarkGray : ConsoleColor.Cyan);
    }

    /// <summary>
    /// A parti-listában megjelenő egy soros karakterstátuszt építi fel.
    /// Kezeli a kijelölt marker, osztály-jel, névrövidítés, halott állapot,
    /// valamint az életerő és mána aktuális értékeinek megjelenítését.
    /// Az erőforrások színét a százalékos állapot határozza meg; a vezér neve és szintje inverz színt kap.
    /// </summary>
    private static PartyStatusLine BuildPartyStatus(string name, string classId, int level, int currentVitality,
        int maximumVitality, int currentMana, int maximumMana, bool isAlive, bool usesMana,
        ConsoleColor identityColor,
        bool isDisplayed, bool isLeader, int width)
    {
        var marker = isDisplayed ? "▶ " : "  ";
        var prefix = $"{marker}{CharacterClassGlyph(classId)} ";
        var levelSuffix = $" ★ {level.ToString().PadLeft(PartyStatusLevelColumnWidth)}";
        if (!isAlive)
        {
            const string dead = " 💀";
            var deadRowWidth = Math.Max(Width, width);
            var deadMaximumNameLength = Math.Min(PartyStatusNameColumnWidth,
                deadRowWidth - prefix.Length - levelSuffix.Length - dead.Length);
            var alignedName = Shorten(name, deadMaximumNameLength).PadRight(Math.Max(1, deadMaximumNameLength));
            return new PartyStatusLine(prefix + alignedName + levelSuffix,
                identityColor, dead, ConsoleColor.DarkRed, string.Empty, ConsoleColor.DarkGray,
                isLeader ? prefix.Length : -1);
        }

        var vitalityPercent = Percent(currentVitality, maximumVitality);
        var manaPercent = Percent(currentMana, maximumMana);
        var vitality = $" ❤️{currentVitality}";
        var mana = usesMana && maximumMana > 0 ? $" 🔷{currentMana}" : string.Empty;
        var effectiveWidth = Math.Max(Width, width);
        var maximumNameLength = Math.Min(PartyStatusNameColumnWidth,
            effectiveWidth - prefix.Length - levelSuffix.Length - vitality.Length - mana.Length);
        var alignedLiveName = Shorten(name, maximumNameLength).PadRight(Math.Max(1, maximumNameLength));
        return new PartyStatusLine(prefix + alignedLiveName + levelSuffix, identityColor,
            vitality.PadRight(PartyStatusHpColumnWidth), vitalityPercent <= 25 ? ConsoleColor.Red :
            vitalityPercent <= 50 ? ConsoleColor.Yellow : ConsoleColor.Green,
            mana, !usesMana || maximumMana <= 0 || currentMana <= 0 ? ConsoleColor.DarkGray :
            manaPercent <= 50 ? ConsoleColor.Blue : ConsoleColor.Cyan,
            isLeader ? prefix.Length : -1);
    }

    /// <summary>
    /// Biztonságosan százalékot számol két értékből.
    /// Nullás vagy negatív maximum esetén 0-t ad, különben kerekít,
    /// majd az eredményt 0 és 100 közé korlátozza.
    /// </summary>
    private static int Percent(int current, int maximum) => maximum <= 0
        ? 0
        : Math.Clamp((int)Math.Round(current * 100d / maximum), 0, 100);

    /// <summary>
    /// A kapott szöveget legfeljebb a megadott hosszig vágja vissza.
    /// A hosszkorlát legalább 1; az üres bemenet továbbra is üres marad.
    /// </summary>
    private static string Shorten(string value, int maximumLength) =>
        value[..Math.Min(value.Length, Math.Max(1, maximumLength))];

    /// <summary>
    /// Élő karakterből teljes karakterlap-panel sorlistát készít.
    /// A metódus először snapshot/projekció objektumokat hoz létre, majd a
    /// snapshot alapú Build overloadot hívja a tényleges panelsorok összeállítására.
    /// </summary>
    /// <param name="character">A megjelenítendő élő karakter.</param>
    /// <param name="experienceByLevel">A szintenkénti tapasztalati küszöbértékek.</param>
    /// <param name="mazeLevel">A labirintusszint, amely a fejlécet és a látásmódosítót meghatározza.</param>
    /// <param name="goldenKeyCount">A megszerzett aranykulcsok száma.</param>
    /// <param name="bossCount">Az aranykulcsok célértéke.</param>
    /// <param name="isPartyLeader">Jelzi, hogy a karakter a parti vezére.</param>
    /// <param name="isTemporaryFollower">Jelzi, hogy a karakter ideiglenes követő.</param>
    /// <param name="width">A formázási szélesség; legalább Width érvényesül.</param>
    /// <param name="combatStatusIcons">A karakter állapotikonjaihoz hozzáadott, ismétlés nélkül megjelenő harci ikonok.</param>
    /// <param name="explorationClockIndicator">A fejléc opcionális időjelzője.</param>
    /// <returns>Fix sorpozíciókkal ellátott paneladatok; az erőforrássort a renderer tölti ki.</returns>
    public static IReadOnlyList<CharacterSheetPanelLine> Build(LiveCharacter character,
        IReadOnlyDictionary<int, int> experienceByLevel, int mazeLevel, int goldenKeyCount, int bossCount,
        bool isPartyLeader = false, bool isTemporaryFollower = false, int width = Width,
        IReadOnlyList<string>? combatStatusIcons = null, string explorationClockIndicator = "", GameTimeSnapshot? gameTime = null)
    {
        var characterSheet = CharacterSheetSnapshotProjector.Create(character, experienceByLevel,
            MazeLevelConfigurations.Get(mazeLevel).VisionModifier);
        characterSheet = characterSheet with
        {
            StatusIcons = characterSheet.StatusIcons.Concat(combatStatusIcons ?? [])
                .Distinct(StringComparer.Ordinal).ToArray()
        };
        var snapshot = new SessionCharacterSnapshot(character.Id, character.Name, character.Race.Id,
            character.CharacterClass.Id, character.Level, character.CurrentVitality, character.MaximumVitality,
            character.CurrentMana, character.MaximumMana, character.FoodLevel, character.WaterLevel,
            character.Gold, character.IsAlive, null, character.Statuses.Select(status => status.Id).ToArray(),
            InventorySnapshotProjector.Create(character),
            characterSheet,
            IsTemporaryFollower: isTemporaryFollower);
        return Build(snapshot, mazeLevel, goldenKeyCount, bossCount, isPartyLeader, width,
            explorationClockIndicator, gameTime);
    }

    /// <summary>
    /// Session snapshot alapján felépíti a teljes karakterlap panel minden sorát,
    /// beleértve az alapadatokat, statokat, erőforrásokat, állapotokat, osztályfejlesztéseket
    /// és az inventory külön blokkjait fix sorpozíciókkal.
    /// A külön színű kiegészítő szöveget és szövegvéget a panelsor megjelenítési adatai írják le.
    /// </summary>
    /// <param name="character">Karakterlap- és inventory-projekcióval rendelkező session snapshot.</param>
    /// <param name="mazeLevel">A fejlécben megjelenített labirintusszint.</param>
    /// <param name="goldenKeyCount">A megszerzett aranykulcsok száma.</param>
    /// <param name="bossCount">Az aranykulcsok célértéke.</param>
    /// <param name="isPartyLeader">Jelzi, hogy a karakter a parti vezére.</param>
    /// <param name="width">A formázási szélesség; legalább Width érvényesül.</param>
    /// <param name="explorationClockIndicator">A fejléc opcionális időjelzője.</param>
    /// <returns>A karakterlap sorai, az 5. soron az erőforrás-megjelenítés helyőrzőjével.</returns>
    /// <exception cref="ArgumentNullException">A character null.</exception>
    /// <exception cref="ArgumentException">Hiányzik a karakterlap- vagy inventory-projekció.</exception>
    public static IReadOnlyList<CharacterSheetPanelLine> Build(SessionCharacterSnapshot character,
        int mazeLevel, int goldenKeyCount, int bossCount, bool isPartyLeader = false, int width = Width,
        string explorationClockIndicator = "", GameTimeSnapshot? gameTime = null)
    {
        ArgumentNullException.ThrowIfNull(character);
        var details = character.CharacterSheet ?? throw new ArgumentException(
            "A karakterlap-projekció hiányzik a session snapshotból.", nameof(character));
        var inventory = character.Inventory ?? throw new ArgumentException(
            "Az inventory-projekció hiányzik a session snapshotból.", nameof(character));
        var effectiveWidth = Math.Max(Width, width);
        var lines = new List<CharacterSheetPanelLine>
        {
            BuildWorldHeaderLine(mazeLevel, goldenKeyCount, bossCount, explorationClockIndicator, effectiveWidth, gameTime),
            new(1, $"KARAKTERLAP - {character.Name}", ConsoleColor.Yellow),
            new(2, $"{details.RaceName} {details.CharacterClassName}" +
                   (isPartyLeader ? "  👑 VEZÉR" : character.IsTemporaryFollower ? "  👤 KÖVETŐ" : string.Empty),
                character.IsTemporaryFollower ? ConsoleColor.Black :
                isPartyLeader ? ConsoleColor.Yellow : ConsoleColor.White,
                Background: character.IsTemporaryFollower ? ConsoleColor.Yellow : ConsoleColor.Black)
        };
        lines.Add(new(3, details.NextLevelExperience is { } next
            ? $"Szint: {character.Level}  XP: {details.Experience}/{next}"
            : $"Szint: {character.Level}  XP: MAX", ConsoleColor.Cyan));
        
        var visionColor = details.VisionRange < details.NaturalVisionRange ? ConsoleColor.Red :
            details.VisionRange > details.NaturalVisionRange ? ConsoleColor.Green : ConsoleColor.White;
        var visionText =
            $"💪{details.Abilities.Strength} 🏹{details.Abilities.Dexterity} " +
            $"💖{details.Abilities.Health} 🧠{details.Abilities.Intelligence} 👁️";

        lines.Add(new(
            4,
            visionText + details.VisionRange,
            ConsoleColor.White,
            ColoredTextStart: visionText.Length,
            ColoredTextColor: visionColor));
        
        lines.Add(new(5, "", ConsoleColor.Black)); //placeholder, it is replaced by the resource line in the UI
        lines.Add(new(6, $"É: {ResourceIcons("🍖", character.FoodLevel)}", ConsoleColor.Yellow));
        lines.Add(new(7, $"V: {ResourceIcons("💧", character.WaterLevel)}", ConsoleColor.Cyan));
        var statusIcons = details.StatusIcons
            .Select(icon => icon == "🪨" ? ConsoleRenderer.DamageReductionIcon : icon).ToArray();
        lines.Add(new(8, statusIcons.Length == 0 ? "Áll: nincs" : $"Áll: {string.Join(' ', statusIcons)}",
            details.StatusIcons.Count == 0 ? ConsoleColor.DarkGray : ConsoleColor.Magenta));
        lines.Add(BuildGoldLine(character.Gold, goldenKeyCount, bossCount, effectiveWidth));
        var proficiencies = details.WeaponProficiencyNames ?? [];
        lines.Add(new(10, proficiencies.Count > 0 ? $"Jártasság: {string.Join(' ', proficiencies)}" : "Jártasság: —",
            proficiencies.Count > 0 ? ConsoleColor.Yellow : ConsoleColor.DarkGray));
        var perkRows = BuildPerkRows(details.PerkNames, effectiveWidth);
        lines.Add(new(11, perkRows[0], ConsoleColor.Magenta));
        lines.Add(new(12, perkRows[1], perkRows[1].Length == 0 ? ConsoleColor.Black : ConsoleColor.Magenta));
        lines.Add(new(13, "OSZTÁLYFEJLESZTÉSEK", ConsoleColor.DarkCyan));
        var upgrades = details.ClassFeatureUpgradeNames ?? [];
        lines.Add(new(14, upgrades.Count > 0 ? Shorten($"L10: {upgrades[0]}", effectiveWidth) : "L10: —", upgrades.Count > 0 ? ConsoleColor.Cyan : ConsoleColor.DarkGray));
        lines.Add(new(15, upgrades.Count > 1 ? Shorten($"L20: {upgrades[1]}", effectiveWidth) : "L20: —", upgrades.Count > 1 ? ConsoleColor.Cyan : ConsoleColor.DarkGray));
        lines.Add(new(16, BlankLineForWidth(effectiveWidth), ConsoleColor.Black));
        lines.Add(new(17, "FEGYVEREK ", ConsoleColor.Yellow,
            ColoredSuffix: $"⚔ ⚖ {details.EquippedWeight}/{details.CombatCarryingCapacity:0.##}  ⚡ {details.InitiativeBase}",
            ColoredSuffixColor: EncumbranceColor(details.Encumbrance)));
        AddInventoryLines(lines, inventory, details, effectiveWidth);
        return lines;
    }

    /// <summary>A labirintusszintet, játékidőt, napszakot és homokórát a 0. sorra formázza.</summary>
    /// <remarks>Balra a ▦ pályajel, középen │ jelekkel elválasztott idő, jobbra négycellás homokórahely.</remarks>
    public static CharacterSheetPanelLine BuildWorldHeaderLine(int mazeLevel, int goldenKeyCount, int bossCount,
        string explorationClockIndicator, int width = Width, GameTimeSnapshot? gameTime = null)
    {
        var time = gameTime ?? new GameTimeSnapshot();
        const int clockWidth = 4;
        var level = $"▦ {mazeLevel}";
        var levelWidth = BattleCommandPanel.DisplayWidth(level);
        var available = Math.Max(0, width - levelWidth - clockWidth - 2);
        var middle = $"│ {time.DisplayText} │";
        if (BattleCommandPanel.DisplayWidth(middle) > available)
            middle = $"│{time.DisplayText}│";
        if (BattleCommandPanel.DisplayWidth(middle) > available)
            middle = $"│{time.Day}n {time.Hour:00}:{time.Minute:00} {time.DayNightIcon}│";
        middle = BattleCommandPanel.TruncateToDisplayWidth(middle, available);
        var middleWidth = BattleCommandPanel.DisplayWidth(middle);
        var clockStart = Math.Max(levelWidth, width - clockWidth);
        var middleStart = Math.Clamp((width - middleWidth) / 2,
            levelWidth, Math.Max(levelWidth, clockStart - middleWidth));
        return new CharacterSheetPanelLine(0,
            level + new string(' ', middleStart - levelWidth) + middle +
            new string(' ', Math.Max(0, clockStart - middleStart - middleWidth)) +
            explorationClockIndicator, ConsoleColor.Green);
    }

    /// <summary>Egy fejlécsor elé fókuszjelzőt tesz, és beállítja a fókuszhoz tartozó hátteret.</summary>
    internal static CharacterSheetPanelLine WithFocusMarker(CharacterSheetPanelLine line, bool focused,
        int width = Width)
    {
        var contentWidth = Math.Max(0, width - 1);
        var content = BattleCommandPanel.TruncateToDisplayWidth(line.Text, contentWidth);
        return line with
        {
            Text = (focused ? "»" : "«") + content,
            Background = focused ? ConsoleColor.DarkCyan : ConsoleColor.Black
        };
    }

    /// <summary>
    /// Az inventory-hoz tartozó panelsorokat (fegyverek, páncél, varázstárgyak, hátizsák)
    /// hozzáfűzi a meglévő sorlistához. Kezeli a kétkezes fegyver miatti tiltott második
    /// fegyverhely megjelenítését és a terheltséghez tartozó színkódolást is.
    /// </summary>
    private static void AddInventoryLines(ICollection<CharacterSheetPanelLine> lines,
        CharacterInventorySnapshot inventory, CharacterSheetSnapshot details, int width)
    {
        var weapons = Slots(inventory, InventorySlotKind.Weapon, 3);
        lines.Add(DurableInventoryLine(18, "1", weapons[0], InventorySlotKind.Weapon, 0,
            ConsoleColor.Gray));
        lines.Add(new(19, weapons[0].Item?.IsTwoHanded == true
                ? "2: ⛔ kétkezes fegyver"
                : $"2: {ItemName(weapons[1].Item)}",
            weapons[0].Item?.IsTwoHanded == true ? ConsoleColor.DarkGray : ConsoleColor.Gray,
            new InventorySlotAddress(InventorySlotKind.Weapon, 1),
            ColoredTextStart: weapons[0].Item?.IsTwoHanded == true ? -1 : "2: ".Length,
            ColoredTextColor: DurabilityColor(weapons[1].Item)));
        lines.Add(DurableInventoryLine(20, "3", weapons[2], InventorySlotKind.Weapon, 2,
            ConsoleColor.Gray));
        var armor = Slots(inventory, InventorySlotKind.Armor, 1)[0];
        lines.Add(DurableInventoryLine(21, "páncél", armor, InventorySlotKind.Armor, 0,
            ConsoleColor.DarkYellow));

        var magicItems = Slots(inventory, InventorySlotKind.MagicItem, 3);
        lines.Add(new(22, $"VARÁZSTÁRGYAK {magicItems.Count(slot => slot.Item is not null)}/3", ConsoleColor.Magenta));
        for (var index = 0; index < magicItems.Count; index++)
            lines.Add(new(23 + index, $"{index + 1}: {ItemName(magicItems[index].Item)}", MagicItemSlotColor(magicItems[index].Item),
                new InventorySlotAddress(InventorySlotKind.MagicItem, index)));

        var backpack = Slots(inventory, InventorySlotKind.Backpack, LiveCharacter.MaximumBackpackItemCount);
        lines.Add(new(26, $"HÁTIZSÁK {backpack.Count(slot => slot.Item is not null)}/{LiveCharacter.MaximumBackpackItemCount} " +
            $"⚖ {details.CarriedWeight:F1}/{details.CarryingCapacity}", EncumbranceColor(details.CarriedEncumbrance)));
        for (var index = 0; index < backpack.Count; index++)
            lines.Add(new(27 + index, $"{index + 1}: {ItemName(backpack[index].Item)}", BackpackSlotColor(backpack[index].Item),
                new InventorySlotAddress(InventorySlotKind.Backpack, index)));
    }

    /// <summary>Az üres hátizsákhelyet szürkíti, a foglalt helyet normál szürkével jelöli.</summary>
    private static ConsoleColor BackpackSlotColor(InventoryItemSnapshot? item)
    {
        if (item is null) return ConsoleColor.DarkGray;
        return ConsoleColor.Gray;
    }

    /// <summary>Az üres varázstárgyhelyet szürkíti, a foglalt helyet ciánnal jelöli.</summary>
    private static ConsoleColor MagicItemSlotColor(InventoryItemSnapshot? item)
    {
        if (item is null) return ConsoleColor.DarkGray;
        return ConsoleColor.Cyan;   
    }

    /// <summary>
    /// Visszaadja egy adott típus (kind) meghatározott darabszámú slotjait index szerinti
    /// sorrendben. Feltételezi, hogy minden keresett indexhez létezik megfelelő slot.
    /// </summary>
    private static IReadOnlyList<InventorySlotSnapshot> Slots(CharacterInventorySnapshot inventory,
        InventorySlotKind kind, int count) => Enumerable.Range(0, count)
        .Select(index => inventory.Slots.First(slot => slot.Kind == kind && slot.Index == index)).ToArray();

    /// <summary>
    /// Egy inventory elem megjelenítendő nevét állítja elő.
    /// Üres slot esetén "üres" szöveget ad, töltetes tárgynál megjeleníti a
    /// töltetszámot, halmozható tárgynál pedig a darabszámot is.
    /// </summary>
    private static string ItemName(InventoryItemSnapshot? item) => item is null
        ? "üres"
        : (item.MaximumCharges > 0 ? $"{item.Name} ({item.Charges}/{item.MaximumCharges})" : item.Name) +
          (item.Quantity > 1 ? $" ×{item.Quantity}" : string.Empty);

    /// <summary>Inventory-sort készít, amelyben a címke és a tartósság szerint színezett tárgynév különválik.</summary>
    private static CharacterSheetPanelLine DurableInventoryLine(int row, string label,
        InventorySlotSnapshot slot, InventorySlotKind kind, int index, ConsoleColor labelColor) =>
        new(row, $"{label}: {ItemName(slot.Item)}", labelColor,
            new InventorySlotAddress(kind, index), ColoredTextStart: $"{label}: ".Length,
            ColoredTextColor: DurabilityColor(slot.Item));

    /// <summary>A felszerelés állapotához előtérszínt rendel; üres helyhez sötétszürkét ad.</summary>
    private static ConsoleColor DurabilityColor(InventoryItemSnapshot? item) => item is null
        ? ConsoleColor.DarkGray
        : EquipmentDurabilityRules.Condition(item.MaximumDurability, item.DurabilityDamage) switch
        {
            EquipmentCondition.Intact => ConsoleColor.Green,
            EquipmentCondition.Worn => ConsoleColor.Yellow,
            EquipmentCondition.Damaged => ConsoleColor.Red,
            EquipmentCondition.Broken => ConsoleColor.DarkRed,
            _ => ConsoleColor.Gray
        };

    /// <summary>
    /// A terheltségi kategória szövegét UI színre képezi le.
    /// Nehéz terhelés: piros, közepes: sárga, minden más eset: zöld.
    /// </summary>
    private static ConsoleColor EncumbranceColor(string encumbrance) => encumbrance switch
    {
        "Nehéz" => ConsoleColor.Red,
        "Közepes" => ConsoleColor.Yellow,
        _ => ConsoleColor.Green
    };

    /// <summary>
    /// Egy erőforrás-szintből (pl. éhség/szomjúság) ismételt ikonláncot készít.
    /// Az ikonok darabszámát a ResourceIconStep osztással számolja.
    /// </summary>
    private static string ResourceIcons(string icon, int level) =>
        string.Concat(Enumerable.Repeat(icon, level / ResourceIconStep));

    /// <summary>
    /// A teljes tehetségneveket két sorba osztja, ha van olyan töréspont, ahol mind elférnek.
    /// Csak akkor rövidít, ha a teljes nevek egyik két soros elrendezésben sem férnek el.
    /// </summary>
    internal static IReadOnlyList<string> BuildPerkRows(IEnumerable<string> values, int width)
    {
        var names = values.ToList();
        var effectiveWidth = Math.Max(Width, width);
        if (names.Count == 0) return ["Teh: nincs", BlankLineForWidth(effectiveWidth)];
        string First(int count) => "Teh: " + string.Join(", ", names.Take(count));
        string Second(int count) => string.Join(", ", names.Skip(count));
        var fullRows = Enumerable.Range(1, names.Count)
            .Select(count => (First: First(count), Second: Second(count)))
            .Where(rows => BattleCommandPanel.DisplayWidth(rows.First) <= effectiveWidth &&
                           BattleCommandPanel.DisplayWidth(rows.Second) <= effectiveWidth)
            .OrderBy(rows => Math.Max(BattleCommandPanel.DisplayWidth(rows.First),
                BattleCommandPanel.DisplayWidth(rows.Second)))
            .FirstOrDefault();
        if (fullRows.First is not null)
            return [fullRows.First, fullRows.Second.Length == 0 ? BlankLineForWidth(effectiveWidth) : fullRows.Second];

        var namesPerRow = (int)Math.Ceiling(names.Count / 2d);
        var result = new string[2];
        for (var row = 0; row < 2; row++)
        {
            var rowNames = names.Skip(row * namesPerRow).Take(namesPerRow).ToList();
            if (rowNames.Count == 0)
            {
                result[row] = BlankLineForWidth(effectiveWidth);
                continue;
            }
            var rowPrefix = row == 0 ? "Teh: " : string.Empty;
            var separatorWidth = (rowNames.Count - 1) * 2;
            var availablePerName = Math.Max(1, (effectiveWidth - rowPrefix.Length - separatorWidth) / rowNames.Count);
            result[row] = rowPrefix + string.Join(", ", rowNames.Select(name =>
                name.Length <= availablePerName ? name : name[..availablePerName]));
        }
        return result;
    }
}
