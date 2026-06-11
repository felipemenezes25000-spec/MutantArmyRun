using System;

namespace MutantArmy.Core
{
    /// <summary>
    /// Contrato de ads (doc 12 §4.8/§7.3). Declarado em Core para a direção de dependência
    /// valer (§2.3): MutantArmy.Services implementa (NullAdsProvider no MVP; o wrapper de
    /// MAX entra pós-instalação do Unity, sem tocar em quem consome). Regra de UI: botão de
    /// rewarded só renderiza com <see cref="IsRewardedReady"/> — nunca prometer e falhar.
    /// </summary>
    public interface IAdsProvider
    {
        bool IsRewardedReady { get; }

        void Init();

        /// <param name="placement">Um dos placements de <see cref="AdPlacement"/> (CANON §11).</param>
        /// <param name="onResult">true = recompensa concedida; false = não exibiu/abandonou.</param>
        void ShowRewarded(string placement, Action<bool> onResult);

        void ShowInterstitial();
    }
}
