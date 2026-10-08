using KaoszRubin.Combat;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;
using System.Diagnostics;
using System.Text;

namespace KaoszRubin.UI;

public sealed partial class ConsoleRenderer
{
    /// <summary>
    /// Renders the right-hand character sheet, detail pages, party rows, and portrait using cached row state.
    /// </summary>
    /// <remarks>Input dispatch remains the caller's responsibility; opening a detail page does not itself filter keys.</remarks>
    public sealed class CharacterSheetRenderer
    {
        private const int CharacterSheetHeaderLine = 1;
        private const int CharacterSheetStatusLine = 8;
        private const int CharacterSheetVitalityLine = 5;
        private const int CharacterSheetGoldLine = 9;
        private const int CharacterSheetWeaponsHeadingLine = 17;
        private const int CharacterSheetMagicItemsHeadingLine = 22;
        private const int CharacterSheetBackpackHeadingLine = 26;
        public const int CharacterSheetPartyMembersStartLine = PartyFormationDisplay.PartyStartRow;
        public const int CharacterSheetPartyMemberRows = PartyFormationDisplay.PartyRows;
        private const int CharacterSheetReservedMessageLine = 39;
        /// <summary>The zero-based row reserved for party formation status.</summary>
        public const int CharacterSheetControlsLine = PartyFormationDisplay.FormationRow;

        private enum SheetSelectionKind { Weapon, Armor, MagicItem, Backpack, PartyMember }
        private readonly record struct SheetSelectionKey(SheetSelectionKind Kind, int Index);
        private sealed record SheetSelectionEntry(SheetSelectionKey Key);
        private readonly record struct PartyStatusRowState(PartyStatusLine? Status, ConsoleColor Background);
        private readonly record struct InventoryRowState(CharacterSheetPanelLine Line, ConsoleColor Background);
        private sealed record PicturePanelState(string Portrait, ConsoleColor Color, ConsoleColor Background,
            int Width, WindowFrameStyle Style);

        private readonly ConsoleRenderer _owner;
        private readonly Party _party;

        private bool _characterSheetFocused;
        private int _selectedSpellInfoIndex;
        private IReadOnlyList<CharacterSheetPanelLine>? _itemInspectionPanel;
        private SheetSelectionKey? _activeSheetSelection;
        private readonly Dictionary<LiveCharacter, SheetSelectionKey> _lastSheetSelections = [];

        private CharacterId? _lastCharacterSheetCharacterId;
        private readonly Dictionary<int, CharacterSheetPanelLine> _lastCharacterSheetLines = [];
        private readonly Dictionary<int, InventoryRowState> _lastInventoryRows = [];
        private CharacterResourceLine? _lastCharacterVitalsOverlay;
        private readonly Dictionary<int, PartyStatusRowState> _lastPartyStatusRows = [];
        private PicturePanelState? _lastPicturePanelState;

        private PartyFormationSnapshot? _formation;
        private LiveCharacter? _displayedCharacter;

        /// <summary>Creates a character-sheet renderer sharing the owner's console surface and party state.</summary>
        internal CharacterSheetRenderer(ConsoleRenderer owner, Party party)
        {
            _owner = owner;
            _party = party;
        }

        /// <summary>
        /// Gets the character currently displayed on the right panel.
        /// Falls back to the party leader when no character has been displayed.
        /// </summary>
        /// <exception cref="InvalidOperationException">Neither a displayed character nor a party leader is available.</exception>
        public LiveCharacter DisplayedCharacter =>
            _displayedCharacter ?? _party.Leader ?? throw new InvalidOperationException("Nincs megjeleníthető karakter.");

        /// <summary>
        /// Gets whether the spell info page is currently open.
        /// </summary>
        /// <remarks>Callers must keep F-key quick-slot assignment available while disabling item-only actions.</remarks>
        public bool IsSpellInfoPageOpen => _owner._spellInfoCharacter is not null;

        /// <summary>
        /// Gets whether an item inspection page is currently open.
        /// </summary>
        /// <remarks>Callers must restrict input to the exit actions: Esc, I, or Enter.</remarks>
        public bool IsItemInspectionPageOpen => _itemInspectionPanel is not null;

        /// <summary>
        /// Gets the character whose spells are currently shown on the spell info page.
        /// Use this when applying quick-slot or cast actions from spell info selection.
        /// </summary>
        public LiveCharacter? SpellInfoCharacter => _owner._spellInfoCharacter;

        /// <summary>
        /// Refreshes the right panel for the current display context.
        /// Preserves open detail pages and does nothing when neither a displayed character nor a party member exists.
        /// </summary>
        public void RefreshCharacterSheet()
        {
            var character = _displayedCharacter ?? _party.Members.FirstOrDefault();
            if (character is null) return;

            RefreshCharacterSheet(character);
        }

        /// <summary>
        /// Refreshes the right panel using a fallback character when no display character is set.
        /// Use this after character state updates when spell info and inspection overlays should be preserved.
        /// </summary>
        /// <param name="character">The fallback when the displayed character is no longer a party member or temporary follower.</param>
        public void RefreshCharacterSheet(LiveCharacter character)
        {
            if (_owner._spellInfoCharacter is not null)
            {
                DrawSpellInfoPage(_owner._spellInfoCharacter, _selectedSpellInfoIndex);
                return;
            }

            if (_itemInspectionPanel is not null)
            {
                DrawItemInspectionPage(_itemInspectionPanel);
                return;
            }

            var characterToDraw = _displayedCharacter is not null && SheetCharacters().Contains(_displayedCharacter)
                ? _displayedCharacter
                : character;
            DrawCharacterSheet(characterToDraw);
        }

        /// <summary>
        /// Updates only the gold row in the character sheet.
        /// Use this for lightweight UI refresh after transactions that only change currency.
        /// </summary>
        /// <param name="character">The character whose gold is written, regardless of the current display selection.</param>
        public void UpdateGoldInCharacterSheet(LiveCharacter character)
        {
            var goldLine = CharacterSheetPanel.BuildGoldLine(character);
            WriteSheetLine(CharacterSheetGoldLine, goldLine.Text, goldLine.Color, goldLine.Background);
        }

        /// <summary>
        /// Draws the inn variant of the character sheet and focuses sheet navigation.
        /// Use this when entering inn inventory management mode.
        /// </summary>
        public void DrawInnCharacterSheet(LiveCharacter character)
        {
            _owner.DrawFrame();
            if (_displayedCharacter is null || !SheetCharacters().Contains(_displayedCharacter))
                _displayedCharacter = character;
            DrawCharacterSheet(_displayedCharacter);
            SetCharacterSheetFocused(true);
        }

        /// <summary>
        /// Sets focus visual state for character sheet controls.
        /// Use this when toggling between movement/input mode and character-sheet input mode.
        /// </summary>
        /// <param name="focused">Whether to highlight the two sheet header rows as focused.</param>
        public void SetCharacterSheetFocused(bool focused)
        {
            _characterSheetFocused = focused;
            if (_displayedCharacter is null) return;
            DrawCharacterSheetHeaders(_displayedCharacter);
        }

        /// <summary>
        /// Moves the current character-sheet selection up or down.
        /// Use positive values to move forward and negative values to move backward.
        /// </summary>
        /// <param name="direction">The selection offset, normally 1 or -1; zero leaves the selection unchanged.</param>
        /// <remarks>Navigation wraps through inventory and party rows and is ignored during item inspection.</remarks>
        public void MoveCharacterSheetSelection(int direction)
        {
            if (_itemInspectionPanel is not null) return;
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

        /// <summary>
        /// Switches the displayed party member on the character sheet.
        /// Use this for left/right navigation between members while preserving per-character selection.
        /// </summary>
        /// <param name="direction">The character offset, normally 1 or -1; zero leaves the display unchanged.</param>
        /// <remarks>Cycles through party members and temporary followers; ignored during item inspection.</remarks>
        public void MoveDisplayedPartyMember(int direction)
        {
            if (_itemInspectionPanel is not null) return;
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

        /// <summary>
        /// Draws the spell information page for a character.
        /// Use this when opening spell details or when spell selection index changes.
        /// </summary>
        /// <param name="character">The character whose known spells are ordered by level and then name.</param>
        /// <param name="selectedIndex">The requested spell index, clamped to the available list.</param>
        /// <remarks>Replaces item inspection and clears unused rows through the portrait panel's bottom edge.</remarks>
        public void DrawSpellInfoPage(LiveCharacter character, int selectedIndex)
        {
            _itemInspectionPanel = null;
            var spells = character.KnownSpells.OrderBy(spell => spell.Level).ThenBy(spell => spell.Name).ToList();
            selectedIndex = spells.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, spells.Count - 1);
            _owner._spellInfoCharacter = character;
            _selectedSpellInfoIndex = selectedIndex;
            var info = SpellInfoSnapshotProjector.Create(character, _owner._gameData);
            var panelLines = SpellInfoPanel.Build(character.Name, character.CharacterClass.Id, character.Level,
                info, selectedIndex, _characterSheetFocused, RightSheetWidthForWindow()).ToDictionary(line => line.Row);
            for (var row = 0; row <= PicturePanelBottom; row++)
                if (panelLines.TryGetValue(row, out var line))
                    WriteCharacterSheetPanelLine(line);
                else
                    WriteSheetLine(row, string.Empty, ConsoleColor.Gray);
        }

        /// <summary>
        /// Gets the currently selected spell from the spell info page.
        /// Use this to map Enter/F-key actions to the selected spell.
        /// </summary>
        /// <returns>The selected known spell, or null when the page is closed or its index has no spell.</returns>
        public SpellDefinition? GetSelectedSpellInfo()
        {
            if (_owner._spellInfoCharacter is null) return null;
            var spells = _owner._spellInfoCharacter.KnownSpells.OrderBy(spell => spell.Level).ThenBy(spell => spell.Name).ToList();
            return spells.ElementAtOrDefault(_selectedSpellInfoIndex);
        }

        /// <summary>
        /// Redraws the currently open spell info page.
        /// Use this after quick-slot assignment or any state change affecting spell details.
        /// </summary>
        public void RefreshSpellInfoPage()
        {
            if (_owner._spellInfoCharacter is not null)
                DrawSpellInfoPage(_owner._spellInfoCharacter, _selectedSpellInfoIndex);
        }

        /// <summary>
        /// Moves the selected row on the spell info page.
        /// Use positive/negative direction values for down/up navigation.
        /// </summary>
        /// <param name="direction">The spell offset, normally 1 or -1; zero leaves the selection unchanged.</param>
        /// <remarks>Wraps the selection and does nothing when the page is closed or the character has no known spells.</remarks>
        public void MoveSpellInfoSelection(int direction)
        {
            if (_owner._spellInfoCharacter is null || direction == 0 || _owner._spellInfoCharacter.KnownSpells.Count == 0) return;
            _selectedSpellInfoIndex = (_selectedSpellInfoIndex + direction + _owner._spellInfoCharacter.KnownSpells.Count) %
                                      _owner._spellInfoCharacter.KnownSpells.Count;
            DrawSpellInfoPage(_owner._spellInfoCharacter, _selectedSpellInfoIndex);
        }

        /// <summary>
        /// Closes the spell info page and restores the regular character sheet.
        /// Use this for Escape handling or before starting a cast action from spell info mode.
        /// </summary>
        public void CloseSpellInfoPage()
        {
            if (_owner._spellInfoCharacter is null) return;
            var character = _owner._spellInfoCharacter;
            _owner._spellInfoCharacter = null;
            InvalidateCharacterSheetCache();
            DrawCharacterSheet(character);
        }

        /// <summary>
        /// Draws an item inspection panel on the right side.
        /// Use this when inspecting an identified or unidentified inventory item.
        /// </summary>
        /// <param name="lines">Panel rows with unique row numbers; omitted rows are cleared.</param>
        /// <remarks>Closes spell info. The caller must allow only Esc, I, or Enter to exit inspection.</remarks>
        public void DrawItemInspectionPage(IReadOnlyList<CharacterSheetPanelLine> lines)
        {
            _owner._spellInfoCharacter = null;
            _itemInspectionPanel = lines;
            var panelLines = lines.ToDictionary(line => line.Row);
            for (var row = 0; row <= PicturePanelBottom; row++)
                if (panelLines.TryGetValue(row, out var line))
                    WriteCharacterSheetPanelLine(line);
                else
                    WriteSheetLine(row, string.Empty, ConsoleColor.Gray);
        }

        /// <summary>
        /// Closes the item inspection panel and redraws the selected character sheet.
        /// Use this for inspection exit actions (Esc, I, Enter).
        /// </summary>
        public void CloseItemInspectionPage()
        {
            if (_itemInspectionPanel is null) return;
            _itemInspectionPanel = null;
            if (_displayedCharacter is not null)
            {
                InvalidateCharacterSheetCache();
                DrawCharacterSheet(_displayedCharacter);
            }
        }

        /// <summary>
        /// Returns the inventory slot currently selected on the character sheet.
        /// Use this before drop/use/move/split actions to resolve the target slot.
        /// </summary>
        /// <returns>The selected slot, or null when no character or inventory row is selected.</returns>
        /// <remarks>This lookup does not gate actions; callers must reject item actions while either detail page is open.</remarks>
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

        /// <summary>
        /// Returns the party member currently selected in the party section.
        /// Use this for member-specific actions such as dismissal or behavior display.
        /// </summary>
        /// <returns>The selected party member, or null when the selection is not a valid party row.</returns>
        public LiveCharacter? GetSelectedPartyMember()
        {
            if (_activeSheetSelection is not { Kind: SheetSelectionKind.PartyMember } selection) return null;
            return _party.Members.ElementAtOrDefault(selection.Index);
        }

        /// <summary>
        /// Reconciles sheet state after removing a party member.
        /// Use this immediately after party roster removal to keep selection and display valid.
        /// </summary>
        public void RefreshAfterPartyMemberRemoved(LiveCharacter removedCharacter, LiveCharacter leader)
        {
            _lastSheetSelections.Remove(removedCharacter);
            if (_displayedCharacter == removedCharacter) _displayedCharacter = leader;
            if (_displayedCharacter is null || !_party.Members.Contains(_displayedCharacter)) _displayedCharacter = leader;
            _activeSheetSelection = _lastSheetSelections.GetValueOrDefault(_displayedCharacter);
            DrawCharacterSheet(_displayedCharacter);
        }

        /// <summary>
        /// Redraws only selectable inventory and party rows.
        /// Use this after selection or inventory-content changes when full redraw is unnecessary.
        /// </summary>
        public void RefreshInventoryRows()
        {
            if (_displayedCharacter is not null)
                DrawSelectableCharacterSheetRows(_displayedCharacter);
        }

        /// <summary>
        /// Refreshes visible inn-related transaction rows (gold and inventory rows).
        /// Use this after buy/sell operations to avoid redrawing the whole frame.
        /// </summary>
        public void RefreshInnTransactionRows()
        {
            if (_displayedCharacter is null) return;
            if (_displayedCharacter == _party.Leader) UpdateGoldInCharacterSheet(_displayedCharacter);
            var panelLines = CharacterSheetPanel.Build(_displayedCharacter, _owner._gameData.ExperienceByLevel, _owner._mazeLevel,
                _owner._goldenKeyCount, MonsterIds.Bosses.Count, _displayedCharacter == _party.Leader,
                IsTemporaryFollower(_displayedCharacter), RightSheetWidthForWindow());
            foreach (var heading in panelLines.Where(line => line.Row is CharacterSheetWeaponsHeadingLine or
                         CharacterSheetMagicItemsHeadingLine or CharacterSheetBackpackHeadingLine))
                WriteCharacterSheetPanelLine(heading);
            DrawInventorySlotRows(_displayedCharacter, panelLines);
        }

        /// <summary>Refreshes the party list immediately after recruitment, preserving the displayed character and detail pages.</summary>
        public void RefreshPartyStatusRows()
        {
            if (_displayedCharacter is null || _itemInspectionPanel is not null ||
                _owner._spellInfoCharacter is not null) return;
            if (!SheetCharacters().Contains(_displayedCharacter))
            {
                RefreshCharacterSheet(_party.Leader!);
                return;
            }
            DrawPartyStatusRows(_displayedCharacter);
        }

        /// <summary>
        /// Refreshes battle-sensitive rows (status icons, resources, party status).
        /// Use this after damage, healing, mana use, or status effect changes in battle.
        /// </summary>
        public void RefreshBattleStatusRows()
        {
            if (_displayedCharacter is null) return;
            DrawBattleStatusRows(_displayedCharacter);
            DrawPartyStatusRows(_displayedCharacter);
        }

        /// <summary>
        /// Stores and renders current formation status text on the controls row.
        /// Use this whenever formation state/facing/layout changes.
        /// </summary>
        /// <remarks>The state is stored while a detail page is open, but that page is not overwritten.</remarks>
        public void SetFormationStatus(PartyFormationSnapshot formation)
        {
            _formation = formation;
            if (_displayedCharacter is null || _itemInspectionPanel is not null ||
                _owner._spellInfoCharacter is not null) return;

            var line = new CharacterSheetPanelLine(CharacterSheetControlsLine,
                FormationStatusText(formation),
                formation.State == PartyFormationState.Locked ? ConsoleColor.Green : ConsoleColor.DarkCyan,
                Segments: PartyFormationDisplay.Segments(formation, _party.Members.ToDictionary(member => member.Id, member => member.Color)));
            if (_lastCharacterSheetLines.TryGetValue(CharacterSheetControlsLine, out var previous) &&
                SheetLineEquals(previous, line)) return;

            WriteCharacterSheetPanelLine(line);
            _lastCharacterSheetLines[CharacterSheetControlsLine] = line;
        }

        /// <summary>
        /// Formats formation state into the controls-row text.
        /// Use this helper when any UI needs a consistent formation label.
        /// </summary>
        /// <returns>A label containing the facing arrow, formation state, and locked layout when applicable.</returns>
        public static string FormationStatusText(PartyFormationSnapshot formation) => PartyFormationDisplay.Text(formation);

        /// <summary>
        /// Draws detailed battle action text on the right panel.
        /// Use this in non-quick battles when new action details become available.
        /// </summary>
        /// <param name="details">The action details to display, or null for the panel's empty state.</param>
        /// <remarks>Changing the action ID resets paging; rendering is deferred while spell info is open.</remarks>
        public void DrawBattleDetails(BattleActionDetails? details)
        {
            if (_owner._battleDetails?.Id != details?.Id) _owner._battleDetailsPage = 0;
            _owner._battleDetails = details;
            if (_owner._spellInfoCharacter is null)
                foreach (var line in BattleDetailsPanel.Build(details, _owner._battleDetailsPage, RightSheetWidthForWindow()))
                    if (line.ExtendsToDivider)
                        WriteExtendedCharacterSheetLine(line);
                    else
                        WriteCharacterSheetPanelLine(line);
        }

        /// <summary>
        /// Changes the page index of battle details and redraws the panel.
        /// Use +1/-1 direction values for PageDown/PageUp-like behavior.
        /// </summary>
        public void PageBattleDetails(int direction)
        {
            _owner._battleDetailsPage = BattleDetailsPanel.Normalize(_owner._battleDetailsPage + direction,
                BattleDetailsPanel.PageCount(_owner._battleDetails));
            DrawBattleDetails(_owner._battleDetails);
        }

        /// <summary>
        /// Draws the full character sheet for one character into the right panel.
        /// Clears the panel when the character changes; otherwise writes only changed cached rows.
        /// </summary>
        /// <param name="character">The character to display and retain as the active sheet context.</param>
        /// <remarks>Use RefreshCharacterSheet to preserve an open spell info or item inspection page.</remarks>
        public void DrawCharacterSheet(LiveCharacter character)
        {
            var fullRedraw = _lastCharacterSheetCharacterId != character.Id;

            _displayedCharacter = character;

            if (fullRedraw)
            {
                ClearRightPanel();
                InvalidateCharacterSheetCache();
            }

            var panelLines = CharacterSheetPanel.Build(
                character,
                _owner._gameData.ExperienceByLevel,
                _owner._mazeLevel,
                _owner._goldenKeyCount,
                MonsterIds.Bosses.Count,
                character == _party.Leader,
                IsTemporaryFollower(character),
                RightSheetWidthForWindow(),
                _owner.CombatStatusIconsFor(character),
                _owner._explorationClockIndicator);

            if (fullRedraw)
                DrawCharacterSheetHeaders(character);

            var resourceLine = CharacterSheetPanel.BuildResourceLine(character);

            if (fullRedraw || _lastCharacterVitalsOverlay != resourceLine)
            {
                DrawCharacterResourceLine(CharacterSheetVitalityLine, resourceLine);
                _lastCharacterVitalsOverlay = resourceLine;
            }

            foreach (var line in panelLines.Where(line =>
                         line.Row != 0 &&
                         line.Row != CharacterSheetHeaderLine &&
                         line.Row != CharacterSheetVitalityLine &&
                         line.InventorySlot is null))
            {
                if (!fullRedraw &&
                    _lastCharacterSheetLines.TryGetValue(line.Row, out var previous) &&
                    SheetLineEquals(previous, line))
                {
                    continue;
                }

                WriteCharacterSheetPanelLine(line);
                _lastCharacterSheetLines[line.Row] = line;
            }

            DrawSelectableCharacterSheetRows(character);

            if (_owner._battleActive && _owner._battleDetails is not null)
                DrawBattleDetails(_owner._battleDetails);

            if (fullRedraw)
                WriteSheetLine(CharacterSheetReservedMessageLine, string.Empty, ConsoleColor.DarkGray);

            var controlsLine = new CharacterSheetPanelLine(CharacterSheetControlsLine,
                _formation is null ? string.Empty : FormationStatusText(_formation),
                _formation?.State == PartyFormationState.Locked ? ConsoleColor.Green : ConsoleColor.DarkCyan,
                Segments: _formation is null ? null : PartyFormationDisplay.Segments(_formation,
                    _party.Members.ToDictionary(member => member.Id, member => member.Color)));
            if (fullRedraw || !_lastCharacterSheetLines.TryGetValue(CharacterSheetControlsLine, out var oldControls) ||
                !SheetLineEquals(oldControls, controlsLine))
            {
                WriteCharacterSheetPanelLine(controlsLine);
                _lastCharacterSheetLines[CharacterSheetControlsLine] = controlsLine;
            }

            DrawPicturePanel();
            _lastCharacterSheetCharacterId = character.Id;
        }

        /// <summary>
        /// Draws the portrait panel next to the message log.
        /// Use this after character/enemy context changes that affect portrait or portrait color.
        /// </summary>
        /// <remarks>
        /// Prioritizes the acting character, then the battle enemy, then the displayed character.
        /// The panel stays bottom-aligned with the message log as the console height changes.
        /// </remarks>
        public void DrawPicturePanel()
        {
            var actingCharacter = _owner._battleActive ? _owner._battleActingCharacter : null;
            var portrait = actingCharacter is not null
                ? AsciiPortraits.ForCharacterClass(actingCharacter.CharacterClass.Id)
                : _owner._battleActive && _owner._battleEnemy is not null
                    ? AsciiPortraits.ForEnemy(_owner._battleEnemy.Definition.Id)
                    : AsciiPortraits.ForCharacterClass(_displayedCharacter?.CharacterClass.Id ?? "");
            var color = actingCharacter is not null ? actingCharacter.Color
                : _owner._battleActive && _owner._battleEnemy is not null
                    ? _owner._battleEnemy.Definition.StrengthTier switch
                    {
                        1 => ConsoleColor.Green,
                        2 => ConsoleColor.Yellow,
                        3 => ConsoleColor.DarkYellow,
                        4 => ConsoleColor.Red,
                        _ => ConsoleColor.Magenta
                    }
                    : _displayedCharacter?.Color ?? ConsoleColor.Cyan;
            var background = actingCharacter is null && _owner._battleEnemy is { } enemy &&
                             _owner.IsStaggeredBattleEnemy(enemy)
                ? StaggerBackgroundColor
                : ConsoleColor.Black;
            var style = WindowFrameConfiguration.For(FramedWindow.CreaturePortrait);
            var rightSheetWidth = RightSheetWidthForWindow();
            var pictureState = new PicturePanelState(string.Join('\n', portrait.Lines), color, background,
                rightSheetWidth, style);
            if (_lastPicturePanelState == pictureState) return;
            WriteSheetLine(PicturePanelTop, WindowFrameCatalog.Horizontal(style, rightSheetWidth), ConsoleColor.DarkCyan);
            for (var index = 0; index < PicturePanelHeight; index++)
            {
                var line = index < portrait.Lines.Count ? portrait.Lines[index] : string.Empty;
                var sides = WindowFrameCatalog.Sides(style, index, PicturePanelHeight);
                var interiorWidth = rightSheetWidth - sides.Left.Length - sides.Right.Length;
                WriteSheetLine(PicturePanelTop + index + FirstMessageLineOffset,
                    sides.Left + CenterPanelText(line, portrait.CanvasWidth, interiorWidth) + sides.Right,
                    color, background);
            }
            WriteSheetLine(PicturePanelBottom, WindowFrameCatalog.Horizontal(style, rightSheetWidth, bottom: true),
                ConsoleColor.DarkCyan);
            _lastPicturePanelState = pictureState;
        }

        /// <summary>
        /// Writes a single colored line to the right panel with default black background.
        /// Use this for lightweight row output when only foreground color varies.
        /// </summary>
        /// <param name="y">The zero-based console row.</param>
        /// <param name="text">The text, clipped and padded to the current right-panel display width.</param>
        /// <param name="foregroundColor">The text color.</param>
        public void WriteSheetLine(int y, string text, ConsoleColor foregroundColor) =>
            WriteSheetLine(y, text, foregroundColor, ConsoleColor.Black);

        /// <summary>
        /// Clears cached line and status snapshots used for incremental redraws.
        /// Use this before a forced full redraw to avoid stale diffing data.
        /// </summary>
        private void InvalidateCharacterSheetCache()
        {
            _lastCharacterSheetCharacterId = null;
            _lastCharacterSheetLines.Clear();
            _lastInventoryRows.Clear();
            _lastCharacterVitalsOverlay = null;
            _lastPartyStatusRows.Clear();
            _lastPicturePanelState = null;
        }

        /// <summary>Invalidates all row and portrait snapshots before the owner rebuilds the console surface.</summary>
        internal void InvalidateForSurfaceRebuild() => InvalidateCharacterSheetCache();

        /// <summary>
        /// Compares two panel lines to decide whether redraw is needed.
        /// Use this from incremental rendering paths to skip unchanged rows.
        /// </summary>
        private static bool SheetLineEquals(CharacterSheetPanelLine a, CharacterSheetPanelLine b)
        {
            return a.Row == b.Row &&
                   a.Text == b.Text &&
                   a.Color == b.Color &&
                   a.Background == b.Background &&
                   a.ColoredSuffix == b.ColoredSuffix &&
                   a.ColoredSuffixColor == b.ColoredSuffixColor &&
                   a.ExtendsToDivider == b.ExtendsToDivider &&
                   a.ColoredTextStart == b.ColoredTextStart &&
                   a.ColoredTextColor == b.ColoredTextColor &&
                   a.InventorySlot == b.InventorySlot &&
                   SegmentsEqual(a.Segments, b.Segments);
        }

        /// <summary>
        /// Compares optional text segment lists for content equality.
        /// Use this helper when line rendering supports segmented colored text.
        /// </summary>
        private static bool SegmentsEqual(IReadOnlyList<TextSegment>? a, IReadOnlyList<TextSegment>? b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (a is null || b is null || a.Count != b.Count)
                return false;

            return a.SequenceEqual(b);
        }

        /// <summary>
        /// Writes a panel line model, including suffix and mixed-color variants.
        /// Use this for non-selectable rows generated by character sheet panel builders.
        /// </summary>
        private void WriteCharacterSheetPanelLine(CharacterSheetPanelLine line)
        {
            SetColors(ConsoleColor.DarkCyan, ConsoleColor.Black);
            WriteAt(RightBorderX, line.Row, "│ ");

            if (line.Segments is { } segments)
            {
                WriteSheetLine(line.Row, string.Empty, line.Color, line.Background);
                var offset = 0;
                var width = RightSheetWidthForWindow();
                foreach (var segment in segments)
                {
                    var text = BattleCommandPanel.TruncateToDisplayWidth(segment.Text, width - offset);
                    SetColors(segment.Color ?? line.Color, line.Background);
                    WriteAt(RightSheetX + offset, line.Row, text);
                    offset += BattleCommandPanel.DisplayWidth(text);
                    if (offset >= width) break;
                }
            }
            else if (line.ColoredTextStart >= 0)
            {
                WriteSheetLineWithColoredTail(
                    line.Row,
                    line.Text,
                    line.ColoredTextStart,
                    line.Color,
                    line.ColoredTextColor,
                    line.Background);
            }
            else if (!string.IsNullOrEmpty(line.ColoredSuffix))
            {
                WriteSheetLine(
                    line.Row,
                    line.Text,
                    line.Color,
                    line.Background,
                    line.ColoredSuffix,
                    line.ColoredSuffixColor);
            }
            else
            {
                WriteSheetLine(line.Row, line.Text, line.Color, line.Background);
            }
        }

        /// <summary>
        /// Writes a line that can extend beyond the standard right panel width.
        /// Use this for battle details lines that need divider-to-divider rendering.
        /// </summary>
        private void WriteExtendedCharacterSheetLine(CharacterSheetPanelLine line)
        {
            var x = RightSheetX - 2;
            var remaining = RightSheetExtendedWidthForWindow();
            SetColors(line.Color, line.Background);
            Console.SetCursorPosition(x, line.Row);
            foreach (var segment in line.Segments ?? [new TextSegment(line.Text, line.Color)])
            {
                if (remaining <= 0) break;
                var text = segment.Text[..Math.Min(segment.Text.Length, remaining)];
                SetColors(segment.Color ?? line.Color, line.Background);
                Console.Write(text);
                remaining -= text.Length;
            }
            if (remaining > 0) Console.Write(new string(' ', remaining));
        }

        /// <summary>
        /// Draws battle status icons and resource line for the selected character.
        /// Use this after combat effects, buffs, debuffs, HP, or mana changes.
        /// </summary>
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
                    ActiveSpellEffectType.FireResistance => "🔥🛡️",
                    ActiveSpellEffectType.AcidResistance => "🧪🛡️",
                    ActiveSpellEffectType.NecroticResistance => "💀🛡️",
                    ActiveSpellEffectType.MagicResistance => "🔮🛡️",
                    ActiveSpellEffectType.WeaponDamageType when string.Equals(effect.Parameter, "Fire", StringComparison.OrdinalIgnoreCase) => "🔥⚔️",
                    ActiveSpellEffectType.WeaponDamageType => "☠️⚔️",
                    ActiveSpellEffectType.VisionBonus when effect.Value < 0 => "🌑",
                    ActiveSpellEffectType.VisionBonus => "🔆",
                    _ => "✨"
                }))
                .Concat(_owner.CombatStatusIconsFor(character))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            WriteSheetLine(CharacterSheetStatusLine, statusIcons.Count == 0
                    ? "Áll: nincs"
                    : $"Áll: {string.Join(' ', statusIcons)}",
                statusIcons.Count > 0 ? ConsoleColor.Magenta : ConsoleColor.DarkGray);
            DrawCharacterResourceLine(CharacterSheetVitalityLine, CharacterSheetPanel.BuildResourceLine(character));
        }

        /// <summary>
        /// Draws the two character-sheet header rows with focus-aware markers and background.
        /// Use this when displayed character or focus state changes.
        /// </summary>
        private void DrawCharacterSheetHeaders(LiveCharacter character)
        {
            var width = RightSheetWidthForWindow();
            var worldHeader = CharacterSheetPanel.WithFocusMarker(
                CharacterSheetPanel.BuildWorldHeaderLine(_owner._mazeLevel, _owner._goldenKeyCount,
                    MonsterIds.Bosses.Count, _owner._explorationClockIndicator, Math.Max(1, width - 1)),
                _characterSheetFocused, width);
            // Az emojik terminálfüggő cellaszélessége nem befolyásolhatja a háttér jobb szélét.
            WriteSheetLine(worldHeader.Row, string.Empty, worldHeader.Color, worldHeader.Background);
            SetColors(worldHeader.Color, worldHeader.Background);
            WriteAt(RightSheetX, worldHeader.Row,
                BattleCommandPanel.TruncateToDisplayWidth(worldHeader.Text, width));
            _lastCharacterSheetLines[worldHeader.Row] = worldHeader;

            var background = _characterSheetFocused ? ConsoleColor.DarkCyan : ConsoleColor.Black;
            var marker = _characterSheetFocused ? "»" : "«";
            WriteSheetLine(CharacterSheetHeaderLine, string.Empty, ConsoleColor.Yellow, background);
            var title = marker + "KARAKTERLAP";
            SetColors(ConsoleColor.Yellow, background);
            WriteAt(RightSheetX, CharacterSheetHeaderLine, title);
            var titleWidth = BattleCommandPanel.DisplayWidth(title);
            SetColors(character.Color, background);
            WriteAt(RightSheetX + titleWidth, CharacterSheetHeaderLine,
                BattleCommandPanel.TruncateToDisplayWidth(" - " + character.Name, width - titleWidth));
        }

        /// <summary>Updates only the exploration clock segment in the world header without redrawing the rest of the row.</summary>
        /// <remarks>Does nothing when a detail page is open, no character is displayed, or the indicator is absent.</remarks>
        public void RefreshExplorationClockLine()
        {
            if (_owner._spellInfoCharacter is not null || _itemInspectionPanel is not null ||
                _displayedCharacter is null) return;
            var width = RightSheetWidthForWindow();
            var line = CharacterSheetPanel.WithFocusMarker(
                CharacterSheetPanel.BuildWorldHeaderLine(_owner._mazeLevel,
                    _owner._goldenKeyCount, MonsterIds.Bosses.Count, _owner._explorationClockIndicator,
                    Math.Max(1, width - 1)), _characterSheetFocused, width);
            var indicatorStart = line.Text.LastIndexOf(_owner._explorationClockIndicator,
                StringComparison.Ordinal);
            if (indicatorStart < 0) return;

            // Az óra saját, fix szélességű helyét frissítjük; a fejléc többi része nem villan újra.
            var prefixWidth = BattleCommandPanel.DisplayWidth(line.Text[..indicatorStart]);
            const int clockSegmentWidth = 4;
            var indicatorWidth = BattleCommandPanel.DisplayWidth(_owner._explorationClockIndicator);
            // Más képernyőrészek közvetlenül is állítanak konzolszínt, ezért az időjelző
            // részleges frissítésénél nem hagyatkozhatunk kizárólag a színcache-re.
            Console.ForegroundColor = line.Color;
            Console.BackgroundColor = line.Background;
            _owner._currentForegroundColor = line.Color;
            _owner._currentBackgroundColor = line.Background;
            Console.SetCursorPosition(RightSheetX + prefixWidth, line.Row);
            Console.Write(_owner._explorationClockIndicator);
            if (indicatorWidth < clockSegmentWidth)
                Console.Write(new string(' ', clockSegmentWidth - indicatorWidth));
            _lastCharacterSheetLines[line.Row] = line;
        }

        /// <summary>
        /// Draws selectable inventory rows and party rows with selection highlighting.
        /// Use this after selection movement or inventory mutations.
        /// </summary>
        private void DrawSelectableCharacterSheetRows(LiveCharacter character)
        {
            var entries = BuildSheetSelections(character);
            if (_activeSheetSelection is null || entries.All(entry => entry.Key != _activeSheetSelection))
                _activeSheetSelection = entries.FirstOrDefault()?.Key;
            var panelLines = CharacterSheetPanel.Build(character, _owner._gameData.ExperienceByLevel, _owner._mazeLevel,
                _owner._goldenKeyCount, MonsterIds.Bosses.Count, character == _party.Leader, IsTemporaryFollower(character),
                RightSheetWidthForWindow(), explorationClockIndicator: _owner._explorationClockIndicator);
            DrawInventorySlotRows(character, panelLines);
            DrawPartyStatusRows(character);
        }

        /// <summary>
        /// Draws inventory-slot lines and applies selection highlight backgrounds.
        /// Use this when inventory rows need redraw without full sheet render.
        /// </summary>
        private void DrawInventorySlotRows(LiveCharacter character, IEnumerable<CharacterSheetPanelLine> panelLines)
        {
            foreach (var line in panelLines.Where(line => line.InventorySlot is not null))
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
                var background = SelectionBackground(new SheetSelectionKey(kind, slot.Index));
                var state = new InventoryRowState(line, background);
                if (_lastInventoryRows.TryGetValue(line.Row, out var previous) &&
                    previous.Background == state.Background && SheetLineEquals(previous.Line, state.Line))
                    continue;
                if (line.Segments is { } segments)
            {
                WriteSheetLine(line.Row, string.Empty, line.Color, line.Background);
                var offset = 0;
                var width = RightSheetWidthForWindow();
                foreach (var segment in segments)
                {
                    var text = BattleCommandPanel.TruncateToDisplayWidth(segment.Text, width - offset);
                    SetColors(segment.Color ?? line.Color, line.Background);
                    WriteAt(RightSheetX + offset, line.Row, text);
                    offset += BattleCommandPanel.DisplayWidth(text);
                    if (offset >= width) break;
                }
            }
            else if (line.ColoredTextStart >= 0)
                    WriteSheetLineWithColoredTail(line.Row, line.Text, line.ColoredTextStart,
                        line.Color, line.ColoredTextColor, background);
                else
                    WriteSheetLine(line.Row, line.Text, line.Color, background);
                _lastInventoryRows[line.Row] = state;
            }
        }

        /// <summary>
        /// Draws the compact party member status section.
        /// Use this when displayed member, HP, mana, or roster content changes.
        /// </summary>
        private void DrawPartyStatusRows(LiveCharacter displayedCharacter)
        {
            var partyMembers = _party.Members.Take(CharacterSheetPartyMemberRows).ToList();
            WriteSheetLine(PartyFormationDisplay.PartyHeadingRow, $"Parti: {partyMembers.Count}/{_party.Capacity}",
                ConsoleColor.Cyan, ConsoleColor.Black);

            for (var index = 0; index < CharacterSheetPartyMemberRows; index++)
            {
                var row = CharacterSheetPartyMembersStartLine + index;

                if (index >= partyMembers.Count)
                {
                    var emptyState = new PartyStatusRowState(
                        Status: new PartyStatusLine(PartyFormationDisplay.EmptySlotText(index, _party.Capacity), ConsoleColor.DarkGray,
                            string.Empty, ConsoleColor.Gray, string.Empty, ConsoleColor.Gray),
                        Background: ConsoleColor.Black);

                    if (_lastPartyStatusRows.TryGetValue(row, out var previous) && previous == emptyState)
                        continue;

                    DrawPartyStatusLine(row, emptyState.Status!, ConsoleColor.Black);
                    _lastPartyStatusRows[row] = emptyState;
                    continue;
                }

                var member = partyMembers[index];

                var status = CharacterSheetPanel.BuildPartyStatus(
                    member,
                    member == displayedCharacter,
                    member == _party.Leader,
                    RightSheetWidthForWindow());

                var background = SelectionBackground(new(SheetSelectionKind.PartyMember, index));
                var state = new PartyStatusRowState(status, background);

                if (_lastPartyStatusRows.TryGetValue(row, out var previousState) && previousState == state)
                    continue;

                DrawPartyStatusLine(row, status, background);
                _lastPartyStatusRows[row] = state;
            }
        }

        /// <summary>
        /// Draws one party status row with identity and resource coloring.
        /// Use this only from party row render paths where row background is already resolved.
        /// </summary>
        private void DrawPartyStatusLine(int y, PartyStatusLine status, ConsoleColor background)
        {
            FillPartyStatusRowBackground(y, background);
            var x = RightSheetX - 1;
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

        /// <summary>
        /// Fills a party-status row across the extended right panel width.
        /// Use this to keep party rows aligned with the battle details panel width and left offset.
        /// </summary>
        private void FillPartyStatusRowBackground(int y, ConsoleColor background)
        {
            var startX = RightSheetX - 1;
            var width = RightSheetExtendedWidthForWindow();
            SetColors(ConsoleColor.Gray, background);
            WriteAt(startX, y, new string(' ', width));
        }

        /// <summary>
        /// Builds navigation entries for the current character sheet.
        /// Includes inventory slots followed by up to six party rows; temporary followers have no selectable entries.
        /// </summary>
        private List<SheetSelectionEntry> BuildSheetSelections(LiveCharacter character)
        {
            var entries = new List<SheetSelectionEntry>();
            if (IsTemporaryFollower(character)) return entries;
            for (var index = 0; index < character.WeaponSlots.Count; index++)
                entries.Add(new(new(SheetSelectionKind.Weapon, index)));
            entries.Add(new(new(SheetSelectionKind.Armor, 0)));
            for (var index = 0; index < character.MagicItems.Count; index++)
                entries.Add(new(new(SheetSelectionKind.MagicItem, index)));
            for (var index = 0; index < character.Backpack.Count; index++)
                entries.Add(new(new(SheetSelectionKind.Backpack, index)));
            var partyMemberCount = Math.Min(CharacterSheetPartyMemberRows, _party.Members.Count);
            for (var index = 0; index < partyMemberCount; index++)
                entries.Add(new(new(SheetSelectionKind.PartyMember, index)));
            return entries;
        }

        /// <summary>
        /// Returns all characters that can be cycled on the sheet.
        /// Use this for left/right display navigation.
        /// </summary>
        private List<LiveCharacter> SheetCharacters() =>
            _party.Members.Concat(_owner._temporaryFollowers()).Distinct().ToList();

        /// <summary>
        /// Determines whether a character is shown as temporary follower instead of party member.
        /// Use this to alter selectable slots and panel rendering behavior.
        /// </summary>
        private bool IsTemporaryFollower(LiveCharacter character) =>
            !_party.Members.Contains(character) && _owner._temporaryFollowers().Contains(character);

        /// <summary>
        /// Returns highlight background for a selectable key.
        /// Use this during row rendering to keep current selection visually distinct.
        /// </summary>
        private ConsoleColor SelectionBackground(SheetSelectionKey key) =>
            _activeSheetSelection == key ? ConsoleColor.DarkCyan : ConsoleColor.Black;

        /// <summary>
        /// Clears the whole right panel area.
        /// Use this before full redraws to avoid leftover characters from previous content.
        /// </summary>
        private void ClearRightPanel()
        {
            for (var row = 0; row <= PicturePanelBottom; row++)
                WriteSheetLine(row, string.Empty, ConsoleColor.Gray);
        }

        /// <summary>
        /// Centers portrait text inside the panel interior width.
        /// Use this when writing monospaced portrait lines with fixed canvas width.
        /// </summary>
        private static string CenterPanelText(string text, int canvasWidth, int interiorWidth = PortraitInteriorWidth)
        {
            var canvas = text.PadRight(canvasWidth);
            var leftPadding = Math.Max(0, (interiorWidth - canvasWidth) / FrameBorderWidth);
            return (new string(' ', leftPadding) + canvas).PadRight(interiorWidth);
        }

        /// <summary>
        /// Writes one right-panel line with explicit foreground and background.
        /// Use this for any row where full-width clipping and padding must be enforced.
        /// </summary>
        private void WriteSheetLine(int y, string text, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
        {
            var rightSheetWidth = RightSheetWidthForWindow();
            SetColors(foregroundColor, backgroundColor);
            WriteAt(RightSheetX, y, BattleCommandPanel.FitToDisplayWidth(text, rightSheetWidth));
        }

        /// <summary>
        /// Writes a line where a suffix region uses a second color.
        /// Use this for rows that need mixed coloring without splitting into multiple write calls externally.
        /// </summary>
        private void WriteSheetLineWithColoredTail(
            int y,
            string text,
            int coloredTextStart,
            ConsoleColor color,
            ConsoleColor coloredTextColor,
            ConsoleColor background)
        {
            var split = Math.Clamp(coloredTextStart, 0, text.Length);
            var firstPart = text[..split];
            var coloredPart = text[split..];

            WriteSheetLine(y, string.Empty, color, background);
            var first = BattleCommandPanel.TruncateToDisplayWidth(firstPart, RightSheetWidthForWindow());
            var remaining = Math.Max(0, RightSheetWidthForWindow() - BattleCommandPanel.DisplayWidth(first));
            var colored = BattleCommandPanel.TruncateToDisplayWidth(coloredPart, remaining);
            Console.SetCursorPosition(RightSheetX, y);

            SetColors(color, background);
            Console.Write(first);

            SetColors(coloredTextColor, background);
            Console.Write(colored);
        }

        /// <summary>
        /// Writes a two-part line where left and right text blocks use different colors.
        /// Use this for header-style rows that show two semantic segments on one line.
        /// </summary>
        private void WriteSheetLine(
            int y,
            string leftText,
            ConsoleColor leftColor,
            ConsoleColor leftColorBg,
            string rightText,
            ConsoleColor rightColor)
        {
            var rightSheetWidth = RightSheetWidthForWindow();
            var leftClipped = BattleCommandPanel.TruncateToDisplayWidth(leftText, rightSheetWidth);
            var leftWidth = BattleCommandPanel.DisplayWidth(leftClipped);
            var rightClipped = BattleCommandPanel.TruncateToDisplayWidth(rightText, rightSheetWidth - leftWidth);

            SetColors(leftColor, leftColorBg);
            WriteAt(RightSheetX, y, leftClipped);

            SetColors(rightColor, leftColorBg);
            var secondX = RightSheetX + leftWidth;
            var remainingWidth = rightSheetWidth - leftWidth;
            var rightPadded = BattleCommandPanel.FitToDisplayWidth(rightClipped, remainingWidth);
            WriteAt(secondX, y, rightPadded);
        }

        /// <summary>
        /// Sets console colors with owner-level caching to reduce redundant writes.
        /// Use this before direct console writes in this renderer.
        /// </summary>
        private void SetColors(ConsoleColor foregroundColor, ConsoleColor backgroundColor)
        {
            if (_owner._currentForegroundColor != foregroundColor)
            {
                Console.ForegroundColor = foregroundColor;
                _owner._currentForegroundColor = foregroundColor;
            }

            if (_owner._currentBackgroundColor != backgroundColor)
            {
                Console.BackgroundColor = backgroundColor;
                _owner._currentBackgroundColor = backgroundColor;
            }
        }

        /// <summary>
        /// Draws vitality and mana tokens in one resource row.
        /// Use this for both normal and battle refresh paths.
        /// </summary>
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
    }
}
