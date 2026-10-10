using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Quests;

namespace KaoszRubin.World;

public sealed record CampaignBoss(int Level, string EnemyId, string RoomId, string RoomName, QuestId QuestId);

/// <summary>A kampány tizenkét, egyedi aranykulcsőrzője.</summary>
public static class CampaignBosses
{
    public static readonly IReadOnlyList<CampaignBoss> All =
    [
        new(9, MonsterIds.OrkTörzsfő, "ORC_WAR_COUNCIL", "Harci tanácsterem", QuestId.OrcDeserterWarchief),
        new(11, MonsterIds.Fagyóriás, "HROLD_FROZEN_HALL", "Jégszakáll csarnoka", QuestId.GiantHunterHrold),
        new(12, MonsterIds.VörösSárkány, "AZRAKAR_EMBER_SANCTUM", "Parázsszentély", QuestId.DragonResearcherAzrakar),
        new(14, MonsterIds.ŐsiHidra, "SSIZARA_DROWNED_DEN", "Elárasztott méregcsarnok", QuestId.FerrymanAncientHydra),
        new(15, MonsterIds.GyíkemberKirály, "SCALED_KING_THRONE", "Királyi pikkelytrón", QuestId.ArchivistScaledKing),
        new(16, MonsterIds.KígyóFőpap, "SHEDDING_HIGH_ALTAR", "A vedlő isten főoltára", QuestId.FugitiveSnakeHighPriest),
        new(18, MonsterIds.VénBeholder, "XYRAX_CRYSTAL_EYE", "A Századik Tekintet szentélye", QuestId.CrystalEngineerXyrax),
        new(19, MonsterIds.Csontsárkány, "OSSYRA_FROZEN_OATH", "A dermedt eskü sírkamrája", QuestId.FrostGuideOssyra),
        new(20, MonsterIds.Ősvámpír, "VELKHAR_NIGHT_THRONE", "Az örökéj trónterme", QuestId.SpiritSeerVelkhar),
        new(21, MonsterIds.Drakolich, "NHARAZ_BLACK_GOSPEL", "A fekete evangélium kriptája", QuestId.DragonResearcherNharaz),
        new(23, MonsterIds.BalorDémon, "ASHKAROTH_BLOOD_THRONE", "Vértrónus", QuestId.DemonHunterAshkaroth),
        new(24, MonsterIds.Káoszsárkány, "KAEL_ZHUR_LAST_SEAL", "Az utolsó lakat terme", QuestId.ChaosPilgrimKaelZhur),
    ];

    public static CampaignBoss? ForLevel(int level) => All.FirstOrDefault(boss => boss.Level == level);
    public static bool IsBossRoom(string roomId) => All.Any(boss => boss.RoomId == roomId);
    public static bool CanLeave(int level, IEnumerable<string> collectedKeyIds) =>
        ForLevel(level) is not { } boss || collectedKeyIds.Contains(boss.EnemyId, StringComparer.OrdinalIgnoreCase);
}

