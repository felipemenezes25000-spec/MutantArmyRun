using System.Collections;
using MutantArmy.Core;
using MutantArmy.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MutantArmy.UI
{
    /// <summary>
    /// FTUE / onboarding sem texto longo (doc 14 §6) + diretor de tutorial CONTEXTUAL
    /// multi-passo (missão Nota 10). Duas camadas independentes:
    ///
    /// 1) Onboarding legado da fase 1 (inalterado — o wiring da JuiceFactory depende dos
    ///    campos): dica ARRASTE (mão deslizando ~2 s) + callout ESCOLHA! até o 1º portal;
    ///    persiste tutorialSeen (bool legado) no fim da 1ª fase.
    ///
    /// 2) Diretor contextual: 1 dica curta (3-5 palavras) por passo, cada passo é um BIT em
    ///    SaveData.tutorialStepMask (mostra 1×, marca o bit na hora, nunca reaparece):
    ///    bit0 F1 "Pegue mais tropas!" · bit1 F2 "Multiplique!" · bit2 F3 "Fogo vence
    ///    madeira!" · bit3 F4 "Destrua os inimigos!" · bit4 F5 "Cuidado com o Suprimento!".
    ///    A dica some quando o jogador AGE (portal consumido / inimigo destruído) ou após
    ///    ~4 s — nunca bloqueia gameplay. Compatível com o tutorialSeen legado: quem já viu
    ///    o onboarding antigo não revê o passo F1 (bit0 tratado como visto).
    ///
    /// Camada e fronteiras: vive no HudCanvas (UI). Lê o estado de jogo só pelo bus do Core
    /// (GameManager.LevelStarted/StateEntered + GameEvents) e a persistência pelo blackboard
    /// (GameBootstrap.Current.Save + MarkSaveDirty) — UI não enxerga Gameplay/Meta concretos
    /// (§2.3), mas SaveData é Domain. Tudo cosmético com raycastTarget OFF: o AutoPilot do
    /// DevScreenshotRig continua jogando por baixo intacto. O banner do passo é construído
    /// em código se a factory (Onda 4) ainda não ligou os campos — greybox-friendly.
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        [Header("Dica (a): ARRASTE — mão/seta que desliza lateralmente")]
        [SerializeField] private RectTransform _dragHint;       // raiz da mão + rótulo "ARRASTE"
        [SerializeField] private CanvasGroup _dragHintGroup;    // fade in/out sem mexer no SetActive por frame
        [SerializeField] private float _dragTravel = 220f;      // amplitude do vai-e-vem (px da tela 1080 de ref)
        [SerializeField] private float _dragPeriod = 1.1f;      // 1 ciclo ida/volta
        [SerializeField] private float _dragSeconds = 2.4f;     // dura ~2 s da corrida e some

        [Header("Dica (b): ESCOLHA! — destaque pulsante no 1º par de portais")]
        [SerializeField] private RectTransform _chooseHint;     // raiz do callout "ESCOLHA!" + setas
        [SerializeField] private CanvasGroup _chooseHintGroup;
        [SerializeField] private float _choosePulseAmplitude = 0.12f;
        [SerializeField] private float _choosePulsePeriod = 0.7f;

        [Header("Fades")]
        [SerializeField] private float _fadeSeconds = 0.25f;

        [Header("Diretor contextual (banner — Onda 4 pode ligar; null = fallback em código)")]
        [SerializeField] private RectTransform _stepHint;       // holder do banner da dica
        [SerializeField] private CanvasGroup _stepHintGroup;
        [SerializeField] private TMP_Text _stepHintText;
        [SerializeField] private float _stepSeconds = 4f;       // a dica some sozinha após ~4 s

        private const int FirstLevelIndex = 1;

        // Bits de SaveData.tutorialStepMask (contrato da missão — append-only, como enum).
        private const int StepGates = 0;        // F1: 1º par de portais
        private const int StepMultiply = 1;     // F2: portal ×2
        private const int StepElement = 2;      // F3: elemento vs boss
        private const int StepEnemies = 3;      // F4: inimigos de pista
        private const int StepSupply = 4;       // F5: estouro de Supply

        private bool _activeRun;          // onboarding legado rodando NESTA corrida
        private bool _firstGateSeen;
        private bool _subscribedToGameManager;
        private Vector2 _dragHintHome;    // posição de descanso da mão (centro do vai-e-vem)
        private Coroutine _dragRoutine;
        private Coroutine _chooseRoutine;

        private int _currentLevel;
        private int _activeStep = -1;     // passo contextual na tela (-1 = nenhum); nunca 2 ao mesmo tempo
        private bool _stepUiBuilt;
        private Coroutine _stepRoutine;

        private void Awake()
        {
            if (_dragHint != null) _dragHintHome = _dragHint.anchoredPosition;
            HideImmediate(_dragHintGroup, _dragHint);
            HideImmediate(_chooseHintGroup, _chooseHint);
            HideImmediate(_stepHintGroup, _stepHint);
        }

        private void OnEnable()
        {
            GameEvents.OnGateConsumed += HandleGateConsumed;
            GameEvents.OnLevelFinished += HandleLevelFinished;
            GameEvents.OnSupplyOverflow += HandleSupplyOverflow;
            GameEvents.OnTrackEnemyKilled += HandleTrackEnemyKilled;
            TrySubscribeGameManager();
        }

        private void OnDisable()
        {
            GameEvents.OnGateConsumed -= HandleGateConsumed;   // bus estático sobrevive a cenas — sempre limpar
            GameEvents.OnLevelFinished -= HandleLevelFinished;
            GameEvents.OnSupplyOverflow -= HandleSupplyOverflow;
            GameEvents.OnTrackEnemyKilled -= HandleTrackEnemyKilled;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LevelStarted -= HandleLevelStarted;
                GameManager.Instance.StateEntered -= HandleStateEntered;
            }
            _subscribedToGameManager = false;
            StopAllCoroutines();
            _dragRoutine = null;
            _chooseRoutine = null;
            _stepRoutine = null;
            _activeStep = -1;
            if (_stepHintGroup != null) _stepHintGroup.alpha = 0f;
        }

        // GameManager persiste do Boot; se a cena Game abrir direto no editor ele pode não
        // existir no OnEnable — re-tenta no Update (idempotente, -= antes de +=), igual ao
        // JuiceController. LevelStarted é o gatilho do tutorial: nunca pode escapar.
        private void Update()
        {
            TrySubscribeGameManager();
        }

        private void TrySubscribeGameManager()
        {
            if (_subscribedToGameManager || GameManager.Instance == null) return;
            GameManager.Instance.LevelStarted -= HandleLevelStarted;
            GameManager.Instance.LevelStarted += HandleLevelStarted;
            GameManager.Instance.StateEntered -= HandleStateEntered;
            GameManager.Instance.StateEntered += HandleStateEntered;
            _subscribedToGameManager = true;
        }

        // ------------------------------------------------------------------ gatilhos do bus

        private void HandleLevelStarted(int levelIndex)
        {
            _currentLevel = levelIndex;
            DismissActiveStep();    // nova fase nunca herda banner da anterior

            // Onboarding legado: só a 1ª corrida da fase 1, e só se nunca visto.
            if (levelIndex != FirstLevelIndex || TutorialAlreadySeen())
            {
                EndRun(persist: false);
                return;
            }
            BeginRun();
        }

        // O passo contextual da fase aparece quando a CORRIDA começa de fato (Running vem
        // depois do cartão Boss Scout — a dica não disputa atenção com o cartão).
        private void HandleStateEntered(GameState state)
        {
            if (state != GameState.Running) return;
            switch (_currentLevel)
            {
                case 1: TryShowStep(StepGates, "Pegue mais tropas!"); break;
                case 2: TryShowStep(StepMultiply, "Multiplique!"); break;
                case 3: TryShowStep(StepElement, "Fogo vence madeira!"); break;
                case 4: TryShowStep(StepEnemies, "Destrua os inimigos!"); break;
            }
        }

        private void HandleGateConsumed(GateResult result)
        {
            // Jogador agiu num portal: dicas de portal (F1/F2/F3) cumpriram o papel.
            if (_activeStep == StepGates || _activeStep == StepMultiply || _activeStep == StepElement)
                DismissActiveStep();

            if (!_activeRun || _firstGateSeen) return;
            _firstGateSeen = true;
            // Aprendeu a escolher: a dica de ESCOLHA cumpriu o papel, recolhe com fade.
            FadeOut(_chooseHintGroup, _chooseHint, ref _chooseRoutine);
        }

        private void HandleTrackEnemyKilled(TrackEnemyKilled kill)
        {
            // Primeiro inimigo de pista destruído: a dica da F4 já ensinou.
            if (_activeStep == StepEnemies) DismissActiveStep();
        }

        private void HandleSupplyOverflow(SupplyOverflow overflow)
        {
            // 1º estouro de Supply da vida do jogador: explica o sistema na hora em que
            // ele acontece (contextual de verdade) — some sozinho após ~4 s.
            TryShowStep(StepSupply, "Cuidado com o Suprimento!");
        }

        private void HandleLevelFinished(LevelResult result)
        {
            DismissActiveStep();
            if (!_activeRun) return;
            // Fim da 1ª fase: dicas somem e tutorialSeen é gravado — onboarding acabou (doc 14 §6).
            EndRun(persist: true);
        }

        // ------------------------------------------------------------------ ciclo de vida do onboarding legado

        private void BeginRun()
        {
            _activeRun = true;
            _firstGateSeen = false;

            // (a) ARRASTE: vai-e-vem por ~2 s e some sozinho.
            StopRoutine(ref _dragRoutine);
            if (_dragHint != null)
            {
                _dragHint.anchoredPosition = _dragHintHome;
                _dragRoutine = StartCoroutine(DragHintRoutine());
            }

            // (b) ESCOLHA!: pulsa desde já até o 1º portal (HandleGateConsumed recolhe).
            StopRoutine(ref _chooseRoutine);
            if (_chooseHint != null)
            {
                _chooseHint.localScale = Vector3.one;   // descarta escala residual de um pulso interrompido
                _chooseRoutine = StartCoroutine(ChooseHintRoutine());
            }
        }

        private void EndRun(bool persist)
        {
            bool wasActive = _activeRun;
            _activeRun = false;
            FadeOut(_dragHintGroup, _dragHint, ref _dragRoutine);
            FadeOut(_chooseHintGroup, _chooseHint, ref _chooseRoutine);
            if (persist && wasActive) PersistTutorialSeen();
        }

        // Mão desliza esquerda↔direita (seno) por _dragSeconds, com fade in no começo e
        // fade out ao terminar — tempo UNSCALED para conviver com qualquer slow-mo.
        private IEnumerator DragHintRoutine()
        {
            yield return Fade(_dragHintGroup, _dragHint, 0f, 1f, _fadeSeconds);

            float elapsed = 0f;
            float omega = _dragPeriod > 0f ? (2f * Mathf.PI) / _dragPeriod : 0f;
            while (elapsed < _dragSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_dragHint != null)
                {
                    float x = Mathf.Sin(elapsed * omega) * _dragTravel;
                    _dragHint.anchoredPosition = _dragHintHome + new Vector2(x, 0f);
                }
                yield return null;
            }

            yield return Fade(_dragHintGroup, _dragHint, 1f, 0f, _fadeSeconds);
            if (_dragHint != null)
            {
                _dragHint.anchoredPosition = _dragHintHome;
                _dragHint.gameObject.SetActive(false);
            }
            _dragRoutine = null;
        }

        // Callout "ESCOLHA!" pulsa em escala até ser recolhido (1º portal ou fim da fase).
        private IEnumerator ChooseHintRoutine()
        {
            yield return Fade(_chooseHintGroup, _chooseHint, 0f, 1f, _fadeSeconds);

            Vector3 baseScale = _chooseHint != null ? _chooseHint.localScale : Vector3.one;
            float t = 0f;
            float omega = _choosePulsePeriod > 0f ? (2f * Mathf.PI) / _choosePulsePeriod : 0f;
            while (true)
            {
                t += Time.unscaledDeltaTime;
                if (_chooseHint != null)
                {
                    float s = 1f + Mathf.Sin(t * omega) * _choosePulseAmplitude;
                    _chooseHint.localScale = baseScale * s;
                }
                yield return null;
            }
        }

        // ------------------------------------------------------------------ diretor contextual (passos por bit)

        // Mostra o passo se: bit ainda não visto, nenhum outro passo na tela. Marca o bit NA
        // HORA (mostrou = viu, mesmo que o jogador aja antes dos 4 s) — contrato "1× só".
        private void TryShowStep(int bit, string text)
        {
            if (_activeStep != -1) return;          // nunca 2 dicas ao mesmo tempo (doc 09 §4.2)
            if (StepAlreadySeen(bit)) return;

            EnsureStepHintUi();
            if (_stepHintText == null || _stepHintGroup == null || _stepHint == null) return;   // sem UI: degrada

            MarkStepSeen(bit);
            _activeStep = bit;
            _stepHintText.text = text;

            StopRoutine(ref _stepRoutine);
            _stepRoutine = StartCoroutine(StepRoutine());
        }

        // Banner do passo: fade in → segura ~4 s (unscaled) → fade out. O dismiss por ação
        // do jogador (DismissActiveStep) corta a espera no meio.
        private IEnumerator StepRoutine()
        {
            yield return Fade(_stepHintGroup, _stepHint, 0f, 1f, _fadeSeconds);

            float elapsed = 0f;
            while (elapsed < _stepSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return Fade(_stepHintGroup, _stepHint, 1f, 0f, _fadeSeconds);
            if (_stepHint != null) _stepHint.gameObject.SetActive(false);
            _activeStep = -1;
            _stepRoutine = null;
        }

        private void DismissActiveStep()
        {
            if (_activeStep == -1) return;
            _activeStep = -1;
            StopRoutine(ref _stepRoutine);
            FadeOut(_stepHintGroup, _stepHint, ref _stepRoutine);
        }

        // Constrói o banner em código quando a factory (Onda 4) ainda não ligou os campos —
        // mesmo padrão greybox-friendly do BossHudController. Topo central, abaixo do HUD do boss.
        private void EnsureStepHintUi()
        {
            if (_stepHint != null || _stepUiBuilt) return;
            _stepUiBuilt = true;

            RectTransform parent = transform as RectTransform;
            if (parent == null) return;     // fora de canvas: sem banner (degrada, não quebra)

            var go = new GameObject("StepHint", typeof(RectTransform));
            _stepHint = (RectTransform)go.transform;
            _stepHint.SetParent(parent, false);
            _stepHint.anchorMin = new Vector2(0.5f, 1f);
            _stepHint.anchorMax = new Vector2(0.5f, 1f);
            _stepHint.pivot = new Vector2(0.5f, 1f);
            _stepHint.anchoredPosition = new Vector2(0f, -270f);
            _stepHint.sizeDelta = new Vector2(820f, 110f);

            _stepHintGroup = go.AddComponent<CanvasGroup>();
            _stepHintGroup.alpha = 0f;
            _stepHintGroup.interactable = false;
            _stepHintGroup.blocksRaycasts = false;      // AutoPilot joga por baixo intacto

            // Faixa escura translúcida atrás do texto: legível sobre qualquer pista.
            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.06f, 0.10f, 0.62f);
            bg.raycastTarget = false;

            var textGo = new GameObject("Label", typeof(RectTransform));
            var textRect = (RectTransform)textGo.transform;
            textRect.SetParent(_stepHint, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var label = textGo.AddComponent<TextMeshProUGUI>();     // fonte default do TMP Settings
            label.fontSize = 52f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.86f, 0.35f);              // âmbar do HUD
            label.raycastTarget = false;
            _stepHintText = label;

            _stepHint.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ fade helpers

        private void FadeOut(CanvasGroup group, RectTransform root, ref Coroutine routine)
        {
            StopRoutine(ref routine);
            if (root == null) return;
            routine = StartCoroutine(FadeOutRoutine(group, root));
        }

        private IEnumerator FadeOutRoutine(CanvasGroup group, RectTransform root)
        {
            float from = group != null ? group.alpha : 1f;
            yield return Fade(group, root, from, 0f, _fadeSeconds);
            if (root != null) root.gameObject.SetActive(false);
        }

        private static IEnumerator Fade(CanvasGroup group, RectTransform root, float from, float to, float seconds)
        {
            if (root != null) root.gameObject.SetActive(true);
            if (group != null) group.alpha = from;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = seconds > 0f ? Mathf.Clamp01(elapsed / seconds) : 1f;
                if (group != null) group.alpha = Mathf.Lerp(from, to, k);
                yield return null;
            }
            if (group != null) group.alpha = to;
        }

        private void StopRoutine(ref Coroutine routine)
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
        }

        private static void HideImmediate(CanvasGroup group, RectTransform root)
        {
            if (group != null) group.alpha = 0f;
            if (root != null) root.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ persistência (blackboard do Core)

        private static bool TutorialAlreadySeen()
        {
            SaveData save = ActiveSave();
            return save != null && save.tutorialSeen;   // sem save publicado: mostra (default false)
        }

        private void PersistTutorialSeen()
        {
            GameBootstrap root = GameBootstrap.Current;
            if (root == null || root.Save == null) return;
            if (root.Save.tutorialSeen) return;
            root.Save.tutorialSeen = true;
            root.MarkSaveDirty?.Invoke();   // flush real fica nas transições de estado (doc 12 §4.7)
        }

        // Máscara efetiva: o bool legado conta como bit0 visto — quem já passou pelo
        // onboarding antigo da fase 1 não revê "Pegue mais tropas!" (não regredir).
        private static bool StepAlreadySeen(int bit)
        {
            SaveData save = ActiveSave();
            if (save == null) return false;     // sem save publicado: mostra (mesma regra do legado)
            int mask = save.tutorialStepMask | (save.tutorialSeen ? 1 << StepGates : 0);
            return (mask & (1 << bit)) != 0;
        }

        private static void MarkStepSeen(int bit)
        {
            GameBootstrap root = GameBootstrap.Current;
            if (root == null || root.Save == null) return;
            int flag = 1 << bit;
            if ((root.Save.tutorialStepMask & flag) != 0) return;
            root.Save.tutorialStepMask |= flag;
            root.MarkSaveDirty?.Invoke();   // flush real nas transições de estado (doc 12 §4.7)
        }

        private static SaveData ActiveSave()
        {
            GameBootstrap root = GameBootstrap.Current;
            return root != null ? root.Save : null;
        }
    }
}
