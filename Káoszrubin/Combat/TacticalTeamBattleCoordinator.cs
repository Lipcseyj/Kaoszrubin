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
public enum WeaponAttackPattern { Single, Arc, Line, Compact }

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

    public static bool ShouldNpcSwapToReserveWeapon(LiveCharacter character)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (character.AttackWeapon is not null || !character.CanSwapReserveWeapon) return false;
        return character.GetInventoryItem(InventorySlotKind.Weapon, 2) is WeaponDefinition reserve &&
               reserve.WeaponTypeId != "WT003" &&
               character.IsInventoryItemOperational(InventorySlotKind.Weapon, 2);
    }

    public static Enemy ClosestLivingTeamEnemy(TeamBattleEncounter battle, Position origin) =>
        battle.Enemies.Where(enemy => enemy.CurrentHitPoints > 0)
            .OrderBy(enemy => TacticalDistance.Between(origin, enemy.Position))
            .ThenBy(enemy => enemy.CurrentHitPoints).First();

    public static int PreferredSpellcasterRetreatDistance(IEnumerable<Enemy> livingEnemies) =>
        livingEnemies.Any(enemy => enemy.CurrentHitPoints > 0 &&
                                   enemy.Definition.HasTrait(EnemyTraits.Flying)) ? 8 : 6;

    public static bool CanTeamRetreat(int friendlySpeed, int hostileSpeed,
        bool everyEnemyHasTwoCellGap, bool anyEnemyVisible) =>
        friendlySpeed > hostileSpeed ||
        friendlySpeed == hostileSpeed && everyEnemyHasTwoCellGap ||
        friendlySpeed < hostileSpeed && !anyEnemyVisible;

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
        WeaponFamilies.ForWeapon(weapon) switch
        {
            WeaponFamilies.Polearm when weapon?.MaximumTargets > 1 => WeaponAttackPattern.Line,
            WeaponFamilies.Axe when weapon?.MaximumTargets > 1 => WeaponAttackPattern.Arc,
            WeaponFamilies.Blunt when weapon is { IsTwoHanded: true, MaximumTargets: > 1 } =>
                WeaponAttackPattern.Compact,
            WeaponFamilies.Sword when weapon?.MaximumTargets > 1 => WeaponAttackPattern.Arc,
            _ => WeaponAttackPattern.Single
        };

    public static LiveCharacter? PolearmMasterControlling(TeamBattleEncounter battle, Position position) =>
        battle.Characters.FirstOrDefault(character => character.IsAlive &&
            character.OperationalWeapons.Any(weapon => WeaponFamilies.ForWeapon(weapon) == WeaponFamilies.Polearm) &&
            character.WeaponProficiencyRankFor(WeaponFamilies.Polearm) == WeaponProficiencyRank.Master &&
            TacticalDistance.IsMeleeAdjacent(battle.PositionOf(character), position));

    public static TacticalAttackAdvantage AttackAdvantage(TeamBattleEncounter battle,
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

    public static int SweepDamagePercent(LiveCharacter character, TeamCharacterBattleRuntime runtime,
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

    public static int AlliedGuardDefense(TeamBattleEncounter battle, LiveCharacter protectedCharacter,
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

    public static int? ChooseNpcHealingPotionIndex(TeamBattleEncounter battle, LiveCharacter character,
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
        var staggered = battle.IsCharacterStaggered(character);
        var turnUndeadTargets = AdjacentTeamEnemies(battle, character, characterPosition)
            .Concat(battle.RearFormationEngagedEnemies(character))
            .Where(enemy => BattleActionCoordinator.CanTurnUndead(character, enemy))
            .DistinctBy(enemy => enemy.Id).ToArray();
        if (battle.Turns.Cycle == 1 && character.Id == battle.InitiatingCharacterId)
        {
            var openingActions = new List<BattleActionKind> { BattleActionKind.Pass };
            if (reachable.Length > 0) openingActions.Insert(0, BattleActionKind.PhysicalAttack);
            else if (battle.HasActiveFormation && character == selectedCharacter)
            {
                if (!staggered && !battle.HasStaggeredFormationMember)
                    openingActions.Insert(0, BattleActionKind.MoveFormation);
            }
            else if (!staggered) openingActions.Insert(0, BattleActionKind.Move);
            if (hasUsableCombatSpell)
                openingActions.Insert(0, BattleActionKind.CastSpell);
            if (turnUndeadTargets.Length > 0 && !turnUndeadUsedThisBattle.Contains(character))
                openingActions.Insert(0, BattleActionKind.TurnUndead);
            if (character.CanSwapReserveWeapon) openingActions.Add(BattleActionKind.SwapWeapon);
            AddRearPreparationActions(battle, character, selectedCharacter, openingActions);
            return openingActions;
        }
        var actions = new List<BattleActionKind> { BattleActionKind.Pass };
        if (character.CanSwapReserveWeapon) actions.Add(BattleActionKind.SwapWeapon);
        if (reachable.Length > 0)
        {
            actions.Add(BattleActionKind.PhysicalAttack);
            if (reachable.Length > 1) actions.Add(BattleActionKind.SelectTarget);
        }
        if (turnUndeadTargets.Length > 0 && !turnUndeadUsedThisBattle.Contains(character))
            actions.Add(BattleActionKind.TurnUndead);
        if (!staggered && battle.HasProtectiveFormation && battle.IsFrontRow(character) &&
            battle.RearPartnerOf(character) is { IsAlive: true } rearPartner &&
            !battle.IsCharacterStaggered(rearPartner))
            actions.Add(BattleActionKind.SwapToRear);
        if (!staggered && !battle.HasStaggeredFormationMember && battle.HasActiveFormation &&
            character == selectedCharacter)
            actions.Add(BattleActionKind.MoveFormation);
        if (hasUsableCombatSpell)
            actions.Add(BattleActionKind.CastSpell);
        if (!battle.IsEngaged(character))
        {
            if (!staggered && (!battle.HasActiveFormation || battle.FormationSlotFor(character) is null))
                actions.Add(BattleActionKind.Move);
            if (GetBattleItemOptions(battle, character).Count > 0) actions.Add(BattleActionKind.UseItem);
        }
        if (character == selectedCharacter && battle.Turns.Cycle > 1)
            actions.Add(BattleActionKind.Retreat);
        AddRearPreparationActions(battle, character, selectedCharacter, actions);
        return actions;
    }

    private static void AddRearPreparationActions(TeamBattleEncounter battle, LiveCharacter character,
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
                new(BattleActionKind.ThiefAmbush, "🗡️ Orvtámadás", "első találat ×2; tőrrel hátsó sorból is, hátba kerülve ismételhető",
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
