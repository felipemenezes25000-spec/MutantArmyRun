using System;

namespace MutantArmy.Domain
{
    /// <summary>
    /// Matemática pura de retenção diária (doc 07 §2/§3): recompensa de login em streak de 7 dias,
    /// recompensas de missão diária e a regra de RESET por dia-calendário UTC (consistente entre
    /// fusos e troca de relógio — mesma base do RewardSystem.CanClaimDailyChest). Sem UnityEngine.
    /// </summary>
    public static class MissionMath
    {
        /// <summary>
        /// Dia-calendário UTC de um instante Unix (segundos) — número de dias desde a época.
        /// Dois claims no MESMO dia UTC compartilham este número; vira em UTC midnight.
        /// </summary>
        public static long UtcDayIndex(long unixSeconds)
        {
            return unixSeconds / 86400L;   // divisão inteira: piso para o dia UTC (segundos negativos não ocorrem aqui)
        }

        /// <summary>True se <paramref name="nowUnix"/> está em um dia UTC POSTERIOR ao do último claim.</summary>
        public static bool IsNewUtcDay(long lastClaimUnix, long nowUnix)
        {
            if (lastClaimUnix <= 0) return true;
            return UtcDayIndex(nowUnix) > UtcDayIndex(lastClaimUnix);
        }

        /// <summary>
        /// Resolve o novo valor de streak ao reivindicar HOJE (doc 07 §2.5):
        /// - mesmo dia do último claim → streak inalterado (claim repetido é no-op no chamador);
        /// - dia seguinte exato → streak + 1;
        /// - gap de ≥2 dias → reseta para 1.
        /// O ciclo do calendário (7 dias) é tratado pelo índice do dia em LoginRewardCoins.
        /// </summary>
        public static int NextLoginStreak(int currentStreak, long lastClaimUnix, long nowUnix)
        {
            if (currentStreak < 0) currentStreak = 0;
            if (lastClaimUnix <= 0) return 1;                 // primeiro login de sempre

            long lastDay = UtcDayIndex(lastClaimUnix);
            long today = UtcDayIndex(nowUnix);
            if (today <= lastDay) return Math.Max(1, currentStreak);   // mesmo dia: sem mudança
            if (today == lastDay + 1) return currentStreak + 1;        // dia seguinte: cresce
            return 1;                                                  // gap: recomeça
        }

        // Calendário de 7 dias (doc 07 §2.2 — recompensa crescente; dia 7 dá baú/destaque).
        // Índice do dia no ciclo = (streak−1) % 7. Moedas e gemas por dia do ciclo:
        private static readonly long[] LoginCoinsByCycleDay = { 100, 150, 200, 300, 400, 600, 1000 };
        private static readonly int[] LoginGemsByCycleDay = { 0, 0, 5, 0, 10, 0, 20 };

        public const int LoginCycleLength = 7;

        /// <summary>Dia do ciclo de 7 (0..6) para um streak ≥1 (doc 07 §2.2). Streak 8 volta ao dia 1.</summary>
        public static int LoginCycleDay(int streak)
        {
            if (streak < 1) streak = 1;
            return (streak - 1) % LoginCycleLength;
        }

        /// <summary>Moedas do login do dia, dado o streak atual (já reivindicado) — doc 07 §2.2.</summary>
        public static long LoginRewardCoins(int streak)
        {
            return LoginCoinsByCycleDay[LoginCycleDay(streak)];
        }

        /// <summary>Gemas do login do dia, dado o streak atual — doc 07 §2.2 (dias 3/5/7 do ciclo).</summary>
        public static int LoginRewardGems(int streak)
        {
            return LoginGemsByCycleDay[LoginCycleDay(streak)];
        }

        // ---- Missões diárias (doc 07 §2.1: 100 × Mb moedas + 10 gemas por missão; +10 gemas ao completar as 3) ----

        public const int DailyMissionCount = 3;
        public const long MissionRewardCoinsBase = 100;   // × Mb na Meta
        public const int MissionRewardGems = 10;
        public const int AllMissionsBonusGems = 10;       // bônus por completar as 3 (doc 07 §2.2)

        /// <summary>Recompensa de moedas de uma missão dado o multiplicador de mundo Mb (doc 07 §2.1).</summary>
        public static long MissionCoins(float worldMult = 1f)
        {
            if (worldMult < 1f) worldMult = 1f;
            return (long)MathF.Round(MissionRewardCoinsBase * worldMult);
        }

        /// <summary>Progresso atingiu o alvo? (clamp defensivo — alvo ≤0 nunca completa).</summary>
        public static bool IsComplete(int progress, int target)
        {
            return target > 0 && progress >= target;
        }
    }
}
