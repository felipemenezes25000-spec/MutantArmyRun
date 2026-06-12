using System;
using System.Threading.Tasks;
using MutantArmy.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MutantArmy.Core
{
    /// <summary>
    /// Contrato mínimo de init ordenado (doc 12 §3.3): cada manager expõe Instance
    /// somente-leitura atribuído DENTRO do Init() chamado pelo composition root —
    /// banido o auto-singleton setado em Awake (ordem de Awake é loteria de serialização).
    /// </summary>
    public interface IInitializable
    {
        void Init();
    }

    /// <summary>
    /// Composition root do projeto (doc 12 §3.3) — vive no prefab [Services] da cena Boot e é
    /// o ÚNICO lugar que registra e inicializa managers persistentes, em ordem explícita:
    /// Save → RemoteConfig → Analytics → Ads/IAP → Economy → Upgrade → Unit → Reward →
    /// Audio/VFX → UI. Os SDKs ficam atrás das interfaces de provider declaradas em Core
    /// (resolvidas por componente no próprio prefab) — o projeto compila e roda sem
    /// MAX/Firebase/RevenueCat, com os providers Null de MutantArmy.Services.
    /// Nenhum passo bloqueia além do timeout de 3 s; Main abre mesmo com SDKs pendentes.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        /// <summary>O root se auto-expõe ANTES de inicializar os managers — é o registrador,
        /// não um manager; a regra anti-singleton-em-Awake (§3.3) vale para os registrados.</summary>
        public static GameBootstrap Current { get; private set; }

        [Header("Estado de jogo")]
        [SerializeField] private GameManager _gameManager;

        [Header("1º da fila: save local (IInitializable — SaveSystem, nunca depende de rede)")]
        [SerializeField] private MonoBehaviour _saveService;

        [Header("Demais managers (IInitializable) na ORDEM canônica §3.3:\nIAP → Economy → Upgrade → Unit → Reward → Audio → VFX → UI")]
        [SerializeField] private MonoBehaviour[] _managersInOrder;

        // Cena por const: strings mágicas espalhadas são banidas (doc 12 §3.3).
        private const string MainSceneName = "Main";
        private const int NetworkTimeoutMs = 3000;

        /// <summary>Lidos do save ANTES dos SDKs: o SaveSystem (Meta) preenche estes valores
        /// no Init() dele via <see cref="Current"/> — Core não conhece o tipo concreto (§2.3).</summary>
        public bool AdsRemoved { get; set; }
        public string ConsentStatus { get; set; } = "unknown";

        /// <summary>Instância VIVA do save (SaveData é Domain — visível dos dois lados da
        /// fronteira Meta/Services). O SaveSystem preenche no Init() dele; Ads/IAP/Audio
        /// fazem BindSaveState a partir daqui para ler e zerar pacing/entitlements/
        /// preferências direto no save (doc 12 §4.8) — Save roda ANTES na ordem §3.3.</summary>
        public SaveData Save { get; set; }

        /// <summary>Dirty flag centralizado do SaveSystem (doc 12 §4.7) — managers que mutam
        /// o save via blackboard marcam por aqui; o flush real fica nas transições de estado.</summary>
        public Action MarkSaveDirty { get; set; }

        /// <summary>Fill de rewarded — publicado pelo AdsManager (Services) no Init dele.
        /// UI consome via blackboard porque UI não referencia Services (doc 12 §2.3);
        /// botão de rewarded só renderiza quando isto retorna true (doc 12 §7.3).</summary>
        public Func<bool> RewardedAdReady { get; set; }

        /// <summary>Exibição de rewarded (placement de <see cref="AdPlacement"/>, callback
        /// SEMPRE responde: true = recompensa concedida) — publicado pelo AdsManager.</summary>
        public Action<string, Action<bool>> ShowRewardedAd { get; set; }

        /// <summary>Entitlement do Passe de Temporada — publicado pelo IAPManager (Services) no Init dele.
        /// UI consome via blackboard porque UI não referencia Services (doc 12 §2.3); null = sem passe.
        /// O estado também vive no SaveData (seasonPassActive) — este hook é a fonte de verdade viva
        /// quando o IAP está montado.</summary>
        public Func<bool> SeasonPassOwned { get; set; }

        /// <summary>Compra de produto IAP por id (doc 11 §4.6: remove_ads_499 / season_pass_699). O
        /// callback SEMPRE responde: true = compra confirmada (entitlement concedido). Publicado pelo
        /// IAPManager; com provider Null responde false e a tela mostra "EM BREVE" (doc 12 §7.4).
        /// Assinatura: (productId, priceUsd, sourceScreen, onResult).</summary>
        public Action<string, float, string, Action<bool>> PurchaseProduct { get; set; }

        private void Awake()
        {
            Current = this;
        }

        private async void Start()
        {
            DontDestroyOnLoad(gameObject);                    // 1 chamada, no root [Services]
            _gameManager.Init();                              // pilha de estados nasce em Boot

            // Providers atrás de interface, resolvidos no prefab [Services]: a implementação
            // concreta (Null no MVP, SDK depois) é decidida pelo prefab, nunca por este código.
            IRemoteConfigProvider remoteConfig = GetComponentInChildren<IRemoteConfigProvider>(true);
            IAnalyticsProvider analytics = GetComponentInChildren<IAnalyticsProvider>(true);
            IAdsProvider ads = GetComponentInChildren<IAdsProvider>(true);

            // 1. SAVE (local, síncrono-rápido, NUNCA depende de rede) — define quem é o
            //    jogador (adsRemoved, consentimento) antes de qualquer SDK.
            InitService(_saveService);

            // 2. REMOTE CONFIG (timeout 3 s; falhou → cache → defaults embutidos)
            bool online = Application.internetReachability != NetworkReachability.NotReachable;
            if (remoteConfig != null) await remoteConfig.InitAsync(online, NetworkTimeoutMs);
            else Debug.LogError("[GameBootstrap] IRemoteConfigProvider ausente no prefab [Services].", this);

            // 3. ANALYTICS (consentimento lido do save; eventos pré-init ficam na fila)
            if (analytics != null) analytics.Init(online, ConsentStatus);
            else Debug.LogError("[GameBootstrap] IAnalyticsProvider ausente no prefab [Services].", this);

            // 4. ADS — só se o jogador não comprou Remover Anúncios (CANON §11)
            if (!AdsRemoved && ads != null) ads.Init();

            // 5. META + APRESENTAÇÃO, na ordem declarada no Inspector (a ordem do array É a ordem de init)
            for (int i = 0; i < _managersInOrder.Length; i++) InitService(_managersInOrder[i]);

            // 6. UI — Main abre mesmo que 2–4 ainda estejam pendentes em background
            AsyncOperation load = SceneManager.LoadSceneAsync(MainSceneName, LoadSceneMode.Single);
            while (load != null && !load.isDone) await Task.Yield();
            _gameManager.ChangeState(GameState.MainMenu);
        }

        private void InitService(MonoBehaviour candidate)
        {
            if (candidate == null)
            {
                Debug.LogError("[GameBootstrap] Campo de manager vazio no prefab [Services] — ver ordem canônica §3.3.", this);
                return;
            }
            if (candidate is IInitializable initializable) initializable.Init();
            else Debug.LogError($"[GameBootstrap] {candidate.GetType().Name} não implementa IInitializable.", candidate);
        }
    }
}
