using System;

namespace MutantArmy.Domain
{
    /// <summary>
    /// Curvas canônicas de economia (CANON §8/§9, doc 12 §4.6). Funções puras —
    /// a camada Unity injeta os parâmetros vindos do Remote Config e dos upgrades.
    /// </summary>
    public static class EconomyMath
    {
        /// <summary>
        /// Recompensa da fase (CANON §8): fase 1 = 100 moedas; cresce baseReward × growth^(fase−1).
        /// <paramref name="mult"/> = 1 + TrackBonus(RewardMultiplier, nível) na chamada real.
        /// </summary>
        public static int LevelReward(int levelIndex, float baseReward = 100f, float growth = 1.10f, float mult = 1f)
        {
            if (levelIndex < 1) levelIndex = 1;
            return (int)MathF.Round(baseReward * MathF.Pow(growth, levelIndex - 1) * mult);
        }

        /// <summary>
        /// XP concedida por fase (CANON §8: XP sobe o nível do jogador e desbloqueia features).
        /// Curva simples linear: base + step × (fase−1) — fase 1 = base, fase 2 = base+step, …
        /// Creditada UMA vez por corrida na RunWallet; recalibrável por Remote Config.
        /// </summary>
        public static int LevelXp(int levelIndex, int baseXp = 20, int step = 10)
        {
            if (levelIndex < 1) levelIndex = 1;
            if (baseXp < 0) baseXp = 0;
            if (step < 0) step = 0;
            return baseXp + step * (levelIndex - 1);
        }

        /// <summary>Custo da trilha de upgrade (CANON §8/§9): custo(n) = costBase × growth^n; nível 0 = 100.</summary>
        public static int UpgradeCost(int level, float costBase = 100f, float growth = 1.35f)
        {
            if (level < 0) level = 0;
            return (int)MathF.Round(costBase * MathF.Pow(growth, level));
        }

        /// <summary>
        /// Bônus da trilha (CANON §9): +5% por nível (retorno fracionário, ex.: 0.15f).
        /// Exceção: StartArmy retorna UNIDADES inteiras — +1 unidade a cada 2 níveis.
        /// </summary>
        public static float TrackBonus(UpgradeTrack track, int level)
        {
            if (level < 0) level = 0;
            if (track == UpgradeTrack.StartArmy)
            {
                return level / 2;        // divisão inteira proposital: nível 3 ainda dá +1
            }
            return level * 0.05f;
        }

        /// <summary>
        /// Fragmentos para evoluir a tropa do nível n para n+1 (CANON §8): 10 × 2^(n−1).
        /// Nível máximo 10 → último custo válido é ShardsToLevel(9). n inválido retorna 0.
        /// </summary>
        public static int ShardsToLevel(int n)
        {
            if (n < 1) return 0;
            return 10 * (1 << (n - 1));
        }

        /// <summary>
        /// Moedas para evoluir a tropa do nível n para n+1 (doc 07 §6): 100 × 2^(n−1) × raridade.
        /// O multiplicador de raridade vem de <see cref="RarityCoinMultiplier"/> (C×1 · R×2 · E×4 · L×8);
        /// <paramref name="rarityMult"/> é injetado já resolvido para a curva ficar pura. n inválido
        /// (fora de 1..9, pois nv máx é 10) retorna 0.
        /// </summary>
        public static long EvolveCoinCost(int n, int rarityMult = 1)
        {
            if (n < 1 || n > 9) return 0;
            if (rarityMult < 1) rarityMult = 1;
            return 100L * (1L << (n - 1)) * rarityMult;
        }

        /// <summary>Prêmio de moedas da evolução por raridade (doc 07 §6): Comum 1 · Raro 2 · Épico 4 · Lendário 8.</summary>
        public static int RarityCoinMultiplier(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Rare: return 2;
                case Rarity.Epic: return 4;
                case Rarity.Legendary: return 8;
                default: return 1;   // Common
            }
        }

        // Limiares ACUMULADOS de XP por nível de jogador (doc 07 §3.3 — array xp_per_level_curve).
        // Índice = nível; [0]/[1] = 0 (nível 1 não exige XP). Tabela curada para cair nos marcos do
        // CANON §16 (nível 2 na fase 5, nível 3 na fase 7, nível 4 na fase 10). Acima do nível 20 a
        // curva é extrapolada por degrau geométrico (ver PlayerLevelXpThreshold).
        private static readonly int[] PlayerXpThresholds =
        {
            0, 0, 120, 220, 380, 550, 750, 1000, 1350, 1800, 2350,
            3000, 3800, 4800, 6000, 7400, 9000, 10800, 12800, 15000, 17400
        };

        private const int MaxTabulatedPlayerLevel = 20;

        /// <summary>
        /// XP ACUMULADA necessária para ATINGIR o nível de jogador <paramref name="level"/> (doc 07 §3.3).
        /// Tabela curada nos marcos do CANON §16 (nível 2 = 120, 3 = 220, 4 = 380, … 10 = 2.350, 20 = 17.400);
        /// acima do nível 20 estende por ×1,16/nível sobre o último degrau (mantém o ritmo de fim de tabela).
        /// Recalibrável por Remote Config. Monotônica; nível ≤ 1 → 0.
        /// </summary>
        public static int PlayerLevelXpThreshold(int level)
        {
            if (level <= 1) return 0;
            if (level <= MaxTabulatedPlayerLevel) return PlayerXpThresholds[level];

            // Extrapolação: o último DEGRAU (Δ entre nível 20 e 19) cresce ×1,16 por nível extra
            // e vai sendo somado ao acumulado — sem teto, sem regressão.
            int acc = PlayerXpThresholds[MaxTabulatedPlayerLevel];
            int step = PlayerXpThresholds[MaxTabulatedPlayerLevel] - PlayerXpThresholds[MaxTabulatedPlayerLevel - 1];
            for (int l = MaxTabulatedPlayerLevel + 1; l <= level; l++)
            {
                step = (int)MathF.Round(step * 1.16f);
                acc += step;
            }
            return acc;
        }

        /// <summary>XP do PRÓXIMO nível (delta level→level+1) — o EconomySystem usa para o level-up incremental.</summary>
        public static int PlayerLevelXpToNext(int level)
        {
            if (level < 1) level = 1;
            return PlayerLevelXpThreshold(level + 1) - PlayerLevelXpThreshold(level);
        }

        /// <summary>
        /// Velocidade de CORRIDA efetiva da trilha Speed (doc 07 §5.3): +5%/nível com cap de +50%
        /// (nível 10). Do nível 11 em diante o ganho vai só para a velocidade de ATAQUE, então a
        /// corrida estabiliza em 1.5×. Retorna o multiplicador (1.0 = sem bônus).
        /// </summary>
        public static float SpeedRunMultiplier(int speedLevel, float perLevel = 0.05f, float capFraction = 0.50f)
        {
            if (speedLevel < 0) speedLevel = 0;
            if (perLevel < 0f) perLevel = 0f;
            float frac = speedLevel * perLevel;
            if (frac > capFraction) frac = capFraction;
            return 1f + frac;
        }

        /// <summary>
        /// Fator de perdas por obstáculo da trilha ObstacleResist (doc 07 §5.3): composto 0.95^nível,
        /// nunca chega a imunidade (assintótico a 0). nível 30 ⇒ perde só ~21% do normal.
        /// </summary>
        public static float ObstacleLossFactor(int resistLevel, float perLevel = 0.95f)
        {
            if (resistLevel < 0) resistLevel = 0;
            if (perLevel < 0f) perLevel = 0f;
            if (perLevel > 1f) perLevel = 1f;
            return (float)Math.Pow(perLevel, resistLevel);
        }
    }
}
