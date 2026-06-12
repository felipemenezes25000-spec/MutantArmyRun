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
        private int _fallenThisFight;   // cadáveres acumulados na arena → alvos do Necromante (Levantar)
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
        private float _mutationSpeedMult = 1f;
        private bool _mutationGrantsFlight;
        private ElementType _mutationElement = ElementType.None;   // ex.: laser adiciona Raio (MutationConfigSO.addsElement)

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
        public float MutationSpeedMult => _mutationSpeedMult;   // CrowdAnchor multiplica a velocidade de corrida
        public bool HasFlight => _mutationGrantsFlight;        // asas: ignora obstáculos de chão (CANON §3.3/§5)
        public ElementType ArmyElement => _armyElement;
        /// <summary>Elemento EFETIVO do dano agregado: portal de elemento tem prioridade; senão a mutação de elemento.</summary>
        public ElementType EffectiveElement => _armyElement != ElementType.None ? _armyElement : _mutationElement;
        /// <summary>Tropa de spawn padrão (Soldado): alvo do revive do Necromante quando o tipo não é ditado.</summary>
        public UnitConfigSO DefaultUnit => _defaultUnit;
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
            _fallenThisFight++;   // alimenta o Necromante (Levantar revive parte dos caídos)
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

        /// <summary>Soma de DPS × chart elemental × mutações × habilidades — consumida pelo CombatSystem (doc 12 §4.4).</summary>
        public float GetTotalDps(ElementType vsElement)
        {
            float total = 0f;
            for (int i = 0; i < _count; i++)
            {
                if ((_flags[i] & FlagDying) != 0) continue;
                UnitConfigSO cfg = _types[_typeIds[i]];
                ElementType atk = ResolveAtkElement(cfg);
                float mult = _chart != null ? _chart.GetMultiplier(atk, vsElement) : 1f;
                total += UnitOffense(cfg) * mult;
            }
            return total * _mutationDpsMult;
        }

        // Elemento de ataque efetivo da unidade: portal de elemento > mutação de elemento > elemento nativo da tropa.
        private ElementType ResolveAtkElement(UnitConfigSO cfg)
        {
            if (_armyElement != ElementType.None) return _armyElement;
            if (_mutationElement != ElementType.None) return _mutationElement;
            return cfg.element;
        }

        // DPS ofensivo agregado da unidade (doc 12 §4.4): baseDps + a parcela contínua das
        // habilidades de DANO (Nova Arcana, Sopro, Cone Ígneo, Arsenal). Habilidades de SUPORTE
        // (cura/revive/torreta) não somam DPS aqui — rodam no CombatSystem por tick.
        private static float UnitOffense(UnitConfigSO cfg)
        {
            return cfg.baseDps + CombatAbilities.OffenseBonusDps(cfg.specialAbilityId, cfg.baseDps);
        }

        // ---- Agregação de habilidades por tipo VIVO (doc 12 §4.4): o CombatSystem consome ----

        /// <summary>Total de unidades VIVAS cujo tipo tem a habilidade especial dada (doc 03 §3.2).</summary>
        public int CountAbility(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return 0;
            int n = 0;
            for (int i = 0; i < _count; i++)
            {
                if ((_flags[i] & FlagDying) != 0) continue;
                UnitConfigSO cfg = _types[_typeIds[i]];
                if (cfg != null && cfg.specialAbilityId == abilityId) n++;
            }
            return n;
        }

        /// <summary>
        /// Soma de HP/s de cura de TODAS as unidades vivas com cura (doc 03 §3.2). Resolve o número fino
        /// por unitId via <paramref name="healOf"/> (RC: Médico 8, Anjo 12); fallback de fábrica se nulo.
        /// </summary>
        public float TotalHealPerSecond(System.Func<UnitConfigSO, float> healOf)
        {
            float total = 0f;
            for (int i = 0; i < _count; i++)
            {
                if ((_flags[i] & FlagDying) != 0) continue;
                UnitConfigSO cfg = _types[_typeIds[i]];
                if (cfg == null || cfg.specialAbilityId != CombatAbilities.HealAllies) continue;
                total += healOf != null ? healOf(cfg) : CombatAbilities.HealPerSecond(cfg.specialAbilityId);
            }
            return total;
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
                ElementType atk = ResolveAtkElement(cfg);
                total += UnitOffense(cfg) * GetBossMultiplier(atk, boss);
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
            _fallenThisFight = 0;   // a contagem de caídos para o Necromante começa na arena
        }

        // ------------------------------------------------------------------
        // Habilidades AGREGADAS de SUPORTE (doc 12 §4.4) — chamadas pelo CombatSystem por tick.
        // Aplicam-se ao exército inteiro como dado, nunca por unidade individual.
        // ------------------------------------------------------------------

        /// <summary>
        /// Cura agregada (Médico/Anjo, doc 03 §3.2): distribui <paramref name="totalHeal"/> de HP
        /// começando pelas unidades MAIS FERIDAS (priorização "Triagem"), sem ultrapassar o HP máximo.
        /// </summary>
        public void HealArmy(float totalHeal)
        {
            if (totalHeal <= 0f || _count == 0) return;

            // 2 passadas baratas (sem sort/alloc): 1ª cura quem está abaixo de 50% do máximo,
            // 2ª distribui o resto a quem ainda falta — converge para "mais ferido primeiro".
            for (int pass = 0; pass < 2 && totalHeal > 0f; pass++)
            {
                float threshold = pass == 0 ? 0.5f : 1f;
                for (int i = 0; i < _count && totalHeal > 0f; i++)
                {
                    if ((_flags[i] & FlagDying) != 0) continue;
                    float max = MaxHpOf(i);
                    if (_hp[i] >= max * threshold) continue;
                    float missing = max - _hp[i];
                    float give = Mathf.Min(missing, totalHeal);
                    _hp[i] += give;
                    totalHeal -= give;
                }
            }
        }

        /// <summary>
        /// Levantar do Necromante (doc 03 §3.2): revive uma fração dos caídos da luta como tropas
        /// do tipo <paramref name="reviveType"/>, respeitando um teto de Supply total. Retorna quantas reviveu.
        /// </summary>
        public int ReviveFallen(UnitConfigSO reviveType, int maxCount, int supplyBudget)
        {
            if (reviveType == null || maxCount <= 0 || _fallenThisFight <= 0) return 0;

            int byBudget = reviveType.supplyCost > 0 ? supplyBudget / reviveType.supplyCost : maxCount;
            int toRevive = Mathf.Clamp(Mathf.Min(maxCount, byBudget), 0, _fallenThisFight);
            if (toRevive <= 0) return 0;

            int before = Count;
            SpawnUnits(reviveType, toRevive);
            int revived = Count - before;
            _fallenThisFight -= revived;
            if (revived > 0) GameEvents.RaiseCrowdChanged(Count, SupplyUsed);
            return revived;
        }

        /// <summary>HP máximo efetivo do índice (base do tipo × mult de mutação) — referência para cura.</summary>
        private float MaxHpOf(int i)
        {
            UnitConfigSO cfg = _types[_typeIds[i]];
            float baseHp = cfg != null ? cfg.baseHp : 1f;
            return Mathf.Max(1f, baseHp * _mutationHpMult);
        }

        /// <summary>
        /// Sacrifício do Titã (doc 04 §3.5): consome 50% das unidades (mais baratas primeiro,
        /// piso ≥1 além do Titã) e entrega 1 Titã. O cap re-checa no funil — o Titã nunca é convertido.
        /// </summary>
        public void SacrificeForTitan(UnitConfigSO titan)
        {
            if (titan == null || Count == 0) return;

            int toSacrifice = Mathf.Clamp(Count / 2, 0, Mathf.Max(0, Count - 1));
            SacrificeCheapest(toSacrifice);
            SpawnUnits(titan, 1);
            EnforceSupplyCap();   // estourou o cap? converte OUTRAS tropas em moedas — nunca o Titã (overflow ordena por custo, Titã é o mais caro)
            GameEvents.RaiseCrowdChanged(Count, SupplyUsed);
        }

        // Remove as N unidades de MENOR custo de Supply (Sacrifício do Titã, doc 04 §3.5).
        private void SacrificeCheapest(int n)
        {
            for (int k = 0; k < n; k++)
            {
                int best = -1, bestCost = int.MaxValue;
                for (int i = 0; i < _count; i++)
                {
                    if ((_flags[i] & FlagDying) != 0) continue;
                    int cost = GetSupplyCost(_typeIds[i]);
                    if (cost < bestCost) { bestCost = cost; best = i; }
                }
                if (best < 0) return;
                _supply.Remove(bestCost);
                RemoveAt(best);
            }
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
            _fallenThisFight = 0;
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

        // Multiplicadores ACUMULAM multiplicativamente entre mutações diferentes (doc 04 §4):
        // Braços ×1,3 e Cabeças ×1,25 → DPS ×1,625. As flags (voo/elemento) ligam se QUALQUER slot as carrega.
        private void RecomputeMutationMultipliers()
        {
            _mutationDpsMult = 1f;
            _mutationHpMult = 1f;
            _mutationSpeedMult = 1f;
            MutationSizeMult = 1f;
            _mutationGrantsFlight = false;
            _mutationElement = ElementType.None;
            for (int s = 0; s < _mutationSlots.Length; s++)
            {
                MutationConfigSO m = _mutationSlots[s];
                if (m == null) continue;
                _mutationDpsMult *= m.dpsMult;
                _mutationHpMult *= m.hpMult;
                _mutationSpeedMult *= m.speedMult;
                MutationSizeMult *= m.sizeMult;
                if (m.grantsFlight) _mutationGrantsFlight = true;
                if (m.addsElement != ElementType.None) _mutationElement = m.addsElement;   // última mutação de elemento vence
            }
            // _mutationSpeedMult é EXPOSTO via MutationSpeedMult; o CrowdAnchor (dono do speed) o
            // combina com a trilha de meta "Velocidade" — não setamos aqui para não sobrescrever
            // o canal de meta (SetSpeedMultiplier é setter único). Ver aviso de integração.
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
