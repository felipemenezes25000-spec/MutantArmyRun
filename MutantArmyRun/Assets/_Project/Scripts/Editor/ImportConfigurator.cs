using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MutantArmy.Editor
{
    /// <summary>
    /// Configura os importadores dos assets CC0 trazidos do staging (fase beauty):
    ///  - FBX de personagem/monstro: rig Generic, animações importadas, materiais
    ///    extraídos para Art/Models/Materials (URP gera Lit ao extrair com URP ativo);
    ///  - FBX de props: sem animação, materiais embutidos;
    ///  - texturas de VFX: sRGB + alpha transparency;
    ///  - sprites de UI: Sprite (2D and UI), 9-slice quando o nome indicar (button/panel/slide);
    ///  - áudio: Decompress On Load p/ SFX curtos, Vorbis streaming p/ jingles.
    /// Execução batch: -executeMethod MutantArmy.Editor.ImportConfigurator.ConfigureAll
    /// </summary>
    public static class ImportConfigurator
    {
        private const string CharactersRoot = "Assets/_Project/Art/Models/Characters";
        private const string BossesRoot = "Assets/_Project/Art/Models/Bosses";
        private const string PropsRoot = "Assets/_Project/Art/Models/Props";
        private const string MaterialsFolder = "Assets/_Project/Art/Models/Materials";
        private const string VfxTexturesRoot = "Assets/_Project/VFX/Textures";
        private const string UiRoot = "Assets/_Project/Art/UI";
        private const string SfxRoot = "Assets/_Project/Audio/SFX";
        private const string JinglesRoot = "Assets/_Project/Audio/Jingles";

        private const string LogPrefix = "[ImportConfigurator] ";

        [MenuItem("MAR Tools/Configure Imported Assets")]
        public static void ConfigureAll()
        {
            EnsureMaterialsFolder();

            int models = ConfigureAnimatedModels();
            int props = ConfigureProps();
            int vfx = ConfigureVfxTextures();
            int ui = ConfigureUiSprites();
            int audio = ConfigureAudio();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            LogAnimationTakes();

            Debug.Log(LogPrefix + "DONE — modelos animados: " + models +
                      ", props: " + props + ", texturas VFX: " + vfx +
                      ", sprites UI: " + ui + ", clipes de áudio: " + audio);
        }

        // ------------------------------------------------------------------ FBX

        private static int ConfigureAnimatedModels()
        {
            int count = 0;
            foreach (string path in FindAssets("t:Model", CharactersRoot, BossesRoot))
            {
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;

                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = true;
                importer.importBlendShapes = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

                importer.SaveAndReimport();
                ExtractMaterials(path);
                count++;
            }
            return count;
        }

        private static int ConfigureProps()
        {
            int count = 0;
            foreach (string path in FindAssets("t:Model", PropsRoot))
            {
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;

                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;

                importer.SaveAndReimport();
                count++;
            }
            return count;
        }

        private static void ExtractMaterials(string modelPath)
        {
            string baseName = Path.GetFileNameWithoutExtension(modelPath);
            foreach (UnityEngine.Object sub in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                if (!(sub is Material material)) continue;

                string safeName = Sanitize(baseName + "_" + material.name);
                string target = MaterialsFolder + "/" + safeName + ".mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(target) != null) continue; // já extraído

                string error = AssetDatabase.ExtractAsset(material, target);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning(LogPrefix + "Falha ao extrair material '" +
                                     material.name + "' de " + modelPath + ": " + error);
                }
            }

            AssetDatabase.WriteImportSettingsIfDirty(modelPath);
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
        }

        // -------------------------------------------------------------- Texturas

        private static int ConfigureVfxTextures()
        {
            int count = 0;
            foreach (string path in FindAssets("t:Texture2D", VfxTexturesRoot))
            {
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;

                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;

                importer.SaveAndReimport();
                count++;
            }
            return count;
        }

        private static int ConfigureUiSprites()
        {
            int count = 0;
            foreach (string path in FindAssets("t:Texture2D", UiRoot))
            {
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;

                if (NeedsNineSlice(path))
                    importer.spriteBorder = ComputeNineSliceBorder(path);

                importer.SaveAndReimport();
                count++;
            }
            return count;
        }

        private static bool NeedsNineSlice(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            return name.Contains("button") || name.Contains("panel") ||
                   name.Contains("slide_horizontal") || name.Contains("slide_vertical") ||
                   name.Contains("progress");
        }

        private static Vector4 ComputeNineSliceBorder(string path)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) return new Vector4(8f, 8f, 8f, 8f);

            float border = Mathf.Clamp(Mathf.Min(texture.width, texture.height) / 4f, 4f, 16f);
            border = Mathf.Floor(border);
            return new Vector4(border, border, border, border); // L, B, R, T
        }

        // ----------------------------------------------------------------- Áudio

        private static int ConfigureAudio()
        {
            int count = 0;

            foreach (string path in FindAssets("t:AudioClip", SfxRoot))
                if (ApplyAudioSettings(path, AudioClipLoadType.DecompressOnLoad)) count++;

            foreach (string path in FindAssets("t:AudioClip", JinglesRoot))
                if (ApplyAudioSettings(path, AudioClipLoadType.Streaming)) count++;

            return count;
        }

        private static bool ApplyAudioSettings(string path, AudioClipLoadType loadType)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) return false;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = loadType;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.8f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.loadInBackground = loadType == AudioClipLoadType.Streaming;

            importer.SaveAndReimport();
            return true;
        }

        // -------------------------------------------------------- Relatório/takes

        private static void LogAnimationTakes()
        {
            foreach (string path in FindAssets("t:Model", CharactersRoot, BossesRoot))
            {
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;

                TakeInfo[] takes = importer.importedTakeInfos;
                var names = new List<string>(takes.Length);
                for (int i = 0; i < takes.Length; i++)
                    names.Add(takes[i].defaultClipName);

                Debug.Log(LogPrefix + "TAKES " + path + " => " +
                          (names.Count > 0 ? string.Join(" | ", names) : "(nenhum)"));
            }
        }

        // -------------------------------------------------------------- Utilidades

        private static IEnumerable<string> FindAssets(string filter, params string[] folders)
        {
            var existing = new List<string>(folders.Length);
            foreach (string folder in folders)
                if (AssetDatabase.IsValidFolder(folder)) existing.Add(folder);
            if (existing.Count == 0) yield break;

            string[] guids = AssetDatabase.FindAssets(filter, existing.ToArray());
            var seen = new HashSet<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (seen.Add(path)) yield return path;
            }
        }

        private static void EnsureMaterialsFolder()
        {
            if (AssetDatabase.IsValidFolder(MaterialsFolder)) return;
            AssetDatabase.CreateFolder("Assets/_Project/Art/Models", "Materials");
        }

        private static string Sanitize(string name)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            return name.Replace(' ', '_');
        }
    }
}
