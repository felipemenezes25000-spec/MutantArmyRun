using System.Collections.Generic;
using System.IO;
using MutantArmy.Core;
using MutantArmy.Gameplay;
using MutantArmy.Services;
using MutantArmy.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MutantArmy.Editor
{
    /// <summary>
    /// MAR Tools/Build Juice — monta toda a camada de juice/VFX/áudio por código:
    /// 1. Copia os clips CC0 do staging (PLANO-DE-USO §1.7) para Assets/_Project/Audio com
    ///    nomes canônicos por evento; fonte ausente = aviso, nunca erro (fallback nulo).
    /// 2. Copia texturas do Kenney Particle Pack (§1.8) + gera o anel do telegraph em código.
    /// 3. Materiais URP Particles/Unlit (aditivo/alpha) e 5 prefabs de ParticleSystem
    ///    construídos em código (burst de portal, pop, moeda, confete, desmonte).
    /// 4. AudioCatalogSO preenchido por nome (clip ausente fica null — no-op silencioso).
    /// 5. Volume profile de derrota (ColorAdjustments saturation −100) p/ o JuiceController.
    /// 6. Cena Boot: catálogo no AudioManager + AudioListener/AudioSources garantidos.
    /// 7. Cena Game: JuiceController + DevScreenshotRig + FloatingTextSpawner + TutorialController
    ///    (FTUE: dicas ARRASTE/ESCOLHA no HudCanvas) adicionados e ligados; VFXManager recebe os
    ///    prefabs novos + decal de telegraph; câmera com post-processing ligado (dessaturação).
    /// Idempotente: re-rodar atualiza no lugar, nunca duplica.
    /// </summary>
    public static class JuiceFactory
    {
        private const string AudioFolder = "Assets/_Project/Audio";
        private const string VfxTexturesFolder = "Assets/_Project/VFX/Textures";
        private const string VfxMaterialsFolder = "Assets/_Project/VFX/Materials";
        private const string VfxPrefabsFolder = "Assets/_Project/Prefabs/VFX";
        private const string PostFxFolder = "Assets/_Project/Settings/PostFX";
        private const string CatalogPath = "Assets/_Project/ScriptableObjects/Audio/AudioCatalog.asset";
        private const string DefeatProfilePath = PostFxFolder + "/Profile_DefeatDesaturation.asset";

        private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

        // staging fica AO LADO da pasta do projeto: <raiz>\_assets-staging
        private static string StagingRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "_assets-staging"));

        // destino canônico (por evento) → origem no staging (PLANO-DE-USO §1.7, tudo CC0)
        private static readonly (string dest, string source)[] AudioMap =
        {
            ("sfx_gate_positive.ogg", @"audio\kenney_digital-audio\Audio\phaserUp4.ogg"),
            ("sfx_gate_negative.ogg", @"audio\kenney_digital-audio\Audio\phaserDown1.ogg"),
            ("sfx_coin.ogg", @"audio\kenney_rpg-audio\Audio\handleCoins.ogg"),
            ("sfx_pop.ogg", @"audio\kenney_digital-audio\Audio\pepSound3.ogg"),
            ("sfx_boss_hit.ogg", @"audio\kenney_impact-sounds\Audio\impactPunch_heavy_001.ogg"),
            ("sfx_boss_roar.ogg", @"audio\kenney_sci-fi-sounds\Audio\lowFrequency_explosion_000.ogg"),   // Lacuna L6
            ("sfx_ui_click.ogg", @"audio\kenney_interface-sounds\Audio\click_002.ogg"),
            ("sfx_ui_confirm.ogg", @"audio\kenney_interface-sounds\Audio\confirmation_002.ogg"),
            ("sfx_supply_fanfare.ogg", @"audio\kenney_digital-audio\Audio\powerUp1.ogg"),
            ("sfx_mutation.ogg", @"audio\kenney_digital-audio\Audio\powerUp6.ogg"),
            ("jingle_victory.ogg", @"audio\kenney_music-jingles\Audio\Hit jingles\jingles_HIT16.ogg"),
            ("jingle_defeat.ogg", @"audio\kenney_music-jingles\Audio\Hit jingles\jingles_HIT04.ogg")
        };

        private static readonly (string dest, string source)[] TextureMap =
        {
            ("vfx_circle.png", @"vfx\kenney_particle-pack\PNG (Transparent)\circle_05.png"),
            ("vfx_star.png", @"vfx\kenney_particle-pack\PNG (Transparent)\star_06.png"),
            ("vfx_spark.png", @"vfx\kenney_particle-pack\PNG (Transparent)\spark_04.png"),
            ("vfx_smoke.png", @"vfx\kenney_particle-pack\PNG (Transparent)\smoke_07.png")
        };

        [MenuItem("MAR Tools/Build Juice")]
        public static void BuildAll()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("MAR Tools: Build Juice não roda em play mode.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                Debug.Log("MAR Tools: cena Game ausente — rodando ProjectSetup.SetupProject() antes do juice.");
                ProjectSetup.SetupProject();
            }

            EnsureFolder(AudioFolder);
            EnsureFolder(VfxTexturesFolder);
            EnsureFolder(VfxMaterialsFolder);
            EnsureFolder(VfxPrefabsFolder);
            EnsureFolder(PostFxFolder);
            EnsureFolder("Assets/_Project/ScriptableObjects/Audio");

            ImportAudioFromStaging();
            ImportTexturesFromStaging();
            GenerateRingTexture();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            MakeSpriteImportable(VfxTexturesFolder + "/vfx_circle.png");

            Dictionary<string, Material> mats = CreateMaterials();
            Dictionary<string, ParticleSystem> prefabs = CreateParticlePrefabs(mats);
            AudioCatalogSO catalog = CreateAudioCatalog();
            VolumeProfile defeatProfile = CreateDefeatProfile();

            WireBootScene(catalog);
            WireGameScene(prefabs, mats, defeatProfile);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MAR Tools: juice pronto — áudio importado + catálogo, 5 prefabs de partícula, " +
                      "telegraph pulsante, volume de derrota e cenas Boot/Game costuradas " +
                      "(JuiceController, DevScreenshotRig, FloatingTextSpawner, TutorialController FTUE).");
        }

        // ------------------------------------------------------------------ 1/2. import do staging

        private static void ImportAudioFromStaging()
        {
            CopyFromStaging(AudioMap, AudioFolder);
        }

        private static void ImportTexturesFromStaging()
        {
            CopyFromStaging(TextureMap, VfxTexturesFolder);
        }

        private static void CopyFromStaging((string dest, string source)[] map, string destFolder)
        {
            string staging = StagingRoot;
            if (!Directory.Exists(staging))
            {
                Debug.LogWarning("MAR Tools: staging não encontrado em " + staging +
                                 " — assets ficarão nulos (fallback silencioso).");
                return;
            }

            foreach ((string dest, string source) in map)
            {
                string sourcePath = Path.Combine(staging, source);
                string destPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", destFolder, dest));
                if (!File.Exists(sourcePath))
                {
                    Debug.LogWarning("MAR Tools: fonte ausente no staging: " + source + " — '" + dest + "' pulado.");
                    continue;
                }
                if (File.Exists(destPath)) continue;   // idempotente: nunca re-copia (preserva tweaks)
                File.Copy(sourcePath, destPath);
            }
        }

        // anel do telegraph gerado em código (não há ring dedicado no Particle Pack):
        // 256², branco, raio interno/externo com borda suave — tint vem do material/MPB
        private static void GenerateRingTexture()
        {
            string assetPath = VfxTexturesFolder + "/vfx_telegraph_ring.png";
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            if (File.Exists(fullPath)) return;

            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float outer = size * 0.46f;
            float inner = size * 0.34f;
            float soft = size * 0.05f;

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner - soft, inner + soft, d))
                              * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(outer - soft, outer + soft, d)));
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            tex.SetPixels32(pixels);
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void MakeSpriteImportable(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            if (importer.textureType == TextureImporterType.Sprite) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ 3. materiais + prefabs

        private static Dictionary<string, Material> CreateMaterials()
        {
            var mats = new Dictionary<string, Material>
            {
                ["burst"] = ParticleMaterial("M_VFX_Burst", VfxTexturesFolder + "/vfx_circle.png", additive: true),
                ["spark"] = ParticleMaterial("M_VFX_Spark", VfxTexturesFolder + "/vfx_spark.png", additive: true),
                ["confetti"] = ParticleMaterial("M_VFX_Confetti", VfxTexturesFolder + "/vfx_star.png", additive: false),
                ["smoke"] = ParticleMaterial("M_VFX_Smoke", VfxTexturesFolder + "/vfx_smoke.png", additive: false),
                ["telegraph"] = ParticleMaterial("M_VFX_Telegraph", VfxTexturesFolder + "/vfx_telegraph_ring.png",
                                                 additive: false, new Color(1f, 0.15f, 0.10f, 0.6f))
            };
            return mats;
        }

        private static Material ParticleMaterial(string name, string texturePath, bool additive)
        {
            return ParticleMaterial(name, texturePath, additive, Color.white);
        }

        private static Material ParticleMaterial(string name, string texturePath, bool additive, Color color)
        {
            string path = VfxMaterialsFolder + "/" + name + ".mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

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

            // receita URP transparent: aditivo = SrcAlpha/One, alpha = SrcAlpha/OneMinusSrcAlpha
            mat.SetOverrideTag("RenderType", "Transparent");
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", additive ? 2f : 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
                mat.SetFloat("_DstBlend", additive
                    ? (float)UnityEngine.Rendering.BlendMode.One
                    : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            if (texture != null) mat.mainTexture = texture;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Dictionary<string, ParticleSystem> CreateParticlePrefabs(Dictionary<string, Material> mats)
        {
            var prefabs = new Dictionary<string, ParticleSystem>
            {
                ["gateBurst"] = BuildBurstPrefab("PS_GateBurst", mats["burst"], Color.white,
                    count: 22, speedMin: 2.5f, speedMax: 5.5f, sizeMin: 0.18f, sizeMax: 0.40f,
                    lifeMin: 0.45f, lifeMax: 0.70f, gravity: 0.1f),
                ["popBurst"] = BuildBurstPrefab("PS_PopBurst", mats["spark"], new Color(0.6f, 1f, 0.7f),
                    count: 6, speedMin: 1.5f, speedMax: 3.0f, sizeMin: 0.12f, sizeMax: 0.22f,
                    lifeMin: 0.30f, lifeMax: 0.45f, gravity: 0f),
                ["coinBurst"] = BuildBurstPrefab("PS_CoinBurst", mats["burst"], new Color(1f, 0.84f, 0.25f),
                    count: 5, speedMin: 3.0f, speedMax: 5.0f, sizeMin: 0.20f, sizeMax: 0.30f,
                    lifeMin: 0.50f, lifeMax: 0.70f, gravity: 1.2f),
                ["despawnBurst"] = BuildBurstPrefab("PS_DespawnBurst", mats["smoke"], new Color(0.7f, 0.7f, 0.75f, 0.8f),
                    count: 10, speedMin: 1.0f, speedMax: 2.0f, sizeMin: 0.40f, sizeMax: 0.80f,
                    lifeMin: 0.50f, lifeMax: 0.80f, gravity: -0.1f),
                ["confetti"] = BuildConfettiPrefab("PS_Confetti", mats["confetti"])
            };
            return prefabs;
        }

        private static ParticleSystem BuildBurstPrefab(string name, Material mat, Color color,
            int count, float speedMin, float speedMax, float sizeMin, float sizeMax,
            float lifeMin, float lifeMax, float gravity)
        {
            string path = VfxPrefabsFolder + "/" + name + ".prefab";
            var go = new GameObject(name);
            try
            {
                ParticleSystem ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                ParticleSystem.MainModule main = ps.main;
                main.duration = lifeMax;
                main.loop = false;
                main.playOnAwake = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
                main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
                main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
                main.startColor = color;
                main.gravityModifier = gravity;
                main.maxParticles = count + 8;   // orçamento §6.3: teto justo por sistema
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                ParticleSystem.EmissionModule emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

                ParticleSystem.ShapeModule shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.45f;

                ApplyFadeOut(ps);
                ConfigureRenderer(go, mat);

                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<ParticleSystem>();
        }

        // confete da vitória (doc 09 §4.4): cone para cima com spread, estrelas coloridas
        // girando, gravidade leve — 2 instâncias são posicionadas pelo VFXManager
        private static ParticleSystem BuildConfettiPrefab(string name, Material mat)
        {
            string path = VfxPrefabsFolder + "/" + name + ".prefab";
            var go = new GameObject(name);
            try
            {
                ParticleSystem ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                ParticleSystem.MainModule main = ps.main;
                main.duration = 1.2f;
                main.loop = false;
                main.playOnAwake = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 11f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.30f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.gravityModifier = 0.7f;
                main.maxParticles = 120;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                // estrelas multicoloridas: gradiente em modo RandomColor (paleta viva do doc 01 §6)
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color(0.20f, 0.75f, 1.00f), 0.00f),   // ciano
                        new GradientColorKey(new Color(1.00f, 0.80f, 0.20f), 0.33f),   // âmbar
                        new GradientColorKey(new Color(0.45f, 0.95f, 0.35f), 0.66f),   // verde
                        new GradientColorKey(new Color(0.95f, 0.35f, 0.75f), 1.00f)    // rosa
                    },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                var startColor = new ParticleSystem.MinMaxGradient(gradient)
                {
                    mode = ParticleSystemGradientMode.RandomColor
                };
                main.startColor = startColor;

                ParticleSystem.EmissionModule emission = ps.emission;
                emission.rateOverTime = 70f;

                ParticleSystem.ShapeModule shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 28f;
                shape.radius = 0.25f;
                shape.rotation = new Vector3(-80f, 0f, 0f);   // aponta para cima/dentro do quadro

                ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-4f, 4f);   // rad/s: confete rodopia

                ApplyFadeOut(ps);
                ConfigureRenderer(go, mat);

                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<ParticleSystem>();
        }

        private static void ApplyFadeOut(ParticleSystem ps)
        {
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void ConfigureRenderer(GameObject go, Material mat)
        {
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ------------------------------------------------------------------ 4. catálogo de áudio

        private static AudioCatalogSO CreateAudioCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalogSO>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AudioCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.gatePositive = LoadClip("sfx_gate_positive");
            catalog.gateNegative = LoadClip("sfx_gate_negative");
            catalog.coin = LoadClip("sfx_coin");
            catalog.pop = LoadClip("sfx_pop");
            catalog.bossHit = LoadClip("sfx_boss_hit");
            catalog.bossRoar = LoadClip("sfx_boss_roar");
            catalog.uiClick = LoadClip("sfx_ui_click");
            catalog.uiConfirm = LoadClip("sfx_ui_confirm");
            catalog.victoryJingle = LoadClip("jingle_victory");
            catalog.defeatJingle = LoadClip("jingle_defeat");

            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        // clip ausente fica NULL — o AudioManager trata como no-op silencioso (contrato)
        private static AudioClip LoadClip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/" + name + ".ogg");
            if (clip == null)
                Debug.LogWarning("MAR Tools: clip '" + name + "' não importado — evento ficará mudo (fallback nulo).");
            return clip;
        }

        // ------------------------------------------------------------------ 5. volume de derrota

        private static VolumeProfile CreateDefeatProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(DefeatProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, DefeatProfilePath);
            }

            ColorAdjustments adjustments = null;
            foreach (VolumeComponent component in profile.components)
            {
                if (component is ColorAdjustments found)
                {
                    adjustments = found;
                    break;
                }
            }
            if (adjustments == null)
            {
                adjustments = profile.Add<ColorAdjustments>(false);
                adjustments.name = "ColorAdjustments_Defeat";
                AssetDatabase.AddObjectToAsset(adjustments, profile);
            }

            adjustments.active = true;
            adjustments.saturation.overrideState = true;
            adjustments.saturation.value = -100f;   // dessaturação total; o peso anima 0→1

            EditorUtility.SetDirty(adjustments);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        // ------------------------------------------------------------------ 6. cena Boot

        private static void WireBootScene(AudioCatalogSO catalog)
        {
            Scene scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            var audio = Object.FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);
            if (audio == null)
            {
                Debug.LogError("MAR Tools: AudioManager não existe na cena Boot — rode MAR Tools/Setup Project.");
            }
            else
            {
                WireField(audio, "_catalog", catalog);
                WireField(audio, "_supplyFanfareClip", LoadClip("sfx_supply_fanfare"));
                WireField(audio, "_mutationClip", LoadClip("sfx_mutation"));
                EnsureAudioSources(audio);
            }

            EnsureAudioListener();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // garante as 2 fontes do prefab [Services] (música + SFX) ligadas nos campos reais
        private static void EnsureAudioSources(AudioManager audio)
        {
            var serialized = new SerializedObject(audio);
            SerializedProperty music = serialized.FindProperty("_musicSource");
            SerializedProperty sfx = serialized.FindProperty("_sfxSource");
            if (music == null || sfx == null)
            {
                Debug.LogError("MAR Tools: campos _musicSource/_sfxSource não existem no AudioManager.");
                return;
            }

            if (music.objectReferenceValue == null)
            {
                AudioSource source = audio.gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                music.objectReferenceValue = source;
            }
            if (sfx.objectReferenceValue == null)
            {
                AudioSource source = audio.gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                sfx.objectReferenceValue = source;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureAudioListener()
        {
            if (Object.FindFirstObjectByType<AudioListener>(FindObjectsInactive.Include) != null) return;
            Camera cam = Camera.main;
            if (cam != null) cam.gameObject.AddComponent<AudioListener>();
            else Debug.LogWarning("MAR Tools: cena sem Camera.main — AudioListener não garantido.");
        }

        // ------------------------------------------------------------------ 7. cena Game

        private static void WireGameScene(Dictionary<string, ParticleSystem> prefabs,
                                          Dictionary<string, Material> mats, VolumeProfile defeatProfile)
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            // --- VFXManager: prefabs novos + decal de telegraph ---
            var vfx = Object.FindFirstObjectByType<VFXManager>(FindObjectsInactive.Include);
            if (vfx == null)
            {
                Debug.LogError("MAR Tools: VFXManager não existe na cena Game — rode MAR Tools/Setup Project.");
            }
            else
            {
                (Transform decalRoot, Renderer decalRenderer) = EnsureTelegraphDecal(scene, mats["telegraph"]);
                WireField(vfx, "_gateBurstPrefab", prefabs["gateBurst"]);
                WireField(vfx, "_popBurstPrefab", prefabs["popBurst"]);
                WireField(vfx, "_confettiPrefab", prefabs["confetti"]);
                WireField(vfx, "_coinFanfarePrefab", prefabs["coinBurst"]);
                WireField(vfx, "_despawnBurstPrefab", prefabs["despawnBurst"]);
                WireField(vfx, "_telegraphDecal", decalRoot);
                WireField(vfx, "_telegraphRenderer", decalRenderer);
            }

            // --- Volume de derrota + post-processing na câmera ---
            Volume defeatVolume = EnsureDefeatVolume(scene, defeatProfile);
            EnablePostProcessingOnCamera();

            // --- JuiceController ---
            var juice = Object.FindFirstObjectByType<JuiceController>(FindObjectsInactive.Include);
            if (juice == null) juice = new GameObject("[Juice]").AddComponent<JuiceController>();
            WireField(juice, "_defeatVolume", defeatVolume);

            // --- DevScreenshotRig (inerte sem -screenshotRun) ---
            if (Object.FindFirstObjectByType<DevScreenshotRig>(FindObjectsInactive.Include) == null)
                new GameObject("[DevScreenshotRig]").AddComponent<DevScreenshotRig>();

            // --- FloatingTextSpawner no HudCanvas ---
            WireFloatingTextSpawner(scene);

            // --- TutorialController (FTUE) no HudCanvas ---
            WireTutorialController(scene);

            EnsureAudioListener();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static (Transform, Renderer) EnsureTelegraphDecal(Scene scene, Material mat)
        {
            GameObject root = FindInScene(scene, "TelegraphDecal");
            if (root == null)
            {
                root = new GameObject("TelegraphDecal");
                var ring = new GameObject("Ring");
                ring.transform.SetParent(root.transform, false);
                ring.transform.localPosition = new Vector3(0f, 0.05f, 0f);   // acima do chão: sem z-fight
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var filter = ring.AddComponent<MeshFilter>();
                filter.sharedMesh = GetPrimitiveMesh(PrimitiveType.Quad);
                var meshRenderer = ring.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                root.SetActive(false);   // VFXManager ativa no ShowTelegraph
            }

            Renderer rendererRef = root.GetComponentInChildren<Renderer>(true);
            if (rendererRef != null) rendererRef.sharedMaterial = mat;
            return (root.transform, rendererRef);
        }

        private static Volume EnsureDefeatVolume(Scene scene, VolumeProfile profile)
        {
            GameObject go = FindInScene(scene, "DefeatVolume");
            if (go == null) go = new GameObject("DefeatVolume");
            Volume volume = go.GetComponent<Volume>();
            if (volume == null) volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            volume.weight = 0f;            // o JuiceController anima 0→1 na derrota
            volume.sharedProfile = profile;
            return volume;
        }

        private static void EnablePostProcessingOnCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("MAR Tools: cena Game sem Camera.main — post-processing não ligado.");
                return;
            }
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;   // sem isto ColorAdjustments não tem efeito
            EditorUtility.SetDirty(cam.gameObject);
        }

        private static void WireFloatingTextSpawner(Scene scene)
        {
            GameObject hudCanvas = FindInScene(scene, "HudCanvas");
            if (hudCanvas == null)
            {
                Debug.LogWarning("MAR Tools: HudCanvas não existe na cena Game — FloatingTextSpawner pulado.");
                return;
            }

            var spawner = hudCanvas.GetComponentInChildren<FloatingTextSpawner>(true);
            if (spawner == null)
            {
                var layerGo = new GameObject("FloatingTextLayer", typeof(RectTransform));
                var layerRect = (RectTransform)layerGo.transform;
                layerRect.SetParent(hudCanvas.transform, false);
                layerRect.anchorMin = Vector2.zero;
                layerRect.anchorMax = Vector2.one;
                layerRect.offsetMin = Vector2.zero;
                layerRect.offsetMax = Vector2.zero;
                layerRect.SetAsLastSibling();   // moedas voam SOBRE o HUD
                spawner = layerGo.AddComponent<FloatingTextSpawner>();
            }

            RectTransform coinTarget = null;
            Transform coinsText = FindChildRecursive(hudCanvas.transform, "CoinsText");
            if (coinsText != null) coinTarget = coinsText as RectTransform;

            var coinSprite = AssetDatabase.LoadAssetAtPath<Sprite>(VfxTexturesFolder + "/vfx_circle.png");

            WireField(spawner, "_layer", spawner.transform as RectTransform);
            WireField(spawner, "_coinTarget", coinTarget);
            WireField(spawner, "_coinSprite", coinSprite);
        }

        // ------------------------------------------------------------------ FTUE (tutorial)

        // Monta a camada de onboarding (doc 14 §6) no HudCanvas: um [Tutorial] com a dica de
        // ARRASTE (dedo deslizando + rótulo) e o callout ESCOLHA!. Tudo raycastTarget OFF para
        // o AutoPilot continuar jogando por baixo. Idempotente: re-roda atualiza no lugar.
        private static void WireTutorialController(Scene scene)
        {
            GameObject hudCanvas = FindInScene(scene, "HudCanvas");
            if (hudCanvas == null)
            {
                Debug.LogWarning("MAR Tools: HudCanvas não existe na cena Game — TutorialController pulado.");
                return;
            }

            var controller = hudCanvas.GetComponentInChildren<TutorialController>(true);
            RectTransform root;
            if (controller == null)
            {
                var rootGo = new GameObject("[Tutorial]", typeof(RectTransform));
                root = (RectTransform)rootGo.transform;
                root.SetParent(hudCanvas.transform, false);
                StretchRect(root);
                root.SetAsLastSibling();   // dicas SOBRE o HUD, sob as moedas voadoras
                controller = rootGo.AddComponent<TutorialController>();
            }
            else
            {
                root = (RectTransform)controller.transform;
            }

            // (a) ARRASTE: dedo (círculo) + 2 setas + rótulo, ancorado no terço inferior central.
            (RectTransform dragHint, CanvasGroup dragGroup) = EnsureHintHolder(root, "DragHint",
                new Vector2(0f, 340f));
            BuildDragHintVisuals(dragHint);

            // (b) ESCOLHA!: callout com setas ◄ ► acima do centro — onde o 1º par de portais surge.
            (RectTransform chooseHint, CanvasGroup chooseGroup) = EnsureHintHolder(root, "ChooseHint",
                new Vector2(0f, 60f));
            BuildChooseHintVisuals(chooseHint);

            WireField(controller, "_dragHint", dragHint);
            WireField(controller, "_dragHintGroup", dragGroup);
            WireField(controller, "_chooseHint", chooseHint);
            WireField(controller, "_chooseHintGroup", chooseGroup);
        }

        // Holder de uma dica: RectTransform centrado (âncora inferior) + CanvasGroup p/ fade.
        // Limpa os filhos antigos para o rebuild ser determinístico ao re-rodar a factory.
        private static (RectTransform, CanvasGroup) EnsureHintHolder(RectTransform parent, string name,
                                                                     Vector2 anchoredPos)
        {
            Transform existing = FindChildRecursive(parent, name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(700f, 220f);

            for (int i = rect.childCount - 1; i >= 0; i--)   // rebuild limpo
                Object.DestroyImmediate(rect.GetChild(i).gameObject);

            var group = go.GetComponent<CanvasGroup>();
            if (group == null) group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;   // nunca rouba o toque do AutoPilot/jogador
            go.SetActive(false);            // o controller ativa por evento
            return (rect, group);
        }

        // Dedo deslizante: um disco (vfx_circle) com 2 chevrons e o rótulo "ARRASTE".
        private static void BuildDragHintVisuals(RectTransform holder)
        {
            var circle = AssetDatabase.LoadAssetAtPath<Sprite>(VfxTexturesFolder + "/vfx_circle.png");
            Image finger = CreateHintImage(holder, "Finger", circle, new Color(1f, 1f, 1f, 0.95f),
                new Vector2(0f, 10f), new Vector2(120f, 120f));
            // halo levemente maior atrás do dedo, dá volume sem asset extra
            CreateHintImage(finger.rectTransform, "Halo", circle, new Color(0.30f, 0.85f, 1f, 0.35f),
                Vector2.zero, new Vector2(180f, 180f)).rectTransform.SetAsFirstSibling();

            CreateHintLabel(holder, "Label", "ARRASTE  ◄ ►", 56f,
                new Vector2(0f, -90f), new Vector2(640f, 80f), new Color(0.95f, 0.98f, 1f));
        }

        // Callout de escolha: rótulo "ESCOLHA!" com setas para os dois lados (os 2 portais).
        private static void BuildChooseHintVisuals(RectTransform holder)
        {
            CreateHintLabel(holder, "Label", "◄  ESCOLHA!  ►", 64f,
                Vector2.zero, new Vector2(680f, 110f), new Color(1f, 0.86f, 0.35f));
        }

        private static Image CreateHintImage(Transform parent, string name, Sprite sprite, Color color,
                                             Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateHintLabel(Transform parent, string name, string content, float fontSize,
                                                Vector2 anchoredPos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            text.raycastTarget = false;

            // mesma cascata de fonte do ProjectSetup: skin premium → LiberationSans fallback.
            TMP_FontAsset font = UiSkin.FontAsset;
            if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null) text.font = font;
            return text;
        }

        private static void StretchRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------ infra

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                Transform found = FindChildRecursive(root.transform, name);
                if (found != null) return found.gameObject;
            }
            return null;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name) return child;
                Transform found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

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
                Debug.LogError(
                    $"MAR Tools: campo serializado '{fieldName}' não existe em {target.GetType().Name} — wiring ignorado.",
                    target);
                return;
            }
            property.objectReferenceValue = value;
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
