using KaoszRubin.Application.Quests;
using KaoszRubin.Data;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Quests;
using KaoszRubin.World;

namespace KaoszRubin.Tests.Quests;

internal static class RodericReworkTests
{
    private static GameDataCatalog Data() => CsvGameDataLoader.Load(
        Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    public static void FiveQuestsFollowTheStory()
    {
        var data = Data();
        var fixture = new QuestTestFixture(data.Quests.All.ToArray());
        var quests = fixture.Manager.Roderic.Quests;
        void State(QuestStoryState state) => fixture.StoryStates[(QuestNpcId.SirRoderic, default)] = state;
        void Kill(string id) => fixture.Manager.RegisterKill(new ConfiguredEnemy(new(2, 2), data.GetEnemy(id)));
        State(QuestStoryState.ProofActive);
        quests.FightingOnTheSameSide.Activate();
        Kill(MonsterIds.Goblin);
        Check(quests.FightingOnTheSameSide.Progress == 0, "Nem élőholt is bizonyítéknak számított.");
        for (var index = 0; index < 8; index++) Kill(index % 2 == 0 ? MonsterIds.Csontváz : MonsterIds.Zombi);
        quests.FightingOnTheSameSide.Complete();
        Check(RodericStoryProgression.NextState("PROOF_ACTIVE", quests) == "PROOF_COMPLETE", "A bizonyítás nem zárult le.");

        State(QuestStoryState.InsigniasActive);
        quests.FallenComradesInsignia.Activate();
        fixture.Inventory[data.GetItemDefinition("T026")] = 3;
        fixture.Manager.SynchronizeCollectQuests();
        quests.FallenComradesInsignia.Complete();
        Check(fixture.Inventory[data.GetItemDefinition("T026")] == 0 &&
            RodericStoryProgression.NextState("INSIGNIAS_ACTIVE", quests) == "CONFESSION", "A jelvények leadása hibás.");

        State(QuestStoryState.Following);
        fixture.Manager.Roderic.ActivateAvailableQuests();
        Kill(MonsterIds.ÉlőholtPátriárka);
        Check(quests.PatriarchsShadows.Progress == 0, "Roderic részvétele nélkül haladt a feladat.");
        fixture.ParticipatingInCombat = true;
        Kill(MonsterIds.Csontváz);
        Check(quests.PatriarchsShadows.Progress == 0, "Más élőholt is pátriárkának számított.");
        Kill(MonsterIds.ÉlőholtPátriárka);
        Check(RodericStoryProgression.NextState("FOLLOWING", quests) is null, "Az első pátriárka lezárta a történeti szakaszt.");
        Kill(MonsterIds.ÉlőholtPátriárka);
        quests.PatriarchsShadows.Complete();
        Check(RodericStoryProgression.NextState("FOLLOWING", quests) == "TRUSTED", "Hiányzó ereklyefelvezetés.");

        State(QuestStoryState.RelicsActive);
        quests.OrderRelics.Activate();
        var chest = new TreasureChest(new(3, 3), data.GetQuestChest(new("RODERIC_ORDER_RELICS")));
        var expectedRemaining = chest.RemainingItems.Sum(item => item.Quantity);
        var result = new QuestChestService(fixture.Manager).Collect(chest, _ => false, _ => { });
        quests.OrderRelics.Complete();
        Check(result.RemainingCount == expectedRemaining &&
            RodericStoryProgression.NextState("RELICS_ACTIVE", quests) == "RELICS_COMPLETE",
            "A teli inventory megakasztotta az ereklye történetét.");
        State(QuestStoryState.MalrecApproach);
        quests.OathbreakerKnight.Activate();
        Kill(MonsterIds.SirMalrec);
        Check(!quests.OathbreakerKnight.IsReadyToTurnIn, "A Malrec-párbeszéd előtt teljesült a cél.");
        State(QuestStoryState.MalrecFight);
        Kill(MonsterIds.SirMalrec);
        quests.OathbreakerKnight.Complete();
        Check(RodericStoryProgression.NextState("MALREC_FIGHT", quests) == "MALREC_DEFEATED", "Hiányzó hazatérés.");
        Check(data.GetNpcStoryChoices("RODERIC_OATH", "CACHE_BLOCKED").Count == 0, "Megmaradt a CACHE-szál.");
    }

    public static void EncountersAndGatesAreExact()
    {
        var data = Data();
        var level = MazeLevelConfigurations.Get(5);
        Check(level.QuestDoorRequirements.Count == 3 &&
            level.QuestDoorRequirements["RODERIC_PATRIARCHS"] == QuestId.RodericSharedBladeTrial &&
            level.QuestDoorRequirements["RODERIC_RELICS"] == QuestId.RodericOrderRelics &&
            level.QuestRoomEnemyEncounters.Single(e => e.EnemyId == MonsterIds.ÉlőholtPátriárka).Count == 2 &&
            level.QuestRoomEnemyEncounters.Single(e => e.EnemyId == MonsterIds.CsontvázLovag).Count == 3,
            "Hibás fix ellenfélszám vagy questkapu.");
        Check(level.RoomEncounters.Concat(level.CorridorEncounters).SelectMany(e => e.Members).All(m =>
            m.EnemyId != MonsterIds.ÉlőholtPátriárka && m.EnemyId != MonsterIds.CsontvázLovag),
            "Történeti ellenfél véletlen találkozásként is megjelenhet.");
        var maze = new Maze(9, 9);
        maze.Carve(new(3, 3));
        maze.AddCorpse(new MonsterCorpse(new(3, 3), "Pátriárka", MonsterIds.ÉlőholtPátriárka));
        var templates = new List<KaoszRubin.Application.ExpeditionEnemyTemplate>();
        KaoszRubin.Application.DungeonExpeditionCoordinator.CaptureExpeditionEnemyTemplates(templates, maze, data);
        Check(templates.Count == 0, "A visszatérő expedíció újabb pátriárkát támasztana fel.");
        Check(data.GetEnemy(MonsterIds.ÉlőholtPátriárka).Traits.HasFlag(EnemyTraits.Undead), "A pátriárka nem élőholt.");
    }

    public static void OldCampaignAdvancesWithoutRewardsOrRebuilding()
    {
        foreach (var story in new[] { "INITIAL", "PROOF_ACTIVE", "FOLLOWING", "CACHE_BLOCKED" })
        {
            var save = new GameSaveData { Version = 24, Quests = new() };
            save.Maze.Npcs.Add(new(new(2, 2), "NPC021", 0, default, false, true, "", default,
                StoryId: "RODERIC_OATH", StoryStateId: story));
            GameSaveFormat.MigrateToCurrent(save);
            Check(save.Version == GameSaveFormat.CurrentVersion && save.Maze.Npcs.Single().StoryStateId == "MALREC_READY" &&
                save.Quests.States.Count == 4 && save.Quests.States.All(s => s.State == "completed" && s.CompletionCount == 1) &&
                save.Maze.Chests.Count == 0 && save.Maze.Enemies.Count == 0,
                "A régi mentés új pályatartalmat kapott vagy megakadt.");
            GameSaveFormat.MigrateToCurrent(save);
            Check(save.Quests.States.Count == 4 && save.Quests.MigrationNotes.Count == 1, "A migráció nem idempotens.");
        }
        var historical = new GameSaveData { Version = 24, Quests = new() };
        historical.Quests.States.Add(new("NPCQ038", 0, "completed", 3, 1, "Régi pengepróba", "", "Sir Roderic", 650));
        GameSaveFormat.MigrateToCurrent(historical);
        var restored = new KaoszRubin.Infrastructure.Quests.QuestSaveAdapter(Data(), new())
            .PrepareRestore(historical, new KaoszRubin.Domain.Characters.CharacterRoster());
        Check(restored.Single(s => s.QuestId == QuestId.RodericSharedBladeTrial).Progress == 2 &&
            restored.Count == 4, "A már távozott Roderic régi questelőzménye nem tölthető vissza.");
        var current = new GameSaveData { Version = 24, LocationKind = AdventureLocationKind.Quest, Quests = new() };
        current.Maze.Npcs.Add(new(new(2, 2), "NPC021", 0, default, false, true, "", default,
            StoryId: "RODERIC_OATH", StoryStateId: "MALREC_FIGHT"));
        GameSaveFormat.MigrateToCurrent(current);
        Check(current.Maze.Npcs.Single().StoryStateId == "MALREC_FIGHT", "A folyó Malrec-harc visszaugrott.");
    }
}
