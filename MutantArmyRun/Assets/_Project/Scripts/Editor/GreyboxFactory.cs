using System.Collections.Generic;
using System.IO;
using MutantArmy.Core;
using MutantArmy.Gameplay;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MutantArmy.Editor
{
    /// <summary>
    /// MAR Tools/Build Greybox — constrói o greybox jogável por código, em cima do que
    /// ProjectSetup.SetupProject (cenas) e MvpContentFactory.CreateAll (SOs) já criaram:
    /// 1. URP asset + Universal Renderer em Assets/_Project/Settings/URP, atribuído em
    ///    GraphicsSettings e em TODOS os níveis de qualidade (doc 12 §2.4: HDR off,
    ///    MSAA off, depth/opaque off, 1 cascade, 30 m, hard shadows, SRP Batcher on).
    /// 2. TMP Essential Resources importados do pacote com.unity.ugui se ausentes.
    /// 3. Materiais URP/Lit de greybox (pista, 5 tropas, portais, boss, obstáculo).
    /// 4. Prefabs: Segment/Obstacle (Track), GatePair (Gates), Boss (Bosses).
    /// 5. Wiring de DADOS nos SOs (Unit.mesh/material, Boss.prefab, World.trackSegmentPrefabs).
    /// 6. Wiring da cena Game (GateManager._pairPrefab/_autoBalancePool, CrowdManager
    ///    ._chart/_defaultUnit, CombatSystem._chart).
    /// Idempotente: re-rodar atualiza no lugar (mesmos paths/GUIDs), nunca duplica.
    /// </summary>
    public static class GreyboxFactory
    {
        private const string UrpFolder = "Assets/_Project/Settings/URP";
        private const string RendererDataPath = UrpFolder + "/URP-Greybox_Renderer.asset";
        private const string PipelineAssetPath = UrpFolder + "/URP-Asset-Greybox.asset";

        private const string MaterialsFolder = "Assets/_Project/Art/Materials";
        private const string PrefabsFolder = "Assets/_Project/Prefabs";
        private const string SegmentPrefabPath = PrefabsFolder + "/Track/Segment_Greybox.prefab";
        private const string ObstaclePrefabPath = PrefabsFolder + "/Track/Obstacle_Greybox.prefab";
        private const string GatePairPrefabPath = PrefabsFolder + "/Gates/GatePair_Greybox.prefab";
        private const string BossPrefabPath = PrefabsFolder + "/Bosses/Boss_Greybox.prefab";

        private const string SoRoot = "Assets/_Project/ScriptableObjects";
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string TmpEssentialsFolder = "Assets/TextMesh Pro";
        private const string TmpDefaultFontPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        // dimensões do greybox: pista 8 m de largura × 30 m por segmento (tarefa/doc 12 §4.11);
        // meio-portal 3,6×3 m centrado em ±1,9 — cobre cada metade da pista de meia-faixa 2,2 m
        private const float SegmentLength = 30f;
        private const float TrackWidth = 8f;
        private const float GateHalfOffsetX = 1.9f;
        private const float GateWidth = 3.6f;
        private const float GateHeight = 3f;

        private sealed class GreyboxMaterials
        {
            public Material Track, Obstacle, Boss;
            public Material Soldier, Archer, Shieldbearer, Mage, Giant;
            public Material GatePositive, GateNegative;
        }

        [MenuItem("MAR Tools/Build Greybox")]
        public static void BuildAll()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("MAR Tools: Build Greybox não roda em play mode.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsurePrerequisites();
            ImportTmpEssentials();
            SetupUrp();
            GreyboxMaterials mats = CreateMaterials();
            CreatePrefabs(mats);
            WireContentData(mats);
            WireGameScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MAR Tools: greybox pronto — URP atribuído, TMP verificado, materiais e " +
                      "prefabs criados, SOs e cena Game ligados.");
        }

        // ------------------------------------------------------------------ pré-requisitos

        // BuildAll roda DEPOIS de SetupProject/CreateAll no pipeline; se algo faltar
        // (execução isolada), roda os factories idempotentes em vez de falhar.
        private static void EnsurePrerequisites()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                Debug.Log("MAR Tools: cena Game ausente — rodando ProjectSetup.SetupProject() antes do greybox.");
                ProjectSetup.SetupProject();
            }
            if (AssetDatabase.LoadAssetAtPath<UnitConfigSO>(SoRoot + "/Units/Unit_Soldier.asset") == null)
            {
                Debug.Log("MAR Tools: conteúdo MVP ausente — rodando MvpContentFactory.CreateAll() antes do greybox.");
                MvpContentFactory.CreateAll();
            }
        }

        // ------------------------------------------------------------------ TMP (item 2)

        private static void ImportTmpEssentials()
        {
            if (AssetDatabase.IsValidFolder(TmpEssentialsFolder)) return;

            // No editor, Path.GetFullPath resolve o caminho virtual Packages/<id> para o
            // PackageCache — mesmo mecanismo do TMP_EditorUtility.packageFullPath.
            string package = null;
            try
            {
                string candidate = Path.GetFullPath(
                    "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
                if (File.Exists(candidate)) package = candidate;
            }
            catch (System.Exception)
            {
                // resolução virtual indisponível: cai no scan do PackageCache abaixo
            }

            if (package == null)
            {
                string cache = Path.Combine(Directory.GetCurrentDirectory(), "Library", "PackageCache");
                if (Directory.Exists(cache))
                {
                    string[] found = Directory.GetFiles(
                        cache, "TMP Essential Resources.unitypackage", SearchOption.AllDirectories);
                    if (found.Length > 0) package = found[0];
                }
            }

            if (package == null)
            {
                Debug.LogWarning("MAR Tools: 'TMP Essential Resources.unitypackage' não encontrado no pacote " +
                                 "com.unity.ugui — rótulos TMP ficarão sem fonte até o import manual.");
                return;
            }

            PackageImportUtil.ImportSync(package);
            Debug.Log("MAR Tools: TMP Essential Resources importados de " + package);
        }

        // ------------------------------------------------------------------ URP (item 1)

        private static void SetupUrp()
        {
            EnsureFolder(UrpFolder);

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererDataPath);
            }
            // renderer criado por código nasce com os shaders internos nulos — religa do pacote
            ResourceReloader.ReloadAllNullIn(rendererData, UniversalRenderPipelineAsset.packagePath);
            EditorUtility.SetDirty(rendererData);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, PipelineAssetPath);
            }

            // doc 12 §2.4: HDR off · MSAA off · depth/opaque off · SRP Batcher on ·
            // 1 cascade · distância 30 m · hard shadows
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 1;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.useSRPBatcher = true;
            pipeline.shadowDistance = 30f;
            pipeline.shadowCascadeCount = 1;
            pipeline.mainLightShadowmapResolution = 1024;

            // setters internos no URP 17 → SerializedObject (caminho oficial de editor)
            var serialized = new SerializedObject(pipeline);
            SetBoolProperty(serialized, "m_MainLightShadowsSupported", true);
            SetBoolProperty(serialized, "m_SoftShadowsSupported", false);
            SetBoolProperty(serialized, "m_AdditionalLightShadowsSupported", false);
            SerializedProperty rendererList = serialized.FindProperty("m_RendererDataList");
            if (rendererList != null && rendererList.isArray)
            {
                if (rendererList.arraySize < 1) rendererList.arraySize = 1;
                rendererList.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;

            // QualitySettings.renderPipeline só afeta o nível ativo — aplica em todos
            int active = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(active, false);
        }

        private static void SetBoolProperty(SerializedObject serialized, string name, bool value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null)
            {
                Debug.LogWarning($"MAR Tools: propriedade '{name}' não existe no URP asset — config ignorada.");
                return;
            }
            property.boolValue = value;
        }

        // ------------------------------------------------------------------ materiais (item 3)

        private static GreyboxMaterials CreateMaterials()
        {
            EnsureFolder(MaterialsFolder);
            return new GreyboxMaterials
            {
                Track = OpaqueMaterial("M_Track_Greybox", new Color(0.50f, 0.52f, 0.55f)),
                Obstacle = OpaqueMaterial("M_Obstacle_Greybox", new Color(0.24f, 0.25f, 0.28f)),
                Boss = OpaqueMaterial("M_Boss_Greybox", new Color(0.55f, 0.08f, 0.08f)),
                Soldier = OpaqueMaterial("M_Unit_Soldier", new Color(0.20f, 0.45f, 0.95f)),
                Archer = OpaqueMaterial("M_Unit_Archer", new Color(0.20f, 0.80f, 0.35f)),
                Shieldbearer = OpaqueMaterial("M_Unit_Shieldbearer", new Color(0.95f, 0.85f, 0.20f)),
                Mage = OpaqueMaterial("M_Unit_Mage", new Color(0.60f, 0.30f, 0.90f)),
                Giant = OpaqueMaterial("M_Unit_Giant", new Color(1.00f, 0.55f, 0.15f)),
                GatePositive = TransparentMaterial("M_Gate_Positive", new Color(0.20f, 0.55f, 1.00f, 0.45f)),
                GateNegative = TransparentMaterial("M_Gate_Negative", new Color(1.00f, 0.20f, 0.15f, 0.45f))
            };
        }

        private static Material OpaqueMaterial(string name, Color color)
        {
            Material mat = LoadOrCreateMaterial(name, color);
            if (mat.HasProperty("_Surface"))
            {
                mat.SetOverrideTag("RenderType", "Opaque");
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.One);
                mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
                mat.SetFloat("_ZWrite", 1f);
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = -1;   // volta ao queue do shader (idempotência se já foi transparente)
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material TransparentMaterial(string name, Color color)
        {
            Material mat = LoadOrCreateMaterial(name, color);
            if (mat.HasProperty("_Surface"))
            {
                // receita padrão do URP/Lit para surface Transparent + alpha blend
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material LoadOrCreateMaterial(string name, Color color)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("MAR Tools: shader 'Universal Render Pipeline/Lit' não encontrado — usando Standard.");
                lit = Shader.Find("Standard");
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(lit);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != lit)
            {
                mat.shader = lit;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            // CrowdRenderer exige GPU Instancing (Graphics.RenderMeshInstanced) — sem isto a tropa não renderiza
            mat.enableInstancing = true;
            return mat;
        }

        // ------------------------------------------------------------------ prefabs (item 4)

        private static void CreatePrefabs(GreyboxMaterials mats)
        {
            EnsureFolder(PrefabsFolder + "/Track");
            EnsureFolder(PrefabsFolder + "/Gates");
            EnsureFolder(PrefabsFolder + "/Bosses");

            BuildSegmentPrefab(mats);
            BuildObstaclePrefab(mats);
            BuildGatePairPrefab(mats);
            BuildBossPrefab(mats);
        }

        // (a) chão 8×30 com TrackSegment + âncoras — a origem do segmento é o INÍCIO dele:
        // o LevelManager posiciona em z=_furthestZ e soma length para achar o EndZ (§4.11)
        private static void BuildSegmentPrefab(GreyboxMaterials mats)
        {
            var root = new GameObject("Segment_Greybox");
            try
            {
                TrackSegment segment = root.AddComponent<TrackSegment>();
                segment.length = SegmentLength;

                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Floor";
                floor.transform.SetParent(root.transform, false);
                floor.transform.localPosition = new Vector3(0f, -0.1f, SegmentLength * 0.5f);
                floor.transform.localScale = new Vector3(TrackWidth, 0.2f, SegmentLength);   // topo do chão em y=0
                floor.GetComponent<MeshRenderer>().sharedMaterial = mats.Track;

                segment.gatePairAnchors = new[]
                {
                    CreateAnchor(root.transform, "GatePairAnchor_A", new Vector3(0f, 0f, 10f)),
                    CreateAnchor(root.transform, "GatePairAnchor_B", new Vector3(0f, 0f, 20f))
                };
                segment.obstacleAnchors = new[]
                {
                    CreateAnchor(root.transform, "ObstacleAnchor_A", new Vector3(-1.5f, 0f, 8f)),
                    CreateAnchor(root.transform, "ObstacleAnchor_B", new Vector3(1.5f, 0f, 16f)),
                    CreateAnchor(root.transform, "ObstacleAnchor_C", new Vector3(0f, 0f, 24f))
                };

                PrefabUtility.SaveAsPrefabAsset(root, SegmentPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // (d) cubo com collider (sólido): o ObstacleSlot do LevelConfigSO aponta para este prefab
        private static void BuildObstaclePrefab(GreyboxMaterials mats)
        {
            var root = new GameObject("Obstacle_Greybox");
            try
            {
                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "Box";
                box.transform.SetParent(root.transform, false);
                box.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                box.transform.localScale = new Vector3(1.6f, 1.6f, 1.6f);
                box.GetComponent<MeshRenderer>().sharedMaterial = mats.Obstacle;

                PrefabUtility.SaveAsPrefabAsset(root, ObstaclePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // (b) GatePairView com 2 GateView por referência SERIALIZADA (contrato do GatePairView)
        private static void BuildGatePairPrefab(GreyboxMaterials mats)
        {
            var root = new GameObject("GatePair_Greybox");
            try
            {
                GatePairView pair = root.AddComponent<GatePairView>();
                GateView left = BuildGateHalf(root.transform, "Gate_L", -GateHalfOffsetX, mats.GatePositive);
                GateView right = BuildGateHalf(root.transform, "Gate_R", GateHalfOffsetX, mats.GateNegative);

                WireField(pair, "_left", left);
                WireField(pair, "_right", right);

                PrefabUtility.SaveAsPrefabAsset(root, GatePairPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GateView BuildGateHalf(Transform parent, string name, float x, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, 0f, 0f);

            GateView view = go.AddComponent<GateView>();

            // trigger no GO do GateView: OnTriggerEnter dispara contra o proxy kinemático do CrowdAnchor
            BoxCollider trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, GateHeight * 0.5f, 0f);
            trigger.size = new Vector3(GateWidth, GateHeight, 0.6f);

            // quad translúcido voltado para −Z: a câmera do rig olha a pista por trás (doc 12 §4.12)
            var frame = new GameObject("Frame");
            frame.transform.SetParent(go.transform, false);
            frame.transform.localPosition = new Vector3(0f, GateHeight * 0.5f, 0f);
            frame.transform.localScale = new Vector3(GateWidth, GateHeight, 1f);
            var filter = frame.AddComponent<MeshFilter>();
            filter.sharedMesh = GetPrimitiveMesh(PrimitiveType.Quad);
            var frameRenderer = frame.AddComponent<MeshRenderer>();
            frameRenderer.sharedMaterial = material;
            frameRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, GateHeight * 0.5f, -0.06f);
            var label = labelGo.AddComponent<TextMeshPro>();
            label.text = "+0";   // placeholder: GateView.Bind/OnValidate SEMPRE re-renderiza do GateConfigSO
            label.fontSize = 8f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.rectTransform.sizeDelta = new Vector2(GateWidth - 0.2f, GateHeight - 0.4f);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpDefaultFontPath);
            if (font != null) label.font = font;   // senão TMP resolve a default em runtime

            WireField(view, "_label", label);
            WireField(view, "_trigger", trigger);
            WireField(view, "_frameRenderer", frameRenderer);
            return view;
        }

        // (c) cápsula escala 4 (≈8 m) com material de boss; BossConfigSO.prefab aponta para cá
        private static void BuildBossPrefab(GreyboxMaterials mats)
        {
            var root = new GameObject("Boss_Greybox");
            try
            {
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = new Vector3(0f, 4f, 0f);   // base da cápsula no chão
                body.transform.localScale = new Vector3(4f, 4f, 4f);
                body.GetComponent<MeshRenderer>().sharedMaterial = mats.Boss;

                PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Transform CreateAnchor(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        // ------------------------------------------------------------------ dados (item 5)

        private static void WireContentData(GreyboxMaterials mats)
        {
            Mesh capsule = GetPrimitiveMesh(PrimitiveType.Capsule);
            var segmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SegmentPrefabPath);
            var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            var obstaclePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ObstaclePrefabPath);

            // À prova de ORDEM (greybox vs UnitVisual/WorldVisual): só preenche o que ainda
            // estiver vazio. mesh/material da tropa são o fallback INSTANCED — o UnitVisualFactory
            // mexe só em viewPrefab, então estes nunca colidem e podem ir incondicionalmente.
            // Já boss.prefab e world.trackSegmentPrefabs SÃO preenchidos pelas Visual factories:
            // se já vierem setados (ex.: modelo Quaternius), o greybox NÃO sobrescreve.
            foreach (UnitConfigSO unit in LoadAllAssets<UnitConfigSO>(SoRoot + "/Units"))
            {
                if (unit.mesh == null) unit.mesh = capsule;
                if (unit.material == null) unit.material = MaterialForUnit(unit, mats);
                EditorUtility.SetDirty(unit);
            }

            foreach (BossConfigSO boss in LoadAllAssets<BossConfigSO>(SoRoot + "/Bosses"))
            {
                if (boss.prefab == null)   // não sobrescreve o modelo Quaternius já atribuído (#2)
                {
                    boss.prefab = bossPrefab;
                    EditorUtility.SetDirty(boss);
                }
            }

            foreach (WorldConfigSO world in LoadAllAssets<WorldConfigSO>(SoRoot + "/Worlds"))
            {
                // skybox/música seguem nulos no greybox (escopo da tarefa)
                if (world.trackSegmentPrefabs == null || world.trackSegmentPrefabs.Length == 0)
                {
                    world.trackSegmentPrefabs = new[] { segmentPrefab };
                    EditorUtility.SetDirty(world);
                }
            }

            // Obstáculos/armadilhas (doc 12 §4.11): o MvpContentFactory popula as POSIÇÕES
            // (ObstacleSlot.trackPosition) e deixa o prefab null; aqui ligamos o Obstacle_Greybox
            // em cada slot ainda vazio. À prova de ORDEM como o trackSegmentPrefabs: se uma Visual
            // factory já tiver atribuído um prefab temático ao slot, o greybox NÃO sobrescreve.
            if (obstaclePrefab != null)
            {
                foreach (LevelConfigSO level in LoadAllAssets<LevelConfigSO>(SoRoot + "/Levels"))
                {
                    if (level.obstacles == null || level.obstacles.Length == 0) continue;
                    bool dirty = false;
                    foreach (ObstacleSlot slot in level.obstacles)
                    {
                        if (slot == null || slot.prefab != null) continue;
                        slot.prefab = obstaclePrefab;
                        dirty = true;
                    }
                    if (dirty) EditorUtility.SetDirty(level);
                }
            }
        }

        private static Material MaterialForUnit(UnitConfigSO unit, GreyboxMaterials mats)
        {
            switch (unit.unitId)
            {
                case "unit_soldier": return mats.Soldier;
                case "unit_archer": return mats.Archer;
                case "unit_shieldbearer": return mats.Shieldbearer;
                case "unit_mage": return mats.Mage;
                case "unit_giant": return mats.Giant;
                default:
                    Debug.LogWarning($"MAR Tools: tropa '{unit.unitId}' sem cor canônica — usando material da pista.", unit);
                    return mats.Track;
            }
        }

        // mesh builtin da cápsula via primitive temporária — caminho estável entre versões
        // (equivalente ao Resources.GetBuiltinResource<Mesh>("New-Capsule.fbx"), sem string mágica)
        private static Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            try
            {
                return temp.GetComponent<MeshFilter>().sharedMesh;
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        // ------------------------------------------------------------------ cena Game (item 6)

        private static void WireGameScene()
        {
            var pairPrefab = AssetDatabase.LoadAssetAtPath<GatePairView>(GatePairPrefabPath);
            var chart = AssetDatabase.LoadAssetAtPath<ElementChartSO>(SoRoot + "/Balance/ElementChart_Default.asset");
            var soldier = AssetDatabase.LoadAssetAtPath<UnitConfigSO>(SoRoot + "/Units/Unit_Soldier.asset");
            List<GateConfigSO> gatePool = LoadAllAssets<GateConfigSO>(SoRoot + "/Gates");

            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            GateManager gate = FindSceneManager<GateManager>();
            CrowdManager crowd = FindSceneManager<CrowdManager>();
            CombatSystem combat = FindSceneManager<CombatSystem>();

            WireField(gate, "_pairPrefab", pairPrefab);
            WireArray(gate, "_autoBalancePool", gatePool.ToArray());
            WireField(crowd, "_chart", chart);
            WireField(crowd, "_defaultUnit", soldier);
            WireField(combat, "_chart", chart);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static T FindSceneManager<T>() where T : Component
        {
            var found = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (found == null)
                Debug.LogError($"MAR Tools: {typeof(T).Name} não existe na cena Game — rode MAR Tools/Setup Project.");
            return found;
        }

        // ------------------------------------------------------------------ infra

        /// <summary>
        /// Liga campo [SerializeField] por nome via SerializedObject — campo inexistente é
        /// ERRO explícito (mesma regra do ProjectSetup: wiring silencioso quebra a cena).
        /// </summary>
        private static void WireField(Component target, string fieldName, Object value)
        {
            if (target == null) return;
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError(
                    $"MAR Tools: campo serializado '{fieldName}' não existe em {target.GetType().Name} — wiring ignorado.",
                    target);
                return;
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireArray<T>(Component target, string fieldName, T[] values) where T : Object
        {
            if (target == null) return;
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null || !property.isArray)
            {
                Debug.LogError(
                    $"MAR Tools: campo-array serializado '{fieldName}' não existe (ou não é array) em {target.GetType().Name} — wiring ignorado.",
                    target);
                return;
            }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Carrega todos os assets de um tipo numa pasta, em ordem estável por nome.</summary>
        private static List<T> LoadAllAssets<T>(string folder) where T : Object
        {
            var list = new List<T>();
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder });
            foreach (string guid in guids)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list;
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
