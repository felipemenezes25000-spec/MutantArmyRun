using System;
using System.Collections;
using MutantArmy.Core;
using UnityEngine;

namespace MutantArmy.Services
{
    /// <summary>
    /// Provider de ads SIMULADO para desenvolvimento: rewarded sempre "carregado" e concedido
    /// após um delay fake (0,5–1 s, tempo real) — permite testar o fluxo DOBRAR/REVIVE de
    /// ponta a ponta no editor sem SDK, inclusive o caminho de abandono (_grantReward=false).
    /// NÃO é o default: o NullAdsProvider continua no prefab [Services] (decisão de wiring é
    /// da Onda 4 — trocar o componente no prefab é a única mudança; o GameBootstrap resolve
    /// IAdsProvider por GetComponentInChildren).
    ///
    /// COMO PLUGAR O APPLOVIN MAX REAL (sem tocar em nenhum consumidor):
    /// 1. Importar o AppLovin MAX Unity Plugin (Integration Manager) e configurar a SDK key
    ///    no AppLovinSettings (NUNCA hardcoded em script versionado);
    /// 2. Criar MaxAdsProvider : MonoBehaviour, IAdsProvider — Init() chama
    ///    MaxSdk.InitializeSdk() e pré-carrega rewarded/interstitial nos callbacks de load;
    /// 3. IsRewardedReady => MaxSdk.IsRewardedAdReady(adUnitId); ShowRewarded mapeia
    ///    OnAdReceivedRewardEvent → onResult(true) e Hidden/Failed sem reward → onResult(false)
    ///    (o callback SEMPRE responde — contrato do IAdsProvider);
    /// 4. Substituir o componente provider no prefab [Services] pelo MaxAdsProvider — fim.
    /// </summary>
    public class MockAdsProvider : MonoBehaviour, IAdsProvider
    {
        [Header("Simulação (dev only — sem SDK, sem rede)")]
        [Tooltip("false simula 'sem fill'/abandono: o callback responde false após o delay.")]
        [SerializeField] private bool _grantReward = true;
        [Tooltip("Delay fake de exibição do ad em segundos (tempo REAL — sobrevive a pausa/slow-mo).")]
        [SerializeField] private float _minDelaySeconds = 0.5f;
        [SerializeField] private float _maxDelaySeconds = 1f;

        private bool _initialized;

        /// <summary>Rewarded "carregado" assim que o Init roda — a UI mostra o botão de verdade.</summary>
        public bool IsRewardedReady => _initialized;

        public void Init()
        {
            _initialized = true;
            LogDev("provider simulado ativo — ads fake, nenhum SDK real");
        }

        public void ShowRewarded(string placement, Action<bool> onResult)
        {
            // Contrato do IAdsProvider: o callback SEMPRE responde — destruído/inativo = false.
            if (!_initialized || !isActiveAndEnabled)
            {
                if (onResult != null) onResult(false);
                return;
            }
            StartCoroutine(FakeRewardedRoutine(placement, onResult));
        }

        public void ShowInterstitial()
        {
            // Interstitial fake é instantâneo: só registra que a política decidiu exibir.
            LogDev("interstitial simulado exibido");
        }

        private IEnumerator FakeRewardedRoutine(string placement, Action<bool> onResult)
        {
            float min = Mathf.Min(_minDelaySeconds, _maxDelaySeconds);
            float max = Mathf.Max(_minDelaySeconds, _maxDelaySeconds);
            float delay = UnityEngine.Random.Range(min, max);
            LogDev($"rewarded '{placement}' simulado — resolve em {delay:0.0}s (granted={_grantReward})");
            // Tempo REAL: o revive abre com o jogo congelado — o ad fake resolve mesmo assim.
            yield return new WaitForSecondsRealtime(delay);
            if (onResult != null) onResult(_grantReward);
        }

        // Log discreto SÓ em dev (padrão NullAnalyticsProvider) — release fica silencioso.
        private static void LogDev(string message)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log("[MockAds] " + message);
#endif
        }
    }
}
