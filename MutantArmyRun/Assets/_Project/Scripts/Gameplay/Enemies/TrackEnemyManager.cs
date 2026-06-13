using System.Collections;
using System.Collections.Generic;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;
using UnityEngine.Pool;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Inimigos DE PISTA (missão Nota 10): hordas fracas que explodem em cadeia, tanques
    /// que bloqueiam fisicamente, atiradores que punem de longe e curadores que viram alvo
    /// prioritário emergente. Tudo AGREGADO por GRUPO — 1 entidade lógica por EnemySlot do
    /// LevelConfigSO, com no MÁXIMO 3 views pooled por grupo (contrato crowd-combat §2:
    /// proibido MonoBehaviour/collider/Update por inimigo individual; o "count" vive como
    /// HP agregado + escala/quantidade de views).
    ///
    /// Decisão de design: a variação por kind mora num switch único aqui dentro — cada kind
    /// (WeakHorde/Tank/Ranged/Healer) difere em ~5 linhas; classes-estratégia seriam
    /// indireção sem ganho neste tamanho. Se um kind crescer, extrai-se na hora.
    ///
    /// Determinismo: RNG DERIVADO da seed da fase (seed*92821+7, CONTRACT §1.6) só para
    /// lane X, jitter e fase de bob — o RNG PRINCIPAL do LevelManager (gates → obstáculos
    /// → segmentos) NUNCA é tocado, preservando as 100 pistas desenhadas e o endless.
    ///
    /// Dano segue os funis canônicos do CrowdManager (ApplyAggregateDamage/ApplyObstacleHit
    /// respeitam invencibilidade, esquiva e piso de 1 — map-crowd-combat RISKS §11); o
    /// exército morde o grupo com GetTotalDps(elemento do inimigo): a fraqueza elemental
    /// funciona também na corrida, não só na arena do boss.
    ///
    /// Moedas: a morte NÃO credita moeda diretamente — o drop agregado viaja no evento
    /// TrackEnemyKilled (1 Raise por GRUPO) e a Onda 3 (Economy) credita assinando o bus.
    /// </summary>
    public class TrackEnemyManager : MonoBehaviour, IInitializable
    {
        public static TrackEnemyManager Instance { get; private set; }

        [Header("Engajamento (metros, relativos ao líder do exército)")]
        [SerializeField] private float _engageRangeMeters = 8f;     // corpo-a-corpo: exército morde, inimigo morde
        [SerializeField] private float _rangedEngageMeters = 14f;   // Ranged abre fogo ANTES do contato (pressão p/ correr)
        [SerializeField] private float _healRadiusMeters = 12f;     // Healer cura grupos vivos neste raio
        [SerializeField] private float _tankBlockHalfWidth = 1.2f;  // faixa do bloqueio físico do Tank (ApplyObstacleHit)
        [SerializeField] private float _approachStopMeters = 1.5f;  // moveSpeed>0: avança até esta distância do líder

        [Header("Streaming de views (mesma filosofia por distância do LevelManager)")]
        [SerializeField] private float _viewAheadMeters = 60f;
        [SerializeField] private float _viewBehindMeters = 25f;
        [SerializeField] private int _maxVisibleGroups = 12;        // teto de grupos com views simultâneos

        [Header("Pista / juice")]
        [SerializeField] private float _laneHalfWidth = 2.2f;       // mesmo valor do LevelManager/CrowdAnchor
        [SerializeField] private float _chainStepSeconds = 0.06f;   // escalonamento da morte em cadeia (prazer visual)
        [SerializeField] private float _tankBlockShakeDegrees = 0.6f;

        private const int MaxViewsPerGroup = 3;

        private enum GroupState { Alive, Dying, Dead }

        // Grupo agregado: o "inimigo" é UM bloco de HP/DPS — count individual é derivado
        // do HP restante (AliveCount), nunca simulado por unidade (contrato anti-per-unidade).
        private sealed class TrackEnemyGroup
        {
            public EnemyConfigSO config;
            public int initialCount;
            public float hp;                 // HP agregado (maxHp do config × count)
            public float maxHp;
            public float hpPerUnit;
            public Vector3 position;         // posição lógica no CHÃO (y = 0)
            public GroupState state;
            public bool tankBlockApplied;    // bloqueio físico do Tank é one-shot
            public Transform root;           // 1 transform ANIMADO por grupo (bob/giro — nunca por inimigo)
            public Vector3[] viewOffsets;    // offsets locais sorteados no spawn (RNG derivado)
            public float bobPhase;
            public readonly List<GameObject> views = new List<GameObject>(MaxViewsPerGroup);
        }

        private readonly List<TrackEnemyGroup> _groups = new List<TrackEnemyGroup>();
        private int _killedThisRun;
        private bool _waveClearedRaised;

        // pooling POR PREFAB (CONTRACT §1.5) + fallback de primitivo POR KIND (greybox sem arte)
        private readonly Dictionary<GameObject, ObjectPool<GameObject>> _prefabViewPools =
            new Dictionary<GameObject, ObjectPool<GameObject>>();
        private readonly ObjectPool<GameObject>[] _fallbackViewPools = new ObjectPool<GameObject>[4];
        private readonly Dictionary<GameObject, ObjectPool<GameObject>> _viewOwner =
            new Dictionary<GameObject, ObjectPool<GameObject>>();   // instância → pool dono (Release certeiro)
        private ObjectPool<Transform> _rootPool;

        private WaitForSeconds _chainWait;
        private MaterialPropertyBlock _mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>Total de inimigos individuais mortos na corrida atual (alimenta EnemyWaveCleared e missões da Onda 3).</summary>
        public int KilledThisRun => _killedThisRun;

        public void Init()   // chamado pelo bootstrap da cena Game (doc 12 §3.3); idempotente
        {
            Instance = this;
            _chainWait = new WaitForSeconds(Mathf.Max(0.01f, _chainStepSeconds));
            if (_rootPool == null)
            {
                _rootPool = new ObjectPool<Transform>(
                    CreateGroupRoot,
                    t => t.gameObject.SetActive(true),
                    t =>
                    {
                        t.gameObject.SetActive(false);
                        t.SetParent(transform, false);
                    },
                    null,
                    collectionCheck: false, defaultCapacity: 4, maxSize: 16);
            }
            DrainAll();   // Init repetido (re-bootstrap) nunca herda estado de corrida anterior
        }

        /// <summary>
        /// Popula os grupos a partir de level.enemies (EnemySlot[] da Onda 1). Chamado pelo
        /// LevelManager.BeginRun APÓS SpawnObstacles — mas com RNG próprio derivado da seed
        /// (seed*92821+7, CONTRACT §1.6), então a posição na ordem de chamada não desloca o
        /// consumo do RNG principal da fase. Fase sem inimigos = lista vazia, custo zero.
        /// </summary>
        public void SpawnFromLevel(LevelConfigSO level)
        {
            DrainAll();
            if (level == null || level.enemies == null || level.enemies.Length == 0) return;

            var rng = new System.Random(level.seed * 92821 + 7);   // RNG DERIVADO — nunca o da pista
            foreach (EnemySlot slot in level.enemies)
            {
                if (slot == null || slot.enemy == null || slot.count <= 0) continue;

                var g = new TrackEnemyGroup
                {
                    config = slot.enemy,
                    initialCount = slot.count,
                    hpPerUnit = Mathf.Max(0.01f, slot.enemy.maxHp),
                    state = GroupState.Alive,
                    bobPhase = (float)(rng.NextDouble() * Mathf.PI * 2.0)
                };
                g.maxHp = g.hpPerUnit * slot.count;
                g.hp = g.maxHp;

                // lane X sorteada no RNG derivado (jitter leve em Z para a fila não ficar robótica)
                float laneX = (float)(rng.NextDouble() * 2.0 - 1.0) * _laneHalfWidth;
                float jitterZ = (float)(rng.NextDouble() * 2.0 - 1.0) * 1.5f;
                g.position = new Vector3(laneX, 0f, Mathf.Max(8f, slot.trackPosition + jitterZ));

                // offsets das views sorteados JÁ no spawn (mesma corrida = mesmo visual)
                int viewCount = Mathf.Min(MaxViewsPerGroup, slot.count);
                g.viewOffsets = new Vector3[viewCount];
                float y = KindHalfHeight(slot.enemy.kind);
                for (int v = 0; v < viewCount; v++)
                {
                    g.viewOffsets[v] = v == 0
                        ? new Vector3(0f, y, 0f)
                        : new Vector3((float)(rng.NextDouble() * 2.0 - 1.0) * 0.6f, y,
                                      (float)(rng.NextDouble() * 2.0 - 1.0) * 0.6f);
                }

                _groups.Add(g);
            }
        }

        /// <summary>
        /// Release TOTAL (soft reset, CONTRACT §1.5): chamado pelo LevelManager.DrainAll e
        /// no início de SpawnFromLevel. Corrotinas de morte em cadeia NÃO sobrevivem ao
        /// reset — as views pendentes delas ainda estão na lista do grupo e são devolvidas aqui.
        /// </summary>
        public void DrainAll()
        {
            StopAllCoroutines();
            for (int i = 0; i < _groups.Count; i++)
            {
                TrackEnemyGroup g = _groups[i];
                ReleaseViews(g);
                ReleaseRoot(g);
            }
            _groups.Clear();
            _killedThisRun = 0;
            _waveClearedRaised = false;
        }

        private void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Running || _groups.Count == 0) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 leader = CrowdAnchor.Position;
            CrowdManager crowd = CrowdManager.Instance;

            // teto de grupos visíveis: contagem barata por frame (lista é pequena, ≤ slots da fase)
            int visible = 0;
            for (int i = 0; i < _groups.Count; i++)
                if (_groups[i].views.Count > 0) visible++;

            for (int i = 0; i < _groups.Count; i++)
            {
                TrackEnemyGroup g = _groups[i];
                if (g.state == GroupState.Dead) continue;
                if (g.state == GroupState.Dying)
                {
                    AnimateGroup(g);   // a cadeia de release cuida das views; o root ainda balança
                    continue;
                }

                float dz = g.position.z - leader.z;

                // streaming de views POR DISTÂNCIA (mesma filosofia dos segmentos/obstáculos):
                // grupo longe demais à frente ou já ultrapassado não gasta view nem pool
                if (g.views.Count == 0)
                {
                    if (dz <= _viewAheadMeters && dz >= -_viewBehindMeters && visible < _maxVisibleGroups)
                    {
                        AttachViews(g);
                        visible++;
                    }
                }
                else if (dz < -_viewBehindMeters)
                {
                    ReleaseViews(g);
                    ReleaseRoot(g);
                    visible--;
                }

                AnimateGroup(g);
                if (crowd == null) continue;   // greybox sem exército: inimigos só decoram

                float planar = PlanarDistance(leader, g.position);
                TrackEnemyKind kind = g.config.kind;

                // inimigo móvel avança até o exército quando o "vê" (moveSpeed 0 = estacionário)
                if (g.config.moveSpeed > 0f && planar <= _rangedEngageMeters && planar > _approachStopMeters)
                {
                    Vector3 to = leader - g.position;
                    to.y = 0f;
                    float dist = to.magnitude;
                    if (dist > 0.001f)
                    {
                        g.position += to * (g.config.moveSpeed * dt / dist);
                        g.position.x = Mathf.Clamp(g.position.x, -_laneHalfWidth, _laneHalfWidth);
                        planar = PlanarDistance(leader, g.position);
                    }
                }

                int alive = AliveCount(g);

                // ---- comportamento por KIND (switch único — ver decisão no cabeçalho) ----
                switch (kind)
                {
                    case TrackEnemyKind.Healer:
                        // Healer NÃO ataca: cura grupos vivos próximos — com HP baixo (conteúdo),
                        // matar ele primeiro vira a prioridade EMERGENTE do jogador.
                        if (g.config.healPerSecond > 0f) HealNearbyGroups(g, dt);
                        break;

                    case TrackEnemyKind.Ranged:
                        if (planar <= _rangedEngageMeters)
                            crowd.ApplyAggregateDamage(g.config.dps * alive * dt);
                        break;

                    case TrackEnemyKind.Tank:
                        if (planar <= _engageRangeMeters)
                            crowd.ApplyAggregateDamage(g.config.dps * alive * dt);
                        // bloqueio FÍSICO one-shot: o líder cruza a Z do tanque vivo e a parede
                        // de carne cobra pedágio pelo funil canônico de obstáculo
                        if (!g.tankBlockApplied && leader.z >= g.position.z)
                        {
                            g.tankBlockApplied = true;
                            int removed = crowd.ApplyObstacleHit(g.position.x, _tankBlockHalfWidth);
                            if (removed > 0)
                            {
                                Vector3 fxPos = new Vector3(g.position.x, 0.5f, g.position.z);
                                if (VFXManager.Instance != null)
                                    VFXManager.Instance.PlayGateBurst(fxPos, KindColor(kind));
                                // áudio via bus de juice (Gameplay não enxerga Services, doc 12 §2.3)
                                JuiceEvents.RaiseObstacleHit(fxPos);
                                Tween.ShakeCamera(_tankBlockShakeDegrees, 0.12f);
                            }
                        }
                        break;

                    default:   // WeakHorde
                        if (planar <= _engageRangeMeters)
                            crowd.ApplyAggregateDamage(g.config.dps * alive * dt);
                        break;
                }

                // ATROPELO (missão F4 — "horda fraca morre rápido, dá prazer visual"): quando o
                // exército ALCANÇA fisicamente uma horda fraca, ela morre na hora, em cadeia —
                // independente do DPS. Exércitos pequenos do tutorial atropelam de verdade; o
                // pedágio da horda é o ApplyAggregateDamage cobrado acima até o contato. Os
                // demais kinds (Tank/Ranged/Healer) continuam caindo só por DPS.
                if (kind == TrackEnemyKind.WeakHorde
                    && leader.z >= g.position.z - 0.5f && planar <= _engageRangeMeters)
                {
                    KillGroup(g, leader);
                    continue;
                }

                // o exército morde o grupo no engajamento corpo-a-corpo: DPS total contra o
                // ELEMENTO do inimigo (chart elemental vale na pista, não só no boss)
                if (planar <= _engageRangeMeters)
                {
                    g.hp -= crowd.GetTotalDps(g.config.element) * dt;
                    if (g.hp <= 0f)
                    {
                        KillGroup(g, leader);
                        continue;
                    }
                    RefreshViewsForDamage(g);
                }
            }
        }

        // ------------------------------------------------------------------ morte e eventos

        private void KillGroup(TrackEnemyGroup g, Vector3 leader)
        {
            g.hp = 0f;
            g.state = GroupState.Dying;
            _killedThisRun += g.initialCount;

            Vector3 pos = g.position + Vector3.up * 0.5f;
            // 1 Raise POR GRUPO com o drop AGREGADO — a Onda 3 (Economy) credita assinando o bus
            int coins = g.initialCount * Mathf.Max(0, g.config.rewardCoins);
            GameEvents.RaiseTrackEnemyKilled(new TrackEnemyKilled(g.config.kind, pos, coins));

            StartCoroutine(ChainDeath(g));

            // wave limpa: nenhum grupo VIVO restante à frente do líder (one-shot por corrida)
            if (!_waveClearedRaised && !AnyAliveAhead(leader.z))
            {
                _waveClearedRaised = true;
                GameEvents.RaiseEnemyWaveCleared(new EnemyWaveCleared(_killedThisRun, pos));
            }
        }

        private bool AnyAliveAhead(float leaderZ)
        {
            for (int i = 0; i < _groups.Count; i++)
            {
                TrackEnemyGroup g = _groups[i];
                if (g.state == GroupState.Alive && g.position.z > leaderZ) return true;
            }
            return false;
        }

        // Morte em CADEIA (prazer visual da missão): as views estouram escalonadas ~0.06s.
        // Soft reset seguro: DrainAll para a corrotina E devolve o que sobrou na lista —
        // cada view sai da lista ANTES do Release, então nunca há release duplo.
        private IEnumerator ChainDeath(TrackEnemyGroup g)
        {
            Color tint = KindColor(g.config.kind);
            if (g.views.Count == 0)
            {
                // grupo morto fora da janela visível (teto de views): 1 burst no centro e pronto
                if (VFXManager.Instance != null)
                    VFXManager.Instance.PlayGateBurst(g.position + Vector3.up * 0.5f, tint);
            }
            while (g.views.Count > 0)
            {
                int last = g.views.Count - 1;
                GameObject v = g.views[last];
                g.views.RemoveAt(last);
                if (v != null)
                {
                    if (VFXManager.Instance != null)
                        VFXManager.Instance.PlayGateBurst(v.transform.position, tint);
                    ReleaseView(v);
                }
                if (g.views.Count > 0) yield return _chainWait;
            }
            ReleaseRoot(g);
            g.state = GroupState.Dead;
        }

        // ------------------------------------------------------------------ healer

        private void HealNearbyGroups(TrackEnemyGroup healer, float dt)
        {
            float heal = healer.config.healPerSecond * dt;
            if (heal <= 0f) return;
            for (int i = 0; i < _groups.Count; i++)
            {
                TrackEnemyGroup g = _groups[i];
                if (g == healer || g.state != GroupState.Alive) continue;
                if (PlanarDistance(healer.position, g.position) > _healRadiusMeters) continue;
                g.hp = Mathf.Min(g.maxHp, g.hp + heal);
            }
        }

        // ------------------------------------------------------------------ views (pooled)

        private void AttachViews(TrackEnemyGroup g)
        {
            if (g.root == null)
            {
                g.root = _rootPool != null ? _rootPool.Get() : null;
                if (g.root == null) return;   // Init não rodou (bootstrap fora de ordem): degrada sem view
                g.root.position = g.position;
            }
            int count = g.viewOffsets != null ? g.viewOffsets.Length : 0;
            for (int i = 0; i < count; i++)
            {
                GameObject v = GetView(g.config);
                if (v == null) continue;
                v.transform.SetParent(g.root, false);
                v.transform.localPosition = g.viewOffsets[i];
                v.transform.localRotation = Quaternion.identity;
                g.views.Add(v);
            }
            ApplyViewScales(g, AliveCount(g));
        }

        // Conforme o grupo apanha, as views somem uma a uma e as restantes encolhem — o
        // "count" é representado por escala + nº de views, nunca por entidades individuais.
        private void RefreshViewsForDamage(TrackEnemyGroup g)
        {
            if (g.views.Count == 0) return;
            int alive = AliveCount(g);
            int target = Mathf.Clamp(
                Mathf.CeilToInt(MaxViewsPerGroup * (float)alive / Mathf.Max(1, g.initialCount)),
                1, g.views.Count);
            while (g.views.Count > target)
            {
                int last = g.views.Count - 1;
                GameObject v = g.views[last];
                g.views.RemoveAt(last);
                if (v != null)
                {
                    if (VFXManager.Instance != null) VFXManager.Instance.PlayPopBurst(v.transform.position);
                    ReleaseView(v);
                }
            }
            ApplyViewScales(g, alive);
        }

        private void ApplyViewScales(TrackEnemyGroup g, int alive)
        {
            float fraction = g.initialCount > 0 ? Mathf.Clamp01((float)alive / g.initialCount) : 1f;
            float s = KindScale(g.config.kind) * (0.85f + 0.30f * fraction);
            for (int i = 0; i < g.views.Count; i++)
            {
                GameObject v = g.views[i];
                if (v != null) v.transform.localScale = new Vector3(s, s, s);
            }
        }

        // Bob/giro BARATO por código: 1 transform por GRUPO (nunca por inimigo) — sem Animator.
        private void AnimateGroup(TrackEnemyGroup g)
        {
            if (g.root == null) return;
            float t = Time.time + g.bobPhase;
            float bob;
            switch (g.config.kind)
            {
                case TrackEnemyKind.Tank:
                    bob = Mathf.Sin(t * 2f) * 0.04f;                       // respiração pesada
                    break;
                case TrackEnemyKind.Ranged:
                    bob = Mathf.Sin(t * 4f) * 0.06f;
                    g.root.localRotation = Quaternion.Euler(0f, t * 25f, 0f);   // giro de vigia
                    break;
                case TrackEnemyKind.Healer:
                    bob = Mathf.Sin(t * 3f) * 0.10f;                       // flutua — lê como "suporte"
                    g.root.localRotation = Quaternion.Euler(0f, t * -20f, 0f);
                    break;
                default:   // WeakHorde
                    bob = Mathf.Abs(Mathf.Sin(t * 6f)) * 0.10f;            // pulinhos nervosos de horda
                    break;
            }
            g.root.position = g.position + Vector3.up * bob;
        }

        private GameObject GetView(EnemyConfigSO cfg)
        {
            ObjectPool<GameObject> pool = cfg.prefab != null
                ? GetPrefabViewPool(cfg.prefab)
                : GetFallbackViewPool(cfg.kind);
            GameObject v = pool.Get();
            _viewOwner[v] = pool;   // mapeamento estável: instâncias pooled persistem
            return v;
        }

        private void ReleaseView(GameObject v)
        {
            if (v == null) return;
            v.transform.SetParent(transform, false);   // sai do root ANTES do root voltar ao pool
            if (_viewOwner.TryGetValue(v, out ObjectPool<GameObject> pool) && pool != null)
                pool.Release(v);
            else
                v.SetActive(false);   // origem desconhecida (não deveria ocorrer): degrada sem Destroy
        }

        private void ReleaseViews(TrackEnemyGroup g)
        {
            for (int i = g.views.Count - 1; i >= 0; i--) ReleaseView(g.views[i]);
            g.views.Clear();
        }

        private void ReleaseRoot(TrackEnemyGroup g)
        {
            if (g.root == null) return;
            if (_rootPool != null) _rootPool.Release(g.root);
            else g.root.gameObject.SetActive(false);
            g.root = null;
        }

        private Transform CreateGroupRoot()
        {
            var go = new GameObject("TrackEnemyGroup");
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            return go.transform;
        }

        private ObjectPool<GameObject> GetPrefabViewPool(GameObject prefab)
        {
            if (_prefabViewPools.TryGetValue(prefab, out ObjectPool<GameObject> pool)) return pool;
            pool = new ObjectPool<GameObject>(
                () =>
                {
                    GameObject go = Instantiate(prefab);
                    go.SetActive(false);
                    return go;
                },
                go => go.SetActive(true),
                go => go.SetActive(false),
                null,
                collectionCheck: false, defaultCapacity: 8, maxSize: 64);
            _prefabViewPools[prefab] = pool;
            return pool;
        }

        private ObjectPool<GameObject> GetFallbackViewPool(TrackEnemyKind kind)
        {
            int idx = (int)kind;
            if (_fallbackViewPools[idx] != null) return _fallbackViewPools[idx];
            var pool = new ObjectPool<GameObject>(
                () => CreateFallbackView(kind),
                go => go.SetActive(true),
                go => go.SetActive(false),
                null,
                collectionCheck: false, defaultCapacity: 8, maxSize: 64);
            _fallbackViewPools[idx] = pool;
            return pool;
        }

        // Greybox sem arte (CONTRACT §1.12): primitivo por kind, tintado via MPB (nunca
        // instanciar material em runtime) e SEM collider de gameplay — detecção é por
        // distância no Update, igual aos obstáculos do LevelManager.
        private GameObject CreateFallbackView(TrackEnemyKind kind)
        {
            PrimitiveType prim;
            switch (kind)
            {
                case TrackEnemyKind.Tank: prim = PrimitiveType.Cube; break;
                case TrackEnemyKind.Ranged: prim = PrimitiveType.Cylinder; break;
                case TrackEnemyKind.Healer: prim = PrimitiveType.Sphere; break;
                default: prim = PrimitiveType.Capsule; break;
            }
            GameObject go = GameObject.CreatePrimitive(prim);
            go.name = "TrackEnemy_" + kind;
            Collider col = go.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                _mpb.Clear();
                Color c = KindColor(kind);
                _mpb.SetColor(BaseColorId, c);   // URP
                _mpb.SetColor(ColorId, c);       // builtin/fallback (mesmo padrão do GateView)
                r.SetPropertyBlock(_mpb);
            }
            go.SetActive(false);
            return go;
        }

        // ------------------------------------------------------------------ tabelas por kind

        // verde-doente / cinza / amarelo / rosa: leitura instantânea do papel de cada inimigo
        private static Color KindColor(TrackEnemyKind kind)
        {
            switch (kind)
            {
                case TrackEnemyKind.Tank: return new Color(0.55f, 0.55f, 0.60f);
                case TrackEnemyKind.Ranged: return new Color(0.95f, 0.80f, 0.20f);
                case TrackEnemyKind.Healer: return new Color(0.95f, 0.50f, 0.70f);
                default: return new Color(0.55f, 0.78f, 0.25f);
            }
        }

        private static float KindScale(TrackEnemyKind kind)
        {
            switch (kind)
            {
                case TrackEnemyKind.Tank: return 1.6f;
                case TrackEnemyKind.Ranged: return 0.8f;
                case TrackEnemyKind.Healer: return 0.9f;
                default: return 0.5f;
            }
        }

        // Altura do pé do primitivo: cápsula/cilindro têm meia-altura = escala; cubo/esfera = escala/2.
        private static float KindHalfHeight(TrackEnemyKind kind)
        {
            switch (kind)
            {
                case TrackEnemyKind.Tank: return 1.6f * 0.5f;
                case TrackEnemyKind.Ranged: return 0.8f;
                case TrackEnemyKind.Healer: return 0.9f * 0.5f;
                default: return 0.5f;
            }
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private int AliveCount(TrackEnemyGroup g)
        {
            return Mathf.Clamp(Mathf.CeilToInt(g.hp / g.hpPerUnit), 0, g.initialCount);
        }
    }
}
