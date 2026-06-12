using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MutantArmy.Core;
using MutantArmy.Domain;
using MutantArmy.Meta;

namespace MutantArmy.UI
{
    /// <summary>
    /// SCR-01 — menu inicial (doc 12 §2.2): moedas/gemas/próxima fase lidos do save no
    /// enable e mantidos por EVENTO (OnCurrencyChanged — zero polling, doc 12 §3.2).
    /// O botão JOGAR decide a fase pelo save (highestLevelCleared+1, cap do catálogo),
    /// carrega a cena Game e chama GameManager.StartLevel com o LevelConfigSO resolvido
    /// pelo GameSettingsSO de Resources (bootstrap, doc 12 §2.1). O StartLevel acontece
    /// no callback ESTÁTICO de sceneLoaded: este controller morre junto com a cena Main
    /// (load Single) — coroutine local nunca sobreviveria à troca.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button _playButton;
        [SerializeField] private TMP_Text _playLabel;
        [SerializeField] private TMP_Text _coinsText;
        [SerializeField] private TMP_Text _gemsText;
        [SerializeField] private TMP_Text _levelText;

        [Header("Telas de meta (push via UIManager) — ligadas pela MetaScreensFactory")]
        [SerializeField] private Button _troopsButton;
        [SerializeField] private Button _upgradesButton;
        [SerializeField] private Button _shopButton;
        [SerializeField] private Button _mapButton;
        [SerializeField] private Button _dailyButton;
        [SerializeField] private TroopsScreen _troopsScreen;
        [SerializeField] private UpgradesScreen _upgradesScreen;
        [SerializeField] private ShopScreen _shopScreen;
        [SerializeField] private MapScreen _mapScreen;
        [SerializeField] private DailyScreen _dailyScreen;

        [Header("Passe de Temporada (push a partir da Loja) — ligado pela RewardScreensFactory")]
        [SerializeField] private SeasonPassScreen _seasonPassScreen;

        [Header("Telas-sistema (push via UIManager) — ligadas pela SystemScreensFactory")]
        [SerializeField] private Button _settingsButton;   // engrenagem no canto
        [SerializeField] private Button _eventsButton;     // entrada para EVENTOS
        [SerializeField] private SettingsScreen _settingsScreen;
        [SerializeField] private EventsScreen _eventsScreen;

        private const string GameSceneName = "Game";   // const: sem string mágica espalhada (doc 12 §3.3)

        // Estático: sobrevive ao unload da cena Main enquanto a Game carrega.
        private static LevelConfigSO s_pendingLevel;

        private void Awake()
        {
            if (_playButton != null) _playButton.onClick.AddListener(OnPlayClicked);
            WireMetaNavigation();
        }

        /// <summary>
        /// NAVEGAÇÃO (brief F4 item 6): cada botão faz UIManager.Push da tela; cada tela tem
        /// VOLTAR (evento BackRequested → Pop). Tudo por evento, sem acoplamento — a tela não
        /// conhece o menu. O Mapa devolve a fase selecionada e o menu inicia a corrida.
        /// </summary>
        private void WireMetaNavigation()
        {
            BindNav(_troopsButton, _troopsScreen);
            BindNav(_upgradesButton, _upgradesScreen);
            BindNav(_shopButton, _shopScreen);
            BindNav(_mapButton, _mapScreen);
            BindNav(_dailyButton, _dailyScreen);
            // Telas-sistema (SystemScreensFactory): engrenagem → Configurações, botão → Eventos.
            BindNav(_settingsButton, _settingsScreen);
            BindNav(_eventsButton, _eventsScreen);

            if (_troopsScreen != null) _troopsScreen.BackRequested += PopScreen;
            if (_upgradesScreen != null) _upgradesScreen.BackRequested += PopScreen;
            if (_shopScreen != null) _shopScreen.BackRequested += PopScreen;
            if (_dailyScreen != null) _dailyScreen.BackRequested += PopScreen;
            if (_settingsScreen != null) _settingsScreen.BackRequested += PopScreen;
            if (_eventsScreen != null) _eventsScreen.BackRequested += PopScreen;
            if (_mapScreen != null)
            {
                _mapScreen.BackRequested += PopScreen;
                _mapScreen.WorldSelected += OnWorldSelected;
            }

            // Passe de Temporada: o botão PASSE da Loja dá Push na SeasonPassScreen; ela volta com Pop.
            if (_seasonPassScreen != null) _seasonPassScreen.BackRequested += PopScreen;
            if (_shopScreen != null && _seasonPassScreen != null)
                _shopScreen.SeasonPassRequested += OnSeasonPassRequested;
        }

        private void OnSeasonPassRequested()
        {
            if (_seasonPassScreen == null) return;
            if (UIManager.Instance != null) UIManager.Instance.Push(_seasonPassScreen);
            else _seasonPassScreen.Show();
        }

        private void BindNav(Button button, UIScreen screen)
        {
            if (button == null || screen == null) return;
            button.onClick.AddListener(() =>
            {
                if (UIManager.Instance != null) UIManager.Instance.Push(screen);
                else screen.Show();
            });
        }

        private static void PopScreen()
        {
            if (UIManager.Instance != null) UIManager.Instance.Pop();
        }

        /// <summary>Mapa: o jogador escolheu um mundo desbloqueado → inicia a fase resolvida.</summary>
        private void OnWorldSelected(int levelIndex)
        {
            if (UIManager.Instance != null) UIManager.Instance.Pop();   // fecha o Mapa
            StartLevelByIndex(levelIndex);
        }

        private void OnEnable()
        {
            GameEvents.OnCurrencyChanged += HandleCurrencyChanged;   // limpar em OnDisable (bus estático)
            Refresh();
        }

        private void OnDisable()
        {
            GameEvents.OnCurrencyChanged -= HandleCurrencyChanged;
        }

        private void HandleCurrencyChanged(CurrencyChange change)
        {
            Refresh();   // raro no menu (compra/baú) — re-render barato por evento
        }

        private void Refresh()
        {
            SaveData save = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
            if (_coinsText != null) _coinsText.text = (save != null ? save.coins : 0L).ToString();
            if (_gemsText != null) _gemsText.text = (save != null ? save.gems : 0).ToString();
            if (_levelText != null) _levelText.text = "FASE " + ResolveNextLevelIndex(save);
            if (_playLabel != null) _playLabel.text = "JOGAR";
        }

        private static int ResolveNextLevelIndex(SaveData save)
        {
            int highest = save != null ? save.highestLevelCleared : 0;
            GameSettingsSO settings = GameSettingsSO.Load();
            return settings != null ? settings.NextLevelIndex(highest) : Mathf.Max(1, highest + 1);
        }

        private void OnPlayClicked()
        {
            GameSettingsSO settings = GameSettingsSO.Load();
            if (settings == null) return;   // erro já logado pelo Load()

            SaveData save = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
            int nextIndex = settings.NextLevelIndex(save != null ? save.highestLevelCleared : 0);
            StartLevelByIndex(nextIndex);
        }

        /// <summary>
        /// Carrega a cena Game e inicia a fase do índice dado — caminho único do JOGAR (próxima
        /// fase) e do Mapa (mundo selecionado). Guard anti duplo-clique via s_pendingLevel.
        /// </summary>
        private void StartLevelByIndex(int levelIndex)
        {
            if (s_pendingLevel != null) return;   // load já em andamento: guard anti duplo-clique

            if (GameManager.Instance == null)
            {
                Debug.LogError("[MainMenuController] GameManager ausente — a cena Main precisa " +
                               "nascer via Boot (composition root, doc 12 §3.3).");
                return;
            }

            GameSettingsSO settings = GameSettingsSO.Load();
            if (settings == null) return;

            LevelConfigSO level = settings.GetLevel(levelIndex);
            if (level == null)
            {
                Debug.LogError($"[MainMenuController] Fase {levelIndex} ausente no catálogo " +
                               "(Resources/GameSettings) — rode MAR Tools/Create MVP Content.");
                return;
            }

            if (_playButton != null) _playButton.interactable = false;
            s_pendingLevel = level;
            SceneManager.sceneLoaded += HandleGameSceneLoaded;
            SceneManager.LoadSceneAsync(GameSceneName, LoadSceneMode.Single);
        }

        // Estático: roda DEPOIS do Awake/OnEnable dos objetos da cena Game — o
        // GameSceneBootstrap já registrou os managers e suas assinaturas de estado,
        // então o StartLevel (MainMenu→BossScout) encontra todo mundo ligado.
        private static void HandleGameSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != GameSceneName) return;
            SceneManager.sceneLoaded -= HandleGameSceneLoaded;

            LevelConfigSO level = s_pendingLevel;
            s_pendingLevel = null;
            if (level == null) return;

            if (GameManager.Instance != null) GameManager.Instance.StartLevel(level);
            else Debug.LogError("[MainMenuController] GameManager sumiu durante o load da cena Game.");
        }
    }
}
