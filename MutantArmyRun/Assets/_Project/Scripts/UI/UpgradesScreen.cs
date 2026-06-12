using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MutantArmy.Core;
using MutantArmy.Domain;

namespace MutantArmy.UI
{
    /// <summary>
    /// SCR-07 — Upgrades (doc 09 §4.7). As 8 trilhas de meta (CANON §9) como linhas: nome,
    /// nível atual, efeito acumulado (atual → próximo), custo e botão MELHORAR. As linhas são
    /// construídas em runtime a partir do MetaBridge. MELHORAR delega a MetaBridge.TryBuyUpgrade
    /// (transacional); desabilita sem moeda ou no teto, mostrando o que falta.
    /// </summary>
    public class UpgradesScreen : UIScreen
    {
        [Header("Cabeçalho")]
        [SerializeField] private TMP_Text _coinsText;
        [SerializeField] private Button _backButton;

        [Header("Lista")]
        [SerializeField] private RectTransform _listContent;     // container com VerticalLayoutGroup
        [SerializeField] private float _rowHeight = 200f;

        public event System.Action BackRequested;

        private readonly List<TrackRow> _rows = new List<TrackRow>();
        private bool _built;

        private static readonly Color Green = new Color(0.25f, 0.80f, 0.35f);
        private static readonly Color Grey = new Color(0.35f, 0.38f, 0.45f);
        private static readonly Color Gold = new Color(1.00f, 0.86f, 0.40f);
        private static readonly Color Red = new Color(0.90f, 0.35f, 0.32f);
        private static readonly Color TextSoft = new Color(0.80f, 0.85f, 0.90f);
        private static readonly Color RowBg = new Color(0.10f, 0.12f, 0.20f, 0.95f);

        protected override void Awake()
        {
            base.Awake();
            if (_backButton != null) _backButton.onClick.AddListener(() => { if (BackRequested != null) BackRequested(); });
        }

        private void OnEnable() { GameEvents.OnCurrencyChanged += HandleCurrencyChanged; }
        private void OnDisable() { GameEvents.OnCurrencyChanged -= HandleCurrencyChanged; }

        private void HandleCurrencyChanged(CurrencyChange c)
        {
            RefreshHeader();
            RefreshRows();
        }

        protected override void OnShown()
        {
            base.OnShown();
            EnsureBuilt();
            RefreshHeader();
            RefreshRows();
        }

        private void EnsureBuilt()
        {
            if (_built || _listContent == null) return;
            _built = true;
            for (int i = 0; i < MetaBridge.AllTracks.Length; i++)
                _rows.Add(BuildRow(MetaBridge.AllTracks[i]));
        }

        private void RefreshHeader()
        {
            if (_coinsText != null) _coinsText.text = MetaText.Coins(MetaBridge.Coins);
        }

        private void RefreshRows()
        {
            for (int i = 0; i < _rows.Count; i++) _rows[i].Refresh();
        }

        private TrackRow BuildRow(UpgradeTrack track)
        {
            var go = new GameObject("Row_" + track, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_listContent, false);
            rect.sizeDelta = new Vector2(0f, _rowHeight);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = _rowHeight; le.preferredHeight = _rowHeight;
            go.GetComponent<Image>().color = RowBg;
            go.GetComponent<Image>().raycastTarget = false;

            TMP_Text name = Label(rect, "Name", MetaText.TrackName(track), 38f,
                new Vector2(0f, 1f), new Vector2(30f, -16f), new Vector2(560f, 50f),
                Color.white, TextAlignmentOptions.TopLeft);

            TMP_Text levelText = Label(rect, "Level", "nv 0", 32f,
                new Vector2(1f, 1f), new Vector2(-30f, -16f), new Vector2(220f, 50f),
                Gold, TextAlignmentOptions.TopRight);

            TMP_Text effect = Label(rect, "Effect", "", 30f,
                new Vector2(0f, 1f), new Vector2(30f, -70f), new Vector2(640f, 44f),
                TextSoft, TextAlignmentOptions.TopLeft);

            RectTransform levelFill = Bar(rect, new Vector2(0f, 1f), new Vector2(30f, -120f), new Vector2(560f, 18f));

            TMP_Text buyLabel;
            Button buy = Btn(rect, "Buy", "MELHORAR", 32f,
                new Vector2(1f, 0f), new Vector2(-30f, 24f), new Vector2(360f, 110f), out buyLabel);

            var row = new TrackRow
            {
                track = track,
                levelText = levelText,
                effect = effect,
                levelFill = levelFill,
                buy = buy,
                buyLabel = buyLabel,
                buyImage = buy.GetComponent<Image>()
            };
            buy.onClick.AddListener(() => OnBuy(row));
            row.Refresh();
            return row;
        }

        private void OnBuy(TrackRow row)
        {
            if (MetaBridge.TryBuyUpgrade(row.track))
            {
                RefreshHeader();
                RefreshRows();
            }
        }

        // ---------------------------------------------------------------- helpers

        private TMP_Text Label(Transform parent, string name, string content, float size, Vector2 anchor,
                               Vector2 pos, Vector2 sizeDelta, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = anchor;
            rect.anchoredPosition = pos; rect.sizeDelta = sizeDelta;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = content; t.fontSize = size; t.alignment = align; t.color = color; t.raycastTarget = false;
            return t;
        }

        // Barra ANCORADA (largura via anchorMax.x), não Image.Type.Filled: uma Image sem sprite
        // ignora fillAmount e renderia a barra sempre cheia. Refresh seta a fração (nível/máx).
        private RectTransform Bar(Transform parent, Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
        {
            var bgGo = new GameObject("LevelBar", typeof(RectTransform), typeof(Image));
            var bgRect = (RectTransform)bgGo.transform;
            bgRect.SetParent(parent, false);
            bgRect.anchorMin = anchor; bgRect.anchorMax = anchor; bgRect.pivot = anchor;
            bgRect.anchoredPosition = pos; bgRect.sizeDelta = sizeDelta;
            bgGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            bgGo.GetComponent<Image>().raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.SetParent(bgRect, false);
            fillRect.anchorMin = new Vector2(0f, 0f); fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;
            var fill = fillGo.GetComponent<Image>();
            fill.color = Green; fill.type = Image.Type.Simple; fill.raycastTarget = false;
            return fillRect;
        }

        private Button Btn(Transform parent, string name, string label, float size, Vector2 anchor,
                           Vector2 pos, Vector2 sizeDelta, out TMP_Text labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = anchor;
            rect.anchoredPosition = pos; rect.sizeDelta = sizeDelta;
            go.GetComponent<Image>().color = Green;
            go.AddComponent<UIButtonPop>();
            labelText = Label(rect, "Label", label, size, new Vector2(0.5f, 0.5f),
                Vector2.zero, sizeDelta, Color.white, TextAlignmentOptions.Center);
            ((RectTransform)labelText.transform).anchoredPosition = Vector2.zero;
            return go.GetComponent<Button>();
        }

        /// <summary>Linha viva de uma trilha — refrescada por evento.</summary>
        private class TrackRow
        {
            public UpgradeTrack track;
            public TMP_Text levelText, effect, buyLabel;
            public RectTransform levelFill;   // largura = fração nível/máx (anchorMax.x)
            public Image buyImage;
            public Button buy;

            public void Refresh()
            {
                int level = MetaBridge.GetUpgradeLevel(track);
                int max = MetaBridge.GetUpgradeMaxLevel(track);
                bool maxed = MetaBridge.IsUpgradeMaxed(track);
                float effNow = MetaBridge.GetUpgradeEffect(track);

                if (levelText != null) levelText.text = "nv " + level;
                if (levelFill != null)
                {
                    float frac = max > 0 ? Mathf.Clamp01((float)level / max) : 0f;
                    levelFill.anchorMax = new Vector2(frac, 1f);
                    levelFill.offsetMin = Vector2.zero;
                    levelFill.offsetMax = Vector2.zero;
                }

                if (effect != null)
                {
                    if (maxed)
                    {
                        effect.text = "Atual: " + MetaText.TrackEffectLabel(track, effNow) + "  (MÁXIMO)";
                    }
                    else
                    {
                        // efeito do PRÓXIMO nível = bônus por nível somado ao atual
                        float perLevel = MetaBridge.IsUnitTrack(track)
                            ? EconomyMath.TrackBonus(track, level + 1) - effNow
                            : 0.05f;
                        float effNext = MetaBridge.IsUnitTrack(track)
                            ? EconomyMath.TrackBonus(track, level + 1)
                            : effNow + perLevel;
                        effect.text = MetaText.TrackEffectLabel(track, effNow) + "  →  " +
                                      MetaText.TrackEffectLabel(track, effNext);
                    }
                }

                if (buy == null) return;
                if (maxed)
                {
                    buy.interactable = false;
                    if (buyImage != null) buyImage.color = Gold;
                    if (buyLabel != null) { buyLabel.text = "MAX"; buyLabel.color = Color.black; }
                    return;
                }

                long cost = MetaBridge.GetUpgradeCost(track);
                bool canBuy = MetaBridge.Coins >= cost;
                buy.interactable = canBuy;
                if (buyImage != null) buyImage.color = canBuy ? Green : Grey;
                if (buyLabel != null)
                {
                    buyLabel.text = MetaText.Coins(cost);
                    buyLabel.color = canBuy ? Color.white : Red;
                }
            }
        }
    }
}
