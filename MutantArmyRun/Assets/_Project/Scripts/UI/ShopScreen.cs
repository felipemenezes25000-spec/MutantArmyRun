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
    /// SCR-08 — Loja (doc 09 §4.8). Honesta sobre o estado do MVP: IAP é provider Null, então
    /// pacotes de moeda/gema, Remover Anúncios e Passe de Temporada são EXIBIÇÃO com preço +
    /// selo "EM BREVE" (P9: preço nunca escondido, nada de trapaça). O que JÁ funciona pelo
    /// contrato real do RewardSystem: baú grátis diário (TryClaimDailyChest) e baú por gemas
    /// (TryBuyChest) — ambos transacionais e com ChestResult de verdade. O resultado do baú
    /// aparece como faixa de resumo no topo (moedas/gemas/fragmentos efetivamente concedidos).
    /// </summary>
    public class ShopScreen : UIScreen
    {
        [Header("Cabeçalho")]
        [SerializeField] private TMP_Text _coinsText;
        [SerializeField] private TMP_Text _gemsText;
        [SerializeField] private Button _backButton;

        [Header("Faixa de resultado (baú aberto)")]
        [SerializeField] private TMP_Text _resultBanner;

        [Header("Baú grátis diário")]
        [SerializeField] private Button _freeChestButton;
        [SerializeField] private TMP_Text _freeChestLabel;

        [Header("Baú por gemas")]
        [SerializeField] private Button _gemChestButton;
        [SerializeField] private TMP_Text _gemChestLabel;
        [SerializeField] private int _gemChestPrice = 300;            // CANON §8: baú raro = 300 gemas

        [Header("Placeholders de IAP (provider Null)")]
        [SerializeField] private TMP_Text _removeAdsLabel;
        [SerializeField] private TMP_Text _seasonPassLabel;

        public event System.Action BackRequested;

        private readonly StringBuilder _sb = new StringBuilder(96);

        private static readonly Color Green = new Color(0.25f, 0.80f, 0.35f);
        private static readonly Color Grey = new Color(0.35f, 0.38f, 0.45f);
        private static readonly Color Cyan = new Color(0.20f, 0.75f, 1.00f);

        protected override void Awake()
        {
            base.Awake();
            if (_backButton != null) _backButton.onClick.AddListener(() => { if (BackRequested != null) BackRequested(); });
            if (_freeChestButton != null) _freeChestButton.onClick.AddListener(OnFreeChest);
            if (_gemChestButton != null) _gemChestButton.onClick.AddListener(OnGemChest);
        }

        private void OnEnable() { GameEvents.OnCurrencyChanged += HandleCurrencyChanged; }
        private void OnDisable() { GameEvents.OnCurrencyChanged -= HandleCurrencyChanged; }

        private void HandleCurrencyChanged(CurrencyChange c) { RefreshHeader(); RefreshButtons(); }

        protected override void OnShown()
        {
            base.OnShown();
            RefreshHeader();
            RefreshButtons();
            if (_resultBanner != null) _resultBanner.gameObject.SetActive(false);

            // Preços de IAP visíveis + selo "EM BREVE" (honestidade — P9).
            if (_removeAdsLabel != null) _removeAdsLabel.text = "SEM ANÚNCIOS  US$ 4,99\n+200 gemas de bônus  (EM BREVE)";
            if (_seasonPassLabel != null) _seasonPassLabel.text = "PASSE DE TEMPORADA  US$ 6,99/mês  (EM BREVE)";
        }

        private void RefreshHeader()
        {
            if (_coinsText != null) _coinsText.text = MetaText.Coins(MetaBridge.Coins);
            if (_gemsText != null) _gemsText.text = MetaBridge.Gems.ToString();
        }

        private void RefreshButtons()
        {
            // Baú grátis: habilitado quando disponível (1×/dia UTC).
            bool freeReady = MetaBridge.CanClaimDailyChest();
            if (_freeChestButton != null)
            {
                _freeChestButton.interactable = freeReady;
                var img = _freeChestButton.GetComponent<Image>();
                if (img != null) img.color = freeReady ? Green : Grey;
            }
            if (_freeChestLabel != null)
                _freeChestLabel.text = freeReady ? "ABRIR BAÚ GRÁTIS" : "BAÚ GRÁTIS  (volte amanhã)";

            // Baú por gemas.
            bool canAfford = MetaBridge.Gems >= _gemChestPrice;
            if (_gemChestButton != null)
            {
                _gemChestButton.interactable = canAfford;
                var img = _gemChestButton.GetComponent<Image>();
                if (img != null) img.color = canAfford ? Cyan : Grey;
            }
            if (_gemChestLabel != null)
                _gemChestLabel.text = "BAÚ RARO  " + _gemChestPrice + " gemas";
        }

        private void OnFreeChest()
        {
            ChestResult result;
            if (MetaBridge.TryClaimDailyChest(out result))
            {
                ShowResult("Baú grátis aberto!", result);
                RefreshHeader();
                RefreshButtons();
            }
        }

        private void OnGemChest()
        {
            ChestResult result;
            if (MetaBridge.TryBuyChest(MetaBridge.ShopChest.Rare, _gemChestPrice, out result))
            {
                ShowResult("Baú raro aberto!", result);
                RefreshHeader();
                RefreshButtons();
            }
        }

        /// <summary>
        /// Resumo HONESTO a partir do ChestResult real (RewardSystem.OpenChest): moedas/gemas
        /// creditadas e fragmentos por tropa. Sem inventar números — o que entrou foi exatamente
        /// isto (drop tables do Domain.ChestMath, P9).
        /// </summary>
        private void ShowResult(string title, ChestResult result)
        {
            if (_resultBanner == null) return;
            _sb.Length = 0;
            _sb.Append(title);
            if (result.coins > 0) _sb.Append("  +").Append(result.coins).Append(" moedas");
            if (result.gems > 0) _sb.Append("  +").Append(result.gems).Append(" gemas");

            int cards = result.unitIds != null ? result.unitIds.Length : 0;
            if (cards > 0)
            {
                _sb.Append('\n');
                int shown = 0;
                for (int i = 0; i < cards && shown < 3; i++)
                {
                    int s = result.shards != null && i < result.shards.Length ? result.shards[i] : 0;
                    if (s <= 0) continue;
                    if (shown > 0) _sb.Append("  ");
                    _sb.Append('+').Append(s).Append(" frag ").Append(UnitLabel(result.unitIds[i]));
                    shown++;
                }
                if (shown == 0) _sb.Append("(fragmentos para as Tropas)");
            }
            _resultBanner.text = _sb.ToString();
            _resultBanner.gameObject.SetActive(true);
        }

        private static string UnitLabel(string unitId)
        {
            UnitConfigSO cfg = UnitManager.Instance != null ? UnitManager.Instance.GetConfig(unitId) : null;
            return cfg != null ? MetaText.UnitName(cfg) : MetaText.Humanize(unitId);
        }
    }
}
