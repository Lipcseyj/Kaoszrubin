namespace KaoszRubin.Data;

/// <summary>A régi pályákon nincs helye az új questszobáknak: a katakombai szakasz jutalom nélkül lezárul.</summary>
internal static class RodericReworkSaveMigration
{
    public static void Apply(GameSaveData save)
    {
        var hasRoderic = save.Quests?.States.Any(state => state.QuestId is
            "NPCQ037" or "NPCQ038" or "NPCQ039" or "NPCQ040") == true;
        WorldNpcSaveData Convert(WorldNpcSaveData npc)
        {
            if (npc.DefinitionId != "NPC021") return npc;
            hasRoderic = true;
            if (save.LocationKind == AdventureLocationKind.Quest || npc.StoryStateId is
                "MALREC_DEFEATED" or "SECOND_CHANCE" or "JOIN_VERDICT" or "JOIN_ACCEPTED" or
                "JOIN_PENDING" or "JOINED" or "JOIN_REFUSED" or "OATH_BROKEN") return npc;
            return npc with { StoryStateId = "MALREC_READY" };
        }
        save.Maze.Npcs = save.Maze.Npcs.Select(Convert).ToList();
        save.Maze.PartyAvatars = save.Maze.PartyAvatars.Select(avatar => avatar.TemporaryFollower is { } npc
            ? avatar with { TemporaryFollower = Convert(npc) } : avatar).ToList();
        // A küldetéshelyszín aktív mentésében a felfüggesztett kampány NPC-je is előfordulhat.
        hasRoderic |= save.SuspendedCampaign?.Maze.Npcs.Any(npc => npc.DefinitionId == "NPC021") == true ||
            save.SuspendedCampaign?.Maze.PartyAvatars.Any(avatar => avatar.TemporaryFollower?.DefinitionId == "NPC021") == true;
        if (!hasRoderic) return;
        if (save.Quests is null)
        {
            save.RequiresRodericReworkQuestMigration = true;
            return;
        }
        foreach (var (id, count, title) in new[]
        {
            ("NPCQ040", 8, "Egy oldalon harcolunk"),
            ("NPCQ037", 3, "Az elesettek jelvényei"),
            ("NPCQ038", 2, "A pátriárkák árnyai"),
            ("NPCQ041", 1, "A rend ereklyéi")
        })
        {
            var index = save.Quests.States.FindIndex(state => state.QuestId == id);
            if (index >= 0)
            {
                var previous = save.Quests.States[index];
                save.Quests.States[index] = previous with
                { State = "completed", Progress = count, CompletionCount = Math.Max(1, previous.CompletionCount) };
            }
            else save.Quests.States.Add(new(id, 0, "completed", count, 1, title,
                "A korábbi Roderic-történet átálláskor lezárt szakasza.", "Sir Roderic", 0));
        }
        save.Quests.MigrationNotes.Add("Roderic régi katakombai története lezárult: a következő állomás Sir Malrec. Új jutalmat az átállás nem osztott ki.");
    }
}
