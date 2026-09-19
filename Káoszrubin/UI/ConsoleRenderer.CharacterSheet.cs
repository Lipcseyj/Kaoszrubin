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
        private const int CharacterSheetGoldLine = 9;
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
        public const int CharacterSheetControlsLine = 40;

        private readonly ConsoleRenderer _owner;

        private bool _characterSheetFocused;
        private int _selectedSpellInfoIndex;
        private IReadOnlyList<CharacterSheetPanelLine>? _itemInspectionPanel;

        private SheetSelectionKey? _activeSheetSelection;
        private readonly Dictionary<LiveCharacter, SheetSelectionKey> _lastSheetSelections = [];

        private CharacterId? _lastCharacterSheetCharacterId;
        private readonly Dictionary<int, CharacterSheetPanelLine> _lastCharacterSheetLines = [];
        private CharacterResourceLine? _lastCharacterVitalsOverlay;

        private readonly Dictionary<int, PartyStatusRowState> _lastPartyStatusRows = [];
        private readonly record struct PartyStatusRowState(
            PartyStatusLine? Status,
            ConsoleColor Background);
        private PartyFormationSnapshot? _formation;
        private readonly Party _party;
        private LiveCharacter? _displayedCharacter;
        public LiveCharacter DisplayedCharacter => _displayedCharacter ?? _party.Leader
            ?? throw new InvalidOperationException("Nincs megjeleníthető karakter.");

        internal CharacterSheetRenderer(ConsoleRenderer owner, Party party)
        {
            _owner = owner;
            _party = party;
        }

        public void RefreshCharacterSheet()
        {
            var character = _displayedCharacter ?? _party.Members.FirstOrDefault();
            if (character is null) return;

            RefreshCharacterSheet(character);
        }

        /// <summary>Csak a jobb oldali karakterlapot rajzolja újra, a játéktér érintése nélkül.</summary>
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

        public void UpdateGoldInCharacterSheet(LiveCharacter character)
        {
            var goldLine = CharacterSheetPanel.BuildGoldLine(character);
            WriteSheetLine(CharacterSheetGoldLine, goldLine.Text, goldLine.Color, goldLine.Background);
        }

        public void DrawInnCharacterSheet(LiveCharacter character)
        {
            _owner.DrawFrame(5);
            if (_displayedCharacter is null || !SheetCharacters().Contains(_displayedCharacter))
                _displayedCharacter = character;
            DrawCharacterSheet(_displayedCharacter);
            SetCharacterSheetFocused(true);
            _owner.DrawInnMessage("Fogadói karakterlap — Tab: vissza a fogadóba | ↑/↓: választás | ←/→: karakter");
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

        public bool IsSpellInfoPageOpen => _owner._spellInfoCharacter is not null;
        public bool IsItemInspectionPageOpen => _itemInspectionPanel is not null;

        public LiveCharacter? SpellInfoCharacter => _owner._spellInfoCharacter;

        public SpellDefinition? GetSelectedSpellInfo()
        {
            if (_owner._spellInfoCharacter is null) return null;
            var spells = _owner._spellInfoCharacter.KnownSpells.OrderBy(spell => spell.Level).ThenBy(spell => spell.Name).ToList();
            return spells.ElementAtOrDefault(_selectedSpellInfoIndex);
        }

        public void RefreshSpellInfoPage()
        {
            if (_owner._spellInfoCharacter is not null) DrawSpellInfoPage(_owner._spellInfoCharacter, _selectedSpellInfoIndex);
        }

        public void MoveSpellInfoSelection(int direction)
        {
            if (_owner._spellInfoCharacter is null || direction == 0 || _owner._spellInfoCharacter.KnownSpells.Count == 0) return;
            _selectedSpellInfoIndex = (_selectedSpellInfoIndex + direction + _owner._spellInfoCharacter.KnownSpells.Count) %
                                      _owner._spellInfoCharacter.KnownSpells.Count;
            DrawSpellInfoPage(_owner._spellInfoCharacter, _selectedSpellInfoIndex);
        }

        public void CloseSpellInfoPage()
        {
            if (_owner._spellInfoCharacter is null) return;
            var character = _owner._spellInfoCharacter;
            _owner._spellInfoCharacter = null;
            DrawCharacterSheet(character);
        }

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

        public void CloseItemInspectionPage()
        {
            if (_itemInspectionPanel is null) return;
            _itemInspectionPanel = null;
            if (_displayedCharacter is not null)
                DrawCharacterSheet(_displayedCharacter);
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

        /// <summary>Fogadói üzlet után csak a látható arany- és tárgysorokat frissíti.</summary>
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

        /// <summary>Csata közben csak az állapot-, HP- és mannasorokat frissíti.</summary>
        public void RefreshBattleStatusRows()
        {
            if (_displayedCharacter is null) return;
            DrawBattleStatusRows(_displayedCharacter);
            DrawPartyStatusRows(_displayedCharacter);
        }

        private void InvalidateCharacterSheetCache()
        {
            _lastCharacterSheetCharacterId = null;
            _lastCharacterSheetLines.Clear();
            _lastCharacterVitalsOverlay = null;
            _lastPartyStatusRows.Clear();
        }

        public void SetFormationStatus(PartyFormationSnapshot formation)
        {
            _formation = formation;
            if (_displayedCharacter is null) return;
            if (_owner._spellInfoCharacter is not null)
            {
                RefreshSpellInfoPage();
                return;
            }
            WriteSheetLine(CharacterSheetRenderer.CharacterSheetControlsLine, FormationStatusText(formation),
                formation.State == PartyFormationState.Locked ? ConsoleColor.Green : ConsoleColor.DarkCyan);
        }

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

        public void PageBattleDetails(int direction)
        {
            _owner._battleDetailsPage = BattleDetailsPanel.Normalize(_owner._battleDetailsPage + direction,
                BattleDetailsPanel.PageCount(_owner._battleDetails));
            DrawBattleDetails(_owner._battleDetails);
        }

        private static bool SheetLineEquals(
        CharacterSheetPanelLine a,
        CharacterSheetPanelLine b)
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

        private static bool SegmentsEqual(
            IReadOnlyList<TextSegment>? a,
            IReadOnlyList<TextSegment>? b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (a is null || b is null || a.Count != b.Count)
                return false;

            return a.SequenceEqual(b);
        }

        /// <summary>
        /// Teljes karakterlap rajzolása a jobb oldali panelre. Minden sor a WriteSheetLine segítségével kerül oda.
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
                RightSheetWidthForWindow());

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

            WriteSheetLine(
                CharacterSheetReservedMessageLine,
                string.Empty,
                ConsoleColor.DarkGray);

            WriteSheetLine(
                CharacterSheetControlsLine,
                _formation is null ? string.Empty : FormationStatusText(_formation),
                _formation?.State == PartyFormationState.Locked
                    ? ConsoleColor.Green
                    : ConsoleColor.DarkCyan);

            DrawPicturePanel();
        }

        public void WriteCharacterSheetPanelLine(CharacterSheetPanelLine line)
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
                WriteSheetLine(
                    line.Row,
                    line.Text,
                    line.Color,
                    line.Background);
            }
        }

        public void WriteExtendedCharacterSheetLine(CharacterSheetPanelLine line)
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
            _characterSheetFocused ? ConsoleColor.DarkGreen : ConsoleColor.Black,
            " - " + character.Name, character.Color);

        private void DrawSelectableCharacterSheetRows(LiveCharacter character)
        {
            var entries = BuildSheetSelections(character);
            if (_activeSheetSelection is null || entries.All(entry => entry.Key != _activeSheetSelection))
                _activeSheetSelection = entries.FirstOrDefault()?.Key;
            var panelLines = CharacterSheetPanel.Build(character, _owner._gameData.ExperienceByLevel, _owner._mazeLevel,
                _owner._goldenKeyCount, MonsterIds.Bosses.Count, character == _party.Leader, IsTemporaryFollower(character),
                RightSheetWidthForWindow());
            DrawInventorySlotRows(character, panelLines);
            DrawPartyStatusRows(character);
        }

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

        private void DrawPartyStatusRows(LiveCharacter displayedCharacter)
        {
            var partyMembers = _party.Members
                .Take(CharacterSheetPartyMemberRows)
                .ToList();

            for (var index = 0; index < CharacterSheetPartyMemberRows; index++)
            {
                var row = CharacterSheetPartyMembersStartLine + index;

                if (index >= partyMembers.Count)
                {
                    var emptyState = new PartyStatusRowState(
                        Status: null,
                        Background: ConsoleColor.Black);

                    if (_lastPartyStatusRows.TryGetValue(row, out var previous) &&
                        previous == emptyState)
                    {
                        continue;
                    }

                    WriteSheetLine(row, string.Empty, ConsoleColor.DarkGray);
                    _lastPartyStatusRows[row] = emptyState;
                    continue;
                }

                var member = partyMembers[index];

                var status = CharacterSheetPanel.BuildPartyStatus(
                    member,
                    member == displayedCharacter,
                    member == _party.Leader,
                    RightSheetWidthForWindow());

                var background = SelectionBackground(
                    new(SheetSelectionKind.PartyMember, index));

                var state = new PartyStatusRowState(status, background);

                if (_lastPartyStatusRows.TryGetValue(row, out var previousState) &&
                    previousState == state)
                {
                    continue;
                }

                DrawPartyStatusLine(row, status, background);
                _lastPartyStatusRows[row] = state;
            }
        }

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
            .Concat(_owner._temporaryFollowers())
            .Distinct()
            .ToList();

        private bool IsTemporaryFollower(LiveCharacter character) =>
            !_party.Members.Contains(character) && _owner._temporaryFollowers().Contains(character);

        private ConsoleColor SelectionBackground(SheetSelectionKey key) =>
            _activeSheetSelection == key ? ConsoleColor.DarkCyan : ConsoleColor.Black;

        /// <summary>
        /// A jobb oldali kép-panel (ASCII portré) kirajzolása. A PicturePanelTop-ról indul,
        /// és a WriteSheetLine metódussal írja ki a keretet és a képsorokat.
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
            var style = WindowFrameConfiguration.For(FramedWindow.CreaturePortrait);
            var rightSheetWidth = RightSheetWidthForWindow();
            WriteSheetLine(PicturePanelTop, WindowFrameCatalog.Horizontal(style, rightSheetWidth), ConsoleColor.DarkCyan);
            for (var index = 0; index < PicturePanelHeight; index++)
            {
                var line = index < portrait.Lines.Count ? portrait.Lines[index] : string.Empty;
                var sides = WindowFrameCatalog.Sides(style, index, PicturePanelHeight);
                var interiorWidth = rightSheetWidth - sides.Left.Length - sides.Right.Length;
                WriteSheetLine(PicturePanelTop + index + FirstMessageLineOffset,
                    sides.Left + CenterPanelText(line, portrait.CanvasWidth, interiorWidth) + sides.Right, color);
            }
            WriteSheetLine(PicturePanelBottom, WindowFrameCatalog.Horizontal(style, rightSheetWidth, bottom: true),
                ConsoleColor.DarkCyan);
        }

        private void ClearRightPanel()
        {
            for (var row = 0; row <= PicturePanelBottom; row++)
                WriteSheetLine(row, string.Empty, ConsoleColor.Gray);
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
        public void WriteSheetLine(int y, string text, ConsoleColor foregroundColor)
            => WriteSheetLine(y, text, foregroundColor, ConsoleColor.Black);

        public void WriteSheetLine(int y, string text, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
        {
            var rightSheetWidth = RightSheetWidthForWindow();
            var clippedText = text.Length <= rightSheetWidth ? text : text[..rightSheetWidth];
            SetColors(foregroundColor, backgroundColor);
            WriteAt(RightSheetX, y, clippedText.PadRight(rightSheetWidth));
        }

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

            // Sor törlése/padding, ahogy eddig is szükséges
            WriteSheetLine(y, string.Empty, color, background);

            Console.SetCursorPosition(RightSheetX, y);

            SetColors(color, background);
            Console.Write(firstPart);

            // NEM állítjuk újra a cursor X-et!
            SetColors(coloredTextColor, background);
            Console.Write(coloredPart);
        }

        /// <summary>
        /// Két szöveget ír ki egymás mellé a jobb oldali karakterlapra, két külön színnel.
        /// A teljes sor hossza nem haladja meg a maximum 27 karaktert — ha szükséges,
        /// levágja a szövegeket úgy, hogy mindkét rész látható maradjon lehetőleg.
        /// </summary>
        private void WriteSheetLine(int y, string leftText, ConsoleColor leftColor, ConsoleColor leftColorBg, string rightText, ConsoleColor rightColor)
        {
            // Alap felosztás: fele-fele, de dinamikusan kiegészítjük ha az egyik rövidebb
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

            // Kiírás: először a bal oldali rész, majd a jobb oldali közvetlenül utána
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
        /// Színkezelő: csak akkor állítja át Console.ForegroundColor/BackgroundColor értékét,
        /// ha azok eltérnek a cache-elt értékektől, így minimalizálva a felesleges rendszerhívásokat.
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

        /// <summary>Reseteli a konzol színeket és törli a cache-elt színértékeket.</summary>
        private void ResetColorCache()
        {
            Console.ResetColor();
            _owner._currentForegroundColor = null;
            _owner._currentBackgroundColor = null;
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
            var rightSheetWidth = RightSheetWidthForWindow();
            var availablePerName = Math.Max(FirstItemNumber, (rightSheetWidth - prefix.Length - separatorsWidth) / names.Count);
            var shortenedNames = names.Select(name => name.Length <= availablePerName ? name : name[..availablePerName]);
            return prefix + string.Join(", ", shortenedNames);
        }

        private static IReadOnlyList<string> FormatCompactListRows(string label, IEnumerable<string> values, int rowCount)
        {
            var names = values.ToList();
            if (names.Count == 0) return [$"{label}: nincs", .. Enumerable.Repeat(string.Empty, rowCount - FirstItemNumber)];

            var rows = new List<string>(rowCount);
            var namesPerRow = (int)Math.Ceiling(names.Count / (double)rowCount);
            var rightSheetWidth = RightSheetWidthForWindow();
            for (var row = 0; row < rowCount; row++)
            {
                var rowNames = names.Skip(row * namesPerRow).Take(namesPerRow).ToList();
                if (rowNames.Count == 0) { rows.Add(string.Empty); continue; }
                var prefix = row == 0 ? $"{label}: " : new string(' ', label.Length + FrameBorderWidth);
                var separatorsWidth = (rowNames.Count - FirstItemNumber) * FrameBorderWidth;
                var availablePerName = Math.Max(FirstItemNumber, (rightSheetWidth - prefix.Length - separatorsWidth) / rowNames.Count);
                var shortenedNames = rowNames.Select(name => name.Length <= availablePerName ? name : name[..availablePerName]);
                rows.Add(prefix + string.Join(", ", shortenedNames));
            }
            return rows;
        }


        private enum SheetSelectionKind { Weapon, Armor, MagicItem, Backpack, PartyMember }
        private readonly record struct SheetSelectionKey(SheetSelectionKind Kind, int Index);
        private sealed record SheetSelectionEntry(SheetSelectionKey Key);

    }
}   