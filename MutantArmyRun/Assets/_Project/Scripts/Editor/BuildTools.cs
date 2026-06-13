using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MutantArmy.Editor
{
    /// Builds do player — Windows para teste local (greybox/preview) e Android para
    /// device/loja (missão Nota 10, prontidão comercial).
    /// Batchmode: -batchmode -executeMethod MutantArmy.Editor.BuildTools.BuildWindows
    ///            -batchmode -executeMethod MutantArmy.Editor.BuildTools.BuildAndroidAab
    ///            -batchmode -executeMethod MutantArmy.Editor.BuildTools.BuildAndroidApkDev
    /// Android REQUER o módulo "Android Build Support" instalado na 6000.4.8f1 — sem ele
    /// os métodos logam erro claro e saem com exit code 1 (nunca falham em silêncio).
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

        /// AAB de loja (Play Store exige App Bundle). Sem keystore configurada o Unity assina
        /// com a DEBUG keystore — serve para teste em device, NÃO para upload (warning no log;
        /// keystore é pendência documentada: nunca inventamos credencial).
        [MenuItem("MAR Tools/Build Android (AAB)")]
        public static void BuildAndroidAab()
        {
            BuildAndroid(appBundle: true, development: false);
        }

        /// APK de desenvolvimento (Development Build + debugging): instala direto via adb
        /// para profiling/QA em device — caminho do DevPerfOverlay e do Profiler remoto.
        [MenuItem("MAR Tools/Build Android (APK Dev)")]
        public static void BuildAndroidApkDev()
        {
            BuildAndroid(appBundle: false, development: true);
        }

        // Núcleo compartilhado dos builds Android — espelha o BuildWindows: cenas habilitadas
        // do Build Settings, saída em Build/Android, exit code 1 em falha (batchmode).
        // NÃO troca o target ativo do projeto via SwitchActiveBuildTarget: o BuildPlayerOptions
        // com target Android deixa o próprio pipeline resolver a troca durante o build
        // (fluxo recomendado para batch; em editor interativo evita deixar o projeto
        // re-importado em Android sem o dev pedir).
        private static void BuildAndroid(bool appBundle, bool development)
        {
            // Guarda explícita: sem o módulo Android instalado, BuildPlayer falharia com
            // mensagem genérica — aqui o diagnóstico é direto e o batch sai com código 1.
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[BuildTools] Módulo 'Android Build Support' não está instalado nesta " +
                               "instalação do Unity (6000.4.8f1). Instale via Unity Hub (Android SDK/NDK + " +
                               "OpenJDK) e rode o build de novo.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[BuildTools] Nenhuma cena no Build Settings — rode MAR Tools/Setup Project antes.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            // Keystore: regra do contrato — NUNCA criar/inventar credencial por código. Sem
            // keystore custom o Unity usa debug signing: ok para device de teste, INVÁLIDO
            // para a Play Store (pendência de loja, documentada aqui no log).
            if (!PlayerSettings.Android.useCustomKeystore ||
                string.IsNullOrEmpty(PlayerSettings.Android.keystoreName))
            {
                Debug.LogWarning("[BuildTools] AVISO: keystore de release NÃO configurada — o build sai " +
                                 "com DEBUG SIGNING. Serve para teste em device; upload na Play Store " +
                                 "exige keystore própria (Player Settings > Publishing Settings). " +
                                 "Pendência de loja documentada.");
            }

            string outDir = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "..", "Build", "Android");
            Directory.CreateDirectory(outDir);
            string fileName = appBundle ? "MutantArmyRun.aab" : "MutantArmyRun_dev.apk";
            string outPath = Path.GetFullPath(Path.Combine(outDir, fileName));

            // buildAppBundle é estado GLOBAL do editor: restaura no finally para o toggle
            // AAB/APK de um build não vazar para o próximo (ou para o dev no editor aberto).
            bool previousAppBundle = EditorUserBuildSettings.buildAppBundle;
            EditorUserBuildSettings.buildAppBundle = appBundle;

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = development
                        ? BuildOptions.Development | BuildOptions.AllowDebugging
                        : BuildOptions.None,
                });
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = previousAppBundle;
            }

            if (report == null || report.summary.result != BuildResult.Succeeded)
            {
                int errors = report != null ? report.summary.totalErrors : -1;
                Debug.LogError($"[BuildTools] Build Android FALHOU: " +
                               $"{(report != null ? report.summary.result.ToString() : "sem report")} — {errors} erro(s).");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Debug.Log($"[BuildTools] Build Android OK: {outPath} " +
                      $"({report.summary.totalSize / (1024 * 1024)} MB, " +
                      $"{(appBundle ? "AAB release-signing-pendente" : "APK development")}).");
        }
    }
}
