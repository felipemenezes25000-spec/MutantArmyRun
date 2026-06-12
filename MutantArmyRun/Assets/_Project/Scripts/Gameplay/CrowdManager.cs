using System;
using System.Collections.Generic;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Dono do exército (doc 12 §4.2): dados em SoA (arrays paralelos, índice = unidade)
    /// tickados num ÚNICO loop. Proibições do contrato: 1 MonoBehaviour/Update por unidade,
    /// Rigidbody dinâmico por unidade, NavMeshAgent por unidade, collider por unidade.
    /// O layout já nasce Jobs/Burst-ready: migrar p/ NativeArray + IJobParallelFor é trocar
    /// o contêiner, não o layout (Dispose entraria em OnDestroy nesse passo).
    /// Toda mutação de contagem passa pelo funil único ReconcileTo (total-alvo, §4.3).
    /// </summary>
    public class CrowdManager : MonoBehaviour, IInitializable
    {
        public static CrowdManager Instance { get; private set; }

        public const byte FlagAlive = 0b01;
        public const byte FlagDying = 0b10;
        private const int DefaultSupplyCap = 60;   // fixo no MVP (CANON §15); meta eleva até 300 pós-MVP

        [SerializeField] private ElementChartSO _chart;            // delega ao Domain.ElementChart (Task 13)
        [SerializeField] private UnitConfigSO _defaultUnit;        // Soldado: tipo de spawn quando o portal não dita
        [SerializeField] private float _convergeGain = 4f;
        [SerializeField] private float _separationGain = 2.4f;
        [SerializeField] private float _separationRadius = 0.6f;
        [SerializeField] private float _conversionMeterSeconds = 0.08f;   // metering: 1 conversão a cada ~80 ms
        [SerializeField] private float _dyingSeconds = 0.35f;             // anim de desmonte antes do descarte
        [SerializeField] private int _coinPerSupplyRate = 2;              // chave RC supply_overflow_coin_rate

        // ---- Dados SoA: arrays paralelos, índice = unidade; [0.._count) = ocupadas ----
        private Vector3[] _positions;
        private Vector3[] _velocities;
        private byte[] _typeIds;
        private float[] _hp;
        private byte[] _flags;          // bit0 alive · bit1 dying (anim de desmonte)
        private int[] _slot;            // ledger: unidade i ocupa o slot de formação _slot[i]
        private float[] _dyingTimer;
        private int _count;
        private int _dyingCount;

        private SpatialGridXZ _grid;
        private SupplyLedger _supply;   // Domain: contabilidade pura de Supply
        private ElementType _armyElement = ElementType.None;
        private bool _arenaFormation;
        private int _arenaEntryCount;
        private readonly Countdown _invincibility = new Countdown();   // Domain: janela pós-revive

        // catálogo typeId → config (evita depender do UnitManager, que vive na Meta — §2.3)
        private readonly List<UnitConfigSO> _types = new List<UnitConfigSO>();
        private readonly Dictionary<UnitConfigSO, int> _typeIdByConfig = new Dictionary<UnitConfigSO, int>();

        // slots de formação: reuso de slots liberados mantém a formação compacta
        private readonly Stack<int> _freeSlots = new Stack<int>();
        private int _slotCursor;

        // mutações: 3 slots rotativos (CANON §3.3)
        private readonly MutationConfigSO[] _mutationSlots = new MutationConfigSO[3];
        private int _nextMutationSlot;
        private float _mutationDpsMult = 1f;
        private float _mutationHpMult = 1f;

        // conversão de excedente de Supply com metering (CANON §3.2: fanfarra, nunca punição)
        private int _pendingConversions;
        private float _meterAccum;
        private readonly List<(int index, int cost)> _sortBuffer = new List<(int index, int cost)>();
        private static readonly Comparison<(int index, int cost)> CostAscending = (a, b) => a.cost.CompareTo(b.cost);

        public int Count => _count - _dyingCount;            // contagem JOGÁVEL exclui quem está desmontando
        public int SupplyCap => _supply != null ? _supply.Cap : DefaultSupplyCap;
        public int SupplyUsed => _supply != null ? _supply.Used : 0;
        public Vector3 Centroid { get; private set; }        // consumido pela CameraRig (doc 12 §4.12)
        public float MutationSizeMult { get; private set; } = 1f;
        public ElementType ArmyElement => _armyElement;
        public SpatialGridXZ Grid => _grid;                  // reusada pelo CombatSystem (doc 12 §4.4)

        public void Init()   // chamado pelo bootstrap da cena Game (doc 12 §3.3)
        {
            Instance = this;
            int max = 300 + 32;   // teto canônico de Supply + folga de transição (doc 12 §4.2)
            _positions = new Vector3[max];
            _velocities = new Vector3[max];
            _typeIds = new byte[max];
            _hp = new float[max];
            _flags = new byte[max];
            _slot = new int[max];
            _dyingTimer = new float[max];
            _grid = new SpatialGridXZ(cellSize: 0.9f, capacity: max);
            _supply = new SupplyLedger(DefaultSupplyCap);
            Centroid = CrowdAnchor.Position;

            // Hook do revive (doc 12 §4.1): += porque o hook é compartilhado com o
            // SaveSystem (MarkReviveUsed); -= antes evita assinatura dupla em re-Init.
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReviveCrowd -= Revive;
                GameManager.Instance.ReviveCrowd += Revive;
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.ReviveCrowd -= Revive;
        }

        // ------------------------------------------------------------------
        // FUNIL ÚNICO de mutação de contagem: todo portal entrega um TOTAL-ALVO
        // (doc 12 §4.3) e o manager reconcilia atual→alvo aqui — spawn/despawn da
        // diferença + Supply check no mesmo funil.
        // ------------------------------------------------------------------
        public void ReconcileTo(int targetCount, UnitConfigSO spawnType)
        {
            if (_positions == null) return;
            int capacityLeft = _positions.Length - _count;
            targetCount = Mathf.Clamp(targetCount, 1, Count + capacityLeft);

            int delta = targetCount - Count;
            if (delta > 0) SpawnUnits(spawnType, delta);
            else if (delta < 0) RemoveUnits(-delta);

            EnforceSupplyCap();
            GameEvents.RaiseCrowdChanged(Count, SupplyUsed);
        }

        // CANON §3.2: excedente vira moedas COM FANFARRA. O plano vem do Domain.SupplyLedger;
        // a execução usa METERING (1 unidade a cada ~80 ms) — espetáculo sequencial, nunca frame-spike.
        private void EnforceSupplyCap()
        {
            if (_supply == null) return;
            if (_pendingConversions > 0) return;          // meter drenando: re-checado quando esvaziar
            if (_supply.Used <= _supply.Cap) return;

            _sortBuffer.Clear();
            for (int i = 0; i < _count; i++)
            {
                if ((_flags[i] & FlagDying) != 0) continue;
                _sortBuffer.Add((i, GetSupplyCost(_typeIds[i])));
            }
            _sortBuffer.Sort(CostAscending);              // mais baratas primeiro (CANON §3.2)

            OverflowPlan plan = _supply.EnforceCap(_sortBuffer, _coinPerSupplyRate);
            if (plan.RemoveIndices == null || plan.RemoveIndices.Length == 0) return;

            _pendingConversions = plan.RemoveIndices.Length;
            _meterAccum = 0f;
            // moedas do overflow creditam NA HORA (exceção do RunWallet, doc 12 §4.6):
            // a Meta ouve este evento e credita; aqui só o espetáculo
            GameEvents.RaiseSupplyOverflow(new SupplyOverflow(plan.RemoveIndices.Length, plan.CoinsGranted));
        }

        public void ApplyMutation(MutationConfigSO m)     // 4ª mutação substitui a mais antiga (CANON §3.3)
        {
            if (m == null) return;
            _mutationSlots[_nextMutationSlot] = m;
            _nextMutationSlot = (_nextMutationSlot + 1) % _mutationSlots.Length;
            RecomputeMutationMultipliers();
            GameEvents.RaiseMutationGained(m);
        }

        // Morte: estado "dying" + descarte em lote — NUNCA Destroy. Supply é liberado aqui;
        // VFX de desmonte e analytics assinam OnUnitDied, sem acoplamento (doc 12 §4.2).
        public void KillUnit(int i)
        {
            if (i < 0 || i >= _count || (_flags[i] & FlagDying) != 0) return;
            _flags[i] |= FlagDying;
            _dyingTimer[i] = _dyingSeconds;
            _dyingCount++;
            _supply.Remove(GetSupplyCost(_typeIds[i]));
            GameEvents.RaiseUnitDied(new UnitDeath(_typeIds[i], _positions[i]));
            GameEvents.RaiseCrowdChanged(Count, SupplyUsed);
            if (Count == 0) NotifyArmyWiped();
        }

        public void ConvertClass(UnitConfigSO target, float fraction)   // portal de classe (doc 12 §4.3)
        {
            if (target == null || Count == 0) return;
            if (fraction <= 0f || fraction > 1f) fraction = 1f;          // "Virar Arqueiro": conversão integral
            int toConvert = Mathf.Clamp(Mathf.RoundToInt(Count * fraction), 1, Count);
            byte newId = RegisterType(target);

            for (int i = 0; i < _count && toConvert > 0; i++)
            {
                if ((_flags[i] & FlagDying) != 0) continue;
                if (_typeIds[i] == newId) continue;
                _supply.Remove(GetSupplyCost(_typeIds[i]));
                _typeIds[i] = newId;
                _supply.Add(target.supplyCost);
                _hp[i] = target.baseHp * _mutationHpMult;
                toConvert--;
            }

            EnforceSupplyCap();   // Supply re-checado no funil (doc 12 §4.2)
            GameEvents.RaiseCrowdChanged(Count, SupplyUsed);
        }

        public void SetElement(ElementType e)   // portal de elemento: aplica ao exército inteiro
        {
            _armyElement = e;
        }

        /// <summary>Soma de DPS × chart elemental × mutações — consumida pelo CombatSystem (doc 12 §4.4).</summary>
        public float GetTotalDps(ElementType vsElement)
        {
            float total = 0f;
            for (int i = 0; i < _count; i++)
            {
                if ((_flags[i] & FlagDying) != 0) continue;
                UnitConfigSO cfg = _types[_typeIds[i]];
                ElementType atk = _armyElement != ElementType.None ? _armyElement : cfg.element;
                float mult = _chart != null ? _chart.GetMultiplier(atk, vsElement) : 1f;
                total += cfg.baseDps * mult;
            }
            return total * _mutationDpsMult;
        }

        /// <summary>
        /// DPS agregado contra o BOSS: a fraqueza/imunidade anunciada no Boss Scout tem efeito
        /// MECÂNICO no dano (CANON §3.1/§3.4 — a promessa do cartão nunca é falsa):
        /// atk ∈ weaknesses → 1.5x · atk ∈ immunities → 0x · senão chart(atk, boss.element).
        /// As regras de CORPO do chart (CANON §4, ex.: Veneno 0% vs máquina) multiplicam por cima.
        /// </summary>
        public float GetTotalDps(BossRuntime boss)
        {
            if (boss == null || boss.Config == null) return GetTotalDps(ElementType.None);

            float total = 0f;
            for (int i = 0; i < _count; i++)
            {
                if ((_flags[i] & FlagDying) != 0) continue;
                UnitConfigSO cfg = _types[_typeIds[i]];
                ElementType atk = _armyElement != ElementType.None ? _armyElement : cfg.element;
                total += cfg.baseDps * GetBossMultiplier(atk, boss);
            }
            return total * _mutationDpsMult;
        }

        private float GetBossMultiplier(ElementType atk, BossRuntime boss)
        {
            BossConfigSO cfg = boss.Config;

            float elementMult;
            if (atk != ElementType.None && ContainsElement(cfg.immunities, atk))
                elementMult = 0f;                       // imune: o portal de elemento errado zera o dano
            else if (IsActiveWeakness(boss, atk))
                elementMult = 1.5f;                     // "FRACO CONTRA X": recompensa do plano do Scout
            else
                elementMult = _chart != null ? _chart.GetMultiplier(atk, cfg.element) : 1f;

            float bodyMult = _chart != null ? _chart.GetBodyMultiplier(atk, cfg.bodyType) : 1f;
            return elementMult * bodyMult;
        }

        private static bool IsActiveWeakness(BossRuntime boss, ElementType atk)
        {
            if (atk == ElementType.None) return false;
            // fraqueza rotativa (Alien Supremo, CANON §6): só a ATIVA conta — a mesma exibida no HUD
            if (boss.Config.rotatingWeakness) return atk == boss.ActiveWeakness;
            return ContainsElement(boss.Config.weaknesses, atk);
        }

        private static bool ContainsElement(ElementType[] list, ElementType e)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Length; i++)
                if (list[i] == e) return true;
            return false;
        }

        /// <summary>Especial do boss: dano em área ao redor do centróide, por índice (doc 12 §4.4).</summary>
        public void DamageArea(float area, float damage)
        {
            if (!_invincibility.Done) return;
            Vector3 center = Centroid;
            float sqr = area * area;
            for (int i = _count - 1; i >= 0; i--)
            {
                if ((_flags[i] & FlagDying) != 0) continue;
                Vector3 d = _positions[i] - center;
                d.y = 0f;
                if (d.sqrMagnitude > sqr) continue;
                _hp[i] -= damage;
                if (_hp[i] <= 0f) KillUnit(i);
            }
        }

        /// <summary>Dano agregado contínuo (contato do boss + waves): consome unidades do fundo da formação.</summary>
        public void ApplyAggregateDamage(float damage)
        {
            if (!_invincibility.Done || damage <= 0f) return;
            for (int i = _count - 1; i >= 0 && damage > 0f; i--)
            {
                if ((_flags[i] & FlagDying) != 0) continue;
                if (damage >= _hp[i])
                {
                    damage -= _hp[i];
                    KillUnit(i);
                }
                else
                {
                    _hp[i] -= damage;
                    damage = 0f;
                }
            }
        }

        /// <summary>Handoff sim→cinemática (doc 12 §4.5): congela a separação; easing aos slots continua.</summary>
        public void EnterArenaFormation()
        {
            _arenaFormation = true;
            _arenaEntryCount = Count;
        }

        /// <summary>Revive do rewarded (doc 12 §4.1): restaura metade da entrada na arena + invencibilidade breve.</summary>
        public void Revive()
        {
            int target = Mathf.Max(1, _arenaEntryCount / 2);
            ReconcileTo(target, _defaultUnit);
            _invincibility.Set(2f);
        }

        /// <summary>Soft reset da corrida (doc 12 §4.11): zera o exército sem recarregar cena.</summary>
        public void ResetArmy()
        {
            _count = 0;
            _dyingCount = 0;
            _supply = new SupplyLedger(DefaultSupplyCap);
            _armyElement = ElementType.None;
            for (int s = 0; s < _mutationSlots.Length; s++) _mutationSlots[s] = null;
            _nextMutationSlot = 0;
            RecomputeMutationMultipliers();
            _pendingConversions = 0;
            _meterAccum = 0f;
            _freeSlots.Clear();
            _slotCursor = 0;
            _arenaFormation = false;
            _arenaEntryCount = 0;
            _invincibility.Set(0f);
            Centroid = CrowdAnchor.Position;
            GameEvents.RaiseCrowdChanged(0, 0);
        }

        public int GetSupplyCost(byte typeId)
        {
            return typeId < _types.Count && _types[typeId] != null ? _types[typeId].supplyCost : 1;
        }

        public UnitConfigSO GetTypeConfig(byte typeId)
        {
            return typeId < _types.Count ? _types[typeId] : null;
        }

        // ------------------------------------------------------------------
        // O ÚNICO loop da multidão no jogo (doc 12 §4.2)
        // ------------------------------------------------------------------
        private void Update()
        {
            if (_grid == null) return;   // Init() ainda não rodou (ordem do bootstrap)
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _invincibility.Tick(dt);
            TickConversionMeter(dt);
            TickDying(dt);

            _grid.Rebuild(_positions, _count);
            Vector3 anchor = CrowdAnchor.Position;
            Vector3 sum = Vector3.zero;
            float lerp = 1f - Mathf.Exp(-12f * dt);   // damping exponencial: framerate-independente

            for (int i = 0; i < _count; i++)
            {
                if ((_flags[i] & FlagDying) == 0)
                {
                    Float2 slotOffset = FormationMath.GetSlotOffset(_slot[i]);   // Domain: espiral de Vogel
                    Vector3 target = anchor + new Vector3(slotOffset.x, 0f, slotOffset.y);
                    Vector3 steer = (target - _positions[i]) * _convergeGain;    // converge ao slot (auto-curativa)
                    if (!_arenaFormation) steer += Separation(i);                // SÓ separação local
                    _velocities[i] = Vector3.Lerp(_velocities[i], steer, lerp);
                    _positions[i] += _velocities[i] * dt;
                }
                sum += _positions[i];
            }

            if (_count > 0) Centroid = sum / _count;   // cache: mantém último valor se zerar
            // velocidades (rotação da view) + timers de dying (anim de queda) vão juntos:
            // o caminho pooled do CrowdRenderer precisa deles; o instanced ignora
            CrowdRenderer.Submit(_positions, _velocities, _typeIds, _flags, _dyingTimer, _dyingSeconds, _count);
        }

        // Separação com falloff linear Clamp01(1 − d/raio); vizinhos via grid uniforme em XZ.
        // Guards anti-NaN: d mínimo e 0 vizinhos (doc 12 §4.2).
        private Vector3 Separation(int i)
        {
            Vector3 force = Vector3.zero;
            int n = 0;
            foreach (int j in _grid.Neighbors(i))
            {
                Vector3 away = _positions[i] - _positions[j];
                away.y = 0f;
                float d = away.magnitude;
                if (d < 1e-4f)
                {
                    away = JitterFor(i);   // sobrepostas: desempate estável por índice
                    d = 1e-4f;
                }
                force += (away / d) * Mathf.Clamp01(1f - d / _separationRadius);
                n++;
            }
            return n == 0 ? Vector3.zero : force * (_separationGain / n);
        }

        private static Vector3 JitterFor(int i)
        {
            float a = (i * 2.39996f) % (2f * Mathf.PI);
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1e-4f;
        }

        private void TickConversionMeter(float dt)
        {
            if (_pendingConversions <= 0) return;
            _meterAccum += dt;
            while (_meterAccum >= _conversionMeterSeconds && _pendingConversions > 0)
            {
                _meterAccum -= _conversionMeterSeconds;
                _pendingConversions--;
                ConvertOneCheapest();
                if (_pendingConversions == 0)
                {
                    _meterAccum = 0f;
                    EnforceSupplyCap();   // re-checa: estouro novo pode ter ocorrido durante o metering
                }
            }
        }

        private void ConvertOneCheapest()
        {
            if (Count <= 1)   // piso de 1 unidade: nunca zera o exército (CANON §3.2)
            {
                _pendingConversions = 0;
                return;
            }

            int best = -1;
            int bestCost = int.MaxValue;
            for (int i = 0; i < _count; i++)
            {
                if ((_flags[i] & FlagDying) != 0) continue;
                int cost = GetSupplyCost(_typeIds[i]);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = i;
                }
            }
            if (best < 0)
            {
                _pendingConversions = 0;
                return;
            }

            Vector3 pos = _positions[best];
            _supply.Remove(bestCost);
            RemoveAt(best);
            if (VFXManager.Instance != null) VFXManager.Instance.PlayCoinFanfare(pos);   // moeda voadora
            GameEvents.RaiseCrowdChanged(Count, SupplyUsed);
        }

        private void TickDying(float dt)
        {
            for (int i = _count - 1; i >= 0; i--)
            {
                if ((_flags[i] & FlagDying) == 0) continue;
                _dyingTimer[i] -= dt;
                if (_dyingTimer[i] <= 0f) RemoveAt(i);   // Supply já foi liberado no KillUnit
            }
        }

        private void SpawnUnits(UnitConfigSO type, int n)
        {
            if (type == null) type = _defaultUnit;
            if (type == null)
            {
                Debug.LogError("CrowdManager: spawn sem UnitConfigSO e sem _defaultUnit configurado.");
                return;
            }

            byte typeId = RegisterType(type);
            Vector3 anchor = CrowdAnchor.Position;
            float scatterRadius = 0.45f * Mathf.Sqrt(Count + n + 1);

            for (int k = 0; k < n; k++)
            {
                if (_count >= _positions.Length) break;
                int i = _count++;
                // scatter é só cosmético (converge ao slot em ~0,5 s); RNG de pista é System.Random no LevelManager
                Vector2 ring = UnityEngine.Random.insideUnitCircle * scatterRadius;
                _positions[i] = anchor + new Vector3(ring.x, 0f, ring.y);
                _velocities[i] = Vector3.zero;
                _typeIds[i] = typeId;
                _hp[i] = type.baseHp * _mutationHpMult;
                _flags[i] = FlagAlive;
                _dyingTimer[i] = 0f;
                _slot[i] = _freeSlots.Count > 0 ? _freeSlots.Pop() : _slotCursor++;
                _supply.Add(type.supplyCost);
            }
        }

        // Remoção em LOTE, de trás pra frente (swap-back), com 1 VFX agregado (doc 12 §6.4).
        private void RemoveUnits(int n)
        {
            int removed = 0;
            Vector3 vfxPos = Centroid;
            for (int i = _count - 1; i >= 0 && removed < n; i--)
            {
                if ((_flags[i] & FlagDying) != 0) continue;
                _supply.Remove(GetSupplyCost(_typeIds[i]));
                RemoveAt(i);
                removed++;
            }
            if (removed > 0 && VFXManager.Instance != null)
                VFXManager.Instance.PlayCrowdDespawnBurst(vfxPos);
        }

        private void RemoveAt(int i)
        {
            if ((_flags[i] & FlagDying) != 0) _dyingCount--;
            _freeSlots.Push(_slot[i]);

            int last = _count - 1;
            _positions[i] = _positions[last];
            _velocities[i] = _velocities[last];
            _typeIds[i] = _typeIds[last];
            _hp[i] = _hp[last];
            _flags[i] = _flags[last];
            _slot[i] = _slot[last];
            _dyingTimer[i] = _dyingTimer[last];
            _count = last;
        }

        private byte RegisterType(UnitConfigSO config)
        {
            if (_typeIdByConfig.TryGetValue(config, out int existing)) return (byte)existing;

            int id = _types.Count;
            if (id > byte.MaxValue)
            {
                Debug.LogError("CrowdManager: catálogo de tipos estourou 256 entradas.");
                return 0;
            }
            _types.Add(config);
            _typeIdByConfig[config] = id;
            if (CrowdRenderer.Instance != null) CrowdRenderer.Instance.RegisterType(id, config);
            return (byte)id;
        }

        private void RecomputeMutationMultipliers()
        {
            _mutationDpsMult = 1f;
            _mutationHpMult = 1f;
            MutationSizeMult = 1f;
            for (int s = 0; s < _mutationSlots.Length; s++)
            {
                MutationConfigSO m = _mutationSlots[s];
                if (m == null) continue;
                _mutationDpsMult *= m.dpsMult;
                _mutationHpMult *= m.hpMult;
                MutationSizeMult *= m.sizeMult;
            }
        }

        private void NotifyArmyWiped()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            if (gm.State == GameState.BossFight) gm.OfferRevive();              // revive 1×/fase (doc 12 §4.1)
            else if (gm.State == GameState.Running) gm.ChangeState(GameState.Defeat);
        }
    }
}
