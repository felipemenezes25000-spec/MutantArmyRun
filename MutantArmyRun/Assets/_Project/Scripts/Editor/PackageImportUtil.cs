using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MutantArmy.Editor
{
    /// <summary>
    /// Import de .unitypackage confiável em pipelines batchmode. AssetDatabase.ImportPackage
    /// é assíncrono: com -quit o editor encerra antes do import acontecer e o pacote nunca
    /// chega ao disco (observado no Unity 6000.4 — três execuções logaram sucesso sem criar
    /// Assets/TextMesh Pro). A API interna ImportPackageImmediately importa na hora; se ela
    /// sumir em versões futuras, cai no ImportPackage assíncrono (suficiente no editor
    /// interativo, onde há frames para o import concluir).
    /// </summary>
    internal static class PackageImportUtil
    {
        internal static void ImportSync(string packagePath)
        {
            MethodInfo immediate = typeof(AssetDatabase).GetMethod(
                "ImportPackageImmediately",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null, new[] { typeof(string) }, null);
            if (immediate != null)
            {
                try
                {
                    immediate.Invoke(null, new object[] { packagePath });
                    AssetDatabase.Refresh();
                    return;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("MAR Tools: ImportPackageImmediately falhou (" + e.Message +
                                     ") — usando o import assíncrono.");
                }
            }
            AssetDatabase.ImportPackage(packagePath, false);
            AssetDatabase.Refresh();
        }
    }
}
