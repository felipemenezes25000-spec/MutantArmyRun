using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MutantArmy.Core;
using MutantArmy.Domain;
using MutantArmy.Meta;

namespace MutantArmy.UI
{
    /// <summary>
    /// SCR-06 — Tropas (doc 09 §4.6). Grade de cartas das tropas do catálogo (moldura por
    /// raridade, ícone/cor da tropa, nível, barra de fragmentos, cadeado nas bloqueadas).
    /// Tocar numa carta abre o painel de detalhe (stats + EVOLUIR). A grade é construída em
    /// runtime a partir do MetaBridge (data-driven — o catálogo só é conhecido em runtime).
    /// Tela PASSIVA quanto à economia: EVOLUIR delega ao MetaBridge.TryEvolve (transacional).
    /// </summary>
    public class TroopsScreen : UIScreen
    {
        [Header("Cabeçalho")]
        [SerializeField] private TMP_Text _coinsText;
        [SerializeField] private TMP_Text _gemsText;
        [SerializeField] private Button _backButton;

        [Header("Grade")]
        [SerializeField] private RectTransform _gridContent;     // container com GridLayoutGroup
        [SerializeField] private float _cardSize = 300f;

        [Header("Painel de detalhe")]
        [SerializeField] private GameObject _detailPanel;
        [SerializeField] private TMP_Text _detailTitle;
        [SerializeField] private TMP_Text _detailStats;
        [SerializeField] private TMP_Text _detailShards;
        [SerializeField] private Button _evolveButton;
        [SerializeField] private TMP_Text _evolveLabel;

        public event System.Action BackRequested;

        private readonly List<TroopCard> _cards = new List<TroopCard>();
        private string _selectedUnitId;
        private bool _built;

        // Cores reutilizadas dos helpers de skin (coerência com o resto da UI).
        private static readonly Color Green = new Color(0.25f, 0.80f, 0.35f);
        private static readonly Color Grey = new Color(0.35f, 0.38f, 0.45f);
        private static readonly Color Red = new Color(0.90f, 0.35f, 0.32f);
        private static readonly Color TextSoft = new Color(0.80f, 0.85f, 0.90f);
        private static readonly Color Gold = new Color(1.00f, 0.86f, 0.40f);

        protected override void Awake()
        {
            base.Awake();
            if (_backButton != null) _backButton.onClick.AddListener(() => { if (BackRequested != null) BackRequested(); });
            if (_evolveButton != null) _evolveButton.onClick.AddListener(OnEvolveClicked);
        }

        private void OnEnable()
        {
            GameEvents.OnCurrencyChanged += HandleCurrencyChanged;
            if (UnitManager.Instance != null) UnitManager.Instance.OnTroopChanged += HandleTroopChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnCurrencyChanged -= HandleCurrencyChanged;
            if (UnitManager.Instance != null) UnitManager.Instance.OnTroopChanged -= HandleTroopChanged;
        }

        private void HandleCurrencyChanged(CurrencyChange c)
        {
            RefreshHeader();
            RefreshCards();
            if (!string.IsNullOrEmpty(_selectedUnitId)) BindDetail(_selectedUnitId);
        }

        private void HandleTroopChanged(string unitId)
        {
            // Desbloqueio/fragmentos por baú não passam por moeda — re-render por este evento.
            RefreshCards();
            if (!string.IsNullOrEmpty(_selectedUnitId)) BindDetail(_selectedUnitId);
        }

        protected override void OnShown()
        {
            base.OnShown();
            EnsureBuilt();
            RefreshHeader();
            RefreshCards();
            if (_detailPanel != null && string.IsNullOrEmpty(_selectedUnitId))
                _detailPanel.SetActive(false);
        }

        private void EnsureBuilt()
        {
            if (_built || _gridContent == null) return;
            _built = true;

            IReadOnlyList<UnitConfigSO> troops = MetaBridge.AllTroops();
            for (int i = 0; i < troops.Count; i++)
            {
                UnitConfigSO unit = troops[i];
                if (unit == null) continue;
                TroopCard card = BuildCard(unit);
                _cards.Add(card);
            }
        }

        private void RefreshHeader()
        {
            if (_coinsText != null) _coinsText.text = MetaText.Coins(MetaBridge.Coins);
            if (_gemsText != null) _gemsText.text = MetaBridge.Gems.ToString();
        }

        private void RefreshCards()
        {
            for (int i = 0; i < _cards.Count; i++) _cards[i].Refresh();
        }

        // ---------------------------------------------------------------- Carta

        private TroopCard BuildCard(UnitConfigSO unit)
        {
            var go = new GameObject("Card_" + unit.unitId, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_gridContent, false);
            rect.sizeDelta = new Vector2(_cardSize, _cardSize);

            var bg = go.GetComponent<Image>();
            bg.color = MetaText.RarityCardBg(unit.rarity);
            bg.raycastTarget = true;

            // Moldura de raridade (borda colorida).
            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            var frameRect = (RectTransform)frameGo.transform;
            frameRect.SetParent(rect, false);
            Stretch(frameRect);
            var frame = frameGo.GetComponent<Image>();
            frame.color = MetaText.RarityFrame(unit.rarity);
            frame.raycastTarget = false;
            frame.type = Image.Type.Sliced;

            // Ícone/cor da tropa.
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.SetParent(rect, false);
            Place(iconRect, new Vector2(0.5f, 0.62f), new Vector2(_cardSize * 0.5f, _cardSize * 0.5f));
            var icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            if (unit.cardIcon != null) { icon.sprite = unit.cardIcon; icon.color = Color.white; }
            else icon.color = BossScoutOverlay.ElementColorPt(unit.element);

            // Nome.
            TMP_Text name = Label(rect, "Name", MetaText.UnitName(unit), 30f, new Vector2(0.5f, 0.30f),
                new Vector2(_cardSize - 16f, 40f), Color.white, TextAlignmentOptions.Center);

            // Nível.
            TMP_Text level = Label(rect, "Level", "nv 1", 28f, new Vector2(0.5f, 0.18f),
                new Vector2(_cardSize - 16f, 36f), Gold, TextAlignmentOptions.Center);

            // Barra de fragmentos.
            RectTransform shardFill = ShardBar(rect, new Vector2(0.5f, 0.08f), new Vector2(_cardSize - 40f, 24f));

            // Cadeado (bloqueada).
            var lockGo = new GameObject("Lock", typeof(RectTransform), typeof(Image));
            var lockRect = (RectTransform)lockGo.transform;
            lockRect.SetParent(rect, false);
            Stretch(lockRect);
            var lockImg = lockGo.GetComponent<Image>();
            lockImg.color = new Color(0f, 0f, 0f, 0.66f);
            lockImg.raycastTarget = false;
            TMP_Text lockText = Label(lockRect, "LockText", "BLOQUEADA", 30f, new Vector2(0.5f, 0.5f),
                new Vector2(_cardSize - 16f, 60f), TextSoft, TextAlignmentOptions.Center);
            lockGo.SetActive(false);

            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            go.AddComponent<UIButtonPop>();
            string id = unit.unitId;
            btn.onClick.AddListener(() => OnCardClicked(id));

            var card = new TroopCard
            {
                unitId = unit.unitId,
                level = level,
                shardFill = shardFill,
                lockRoot = lockGo,
                name = name
            };
            card.Refresh();
            return card;
        }

        private void OnCardClicked(string unitId)
        {
            _selectedUnitId = unitId;
            BindDetail(unitId);
            if (_detailPanel != null) _detailPanel.SetActive(true);
        }

        private void BindDetail(string unitId)
        {
            MetaBridge.TroopView v = MetaBridge.GetTroop(unitId);
            if (v.config == null) return;

            if (_detailTitle != null)
                _detailTitle.text = MetaText.UnitName(v.config) + "  nv " + v.level +
                                    (v.maxed ? "" : " → " + (v.level + 1));

            if (_detailStats != null)
            {
                float hp = MetaBridge.GetEffectiveHp(unitId);
                float dps = MetaBridge.GetEffectiveDps(unitId);
                float spd = MetaBridge.GetEffectiveMoveSpeed(unitId);
                _detailStats.text = string.Format(
                    "Raridade: {0}\nHP {1:0}   DPS {2:0.0}\nVeloc {3:0.0} m/s   Supply {4}",
                    MetaText.RarityName(v.config.rarity), hp, dps, spd, v.config.supplyCost);
            }

            if (_detailShards != null)
            {
                if (v.maxed) _detailShards.text = "NÍVEL MÁXIMO";
                else _detailShards.text = "Fragmentos: " + v.shards + "/" + v.shardsToNext;
            }

            BindEvolveButton(v);
        }

        private void BindEvolveButton(MetaBridge.TroopView v)
        {
            if (_evolveButton == null) return;
            var img = _evolveButton.GetComponent<Image>();

            if (!v.unlocked)
            {
                _evolveButton.interactable = false;
                if (img != null) img.color = Grey;
                if (_evolveLabel != null) { _evolveLabel.text = "BLOQUEADA"; _evolveLabel.color = TextSoft; }
                return;
            }
            if (v.maxed)
            {
                _evolveButton.interactable = false;
                if (img != null) img.color = Gold;
                if (_evolveLabel != null) { _evolveLabel.text = "MAX"; _evolveLabel.color = Color.black; }
                return;
            }

            _evolveButton.interactable = v.canEvolve;
            if (img != null) img.color = v.canEvolve ? Green : Grey;
            if (_evolveLabel != null)
            {
                if (v.canEvolve)
                {
                    _evolveLabel.text = "EVOLUIR  " + v.shardsToNext + " frag + " + v.evolveCoinCost + " moedas";
                    _evolveLabel.color = Color.white;
                }
                else
                {
                    _evolveLabel.text = "✗ " + v.shards + "/" + v.shardsToNext + " frag";
                    _evolveLabel.color = Red;
                }
            }
        }

        private void OnEvolveClicked()
        {
            if (string.IsNullOrEmpty(_selectedUnitId)) return;
            if (MetaBridge.TryEvolve(_selectedUnitId))
            {
                BindDetail(_selectedUnitId);
                RefreshCards();
                RefreshHeader();
            }
        }

        // ---------------------------------------------------------------- helpers de construção

        private TMP_Text Label(Transform parent, string name, string content, float size, Vector2 anchor,
                               Vector2 sizeDelta, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Place(rect, anchor, sizeDelta);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = content; t.fontSize = size; t.alignment = align; t.color = color; t.raycastTarget = false;
            return t;
        }

        // Barra de fragmentos ANCORADA (largura via anchorMax.x), não Image.Type.Filled: uma Image
        // sem sprite ignora fillAmount e renderia a barra sempre cheia. Refresh seta a fração.
        private RectTransform ShardBar(Transform parent, Vector2 anchor, Vector2 sizeDelta)
        {
            var bgGo = new GameObject("ShardBar", typeof(RectTransform), typeof(Image));
            var bgRect = (RectTransform)bgGo.transform;
            bgRect.SetParent(parent, false);
            Place(bgRect, anchor, sizeDelta);
            bgGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            bgGo.GetComponent<Image>().raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.SetParent(bgRect, false);
            // ancora à esquerda; anchorMax.x = fração → preenchimento 0..1 sem depender de sprite.
            fillRect.anchorMin = new Vector2(0f, 0f); fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;
            var fill = fillGo.GetComponent<Image>();
            fill.color = new Color(0.55f, 0.80f, 1f);
            fill.type = Image.Type.Simple;
            fill.raycastTarget = false;
            return fillRect;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 sizeDelta)
        {
            rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = sizeDelta;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }

        /// <summary>Estado vivo de uma carta — refrescada por evento (nível/fragmentos/cadeado).</summary>
        private class TroopCard
        {
            public string unitId;
            public TMP_Text level;
            public TMP_Text name;
            public RectTransform shardFill;   // largura = fração de fragmentos (anchorMax.x)
            public GameObject lockRoot;

            public void Refresh()
            {
                MetaBridge.TroopView v = MetaBridge.GetTroop(unitId);
                if (level != null) level.text = v.maxed ? "MAX" : "nv " + v.level;
                if (shardFill != null)
                {
                    float frac = v.shardsToNext > 0 ? Mathf.Clamp01((float)v.shards / v.shardsToNext) : 1f;
                    shardFill.anchorMax = new Vector2(frac, 1f);
                    shardFill.offsetMin = Vector2.zero;
                    shardFill.offsetMax = Vector2.zero;
                }
                if (lockRoot != null) lockRoot.SetActive(!v.unlocked);
            }
        }
    }
}
