using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MutantArmy.Core;
using MutantArmy.Domain;
using MutantArmy.Meta;
using MutantArmy.Services;

namespace MutantArmy.UI
{
    /// <summary>
    /// SettingsScreen (SCR-SISTEMA, doc 09 §4): CONFIGURAÇÕES abertas pela engrenagem do
    /// MainMenuController. Três toggles persistentes — Som (SaveData.sfxOn), Música
    /// (musicOn), Vibração (hapticsOn) — gravados via SaveSystem.MarkDirty e refletidos no
    /// AudioManager em runtime. Mais: RESTAURAR COMPRAS (IAPManager.RestorePurchases — provider
    /// Null no MVP, então o feedback é honesto: "nada a restaurar"), POLÍTICA DE PRIVACIDADE
    /// (placeholder), CRÉDITOS (lista dos pacotes CC0 — KayKit/Quaternius/Kenney) e a versão
    /// do app (Application.version). Estilo casual premium: header com VOLTAR, fundo OPACO
    /// (herdado do panel da factory), botões com UIButtonPop. Toda a lista construída em runtime.
    /// </summary>
    public class SettingsScreen : UIScreen
    {
        [Header("Cabeçalho")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Button _backButton;

        [Header("Toggles (linhas com switch construídas em runtime)")]
        [SerializeField] private RectTransform _togglesContent;     // container com VerticalLayoutGroup

        [Header("Ações")]
        [SerializeField] private Button _restoreButton;
        [SerializeField] private TMP_Text _restoreLabel;
        [SerializeField] private Button _privacyButton;
        [SerializeField] private Button _creditsButton;

        [Header("Créditos (painel deslizante) + versão")]
        [SerializeField] private GameObject _creditsPanel;
        [SerializeField] private TMP_Text _creditsText;
        [SerializeField] private Button _creditsCloseButton;
        [SerializeField] private TMP_Text _versionText;

        public event System.Action BackRequested;

        private bool _built;
        private readonly List<ToggleRow> _rows = new List<ToggleRow>();

        private static readonly Color Green = new Color(0.25f, 0.80f, 0.35f);
        private static readonly Color Grey = new Color(0.35f, 0.38f, 0.45f);
        private static readonly Color Gold = new Color(1.00f, 0.86f, 0.40f);
        private static readonly Color TextSoft = new Color(0.80f, 0.85f, 0.90f);
        private static readonly Color RowBg = new Color(0.10f, 0.12f, 0.20f, 0.96f);

        protected override void Awake()
        {
            base.Awake();
            if (_backButton != null) _backButton.onClick.AddListener(RaiseBack);
            if (_restoreButton != null) _restoreButton.onClick.AddListener(OnRestore);
            if (_privacyButton != null) _privacyButton.onClick.AddListener(OnPrivacy);
            if (_creditsButton != null) _creditsButton.onClick.AddListener(() => ShowCredits(true));
            if (_creditsCloseButton != null) _creditsCloseButton.onClick.AddListener(() => ShowCredits(false));
        }

        protected override void OnShown()
        {
            base.OnShown();
            if (_titleText != null) _titleText.text = "CONFIGURAÇÕES";
            EnsureBuilt();
            Refresh();
            ShowCredits(false);
            if (_versionText != null) _versionText.text = "Versão " + Application.version;
        }

        private void RaiseBack()
        {
            if (BackRequested != null) BackRequested();
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            if (_togglesContent != null)
            {
                _rows.Add(BuildToggleRow("Som", () => Save != null && Save.sfxOn, SetSfx));
                _rows.Add(BuildToggleRow("Música", () => Save != null && Save.musicOn, SetMusic));
                _rows.Add(BuildToggleRow("Vibração", () => Save != null && Save.hapticsOn, SetHaptics));
            }

            if (_creditsText != null) _creditsText.text = BuildCreditsText();
        }

        private void Refresh()
        {
            for (int i = 0; i < _rows.Count; i++) _rows[i].Refresh();
            if (_restoreLabel != null) _restoreLabel.text = "RESTAURAR COMPRAS";
        }

        // ---------------------------------------------------------------- toggles → SaveData via SaveSystem.MarkDirty

        private static SaveData Save => SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;

        // O AudioManager é a fonte de verdade das prefs de áudio: ele grava no save E aplica o
        // mute em runtime (SetSfxOn/SetMusicOn chamam MarkDirty). Sem AudioManager (teste de
        // cena isolada) caímos para a escrita direta no save + MarkDirty.
        private void SetSfx(bool on)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetSfxOn(on);
            else WriteFlag(d => d.sfxOn = on);
        }

        private void SetMusic(bool on)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicOn(on);
            else WriteFlag(d => d.musicOn = on);
        }

        // Vibração não tem manager dedicado (Core.Haptics lê SaveData.hapticsOn direto), então
        // a tela grava no save e marca dirty — mesmo padrão transacional das demais prefs.
        private void SetHaptics(bool on)
        {
            WriteFlag(d => d.hapticsOn = on);
            if (on) Haptics.Light();   // confirma a ativação com um tap leve (no-op fora de Android/iOS)
        }

        private void WriteFlag(System.Action<SaveData> mutate)
        {
            SaveData d = Save;
            if (d == null) return;
            mutate(d);
            if (SaveSystem.Instance != null) SaveSystem.Instance.MarkDirty();
        }

        // ---------------------------------------------------------------- ações

        private void OnRestore()
        {
            if (_restoreLabel != null) _restoreLabel.text = "RESTAURANDO…";
            if (IAPManager.Instance == null)
            {
                SetRestoreResult(false);
                return;
            }
            IAPManager.Instance.RestorePurchases(SetRestoreResult);
        }

        // Provider Null no MVP: RestorePurchases responde false (não há recibos). Feedback
        // honesto, nunca um "sucesso" falso (doc 12 §7.4).
        private void SetRestoreResult(bool restored)
        {
            if (_restoreLabel == null) return;
            _restoreLabel.text = restored ? "✓ COMPRAS RESTAURADAS" : "NADA A RESTAURAR";
        }

        // Placeholder honesto: sem URL real no MVP, o botão confirma a intenção. Quando houver
        // página de privacidade, troca-se por Application.OpenURL aqui.
        private void OnPrivacy()
        {
            if (_creditsText != null) _creditsText.text = PrivacyText();
            ShowCredits(true);
        }

        // O conteúdo do painel é setado por quem abre (créditos = lista de assets; privacidade =
        // texto de privacidade); este método só alterna a visibilidade.
        private void ShowCredits(bool show)
        {
            if (_creditsPanel != null) _creditsPanel.SetActive(show);
        }

        // ---------------------------------------------------------------- créditos (assets CC0)

        // Espelha licencas-de-assets.csv: KayKit (Kay Lousberg), Quaternius e Kenney — todos CC0.
        // Lista curada e honesta; a fonte da verdade é o CSV no repositório.
        private static string BuildCreditsText()
        {
            return
                "<b>CRÉDITOS</b>\n\n" +
                "Arte e áudio sob licença <b>CC0 1.0</b> (domínio público):\n\n" +
                "<b>KayKit</b> — Kay Lousberg\n" +
                "Tropas, esqueletos, dungeon e city builder bits.\n\n" +
                "<b>Quaternius</b>\n" +
                "Bosses (Ultimate Monsters), vegetação (Ultimate Nature) e\n" +
                "animações (Universal Animation Library).\n\n" +
                "<b>Kenney</b>\n" +
                "UI Pack, ícones, partículas e efeitos sonoros.\n\n" +
                "Obrigado a estes criadores por disponibilizarem assets CC0.";
        }

        private static string PrivacyText()
        {
            return
                "<b>POLÍTICA DE PRIVACIDADE</b>\n\n" +
                "Esta build de desenvolvimento não coleta dados pessoais.\n\n" +
                "Na versão publicada, a política completa estará disponível\n" +
                "na loja do app e em um link dedicado.\n\n" +
                "(Placeholder — sem URL real no MVP.)";
        }

        // ---------------------------------------------------------------- construção de linha

        private ToggleRow BuildToggleRow(string label, System.Func<bool> getter, System.Action<bool> setter)
        {
            var go = new GameObject("Toggle_" + label, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_togglesContent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 140f; le.preferredHeight = 140f;
            var bg = go.GetComponent<Image>();
            bg.color = RowBg; bg.raycastTarget = false;

            TMP_Text caption = Label(rect, "Caption", label, 40f, new Vector2(0f, 0.5f),
                new Vector2(40f, 0f), new Vector2(560f, 60f), Color.white, TextAlignmentOptions.Left);
            ((RectTransform)caption.transform).pivot = new Vector2(0f, 0.5f);

            // "Switch" simples: um botão pill que troca ON/OFF e cor (verde/cinza). Evita o
            // componente Toggle padrão (que exige Graphic de checkmark e não combina com o skin).
            var switchGo = new GameObject("Switch", typeof(RectTransform), typeof(Image), typeof(Button));
            var switchRect = (RectTransform)switchGo.transform;
            switchRect.SetParent(rect, false);
            switchRect.anchorMin = new Vector2(1f, 0.5f); switchRect.anchorMax = new Vector2(1f, 0.5f);
            switchRect.pivot = new Vector2(1f, 0.5f);
            switchRect.anchoredPosition = new Vector2(-40f, 0f);
            switchRect.sizeDelta = new Vector2(260f, 96f);
            var switchImg = switchGo.GetComponent<Image>();
            switchImg.color = Grey;
            switchGo.AddComponent<UIButtonPop>();

            TMP_Text state = Label(switchRect, "State", "OFF", 34f, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(260f, 96f), Color.white, TextAlignmentOptions.Center);
            ((RectTransform)state.transform).anchorMin = Vector2.zero;
            ((RectTransform)state.transform).anchorMax = Vector2.one;
            ((RectTransform)state.transform).offsetMin = Vector2.zero;
            ((RectTransform)state.transform).offsetMax = Vector2.zero;

            var row = new ToggleRow
            {
                getter = getter,
                setter = setter,
                switchImage = switchImg,
                stateLabel = state
            };
            switchGo.GetComponent<Button>().onClick.AddListener(() =>
            {
                bool next = !row.getter();
                row.setter(next);
                row.Refresh();
            });
            row.Refresh();
            return row;
        }

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

        private class ToggleRow
        {
            public System.Func<bool> getter;
            public System.Action<bool> setter;
            public Image switchImage;
            public TMP_Text stateLabel;

            public void Refresh()
            {
                bool on = getter != null && getter();
                if (switchImage != null) switchImage.color = on ? Green : Grey;
                if (stateLabel != null) stateLabel.text = on ? "ON" : "OFF";
            }
        }
    }
}
