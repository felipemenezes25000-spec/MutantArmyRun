using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
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

        private const string MainSceneName = "Main";   // const: sem string mágica (doc 12 §3.3)

        private int _unitsLostThisRun;
        private LevelResult _lastResult;
        private bool _hasResult;

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
        }

        private void HandleLevelStarted(int levelIndex)
        {
            _unitsLostThisRun = 0;
            _hasResult = false;
            CloseResult();   // re-start vindo de fora (cheat/AutoPilot) não deixa tela órfã
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
        }

        private void HandleLevelFinished(LevelResult result)
        {
            _lastResult = result;
            _hasResult = true;
            StartCoroutine(ShowResultNextFrame());
        }

        private IEnumerator ShowResultNextFrame()
        {
            yield return null;   // troca de tela deferida 1 frame (doc 12 §3.2)
            if (_resultScreen == null || !_hasResult) yield break;

            LevelResult r = _lastResult;
            // Derrota descarta as moedas da corrida (doc 12 §4.6) — o delta exibido é 0;
            // a XP aparece sempre (comitada vitória ou derrota).
            long coinsDelta = r.won ? r.runCoins : 0L;
            bool perfect = r.won && _unitsLostThisRun == 0;
            _resultScreen.Bind(r.won, coinsDelta, r.runXp, r.survivors, (long)r.damageDealt, perfect);

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

            // Vitória: fase seguinte (cap do catálogo trava na 20); derrota: retry da MESMA
            // fase. Os dois re-iniciam NA MESMA cena — StartLevel→BossScout→Running refaz a
            // pista via soft reset do LevelManager (doc 12 §2.2: nunca SceneManager.LoadScene).
            int current = _lastResult.levelIndex;
            int nextIndex = _lastResult.won
                ? Mathf.Min(current + 1, Mathf.Max(1, settings.MaxLevelIndex))
                : Mathf.Max(1, current);
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
