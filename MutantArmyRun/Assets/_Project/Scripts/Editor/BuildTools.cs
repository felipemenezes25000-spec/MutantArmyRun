using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MutantArmy.Editor
{
    /// Build do player Windows para teste local (greybox/preview).
    /// Executável via: -batchmode -executeMethod MutantArmy.Editor.BuildTools.BuildWindows
    public static class BuildTools
    {
        [MenuItem("MAR Tools/Build Windows Player")]
        public static void BuildWindows()
        {
            // Janela 540x960: preview de retrato 9:16 que cabe em monitor desktop.
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.runInBackground = true;

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[BuildTools] Nenhuma cena no Build Settings — rode MAR Tools/Setup Project antes.");
                EditorApplication.Exit(1);
                return;
            }

            string outDir = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "..", "Build", "Win");
            Directory.CreateDirectory(outDir);
            string exePath = Path.GetFullPath(Path.Combine(outDir, "MutantArmyRun.exe"));

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[BuildTools] Build FALHOU: {report.summary.result} — {report.summary.totalErrors} erro(s).");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Debug.Log($"[BuildTools] Build OK: {exePath} ({report.summary.totalSize / (1024 * 1024)} MB)");
        }
    }
}
