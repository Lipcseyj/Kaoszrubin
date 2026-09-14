namespace KaoszRubin.Data;

// A 21-es és korábbi mentések numerikus állapotértékei. Nem futásidejű questállapot.
public enum LegacyNpcQuestState { Offered = 0, Active = 1, Completed = 2, Abandoned = 3 }

/// <summary>Kizárólag a régi WorldNpcSaveData.Quests mező importálásához szükséges szerződés.</summary>
public sealed record LegacyNpcQuestProgress(
    string QuestId, LegacyNpcQuestState State = LegacyNpcQuestState.Offered, int Progress = 0);
