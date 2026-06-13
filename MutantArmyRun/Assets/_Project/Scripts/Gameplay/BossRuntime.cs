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
        private float _vulnerabilityMultiplier = 1f;
        private float _contactDpsMultiplier = 1f;
        private float _specialDamageMultiplier = 1f;
        private float _specialCooldownMultiplier = 1f;

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

        /// <summary>Variante RARA (RareBossMath, missão Nota 10): HP ×1.5 já aplicado no BeginFight.</summary>
        public bool IsRare { get; set; }

        /// <summary>Fator de ritmo do boss sob lentidão de Gelo: 1 = normal, 0.7 = −30%.</summary>
        public float SlowFactor => _slow.Done ? 1f : 1f - _slowFraction;

        // ---- Multiplicadores dos BossBehaviors (missão Nota 10, W2-A) ----
        // O SO é READ-ONLY em runtime: cada efeito de behavior (núcleo exposto do Escorpião,
        // desespero do Gigante, voo do Dragão, escudo do Rei de Gelo) entra por aqui e morre
        // junto do runtime no fim da luta — nunca vaza tuning sujo para a próxima.

        /// <summary>Dano RECEBIDO ×N (núcleo exposto 2.0, voo 0.25, escudo 0.4). Aplicado no funil BossManager.ApplyDamage.</summary>
        public float VulnerabilityMultiplier
        {
            get => _vulnerabilityMultiplier;
            set => _vulnerabilityMultiplier = value < 0f ? 0f : value;
        }

        /// <summary>Dano de CONTATO efetivo ×N (desespero do Gigante = 1.25). Consumido pelo CombatSystem.</summary>
        public float ContactDpsMultiplier
        {
            get => _contactDpsMultiplier;
            set => _contactDpsMultiplier = value < 0f ? 0f : value;
        }

        /// <summary>Dano do ESPECIAL efetivo ×N (laser fase 2 do Escorpião = 1.5).</summary>
        public float SpecialDamageMultiplier
        {
            get => _specialDamageMultiplier;
            set => _specialDamageMultiplier = value < 0f ? 0f : value;
        }

        /// <summary>Cooldown do especial ×N (&lt;1 = mais frequente; batida no chão do Gigante = 0.7).</summary>
        public float SpecialCooldownMultiplier
        {
            get => _specialCooldownMultiplier;
            set => _specialCooldownMultiplier = value < 0f ? 0f : value;
        }

        /// <summary>contactDps do SO × multiplicador de behavior — o CombatSystem consome ESTE valor.</summary>
        public float EffectiveContactDps => Config.contactDps * _contactDpsMultiplier;

        /// <summary>specialAttackDamage do SO × multiplicador de behavior — o FireSpecial consome ESTE valor.</summary>
        public float EffectiveSpecialDamage => Config.specialAttackDamage * _specialDamageMultiplier;

        /// <summary>Cooldown do especial encurta por fase (doc 12 §4.5): −25% por fase, piso 1 s.
        /// O multiplicador de behavior entra ANTES do piso — o piso continua absoluto.</summary>
        public float SpecialCooldown()
        {
            float cd = Config.specialBaseCooldown * (1f - 0.25f * Phase) * _specialCooldownMultiplier;
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
