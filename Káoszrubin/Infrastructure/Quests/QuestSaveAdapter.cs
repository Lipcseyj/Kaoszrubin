using KaoszRubin.Application;
using KaoszRubin.Application.Quests;
using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Quests;
using KaoszRubin.World;

namespace KaoszRubin.Infrastructure.Quests;

/// <summary>
/// A mentési séma és a típusos questállapot közötti határ. A régi NPC- és naplóadatokat
/// egyszer importálja; az új mentésben kizárólag a Quests blokk a hiteles állapotforrás.
/// Nem aktivál küldetést, nem jutalmaz, és nem vezérel UI-t vagy hálózati publikálást.
/// </summary>
public sealed class QuestSaveAdapter
{
    private readonly GameDataCatalog _data;
    private readonly QuestCatalog _catalog;
    private readonly QuestNpcInstanceRegistry _instances;
    private readonly Dictionary<QuestId, string> _questIds;
    private readonly Dictionary<QuestNpcId, string> _npcIds;
    private Dictionary<QuestKey, QuestRuntimeSaveData> _metadata = [];
    private List<QuestJournalSaveData> _archive = [];
    private List<string> _notes = [];

    public QuestSaveAdapter(GameDataCatalog data, QuestNpcInstanceRegistry instances)
    {
        _data = data;
        _catalog = new QuestCatalogBuilder(data).Build();
        _instances = instances;
        _questIds = data.NpcQuests.ToDictionary(q => LegacyQuestIdMap.ToQuestId(q.Id), q => q.Id);
        _npcIds = data.NpcQuests.Select(quest => quest.NpcId).Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(LegacyNpcIdMap.ToQuestNpcId, id => id);
    }

    /// <summary>A világ létrehozása előtt feloldja és ellenőrzi a teljes questmentést és az NPC-azonosságokat.</summary>
    public IReadOnlyList<QuestStateSnapshot> PrepareRestore(GameSaveData save, CharacterRoster roster)
    {
        var block = save.Quests ?? ImportLegacy(save, roster);
        if (block.States is null || block.NpcIdentities is null || block.LegacyJournalArchive is null ||
            block.MigrationNotes is null || block.States.Any(state => state is null) ||
            block.NpcIdentities.Any(identity => identity is null))
            throw new InvalidDataException("Hiányzó lista vagy null rekord a questmentésben.");
        var identities = block.NpcIdentities.Select(identity => new QuestNpcIdentity(
            Instance(identity.InstanceId), new CharacterId(identity.CharacterId),
            LegacyNpcIdMap.ToQuestNpcId(identity.NpcId))).ToArray();
        var candidate = new QuestNpcInstanceRegistry();
        candidate.Import(identities);
        var byInstance = identities.ToDictionary(identity => identity.InstanceId);
        var states = block.States.Select(ReadState).ToArray();
        // Az állapottároló ellenőrzése a teljes bemenetre, az élő manager módosítása nélkül fut.
        new QuestStateStore(_catalog).Restore(states);
        foreach (var state in states.Where(state => !state.GiverInstanceId.IsNone))
            if (!byInstance.TryGetValue(state.GiverInstanceId, out var identity) ||
                identity.NpcId != _catalog.Get(state.QuestId).Giver)
                throw new InvalidDataException($"Hiányzó vagy hibás questadó: {state.Key}.");
        foreach (var frame in Frames(save))
        {
            var seen = new HashSet<Guid>();
            var seenUnique = new HashSet<QuestNpcId>();
            foreach (var npc in Npcs(frame).Where(npc => npc.IsQuestNpc))
            {
                if (npc.CharacterId is not { } characterId || !seen.Add(characterId) ||
                    ReferenceEquals(frame, save) && !roster.Characters.Any(character => character.Id.Value == characterId) ||
                    !byInstance.TryGetValue(Instance(npc.QuestInstanceId), out var identity) ||
                    identity.CharacterId.Value != characterId ||
                    identity.NpcId != LegacyNpcIdMap.ToQuestNpcId(npc.DefinitionId))
                    throw new InvalidDataException("A mentett világ quest NPC-azonossága érvénytelen vagy ismétlődő.");
                if (_data.GetNpc(npc.DefinitionId).Unique && !seenUnique.Add(identity.NpcId))
                    throw new InvalidDataException("Egy egyedi quest NPC több karakterként szerepel ugyanabban a világban.");
            }
        }
        _instances.Import(identities);
        _metadata = block.States.ToDictionary(saved => ReadState(saved).Key);
        _archive = block.LegacyJournalArchive.ToList();
        _notes = block.MigrationNotes.ToList();
        save.Quests = block;
        return states;
    }

    public QuestSaveData Export(QuestManager manager) => new()
    {
        States = manager.ExportState().Select(WriteState).ToList(),
        NpcIdentities = _instances.Export().Select(identity => new QuestNpcIdentitySaveData(
            identity.InstanceId.Value, identity.CharacterId.Value, _npcIds[identity.NpcId])).ToList(),
        LegacyJournalArchive = _archive.ToList(),
        MigrationNotes = _notes.ToList()
    };

    public void RecordCompletion(QuestHandle quest, string experienceSummary, string itemSummary)
    {
        var state = new QuestStateSnapshot(quest.Id, quest.GiverInstanceId, quest.State,
            quest.Progress, quest.CompletionCount);
        _metadata[state.Key] = WriteState(state) with
        {
            CompletionExperienceSummary = experienceSummary,
            CompletionItemRewardSummary = itemSummary
        };
    }

    public IReadOnlyList<QuestJournalEntrySnapshot> CreateJournal(QuestManager manager) =>
        manager.ExportState().Where(state => state.State is not (QuestState.Locked or QuestState.Available))
            .Select(state =>
            {
                var saved = WriteState(state);
                return new QuestJournalEntrySnapshot(saved.QuestId, saved.Title, saved.Description,
                    saved.GiverName, state.State switch
                    {
                        QuestState.Completed => QuestJournalStatus.Completed,
                        QuestState.Failed => QuestJournalStatus.Abandoned,
                        _ => QuestJournalStatus.Active
                    }, state.Progress, _catalog.Get(state.QuestId).Objective.RequiredCount, saved.ExperienceReward,
                    saved.CompletionExperienceSummary, saved.CompletionItemRewardSummary);
            }).ToArray();

    private QuestRuntimeSaveData WriteState(QuestStateSnapshot state)
    {
        var definition = _catalog.Get(state.QuestId);
        var saved = _metadata.GetValueOrDefault(state.Key) ?? new QuestRuntimeSaveData(
            _questIds[state.QuestId], state.GiverInstanceId.Value, "locked", 0, 0,
            definition.Title, definition.Description, _data.GetNpc(_npcIds[definition.Giver]).Name,
            definition.ExperienceReward);
        return saved with
        {
            State = StateName(state.State), Progress = state.Progress, CompletionCount = state.CompletionCount
        };
    }

    private static string StateName(QuestState state) => state switch
    {
        QuestState.Locked => "locked", QuestState.Available => "available", QuestState.Active => "active",
        QuestState.ReadyToTurnIn => "ready-to-turn-in", QuestState.Completed => "completed", QuestState.Failed => "failed",
        _ => throw new InvalidDataException($"Ismeretlen questállapot: {state}.")
    };

    private static QuestStateSnapshot ReadState(QuestRuntimeSaveData saved) => new(
        LegacyQuestIdMap.ToQuestId(saved.QuestId), Instance(saved.GiverInstanceId), saved.State switch
        {
            "locked" => QuestState.Locked, "available" => QuestState.Available, "active" => QuestState.Active,
            "ready-to-turn-in" => QuestState.ReadyToTurnIn, "completed" => QuestState.Completed, "failed" => QuestState.Failed,
            _ => throw new InvalidDataException($"Ismeretlen mentett questállapot: '{saved.State}'.")
        }, saved.Progress, saved.CompletionCount);

    private static QuestNpcInstanceId Instance(int value) => value >= 0
        ? value == 0 ? default : new QuestNpcInstanceId(value)
        : throw new InvalidDataException("Negatív quest NPC példányazonosító.");

    private static IEnumerable<GameSaveData> Frames(GameSaveData root)
    {
        var visited = new HashSet<GameSaveData>(ReferenceEqualityComparer.Instance);
        for (GameSaveData? frame = root; frame is not null; frame = frame.SuspendedCampaign)
        {
            if (!visited.Add(frame)) throw new InvalidDataException("Körkörös felfüggesztett kampány a mentésben.");
            yield return frame;
        }
    }

    private static IEnumerable<WorldNpcSaveData> Npcs(GameSaveData frame) =>
        (frame.Maze.Npcs ?? []).Concat(frame.Maze.PartyAvatars
            .Where(avatar => avatar.TemporaryFollower is not null).Select(avatar => avatar.TemporaryFollower!));

    private QuestSaveData ImportLegacy(GameSaveData save, CharacterRoster roster)
    {
        var registry = new QuestNpcInstanceRegistry();
        var states = new Dictionary<QuestKey, QuestStateSnapshot>();
        var metadata = new Dictionary<QuestKey, QuestJournalSaveData>();
        var result = new QuestSaveData();
        var frames = Frames(save).ToArray();
        foreach (var frame in frames)
        {
            WorldNpcSaveData Identify(WorldNpcSaveData npc)
            {
                if (!npc.IsQuestNpc) return npc;
                var character = npc.CharacterId is { } id
                    ? roster.Characters.SingleOrDefault(character => character.Id.Value == id)
                    : npc.CharacterIndex >= 0 && npc.CharacterIndex < roster.Characters.Count
                        ? roster.Characters[npc.CharacterIndex] : null;
                if (character is null) throw new InvalidDataException("A régi quest NPC karaktere nem található.");
                var instance = registry.GetOrCreate(character.Id, LegacyNpcIdMap.ToQuestNpcId(npc.DefinitionId));
                return npc with { QuestInstanceId = instance.Value, CharacterId = character.Id.Value };
            }
            frame.Maze.Npcs = (frame.Maze.Npcs ?? []).Select(Identify).ToList();
            frame.Maze.PartyAvatars = frame.Maze.PartyAvatars.Select(avatar => avatar.TemporaryFollower is { } npc
                ? avatar with { TemporaryFollower = Identify(npc) } : avatar).ToList();
            foreach (var npc in Npcs(frame).Where(npc => npc.IsQuestNpc))
            {
                var progressById = (npc.Quests ?? []).ToDictionary(p => p.QuestId, StringComparer.OrdinalIgnoreCase);
                foreach (var id in (npc.QuestIds ?? []).Concat(progressById.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var definition = _catalog.Get(LegacyQuestIdMap.ToQuestId(id));
                    if (definition.Giver != LegacyNpcIdMap.ToQuestNpcId(npc.DefinitionId))
                        throw new InvalidDataException($"A régi quest másik NPC-hez tartozik: {id}.");
                    progressById.TryGetValue(id, out var progress);
                    var state = progress?.State switch
                    {
                        null or NpcQuestState.Offered => QuestState.Locked,
                        NpcQuestState.Active => QuestState.Active,
                        NpcQuestState.Completed => QuestState.Completed,
                        NpcQuestState.Abandoned => QuestState.Failed,
                        _ => throw new InvalidDataException($"Ismeretlen régi questállapot: {id}.")
                    };
                    Merge(Convert(definition, definition.Scope == QuestScope.Global ? default : Instance(npc.QuestInstanceId),
                        state, progress?.Progress ?? 0));
                }
            }
        }
        // A régi napló csak questazonosítót tárolt. Több lehetséges NPC esetén nem találgatunk.
        foreach (var entry in frames.SelectMany(frame => frame.QuestJournal ?? []))
        {
            QuestId id;
            try { id = LegacyQuestIdMap.ToQuestId(entry.QuestId); }
            catch (InvalidDataException) { Archive(entry, "Ismeretlen questazonosító"); continue; }
            var definition = _catalog.Get(id);
            var keys = states.Keys.Where(key => key.QuestId == id).ToArray();
            if (definition.Scope == QuestScope.PerNpcInstance && keys.Length != 1)
            {
                Archive(entry, "Nem egyértelmű NPC-példány");
                continue;
            }
            var key = definition.Scope == QuestScope.Global ? new QuestKey(id) : keys[0];
            var state = entry.Status switch
            {
                QuestJournalStatus.Completed => QuestState.Completed,
                QuestJournalStatus.Abandoned => QuestState.Failed,
                QuestJournalStatus.Active => QuestState.Active,
                _ => throw new InvalidDataException("Ismeretlen régi naplóállapot.")
            };
            var converted = Convert(definition, key.GiverInstanceId, state, entry.Progress);
            if (definition.Scope == QuestScope.PerNpcInstance)
            {
                // A napló nem tudja igazolni, hogy ugyanennek a példánynak a története.
                // Eltérő állapot esetén az NPC-rekord az elsődleges; az archívum megőrzi az ellentmondást.
                if (Rank(states[key].State) != Rank(converted.State))
                {
                    Archive(entry, "Az NPC-példány rekordjának ellentmondó naplóállapot");
                    continue;
                }
            }
            else Merge(converted);
            if (!metadata.ContainsKey(key) || Rank(converted.State) > Rank(ToState(metadata[key].Status)))
                metadata[key] = entry;
        }
        foreach (var state in states.Values.OrderBy(s => s.QuestId).ThenBy(s => s.GiverInstanceId.Value))
        {
            var definition = _catalog.Get(state.QuestId);
            metadata.TryGetValue(state.Key, out var entry);
            result.States.Add(new(_questIds[state.QuestId], state.GiverInstanceId.Value, StateName(state.State),
                state.Progress, state.CompletionCount, definition.Title, definition.Description,
                _data.GetNpc(_npcIds[definition.Giver]).Name, entry?.ExperienceReward ?? definition.ExperienceReward,
                state.State == QuestState.Completed ? entry?.CompletionExperienceSummary : null,
                state.State == QuestState.Completed ? entry?.CompletionItemRewardSummary : null));
        }
        result.NpcIdentities = registry.Export().Select(identity => new QuestNpcIdentitySaveData(
            identity.InstanceId.Value, identity.CharacterId.Value, _npcIds[identity.NpcId])).ToList();
        return result;

        void Archive(QuestJournalSaveData entry, string reason)
        {
            if (!result.LegacyJournalArchive.Contains(entry)) result.LegacyJournalArchive.Add(entry);
            result.MigrationNotes.Add($"{reason}: {entry.QuestId}; a naplóbejegyzés az archívumban megmaradt.");
        }
        void Merge(QuestStateSnapshot incoming)
        {
            if (states.TryGetValue(incoming.Key, out var previous))
            {
                if (previous != incoming) result.MigrationNotes.Add(
                    $"Állapotegyeztetés: {incoming.Key}; Completed > Failed > Active/Ready > Offered, azonos rangnál nagyobb progress.");
                if (Rank(previous.State) > Rank(incoming.State) ||
                    Rank(previous.State) == Rank(incoming.State) && previous.Progress >= incoming.Progress) return;
            }
            states[incoming.Key] = incoming;
        }
    }

    private static QuestState ToState(QuestJournalStatus status) => status switch
    {
        QuestJournalStatus.Completed => QuestState.Completed,
        QuestJournalStatus.Abandoned => QuestState.Failed,
        _ => QuestState.Active
    };

    private static int Rank(QuestState state) => state switch
    {
        QuestState.Completed => 3, QuestState.Failed => 2,
        QuestState.Active or QuestState.ReadyToTurnIn => 1, _ => 0
    };

    private static QuestStateSnapshot Convert(QuestDefinition definition, QuestNpcInstanceId instance,
        QuestState state, int progress)
    {
        var count = definition.Objective.RequiredCount;
        progress = state switch
        {
            QuestState.Locked => 0, QuestState.Completed => count, _ => Math.Clamp(progress, 0, count)
        };
        if (state == QuestState.Active && progress == count) state = QuestState.ReadyToTurnIn;
        return new(definition.Id, instance, state, progress, state == QuestState.Completed ? 1 : 0);
    }
}
