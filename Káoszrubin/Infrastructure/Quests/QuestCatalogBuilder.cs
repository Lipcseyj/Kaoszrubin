using KaoszRubin.Application.Quests;
using KaoszRubin.Data;
using KaoszRubin.Domain;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Quests;
using static KaoszRubin.Domain.Quests.QuestObjective;

namespace KaoszRubin.Infrastructure.Quests;

/// <summary>
/// A legacy GameDataCatalog questadataiból felépíti az új,
/// erősen tipizált QuestCatalogot.
///
/// A legacy string azonosítók ezen az osztályon túl
/// nem kerülhetnek a quest-rendszerbe.
/// </summary>
public sealed class QuestCatalogBuilder
{
    private readonly GameDataCatalog _gameData;

    public QuestCatalogBuilder(GameDataCatalog gameData)
    {
        ArgumentNullException.ThrowIfNull(gameData);

        _gameData = gameData;
    }

    public QuestCatalog Build()
    {
        var definitions = _gameData.NpcQuests
            .Select(CreateDefinition)
            .ToArray();

        return new QuestCatalog(definitions);
    }

    private QuestDefinition CreateDefinition(
        NpcQuestDefinition source)
    {
        var id =
            LegacyQuestIdMap.ToQuestId(source.Id);

        var giver =
            LegacyNpcIdMap.ToQuestNpcId(source.NpcId);

        var npcDefinition =
            _gameData.GetNpc(source.NpcId);

        var scope = npcDefinition.Unique
            ? QuestScope.Global
            : QuestScope.PerNpcInstance;

        return new QuestDefinition(
            Id: id,
            Giver: giver,
            Title: source.Title,
            Description: source.Description,
            Objective: CreateObjective(source, giver, id),
            ExperienceReward: source.ExperienceReward,
            Scope: scope,
            RepeatPolicy: QuestRepeatPolicy.Once,
            ActivationRequirement:
                CreateStoryActivationRequirement(id) ?? CreateActivationRequirement(source),
            FixedRewardItem:
                ResolveRewardItem(source.RewardItemId),
            FixedRewardItemCount:
                source.RewardItemCount,
            RandomRewardCount:
                source.RandomRewardCount,
            ActivationKind: id is QuestId.RodericTheDeadAreNotPrey or
                QuestId.RodericFallenComradesInsignia or QuestId.RodericOathbreakerKnight
                    ? QuestActivationKind.Story : QuestActivationKind.Offered);
    }

    // A történeti választás a hatás végrehajtása ELŐTT állítja át az NPC állapotát.
    // A Malrec-küldetés a helyszínre induláskor nyílik meg, nem már TRUSTED állapotban.
    private static QuestActivationRequirement? CreateStoryActivationRequirement(QuestId id) => id switch
    {
        QuestId.RodericTheDeadAreNotPrey => new QuestActivationRequirement.StoryStateEquals(QuestStoryState.ProofActive),
        QuestId.RodericFallenComradesInsignia => new QuestActivationRequirement.StoryStateEquals(QuestStoryState.InsigniasActive),
        QuestId.RodericSharedBladeTrial => new QuestActivationRequirement.StoryStateEquals(QuestStoryState.Following),
        QuestId.RodericOathbreakerKnight => new QuestActivationRequirement.StoryStateEquals(QuestStoryState.MalrecApproach),
        _ => null
    };

    private QuestObjective CreateObjective(NpcQuestDefinition source, QuestNpcId giver, QuestId questId)
    {
        return source.Type switch
        {
            NpcQuestType.Collect =>
                new QuestObjective.CollectItem(
                    _gameData.GetItemDefinition(source.TargetId),
                    source.RequiredCount),

            NpcQuestType.Kill =>
                CreateKillObjective(source, questId),

            NpcQuestType.KillWithFollower =>
                CreateKillWithFollowerObjective(
                    source,
                    giver),

            NpcQuestType.Explore =>
                new QuestObjective.ExploreLocation(
                    MapLocation(source.TargetId)),

            NpcQuestType.Disarm =>
                CreateDisarmObjective(source),

            NpcQuestType.OpenChest =>
                CreateOpenChestObjective(source),

            NpcQuestType.Escort =>
                new QuestObjective.EscortNpc(
                    giver,
                    MapLocation(source.TargetId)),

            _ => throw new InvalidDataException(
                $"Nem támogatott quest típus: '{source.Type}' " +
                $"({source.Id}).")
        };
    }

    //Ez az egyetlen quest-specifikus workaroundunk jelenleg.
    //    Később érdemes lehet a CSV - t kibővíteni például:
    //SzükségesKövetőNpc
    //SzükségesKövetőÁllapot
    //MaxKövetőTávolság
    //oszlopokkal, és akkor ez a special case is eltűnik.Most viszont egy helyre zártuk a legacy kompatibilitást, ami már nagy előrelépés.
    private QuestObjective CreateKillObjective(NpcQuestDefinition source, QuestId questId)
    {
        var enemy =
            _gameData.GetEnemy(source.TargetId);

        QuestFollowerRequirement? followerRequirement =
            questId == QuestId.RodericOathbreakerKnight
                ? new QuestFollowerRequirement(
                    QuestNpcId.SirRoderic,
                    QuestStoryState.MalrecFight,
                    MaximumDistance: 6)
                : null;

        return new QuestObjective.KillEnemy(
            enemy,
            source.RequiredCount,
            followerRequirement);
    }

    private static QuestObjective CreateKillWithFollowerObjective(
        NpcQuestDefinition source,
        QuestNpcId giver)
    {
        var traits =
            MapLegacyEnemyTraits(source.TargetId);

        return new QuestObjective.KillEnemyWithTraits(
            traits,
            source.RequiredCount,
            new QuestFollowerRequirement(giver));
    }

    private static QuestObjective CreateDisarmObjective(
        NpcQuestDefinition source)
    {
        EnsureAnyTarget(source);

        return new QuestObjective.DisarmTraps(
            source.RequiredCount);
    }

    private static QuestObjective CreateOpenChestObjective(
        NpcQuestDefinition source)
    {
        EnsureAnyTarget(source);

        return new QuestObjective.OpenChests(
            source.RequiredCount);
    }

    private static QuestLocation MapLocation(
        string legacyTargetId)
    {
        return legacyTargetId.ToUpperInvariant() switch
        {
            "EXIT" => QuestLocation.Exit,

            _ => throw new InvalidDataException(
                $"Ismeretlen legacy quest-helyszín: " +
                $"'{legacyTargetId}'.")
        };
    }

    private static EnemyTraits MapLegacyEnemyTraits(
        string legacyTargetId)
    {
        if (string.Equals(
                legacyTargetId,
                MonsterAbilityIds.Undead,
                StringComparison.OrdinalIgnoreCase))
        {
            return EnemyTraits.Undead;
        }

        if (string.Equals(
                legacyTargetId,
                MonsterAbilityIds.Demonic,
                StringComparison.OrdinalIgnoreCase))
        {
            return EnemyTraits.Demonic;
        }

        if (string.Equals(
                legacyTargetId,
                MonsterAbilityIds.Flying,
                StringComparison.OrdinalIgnoreCase))
        {
            return EnemyTraits.Flying;
        }

        throw new InvalidDataException(
            $"Ismeretlen legacy enemy trait: '{legacyTargetId}'.");
    }

    private static QuestActivationRequirement?
        CreateActivationRequirement(
            NpcQuestDefinition source)
    {
        if (string.IsNullOrWhiteSpace(
                source.RequiredStoryStateId))
        {
            return null;
        }

        var requiredState =
            LegacyQuestStoryStateMap.ToQuestStoryState(
                source.RequiredStoryStateId);

        return new QuestActivationRequirement.StoryStateEquals(
            requiredState);
    }

    private IItemDefinition? ResolveRewardItem(
        string? rewardItemId)
    {
        return string.IsNullOrWhiteSpace(rewardItemId)
            ? null
            : _gameData.GetItemDefinition(rewardItemId);
    }

    private static void EnsureAnyTarget(
        NpcQuestDefinition source)
    {
        if (!string.Equals(
                source.TargetId,
                "ANY",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"A(z) '{source.Id}' {source.Type} quest " +
                $"célja '{source.TargetId}', de jelenleg csak " +
                "'ANY' támogatott.");
        }
    }
}
