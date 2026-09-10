using System.Text;
using KaoszRubin.Application;
using KaoszRubin.Combat;
using KaoszRubin.Data;
using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Infrastructure;

/// <summary>Részletes, hibabiztos diagnosztikai napló a fejlesztői harci tesztpályához.</summary>
public sealed class DeveloperBattleLog
{
    private readonly object _sync = new();
    private string? _filePath;
    private FileStream? _stream;
    private StreamWriter? _writer;

    public string? FilePath
    {
        get { lock (_sync) return _filePath; }
    }

    public bool IsActive => FilePath is not null;

    public void BeginScenario(DeveloperBattleTestOptions options, DeveloperBattleTestScenario scenario,
        IReadOnlyList<LiveCharacter> party, GameDataCatalog gameData,
        Func<LiveCharacter, NpcSpellcasterTactics>? tacticsFor = null)
    {
        lock (_sync)
        {
            if (!TryOpenNewLog("scenario")) return;
        }

        Append("SCENARIO", $"partyLevel={options.PartyLevel}; groups={options.EnemyGroupCount}; " +
                           $"membersPerGroup={options.EnemiesPerGroup}; map={scenario.Maze.Width}x{scenario.Maze.Height}; " +
                           $"leader={FormatPosition(scenario.LeaderPosition)}; corridor={FormatPosition(scenario.CorridorTopLeft)} " +
                           $"size={DeveloperBattleTestScenarioBuilder.CorridorWidth}x{DeveloperBattleTestScenarioBuilder.CorridorLength}");
        foreach (var character in party)
        {
            var abilities = character.EffectiveAbilities;
            var tactics = (tacticsFor?.Invoke(character) ??
                           NpcSpellcasterTactics.DefaultFor(character.CharacterClass.Id)).Normalize();
            Append("PARTY", $"id={character.Id}; name={character.Name}; class={character.CharacterClass.Name}({character.CharacterClass.Id}); " +
                            $"level={character.Level}; HP={character.CurrentVitality}/{character.MaximumVitality}; " +
                            $"MP={character.CurrentMana}/{character.MaximumMana}; STR={abilities.Strength}; DEX={abilities.Dexterity}; " +
                            $"HEA={abilities.Health}; INT={abilities.Intelligence}; behavior={character.NpcBehavior}; " +
                            $"weapons={JoinNames(character.ActiveWeapons.Where(weapon => weapon is not null).Select(weapon => weapon!.Name))}; " +
                            $"armor={character.Armor?.Name ?? "-"}; tactics={FormatTactics(tactics.StandardProfile)}; " +
                            $"unholyTactics={(tactics.UnholyProfile is { } unholy ? FormatTactics(unholy) : "-")}");
            foreach (var spell in character.MemorizedSpells)
            {
                var effects = gameData.GetSpellEffects(spell.Id).ToArray();
                var classification = NpcSpellTacticalClassifier.Classify(spell, effects);
                Append("SPELL", $"caster={character.Name}; id={spell.Id}; name={spell.Name}; level={spell.Level}; " +
                                $"mana={SpellcastingRules.EffectiveManaCost(character, spell)}; target={spell.TargetType}; " +
                                $"range={spell.Range}; area={spell.AreaRadius}; offensive={classification.IsOffensive}; " +
                                $"pattern={classification.AttackPattern}; roles={classification.Roles}; " +
                                $"effects={string.Join(',', effects.Select(effect => effect.Type))}");
            }
        }
        for (var index = 0; index < scenario.EnemyGroups.Count; index++)
        {
            var group = scenario.EnemyGroups[index];
            Append("ENEMY-GROUP", $"index={index + 1}; id={group.FirstOrDefault()?.GroupId ?? "-"}; " +
                                  $"count={group.Count}; totalStrength={NpcSpellPlanningPolicy.EnemyStrength(group.Select(enemy => enemy.Definition.Strength ?? 1))}; " +
                                  $"marker={FormatPosition(scenario.GroupMarkers[index].Position)}");
            foreach (var enemy in group)
                Append("ENEMY", $"group={index + 1}; id={enemy.Id}; definition={enemy.Definition.Id}; name={enemy.Name}; " +
                                $"position={FormatPosition(enemy.Position)}; strength={enemy.Definition.Strength ?? 1}; " +
                                $"tier={enemy.Definition.StrengthTier}; rank={enemy.Definition.Rank}; " +
                                $"HP={enemy.CurrentHitPoints}; armor={enemy.Definition.Armor}; speed={enemy.EffectiveSpeed}; " +
                                $"abilities={JoinNames(enemy.Definition.AbilityIds)}");
        }
    }

    public void BeginBattle(TeamBattleEncounter battle)
    {
        var recovered = false;
        lock (_sync)
        {
            if (_filePath is null)
            {
                if (!TryOpenNewLog("battle-recovery")) return;
                recovered = true;
            }
        }
        if (recovered)
            Append("SCENARIO-RECOVERY",
                "A mentésből betöltött tesztpályához nem tartozott aktív scenario-log; a csatanapló helyreállítva.");
        Append("BATTLE-START", $"battle={battle.Id}; center={FormatPosition(battle.Turns.Center)}; " +
                               $"radius={battle.Turns.Radius}; formation={battle.Formation?.Layout.ToString() ?? "none"}; " +
                               $"friendly={battle.Characters.Count}; hostile={battle.Enemies.Count}; " +
                               $"enemyStrength={NpcSpellPlanningPolicy.EnemyStrength(battle.Enemies.Select(enemy => enemy.Definition.Strength ?? 1))}");
        AppendBattleState(battle, "initial");
    }

    public void AppendBattleEntries(TeamBattleEncounter battle, IEnumerable<BattleLogEntry> entries)
    {
        foreach (var entry in entries)
        {
            Append("BATTLE-ENTRY", $"battle={battle.Id}; cycle={battle.Turns.Cycle}; turn={battle.Turns.TurnId}; " +
                                   $"action={battle.ActionNumber}; kind={entry.Kind}; message={Clean(entry.Message)}");
            if (entry.Details is not { } details) continue;
            Append("ACTION-DETAIL", $"actor={Clean(details.Actor)}; target={Clean(details.Target)}; " +
                                      $"summary={JoinNames(details.Summary)}; calculation={JoinNames(details.Calculation)}");
        }
        AppendBattleState(battle, "after-entry");
    }

    public void AppendBattleState(TeamBattleEncounter battle, string reason)
    {
        var current = battle.Turns.CurrentParticipant;
        Append("BATTLE-STATE", $"reason={reason}; battle={battle.Id}; cycle={battle.Turns.Cycle}; " +
                               $"turn={battle.Turns.TurnId}; action={battle.ActionNumber}; current={current?.Id.Value ?? "-"}");
        foreach (var participant in battle.Turns.Participants.OrderBy(participant => participant.Side)
                     .ThenBy(participant => participant.Id.Value, StringComparer.Ordinal))
        {
            if (battle.CharacterFor(participant.Id) is { } character)
            {
                var plan = battle.NpcSpellPlanFor(character);
                Append("STATE-ALLY", $"name={character.Name}; id={character.Id}; position={FormatPosition(participant.Position)}; " +
                                     $"state={participant.State}; eligibleFrom={participant.EligibleFromCycle}; " +
                                     $"initiative={participant.CurrentInitiative}; movement={participant.MovementAllowance}; " +
                                     $"HP={character.CurrentVitality}/{character.MaximumVitality}; MP={character.CurrentMana}/{character.MaximumMana}; " +
                                     $"engaged={battle.IsEngaged(character)}; offensiveCasts={battle.OffensiveSpellCastsFor(character)}; " +
                                     $"plan={FormatPlan(plan)}; statuses={JoinNames(character.Statuses.Select(status => status.Id))}; " +
                                     $"spellEffects={JoinNames(character.ActiveSpellEffects.Select(effect => $"{effect.Type}:{effect.Value}/{effect.RemainingRounds}"))}");
            }
            else if (battle.EnemyFor(participant.Id) is { } enemy)
                Append("STATE-ENEMY", $"name={enemy.Name}; id={enemy.Id}; position={FormatPosition(participant.Position)}; " +
                                      $"state={participant.State}; eligibleFrom={participant.EligibleFromCycle}; " +
                                      $"initiative={participant.CurrentInitiative}; movement={participant.MovementAllowance}; " +
                                      $"HP={enemy.CurrentHitPoints}/{enemy.Definition.HitPoints}; strength={enemy.Definition.Strength ?? 1}; " +
                                      $"tier={enemy.Definition.StrengthTier}; " +
                                      $"group={enemy.GroupId ?? "-"}; engaged={battle.IsEngaged(enemy)}; " +
                                      $"effects={JoinNames(enemy.ActiveSpellEffects.Select(effect => $"{effect.Type}:{effect.Value}/{effect.RemainingRounds}"))}");
        }
    }

    public void CompleteBattle(TeamBattleEncounter battle, string outcome)
    {
        AppendBattleState(battle, "final");
        Append("BATTLE-END", $"battle={battle.Id}; outcome={outcome}; cycles={battle.Turns.Cycle}; " +
                             $"actions={battle.ActionNumber}; kills={battle.Kills.Count}");
        FlushToDisk();
    }

    public void Append(string eventName, string details)
    {
        lock (_sync)
        {
            if (_filePath is null) return;
            try
            {
                EnsureWriter();
                _writer!.WriteLine($"{DateTimeOffset.Now:O} [{eventName}] {Clean(details)}");
            }
            catch (Exception exception)
            {
                Log.Error("battle-test-log.write-failed", exception);
                CloseWriter();
            }
        }
    }

    private void FlushToDisk()
    {
        lock (_sync)
        {
            if (_writer is null || _stream is null) return;
            try
            {
                _writer.Flush();
                _stream.Flush(flushToDisk: true);
            }
            catch (Exception exception)
            {
                Log.Error("battle-test-log.flush-failed", exception);
                CloseWriter();
            }
        }
    }

    private void EnsureWriter()
    {
        if (_writer is not null || _filePath is null) return;
        _stream = new FileStream(_filePath, FileMode.Append, FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);
        _writer = new StreamWriter(_stream, new UTF8Encoding(false)) { AutoFlush = true };
    }

    private bool TryOpenNewLog(string reason)
    {
        try
        {
            CloseWriter();
            Log.Initialize();
            var directory = Path.GetDirectoryName(Log.FilePath);
            if (string.IsNullOrWhiteSpace(directory))
                directory = Path.Combine(AppContext.BaseDirectory, "naplók");
            Directory.CreateDirectory(directory);
            _filePath = Path.Combine(directory,
                $"battle-test-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log");
            _stream = new FileStream(_filePath, FileMode.Create, FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete);
            _writer = new StreamWriter(_stream, new UTF8Encoding(false)) { AutoFlush = true };
            _writer.WriteLine("KÁOSZRUBIN — FEJLESZTŐI HARCI TESZTNAPLÓ");
            _writer.WriteLine($"{DateTimeOffset.Now:O} [LOG-START] reason={reason}");
            Log.Info("battle-test-log.created", $"reason={reason}; Napló: {_filePath}");
            return true;
        }
        catch (Exception exception)
        {
            CloseWriter();
            _filePath = null;
            Log.Error("battle-test-log.create-failed", exception);
            return false;
        }
    }

    private void CloseWriter()
    {
        try { _writer?.Dispose(); }
        catch { /* A diagnosztikai napló lezárása nem állíthatja meg a játékot. */ }
        _writer = null;
        _stream = null;
    }

    public static string FormatTactics(NpcSpellcasterCombatProfile tactics) =>
        $"limit={tactics.OffensiveSpellsPerBattle},min={tactics.MinimumEnemyStrength},full={tactics.FullOffenseEnemyStrength},fallback={tactics.ManaFallback}";

    private static string FormatPlan(NpcSpellPlan? plan) => plan is null ? "none" :
        $"{plan.SpellId}/{plan.Status}/target={plan.TargetEnemyId}/aim={FormatPosition(plan.TargetPosition)}/" +
        $"castFrom={FormatPosition(plan.RequiredCastingPosition)}/targets={plan.ExpectedTargetCount}/utility={plan.ExpectedUtility:F2}";

    private static string FormatPosition(Position? position) => position is { } value
        ? $"({value.X},{value.Y})"
        : "-";

    private static string JoinNames(IEnumerable<string> values) =>
        string.Join(',', values.Select(Clean).DefaultIfEmpty("-"));

    private static string Clean(string? value) => (value ?? "-")
        .Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/');
}
