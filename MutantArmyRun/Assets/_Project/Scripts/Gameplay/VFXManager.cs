using System.Collections.Generic;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;
using UnityEngine.Pool;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Pools de partículas com ORÇAMENTO GLOBAL (doc 12 §6.3: ≤500 partículas vivas,
    /// ≤8 sistemas ativos — pedidos acima do teto sofrem drop silencioso das de menor
    /// prioridade) e o slow motion canônico do golpe final (doc 12 §3.1):
    /// Time.timeScale = 0,3 por 0,8 s com Time.fixedDeltaTime escalado na MESMA proporção
    /// (0,02 → 0,006 s) para a física acompanhar sem engasgo.
    /// </summary>
    public class VFXManager : MonoBehaviour, IInitializable
    {
        public static VFXManager Instance { get; private set; }

        [Header("Orçamento global (doc 12 §6.3)")]
        [SerializeField] private int _maxActiveSystems = 8;
        [SerializeField] private int _maxLiveParticles = 500;

        [Header("Hooks de VFX (opcionais até a arte existir)")]
        [SerializeField] private Transform _telegraphDecal;            // decal do especial do boss
        [SerializeField] private ParticleSystem _coinFanfarePrefab;    // moeda voadora do overflow de Supply
        [SerializeField] private ParticleSystem _despawnBurstPrefab;   // 1 VFX agregado por lote removido

        [Header("Juice (preenchidos pelo MAR Tools/Build Juice)")]
        [SerializeField] private ParticleSystem _gateBurstPrefab;      // burst no consumo de portal (tintado)
        [SerializeField] private ParticleSystem _popBurstPrefab;       // pop pequeno da cascata de multiplicação
        [SerializeField] private ParticleSystem _confettiPrefab;       // vitória: 2 emissores laterais
        [SerializeField] private Renderer _telegraphRenderer;          // anel vermelho pulsante (alpha via MPB)

        private struct ActiveVfx
        {
            public ParticleSystem Ps;
            public ParticleSystem SourcePrefab;
            public int Priority;
        }

        private readonly List<ActiveVfx> _active = new List<ActiveVfx>();
        private readonly Dictionary<ParticleSystem, ObjectPool<ParticleSystem>> _pools =
            new Dictionary<ParticleSystem, ObjectPool<ParticleSystem>>();   // pool POR PREFAB (doc 12 §6.4)

        private float _baseFixedDelta = 0.02f;
        private readonly Countdown _slowMo = new Countdown();      // Domain; tickado com tempo UNSCALED
        private bool _slowMoActive;
        private readonly Countdown _telegraphTimer = new Countdown();
        private bool _telegraphActive;
        private float _telegraphArea = 1f;
        private MaterialPropertyBlock _telegraphMpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public void Init()   // chamado pelo bootstrap da cena Game (doc 12 §3.3)
        {
            Instance = this;
            _baseFixedDelta = Time.fixedDeltaTime;
            if (_telegraphDecal != null) _telegraphDecal.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            // timeScale é GLOBAL: trocar de cena (botão MENU) no meio do slow-mo do golpe
            // final deixaria menu e próxima corrida presos a 0,3× — restaura sempre.
            if (_slowMoActive)
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = _baseFixedDelta;
            }
        }

        /// <summary>Slow motion canônico do golpe final: SlowMotion(0.3f, 0.8f).</summary>
        public void SlowMotion(float scale, float seconds)
        {
            Time.timeScale = Mathf.Clamp(scale, 0.05f, 1f);
            Time.fixedDeltaTime = _baseFixedDelta * Time.timeScale;   // física acompanha (doc 12 §3.1)
            _slowMo.Set(seconds);
            _slowMoActive = true;
        }

        /// <summary>Telegraph do especial do boss (doc 12 §4.5): decal na área, pelo windup.</summary>
        public void ShowTelegraph(float area, float seconds)
        {
            _telegraphTimer.Set(seconds);
            _telegraphActive = true;
            _telegraphArea = Mathf.Max(0.5f, area);
            if (_telegraphDecal == null) return;
            // o especial mira a massa do exército: decal no centróide, escala = área de efeito
            _telegraphDecal.position = CrowdManager.Instance != null
                ? CrowdManager.Instance.Centroid
                : Vector3.zero;
            _telegraphDecal.localScale = new Vector3(area, 1f, area);
            _telegraphDecal.gameObject.SetActive(true);
        }

        /// <summary>Burst de portal consumido — tintado pela cor do GateConfigSO (honestidade visual §3.4).</summary>
        public void PlayGateBurst(Vector3 position, Color tint)
        {
            Play(_gateBurstPrefab, position, priority: 2, tint);
        }

        /// <summary>Pop pequeno da cascata de multiplicação (1 por unidade visível, com stagger no chamador).</summary>
        public void PlayPopBurst(Vector3 position)
        {
            Play(_popBurstPrefab, position, priority: 1, null);
        }

        /// <summary>
        /// Confete de vitória: 2 emissores nas laterais do enquadramento, apontados para
        /// dentro (doc 09 §4.4). Prioridade máxima — vitória nunca sofre drop de orçamento.
        /// </summary>
        public void PlayConfetti()
        {
            if (_confettiPrefab == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Transform t = cam.transform;
            Vector3 center = t.position + t.forward * 7f + Vector3.up * 1.5f;
            Play(_confettiPrefab, center - t.right * 3.5f, priority: 3, null);
            Play(_confettiPrefab, center + t.right * 3.5f, priority: 3, null);
        }

        /// <summary>Fanfarra da conversão de Supply (CANON §3.2) — 1 chamada por pop do meter.</summary>
        public void PlayCoinFanfare(Vector3 position)
        {
            Play(_coinFanfarePrefab, position, priority: 2);
        }

        /// <summary>1 VFX AGREGADO para o lote inteiro removido (doc 12 §6.4) — nunca 1 por unidade.</summary>
        public void PlayCrowdDespawnBurst(Vector3 position)
        {
            Play(_despawnBurstPrefab, position, priority: 1);
        }

        /// <summary>Toca um sistema pooled respeitando o orçamento. Retorna false no drop silencioso.</summary>
        public bool Play(ParticleSystem prefab, Vector3 position, int priority)
        {
            return Play(prefab, position, priority, null);
        }

        /// <summary>Variante com tint: sobrescreve o startColor da instância pooled antes do Play.</summary>
        public bool Play(ParticleSystem prefab, Vector3 position, int priority, Color? tint)
        {
            if (prefab == null) return false;   // hook sem asset (scaffold sem arte): silencioso
            if (!TryReserveBudget(prefab, priority)) return false;

            ParticleSystem ps = GetPool(prefab).Get();
            ps.transform.position = position;
            if (tint.HasValue)
            {
                ParticleSystem.MainModule main = ps.main;
                main.startColor = tint.Value;
            }
            ps.Play(true);
            _active.Add(new ActiveVfx { Ps = ps, SourcePrefab = prefab, Priority = priority });
            return true;
        }

        private void Update()
        {
            if (_slowMoActive)
            {
                _slowMo.Tick(Time.unscaledDeltaTime);   // unscaled: o próprio slow-mo não se alonga
                if (_slowMo.Done)
                {
                    _slowMoActive = false;
                    Time.timeScale = 1f;
                    Time.fixedDeltaTime = _baseFixedDelta;
                }
            }

            if (_telegraphActive)
            {
                _telegraphTimer.Tick(Time.deltaTime);   // scaled: sincronizado com o windup do boss
                PulseTelegraph();                       // anel vermelho PULSANTE: leitura clara do perigo
                if (_telegraphTimer.Done)
                {
                    _telegraphActive = false;
                    if (_telegraphDecal != null) _telegraphDecal.gameObject.SetActive(false);
                }
            }

            // devolve sistemas terminados ao pool — Release, nunca Destroy (doc 12 §6.4)
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                ActiveVfx a = _active[i];
                if (a.Ps == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }
                if (!a.Ps.IsAlive(true))
                {
                    GetPool(a.SourcePrefab).Release(a.Ps);
                    _active.RemoveAt(i);
                }
            }
        }

        // Anel do telegraph pulsa em escala (±12%) e alpha (0.35–0.85) — urgência crescente
        // ao fim do windup. MPB: nunca instancia material em runtime (doc 12 §6.4).
        private void PulseTelegraph()
        {
            if (_telegraphDecal == null) return;

            float urgency = 1f + 6f * Mathf.Clamp01(1f - _telegraphTimer.Remaining);   // acelera no fim
            float wave = Mathf.Sin(Time.time * 9f * urgency);
            float scale = _telegraphArea * (1f + 0.12f * wave);
            _telegraphDecal.localScale = new Vector3(scale, 1f, scale);

            if (_telegraphRenderer == null) return;
            if (_telegraphMpb == null) _telegraphMpb = new MaterialPropertyBlock();
            var color = new Color(1f, 0.15f, 0.10f, Mathf.Lerp(0.35f, 0.85f, (wave + 1f) * 0.5f));
            _telegraphMpb.SetColor(BaseColorId, color);
            _telegraphMpb.SetColor(ColorId, color);
            _telegraphRenderer.SetPropertyBlock(_telegraphMpb);
        }

        private bool TryReserveBudget(ParticleSystem prefab, int priority)
        {
            int liveParticles = 0;
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Ps != null) liveParticles += _active[i].Ps.particleCount;

            bool overSystems = _active.Count >= _maxActiveSystems;
            bool overParticles = liveParticles + prefab.main.maxParticles > _maxLiveParticles;
            if (!overSystems && !overParticles) return true;

            // acima do teto: derruba a ativa de MENOR prioridade se a nova for mais importante;
            // senão, drop silencioso da nova (doc 12 §6.3)
            int victim = -1;
            int victimPriority = priority;
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Priority < victimPriority)
                {
                    victimPriority = _active[i].Priority;
                    victim = i;
                }
            }
            if (victim < 0) return false;
            ReleaseAt(victim);
            return true;
        }

        private void ReleaseAt(int index)
        {
            ActiveVfx a = _active[index];
            if (a.Ps != null)
            {
                a.Ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                GetPool(a.SourcePrefab).Release(a.Ps);
            }
            _active.RemoveAt(index);
        }

        private ObjectPool<ParticleSystem> GetPool(ParticleSystem prefab)
        {
            if (_pools.TryGetValue(prefab, out ObjectPool<ParticleSystem> pool)) return pool;
            pool = new ObjectPool<ParticleSystem>(
                () =>
                {
                    ParticleSystem ps = Instantiate(prefab);
                    ps.gameObject.SetActive(false);
                    return ps;
                },
                ps => ps.gameObject.SetActive(true),
                ps =>
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.gameObject.SetActive(false);
                },
                null,
                collectionCheck: false, defaultCapacity: 4, maxSize: 16);
            _pools[prefab] = pool;
            return pool;
        }
    }
}
