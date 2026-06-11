using System;

namespace MutantArmy.Domain
{
    /// <summary>
    /// Resolução do portal de risco (doc 12 §4.3): RNG SEMPRE injetado (System.Random,
    /// nunca UnityEngine.Random) — determinístico por seed para QA e auditoria das odds
    /// exibidas (CANON §3.4: portais honestos, o RNG usa as odds mostradas).
    /// </summary>
    public static class RiskGate
    {
        /// <summary>
        /// Retorna o novo TOTAL do exército (mesma semântica de total-alvo do GateMath):
        /// sucesso → ×rewardMult; falha → ×failPenalty com piso de 1 unidade.
        /// </summary>
        public static int Resolve(Random rng, float successChance, float rewardMult, float failPenalty, int current)
        {
            bool success = rng.NextDouble() < successChance;
            float mult = success ? rewardMult : failPenalty;
            return GateMath.Apply(GateType.Multiply, mult, current);
        }
    }
}
