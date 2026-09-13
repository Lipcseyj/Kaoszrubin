using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

public sealed class EliraNpcApi
    : QuestNpcHandle
{
    internal EliraNpcApi(
        QuestManager manager)
        : base(
            manager,
            QuestNpcId.EliraSilverbranch)
    {
        Quests =
            new EliraQuestApi(manager);
    }

    public EliraQuestApi Quests { get; }

    /// <summary>A kijárati búcsúzás/csatlakozás csak a mentőküldetés elfogadott leadása után nyílhat meg.</summary>
    public bool CanResolveDeparture => Quests.Rescue.IsCompleted;
}
