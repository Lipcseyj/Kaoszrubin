using KaoszRubin.Application;
using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Magic;
using KaoszRubin.UI;

namespace KaoszRubin.Combat;

public sealed class SingleBattleCoordinator
{
    private readonly GameDataCatalog _gameData;
    private readonly BattleSystem _battleSystem;
    private readonly SpellExecutionService _spellExecutionService;
    private readonly Random _random;

    public SingleBattleCoordinator(
        GameDataCatalog gameData,
        BattleSystem battleSystem,
        SpellExecutionService spellExecutionService,
        Random random)
    {
        _gameData = gameData;
        _battleSystem = battleSystem;
        _spellExecutionService = spellExecutionService;
        _random = random;
    }

    public static bool CanTurnUndead(LiveCharacter character, Enemy enemy) =>
        character.CharacterClass.Id is CharacterClassIds.Pap or CharacterClassIds.Lovag &&
        enemy.Definition.HasTrait(EnemyTraits.Undead);

    public BattlePlayerAction ResolveTurnUndead(LiveCharacter character, Enemy enemy, HashSet<LiveCharacter> turnUndeadUsedThisBattle)
    {
        turnUndeadUsedThisBattle.Add(character);
        var priest = character.CharacterClass.Id == CharacterClassIds.Pap;
        var ability = priest ? character.EffectiveAbilities.Intelligence : character.EffectiveAbilities.Strength;
        var levelBonus = priest ? character.Level / 2 : character.Level / 3;
        var roll = _random.Next(1, 21);
        var total = roll + ability + levelBonus;
        var difficulty = 10 + enemy.Definition.StrengthTier * 2;
        var abilityName = priest ? "Halottűzés" : "Szent elűzés";
        if (total < difficulty)
            return new BattlePlayerAction($"{character.Name} megkísérli: {abilityName}, de az élőholt ellenáll " +
                $"({roll} + {ability} + {levelBonus} = {total}, cél {difficulty}).", BattleLogKind.Information);

        if (priest && total >= difficulty + 10 && enemy.Definition.StrengthTier <= 2 && enemy.GroupRole != EnemyGroupRole.Leader)
            return new BattlePlayerAction($"{character.Name} {abilityName} képessége szent fénnyel megsemmisíti {enemy.Name} ellenfelet " +
                $"({total}, cél {difficulty}+10).", BattleLogKind.PlayerAttack, enemy.CurrentHitPoints);

        if (priest)
        {
            enemy.ApplySpellEffect(new ActiveSpellEffect("TURN-UNDEAD", ActiveSpellEffectType.SkipNext, 0, 2));
            return new BattlePlayerAction($"{character.Name} sikeresen használja: {abilityName}. {enemy.Name} két akciót kihagy " +
                $"({total}, cél {difficulty}).", BattleLogKind.PlayerAttack);
        }

        var damage = _random.Next(1, 7) + character.Level / 2;
        enemy.ApplySpellEffect(new ActiveSpellEffect("HOLY-TURNING", ActiveSpellEffectType.SkipNext, 0, 1));
        character.ApplySpellEffect(new ActiveSpellEffect("HOLY-TURNING", ActiveSpellEffectType.DefenseBonus, 2, 2, Beneficial: true));
        return new BattlePlayerAction($"{character.Name} sikeresen használja: {abilityName}. {enemy.Name} -{damage} HP, " +
            "kihagyja következő akcióját; a Lovag +2 védelmet kap 2 akcióig " +
            $"({total}, cél {difficulty}).", BattleLogKind.PlayerAttack, damage);
    }

    public static BattleTactic ToBattleTactic(BattleActionKind action) => action switch
    {
        BattleActionKind.FighterPrecise => BattleTactic.FighterPrecise,
        BattleActionKind.FighterPowerful => BattleTactic.FighterPowerful,
        BattleActionKind.FighterDefensive => BattleTactic.FighterDefensive,
        BattleActionKind.ThiefAmbush => BattleTactic.ThiefAmbush,
        BattleActionKind.ThiefObserve => BattleTactic.ThiefObserve,
        BattleActionKind.ThiefPoison => BattleTactic.ThiefPoison,
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    public static string BattleTacticName(BattleTactic tactic, LiveCharacter character) => tactic switch
    {
        BattleTactic.FighterPrecise => $"Pontos állás (+2 találat, ×{(character.HasClassFeatureUpgrade(ClassFeatureUpgrades.FighterPrecise) ? "0,85" : "0,75")} sebzés)",
        BattleTactic.FighterPowerful => $"Erőteljes állás (-1 találat, ×1,25 sebzés, {(character.HasClassFeatureUpgrade(ClassFeatureUpgrades.FighterPowerful) ? 75 : 50)}% páncéltörés)",
        BattleTactic.FighterDefensive => $"Védekező állás (×0,75 sebzés, +{(character.HasClassFeatureUpgrade(ClassFeatureUpgrades.FighterDefensive) ? 4 : 3)} védelem)",
        BattleTactic.ThiefAmbush => "Orvtámadás (az első sikeres támadás dupla sebzés)",
        BattleTactic.ThiefObserve => "Megfigyelés (+2 találat)",
        BattleTactic.ThiefPoison => "Mérgezett penge (+1-4 sebzés találatonként)",
        _ => tactic.ToString()
    };

    public IReadOnlyList<BattleTacticOptionSnapshot>? GetBattleTacticOptions(BattleState state)
    {
        if (!state.IsAwaitingTacticSelection) return null;
        return state.Player.CharacterClass.Id switch
        {
            CharacterClassIds.Harcos =>
            [
                new(BattleActionKind.FighterPrecise, "🎯 Pontos",
                    $"sebzés ×{(state.Player.HasClassFeatureUpgrade(ClassFeatureUpgrades.FighterPrecise) ? "0,85" : "0,75")}",
                    _battleSystem.EstimatePlayerHitChance(state.Player, state.Enemy, BattleTactic.FighterPrecise)),
                new(BattleActionKind.FighterPowerful, "💥 Erőteljes",
                    $"sebzés ×1,25, {(state.Player.HasClassFeatureUpgrade(ClassFeatureUpgrades.FighterPowerful) ? "negyed" : "fél")} páncél",
                    _battleSystem.EstimatePlayerHitChance(state.Player, state.Enemy, BattleTactic.FighterPowerful)),
                new(BattleActionKind.FighterDefensive, "🛡️ Védekező",
                    $"sebzés ×0,75, védelem +{(state.Player.HasClassFeatureUpgrade(ClassFeatureUpgrades.FighterDefensive) ? 4 : 3)}",
                    _battleSystem.EstimatePlayerHitChance(state.Player, state.Enemy, BattleTactic.FighterDefensive))
            ],
            CharacterClassIds.Tolvaj =>
            [
                new(BattleActionKind.ThiefAmbush, "🗡️ Orvtámadás", "első találat ×2 sebzés",
                    _battleSystem.EstimatePlayerHitChance(state.Player, state.Enemy, BattleTactic.ThiefAmbush)),
                new(BattleActionKind.ThiefObserve, "👁️ Megfigyelés", "+2 találat",
                    _battleSystem.EstimatePlayerHitChance(state.Player, state.Enemy, BattleTactic.ThiefObserve)),
                new(BattleActionKind.ThiefPoison, "☠️ Mérgezett penge", "+1–4 sebzés találatonként",
                    _battleSystem.EstimatePlayerHitChance(state.Player, state.Enemy, BattleTactic.ThiefPoison))
            ],
            _ => null
        };
    }

    public static BattleActionKind TacticActionFor(string characterClassId, int option) =>
        characterClassId == CharacterClassIds.Harcos
            ? option switch
            {
                1 => BattleActionKind.FighterPrecise,
                2 => BattleActionKind.FighterPowerful,
                _ => BattleActionKind.FighterDefensive
            }
            : option switch
            {
                1 => BattleActionKind.ThiefAmbush,
                2 => BattleActionKind.ThiefObserve,
                _ => BattleActionKind.ThiefPoison
            };

    public IReadOnlyList<BattleActionKind> GetAllowedBattleActions(
        BattleState? activeBattleState,
        LiveCharacter character,
        Position characterPosition,
        Enemy enemy,
        bool hasUsableCombatSpell,
        HashSet<LiveCharacter> turnUndeadUsedThisBattle)
    {
        if (activeBattleState is { IsCompleted: false, IsPlayerTurn: false } enemyTurn &&
            enemyTurn.PlayerCharacterId == character.Id)
            return [BattleActionKind.AdvanceEnemyTurn];
        if (activeBattleState is { IsAwaitingTacticSelection: true } state && state.PlayerCharacterId == character.Id)
            return character.CharacterClass.Id == CharacterClassIds.Harcos
                ? [BattleActionKind.FighterPrecise, BattleActionKind.FighterPowerful, BattleActionKind.FighterDefensive]
                : [BattleActionKind.ThiefAmbush, BattleActionKind.ThiefObserve, BattleActionKind.ThiefPoison];
        var actions = new List<BattleActionKind> { BattleActionKind.PhysicalAttack };
        if (hasUsableCombatSpell) actions.Add(BattleActionKind.CastSpell);
        if (CanTurnUndead(character, enemy) && !turnUndeadUsedThisBattle.Contains(character))
            actions.Add(BattleActionKind.TurnUndead);
        return actions;
    }

    public LiveCharacter? TryRollKnightProtector(
        LiveCharacter protectedCharacter,
        Position protectedPosition,
        IEnumerable<(LiveCharacter Character, Position Position)> livingPartyWithPositions)
    {
        var knight = livingPartyWithPositions
            .Where(entry => entry.Character != protectedCharacter && entry.Character.IsAlive &&
                            entry.Character.CharacterClass.Id == CharacterClassIds.Lovag &&
                            Chebyshev(entry.Position, protectedPosition) <= 2)
            .OrderBy(entry => Chebyshev(entry.Position, protectedPosition))
            .Select(entry => entry.Character).FirstOrDefault();
        var chance = knight?.HasClassFeatureUpgrade(ClassFeatureUpgrades.KnightBodyguard) == true ? 90 : 75;
        return knight is not null && _random.Next(100) < chance ? knight : null;
    }

    public void TryAssignKnightProtection(
        BattleState state,
        LiveCharacter protectedCharacter,
        Position protectedPosition,
        IEnumerable<(LiveCharacter Character, Position Position)> livingPartyWithPositions,
        Action<string, ConsoleColor> onMessage)
    {
        var knight = TryRollKnightProtector(protectedCharacter, protectedPosition, livingPartyWithPositions);
        if (knight is null) return;
        state.SetKnightProtection(knight);
        onMessage($"🛡️ {knight.Name} készen áll közbelépni: a társ első találatát teljesen kivédi, " +
                  "de a sebzés harmadát ő kapja.",
            ConsoleColor.Cyan);
    }

    public bool HasUsableCombatSpell(
        LiveCharacter character,
        Position characterPosition,
        Enemy enemy,
        bool timeStopUsedThisBattle,
        IEnumerable<MagicItemDefinition> equippedCastingItems,
        Func<LiveCharacter, Position, SpellDefinition, Enemy, bool> hasValidSpellTarget)
    {
        return (character.CanCastSpells && character.MemorizedSpells.Any(spell =>
                spell.CanUseInCombat && SpellcastingRules.EffectiveManaCost(character, spell) <= character.CurrentMana &&
                (!timeStopUsedThisBattle || _gameData.GetSpellEffects(spell.Id).All(effect => effect.Type != SpellEffectType.ExtraActions)) &&
                hasValidSpellTarget(character, characterPosition, spell, enemy))) ||
            equippedCastingItems.Any(item =>
                _gameData.GetSpell(item.SpellId!) is { } spell && spell.CanUseInCombat &&
                (!timeStopUsedThisBattle || _gameData.GetSpellEffects(spell.Id).All(effect => effect.Type != SpellEffectType.ExtraActions)) &&
                hasValidSpellTarget(character, characterPosition, spell, enemy));
    }

    public IReadOnlyList<BattleSpellOption> GetSpellOptions(
        LiveCharacter character,
        Position characterPosition,
        Enemy? enemy,
        bool inCombat,
        Func<LiveCharacter, Position, SpellDefinition, Enemy?, bool> hasValidSpellTarget,
        Func<Position, SpellDefinition, Enemy?, IEnumerable<Position>> getValidSpellTargets)
    {
        return character.MemorizedSpells
            .Where(spell => inCombat ? spell.CanUseInCombat : spell.CanUseDuringExploration)
            .Select(spell => (Spell: spell, Item: (MagicItemDefinition?)null, Slot: (int?)null))
            .Concat(character.MagicItems.Select((item, index) => (Item: item, Index: index))
                .Where(entry => entry.Item?.Kind is MagicItemKind.Scroll or MagicItemKind.Wand &&
                                entry.Item.SpellId is not null && character.MagicItemCharges[entry.Index] > 0)
                .Select(entry => (Spell: _gameData.GetSpell(entry.Item!.SpellId!), Item: (MagicItemDefinition?)entry.Item,
                    Slot: (int?)entry.Index))
                .Where(entry => (inCombat ? entry.Spell.CanUseInCombat : entry.Spell.CanUseDuringExploration) &&
                                SpellcastingRules.CanUseCastingItem(character, entry.Item!, entry.Spell)))
            .OrderBy(entry => entry.Spell.Level).ThenBy(entry => entry.Spell.Name)
            .ThenBy(entry => entry.Item is not null)
            .Select(entry =>
            {
                var targets = entry.Spell.TargetType is SpellTargetType.Self or SpellTargetType.Party
                    ? hasValidSpellTarget(character, characterPosition, entry.Spell, enemy)
                        ? new[] { characterPosition }
                        : []
                    : getValidSpellTargets(characterPosition, entry.Spell, enemy).Distinct().ToArray();
                var quickIndex = character.QuickSpells.ToList().FindIndex(candidate =>
                    string.Equals(candidate?.Id, entry.Spell.Id, StringComparison.OrdinalIgnoreCase));
                return new BattleSpellOption(entry.Spell.Id, entry.Spell.Name, entry.Spell.Level,
                    entry.Item is null ? SpellcastingRules.EffectiveManaCost(character, entry.Spell) : 0,
                    entry.Spell.TargetType, entry.Spell.Range, entry.Spell.AreaRadius, entry.Slot,
                    entry.Item?.Kind, entry.Slot is { } slot ? character.MagicItemCharges[slot] : 0,
                    entry.Item is null && quickIndex >= 0 ? quickIndex : null, targets);
            }).ToArray();
    }

    private static int Chebyshev(Position first, Position second) =>
        Math.Max(Math.Abs(first.X - second.X), Math.Abs(first.Y - second.Y));
}
