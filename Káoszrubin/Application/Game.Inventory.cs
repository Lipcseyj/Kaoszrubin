using KaoszRubin.Application;
using KaoszRubin.Application.Quests;
using KaoszRubin.Combat;
using KaoszRubin.Data;
using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;
using KaoszRubin.Domain.Quests;
using KaoszRubin.Infrastructure;
using KaoszRubin.Infrastructure.Quests;
using KaoszRubin.UI;
using System.Runtime;
using System.Security.Cryptography.Xml;
using static KaoszRubin.UI.GameInput;
using MainMenu = KaoszRubin.UI.MainMenu;

namespace KaoszRubin.Application;

public sealed partial class Game
{
    private bool TryStoreSearchedLoot(LiveCharacter character, IItemDefinition item, bool shareLootWithParty,
        out string ownerName, InventoryItemInstanceState? state = null) =>
        LootAndInventoryService.TryStoreSearchedLoot(character, item, shareLootWithParty,
            CharacterRoster.Party.Members, out ownerName, state);

    private MageIdentificationResult RollLootItemState(IItemDefinition item)
    {
        var state = ItemIdentificationRules.CreateLootState(item, _gameData.ItemCurses, _random,
            CurrentLevelConfiguration.ItemCurseChancePercent,
            _gameData.LootRules.MinimumEquipmentDurabilityPercent,
            _gameData.LootRules.MaximumEquipmentDurabilityPercent);
        return ItemIdentificationRules.AttemptByBestMage(item, state, CharacterRoster.Party.Members, _random);
    }

    private static string FormatMageIdentification(MageIdentificationResult result)
    {
        if (!result.Attempted || result.Mage is null) return string.Empty;
        return result.Succeeded
            ? $" 🔮 {result.Mage.Name} felismerte (dobás: {result.Roll}, esély: {result.ChancePercent}%)."
            : $" 🔮 {result.Mage.Name} nem tudta azonosítani (dobás: {result.Roll}, esély: {result.ChancePercent}%).";
    }

    private void PickUpGroundItems(LiveCharacter character, Position position, bool shareLootWithParty,
        ICollection<string> messages)
    {
        var pile = _maze.GetGroundItemPileAt(position);
        if (pile is null) return;
        var pickedUp = new List<string>();
        foreach (var entry in pile.Entries.ToArray())
        {
            if (!LootAndInventoryService.TryStoreSearchedLoot(character, entry.Item, shareLootWithParty,
                    CharacterRoster.Party.Members, out var owner, entry.State)) continue;
            pile.Remove(entry.Item);
            pickedUp.Add($"{ItemIdentificationRules.DisplayName(entry.Item, entry.State.IsIdentified)} → {owner}");
        }
        if (pickedUp.Count > 0) messages.Add("felvéve: " + string.Join(", ", pickedUp));
        if (pile.Items.Count == 0) _maze.RemoveGroundItemPile(pile);
        else messages.Add($"a földön maradt {pile.Items.Count} tárgy (nincs hely)");
    }

    private void InspectSelectedInventoryItem()
    {
        var slot = _renderer.CharacterSheet.GetSelectedInventorySlot();
        if (slot is null)
        {
            if (_renderer.CharacterSheet.GetSelectedPartyMember() is { } partyMember)
                _renderer.DrawInventoryMessage($"{partyMember.Name} — mozgásprofil: {NpcBehaviorName(partyMember.NpcBehavior)}.",
                    partyMember.Color);
            else
                _renderer.DrawInventoryMessage("A kijelölt helyen nincs megvizsgálható tárgy.", ConsoleColor.DarkYellow);
            return;
        }

        var item = slot.Value.Character.GetInventoryItem(slot.Value.Kind, slot.Value.Index);
        if (item is null)
        {
            _renderer.DrawInventoryMessage("A kijelölt helyen nincs megvizsgálható tárgy.", ConsoleColor.DarkYellow);
            return;
        }

        if (!slot.Value.Character.IsInventoryItemIdentified(slot.Value.Kind, slot.Value.Index))
        {
            var unknownItem = InventorySnapshotProjector.Create(slot.Value.Character).Slots
                .FirstOrDefault(entry => entry.Kind == slot.Value.Kind && entry.Index == slot.Value.Index)?.Item;
            if (unknownItem is null)
            {
                _renderer.DrawInventoryMessage("A tárgy adatai jelenleg nem olvashatók.", ConsoleColor.DarkYellow);
                return;
            }
            _renderer.CharacterSheet.DrawItemInspectionPage(ItemInspectionPanel.BuildUnidentified(unknownItem,
                focused: _characterSheetFocused, width: CharacterSheetPanel.Width));
            return;
        }

        var state = slot.Value.Character.GetInventoryItemState(slot.Value.Kind, slot.Value.Index);
        var charges = slot.Value.Character.GetInventoryItemCharges(slot.Value.Kind, slot.Value.Index);
        var quantity = slot.Value.Character.GetInventoryItemQuantity(slot.Value.Kind, slot.Value.Index);
        _renderer.CharacterSheet.DrawItemInspectionPage(ItemInspectionPanel.BuildKnown(item, _gameData,
            quantity, charges, state, focused: _characterSheetFocused, width: CharacterSheetPanel.Width));
    }

    private void DismissSelectedPartyMember()
    {
        var character = _renderer.CharacterSheet.GetSelectedPartyMember();
        if (character is null)
        {
            _renderer.DrawInventoryMessage("A Del használatához jelölj ki egy partitársat.", ConsoleColor.DarkYellow);
            return;
        }
        if (character == PartyLeader)
        {
            _renderer.DrawInventoryMessage("👑 A party leaderét nem lehet kirúgni.", ConsoleColor.DarkYellow);
            return;
        }

        var avatar = _maze.PartyMembers.FirstOrDefault(member => member.Character == character);
        var canWait = _session.Phase == GameSessionPhase.Exploration && character.IsAlive && avatar is not null;
        CancelHeldInventoryItem();
        var choice = ChoosePartyMemberDismissal(character, canWait);
        if (choice == PartyMemberDismissalChoice.Cancel)
        {
            _renderer.DrawInventoryMessage($"{character.Name} a partiban marad.", ConsoleColor.DarkYellow);
            return;
        }

        var guestCharacterId = _session.CharacterControls
            .FirstOrDefault(control => control.CharacterId == character.Id &&
                                       control.ControllerKind == CharacterControllerKind.RemotePlayer)?.CharacterId;
        var changedPositions = new List<Position>();
        if (avatar is not null)
        {
            changedPositions.Add(avatar.Position);
            _maze.RemovePartyMember(avatar);
            _nextPartyMoves.Remove(avatar);
        }
        foreach (var corpse in _maze.Corpses.OfType<PartyMemberCorpse>()
                     .Where(corpse => corpse.Character == character).ToList())
        {
            changedPositions.Add(corpse.Position);
            _maze.RemoveCorpse(corpse);
        }

        if (choice == PartyMemberDismissalChoice.WaitForParty)
        {
            CharacterRoster.Party.Remove(character);
            _waitingDismissedCompanions.RemoveAll(waiting => ReferenceEquals(waiting.Character, character));
            _waitingDismissedCompanions.Add(new WaitingDismissedCompanion(character, 2));
            _maze.AddWorldNpc(new WorldNpc(avatar!.Position, "NPC-FIRST-COMPANION", character,
                NpcDisposition.Friendly, recruitable: false, isQuestNpc: false,
                "Itt maradok a kijáratig. Utána két fogadón át megtaláltok, ha ismét fel akartok fogadni.",
                friendliness: 10, behavior: NpcWorldBehavior.Friendly));
        }
        else
        {
            CharacterRoster.Remove(character);
        }
        _npcSpellcasterTactics.Remove(character.Id);
        _humanMemberIds.Remove(character.Id);
        _formation = PartyFormationRules.Normalize(_formation,
            CharacterRoster.Party.Members.Select(member => member.Id), PartyLeader.Id);
        _renderer.CharacterSheet.SetFormationStatus(_formation);
        _session.SynchronizeParty();
        foreach (var position in changedPositions.Distinct())
            _renderer.DrawMapCellAfterBattle(_maze, _fogOfWar, position, _player.Position);
        _renderer.CharacterSheet.RefreshAfterPartyMemberRemoved(character, PartyLeader);
        var sackMemberMsg = choice == PartyMemberDismissalChoice.WaitForParty
            ? $"👋 {character.Name} itt vár a kijáratig, majd két fogadón át ismét felfogadható lesz standard áron."
            : $"👋 {character.Name} felszerelésével együtt végleg távozott a partiból.";
        _renderer.DrawInventoryMessage(sackMemberMsg, ConsoleColor.DarkYellow);
        if (guestCharacterId is not null)
        {
            RecordSessionActivity(SessionActivityKind.System, sackMemberMsg, ConsoleColor.DarkYellow, [guestCharacterId.Value]);
        }
        RequestCoopSnapshotPublish();
        TryFinalizeRodericPermanentJoin();
    }

    private PartyMemberDismissalChoice ChoosePartyMemberDismissal(LiveCharacter character, bool canWait)
    {
        _renderer.DrawInventoryMessage(canWait
            ? $"⚠️ Mi történjen {character.Name} karakterrel? V: végleg távozik | R: itt vár rátok | N/Esc: marad"
            : $"⚠️ Biztosan kirúgod {character.Name} karaktert? Felszerelésével együtt végleg távozik. I/Y: igen | N/Esc: nem",
            ConsoleColor.Red);
        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.N or ConsoleKey.Escape) return PartyMemberDismissalChoice.Cancel;
            if (canWait && key == ConsoleKey.R) return PartyMemberDismissalChoice.WaitForParty;
            if ((canWait && key == ConsoleKey.V) || (!canWait && key is ConsoleKey.I or ConsoleKey.Y))
                return PartyMemberDismissalChoice.Permanent;
        }
    }

    private bool ConfirmReturnToMainMenu()
    {
        _renderer.DrawInventoryMessage(
            "⚠️ Visszatérsz a főmenübe? A legutóbbi mentés óta történt változások elvesznek. I/Y: igen | N/Esc: maradok",
            ConsoleColor.Red);
        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.I or ConsoleKey.Y) return true;
            if (key is ConsoleKey.N or ConsoleKey.Escape)
            {
                _renderer.DrawInventoryMessage("A játék folytatódik.", ConsoleColor.Cyan);
                return false;
            }
        }
    }

    private void UseSelectedInventoryItem()
    {
        var slot = _renderer.CharacterSheet.GetSelectedInventorySlot();
        if (slot is { } reserve && reserve.Kind == InventorySlotKind.Weapon && reserve.Index == 2)
        {
            var character = reserve.Character;
            var swapCommandId = _localCommandId + 1;
            if (_session.Submit(new InventoryTransferCommand(_session.HostPlayerId, swapCommandId, character.Id,
                character.InventoryRevision, InventorySlotKind.Weapon, 2, character.Id,
                character.InventoryRevision, InventorySlotKind.Weapon, 0))) _localCommandId = swapCommandId;
            return;
        }
        if (slot is { } repairTarget &&
            repairTarget.Character.GetInventoryItem(repairTarget.Kind, repairTarget.Index) is { } targetItem &&
            EquipmentDurabilityRules.MaximumDurability(targetItem) > 0)
        {
            var repairKitIndex = FindBackpackItemIndex(repairTarget.Character, MiscItemIds.RepairKit);
            if (repairKitIndex is null)
            {
                _renderer.DrawInventoryMessage("A célzott terepi javításhoz javítókészlet kell a hátizsákba.",
                    ConsoleColor.DarkYellow);
                return;
            }
            var repairCommandId = _localCommandId + 1;
            if (_session.Submit(new UseInventoryItemCommand(_session.HostPlayerId, repairCommandId,
                    repairTarget.Character.Id, repairTarget.Character.InventoryRevision, repairKitIndex.Value,
                    repairTarget.Kind, repairTarget.Index))) _localCommandId = repairCommandId;
            return;
        }
        if (slot is null || slot.Value.Kind != InventorySlotKind.Backpack)
        { _renderer.DrawInventoryMessage("Használható tárgyat a hátizsákban jelölj ki.", ConsoleColor.DarkYellow); return; }
        var selectedItem = slot.Value.Character.GetInventoryItem(slot.Value.Kind, slot.Value.Index);
        if (SpellcastingRules.IsSpellcastingFocus(selectedItem))
        {
            _renderer.CharacterSheet.DrawSpellInfoPage(slot.Value.Character, 0);
            return;
        }
        if (selectedItem is not MiscItemDefinition item || item.Effect == ConsumableEffect.None)
        { _renderer.DrawInventoryMessage("A kijelölt tárgy közvetlenül nem használható.", ConsoleColor.DarkYellow); return; }

        var commandId = _localCommandId + 1;
        if (!_session.Submit(new UseInventoryItemCommand(_session.HostPlayerId, commandId,
                slot.Value.Character.Id, slot.Value.Character.InventoryRevision, slot.Value.Index))) return;
        _localCommandId = commandId;
    }

    private void ExecuteUseInventoryItem(UseInventoryItemCommand command)
    {
        var character = CharacterRoster.Party.Members.FirstOrDefault(member => member.Id == command.CharacterId);
        if (character?.GetInventoryItem(InventorySlotKind.Backpack, command.BackpackIndex) is not MiscItemDefinition item ||
            item.Effect == ConsumableEffect.None || character.InventoryRevision != command.ExpectedInventoryRevision)
            return;

        var used = true;
        var result = item.Id == MiscItemIds.HerbalTea &&
                     (character.WaterLevel < 100 || character.IsAlive && character.CurrentVitality < character.MaximumVitality)
            ? UseHerbalTea(character, item.EffectValue)
            : IsInitiativeDrink(item) && character.IsAlive
                ? UseInitiativeDrink(character, item)
            : item.Effect switch
            {
                ConsumableEffect.Food when character.FoodLevel < 100 => UseFood(character, item.EffectValue),
                ConsumableEffect.Water when character.WaterLevel < 100 => UseWater(character, item.EffectValue),
                ConsumableEffect.Heal when character.IsAlive && character.CurrentVitality < character.MaximumVitality => UseHealing(character, item.EffectValue),
                ConsumableEffect.RestoreMana when character.IsAlive && character.UsesMana && character.CurrentMana < character.MaximumMana => UseManaPotion(character, item.EffectValue),
                ConsumableEffect.CurePoison when character.RemoveStatus(CharacterStatusIds.Poisoned) => "a mérgezés megszűnt",
                ConsumableEffect.CureDisease when character.RemoveStatus(CharacterStatusIds.Diseased) => "a betegség megszűnt",
                ConsumableEffect.StopBleeding when character.RemoveStatus(CharacterStatusIds.Bleeding) => "a vérzés elállt",
                ConsumableEffect.Vision when character.IsAlive => UseVisionItem(character, item),
                ConsumableEffect.RepairEquipment => UseFieldRepairKit(character,
                    command.TargetKind, command.TargetIndex, item.EffectValue),
                _ => string.Empty
            };
        if (string.IsNullOrEmpty(result)) used = false;
        if (!used) 
        {
            var notUsableMsg = "A tárgy hatására most nincs szükség vagy nem alkalmazható.";
            if (command.SenderId == _session.HostPlayerId)
                _renderer.DrawInventoryMessage(notUsableMsg, ConsoleColor.DarkYellow); 
            else RecordSessionActivity(SessionActivityKind.System, notUsableMsg, ConsoleColor.DarkYellow, [character.Id]);
            
            return; 
        }

        character.RemoveOneInventoryItem(InventorySlotKind.Backpack, command.BackpackIndex);
        character.SynchronizeNeedStatuses(_gameData.GetStatus(CharacterStatusIds.Hungry), _gameData.GetStatus(CharacterStatusIds.Thirsty));
        // Fogadóban is rögtön rajzoljuk újra a látható karakterlapot az új
        // éhség-/szomjúságértékkel és a megváltozott inventoryval.
        _renderer.RefreshCharacterSheet(character);
        var message = $"{character.Name} használta: {item.Name} — {result}.";
        if (command.SenderId == _session.HostPlayerId)
            _renderer.DrawInventoryMessage(message, ConsoleColor.Green);
        else RecordSessionActivity(SessionActivityKind.System, message, ConsoleColor.Green, [character.Id]);
        if (item.Effect == ConsumableEffect.Heal)
            PlaySessionSound(SoundEffect.DefensiveSpell, [character.Id]);
    }

    private static int? FindBackpackItemIndex(LiveCharacter character, string itemId)
    {
        for (var index = 0; index < LiveCharacter.MaximumBackpackItemCount; index++)
            if (string.Equals(character.GetInventoryItem(InventorySlotKind.Backpack, index)?.Id, itemId,
                    StringComparison.OrdinalIgnoreCase)) return index;
        return null;
    }

    private static string UseFieldRepairKit(LiveCharacter character, InventorySlotKind? requestedKind,
        int? requestedIndex, int amount)
    {
        var target = requestedKind is { } kind && requestedIndex is { } index
            ? (Kind: kind, Index: index)
            : FindFieldRepairTarget(character);
        if (target is null) return string.Empty;
        var repaired = character.RepairInventoryItemLimited(target.Value.Kind, target.Value.Index,
            amount, FieldRepairMaximumPercent);
        return repaired.Changed
            ? $"{repaired.ItemName} tartóssága {repaired.PreviousDurability}/{repaired.MaximumDurability} → " +
              $"{repaired.CurrentDurability}/{repaired.MaximumDurability} (terepi maximum: {FieldRepairMaximumPercent}%)"
            : string.Empty;
    }

    private static (InventorySlotKind Kind, int Index)? FindFieldRepairTarget(LiveCharacter character)
    {
        var addresses = Enumerable.Range(0, 3).Select(index => (InventorySlotKind.Weapon, index))
            .Append((InventorySlotKind.Armor, 0))
            .Concat(Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount)
                .Select(index => (InventorySlotKind.Backpack, index)));
        return addresses.Select(address =>
            {
                var item = character.GetInventoryItem(address.Item1, address.Item2);
                var state = character.GetInventoryItemState(address.Item1, address.Item2);
                var maximum = item is null ? 0 : EquipmentDurabilityRules.MaximumDurability(item);
                var current = item is null || state is null ? maximum :
                    EquipmentDurabilityRules.CurrentDurability(item, state.Value);
                return (Kind: address.Item1, Index: address.Item2, Maximum: maximum, Current: current);
            })
            .Where(candidate => candidate.Maximum > 0 &&
                                candidate.Current < candidate.Maximum * FieldRepairMaximumPercent / 100)
            .OrderBy(candidate => candidate.Current * 100d / candidate.Maximum)
            .Select(candidate => ((InventorySlotKind Kind, int Index)?)(candidate.Kind, candidate.Index))
            .FirstOrDefault();
    }

    private void ExecuteDropInventoryItem(DropInventoryItemCommand command)
    {
        var character = CharacterRoster.Party.Members.FirstOrDefault(member => member.Id == command.CharacterId);
        if (character is null || character.InventoryRevision != command.ExpectedInventoryRevision) return;
        var item = character.GetInventoryItem(command.SlotKind, command.SlotIndex);
        if (item is null || SpellcastingRules.IsSpellcastingFocus(item) || CharacterBoundItemRules.IsBound(item)) return;
        var charges = character.GetInventoryItemCharges(command.SlotKind, command.SlotIndex);
        var state = character.GetInventoryItemState(command.SlotKind, command.SlotIndex);
        var quantity = character.GetInventoryItemQuantity(command.SlotKind, command.SlotIndex);
        var position = GetCharacterWorldPosition(character);
        if (position is null || !character.RemoveOneInventoryItem(command.SlotKind, command.SlotIndex)) return;
        var droppedState = quantity > 1 && state is { } stackedState
            ? stackedState with { InstanceId = Guid.NewGuid() }
            : state;
        _maze.DropItem(position.Value, item, charges, droppedState);
        _renderer.RefreshCharacterSheet(PartyLeader);
        _renderer.DrawMapCellsChanged(_maze, _fogOfWar, _player.Position, [position.Value]);
        var pileCount = _maze.GetGroundItemPileAt(position.Value)?.Items.Count ?? 1;
        var message = $"Ledobtad: {ItemIdentificationRules.DisplayName(item, state?.IsIdentified != false)}. " +
                      $"A mezőn {pileCount} tárgy van.";
        if (command.SenderId == _session.HostPlayerId)
            _renderer.DrawInventoryMessage(message, ConsoleColor.Cyan);
        else
            RecordSessionActivity(SessionActivityKind.System, message, ConsoleColor.Cyan, [character.Id]);
        PlaySessionSound(SoundEffect.Item, [character.Id]);
    }

    private void ExecutePickUpGroundItem(PickUpGroundItemCommand command)
    {
        var character = CharacterRoster.Party.Members.FirstOrDefault(member => member.Id == command.CharacterId);
        var pile = _maze.GroundItemPiles.FirstOrDefault(candidate => candidate.Id == command.GroundPileId);
        var position = character is null ? null : GetCharacterWorldPosition(character);
        if (character is null || pile is null || position != pile.Position ||
            character.InventoryRevision != command.ExpectedInventoryRevision ||
            pile.Revision != command.ExpectedGroundPileRevision || command.GroundItemIndex < 0 ||
            command.GroundItemIndex >= pile.Entries.Count)
            return;
        var entry = pile.Entries[command.GroundItemIndex];
        var destinationItem = character.GetInventoryItem(InventorySlotKind.Backpack,
            command.DestinationBackpackIndex);
        var destinationQuantity = character.GetInventoryItemQuantity(InventorySlotKind.Backpack,
            command.DestinationBackpackIndex);
        var destinationState = character.GetInventoryItemState(InventorySlotKind.Backpack,
            command.DestinationBackpackIndex);
        if (destinationItem is not null && (!string.Equals(destinationItem.Id, entry.Item.Id,
                StringComparison.OrdinalIgnoreCase) ||
            destinationState?.IsIdentified != true || !entry.State.IsIdentified ||
            character.GetInventoryItemCharges(InventorySlotKind.Backpack, command.DestinationBackpackIndex) !=
            entry.Charges || destinationQuantity >= LiveCharacter.MaximumBackpackStackSize)) return;
        var change = new InventorySlotChange(InventorySlotKind.Backpack, command.DestinationBackpackIndex,
            entry.Item, entry.Charges, destinationQuantity + 1,
            destinationItem is null ? entry.State : destinationState);
        if (!character.CanApplyInventoryChanges(change) ||
            !pile.TryTake(command.GroundItemIndex, command.ExpectedGroundPileRevision, out _)) return;
        character.ApplyInventoryChanges(change);
        if (pile.Entries.Count == 0) _maze.RemoveGroundItemPile(pile);
        _renderer.RefreshCharacterSheet(PartyLeader);
        _renderer.DrawMapCellsChanged(_maze, _fogOfWar, _player.Position, [position.Value]);
        if (command.SenderId == _session.HostPlayerId)
            _renderer.DrawInventoryMessage($"Felvetted: {ItemIdentificationRules.DisplayName(entry.Item, entry.State.IsIdentified)}.", ConsoleColor.Green);
        PlaySessionSound(SoundEffect.Item, [character.Id]);
    }

    private Position? GetCharacterWorldPosition(LiveCharacter character)
    {
        if (character == PartyLeader) return _player.Position;
        return _maze.PartyMembers.FirstOrDefault(member => member.Character == character)?.Position;
    }

    private static string UseFood(LiveCharacter character, int amount)
    {
        var before = character.FoodLevel;
        character.RestoreFood(amount);
        return $"élelem +{character.FoodLevel - before}";
    }

    private static string UseWater(LiveCharacter character, int amount)
    {
        var before = character.WaterLevel;
        character.RestoreWater(amount);
        return $"víz +{character.WaterLevel - before}";
    }

    private string UseHerbalTea(LiveCharacter character, int waterAmount)
    {
        var waterBefore = character.WaterLevel;
        var vitalityBefore = character.CurrentVitality;
        character.RestoreWater(waterAmount);
        var healing = _random.Next(5, 16);
        if (character.IsAlive) character.RestoreVitality(healing);
        return $"víz +{character.WaterLevel - waterBefore}, {FormatHealingResult(character, healing, vitalityBefore)}";
    }

    private static bool IsInitiativeDrink(MiscItemDefinition item) =>
        item.Id is MiscItemIds.Mead or MiscItemIds.SpicedWine;

    private static string UseInitiativeDrink(LiveCharacter character, MiscItemDefinition item)
    {
        var waterBefore = character.WaterLevel;
        character.RestoreWater(item.EffectValue);
        character.ApplySpellEffect(new ActiveSpellEffect(item.Id, ActiveSpellEffectType.InitiativeBonus,
            2, 10, Beneficial: true));
        character.ApplySpellEffect(new ActiveSpellEffect(item.Id, ActiveSpellEffectType.HitBonus,
            1, 10, Beneficial: true));
        return $"víz +{character.WaterLevel - waterBefore}, +2 kezdeményezés és +1 találat 10 körig";
    }

    private string UseVisionItem(LiveCharacter character, MiscItemDefinition item)
    {
        character.ApplySpellEffect(new ActiveSpellEffect(item.Id, ActiveSpellEffectType.VisionBonus,
            item.EffectValue, 12, Beneficial: true));
        if (GetCharacterWorldPosition(character) is { } position) RevealFor(character, position);
        return $"látótáv +{item.EffectValue} 12 körig";
    }

    private static string UseHealing(LiveCharacter character, int amount)
    {
        var before = character.CurrentVitality;
        character.RestoreVitality(amount);
        return FormatHealingResult(character, amount, before);
    }

    private static string FormatHealingResult(LiveCharacter character, int requestedAmount, int vitalityBefore)
    {
        var actual = character.CurrentVitality - vitalityBefore;
        var adjusted = character.PreviewVitalityRecovery(requestedAmount);
        var penalties = character.Statuses
            .Where(status => status.VitalityRecoveryPercent < 100)
            .Select(status => $"{status.Icon} {status.VitalityRecoveryPercent}%")
            .ToArray();
        var reduction = adjusted < requestedAmount && penalties.Length > 0
            ? $" (állapotok csökkentették: {requestedAmount} → {adjusted}; {string.Join(" × ", penalties)})"
            : string.Empty;
        return $"❤️ +{actual} HP{reduction}";
    }

    private static string UseManaPotion(LiveCharacter character, int amount)
    {
        var before = character.CurrentMana;
        character.RestoreMana(amount);
        return $"manna +{character.CurrentMana - before}";
    }

    private static string NpcBehaviorName(NpcBehavior? behavior) => behavior switch
    {
        NpcBehavior.Defensive => "Defenzív",
        NpcBehavior.Aggressive => "Aggresszív",
        NpcBehavior.Scout => "Felderítő",
        NpcBehavior.Cautious => "Óvatos",
        _ => "inaktív"
    };

    private void GrabOrPlaceInventoryItem()
    {
        var slot = _renderer.CharacterSheet.GetSelectedInventorySlot();
        if (slot is null) 
        { 
            var selectSlotErrorMsg = "Válassz egy felszerelés- vagy hátizsákhelyet.";
                _renderer.DrawInventoryMessage(selectSlotErrorMsg, ConsoleColor.DarkYellow);
            return;
        }
        InventorySlotReference target = slot.Value;
        if (_heldInventoryItem is null)
        {
            var item = target.Character.GetInventoryItem(target.Kind, target.Index);
            if (item is null) 
            { 
                var emptySlotErrorMsg = "A kijelölt hely üres.";
                _renderer.DrawInventoryMessage(emptySlotErrorMsg, ConsoleColor.DarkYellow);
                return; 
            }
            if (SpellcastingRules.IsSpellcastingFocus(item))
            { 
                var focusErrorMsg = $"A(z) {item.Name} a hátizsák első helyéhez kötött, ezért nem mozgatható.";
                _renderer.DrawInventoryMessage(focusErrorMsg, ConsoleColor.Red); 
                return; 
            }
            _heldInventoryItem = new HeldInventoryItem(item, target, target.Character.InventoryRevision);
            var grabItemMsg = $"Kézbe vetted: {item.Name}. Válassz célhelyet, majd nyomj Space-t.";
            _renderer.DrawInventoryMessage(grabItemMsg, ConsoleColor.Yellow);
            return;
        }

        var held = _heldInventoryItem;
        if (target == held.Source)
        {
            _heldInventoryItem = null;

            var cancelMoveMsg = $"A(z) {held.Item.Name} áthelyezése megszakítva.";
            _renderer.DrawInventoryMessage(cancelMoveMsg, ConsoleColor.DarkYellow);
            return;
        }
        var commandId = _localCommandId + 1;
        var command = new InventoryTransferCommand(_session.HostPlayerId, commandId, held.Source.Character.Id,
            held.SourceRevision, held.Source.Kind, held.Source.Index, target.Character.Id,
            target.Character.InventoryRevision, target.Kind, target.Index);
        if (!_session.Submit(command)) return;
        _localCommandId = commandId;
        _heldInventoryItem = null;
    }

    private void CancelHeldInventoryItem()
    {
        if (_heldInventoryItem is not { } held) return;
        _heldInventoryItem = null;
        var cancelHeldInventoryItemMsg = $"A(z) {held.Item.Name} áthelyezése megszakítva.";
        _renderer.DrawInventoryMessage(cancelHeldInventoryItemMsg, ConsoleColor.DarkYellow);
    }

    private void SplitSelectedInventoryStack()
    {
        if (_heldInventoryItem is not null)
        {
            var splitStackErrorMsg = "Előbb fejezd be vagy szakítsd meg a kézben tartott tárgy mozgatását.";
            _renderer.DrawInventoryMessage(splitStackErrorMsg, ConsoleColor.DarkYellow);
            return;
        }
        var slot = _renderer.CharacterSheet.GetSelectedInventorySlot();
        if (slot is null || slot.Value.Kind != InventorySlotKind.Backpack)
        {
            var splitStackErrorMsg = "Hátizsákban levő köteget jelölj ki a felezéshez.";
            _renderer.DrawInventoryMessage(splitStackErrorMsg, ConsoleColor.DarkYellow);
            return;
        }
        var selected = slot.Value;
        var commandId = _localCommandId + 1;
        var command = new SplitInventoryStackCommand(_session.HostPlayerId, commandId,
            selected.Character.Id, selected.Character.InventoryRevision, selected.Index);
        if (!InventoryStackService.Validate(CharacterRoster.Party, command, out var error))
        {
            _renderer.DrawInventoryMessage(error, ConsoleColor.Red);
            return;
        }
        if (!_session.Submit(command)) return;
        _localCommandId = commandId;
    }

    private void ExecuteSplitInventoryStack(SplitInventoryStackCommand command)
    {
        if (!InventoryStackService.TryExecute(CharacterRoster.Party, command, out var result, out var error))
        {
            _renderer.DrawInventoryMessage(error, ConsoleColor.Red);
            return;
        }
        _renderer.RefreshCharacterSheet(PartyLeader);
        var message = $"Köteg megfelezve: {result.ItemName} ({result.RemainingQuantity}+{result.NewQuantity}).";
        if (command.SenderId == _session.HostPlayerId)
            _renderer.DrawInventoryMessage(message, ConsoleColor.Green);
        else
            RecordSessionActivity(SessionActivityKind.System, message, ConsoleColor.Green);
        PlaySessionSound(SoundEffect.Item, [command.CharacterId]);
    }

    private void DistributeSelectedInventoryStack()
    {
        if (_heldInventoryItem is not null)
        {
            var distributeStackErrorMsg = "Előbb fejezd be vagy szakítsd meg a kézben tartott tárgy mozgatását.";
            _renderer.DrawInventoryMessage(distributeStackErrorMsg, ConsoleColor.DarkYellow);
            return;
        }
        var slot = _renderer.CharacterSheet.GetSelectedInventorySlot();
        if (slot is null || slot.Value.Kind != InventorySlotKind.Backpack)
        {
            var distributeStackErrorMsg = "Elfogyasztható hátizsáktárgyat jelölj ki a szétosztáshoz.";
            _renderer.DrawInventoryMessage(distributeStackErrorMsg, ConsoleColor.DarkYellow);
            return;
        }
        var selected = slot.Value;
        var commandId = _localCommandId + 1;
        var command = new DistributeInventoryStackCommand(_session.HostPlayerId, commandId,
            selected.Character.Id, selected.Character.InventoryRevision, selected.Index);
        if (!InventoryDistributionService.Validate(CharacterRoster.Party, command, out var error))
        {
            _renderer.DrawInventoryMessage(error, ConsoleColor.Red);
            return;
        }
        if (!_session.Submit(command)) return;
        _localCommandId = commandId;
    }

    private void ExecuteDistributeInventoryStack(DistributeInventoryStackCommand command)
    {
        if (!InventoryDistributionService.TryExecute(CharacterRoster.Party, command, out var result, out var error))
        {
            _renderer.DrawInventoryMessage(error, ConsoleColor.Red);
            return;
        }
        _renderer.RefreshCharacterSheet(PartyLeader);
        var recipients = result.RecipientNames.Count == 0 ? string.Empty :
            $" → {string.Join(", ", result.RecipientNames)}";
        _renderer.DrawInventoryMessage(
            $"Szétosztva: {result.ItemName}, {result.DistributedQuantity} db{recipients}. " +
            $"A forráshelyen maradt: {result.RemainingSourceQuantity} db.", ConsoleColor.Green);
        RecordSessionActivity(SessionActivityKind.System,
            $"{result.ItemName} szétosztva a partyban ({result.DistributedQuantity} db).", ConsoleColor.Green);
        PlaySessionSound(SoundEffect.Item);
    }

    private void GiveSelectedStackToFollower()
    {
        if (_heldInventoryItem is not null)
        {
            var giveStackError1Msg = "Előbb fejezd be vagy szakítsd meg a kézben tartott tárgy mozgatását.";
            _renderer.DrawInventoryMessage(giveStackError1Msg, ConsoleColor.DarkYellow);
            return;
        }
        var slot = _renderer.CharacterSheet.GetSelectedInventorySlot();
        if (slot is null || slot.Value.Kind != InventorySlotKind.Backpack)
        {
            var giveStackError2Msg = "Elfogyasztható hátizsákköteget jelölj ki az átadáshoz.";
            _renderer.DrawInventoryMessage(giveStackError2Msg, ConsoleColor.DarkYellow);
            return;
        }
        var follower = _maze.PartyMembers.FirstOrDefault(member => member.IsTemporaryFollower && member.Character.IsAlive)
            ?.Character;
        if (follower is null)
        {
            var giveStackError3Msg = "Nincs aktív követő NPC, akinek átadhatnád.";
            _renderer.DrawInventoryMessage(giveStackError3Msg, ConsoleColor.DarkYellow);
            return;
        }
        var selected = slot.Value;
        var commandId = _localCommandId + 1;
        var command = new GiveFollowerStackCommand(_session.HostPlayerId, commandId, selected.Character.Id,
            selected.Character.InventoryRevision, selected.Index, follower.Id, follower.InventoryRevision);
        if (!_session.Submit(command)) return;
        _localCommandId = commandId;
    }

    private void ExecuteGiveFollowerStack(GiveFollowerStackCommand command)
    {
        var source = CharacterRoster.Party.Members.FirstOrDefault(character => character.Id == command.CharacterId);
        var follower = _maze.PartyMembers.FirstOrDefault(member => member.IsTemporaryFollower &&
            member.Character.Id == command.FollowerCharacterId)?.Character;
        var error = "A követő már nincs a csapattal.";
        if (source is null || follower is null || !FollowerStackTransferService.TryExecute(source, follower, command,
                out var result, out error))
        {
            _renderer.DrawInventoryMessage(error, ConsoleColor.Red);
            return;
        }
        _renderer.RefreshCharacterSheet(PartyLeader);
        var message = $"{result.FollowerName} kapott: {result.ItemName} ×{result.TransferredQuantity}; " +
                      $"a forrásnál maradt: {result.RemainingQuantity}.";
        _renderer.DrawInventoryMessage(message, ConsoleColor.Green);
        RecordSessionActivity(SessionActivityKind.System, message, ConsoleColor.Green);
        PlaySessionSound(SoundEffect.Item);
    }

    private void ExecuteInventoryTransfer(InventoryTransferCommand command)
    {
        if (!InventoryTransferService.TryExecute(CharacterRoster.Party, command, out var result, out var error))
        {
            _renderer.DrawInventoryMessage(error, ConsoleColor.Red);
            return;
        }
        _renderer.RefreshCharacterSheet(PartyLeader);
        
        foreach (var activation in result.CurseActivations ?? [])
        {
            _renderer.DrawInventoryMessage(activation, ConsoleColor.Red);
            RecordSessionActivity(SessionActivityKind.System, activation, ConsoleColor.Red);
        }

        var guest = _session.CharacterControls
            .Where(control => control.ControllerKind == CharacterControllerKind.RemotePlayer)
            .Select(control => CharacterRoster.Party.Members.FirstOrDefault(character =>
                character.Id == control.CharacterId))
            .FirstOrDefault(character => character is not null);

        var leader = CharacterRoster.Party.Leader;

        if (guest != null && leader != null && 
            (command.CharacterId == guest.Id || command.CharacterId == leader.Id) &&
            (command.CharacterId != command.DestinationCharacterId))
        {
            CharacterId? thirdPartyId = null;
            if (command.CharacterId != leader.Id && command.CharacterId != guest.Id)
                thirdPartyId = command.CharacterId;

            if (thirdPartyId == null &&
               (command.DestinationCharacterId != leader.Id && command.DestinationCharacterId != guest.Id))
                thirdPartyId = command.DestinationCharacterId;

            // ha a host elvesz vagy ad a guest-nek
            if (command.SenderId == _session.HostPlayerId)
            {
                var secondActor = thirdPartyId == null ? guest.Name : (CharacterRoster.Party.Members.FirstOrDefault(character => character.Id == thirdPartyId)?.Name ?? "ismeretlen");
                string hostTransferMessage = $"Tárgy adásvétel {leader.Name} és {secondActor} között: {result.SourceItemName}";
                _renderer.DrawInventoryMessage(hostTransferMessage, ConsoleColor.Magenta);
                RecordSessionActivity(SessionActivityKind.System, hostTransferMessage, ConsoleColor.Magenta,
                    [leader.Id, guest.Id]);
                PlaySessionSound(SoundEffect.Item, [leader.Id, guest.Id]);
            }
            // ha a guest játékos ad át tárgyat a hostnak, vagy vesz el tőle
            else 
            {
                var secondActor = thirdPartyId == null ? leader.Name : (CharacterRoster.Party.Members.FirstOrDefault(character => character.Id == thirdPartyId)?.Name ?? "ismeretlen");
                string hostTransferMessage = $"Tárgy adásvétel {guest.Name} és {secondActor} között: {result.SourceItemName}";
                _renderer.DrawInventoryMessage(hostTransferMessage, ConsoleColor.Magenta);
                RecordSessionActivity(SessionActivityKind.System, hostTransferMessage, ConsoleColor.Magenta,
                    [leader.Id, guest.Id]);
                PlaySessionSound(SoundEffect.Item, [leader.Id, guest.Id]);
            }
        }
        else
        {
            var transferMessage = (result.DisplacedItemName is null || result.DisplacedItemName == result.SourceItemName)
                ? $"Áthelyezted: {result.SourceItemName}."
                : $"Felcserélted: {result.SourceItemName} ↔ {result.DisplacedItemName}.";
            if (command.SenderId == _session.HostPlayerId)
                _renderer.DrawInventoryMessage(transferMessage, ConsoleColor.Green);
            RecordSessionActivity(SessionActivityKind.System, transferMessage, ConsoleColor.Green, [command.CharacterId]);
            PlaySessionSound(SoundEffect.Item, [command.CharacterId]);
        }
    }
}
