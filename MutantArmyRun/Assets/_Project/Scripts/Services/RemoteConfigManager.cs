using System.Threading.Tasks;
using MutantArmy.Core;
using UnityEngine;

namespace MutantArmy.Services
{
    /// <summary>
    /// Fachada de Remote Config (doc 12 §4.10): fonte de verdade de tuning em produção.
    /// O fetch real mora no IRemoteConfigProvider (Null no MVP; Firebase depois) — este
    /// manager só resolve o provider, garante o timeout e expõe GetInt/GetFloat/GetBool
    /// para o restante do assembly Services (Meta/Gameplay recebem o provider injetado,
    /// nunca este tipo concreto — direção de dependência do doc 12 §2.3).
    /// </summary>
    public class RemoteConfigManager : MonoBehaviour, IInitializable
    {
        public static RemoteConfigManager Instance { get; private set; }

        private IRemoteConfigProvider _provider;

        /// <summary>Chamado pelo GameBootstrap (doc 12 §3.3) via IInitializable.</summary>
        public void Init()
        {
            Init(ResolveProvider());
        }

        /// <summary>Overload com provider explícito — testes injetam um fake determinístico.</summary>
        public void Init(IRemoteConfigProvider provider)
        {
            Instance = this;
            _provider = provider;
        }

        /// <summary>
        /// Re-fetch fora do boot (ex.: volta de background): nunca segura o chamador além
        /// do timeout — falhou → cache → defaults embutidos (doc 12 §3.3/§4.10).
        /// </summary>
        public async Task InitAsync(bool online, int timeoutMs)
        {
            if (Instance == null) Instance = this;
            if (_provider == null) _provider = ResolveProvider();
            if (_provider == null || !online) return;
            Task fetch = _provider.InitAsync(online, timeoutMs);
            await Task.WhenAny(fetch, Task.Delay(timeoutMs));
        }

        public int GetInt(string key, int fallbackValue)
            => _provider != null ? _provider.GetInt(key, fallbackValue) : fallbackValue;

        public float GetFloat(string key, float fallbackValue)
            => _provider != null ? _provider.GetFloat(key, fallbackValue) : fallbackValue;

        public bool GetBool(string key, bool fallbackValue)
            => _provider != null ? _provider.GetBool(key, fallbackValue) : fallbackValue;

        private static IRemoteConfigProvider ResolveProvider()
        {
            return GameBootstrap.Current != null
                ? GameBootstrap.Current.GetComponentInChildren<IRemoteConfigProvider>(true)
                : null;
        }
    }
}
