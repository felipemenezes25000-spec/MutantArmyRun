using System.Collections.Generic;

namespace MutantArmy.Core
{
    /// <summary>
    /// Contrato de analytics (doc 12 §4.9). Declarado em Core (§2.3); MutantArmy.Services
    /// implementa (NullAnalyticsProvider loga no console em DEV; o wrapper de Firebase entra
    /// depois). Eventos disparados antes do Init ficam em fila no provider.
    /// </summary>
    public interface IAnalyticsProvider
    {
        /// <param name="online">Conectividade no boot (informativo; fila local cobre offline).</param>
        /// <param name="consentStatus">Resultado UMP cacheado no save ("unknown"/"granted"/"denied").</param>
        void Init(bool online, string consentStatus);

        void Log(string eventName, IDictionary<string, object> parameters = null);
    }
}
