using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MutantArmy.Core;
using MutantArmy.Domain;
using MutantArmy.Meta;

namespace MutantArmy.UI
{
    /// <summary>
    /// Glue de UI da cena Game (doc 12 §4.13): assina OnLevelFinished e mostra a
    /// ResultScreen (passiva) com o DELTA da corrida no frame SEGUINTE — listeners de
    /// dados (commit/save) rodam antes da troca de tela ("dados prontos → tela mostra",
    /// doc 12 §3.2). Orquestra os pedidos da tela:
    /// - PRÓXIMA FASE: re-inicia NA MESMA cena (soft reset via StartLevel — Victory/
    ///   Defeat→BossScout na tabela do Domain; derrota = retry da mesma fase);
    /// - MENU: ChangeState(MainMenu) + load da cena Main;
    /// - DOBRAR ×2: rewarded via hook do blackboard (UI não referencia Services, §2.3) e
    ///   crédito por EconomySystem.GrantRunDouble SÓ no sucesso (CANON §11).
    /// Também é o glue que dispara o PERFECT (FeedbackTextController.NotifyArenaReached)
    /// ao entrar em BossFight — contagem própria de mortes, imune à ordem dos listeners.
    /// </summary>
    public class GameUIController : MonoBehaviour
    {
        [SerializeField] private ResultScreen _resultScreen;
        [SerializeField] private FeedbackTextController _feedback;

        [Header("Pausa (OVL-SISTEMA) — ligada pela SystemScreensFactory")]
        [SerializeField] private Button _pauseButton;       // botão no HUD (canto superior)
        [SerializeField] private PauseOverlay _pauseOverlay; // overlay modal sobre o HUD
        [SerializeField] private SettingsScreen _settingsScreen; // opcional: Configurações a partir da pausa

        private const string MainSceneName = "Main";   // const: sem string mágica (doc 12 §3.3)

        private int _unitsLostThisRun;
        private LevelResult _lastResult;
        private bool _hasResult;
        private bool _paused;

        private void OnEnable()
        {
            GameEvents.OnLevelFinished += HandleLevelFinished;
            GameEvents.OnUnitDied += HandleUnitDied;

            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.StateEntered -= HandleStateEntered;   // -= antes de += : enable repetido não duplica
                gm.StateEntered += HandleStateEntered;
                gm.LevelStarted -= HandleLevelStarted;
                gm.LevelStarted += HandleLevelStarted;
            }

            if (_resultScreen != null)
            {
                _resultScreen.NextRequested += HandleNextRequested;
                _resultScreen.HomeRequested += HandleHomeRequested;
                _resultScreen.DoubleRequested += HandleDoubleRequested;
            }

            // Pausa: o botão do HUD abre o overlay; o overlay despacha eventos de ação. O
            // AutoPilot NUNCA chama isto — só o toque humano no botão (doc do brief item 2).
            if (_pauseButton != null) _pauseButton.onClick.AddListener(OpenPause);
            if (_pauseOverlay != null)
            {
                _pauseOverlay.ResumeRequested += HandleResume;
                _pauseOverlay.RestartRequested += HandlePauseRestart;
                _pauseOverlay.SettingsRequested += HandlePauseSettings;
                _pauseOverlay.MenuRequested += HandlePauseMenu;
            }
            UpdatePauseButtonVisibility();
        }

        private void OnDisable()
        {
            GameEvents.OnLevelFinished -= HandleLevelFinished;   // bus estático: sempre limpar (doc 12 §3.2)
            GameEvents.OnUnitDied -= HandleUnitDied;

            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.StateEntered -= HandleStateEntered;
                gm.LevelStarted -= HandleLevelStarted;
            }

            if (_resultScreen != null)
            {
                _resultScreen.NextRequested -= HandleNextRequested;
                _resultScreen.HomeRequested -= HandleHomeRequested;
                _resultScreen.DoubleRequested -= HandleDoubleRequested;
            }

            if (_pauseButton != null) _pauseButton.onClick.RemoveListener(OpenPause);
            if (_pauseOverlay != null)
            {
                _pauseOverlay.ResumeRequested -= HandleResume;
                _pauseOverlay.RestartRequested -= HandlePauseRestart;
                _pauseOverlay.SettingsRequested -= HandlePauseSettings;
                _pauseOverlay.MenuRequested -= HandlePauseMenu;
            }
            // segurança: se o objeto for desabilitado pausado (troca de cena), destrava o tempo
            if (_paused) { Time.timeScale = 1f; _paused = false; }
        }

        private void HandleLevelStarted(int levelIndex)
        {
            _unitsLostThisRun = 0;
            _hasResult = false;
            CloseResult();   // re-start vindo de fora (cheat/AutoPilot) não deixa tela órfã
            ForceClosePause();   // nova fase nunca herda pausa da anterior
        }

        private void HandleUnitDied(UnitDeath death)
        {
            _unitsLostThisRun++;
        }

        private void HandleStateEntered(GameState s)
        {
            // PERFECT dispara ao chegar à arena sem perder unidade — não há evento de
            // "arena alcançada" no bus; este glue é o chamador previsto pelo controller.
            if (s == GameState.BossFight && _feedback != null && _unitsLostThisRun == 0)
                _feedback.NotifyArenaReached();

            // O pause só faz sentido DURANTE a corrida (Running/BossFight). Em telas/menus
            // (BossScout, Victory, Defeat, ReviveOffer) o botão some — e se sairmos da corrida
            // com a pausa aberta (não deveria), destravamos o tempo.
            UpdatePauseButtonVisibility();
            if (_paused && s != GameState.Running && s != GameState.BossFight) ForceClosePause();
        }

        private void UpdatePauseButtonVisibility()
        {
            if (_pauseButton == null) return;
            GameManager gm = GameManager.Instance;
            bool inRun = gm != null && (gm.State == GameState.Running || gm.State == GameState.BossFight);
            // esconde quando há resultado na tela ou já está pausado (o overlay tem o CONTINUAR)
            bool show = inRun && !_paused && !_hasResult;
            if (_pauseButton.gameObject.activeSelf != show) _pauseButton.gameObject.SetActive(show);
        }

        private void HandleLevelFinished(LevelResult result)
        {
            _lastResult = result;
            _hasResult = true;
            ForceClosePause();              // fim de fase nunca convive com a pausa aberta
            UpdatePauseButtonVisibility();  // some o botão de pause sob o resultado
            StartCoroutine(ShowResultNextFrame());
        }

        private IEnumerator ShowResultNextFrame()
        {
            yield return null;   // troca de tela deferida 1 frame (doc 12 §3.2)
            if (_resultScreen == null || !_hasResult) yield break;

            LevelResult r = _lastResult;
            // Vitória exibe o TOTAL ganho na fase (recompensa de fase + moedas da corrida,
            // preenchido no ResolveEnd); derrota descarta as moedas (coinsAwarded já é 0).
            // A XP exibida é a realmente ganha (xpAwarded). O "DOBRAR x2" dobra SÓ o delta
            // da corrida (r.runCoins) — a recompensa de fase nunca é dobrada (CANON §11).
            long coinsDelta = r.won ? r.coinsAwarded : 0L;
            long doubleBase = r.won ? r.runCoins : 0L;
            bool perfect = r.won && _unitsLostThisRun == 0;
            // motivo da derrota (doc 09 §4.5): o GameManager o registrou (CrowdManager no wipe).
            string defeatReason = r.won ? null : DefeatReasonText();
            _resultScreen.Bind(r.won, coinsDelta, r.xpAwarded, r.survivors, (long)r.damageDealt, perfect,
                               doubleBase, defeatReason);

            GameBootstrap root = GameBootstrap.Current;
            bool doubleReady = root != null && root.RewardedAdReady != null && root.RewardedAdReady();
            _resultScreen.SetDoubleAvailable(doubleReady);   // sem fill o botão SOME (doc 12 §7.3)

            if (UIManager.Instance != null) UIManager.Instance.Push(_resultScreen);
            else _resultScreen.Show();
        }

        private void HandleNextRequested()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || !_hasResult) return;

            GameSettingsSO settings = GameSettingsSO.Load();
            if (settings == null) return;   // erro já logado pelo Load()

            // Vitória: fase seguinte SEM teto (o endless do GameSettingsSO gera além da campanha
            // desenhada de 100); derrota: retry da MESMA fase. Os dois re-iniciam NA MESMA cena —
            // StartLevel→BossScout→Running refaz a pista via soft reset do LevelManager
            // (doc 12 §2.2: nunca SceneManager.LoadScene).
            int current = _lastResult.levelIndex;
            int nextIndex = settings.NextLevelAfter(current, _lastResult.won);
            LevelConfigSO level = settings.GetLevel(nextIndex);
            if (level == null)
            {
                Debug.LogError($"[GameUIController] Fase {nextIndex} ausente no catálogo (Resources/GameSettings).");
                return;
            }

            _hasResult = false;
            CloseResult();
            gm.StartLevel(level);
        }

        private void HandleHomeRequested()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;

            _hasResult = false;
            CloseResult();
            gm.ChangeState(GameState.MainMenu);   // Victory/Defeat→MainMenu (tabela do Domain)
            SceneManager.LoadSceneAsync(MainSceneName, LoadSceneMode.Single);
        }

        private void HandleDoubleRequested(long baseCoins)
        {
            GameBootstrap root = GameBootstrap.Current;
            if (root == null || root.ShowRewardedAd == null)
            {
                if (_resultScreen != null) _resultScreen.CancelDoubleRequest();
                return;
            }

            root.ShowRewardedAd(AdPlacement.DoubleReward, granted =>
            {
                if (_resultScreen == null) return;
                if (granted)
                {
                    // Crédito do MESMO delta uma 2ª vez (a RunWallet já foi comitada e
                    // zerada no ResolveEnd) — contrato do EconomySystem.GrantRunDouble.
                    if (EconomySystem.Instance != null) EconomySystem.Instance.GrantRunDouble(baseCoins);
                    _resultScreen.ConfirmDoubled();
                }
                else
                {
                    _resultScreen.CancelDoubleRequest();   // falhou/abandonou: destrava sem conceder
                }
            });
        }

        // ---------------------------------------------------------------- pausa (OVL-SISTEMA)

        // Abre a pausa SOBRE o HUD: congela o tempo (timeScale 0 — a UI roda em UNSCALED, então o
        // fade do overlay segue) e sobe pela pilha de overlays do UIManager (a tela embaixo segue
        // intacta). Só durante a corrida; ignora clique se já pausado/sem corrida. O timeScale é
        // controlado aqui (não no overlay) porque o UIManager.ShowOverlay chama Show() pela base.
        private void OpenPause()
        {
            if (_pauseOverlay == null || _paused) return;
            GameManager gm = GameManager.Instance;
            if (gm == null || (gm.State != GameState.Running && gm.State != GameState.BossFight)) return;

            _paused = true;
            Time.timeScale = 0f;
            UpdatePauseButtonVisibility();
            if (UIManager.Instance != null) UIManager.Instance.ShowOverlay(_pauseOverlay);
            else _pauseOverlay.Show();
        }

        // Fecha o overlay e restaura o tempo (sempre 1 — nenhum slow motion canônico sobrevive a
        // uma pausa: o pause só existe em Running/BossFight, fora do slow-mo de vitória).
        private void ClosePause()
        {
            if (!_paused) return;
            _paused = false;
            Time.timeScale = 1f;
            if (_pauseOverlay != null)
            {
                if (UIManager.Instance != null
                    && ReferenceEquals(UIManager.Instance.CurrentOverlay, _pauseOverlay))
                    UIManager.Instance.PopOverlay();
                else if (_pauseOverlay.IsVisible)
                    _pauseOverlay.Hide();
            }
            UpdatePauseButtonVisibility();
        }

        // Garante tempo normal mesmo se o overlay sumir por troca de cena/estado sem passar pelo
        // fluxo normal (fim de fase, troca de estado com a pausa aberta).
        private void ForceClosePause()
        {
            if (!_paused) { UpdatePauseButtonVisibility(); return; }
            ClosePause();
        }

        private void HandleResume()
        {
            ClosePause();
        }

        // REINICIAR FASE: fecha a pausa (restaura o tempo) e refaz a fase atual por transições
        // legais a partir de qualquer estado (mesmo soft-reset do showcase). Sem UI/cena nova.
        private void HandlePauseRestart()
        {
            ClosePause();
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.CurrentLevel == null) return;
            gm.RestartLevelFromAnyState(gm.CurrentLevel);
        }

        // CONFIGURAÇÕES a partir da pausa: se houver uma SettingsScreen na cena (ligada pela
        // factory), empilha-a sobre o overlay; senão, no-op gracioso (a cena Game não tem a
        // tela de Settings no MVP — a engrenagem do menu é o caminho principal).
        private void HandlePauseSettings()
        {
            if (_settingsScreen == null) return;
            if (UIManager.Instance != null) UIManager.Instance.Push(_settingsScreen);
            else _settingsScreen.Show();
        }

        // MENU: abandona a corrida, volta o tempo, vai para o estado MainMenu e carrega a Main.
        // Running/BossFight→MainMenu é ilegal na tabela (só pós-fase), então usa o reset de pilha
        // do GameManager (GoToMainMenuFromAnyState) — abandono sem ResolveEnd, igual ao showcase.
        private void HandlePauseMenu()
        {
            ClosePause();
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            _hasResult = false;
            CloseResult();
            gm.GoToMainMenuFromAnyState();
            SceneManager.LoadSceneAsync(MainSceneName, LoadSceneMode.Single);
        }

        // Texto PT-BR do motivo da derrota (doc 09 §4.5) lido do GameManager.
        private static string DefeatReasonText()
        {
            GameManager gm = GameManager.Instance;
            DefeatReason reason = gm != null ? gm.LastDefeatReason : DefeatReason.None;
            switch (reason)
            {
                case DefeatReason.ArmyWiped: return "Exército eliminado";
                case DefeatReason.BossWon: return "O boss venceu";
                default: return "Exército eliminado";   // fallback seguro: derrota sem motivo gravado
            }
        }

        private void CloseResult()
        {
            if (_resultScreen == null) return;
            if (UIManager.Instance != null
                && ReferenceEquals(UIManager.Instance.CurrentScreen, _resultScreen))
            {
                UIManager.Instance.Pop();   // a pilha do UIManager fica limpa para o próximo push
            }
            else if (_resultScreen.IsVisible)
            {
                _resultScreen.Hide();
            }
        }
    }
}
