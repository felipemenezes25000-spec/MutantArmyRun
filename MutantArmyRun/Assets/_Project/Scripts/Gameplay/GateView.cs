using MutantArmy.Core;
using TMPro;
using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Meio-portal (doc 12 §4.3). Rótulo SEMPRE renderizado do dado (GateConfigSO) via
    /// OnValidate — texto digitado à mão na cena é a antítese de "portais honestos"
    /// (CANON §3.4). Contra-escala mantém o texto legível em qualquer largura de portal.
    /// </summary>
    public class GateView : MonoBehaviour
    {
        [SerializeField] private GateConfigSO _config;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Collider _trigger;
        [SerializeField] private MeshRenderer _frameRenderer;
        // Moldura/arco emissivo do portal (WorldVisualFactory) — tintado pelo portalColor
        // do config via MPB, igual ao painel. Opcional: vazio no greybox puro.
        [SerializeField] private Renderer[] _frameTrimRenderers;

        private GatePairView _pair;
        private MaterialPropertyBlock _mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");   // URP Lit
        private static readonly int ColorId = Shader.PropertyToID("_Color");           // fallback
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public GateConfigSO Config => _config;

        private void Awake()
        {
            _pair = GetComponentInParent<GatePairView>();
        }

        public void Bind(GateConfigSO c)
        {
            _config = c;
            EnableCollider();   // pool reusa: portal volta vivo (doc 12 §6.4)
            RenderLabel();
        }

        public void DisableCollider()
        {
            if (_trigger != null) _trigger.enabled = false;
        }

        public void EnableCollider()
        {
            if (_trigger != null) _trigger.enabled = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            // 1 trigger contra o proxy do exército (doc 12 §4.2); detecção por componente
            // tipado — tag/string mágica é banida (doc 12 §3.3)
            if (_pair == null) return;
            if (other.GetComponentInParent<CrowdAnchor>() == null) return;
            _pair.OnArmyTouched(this);
        }

        private void RenderLabel()   // rótulo SEMPRE derivado do dado (doc 12 §4.3, regra 4)
        {
            if (_config == null) return;

            if (_label != null)
            {
                _label.text = _config.displayLabel;   // "+10" · "x2" · "70% x10 / 30% −½"
                float s = transform.lossyScale.x;
                if (s > 1e-4f) _label.transform.localScale = Vector3.one / s;   // contra-escala
            }

            if (_frameRenderer != null)
            {
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                _mpb.SetColor(BaseColorId, _config.portalColor);
                _mpb.SetColor(ColorId, _config.portalColor);
                _frameRenderer.SetPropertyBlock(_mpb);   // MPB: sem instanciar material em runtime
            }

            if (_frameTrimRenderers != null && _frameTrimRenderers.Length > 0)
            {
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                // Emissão acima de 1: clampa no buffer LDR mas cruza o threshold do Bloom —
                // a moldura "acende" na cor do portal (azul=positivo, laranja=negativo,
                // dourado=risco; cores vêm do GateConfigSO — portais honestos, CANON §3.4).
                _mpb.SetColor(BaseColorId, _config.portalColor);
                _mpb.SetColor(ColorId, _config.portalColor);
                _mpb.SetColor(EmissionColorId, _config.portalColor * 2f);
                for (int i = 0; i < _frameTrimRenderers.Length; i++)
                {
                    if (_frameTrimRenderers[i] != null)
                        _frameTrimRenderers[i].SetPropertyBlock(_mpb);
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()   // preview do rótulo na cena, sem play mode (doc 12 §4.3)
        {
            if (_config != null && _label != null) RenderLabel();
        }
#endif
    }
}
