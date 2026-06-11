using System;

namespace MutantArmy.Domain
{
    /// <summary>
    /// Evento de wave da arena do boss como dado puro (doc 12 §4.5). Campos minúsculos
    /// espelham o ArenaWaveEvent do BossConfigSO; aqui o tipo de inimigo é id numérico
    /// (o SO mapeia UnitConfigSO → id ao delegar para o Domain).
    /// </summary>
    public struct ArenaWave
    {
        public float time;
        public int enemyTypeId;
        public int count;
    }

    /// <summary>
    /// Consumo de waves por PONTEIRO de próximo evento (doc 12 §4.5, decisão 1):
    /// lista ordenada por tempo, cada evento dispara exatamente 1× — nunca polling
    /// "(int)timer == x", que pula ou duplica conforme o framerate.
    /// </summary>
    public static class WavePointer
    {
        /// <summary>Avança o ponteiro e retorna quantos eventos venceram em ordem até o tempo t.</summary>
        public static int Consume(float t, ArenaWave[] sorted, ref int next)
        {
            int fired = 0;
            while (next < sorted.Length && t >= sorted[next].time)
            {
                next++;
                fired++;
            }
            return fired;
        }
    }

    /// <summary>Combate agregado do doc 12 §4.4: o crowd ataca como soma de DPS por tick.</summary>
    public static class CombatMath
    {
        /// <summary>
        /// DPS agregado do exército: base × chart elemental × (1 + bônus da trilha
        /// "Dano contra boss"); crítico dobra (×2).
        /// </summary>
        public static float CrowdDps(float baseDps, float chartMult, float bossDamageBonus, bool crit)
        {
            float dps = baseDps * chartMult * (1f + bossDamageBonus);
            if (crit) dps *= 2f;
            return dps;
        }

        /// <summary>
        /// Fase de agressividade do boss (0/1/2) pelos limiares canônicos 0.5/0.25
        /// (doc 12 §4.5 PhaseThresholds). Atingir o limiar já muda a fase.
        /// </summary>
        public static int BossPhase(float hp, float maxHp)
        {
            float frac = maxHp > 0f ? hp / maxHp : 0f;
            if (frac <= 0.25f) return 2;
            if (frac <= 0.5f) return 1;
            return 0;
        }
    }
}
