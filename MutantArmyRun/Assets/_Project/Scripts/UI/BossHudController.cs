using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MutantArmy.Core;
using MutantArmy.Domain;

namespace MutantArmy.UI
{
    /// <summary>
    /// HUD do boss (missão Nota 10): barra de HP grande no topo, nome + fraqueza ativa e o
    /// aviso piscante de ataque especial. Aparece SÓ em GameState.BossFight (assina
    /// GameManager.StateEntered/StateExited) e atualiza 100% por evento do bus — zero polling
    /// (doc 12 §3.2): OnBossHpChanged (fill), OnBossPhaseChanged (cor por fase + fraqueza
    /// rotativa), OnBossSpecialWarning (banner), OnRareBossAnnounced ("[RARO]" no nome).
    /// Fronteira dura: UI não enxerga Gameplay (asmdef, doc 12 §2.3) — o boss vem de
    /// GameManager.CurrentLevel.boss (Core) e os limiares de fase são hardcoded.
    /// Greybox-friendly: se a factory da Onda 4 ainda não ligou os campos serializados,
    /// EnsureUi monta a própria hierarquia em código na 1ª luta.
    /// </summary>
    public class BossHudController : MonoBehaviour
    {
        // Limiares canônicos de fase do boss — 0.5/0.25 (CONTRACT §1 item 14, espelha
        // Domain.CombatMath.BossPhase). BossManager.PhaseThresholds é Gameplay e a UI
        // NÃO pode referenciar — o valor é contrato, não tuning.
        private const float Phase1Threshold = 0.5f;
        private const float Phase2Threshold = 0.25f;

        [Header("Raiz (ligada pela factory da Onda 4 — null = fallback em código)")]
        [SerializeField] private CanvasGroup _rootGroup;     // alpha 0/1 mostra/esconde (sem SetActive por frame)
        [SerializeField] private TMP_Text _bossNameText;
        [SerializeField] private TMP_Text _weaknessText;     // "FRACO: FOGO" tintado pela cor do elemento
        [SerializeField] private Image _hpFillImage;         // cor por fase (verde→amarelo→vermelho)
        [SerializeField] private RectTransform _hpFillRect;  // fill ANCORADO: anchorMax.x = HP normalizado
        [SerializeField] private TMP_Text _warningText;      // "!! ATAQUE ESPECIAL !!"
        [SerializeField] private CanvasGroup _warningGroup;

        [Header("Cores por fase (0.5/0.25)")]
        [SerializeField] private Color _phase0Color = new Color(0.35f, 0.90f, 0.40f);
        [SerializeField] private Color _phase1Color = new Color(1.00f, 0.80f, 0.20f);
        [SerializeField] private Color _phase2Color = new Color(0.95f, 0.30f, 0.25f);

        [Header("Tuning")]
        [SerializeField] private float _fadeSeconds = 0.2f;
        [SerializeField] private float _warningBlinkPeriod = 0.35f;

        private bool _subscribedToGameManager;
        private bool _visible;
        private bool _fallbackBuilt;
        private string _rareBossId;       // bossId anunciado como raro nesta corrida (BossScout)
        private Coroutine _fadeRoutine;
        private Coroutine _warningRoutine;

        private void OnEnable()
        {
            GameEvents.OnBossHpChanged += HandleBossHpChanged;
            GameEvents.OnBossPhaseChanged += HandleBossPhaseChanged;
            GameEvents.OnBossSpecialWarning += HandleSpecialWarning;
            GameEvents.OnRareBossAnnounced += HandleRareBossAnnounced;
            TrySubscribeGameManager();
        }

        private void OnDisable()
        {
            GameEvents.OnBossHpChanged -= HandleBossHpChanged;     // bus estático: sempre limpar
            GameEvents.OnBossPhaseChanged -= HandleBossPhaseChanged;
            GameEvents.OnBossSpecialWarning -= HandleSpecialWarning;
            GameEvents.OnRareBossAnnounced -= HandleRareBossAnnounced;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StateEntered -= HandleStateEntered;
                GameManager.Instance.StateExited -= HandleStateExited;
                GameManager.Instance.LevelStarted -= HandleLevelStarted;
            }
            _subscribedToGameManager = false;
            StopAllCoroutines();
            _fadeRoutine = null;
            _warningRoutine = null;
            _visible = false;
            if (_rootGroup != null) _rootGroup.alpha = 0f;
            if (_warningGroup != null) _warningGroup.alpha = 0f;
        }

        // GameManager persiste do Boot; se a cena Game abrir direto no editor ele pode não
        // existir no OnEnable — re-tenta no Update (idempotente, -= antes de +=), mesmo
        // padrão do TutorialController. StateEntered(BossFight) é o gatilho: nunca pode escapar.
        private void Update()
        {
            TrySubscribeGameManager();
        }

        private void TrySubscribeGameManager()
        {
            if (_subscribedToGameManager || GameManager.Instance == null) return;
            GameManager.Instance.StateEntered -= HandleStateEntered;
            GameManager.Instance.StateEntered += HandleStateEntered;
            GameManager.Instance.StateExited -= HandleStateExited;
            GameManager.Instance.StateExited += HandleStateExited;
            GameManager.Instance.LevelStarted -= HandleLevelStarted;
            GameManager.Instance.LevelStarted += HandleLevelStarted;
            _subscribedToGameManager = true;
        }

        // ---------------------------------------------------------------- estados

        // Nova fase: limpa o anúncio de raro ANTES do BossScout (LevelStarted dispara no
        // StartLevel, antes do ChangeState — não pode ser no StateEntered(BossScout), onde o
        // BossManager pode já ter publicado o RareBossAnnounce na MESMA invocação) e esconde
        // o HUD (RestartLevelFromAnyState zera a pilha SEM StateExited(BossFight)).
        private void HandleLevelStarted(int levelIndex)
        {
            _rareBossId = null;
            if (_visible) Hide();
        }

        private void HandleStateEntered(GameState state)
        {
            if (state == GameState.BossFight) ShowForCurrentBoss();
        }

        private void HandleStateExited(GameState state)
        {
            // ReviveOffer é PUSH sobre o BossFight (não dispara StateExited(BossFight)) —
            // o HUD do boss segue visível durante a oferta, como deve.
            if (state == GameState.BossFight) Hide();
        }

        private void ShowForCurrentBoss()
        {
            EnsureUi();
            if (_rootGroup == null) return;     // sem RectTransform pai: degrada em silêncio

            BossConfigSO boss = CurrentBoss();
            BindBoss(boss);
            SetFill(1f);                        // OnBossHpChanged publica 1.0 no BeginFight; aqui é o estado inicial
            ApplyPhaseColor(0);

            _visible = true;
            StartFade(_rootGroup, 1f);
        }

        private void Hide()
        {
            _visible = false;
            StopWarning();
            if (_rootGroup != null) StartFade(_rootGroup, 0f);
        }

        // ---------------------------------------------------------------- eventos do bus

        private void HandleBossHpChanged(float normalizedHp)
        {
            if (!_visible) return;
            SetFill(Mathf.Clamp01(normalizedHp));
        }

        private void HandleBossPhaseChanged(BossPhase phase)
        {
            if (!_visible) return;
            ApplyPhaseColor(phase.phase);
            // Fraqueza ROTATIVA (Alien Supremo, CANON §6): o texto acompanha a fase.
            if (phase.activeWeakness != ElementType.None) SetWeakness(phase.activeWeakness);
        }

        private void HandleSpecialWarning(BossSpecialTelegraph telegraph)
        {
            if (!_visible || _warningGroup == null) return;
            StopWarning();
            _warningRoutine = StartCoroutine(WarningRoutine(Mathf.Max(0.2f, telegraph.seconds)));
        }

        private void HandleRareBossAnnounced(RareBossAnnounce announce)
        {
            _rareBossId = announce.bossId;      // o BindBoss (BossFight) decora o nome com "[RARO]"
        }

        // ---------------------------------------------------------------- visual

        private void BindBoss(BossConfigSO boss)
        {
            if (_bossNameText != null)
            {
                string name = DisplayName(boss);
                bool rare = boss != null && !string.IsNullOrEmpty(_rareBossId) && _rareBossId == boss.bossId;
                // "[RARO]" em texto puro (sem ★: glifo ausente na fonte default do TMP);
                // o roxo é o mesmo do feedback "BOSS RARO!" — cor reforça, nome informa.
                _bossNameText.text = rare ? name + " [RARO]" : name;
                _bossNameText.color = rare ? new Color(0.80f, 0.45f, 1.00f) : Color.white;
            }

            ElementType weakness = boss != null && boss.weaknesses != null && boss.weaknesses.Length > 0
                ? boss.weaknesses[0]
                : ElementType.None;
            SetWeakness(weakness);
        }

        private void SetWeakness(ElementType element)
        {
            if (_weaknessText == null) return;
            if (element == ElementType.None)
            {
                _weaknessText.text = string.Empty;      // boss sem fraqueza: linha some, não mente
                return;
            }
            // NOME informa, COR reforça (doc 09 P7) — nunca só cor.
            _weaknessText.text = "FRACO: " + BossScoutOverlay.ElementNamePt(element);
            _weaknessText.color = BossScoutOverlay.ElementColorPt(element);
        }

        private void SetFill(float normalized)
        {
            // Caminho da factory (Image.Type.Filled) e do fallback (fill ancorado) — os dois
            // são suportados para o wiring da Onda 4 escolher o estilo.
            if (_hpFillImage != null && _hpFillImage.type == Image.Type.Filled)
                _hpFillImage.fillAmount = normalized;
            else if (_hpFillRect != null)
                _hpFillRect.anchorMax = new Vector2(Mathf.Max(0.001f, normalized), 1f);
        }

        private void ApplyPhaseColor(int phase)
        {
            if (_hpFillImage == null) return;
            Color c = phase >= 2 ? _phase2Color : phase == 1 ? _phase1Color : _phase0Color;
            _hpFillImage.color = c;
        }

        private void StartFade(CanvasGroup group, float to)
        {
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            if (!isActiveAndEnabled)
            {
                group.alpha = to;       // objeto desabilitado: aplica direto, sem coroutine
                return;
            }
            _fadeRoutine = StartCoroutine(UIUtils.FadeRoutine(group, group.alpha, to, _fadeSeconds, null));
        }

        // Banner do especial: pisca em seno por toda a janela do telegraph (unscaled —
        // convive com qualquer slow-mo) e some junto do golpe. "!!" no lugar de "⚠":
        // a fonte default do TMP (LiberationSans) não tem o glifo U+26A0 — quadradinho
        // + warning de glifo ausente em todo telegraph.
        private IEnumerator WarningRoutine(float seconds)
        {
            if (_warningText != null) _warningText.text = "!! ATAQUE ESPECIAL !!";
            float elapsed = 0f;
            float omega = _warningBlinkPeriod > 0f ? (2f * Mathf.PI) / _warningBlinkPeriod : 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_warningGroup != null)
                    _warningGroup.alpha = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(elapsed * omega * 0.5f));
                yield return null;
            }
            if (_warningGroup != null) _warningGroup.alpha = 0f;
            _warningRoutine = null;
        }

        private void StopWarning()
        {
            if (_warningRoutine != null)
            {
                StopCoroutine(_warningRoutine);
                _warningRoutine = null;
            }
            if (_warningGroup != null) _warningGroup.alpha = 0f;
        }

        // ---------------------------------------------------------------- dados (Core, permitido)

        private static BossConfigSO CurrentBoss()
        {
            GameManager gm = GameManager.Instance;
            LevelConfigSO level = gm != null ? gm.CurrentLevel : null;
            return level != null ? level.boss : null;
        }

        private static string DisplayName(BossConfigSO boss)
        {
            if (boss == null) return "BOSS";
            if (!string.IsNullOrEmpty(boss.displayName)) return boss.displayName.ToUpperInvariant();
            return string.IsNullOrEmpty(boss.bossId) ? "BOSS" : boss.bossId.Replace('_', ' ').ToUpperInvariant();
        }

        // ---------------------------------------------------------------- fallback greybox

        /// <summary>
        /// Monta a hierarquia em código quando a factory (Onda 4) ainda não ligou os campos —
        /// o HUD do boss funciona em qualquer cena greybox. Roda 1× e só se _rootGroup == null.
        /// </summary>
        private void EnsureUi()
        {
            if (_rootGroup != null || _fallbackBuilt) return;
            _fallbackBuilt = true;

            RectTransform parent = transform as RectTransform;
            if (parent == null) return;     // fora de canvas: sem HUD do boss (degrada, não quebra)

            // P2-UILAYOUT — Raiz BEM abaixo do HUD de corrida: o badge da contagem do exército
            // termina em y≈-250 e a barra de Supply em y≈-310 (BuildHudElements). ANTES a raiz
            // ficava em -110 e a barra caía ~-160, COLIDINDO com o badge "60"/Supply (bug do
            // screenshot). Agora a raiz começa em -322 (mesma faixa do BuildBossHud da factory):
            // nome -322, barra -372, fraqueza -428 — sem tocar o HUD de corrida acima.
            var rootGo = new GameObject("BossHud", typeof(RectTransform));
            var root = (RectTransform)rootGo.transform;
            root.SetParent(parent, false);
            root.anchorMin = new Vector2(0.5f, 1f);
            root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -322f);
            root.sizeDelta = new Vector2(760f, 150f);

            _rootGroup = rootGo.AddComponent<CanvasGroup>();
            _rootGroup.alpha = 0f;
            _rootGroup.interactable = false;
            _rootGroup.blocksRaycasts = false;      // AutoPilot joga por baixo intacto (doc 14 §6)

            // Nome do boss no TOPO da raiz (a faixa de corrida já terminou acima).
            _bossNameText = CreateText(root, "BossName", 40f, Color.white,
                new Vector2(0f, 0f), new Vector2(760f, 46f));

            // Fundo da barra de HP — 700×46 (mesma medida da factory), abaixo do nome.
            var bgGo = new GameObject("HpBarBg", typeof(RectTransform));
            var bg = (RectTransform)bgGo.transform;
            bg.SetParent(root, false);
            bg.anchorMin = new Vector2(0.5f, 1f);
            bg.anchorMax = new Vector2(0.5f, 1f);
            bg.pivot = new Vector2(0.5f, 1f);
            bg.anchoredPosition = new Vector2(0f, -50f);
            bg.sizeDelta = new Vector2(700f, 46f);
            Image bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(0.08f, 0.09f, 0.13f, 0.92f);
            bgImage.raycastTarget = false;

            // Fill ANCORADO (anchorMax.x = HP) — Image.Type.Filled exige sprite e o fallback
            // não carrega asset nenhum (mesma técnica das barras da RewardScreensFactory).
            var fillGo = new GameObject("HpBarFill", typeof(RectTransform));
            _hpFillRect = (RectTransform)fillGo.transform;
            _hpFillRect.SetParent(bg, false);
            _hpFillRect.anchorMin = Vector2.zero;
            _hpFillRect.anchorMax = Vector2.one;
            _hpFillRect.offsetMin = new Vector2(4f, 4f);
            _hpFillRect.offsetMax = new Vector2(-4f, -4f);
            _hpFillImage = fillGo.AddComponent<Image>();
            _hpFillImage.color = _phase0Color;
            _hpFillImage.raycastTarget = false;

            // Marcadores nos limiares de fase 0.5/0.25 (CONTRACT §1 item 14 — hardcode
            // consciente: a UI não enxerga BossManager.PhaseThresholds, que é Gameplay).
            CreateMarker(bg, "Marker50", Phase1Threshold);
            CreateMarker(bg, "Marker25", Phase2Threshold);

            // Fraqueza ativa, abaixo da barra (rotativa via OnBossPhaseChanged).
            _weaknessText = CreateText(root, "WeaknessTag", 28f, Color.white,
                new Vector2(0f, -104f), new Vector2(760f, 34f));

            // Aviso do especial, com CanvasGroup próprio para piscar sem mexer no resto.
            _warningText = CreateText(root, "SpecialWarning", 34f, new Color(1.00f, 0.35f, 0.20f),
                new Vector2(0f, -148f), new Vector2(760f, 40f));
            _warningGroup = _warningText.gameObject.AddComponent<CanvasGroup>();
            _warningGroup.alpha = 0f;
            _warningGroup.interactable = false;
            _warningGroup.blocksRaycasts = false;
            _warningText.text = "!! ATAQUE ESPECIAL !!";
        }

        private static TMP_Text CreateText(RectTransform parent, string name, float fontSize, Color color,
                                           Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var text = go.AddComponent<TextMeshProUGUI>();      // fonte default do TMP Settings
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private static void CreateMarker(RectTransform bar, string name, float normalizedX)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(bar, false);
            rect.anchorMin = new Vector2(normalizedX, 0f);
            rect.anchorMax = new Vector2(normalizedX, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(3f, 0f);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.65f);
            img.raycastTarget = false;
        }
    }
}
