using System.Reflection;
using Moq;

internal static partial class Program
{
    static void BattleCoopUpdatesWaitForStableState()
    {
        var root = CoopFixtureFactory.CreateTemporaryWorkspaceRoot(null);
        var previousSettings = Game.StaticGameSettings;
        try
        {
            var fixture = CoopFixtureFactory.Create(root);
            fixture.GameSettingsService.Settings.MusicEnabled = false;
            fixture.GameSettingsService.Settings.SoundEffectsEnabled = false;
            using var music = new BackgroundMusicPlayer(fixture.GameSettingsService.Settings);
            var game = new Game(fixture.Catalog, fixture.HostRoster, fixture.HostLeader,
                fixture.GameSaveService, music, gameSettings: fixture.GameSettingsService);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            void Set(string name, object? value) => typeof(Game).GetField(name, flags)!.SetValue(game, value);
            void Invoke(string name, params object[] args)
            {
                try { typeof(Game).GetMethod(name, flags)!.Invoke(game, args); }
                catch (TargetInvocationException exception) when (exception.InnerException is not null)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                }
            }
            var maze = new Maze(9, 9);
            for (var y = 1; y < 8; y++)
            for (var x = 1; x < 8; x++) maze.Carve(new Position(x, y));
            var origin = new Position(4, 4);
            var fog = new FogOfWar(9, 9, 3);
            fog.RevealFrom(maze, origin);
            Set("_maze", maze);
            Set("_player", new Player(origin, fixture.HostLeader));
            Set("_fogOfWar", fog);
            Set("_dungeonLevel", new DungeonLevel([new DungeonArea("test", maze, fog)], "test", "test"));
            var snapshots = new List<SessionSnapshot>();
            var host = new Mock<ICoopHostLoop>();
            host.Setup(value => value.ShouldPublish(It.IsAny<DateTime>())).Returns(true);
            host.Setup(value => value.TryPublish(It.IsAny<SessionSnapshot>()))
                .Callback<SessionSnapshot>(snapshots.Add).Returns(true);
            Set("_activeCoopHost", host.Object);
            using var sound = (SoundEffects)typeof(Game).GetField("_soundEffects", flags)!.GetValue(game)!;

            foreach (var quick in new[] { true, false })
            {
                var first = CreateEnemyAt(new Position(4, 3), "E-FIRST");
                var second = CreateEnemyAt(new Position(5, 4), "E-SECOND");
                var system = CreateBattleSystem(1811);
                var battle = new BattleEncounter(origin,
                    [new BattleCharacterParticipant(fixture.HostLeader, origin,
                        TacticalParticipantKind.PartyMember, 10, 3, 1,
                        system.PrepareCharacter(fixture.HostLeader).Runtime)],
                    [new BattleEnemyParticipant(first, 5, 2, 1), new BattleEnemyParticipant(second, 4, 2, 1)],
                    fixture.HostLeader.Id, first.Id);
                battle.Turns.StartTurns();
                Set("_activeBattle", battle);
                Set("_battleStarted", true);
                Set("_isQuickBattle", quick);
                game.Session.SetPhase(GameSessionPhase.Battle);
                if (!quick)
                    game.Session.SetBattlePrompt(battle.Id, battle.Turns.TurnId, fixture.HostLeader.Id,
                        [BattleActionKind.PhysicalAttack]);

                first.ReceiveSpellDamage(first.CurrentHitPoints);
                battle.MarkDefeated(first);
                var before = snapshots.Count;
                // Az öléshez tartozó questfrissítés ugyanitt kér azonnali publikálást:
                // gyorsharcban még nincs prompt, taktikai harcban már elavult lehet.
                Invoke("RequestCoopSnapshotPublish");
                Assert(snapshots.Count == before &&
                       (bool)typeof(Game).GetField("_coopSnapshotDirty", flags)!.GetValue(game)!,
                    "Az akció közbeni frissítés publikált vagy elveszett.");

                var projected = (BattleSnapshot)typeof(Game).GetMethod("CreateBattleSnapshot", flags)!
                    .Invoke(game, [battle])!;
                game.Session.SetBattlePrompt(projected.BattleId, projected.TurnId,
                    projected.ActingCharacterId, projected.AllowedActions);
                Invoke("TryPublishScheduledCoopSnapshot", DateTime.UtcNow);
                Assert(snapshots.Count == before + 1 && snapshots[^1].Battle?.BattleId == battle.Id,
                    "A stabil harci prompt nem küldte el az elhalasztott állapotot.");

                if (!quick && !Console.IsOutputRedirected)
                {
                    // Egy automatikus ellenfél-akció után a következő kör promptja
                    // már a megjelenítési késleltetés alatt is legyen szinkronban.
                    // A ContinueBattle a konzolos fókuszt is rajzolja, ezért ezt
                    // a részt valódi terminálban futtatjuk.
                    do { battle.AdvanceTurn(); }
                    while (battle.LastActionActorId != CombatantId.ForEnemy(second.Id));
                    fixture.GameSettingsService.Settings.CombatSpeed = CombatSpeed.PauseBeforePlayerAction;
                    fixture.GameSettingsService.Settings.CombatDelayMilliseconds = 10_000;
                    Set("_battleLogCycle", battle.Turns.Cycle);
                    Invoke("ContinueBattle");
                    var delayed = game.CreateSessionSnapshot();
                    Assert(delayed.Battle?.TurnId == battle.Turns.TurnId &&
                           typeof(Game).GetField("_automaticBattleResumeUtc", flags)!.GetValue(game) is DateTime,
                        "A késleltetett automatikus kör elavult session-promptot hagyott.");
                }

                game.Session.EndBattle(battle.Id);
                Set("_activeBattle", null);
                Set("_battleStarted", false);
                Set("_isQuickBattle", false);
                game.Session.SetPhase(GameSessionPhase.Exploration);
                Invoke("RequestCoopSnapshotPublish");
                Assert(snapshots.Count == before + 2 && snapshots[^1].Battle is null &&
                       snapshots[^1].Phase == GameSessionPhase.Exploration,
                    "A csata lezárása után nem érkezett felfedezési snapshot.");
            }
        }
        finally
        {
            Game.StaticGameSettings = previousSettings;
            Directory.Delete(root, recursive: true);
        }
    }
}
