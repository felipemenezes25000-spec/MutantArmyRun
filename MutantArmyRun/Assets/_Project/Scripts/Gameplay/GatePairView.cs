using MutantArmy.Core;
using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Par de portais L/R num único prefab (doc 12 §4.3). Contrato one-shot:
    /// 1. Meios-portais por referência SERIALIZADA — nunca GetChild(n).
    /// 2. Flag _consumed no manager DO PAR: a 1ª unidade que toca consome o par inteiro;
    ///    callbacks de física são síncronos — sem race; exatamente 1 evento por exército.
    /// 3. Como pares são pooled (§6.4), Setup() SEMPRE reseta a flag — flag que nunca
    ///    reseta = portal morto no chunk reciclado.
    /// </summary>
    public class GatePairView : MonoBehaviour
    {
        [SerializeField] private GateView _left;
        [SerializeField] private GateView _right;

        private bool _consumed;   // one-shot do PAR (regra 2)

        public void Setup(GateConfigSO l, GateConfigSO r, float trackPos)
        {
            _consumed = false;   // pool reusa: SEMPRE resetar a flag (regra 3)
            transform.position = new Vector3(0f, 0f, trackPos);
            if (_left != null) _left.Bind(l);
            if (_right != null) _right.Bind(r);
        }

        public void OnArmyTouched(GateView touched)   // 1 trigger contra o AABB do exército (§4.2)
        {
            if (_consumed || touched == null) return;
            _consumed = true;

            if (_left != null) _left.DisableCollider();
            if (_right != null) _right.DisableCollider();   // ambos no MESMO frame; descarte anima depois

            GateView other = OtherOf(touched);
            if (GateManager.Instance != null && other != null)
                GateManager.Instance.Consume(touched.Config, other.Config);
        }

        public GateView OtherOf(GateView g)
        {
            return g == _left ? _right : _left;
        }
    }
}
