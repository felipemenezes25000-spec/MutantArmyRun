using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MutantArmy.Editor
{
    /// <summary>
    /// Guard de compliance (doc 12 §2.3 regra 1 / §9): 'UnityEditor' fora de pasta
    /// Editor/ quebra o build de device. Faz o scan dos fontes de runtime e reporta:
    /// ERRO quando a referência está sem proteção; WARNING quando está dentro de
    /// #if UNITY_EDITOR (compila, mas o lugar certo é o assembly de Editor).
    /// </summary>
    public static class EditorGuards
    {
        private const string ScriptsRoot = "/_Project/Scripts";

        [MenuItem("MAR Tools/Verify Editor Guards")]
        public static void VerifyMenu()
        {
            int violations = Scan();
            if (violations == 0)
                EditorUtility.DisplayDialog("MAR Tools",
                    "Nenhum uso de UnityEditor fora de Editor/.", "OK");
            else
                EditorUtility.DisplayDialog("MAR Tools",
                    violations + " violação(ões) de UnityEditor fora de Editor/ — veja o Console.", "OK");
        }

        /// <summary>Retorna o número de violações (referências NÃO protegidas fora de Editor/).</summary>
        public static int Scan()
        {
            string root = Application.dataPath + ScriptsRoot;
            if (!Directory.Exists(root)) return 0;

            int violations = 0;
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = file.Replace('\\', '/');
                if (normalized.Contains("/Editor/")) continue;
                // Tests/EditMode é assembly Editor-only (includePlatforms: Editor) — permitido.
                if (normalized.Contains("/Tests/EditMode/")) continue;
                violations += ScanFile(file, normalized);
            }
            return violations;
        }

        private static int ScanFile(string path, string displayPath)
        {
            int count = 0;
            // Pilha de #if: true quando o bloco atual é guardado por UNITY_EDITOR.
            var guards = new Stack<bool>();
            string[] lines = File.ReadAllLines(path);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimStart();

                if (line.StartsWith("#if"))
                {
                    guards.Push(line.Contains("UNITY_EDITOR"));
                    continue;
                }
                if (line.StartsWith("#endif"))
                {
                    if (guards.Count > 0) guards.Pop();
                    continue;
                }
                if (!line.Contains("UnityEditor")) continue;

                bool guarded = guards.Contains(true);
                if (guarded)
                {
                    Debug.LogWarning("[EditorGuards] 'UnityEditor' sob #if UNITY_EDITOR em assembly de runtime — " +
                                     "compila, mas prefira mover para Scripts/Editor: " + displayPath + ":" + (i + 1));
                }
                else
                {
                    Debug.LogError("[EditorGuards] 'UnityEditor' fora de Editor/ — quebra o build de device " +
                                   "(doc 12 §2.3): " + displayPath + ":" + (i + 1));
                    count++;
                }
            }
            return count;
        }
    }
}
