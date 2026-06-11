namespace MutantArmy.Core
{
    /// <summary>
    /// Placements de rewarded como const (doc 12 §3.3 — sem string mágica espalhada).
    /// São os 5 usos canônicos do CANON §11; rewarded é SEMPRE opcional e o botão só
    /// renderiza com IAdsProvider.IsRewardedReady.
    /// </summary>
    public static class AdPlacement
    {
        public const string DoubleReward = "double_reward";    // dobrar recompensa da fase (x2)
        public const string ReviveBoss = "revive_boss";        // reviver no boss (1×/fase)
        public const string DailyChest = "daily_chest";        // baú extra diário
        public const string TryLegendary = "try_legendary";    // testar tropa lendária por 1 fase
        public const string SpeedUpgrade = "speed_upgrade";    // acelerar upgrade
    }
}
