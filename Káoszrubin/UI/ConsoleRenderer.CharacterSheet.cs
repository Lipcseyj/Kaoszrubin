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
    public sealed class CharacterSheetRenderer
    {
        private const int CharacterSheetHeaderLine = 1;
        private const int CharacterSheetStatusLine = 8;
        private const int CharacterSheetVitalityLine = 5;
        private const int CharacterSheetGoldLine = 9;
        private const int CharacterSheetWeaponsHeadingLine = 17;
        private const int CharacterSheetMagicItemsHeadingLine = 22;
        private const int CharacterSheetBackpackHeadingLine = 26;
        private const int CharacterSheetPartyMembersStartLine = 41;
        private const int CharacterSheetPartyMemberRows = 4;
        private const int CharacterSheetReservedMessageLine = 39;
        public const int CharacterSheetControlsLine = 40;

        private enum SheetSelectionKind { Weapon, Armor, MagicItem, Backpack, PartyMember }
        private readonly record struct SheetSelectionKey(SheetSelectionKind Kind, int Index);
        private sealed record SheetSelectionEntry(SheetSelectionKey Key);
        private readonly record struct PartyStatusRowState(PartyStatusLine? Status, ConsoleColor Background);

        private readonly ConsoleRenderer _owner;
        private readonly Party _party;

        private bool _characterSheetFocused;
        private int _selectedSpellInfoIndex;
        private IReadOnlyList<CharacterSheetPanelLine>? _itemInspectionPanel;
        private SheetSelectionKey? _activeSheetSelection;
        private readonly Dictionary<LiveCharacter, SheetSelectionKey> _lastSheetSelections = [];

        private CharacterId? _lastCharacterSheetCharacterId;
        private readonly Dictionary<int, CharacterSheetPanelLine> _lastCharacterSheetLines = [];
        private CharacterResourceLine? _lastCharacterVitalsOverlay;
        private readonly Dictionary<int, PartyStatusRowState> _lastPartyStatusRows = [];

        private PartyFormationSnapshot? _formation;
        private LiveCharacter? _displayedCharacter;

        internal CharacterSheetRenderer(ConsoleRenderer owner, Party party)
        {
            _owner = owner;
            _party = party;
        }

        /// <summary>
        /// Gets the character currently displayed on the right panel.
        /// Use this when an external flow needs the active character-sheet context.
        /// </summary>
        public LiveCharacter DisplayedCharacter =>
            _displayedCharacter ?? _party.Leader ?? throw new InvalidOperationException("Nincs megjeleníthető karakter.");

        /// <summary>
        /// Gets whether the spell info page is currently open.
        /// Use this to gate input handling while spell detail mode is active.
        /// </summary>
        public bool IsSpellInfoPageOpen => _owner._spellInfoCharacter is not null;

        /// <summary>
        /// Gets whether an item inspection page is currently open.
        /// Use this to restrict actions to inspection-specific keys while the page is visible.
        /// </summary>
        public bool IsItemInspectionPageOpen => _itemInspectionPanel is not null;

        /// <summary>
        /// Gets the character whose spells are currently shown on the spell info page.
        /// Use this when applying quick-slot or cast actions from spell info selection.
        /// </summary>
        public LiveCharacter? SpellInfoCharacter => _owner._spellInfoCharacter;

        /// <summary>
        /// Refreshes the right panel for the current display context.
        /// Use this when underlying data changed and the currently visible character sheet should be redrawn.
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
            _owner.DrawFrame(5);
            if (_displayedCharacter is null || !SheetCharacters().Contains(_displayedCharacter))
                _displayedCharacter = character;
            DrawCharacterSheet(_displayedCharacter);
            SetCharacterSheetFocused(true);
            _owner.DrawInnMessage("Fogadói karakterlap — Tab: vissza a fogadóba | ↑/↓: választás | ←/→: karakter");
        }

        /// <summary>
        /// Sets focus visual state for character sheet controls.
        /// Use this when toggling between movement/input mode and character-sheet input mode.
        /// </summary>
        public void SetCharacterSheetFocused(bool focused)
        {
            _characterSheetFocused = focused;
            if (_displayedCharacter is null) return;
            DrawCharacterSheetHeader(_displayedCharacter);
            DrawSelectableCharacterSheetRows(_displayedCharacter);
        }

        /// <summary>
        /// Moves the current character-sheet selection up or down.
        /// Use positive values to move forward and negative values to move backward.
        /// </summary>
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
        public void DrawSpellInfoPage(LiveCharacter character, int selectedIndex)
        {
            _itemInspectionPanel = null;
            var spells = character.KnownSpells.OrderBy(spell => spell.Level).ThenBy(spell => spell.Name).ToList();
            selectedIndex = spells.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, spells.Count - 1);
            _owner._spellInfoCharacter = character;
            _selectedSpellInfoIndex = selectedIndex;
            var info = SpellInfoSnapshotProjector.Create(character);
            var panelLines = SpellInfoPanel.Build(character.Name, character.CharacterClass.Id, character.Level,
                info, selectedIndex, _characterSheetFocused, RightSheetWidthForWindow()).ToDictionary(line => line.Row);
            for (var row = 0; row <= PicturePanelBottom; row++)
                if (panelLines.TryGetValue(row, out var line))
                    WriteSheetLine(line.Row, line.Text, line.Color, line.Background);
                else
                    WriteSheetLine(row, string.Empty, ConsoleColor.Gray);
        }

        /// <summary>
        /// Gets the currently selected spell from the spell info page.
        /// Use this to map Enter/F-key actions to the selected spell.
        /// </summary>
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
            DrawCharacterSheet(character);
        }

        /// <summary>
        /// Draws an item inspection panel on the right side.
        /// Use this when inspecting an identified or unidentified inventory item.
        /// </summary>
        public void DrawItemInspectionPage(IReadOnlyList<CharacterSheetPanelLine> lines)
        {
            _owner._spellInfoCharacter = null;
            _itemInspectionPanel = lines;
            var panelLines = lines.ToDictionary(line => line.Row);
            for (var row = 0; row <= PicturePanelBottom; row++)
                if (panelLines.TryGetValue(row, out var line))
                    WriteSheetLine(line.Row, line.Text, line.Color, line.Background);
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
                DrawCharacterSheet(_displayedCharacter);
        }

        /// <summary>
        /// Returns the inventory slot currently selected on the character sheet.
        /// Use this before drop/use/move/split actions to resolve the target slot.
        /// </summary>
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
        public void SetFormationStatus(PartyFormationSnapshot formation)
        {
            _formation = formation;
            if (_displayedCharacter is null) return;
            if (_owner._spellInfoCharacter is not null)
            {
                RefreshSpellInfoPage();
                return;
            }
            WriteSheetLine(CharacterSheetControlsLine, FormationStatusText(formation),
                formation.State == PartyFormationState.Locked ? ConsoleColor.Green : ConsoleColor.DarkCyan);
        }

        /// <summary>
        /// Formats formation state into the controls-row text.
        /// Use this helper when any UI needs a consistent formation label.
        /// </summary>
        public static string FormationStatusText(PartyFormationSnapshot formation)
        {
            var arrow = formation.Facing switch
            {
                Direction.Up => "↑",
                Direction.Right => "→",
                Direction.Down => "↓",
                _ => "←"
            };
            var state = formation.State switch
            {
                PartyFormationState.Assembling => "összeáll",
                PartyFormationState.Locked when formation.Layout == PartyFormationLayout.SingleFile => "zárt · libasor",
                PartyFormationState.Locked => "zárt · 2×2",
                _ => "feloszlatva"
            };
            return $"ALAKZAT {arrow}  {state}";
        }

        /// <summary>
        /// Draws detailed battle action text on the right panel.
        /// Use this in non-quick battles when new action details become available.
        /// </summary>
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
        /// Use this as the primary render entry point for character sheet state changes.
        /// </summary>
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
                DrawCharacterSheetHeader(character);

            var resourceLine = CharacterSheetPanel.BuildResourceLine(character);

            if (fullRedraw || _lastCharacterVitalsOverlay != resourceLine)
            {
                DrawCharacterResourceLine(CharacterSheetVitalityLine, resourceLine);
                _lastCharacterVitalsOverlay = resourceLine;
            }

            foreach (var line in panelLines.Where(line =>
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

            WriteSheetLine(CharacterSheetReservedMessageLine, string.Empty, ConsoleColor.DarkGray);

            WriteSheetLine(
                CharacterSheetControlsLine,
                _formation is null ? string.Empty : FormationStatusText(_formation),
                _formation?.State == PartyFormationState.Locked
                    ? ConsoleColor.Green
                    : ConsoleColor.DarkCyan);

            DrawPicturePanel();
        }

        /// <summary>
        /// Draws the portrait panel next to the message log.
        /// Use this after character/enemy context changes that affect portrait or portrait color.
        /// </summary>
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
        }

        /// <summary>
        /// Writes a single colored line to the right panel with default black background.
        /// Use this for lightweight row output when only foreground color varies.
        /// </summary>
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
            _lastCharacterVitalsOverlay = null;
            _lastPartyStatusRows.Clear();
        }

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

            if (line.ColoredTextStart >= 0)
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
        /// Draws the character-sheet header with focus-aware background.
        /// Use this when displayed character or focus state changes.
        /// </summary>
        private void DrawCharacterSheetHeader(LiveCharacter character) => WriteSheetLine(
            CharacterSheetHeaderLine,
            "KARAKTERLAP",
            ConsoleColor.Yellow,
            _characterSheetFocused ? ConsoleColor.DarkGreen : ConsoleColor.Black,
            " - " + character.Name,
            character.Color);

        public void RefreshExplorationClockLine()
        {
            if (_owner._spellInfoCharacter is not null || _itemInspectionPanel is not null ||
                _displayedCharacter is null) return;
            var line = CharacterSheetPanel.BuildWorldHeaderLine(_owner._mazeLevel,
                _owner._goldenKeyCount, MonsterIds.Bosses.Count, _owner._explorationClockIndicator,
                RightSheetWidthForWindow());
            var indicatorStart = line.Text.LastIndexOf(_owner._explorationClockIndicator,
                StringComparison.Ordinal);
            if (indicatorStart < 0) return;

            // Az óra saját, fix szélességű helyét frissítjük; a fejléc többi része nem villan újra.
            var prefixWidth = BattleCommandPanel.DisplayWidth(line.Text[..indicatorStart]);
            const int clockSegmentWidth = 4;
            var indicatorWidth = BattleCommandPanel.DisplayWidth(_owner._explorationClockIndicator);
            SetColors(line.Color, line.Background);
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
                if (line.ColoredTextStart >= 0)
                    WriteSheetLineWithColoredTail(line.Row, line.Text, line.ColoredTextStart,
                        line.Color, line.ColoredTextColor, background);
                else
                    WriteSheetLine(line.Row, line.Text, line.Color, background);
            }
        }

        /// <summary>
        /// Draws the compact party member status section.
        /// Use this when displayed member, HP, mana, or roster content changes.
        /// </summary>
        private void DrawPartyStatusRows(LiveCharacter displayedCharacter)
        {
            var partyMembers = _party.Members.Take(CharacterSheetPartyMemberRows).ToList();

            for (var index = 0; index < CharacterSheetPartyMemberRows; index++)
            {
                var row = CharacterSheetPartyMembersStartLine + index;

                if (index >= partyMembers.Count)
                {
                    var emptyState = new PartyStatusRowState(
                        Status: null,
                        Background: ConsoleColor.Black);

                    if (_lastPartyStatusRows.TryGetValue(row, out var previous) && previous == emptyState)
                        continue;

                    FillPartyStatusRowBackground(row, ConsoleColor.Black);
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
        /// Use this to map keyboard movement to selectable rows.
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
            var clippedText = text.Length <= rightSheetWidth ? text : text[..rightSheetWidth];
            SetColors(foregroundColor, backgroundColor);
            WriteAt(RightSheetX, y, clippedText.PadRight(rightSheetWidth));
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
            Console.SetCursorPosition(RightSheetX, y);

            SetColors(color, background);
            Console.Write(firstPart);

            SetColors(coloredTextColor, background);
            Console.Write(coloredPart);
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
            var leftMax = rightSheetWidth / FrameBorderWidth;
            var rightMax = rightSheetWidth - leftMax;

            string leftClipped;
            string rightClipped;

            if (leftText.Length <= leftMax)
            {
                leftClipped = leftText;
                var remaining = rightSheetWidth - leftClipped.Length;
                rightClipped = rightText.Length <= remaining ? rightText : rightText[..remaining];
            }
            else if (rightText.Length <= rightMax)
            {
                rightClipped = rightText;
                var remaining = rightSheetWidth - rightClipped.Length;
                leftClipped = leftText.Length <= remaining ? leftText : leftText[..remaining];
            }
            else
            {
                leftClipped = leftText[..leftMax];
                rightClipped = rightText.Length <= rightMax ? rightText : rightText[..rightMax];
            }

            SetColors(leftColor, leftColorBg);
            var leftPadded = leftClipped.PadRight(leftClipped.Length);
            WriteAt(RightSheetX, y, leftPadded);

            SetColors(rightColor, ConsoleColor.Black);
            var secondX = RightSheetX + leftPadded.Length;
            var remainingWidth = rightSheetWidth - leftPadded.Length;
            var rightPadded = rightClipped.PadRight(remainingWidth);
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
