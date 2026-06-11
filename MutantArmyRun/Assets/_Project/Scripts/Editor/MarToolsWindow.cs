using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MutantArmy.Editor
{
    /// <summary>
    /// Janela "MAR Tools" (doc 12 §8): limpar save + PlayerPrefs em 1 clique, abrir o
    /// persistentDataPath no Explorer e simular Remote Config localmente (overrides dos
    /// defaults do doc 12 §4.10 sem rede). Toda operação destrutiva pede confirmação.
    /// </summary>
    public class MarToolsWindow : EditorWindow
    {
        // Convenção dos overrides locais de RC: o provider de Remote Config consulta
        // PlayerPrefs com este prefixo ANTES dos defaults em builds de desenvolvimento.
        private const string OverrideKeysPref = "MAR_RC_OVERRIDE_KEYS";
        private const string OverridePrefix = "MAR_RC_OVERRIDE_";

        private string _newKey = string.Empty;
        private string _newValue = string.Empty;
        private Vector2 _scroll;

        [MenuItem("MAR Tools/MAR Tools Window")]
        public static void Open()
        {
            GetWindow<MarToolsWindow>("MAR Tools");
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawSaveSection();
            EditorGUILayout.Space(12f);
            DrawRemoteConfigSection();
            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------ save / prefs

        private void DrawSaveSection()
        {
            GUILayout.Label("Save / PlayerPrefs", EditorStyles.boldLabel);

            if (GUILayout.Button("Limpar save (save.json / save.bak / .tmp)"))
            {
                if (EditorUtility.DisplayDialog("MAR Tools",
                        "Apagar o save local? Operação irreversível.", "Apagar", "Cancelar"))
                {
                    int removed = DeleteSaveFiles();
                    Debug.Log("MAR Tools: " + removed + " arquivo(s) de save removido(s).");
                }
            }

            if (GUILayout.Button("Limpar PlayerPrefs"))
            {
                if (EditorUtility.DisplayDialog("MAR Tools",
                        "Apagar TODOS os PlayerPrefs (inclui overrides de RC)?", "Apagar", "Cancelar"))
                {
                    PlayerPrefs.DeleteAll();
                    PlayerPrefs.Save();
                    Debug.Log("MAR Tools: PlayerPrefs limpos.");
                }
            }

            if (GUILayout.Button("Abrir persistentDataPath"))
            {
                EditorUtility.RevealInFinder(Application.persistentDataPath);
            }

            EditorGUILayout.LabelField("persistentDataPath:", EditorStyles.miniLabel);
            EditorGUILayout.SelectableLabel(Application.persistentDataPath, EditorStyles.miniLabel,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }

        private static int DeleteSaveFiles()
        {
            // Nomes canônicos do SaveSystem (doc 12 §4.7): principal, backup e tmp da
            // gravação atômica.
            string[] names = { "save.json", "save.bak", "save.json.tmp" };
            int removed = 0;
            foreach (string name in names)
            {
                string path = Path.Combine(Application.persistentDataPath, name);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    removed++;
                }
            }
            return removed;
        }

        // ------------------------------------------------------------------ overrides de RC

        private void DrawRemoteConfigSection()
        {
            GUILayout.Label("Remote Config — overrides locais (DEV)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Overrides gravados em PlayerPrefs com o prefixo " + OverridePrefix +
                ". O provider de Remote Config consulta esse prefixo antes dos defaults em " +
                "builds de desenvolvimento — simula RC sem rede. Overrides escrevem em " +
                "estado de runtime, nunca nos ScriptableObjects (doc 12 §5.1).",
                MessageType.Info);

            List<string> keys = GetOverrideKeys();
            foreach (string key in keys)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(key, GUILayout.Width(220f));
                EditorGUILayout.LabelField(PlayerPrefs.GetString(OverridePrefix + key, string.Empty));
                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    RemoveOverride(key);
                    EditorGUILayout.EndHorizontal();
                    GUIUtility.ExitGUI();   // a lista mudou no meio do layout — encerra o frame de GUI
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            _newKey = EditorGUILayout.TextField(_newKey, GUILayout.Width(220f));
            _newValue = EditorGUILayout.TextField(_newValue);
            if (GUILayout.Button("Adicionar/Atualizar", GUILayout.Width(140f)))
            {
                if (!string.IsNullOrEmpty(_newKey))
                {
                    SetOverride(_newKey.Trim(), _newValue);
                    _newKey = string.Empty;
                    _newValue = string.Empty;
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (keys.Count > 0 && GUILayout.Button("Remover todos os overrides"))
            {
                if (EditorUtility.DisplayDialog("MAR Tools",
                        "Remover todos os overrides locais de Remote Config?", "Remover", "Cancelar"))
                {
                    foreach (string key in keys) RemoveOverride(key);
                }
            }
        }

        public static List<string> GetOverrideKeys()
        {
            var result = new List<string>();
            string raw = PlayerPrefs.GetString(OverrideKeysPref, string.Empty);
            if (string.IsNullOrEmpty(raw)) return result;
            foreach (string key in raw.Split(';'))
            {
                if (!string.IsNullOrEmpty(key)) result.Add(key);
            }
            return result;
        }

        public static void SetOverride(string key, string value)
        {
            List<string> keys = GetOverrideKeys();
            if (!keys.Contains(key))
            {
                keys.Add(key);
                PlayerPrefs.SetString(OverrideKeysPref, string.Join(";", keys));
            }
            PlayerPrefs.SetString(OverridePrefix + key, value);
            PlayerPrefs.Save();
        }

        public static void RemoveOverride(string key)
        {
            List<string> keys = GetOverrideKeys();
            if (keys.Remove(key))
                PlayerPrefs.SetString(OverrideKeysPref, string.Join(";", keys));
            PlayerPrefs.DeleteKey(OverridePrefix + key);
            PlayerPrefs.Save();
        }
    }
}
