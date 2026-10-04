using KaoszRubin.Combat;
using KaoszRubin.Domain.Characters;
using KaoszRubin.World;

namespace KaoszRubin.Application;

public sealed partial class Game
{
    private static readonly TimeSpan ExplorationStormPulseInterval = TimeSpan.FromSeconds(5);
    private readonly StormZoneExposureTracker _stormExposure = new(ExplorationStormPulseInterval);

    private sealed record StormOccupant(Position Position, Enemy? Enemy = null,
        LiveCharacter? Character = null);

    private Dictionary<StormExposureActor, StormOccupant> CurrentStormOccupants(BattleEncounter? battle)
    {
        var occupants = new Dictionary<StormExposureActor, StormOccupant>();
        foreach (var enemy in _maze.Enemies.Where(enemy => enemy.CurrentHitPoints > 0))
            occupants[new StormExposureActor(true, enemy.Id.Value)] = new StormOccupant(enemy.Position, enemy);

        var characters = battle is null
            ? CharacterRoster.Party.Members.Where(character => character == PartyLeader ||
                _maze.PartyMembers.Any(member => member.Character == character))
                .Concat(_maze.PartyMembers.Where(member => member.IsTemporaryFollower)
                    .Select(member => member.Character)).Distinct()
            : battle.Characters;
        foreach (var character in characters.Where(character => character.IsAlive))
            occupants[new StormExposureActor(false, character.Id.Value)] =
                new StormOccupant(GetCasterPosition(character), Character: character);
        return occupants;
    }

    /// <summary>Belépéskor azonnal sebez, a varázsláskor már bent állókat csak nyilvántartásba veszi.</summary>
    private void ProcessStormZoneEntries(BattleEncounter? battle, DateTime now)
    {
        if (_maze.StormZones.Count == 0) return;
        var occupants = CurrentStormOccupants(battle);
        var positions = occupants.ToDictionary(entry => entry.Key, entry => entry.Value.Position);
        var entries = _stormExposure.Observe(_maze.Id, _maze.StormZones, positions, now,
            battle is not null);
        if (entries.Count == 0) return;

        var notes = new List<string>();
        foreach (var entry in entries)
        {
            var zone = _maze.StormZones.FirstOrDefault(candidate => candidate.Id == entry.ZoneId);
            if (zone is null || !occupants.TryGetValue(entry.Actor, out var occupant) ||
                !zone.Contains(occupant.Position)) continue;
            _stormExposure.RecordExposure(zone.Id, entry.Actor, now);
            if (battle is not null)
                _stormExposure.RecordBattleEntry(zone.Id, entry.Actor, battle.Turns.Cycle);
            ApplyStormDamage(zone, occupant, battle, notes);
            if (_gameOver) break;
        }
        PresentStormDamage(battle, notes);
    }

    private void ProcessExplorationStormPulses(DateTime now)
    {
        if (_maze.StormZones.Count == 0) return;
        foreach (var zoneId in _stormExposure.DueZones(now))
        {
            if (_maze.StormZones.All(zone => zone.Id != zoneId)) continue;
            TickStormZones(null, zoneId, now);
            _stormExposure.MarkPulsed(zoneId, now);
            if (_gameOver) break;
        }
    }

    private void TickStormZones(BattleEncounter? battle, Guid? onlyZoneId = null, DateTime? nowUtc = null)
    {
        if (_maze.StormZones.Count == 0) return;
        var now = nowUtc ?? DateTime.UtcNow;
        if (battle is not null) ProcessStormZoneEntries(battle, now);
        var occupants = CurrentStormOccupants(battle);
        var notes = new List<string>();
        foreach (var zone in _maze.StormZones.Where(zone => onlyZoneId is null || zone.Id == onlyZoneId).ToArray())
        {
            if (zone.RemainingRounds <= 0) continue;
            foreach (var entry in occupants)
            {
                if (!zone.Contains(entry.Value.Position)) continue;
                if (battle is null && !_stormExposure.CanPulse(zone.Id, entry.Key, now) ||
                    battle is not null && !_stormExposure.CanBattlePulse(zone.Id, entry.Key,
                        battle.Turns.Cycle)) continue;
                _stormExposure.RecordExposure(zone.Id, entry.Key, now);
                ApplyStormDamage(zone, entry.Value, battle, notes);
                if (_gameOver) break;
            }
            if (_gameOver) break;
        }
        _maze.AdvanceStormZones(onlyZoneId);
        PresentStormDamage(battle, notes);
        RequestCoopSnapshotPublish();
    }

    private void ApplyStormDamage(ActiveStormZone zone, StormOccupant occupant,
        BattleEncounter? battle, List<string> notes)
    {
        var caster = zone.CharacterCasterId is { } casterId
            ? CharacterRoster.Party.Members.FirstOrDefault(member => member.Id == casterId && member.IsAlive)
                ?? PartyLeader
            : null;
        if (occupant.Enemy is { CurrentHitPoints: > 0 } enemy)
        {
            var typedResistance = zone.DamageType is { } type
                ? enemy.Definition.Resistances?.Against(type) ?? 0 : 0;
            var amount = zone.RollDamage(_random, enemy.EffectiveSpeed, enemy.Definition.MagicResistance,
                typedResistance);
            if (amount <= 0) return;
            notes.Add($"{enemy.Name} -{amount} HP");
            if (battle is not null && battle.Enemies.Contains(enemy))
            {
                enemy.ReceiveSpellDamage(amount);
                if (enemy.CurrentHitPoints <= 0) ResolveEnemyDefeat(battle, enemy, caster);
            }
            else if (caster is not null)
                ApplyExplorationSpellDamage(caster, enemy, amount, notes);
            else
            {
                enemy.ReceiveSpellDamage(amount);
                if (enemy.CurrentHitPoints <= 0)
                {
                    _maze.ReplaceEnemyWithCorpse(enemy);
                    _nextEnemyMoves.Remove(enemy);
                    _renderer.DrawMapCellAfterBattle(_maze, _fogOfWar, enemy.Position, _player.Position);
                    notes.Add($"{enemy.Name} elpusztult");
                }
            }
        }
        else if (occupant.Character is { IsAlive: true } character)
        {
            var amount = zone.RollDamage(_random, character.EffectiveAbilities.Dexterity, 0);
            if (amount <= 0) return;
            character.ReceiveDamage(amount);
            notes.Add($"{character.Name} -{amount} HP");
            _renderer.RefreshCharacterSheet(character);
            if (!character.IsAlive)
            {
                if (battle is null) ResolveExplorationStatusDefeat(character);
                else ResolveCharacterDefeat(battle, character);
            }
        }
    }

    private void PresentStormDamage(BattleEncounter? battle, IReadOnlyList<string> notes)
    {
        if (notes.Count == 0) return;
        var message = $"🌩 Vihar: {string.Join(", ", notes)}.";
        if (battle is not null)
            PresentBattleEntries([new BattleLogEntry(message, BattleLogKind.Information)]);
        else
        {
            _renderer.DrawInventoryMessage(message, ConsoleColor.Magenta);
            RecordSessionActivity(SessionActivityKind.Spell, message, ConsoleColor.Magenta);
        }
        RequestCoopSnapshotPublish();
    }
}
