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
    /// PAINEL DIÁRIO (doc 09 §4.10, subset de SCR-10): recompensa de login (calendário de 7
    /// dias, botão RECLAMAR) + 3 missões diárias com progresso/target e botão de resgate. Os
    /// dados vêm do MetaBridge (degradação graciosa enquanto não há MissionSystem dedicado).
    /// Calendário de 7 dias e linhas de missão construídos em runtime.
    /// </summary>
    public class DailyScreen : UIScreen
    {
        [Header("Cabeçalho")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Button _backButton;

        [Header("Login (7 dias)")]
        [SerializeField] private RectTransform _calendarContent;     // container com HorizontalLayoutGroup
        [SerializeField] private Button _claimLoginButton;
        [SerializeField] private TMP_Text _claimLoginLabel;

        [Header("Missões")]
        [SerializeField] private RectTransform _missionsContent;     // container com VerticalLayoutGroup

        public event System.Action BackRequested;

        private readonly List<DayCell> _days = new List<DayCell>();
        private readonly List<MissionRow> _missions = new List<MissionRow>();
        private bool _built;

        private static readonly Color Green = new Color(0.25f, 0.80f, 0.35f);
        private static readonly Color Grey = new Color(0.35f, 0.38f, 0.45f);
        private static readonly Color Gold = new Color(1.00f, 0.86f, 0.40f);
        private static readonly Color TextSoft = new Color(0.80f, 0.85f, 0.90f);
        private static readonly Color CellBg = new Color(0.10f, 0.12f, 0.20f, 0.96f);
        private static readonly Color CellToday = new Color(0.20f, 0.30f, 0.48f, 0.98f);
        private static readonly Color CellClaimed = new Color(0.14f, 0.30f, 0.18f, 0.96f);

        protected override void Awake()
        {
            base.Awake();
            if (_backButton != null) _backButton.onClick.AddListener(() => { if (BackRequested != null) BackRequested(); });
            if (_claimLoginButton != null) _claimLoginButton.onClick.AddListener(OnClaimLogin);
        }

        private void OnEnable()
        {
            GameEvents.OnCurrencyChanged += HandleCurrencyChanged;
            if (MissionSystem.Instance != null)
            {
                MissionSystem.Instance.OnMissionsChanged += Refresh;
                MissionSystem.Instance.OnLoginClaimed += Refresh;
            }
        }

        private void OnDisable()
        {
            GameEvents.OnCurrencyChanged -= HandleCurrencyChanged;
            if (MissionSystem.Instance != null)
            {
                MissionSystem.Instance.OnMissionsChanged -= Refresh;
                MissionSystem.Instance.OnLoginClaimed -= Refresh;
            }
        }

        private void HandleCurrencyChanged(CurrencyChange c) { Refresh(); }

        protected override void OnShown()
        {
            base.OnShown();
            EnsureBuilt();
            Refresh();
            if (_titleText != null) _titleText.text = "DIÁRIO";
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            if (_calendarContent != null)
            {
                for (int day = 1; day <= 7; day++)
                    _days.Add(BuildDayCell(day));
            }

            if (_missionsContent != null)
            {
                IReadOnlyList<MetaBridge.MissionView> missions = MetaBridge.DailyMissions();
                for (int i = 0; i < missions.Count; i++)
                    _missions.Add(BuildMissionRow(missions[i]));
            }
        }

        private void Refresh()
        {
            MetaBridge.LoginView login = MetaBridge.TodayLogin();

            for (int i = 0; i < _days.Count; i++)
                _days[i].Refresh(login.streakDay, login.claimedToday);

            if (_claimLoginButton != null)
            {
                bool canClaim = !login.claimedToday;
                _claimLoginButton.interactable = canClaim;
                var img = _claimLoginButton.GetComponent<Image>();
                if (img != null) img.color = canClaim ? Green : Grey;
            }
            if (_claimLoginLabel != null)
            {
                MetaBridge.LoginView v = login;
                _claimLoginLabel.text = v.claimedToday
                    ? "VOLTE AMANHÃ"
                    : "RECLAMAR  +" + v.todayCoins + " moedas  +" + v.todayGems + " gemas";
            }

            IReadOnlyList<MetaBridge.MissionView> missions = MetaBridge.DailyMissions();
            for (int i = 0; i < _missions.Count && i < missions.Count; i++)
                _missions[i].Refresh(missions[i]);
        }

        private void OnClaimLogin()
        {
            if (MetaBridge.TryClaimLogin())
                Refresh();
        }

        private void OnClaimMission(MissionRow row)
        {
            if (MetaBridge.TryClaimMission(row.missionId))
                Refresh();
        }

        // ---------------------------------------------------------------- células

        private DayCell BuildDayCell(int day)
        {
            var go = new GameObject("Day" + day, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_calendarContent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = 124f; le.preferredWidth = 124f; le.minHeight = 170f; le.preferredHeight = 170f;
            var bg = go.GetComponent<Image>();
            bg.color = CellBg; bg.raycastTarget = false;

            Label(rect, "DayLabel", "Dia " + day, 24f, new Vector2(0.5f, 0.86f),
                new Vector2(120f, 30f), TextSoft, TextAlignmentOptions.Center);
            Label(rect, "Coins", "+" + MetaBridge.LoginCoinsForDay(day), 24f, new Vector2(0.5f, 0.5f),
                new Vector2(120f, 30f), Color.white, TextAlignmentOptions.Center);
            Label(rect, "Gems", "◆" + MetaBridge.LoginGemsForDay(day), 24f, new Vector2(0.5f, 0.30f),
                new Vector2(120f, 30f), new Color(0.55f, 0.85f, 1f), TextAlignmentOptions.Center);
            TMP_Text check = Label(rect, "Check", "", 36f, new Vector2(0.5f, 0.10f),
                new Vector2(120f, 36f), Green, TextAlignmentOptions.Center);

            return new DayCell { day = day, bg = bg, check = check };
        }

        private MissionRow BuildMissionRow(MetaBridge.MissionView m)
        {
            var go = new GameObject("Mission_" + m.id, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_missionsContent, false);
            rect.sizeDelta = new Vector2(0f, 150f);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 150f; le.preferredHeight = 150f;
            go.GetComponent<Image>().color = CellBg;
            go.GetComponent<Image>().raycastTarget = false;

            TMP_Text desc = Label(rect, "Desc", m.desc, 32f, new Vector2(0f, 1f),
                new Vector2(30f, -16f), new Vector2(620f, 44f), Color.white, TextAlignmentOptions.TopLeft);
            ((RectTransform)desc.transform).pivot = new Vector2(0f, 1f);

            TMP_Text reward = Label(rect, "Reward", "", 28f, new Vector2(0f, 1f),
                new Vector2(30f, -68f), new Vector2(620f, 40f), Gold, TextAlignmentOptions.TopLeft);
            ((RectTransform)reward.transform).pivot = new Vector2(0f, 1f);

            RectTransform progressFill = Bar(rect, new Vector2(0f, 0f), new Vector2(30f, 24f), new Vector2(620f, 20f));

            // Botão RESGATAR à direita (habilita só quando completa e não reclamada).
            TMP_Text claimLabel;
            Button claim = Btn(rect, "Claim", "RESGATAR", 28f, new Vector2(1f, 0.5f),
                new Vector2(-30f, 0f), new Vector2(300f, 110f), out claimLabel);

            var row = new MissionRow
            {
                missionId = m.id,
                reward = reward,
                progressFill = progressFill,
                claim = claim,
                claimLabel = claimLabel,
                claimImage = claim.GetComponent<Image>()
            };
            claim.onClick.AddListener(() => OnClaimMission(row));
            row.Refresh(m);
            return row;
        }

        // ---------------------------------------------------------------- helpers

        private TMP_Text Label(Transform parent, string name, string content, float size, Vector2 anchor,
                               Vector2 sizeDelta, Color color, TextAlignmentOptions align)
            => Label(parent, name, content, size, anchor, Vector2.zero, sizeDelta, color, align);

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

        // Barra ANCORADA (largura via anchorMax.x), não Image.Type.Filled: uma Image sem sprite
        // ignora fillAmount e renderia a barra sempre cheia. Refresh seta a fração (progresso/alvo).
        private RectTransform Bar(Transform parent, Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
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
            var lblGo = new GameObject("Label", typeof(RectTransform));
            var lblRect = (RectTransform)lblGo.transform;
            lblRect.SetParent(rect, false);
            lblRect.anchorMin = Vector2.zero; lblRect.anchorMax = Vector2.one;
            lblRect.offsetMin = Vector2.zero; lblRect.offsetMax = Vector2.zero;
            labelText = lblGo.AddComponent<TextMeshProUGUI>();
            labelText.text = label; labelText.fontSize = size; labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = Color.white; labelText.raycastTarget = false;
            return go.GetComponent<Button>();
        }

        private class DayCell
        {
            public int day;
            public Image bg;
            public TMP_Text check;

            public void Refresh(int currentStreakDay, bool claimedToday)
            {
                bool isToday = day == currentStreakDay;
                bool past = day < currentStreakDay || (isToday && claimedToday);
                if (bg != null)
                    bg.color = past ? CellClaimed : (isToday ? CellToday : CellBg);
                if (check != null) check.text = past ? "✓" : (isToday ? "HOJE" : "");
            }
        }

        private class MissionRow
        {
            public string missionId;
            public TMP_Text reward, claimLabel;
            public RectTransform progressFill;   // largura = fração progresso/alvo (anchorMax.x)
            public Image claimImage;
            public Button claim;

            public void Refresh(MetaBridge.MissionView m)
            {
                if (reward != null) reward.text = "+" + m.rewardCoins + " moedas  +" + m.rewardGems + " gemas";
                if (progressFill != null)
                {
                    float frac = m.target > 0 ? Mathf.Clamp01((float)m.progress / m.target) : 0f;
                    progressFill.anchorMax = new Vector2(frac, 1f);
                    progressFill.offsetMin = Vector2.zero;
                    progressFill.offsetMax = Vector2.zero;
                }

                if (claim == null) return;
                bool canClaim = m.complete && !m.claimed;
                claim.interactable = canClaim;
                if (claimImage != null) claimImage.color = m.claimed ? Gold : (canClaim ? Green : Grey);
                if (claimLabel != null)
                {
                    if (m.claimed) { claimLabel.text = "✓ RESGATADO"; claimLabel.color = Color.black; }
                    else if (canClaim) { claimLabel.text = "RESGATAR"; claimLabel.color = Color.white; }
                    else { claimLabel.text = m.progress + "/" + m.target; claimLabel.color = TextSoft; }
                }
            }
        }
    }
}
