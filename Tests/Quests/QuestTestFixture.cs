using KaoszRubin.Application;
using KaoszRubin.Application.Quests;
using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Quests;
using KaoszRubin.World;

namespace KaoszRubin.Tests.Quests;

// CSV, Game, renderer és valódi inventory nélkül futó quest-szolgáltatáslánc.
internal sealed class QuestTestFixture : IQuestWorldContext, IQuestRewardContext, IQuestNpcConversationService
{
    public static readonly IItemDefinition Supplies = new MiscItemDefinition("TEST-SUPPLIES", "Készlet", "Tesztkészlet", 1);
    public static readonly IItemDefinition Reward = new MiscItemDefinition("TEST-REWARD", "Jutalom", "Tesztjutalom", 1);

    public QuestManager Manager { get; }
    public Dictionary<IItemDefinition, int> Inventory { get; } = [];
    public Dictionary<(QuestNpcId, QuestNpcInstanceId), QuestStoryState> StoryStates { get; } = [];
    public List<IItemDefinition> StoredRewards { get; } = [];
    public List<IItemDefinition> DroppedRewards { get; } = [];
    public List<(QuestNpcId Npc, QuestNpcInstanceId Instance)> Conversations { get; } = [];
    public List<(QuestNpcId Npc, QuestNpcInstanceId Instance, Enemy Enemy, int Distance)> CombatChecks { get; } = [];
    public bool ParticipatingInCombat { get; set; }
    public bool AliveAndFollowing { get; set; }
    public bool RejectConsumption { get; set; }
    public bool BackpackFull { get; set; }
    public int ConsumptionAttempts { get; private set; }
    public int ConsumedItems { get; private set; }
    public int RandomRewardRolls { get; private set; }
    public LiveCharacter SelectedCharacter { get; } = CreateCharacter("Vezér");
    public LiveCharacter Companion { get; } = CreateCharacter("Társ");
    public IEnumerable<LiveCharacter> PartyMembers => [SelectedCharacter, Companion];

    public QuestTestFixture(params QuestDefinition[] definitions)
    {
        var catalog = new QuestCatalog(definitions);
        var store = new QuestStateStore(catalog);
        var progress = new QuestProgressEngine(catalog, store, this);
        var gameData = new GameDataCatalog
        {
            VitalityGrowthByHealth = new Dictionary<int, ValueRange> { [1] = new(1, 1) },
            ManaGrowthByIntelligence = new Dictionary<int, ValueRange> { [1] = new(1, 1) },
            CharacterResourceGrowthByClass = new Dictionary<string, CharacterResourceGrowthDefinition>
            {
                [CharacterClassIds.Harcos] = new(CharacterClassIds.Harcos, 0, 0, 0)
            }
        };
        var rewards = new QuestRewardService(new CharacterProgressionService(gameData, new Random(17)), this);
        Manager = new QuestManager(catalog, store, new QuestAvailabilityService(catalog, store, this),
            progress, new QuestCompletionProcessor(catalog, store, progress, this, rewards), this);
    }

    public static QuestDefinition Define(QuestObjective objective,
        QuestId id = QuestId.HerbalistHealingSupplies,
        QuestNpcId giver = QuestNpcId.WanderingHerbalist,
        QuestScope scope = QuestScope.Global) =>
        new(id, giver, "Tesztküldetés", "A teszt céljának teljesítése.", objective, 20,
            scope, QuestRepeatPolicy.Once, FixedRewardItem: Reward, FixedRewardItemCount: 1);

    private static LiveCharacter CreateCharacter(string name) => new(name,
        new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero),
        new CharacterClassDefinition(CharacterClassIds.Harcos, "Harcos", PrimaryAbilities.Zero, false, 1.0),
        new PrimaryAbilities(5, 5, 5, 5), 20, 0, 0, 0);

    public int CountPartyItem(IItemDefinition item) => Inventory.GetValueOrDefault(item);

    public bool TryConsumePartyItem(IItemDefinition item, int amount)
    {
        ConsumptionAttempts++;
        if (RejectConsumption || CountPartyItem(item) < amount) return false;
        Inventory[item] -= amount;
        ConsumedItems += amount;
        return true;
    }

    public QuestStoryState GetNpcStoryState(QuestNpcId npcId, QuestNpcInstanceId instanceId) =>
        StoryStates.GetValueOrDefault((npcId, instanceId));

    public bool IsNpcParticipatingInCombat(QuestNpcId npcId, QuestNpcInstanceId instanceId,
        Enemy defeatedEnemy, int maximumDistance)
    {
        CombatChecks.Add((npcId, instanceId, defeatedEnemy, maximumDistance));
        return ParticipatingInCombat;
    }

    public bool IsNpcAliveAndFollowing(QuestNpcId npcId, QuestNpcInstanceId instanceId) => AliveAndFollowing;

    public IItemDefinition? RollRandomReward(int experienceReward)
    {
        RandomRewardRolls++;
        return Reward;
    }

    public bool TryStoreItem(IItemDefinition item, out string ownerName)
    {
        ownerName = SelectedCharacter.Name;
        if (BackpackFull) return false;
        StoredRewards.Add(item);
        return true;
    }

    public void DropItem(IItemDefinition item) => DroppedRewards.Add(item);
    public void StartConversation(QuestNpcId npcId, QuestNpcInstanceId instanceId = default) =>
        Conversations.Add((npcId, instanceId));
}
