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
    }
}
