using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MutantArmy.Core;
using MutantArmy.Domain;

namespace MutantArmy.UI
{
    /// <summary>
    /// SCR — Passe de Temporada (brief F-SeasonPass): trilha de ~30 níveis com DUAS faixas lado a
    /// lado (GRÁTIS vs PREMIUM), barra de progresso do passe (XP derivada de fases vencidas/missões
    /// via MetaBridge, determinística/local), botão COMPRAR PASSE (IAP provider Null → "EM BREVE" +
    /// US$ 6,99) e botão COLETAR que resgata as recompensas atingidas pelo funil REAL da Meta.
    ///
    /// Estilo casual premium: fundo OPACO (Panel da factory), scroll com RectMask2D (NUNCA Mask
    /// stencil — invisível offscreen no -menuShowcase), botões com UIButtonPop (ScalePop). As linhas
    /// da trilha são DATA-DRIVEN em runtime (o catálogo de níveis só é conhecido em play), exatamente
    /// como TroopsScreen/DailyScreen. A factory monta header + scroll vazio + barra + botões e liga.
    /// </summary>
    public class SeasonPassScreen : UIScreen
    {
        [Header("Cabeçalho")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Button _backButton;

        [Header("Progresso do passe")]
        [SerializeField] private TMP_Text _tierText;            // "NÍVEL 7 / 30"
        [SerializeField] private RectTransform _xpFill;         // largura = fração da XP no nível atual
        [SerializeField] private TMP_Text _xpText;              // "40 / 100 XP"

        [Header("Trilha (linhas em runtime)")]
        [SerializeField] private RectTransform _tracksContent;  // VerticalLayoutGroup; uma linha por nível

        [Header("Ações")]
        [SerializeField] private Button _buyButton;
        [SerializeField] private TMP_Text _buyLabel;
        [SerializeField] private Button _claimButton;
        [SerializeField] private TMP_Text _claimLabel;

        /// <summary>VOLTAR → UIManager.Pop (ligado pelo MainMenuController, como as demais telas).</summary>
        public event System.Action BackRequested;

        private readonly List<TierRow> _rows = new List<TierRow>();
        private bool _built;
        private bool _purchasePending;

        private static readonly Color Gold = new Color(1.00f, 0.86f, 0.40f);
        private static readonly Color Green = new Color(0.25f, 0.80f, 0.35f);
        private static readonly Color Grey = new Color(0.35f, 0.38f, 0.45f);
        private static readonly Color TextSoft = new Color(0.80f, 0.85f, 0.90f);
        private static readonly Color RowBg = new Color(0.10f, 0.12f, 0.20f, 0.96f);
        private static readonly Color RowReached = new Color(0.14f, 0.24f, 0.20f, 0.97f);
        private static readonly Color FreeChip = new Color(0.16f, 0.30f, 0.42f, 0.98f);
        private static readonly Color PremiumChip = new Color(0.28f, 0.16f, 0.40f, 0.98f);
        private static readonly Color PremiumLocked = new Color(0.16f, 0.16f, 0.22f, 0.96f);

        protected override void Awake()
        {
            base.Awake();
            if (_backButton != null) _backButton.onClick.AddListener(() => { if (BackRequested != null) BackRequested(); });
            if (_buyButton != null) _buyButton.onClick.AddListener(OnBuyClicked);
            if (_claimButton != null) _claimButton.onClick.AddListener(OnClaimClicked);
        }

        private void OnEnable() { GameEvents.OnCurrencyChanged += HandleCurrencyChanged; }
        private void OnDisable() { GameEvents.OnCurrencyChanged -= HandleCurrencyChanged; }

        private void HandleCurrencyChanged(CurrencyChange c) { Refresh(); }

        protected override void OnShown()
        {
            base.OnShown();
            if (_titleText != null) _titleText.text = "PASSE DE TEMPORADA";
            EnsureBuilt();
            Refresh();
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            if (_tracksContent == null) return;

            IReadOnlyList<MetaBridge.SeasonTierView> tiers = MetaBridge.SeasonTiers();
            for (int i = 0; i < tiers.Count; i++)
                _rows.Add(BuildTierRow(tiers[i]));
        }

        private void Refresh()
        {
            MetaBridge.SeasonPassView pass = MetaBridge.GetSeasonPass();

            if (_tierText != null) _tierText.text = "NÍVEL " + pass.currentTier + " / " + pass.tierCount;
            if (_xpFill != null)
            {
                _xpFill.anchorMax = new Vector2(Mathf.Clamp01(pass.tierProgress01), 1f);
                _xpFill.offsetMin = Vector2.zero;
                _xpFill.offsetMax = Vector2.zero;
            }
            if (_xpText != null) _xpText.text = pass.xpIntoTier + " / " + pass.xpPerTier + " XP";

            IReadOnlyList<MetaBridge.SeasonTierView> tiers = MetaBridge.SeasonTiers();
            for (int i = 0; i < _rows.Count && i < tiers.Count; i++)
                _rows[i].Refresh(tiers[i], pass.owned);

            RefreshBuyButton(pass);
            RefreshClaimButton();
        }

        private void RefreshBuyButton(MetaBridge.SeasonPassView pass)
        {
            if (_buyButton == null) return;
            // Passe já ativo: vira selo "PASSE ATIVO" desabilitado. Sem SDK, a compra responde false
            // e a tela mantém o preço + "EM BREVE" (honestidade P9 / doc 12 §7.4).
            if (pass.owned)
            {
                _buyButton.interactable = false;
                var img = _buyButton.GetComponent<Image>();
                if (img != null) img.color = Green;
                if (_buyLabel != null) _buyLabel.text = "PASSE ATIVO";
                return;
            }

            _buyButton.interactable = !_purchasePending;
            var bimg = _buyButton.GetComponent<Image>();
            if (bimg != null) bimg.color = _purchasePending ? Grey : Gold;
            if (_buyLabel != null)
                _buyLabel.text = _purchasePending
                    ? "PROCESSANDO..."
                    : "COMPRAR PASSE  US$ " + pass.priceUsd.ToString("0.00") + "  (EM BREVE)";
        }

        private void RefreshClaimButton()
        {
            if (_claimButton == null) return;
            bool canClaim = MetaBridge.CanClaimSeasonRewards();
            _claimButton.interactable = canClaim;
            var img = _claimButton.GetComponent<Image>();
            if (img != null) img.color = canClaim ? Green : Grey;
            if (_claimLabel != null) _claimLabel.text = canClaim ? "COLETAR RECOMPENSAS" : "TUDO COLETADO";
        }

        private void OnBuyClicked()
        {
            if (_purchasePending || MetaBridge.SeasonPassOwned()) return;
            _purchasePending = true;
            RefreshBuyButton(MetaBridge.GetSeasonPass());

            // UI não enxerga Services: a compra passa pelo blackboard de IAP (MetaBridge → GameBootstrap).
            // Com provider Null o resultado é false e a trilha premium continua bloqueada (sem fingir).
            MetaBridge.TryBuySeasonPass(granted =>
            {
                _purchasePending = false;
                Refresh();
            });
        }

        private void OnClaimClicked()
        {
            long coins; int gems, shards, skins;
            if (!MetaBridge.TryClaimSeasonRewards(out coins, out gems, out shards, out skins)) { Refresh(); return; }

            // Pop tátil no botão + celebração reutilizável (se montada na cena).
            Tween.PunchScale(_claimButton != null ? _claimButton.transform : transform, 0.25f, 0.25f);
            if (CelebrationOverlay.Instance != null)
                CelebrationOverlay.Instance.ShowSeasonReward(coins, gems, shards, skins);
            Refresh();
        }

        // ---------------------------------------------------------------- linha da trilha

        private TierRow BuildTierRow(MetaBridge.SeasonTierView t)
        {
            var go = new GameObject("Tier" + t.tier, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_tracksContent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 150f; le.preferredHeight = 150f;
            var bg = go.GetComponent<Image>();
            bg.color = RowBg; bg.raycastTarget = false;

            // Selo do nível (esquerda).
            TMP_Text tierLabel = Label(rect, "TierNum", t.tier.ToString(), 40f, new Vector2(0f, 0.5f),
                new Vector2(70f, 0f), new Vector2(120f, 120f), Gold, TextAlignmentOptions.Center);

            // Chip GRÁTIS (centro-esquerda).
            Image freeBg;
            TMP_Text freeLabel = Chip(rect, "Free", "GRÁTIS", new Vector2(0.5f, 0.5f),
                new Vector2(-180f, 0f), FreeChip, out freeBg);

            // Chip PREMIUM (centro-direita).
            Image premBg;
            TMP_Text premLabel = Chip(rect, "Premium", "PREMIUM", new Vector2(0.5f, 0.5f),
                new Vector2(180f, 0f), PremiumChip, out premBg);

            var row = new TierRow
            {
                tier = t.tier,
                rowBg = bg,
                tierLabel = tierLabel,
                freeBg = freeBg, freeLabel = freeLabel,
                premBg = premBg, premLabel = premLabel
            };
            return row;
        }

        private TMP_Text Chip(Transform parent, string name, string title, Vector2 anchor, Vector2 pos,
                              Color color, out Image bg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos; rect.sizeDelta = new Vector2(330f, 120f);
            bg = go.GetComponent<Image>();
            bg.color = color; bg.raycastTarget = false;

            Label(rect, "Title", title, 22f, new Vector2(0.5f, 1f), new Vector2(0f, -18f),
                new Vector2(310f, 30f), TextSoft, TextAlignmentOptions.Center);
            return Label(rect, "Value", "", 30f, new Vector2(0.5f, 0.35f), Vector2.zero,
                new Vector2(310f, 50f), Color.white, TextAlignmentOptions.Center);
        }

        private TMP_Text Label(Transform parent, string name, string content, float size, Vector2 anchor,
                               Vector2 pos, Vector2 sizeDelta, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos; rect.sizeDelta = sizeDelta;
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = content; label.fontSize = size; label.alignment = align; label.color = color;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>Estado de UMA linha da trilha — refletido a cada Refresh.</summary>
        private class TierRow
        {
            public int tier;
            public Image rowBg;
            public TMP_Text tierLabel;
            public Image freeBg, premBg;
            public TMP_Text freeLabel, premLabel;

            public void Refresh(MetaBridge.SeasonTierView t, bool owned)
            {
                if (rowBg != null) rowBg.color = t.reached ? RowReached : RowBg;
                if (tierLabel != null) tierLabel.color = t.reached ? Green : Gold;

                if (freeLabel != null) freeLabel.text = t.free.hasReward ? t.free.label : "—";
                if (freeBg != null)
                    freeBg.color = t.reached ? FreeChip : new Color(FreeChip.r, FreeChip.g, FreeChip.b, 0.55f);

                if (premLabel != null)
                {
                    premLabel.text = t.premium.hasReward ? t.premium.label : "—";
                    // Sem o passe, o nó premium fica visível mas APAGADO (incentivo honesto à compra).
                    premLabel.color = owned ? Color.white : new Color(0.7f, 0.7f, 0.75f, 0.6f);
                }
                if (premBg != null)
                {
                    if (!owned) premBg.color = PremiumLocked;
                    else premBg.color = t.reached ? PremiumChip : new Color(PremiumChip.r, PremiumChip.g, PremiumChip.b, 0.55f);
                }
            }
        }
    }
}
