namespace KaoszRubin;

public static class ReturnExpeditionRules
{
    public const int EnemyPopulationPercent = 30;
    public const int TravelNeedCost = 3;

    public static int TargetNormalEnemyCount(int originalNormalEnemyCount) =>
        originalNormalEnemyCount <= 0 ? 0 : Math.Max(1,
            (int)Math.Ceiling(originalNormalEnemyCount * EnemyPopulationPercent / 100d));

    public static int AdditionalEnemiesNeeded(int originalNormalEnemyCount, int survivingNormalEnemyCount) =>
        Math.Max(0, TargetNormalEnemyCount(originalNormalEnemyCount) - Math.Max(0, survivingNormalEnemyCount));
}
