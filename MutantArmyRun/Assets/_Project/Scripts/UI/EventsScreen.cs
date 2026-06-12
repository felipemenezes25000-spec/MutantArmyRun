using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MutantArmy.Core;
using MutantArmy.Domain;

namespace MutantArmy.UI
{
    /// <summary>
    /// EventsScreen (SCR-SISTEMA, doc 09 §4): EVENTOS — card de evento DIÁRIO e SEMANAL (tempo
    /// restante, recompensa, barra de progresso) + RANKING local (top 10 com o jogador
    /// destacado). Os dados vêm do MetaBridge (DailyEvent/WeeklyEvent/LocalLeaderboard —
    /// determinísticos e locais). O ranking é HONESTO: a tela rotula que é local e que o online
    /// "chega em breve". Cards e linhas de ranking construídos em runtime; o tempo restante é
    /// re-renderizado por um tick leve em unscaled time (nunca polling de dados pesados).
    /// </summary>
    public class EventsScreen : UIScreen
    {
        [Header("Cabeçalho")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Button _backButton;

        [Header("Cards de evento")]
        [SerializeField] private RectTransform _dailyCard;
        [SerializeField] private RectTransform _weeklyCard;

        [Header("Ranking (lista rolável construída em runtime)")]
        [SerializeField] private RectTransform _rankingContent;
        [SerializeField] private TMP_Text _rankingNote;     // "ranking local — online em breve"

        public event System.Action BackRequested;

        private EventCard _daily;
        private EventCard _weekly;
        private readonly List<RankRow> _ranks = new List<RankRow>();
        private bool _built;
        private float _nextTick;

        private static readonly Color Gold = new Color(1.00f, 0.86f, 0.40f);
        private static readonly Color Cyan = new Color(0.20f, 0.75f, 1.00f);
        private static readonly Color Green = new Color(0.25f, 0.80f, 0.35f);
        private static readonly Color TextSoft = new Color(0.80f, 0.85f, 0.90f);
        private static readonly Color CardBg = new Color(0.10f, 0.12f, 0.20f, 0.96f);
        private static readonly Color RowBg = new Color(0.08f, 0.10f, 0.16f, 0.94f);
        private static readonly Color PlayerBg = new Color(0.16f, 0.30f, 0.48f, 0.98f);

        protected override void Awake()
        {
            base.Awake();
            if (_backButton != null) _backButton.onClick.AddListener(() => { if (BackRequested != null) BackRequested(); });
        }

        protected override void OnShown()
        {
            base.OnShown();
            if (_titleText != null) _titleText.text = "EVENTOS";
            if (_rankingNote != null) _rankingNote.text = "Ranking local — versão online em breve";
            EnsureBuilt();
            Refresh();
            _nextTick = Time.unscaledTime + 1f;
        }

        private void Update()
        {
            if (!IsVisible) return;
            // tick leve só para o COUNTDOWN dos cards (1 Hz, unscaled) — não reconstrói nada
            if (Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + 1f;
            if (_daily != null) _daily.RefreshTimer(MetaBridge.DailyEvent());
            if (_weekly != null) _weekly.RefreshTimer(MetaBridge.WeeklyEvent());
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            if (_dailyCard != null) _daily = BuildEventCard(_dailyCard, Cyan);
            if (_weeklyCard != null) _weekly = BuildEventCard(_weeklyCard, Gold);

            if (_rankingContent != null)
            {
                IReadOnlyList<MetaBridge.LeaderboardRow> rows = MetaBridge.LocalLeaderboard();
                for (int i = 0; i < rows.Count; i++) _ranks.Add(BuildRankRow(rows[i]));
            }
        }

        private void Refresh()
        {
            if (_daily != null) _daily.Refresh(MetaBridge.DailyEvent());
            if (_weekly != null) _weekly.Refresh(MetaBridge.WeeklyEvent());

            IReadOnlyList<MetaBridge.LeaderboardRow> rows = MetaBridge.LocalLeaderboard();
            for (int i = 0; i < _ranks.Count && i < rows.Count; i++) _ranks[i].Refresh(rows[i]);
        }

        // ---------------------------------------------------------------- card de evento

        private EventCard BuildEventCard(RectTransform card, Color accent)
        {
            // o card já é um RectTransform vazio (criado pela factory): pintamos o fundo e o
            // conteúdo aqui para o layout ficar 100% no código da tela (data-driven).
            Image bg = card.GetComponent<Image>();
            if (bg == null) bg = card.gameObject.AddComponent<Image>();
            bg.color = CardBg; bg.raycastTarget = false;

            TMP_Text title = Label(card, "Title", "", 38f, new Vector2(0f, 1f),
                new Vector2(36f, -26f), new Vector2(560f, 50f), accent, TextAlignmentOptions.TopLeft);
            ((RectTransform)title.transform).pivot = new Vector2(0f, 1f);

            TMP_Text timer = Label(card, "Timer", "", 30f, new Vector2(1f, 1f),
                new Vector2(-36f, -28f), new Vector2(280f, 46f), TextSoft, TextAlignmentOptions.TopRight);
            ((RectTransform)timer.transform).pivot = new Vector2(1f, 1f);

            TMP_Text desc = Label(card, "Desc", "", 32f, new Vector2(0f, 1f),
                new Vector2(36f, -84f), new Vector2(700f, 46f), Color.white, TextAlignmentOptions.TopLeft);
            ((RectTransform)desc.transform).pivot = new Vector2(0f, 1f);

            TMP_Text reward = Label(card, "Reward", "", 30f, new Vector2(0f, 1f),
                new Vector2(36f, -136f), new Vector2(700f, 44f), Gold, TextAlignmentOptions.TopLeft);
            ((RectTransform)reward.transform).pivot = new Vector2(0f, 1f);

            RectTransform fill = Bar(card, new Vector2(0f, 0f), new Vector2(36f, 30f), new Vector2(700f, 26f), accent);

            TMP_Text progress = Label(card, "Progress", "", 28f, new Vector2(1f, 0f),
                new Vector2(-36f, 30f), new Vector2(220f, 40f), TextSoft, TextAlignmentOptions.Right);
            ((RectTransform)progress.transform).pivot = new Vector2(1f, 0f);

            return new EventCard
            {
                title = title, timer = timer, desc = desc, reward = reward,
                progressFill = fill, progressText = progress
            };
        }

        // ---------------------------------------------------------------- linha de ranking

        private RankRow BuildRankRow(MetaBridge.LeaderboardRow data)
        {
            var go = new GameObject("Rank_" + data.rank, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_rankingContent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 96f; le.preferredHeight = 96f;
            var bg = go.GetComponent<Image>();
            bg.raycastTarget = false;

            TMP_Text rank = Label(rect, "Rank", "", 38f, new Vector2(0f, 0.5f),
                new Vector2(30f, 0f), new Vector2(90f, 70f), Gold, TextAlignmentOptions.Center);
            ((RectTransform)rank.transform).pivot = new Vector2(0f, 0.5f);

            TMP_Text name = Label(rect, "Name", "", 34f, new Vector2(0f, 0.5f),
                new Vector2(140f, 0f), new Vector2(520f, 70f), Color.white, TextAlignmentOptions.Left);
            ((RectTransform)name.transform).pivot = new Vector2(0f, 0.5f);

            TMP_Text score = Label(rect, "Score", "", 32f, new Vector2(1f, 0.5f),
                new Vector2(-30f, 0f), new Vector2(260f, 70f), TextSoft, TextAlignmentOptions.Right);
            ((RectTransform)score.transform).pivot = new Vector2(1f, 0.5f);

            var row = new RankRow { bg = bg, rank = rank, name = name, score = score };
            row.Refresh(data);
            return row;
        }

        // ---------------------------------------------------------------- helpers

        private TMP_Text Label(Transform parent, string name, string content, float size, Vector2 anchor,
                               Vector2 pos, Vector2 sizeDelta, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos; rect.sizeDelta = sizeDelta;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = content; t.fontSize = size; t.alignment = align; t.color = color; t.raycastTarget = false;
            return t;
        }

        // Barra ANCORADA (fração via anchorMax.x) — mesmo padrão do DailyScreen (uma Image sem
        // sprite ignora fillAmount e renderizaria sempre cheia).
        private RectTransform Bar(Transform parent, Vector2 anchor, Vector2 pos, Vector2 sizeDelta, Color fillColor)
        {
            var bgGo = new GameObject("Bar", typeof(RectTransform), typeof(Image));
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
            fill.color = fillColor; fill.raycastTarget = false;
            return fillRect;
        }

        private class EventCard
        {
            public TMP_Text title, timer, desc, reward, progressText;
            public RectTransform progressFill;

            public void Refresh(MetaBridge.EventView e)
            {
                if (title != null) title.text = e.title;
                if (desc != null) desc.text = e.desc;
                if (reward != null) reward.text = "Recompensa: +" + e.rewardCoins + " moedas  +" + e.rewardGems + " gemas";
                if (progressText != null) progressText.text = e.progress + "/" + e.target;
                if (progressFill != null)
                {
                    float frac = e.target > 0 ? Mathf.Clamp01((float)e.progress / e.target) : 0f;
                    progressFill.anchorMax = new Vector2(frac, 1f);
                    progressFill.offsetMin = Vector2.zero;
                    progressFill.offsetMax = Vector2.zero;
                }
                RefreshTimer(e);
            }

            public void RefreshTimer(MetaBridge.EventView e)
            {
                if (timer != null) timer.text = "⏳ " + MetaBridge.FormatRemaining(e.secondsRemaining);
            }
        }

        private class RankRow
        {
            public Image bg;
            public TMP_Text rank, name, score;

            public void Refresh(MetaBridge.LeaderboardRow r)
            {
                if (bg != null) bg.color = r.isPlayer ? PlayerBg : RowBg;
                if (rank != null) rank.text = "#" + r.rank;
                if (name != null)
                {
                    name.text = r.name;
                    name.color = r.isPlayer ? Green : Color.white;
                    name.fontStyle = r.isPlayer ? FontStyles.Bold : FontStyles.Normal;
                }
                if (score != null) score.text = r.score.ToString();
            }
        }
    }
}
