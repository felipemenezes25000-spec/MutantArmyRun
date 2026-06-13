using System;

namespace MutantArmy.Domain
{
    /// <summary>
    /// Variante RARA de boss (missão Nota 10): roll determinístico por seed (padrão RiskGate —
    /// System.Random SEMPRE injetado; o BossManager usa o RNG derivado da fase) e
    /// multiplicadores fixos da variante. Chance capada em 25% para a raridade continuar
    /// sendo evento — tuning via Remote Config nunca passa do teto.
    /// </summary>
    public static class RareBossMath
    {
        /// <summary>HP da variante rara = HP base × 1.5 (mais dura, recompensa ×3 compensa).</summary>
        public static float HpMultiplier => 1.5f;

        /// <summary>Recompensa da variante rara = recompensa base × 3 (momento de sorte memorável).</summary>
        public static float RewardMultiplier => 3f;

        /// <summary>
        /// Decide se o boss desta corrida nasce raro. chance é clampada em 0..0.25;
        /// fora disso o tuning quebraria o contrato de raridade (0 nunca, 0.25 no máx).
        /// </summary>
        public static bool Roll(Random rng, float chance)
        {
            if (rng == null) return false;
            if (chance < 0f) chance = 0f;
            if (chance > 0.25f) chance = 0.25f;
            // SEMPRE consome exatamente 1 draw, mesmo com chance 0 — a ordem/contagem de
            // consumo do RNG é contrato de determinismo (regra 6) e não pode depender do tuning.
            return rng.NextDouble() < chance;
        }
    }
}
