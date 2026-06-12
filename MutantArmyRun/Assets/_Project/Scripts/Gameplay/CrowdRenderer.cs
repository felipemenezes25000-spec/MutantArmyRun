using System.Collections.Generic;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Render da multidão (doc 12 §6.2) em DOIS caminhos, decididos por tipo:
    /// 1. POOLED (decisão desta fase, Supply cap 60): UnitConfigSO.viewPrefab existe → cada
    ///    índice vivo do SoA ganha uma instância do CrowdViewPool (SkinnedMeshRenderer +
    ///    Animator Idle/Run/Attack por parâmetro), reposicionada por frame a partir dos
    ///    arrays; rotação = direção da velocidade; morte = encolhe/afunda e Release.
    /// 2. INSTANCED (fallback intacto): Graphics.RenderMeshInstanced por tipo, alimentado
    ///    por Matrix4x4[] reusados — alocação só quando um tipo cresce além da capacidade.
    /// Nos dois casos o CrowdManager submete os arrays SoA 1×/frame — zero Update por unidade.
    /// </summary>
    public class CrowdRenderer : MonoBehaviour, IInitializable
    {
        public static CrowdRenderer Instance { get; private set; }

        private const int MaxInstancesPerCall = 1023;   // limite do instancing por chamada

        private sealed class TypeBatch
        {
            public Mesh Mesh;
            public Material Material;
            public bool HasView;   // viewPrefab registrado → caminho pooled
            public Matrix4x4[] Matrices = new Matrix4x4[64];
            public int Count;
            public bool WarnedNoInstancing;
        }

        private readonly List<TypeBatch> _batches = new List<TypeBatch>();
        private CrowdViewPool _viewPool;

        public void Init()   // chamado pelo bootstrap da cena Game (doc 12 §3.3)
        {
            Instance = this;
            if (_viewPool == null) _viewPool = new CrowdViewPool(transform);
        }

        private void OnDestroy()
        {
            // pools guardam instâncias-filhas deste GO: limpar evita Release em objeto morto
            if (_viewPool != null) _viewPool.Clear();
        }

        private void Update()
        {
            // pré-aquecimento amortizado do pool de views — N instâncias/frame, sem spike
            if (_viewPool != null) _viewPool.TickPrewarm();
        }

        /// <summary>Registrado pelo CrowdManager quando um tipo novo entra no exército.</summary>
        public void RegisterType(int typeId, UnitConfigSO config)
        {
            while (_batches.Count <= typeId) _batches.Add(new TypeBatch());
            TypeBatch b = _batches[typeId];
            b.Mesh = config != null ? config.mesh : null;
            b.Material = config != null ? config.material : null;

            GameObject view = config != null ? config.viewPrefab : null;
            b.HasView = view != null;
            if (_viewPool == null) _viewPool = new CrowdViewPool(transform);   // RegisterType antes do Init (ordem de bootstrap)
            _viewPool.Register(typeId, view);
        }

        /// <summary>Compat com a assinatura antiga: sem velocidades/timers — rotação identidade.</summary>
        public static void Submit(Vector3[] positions, byte[] typeIds, byte[] flags, int count)
        {
            Submit(positions, null, typeIds, flags, null, 0f, count);
        }

        /// <summary>Ponto de entrada do CrowdManager (doc 12 §4.2): submete o frame inteiro.</summary>
        public static void Submit(Vector3[] positions, Vector3[] velocities, byte[] typeIds,
                                  byte[] flags, float[] dyingTimers, float dyingSeconds, int count)
        {
            if (Instance != null)
                Instance.SubmitInternal(positions, velocities, typeIds, flags, dyingTimers, dyingSeconds, count);
        }

        private void SubmitInternal(Vector3[] positions, Vector3[] velocities, byte[] typeIds,
                                    byte[] flags, float[] dyingTimers, float dyingSeconds, int count)
        {
            if (positions == null || typeIds == null || flags == null) return;
            if (_viewPool == null) _viewPool = new CrowdViewPool(transform);

            // BeginFrame SEMPRE roda (mesmo com count 0): soft reset/derrota devolvem todas
            // as instâncias pooled no EndFrame — Release, nunca Destroy (doc 12 §6.4)
            _viewPool.BeginFrame();

            for (int t = 0; t < _batches.Count; t++) _batches[t].Count = 0;

            float sizeMult = CrowdManager.Instance != null ? CrowdManager.Instance.MutationSizeMult : 1f;
            int animState = ResolveAnimState();
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;
            bool boundsInit = false;
            int submitted = 0;

            for (int i = 0; i < count; i++)
            {
                int typeId = typeIds[i];
                if (typeId >= _batches.Count) continue;
                TypeBatch b = _batches[typeId];

                // caminho pooled: viewPrefab existe — instância com Animator por índice vivo
                if (b.HasView && _viewPool.HasView(typeId))
                {
                    PlaceView(typeId, i, positions, velocities, flags, dyingTimers, dyingSeconds,
                              sizeMult, animState);
                    continue;
                }

                if (b.Mesh == null || b.Material == null) continue;   // sem arte ligada: pula sem erro

                // dying encolhe (leitura do desmonte); mutação de tamanho vale p/ o exército todo
                float s = ((flags[i] & CrowdManager.FlagDying) != 0 ? 0.6f : 1f) * sizeMult;
                if (b.Count == b.Matrices.Length)
                    System.Array.Resize(ref b.Matrices, b.Matrices.Length * 2);
                b.Matrices[b.Count++] = Matrix4x4.TRS(positions[i], Quaternion.identity, new Vector3(s, s, s));

                if (!boundsInit)
                {
                    min = positions[i];
                    max = positions[i];
                    boundsInit = true;
                }
                else
                {
                    min = Vector3.Min(min, positions[i]);
                    max = Vector3.Max(max, positions[i]);
                }
                submitted++;
            }

            _viewPool.EndFrame();   // índices que sumiram neste frame devolvem a view ao pool

            if (submitted == 0) return;

            // RenderParams exige worldBounds explícito — sem ele a multidão é toda culled
            Bounds bounds = new Bounds((min + max) * 0.5f, (max - min) + new Vector3(4f, 4f, 4f));

            for (int t = 0; t < _batches.Count; t++)
            {
                TypeBatch b = _batches[t];
                if (b.Count == 0) continue;
                if (!b.Material.enableInstancing)
                {
                    if (!b.WarnedNoInstancing)
                    {
                        b.WarnedNoInstancing = true;
                        Debug.LogWarning($"CrowdRenderer: material '{b.Material.name}' sem GPU Instancing — tipo não renderiza.");
                    }
                    continue;
                }

                RenderParams rp = new RenderParams(b.Material)
                {
                    worldBounds = bounds,
                    shadowCastingMode = ShadowCastingMode.On,
                    receiveShadows = true
                };
                for (int start = 0; start < b.Count; start += MaxInstancesPerCall)
                {
                    int len = Mathf.Min(MaxInstancesPerCall, b.Count - start);
                    Graphics.RenderMeshInstanced(rp, b.Mesh, 0, b.Matrices, len, start);
                }
            }
        }

        private void PlaceView(int typeId, int i, Vector3[] positions, Vector3[] velocities, byte[] flags,
                               float[] dyingTimers, float dyingSeconds, float sizeMult, int animState)
        {
            Vector3 pos = positions[i];

            // rotação = direção da velocidade (XZ); parado/limiar → mantém o forward da pista
            Quaternion rot = Quaternion.identity;
            if (velocities != null)
            {
                Vector3 dir = velocities[i];
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0025f) rot = Quaternion.LookRotation(dir);
            }

            float scale = _viewPool.GetBaseScale(typeId) * sizeMult;
            if ((flags[i] & CrowdManager.FlagDying) != 0)
            {
                // desmonte: queda rápida — encolhe e afunda ao longo do timer de dying;
                // quando o índice some do SoA, o EndFrame devolve a instância ao pool
                float p = dyingSeconds > 0f && dyingTimers != null
                    ? Mathf.Clamp01(dyingTimers[i] / dyingSeconds)
                    : 0.6f;
                scale *= Mathf.Max(0.05f, p);
                pos.y -= (1f - p) * 0.5f;
            }

            _viewPool.Place(typeId, pos, rot, scale, animState);
        }

        // Estado do Animator segue o estado do JOGO (contrato da fase): Running → Run,
        // arena (BossFight) → Attack, resto (menu/scout/vitória/derrota/revive) → Idle.
        private static int ResolveAnimState()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return CrowdViewPool.StateIdle;
            GameState s = gm.State;
            if (s == GameState.Running) return CrowdViewPool.StateRun;
            if (s == GameState.BossFight) return CrowdViewPool.StateAttack;
            return CrowdViewPool.StateIdle;
        }
    }
}
