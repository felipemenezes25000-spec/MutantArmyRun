using System.Collections.Generic;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Alvo de dano/efeitos para o combate agregado (DoT de Veneno, lentidão de Gelo).
    /// Implementado pelo BossRuntime; inimigos de arena são agregados no BossManager.
    /// </summary>
    public interface IDamageable
    {
        float MaxHp { get; }
        float Hp { get; }
        BodyType BodyType { get; }
        void TakeDamage(float amount);
        void ApplySlow(float fraction, float seconds);
    }

    /// <summary>
    /// Resolução de dano por AGREGADOS HP/DPS com tick de 10 Hz (doc 12 §4.4) — suficiente
    /// para um combate de 10–20 s, barato em CPU. O crowd ataca como soma de DPS (× chart
    /// elemental × mutações × upgrades); o chart entra UMA vez, no cálculo do tick agregado —
    /// nunca por colisão individual. Aquisição de alvo é centralizada aqui.
    /// </summary>
    public class CombatSystem : MonoBehaviour, IInitializable
    {
        public static CombatSystem Instance { get; private set; }

        [SerializeField] private ElementChartSO _chart;   // NUNCA switch hardcoded (doc 12 §4.4)

        private const float TickRate = 0.1f;   // 10 Hz
        private float _accum;

        // bônus de meta (BossDamage/CritChance) chegam por setter: Gameplay não enxerga
        // o UpgradeSystem (fronteira de asmdef, doc 12 §2.3)
        private float _bossDamageBonus;
        private float _critChance;

        // regras de CORPO (Veneno 0% vs máquina/morto-vivo, CANON §4) vêm do chart canônico
        // do Domain — dado embutido, não switch
        private readonly ElementChart _bodyChart = ElementChart.Default();

        private struct ActiveDot
        {
            public IDamageable Target;
            public float DamagePerSecond;
            public float Remaining;
        }

        private readonly List<ActiveDot> _dots = new List<ActiveDot>();

        // Habilidades AGREGADAS de suporte (doc 12 §4.4): timers/estado por LUTA, não por unidade.
        private readonly Countdown _reviveTimer = new Countdown();    // Necromante: proc a cada 6 s
        private readonly Countdown _turretBuild = new Countdown();    // Engenheiro: torreta sobe em 1,5 s
        private bool _turretBuilt;
        private int _turretCount;                                     // 1 torreta por Engenheiro vivo na entrada
        private bool _fightInitialized;

        // injeção opcional de números finos por Remote Config (Médico 8 vs Anjo 12, etc.); sem
        // provider, usam os fallbacks canônicos do Domain.CombatAbilities. Setado pela Meta.
        private System.Func<UnitConfigSO, float> _healOf;

        public float TotalDamageDealt { get; private set; }   // alimenta o LevelResult (doc 12 §4.11)

        public void Init()   // chamado pelo bootstrap da cena Game (doc 12 §3.3)
        {
            Instance = this;
        }

        /// <summary>Chamado pela camada Meta no início da corrida (trilhas BossDamage e CritChance).</summary>
        public void SetRunBonuses(float bossDamageBonus, float critChance)
        {
            _bossDamageBonus = Mathf.Max(0f, bossDamageBonus);
            _critChance = Mathf.Clamp01(critChance);
        }

        /// <summary>
        /// Resolvedor opcional de HP/s de cura por unitId (Remote Config: rc_medic_heal_per_s 8 vs
        /// rc_angel... 12, doc 03 §6). Injetado pela Meta; sem ele, usa o fallback do Domain.
        /// </summary>
        public void SetHealResolver(System.Func<UnitConfigSO, float> healOf)
        {
            _healOf = healOf;
        }

        public void ResetRunStats()
        {
            TotalDamageDealt = 0f;
            _dots.Clear();
            _accum = 0f;
            _fightInitialized = false;
            _turretBuilt = false;
            _turretCount = 0;
            _reviveTimer.Set(0f);
            _turretBuild.Set(0f);
        }

        private void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.BossFight) return;

            BossManager bm = BossManager.Instance;
            BossRuntime boss = bm != null ? bm.Current : null;
            if (boss == null) return;

            if (!_fightInitialized) InitFight();

            _accum += Time.deltaTime;
            while (_accum >= TickRate)
            {
                _accum -= TickRate;

                // habilidades agregadas de SUPORTE rodam ANTES do dano (cura mantém uptime do DPS)
                TickAggregatedAbilities(TickRate);

                // crowd → arena: waves vivas absorvem antes do boss
                float dmg = ComputeCrowdDamage(boss, TickRate);
                dmg += TurretDamage(TickRate);                  // torreta do Engenheiro: DPS "grátis" na arena
                float leftover = bm.DamageArenaEnemies(dmg);
                if (leftover > 0f) bm.ApplyDamage(leftover);
                TotalDamageDealt += dmg;

                // o boss pode ter morrido dentro do tick — nada mais roda neste frame
                boss = bm.Current;
                if (boss == null) break;

                TickDots(TickRate);
                TickIncomingDamage(bm, boss, TickRate);

                boss = bm.Current;   // DoTs também matam
                if (boss == null) break;
            }
        }

        // ------------------------------------------------------------------
        // Habilidades AGREGADAS (doc 12 §4.4): nada por unidade — somas por tipo vivo.
        // ------------------------------------------------------------------

        // 1× por entrada na arena: conta Engenheiros (1 torreta cada) e arma o build de 1,5 s.
        private void InitFight()
        {
            _fightInitialized = true;
            _turretBuilt = false;
            CrowdManager crowd = CrowdManager.Instance;
            _turretCount = crowd != null ? crowd.CountAbility(CombatAbilities.BuildTurret) : 0;
            _reviveTimer.Set(CombatAbilities.ReviveIntervalSeconds);
            _turretBuild.Set(CombatAbilities.TurretBuildSeconds);
        }

        private void TickAggregatedAbilities(float dt)
        {
            CrowdManager crowd = CrowdManager.Instance;
            if (crowd == null) return;

            // heal_allies (Médico/Anjo): soma de HP/s × dt curando os mais feridos primeiro
            float healPerSec = crowd.TotalHealPerSecond(_healOf);
            if (healPerSec > 0f) crowd.HealArmy(healPerSec * dt);

            // revive_dead (Necromante): proc a cada 6 s, revive até 3 caídos (Supply total ≤6)
            if (crowd.CountAbility(CombatAbilities.ReviveDead) > 0)
            {
                _reviveTimer.Tick(dt);
                if (_reviveTimer.Done)
                {
                    crowd.ReviveFallen(crowd.DefaultUnit,
                        CombatAbilities.ReviveCountPerProc, CombatAbilities.ReviveSupplyBudget);
                    _reviveTimer.Set(CombatAbilities.ReviveIntervalSeconds);
                }
            }
        }

        // build_turret (Engenheiro): DPS agregado das torretas após o build de 1,5 s.
        private float TurretDamage(float dt)
        {
            if (_turretCount <= 0) return 0f;
            if (!_turretBuilt)
            {
                _turretBuild.Tick(dt);
                if (!_turretBuild.Done) return 0f;   // ainda construindo
                _turretBuilt = true;
            }
            return CombatAbilities.TurretDps * _turretCount * dt;
        }

        private float ComputeCrowdDamage(BossRuntime boss, float dt)
        {
            CrowdManager crowd = CrowdManager.Instance;
            if (crowd == null) return 0f;

            // chart + fraqueza/imunidade/corpo do boss aplicados POR UNIDADE dentro do
            // GetTotalDps(boss) (CANON §3.1/§3.4/§4); o Domain centraliza o bônus de trilha
            // e o crítico ×2 (chartMult = 1 aqui para não aplicar 2×)
            float baseDps = crowd.GetTotalDps(boss);
            bool crit = UnityEngine.Random.value < _critChance;
            float dps = CombatMath.CrowdDps(baseDps, 1f, _bossDamageBonus, crit);
            return dps * dt;
        }

        // boss + waves → crowd: dano agregado distribuído pelos índices das unidades
        private void TickIncomingDamage(BossManager bm, BossRuntime boss, float dt)
        {
            CrowdManager crowd = CrowdManager.Instance;
            if (crowd == null) return;
            float incoming = (boss.Config.contactDps * boss.SlowFactor + bm.TotalArenaDps) * dt;
            if (incoming > 0f) crowd.ApplyAggregateDamage(incoming);
        }

        /// <summary>Multiplicador elemental consultado por TODO dano do jogo (doc 12 §4.4).</summary>
        public float GetElementMultiplier(ElementType attacker, ElementType defender)
        {
            return _chart != null ? _chart.GetMultiplier(attacker, defender) : 1f;
        }

        /// <summary>DoT de Veneno (CANON §4): 3% do HP máximo por segundo, por 4 s.</summary>
        public void ApplyPoison(IDamageable target)
        {
            if (target == null) return;
            float bodyMult = _bodyChart.GetBodyMultiplier(ElementType.Poison, target.BodyType);
            if (bodyMult <= 0f) return;   // 0% vs máquinas e mortos-vivos
            _dots.Add(new ActiveDot
            {
                Target = target,
                DamagePerSecond = target.MaxHp * 0.03f * bodyMult,
                Remaining = 4f
            });
        }

        /// <summary>Lentidão de Gelo (CANON §4): 30% por 2 s — reaplicar reinicia, nunca acumula.</summary>
        public void ApplyIceSlow(IDamageable target)
        {
            if (target != null) target.ApplySlow(0.3f, 2f);
        }

        /// <summary>Encadeamento do Raio (CANON §4): 50% do dano para até 2 inimigos próximos.</summary>
        public void ChainLightning(Vector3 origin, float dmg)
        {
            if (BossManager.Instance != null)
                BossManager.Instance.DamageNearestArenaEnemies(origin, dmg * 0.5f, 2);
        }

        private void TickDots(float dt)
        {
            for (int i = _dots.Count - 1; i >= 0; i--)
            {
                ActiveDot d = _dots[i];
                if (d.Target == null || d.Target.Hp <= 0f)
                {
                    _dots.RemoveAt(i);
                    continue;
                }

                float step = Mathf.Min(dt, d.Remaining);
                DealToTarget(d.Target, d.DamagePerSecond * step);
                d.Remaining -= dt;
                if (d.Remaining <= 0f) _dots.RemoveAt(i);
                else _dots[i] = d;
            }
        }

        private static void DealToTarget(IDamageable target, float damage)
        {
            // dano no boss passa pelo funil do BossManager (fases/morte); demais alvos direto
            BossManager bm = BossManager.Instance;
            if (bm != null && ReferenceEquals(target, bm.Current)) bm.ApplyDamage(damage);
            else target.TakeDamage(damage);
        }
    }
}
