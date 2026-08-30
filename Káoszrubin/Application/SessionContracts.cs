using KaoszRubin.Domain.Characters;
using KaoszRubin.Combat;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Application;

public readonly record struct PlayerId(Guid Value)
{
    public static PlayerId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public enum GameSessionPhase
{
    Exploration,
    Battle,
    Inn,
    Paused,
    GameOver
}

public enum CharacterControllerKind
{
    HostPlayer,
    RemotePlayer,
    Npc
}

public enum PlayerConnectionState
{
    Connected,
    Reconnecting,
    Disconnected
}

public sealed record CharacterControlState(CharacterId CharacterId, CharacterControllerKind ControllerKind,
    PlayerId? AssignedPlayerId, PlayerConnectionState ConnectionState);

public abstract record GameCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId);

public sealed record SetHelpVisibilityCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    bool IsOpen) : GameCommand(SenderId, CommandId, CharacterId);

public sealed record MoveCharacterCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    Direction Direction) : GameCommand(SenderId, CommandId, CharacterId);

public enum CharacterAction
{
    OpenDoor,
    CloseOrLockDoor,
    SearchCurrentPosition
}

public sealed record CharacterActionCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    CharacterAction Action, Position? TargetDoorPosition = null, bool? UseKey = null)
    : GameCommand(SenderId, CommandId, CharacterId);

public enum LeaderAction
{
    ToggleRegrouping,
    ToggleHoldPosition,
    ScatterParty,
    ToggleAttackMode,
    Rest,
    ActivateExit
}

public sealed record LeaderActionCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    LeaderAction Action) : GameCommand(SenderId, CommandId, CharacterId);

/// <summary>Atomi slotcsere-szándék; tárgydefiníciót vagy töltetszámot szándékosan nem fogad a klienstől.</summary>
public sealed record InventoryTransferCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    long ExpectedSourceRevision, InventorySlotKind SourceKind, int SourceIndex,
    CharacterId DestinationCharacterId, long ExpectedDestinationRevision,
    InventorySlotKind DestinationKind, int DestinationIndex)
    : GameCommand(SenderId, CommandId, CharacterId);

public sealed record UseInventoryItemCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    long ExpectedInventoryRevision, int BackpackIndex)
    : GameCommand(SenderId, CommandId, CharacterId);

public sealed record DropInventoryItemCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    long ExpectedInventoryRevision, InventorySlotKind SlotKind, int SlotIndex)
    : GameCommand(SenderId, CommandId, CharacterId);

public sealed record SplitInventoryStackCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    long ExpectedInventoryRevision, int BackpackIndex)
    : GameCommand(SenderId, CommandId, CharacterId);

public sealed record DistributeInventoryStackCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    long ExpectedInventoryRevision, int BackpackIndex)
    : GameCommand(SenderId, CommandId, CharacterId);

public sealed record PickUpGroundItemCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    long ExpectedInventoryRevision, WorldEntityId GroundPileId, long ExpectedGroundPileRevision,
    int GroundItemIndex, int DestinationBackpackIndex)
    : GameCommand(SenderId, CommandId, CharacterId);

public enum BattleActionKind
{
    PhysicalAttack,
    AdvanceEnemyTurn,
    CastSpell,
    TurnUndead,
    FighterPrecise,
    FighterPowerful,
    FighterDefensive,
    ThiefAmbush,
    ThiefObserve,
    ThiefPoison
}

/// <summary>A kliens választása, nem kész sebzés- vagy dobáseredmény.</summary>
public sealed record BattleActionCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    BattleId BattleId, long TurnId, BattleActionKind Action, string? SpellId = null,
    int? CastingItemSlotIndex = null, Position? Target = null)
    : GameCommand(SenderId, CommandId, CharacterId);

public sealed record CastExplorationSpellCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    string SpellId, int? CastingItemSlotIndex, Position Target)
    : GameCommand(SenderId, CommandId, CharacterId);

public sealed record InnPurchaseCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    long ExpectedInnRevision, InnVendorKind Vendor, int OfferIndex)
    : GameCommand(SenderId, CommandId, CharacterId);

public sealed record InnSaleCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    long ExpectedInnRevision, long ExpectedInventoryRevision, int BackpackIndex)
    : GameCommand(SenderId, CommandId, CharacterId);

public sealed record AcknowledgeNarrativeCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    Guid NarrativeId) : GameCommand(SenderId, CommandId, CharacterId);

public sealed record AcknowledgeLevelImageCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    Guid ImageId) : GameCommand(SenderId, CommandId, CharacterId);

public sealed record AcknowledgeRestCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    Guid RestId) : GameCommand(SenderId, CommandId, CharacterId);

public sealed record AssignQuickSpellCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    string SpellId, int QuickSlot) : GameCommand(SenderId, CommandId, CharacterId);

public sealed record PrepareSpellsCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    Guid PromptId, IReadOnlyList<string> SpellIds) : GameCommand(SenderId, CommandId, CharacterId);

public sealed record ResolveLevelUpPromptCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    Guid PromptId, string? ChoiceId) : GameCommand(SenderId, CommandId, CharacterId);

public abstract record GameSessionEvent(long Sequence);

public sealed record SessionPhaseChangedEvent(long Sequence, GameSessionPhase PreviousPhase,
    GameSessionPhase CurrentPhase) : GameSessionEvent(Sequence);

public sealed record CharacterControlChangedEvent(long Sequence, CharacterControlState Control)
    : GameSessionEvent(Sequence);

public sealed record GameCommandRejectedEvent(long Sequence, PlayerId PlayerId, long CommandId,
    string Reason) : GameSessionEvent(Sequence);

public sealed record BattlePromptEvent(long Sequence, BattleId BattleId, long TurnId,
    CharacterId ActingCharacterId, IReadOnlyList<BattleActionKind> AllowedActions) : GameSessionEvent(Sequence);

public sealed record BattleEndedEvent(long Sequence, BattleId BattleId) : GameSessionEvent(Sequence);
