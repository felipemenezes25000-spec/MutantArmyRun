using System;
using MutantArmy.Core;
using UnityEngine;

namespace MutantArmy.Services
{
    /// <summary>
    /// Provider de ads do MVP sem SDK (doc 12 §7.3): rewarded NUNCA está pronto — a UI
    /// esconde o botão (nunca botão morto, nunca prometer e falhar) — e interstitial é
    /// no-op. O wrapper de AppLovin MAX substitui este componente no prefab [Services]
    /// pós-instalação do Unity, sem tocar em nenhum consumidor (todos falam IAdsProvider).
    /// </summary>
    public class NullAdsProvider : MonoBehaviour, IAdsProvider
    {
        public bool IsRewardedReady => false;

        public void Init()
        {
            // Sem SDK para inicializar — o jogo segue 100% funcional sem ads (doc 12 §3.3).
        }

        public void ShowRewarded(string placement, Action<bool> onResult)
        {
            // Contrato do IAdsProvider: o callback SEMPRE responde — false = não exibiu.
            if (onResult != null) onResult(false);
        }

        public void ShowInterstitial()
        {
        }
    }
}
