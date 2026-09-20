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
    private void ProcessSessionCommands()
    {
        _processingSessionCommands = true;
        try
        {
            if (_commandDispatcher.ProcessPendingCommands() > 0) MarkCoopSnapshotDirty();
        }
        finally
        {
            _processingSessionCommands = false;
            SynchronizeInventoryQuests();
        }
    }

    private void MarkCoopSnapshotDirty() => _coopSnapshotDirty = true;

    private void RequestCoopSnapshotPublish()
    {
        SynchronizeInventoryQuests();
        MarkCoopSnapshotDirty();
        if (!_processingSessionCommands && !_synchronizingQuestInventory)
            TryPublishScheduledCoopSnapshot(DateTime.UtcNow);
    }

    private void TryPublishScheduledCoopSnapshot(DateTime now)
    {
        if (_activeCoopHost is null ||
            (!_coopSnapshotDirty && now < _nextCoopSnapshotHeartbeatUtc) ||
            !_activeCoopHost.ShouldPublish(now)) return;
        if (!_activeCoopHost.TryPublish(CreateSessionSnapshot())) return;
        _coopSnapshotDirty = false;
        _nextCoopSnapshotHeartbeatUtc = now + CoopSnapshotHeartbeatInterval;
    }

    void ISessionCommandHandler.OnSetPlayerWindowVisibility(SetPlayerWindowVisibilityCommand command) =>
        UpdatePlayerBlockingWindowState(command.SenderId, command.CharacterId, command.Kind, command.WindowId,
            command.IsOpen);

    bool ISessionCommandHandler.IsPausedByPlayerWindow() => _openPlayerWindows.Count > 0;

    void ISessionCommandHandler.OnMoveLeader(Direction direction, bool preserveFormationFacing) =>
        MovePlayer(direction, preserveFormationFacing);

    void ISessionCommandHandler.OnMoveRemoteMember(MoveCharacterCommand command) => MoveRemotePartyMember(command);

    void ISessionCommandHandler.OnCharacterAction(CharacterActionCommand command) => ExecuteCharacterAction(command);

    void ISessionCommandHandler.OnLeaderAction(LeaderAction action) => ExecuteLeaderAction(action);

    void ISessionCommandHandler.OnInventoryTransfer(InventoryTransferCommand command) => ExecuteInventoryTransfer(command);

    void ISessionCommandHandler.OnUseInventoryItem(UseInventoryItemCommand command) => ExecuteUseInventoryItem(command);

    void ISessionCommandHandler.OnDropInventoryItem(DropInventoryItemCommand command) => ExecuteDropInventoryItem(command);

    void ISessionCommandHandler.OnSplitInventoryStack(SplitInventoryStackCommand command) => ExecuteSplitInventoryStack(command);

    void ISessionCommandHandler.OnDistributeInventoryStack(DistributeInventoryStackCommand command) =>
        ExecuteDistributeInventoryStack(command);

    void ISessionCommandHandler.OnGiveFollowerStack(GiveFollowerStackCommand command) => ExecuteGiveFollowerStack(command);

    void ISessionCommandHandler.OnPickUpGroundItem(PickUpGroundItemCommand command) => ExecutePickUpGroundItem(command);

    void ISessionCommandHandler.OnBattleAction(BattleActionCommand command) => ExecuteBattleAction(command);

    void ISessionCommandHandler.OnCastExplorationSpell(CastExplorationSpellCommand command) => ExecuteExplorationSpell(command);

    void ISessionCommandHandler.OnInnPurchase(InnPurchaseCommand command) => ExecuteInnPurchase(command);

    void ISessionCommandHandler.OnInnSale(InnSaleCommand command) => ExecuteInnSale(command);

    void ISessionCommandHandler.OnAcknowledgeNarrative(AcknowledgeNarrativeCommand command) =>
        ExecuteNarrativeAcknowledgement(command);

    void ISessionCommandHandler.OnAcknowledgeLevelImage(AcknowledgeLevelImageCommand command) =>
        ExecuteLevelImageAcknowledgement(command);

    void ISessionCommandHandler.OnAcknowledgeRest(AcknowledgeRestCommand command) => ExecuteRestAcknowledgement(command);

    void ISessionCommandHandler.OnAcknowledgeSharedWindow(AcknowledgeSharedWindowCommand command) =>
        ExecuteSharedWindowAcknowledgement(command);

    void ISessionCommandHandler.OnAssignQuickSpell(AssignQuickSpellCommand command) => ExecuteAssignQuickSpell(command);

    void ISessionCommandHandler.OnPrepareSpells(PrepareSpellsCommand command) => ExecuteSpellPreparation(command);

    void ISessionCommandHandler.OnResolveLevelUpPrompt(ResolveLevelUpPromptCommand command) => ExecuteLevelUpPrompt(command);


    private void ExecuteInnPurchase(InnPurchaseCommand command)
    {
        var recipient = CharacterRoster.Party.Members.FirstOrDefault(character => character.Id == command.CharacterId);
        if (recipient is null)
        {
            _session.RejectExecutedCommand(command, "A vásárló karakter már nem tagja a partinak.");
            return;
        }
        if (!_innController.TryPurchase(command.Vendor, command.OfferIndex, command.ExpectedInnRevision,
                recipient, out var message))
            _session.RejectExecutedCommand(command, message);
    }

    private void ExecuteInnSale(InnSaleCommand command)
    {
        var seller = CharacterRoster.Party.Members.FirstOrDefault(character => character.Id == command.CharacterId);
        if (seller is null)
        {
            _session.RejectExecutedCommand(command, "Az eladó karakter már nem tagja a partinak.");
            return;
        }
        if (!_innController.TrySell(command.ExpectedInnRevision, command.ExpectedInventoryRevision,
                command.BackpackIndex, seller, out var message))
            _session.RejectExecutedCommand(command, message);
    }

    private ConsoleKeyInfo ReadInnKey()
    {
        var key = ReadInnKeyCore();
        if (key.Key == ConsoleKey.Q)
        {
            ShowQuestJournal();
            return new ConsoleKeyInfo('\0', InnController.StateChangedKey, false, false, false);
        }
        if (GameInputBindings.IsCharacterSheetToggle(key.Key))
        {
            ManageCharacterSheetAtInn();
            return new ConsoleKeyInfo('\0', InnController.StateChangedKey, false, false, false);
        }
        return key;
    }

    private ConsoleKeyInfo ReadInnKeyCore()
    {
        var initialRevision = _innController.Revision;
        while (!Console.KeyAvailable)
        {
            ProcessSessionCommands();
            if (_innController.Revision != initialRevision)
            {
                RequestCoopSnapshotPublish();
                return new ConsoleKeyInfo('\0', InnController.StateChangedKey, false, false, false);
            }
            TryPublishScheduledCoopSnapshot(DateTime.UtcNow);
            Thread.Sleep(20);
        }
        return Console.ReadKey(intercept: true);
    }

    private void ManageCharacterSheetAtInn()
    {
        CancelHeldInventoryItem();
        _characterSheetFocused = true;
        _renderer.CharacterSheet.DrawInnCharacterSheet(PartyLeader);
        while (true)
        {
            var keyInfo = ReadInnKeyCore();
            if (keyInfo.Key == InnController.StateChangedKey)
            {
                _renderer.RefreshCharacterSheet(PartyLeader);
                continue;
            }
            if (_renderer.CharacterSheet.IsItemInspectionPageOpen)
            {
                if (keyInfo.Key is ConsoleKey.Escape or ConsoleKey.I or ConsoleKey.Enter)
                    _renderer.CharacterSheet.CloseItemInspectionPage();
                continue;
            }
            if (GameInputBindings.IsCharacterSheetToggle(keyInfo.Key) || keyInfo.Key == ConsoleKey.Escape)
            {
                CancelHeldInventoryItem();
                _characterSheetFocused = false;
                _renderer.CharacterSheet.SetCharacterSheetFocused(false);
                return;
            }
            if (keyInfo.Key == ConsoleKey.Q)
            {
                ShowQuestJournal();
                continue;
            }
            switch (GameInputBindings.InventoryAction(keyInfo.Key))
            {
                case InventoryInputAction.MoveUp: _renderer.CharacterSheet.MoveCharacterSheetSelection(-1); break;
                case InventoryInputAction.MoveDown: _renderer.CharacterSheet.MoveCharacterSheetSelection(1); break;
                case InventoryInputAction.Inspect: InspectSelectedInventoryItem(); break;
                case InventoryInputAction.Use: UseSelectedInventoryItem(); break;
                case InventoryInputAction.MoveItem: GrabOrPlaceInventoryItem(); break;
                case InventoryInputAction.SplitStack: SplitSelectedInventoryStack(); break;
                case InventoryInputAction.DistributeStack: DistributeSelectedInventoryStack(); break;
                case InventoryInputAction.CharacterDetails: ShowCharacterDetails(); break;
                case InventoryInputAction.GiveFollowerStack: GiveSelectedStackToFollower(); break;
                case InventoryInputAction.Drop:
                    _renderer.DrawInventoryMessage("A fogadóban nem dobhatsz tárgyat a földre.", ConsoleColor.DarkYellow);
                    break;
                default:
                    if (keyInfo.Key == ConsoleKey.LeftArrow) _renderer.CharacterSheet.MoveDisplayedPartyMember(-1);
                    else if (keyInfo.Key == ConsoleKey.RightArrow) _renderer.CharacterSheet.MoveDisplayedPartyMember(1);
                    else if (keyInfo.Key == ConsoleKey.Delete) DismissSelectedPartyMember();
                    break;
            }
        }
    }

    private void ExecuteNarrativeAcknowledgement(AcknowledgeNarrativeCommand command)
    {
        if (_activeNarrative?.NarrativeId != command.NarrativeId)
        {
            _session.RejectExecutedCommand(command, "Ez a történeti ablak már nem aktív.");
            return;
        }
        _narrativeAcknowledgements.Add(command.SenderId);
        PlaySessionSound(SoundEffect.Waiting, [command.CharacterId]);
    }

    private void ExecuteLevelImageAcknowledgement(AcknowledgeLevelImageCommand command)
    {
        if (_activeLevelImage?.ImageId != command.ImageId)
        {
            _session.RejectExecutedCommand(command, "Ez a pályakép már nem aktív.");
            return;
        }
        AcknowledgeLevelImage(command.SenderId, command.CharacterId);
    }
}
