using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MutantArmy.Core;
using MutantArmy.Domain;

namespace MutantArmy.UI
{
    /// <summary>
    /// SCR-09 — Mapa (doc 09 §4.9). Os 10 mundos como cards/nodes: nome, tema, progresso
    /// X/10, boss do mundo, bloqueado/desbloqueado por highestLevelCleared. Tocar um mundo
    /// desbloqueado seleciona a fase (próxima jogável) e dispara WorldSelected — o
    /// MainMenuController decide iniciar/voltar ao menu. Cards construídos em runtime.
    /// </summary>
    public class MapScreen : UIScreen
    {
        [Header("Cabeçalho")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Button _backButton;

        [Header("Lista de mundos")]
        [SerializeField] private RectTransform _listContent;     // container com VerticalLayoutGroup
        [SerializeField] private float _cardHeight = 280f;

        /// <summary>Disparado ao tocar num mundo desbloqueado, com o índice da fase a jogar.</summary>
        public event System.Action<int> WorldSelected;
        public event System.Action BackRequested;

        private readonly List<WorldCard> _cards = new List<WorldCard>();
        private bool _built;

        private static readonly Color Gold = new Color(1.00f, 0.86f, 0.40f);
        private static readonly Color Green = new Color(0.25f, 0.80f, 0.35f);
        private static readonly Color TextSoft = new Color(0.80f, 0.85f, 0.90f);
        private static readonly Color LockedBg = new Color(0.08f, 0.09f, 0.13f, 0.95f);
        private static readonly Color UnlockedBg = new Color(0.12f, 0.16f, 0.26f, 0.97f);

        protected override void Awake()
        {
            base.Awake();
            if (_backButton != null) _backButton.onClick.AddListener(() => { if (BackRequested != null) BackRequested(); });
        }

        protected override void OnShown()
        {
            base.OnShown();
            EnsureBuilt();
            RefreshCards();
            if (_titleText != null) _titleText.text = "MAPA";
        }

        private void EnsureBuilt()
        {
            if (_built || _listContent == null) return;
            _built = true;
            IReadOnlyList<MetaBridge.WorldView> worlds = MetaBridge.Worlds();
            for (int i = 0; i < worlds.Count; i++)
                _cards.Add(BuildCard(worlds[i]));
        }

        private void RefreshCards()
        {
            IReadOnlyList<MetaBridge.WorldView> worlds = MetaBridge.Worlds();
            for (int i = 0; i < _cards.Count && i < worlds.Count; i++)
                _cards[i].Refresh(worlds[i]);
        }

        private WorldCard BuildCard(MetaBridge.WorldView w)
        {
            var go = new GameObject("World_" + w.worldIndex, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_listContent, false);
            rect.sizeDelta = new Vector2(0f, _cardHeight);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = _cardHeight; le.preferredHeight = _cardHeight;
            var bg = go.GetComponent<Image>();
            bg.color = w.unlocked ? UnlockedBg : LockedBg;
            go.AddComponent<UIButtonPop>();

            // Faixa lateral colorida (tema do mundo).
            var stripeGo = new GameObject("Stripe", typeof(RectTransform), typeof(Image));
            var stripeRect = (RectTransform)stripeGo.transform;
            stripeRect.SetParent(rect, false);
            stripeRect.anchorMin = new Vector2(0f, 0f); stripeRect.anchorMax = new Vector2(0f, 1f);
            stripeRect.pivot = new Vector2(0f, 0.5f); stripeRect.sizeDelta = new Vector2(18f, 0f);
            stripeRect.anchoredPosition = Vector2.zero;
            stripeGo.GetComponent<Image>().raycastTarget = false;
            Image stripe = stripeGo.GetComponent<Image>();

            TMP_Text name = Label(rect, "Name", "", 40f, new Vector2(0f, 1f),
                new Vector2(48f, -20f), new Vector2(640f, 56f), Color.white, TextAlignmentOptions.TopLeft);
            TMP_Text boss = Label(rect, "Boss", "", 30f, new Vector2(0f, 1f),
                new Vector2(48f, -90f), new Vector2(640f, 44f), TextSoft, TextAlignmentOptions.TopLeft);
            TMP_Text progress = Label(rect, "Progress", "", 34f, new Vector2(1f, 1f),
                new Vector2(-40f, -20f), new Vector2(260f, 50f), Gold, TextAlignmentOptions.TopRight);

            Image progressFill = Bar(rect, new Vector2(0f, 0f), new Vector2(48f, 100f), new Vector2(640f, 22f));

            TMP_Text cta = Label(rect, "Cta", "", 32f, new Vector2(1f, 0f),
                new Vector2(-40f, 40f), new Vector2(320f, 60f), Green, TextAlignmentOptions.BottomRight);

            var card = new WorldCard
            {
                root = go.GetComponent<Button>(),
                bg = bg, stripe = stripe,
                name = name, boss = boss, progress = progress, progressFill = progressFill, cta = cta
            };
            card.root.onClick.AddListener(() => OnCardClicked(card));
            card.Refresh(w);
            return card;
        }

        private void OnCardClicked(WorldCard card)
        {
            if (!card.current.unlocked) return;
            int level = MetaBridge.NextPlayableLevelInWorld(card.current);
            if (WorldSelected != null) WorldSelected(level);
        }

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

        private Image Bar(Transform parent, Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
        {
            var bgGo = new GameObject("ProgressBar", typeof(RectTransform), typeof(Image));
            var bgRect = (RectTransform)bgGo.transform;
            bgRect.SetParent(parent, false);
            bgRect.anchorMin = anchor; bgRect.anchorMax = anchor; bgRect.pivot = anchor;
            bgRect.anchoredPosition = pos; bgRect.sizeDelta = sizeDelta;
            bgGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            bgGo.GetComponent<Image>().raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.SetParent(bgRect, false);
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;
            var fill = fillGo.GetComponent<Image>();
            fill.color = Green; fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 0f; fill.raycastTarget = false;
            return fill;
        }

        /// <summary>Card vivo de um mundo.</summary>
        private class WorldCard
        {
            public Button root;
            public Image bg, stripe, progressFill;
            public TMP_Text name, boss, progress, cta;
            public MetaBridge.WorldView current;

            public void Refresh(MetaBridge.WorldView w)
            {
                current = w;
                WorldConfigSO cfg = w.config;

                if (name != null) name.text = MetaText.WorldName(cfg, w.worldIndex);

                BossConfigSO worldBoss = cfg != null ? cfg.worldBoss : null;
                if (boss != null)
                {
                    string bossName = worldBoss != null && !string.IsNullOrEmpty(worldBoss.displayName)
                        ? worldBoss.displayName
                        : (worldBoss != null ? MetaText.Humanize(worldBoss.displayNameKey) : "—");
                    boss.text = "Boss: " + bossName;
                }

                // Cor do tema (céu do mundo) na faixa lateral.
                if (stripe != null && cfg != null) stripe.color = cfg.skyTopColor;

                if (progress != null) progress.text = w.clearedInWorld + "/" + MetaBridge.LevelsPerWorld;
                if (progressFill != null)
                    progressFill.fillAmount = Mathf.Clamp01((float)w.clearedInWorld / MetaBridge.LevelsPerWorld);

                if (bg != null) bg.color = w.unlocked ? UnlockedBg : LockedBg;
                if (cta != null)
                {
                    if (w.unlocked)
                    {
                        int lv = MetaBridge.NextPlayableLevelInWorld(w);
                        cta.text = "JOGAR F" + lv + " ▶";
                        cta.color = Green;
                    }
                    else
                    {
                        cta.text = "🔒 Complete a fase " + (w.firstLevel - 1);
                        cta.color = TextSoft;
                    }
                }
            }
        }
    }
}
