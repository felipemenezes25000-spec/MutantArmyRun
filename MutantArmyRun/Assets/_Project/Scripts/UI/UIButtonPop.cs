using UnityEngine;
using UnityEngine.EventSystems;
using MutantArmy.Core;

namespace MutantArmy.UI
{
    /// <summary>
    /// Microinteração canônica de botão (doc 09 §6): ScalePop no press, via a API
    /// compartilhada Core.Tween do pacote de juice — TODO botão construído pelo
    /// ProjectSetup recebe este componente. Pop no pointer-DOWN (não no click):
    /// resposta tátil imediata, antes mesmo do onClick disparar.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIButtonPop : MonoBehaviour, IPointerDownHandler
    {
        public void OnPointerDown(PointerEventData eventData)
        {
            Tween.ScalePop(transform);
        }
    }
}
