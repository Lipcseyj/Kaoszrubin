using KaoszRubin.Application;
using KaoszRubin.Data;
using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;
using KaoszRubin.UI;

namespace KaoszRubin.Combat;

public sealed class TacticalTeamBattleCoordinator
{
    private readonly GameDataCatalog _gameData;
    private readonly BattleSystem _battleSystem;
    private readonly Random _random;

    public TacticalTeamBattleCoordinator(
        GameDataCatalog gameData,
        BattleSystem battleSystem,
        Random random)
    {
        _gameData = gameData;
        _battleSystem = battleSystem;
        _random = random;
    }

    public static bool IsQuestImportantEnemy(Enemy enemy) =>
        enemy.Definition.IsBoss || enemy.Definition.Rank is EnemyRank.MiniBoss or EnemyRank.Boss ||
        enemy.GroupId?.StartsWith("QUEST:", StringComparison.OrdinalIgnoreCase) == true;

    public static Enemy ClosestLivingTeamEnemy(TeamBattleEncounter battle, Position origin) =>
        battle.Enemies.Where(enemy => enemy.CurrentHitPoints > 0)
            .OrderBy(enemy => TacticalDistance.Between(origin, enemy.Position))
            .ThenBy(enemy => enemy.CurrentHitPoints).First();

    public static IEnumerable<Enemy> AdjacentTeamEnemies(TeamBattleEncounter battle, LiveCharacter character, Position characterPosition) =>
        battle.Enemies.Where(enemy => enemy.CurrentHitPoints > 0 &&
            TacticalDistance.IsMeleeAdjacent(characterPosition, enemy.Position));

    public static IEnumerable<Enemy> ReachableTeamEnemies(TeamBattleEncounter battle, LiveCharacter character, Position characterPosition)
    {
        var adjacent = AdjacentTeamEnemies(battle, character, characterPosition).ToArray();
        return adjacent.Concat(battle.RearFormationEnemiesInReach(character))
            .Where(enemy => enemy.CurrentHitPoints > 0)
            .DistinctBy(enemy => enemy.Id);
    }

    public static IReadOnlyList<Enemy> SweepTargets(TeamBattleEncounter battle, LiveCharacter character,
        Position origin, Enemy primary)
    {
        var targets = new List<Enemy> { primary };
        var maximum = Math.Clamp(character.AttackWeapon?.MaximumTargets ?? 1, 1, 4);
        if (maximum == 1 || !TacticalDistance.IsMeleeAdjacent(origin, primary.Position)) return targets;
        foreach (var enemy in battle.Enemies.Where(enemy => enemy.CurrentHitPoints > 0 && enemy.Id != primary.Id)
                     .OrderBy(enemy => enemy.Position.Y).ThenBy(enemy => enemy.Position.X))
        {
            if (!TacticalDistance.IsMeleeAdjacent(origin, enemy.Position) ||
                !targets.All(target => TacticalDistance.IsMeleeAdjacent(target.Position, enemy.Position))) continue;
            targets.Add(enemy);
            if (targets.Count == maximum) break;
        }
        return targets;
    }

    public static IEnumerable<LiveCharacter> AdjacentTeamCharacters(TeamBattleEncounter battle, Enemy enemy,
        Func<LiveCharacter, Position> getCasterPosition) =>
        battle.Characters.Where(character => character.IsAlive &&
            TacticalDistance.IsMeleeAdjacent(getCasterPosition(character), enemy.Position) &&
            !battle.IsProtectedRearTarget(character, enemy.Position));

    public static IReadOnlyList<LiveCharacter> EnemyAttackTargets(TeamBattleEncounter battle, Enemy enemy,
        WeaponDefinition? weapon, Func<LiveCharacter, Position> getCharacterPosition)
    {
        var maximumRange = weapon?.CanAttackFromRear == true ? 2 : 1;
        bool InRange(LiveCharacter character)
        {
            var position = getCharacterPosition(character);
            return maximumRange == 1
                ? TacticalDistance.IsMeleeAdjacent(enemy.Position, position)
                : TacticalDistance.IsWithin(enemy.Position, position, maximumRange) && position != enemy.Position;
        }

        var directCandidates = TeamEnemyTargets(battle, enemy).Where(InRange)
            .OrderBy(character => (double)character.CurrentVitality / Math.Max(1, character.MaximumVitality))
            .ThenBy(character => TacticalDistance.Between(enemy.Position, getCharacterPosition(character)))
            .ToArray();
        if (directCandidates.Length == 0) return [];

        var targets = new List<LiveCharacter> { directCandidates[0] };
        var maximumTargets = Math.Clamp(weapon?.MaximumTargets ?? 1, 1, 4);
        if (maximumTargets == 1) return targets;
        var nearbyCandidates = battle.Characters.Where(character => character.IsAlive && character != targets[0])
            .OrderBy(character => battle.IsProtectedRearTarget(character, enemy.Position))
            .ThenBy(character => TacticalDistance.Between(getCharacterPosition(targets[0]),
                getCharacterPosition(character)))
            .ToArray();
        foreach (var candidate in nearbyCandidates)
        {
            if (!targets.Any(target => AreNeighboringTargets(getCharacterPosition(target),
                    getCharacterPosition(candidate)))) continue;
            targets.Add(candidate);
            if (targets.Count == maximumTargets) break;
        }
        return targets;
    }

    private static bool AreNeighboringTargets(Position first, Position second) =>
        TacticalDistance.IsMeleeAdjacent(first, second) || TacticalDistance.Between(first, second) <= 1;

    public static IEnumerable<LiveCharacter> TeamEnemyTargets(TeamBattleEncounter battle, Enemy enemy)
    {
        var exposed = battle.Characters.Where(character => character.IsAlive &&
            !battle.IsProtectedRearTarget(character, enemy.Position)).ToArray();
        return exposed.Length > 0 ? exposed : battle.Characters.Where(character => character.IsAlive);
    }

    public static IEnumerable<Position> TeamMeleePositions(Position center)
    {
        for (var y = center.Y - 1; y <= center.Y + 1; y++)
        for (var x = center.X - 1; x <= center.X + 1; x++)
            if (x != center.X || y != center.Y)
                yield return new Position(x, y);
    }

    public static Enemy? NextTeamBattleTarget(TeamBattleEncounter battle, LiveCharacter character, Position characterPosition)
    {
        var targets = ReachableTeamEnemies(battle, character, characterPosition).OrderBy(enemy => enemy.Position.Y)
            .ThenBy(enemy => enemy.Position.X).ThenBy(enemy => enemy.Id.ToString(), StringComparer.Ordinal).ToArray();
        if (targets.Length == 0) return null;
        var currentTargetId = battle.SelectedTargetEnemyId ??
                              targets.OrderBy(enemy => enemy.CurrentHitPoints).First().Id;
        var selectedIndex = Array.FindIndex(targets, enemy => enemy.Id == currentTargetId);
        return targets[(selectedIndex + 1) % targets.Length];
    }

    public static IReadOnlyList<BattleItemOptionSnapshot> GetBattleItemOptions(TeamBattleEncounter battle,
        LiveCharacter character)
    {
        if (battle.IsEngaged(character)) return [];
        return Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount)
            .Select(index => (Index: index, Item: character.GetInventoryItem(InventorySlotKind.Backpack, index),
                Quantity: character.GetInventoryItemQuantity(InventorySlotKind.Backpack, index)))
            .Where(entry => entry.Item is MiscItemDefinition item && entry.Quantity > 0 &&
                            battle.CanUseItem(character, item) && IsTeamBattleItemUseful(character, item))
            .GroupBy(entry => entry.Item!.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => new BattleItemOptionSnapshot(group.Min(entry => entry.Index), group.Key,
                group.First().Item!.Name, group.Sum(entry => entry.Quantity)))
            .ToArray();
    }

    public static bool IsTeamBattleItemUseful(LiveCharacter character, MiscItemDefinition item) =>
        !item.UsableInCombat ? false : string.Equals(item.Id, MiscItemIds.HerbalTea, StringComparison.OrdinalIgnoreCase)
            ? character.WaterLevel < 100 || character.CurrentVitality < character.MaximumVitality
            : IsInitiativeDrink(item) || item.Effect switch
            {
                ConsumableEffect.Food => character.FoodLevel < 100,
                ConsumableEffect.Water => character.WaterLevel < 100,
                ConsumableEffect.Heal => character.CurrentVitality < character.MaximumVitality,
                ConsumableEffect.RestoreMana => character.UsesMana && character.CurrentMana < character.MaximumMana,
                ConsumableEffect.CurePoison => character.HasStatus(CharacterStatusIds.Poisoned),
                ConsumableEffect.CureDisease => character.HasStatus(CharacterStatusIds.Diseased),
                ConsumableEffect.StopBleeding => character.HasStatus(CharacterStatusIds.Bleeding),
                ConsumableEffect.Vision => true,
                _ => false
            };

    private static bool IsInitiativeDrink(MiscItemDefinition item) =>
        string.Equals(item.Id, "T023", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(item.Id, "T024", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<BattleActionKind> GetTeamAllowedBattleActions(TeamBattleEncounter battle,
        LiveCharacter character, Enemy focusEnemy, LiveCharacter selectedCharacter,
        Position characterPosition, bool hasUsableCombatSpell, HashSet<LiveCharacter> turnUndeadUsedThisBattle)
    {
        var runtime = battle.RuntimeFor(character);
        if (runtime.RequiresTacticSelection)
            return character.CharacterClass.Id == CharacterClassIds.Harcos
                ? [BattleActionKind.FighterPrecise, BattleActionKind.FighterPowerful, BattleActionKind.FighterDefensive]
                : [BattleActionKind.ThiefAmbush, BattleActionKind.ThiefObserve, BattleActionKind.ThiefPoison];
        var reachable = ReachableTeamEnemies(battle, character, characterPosition).ToArray();
        if (battle.Turns.Cycle == 1 && character.Id == battle.InitiatingCharacterId)
        {
            var openingActions = new List<BattleActionKind> { BattleActionKind.Pass };
            if (reachable.Length > 0) openingActions.Insert(0, BattleActionKind.PhysicalAttack);
            else if (battle.HasActiveFormation && character == selectedCharacter)
                openingActions.Insert(0, BattleActionKind.MoveFormation);
            else openingActions.Insert(0, BattleActionKind.Move);
            if (hasUsableCombatSpell)
                openingActions.Insert(0, BattleActionKind.CastSpell);
            if (character.CanSwapReserveWeapon) openingActions.Add(BattleActionKind.SwapWeapon);
            return openingActions;
        }
        var actions = new List<BattleActionKind> { BattleActionKind.Pass };
        if (character.CanSwapReserveWeapon) actions.Add(BattleActionKind.SwapWeapon);
        if (reachable.Length > 0)
        {
            actions.Add(BattleActionKind.PhysicalAttack);
            if (reachable.Length > 1) actions.Add(BattleActionKind.SelectTarget);
            if (SingleBattleCoordinator.CanTurnUndead(character, focusEnemy) && !turnUndeadUsedThisBattle.Contains(character))
                actions.Add(BattleActionKind.TurnUndead);
        }
        if (battle.HasActiveFormation && battle.IsFrontRow(character) &&
            battle.RearPartnerOf(character) is { IsAlive: true })
            actions.Add(BattleActionKind.SwapToRear);
        if (battle.HasActiveFormation && character == selectedCharacter)
            actions.Add(BattleActionKind.MoveFormation);
        if (hasUsableCombatSpell)
            actions.Add(BattleActionKind.CastSpell);
        if (!battle.IsEngaged(character))
        {
            if (!battle.HasActiveFormation || battle.FormationSlotFor(character) is null)
                actions.Add(BattleActionKind.Move);
            if (GetBattleItemOptions(battle, character).Count > 0) actions.Add(BattleActionKind.UseItem);
        }
        if (character == selectedCharacter && battle.Turns.Cycle > 1)
            actions.Add(BattleActionKind.Retreat);
        return actions;
    }

    public IReadOnlyList<BattleTacticOptionSnapshot>? GetTeamBattleTacticOptions(TeamBattleEncounter battle,
        LiveCharacter character, Enemy enemy)
    {
        if (!battle.RuntimeFor(character).RequiresTacticSelection) return null;
        return character.CharacterClass.Id switch
        {
            CharacterClassIds.Harcos =>
            [
                new(BattleActionKind.FighterPrecise, "🎯 Pontos", "nagyobb találati esély, kisebb sebzés",
                    _battleSystem.EstimatePlayerHitChance(character, enemy, BattleTactic.FighterPrecise)),
                new(BattleActionKind.FighterPowerful, "💥 Erőteljes", "páncéltörés és nagyobb sebzés",
                    _battleSystem.EstimatePlayerHitChance(character, enemy, BattleTactic.FighterPowerful)),
                new(BattleActionKind.FighterDefensive, "🛡️ Védekező", "nagyobb védelem, kisebb sebzés",
                    _battleSystem.EstimatePlayerHitChance(character, enemy, BattleTactic.FighterDefensive))
            ],
            CharacterClassIds.Tolvaj =>
            [
                new(BattleActionKind.ThiefAmbush, "🗡️ Orvtámadás", "az első sikeres támadás dupla sebzés",
                    _battleSystem.EstimatePlayerHitChance(character, enemy, BattleTactic.ThiefAmbush)),
                new(BattleActionKind.ThiefObserve, "👁️ Megfigyelés", "+2 találat",
                    _battleSystem.EstimatePlayerHitChance(character, enemy, BattleTactic.ThiefObserve)),
                new(BattleActionKind.ThiefPoison, "☠️ Mérgezett penge", "+1–4 sebzés találatonként",
                    _battleSystem.EstimatePlayerHitChance(character, enemy, BattleTactic.ThiefPoison))
            ],
            _ => null
        };
    }

    public static double VitalityRatio(LiveCharacter character) =>
        (double)character.CurrentVitality / Math.Max(1, character.MaximumVitality);

    public static IEnumerable<Enemy> OrderedNpcSpellTargets(TeamBattleEncounter battle, Position casterPosition) =>
        battle.Enemies.Where(enemy => enemy.CurrentHitPoints > 0)
            .OrderByDescending(battle.IsEngaged)
            .ThenBy(enemy => battle.Characters.Where(character => battle.EngagedEnemies(character).Contains(enemy))
                .Select(VitalityRatio).DefaultIfEmpty(2d).Min())
            .ThenByDescending(enemy => enemy.Definition.Rank)
            .ThenByDescending(enemy => enemy.Definition.StrengthTier)
            .ThenBy(enemy => enemy.CurrentHitPoints)
            .ThenBy(enemy => TacticalDistance.Between(casterPosition, enemy.Position));

    public Position? ChooseNpcBuffTarget(TeamBattleEncounter battle, LiveCharacter caster,
        Position casterPosition, SpellDefinition spell, IReadOnlyList<SpellEffectDefinition> effects,
        IReadOnlyList<LiveCharacter> allies,
        Func<LiveCharacter, Position> getCasterPosition,
        Func<LiveCharacter, Position, SpellDefinition, Position, Enemy?, bool> isValidExplicitSpellTarget)
    {
        bool NeedsBuff(LiveCharacter character) => effects
            .Where(effect => NpcSpellcastingPolicy.IsBuffEffect(effect.Type))
            .Select(effect => NpcSpellcastingPolicy.ActiveTypeFor(effect.Type))
            .Any(type => type is { } activeType && !character.HasSpellEffect(activeType));

        if (spell.TargetType == SpellTargetType.Self)
            return NeedsBuff(caster) ? casterPosition : null;
        if (spell.TargetType == SpellTargetType.Party)
        {
            var missing = allies.Count(NeedsBuff);
            return missing >= Math.Min(2, allies.Count) ? casterPosition : null;
        }
        if (spell.TargetType != SpellTargetType.PartyMember) return null;
        return allies.Where(NeedsBuff)
            .OrderByDescending(character => battle.IsEngaged(character))
            .ThenByDescending(battle.IsFrontRow)
            .ThenBy(VitalityRatio)
            .Where(character => isValidExplicitSpellTarget(caster, casterPosition, spell,
                getCasterPosition(character), null))
            .Select(character => (Position?)getCasterPosition(character)).FirstOrDefault();
    }
}
