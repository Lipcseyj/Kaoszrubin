using MazeGame.Combat;
using MazeGame.Domain.Characters;
using MazeGame.Domain.Magic;

namespace MazeGame.Application;

/// <summary>A hálózati szerződés jelenlegi verziója. Inkompatibilis DTO-változáskor növelendő.</summary>
public static class SessionProtocol
{
    public const int Version = 29;
}

/// <summary>A host doménállapotától leválasztott, JSON-nal továbbítható teljes session-kép.</summary>
public sealed record SessionSnapshot(int ProtocolVersion, long SnapshotSequence, long LastEventSequence,
    GameSessionPhase Phase, PlayerId HostPlayerId, CharacterId LeaderCharacterId, int MazeLevel,
    string LevelName, IReadOnlyList<SessionCharacterSnapshot> Party,
    IReadOnlyList<CharacterControlState> CharacterControls, BattleSnapshot? Battle, WorldSnapshot? World = null,
    int GoldenKeyCount = 0, int BossKeyCount = 0, InnSnapshot? Inn = null,
    NarrativeSnapshot? Narrative = null, SpellPreparationSnapshot? SpellPreparation = null,
    PartyRestSnapshot? RestNotice = null, LevelUpPromptSnapshot? LevelUpPrompt = null,
    IReadOnlyList<SessionActivitySnapshot>? Activities = null,
    IReadOnlyList<SessionSoundSnapshot>? Sounds = null);

/// <summary>A null címzettlista közös hangot, a nem üres lista karakterhez kötött hallgatókat jelent.</summary>
public sealed record SessionSoundSnapshot(long Sequence, SoundEffect Effect,
    IReadOnlyList<CharacterId>? ListenerCharacterIds = null)
{
    public bool IsAudibleTo(CharacterId characterId) =>
        ListenerCharacterIds is null || ListenerCharacterIds.Contains(characterId);
}

public enum SessionActivityKind { Battle, Spell, Support, System }

public sealed record SessionActivitySnapshot(long Sequence, SessionActivityKind Kind, string Message,
    ConsoleColor Color, IReadOnlyList<CharacterId>? ListenerCharacterIds = null)
{
    public bool IsVisibleTo(CharacterId characterId) =>
        ListenerCharacterIds is null || ListenerCharacterIds.Contains(characterId);
}

public enum LevelUpPromptKind { Summary, PerkChoice, SpecializationChoice, SpellChoice }

public sealed record LevelUpPromptSnapshot(Guid PromptId, CharacterId CharacterId, string CharacterName,
    LevelUpPromptKind Kind, int PreviousLevel, int CurrentLevel, int VitalityGained, int ManaGained,
    IReadOnlyList<LevelUpChoiceSnapshot> Choices, string Message);

public sealed record LevelUpChoiceSnapshot(string Id, string Name, string Description);

public sealed record PartyRestSnapshot(Guid RestId, bool AtInn, IReadOnlyList<CharacterRestSnapshot> Characters);

public sealed record CharacterRestSnapshot(CharacterId CharacterId, string CharacterName, int HealedAmount,
    IReadOnlyList<string> RemovedNegativeStatuses);

public sealed record SpellPreparationSnapshot(Guid PromptId, CharacterId CharacterId, string CharacterName,
    int Capacity, IReadOnlyList<KnownSpellSnapshot> Spells, IReadOnlyList<string> SelectedSpellIds);

public enum NarrativeKind { CampaignIntroduction, BossIntroduction, TwelveKeys, CampaignFinale }

public sealed record NarrativeSnapshot(Guid NarrativeId, NarrativeKind Kind, string Title, string Subtitle,
    IReadOnlyList<string> Paragraphs, IReadOnlyList<PlayerId> AcknowledgedPlayerIds);

public enum InnVendorKind { Market, Witcher, Blacksmith, Armorer, WanderingMage }

public enum InnMenuOptionKind
{
    Rest, Market, Witcher, SecretStash, Blacksmith, Armorer, WanderingMage, Recruit, Rumors, Leave
}

public sealed record InnMenuOptionSnapshot(InnMenuOptionKind Kind, string Label, string Description,
    InnVendorKind? Vendor = null, bool LeaderOnly = false);

public sealed record LevelCompletionSnapshot(Guid CompletionId, int CompletedLevel, int BaseExperience,
    IReadOnlyList<LevelCompletionCharacterSnapshot> Survivors,
    IReadOnlyList<LevelCompletionFallenSnapshot> FallenCharacters);

public sealed record LevelCompletionCharacterSnapshot(string Name, ConsoleColor Color, int GainedExperience,
    int PreviousLevel, int CurrentLevel, int CurrentVitality, int MaximumVitality,
    int CurrentMana, int MaximumMana, bool UsesMana);

public sealed record LevelCompletionFallenSnapshot(string Name, string CharacterClassName);

public sealed record InnSnapshot(long Revision, int PartyGold, IReadOnlyList<InnVendorSnapshot> Vendors,
    IReadOnlyList<InnRumorSnapshot> Rumors, IReadOnlyList<InnTransactionSnapshot> Transactions,
    IReadOnlyList<InnSellPriceSnapshot> SellPrices,
    IReadOnlyList<InnMenuOptionSnapshot>? MenuOptions = null, string ArtisanNotice = "",
    int PartyCount = 0, int PartyFreeBackpackSlots = 0,
    LevelCompletionSnapshot? LevelCompletion = null);

public sealed record InnSellPriceSnapshot(string ItemDefinitionId, int Price);

public sealed record InnRumorSnapshot(string Title, IReadOnlyList<string> Lines, ConsoleColor Color);

public enum InnTransactionKind { Purchase, Sale }

public sealed record InnTransactionSnapshot(long Sequence, InnTransactionKind Kind, string ActorName,
    string ItemName, int Price, string InventoryOwnerName);

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
