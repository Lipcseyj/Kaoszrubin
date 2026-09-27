using KaoszRubin.Combat;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Application;

public sealed partial class Game
{
    private void ExecuteExplorationRangedAttack(ExplorationRangedAttackCommand command)
    {
        var attacker = CharacterRoster.Party.Members.FirstOrDefault(character => character.Id == command.CharacterId);
        if (attacker is null || !attacker.IsAlive) return;
        var origin = attacker == PartyLeader
            ? _player.Position
            : _maze.PartyMembers.FirstOrDefault(member => member.Character == attacker)?.Position;
        if (origin is null) return;

        var equippedRangedWeapons = Enumerable.Range(0, 2)
            .Where(index => attacker.IsInventoryItemOperational(InventorySlotKind.Weapon, index))
            .Select(index => (Weapon: attacker.WeaponSlots[index], Slot: index))
            .Where(entry => entry.Weapon is { IsRanged: true })
            .ToArray();
        if (equippedRangedWeapons.Length == 0)
        {
            PresentBattleEntries([new BattleLogEntry(
                $"{attacker.Name} nem tud lőni: nincs használható lövőfegyver felszerelve.",
                BattleLogKind.Information)]);
            return;
        }
        var rangedWeapon = equippedRangedWeapons.FirstOrDefault(entry =>
            RangedWeaponRules.HasAmmunition(attacker, entry.Weapon));
        if (rangedWeapon.Weapon is null)
        {
            PresentBattleEntries([new BattleLogEntry(
                $"{attacker.Name} nem tud lőni: elfogyott a lőszere.", BattleLogKind.Information)]);
            return;
        }

        var shotStartedUtc = DateTime.UtcNow;
        var nextShotUtc = _nextExplorationShotUtc.GetValueOrDefault(attacker.Id);
        if (shotStartedUtc < nextShotUtc)
        {
            if (_nextExplorationShotNoticeUtc.GetValueOrDefault(attacker.Id) <= shotStartedUtc)
            {
                var remainingSeconds = Math.Max(0.1, (nextShotUtc - shotStartedUtc).TotalSeconds);
                PresentBattleEntries([new BattleLogEntry(
                    $"🏹 {attacker.Name}: {rangedWeapon.Weapon.Name} — a következő lövésig még " +
                    $"{remainingSeconds:0.0} mp.", BattleLogKind.Information)]);
                _nextExplorationShotNoticeUtc[attacker.Id] = shotStartedUtc + TimeSpan.FromMilliseconds(500);
            }
            return;
        }
        _nextExplorationShotUtc[attacker.Id] = shotStartedUtc +
            TimeSpan.FromMilliseconds(rangedWeapon.Weapon.ExplorationShotDelayMilliseconds);
        _nextExplorationShotNoticeUtc.Remove(attacker.Id);

        var tracedPath = ExplorationRangedAttackRules.Trace(_maze, origin.Value, command.Direction,
            rangedWeapon.Weapon.MaximumRange);
        var flightPath = new List<Position>(tracedPath.Count);
        Enemy? enemyTarget = null;
        LiveCharacter? friendlyTarget = null;
        foreach (var position in tracedPath)
        {
            flightPath.Add(position);
            enemyTarget = _maze.GetEnemyAt(position);
            if (enemyTarget is not null) break;
            if (position == _player.Position && attacker != PartyLeader)
            {
                friendlyTarget = PartyLeader;
                break;
            }
            var member = _maze.GetPartyMemberAt(position);
            if (member is not null && member.Character != attacker)
            {
                friendlyTarget = member.Character;
                break;
            }
        }

        _renderer.AnimateExplorationProjectile(_maze, _fogOfWar, _player.Position, flightPath,
            command.Direction);
        var distance = flightPath.Count;
        if (enemyTarget is not null)
        {
            var entry = _battleSystem.ResolveCharacterAttack(attacker,
                _battleSystem.PrepareCharacter(attacker).Runtime, enemyTarget, finishAction: false,
                attackWeapon: rangedWeapon.Weapon, allowTriggeredExtraAttacks: false, allowAmbush: false,
                attackWeaponSlotIndex: rangedWeapon.Slot,
                rangedHitModifier: RangedWeaponRules.CloseRangeModifier(attacker, rangedWeapon.Weapon, distance));
            PresentBattleEntries([entry]);
            if (enemyTarget.CurrentHitPoints <= 0) ResolveExplorationRangedEnemyDefeat(attacker, enemyTarget);
        }
        else if (friendlyTarget is not null)
        {
            var entry = _battleSystem.ResolveCharacterFriendlyFire(attacker,
                _battleSystem.PrepareCharacter(attacker).Runtime, friendlyTarget, rangedWeapon.Weapon,
                rangedWeapon.Slot,
                RangedWeaponRules.CloseRangeModifier(attacker, rangedWeapon.Weapon, distance));
            PresentBattleEntries([entry]);
            if (!friendlyTarget.IsAlive)
                ResolveExplorationStatusDefeat(friendlyTarget,
                    $"☠ {friendlyTarget.Name} elesett {attacker.Name} baráti lövésétől.");
        }
        else
        {
            RangedWeaponRules.TryConsumeAmmunition(attacker, rangedWeapon.Weapon);
            var obstruction = flightPath.Count < rangedWeapon.Weapon.MaximumRange
                ? " A lövedék akadályba csapódott."
                : string.Empty;
            PresentBattleEntries([new BattleLogEntry(
                $"{attacker.Name} {rangedWeapon.Weapon.Name} fegyverrel lő, de nem talál célpontot.{obstruction}",
                BattleLogKind.Information)]);
        }

        _renderer.CharacterSheet.RefreshInventoryRows();
        _renderer.CharacterSheet.RefreshBattleStatusRows();
        RequestCoopSnapshotPublish();
    }

    private void ResolveExplorationRangedEnemyDefeat(LiveCharacter attacker, Enemy enemy)
    {
        if (!_maze.Enemies.Contains(enemy)) return;
        AwardBossKey(enemy);
        RegisterNpcQuestKill(enemy);
        attacker.RecordMonsterKill(enemy.Definition.Id);
        _maze.ReplaceEnemyWithCorpse(enemy);
        _nextEnemyMoves.Remove(enemy);
        var awards = DistributeExperience(attacker, enemy.Definition.ExperienceReward, isQuest: false);
        var message = $"☠ {enemy.Name} elesett; {FormatExperienceAwards(awards)}.";
        PresentBattleEntries([new BattleLogEntry(message, BattleLogKind.Information)]);
        _renderer.DrawMapCellAfterBattle(_maze, _fogOfWar, enemy.Position, _player.Position);
        foreach (var award in awards.Where(award => award.Result.LeveledUp && award.Character.IsAlive))
            ResolvePerkOffers(award.Character, award.Result);
    }
}
