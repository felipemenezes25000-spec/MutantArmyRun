using System.Collections;
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.UI
{
    /// <summary>
    /// FTUE / onboarding sem texto longo (doc 14 §6): na PRIMEIRA corrida da fase 1, mostra
    /// duas dicas visuais NÃO BLOQUEANTES sobre o HUD e some sozinho ao fim da fase.
    /// (a) ARRASTE — uma mão/seta deslizando da esquerda para a direita por ~2 s, ensinando
    ///     o movimento lateral (o mesmo gesto que o CrowdAnchor lê em Input.GetMouseButton).
    /// (b) ESCOLHA! — um destaque pulsante apontando que dá pra escolher o lado no 1º par de
    ///     portais; aparece junto do início da corrida e some ao consumir o primeiro portal.
    /// (c) Ambos somem no fim da 1ª fase, e tutorialSeen é gravado true (SaveData, aditivo) —
    ///     nunca mais reaparece.
    ///
    /// Camada e fronteiras: vive no HudCanvas (UI). Lê o estado de jogo só pelo bus do Core
    /// (GameManager.LevelStarted + GameEvents.OnGateConsumed/OnLevelFinished) e a flag pelo
    /// blackboard (GameBootstrap.Current.Save) — UI não enxerga Gameplay/Meta concretos (§2.3),
    /// mas SaveData é Domain. Tudo é cosmético e com raycastTarget OFF: o AutoPilot do
    /// DevScreenshotRig continua jogando por baixo intacto (regra do enunciado). Em
    /// -screenshotRun/-showcaseRun as dicas PODEM aparecer (save fresco do CI) — tudo bem.
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

        private const int FirstLevelIndex = 1;

        private bool _activeRun;          // tutorial rodando NESTA corrida
        private bool _firstGateSeen;
        private bool _subscribedToGameManager;
        private Vector2 _dragHintHome;    // posição de descanso da mão (centro do vai-e-vem)
        private Coroutine _dragRoutine;
        private Coroutine _chooseRoutine;

        private void Awake()
        {
            if (_dragHint != null) _dragHintHome = _dragHint.anchoredPosition;
            HideImmediate(_dragHintGroup, _dragHint);
            HideImmediate(_chooseHintGroup, _chooseHint);
        }

        private void OnEnable()
        {
            GameEvents.OnGateConsumed += HandleGateConsumed;
            GameEvents.OnLevelFinished += HandleLevelFinished;
            TrySubscribeGameManager();
        }

        private void OnDisable()
        {
            GameEvents.OnGateConsumed -= HandleGateConsumed;   // bus estático sobrevive a cenas — sempre limpar
            GameEvents.OnLevelFinished -= HandleLevelFinished;
            if (GameManager.Instance != null)
                GameManager.Instance.LevelStarted -= HandleLevelStarted;
            _subscribedToGameManager = false;
            StopAllCoroutines();
            _dragRoutine = null;
            _chooseRoutine = null;
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
            _subscribedToGameManager = true;
        }

        // ------------------------------------------------------------------ gatilhos do bus

        private void HandleLevelStarted(int levelIndex)
        {
            // Só a 1ª corrida da fase 1, e só se o jogador nunca viu o onboarding.
            if (levelIndex != FirstLevelIndex || TutorialAlreadySeen())
            {
                EndRun(persist: false);
                return;
            }
            BeginRun();
        }

        private void HandleGateConsumed(GateResult result)
        {
            if (!_activeRun || _firstGateSeen) return;
            _firstGateSeen = true;
            // Aprendeu a escolher: a dica de ESCOLHA cumpriu o papel, recolhe com fade.
            FadeOut(_chooseHintGroup, _chooseHint, ref _chooseRoutine);
        }

        private void HandleLevelFinished(LevelResult result)
        {
            if (!_activeRun) return;
            // Fim da 1ª fase: dicas somem e tutorialSeen é gravado — onboarding acabou (doc 14 §6).
            EndRun(persist: true);
        }

        // ------------------------------------------------------------------ ciclo de vida do tutorial

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

        // ------------------------------------------------------------------ flag de save (blackboard do Core)

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

        private static SaveData ActiveSave()
        {
            GameBootstrap root = GameBootstrap.Current;
            return root != null ? root.Save : null;
        }
    }
}
