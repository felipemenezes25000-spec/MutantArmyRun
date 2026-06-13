// Overlay de performance DEV-ONLY (missão Nota 10, W4-C). O arquivo INTEIRO vive sob
// #if DEVELOPMENT_BUILD || UNITY_EDITOR: em build de release a classe nem compila —
// impacto zero garantido (nem o [RuntimeInitializeOnLoadMethod] existe lá).
#if DEVELOPMENT_BUILD || UNITY_EDITOR
using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// HUD de diagnóstico para QA em device (Development Build) e editor: FPS (média de
    /// 0,5 s em tempo NÃO escalado — slow motion/testes a 8-10x não distorcem), unidades
    /// do exército, inimigos de pista abatidos na corrida e Time.timeScale, mais botões
    /// de STRESS que forçam o exército a 60/120/200 unidades pelo funil canônico
    /// ReconcileTo (mede o custo real de crowd cheio em device).
    ///
    /// Auto-bootstrap via [RuntimeInitializeOnLoadMethod] — nenhuma factory/cena precisa
    /// conhecê-lo. Começa OCULTO (zero pixel, zero GUI): PlayMode tests, screenshots e
    /// demos não veem nada até alguém pedir. Toggle: tecla F3 (editor/Windows) OU 4 toques
    /// rápidos no canto superior DIREITO (device sem teclado). Fora do jogo (Boot/MainMenu)
    /// mostra só FPS — os managers de gameplay nem existem na cena Main.
    ///
    /// Decisões registradas: TrackEnemyManager não expõe contador público de grupos vivos,
    /// então exibimos KilledThisRun (instrução da missão); VFXManager não expõe contador de
    /// partículas ativas, então a linha de VFX é omitida. Zero UnityEditor (EditorGuards) e
    /// zero LogError/Warning em caminho normal (LogAssert estrito dos PlayMode tests).
    /// </summary>
    public class DevPerfOverlay : MonoBehaviour
    {
        private static DevPerfOverlay s_instance;

        private const float FpsWindowSeconds = 0.5f;   // janela da média pedida pela missão
        private const float TapWindowSeconds = 0.6f;   // intervalo máximo entre toques da sequência
        private const int TapsToToggle = 4;

        private bool _visible;            // oculto por padrão — overlay só aparece sob demanda
        private float _fpsAccumTime;
        private int _fpsAccumFrames;
        private float _fps;
        private int _tapCount;
        private float _lastTapTime;
        private GUIStyle _panelStyle;     // cache: OnGUI roda várias vezes por frame, nada de alocar lá

        // AfterSceneLoad: mesmo idioma do DevScreenshotRig — nasce depois do composition
        // root da 1ª cena e sobrevive a todos os loads (DontDestroyOnLoad).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (s_instance != null) return;
            var go = new GameObject("[DevPerfOverlay]");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<DevPerfOverlay>();
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        private void Update()
        {
            // FPS em tempo real: timeScale (slow motion canônico, testes acelerados) não conta
            _fpsAccumTime += Time.unscaledDeltaTime;
            _fpsAccumFrames++;
            if (_fpsAccumTime >= FpsWindowSeconds)
            {
                _fps = _fpsAccumFrames / _fpsAccumTime;
                _fpsAccumTime = 0f;
                _fpsAccumFrames = 0;
            }

            if (Input.GetKeyDown(KeyCode.F3)) _visible = !_visible;
            DetectCornerTaps();
        }

        // 4 toques rápidos no canto superior direito (device). Mouse conta como toque no
        // editor/Windows — só quando NÃO há touch ativo, para não contar duplo (o Input
        // legado simula mouse a partir do touch).
        private void DetectCornerTaps()
        {
            bool tappedCorner = false;
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began && IsTopRightCorner(t.position))
                {
                    tappedCorner = true;
                    break;
                }
            }
            if (!tappedCorner && Input.touchCount == 0 &&
                Input.GetMouseButtonDown(0) && IsTopRightCorner(Input.mousePosition))
            {
                tappedCorner = true;
            }
            if (!tappedCorner) return;

            float now = Time.unscaledTime;
            _tapCount = (now - _lastTapTime) <= TapWindowSeconds ? _tapCount + 1 : 1;
            _lastTapTime = now;
            if (_tapCount >= TapsToToggle)
            {
                _tapCount = 0;
                _visible = !_visible;
            }
        }

        // Canto superior direito em coordenadas de TELA (y cresce para cima no Input).
        private static bool IsTopRightCorner(Vector2 screenPos)
        {
            return screenPos.x >= Screen.width * 0.78f && screenPos.y >= Screen.height * 0.88f;
        }

        private void OnGUI()
        {
            if (!_visible) return;

            // Escala pela altura (referência 960 px do preview retrato): texto/botões legíveis
            // e tocáveis em qualquer DPI de celular sem layout por resolução.
            float scale = Mathf.Max(1f, Screen.height / 960f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            if (_panelStyle == null)
            {
                _panelStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 14,
                    padding = new RectOffset(8, 8, 8, 8),
                };
            }

            GUILayout.BeginArea(new Rect(8f, 8f, 230f, 300f));
            GUILayout.BeginVertical(_panelStyle);
            GUILayout.Label("FPS: " + _fps.ToString("0"));

            GameManager gm = GameManager.Instance;
            bool inGame = gm != null && gm.State != GameState.Boot && gm.State != GameState.MainMenu;
            if (inGame)
            {
                CrowdManager crowd = CrowdManager.Instance;
                if (crowd != null) GUILayout.Label("Unidades: " + crowd.Count);

                TrackEnemyManager enemies = TrackEnemyManager.Instance;
                if (enemies != null) GUILayout.Label("Inimigos mortos: " + enemies.KilledThisRun);

                GUILayout.Label("timeScale: " + Time.timeScale.ToString("0.00"));

                // Stress de crowd pelo funil canônico (ReconcileTo respeita Supply/teto).
                // DefaultUnit null (greybox sem wiring) = sem botões — nunca dispara o
                // LogError de spawn sem tipo do CrowdManager.
                if (crowd != null && crowd.DefaultUnit != null)
                {
                    GUILayout.Space(6f);
                    GUILayout.Label("STRESS:");
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("60", GUILayout.Height(36f)))
                        crowd.ReconcileTo(60, crowd.DefaultUnit);
                    if (GUILayout.Button("120", GUILayout.Height(36f)))
                        crowd.ReconcileTo(120, crowd.DefaultUnit);
                    if (GUILayout.Button("200", GUILayout.Height(36f)))
                        crowd.ReconcileTo(200, crowd.DefaultUnit);
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
#endif
