using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Application;

public interface ISessionCommandHandler
{
    void OnSetHelpVisibility(PlayerId senderId, CharacterId characterId, bool isOpen);
    bool IsPausedByHelp();
    void OnMoveLeader(Direction direction);
    void OnMoveRemoteMember(MoveCharacterCommand command);
    void OnCharacterAction(CharacterActionCommand command);
    void OnLeaderAction(LeaderAction action);
    void OnInventoryTransfer(InventoryTransferCommand command);
    void OnUseInventoryItem(UseInventoryItemCommand command);
    void OnDropInventoryItem(DropInventoryItemCommand command);
    void OnSplitInventoryStack(SplitInventoryStackCommand command);
    void OnDistributeInventoryStack(DistributeInventoryStackCommand command);
    void OnGiveFollowerStack(GiveFollowerStackCommand command);
    void OnPickUpGroundItem(PickUpGroundItemCommand command);
    void OnBattleAction(BattleActionCommand command);
    void OnCastExplorationSpell(CastExplorationSpellCommand command);
    void OnInnPurchase(InnPurchaseCommand command);
    void OnInnSale(InnSaleCommand command);
    void OnAcknowledgeNarrative(AcknowledgeNarrativeCommand command);
    void OnAcknowledgeLevelImage(AcknowledgeLevelImageCommand command);
    void OnAcknowledgeRest(AcknowledgeRestCommand command);
    void OnAssignQuickSpell(AssignQuickSpellCommand command);
    void OnPrepareSpells(PrepareSpellsCommand command);
    void OnResolveLevelUpPrompt(ResolveLevelUpPromptCommand command);
}

public sealed class SessionCommandDispatcher
{
    private readonly GameSession _session;
    private readonly ISessionCommandHandler _handler;
    private readonly CharacterId _leaderCharacterId;

    public SessionCommandDispatcher(
        GameSession session,
        ISessionCommandHandler handler,
        CharacterId leaderCharacterId)
    {
        _session = session;
        _handler = handler;
        _leaderCharacterId = leaderCharacterId;
    }

    public void ProcessPendingCommands()
    {
        while (_session.TryReadCommand(out var command))
        {
            if (command is SetHelpVisibilityCommand helpVisibility)
            {
                _handler.OnSetHelpVisibility(helpVisibility.SenderId, helpVisibility.CharacterId, helpVisibility.IsOpen);
                continue;
            }
            if (_handler.IsPausedByHelp())
            {
                _session.RejectExecutedCommand(command, "A játék szünetel, amíg egy játékos a súgót olvassa.");
                continue;
            }
            switch (command)
            {
                case MoveCharacterCommand move when move.CharacterId == _leaderCharacterId:
                    _handler.OnMoveLeader(move.Direction);
                    break;
                case MoveCharacterCommand move:
                    _handler.OnMoveRemoteMember(move);
                    break;
                case CharacterActionCommand characterAction:
                    _handler.OnCharacterAction(characterAction);
                    break;
                case LeaderActionCommand action:
                    _handler.OnLeaderAction(action.Action);
                    break;
                case InventoryTransferCommand inventoryTransfer:
                    _handler.OnInventoryTransfer(inventoryTransfer);
                    break;
                case UseInventoryItemCommand useItem:
                    _handler.OnUseInventoryItem(useItem);
                    break;
                case DropInventoryItemCommand dropItem:
                    _handler.OnDropInventoryItem(dropItem);
                    break;
                case SplitInventoryStackCommand splitStack:
                    _handler.OnSplitInventoryStack(splitStack);
                    break;
                case DistributeInventoryStackCommand distributeStack:
                    _handler.OnDistributeInventoryStack(distributeStack);
                    break;
                case GiveFollowerStackCommand giveFollowerStack:
                    _handler.OnGiveFollowerStack(giveFollowerStack);
                    break;
                case PickUpGroundItemCommand pickUpItem:
                    _handler.OnPickUpGroundItem(pickUpItem);
                    break;
                case BattleActionCommand battleAction:
                    _handler.OnBattleAction(battleAction);
                    break;
                case CastExplorationSpellCommand castSpell:
                    _handler.OnCastExplorationSpell(castSpell);
                    break;
                case InnPurchaseCommand purchase:
                    _handler.OnInnPurchase(purchase);
                    break;
                case InnSaleCommand sale:
                    _handler.OnInnSale(sale);
                    break;
                case AcknowledgeNarrativeCommand acknowledgement:
                    _handler.OnAcknowledgeNarrative(acknowledgement);
                    break;
                case AcknowledgeLevelImageCommand imageAcknowledgement:
                    _handler.OnAcknowledgeLevelImage(imageAcknowledgement);
                    break;
                case AcknowledgeRestCommand restAcknowledgement:
                    _handler.OnAcknowledgeRest(restAcknowledgement);
                    break;
                case AssignQuickSpellCommand quickSpell:
                    _handler.OnAssignQuickSpell(quickSpell);
                    break;
                case PrepareSpellsCommand preparation:
                    _handler.OnPrepareSpells(preparation);
                    break;
                case ResolveLevelUpPromptCommand levelUp:
                    _handler.OnResolveLevelUpPrompt(levelUp);
                    break;
            }
        }
    }
}
