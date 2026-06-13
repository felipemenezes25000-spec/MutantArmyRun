using System;
using System.Collections;
using UnityEngine;

namespace MutantArmy.Services
{
    /// <summary>
    /// Provider de IAP SIMULADO para desenvolvimento: a compra "confirma" após um delay fake
    /// (0,5–1 s, tempo real) — permite testar o fluxo completo da loja no editor (funil
    /// purchase_started → purchase_completed, entitlement concedido pelo IAPManager,
    /// selo da loja saindo do "EM BREVE"), inclusive a recusa (_approvePurchases=false).
    /// NÃO é o default: sem provider no prefab [Services] o IAPManager mantém o
    /// comportamento Null de fábrica (decisão de wiring é da Onda 4).
    ///
    /// COMO PLUGAR O REVENUECAT REAL (sem tocar em nenhum consumidor — doc 12 §7.4):
    /// 1. Importar o purchases-unity (RevenueCat) e configurar a API key pública no
    ///    componente Purchases do prefab (NUNCA secret key em cliente);
    /// 2. Criar RevenueCatIapProvider : MonoBehaviour, IIapProvider — Init() configura o
    ///    Purchases e busca offerings; Purchase(productId, cb) chama PurchasePackage e
    ///    responde true SÓ com CustomerInfo confirmando o entitlement (recibo validado
    ///    pelo backend do RevenueCat — nunca confiar no callback de UI);
    /// 3. RestorePurchases mapeia Purchases.RestorePurchases → cb(houve entitlement);
    /// 4. Substituir este componente pelo RevenueCatIapProvider no prefab [Services] —
    ///    o IAPManager resolve IIapProvider por GetComponentInChildren.
    /// </summary>
    public class MockIapProvider : MonoBehaviour, IIapProvider
    {
        [Header("Simulação (dev only — sem SDK, sem dinheiro real)")]
        [Tooltip("false simula recusa/cancelamento: o callback responde false após o delay.")]
        [SerializeField] private bool _approvePurchases = true;
        [Tooltip("Delay fake do diálogo de compra em segundos (tempo REAL).")]
        [SerializeField] private float _minDelaySeconds = 0.5f;
        [SerializeField] private float _maxDelaySeconds = 1f;

        private bool _initialized;

        public void Init()
        {
            _initialized = true;
            LogDev("provider simulado ativo — compras fake, nenhum SDK real");
        }

        public void Purchase(string productId, Action<bool> onResult)
        {
            // Contrato do IIapProvider: o callback SEMPRE responde — destruído/inativo = false.
            if (!_initialized || !isActiveAndEnabled)
            {
                if (onResult != null) onResult(false);
                return;
            }
            StartCoroutine(FakePurchaseRoutine(productId, onResult));
        }

        public void RestorePurchases(Action<bool> onResult)
        {
            // Mock não persiste "recibos" próprios — nada a restaurar (os entitlements já
            // concedidos vivem no save). O fluxo real consulta o backend do RevenueCat.
            LogDev("restore simulado — nenhum recibo fake para restaurar");
            if (onResult != null) onResult(false);
        }

        private IEnumerator FakePurchaseRoutine(string productId, Action<bool> onResult)
        {
            float min = Mathf.Min(_minDelaySeconds, _maxDelaySeconds);
            float max = Mathf.Max(_minDelaySeconds, _maxDelaySeconds);
            float delay = UnityEngine.Random.Range(min, max);
            LogDev($"compra '{productId}' simulada — resolve em {delay:0.0}s (approved={_approvePurchases})");
            // Tempo REAL: o diálogo fake resolve mesmo com o jogo pausado (timeScale 0).
            yield return new WaitForSecondsRealtime(delay);
            if (onResult != null) onResult(_approvePurchases);
        }

        // Log discreto SÓ em dev (padrão NullAnalyticsProvider) — release fica silencioso.
        private static void LogDev(string message)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log("[MockIap] " + message);
#endif
        }
    }
}
