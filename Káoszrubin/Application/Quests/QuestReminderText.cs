using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

public static class QuestReminderText
{
    public const int TemplateCount = 5;

    public static string Build(LiveCharacter npc, IReadOnlyList<QuestHandle> quests, int templateIndex)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(quests);
        if (quests.Count == 0) return string.Empty;

        var descriptor = $"{npc.Race.Name.ToLowerInvariant()} {npc.CharacterClass.Name.ToLowerInvariant()}";
        var subject = $"{Article(descriptor)} {descriptor}";
        var opening = Math.Abs(templateIndex % TemplateCount) switch
        {
            0 => $"{subject} szúrósan végigmér benneteket.",
            1 => $"{subject} jöttötökre türelmetlenül felsóhajt. „Még mindig semmi?”",
            2 => $"{subject} összefonja a karját, és várakozóan néz rátok.",
            3 => $"{subject} komoran biccent, de láthatóan többet várt tőletek.",
            _ => $"{subject} egy pillantással jelzi, hogy még nem feledte el a megbízást."
        };
        var objectives = string.Join(' ', quests.Select(ObjectiveSentence));
        return $"{opening} {objectives} Utána gyertek vissza.";
    }

    public static string ObjectiveSentence(QuestHandle quest)
    {
        var remaining = Math.Max(1, quest.RequiredCount - quest.Progress);
        var count = CountText(remaining);
        return quest.Objective switch
        {
            QuestObjective.CollectItem objective =>
                $"Szerezzétek meg: {objective.Item.Name}, még {count} darab.",
            QuestObjective.KillEnemy objective =>
                $"Győzzétek le: {objective.Enemy.Name}, még {count} példány.",
            QuestObjective.KillEnemyWithTraits =>
                $"Győzzetek le még {count} olyan ellenséget, aki megfelel a megbízásnak.",
            QuestObjective.ExploreLocation => "Találjátok meg a kijáratot.",
            QuestObjective.DisarmTraps => $"Hatástalanítsatok még {count} csapdát.",
            QuestObjective.OpenChests => $"Nyissatok ki még {count} kincsesládát.",
            QuestObjective.OpenQuestChest => "Találjátok meg és nyissátok ki a kijelölt küldetésládát.",
            QuestObjective.EscortNpc => "Kísérjétek el a védencet élve a kijárathoz.",
            _ => quest.Description.TrimEnd('.') + "."
        };
    }

    private static string Article(string text) => text.Length > 0 &&
        "aáeéiíoóöőuúüű".Contains(char.ToLowerInvariant(text[0])) ? "Az" : "A";

    private static string CountText(int count) => count switch
    {
        1 => "egy", 2 => "két", 3 => "három", 4 => "négy", 5 => "öt",
        6 => "hat", 7 => "hét", 8 => "nyolc", 9 => "kilenc", 10 => "tíz",
        _ => count.ToString()
    };
}
