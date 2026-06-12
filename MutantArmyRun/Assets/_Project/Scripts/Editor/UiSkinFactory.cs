using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace MutantArmy.Editor
{
    /// <summary>
    /// MAR Tools/Build UI Skin — materializa o skin "mobile casual premium" (doc 01 §6)
    /// a partir dos packs CC0 do staging (PLANO-DE-USO §1.6): copia os sprites Kenney
    /// para Assets/_Project/Art/UI (9-slice configurado por código), gera a textura de
    /// gradiente do menu e cria o TMP_FontAsset da Kenney Future com material outline.
    /// Idempotente: cópias só quando faltam, importers só re-aplicados quando divergem,
    /// fonte criada 1× (re-rodar só re-afina o outline). Staging ausente NÃO é erro:
    /// loga warning e o ProjectSetup degrada para o visual builtin (greybox).
    /// </summary>
    public static class UiSkinFactory
    {
        private const string ArtRoot = "Assets/_Project/Art/UI";
        private const string SpritesFolder = ArtRoot + "/Sprites";
        private const string FontsFolder = ArtRoot + "/Fonts";
        private const string GeneratedFolder = ArtRoot + "/Generated";

        private const string FontTtfPath = FontsFolder + "/KenneyFuture.ttf";
        internal const string FontAssetPath = FontsFolder + "/KenneyFuture SDF.asset";
        internal const string FallbackMaterialPath = FontsFolder + "/LiberationSans Outline.mat";
        internal const string GradientPath = GeneratedFolder + "/menu_gradient.png";

        // Outline canônico do skin: tinta escura azulada — legível sobre o gradiente
        // vibrante E sobre o gameplay (doc 01 §6.4: legível em vídeo 9:16 comprimido).
        private static readonly Color OutlineColor = new Color(0.10f, 0.07f, 0.20f, 1f);
        private const float OutlineWidth = 0.14f;
        private const float FaceDilate = 0.06f;

        /// <summary>
        /// Tabela necessidade→arquivo do PLANO-DE-USO §1.6 (variantes Double = @2x,
        /// nítidas no canvas 1080x1920). border = 9-slice em pixels (L,B,R,T);
        /// Vector4.zero = sprite usado inteiro (ícones/discos).
        /// </summary>
        private static readonly SpriteSpec[] Sprites =
        {
            // botões 9-slice (384x128) — azul base + variantes de ação
            new SpriteSpec(@"ui\kenney_ui-pack\PNG\Blue\Double\button_rectangle_depth_gradient.png", "btn_blue.png", new Vector4(36f, 36f, 36f, 36f)),
            new SpriteSpec(@"ui\kenney_ui-pack\PNG\Green\Double\button_rectangle_depth_gradient.png", "btn_green.png", new Vector4(36f, 36f, 36f, 36f)),
            new SpriteSpec(@"ui\kenney_ui-pack\PNG\Red\Double\button_rectangle_depth_gradient.png", "btn_red.png", new Vector4(36f, 36f, 36f, 36f)),
            new SpriteSpec(@"ui\kenney_ui-pack\PNG\Yellow\Double\button_rectangle_depth_gradient.png", "btn_gold.png", new Vector4(36f, 36f, 36f, 36f)),
            new SpriteSpec(@"ui\kenney_ui-pack\PNG\Grey\Double\button_rectangle_depth_gradient.png", "btn_grey.png", new Vector4(36f, 36f, 36f, 36f)),

            // painéis/badges 9-slice (128x128)
            new SpriteSpec(@"ui\kenney_ui-pack\PNG\Blue\Double\button_round_depth_gradient.png", "badge_round.png", new Vector4(44f, 44f, 44f, 44f)),
            new SpriteSpec(@"ui\kenney_ui-pack\PNG\Grey\Double\button_square_flat.png", "badge_flat.png", new Vector4(44f, 44f, 44f, 44f)),
            new SpriteSpec(@"ui\kenney_ui-pack-adventure\PNG\Double\panel_brown.png", "panel_frame.png", new Vector4(40f, 40f, 40f, 40f)),

            // barras de progresso (192x32)
            new SpriteSpec(@"ui\kenney_ui-pack\PNG\Grey\Double\slide_horizontal_grey.png", "bar_bg.png", new Vector4(14f, 14f, 14f, 14f)),

            // ícones (usados inteiros)
            new SpriteSpec(@"ui\kenney_ui-pack\PNG\Yellow\Double\button_round_depth_gradient.png", "icon_coin.png", Vector4.zero),
            new SpriteSpec(@"ui\kenney_ui-pack-adventure\PNG\Double\minimap_icon_jewel_white.png", "icon_gem.png", Vector4.zero),
            new SpriteSpec(@"ui\kenney_ui-pack\PNG\Extra\Double\icon_play_light.png", "icon_play.png", Vector4.zero),
            new SpriteSpec(@"ui\kenney_ui-pack\PNG\Yellow\Double\star.png", "icon_star.png", Vector4.zero),
            new SpriteSpec(@"ui\kenney_game-icons\PNG\White\2x\gear.png", "icon_gear.png", Vector4.zero),
            new SpriteSpec(@"ui\kenney_game-icons\PNG\White\2x\video.png", "icon_video.png", Vector4.zero),
            new SpriteSpec(@"ui\kenney_game-icons\PNG\White\2x\home.png", "icon_home.png", Vector4.zero),
            new SpriteSpec(@"ui\kenney_game-icons\PNG\White\2x\trophy.png", "icon_trophy.png", Vector4.zero),
            new SpriteSpec(@"ui\kenney_game-icons\PNG\White\2x\exclamation.png", "icon_exclamation.png", Vector4.zero),

            // anéis/brilhos (timer circular + glows do menu + orbes de elemento)
            new SpriteSpec(@"ui\kenney_ui-pack-adventure\PNG\Double\minimap_ring_white.png", "ring.png", Vector4.zero),
            new SpriteSpec(@"vfx\kenney_particle-pack\PNG (Transparent)\circle_05.png", "glow_soft.png", Vector4.zero)
        };

        private readonly struct SpriteSpec
        {
            public readonly string Source;   // relativo ao _assets-staging
            public readonly string Dest;     // nome do arquivo em Sprites/
            public readonly Vector4 Border;  // 9-slice (L,B,R,T) em px; zero = inteiro

            public SpriteSpec(string source, string dest, Vector4 border)
            {
                Source = source;
                Dest = dest;
                Border = border;
            }
        }

        // _assets-staging é irmão da pasta do projeto: <raiz>\MutantArmyRun + <raiz>\_assets-staging.
        private static string StagingRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "_assets-staging"));

        [MenuItem("MAR Tools/Build UI Skin")]
        public static void BuildAll()
        {
            EnsureFolder(SpritesFolder);
            EnsureFolder(FontsFolder);
            EnsureFolder(GeneratedFolder);

            ImportSprites();
            GenerateMenuGradient();
            BuildFontAsset();

            AssetDatabase.SaveAssets();
            Debug.Log("MAR Tools: UI skin pronto — sprites Kenney 9-slice, gradiente do menu e fonte TMP com outline.");
        }

        // ------------------------------------------------------------------ sprites

        private static void ImportSprites()
        {
            bool stagingOk = Directory.Exists(StagingRoot);
            if (!stagingOk)
                Debug.LogWarning("MAR Tools: _assets-staging não encontrado em '" + StagingRoot +
                                 "' — usando apenas sprites já importados (visual degrada para builtin onde faltar).");

            foreach (SpriteSpec spec in Sprites)
            {
                string destPath = SpritesFolder + "/" + spec.Dest;
                if (!File.Exists(FullPath(destPath)))
                {
                    if (!stagingOk) continue;
                    string source = Path.Combine(StagingRoot, spec.Source);
                    if (!File.Exists(source))
                    {
                        Debug.LogWarning("MAR Tools: sprite ausente no staging: " + spec.Source +
                                         " — o skin degrada para builtin neste elemento.");
                        continue;
                    }
                    File.Copy(source, FullPath(destPath), true);
                    AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);
                }
                ApplySpriteImporter(destPath, spec.Border);
            }
        }

        /// <summary>
        /// Importer de sprite de UI: single, sem mipmap, FullRect (tint/glow sem recorte),
        /// sem compressão (PNGs minúsculos; nitidez vale mais que KBs). Só re-importa
        /// quando algo realmente divergiu — idempotente sem churn de reimport.
        /// </summary>
        private static void ApplySpriteImporter(string assetPath, Vector4 border)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
            if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; dirty = true; }
            if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; dirty = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (importer.spriteBorder != border) { importer.spriteBorder = border; dirty = true; }

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                dirty = true;
            }

            if (dirty) importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ gradiente

        /// <summary>
        /// Gradiente vertical vibrante do menu, GERADO em código (doc 01 §6.2 — mundo 1:
        /// céu azul→violeta; gameplay/CTAs são sempre o mais colorido por contraste).
        /// Determinístico: re-rodar reescreve os mesmos bytes.
        /// </summary>
        private static void GenerateMenuGradient()
        {
            const int w = 64;
            const int h = 512;
            Color top = new Color(0.36f, 0.80f, 1.00f);     // ciano-céu
            Color mid = new Color(0.46f, 0.40f, 0.98f);     // violeta elétrico
            Color bottom = new Color(0.22f, 0.12f, 0.50f);  // violeta profundo

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var row = new Color[w];
            for (int y = 0; y < h; y++)
            {
                float t = y / (h - 1f);                      // 0 = base, 1 = topo
                Color c = t < 0.5f ? Color.Lerp(bottom, mid, t * 2f)
                                   : Color.Lerp(mid, top, (t - 0.5f) * 2f);
                for (int x = 0; x < w; x++) row[x] = c;
                tex.SetPixels(0, y, w, 1, row);
            }
            tex.Apply(false);
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            File.WriteAllBytes(FullPath(GradientPath), png);
            AssetDatabase.ImportAsset(GradientPath, ImportAssetOptions.ForceUpdate);
            ApplySpriteImporter(GradientPath, Vector4.zero);
        }

        // ------------------------------------------------------------------ fonte TMP

        /// <summary>
        /// TMP_FontAsset da Kenney Future POR CÓDIGO (TMP_FontAsset.CreateFontAsset,
        /// atlas dinâmico SDFAA 1024² — acentos PT entram on-demand) com material outline
        /// e fallback LiberationSans na própria tabela do asset (glifo ausente nunca vira
        /// quadrado). Se a TTF/criação falhar: material outline sobre a LiberationSans
        /// (bold aplicado pelo ProjectSetup) — o jogo nunca fica sem fonte.
        /// </summary>
        private static void BuildFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                ConfigureOutline(existing.material);          // re-rodar re-afina o outline
                EnsureLiberationFallback(existing);
                EditorUtility.SetDirty(existing);
                return;
            }

            // Os shaders SDF vêm dos TMP Essentials — o Setup Project importa antes.
            if (Shader.Find("TextMeshPro/Distance Field") == null)
            {
                Debug.LogWarning("MAR Tools: shaders TMP ausentes — rode MAR Tools/Setup Project " +
                                 "(importa TMP Essentials) e re-rode Build UI Skin.");
                CreateFallbackMaterial();
                return;
            }

            if (!File.Exists(FullPath(FontTtfPath)))
            {
                string source = Path.Combine(StagingRoot, @"ui\kenney_ui-pack\Font\Kenney Future.ttf");
                if (File.Exists(source))
                {
                    File.Copy(source, FullPath(FontTtfPath), true);
                    AssetDatabase.ImportAsset(FontTtfPath, ImportAssetOptions.ForceUpdate);
                }
            }

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontTtfPath);
            if (sourceFont == null)
            {
                Debug.LogWarning("MAR Tools: 'Kenney Future.ttf' indisponível (staging?) — " +
                                 "fallback LiberationSans bold + outline.");
                CreateFallbackMaterial();
                return;
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            if (fontAsset == null)
            {
                Debug.LogWarning("MAR Tools: CreateFontAsset falhou para Kenney Future — " +
                                 "fallback LiberationSans bold + outline.");
                CreateFallbackMaterial();
                return;
            }

            fontAsset.name = "KenneyFuture SDF";
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            if (fontAsset.material != null)
            {
                fontAsset.material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }
            if (fontAsset.atlasTexture != null)
            {
                fontAsset.atlasTexture.name = fontAsset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            }

            // Referência editor→TTF: sem ela o atlas dinâmico não repopula no player.
            var serialized = new SerializedObject(fontAsset);
            SerializedProperty editorRef = serialized.FindProperty("m_SourceFontFile_EditorRef");
            if (editorRef != null) editorRef.objectReferenceValue = sourceFont;
            SerializedProperty guidProp = serialized.FindProperty("m_SourceFontFileGUID");
            if (guidProp != null && guidProp.propertyType == SerializedPropertyType.String)
                guidProp.stringValue = AssetDatabase.AssetPathToGUID(FontTtfPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            ConfigureOutline(fontAsset.material);
            EnsureLiberationFallback(fontAsset);
            EditorUtility.SetDirty(fontAsset);
            Debug.Log("MAR Tools: TMP_FontAsset 'KenneyFuture SDF' criado com outline + fallback latino.");
        }

        private static void ConfigureOutline(Material material)
        {
            if (material == null) return;
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
            material.SetColor(ShaderUtilities.ID_OutlineColor, OutlineColor);
            material.SetFloat(ShaderUtilities.ID_FaceDilate, FaceDilate);
            EditorUtility.SetDirty(material);
        }

        /// <summary>Acentos PT (É/Ó/Ç…) ausentes na Kenney caem na LiberationSans — nunca ☐.</summary>
        private static void EnsureLiberationFallback(TMP_FontAsset fontAsset)
        {
            var liberation = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (liberation == null || fontAsset == null) return;
            if (fontAsset.fallbackFontAssetTable == null)
                fontAsset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
            if (!fontAsset.fallbackFontAssetTable.Contains(liberation))
                fontAsset.fallbackFontAssetTable.Add(liberation);
        }

        private static void CreateFallbackMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(FallbackMaterialPath) != null) return;
            var liberation = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (liberation == null || liberation.material == null)
            {
                Debug.LogWarning("MAR Tools: LiberationSans SDF indisponível — fallback de fonte sem outline.");
                return;
            }
            var material = new Material(liberation.material);
            ConfigureOutline(material);
            AssetDatabase.CreateAsset(material, FallbackMaterialPath);
        }

        // ------------------------------------------------------------------ helpers

        private static string FullPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
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

    /// <summary>
    /// Acesso ESTÁTICO (decisão "UISkinSO ou estático" — estático: o skin só é consumido
    /// pelo ProjectSetup em edit-time; as referências ficam serializadas nas cenas, sem
    /// asset extra para versionar). Getter nulo = asset não importado: o consumidor
    /// degrada para o builtin — nunca exceção.
    /// </summary>
    public static class UiSkin
    {
        private const string S = "Assets/_Project/Art/UI/Sprites/";

        public static Sprite ButtonBlue { get { return Load(S + "btn_blue.png"); } }
        public static Sprite ButtonGreen { get { return Load(S + "btn_green.png"); } }
        public static Sprite ButtonRed { get { return Load(S + "btn_red.png"); } }
        public static Sprite ButtonGold { get { return Load(S + "btn_gold.png"); } }
        public static Sprite ButtonGrey { get { return Load(S + "btn_grey.png"); } }
        public static Sprite BadgeRound { get { return Load(S + "badge_round.png"); } }
        public static Sprite BadgeFlat { get { return Load(S + "badge_flat.png"); } }
        public static Sprite PanelFrame { get { return Load(S + "panel_frame.png"); } }
        public static Sprite BarBackground { get { return Load(S + "bar_bg.png"); } }
        public static Sprite IconCoin { get { return Load(S + "icon_coin.png"); } }
        public static Sprite IconGem { get { return Load(S + "icon_gem.png"); } }
        public static Sprite IconPlay { get { return Load(S + "icon_play.png"); } }
        public static Sprite IconStar { get { return Load(S + "icon_star.png"); } }
        public static Sprite IconGear { get { return Load(S + "icon_gear.png"); } }
        public static Sprite IconVideo { get { return Load(S + "icon_video.png"); } }
        public static Sprite IconHome { get { return Load(S + "icon_home.png"); } }
        public static Sprite IconTrophy { get { return Load(S + "icon_trophy.png"); } }
        public static Sprite IconExclamation { get { return Load(S + "icon_exclamation.png"); } }
        public static Sprite Ring { get { return Load(S + "ring.png"); } }
        public static Sprite GlowSoft { get { return Load(S + "glow_soft.png"); } }
        public static Sprite MenuGradient { get { return Load(UiSkinFactory.GradientPath); } }

        public static TMP_FontAsset FontAsset
        {
            get { return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiSkinFactory.FontAssetPath); }
        }

        public static Material FallbackOutlineMaterial
        {
            get { return AssetDatabase.LoadAssetAtPath<Material>(UiSkinFactory.FallbackMaterialPath); }
        }

        private static Sprite Load(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
