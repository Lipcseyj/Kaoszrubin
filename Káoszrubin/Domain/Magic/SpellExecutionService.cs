using KaoszRubin.Combat;
using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin.Domain.Magic;

public sealed record SpellCastAttempt(bool ConsumesTurn, string Message, BattleLogKind Kind,
    int DamageToCurrentEnemy = 0, int ExtraPlayerActions = 0, BattleActionDetails? Details = null);

public sealed record SpellExecutionResult(int DamageToCurrentEnemy, int ExtraPlayerActions, string Summary,
    BattleActionDetails? Details = null);

public sealed record SpellResolutionResult(bool Applies, bool Half, bool Critical, string Text);

public sealed class SpellExecutionService
{
    private readonly GameDataCatalog _gameData;
    private readonly Random _random;
    private readonly List<string> _calculation = [];
    private bool _criticalOccurred;

    public SpellExecutionService(GameDataCatalog gameData, Random random)
    {
        _gameData = gameData;
        _random = random;
    }

    public SpellCastAttempt? ValidateSpellCast(LiveCharacter caster, Position casterPosition,
        SpellDefinition spell, bool inCombat, Enemy? currentEnemy, MagicItemDefinition? castingItem = null,
        int? castingItemSlotIndex = null, Position? explicitTarget = null,
        bool timeStopUsedThisBattle = false,
        IReadOnlyList<(LiveCharacter Character, Position Position)>? livingParty = null,
        Maze? maze = null, FogOfWar? fogOfWar = null, Position? playerPosition = null,
        LiveCharacter? selectedCharacter = null)
    {
        if (!caster.IsAlive)
            return new SpellCastAttempt(false, $"{caster.Name} nem képes varázsolni.", BattleLogKind.Information);
        var usingItem = castingItem is not null;
        var castingItemIndex = usingItem ? castingItemSlotIndex ?? -1 : -1;
        if (usingItem && (castingItem!.Kind is not (MagicItemKind.Scroll or MagicItemKind.Wand) || castingItem.SpellId != spell.Id ||
                castingItemIndex is < 0 or >= LiveCharacter.MaximumMagicItemCount ||
                caster.MagicItems[castingItemIndex]?.Id != castingItem.Id || caster.MagicItemCharges[castingItemIndex] <= 0 ||
                !SpellcastingRules.CanUseCastingItem(caster, castingItem, spell)))
            return new SpellCastAttempt(false, "A kiválasztott tekercs vagy pálca nem használható.", BattleLogKind.Information);
        if (!usingItem && !caster.IsSpellcaster)
            return new SpellCastAttempt(false, "Ez az osztály nem használ varázslatokat.", BattleLogKind.Information);
        if (!usingItem && !SpellcastingRules.HasRequiredFocus(caster))
            return new SpellCastAttempt(false, "A varázsláshoz hiányzik a megfelelő fókusztárgy.", BattleLogKind.Information);
        if (!usingItem && caster.MemorizedSpells.All(candidate =>
                !string.Equals(candidate.Id, spell.Id, StringComparison.OrdinalIgnoreCase)))
            return new SpellCastAttempt(false, $"A(z) {spell.Name} nincs memorizálva.", BattleLogKind.Information);
        if (inCombat ? !spell.CanUseInCombat : !spell.CanUseDuringExploration)
            return new SpellCastAttempt(false, $"A(z) {spell.Name} ebben a helyzetben nem használható.", BattleLogKind.Information);
        if (inCombat && timeStopUsedThisBattle && _gameData.GetSpellEffects(spell.Id)
                .Any(effect => effect.Type == SpellEffectType.ExtraActions))
            return new SpellCastAttempt(false, "Az Időmegállítás csatánként csak egyszer használható.", BattleLogKind.Information);
        var manaCost = usingItem ? 0 : SpellcastingRules.EffectiveManaCost(caster, spell);
        if (caster.CurrentMana < manaCost)
            return new SpellCastAttempt(false, $"Nincs elég manna: {spell.Name} {manaCost} mannát igényel.", BattleLogKind.Information);
        if (!HasValidSpellTarget(caster, casterPosition, spell, currentEnemy, livingParty, maze, fogOfWar, playerPosition, selectedCharacter))
            return new SpellCastAttempt(false, $"A(z) {spell.Name} számára nincs érvényes célpont.", BattleLogKind.Information);
        if (explicitTarget is { } target && !IsValidExplicitSpellTarget(caster, casterPosition, spell, target, currentEnemy, livingParty, maze, fogOfWar, playerPosition, selectedCharacter))
            return new SpellCastAttempt(false, "A varázslat célpontja érvénytelen.", BattleLogKind.Information);
        return null;
    }

    public bool HasValidSpellTarget(LiveCharacter caster, Position casterPosition, SpellDefinition spell,
        Enemy? currentEnemy, IReadOnlyList<(LiveCharacter Character, Position Position)>? livingParty = null,
        Maze? maze = null, FogOfWar? fogOfWar = null, Position? playerPosition = null,
        LiveCharacter? selectedCharacter = null) => spell.TargetType switch
    {
        SpellTargetType.Self => CanAffectCharacter(spell, caster),
        SpellTargetType.Party => livingParty?.Any(entry => CanAffectCharacter(spell, entry.Character)) ?? false,
        _ => GetValidSpellTargets(casterPosition, spell, currentEnemy, maze, fogOfWar, playerPosition, selectedCharacter).Any()
    };

    public bool IsValidExplicitSpellTarget(LiveCharacter caster, Position casterPosition, SpellDefinition spell,
        Position target, Enemy? currentEnemy,
        IReadOnlyList<(LiveCharacter Character, Position Position)>? livingParty = null,
        Maze? maze = null, FogOfWar? fogOfWar = null, Position? playerPosition = null,
        LiveCharacter? selectedCharacter = null) => spell.TargetType switch
    {
        SpellTargetType.Self => target == casterPosition && CanAffectCharacter(spell, caster),
        SpellTargetType.Party => target == casterPosition && (livingParty?.Any(entry => CanAffectCharacter(spell, entry.Character)) ?? false),
        _ => IsValidSpellTarget(casterPosition, spell, target, currentEnemy, maze, fogOfWar, playerPosition, selectedCharacter)
    };

    public bool IsValidSpellTarget(Position casterPosition, SpellDefinition spell, Position position,
        Enemy? currentEnemy, Maze? maze, FogOfWar? fogOfWar, Position? playerPosition,
        LiveCharacter? selectedCharacter)
    {
        if (maze is null || fogOfWar is null) return true;
        if (!maze.IsInside(position) || !fogOfWar.IsVisible(position)) return false;
        var inRange = Chebyshev(casterPosition, position) <= Math.Max(1, spell.Range);
        if (!inRange || spell.RequiresLineOfSight && !FogOfWar.CanSee(maze, casterPosition, position, Math.Max(1, spell.Range))) return false;
        return spell.TargetType switch
        {
            SpellTargetType.Enemy => currentEnemy is not null
                ? currentEnemy.CurrentHitPoints > 0 && currentEnemy.Position == position
                : maze.GetEnemyAt(position)?.CurrentHitPoints > 0,
            SpellTargetType.PartyMember => (playerPosition.HasValue && position == playerPosition.Value &&
                                           selectedCharacter is not null && selectedCharacter.IsAlive &&
                                           CanAffectCharacter(spell, selectedCharacter)) ||
                                           maze.PartyMembers.Any(member => member.Position == position && member.Character.IsAlive &&
                                               CanAffectCharacter(spell, member.Character)),
            SpellTargetType.Corpse => maze.Corpses.OfType<PartyMemberCorpse>().Any(corpse =>
                corpse.Position == position && !corpse.Character.WasResurrectedThisLevel &&
                FindResurrectionPosition(maze, playerPosition, corpse) is not null),
            SpellTargetType.Direction => Manhattan(casterPosition, position) == 1,
            SpellTargetType.Cell when _gameData.GetSpellEffects(spell.Id).Any(effect =>
                effect.Type is SpellEffectType.TeleportSelf or SpellEffectType.TeleportParty) =>
                maze.IsWalkable(position) && maze.GetObjectAt(position) is null,
            SpellTargetType.Cell or SpellTargetType.Area => true,
            _ => false
        };
    }

    public IEnumerable<Position> GetValidSpellTargets(Position casterPosition, SpellDefinition spell,
        Enemy? currentEnemy, Maze? maze, FogOfWar? fogOfWar, Position? playerPosition,
        LiveCharacter? selectedCharacter)
    {
        if (maze is null || fogOfWar is null) yield break;
        var range = Math.Max(1, spell.Range);
        for (var y = Math.Max(0, casterPosition.Y - range); y <= Math.Min(maze.Height - 1, casterPosition.Y + range); y++)
            for (var x = Math.Max(0, casterPosition.X - range); x <= Math.Min(maze.Width - 1, casterPosition.X + range); x++)
            {
                var candidate = new Position(x, y);
                if (IsValidSpellTarget(casterPosition, spell, candidate, currentEnemy, maze, fogOfWar, playerPosition, selectedCharacter))
                    yield return candidate;
            }
    }

    public bool CanAffectCharacter(SpellDefinition spell, LiveCharacter character)
    {
        var effects = _gameData.GetSpellEffects(spell.Id);
        return effects.Any(effect => effect.Type switch
        {
            SpellEffectType.Heal => character.CurrentVitality < character.MaximumVitality,
            SpellEffectType.CureStatus => ParseEffectParameters(effect.Parameter).Any(character.HasStatus),
            SpellEffectType.RestoreNeeds => character.FoodLevel < 100 || character.WaterLevel < 100,
            _ => true
        });
    }

    public string DescribeSpellTarget(LiveCharacter caster, SpellDefinition spell, Position position,
        Enemy? currentEnemy, Maze? maze, Position? playerPosition, LiveCharacter? selectedCharacter) => spell.TargetType switch
    {
        SpellTargetType.Self => caster.Name,
        SpellTargetType.Party => "az egész parti",
        SpellTargetType.Enemy when currentEnemy is not null && currentEnemy.Position == position => currentEnemy.Name,
        SpellTargetType.Enemy => maze?.GetEnemyAt(position)?.Name ?? $"({position.X},{position.Y})",
        SpellTargetType.PartyMember when playerPosition.HasValue && position == playerPosition.Value => selectedCharacter?.Name ?? $"({position.X},{position.Y})",
        SpellTargetType.PartyMember => maze?.PartyMembers.FirstOrDefault(member => member.Position == position)?.Character.Name ?? $"({position.X},{position.Y})",
        SpellTargetType.Corpse => maze?.Corpses.OfType<PartyMemberCorpse>().FirstOrDefault(corpse => corpse.Position == position)?.Character.Name ?? $"({position.X},{position.Y})",
        SpellTargetType.Direction when playerPosition.HasValue => $"{DirectionName(playerPosition.Value, position)} irány",
        _ => $"({position.X},{position.Y})"
    };

    public bool IsOffensiveSpell(SpellDefinition spell) => _gameData.GetSpellEffects(spell.Id).Any(effect =>
        effect.Type is SpellEffectType.Damage or SpellEffectType.ChainDamage or SpellEffectType.Burning or
            SpellEffectType.Storm or SpellEffectType.SpeedPenalty or SpellEffectType.SkipAlternate or
            SpellEffectType.Execute or SpellEffectType.RandomElement or SpellEffectType.DispelBeneficial);

    public static string CastingItemUseText(MagicItemDefinition item) => item.Kind == MagicItemKind.Scroll
        ? "📜 A tekercs elhasználódott"
        : $"{ConsoleRenderer.WandIcon} A pálca egy töltete elfogyott";

    public SpellExecutionResult ExecuteSpell(LiveCharacter caster, Position casterPosition, SpellDefinition spell,
        Position target, bool inCombat, Enemy? currentEnemy, bool divineJudgment,
        ref bool timeStopUsedThisBattle,
        IReadOnlyList<(LiveCharacter Character, Position Position)> livingParty,
        Maze maze,
        Action<LiveCharacter, Enemy, int, List<string>> onExplorationSpellDamage,
        Func<Position, bool, bool> onTeleportLeader,
        Func<Position, bool, string> onTeleportLivingParty,
        Func<Position, SpellEffectDefinition, string> onResurrectPartyMember,
        Action<LiveCharacter>? onRefreshCharacterSheet = null)
    {
        _calculation.Clear();
        _criticalOccurred = false;
        _calculation.Add($"✨ {spell.Name}; szint {spell.Level}");
        _calculation.Add(divineJudgment ? "⚡ Isteni ítélet: ×2 hatás, ingyen" : "✨ Normál varázslat");
        var effects = _gameData.GetSpellEffects(spell.Id);
        var targets = ResolveEnemySpellTargets(spell, target, currentEnemy, casterPosition, maze).ToList();
        var characterTargets = ResolveCharacterSpellTargets(caster, spell, target, livingParty, maze).ToList();
        var damage = targets.ToDictionary(enemy => enemy, _ => 0);
        var initialHitPoints = targets.ToDictionary(enemy => enemy, enemy => enemy.CurrentHitPoints);
        var resolutionCache = new Dictionary<(Enemy Enemy, SpellResolution Resolution), SpellResolutionResult>();
        var notes = new List<string>();
        var extraActions = 0;

        foreach (var effect in effects)
        {
            switch (effect.Type)
            {
                case SpellEffectType.Damage:
                    foreach (var enemy in targets)
                        damage[enemy] += ResolveSpellDamage(caster, effect, spell, enemy, resolutionCache, notes, divineJudgment);
                    break;
                case SpellEffectType.ChainDamage:
                    ApplyChainDamage(caster, effect, spell, target, currentEnemy, damage, initialHitPoints, notes, maze);
                    break;
                case SpellEffectType.Burning:
                    ApplyEnemyTimedEffect(caster, effect, spell, targets, ActiveSpellEffectType.Burning, resolutionCache, notes, divineJudgment);
                    break;
                case SpellEffectType.Storm:
                    ApplyEnemyTimedEffect(caster, effect, spell, targets, ActiveSpellEffectType.Storm, resolutionCache, notes, divineJudgment);
                    break;
                case SpellEffectType.SpeedPenalty:
                    ApplyEnemyTimedEffect(caster, effect, spell, targets, ActiveSpellEffectType.SpeedPenalty, resolutionCache, notes, divineJudgment);
                    break;
                case SpellEffectType.SkipAlternate:
                    ApplyEnemyTimedEffect(caster, effect, spell, targets,
                        string.Equals(effect.Parameter, "Next", StringComparison.OrdinalIgnoreCase)
                            ? ActiveSpellEffectType.SkipNext
                            : ActiveSpellEffectType.SkipAlternate,
                        resolutionCache, notes, divineJudgment);
                    break;
                case SpellEffectType.Invisibility:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.Invisibility, divineJudgment);
                    notes.Add($"láthatatlanság {AdjustedDuration(caster, spell, effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.DefenseBonus:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.DefenseBonus, divineJudgment);
                    notes.Add($"+{effect.Value} védelem {AdjustedDuration(caster, spell, effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.PhysicalReduction:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.PhysicalReduction, divineJudgment);
                    notes.Add($"{effect.Value}% fizikai védelem {AdjustedDuration(caster, spell, effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.BleedingImmunity:
                    foreach (var characterTarget in characterTargets)
                    {
                        ApplyCharacterEffect(caster, characterTarget, effect, spell, ActiveSpellEffectType.BleedingImmunity, divineJudgment);
                        characterTarget.RemoveStatus(CharacterStatusIds.Bleeding);
                    }
                    notes.Add("vérzés megszüntetve és ideiglenesen kivédve");
                    break;
                case SpellEffectType.TeleportSelf:
                    notes.Add(onTeleportLeader(target, inCombat) ? "teleportáció sikeres" : "a célmező nem szabad");
                    break;
                case SpellEffectType.TeleportParty:
                    notes.Add(onTeleportLivingParty(target, inCombat));
                    break;
                case SpellEffectType.Dispel:
                    notes.Add(DispelAt(target, spell.AreaRadius, maze, livingParty));
                    break;
                case SpellEffectType.ExtraActions:
                    if (timeStopUsedThisBattle && inCombat)
                        notes.Add("az Időmegállítás ebben a csatában már nem ismételhető");
                    else
                    {
                        extraActions += effect.Value;
                        if (inCombat) timeStopUsedThisBattle = true;
                        notes.Add($"+{effect.Value} azonnali akció");
                    }
                    break;
                case SpellEffectType.Execute:
                    foreach (var enemy in targets)
                    {
                        var resolution = ResolveAgainstEnemy(caster, effect, spell, enemy, resolutionCache);
                        if (!resolution.Applies || enemy.Definition.StrengthTier >= 5 ||
                            initialHitPoints[enemy] * 100 > enemy.Definition.HitPoints * effect.Value) continue;
                        damage[enemy] = Math.Max(damage[enemy], enemy.CurrentHitPoints);
                        notes.Add($"💀 {enemy.Name}: megsemmisítés");
                    }
                    break;
                case SpellEffectType.RandomElement:
                    ApplyRandomElement(caster, effect, spell, targets, damage, resolutionCache, notes);
                    break;
                case SpellEffectType.Heal:
                    ApplyHealing(caster, effect, spell, characterTargets, divineJudgment, notes);
                    break;
                case SpellEffectType.CureStatus:
                    ApplyStatusCure(effect, characterTargets, notes);
                    break;
                case SpellEffectType.HitBonus:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.HitBonus, divineJudgment);
                    notes.Add($"+{effect.Value} találat {AdjustedDuration(caster, spell, effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.DamageBonus:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.DamageBonus, divineJudgment);
                    notes.Add($"+{effect.Value} fizikai sebzés {AdjustedDuration(caster, spell, effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.InitiativeBonus:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.InitiativeBonus, divineJudgment);
                    notes.Add($"+{effect.Value} kezdeményezés {AdjustedDuration(caster, spell, effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.ProtectionFromEvil:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.ProtectionFromEvil, divineJudgment);
                    notes.Add($"gonosz elleni védelem {AdjustedDuration(caster, spell, effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.GuardianAngel:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.GuardianAngel, divineJudgment);
                    notes.Add($"👼 Őrangyal {AdjustedDuration(caster, spell, effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.Sanctuary:
                    var sanctuaryTargets = livingParty
                        .Where(entry => Chebyshev(entry.Position, casterPosition) <= Math.Max(0, spell.AreaRadius))
                        .Select(entry => entry.Character).ToList();
                    ApplyCharacterEffects(caster, sanctuaryTargets, effect, spell, ActiveSpellEffectType.Sanctuary, divineJudgment);
                    notes.Add($"⛪ Szentély: {sanctuaryTargets.Count} karakter védett {AdjustedDuration(caster, spell, effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.Resurrect:
                    notes.Add(onResurrectPartyMember(target, effect));
                    break;
                case SpellEffectType.DispelBeneficial:
                    foreach (var enemy in targets)
                    {
                        var resolution = ResolveAgainstEnemy(caster, effect, spell, enemy, resolutionCache);
                        if (!resolution.Applies) continue;
                        var removed = enemy.RemoveSpellEffects(active => active.Beneficial);
                        notes.Add($"{enemy.Name}: {removed} pozitív varázshatás szétoszlatva");
                    }
                    break;
                case SpellEffectType.RestoreNeeds:
                    ApplyNeedRestoration(characterTargets, effect, divineJudgment, notes);
                    break;
                case SpellEffectType.VisionBonus:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell,
                        ActiveSpellEffectType.VisionBonus, divineJudgment);
                    notes.Add($"👁️ +{effect.Value} látótáv {AdjustedDuration(caster, spell, effect, divineJudgment)} akcióra");
                    break;
            }
        }

        var chainRepeated = caster.HasPerk(PerkIds.MageChainSpell) &&
                            effects.Any(effect => effect.Type is SpellEffectType.Damage or SpellEffectType.ChainDamage) &&
                            _random.Next(100) < 30;
        if (chainRepeated)
        {
            foreach (var effect in effects.Where(effect => effect.Type == SpellEffectType.Damage))
                foreach (var enemy in targets)
                    damage[enemy] += ResolveSpellDamage(caster, effect, spell, enemy,
                        new Dictionary<(Enemy, SpellResolution), SpellResolutionResult>(), notes);
            foreach (var effect in effects.Where(effect => effect.Type == SpellEffectType.ChainDamage))
                ApplyChainDamage(caster, effect, spell, target, currentEnemy, damage, initialHitPoints, notes, maze);
            notes.Add("🔁 Láncvarázs: a sebzés ingyen megismétlődött");
        }

        var currentDamage = 0;
        var actualDamage = 0;
        foreach (var entry in damage.Where(entry => entry.Value > 0))
        {
            if (inCombat && entry.Key == currentEnemy)
            {
                var inflicted = Math.Min(entry.Value, entry.Key.CurrentHitPoints);
                currentDamage += inflicted;
                actualDamage += inflicted;
            }
            else
            {
                actualDamage += Math.Min(entry.Value, entry.Key.CurrentHitPoints);
                onExplorationSpellDamage(caster, entry.Key, entry.Value, notes);
            }
        }
        if (actualDamage > 0 && caster.SpecializationId == ClassSpecializations.MageNecromancer)
        {
            var before = caster.CurrentVitality;
            var lifeStealPercent = caster.HasClassFeatureUpgrade(ClassFeatureUpgrades.MageLifeHarvest) ? 15 : 10;
            caster.RestoreVitality(Math.Max(1, (int)Math.Ceiling(actualDamage * lifeStealPercent / 100d)));
            var restored = caster.CurrentVitality - before;
            if (restored > 0) notes.Add($"💀 Nekromancia: ❤️ +{restored} HP");
        }
        else if (actualDamage > 0 && caster.HasClassFeatureUpgrade(ClassFeatureUpgrades.MageLifeHarvest))
        {
            var before = caster.CurrentVitality;
            caster.RestoreVitality(Math.Max(1, (int)Math.Ceiling(actualDamage * 0.15)));
            var restored = caster.CurrentVitality - before;
            if (restored > 0) notes.Add($"💀 Életaratás: ❤️ +{restored} HP");
        }
        if (actualDamage > 0 && caster.HasClassFeatureUpgrade(ClassFeatureUpgrades.PriestMercifulJudgment))
        {
            var before = caster.CurrentVitality;
            caster.RestoreVitality(Math.Max(1, (int)Math.Ceiling(actualDamage * 0.10)));
            var restored = caster.CurrentVitality - before;
            if (restored > 0) notes.Add($"⚖️ Irgalmas ítélet: ❤️ +{restored} HP");
        }
        if (damage.Values.Any(value => value > 0)) caster.BreakInvisibility();
        if (!inCombat && onRefreshCharacterSheet is not null) onRefreshCharacterSheet(caster);
        return new SpellExecutionResult(currentDamage, extraActions,
            notes.Count == 0 ? "A varázslat nem talált érvényes célpontot." : string.Join("; ", notes.Distinct()),
            new BattleActionDetails(Guid.NewGuid(), caster.Name, spell.Name,
                [$"✨ {spell.Name}", $"💥 Összes sebzés: {damage.Values.Sum()}",
                 effects.Any(effect => effect.Resolution == SpellResolution.Attack)
                    ? $"🎲 Kritikus: 5% / cél — {(_criticalOccurred ? "KRITIKUS!" : "nem")}" :
                      "🎲 Kritikus: nem alkalmazható"],
                _calculation.Concat(notes).ToArray()));
    }

    public IEnumerable<Enemy> ResolveEnemySpellTargets(SpellDefinition spell, Position target, Enemy? currentEnemy,
        Position casterPosition, Maze maze)
    {
        var enemies = maze.Enemies.Where(enemy => enemy.CurrentHitPoints > 0);
        return spell.TargetType switch
        {
            SpellTargetType.Enemy => enemies.Where(enemy => enemy.Position == target)
                .Concat(currentEnemy is not null && currentEnemy.Position == target ? [currentEnemy] : []).Distinct(),
            SpellTargetType.Area => enemies.Where(enemy => Chebyshev(enemy.Position, target) <= spell.AreaRadius),
            SpellTargetType.Direction => enemies.Where(enemy => IsInSpellCone(casterPosition, enemy.Position, target)),
            _ => []
        };
    }

    public IEnumerable<LiveCharacter> ResolveCharacterSpellTargets(LiveCharacter caster, SpellDefinition spell,
        Position target, IReadOnlyList<(LiveCharacter Character, Position Position)> livingParty, Maze maze) => spell.TargetType switch
    {
        SpellTargetType.Self => [caster],
        SpellTargetType.Party => livingParty.Select(entry => entry.Character).DistinctBy(character => character.Id),
        SpellTargetType.PartyMember => livingParty.Where(entry => entry.Position == target)
            .Select(entry => entry.Character).Take(1),
        _ => []
    };

    public static bool IsInSpellCone(Position casterPosition, Position position, Position selectedDirection)
    {
        var dx = selectedDirection.X - casterPosition.X;
        var dy = selectedDirection.Y - casterPosition.Y;
        var relativeX = position.X - casterPosition.X;
        var relativeY = position.Y - casterPosition.Y;
        var forward = relativeX * dx + relativeY * dy;
        var lateral = Math.Abs(relativeX * dy - relativeY * dx);
        return forward is >= 1 and <= 2 && lateral <= forward - 1;
    }

    public int ResolveSpellDamage(LiveCharacter caster, SpellEffectDefinition effect, SpellDefinition spell,
        Enemy enemy, Dictionary<(Enemy Enemy, SpellResolution Resolution), SpellResolutionResult> cache,
        List<string> notes, bool divineJudgment = false)
    {
        var resolution = ResolveAgainstEnemy(caster, effect, spell, enemy, cache);
        if (!resolution.Applies)
        {
            notes.Add($"{enemy.Name}: a varázslat célt tévesztett ({resolution.Text})");
            return 0;
        }
        var diceRoll = effect.Dice?.Roll(_random) ?? 0;
        _calculation.Add($"💥 {enemy.Name}: dobás {diceRoll}; INT {caster.EffectiveAbilities.Intelligence} ×{effect.IntelligenceMultiplier}");
        _calculation.Add($"💥 Szint {caster.Level} ×{effect.LevelMultiplier}; alap +{effect.Value}");
        var rolled = diceRoll +
                     (int)Math.Round(caster.EffectiveAbilities.Intelligence * effect.IntelligenceMultiplier) +
                     caster.Level * effect.LevelMultiplier + effect.Value;
        if (caster.HasPerk(PerkIds.MageElementalMaster)) { rolled = (int)Math.Ceiling(rolled * 1.25); _calculation.Add("💥 Elemi mester ×1,25 ↑"); }
        if (caster.SpecializationId == ClassSpecializations.PriestJudgment && spell.School == SpellSchool.Divine)
            { rolled = (int)Math.Ceiling(rolled * 1.20); _calculation.Add("💥 Specializáció ×1,20 ↑"); }
        if (caster.SpecializationId == ClassSpecializations.MageElementalist && spell.School == SpellSchool.Arcane)
            { rolled = (int)Math.Ceiling(rolled * 1.20); _calculation.Add("💥 Specializáció ×1,20 ↑"); }
        if (caster.HasClassFeatureUpgrade(ClassFeatureUpgrades.MageRagingElements) && spell.School == SpellSchool.Arcane)
            { rolled = (int)Math.Ceiling(rolled * 1.15); _calculation.Add("💥 Tomboló elemek ×1,15 ↑"); }
        if (IsHolyEffect(effect) && IsUnholy(enemy.Definition))
        {
            rolled = (int)Math.Ceiling(rolled * 1.5);
            _calculation.Add("✨ Szent sebezhetőség ×1,50 ↑");
            notes.Add($"{enemy.Name}: ✨ szent sebezhetőség +50%");
        }
        if (divineJudgment) { rolled *= 2; _calculation.Add("⚡ Isteni ítélet ×2"); }
        if (resolution.Critical) { rolled *= 2; _calculation.Add("🎲 KRITIKUS ×2"); }
        if (resolution.Half) { rolled = Math.Max(1, rolled / 2); _calculation.Add("🛡️ Sikeres mentő: felezés ↓, min. 1"); }
        notes.Add($"{enemy.Name}: -{rolled} HP ({resolution.Text})");
        return rolled;
    }

    public SpellResolutionResult ResolveAgainstEnemy(LiveCharacter caster, SpellEffectDefinition effect,
        SpellDefinition spell, Enemy enemy,
        Dictionary<(Enemy Enemy, SpellResolution Resolution), SpellResolutionResult> cache)
    {
        if (effect.Resolution == SpellResolution.Auto) return new SpellResolutionResult(true, false, false, "automatikus");
        var cacheResolution = effect.Resolution == SpellResolution.SaveNegates
            ? SpellResolution.SaveHalf
            : effect.Resolution;
        var key = (enemy, cacheResolution);
        if (cache.TryGetValue(key, out var cached))
            return effect.Resolution == SpellResolution.SaveNegates && cached.Half
                ? cached with { Applies = false, Half = false }
                : cached with { Half = effect.Resolution == SpellResolution.SaveHalf && cached.Half };
        SpellResolutionResult result;
        if (effect.Resolution == SpellResolution.Attack)
        {
            var roll = _random.Next(1, 21);
            var bonus = caster.EffectiveAbilities.Intelligence +
                        (caster.HasPerk(PerkIds.MageArcaneFocus) ? 2 : 0) +
                        caster.GetMagicItemBonus(MagicItemEffect.Hit) +
                        caster.SpellEffectValue(ActiveSpellEffectType.Invisibility) +
                        caster.SpellEffectValue(ActiveSpellEffectType.HitBonus);
            _calculation.Add($"🎯 {enemy.Name}: d20={roll}; INT {caster.EffectiveAbilities.Intelligence}");
            _calculation.Add($"🎯 Arkán fókusz +{(caster.HasPerk(PerkIds.MageArcaneFocus) ? 2 : 0)}; tárgy +{caster.GetMagicItemBonus(MagicItemEffect.Hit)}");
            _calculation.Add($"🎯 Láthatatlanság +{caster.SpellEffectValue(ActiveSpellEffectType.Invisibility)}; varázshatás +{caster.SpellEffectValue(ActiveSpellEffectType.HitBonus)}");
            _calculation.Add($"🎯 {roll}+{bonus} vs 11+{enemy.EffectiveSpeed}");
            _calculation.Add("🎲 Természetes 20: 5%, ×2; természetes 1: mellé");
            var hit = roll == 20 || roll != 1 && roll + bonus >= 11 + enemy.EffectiveSpeed;
            if (roll == 20) _criticalOccurred = true;
            result = new SpellResolutionResult(hit, false, roll == 20, hit ? $"mágikus támadás {roll + bonus}" : $"mellé {roll + bonus}");
        }
        else
        {
            var dc = 10 + caster.EffectiveAbilities.Intelligence / 2 + spell.Level;
            var roll = _random.Next(1, 21) + enemy.EffectiveSpeed;
            _calculation.Add($"🛡️ Mentő: d20+gyorsaság={roll}; cél 10+INT/2+szint={dc}");
            var saved = roll >= dc;
            result = new SpellResolutionResult(!saved || effect.Resolution == SpellResolution.SaveHalf,
                saved && effect.Resolution == SpellResolution.SaveHalf, false,
                saved ? $"sikeres ellenpróba {roll}/{dc}" : $"rontott ellenpróba {roll}/{dc}");
        }
        cache[key] = result;
        return result;
    }

    public void ApplyEnemyTimedEffect(LiveCharacter caster, SpellEffectDefinition effect, SpellDefinition spell,
        IEnumerable<Enemy> targets, ActiveSpellEffectType type,
        Dictionary<(Enemy Enemy, SpellResolution Resolution), SpellResolutionResult> cache,
        List<string> notes, bool divineJudgment = false)
    {
        foreach (var enemy in targets)
        {
            var resolution = ResolveAgainstEnemy(caster, effect, spell, enemy, cache);
            if (!resolution.Applies || _random.Next(100) >= effect.ChancePercent) continue;
            enemy.ApplySpellEffect(new ActiveSpellEffect(spell.Id, type, effect.Value,
                AdjustedDuration(caster, spell, effect, divineJudgment),
                effect.Dice, (int)Math.Round(caster.EffectiveAbilities.Intelligence * effect.IntelligenceMultiplier),
                false, effect.Dice is not null && caster.HasPerk(PerkIds.MageElementalMaster) ? 125 : 100));
            notes.Add($"{enemy.Name}: {TimedEffectName(type)} ({AdjustedDuration(caster, spell, effect, divineJudgment)} akció)");
        }
    }

    public static string TimedEffectName(ActiveSpellEffectType type) => type switch
    {
        ActiveSpellEffectType.Burning => "🔥 égés",
        ActiveSpellEffectType.Storm => "⚡ vihar",
        ActiveSpellEffectType.SpeedPenalty => "🐌 lassítás",
        ActiveSpellEffectType.SkipAlternate => "⏳ minden második akció kimarad",
        ActiveSpellEffectType.SkipNext => "⏳ következő akció kimarad",
        ActiveSpellEffectType.Frost => "❄️ fagyás",
        _ => "varázshatás"
    };

    public void ApplyCharacterEffect(LiveCharacter caster, LiveCharacter character, SpellEffectDefinition effect,
        SpellDefinition spell, ActiveSpellEffectType type, bool divineJudgment = false)
    {
        var multiplier = divineJudgment ? 200 : 100;
        if (type == ActiveSpellEffectType.GuardianAngel && caster.HasPerk(PerkIds.PriestHealingGrace))
            multiplier = multiplier * 125 / 100;
        character.ApplySpellEffect(new ActiveSpellEffect(spell.Id, type,
            effect.Value, AdjustedDuration(caster, spell, effect, divineJudgment), effect.Dice,
            (int)Math.Round(caster.EffectiveAbilities.Intelligence * effect.IntelligenceMultiplier), true,
            multiplier, effect.Parameter));
    }

    public void ApplyCharacterEffects(LiveCharacter caster, IEnumerable<LiveCharacter> characters,
        SpellEffectDefinition effect, SpellDefinition spell, ActiveSpellEffectType type, bool divineJudgment)
    {
        foreach (var character in characters) ApplyCharacterEffect(caster, character, effect, spell, type, divineJudgment);
    }

    public void ApplyHealing(LiveCharacter caster, SpellEffectDefinition effect, SpellDefinition spell,
        IEnumerable<LiveCharacter> characters, bool divineJudgment, ICollection<string> notes)
    {
        foreach (var character in characters.Where(character => character.IsAlive))
        {
            var fullHealing = string.Equals(effect.Parameter, "Full", StringComparison.OrdinalIgnoreCase);
            var healingDice = fullHealing ? 0 : effect.Dice?.Roll(_random) ?? 0;
            _calculation.Add(fullHealing ? "❤️ Teljes gyógyítás" :
                $"❤️ Dobás {healingDice} + INT {caster.EffectiveAbilities.Intelligence}×{effect.IntelligenceMultiplier} + szint {caster.Level}×{effect.LevelMultiplier} +{effect.Value}");
            var amount = fullHealing
                ? character.MaximumVitality
                : healingDice +
                  (int)Math.Round(caster.EffectiveAbilities.Intelligence * effect.IntelligenceMultiplier) +
                  caster.Level * effect.LevelMultiplier + effect.Value;
            if (!fullHealing && divineJudgment) { amount *= 2; _calculation.Add("⚡ Isteni ítélet ×2"); }
            if (caster.HasPerk(PerkIds.PriestHealingGrace))
                { amount = (int)Math.Ceiling(amount * 1.25); _calculation.Add("❤️ Gyógyító kegy ×1,25 ↑"); }
            if (caster.SpecializationId == ClassSpecializations.PriestLife)
                { amount = (int)Math.Ceiling(amount * 1.25); _calculation.Add("❤️ Élet specializáció ×1,25 ↑"); }
            if (caster.HasClassFeatureUpgrade(ClassFeatureUpgrades.PriestOverflowingLife))
                { amount = (int)Math.Ceiling(amount * 1.15); _calculation.Add("❤️ Túláradó élet ×1,15 ↑"); }
            var before = character.CurrentVitality;
            character.RestoreVitality(amount);
            _calculation.Add($"❤️ {character.Name}: {amount} → tényleges {character.CurrentVitality - before}; túltöltés {Math.Max(0, amount - (character.CurrentVitality - before))}");
            notes.Add($"{character.Name}: {FormatHealingResult(character, amount, before)}");
        }
    }

    public void ApplyStatusCure(SpellEffectDefinition effect, IEnumerable<LiveCharacter> characters,
        ICollection<string> notes)
    {
        var statusIds = ParseEffectParameters(effect.Parameter);
        foreach (var character in characters)
        {
            var removed = statusIds.Where(character.RemoveStatus).Select(StatusName).ToList();
            if (removed.Count > 0) notes.Add($"{character.Name}: ✨ megszűnt {string.Join(" és ", removed)}");
        }
    }

    public void ApplyNeedRestoration(IEnumerable<LiveCharacter> characters, SpellEffectDefinition effect,
        bool divineJudgment, ICollection<string> notes)
    {
        var amount = effect.Value * (divineJudgment ? 2 : 1);
        foreach (var character in characters.Where(character => character.IsAlive))
        {
            var foodBefore = character.FoodLevel;
            var waterBefore = character.WaterLevel;
            character.RestoreFood(amount);
            character.RestoreWater(amount);
            character.SynchronizeNeedStatuses(_gameData.GetStatus(CharacterStatusIds.Hungry),
                _gameData.GetStatus(CharacterStatusIds.Thirsty));
            notes.Add($"{character.Name}: 🍖+{character.FoodLevel - foodBefore} 💧+{character.WaterLevel - waterBefore}");
        }
    }

    public void ApplyChainDamage(LiveCharacter caster, SpellEffectDefinition effect, SpellDefinition spell,
        Position target, Enemy? currentEnemy, Dictionary<Enemy, int> damage,
        Dictionary<Enemy, int> initialHitPoints, List<string> notes, Maze maze)
    {
        var candidates = maze.Enemies.Where(enemy => enemy.CurrentHitPoints > 0)
            .Concat(currentEnemy is null ? [] : [currentEnemy]).Distinct()
            .OrderBy(enemy => enemy.Position == target ? 0 : Chebyshev(enemy.Position, target))
            .Where(enemy => enemy.Position == target || Chebyshev(enemy.Position, target) <= 4).Take(4).ToList();
        var multipliers = (effect.Parameter ?? "100|75|50|25").Split('|')
            .Select(value => int.TryParse(value, out var parsed) ? parsed : 100).ToArray();
        for (var index = 0; index < candidates.Count; index++)
        {
            var enemy = candidates[index];
            if (!damage.ContainsKey(enemy)) damage[enemy] = 0;
            if (!initialHitPoints.ContainsKey(enemy)) initialHitPoints[enemy] = enemy.CurrentHitPoints;
            var baseDamage = ResolveSpellDamage(caster, effect, spell, enemy,
                new Dictionary<(Enemy, SpellResolution), SpellResolutionResult>(), notes);
            damage[enemy] += baseDamage * multipliers[Math.Min(index, multipliers.Length - 1)] / 100;
        }
    }

    public void ApplyRandomElement(LiveCharacter caster, SpellEffectDefinition effect, SpellDefinition spell,
        IEnumerable<Enemy> targets, Dictionary<Enemy, int> damage,
        Dictionary<(Enemy Enemy, SpellResolution Resolution), SpellResolutionResult> cache,
        List<string> notes)
    {
        var element = (effect.Parameter ?? "Fire|Frost|Lightning").Split('|')[_random.Next(3)];
        foreach (var enemy in targets)
        {
            if (!ResolveAgainstEnemy(caster, effect, spell, enemy, cache).Applies) continue;
            if (element.Equals("Fire", StringComparison.OrdinalIgnoreCase))
            {
                var fireDamage = effect.Dice?.Roll(_random) ?? 0;
                if (caster.HasPerk(PerkIds.MageElementalMaster))
                    fireDamage = (int)Math.Ceiling(fireDamage * 1.25);
                damage[enemy] += fireDamage;
                notes.Add($"{enemy.Name}: 🔥 -{fireDamage} HP");
            }
            else if (element.Equals("Frost", StringComparison.OrdinalIgnoreCase))
                enemy.ApplySpellEffect(new ActiveSpellEffect(spell.Id, ActiveSpellEffectType.Frost, effect.Value, effect.Duration));
            else if (_random.Next(100) < effect.ChancePercent)
                enemy.ApplySpellEffect(new ActiveSpellEffect(spell.Id, ActiveSpellEffectType.SkipAlternate, 0, effect.Duration));
        }
        notes.Add($"🎲 véletlen elem: {element}");
    }

    public string DispelAt(Position target, int radius, Maze maze,
        IReadOnlyList<(LiveCharacter Character, Position Position)> livingParty)
    {
        var removed = maze.Enemies.Where(enemy => Chebyshev(enemy.Position, target) <= radius)
            .Sum(enemy => enemy.RemoveSpellEffects());
        foreach (var member in livingParty.Where(member => Chebyshev(member.Position, target) <= radius))
            removed += member.Character.RemoveSpellEffects();
        return $"✨ szétoszlatott varázshatások: {removed}";
    }

    public static Position? FindResurrectionPosition(Maze maze, Position? playerPosition, PartyMemberCorpse corpse)
    {
        bool CanUse(Position position) => (!playerPosition.HasValue || position != playerPosition.Value) &&
            position != maze.Entrance && position != maze.Exit && maze.IsWalkable(position) &&
            (maze.GetObjectAt(position) is null || maze.GetObjectAt(position) == corpse);
        if (CanUse(corpse.Position)) return corpse.Position;
        return FindNearbyTeleportPositions(maze, playerPosition, corpse.Position).Where(CanUse).Select(position => (Position?)position).FirstOrDefault();
    }

    public static IEnumerable<Position> FindNearbyTeleportPositions(Maze maze, Position? playerPosition, Position origin)
    {
        var queue = new Queue<Position>();
        var visited = new HashSet<Position> { origin };
        queue.Enqueue(origin);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (!visited.Add(next) || !maze.IsWalkable(next)) continue;
                queue.Enqueue(next);
                if ((!playerPosition.HasValue || next != playerPosition.Value) && maze.GetObjectAt(next) is null) yield return next;
            }
        }
    }

    public static int AdjustedDuration(LiveCharacter caster, SpellDefinition spell,
        SpellEffectDefinition effect, bool divineJudgment)
    {
        var duration = divineJudgment ? effect.Duration * 2 : effect.Duration;
        var priestProtection = caster.SpecializationId == ClassSpecializations.PriestProtection &&
                               spell.School == SpellSchool.Divine && effect.Type is
                                   SpellEffectType.DefenseBonus or SpellEffectType.PhysicalReduction or
                                   SpellEffectType.BleedingImmunity or SpellEffectType.HitBonus or
                                   SpellEffectType.DamageBonus or SpellEffectType.InitiativeBonus or
                                   SpellEffectType.ProtectionFromEvil or SpellEffectType.GuardianAngel or
                                   SpellEffectType.Sanctuary;
        var mageIllusion = caster.SpecializationId == ClassSpecializations.MageIllusionist &&
                           spell.School == SpellSchool.Arcane && effect.Type is
                               SpellEffectType.Invisibility or SpellEffectType.DefenseBonus or
                               SpellEffectType.PhysicalReduction or SpellEffectType.BleedingImmunity or
                               SpellEffectType.SpeedPenalty or SpellEffectType.SkipAlternate;
        var bonusDuration = 0;
        if (duration > 0 && priestProtection) bonusDuration++;
        if (duration > 0 && mageIllusion) bonusDuration++;
        if (duration > 0 && caster.HasClassFeatureUpgrade(ClassFeatureUpgrades.PriestSteadfastProtection) &&
            spell.School == SpellSchool.Divine && effect.Type is
                SpellEffectType.DefenseBonus or SpellEffectType.PhysicalReduction or
                SpellEffectType.BleedingImmunity or SpellEffectType.HitBonus or
                SpellEffectType.DamageBonus or SpellEffectType.InitiativeBonus or
                SpellEffectType.ProtectionFromEvil or SpellEffectType.GuardianAngel or SpellEffectType.Sanctuary)
            bonusDuration++;
        if (duration > 0 && caster.HasClassFeatureUpgrade(ClassFeatureUpgrades.MagePerfectIllusion) &&
            spell.School == SpellSchool.Arcane && effect.Type is
                SpellEffectType.Invisibility or SpellEffectType.DefenseBonus or
                SpellEffectType.PhysicalReduction or SpellEffectType.BleedingImmunity or
                SpellEffectType.SpeedPenalty or SpellEffectType.SkipAlternate)
            bonusDuration++;
        return duration + bonusDuration;
    }

    public static IReadOnlyList<string> ParseEffectParameters(string? parameter) =>
        string.IsNullOrWhiteSpace(parameter)
            ? []
            : parameter.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    public string StatusName(string statusId) => _gameData.Statuses.FirstOrDefault(status =>
        string.Equals(status.Id, statusId, StringComparison.OrdinalIgnoreCase))?.Name ?? statusId;

    public static bool IsHolyEffect(SpellEffectDefinition effect) =>
        string.Equals(effect.Parameter, "Holy50", StringComparison.OrdinalIgnoreCase);

    public static bool IsUnholy(EnemyDefinition enemy) =>
        enemy.HasTrait(EnemyTraits.Undead) || enemy.HasTrait(EnemyTraits.Demonic);

    private static string FormatHealingResult(LiveCharacter character, int amount, int before)
    {
        var gained = character.CurrentVitality - before;
        return gained > 0
            ? $"❤️ +{gained} HP ({character.CurrentVitality}/{character.MaximumVitality})"
            : "teljes életerőn van";
    }

    private static string DirectionName(Position origin, Position position) => position.X < origin.X ? "bal" :
        position.X > origin.X ? "jobb" : position.Y < origin.Y ? "fel" : "le";

    private static int Chebyshev(Position first, Position second) =>
        Math.Max(Math.Abs(first.X - second.X), Math.Abs(first.Y - second.Y));

    private static int Manhattan(Position first, Position second) =>
        Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);

    private static readonly Direction[] Directions = [Direction.Up, Direction.Right, Direction.Down, Direction.Left];
}
