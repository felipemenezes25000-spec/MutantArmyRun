using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// View 3D pooled de UMA unidade da multidão: cache de Transform/Animator, SEM Update
    /// próprio — o contrato do doc 12 §4.2 proíbe MonoBehaviour/Update POR UNIDADE; quem
    /// posiciona é o CrowdRenderer, em lote, a partir dos arrays SoA. Componente é anexado
    /// em RUNTIME pelo pool: o prefab variant fica arte pura (sem dependência de script).
    /// </summary>
    public sealed class CrowdUnitView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public Transform CachedTransform { get; private set; }
        public Animator CachedAnimator { get; private set; }

        private int _appliedState = -1;
        private float _appliedScale = -1f;

        // tint da mutação via MaterialPropertyBlock (zero-alloc, reusado): -1 = nunca aplicado,
        // 0 = sem tint (restaurado), 1 = tintado. Só toca o renderer quando o estado MUDA.
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private int _appliedTintState = -1;
        private Color _appliedTint;

        public void Cache(int stateParamHash)
        {
            CachedTransform = transform;
            CachedAnimator = GetComponentInChildren<Animator>(true);
            // Animator sem o parâmetro int "State" (ex.: prefab de fora do UnitVisualFactory):
            // ignorar evita 1 warning de "Parameter does not exist" POR FRAME no editor.
            if (CachedAnimator != null && !HasIntParam(CachedAnimator, stateParamHash))
                CachedAnimator = null;
            _renderers = GetComponentsInChildren<Renderer>(true);   // aloca 1× no Cache, nunca por frame
        }

        /// <summary>SetActive(false→true) reseta o Animator ao estado de entrada — força reaplicar.</summary>
        public void ResetApplied()
        {
            _appliedState = -1;
            _appliedScale = -1f;
            _appliedTintState = -1;   // reuso do pool: força reavaliar o tint (não vaza entre fases)
        }

        /// <summary>
        /// Empurra (ou restaura) o tint da mutação via MPB — _BaseColor + _EmissionColor.
        /// Só toca os renderers quando o estado de tint muda (zero-alloc por frame). Quando
        /// <paramref name="active"/> é false, restaura branco/preto (sem vazar entre pool/fases).
        /// </summary>
        public void ApplyTint(bool active, Color tint)
        {
            if (_renderers == null || _renderers.Length == 0) return;
            int wanted = active ? 1 : 0;
            if (wanted == _appliedTintState && (!active || tint == _appliedTint)) return;
            _appliedTintState = wanted;
            _appliedTint = tint;

            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _mpb.Clear();
            if (active)
            {
                _mpb.SetColor(BaseColorId, tint);
                _mpb.SetColor(EmissionColorId, tint * 0.6f);   // brilho sutil — leitura clara da mutação
            }
            // bloco vazio quando inativo: limpa qualquer override anterior (restaura o material)
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].SetPropertyBlock(_mpb);
        }

        /// <summary>1 chamada por frame por unidade viva: posição+rotação em lote, escala/anim só em mudança.</summary>
        public void Apply(Vector3 position, Quaternion rotation, float scale, int animState, int stateParamHash)
        {
            CachedTransform.SetPositionAndRotation(position, rotation);
            if (!Mathf.Approximately(scale, _appliedScale))
            {
                _appliedScale = scale;
                CachedTransform.localScale = new Vector3(scale, scale, scale);
            }
            if (CachedAnimator != null && animState != _appliedState)
            {
                _appliedState = animState;
                CachedAnimator.SetInteger(stateParamHash, animState);
            }
        }

        private static bool HasIntParam(Animator animator, int hash)
        {
            AnimatorControllerParameter[] ps = animator.parameters;   // aloca: 1× no Cache, nunca por frame
            for (int i = 0; i < ps.Length; i++)
                if (ps[i].nameHash == hash && ps[i].type == AnimatorControllerParameterType.Int) return true;
            return false;
        }
    }

    /// <summary>
    /// Pool de GameObjects da multidão (decisão de arquitetura desta fase): com Supply cap 60,
    /// quando UnitConfigSO.viewPrefab existe cada índice VIVO do SoA ganha uma instância pooled
    /// (SkinnedMeshRenderer+Animator) reposicionada por frame pelo CrowdRenderer; sem viewPrefab
    /// o tipo permanece no caminho instanced (fallback intacto; VAT fica pós-MVP).
    /// Zero alocação por frame em regime: UnityEngine.Pool + listas que só crescem com o exército;
    /// pré-aquecimento AMORTIZADO (N instâncias/frame) — nunca spike de Instantiate na corrida.
    /// </summary>
    public sealed class CrowdViewPool
    {
        // contrato do parâmetro int "State" dos AnimatorControllers gerados pelo UnitVisualFactory
        public const int StateIdle = 0;
        public const int StateRun = 1;
        public const int StateAttack = 2;

        private static readonly int StateParamHash = Animator.StringToHash("State");

        private const int PrewarmTarget = 24;     // Supply cap 60: tropa barata domina a contagem
        private const int PrewarmPerFrame = 4;    // amortizado — espalha o custo de Instantiate
        private const int PoolMaxSize = 332;      // mesmo teto dos arrays SoA (300 + folga, doc 12 §4.2)

        private sealed class TypeViews
        {
            public GameObject Prefab;
            public float BaseScale = 1f;
            public ObjectPool<CrowdUnitView> Pool;
            public readonly List<CrowdUnitView> Active = new List<CrowdUnitView>(64);
            public int Used;                // cursor do frame corrente
            public int PrewarmRemaining;
        }

        private readonly List<TypeViews> _types = new List<TypeViews>();
        private readonly Transform _root;
        private readonly CrowdUnitView[] _prewarmBuffer = new CrowdUnitView[PrewarmPerFrame];

        public CrowdViewPool(Transform root)
        {
            _root = root;
        }

        public bool HasView(int typeId)
        {
            return typeId >= 0 && typeId < _types.Count && _types[typeId].Prefab != null;
        }

        /// <summary>Escala-base do prefab variant (0.6 tropas / 1.3 Gigante) — multiplicada por mutação/desmonte.</summary>
        public float GetBaseScale(int typeId)
        {
            return _types[typeId].BaseScale;
        }

        /// <summary>Registra o viewPrefab de um typeId (idempotente; null desliga o caminho pooled).</summary>
        public void Register(int typeId, GameObject prefab)
        {
            while (_types.Count <= typeId) _types.Add(new TypeViews());
            TypeViews t = _types[typeId];
            if (t.Prefab == prefab) return;

            // troca de prefab invalida instâncias antigas — devolve e descarta o pool anterior
            ReleaseAllOf(t);
            if (t.Pool != null) t.Pool.Clear();
            t.Prefab = prefab;
            t.Pool = null;
            t.PrewarmRemaining = 0;
            if (prefab == null) return;

            t.BaseScale = prefab.transform.localScale.x;   // variant guarda a escala da tropa
            t.Pool = BuildPool(prefab);
            t.PrewarmRemaining = PrewarmTarget;
        }

        /// <summary>Início do frame: zera cursores; instâncias não reutilizadas voltam ao pool no EndFrame.</summary>
        public void BeginFrame()
        {
            for (int i = 0; i < _types.Count; i++) _types[i].Used = 0;
        }

        /// <summary>Posiciona a PRÓXIMA instância do tipo — 1 chamada por índice vivo do SoA, por frame.</summary>
        public void Place(int typeId, Vector3 position, Quaternion rotation, float scale, int animState,
                          bool tintActive, Color tint)
        {
            TypeViews t = _types[typeId];
            CrowdUnitView view;
            if (t.Used < t.Active.Count)
            {
                view = t.Active[t.Used];
            }
            else
            {
                view = t.Pool.Get();
                t.Active.Add(view);
            }
            t.Used++;
            view.Apply(position, rotation, scale, animState, StateParamHash);
            view.ApplyTint(tintActive, tint);   // tint da mutação via MPB (zero-alloc; só toca em mudança)
        }

        /// <summary>Fim do frame: excedente (morte/divisão/reset) → Release, nunca Destroy (doc 12 §6.4).</summary>
        public void EndFrame()
        {
            for (int i = 0; i < _types.Count; i++)
            {
                TypeViews t = _types[i];
                for (int k = t.Active.Count - 1; k >= t.Used; k--)
                {
                    t.Pool.Release(t.Active[k]);
                    t.Active.RemoveAt(k);
                }
            }
        }

        /// <summary>Pré-aquecimento amortizado — chamado 1×/frame pelo Update do CrowdRenderer.</summary>
        public void TickPrewarm()
        {
            for (int i = 0; i < _types.Count; i++)
            {
                TypeViews t = _types[i];
                if (t.PrewarmRemaining <= 0 || t.Pool == null) continue;
                int n = Mathf.Min(PrewarmPerFrame, t.PrewarmRemaining);
                t.PrewarmRemaining -= n;
                for (int k = 0; k < n; k++) _prewarmBuffer[k] = t.Pool.Get();
                for (int k = 0; k < n; k++)
                {
                    t.Pool.Release(_prewarmBuffer[k]);
                    _prewarmBuffer[k] = null;
                }
                return;   // 1 tipo por frame: espalha o custo ainda mais
            }
        }

        /// <summary>Limpeza total (OnDestroy do CrowdRenderer): esvazia pools e listas.</summary>
        public void Clear()
        {
            for (int i = 0; i < _types.Count; i++)
            {
                TypeViews t = _types[i];
                t.Active.Clear();
                t.Used = 0;
                t.PrewarmRemaining = 0;
                if (t.Pool != null) t.Pool.Clear();
            }
        }

        private static void ReleaseAllOf(TypeViews t)
        {
            for (int k = t.Active.Count - 1; k >= 0; k--)
                if (t.Active[k] != null && t.Pool != null) t.Pool.Release(t.Active[k]);
            t.Active.Clear();
            t.Used = 0;
        }

        private ObjectPool<CrowdUnitView> BuildPool(GameObject prefab)
        {
            Transform root = _root;
            return new ObjectPool<CrowdUnitView>(
                () =>
                {
                    GameObject go = Object.Instantiate(prefab, root);
                    CrowdUnitView view = go.GetComponent<CrowdUnitView>();
                    if (view == null) view = go.AddComponent<CrowdUnitView>();
                    view.Cache(StateParamHash);
                    go.SetActive(false);
                    return view;
                },
                view =>
                {
                    view.ResetApplied();   // SetActive resetou o Animator → reaplica estado no 1º Apply
                    view.gameObject.SetActive(true);
                },
                view => view.gameObject.SetActive(false),
                view => { if (view != null) Object.Destroy(view.gameObject); },
                collectionCheck: false, defaultCapacity: 32, maxSize: PoolMaxSize);
        }
    }
}
