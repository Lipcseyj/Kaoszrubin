using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Quests;
using KaoszRubin.World;

namespace KaoszRubin.Infrastructure.Quests;

public sealed record QuestNpcIdentity(QuestNpcInstanceId InstanceId, CharacterId CharacterId, QuestNpcId NpcId);

/// <summary>
/// A tartós karakterazonossághoz rendeli a quest NPC példányazonosítóját.
/// Ugyanaz a követő több világpillanatképben is ugyanazt az azonosítót kapja.
/// Az eltávozott NPC-k bejegyzéseit is megőrzi, ezért azonosítójuk nem használható fel újra.
/// </summary>
public sealed class QuestNpcInstanceRegistry
{
    private Dictionary<CharacterId, QuestNpcIdentity> _identities = [];
    private Dictionary<QuestNpcInstanceId, CharacterId> _owners = [];
    private long _nextId = 1;

    public QuestNpcInstanceId GetOrCreate(WorldNpc npc) =>
        GetOrCreate(npc.Character.Id, LegacyNpcIdMap.ToQuestNpcId(npc.DefinitionId));

    internal QuestNpcInstanceId GetOrCreate(CharacterId characterId, QuestNpcId npcId)
    {
        if (_identities.TryGetValue(characterId, out var identity))
        {
            if (identity.NpcId != npcId) throw new InvalidDataException("Az NPC karakterazonossága más NPC-típushoz tartozik.");
            return identity.InstanceId;
        }
        if (_nextId > int.MaxValue) throw new InvalidOperationException("Elfogytak a quest NPC példányazonosítók.");
        var id = new QuestNpcInstanceId((int)_nextId);
        Register(new(id, characterId, npcId));
        return id;
    }

    public void Restore(WorldNpc npc, QuestNpcInstanceId instanceId) =>
        Register(new(instanceId, npc.Character.Id, LegacyNpcIdMap.ToQuestNpcId(npc.DefinitionId)));

    public IReadOnlyList<QuestNpcIdentity> Export() =>
        _identities.Values.OrderBy(identity => identity.InstanceId.Value).ToArray();

    public void Import(IEnumerable<QuestNpcIdentity> identities)
    {
        ArgumentNullException.ThrowIfNull(identities);
        var candidate = new QuestNpcInstanceRegistry();
        foreach (var identity in identities)
        {
            if (candidate._identities.ContainsKey(identity.CharacterId))
                throw new InvalidDataException("Ismétlődő NPC karakterazonosító a mentésben.");
            candidate.Register(identity);
        }
        _identities = candidate._identities;
        _owners = candidate._owners;
        _nextId = candidate._nextId;
    }

    private void Register(QuestNpcIdentity identity)
    {
        if (identity.InstanceId.IsNone || identity.CharacterId.Value == Guid.Empty ||
            identity.NpcId == QuestNpcId.None || !Enum.IsDefined(identity.NpcId))
            throw new InvalidDataException("Érvénytelen quest NPC azonosság.");
        if (_identities.TryGetValue(identity.CharacterId, out var existing) && existing != identity ||
            _owners.TryGetValue(identity.InstanceId, out var owner) && owner != identity.CharacterId)
            throw new InvalidDataException("Ütköző quest NPC példányazonosító vagy karakterazonosság.");
        _identities[identity.CharacterId] = identity;
        _owners[identity.InstanceId] = identity.CharacterId;
        _nextId = Math.Max(_nextId, (long)identity.InstanceId.Value + 1);
    }
}
