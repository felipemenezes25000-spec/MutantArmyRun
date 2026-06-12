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
    /// MAR Tools/Build World Visuals — veste o greybox com a direção de arte do doc 01 §6
    /// (mobile casual premium: colorido, limpo, vibrante, silhuetas claras), por código:
    /// 1. Materiais URP vibrantes por mundo (pista W01 grama / W02 asfalto frio / W03 areia
    ///    quente) + faixas laterais brancas emissivas.
    /// 2. Skybox gradient procedural por mundo (textura gerada em código + Skybox/Panoramic;
    ///    fallback Skybox/Procedural tintado) gravado no WorldConfigSO.
    /// 3. Prop-prefabs leves por mundo a partir dos CC0 do staging (W01 Nature, W02
    ///    City+Streets, W03 cactos/sucata recolor) com recolor por heurística de slot.
    /// 4. Variants Segment_W01/W02/W03 (A/B) decorados nas BORDAS (fora da pista jogável)
    ///    de forma aleatório-determinística; WorldConfigSO.trackSegmentPrefabs atualizado.
    /// 5. Cena Game: luz quente com sombras soft, ambiente gradient, Volume global (Bloom
    ///    moderado, Vignette leve, ACES, saturação +10) e WorldAtmosphereApplier registrado
    ///    no GameSceneBootstrap.
    /// 6. Portal bonito: moldura emissiva tintada pelo portalColor, painel translúcido com
    ///    glow, rótulo TMP bold com outline e partícula sutil de borda (textura Kenney).
    /// Idempotente: re-rodar atualiza no lugar (mesmos paths/GUIDs), nunca duplica.
    /// Roda DEPOIS do GreyboxFactory (e re-aplica soft shadows que ele desliga).
    /// </summary>
    public static class WorldVisualFactory
    {
        private const string SoRoot = "Assets/_Project/ScriptableObjects";
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string PipelineAssetPath = "Assets/_Project/Settings/URP/URP-Asset-Greybox.asset";
        private const string PostFxProfilePath = "Assets/_Project/Settings/PostFX/PostFX_Game.asset";

        private const string MaterialsFolder = "Assets/_Project/Art/Materials/World";
        private const string SkyMaterialsFolder = "Assets/_Project/Art/Materials/Skybox";
        private const string SkyTexturesFolder = "Assets/_Project/Art/Textures/Sky";
        private const string VfxTexturesFolder = "Assets/_Project/Art/VFX";
        private const string PropModelsFolder = "Assets/_Project/Art/Models/Props";
        private const string PropPrefabsFolder = "Assets/_Project/Prefabs/Props";
        private const string TrackPrefabsFolder = "Assets/_Project/Prefabs/Track";

        private const string SegmentGreyboxPath = TrackPrefabsFolder + "/Segment_Greybox.prefab";
        private const string GatePairPrefabPath = "Assets/_Project/Prefabs/Gates/GatePair_Greybox.prefab";
        private const string TmpDefaultFontPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        // Geometria do greybox (GreyboxFactory): pista 8 m × segmentos 30 m; gate 3,6×3 m.
        private const float TrackWidth = 8f;
        private const float SegmentLength = 30f;
        private const float GateWidth = 3.6f;
        private const float GateHeight = 3f;
        private const int AnchorsPerSide = 5;   // 4-6 âncoras de decoração por borda

        private const string CityBitsBase =
            "models/KayKit-City-Builder-Bits/addons/kaykit_city_builder_bits/Assets/fbx(unity)/";
        private const string NatureBase = "models/Quaternius-UltimateNature/FBX/";
        private const string StreetsBase = "models/Quaternius-ModularStreets/FBX/";
        private const string ParticleBase = "vfx/kenney_particle-pack/PNG (Transparent)/";

        // ------------------------------------------------------------------ dados de tema

        private sealed class WorldTheme
        {
            public string Key;            // "W01" | "W02" | "W03"
            public string AssetName;      // nome do WorldConfigSO
            public Color SkyTop, SkyHorizon, Fog, Sun, Ambient, Track;
        }

        private sealed class PropSpec
        {
            public string Source;         // caminho relativo ao _assets-staging (com '/')
            public float Height;          // altura-alvo em metros (normalização de escala)
            public string DefaultMat;     // chave de material quando a heurística não decide
            public bool Big;              // grandes ficam mais afastados da pista

            public PropSpec(string source, float height, string defaultMat, bool big)
            {
                Source = source;
                Height = height;
                DefaultMat = defaultMat;
                Big = big;
            }
        }

        private static readonly WorldTheme[] Themes =
        {
            new WorldTheme
            {
                Key = "W01", AssetName = "W01_CampoInicial",
                SkyTop = new Color(0.34f, 0.62f, 0.99f), SkyHorizon = new Color(0.80f, 0.93f, 1.00f),
                Fog = new Color(0.76f, 0.88f, 0.98f), Sun = new Color(1.00f, 0.96f, 0.84f),
                // ambiente CLARO e levemente azulado (preenche as sombras das tropas — sem ele
                // a multidão escurece de baixo p/ cima e vira massa indistinta; doc 01 §6).
                Ambient = new Color(0.70f, 0.76f, 0.86f), Track = new Color(0.33f, 0.70f, 0.28f)
            },
            new WorldTheme
            {
                Key = "W02", AssetName = "W02_CidadeZumbi",
                SkyTop = new Color(0.15f, 0.20f, 0.36f), SkyHorizon = new Color(0.46f, 0.55f, 0.72f),
                Fog = new Color(0.40f, 0.46f, 0.60f), Sun = new Color(0.75f, 0.82f, 1.00f),
                Ambient = new Color(0.42f, 0.48f, 0.62f), Track = new Color(0.27f, 0.31f, 0.40f)
            },
            new WorldTheme
            {
                Key = "W03", AssetName = "W03_DesertoRobotico",
                SkyTop = new Color(0.30f, 0.55f, 0.92f), SkyHorizon = new Color(1.00f, 0.80f, 0.55f),
                Fog = new Color(0.95f, 0.79f, 0.58f), Sun = new Color(1.00f, 0.88f, 0.68f),
                Ambient = new Color(0.78f, 0.68f, 0.55f), Track = new Color(0.88f, 0.72f, 0.46f)
            }
        };

        // Mapa necessidade→arquivo do PLANO-DE-USO.md §1.5 (todos CC0 1.0, recolor liberado).
        private static readonly Dictionary<string, PropSpec[]> PropSpecs = new Dictionary<string, PropSpec[]>
        {
            ["W01"] = new[]
            {
                new PropSpec(NatureBase + "CommonTree_1.fbx", 5.5f, "leaves", true),
                new PropSpec(NatureBase + "CommonTree_2.fbx", 6.2f, "leaves", true),
                new PropSpec(NatureBase + "BirchTree_1.fbx", 5.2f, "leaves", true),
                new PropSpec(NatureBase + "Bush_1.fbx", 0.9f, "leaves", false),
                new PropSpec(NatureBase + "BushBerries_1.fbx", 0.85f, "leaves", false),
                new PropSpec(NatureBase + "Rock_1.fbx", 1.5f, "rock", false),
                new PropSpec(NatureBase + "Rock_Moss_1.fbx", 1.1f, "rock", false),
                new PropSpec(NatureBase + "Flowers.fbx", 0.5f, "flower", false)
            },
            ["W02"] = new[]
            {
                new PropSpec(CityBitsBase + "building_A.fbx", 10f, "city", true),
                new PropSpec(CityBitsBase + "building_B.fbx", 12f, "city", true),
                new PropSpec(CityBitsBase + "building_C.fbx", 9f, "city", true),
                new PropSpec(CityBitsBase + "building_D.fbx", 11f, "city", true),
                new PropSpec(CityBitsBase + "car_sedan.fbx", 1.5f, "city", false),
                new PropSpec(CityBitsBase + "car_taxi.fbx", 1.5f, "city", false),
                new PropSpec(CityBitsBase + "car_police.fbx", 1.6f, "city", false),
                new PropSpec(CityBitsBase + "dumpster.fbx", 1.3f, "city", false),
                new PropSpec(CityBitsBase + "firehydrant.fbx", 0.8f, "city", false),
                new PropSpec(CityBitsBase + "streetlight.fbx", 4.4f, "city", false),
                new PropSpec(StreetsBase + "Streetlight_Single.fbx", 4.6f, "metal", false)
            },
            ["W03"] = new[]
            {
                new PropSpec(NatureBase + "Cactus_1.fbx", 2.4f, "cactus", false),
                new PropSpec(NatureBase + "Cactus_2.fbx", 2.0f, "cactus", false),
                new PropSpec(NatureBase + "Cactus_3.fbx", 2.7f, "cactus", false),
                new PropSpec(NatureBase + "CactusFlowers_2.fbx", 0.8f, "flower", false),
                new PropSpec(NatureBase + "PalmTree_1.fbx", 5.6f, "leaves", true),
                new PropSpec(NatureBase + "Rock_4.fbx", 1.6f, "rock", false),
                new PropSpec(NatureBase + "Rock_5.fbx", 1.2f, "rock", false),
                new PropSpec(NatureBase + "CommonTree_Dead_1.fbx", 4.6f, "trunk", true),
                new PropSpec(CityBitsBase + "box_A.fbx", 1.2f, "scrap", false),
                new PropSpec(CityBitsBase + "box_B.fbx", 1.0f, "scrap", false),
                new PropSpec(CityBitsBase + "dumpster.fbx", 1.3f, "scrap", false),
                new PropSpec(StreetsBase + "Sign_Stop.fbx", 2.6f, "metal", false)
            }
        };

        // ------------------------------------------------------------------ entrada

        [MenuItem("MAR Tools/Build World Visuals")]
        public static void BuildAll()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("MAR Tools: Build World Visuals não roda em play mode.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsurePrerequisites();

            string staging = FindStagingRoot();
            if (staging == null)
                Debug.LogWarning("MAR Tools: pasta _assets-staging não encontrada ao lado do projeto — " +
                                 "props CC0 serão pulados (pista/sky/pós-processo seguem normais).");

            ImportStagingTextures(staging);
            ImportStagingModels(staging);

            Dictionary<string, Dictionary<string, Material>> palettes = CreateWorldMaterials();
            Dictionary<string, Material> trackMats = CreateTrackMaterials();
            Material stripeMat = CreateStripeMaterial();
            Dictionary<string, Material> skyMats = CreateSkyboxMaterials();

            var worldProps = new Dictionary<string, List<GameObject>>();
            foreach (WorldTheme theme in Themes)
                worldProps[theme.Key] = BuildPropPrefabs(theme.Key, palettes[theme.Key]);

            var worldSegments = new Dictionary<string, GameObject[]>();
            foreach (WorldTheme theme in Themes)
                worldSegments[theme.Key] = BuildSegmentVariants(theme.Key, trackMats[theme.Key],
                                                                stripeMat, worldProps[theme.Key]);

            WireWorldData(trackMats, skyMats, worldProps, worldSegments);
            BeautifyGatePrefab();
            EnhanceGameScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MAR Tools: visual de mundo pronto — materiais/sky por mundo, props CC0, " +
                      "segmentos decorados, portal premium e pós-processo na cena Game.");
        }

        private static void EnsurePrerequisites()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SegmentGreyboxPath) == null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(GatePairPrefabPath) == null)
            {
                Debug.Log("MAR Tools: greybox ausente — rodando GreyboxFactory.BuildAll() antes do visual.");
                GreyboxFactory.BuildAll();
            }
        }

        // ------------------------------------------------------------------ staging → projeto

        private static string FindStagingRoot()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;   // .../MutantArmyRun
            DirectoryInfo parent = Directory.GetParent(projectRoot);
            if (parent == null) return null;
            string candidate = Path.Combine(parent.FullName, "_assets-staging");
            return Directory.Exists(candidate) ? candidate : null;
        }

        private static string AbsolutePath(string assetPath)
        {
            return Path.Combine(Directory.GetCurrentDirectory(),
                                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>Copia 1 arquivo do staging para um asset path (skip se já existe). True = asset disponível.</summary>
        private static bool CopyStagingFile(string stagingRoot, string relSource, string destAssetPath,
                                            ref bool anyCopied)
        {
            if (File.Exists(AbsolutePath(destAssetPath))) return true;   // idempotente
            if (stagingRoot == null) return false;

            string src = Path.Combine(stagingRoot, relSource.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(src))
            {
                Debug.LogWarning("MAR Tools: arquivo CC0 ausente no staging: " + relSource);
                return false;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(AbsolutePath(destAssetPath)));
            File.Copy(src, AbsolutePath(destAssetPath));
            anyCopied = true;
            return true;
        }

        private static void ImportStagingTextures(string staging)
        {
            bool copied = false;
            // citybits: única textura do KayKit City Builder (recolor por tint do material)
            CopyStagingFile(staging, CityBitsBase + "citybits_texture.png",
                            VfxTexturesFolder + "/Tex_CityBits.png", ref copied);
            // Kenney Particle Pack (CC0): glow do painel + faísca da borda do portal
            CopyStagingFile(staging, ParticleBase + "light_01.png",
                            VfxTexturesFolder + "/Tex_Glow_Soft.png", ref copied);
            CopyStagingFile(staging, ParticleBase + "spark_04.png",
                            VfxTexturesFolder + "/Tex_Spark.png", ref copied);
            if (copied) AssetDatabase.Refresh();
        }

        private static void ImportStagingModels(string staging)
        {
            bool copied = false;
            foreach (KeyValuePair<string, PropSpec[]> world in PropSpecs)
            {
                foreach (PropSpec spec in world.Value)
                    CopyStagingFile(staging, spec.Source, ModelAssetPath(world.Key, spec), ref copied);
            }
            if (copied) AssetDatabase.Refresh();

            foreach (KeyValuePair<string, PropSpec[]> world in PropSpecs)
            {
                foreach (PropSpec spec in world.Value)
                    ConfigureModelImporter(ModelAssetPath(world.Key, spec));
            }
        }

        private static string ModelAssetPath(string worldKey, PropSpec spec)
        {
            string file = Path.GetFileName(spec.Source.Replace('\\', '/'));
            return PropModelsFolder + "/" + worldKey + "/" + file;
        }

        /// <summary>Prop é DECORAÇÃO estática: sem animação/câmera/luz do FBX (import mínimo).</summary>
        private static void ConfigureModelImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return;

            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.None)
            {
                importer.animationType = ModelImporterAnimationType.None;
                changed = true;
            }
            if (importer.importAnimation) { importer.importAnimation = false; changed = true; }
            if (importer.importCameras) { importer.importCameras = false; changed = true; }
            if (importer.importLights) { importer.importLights = false; changed = true; }
            if (changed) importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ materiais (item a)

        private static Dictionary<string, Dictionary<string, Material>> CreateWorldMaterials()
        {
            EnsureFolder(MaterialsFolder);
            var cityTex = AssetDatabase.LoadAssetAtPath<Texture2D>(VfxTexturesFolder + "/Tex_CityBits.png");

            var w01 = new Dictionary<string, Material>
            {
                ["leaves"] = LitMaterial("M_W01_Leaves", new Color(0.27f, 0.72f, 0.30f)),
                ["trunk"] = LitMaterial("M_W01_Trunk", new Color(0.46f, 0.31f, 0.19f)),
                ["rock"] = LitMaterial("M_W01_Rock", new Color(0.63f, 0.66f, 0.72f)),
                ["flower"] = LitMaterial("M_W01_Flower", new Color(0.95f, 0.42f, 0.60f))
            };

            var w02 = new Dictionary<string, Material>
            {
                // cidade dessaturada/fria (tom apocalíptico) — recolor por tint da textura CC0
                ["city"] = LitMaterial("M_W02_City", new Color(0.72f, 0.76f, 0.88f), cityTex),
                ["metal"] = LitMaterial("M_W02_Metal", new Color(0.30f, 0.34f, 0.42f), null, 0.6f, 0.45f),
                ["lamp"] = LitMaterial("M_W02_Lamp", new Color(0.95f, 0.88f, 0.70f), null, 0f, 0.3f,
                                       new Color(1.00f, 0.85f, 0.50f) * 1.2f)
            };

            var w03 = new Dictionary<string, Material>
            {
                ["cactus"] = LitMaterial("M_W03_Cactus", new Color(0.42f, 0.66f, 0.33f)),
                ["leaves"] = LitMaterial("M_W03_PalmLeaves", new Color(0.55f, 0.66f, 0.36f)),
                ["trunk"] = LitMaterial("M_W03_DeadWood", new Color(0.55f, 0.44f, 0.32f)),
                ["rock"] = LitMaterial("M_W03_Rock", new Color(0.82f, 0.62f, 0.38f)),
                ["flower"] = LitMaterial("M_W03_Flower", new Color(0.98f, 0.55f, 0.40f)),
                // sucata tech: textura citybits recolor quente + metal com acento ciano emissivo
                ["scrap"] = LitMaterial("M_W03_Scrap", new Color(1.00f, 0.86f, 0.66f), cityTex, 0.55f, 0.5f),
                ["metal"] = LitMaterial("M_W03_Metal", new Color(0.45f, 0.50f, 0.56f), null, 0.7f, 0.5f,
                                        new Color(0.00f, 0.55f, 0.65f) * 0.8f)
            };

            return new Dictionary<string, Dictionary<string, Material>>
            {
                ["W01"] = w01,
                ["W02"] = w02,
                ["W03"] = w03
            };
        }

        private static Dictionary<string, Material> CreateTrackMaterials()
        {
            return new Dictionary<string, Material>
            {
                ["W01"] = LitMaterial("M_Track_W01", Themes[0].Track, null, 0f, 0.08f),
                ["W02"] = LitMaterial("M_Track_W02", Themes[1].Track, null, 0.1f, 0.25f),
                ["W03"] = LitMaterial("M_Track_W03", Themes[2].Track, null, 0f, 0.05f)
            };
        }

        private static Material CreateStripeMaterial()
        {
            // branco emissivo ≥ threshold do Bloom (0.9): a faixa lateral "acende" de leve
            return LitMaterial("M_TrackStripe", Color.white, null, 0f, 0.2f, Color.white);
        }

        private static Material LitMaterial(string name, Color color, Texture2D baseMap = null,
                                            float metallic = 0f, float smoothness = 0.25f,
                                            Color? emission = null)
        {
            Material mat = LoadOrCreateMaterial(MaterialsFolder + "/" + name + ".mat",
                                                "Universal Render Pipeline/Lit");
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", baseMap);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material LoadOrCreateMaterial(string path, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("MAR Tools: shader '" + shaderName + "' não encontrado — usando URP/Lit.");
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            return mat;
        }

        // ------------------------------------------------------------------ skybox (item b)

        private static Dictionary<string, Material> CreateSkyboxMaterials()
        {
            EnsureFolder(SkyTexturesFolder);
            EnsureFolder(SkyMaterialsFolder);
            var result = new Dictionary<string, Material>();
            foreach (WorldTheme theme in Themes)
            {
                Texture2D tex = GenerateSkyTexture(SkyTexturesFolder + "/Tex_Sky_" + theme.Key + ".png",
                                                   theme.SkyTop, theme.SkyHorizon);
                result[theme.Key] = CreateSkyboxMaterial("M_Sky_" + theme.Key, tex, theme);
            }
            return result;
        }

        /// <summary>
        /// Gradient vertical em layout lat-long (horizonte em v=0,5): chão liso levemente
        /// escurecido embaixo, horizonte→topo suavizado em cima. 8×256 px sem mip: ~8 KB.
        /// </summary>
        private static Texture2D GenerateSkyTexture(string assetPath, Color top, Color horizon)
        {
            const int W = 8, H = 256;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            Color ground = Color.Lerp(horizon, Color.black, 0.22f);
            for (int y = 0; y < H; y++)
            {
                float v = (y + 0.5f) / H;
                Color c;
                if (v < 0.5f)
                {
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.30f, 0.50f, v));
                    c = Color.Lerp(ground, horizon, t);
                }
                else
                {
                    float t = Mathf.Pow(Mathf.InverseLerp(0.5f, 1f, v), 0.75f);
                    c = Color.Lerp(horizon, top, t);
                }
                c.a = 1f;
                for (int x = 0; x < W; x++) tex.SetPixel(x, y, c);
            }
            tex.Apply();
            File.WriteAllBytes(AbsolutePath(assetPath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(assetPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static Material CreateSkyboxMaterial(string name, Texture2D tex, WorldTheme theme)
        {
            Shader panoramic = Shader.Find("Skybox/Panoramic");
            string path = SkyMaterialsFolder + "/" + name + ".mat";

            if (panoramic != null && tex != null)
            {
                Material mat = LoadOrCreateMaterial(path, "Skybox/Panoramic");
                mat.SetTexture("_MainTex", tex);
                if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
                if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", 1f);
                if (mat.HasProperty("_Mapping")) mat.SetFloat("_Mapping", 1f);   // lat-long
                mat.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
                if (mat.HasProperty("_ImageType")) mat.SetFloat("_ImageType", 0f);
                EditorUtility.SetDirty(mat);
                return mat;
            }

            // fallback: Skybox/Procedural tintado pelo mundo (cores aproximadas)
            Material proc = LoadOrCreateMaterial(path, "Skybox/Procedural");
            if (proc.HasProperty("_SkyTint")) proc.SetColor("_SkyTint", theme.SkyTop);
            if (proc.HasProperty("_GroundColor")) proc.SetColor("_GroundColor", theme.SkyHorizon);
            if (proc.HasProperty("_Exposure")) proc.SetFloat("_Exposure", 1.2f);
            EditorUtility.SetDirty(proc);
            return proc;
        }

        // ------------------------------------------------------------------ props (item c)

        private static List<GameObject> BuildPropPrefabs(string worldKey, Dictionary<string, Material> palette)
        {
            EnsureFolder(PropPrefabsFolder + "/" + worldKey);
            var result = new List<GameObject>();
            foreach (PropSpec spec in PropSpecs[worldKey])
            {
                GameObject prefab = BuildPropPrefab(worldKey, spec, palette);
                if (prefab != null) result.Add(prefab);
            }
            if (result.Count == 0)
                Debug.LogWarning("MAR Tools: nenhum prop disponível para " + worldKey +
                                 " — segmentos sairão sem decoração lateral.");
            return result;
        }

        private static GameObject BuildPropPrefab(string worldKey, PropSpec spec,
                                                  Dictionary<string, Material> palette)
        {
            string modelPath = ModelAssetPath(worldKey, spec);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) return null;   // staging ausente: já avisado no import

            string baseName = Path.GetFileNameWithoutExtension(modelPath);
            string prefabPath = PropPrefabsFolder + "/" + worldKey + "/P_" + worldKey + "_" + baseName + ".prefab";

            var wrapper = new GameObject("P_" + worldKey + "_" + baseName);
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.transform.SetParent(wrapper.transform, false);

                // recolor por slot: heurística de nome do material do FBX → paleta vibrante
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] mats = renderer.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        string slotName = mats[i] != null ? mats[i].name : string.Empty;
                        mats[i] = ResolveSlotMaterial(palette, slotName, spec.DefaultMat);
                    }
                    renderer.sharedMaterials = mats;
                    // sombra só em prop com presença: grama/flor não paga shadow pass
                    renderer.shadowCastingMode = spec.Height >= 1.2f
                        ? ShadowCastingMode.On
                        : ShadowCastingMode.Off;
                }

                // normalização de escala (alturas de export variam entre packs) + base no chão
                Bounds bounds = CalculateBounds(wrapper);
                if (bounds.size.y > 0.001f)
                {
                    float k = spec.Height / bounds.size.y;
                    instance.transform.localScale = instance.transform.localScale * k;
                    instance.transform.localPosition = new Vector3(
                        -bounds.center.x * k, -bounds.min.y * k, -bounds.center.z * k);
                }

                return PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(wrapper);
            }
        }

        private static Material ResolveSlotMaterial(Dictionary<string, Material> palette,
                                                    string slotName, string defaultKey)
        {
            string key = ClassifySlot(slotName, defaultKey);
            Material mat;
            if (palette.TryGetValue(key, out mat) && mat != null) return mat;
            if (palette.TryGetValue(defaultKey, out mat) && mat != null) return mat;
            foreach (KeyValuePair<string, Material> any in palette) return any.Value;   // último recurso
            return null;
        }

        private static string ClassifySlot(string slotName, string defaultKey)
        {
            string n = slotName.ToLowerInvariant();
            if (n.Contains("leaf") || n.Contains("leaves") || n.Contains("foliage") ||
                n.Contains("needle") || n.Contains("green")) return "leaves";
            if (n.Contains("bark") || n.Contains("trunk") || n.Contains("wood")) return "trunk";
            if (n.Contains("rock") || n.Contains("stone")) return "rock";
            if (n.Contains("flower") || n.Contains("berr") || n.Contains("petal")) return "flower";
            if (n.Contains("lamp") || n.Contains("light")) return "lamp";
            return defaultKey;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        // ------------------------------------------------------------------ segmentos (item d)

        private static GameObject[] BuildSegmentVariants(string worldKey, Material trackMat,
                                                         Material stripeMat, List<GameObject> props)
        {
            var baseSegment = AssetDatabase.LoadAssetAtPath<GameObject>(SegmentGreyboxPath);
            if (baseSegment == null)
            {
                Debug.LogError("MAR Tools: Segment_Greybox ausente — rode o GreyboxFactory.");
                return new GameObject[0];
            }

            var big = new List<GameObject>();
            var small = new List<GameObject>();
            SplitPropsBySize(worldKey, props, big, small);

            string[] suffixes = { "A", "B" };
            var result = new List<GameObject>();
            for (int v = 0; v < suffixes.Length; v++)
            {
                string path = TrackPrefabsFolder + "/Segment_" + worldKey + "_" + suffixes[v] + ".prefab";
                var root = (GameObject)PrefabUtility.InstantiatePrefab(baseSegment);
                root.name = "Segment_" + worldKey + "_" + suffixes[v];
                try
                {
                    Transform floor = root.transform.Find("Floor");
                    if (floor != null)
                    {
                        var floorRenderer = floor.GetComponent<MeshRenderer>();
                        if (floorRenderer != null) floorRenderer.sharedMaterial = trackMat;
                    }

                    AddStripe(root.transform, stripeMat, -1f);
                    AddStripe(root.transform, stripeMat, 1f);

                    // aleatório-DETERMINÍSTICO: seed fixa por mundo+variant — re-rodar o
                    // factory reproduz exatamente a mesma decoração (sem diff de prefab)
                    int worldIndex = WorldIndexOf(worldKey);
                    var rng = new System.Random(worldIndex * 7919 + v * 104729 + 17);
                    DecorateSegment(root.transform, big, small, rng);

                    result.Add(PrefabUtility.SaveAsPrefabAsset(root, path));
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
            return result.ToArray();
        }

        private static void SplitPropsBySize(string worldKey, List<GameObject> props,
                                             List<GameObject> big, List<GameObject> small)
        {
            PropSpec[] specs = PropSpecs[worldKey];
            foreach (GameObject prop in props)
            {
                bool isBig = false;
                foreach (PropSpec spec in specs)
                {
                    string baseName = Path.GetFileNameWithoutExtension(spec.Source.Replace('\\', '/'));
                    if (prop.name == "P_" + worldKey + "_" + baseName)
                    {
                        isBig = spec.Big;
                        break;
                    }
                }
                if (isBig) big.Add(prop);
                else small.Add(prop);
            }
        }

        private static int WorldIndexOf(string worldKey)
        {
            for (int i = 0; i < Themes.Length; i++)
                if (Themes[i].Key == worldKey) return i + 1;
            return 0;
        }

        /// <summary>Faixa lateral branca emissiva no limite visual da pista (±4 m).</summary>
        private static void AddStripe(Transform root, Material stripeMat, float sign)
        {
            var go = new GameObject(sign < 0f ? "Stripe_L" : "Stripe_R");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(sign * (TrackWidth * 0.5f - 0.14f), 0.03f, SegmentLength * 0.5f);
            go.transform.localScale = new Vector3(0.28f, 0.06f, SegmentLength);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = GetPrimitiveMesh(PrimitiveType.Cube);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = stripeMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>
        /// 5 âncoras de decoração POR BORDA (10 no segmento), fora da pista jogável
        /// (meia-faixa 2,2 m; pista visual ±4 m): props pequenos a 4,9–6,5 m, grandes a
        /// 7,6–9,5 m (prédio/árvore não invade o chão). 85% das âncoras recebem prop.
        /// </summary>
        private static void DecorateSegment(Transform root, List<GameObject> big,
                                            List<GameObject> small, System.Random rng)
        {
            var decor = new GameObject("Decor").transform;
            decor.SetParent(root, false);

            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1f : 1f;
                for (int i = 0; i < AnchorsPerSide; i++)
                {
                    var anchor = new GameObject("DecorAnchor_" + (side == 0 ? "L" : "R") + i).transform;
                    anchor.SetParent(decor, false);

                    // consumo de RNG em ordem FIXA (z → tipo → x → spawn → prop → rot → escala)
                    float z = 2.5f + i * 5.8f + (float)rng.NextDouble() * 2.4f;
                    bool useBig = big.Count > 0 && (small.Count == 0 || rng.NextDouble() < 0.5);
                    List<GameObject> pool = useBig ? big : small;
                    float x = useBig
                        ? 7.6f + (float)rng.NextDouble() * 1.9f
                        : 4.9f + (float)rng.NextDouble() * 1.6f;
                    anchor.localPosition = new Vector3(sign * x, 0f, z);

                    bool spawn = rng.NextDouble() < 0.85 && pool.Count > 0;
                    int pick = pool.Count > 0 ? rng.Next(pool.Count) : 0;
                    float rotY = (float)rng.NextDouble() * 360f;
                    float scale = 0.85f + (float)rng.NextDouble() * 0.4f;
                    if (!spawn) continue;   // âncora vazia: respiro visual

                    var prop = (GameObject)PrefabUtility.InstantiatePrefab(pool[pick]);
                    prop.transform.SetParent(anchor, false);
                    prop.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
                    prop.transform.localScale = Vector3.one * scale;
                }
            }
        }

        // ------------------------------------------------------------------ dados dos mundos

        private static void WireWorldData(Dictionary<string, Material> trackMats,
                                          Dictionary<string, Material> skyMats,
                                          Dictionary<string, List<GameObject>> worldProps,
                                          Dictionary<string, GameObject[]> worldSegments)
        {
            foreach (WorldTheme theme in Themes)
            {
                string path = SoRoot + "/Worlds/" + theme.AssetName + ".asset";
                var world = AssetDatabase.LoadAssetAtPath<WorldConfigSO>(path);
                if (world == null)
                {
                    Debug.LogError("MAR Tools: WorldConfigSO ausente em " + path +
                                   " — rode MAR Tools/Create MVP Content.");
                    continue;
                }

                world.skyTopColor = theme.SkyTop;
                world.skyHorizonColor = theme.SkyHorizon;
                world.fogColor = theme.Fog;
                world.sunColor = theme.Sun;
                world.ambientColor = theme.Ambient;
                world.trackMaterial = trackMats[theme.Key];
                world.skyboxMaterial = skyMats[theme.Key];
                world.propPrefabs = worldProps[theme.Key].ToArray();
                if (worldSegments[theme.Key].Length > 0)
                    world.trackSegmentPrefabs = worldSegments[theme.Key];
                EditorUtility.SetDirty(world);
            }
        }

        // ------------------------------------------------------------------ portal (item 2)

        private static void BeautifyGatePrefab()
        {
            Material trimMat = LitMaterial("M_Gate_Trim", new Color(0.92f, 0.92f, 0.95f),
                                           null, 0.1f, 0.5f, Color.white * 0.8f);
            Material panelMat = CreateGatePanelMaterial();
            Material sparkleMat = CreateSparkleMaterial();
            Material labelMat = CreateGateLabelMaterial();

            GameObject root = PrefabUtility.LoadPrefabContents(GatePairPrefabPath);
            try
            {
                foreach (GateView view in root.GetComponentsInChildren<GateView>(true))
                {
                    Transform half = view.transform;

                    // painel translúcido com glow radial (textura Kenney; cor via MPB do GateView)
                    Transform frame = half.Find("Frame");
                    if (frame != null)
                    {
                        var panelRenderer = frame.GetComponent<MeshRenderer>();
                        if (panelRenderer != null)
                        {
                            panelRenderer.sharedMaterial = panelMat;
                            panelRenderer.shadowCastingMode = ShadowCastingMode.Off;
                        }
                    }

                    BuildGateTrim(view, half, trimMat);
                    StyleGateLabel(half, labelMat);
                    BuildGateSparkles(half, sparkleMat);
                }

                PrefabUtility.SaveAsPrefabAsset(root, GatePairPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Moldura em U invertido (2 postes + travessa) — emissiva, tintada em runtime.</summary>
        private static void BuildGateTrim(GateView view, Transform half, Material trimMat)
        {
            Transform old = half.Find("FrameTrim");
            if (old != null) Object.DestroyImmediate(old.gameObject);   // idempotente

            var trim = new GameObject("FrameTrim").transform;
            trim.SetParent(half, false);

            var renderers = new Renderer[]
            {
                TrimBar(trim, "Post_L", new Vector3(-GateWidth * 0.5f, GateHeight * 0.5f, 0f),
                        new Vector3(0.22f, GateHeight + 0.22f, 0.22f), trimMat),
                TrimBar(trim, "Post_R", new Vector3(GateWidth * 0.5f, GateHeight * 0.5f, 0f),
                        new Vector3(0.22f, GateHeight + 0.22f, 0.22f), trimMat),
                TrimBar(trim, "TopBar", new Vector3(0f, GateHeight + 0.11f, 0f),
                        new Vector3(GateWidth + 0.44f, 0.22f, 0.22f), trimMat)
            };
            WireArray(view, "_frameTrimRenderers", renderers);
        }

        private static Renderer TrimBar(Transform parent, string name, Vector3 localPos,
                                        Vector3 localScale, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = GetPrimitiveMesh(PrimitiveType.Cube);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }

        private static void StyleGateLabel(Transform half, Material labelMat)
        {
            var label = half.GetComponentInChildren<TextMeshPro>(true);
            if (label == null) return;
            label.fontSize = 9.5f;
            label.fontStyle = FontStyles.Bold;
            if (labelMat != null) label.fontSharedMaterial = labelMat;
        }

        /// <summary>Preset TMP com outline escuro: rótulo legível sobre QUALQUER fundo (doc 01 §6).</summary>
        private static Material CreateGateLabelMaterial()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpDefaultFontPath);
            if (font == null || font.material == null)
            {
                Debug.LogWarning("MAR Tools: fonte TMP default ausente — rótulo do portal sem outline.");
                return null;
            }
            string path = MaterialsFolder + "/M_TMP_GateLabel.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(font.material);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = font.material.shader;
                mat.CopyPropertiesFromMaterial(font.material);
            }
            if (mat.HasProperty("_OutlineWidth")) mat.SetFloat("_OutlineWidth", 0.22f);
            if (mat.HasProperty("_OutlineColor")) mat.SetColor("_OutlineColor", new Color(0.04f, 0.05f, 0.10f));
            if (mat.HasProperty("_FaceDilate")) mat.SetFloat("_FaceDilate", 0.08f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Painel: URP/Unlit transparente com glow radial — alpha vem da TEXTURA, então
        /// o tint opaco do portalColor (MPB) preserva a translucidez.</summary>
        private static Material CreateGatePanelMaterial()
        {
            Material mat = LoadOrCreateMaterial(MaterialsFolder + "/M_Gate_Panel.mat",
                                                "Universal Render Pipeline/Unlit");
            var glow = AssetDatabase.LoadAssetAtPath<Texture2D>(VfxTexturesFolder + "/Tex_Glow_Soft.png");
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", glow);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.9f));
            SetTransparent(mat, (float)UnityEngine.Rendering.BlendMode.SrcAlpha,
                           (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Faísca de borda: Particles/Unlit aditivo com sprite do Kenney Particle Pack.</summary>
        private static Material CreateSparkleMaterial()
        {
            Material mat = LoadOrCreateMaterial(MaterialsFolder + "/M_Gate_Sparkle.mat",
                                                "Universal Render Pipeline/Particles/Unlit");
            var spark = AssetDatabase.LoadAssetAtPath<Texture2D>(VfxTexturesFolder + "/Tex_Spark.png");
            if (spark == null)
                spark = AssetDatabase.LoadAssetAtPath<Texture2D>(VfxTexturesFolder + "/Tex_Glow_Soft.png");
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", spark);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            SetTransparent(mat, (float)UnityEngine.Rendering.BlendMode.SrcAlpha,
                           (float)UnityEngine.Rendering.BlendMode.One);   // aditivo: brilha, nunca escurece
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SetTransparent(Material mat, float srcBlend, float dstBlend)
        {
            mat.SetOverrideTag("RenderType", "Transparent");
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", srcBlend);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", dstBlend);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void BuildGateSparkles(Transform half, Material sparkleMat)
        {
            Transform old = half.Find("EdgeSparkles");
            if (old != null) Object.DestroyImmediate(old.gameObject);   // idempotente

            var go = new GameObject("EdgeSparkles");
            go.transform.SetParent(half, false);
            go.transform.localPosition = new Vector3(0f, GateHeight * 0.5f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.30f);
            main.startColor = new Color(1f, 1f, 1f, 0.55f);
            main.maxParticles = 48;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 8f;   // SUTIL: borda viva, nunca poluição (doc 01 §6)

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(GateWidth * 0.92f, GateHeight * 0.92f, 0.05f);

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.y = new ParticleSystem.MinMaxCurve(0.25f);

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.7f, 0.25f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = sparkleMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ------------------------------------------------------------------ cena Game (itens e/f)

        private static void EnhanceGameScene()
        {
            EnablePipelineSoftShadows();
            VolumeProfile profile = CreatePostFxProfile();

            string w01Path = SoRoot + "/Worlds/" + Themes[0].AssetName + ".asset";
            var w01 = AssetDatabase.LoadAssetAtPath<WorldConfigSO>(w01Path);

            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            // luz direcional QUENTE com sombras soft (1 realtime, doc 12 §2.4) — intensidade
            // 1.2 e inclinação ~50° p/ realçar as tropas e dar silhueta clara contra a pista.
            Light sun = FindDirectionalLight();
            if (sun != null)
            {
                sun.color = new Color(1.00f, 0.95f, 0.84f);
                sun.intensity = 1.2f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.8f;
                sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
                RenderSettings.sun = sun;
            }
            else
            {
                Debug.LogWarning("MAR Tools: cena Game sem directional light — rode o Setup Project.");
            }

            // 2ª luz de PREENCHIMENTO fraca, vinda da frente/baixo (a câmera olha o +Z): tira o
            // "lado escuro" das tropas que o sol único deixaria na frente, sem nova sombra
            // (mantém o orçamento de 1 sombra realtime do doc 12 §2.4).
            EnsureFillLight();

            // câmera: skybox visível + pós-processo ligado
            Camera cam = FindMainCamera();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                UniversalAdditionalCameraData camData = cam.GetUniversalAdditionalCameraData();
                if (camData != null) camData.renderPostProcessing = true;
            }

            // Volume global (Bloom/Vignette/ACES/saturação) — find-or-create, nunca duplica
            GameObject volumeGo = GameObject.Find("PostFX_Global");
            if (volumeGo == null) volumeGo = new GameObject("PostFX_Global");
            var volume = volumeGo.GetComponent<Volume>();
            if (volume == null) volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;

            // atmosfera default da cena = W01 (runtime troca por mundo no BeginRun) —
            // MESMA receita do runtime: WorldAtmosphereApplier.Apply é a fonte única
            if (w01 != null) WorldAtmosphereApplier.Apply(w01, sun);

            RegisterAtmosphereApplier(sun);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Sombras soft no pipeline (o GreyboxFactory as desliga — aqui é a direção de arte).</summary>
        private static void EnablePipelineSoftShadows()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipeline == null)
            {
                Debug.LogWarning("MAR Tools: URP asset do greybox ausente — sombras soft não aplicadas.");
                return;
            }
            var serialized = new SerializedObject(pipeline);
            SerializedProperty soft = serialized.FindProperty("m_SoftShadowsSupported");
            if (soft != null) soft.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }

        private static VolumeProfile CreatePostFxProfile()
        {
            string folder = PostFxProfilePath.Substring(0, PostFxProfilePath.LastIndexOf('/'));
            EnsureFolder(folder);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostFxProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PostFxProfilePath);
            }

            // Bloom moderado: HDR está OFF (doc 12 §2.4), então o threshold fica ABAIXO de
            // 1.0 — só os emissivos clampados em ~1 (faixas, moldura de portal) acendem.
            Bloom bloom = GetOrAddOverride<Bloom>(profile);
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 0.9f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.55f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.6f;

            Vignette vignette = GetOrAddOverride<Vignette>(profile);
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.22f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.45f;

            Tonemapping tonemapping = GetOrAddOverride<Tonemapping>(profile);
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.ACES;

            ColorAdjustments colors = GetOrAddOverride<ColorAdjustments>(profile);
            colors.saturation.overrideState = true;
            colors.saturation.value = 10f;            // +10 de saturação (vibrante, doc 01 §6)
            colors.postExposure.overrideState = true;
            colors.postExposure.value = 0.15f;        // compensa o roll-off do ACES em LDR

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T GetOrAddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component;
            if (profile.TryGet(out component)) return component;
            component = profile.Add<T>(false);
            component.name = typeof(T).Name;
            component.active = true;
            if (EditorUtility.IsPersistent(profile))
                AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        /// <summary>
        /// WorldAtmosphereApplier na cena Game, registrado no FINAL do _managersInOrder do
        /// GameSceneBootstrap (depende só do GameManager, que nasce em Boot).
        /// </summary>
        private static void RegisterAtmosphereApplier(Light sun)
        {
            GameSceneBootstrap bootstrap =
                Object.FindAnyObjectByType<GameSceneBootstrap>(FindObjectsInactive.Include);
            if (bootstrap == null)
            {
                Debug.LogError("MAR Tools: GameSceneBootstrap ausente na cena Game — atmosfera de " +
                               "runtime não registrada. Rode MAR Tools/Setup Project.");
                return;
            }

            WorldAtmosphereApplier applier =
                Object.FindAnyObjectByType<WorldAtmosphereApplier>(FindObjectsInactive.Include);
            if (applier == null)
            {
                var go = new GameObject("WorldAtmosphere");
                go.transform.SetParent(bootstrap.transform, false);
                applier = go.AddComponent<WorldAtmosphereApplier>();
            }
            WireField(applier, "_sunLight", sun);
            AppendToComponentArray(bootstrap, "_managersInOrder", applier);
        }

        private static Light FindDirectionalLight()
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            foreach (Light light in lights)
                if (light.type == LightType.Directional) return light;
            return null;
        }

        /// <summary>
        /// Luz de preenchimento direcional fraca, fria, vinda da FRENTE-BAIXO (a câmera olha
        /// o +Z): ilumina o lado das tropas voltado para a câmera, que o sol único (vindo de
        /// trás/cima) deixaria escuro. SEM sombras — não conta no orçamento de 1 sombra
        /// realtime (doc 12 §2.4). Find-or-create idempotente pelo nome.
        /// </summary>
        private static void EnsureFillLight()
        {
            GameObject go = GameObject.Find("Fill Light");
            if (go == null) go = new GameObject("Fill Light");
            var fill = go.GetComponent<Light>();
            if (fill == null) fill = go.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.74f, 0.82f, 1.00f);   // fria/azulada: contrasta com o sol quente
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;
            // aponta p/ baixo-frente (a luz "vem" de cima-frente da câmera), realça as frentes
            go.transform.rotation = Quaternion.Euler(30f, 200f, 0f);
        }

        private static Camera FindMainCamera()
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            foreach (Camera cam in cameras)
                if (cam.CompareTag("MainCamera")) return cam;
            return cameras.Length > 0 ? cameras[0] : null;
        }

        // ------------------------------------------------------------------ infra

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

        /// <summary>Liga campo [SerializeField] por nome — campo inexistente é ERRO explícito.</summary>
        private static void WireField(Component target, string fieldName, Object value)
        {
            if (target == null) return;
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"MAR Tools: campo serializado '{fieldName}' não existe em " +
                               $"{target.GetType().Name} — wiring ignorado.", target);
                return;
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireArray(Component target, string fieldName, Object[] values)
        {
            if (target == null) return;
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null || !property.isArray)
            {
                Debug.LogError($"MAR Tools: campo-array serializado '{fieldName}' não existe (ou não é " +
                               $"array) em {target.GetType().Name} — wiring ignorado.", target);
                return;
            }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Acrescenta no FINAL do array serializado se ainda não estiver lá (idempotente).</summary>
        private static void AppendToComponentArray(Component target, string fieldName, Component value)
        {
            if (target == null || value == null) return;
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null || !property.isArray)
            {
                Debug.LogError($"MAR Tools: campo-array serializado '{fieldName}' não existe (ou não é " +
                               $"array) em {target.GetType().Name} — append ignorado.", target);
                return;
            }
            for (int i = 0; i < property.arraySize; i++)
            {
                if (property.GetArrayElementAtIndex(i).objectReferenceValue == value) return;   // já registrado
            }
            property.arraySize++;
            property.GetArrayElementAtIndex(property.arraySize - 1).objectReferenceValue = value;
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
