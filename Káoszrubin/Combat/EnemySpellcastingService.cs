using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Magic;
using KaoszRubin.World;

namespace KaoszRubin.Combat;

public sealed record EnemySpellPlan(SpellDefinition Spell, Position TargetPosition,
    IReadOnlyList<LiveCharacter> HostileTargets, IReadOnlyList<Enemy> AlliedTargets, int Score,
    IReadOnlyList<Enemy>? FriendlyFireTargets = null,
    IReadOnlyDictionary<CharacterId, Position>? HostilePositions = null);

/// <summary>Az ellenséges varázslók frakcióhelyes célpontválasztása és hatásfeloldása.</summary>
public sealed class EnemySpellcastingService(GameDataCatalog gameData, Random random)
{
    public EnemySpellPlan? SelectSpell(Enemy caster, IReadOnlyList<Enemy> allies,
        IReadOnlyList<(LiveCharacter Character, Position Position)> hostiles,
        Func<Position, Position, int, bool>? canSee = null, Maze? maze = null)
    {
        var profile = caster.Definition.SpellcasterProfile;
        if (profile is null || hostiles.Count == 0) return null;

        var reserve = profile.MaximumMana * profile.ManaReservePercent / 100;
        var candidates = new List<EnemySpellPlan>();
        foreach (var spellId in profile.SpellIds)
        {
            var spell = gameData.GetSpell(spellId);
            if (spell.ManaCost > caster.CurrentMana || !caster.IsSpellReady(spell.Id)) continue;
            var plan = BuildPlan(caster, allies, hostiles, spell, canSee, maze);
            if (plan is null) continue;
            plan = plan with { Score = ApplyStyle(profile.Style, spell, plan.Score) };
            if (plan.Score <= 0) continue;
            var urgent = plan.Score >= 140;
            if (!urgent && caster.CurrentMana - spell.ManaCost < reserve) continue;
            candidates.Add(plan with
            {
                Score = plan.Score * profile.CastingChancePercent / 100 - spell.ManaCost
            });
        }
        return candidates.OrderByDescending(candidate => candidate.Score).FirstOrDefault();
    }

    private int ApplyStyle(EnemySpellcastingStyle style, SpellDefinition spell, int score)
    {
        var effects = gameData.GetSpellEffects(spell.Id);
        var damage = effects.Any(effect => effect.Type is SpellEffectType.Damage or SpellEffectType.ChainDamage);
        var control = effects.Any(effect => effect.Type is SpellEffectType.SpeedPenalty or SpellEffectType.SkipAlternate ||
                                            (effect.Type is SpellEffectType.HitBonus or SpellEffectType.VisionBonus &&
                                             effect.Value < 0));
        var support = effects.Any(effect => effect.Type is SpellEffectType.Heal or SpellEffectType.DefenseBonus or
                                            SpellEffectType.PhysicalReduction ||
                                            (effect.Type is SpellEffectType.HitBonus or SpellEffectType.DamageBonus or
                                                SpellEffectType.InitiativeBonus && effect.Value > 0));
        var percent = style switch
        {
            EnemySpellcastingStyle.Artillery when damage => 125,
            EnemySpellcastingStyle.Controller when control => 130,
            EnemySpellcastingStyle.Support when support => 130,
            EnemySpellcastingStyle.BattleMage when spell.TargetType == SpellTargetType.Self => 120,
            EnemySpellcastingStyle.Necromancer when damage || control => 115,
            _ => 100
        };
        return score * percent / 100;
    }

    public BattleLogEntry Execute(Enemy caster, EnemySpellPlan plan, int combatFailureChance = 0,
        Maze? maze = null)
    {
        if (!caster.SpendMana(plan.Spell.ManaCost))
            return new BattleLogEntry($"{caster.Name} nem tudja befejezni a varázslatot.", BattleLogKind.Information);
        caster.StartSpellCooldown(plan.Spell.Id,
            Math.Max(1, caster.Definition.SpellcasterProfile?.SpellCooldownRounds ?? 1));
        var failureRoll = combatFailureChance > 0 ? random.Next(1, 101) : 101;
        if (failureRoll <= combatFailureChance)
            return new BattleLogEntry(
                $"💫 {caster.Name} varázslata meghiúsul a közelharci lekötésben: {plan.Spell.Name} — " +
                $"kockázat {combatFailureChance}%, dobás {failureRoll}; -{plan.Spell.ManaCost} manna.",
                BattleLogKind.Information);

        var notes = new List<string>();
        foreach (var effect in gameData.GetSpellEffects(plan.Spell.Id))
        {
            if (random.Next(100) >= effect.ChancePercent) continue;
            if (effect.Type == SpellEffectType.Storm && plan.Spell.HasAreaImpact &&
                effect.Dice is { } stormDice && maze is not null)
            {
                var intelligence = caster.Definition.SpellcasterProfile!.Intelligence;
                maze.AddStormZone(new ActiveStormZone(Guid.NewGuid(), plan.Spell.Id,
                    plan.TargetPosition, SpellAreaFootprint.GetCells(plan.Spell, caster.Position,
                        plan.TargetPosition, maze).ToArray(), Math.Max(1, effect.Duration), stormDice,
                    effect.Value + (int)Math.Round(intelligence * effect.IntelligenceMultiplier) +
                    caster.Definition.StrengthTier * effect.LevelMultiplier,
                    10 + intelligence / 2 + plan.Spell.Level, effect.Resolution, EnemyCasterId: caster.Id));
                notes.Add($"vihar a területen ({effect.Duration} kör)");
            }
            else ApplyEffect(caster, plan, effect, notes);
        }
        var result = notes.Count == 0 ? "a célpont ellenáll" : string.Join(", ", notes);
        return new BattleLogEntry(
            $"✨ {caster.Name} elmondja: {plan.Spell.Name} — {result}.",
            BattleLogKind.EnemyAttack);
    }

    private EnemySpellPlan? BuildPlan(Enemy caster, IReadOnlyList<Enemy> allies,
        IReadOnlyList<(LiveCharacter Character, Position Position)> hostiles, SpellDefinition spell,
        Func<Position, Position, int, bool>? canSee, Maze? maze)
    {
        bool InRange(Position position) => TacticalDistance.Between(caster.Position, position) <= spell.Range &&
            (!spell.RequiresLineOfSight || canSee?.Invoke(caster.Position, position, spell.Range) != false);
        var effects = gameData.GetSpellEffects(spell.Id);
        var isHeal = effects.Any(effect => effect.Type == SpellEffectType.Heal);
        var harmful = effects.Any(effect => effect.Type is SpellEffectType.Damage or SpellEffectType.ChainDamage or
            SpellEffectType.Burning or SpellEffectType.Storm or SpellEffectType.SpeedPenalty or
            SpellEffectType.SkipAlternate || effect.Type == SpellEffectType.HitBonus && effect.Value < 0 ||
            effect.Type == SpellEffectType.VisionBonus && effect.Value < 0);

        if (spell.TargetType is SpellTargetType.Enemy)
        {
            var target = hostiles.Where(item => InRange(item.Position))
                .OrderBy(item => HasAllHarmfulEffects(item.Character, effects))
                .ThenBy(item => item.Character.CurrentVitality)
                .ThenByDescending(item => item.Character.EffectiveAbilities.Intelligence).FirstOrDefault();
            return target.Character is null ? null : new EnemySpellPlan(spell, target.Position,
                [target.Character], [], Score(effects, 1, target.Character.CurrentVitality));
        }
        if (spell.TargetType is SpellTargetType.Area or SpellTargetType.Direction)
        {
            var centers = spell.TargetType == SpellTargetType.Direction && maze is not null
                ? new[] { new Position(caster.Position.X + 1, caster.Position.Y),
                    new Position(caster.Position.X - 1, caster.Position.Y),
                    new Position(caster.Position.X, caster.Position.Y + 1),
                    new Position(caster.Position.X, caster.Position.Y - 1) }
                    .Where(InRange)
                : hostiles.Where(item => InRange(item.Position)).Select(item => item.Position);
            var best = centers.Select(center =>
            {
                var footprint = maze is null ? null :
                    SpellAreaFootprint.GetCells(spell, caster.Position, center, maze);
                var areaDamage = effects.Any(effect => effect.Type == SpellEffectType.Damage);
                var friendlyFire = areaDamage
                    ? allies.Append(caster).Where(ally => ally.CurrentHitPoints > 0 &&
                        (footprint?.Contains(ally.Position) ??
                         TacticalDistance.Between(center, ally.Position) <= Math.Max(1, spell.AreaRadius)))
                        .DistinctBy(ally => ally.Id).ToArray()
                    : [];
                return new
                {
                    Position = center,
                    FriendlyFire = friendlyFire,
                    Targets = hostiles.Where(item => footprint?.Contains(item.Position) ??
                        TacticalDistance.Between(center, item.Position) <= Math.Max(1, spell.AreaRadius))
                        .Select(item => item.Character).ToArray()
                };
            }).Where(item => item.Targets.Length > 0)
                .OrderByDescending(item => Score(effects, item.Targets.Length,
                    item.Targets.Min(target => target.CurrentVitality)) -
                    item.FriendlyFire.Length * Math.Max(30, Score(effects, 1, 100)))
                .FirstOrDefault();
            return best is null ? null : new EnemySpellPlan(spell, best.Position, best.Targets, [],
                Score(effects, best.Targets.Length, best.Targets.Min(target => target.CurrentVitality)) -
                best.FriendlyFire.Length * Math.Max(30, Score(effects, 1, 100)), best.FriendlyFire,
                hostiles.ToDictionary(item => item.Character.Id, item => item.Position));
        }

        var eligibleAllies = spell.TargetType == SpellTargetType.Self
            ? allies.Where(ally => ReferenceEquals(ally, caster)).ToArray()
            : allies.Where(ally => ally.CurrentHitPoints > 0 &&
                (spell.Range == 0 || TacticalDistance.Between(caster.Position, ally.Position) <= spell.Range)).ToArray();
        if (eligibleAllies.Length == 0) return null;
        var allyTarget = isHeal
            ? eligibleAllies.OrderBy(ally => ally.CurrentHitPoints / (double)Math.Max(1, ally.MaximumHitPoints)).First()
            : eligibleAllies.FirstOrDefault(ally => !HasAllEffects(ally, effects)) ?? eligibleAllies[0];
        if (isHeal && allyTarget.CurrentHitPoints >= allyTarget.MaximumHitPoints ||
            !isHeal && !harmful && HasAllEffects(allyTarget, effects)) return null;
        var affected = spell.TargetType == SpellTargetType.Party ? eligibleAllies : [allyTarget];
        var missing = affected.Sum(ally => Math.Max(0, ally.MaximumHitPoints - ally.CurrentHitPoints));
        return new EnemySpellPlan(spell, allyTarget.Position, [], affected,
            isHeal ? Math.Min(220, 60 + missing) : 65 + affected.Length * 12);
    }

    private static int Score(IReadOnlyList<SpellEffectDefinition> effects, int targetCount, int weakestHp)
    {
        var damage = effects.Where(effect => effect.Type is SpellEffectType.Damage or SpellEffectType.ChainDamage)
            .Sum(effect => effect.Dice is { } dice ? dice.Count * (dice.Sides + 1) / 2 : effect.Value);
        var control = effects.Count(effect => effect.Type is SpellEffectType.SpeedPenalty or
            SpellEffectType.SkipAlternate || effect.Type == SpellEffectType.HitBonus && effect.Value < 0) * 25;
        return damage * Math.Max(1, targetCount) + control * Math.Max(1, targetCount) +
               (damage >= weakestHp ? 45 : 0);
    }

    private void ApplyEffect(Enemy caster, EnemySpellPlan plan, SpellEffectDefinition effect, List<string> notes)
    {
        if (effect.Type == SpellEffectType.Heal)
        {
            foreach (var ally in plan.AlliedTargets)
            {
                var amount = RollPower(caster, plan.Spell, effect);
                var restored = ally.RestoreHitPoints(effect.Parameter == "Full" ? ally.MaximumHitPoints : amount);
                if (restored > 0) notes.Add($"{ally.ShortName} +{restored} HP");
            }
            return;
        }
        if (effect.Type is SpellEffectType.Damage or SpellEffectType.ChainDamage)
        {
            var index = 0;
            foreach (var target in plan.HostileTargets)
            {
                var damage = RollPower(caster, plan.Spell, effect);
                if (effect.Type == SpellEffectType.ChainDamage)
                {
                    var percentages = (effect.Parameter ?? "100").Split('|').Select(int.Parse).ToArray();
                    damage = damage * percentages[Math.Min(index++, percentages.Length - 1)] / 100;
                }
                damage = ResolveDamage(target, effect.Resolution, damage, caster.Definition.SpellcasterProfile!.Intelligence,
                    plan.Spell.Level);
                if (plan.Spell.Id == "S011" && damage > 0)
                    damage = Math.Max(1, damage * SpellAreaFootprint.MeteorDamagePercent(
                        caster.Position, plan.TargetPosition,
                        plan.HostilePositions?.GetValueOrDefault(target.Id) ?? plan.TargetPosition) / 100);
                target.ReceiveDamage(damage);
                notes.Add($"{target.Name} -{damage} HP");
            }
            if (effect.Type == SpellEffectType.Damage)
                foreach (var ally in plan.FriendlyFireTargets ?? [])
                {
                    var damage = RollPower(caster, plan.Spell, effect);
                    var saved = (effect.Resolution is SpellResolution.SaveHalf or SpellResolution.SaveNegates) &&
                        random.Next(1, 21) + ally.EffectiveSpeed >=
                        10 + caster.Definition.SpellcasterProfile!.Intelligence / 2 + plan.Spell.Level;
                    if (saved && effect.Resolution == SpellResolution.SaveNegates) damage = 0;
                    else if (saved) damage = Math.Max(1, damage / 2);
                    if (plan.Spell.Id == "S011" && damage > 0)
                        damage = Math.Max(1, damage * SpellAreaFootprint.MeteorDamagePercent(
                            caster.Position, plan.TargetPosition, ally.Position) / 100);
                    var resistance = Math.Clamp(ally.Definition.MagicResistance, 0, 100);
                    damage = resistance >= 100 ? 0 : damage > 0 ? Math.Max(1, damage * (100 - resistance) / 100) : 0;
                    ally.ReceiveSpellDamage(damage);
                    if (damage > 0) notes.Add($"{ally.ShortName} baráti tűz -{damage} HP");
                }
            return;
        }
        if (effect.Type == SpellEffectType.Execute)
        {
            foreach (var target in plan.HostileTargets.Where(target =>
                         target.CurrentVitality * 100 <= target.MaximumVitality * effect.Value))
            {
                target.ReceiveDamage(target.CurrentVitality);
                notes.Add($"{target.Name} megsemmisül");
            }
            return;
        }
        if (effect.Type is SpellEffectType.Dispel or SpellEffectType.DispelBeneficial)
        {
            foreach (var target in plan.HostileTargets.Where(target =>
                         EffectSucceeds(target, effect.Resolution,
                             caster.Definition.SpellcasterProfile!.Intelligence, plan.Spell.Level)))
                target.RemoveSpellEffects(active => effect.Type == SpellEffectType.DispelBeneficial ? active.Beneficial : true);
            foreach (var ally in plan.AlliedTargets)
                ally.RemoveSpellEffects(active => effect.Parameter == "HarmfulOnly" ? !active.Beneficial : true);
            notes.Add("varázshatások szétfoszlanak");
            return;
        }

        if (!TryActiveType(effect, out var activeType)) return;
        var applied = 0;
        foreach (var target in plan.HostileTargets)
        {
            if (!EffectSucceeds(target, effect.Resolution,
                    caster.Definition.SpellcasterProfile!.Intelligence, plan.Spell.Level)) continue;
            target.ApplySpellEffect(CreateActive(caster, plan.Spell, effect, activeType, beneficial: false));
            applied++;
        }
        foreach (var ally in plan.AlliedTargets)
        {
            ally.ApplySpellEffect(CreateActive(caster, plan.Spell, effect, activeType, beneficial: true));
            applied++;
        }
        foreach (var ally in plan.FriendlyFireTargets ?? [])
        {
            if (effect.Resolution == SpellResolution.SaveNegates &&
                random.Next(1, 21) + ally.EffectiveSpeed >=
                10 + caster.Definition.SpellcasterProfile!.Intelligence / 2 + plan.Spell.Level) continue;
            ally.ApplySpellEffect(CreateActive(caster, plan.Spell, effect, activeType, beneficial: false));
            applied++;
        }
        if (applied > 0) notes.Add(effect.Description);
    }

    private int RollPower(Enemy caster, SpellDefinition spell, SpellEffectDefinition effect) =>
        (effect.Dice?.Roll(random) ?? 0) + effect.Value +
        (int)Math.Round(caster.Definition.SpellcasterProfile!.Intelligence * effect.IntelligenceMultiplier) +
        caster.Definition.StrengthTier * effect.LevelMultiplier;

    private int ResolveDamage(LiveCharacter target, SpellResolution resolution, int damage, int intelligence, int level) =>
        resolution switch
        {
            SpellResolution.Attack when random.Next(1, 21) + intelligence < 11 + target.EffectiveAbilities.Dexterity => 0,
            SpellResolution.SaveHalf when Resists(target, resolution, intelligence, level) => damage / 2,
            SpellResolution.SaveNegates when Resists(target, resolution, intelligence, level) => 0,
            _ => damage
        };

    private bool Resists(LiveCharacter target, SpellResolution resolution, int intelligence, int level) =>
        resolution is SpellResolution.SaveHalf or SpellResolution.SaveNegates &&
        random.Next(1, 21) + target.EffectiveAbilities.Dexterity >= 10 + intelligence / 2 + level;

    private bool EffectSucceeds(LiveCharacter target, SpellResolution resolution, int intelligence, int level) =>
        resolution switch
        {
            SpellResolution.Attack => random.Next(1, 21) + intelligence >= 11 + target.EffectiveAbilities.Dexterity,
            SpellResolution.SaveNegates => !Resists(target, resolution, intelligence, level),
            _ => true
        };

    private static bool TryActiveType(SpellEffectDefinition effect, out ActiveSpellEffectType type)
    {
        type = effect.Type switch
        {
            SpellEffectType.Burning => ActiveSpellEffectType.Burning,
            SpellEffectType.Storm => ActiveSpellEffectType.Storm,
            SpellEffectType.SpeedPenalty => ActiveSpellEffectType.SpeedPenalty,
            SpellEffectType.SkipAlternate when effect.Parameter == "Next" => ActiveSpellEffectType.SkipNext,
            SpellEffectType.SkipAlternate => ActiveSpellEffectType.SkipAlternate,
            SpellEffectType.Invisibility => ActiveSpellEffectType.Invisibility,
            SpellEffectType.DefenseBonus => ActiveSpellEffectType.DefenseBonus,
            SpellEffectType.PhysicalReduction => ActiveSpellEffectType.PhysicalReduction,
            SpellEffectType.BleedingImmunity => ActiveSpellEffectType.BleedingImmunity,
            SpellEffectType.HitBonus => ActiveSpellEffectType.HitBonus,
            SpellEffectType.DamageBonus => ActiveSpellEffectType.DamageBonus,
            SpellEffectType.InitiativeBonus => ActiveSpellEffectType.InitiativeBonus,
            SpellEffectType.ProtectionFromEvil => ActiveSpellEffectType.ProtectionFromEvil,
            SpellEffectType.Sanctuary => ActiveSpellEffectType.Sanctuary,
            SpellEffectType.VisionBonus => ActiveSpellEffectType.VisionBonus,
            SpellEffectType.RandomElement => ActiveSpellEffectType.Burning,
            _ => default
        };
        return effect.Type is SpellEffectType.Burning or SpellEffectType.Storm or SpellEffectType.SpeedPenalty or
            SpellEffectType.SkipAlternate or SpellEffectType.Invisibility or SpellEffectType.DefenseBonus or
            SpellEffectType.PhysicalReduction or SpellEffectType.BleedingImmunity or SpellEffectType.HitBonus or
            SpellEffectType.DamageBonus or SpellEffectType.InitiativeBonus or SpellEffectType.ProtectionFromEvil or
            SpellEffectType.Sanctuary or SpellEffectType.VisionBonus or SpellEffectType.RandomElement;
    }

    private static ActiveSpellEffect CreateActive(Enemy caster, SpellDefinition spell, SpellEffectDefinition effect,
        ActiveSpellEffectType type, bool beneficial) => new(spell.Id, type, effect.Value,
        Math.Max(1, effect.Duration), effect.Type is SpellEffectType.Burning or SpellEffectType.Storm or
        SpellEffectType.RandomElement ? effect.Dice : null,
        (int)Math.Round(caster.Definition.SpellcasterProfile!.Intelligence * effect.IntelligenceMultiplier) +
        caster.Definition.StrengthTier * effect.LevelMultiplier, beneficial);

    private static bool HasAllEffects(Enemy ally, IReadOnlyList<SpellEffectDefinition> effects) => effects
        .Where(effect => TryActiveType(effect, out _)).All(effect =>
            TryActiveType(effect, out var type) && ally.ActiveSpellEffects.Any(active => active.Type == type));

    private static bool HasAllHarmfulEffects(LiveCharacter target,
        IReadOnlyList<SpellEffectDefinition> effects)
    {
        var activeEffects = effects.Where(effect => TryActiveType(effect, out _)).ToArray();
        return activeEffects.Length > 0 && activeEffects.All(effect =>
            TryActiveType(effect, out var type) && target.ActiveSpellEffects.Any(active => active.Type == type));
    }
}
