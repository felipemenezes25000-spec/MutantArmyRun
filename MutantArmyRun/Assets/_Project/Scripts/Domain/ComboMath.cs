using System;

namespace MutantArmy.Domain
{
    /// <summary>
    /// Matemática PURA dos combos de fim de corrida (missão Nota 10): o ComboSystem (Gameplay)
    /// acumula os stats durante a fase e chama Evaluate UMA vez no fim — padrão PLANO do
    /// SupplyLedger: o Domain só decide quais combos foram conquistados, a camada Unity
    /// dispara os eventos/celebrations. Buffer preenchido pelo chamador (zero alocação,
    /// mesma filosofia dos payloads struct do GameEvents).
    /// </summary>
    public static class ComboMath
    {
        /// <summary>
        /// Fotografia da corrida no momento da avaliação. Campos minúsculos espelhando o que
        /// os managers já medem (GateManager: escolhas; CombatSystem: weakness hits/overkill;
        /// CrowdManager: perdas/pico; BossManager: duração da luta).
        /// </summary>
        public struct RunComboStats
        {
            public int bestGateChoices;     // escolhas de portal que eram a rota ótima do par
            public int totalGateChoices;    // pares de portal decididos na corrida
            public int weaknessHits;        // golpes classificados Weakness pelo WeaknessJudge
            public int unitsLostOnTrack;    // unidades perdidas na PISTA (obstáculos/inimigos)
            public int survivors;           // vivos no fim da luta
            public int armyPeak;            // maior tamanho do exército na corrida
            public float bossFightSeconds;  // duração da luta contra o boss
            public float overkillDamage;    // dano causado ALÉM do HP restante no golpe final
            public float bossMaxHp;         // HP máximo do boss (base do Overkill proporcional)
        }

        /// <summary>
        /// Avalia os combos conquistados e preenche o buffer do chamador (na ordem do enum).
        /// Retorna quantos foram ESCRITOS — buffer de tamanho ComboKind (6) nunca trunca.
        /// Combos de execução (BossBreaker/Clutch/NoLoss/Overkill) exigem vitória; os de
        /// leitura de rota (PerfectGate/WeaknessHit) pagam mesmo em derrota — reforço positivo
        /// de decisão correta, nunca punição (CANON §3).
        /// </summary>
        public static int Evaluate(RunComboStats s, bool won, ComboKind[] buffer)
        {
            int count = 0;

            if (s.bestGateChoices == s.totalGateChoices && s.totalGateChoices > 0)
                count = Append(buffer, count, ComboKind.PerfectGate);

            if (s.weaknessHits > 0)
                count = Append(buffer, count, ComboKind.WeaknessHit);

            if (won && s.bossFightSeconds > 0f && s.bossFightSeconds <= 8f)
                count = Append(buffer, count, ComboKind.BossBreaker);

            // Clutch: venceu por um fio — sobreviventes ≤ 10% do pico (piso 1: pico pequeno
            // ainda permite o combo com exatamente 1 sobrevivente).
            if (won && s.survivors > 0 && s.armyPeak > 0
                && s.survivors <= Math.Max(1, (int)(s.armyPeak * 0.1f)))
                count = Append(buffer, count, ComboKind.Clutch);

            if (won && s.unitsLostOnTrack == 0)
                count = Append(buffer, count, ComboKind.NoLoss);

            if (won && s.bossMaxHp > 0f && s.overkillDamage >= s.bossMaxHp * 0.25f)
                count = Append(buffer, count, ComboKind.Overkill);

            return count;
        }

        /// <summary>Bônus em moedas de UM combo (tabela determinística, padrão SeasonPassMath).</summary>
        public static int BonusCoins(ComboKind kind)
        {
            switch (kind)
            {
                case ComboKind.PerfectGate: return 25;
                case ComboKind.WeaknessHit: return 15;
                case ComboKind.BossBreaker: return 40;
                case ComboKind.Clutch: return 50;
                case ComboKind.NoLoss: return 30;
                case ComboKind.Overkill: return 20;
                default: return 0;
            }
        }

        // Escreve no buffer só se couber — buffer curto degrada (trunca), nunca lança.
        private static int Append(ComboKind[] buffer, int count, ComboKind kind)
        {
            if (buffer == null || count >= buffer.Length) return count;
            buffer[count] = kind;
            return count + 1;
        }
    }
}
