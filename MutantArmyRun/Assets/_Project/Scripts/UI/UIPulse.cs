using UnityEngine;

namespace MutantArmy.UI
{
    /// <summary>
    /// Pulso de escala em loop SUTIL para o CTA principal (botão JOGAR, doc 09 §4.1) —
    /// senoide própria em unscaled time. COMPÕE multiplicativamente com a escala atual
    /// (divide o fator anterior, aplica o novo) em vez de sobrescrever: convive com o
    /// ScalePop do press (UIButtonPop/Core.Tween) no MESMO transform sem stomp de escala.
    /// Ao desabilitar, desfaz o próprio fator e devolve a escala intacta.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIPulse : MonoBehaviour
    {
        [SerializeField] private float _amplitude = 0.04f;   // ±4%: presença sem ansiedade
        [SerializeField] private float _period = 1.6f;       // respiração lenta

        private float _factor = 1f;   // fator aplicado por ESTE componente no frame anterior
        private float _t;

        private void OnEnable()
        {
            _factor = 1f;
            _t = 0f;
        }

        private void OnDisable()
        {
            // Remove só a contribuição própria — escala externa (tween) fica preservada.
            if (_factor > 0.0001f) transform.localScale /= _factor;
            _factor = 1f;
        }

        private void Update()
        {
            if (_period <= 0f) return;
            _t += Time.unscaledDeltaTime;
            float next = 1f + Mathf.Sin(_t * (2f * Mathf.PI) / _period) * _amplitude;
            if (_factor > 0.0001f) transform.localScale = transform.localScale / _factor * next;
            _factor = next;
        }
    }
}
