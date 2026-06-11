using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MutantArmy.Core;
using MutantArmy.Gameplay;
using MutantArmy.Meta;
using MutantArmy.Services;
using MutantArmy.UI;

namespace MutantArmy.Editor
{
    /// <summary>
    /// MAR Tools/Setup Project — cria as 3 cenas canônicas (Boot/Main/Game, doc 12 §2.2)
    /// por código: prefina o [Services] com os managers persistentes na ordem do
    /// composition root (doc 12 §3.3), EventSystem, Canvas e câmera, e registra as
    /// cenas no Build Settings. Idempotente: rodar de novo recria as cenas.
    /// </summary>
    public static class ProjectSetup
    {
        private const string ScenesFolder = "Assets/_Project/Scenes";

        [MenuItem("MAR Tools/Setup Project")]
        public static void SetupProject()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("MAR Tools: Setup Project não roda em play mode.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(ScenesFolder);
            string bootPath = CreateBootScene();
            string mainPath = CreateMainScene();
            string gamePath = CreateGameScene();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(bootPath, true),
                new EditorBuildSettingsScene(mainPath, true),
                new EditorBuildSettingsScene(gamePath, true)
            };

            AssetDatabase.SaveAssets();
            Debug.Log("MAR Tools: cenas Boot/Main/Game criadas e adicionadas ao Build Settings.");
        }

        // ------------------------------------------------------------------ Boot

        private static string CreateBootScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera("Main Camera", new Vector3(0f, 1f, -10f), Quaternion.identity);
            CreateEventSystem();

            // ---- [Services]: composition root + managers persistentes (doc 12 §3.3) ----
            // Ordem canônica: Save → RemoteConfig → Analytics → Ads/IAP → Economy →
            // Upgrade → Unit → Reward → Audio → UI (+ GameManager).
            var services = new GameObject("[Services]");
            var bootstrap = services.AddComponent<GameBootstrap>();

            // Providers Null no próprio root: GameBootstrap resolve por
            // GetComponentInChildren<I*Provider> — sem eles o boot loga erro e fica
            // sem Remote Config, ads e analytics (doc 12 §3.3/§7.3).
            services.AddComponent<NullRemoteConfigProvider>();
            services.AddComponent<NullAnalyticsProvider>();
            services.AddComponent<NullAdsProvider>();

            var save = AddManager<SaveSystem>(services, "SaveSystem");
            var remoteConfig = AddManager<RemoteConfigManager>(services, "RemoteConfigManager");
            var analytics = AddManager<AnalyticsManager>(services, "AnalyticsManager");
            var ads = AddManager<AdsManager>(services, "AdsManager");
            var iap = AddManager<IAPManager>(services, "IAPManager");
            var economy = AddManager<EconomySystem>(services, "EconomySystem");
            var upgrade = AddManager<UpgradeSystem>(services, "UpgradeSystem");
            var unit = AddManager<UnitManager>(services, "UnitManager");
            var reward = AddManager<RewardSystem>(services, "RewardSystem");
            var audio = AddManager<AudioManager>(services, "AudioManager");
            var gameManager = AddManager<GameManager>(services, "GameManager");
            var ui = AddManager<UIManager>(services, "UIManager");

            // Canvas persistente da UI com root de safe area para o UIManager (doc 12 §4.13).
            GameObject uiCanvas = CreateCanvas("UICanvas", services.transform);
            var safeRoot = new GameObject("SafeAreaRoot", typeof(RectTransform));
            var safeRect = (RectTransform)safeRoot.transform;
            safeRect.SetParent(uiCanvas.transform, false);
            StretchFull(safeRect);
            WireSerializedField(ui, "_root", safeRect);

            // Campos REAIS do GameBootstrap: _gameManager, _saveService e o array
            // _managersInOrder na ordem canônica do composition root (doc 12 §3.3).
            WireSerializedField(bootstrap, "_gameManager", gameManager);
            WireSerializedField(bootstrap, "_saveService", save);
            WireSerializedArray(bootstrap, "_managersInOrder", new Component[]
            {
                remoteConfig, analytics, ads, iap, economy, upgrade, unit, reward, audio, ui
            });

            // Splash: orçamento de boot de 2,5 s sem visual além disto (doc 12 §2.2).
            GameObject splash = CreateCanvas("SplashCanvas", null);
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var bgRect = (RectTransform)bg.transform;
            bgRect.SetParent(splash.transform, false);
            StretchFull(bgRect);
            bg.GetComponent<Image>().color = Color.black;
            CreateLabel(splash.transform, "Title", "MUTANT ARMY RUN", 64);

            string path = ScenesFolder + "/Boot.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        // ------------------------------------------------------------------ Main

        private static string CreateMainScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera("Main Camera", new Vector3(0f, 1f, -10f), Quaternion.identity);
            CreateDirectionalLight();
            CreateEventSystem();

            GameObject canvas = CreateCanvas("MainCanvas", null);
            CreateLabel(canvas.transform, "Header", "MUTANT ARMY RUN", 64);

            string path = ScenesFolder + "/Main.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        // ------------------------------------------------------------------ Game

        private static string CreateGameScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Rig de câmera próprio — jamais filho do player (doc 12 §4.12).
            GameObject cameraGo = CreateCamera("Main Camera",
                new Vector3(0f, 9f, -7f), Quaternion.Euler(45f, 0f, 0f));
            cameraGo.AddComponent<CameraRig>();

            CreateDirectionalLight();
            CreateEventSystem();

            // Líder/âncora da multidão (segue o drag lateral, doc 12 §4.2).
            var anchor = new GameObject("CrowdAnchor").AddComponent<CrowdAnchor>();

            // ---- [GameSystems]: managers de gameplay na ordem Level→Crowd→Gate→Boss→Combat (doc 12 §3.3) ----
            var systems = new GameObject("[GameSystems]");
            var sceneBootstrap = systems.AddComponent<GameSceneBootstrap>();
            var level = AddManager<LevelManager>(systems, "LevelManager");
            var crowd = AddManager<CrowdManager>(systems, "CrowdManager");
            // Sem CrowdRenderer a multidão é INVISÍVEL (Submit é no-op sem Instance);
            // sem RiskResolver o portal de Risco vira no-op (doc 12 §4.3/§6.2).
            var crowdRenderer = AddManager<CrowdRenderer>(systems, "CrowdRenderer");
            var gate = AddManager<GateManager>(systems, "GateManager");
            var boss = AddManager<BossManager>(systems, "BossManager");
            var combat = AddManager<CombatSystem>(systems, "CombatSystem");
            var vfx = AddManager<VFXManager>(systems, "VFXManager");
            var risk = AddManager<RiskResolver>(systems, "RiskResolver");

            // Campo REAL do GameSceneBootstrap é o array _managersInOrder — a ordem do
            // array É a ordem de init (doc 12 §3.3). CrowdAnchor entra na fila: o Init()
            // dele cria o trigger-proxy do exército.
            WireSerializedArray(sceneBootstrap, "_managersInOrder", new Component[]
            {
                level, crowd, crowdRenderer, gate, boss, combat, vfx, risk, anchor
            });

            // HUD da corrida: controllers por evento (campos TMP são ligados no prefab de UI).
            GameObject hudCanvas = CreateCanvas("HudCanvas", null);
            var hud = new GameObject("Hud", typeof(RectTransform));
            ((RectTransform)hud.transform).SetParent(hudCanvas.transform, false);
            StretchFull((RectTransform)hud.transform);
            hud.AddComponent<HudController>();
            var feedback = new GameObject("FeedbackText", typeof(RectTransform));
            ((RectTransform)feedback.transform).SetParent(hudCanvas.transform, false);
            StretchFull((RectTransform)feedback.transform);
            feedback.AddComponent<FeedbackTextController>();

            string path = ScenesFolder + "/Game.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        // ------------------------------------------------------------------ helpers

        private static T AddManager<T>(GameObject root, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            return go.AddComponent<T>();
        }

        private static GameObject CreateCamera(string name, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.tag = "MainCamera";
            go.transform.SetPositionAndRotation(position, rotation);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.18f);
            go.AddComponent<AudioListener>();
            return go;
        }

        private static void CreateDirectionalLight()
        {
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            // 1 directional realtime; sem luzes adicionais em gameplay (doc 12 §2.4).
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private static GameObject CreateCanvas(string name, Transform parent)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);   // 9:16 (doc 09 §6)
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        private static void CreateLabel(Transform parent, string name, string content, int size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900f, 200f);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            // Unity 2022 removeu Arial.ttf; a fonte builtin é LegacyRuntime.ttf.
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Liga um campo [SerializeField] privado por nome via SerializedObject. Campo
        /// inexistente é ERRO (nunca silêncio): wiring contra nome errado foi exatamente
        /// o bug que deixava _gameManager nulo e o boot com NullReferenceException.
        /// </summary>
        private static void WireSerializedField(Component target, string fieldName, Object value)
        {
            if (target == null) return;
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError(
                    $"MAR Tools: campo serializado '{fieldName}' não existe em {target.GetType().Name} — wiring ignorado, cena ficará quebrada.",
                    target);
                return;
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Preenche um campo-array [SerializeField] (ex.: _managersInOrder dos bootstraps)
        /// via SerializedProperty — a ordem dos elementos É a ordem de init (doc 12 §3.3).
        /// Campo inexistente ou não-array é ERRO explícito, nunca silêncio.
        /// </summary>
        private static void WireSerializedArray(Component target, string fieldName, Component[] values)
        {
            if (target == null) return;
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null || !property.isArray)
            {
                Debug.LogError(
                    $"MAR Tools: campo-array serializado '{fieldName}' não existe (ou não é array) em {target.GetType().Name} — wiring ignorado, cena ficará quebrada.",
                    target);
                return;
            }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string leaf = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
