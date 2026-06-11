using System.Collections.Generic;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;
using UnityEngine.Pool;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Monta a pista de segmentos-prefab com âncoras a partir do LevelConfigSO + seed
    /// determinística (doc 12 §4.11): a mesma fase produz SEMPRE a mesma pista — é isso
    /// que viabiliza o contrato do Boss Scout e a repro de bugs em QA. Spawn POR DISTÂNCIA
    /// à frente do líder e reciclagem POR DISTÂNCIA atrás — nunca por timer. Retry e fase
    /// seguinte acontecem na MESMA cena: soft reset, jamais SceneManager.LoadScene.
    /// </summary>
    public class LevelManager : MonoBehaviour, IInitializable
    {
        public static LevelManager Instance { get; private set; }

        [SerializeField] private float _spawnAheadMeters = 60f;
        [SerializeField] private float _recycleBehindMeters = 25f;
        [SerializeField] private float _gateSafetyMeters = 6f;   // zona de segurança pós-portal (doc 12 §4.11)
        [SerializeField] private float _laneHalfWidth = 2.2f;

        private LevelConfigSO _level;
        private System.Random _rng;     // seed do LevelConfigSO — NUNCA UnityEngine.Random aqui
        private float _furthestZ;
        private float _runStartTime;
        private bool _warnedNoSegmentPrefabs;

        private readonly Queue<TrackSegment> _liveSegments = new Queue<TrackSegment>();
        private readonly List<GameObject> _liveObstacles = new List<GameObject>();

        // pooling POR TIPO (doc 12 §6.4): 1 ObjectPool por prefab de segmento/obstáculo
        private readonly Dictionary<GameObject, ObjectPool<TrackSegment>> _segmentPools =
            new Dictionary<GameObject, ObjectPool<TrackSegment>>();
        private readonly Dictionary<GameObject, ObjectPool<GameObject>> _obstaclePools =
            new Dictionary<GameObject, ObjectPool<GameObject>>();
        private readonly Dictionary<TrackSegment, GameObject> _segmentSource =
            new Dictionary<TrackSegment, GameObject>();   // instância → prefab (null = fallback)
        private readonly Dictionary<GameObject, GameObject> _obstacleSource =
            new Dictionary<GameObject, GameObject>();
        private ObjectPool<TrackSegment> _fallbackSegmentPool;

        public float Progress01 =>
            _level != null && _level.trackLength > 0f
                ? Mathf.Clamp01(CrowdAnchor.Position.z / _level.trackLength)
                : 0f;

        public void Init()   // chamado pelo bootstrap da cena Game (doc 12 §3.3)
        {
            Instance = this;
            if (GameManager.Instance != null)
            {
                // Contrato doc 12 §4.1 (EnterState): Core não enxerga Gameplay, então é o
                // manager que assina StateEntered — entrar em Running monta a pista;
                // -= antes de += para Init repetido não duplicar a inscrição.
                GameManager.Instance.StateEntered -= HandleStateEntered;
                GameManager.Instance.StateEntered += HandleStateEntered;
                // Hook do fim de fase (doc 12 §4.1): sem este wiring o ResolveEnd cai no
                // fallback zerado e LevelRecord/analytics/ResultScreen perdem os stats reais.
                GameManager.Instance.ResultBuilder = BuildResult;
            }
        }

        private void HandleStateEntered(GameState s)
        {
            if (s == GameState.Running) BeginRun(GameManager.Instance.CurrentLevel);
        }

        private void OnDestroy()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            gm.StateEntered -= HandleStateEntered;
            // só limpa o hook se ele ainda aponta para ESTA instância (outro LevelManager
            // pode ter assumido num reload de cena)
            if (gm.ResultBuilder != null && ReferenceEquals(gm.ResultBuilder.Target, this))
                gm.ResultBuilder = null;
        }

        public void BeginRun(LevelConfigSO level)
        {
            if (level == null) return;
            DrainAll();

            _level = level;
            _rng = new System.Random(level.seed);   // determinístico: mesma fase = mesma pista
            _furthestZ = 0f;
            _runStartTime = Time.time;

            // RNG do portal de risco é DERIVADO da seed, separado do RNG da pista:
            // o desfecho do risco não pode alterar a sequência de segmentos
            if (RiskResolver.Instance != null)
                RiskResolver.Instance.Configure(new System.Random(level.seed * 486187739 + 1));

            if (CrowdAnchor.Instance != null) CrowdAnchor.Instance.ResetTo(Vector3.zero);
            if (CombatSystem.Instance != null) CombatSystem.Instance.ResetRunStats();
            if (CrowdManager.Instance != null)
            {
                CrowdManager.Instance.ResetArmy();
                // fase sempre começa com startingUnits; bônus de meta (Exército inicial)
                // entra por cima via ReconcileTo chamado pela camada Meta
                CrowdManager.Instance.ReconcileTo(Mathf.Max(1, level.startingUnits), null);
            }

            // ordem fixa de consumo do RNG (gates → obstáculos → segmentos) preserva o determinismo
            if (GateManager.Instance != null) GateManager.Instance.SpawnGates(level, _rng);
            SpawnObstacles(level);
            while (_furthestZ < _spawnAheadMeters) SpawnNextSegment();
        }

        private void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Running || _level == null) return;

            // SPAWN POR DISTÂNCIA à frente do líder; RECICLAGEM POR DISTÂNCIA atrás —
            // NUNCA por timer (com velocidade variável o chão some sob o player)
            float leaderZ = CrowdAnchor.Position.z;
            while (_furthestZ < leaderZ + _spawnAheadMeters) SpawnNextSegment();
            while (_liveSegments.Count > 0 && _liveSegments.Peek().EndZ < leaderZ - _recycleBehindMeters)
                ReleaseSegment(_liveSegments.Dequeue());
            RecycleObstaclesBehind(leaderZ);

            if (leaderZ >= _level.trackLength)
                gm.ChangeState(GameState.BossFight);   // fim da pista → arena (doc 12 §4.1)
        }

        // Soft reset: retry acontece na MESMA cena (doc 12 §2.2) — drena pools, repopula
        // âncoras, zera estado de corrida; nunca SceneManager.LoadScene.
        public void ResetRun()
        {
            LevelConfigSO level = _level;
            if (level != null) BeginRun(level);   // BeginRun já drena tudo antes de repopular
            else DrainAll();
        }

        public LevelResult BuildResult(bool won)
        {
            int survivors = CrowdManager.Instance != null ? CrowdManager.Instance.Count : 0;
            float damageDealt = CombatSystem.Instance != null ? CombatSystem.Instance.TotalDamageDealt : 0f;
            // runCoins/runXp ficam 0 aqui: Gameplay não enxerga Meta (doc 12 §2.3) — o
            // GameManager completa o delta via hook RunSnapshot ANTES do commit e do Raise
            return new LevelResult(_level != null ? _level.levelIndex : 0, won, survivors,
                                   damageDealt, 0, 0, Time.time - _runStartTime);
        }

        private void SpawnNextSegment()
        {
            TrackSegment seg = GetNextSegmentInstance();
            if (seg == null)
            {
                _furthestZ += 30f;   // mantém a janela de spawn viva mesmo sem prefab válido
                return;
            }
            seg.transform.position = new Vector3(0f, 0f, _furthestZ);
            seg.EndZ = _furthestZ + Mathf.Max(1f, seg.length);
            _furthestZ = seg.EndZ;
            _liveSegments.Enqueue(seg);
        }

        private TrackSegment GetNextSegmentInstance()
        {
            GameObject[] prefabs = _level != null && _level.world != null
                ? _level.world.trackSegmentPrefabs
                : null;

            if (prefabs == null || prefabs.Length == 0) return GetFallbackSegment();

            GameObject prefab = prefabs[_rng.Next(prefabs.Length)];
            if (prefab == null) return GetFallbackSegment();
            return GetSegmentPool(prefab).Get();
        }

        private TrackSegment GetFallbackSegment()
        {
            // greybox sem arte: segmento vazio mantém o loop de pista funcional
            if (!_warnedNoSegmentPrefabs)
            {
                _warnedNoSegmentPrefabs = true;
                Debug.LogWarning("LevelManager: WorldConfigSO sem trackSegmentPrefabs — usando segmentos vazios.");
            }
            if (_fallbackSegmentPool == null)
            {
                _fallbackSegmentPool = new ObjectPool<TrackSegment>(
                    CreateFallbackSegment, OnGetSegment, OnReleaseSegment, null,
                    collectionCheck: false, defaultCapacity: 4, maxSize: 16);
            }
            return _fallbackSegmentPool.Get();
        }

        private TrackSegment CreateFallbackSegment()
        {
            TrackSegment seg = new GameObject("TrackSegment_Fallback").AddComponent<TrackSegment>();
            _segmentSource[seg] = null;
            seg.gameObject.SetActive(false);
            return seg;
        }

        private ObjectPool<TrackSegment> GetSegmentPool(GameObject prefab)
        {
            if (_segmentPools.TryGetValue(prefab, out ObjectPool<TrackSegment> pool)) return pool;
            pool = new ObjectPool<TrackSegment>(
                () =>
                {
                    GameObject go = Instantiate(prefab);
                    TrackSegment s = go.GetComponent<TrackSegment>();
                    if (s == null) s = go.AddComponent<TrackSegment>();
                    _segmentSource[s] = prefab;
                    go.SetActive(false);
                    return s;
                },
                OnGetSegment, OnReleaseSegment, null,
                collectionCheck: false, defaultCapacity: 4, maxSize: 16);
            _segmentPools[prefab] = pool;
            return pool;
        }

        private static void OnGetSegment(TrackSegment seg)
        {
            seg.gameObject.SetActive(true);
        }

        private static void OnReleaseSegment(TrackSegment seg)
        {
            seg.gameObject.SetActive(false);
        }

        private void ReleaseSegment(TrackSegment seg)
        {
            if (seg == null) return;
            if (_segmentSource.TryGetValue(seg, out GameObject prefab) && prefab != null)
                GetSegmentPool(prefab).Release(seg);
            else if (_fallbackSegmentPool != null)
                _fallbackSegmentPool.Release(seg);
        }

        private void SpawnObstacles(LevelConfigSO level)
        {
            if (level.obstacles == null) return;
            foreach (ObstacleSlot slot in level.obstacles)
            {
                if (slot == null || slot.prefab == null) continue;
                // ZONA DE SEGURANÇA pós-portal: obstáculo logo depois de um GatePair pune a
                // escolha certa do jogador — regra aplicada na população, não no prefab
                if (InGateSafetyZone(level, slot.trackPosition)) continue;

                GameObject go = GetObstaclePool(slot.prefab).Get();
                float laneX = (float)(_rng.NextDouble() * 2.0 - 1.0) * _laneHalfWidth;
                go.transform.position = new Vector3(laneX, 0f, slot.trackPosition);
                _liveObstacles.Add(go);
            }
        }

        private bool InGateSafetyZone(LevelConfigSO level, float z)
        {
            if (level.gateSlots == null) return false;
            foreach (GateSlot gs in level.gateSlots)
            {
                if (gs == null) continue;
                if (z > gs.trackPosition && z <= gs.trackPosition + _gateSafetyMeters) return true;
            }
            return false;
        }

        private ObjectPool<GameObject> GetObstaclePool(GameObject prefab)
        {
            if (_obstaclePools.TryGetValue(prefab, out ObjectPool<GameObject> pool)) return pool;
            pool = new ObjectPool<GameObject>(
                () =>
                {
                    GameObject go = Instantiate(prefab);
                    _obstacleSource[go] = prefab;
                    go.SetActive(false);
                    return go;
                },
                go => go.SetActive(true),
                go => go.SetActive(false),
                null,
                collectionCheck: false, defaultCapacity: 8, maxSize: 64);
            _obstaclePools[prefab] = pool;
            return pool;
        }

        private void RecycleObstaclesBehind(float leaderZ)
        {
            for (int i = _liveObstacles.Count - 1; i >= 0; i--)
            {
                GameObject go = _liveObstacles[i];
                if (go == null)
                {
                    _liveObstacles.RemoveAt(i);
                    continue;
                }
                if (go.transform.position.z >= leaderZ - _recycleBehindMeters) continue;
                ReleaseObstacle(go);
                _liveObstacles.RemoveAt(i);
            }
        }

        private void ReleaseObstacle(GameObject go)
        {
            if (_obstacleSource.TryGetValue(go, out GameObject prefab) && prefab != null)
                GetObstaclePool(prefab).Release(go);
            else
                go.SetActive(false);
        }

        private void DrainAll()
        {
            while (_liveSegments.Count > 0) ReleaseSegment(_liveSegments.Dequeue());
            for (int i = _liveObstacles.Count - 1; i >= 0; i--)
                if (_liveObstacles[i] != null) ReleaseObstacle(_liveObstacles[i]);
            _liveObstacles.Clear();
            if (GateManager.Instance != null) GateManager.Instance.ReleaseAll();
        }
    }
}
