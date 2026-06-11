using System.Threading.Tasks;

namespace MutantArmy.Core
{
    /// <summary>
    /// Contrato de Remote Config (doc 12 §4.10). Declarado em Core (§2.3); MutantArmy.Services
    /// implementa (NullRemoteConfigProvider devolve os defaults embutidos que espelham o CANON —
    /// o jogo é jogável de fábrica, sem rede). Chaves SEMPRE via <see cref="RcKeys"/>.
    /// </summary>
    public interface IRemoteConfigProvider
    {
        /// <summary>Nunca segura o boot além do timeout; falhou → cache → defaults (§3.3).</summary>
        Task InitAsync(bool online, int timeoutMs);

        int GetInt(string key, int fallbackValue);
        float GetFloat(string key, float fallbackValue);
        bool GetBool(string key, bool fallbackValue);
    }
}
