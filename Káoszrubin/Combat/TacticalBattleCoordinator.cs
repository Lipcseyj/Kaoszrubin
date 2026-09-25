using KaoszRubin.Application;
using KaoszRubin.Data;
using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;
using KaoszRubin.UI;

namespace KaoszRubin.Combat;

public enum TacticalAttackArc { Front, Flank, Rear }
public enum WeaponAttackPattern { Single, Arc, Line, Compact, Cone }

public sealed record TacticalAttackAdvantage(TacticalAttackArc Arc, int HitBonus)
{
    public static readonly TacticalAttackAdvantage Front = new(TacticalAttackArc.Front, 0);
    public bool IsRear => Arc == TacticalAttackArc.Rear;
    public string Name => Arc switch
    {
        TacticalAttackArc.Flank => "Oldalbatámadás",
        TacticalAttackArc.Rear => "Hátbatámadás",
        _ => string.Empty
    };
}

public sealed class TacticalBattleCoordinator
{
    private readonly GameDataCatalog _gameData;
    private readonly BattleSystem _battleSystem;
    private readonly Random _random;

    public TacticalBattleCoordinator(
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

    public static bool ShouldNpcSwapToReserveWeapon(LiveCharacter character, bool engaged = false,
        bool currentWeaponHasTarget = true, bool reserveWeaponHasTarget = false)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (!character.CanSwapReserveWeapon ||
            character.GetInventoryItem(InventorySlotKind.Weapon, 2) is not WeaponDefinition reserve ||
            reserve.WeaponTypeId == "WT003" ||
            !character.IsInventoryItemOperational(InventorySlotKind.Weapon, 2) ||
            !RangedWeaponRules.HasAmmunition(character, reserve)) return false;
        var current = character.AttackWeapon;
        if (current is null) return true;
        if (engaged && current.IsRanged && !reserve.IsRanged) return true;
        if (current.IsRanged && !RangedWeaponRules.HasAmmunition(character, current)) return true;
        return !currentWeaponHasTarget && reserveWeaponHasTarget;
    }

    public static Enemy ClosestLivingEnemy(BattleEncounter battle, Position origin) =>
        battle.Enemies.Where(enemy => enemy.CurrentHitPoints > 0)
            .OrderBy(enemy => TacticalDistance.Between(origin, enemy.Position))
            .ThenBy(enemy => enemy.CurrentHitPoints).First();

    public static int PreferredSpellcasterRetreatDistance(IEnumerable<Enemy> livingEnemies) =>
        livingEnemies.Any(enemy => enemy.CurrentHitPoints > 0 &&
                                   enemy.Definition.HasTrait(EnemyTraits.Flying)) ? 8 : 6;

    public static bool CanRetreat(int friendlySpeed, int hostileSpeed,
        bool everyEnemyHasTwoCellGap, bool anyEnemyVisible) =>
        friendlySpeed > hostileSpeed ||
        friendlySpeed == hostileSpeed && everyEnemyHasTwoCellGap ||
        friendlySpeed < hostileSpeed && !anyEnemyVisible;

    public static IEnumerable<Enemy> AdjacentEnemies(BattleEncounter battle, LiveCharacter character, Position characterPosition) =>
        battle.Enemies.Where(enemy => enemy.CurrentHitPoints > 0 &&
            TacticalDistance.IsMeleeAdjacent(characterPosition, enemy.Position));

    public static IEnumerable<Enemy> ReachableEnemies(BattleEncounter battle, LiveCharacter character,
        Position characterPosition, Func<Position, Position, int, bool>? canSee = null)
    {
        var weapon = character.AttackWeapon;
        if (weapon?.IsRanged == true)
        {
            if (!RangedWeaponRules.HasAmmunition(character, weapon)) return [];
            return battle.Enemies.Where(enemy => enemy.CurrentHitPoints > 0 &&
                RangedWeaponRules.CanReach(weapon, TacticalDistance.Between(characterPosition, enemy.Position)) &&
                (canSee is null || canSee(characterPosition, enemy.Position, weapon.MaximumRange)));
        }

        var adjacent = AdjacentEnemies(battle, character, characterPosition).ToArray();
        return adjacent.Concat(battle.RearFormationEnemiesInReach(character))
            .Where(enemy => enemy.CurrentHitPoints > 0)
            .DistinctBy(enemy => enemy.Id);
    }

    public static IEnumerable<Enemy> TurnUndeadTargets(BattleEncounter battle, LiveCharacter character,
        Position characterPosition) =>
        battle.Enemies.Where(enemy => BattleActionCoordinator.CanTurnUndead(character, enemy, characterPosition));

    public static IReadOnlyList<Enemy> SweepTargets(BattleEncounter battle, LiveCharacter character,
        Position origin, Enemy primary)
    {
        var targets = new List<Enemy> { primary };
        var weapon = character.AttackWeapon;
        var maximum = Math.Clamp(weapon?.MaximumTargets ?? 1, 1, 4);
        if (maximum == 1 || !TacticalDistance.IsMeleeAdjacent(origin, primary.Position)) return targets;
        var pattern = AttackPattern(weapon);
        var directionX = Math.Sign(primary.Position.X - origin.X);
        var directionY = Math.Sign(primary.Position.Y - origin.Y);
        foreach (var enemy in battle.Enemies.Where(enemy => enemy.CurrentHitPoints > 0 && enemy.Id != primary.Id)
                     .OrderBy(enemy => enemy.Position.Y).ThenBy(enemy => enemy.Position.X))
        {
            var inPattern = pattern switch
            {
                WeaponAttackPattern.Line => enemy.Position == new Position(
                    primary.Position.X + directionX, primary.Position.Y + directionY),
                WeaponAttackPattern.Arc => TacticalDistance.IsMeleeAdjacent(origin, enemy.Position) &&
                                           TacticalDistance.IsMeleeAdjacent(primary.Position, enemy.Position),
                WeaponAttackPattern.Compact => TacticalDistance.IsMeleeAdjacent(origin, enemy.Position) &&
                                               TacticalDistance.IsMeleeAdjacent(primary.Position, enemy.Position),
                _ => false
            };
            if (!inPattern) continue;
            targets.Add(enemy);
            if (targets.Count == maximum) break;
        }
        return targets;
    }

    public static WeaponAttackPattern AttackPattern(WeaponDefinition? weapon) =>
        weapon?.AttackShape == WeaponAttackShape.Cone
            ? WeaponAttackPattern.Cone
            : WeaponFamilies.ForWeapon(weapon) switch
        {
            WeaponFamilies.Polearm when weapon?.MaximumTargets > 1 => WeaponAttackPattern.Line,
            WeaponFamilies.Axe when weapon?.MaximumTargets > 1 => WeaponAttackPattern.Arc,
            WeaponFamilies.Blunt when weapon is { IsTwoHanded: true, MaximumTargets: > 1 } =>
                WeaponAttackPattern.Compact,
            WeaponFamilies.Sword when weapon?.MaximumTargets > 1 => WeaponAttackPattern.Arc,
            _ => WeaponAttackPattern.Single
        };

    public static LiveCharacter? PolearmMasterControlling(BattleEncounter battle, Position position) =>
        battle.Characters.FirstOrDefault(character => character.IsAlive &&
            character.OperationalWeapons.Any(weapon => WeaponFamilies.ForWeapon(weapon) == WeaponFamilies.Polearm) &&
            character.WeaponProficiencyRankFor(WeaponFamilies.Polearm) == WeaponProficiencyRank.Master &&
            TacticalDistance.IsMeleeAdjacent(battle.PositionOf(character), position));

    public static TacticalAttackAdvantage AttackAdvantage(BattleEncounter battle,
        LiveCharacter attacker, Enemy defender)
    {
        var attackerPosition = battle.PositionOf(attacker);
        if (!TacticalDistance.IsMeleeAdjacent(attackerPosition, defender.Position))
            return TacticalAttackAdvantage.Front;
        var facingTarget = battle.EnemyFacingTarget(defender) ?? battle.Characters.FirstOrDefault(character =>
            character != attacker && character.IsAlive && battle.EngagedEnemies(character).Contains(defender));
        if (facingTarget is null || facingTarget == attacker) return TacticalAttackAdvantage.Front;
        var facingPosition = battle.PositionOf(facingTarget);
        var forwardX = Math.Sign(facingPosition.X - defender.Position.X);
        var forwardY = Math.Sign(facingPosition.Y - defender.Position.Y);
        var attackX = Math.Sign(attackerPosition.X - defender.Position.X);
        var attackY = Math.Sign(attackerPosition.Y - defender.Position.Y);
        if (forwardX == 0 && forwardY == 0 || attackX == 0 && attackY == 0)
            return TacticalAttackAdvantage.Front;
        var facingDotProduct = forwardX * attackX + forwardY * attackY;
        if (facingDotProduct < 0)
            return new TacticalAttackAdvantage(TacticalAttackArc.Rear, 2);
        if (facingDotProduct == 0)
            return new TacticalAttackAdvantage(TacticalAttackArc.Flank, 1);
        return TacticalAttackAdvantage.Front;
    }

    public static int SweepDamagePercent(LiveCharacter character, CharacterBattleChoices runtime,
        bool secondaryTarget)
    {
        if (!secondaryTarget) return 100;
        var family = WeaponFamilies.ForWeapon(character.AttackWeapon);
        if (family == WeaponFamilies.Axe &&
            character.WeaponProficiencyRankFor(family) == WeaponProficiencyRank.Master) return 100;
        if (character.CharacterClass.Id == CharacterClassIds.Barbár &&
            runtime.Context.BarbarianRageActionsRemaining > 0) return 100;
        if (character.CharacterClass.Id == CharacterClassIds.Harcos &&
            runtime.Context.Tactic == BattleTactic.FighterPowerful) return 100;
        return 75;
    }

    public static int AlliedGuardDefense(BattleEncounter battle, LiveCharacter protectedCharacter,
        Func<LiveCharacter, Position> getPosition)
    {
        var adjacent = battle.Characters.Where(guardian => guardian != protectedCharacter && guardian.IsAlive &&
            TacticalDistance.IsMeleeAdjacent(getPosition(guardian), getPosition(protectedCharacter))).ToArray();
        if (adjacent.Length == 0) return 0;
        var ownDisciplineDefense = protectedCharacter.HasTacticalDiscipline(TacticalDisciplines.Guardian) ? 1 : 0;
        var suppliedDefense = adjacent.Select(guardian =>
        {
            var hasShield = guardian.OperationalWeapons.Any(weapon =>
                WeaponFamilies.ForWeapon(weapon) == WeaponFamilies.Shield);
            var shieldDefense = !hasShield ? 0 : guardian.WeaponProficiencyRankFor(WeaponFamilies.Shield) switch
            {
                WeaponProficiencyRank.Master => guardian.CharacterClass.Id == CharacterClassIds.Lovag ? 3 : 2,
                WeaponProficiencyRank.Trained => guardian.CharacterClass.Id == CharacterClassIds.Lovag ? 2 : 1,
                _ => guardian.CharacterClass.Id == CharacterClassIds.Lovag ? 1 : 0
            };
            var fighterDefense = guardian.CharacterClass.Id == CharacterClassIds.Harcos &&
                                 battle.RuntimeFor(guardian).Context.Tactic == BattleTactic.FighterDefensive
                ? guardian.HasClassFeatureUpgrade(ClassFeatureUpgrades.FighterDefensive) ? 2 : 1
                : 0;
            var swordGuard = guardian.OperationalWeapons.Any(weapon =>
                                 WeaponFamilies.ForWeapon(weapon) == WeaponFamilies.Sword) &&
                             guardian.WeaponProficiencyRankFor(WeaponFamilies.Sword) == WeaponProficiencyRank.Master
                ? 1
                : 0;
            var disciplineDefense = guardian.HasTacticalDiscipline(TacticalDisciplines.Guardian) ? 1 : 0;
            return Math.Max(Math.Max(shieldDefense, fighterDefense), swordGuard) + disciplineDefense;
        }).DefaultIfEmpty(0).Max();
        return ownDisciplineDefense + suppliedDefense;
    }

    public static IEnumerable<LiveCharacter> AdjacentCharacters(BattleEncounter battle, Enemy enemy,
        Func<LiveCharacter, Position> getCasterPosition) =>
        battle.Characters.Where(character => character.IsAlive &&
            TacticalDistance.IsMeleeAdjacent(getCasterPosition(character), enemy.Position) &&
            !battle.IsProtectedRearTarget(character, enemy.Position));

    public static IReadOnlyList<LiveCharacter> EnemyAttackTargets(BattleEncounter battle, Enemy enemy,
        WeaponDefinition? weapon, Func<LiveCharacter, Position> getCharacterPosition,
        Func<Position, Position, int, bool>? canSee = null)
    {
        var maximumTargets = Math.Clamp(weapon?.MaximumTargets ?? 1, 1, 4);
        var pattern = AttackPattern(weapon);
        if (pattern == WeaponAttackPattern.Cone)
            return EnemyConeTargets(battle, enemy, weapon!, maximumTargets, getCharacterPosition, canSee);

        bool InRange(LiveCharacter character)
        {
            var position = getCharacterPosition(character);
            var distance = TacticalDistance.Between(enemy.Position, position);
            if (!RangedWeaponRules.CanReach(weapon, distance)) return false;
            return weapon?.IsRanged != true || canSee is null ||
                   canSee(enemy.Position, position, weapon.MaximumRange);
        }

        var directCandidates = EnemyTargets(battle, enemy).Where(InRange)
            .OrderBy(character => (double)character.CurrentVitality / Math.Max(1, character.MaximumVitality))
            .ThenBy(character => TacticalDistance.Between(enemy.Position, getCharacterPosition(character)))
            .ToArray();
        if (directCandidates.Length == 0) return [];

        var targets = new List<LiveCharacter> { directCandidates[0] };
        if (maximumTargets == 1) return targets;
        var primaryPosition = getCharacterPosition(targets[0]);
        var directionX = Math.Sign(primaryPosition.X - enemy.Position.X);
        var directionY = Math.Sign(primaryPosition.Y - enemy.Position.Y);
        var nearbyCandidates = battle.Characters.Where(character => character.IsAlive && character != targets[0])
            .OrderBy(character => battle.IsProtectedRearTarget(character, enemy.Position))
            .ThenBy(character => TacticalDistance.Between(getCharacterPosition(targets[0]),
                getCharacterPosition(character)))
            .ToArray();
        foreach (var candidate in nearbyCandidates)
        {
            var candidatePosition = getCharacterPosition(candidate);
            var inPattern = pattern switch
            {
                WeaponAttackPattern.Line => candidatePosition == new Position(
                    primaryPosition.X + directionX, primaryPosition.Y + directionY),
                WeaponAttackPattern.Arc => TacticalDistance.IsMeleeAdjacent(enemy.Position, candidatePosition) &&
                                           TacticalDistance.IsMeleeAdjacent(primaryPosition, candidatePosition),
                WeaponAttackPattern.Compact => TacticalDistance.IsMeleeAdjacent(primaryPosition, candidatePosition),
                _ => targets.Any(target => AreNeighboringTargets(getCharacterPosition(target), candidatePosition))
            };
            if (!inPattern) continue;
            targets.Add(candidate);
            if (targets.Count == maximumTargets) break;
        }
        return targets;
    }

    private static IReadOnlyList<LiveCharacter> EnemyConeTargets(BattleEncounter battle, Enemy enemy,
        WeaponDefinition weapon, int maximumTargets, Func<LiveCharacter, Position> getCharacterPosition,
        Func<Position, Position, int, bool>? canSee)
    {
        var directions = new[]
        {
            new Position(enemy.Position.X - 1, enemy.Position.Y - 1),
            new Position(enemy.Position.X, enemy.Position.Y - 1),
            new Position(enemy.Position.X + 1, enemy.Position.Y - 1),
            new Position(enemy.Position.X + 1, enemy.Position.Y),
            new Position(enemy.Position.X + 1, enemy.Position.Y + 1),
            new Position(enemy.Position.X, enemy.Position.Y + 1),
            new Position(enemy.Position.X - 1, enemy.Position.Y + 1),
            new Position(enemy.Position.X - 1, enemy.Position.Y)
        };
        return directions.Select(direction => battle.Characters.Where(character => character.IsAlive &&
                    IsInWeaponCone(enemy.Position, getCharacterPosition(character), direction) &&
                    (canSee is null || canSee(enemy.Position, getCharacterPosition(character),
                        Math.Max(2, weapon.MaximumRange))))
                .OrderBy(character => TacticalDistance.Between(enemy.Position, getCharacterPosition(character)))
                .ThenBy(character => (double)character.CurrentVitality / Math.Max(1, character.MaximumVitality))
                .Take(maximumTargets).ToArray())
            .Where(targets => targets.Length > 0)
            .OrderByDescending(targets => targets.Length)
            .ThenBy(targets => targets.Sum(target => target.CurrentVitality))
            .FirstOrDefault() ?? [];
    }

    private static bool IsInWeaponCone(Position origin, Position position, Position direction)
    {
        var directionX = Math.Sign(direction.X - origin.X);
        var directionY = Math.Sign(direction.Y - origin.Y);
        var relativeX = position.X - origin.X;
        var relativeY = position.Y - origin.Y;
        var depth = Math.Max(Math.Abs(relativeX), Math.Abs(relativeY));
        if (depth is < 1 or > 2) return false;
        var forward = relativeX * directionX + relativeY * directionY;
        if (forward <= 0) return false;
        var lateral = Math.Abs(relativeX * directionY - relativeY * directionX);
        return lateral <= depth - 1;
    }

    private static bool AreNeighboringTargets(Position first, Position second) =>
        TacticalDistance.IsMeleeAdjacent(first, second) || TacticalDistance.Between(first, second) <= 1;

    public static IEnumerable<LiveCharacter> EnemyTargets(BattleEncounter battle, Enemy enemy)
    {
        var exposed = battle.Characters.Where(character => character.IsAlive &&
            !battle.IsProtectedRearTarget(character, enemy.Position)).ToArray();
        return exposed.Length > 0 ? exposed : battle.Characters.Where(character => character.IsAlive);
    }

    public static IEnumerable<Position> MeleePositions(Position center)
    {
        for (var y = center.Y - 1; y <= center.Y + 1; y++)
        for (var x = center.X - 1; x <= center.X + 1; x++)
            if (x != center.X || y != center.Y)
                yield return new Position(x, y);
    }

    public static IReadOnlyList<BattleItemOptionSnapshot> GetBattleItemOptions(BattleEncounter battle,
        LiveCharacter character)
    {
        if (battle.IsEngaged(character)) return [];
        return Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount)
            .Select(index => (Index: index, Item: character.GetInventoryItem(InventorySlotKind.Backpack, index),
                Quantity: character.GetInventoryItemQuantity(InventorySlotKind.Backpack, index)))
            .Where(entry => entry.Item is MiscItemDefinition item && entry.Quantity > 0 &&
                            battle.CanUseItem(character, item) && IsBattleItemUseful(character, item))
            .GroupBy(entry => entry.Item!.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => new BattleItemOptionSnapshot(group.Min(entry => entry.Index), group.Key,
                group.First().Item!.Name, group.Sum(entry => entry.Quantity)))
            .ToArray();
    }

    public static bool IsBattleItemUseful(LiveCharacter character, MiscItemDefinition item) =>
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

    public static int? ChooseNpcHealingPotionIndex(BattleEncounter battle, LiveCharacter character,
        int allowedWaste)
    {
        if (character.CurrentVitality >= character.MaximumVitality) return null;
        var missingVitality = character.MaximumVitality - character.CurrentVitality;
        return Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount)
            .Select(index => (Index: index,
                Item: character.GetInventoryItem(InventorySlotKind.Backpack, index) as MiscItemDefinition,
                Quantity: character.GetInventoryItemQuantity(InventorySlotKind.Backpack, index)))
            .Where(entry => entry.Item is { Effect: ConsumableEffect.Heal } && entry.Quantity > 0 &&
                            battle.CanUseItem(character, entry.Item) &&
                            Math.Max(0, character.PreviewVitalityRecovery(entry.Item.EffectValue) - missingVitality) <=
                            Math.Max(0, allowedWaste))
            .OrderByDescending(entry => character.PreviewVitalityRecovery(entry.Item!.EffectValue))
            .ThenBy(entry => entry.Index)
            .Select(entry => (int?)entry.Index)
            .FirstOrDefault();
    }

    private static bool IsInitiativeDrink(MiscItemDefinition item) =>
        string.Equals(item.Id, "T023", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(item.Id, "T024", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<BattleActionKind> GetAllowedBattleActions(BattleEncounter battle,
        LiveCharacter character, Enemy focusEnemy, LiveCharacter selectedCharacter,
        Position characterPosition, bool hasUsableCombatSpell,
        IReadOnlyDictionary<LiveCharacter, int> turnUndeadNextAvailableRounds,
        Func<Position, Position, int, bool>? canSee = null)
    {
        var runtime = battle.RuntimeFor(character);
        if (runtime.RequiresTacticSelection)
            return character.CharacterClass.Id == CharacterClassIds.Harcos
                ? [BattleActionKind.FighterPrecise, BattleActionKind.FighterPowerful, BattleActionKind.FighterDefensive]
                : [BattleActionKind.ThiefAmbush, BattleActionKind.ThiefObserve, BattleActionKind.ThiefPoison];
        var reachable = ReachableEnemies(battle, character, characterPosition, canSee).ToArray();
        var canShieldBash = AdjacentEnemies(battle, character, characterPosition).Any() &&
                            character.OperationalWeapons.Any(ShieldRules.IsShield);
        var combatantId = CombatantId.ForCharacter(character.Id);
        var staggered = battle.IsMovementBlocked(combatantId);
        var offensiveActionsBlocked = battle.AreOffensiveActionsBlocked(combatantId);
        var canTurnUndead = BattleActionCoordinator.IsTurnUndeadReady(character, battle.Turns.Cycle,
            turnUndeadNextAvailableRounds) && TurnUndeadTargets(battle, character, characterPosition).Any();
        var selectableTargetCount = reachable
            .Concat(canTurnUndead ? TurnUndeadTargets(battle, character, characterPosition) : [])
            .DistinctBy(enemy => enemy.Id)
            .Count();
        if (battle.Turns.Cycle == 1 && character.Id == battle.InitiatingCharacterId)
        {
            var openingActions = new List<BattleActionKind> { BattleActionKind.Pass };
            if (reachable.Length > 0 && !offensiveActionsBlocked)
                openingActions.Insert(0, BattleActionKind.PhysicalAttack);
            if (canShieldBash && !offensiveActionsBlocked)
                openingActions.Insert(0, BattleActionKind.ShieldBash);
            else if (battle.HasActiveFormation && character == selectedCharacter)
            {
                if (!staggered && !battle.HasStaggeredFormationMember)
                    openingActions.Insert(0, BattleActionKind.MoveFormation);
            }
            else if (!staggered) openingActions.Insert(0, BattleActionKind.Move);
            if (hasUsableCombatSpell && !offensiveActionsBlocked)
                openingActions.Insert(0, BattleActionKind.CastSpell);
            if (canTurnUndead && !offensiveActionsBlocked)
                openingActions.Insert(0, BattleActionKind.TurnUndead);
            if (selectableTargetCount > 1 && !offensiveActionsBlocked)
                openingActions.Add(BattleActionKind.SelectTarget);
            if (character.CanSwapReserveWeapon) openingActions.Add(BattleActionKind.SwapWeapon);
            AddRearPreparationActions(battle, character, selectedCharacter, openingActions);
            return openingActions;
        }
        var actions = new List<BattleActionKind> { BattleActionKind.Pass };
        if (character.CanSwapReserveWeapon) actions.Add(BattleActionKind.SwapWeapon);
        if (reachable.Length > 0 && !offensiveActionsBlocked)
        {
            actions.Add(BattleActionKind.PhysicalAttack);
            if (canShieldBash)
                actions.Add(BattleActionKind.ShieldBash);
        }
        if (canTurnUndead && !offensiveActionsBlocked)
            actions.Add(BattleActionKind.TurnUndead);
        if (selectableTargetCount > 1 && !offensiveActionsBlocked)
            actions.Add(BattleActionKind.SelectTarget);
        if (!staggered && battle.HasProtectiveFormation && battle.IsFrontRow(character) &&
            battle.RearPartnerOf(character) is { IsAlive: true } rearPartner &&
            !battle.IsCharacterStaggered(rearPartner))
            actions.Add(BattleActionKind.SwapToRear);
        if (!staggered && !battle.HasStaggeredFormationMember && battle.HasActiveFormation &&
            character == selectedCharacter)
            actions.Add(BattleActionKind.MoveFormation);
        if (hasUsableCombatSpell && !offensiveActionsBlocked)
            actions.Add(BattleActionKind.CastSpell);
        if (!battle.IsEngaged(character))
        {
            if (!staggered && (!battle.HasActiveFormation || battle.FormationSlotFor(character) is null))
                actions.Add(BattleActionKind.Move);
            if (GetBattleItemOptions(battle, character).Count > 0) actions.Add(BattleActionKind.UseItem);
        }
        if (!staggered && character == selectedCharacter && battle.Turns.Cycle > 1)
            actions.Add(BattleActionKind.Retreat);
        AddRearPreparationActions(battle, character, selectedCharacter, actions);
        return actions;
    }

    private static void AddRearPreparationActions(BattleEncounter battle, LiveCharacter character,
        LiveCharacter selectedCharacter, ICollection<BattleActionKind> actions)
    {
        if (!battle.HasProtectiveFormation || character != selectedCharacter) return;
        if (battle.Formation?.CharacterAt(FormationSlot.RearLeft) is { } rearLeftId &&
            battle.Characters.Any(member => member.Id == rearLeftId && member.IsAlive))
            actions.Add(BattleActionKind.PrepareRearLeft);
        if (battle.Formation?.CharacterAt(FormationSlot.RearRight) is { } rearRightId &&
            battle.Characters.Any(member => member.Id == rearRightId && member.IsAlive))
            actions.Add(BattleActionKind.PrepareRearRight);
    }

    public IReadOnlyList<BattleTacticOptionSnapshot>? GetBattleTacticOptions(BattleEncounter battle,
        LiveCharacter character, Enemy enemy)
    {
        if (!battle.RuntimeFor(character).RequiresTacticSelection) return null;
        var ranged = character.AttackWeapon?.IsRanged == true;
        return character.CharacterClass.Id switch
        {
            CharacterClassIds.Harcos =>
            [
                new(BattleActionKind.FighterPrecise, ranged ? "🎯 Célzott lövés" : "🎯 Pontos",
                    "nagyobb találati esély, kisebb sebzés",
                    _battleSystem.EstimateCharacterHitChance(character, enemy, BattleTactic.FighterPrecise)),
                new(BattleActionKind.FighterPowerful, ranged ? "💥 Páncéltörő lövés" : "💥 Erőteljes",
                    "páncéltörés és nagyobb sebzés",
                    _battleSystem.EstimateCharacterHitChance(character, enemy, BattleTactic.FighterPowerful)),
                new(BattleActionKind.FighterDefensive, ranged ? "🛡️ Biztosító lövés" : "🛡️ Védekező",
                    "nagyobb védelem, kisebb sebzés",
                    _battleSystem.EstimateCharacterHitChance(character, enemy, BattleTactic.FighterDefensive))
            ],
            CharacterClassIds.Tolvaj =>
            [
                new(BattleActionKind.ThiefAmbush, ranged ? "🌑 Rejtett lövés" : "🗡️ Orvtámadás",
                    ranged ? "első sikeres lövés ×2" : "első találat ×2; tőrrel, illetve jártas rövid karddal hátsó sorból is; hátba kerülve ismételhető",
                    _battleSystem.EstimateCharacterHitChance(character, enemy, BattleTactic.ThiefAmbush)),
                new(BattleActionKind.ThiefObserve, ranged ? "👁️ Gyengepont-lövés" : "👁️ Megfigyelés", "+2 találat",
                    _battleSystem.EstimateCharacterHitChance(character, enemy, BattleTactic.ThiefObserve)),
                new(BattleActionKind.ThiefPoison, ranged ? "☠️ Mérgezett lövedék" : "☠️ Mérgezett penge",
                    "+1–4 sebzés találatonként",
                    _battleSystem.EstimateCharacterHitChance(character, enemy, BattleTactic.ThiefPoison))
            ],
            _ => null
        };
    }

    public static double VitalityRatio(LiveCharacter character) =>
        (double)character.CurrentVitality / Math.Max(1, character.MaximumVitality);

    public static IEnumerable<Enemy> OrderedNpcSpellTargets(BattleEncounter battle, Position casterPosition) =>
        battle.Enemies.Where(enemy => enemy.CurrentHitPoints > 0)
            .OrderByDescending(battle.IsEngaged)
            .ThenBy(enemy => battle.Characters.Where(character => battle.EngagedEnemies(character).Contains(enemy))
                .Select(VitalityRatio).DefaultIfEmpty(2d).Min())
            .ThenByDescending(enemy => enemy.Definition.Rank)
            .ThenByDescending(enemy => enemy.Definition.StrengthTier)
            .ThenBy(enemy => enemy.CurrentHitPoints)
            .ThenBy(enemy => TacticalDistance.Between(casterPosition, enemy.Position));

    public Position? ChooseNpcBuffTarget(BattleEncounter battle, LiveCharacter caster,
        Position casterPosition, SpellDefinition spell, IReadOnlyList<SpellEffectDefinition> effects,
        IReadOnlyList<LiveCharacter> allies,
        Func<LiveCharacter, Position> getCasterPosition,
        Func<LiveCharacter, Position, SpellDefinition, Position, Enemy?, bool> isValidExplicitSpellTarget,
        bool allowSelfBuff = true)
    {
        bool NeedsBuff(LiveCharacter character) => effects
            .Where(effect => NpcSpellcastingPolicy.IsBuffEffect(effect.Type))
            .Select(effect => NpcSpellcastingPolicy.ActiveTypeFor(effect.Type))
            .Any(type => type is { } activeType && !character.HasSpellEffect(activeType));

        if (spell.TargetType == SpellTargetType.Self)
            return allowSelfBuff && NeedsBuff(caster) ? casterPosition : null;
        if (spell.TargetType == SpellTargetType.Party)
        {
            if (!allowSelfBuff) return null;
            var missing = allies.Count(NeedsBuff);
            return missing >= Math.Min(2, allies.Count) ? casterPosition : null;
        }
        if (spell.TargetType != SpellTargetType.PartyMember) return null;
        return allies.Where(character => allowSelfBuff || character != caster).Where(NeedsBuff)
            .OrderByDescending(character => battle.IsEngaged(character))
            .ThenByDescending(battle.IsFrontRow)
            .ThenBy(VitalityRatio)
            .Where(character => isValidExplicitSpellTarget(caster, casterPosition, spell,
                getCasterPosition(character), null))
            .Select(character => (Position?)getCasterPosition(character)).FirstOrDefault();
    }
}
