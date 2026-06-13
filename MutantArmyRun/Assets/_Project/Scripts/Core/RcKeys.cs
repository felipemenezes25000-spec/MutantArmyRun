namespace MutantArmy.Core
{
    /// <summary>
    /// Chaves de Remote Config como const (doc 12 §3.3: strings mágicas espalhadas são
    /// banidas). Os valores default de cada chave espelham o CANON e vivem nos Defaults
    /// embutidos do provider (doc 12 §4.10) — o jogo é jogável de fábrica, sem rede.
    /// </summary>
    public static class RcKeys
    {
        // Economia (CANON §8)
        public const string LevelRewardBase = "level_reward_base";          // 100
        public const string LevelRewardGrowth = "level_reward_growth";      // 1.10
        public const string LevelXpBase = "level_xp_base";                  // 20 (XP da fase 1)
        public const string LevelXpStep = "level_xp_step";                  // 10 (incremento por fase)
        public const string UpgradeCostBase = "upgrade_cost_base";          // 100
        public const string UpgradeCostGrowth = "upgrade_cost_growth";      // 1.35
        public const string ChestRareGemPrice = "chest_rare_gem_price";     // 300

        // Pacing de interstitial (CANON §11)
        public const string InterMinLevel = "inter_min_level";              // 6
        public const string InterLevelGap = "inter_level_gap";              // 3

        // Supply (CANON §3.2/§15)
        public const string SupplyOverflowCoinRate = "supply_overflow_coin_rate";  // 2
        public const string SupplyCap = "supply_cap";                       // 60 (fixo no MVP)

        // Balanceamento de boss (doc 12 §4.5)
        public const string BossHpGlobalMult = "boss_hp_global_mult";       // 1.0

        // Missão Nota 10 — tuning de variante rara, combos e inimigos de pista. Os defaults
        // espelham o que o Gameplay serializou (BossManager._rareBossChance 0.06 etc.). ENTREGA
        // ao Gameplay é por parâmetro/hook (Gameplay não enxerga Services, §2.3): a ponte
        // RC → campos serializados entra no wiring da Onda 4 — até lá os SOs/managers valem.
        public const string RareBossChance = "rare_boss_chance";            // 0.06
        public const string ComboBonusMult = "combo_bonus_mult";            // 1.0
        public const string EnemyHpMult = "enemy_hp_mult";                  // 1.0
        public const string EnemyDamageMult = "enemy_damage_mult";          // 1.0

        private const string BossHpMultPrefix = "boss_hp_mult_";

        /// <summary>Chave por boss: "boss_hp_mult_&lt;bossId&gt;" (doc 12 §4.5/§5.2).</summary>
        public static string BossHpMult(string bossId) => BossHpMultPrefix + bossId;
    }
}
