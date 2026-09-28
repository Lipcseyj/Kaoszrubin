using KaoszRubin.Data;
using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin.Application;

public sealed class DungeonTrapService
{
    private readonly GameDataCatalog _gameData;
    private readonly Random _random;

    public DungeonTrapService(GameDataCatalog gameData, Random random)
    {
        _gameData = gameData;
        _random = random;
    }

    public static int TrapDetectionChance(LiveCharacter character, TrapDefinition definition) => Math.Clamp(
        35 + (character.EffectiveAbilities.Intelligence + character.EffectiveAbilities.Dexterity) * 3 -
        definition.DetectionDifficulty * 5 +
        (CharacterClassRules.IsThief(character.CharacterClass.Id) ? 30 : 0), 15, 95);

    public static int TrapDisarmChance(LiveCharacter character, TrapDefinition definition) => Math.Clamp(
        30 + character.EffectiveAbilities.Dexterity * 5 - definition.DisarmDifficulty * 6 +
        (CharacterClassRules.IsThief(character.CharacterClass.Id) ? 30 : 0), 10, 95);

    public void ApplyTrap(
        LiveCharacter character,
        MazeTrap trap,
        int difficultyLevel,
        Maze maze,
        Action<LiveCharacter, int> onAlertNearbyEnemies,
        Action<string, ConsoleColor, LiveCharacter> onShowTrapMessage,
        Action<LiveCharacter> onRefreshCharacterSheet,
        Action onDrawMapVisibilityChanged,
        Action<MazeTrap>? onSpellTrap = null)
    {
        trap.Trigger();
        if (trap.Definition.Effect == TrapEffect.Spell)
        {
            onSpellTrap?.Invoke(trap);
            return;
        }
        var scaledDamage = trap.Definition.MaximumDamage == 0 ? 0 :
            _random.Next(trap.Definition.MinimumDamage, trap.Definition.MaximumDamage + 1) + (difficultyLevel - 1) / 3;
        var maximumAllowed = Math.Max(1, character.MaximumVitality / (difficultyLevel <= 4 ? 7 : 4));
        var damage = Math.Min(Math.Min(scaledDamage, maximumAllowed), Math.Max(0, character.CurrentVitality - 1));
        character.ReceiveDamage(damage);
        var extra = string.Empty;
        if (trap.Definition.Effect == TrapEffect.Poison && character.IsAlive &&
            _random.Next(100) < trap.Definition.StatusChancePercent)
        {
            character.AddStatus(_gameData.GetStatus(CharacterStatusIds.Poisoned));
            extra = " Megmérgeződött.";
        }
        else if (trap.Definition.Effect == TrapEffect.Alert)
        {
            onAlertNearbyEnemies(character, 12);
            extra = " A közeli szörnyek felfigyeltek a zajra.";
        }
        else if (trap.Definition.Effect == TrapEffect.Darkness && character.IsAlive)
        {
            character.ApplySpellEffect(new ActiveSpellEffect(trap.Definition.Id,
                ActiveSpellEffectType.VisionBonus, -2, 6));
            extra = " A koromfelhő 6 körre 2-vel csökkentette a látótávját.";
        }
        onRefreshCharacterSheet(character);
        onDrawMapVisibilityChanged();
        var damageText = damage > 0 ? $" {character.Name} {damage} sebzést szenvedett." : string.Empty;
        onShowTrapMessage($"💥 Elsült: {trap.Definition.Name}.{damageText}{extra}",
            ConsoleColor.Red, character);
    }

    public MagicTrapSpellResult TriggerSpell(MazeTrap trap, int casterLevel, int intelligence,
        IReadOnlyList<(LiveCharacter Character, Position Position)> livingParty)
    {
        if (trap.Definition.Effect != TrapEffect.Spell || trap.Definition.SpellId is null)
            throw new InvalidOperationException($"A(z) {trap.Definition.Id} nem érvényes varázscsapda.");
        var party = livingParty.Where(member => member.Character.IsAlive)
            .DistinctBy(member => member.Character.Id).ToArray();
        if (party.Length == 0)
            return new MagicTrapSpellResult(_gameData.GetSpell(trap.Definition.SpellId), trap.Position, [], [],
                "nincs élő célpont");

        var nearestDistance = party.Min(member => Chebyshev(trap.Position, member.Position));
        var nearest = party.Where(member => Chebyshev(trap.Position, member.Position) == nearestDistance).ToArray();
        var aim = nearest[_random.Next(nearest.Length)].Position;
        var spell = _gameData.GetSpell(trap.Definition.SpellId);
        var primaryTargets = spell.TargetType == SpellTargetType.Area
            ? party.Where(member => Chebyshev(aim, member.Position) <= spell.AreaRadius).ToArray()
            : party.Where(member => member.Position == aim).Take(1).ToArray();
        var allAffected = new HashSet<CharacterId>(primaryTargets.Select(member => member.Character.Id));
        var damageByTarget = party.ToDictionary(member => member.Character, _ => 0);
        var notes = new List<string>();
        var resolutionCache = new Dictionary<(CharacterId, SpellResolution), MagicTrapResolution>();

        foreach (var effect in _gameData.GetSpellEffects(spell.Id))
        {
            var effectTargets = effect.Type == SpellEffectType.ChainDamage
                ? party.OrderBy(member => member.Position == aim ? 0 : 1)
                    .ThenBy(member => Chebyshev(aim, member.Position)).ToArray()
                : primaryTargets;
            if (effect.Type == SpellEffectType.ChainDamage)
            {
                var percentages = (effect.Parameter ?? "100").Split('|').Select(int.Parse).ToArray();
                for (var index = 0; index < Math.Min(effectTargets.Length, percentages.Length); index++)
                {
                    var member = effectTargets[index];
                    allAffected.Add(member.Character.Id);
                    var resolution = ResolveMagicTrap(effect, spell, member.Character, intelligence, resolutionCache);
                    if (!resolution.Applies) continue;
                    var damage = RollSpellPower(effect, casterLevel, intelligence) * percentages[index] / 100;
                    if (resolution.Half) damage = Math.Max(1, damage / 2);
                    if (resolution.Critical) damage *= 2;
                    damageByTarget[member.Character] += Math.Max(0, damage);
                }
                continue;
            }

            foreach (var member in effectTargets)
            {
                var resolution = ResolveMagicTrap(effect, spell, member.Character, intelligence, resolutionCache);
                if (!resolution.Applies || _random.Next(100) >= effect.ChancePercent) continue;
                if (effect.Type == SpellEffectType.Damage)
                {
                    var damage = RollSpellPower(effect, casterLevel, intelligence);
                    if (resolution.Half) damage = Math.Max(1, damage / 2);
                    if (resolution.Critical) damage *= 2;
                    damageByTarget[member.Character] += Math.Max(0, damage);
                    continue;
                }
                if (TryMagicTrapTimedEffect(effect, out var activeType))
                {
                    member.Character.ApplySpellEffect(new ActiveSpellEffect(spell.Id, activeType, effect.Value,
                        effect.Duration, effect.Dice,
                        (int)Math.Round(intelligence * effect.IntelligenceMultiplier), false,
                        Parameter: effect.Parameter));
                    notes.Add($"{member.Character.Name}: {SpellExecutionService.TimedEffectName(activeType)}");
                }
            }
        }

        foreach (var entry in damageByTarget.Where(entry => entry.Value > 0))
        {
            var maximumAllowed = Math.Max(1, entry.Key.MaximumVitality / (casterLevel <= 4 ? 7 : 4));
            var damage = Math.Min(Math.Min(entry.Value, maximumAllowed), Math.Max(0, entry.Key.CurrentVitality - 1));
            if (damage <= 0) continue;
            entry.Key.ReceiveDamage(damage);
            notes.Add($"{entry.Key.Name}: -{damage} HP");
        }

        var affectedPositions = party.Where(member => allAffected.Contains(member.Character.Id))
            .Select(member => member.Position).Distinct().ToArray();
        return new MagicTrapSpellResult(spell, aim, affectedPositions,
            party.Where(member => allAffected.Contains(member.Character.Id)).Select(member => member.Character)
                .DistinctBy(character => character.Id).ToArray(),
            notes.Count == 0 ? "a célpontok ellenálltak" : string.Join(", ", notes.Distinct()));
    }

    private int RollSpellPower(SpellEffectDefinition effect, int casterLevel, int intelligence) =>
        (effect.Dice?.Roll(_random) ?? 0) + effect.Value +
        (int)Math.Round(intelligence * effect.IntelligenceMultiplier) + casterLevel * effect.LevelMultiplier;

    private MagicTrapResolution ResolveMagicTrap(SpellEffectDefinition effect, SpellDefinition spell,
        LiveCharacter target, int intelligence,
        IDictionary<(CharacterId, SpellResolution), MagicTrapResolution> cache)
    {
        if (effect.Resolution == SpellResolution.Auto) return new MagicTrapResolution(true, false, false);
        var cacheResolution = effect.Resolution == SpellResolution.SaveNegates
            ? SpellResolution.SaveHalf
            : effect.Resolution;
        var key = (target.Id, cacheResolution);
        if (cache.TryGetValue(key, out var cached))
            return effect.Resolution == SpellResolution.SaveNegates && cached.Half
                ? cached with { Applies = false, Half = false }
                : cached with { Half = effect.Resolution == SpellResolution.SaveHalf && cached.Half };
        MagicTrapResolution result;
        if (effect.Resolution == SpellResolution.Attack)
        {
            var roll = _random.Next(1, 21);
            var hit = roll == 20 || roll != 1 && roll + intelligence >= 11 + target.EffectiveAbilities.Dexterity;
            result = new MagicTrapResolution(hit, false, roll == 20);
        }
        else
        {
            var saved = _random.Next(1, 21) + target.EffectiveAbilities.Dexterity >=
                        10 + intelligence / 2 + spell.Level;
            result = new MagicTrapResolution(!saved || effect.Resolution == SpellResolution.SaveHalf,
                saved && effect.Resolution == SpellResolution.SaveHalf, false);
        }
        cache[key] = result;
        return result;
    }

    private static bool TryMagicTrapTimedEffect(SpellEffectDefinition effect, out ActiveSpellEffectType type)
    {
        type = effect.Type switch
        {
            SpellEffectType.Burning => ActiveSpellEffectType.Burning,
            SpellEffectType.Storm => ActiveSpellEffectType.Storm,
            SpellEffectType.SpeedPenalty => ActiveSpellEffectType.SpeedPenalty,
            SpellEffectType.SkipAlternate when string.Equals(effect.Parameter, "Next", StringComparison.OrdinalIgnoreCase) =>
                ActiveSpellEffectType.SkipNext,
            SpellEffectType.SkipAlternate => ActiveSpellEffectType.SkipAlternate,
            SpellEffectType.HitBonus when effect.Value < 0 => ActiveSpellEffectType.HitBonus,
            SpellEffectType.VisionBonus when effect.Value < 0 => ActiveSpellEffectType.VisionBonus,
            _ => default
        };
        return effect.Type is SpellEffectType.Burning or SpellEffectType.Storm or
            SpellEffectType.SpeedPenalty or SpellEffectType.SkipAlternate ||
            effect.Type == SpellEffectType.HitBonus && effect.Value < 0 ||
            effect.Type == SpellEffectType.VisionBonus && effect.Value < 0;
    }

    private static int Chebyshev(Position left, Position right) =>
        Math.Max(Math.Abs(left.X - right.X), Math.Abs(left.Y - right.Y));

    private sealed record MagicTrapResolution(bool Applies, bool Half, bool Critical);
}

public sealed record MagicTrapSpellResult(SpellDefinition Spell, Position Aim,
    IReadOnlyList<Position> AffectedPositions, IReadOnlyList<LiveCharacter> AffectedCharacters, string Summary);
