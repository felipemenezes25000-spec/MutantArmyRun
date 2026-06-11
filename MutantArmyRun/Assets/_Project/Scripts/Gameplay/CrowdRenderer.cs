using System.Collections.Generic;
using MutantArmy.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Render da multidão (doc 12 §6.2): Graphics.RenderMeshInstanced POR TIPO de tropa,
    /// alimentado por Matrix4x4[] reusados — alocação só quando um tipo cresce além da
    /// capacidade atual (nunca por frame em regime). Zero Animator, zero GameObject por
    /// unidade: o CrowdManager submete os arrays SoA 1×/frame.
    /// </summary>
    public class CrowdRenderer : MonoBehaviour, IInitializable
    {
        public static CrowdRenderer Instance { get; private set; }

        private const int MaxInstancesPerCall = 1023;   // limite do instancing por chamada

        private sealed class TypeBatch
        {
            public Mesh Mesh;
            public Material Material;
            public Matrix4x4[] Matrices = new Matrix4x4[64];
            public int Count;
            public bool WarnedNoInstancing;
        }

        private readonly List<TypeBatch> _batches = new List<TypeBatch>();

        public void Init()   // chamado pelo bootstrap da cena Game (doc 12 §3.3)
        {
            Instance = this;
        }

        /// <summary>Registrado pelo CrowdManager quando um tipo novo entra no exército.</summary>
        public void RegisterType(int typeId, UnitConfigSO config)
        {
            while (_batches.Count <= typeId) _batches.Add(new TypeBatch());
            TypeBatch b = _batches[typeId];
            b.Mesh = config != null ? config.mesh : null;
            b.Material = config != null ? config.material : null;
        }

        /// <summary>Ponto de entrada do CrowdManager (doc 12 §4.2): submete o frame inteiro.</summary>
        public static void Submit(Vector3[] positions, byte[] typeIds, byte[] flags, int count)
        {
            if (Instance != null) Instance.SubmitInternal(positions, typeIds, flags, count);
        }

        private void SubmitInternal(Vector3[] positions, byte[] typeIds, byte[] flags, int count)
        {
            if (positions == null || typeIds == null || flags == null || count <= 0) return;

            for (int t = 0; t < _batches.Count; t++) _batches[t].Count = 0;

            float sizeMult = CrowdManager.Instance != null ? CrowdManager.Instance.MutationSizeMult : 1f;
            Vector3 min = positions[0];
            Vector3 max = positions[0];
            int submitted = 0;

            for (int i = 0; i < count; i++)
            {
                int typeId = typeIds[i];
                if (typeId >= _batches.Count) continue;
                TypeBatch b = _batches[typeId];
                if (b.Mesh == null || b.Material == null) continue;   // sem arte ligada: pula sem erro

                // dying encolhe (leitura do desmonte); mutação de tamanho vale p/ o exército todo
                float s = ((flags[i] & CrowdManager.FlagDying) != 0 ? 0.6f : 1f) * sizeMult;
                if (b.Count == b.Matrices.Length)
                    System.Array.Resize(ref b.Matrices, b.Matrices.Length * 2);
                b.Matrices[b.Count++] = Matrix4x4.TRS(positions[i], Quaternion.identity, new Vector3(s, s, s));

                min = Vector3.Min(min, positions[i]);
                max = Vector3.Max(max, positions[i]);
                submitted++;
            }

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
    }
}
