using MazeGame.Combat;
using MazeGame.Domain.Characters;
using MazeGame.Domain.Magic;

namespace MazeGame.Application;

/// <summary>A hálózati szerződés jelenlegi verziója. Inkompatibilis DTO-változáskor növelendő.</summary>
public static class SessionProtocol
{
    public const int Version = 12;
}

/// <summary>A host doménállapotától leválasztott, JSON-nal továbbítható teljes session-kép.</summary>
public sealed record SessionSnapshot(int ProtocolVersion, long SnapshotSequence, long LastEventSequence,
    GameSessionPhase Phase, PlayerId HostPlayerId, CharacterId LeaderCharacterId, int MazeLevel,
    string LevelName, IReadOnlyList<SessionCharacterSnapshot> Party,
    IReadOnlyList<CharacterControlState> CharacterControls, BattleSnapshot? Battle, WorldSnapshot? World = null,
    int GoldenKeyCount = 0, int BossKeyCount = 0, InnSnapshot? Inn = null,
    NarrativeSnapshot? Narrative = null, SpellPreparationSnapshot? SpellPreparation = null,
    PartyRestSnapshot? RestNotice = null);

public sealed record PartyRestSnapshot(Guid RestId, bool AtInn, IReadOnlyList<CharacterRestSnapshot> Characters);

public sealed record CharacterRestSnapshot(CharacterId CharacterId, string CharacterName, int HealedAmount,
    IReadOnlyList<string> RemovedNegativeStatuses);

public sealed record SpellPreparationSnapshot(Guid PromptId, CharacterId CharacterId, string CharacterName,
    int Capacity, IReadOnlyList<KnownSpellSnapshot> Spells, IReadOnlyList<string> SelectedSpellIds);

public enum NarrativeKind { CampaignIntroduction, BossIntroduction, TwelveKeys, CampaignFinale }

public sealed record NarrativeSnapshot(Guid NarrativeId, NarrativeKind Kind, string Title, string Subtitle,
    IReadOnlyList<string> Paragraphs, IReadOnlyList<PlayerId> AcknowledgedPlayerIds);

public enum InnVendorKind { Market, Witcher, Blacksmith, Armorer, WanderingMage }

public sealed record InnSnapshot(long Revision, int PartyGold, IReadOnlyList<InnVendorSnapshot> Vendors);

public sealed record InnVendorSnapshot(InnVendorKind Kind, string Name, IReadOnlyList<InnOfferSnapshot> Offers);

public sealed record InnOfferSnapshot(int Index, InventoryItemSnapshot Item, int Price);

public sealed record SessionCharacterSnapshot(CharacterId CharacterId, string Name, string RaceId,
    string CharacterClassId, int Level, int CurrentVitality, int MaximumVitality, int CurrentMana,
    int MaximumMana, int FoodLevel, int WaterLevel, int Gold, bool IsAlive, Position? Position,
    IReadOnlyList<string> StatusIds, CharacterInventorySnapshot? Inventory,
    CharacterSheetSnapshot? CharacterSheet = null, ConsoleColor Color = ConsoleColor.Gray,
    IReadOnlyList<BattleSpellOption>? ExplorationSpellOptions = null,
    SpellInfoSnapshot? SpellInfo = null);

public sealed record SpellInfoSnapshot(string FocusName, int MemorizationCapacity,
    IReadOnlyList<KnownSpellSnapshot> KnownSpells);

public sealed record KnownSpellSnapshot(string SpellId, string Name, int Level, int ManaCost,
    SpellTargetType TargetType, string Description, bool IsMemorized, int? QuickSlot);

public sealed record BattleSnapshot(BattleId BattleId, long TurnId, int Round, bool IsPlayerTurn,
    CharacterId ActingCharacterId, SessionEnemySnapshot Enemy,
    IReadOnlyList<BattleActionKind> AllowedActions, IReadOnlyList<BattleSpellOption>? SpellOptions = null);

public sealed record BattleSpellOption(string SpellId, string Name, int Level, int ManaCost,
    SpellTargetType TargetType, int Range, int AreaRadius, int? CastingItemSlotIndex,
    MagicItemKind? CastingItemKind, int Charges, int? QuickSlot, IReadOnlyList<Position> ValidTargets);

public sealed record SessionEnemySnapshot(string DefinitionId, string Name, Position Position,
    int CurrentHitPoints, int MaximumHitPoints);

/// <summary>Host-oldali projekciós input; nem része a hálózaton fogadott parancsoknak.</summary>
public sealed record SessionSnapshotContext(int MazeLevel, string LevelName,
    IReadOnlyDictionary<CharacterId, Position> CharacterPositions, BattleSnapshot? Battle = null,
    WorldSnapshot? World = null);
