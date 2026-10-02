using KaoszRubin.Combat;
using KaoszRubin.Domain.Characters;
using KaoszRubin.World;

namespace KaoszRubin.Application;

public sealed partial class Game
{
    private void TickStormZones(BattleEncounter? battle)
    {
        if (_maze.StormZones.Count == 0) return;
        var notes = new List<string>();
        foreach (var zone in _maze.StormZones.ToArray())
        {
            if (zone.RemainingRounds <= 0) continue;
            var caster = zone.CharacterCasterId is { } casterId
                ? CharacterRoster.Party.Members.FirstOrDefault(member => member.Id == casterId && member.IsAlive)
                    ?? PartyLeader
                : null;
            foreach (var enemy in _maze.Enemies.ToArray())
            {
                if (enemy.CurrentHitPoints <= 0 || !zone.Contains(enemy.Position)) continue;
                var amount = zone.RollDamage(_random, enemy.EffectiveSpeed, enemy.Definition.MagicResistance);
                if (amount <= 0) continue;
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

            var characters = battle is null
                ? CharacterRoster.Party.Members.Where(character => character == PartyLeader ||
                    _maze.PartyMembers.Any(member => member.Character == character))
                    .Concat(_maze.PartyMembers.Where(member => member.IsTemporaryFollower)
                        .Select(member => member.Character)).Distinct().ToArray()
                : battle.Characters.ToArray();
            foreach (var character in characters)
            {
                if (!character.IsAlive || !zone.Contains(GetCasterPosition(character))) continue;
                var amount = zone.RollDamage(_random, character.EffectiveAbilities.Dexterity, 0);
                if (amount <= 0) continue;
                character.ReceiveDamage(amount);
                notes.Add($"{character.Name} -{amount} HP");
                _renderer.RefreshCharacterSheet(character);
                if (!character.IsAlive)
                {
                    if (battle is null) ResolveExplorationStatusDefeat(character);
                    else ResolveCharacterDefeat(battle, character);
                }
                if (_gameOver) break;
            }
            if (_gameOver) break;
        }
        _maze.AdvanceStormZones();
        if (battle is not null)
        {
            if (notes.Count > 0)
                PresentBattleEntries([new BattleLogEntry($"🌩 Vihar: {string.Join(", ", notes)}.",
                    BattleLogKind.Information)]);
        }
        else if (notes.Count > 0)
        {
            var message = $"🌩 Vihar: {string.Join(", ", notes)}.";
            _renderer.DrawInventoryMessage(message, ConsoleColor.Magenta);
            RecordSessionActivity(SessionActivityKind.Spell, message, ConsoleColor.Magenta);
        }
        RequestCoopSnapshotPublish();
    }
}
