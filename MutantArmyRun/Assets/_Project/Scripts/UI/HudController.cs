using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MutantArmy.Core;
using MutantArmy.Domain;
using MutantArmy.Meta;

namespace MutantArmy.UI
{
    /// <summary>
    /// HUD da corrida (doc 09 §4.2). Regra dura do doc 12 §3.2: UI atualiza POR EVENTO,
    /// nunca por frame — assina OnCrowdChanged/OnCurrencyChanged e só redesenha quando
    /// algo muda (zero polling em Update). Strings via StringBuilder cacheado (0 B/frame).
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [SerializeField] private TMP_Text _unitCountText;       // o número mais importante da tela
        [SerializeField] private TMP_Text _supplyText;          // "52/60"
        [SerializeField] private Image _supplyFill;
        [SerializeField] private TMP_Text _coinsText;           // carteira PERSISTENTE (save)
        [SerializeField] private TMP_Text _runCoinsText;        // RunCoins da corrida — visual DISTINTO (doc 12 §4.13)
        [SerializeField] private Image _progressFill;           // barra de progresso da pista (OnRunProgress)
        [SerializeField] private TMP_Text[] _mutationSlotTexts; // 3 slots rotativos (CANON §3.3)
        [SerializeField] private int _supplyCap = 60;           // CANON §15: cap fixo do MVP
        [SerializeField] private Color _supplyNormalColor = new Color(0.30f, 0.85f, 0.95f);
        [SerializeField] private Color _supplyWarnColor = new Color(1.00f, 0.75f, 0.15f);   // âmbar ≥ 80%
        [SerializeField] private float _punchScale = 1.15f;
        [SerializeField] private float _punchSeconds = 0.15f;

        // Source dos ganhos DENTRO da corrida (EconomySystem.EarnInRun): é delta da
        // RunWallet, ainda NÃO é saldo persistente — somar no contador da carteira
        // contaria o mesmo dinheiro duas vezes (de novo no "run_commit" do fim de fase).
        private const string RunEarnSource = "run_earn";

        private readonly StringBuilder _sb = new StringBuilder(24);
        private long _coins;
        private long _runCoins;
        private Coroutine _punch;
        private int _nextMutationSlot;   // rotação espelha o CrowdManager (CANON §3.3: a 4ª substitui a mais antiga)

        private void OnEnable()
        {
            GameEvents.OnCrowdChanged += HandleCrowdChanged;
            GameEvents.OnCurrencyChanged += HandleCurrencyChanged;
            GameEvents.OnLevelFinished += HandleLevelFinished;
            GameEvents.OnRunProgress += HandleRunProgress;
            GameEvents.OnMutationGained += HandleMutationGained;
            if (GameManager.Instance != null)
            {
                // -= antes de += : enable repetido não duplica a inscrição.
                GameManager.Instance.LevelStarted -= HandleLevelStarted;
                GameManager.Instance.LevelStarted += HandleLevelStarted;
            }

            // Leitura única do saldo no enable (não é polling); dali em diante o HUD
            // acompanha só os deltas dos eventos.
            if (EconomySystem.Instance != null)
            {
                _coins = EconomySystem.Instance.Coins;
                _runCoins = EconomySystem.Instance.RunCoins;
            }
            RenderCoins();
            RenderRunCoins();
        }

        private void OnDisable()
        {
            // Sempre limpar inscrições em OnDisable (regra do bus, doc 12 §3.2).
            GameEvents.OnCrowdChanged -= HandleCrowdChanged;
            GameEvents.OnCurrencyChanged -= HandleCurrencyChanged;
            GameEvents.OnLevelFinished -= HandleLevelFinished;
            GameEvents.OnRunProgress -= HandleRunProgress;
            GameEvents.OnMutationGained -= HandleMutationGained;
            if (GameManager.Instance != null)
                GameManager.Instance.LevelStarted -= HandleLevelStarted;
        }

        public void SetSupplyCap(int cap)
        {
            if (cap > 0) _supplyCap = cap;
        }

        private void HandleCrowdChanged(int count, int supplyUsed)
        {
            if (_unitCountText != null)
            {
                _sb.Length = 0;
                UIUtils.AppendCompactCount(_sb, count);
                _unitCountText.SetText(_sb);
                PunchCounter();
            }

            if (_supplyText != null)
            {
                _sb.Length = 0;
                _sb.Append(supplyUsed).Append('/').Append(_supplyCap);
                _supplyText.SetText(_sb);
            }

            if (_supplyFill != null)
            {
                float ratio = _supplyCap > 0 ? Mathf.Clamp01(supplyUsed / (float)_supplyCap) : 0f;
                _supplyFill.fillAmount = ratio;
                _supplyFill.color = ratio >= 0.8f ? _supplyWarnColor : _supplyNormalColor;   // doc 09 §4.2 (4)
            }
        }

        private void HandleCurrencyChanged(CurrencyChange change)
        {
            if (change.type != CurrencyType.Coin) return;

            // RunCoins (carteira temporária) num contador PRÓPRIO: "run_earn" nunca
            // entra no saldo persistente — o commit chega depois como "run_commit".
            if (change.source == RunEarnSource)
            {
                _runCoins += change.amount;
                if (_runCoins < 0) _runCoins = 0;
                RenderRunCoins();
                return;
            }

            _coins += change.amount;
            if (_coins < 0) _coins = 0;
            RenderCoins();
        }

        // Fim de fase: a RunWallet foi comitada (ou descartada) — zera o contador da corrida.
        private void HandleLevelFinished(LevelResult result)
        {
            _runCoins = 0;
            RenderRunCoins();
        }

        // Nova corrida (StartLevel): slots de mutação e progresso nascem limpos.
        private void HandleLevelStarted(int levelIndex)
        {
            _nextMutationSlot = 0;
            if (_mutationSlotTexts != null)
            {
                for (int i = 0; i < _mutationSlotTexts.Length; i++)
                    if (_mutationSlotTexts[i] != null) _mutationSlotTexts[i].text = string.Empty;
            }
            if (_progressFill != null) _progressFill.fillAmount = 0f;
        }

        private void HandleRunProgress(float progress01)
        {
            if (_progressFill != null) _progressFill.fillAmount = Mathf.Clamp01(progress01);
        }

        private void HandleMutationGained(MutationConfigSO mutation)
        {
            if (mutation == null || _mutationSlotTexts == null || _mutationSlotTexts.Length == 0) return;
            int slot = _nextMutationSlot % _mutationSlotTexts.Length;
            _nextMutationSlot = (slot + 1) % _mutationSlotTexts.Length;
            if (_mutationSlotTexts[slot] == null) return;
            // displayName PT-BR (MetaText resolve + fallback humanizado) — nunca a chave crua "MUT_ARMOR_NAME".
            _mutationSlotTexts[slot].text = MetaText.MutationName(mutation);
        }

        private void RenderCoins()
        {
            if (_coinsText == null) return;
            _sb.Length = 0;
            _sb.Append(_coins);
            _coinsText.SetText(_sb);
        }

        private void RenderRunCoins()
        {
            if (_runCoinsText == null) return;
            _sb.Length = 0;
            _sb.Append('+').Append(_runCoins);   // delta da corrida, distinto da carteira
            _runCoinsText.SetText(_sb);
        }

        private void PunchCounter()
        {
            if (!isActiveAndEnabled) return;
            if (_punch != null) StopCoroutine(_punch);
            _punch = StartCoroutine(PunchRoutine(_unitCountText.transform));
        }

        private IEnumerator PunchRoutine(Transform target)
        {
            float t = 0f;
            while (t < _punchSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = _punchSeconds > 0f ? Mathf.Clamp01(t / _punchSeconds) : 1f;
                float scale = Mathf.Lerp(_punchScale, 1f, k);
                target.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            target.localScale = Vector3.one;
            _punch = null;
        }
    }
}
