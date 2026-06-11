using System;
using System.Collections.Generic;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Spawn e luta do boss (doc 12 §4.5). Três decisões do contrato:
    /// 1. Waves da arena são DADOS ordenados por tempo, consumidos por PONTEIRO de próximo
    ///    evento (Domain.WavePointer) — cada evento dispara exatamente 1×, nunca polling.
    /// 2. Telegraph e cooldown são Countdown puros do Domain — testáveis com dt sintético.
    /// 3. Entrada na arena = handoff sim→cinemática (CrowdManager.EnterArenaFormation).
    /// Fases de vida via Domain.CombatMath.BossPhase; morte com slow motion canônico.
    /// </summary>
    public class BossManager : MonoBehaviour, IInitializable
    {
        public static BossManager Instance { get; private set; }

        public BossRuntime Current { get; private set; }

        // limiares canônicos das 3 fases (doc 05 §2.3); Domain.CombatMath.BossPhase usa os mesmos.
        // Público: a barra segmentada do HUD (doc 09 §4.3) lê daqui.
        public static readonly float[] PhaseThresholds = { 0.5f, 0.25f };

        private readonly Countdown _specialCooldown = new Countdown();   // Domain
        private readonly Countdown _telegraph = new Countdown();         // Domain
        private bool _telegraphing;
        private int _nextWaveEvent;   // ponteiro: nunca varre a lista (doc 12 §4.5)
        private ArenaWave[] _domainWaves = Array.Empty<ArenaWave>();

        // Inimigos da arena como AGREGADOS (combate agregado, doc 12 §4.4): grupo = dado,
        // sem GameObject por inimigo. O CombatSystem consome estes agregados por tick.
        private sealed class ArenaEnemyGroup
        {
            public UnitConfigSO Type;
            public int Count;
            public float Hp;
            public Vector3 Position;
        }

        private readonly List<ArenaEnemyGroup> _arenaEnemies = new List<ArenaEnemyGroup>();

        public void Init()   // chamado pelo bootstrap da cena Game (doc 12 §3.3)
        {
            Instance = this;
            if (GameManager.Instance != null)
            {
                // Contrato doc 12 §4.1 (EnterState): entrar em BossFight inicia a luta —
                // Core não enxerga Gameplay, então é o manager que assina StateEntered;
                // -= antes de += para Init repetido não duplicar a inscrição.
                GameManager.Instance.StateEntered -= HandleStateEntered;
                GameManager.Instance.StateEntered += HandleStateEntered;
            }
        }

        private void HandleStateEntered(GameState s)
        {
            if (s != GameState.BossFight) return;
            LevelConfigSO level = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : null;
            if (level != null) BeginFight(level.boss, level.bossHpMultiplier);
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.StateEntered -= HandleStateEntered;
        }

        public void BeginFight(BossConfigSO config)
        {
            BeginFight(config, 1f);
        }

        // O multiplicador de HP do Remote Config (boss_hp_mult_<id>) chega por PARÂMETRO:
        // Gameplay não referencia Services (fronteira de asmdef, doc 12 §2.3).
        public void BeginFight(BossConfigSO config, float hpMultiplier)
        {
            if (config == null) return;
            Current = new BossRuntime(config, hpMultiplier);
            _nextWaveEvent = 0;
            _telegraphing = false;
            _arenaEnemies.Clear();
            BuildDomainWaves(config);

            if (CrowdManager.Instance != null)
                CrowdManager.Instance.EnterArenaFormation();   // handoff sim→cinemática (decisão 3)

            // entrada ≤ 2 s (CANON §6): a luta "começa" depois da animação de entrada
            _specialCooldown.Set(config.specialBaseCooldown + config.entranceSeconds);
        }

        private void BuildDomainWaves(BossConfigSO config)
        {
            ArenaWaveEvent[] src = config.arenaWaves;
            if (src == null || src.Length == 0)
            {
                _domainWaves = Array.Empty<ArenaWave>();
                return;
            }
            if (_domainWaves.Length != src.Length) _domainWaves = new ArenaWave[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                _domainWaves[i] = new ArenaWave
                {
                    time = src[i] != null ? src[i].time : float.MaxValue,
                    enemyTypeId = i,
                    count = src[i] != null ? src[i].count : 0
                };
            }
        }

        private void Update()
        {
            if (Current == null) return;
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.BossFight) return;

            float dt = Time.deltaTime;
            Current.FightTime += dt;
            Current.TickStatus(dt);

            // Waves da arena: lista ORDENADA + ponteiro do Domain (decisão 1) — dt gigante
            // dispara todas em ordem, dt pequeno nunca duplica
            int before = _nextWaveEvent;
            int fired = WavePointer.Consume(Current.FightTime, _domainWaves, ref _nextWaveEvent);
            for (int i = before; i < before + fired; i++)
                SpawnWave(Current.Config.arenaWaves[i]);

            // Especial: cooldown → telegraph (decal + windup) → golpe (decisão 2)
            _specialCooldown.Tick(dt);
            if (_specialCooldown.Done && !_telegraphing)
            {
                _telegraphing = true;
                _telegraph.Set(Current.Config.telegraphSeconds);   // janela de leitura, padrão 1,0 s
                if (VFXManager.Instance != null)
                    VFXManager.Instance.ShowTelegraph(Current.Config.specialAttackArea,
                                                      Current.Config.telegraphSeconds);
            }
            if (_telegraphing)
            {
                _telegraph.Tick(dt);
                if (_telegraph.Done) FireSpecial();
            }

            CheckPhaseAndDeath();   // cobre dano via DoT/efeitos que reduzem Hp direto no runtime
        }

        private void FireSpecial()
        {
            _telegraphing = false;
            if (Current == null) return;
            if (CrowdManager.Instance != null)
                CrowdManager.Instance.DamageArea(Current.Config.specialAttackArea,
                                                 Current.Config.specialAttackDamage);
            _specialCooldown.Set(Current.SpecialCooldown());   // diminui por fase
        }

        public void ApplyDamage(float raw)
        {
            if (Current == null) return;
            Current.Hp -= raw;   // chart elemental já aplicado no DPS agregado (doc 12 §4.4)
            CheckPhaseAndDeath();
        }

        private void CheckPhaseAndDeath()
        {
            if (Current == null) return;

            int phase = CombatMath.BossPhase(Current.Hp, Current.MaxHp);   // Domain: limiares 0.5/0.25
            if (phase != Current.Phase)
            {
                Current.Phase = phase;   // fase nova = especial mais frequente
                if (Current.Config.rotatingWeakness) Current.RotateWeakness();   // Alien Supremo (M8)
                GameEvents.RaiseBossPhaseChanged(new BossPhase(phase, Current.ActiveWeakness));
            }

            if (Current.Hp <= 0f) Die();
        }

        private void Die()
        {
            Current = null;   // guarda contra re-entrada (dano múltiplo no mesmo tick)
            _arenaEnemies.Clear();
            _telegraphing = false;

            // golpe final: timeScale 0,3 por 0,8 s com fixedDeltaTime escalado junto (doc 12 §3.1)
            if (VFXManager.Instance != null) VFXManager.Instance.SlowMotion(0.3f, 0.8f);

            // recompensa do boss é responsabilidade da Meta, que reage a OnLevelFinished
            // (disparado pelo GameManager no ResolveEnd) — fronteira de asmdef (doc 12 §2.3)
            if (GameManager.Instance != null) GameManager.Instance.ChangeState(GameState.Victory);
        }

        private void SpawnWave(ArenaWaveEvent w)
        {
            if (w == null || w.enemyType == null || w.count <= 0) return;
            // grupo agregado posicionado ao redor da arena, alternando os flancos
            int idx = _arenaEnemies.Count;
            Vector3 center = CrowdAnchor.Position + new Vector3(0f, 0f, 10f);
            Vector3 offset = new Vector3((idx & 1) == 0 ? -3f : 3f, 0f, 2f * (idx % 3));
            _arenaEnemies.Add(new ArenaEnemyGroup
            {
                Type = w.enemyType,
                Count = w.count,
                Hp = w.enemyType.baseHp * w.count,
                Position = center + offset
            });
        }

        /// <summary>DPS somado das waves vivas — contagem proporcional ao HP restante do grupo.</summary>
        public float TotalArenaDps
        {
            get
            {
                float dps = 0f;
                for (int i = 0; i < _arenaEnemies.Count; i++)
                {
                    ArenaEnemyGroup g = _arenaEnemies[i];
                    if (g.Type == null || g.Type.baseHp <= 0f) continue;
                    int alive = Mathf.CeilToInt(g.Hp / g.Type.baseHp);
                    dps += g.Type.baseDps * Mathf.Min(alive, g.Count);
                }
                return dps;
            }
        }

        /// <summary>
        /// Waves vivas absorvem o dano do exército ANTES do boss (estão entre os dois).
        /// Retorna o dano que sobrou para o boss.
        /// </summary>
        public float DamageArenaEnemies(float damage)
        {
            for (int i = _arenaEnemies.Count - 1; i >= 0 && damage > 0f; i--)
            {
                ArenaEnemyGroup g = _arenaEnemies[i];
                if (g.Hp > damage)
                {
                    g.Hp -= damage;
                    damage = 0f;
                }
                else
                {
                    damage -= g.Hp;
                    _arenaEnemies.RemoveAt(i);   // fim de onda por esgotamento do agregado
                }
            }
            return damage;
        }

        /// <summary>Encadeamento do Raio (CANON §4): dano nos até N grupos mais próximos da origem.</summary>
        public void DamageNearestArenaEnemies(Vector3 origin, float damage, int maxTargets)
        {
            ArenaEnemyGroup previousHit = null;
            for (int hit = 0; hit < maxTargets; hit++)
            {
                int best = -1;
                float bestSqr = float.MaxValue;
                for (int i = 0; i < _arenaEnemies.Count; i++)
                {
                    ArenaEnemyGroup g = _arenaEnemies[i];
                    if (g == previousHit) continue;
                    float sqr = (g.Position - origin).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = i;
                    }
                }
                if (best < 0) return;

                ArenaEnemyGroup target = _arenaEnemies[best];
                previousHit = target;
                if (target.Hp > damage) target.Hp -= damage;
                else _arenaEnemies.RemoveAt(best);
            }
        }
    }
}
