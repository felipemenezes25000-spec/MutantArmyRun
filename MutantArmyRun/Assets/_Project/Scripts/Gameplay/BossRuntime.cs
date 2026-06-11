using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Estado VIVO do boss (doc 12 §4.5/§5.1): HP, fase, fraqueza rotativa e timers moram
    /// aqui — o BossConfigSO é read-only em runtime. Mutar SO em play mode persiste sujeira
    /// no asset e dessincroniza o tuning de Remote Config; por isso este runtime existe.
    /// </summary>
    public sealed class BossRuntime : IDamageable
    {
        private int _weaknessIndex;
        private float _slowFraction;
        private readonly Countdown _slow = new Countdown();   // Domain: lentidão do Gelo (2 s, não acumula)

        public BossRuntime(BossConfigSO config, float hpMultiplier)
        {
            Config = config;
            MaxHp = config.maxHp * Mathf.Max(0.01f, hpMultiplier);
            Hp = MaxHp;
            Phase = 0;
            FightTime = 0f;
            _weaknessIndex = 0;
            ActiveWeakness = config.weaknesses != null && config.weaknesses.Length > 0
                ? config.weaknesses[0]
                : ElementType.None;
        }

        public BossConfigSO Config { get; }
        public float MaxHp { get; }
        public float Hp { get; set; }
        public int Phase { get; set; }
        public float FightTime { get; set; }
        public ElementType ActiveWeakness { get; private set; }
        public BodyType BodyType => Config.bodyType;

        /// <summary>Fator de ritmo do boss sob lentidão de Gelo: 1 = normal, 0.7 = −30%.</summary>
        public float SlowFactor => _slow.Done ? 1f : 1f - _slowFraction;

        /// <summary>Cooldown do especial encurta por fase (doc 12 §4.5): −25% por fase, piso 1 s.</summary>
        public float SpecialCooldown()
        {
            float cd = Config.specialBaseCooldown * (1f - 0.25f * Phase);
            return Mathf.Max(1f, cd);
        }

        /// <summary>Fraqueza rotativa do Alien Supremo (CANON §6): cicla a lista a cada fase.</summary>
        public void RotateWeakness()
        {
            ElementType[] w = Config.weaknesses;
            if (w == null || w.Length == 0) return;
            _weaknessIndex = (_weaknessIndex + 1) % w.Length;
            ActiveWeakness = w[_weaknessIndex];
        }

        /// <summary>Tick dos efeitos de status — chamado pelo BossManager com o dt da luta.</summary>
        public void TickStatus(float dt)
        {
            _slow.Tick(dt);
        }

        // ---- IDamageable: alvo de DoTs/efeitos do CombatSystem ----
        public void TakeDamage(float amount)
        {
            Hp -= amount;
        }

        public void ApplySlow(float fraction, float seconds)
        {
            // "não acumula" (CANON §4): reaplicar REINICIA o timer, nunca soma frações
            _slowFraction = Mathf.Clamp01(fraction);
            _slow.Set(seconds);
        }
    }
}
