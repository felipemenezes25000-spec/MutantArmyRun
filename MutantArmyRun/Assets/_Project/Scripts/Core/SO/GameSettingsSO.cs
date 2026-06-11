using UnityEngine;

namespace MutantArmy.Core
{
    /// <summary>
    /// Asset de bootstrap em Resources (doc 12 §2.1: "SOMENTE bootstrap — GameSettings.asset").
    /// Carrega o catálogo das fases para a UI resolver LevelConfigSO em runtime sem cena
    /// referenciar asset de fase diretamente: o botão Jogar (Main) e o "próxima fase"
    /// (ResultScreen, na MESMA cena Game) decidem a fase por highestLevelCleared+1 com cap
    /// no tamanho do catálogo (20 no MVP). Gerado/atualizado pelo MvpContentFactory.
    /// </summary>
    [CreateAssetMenu(menuName = "MutantArmy/Game Settings")]
    public class GameSettingsSO : ScriptableObject
    {
        /// <summary>Nome do asset dentro de Resources/ — único uso de Resources permitido (doc 12 §2.1).</summary>
        public const string ResourcesName = "GameSettings";

        [Tooltip("Catálogo das fases (ordenado por levelIndex; 1–20 no MVP)")]
        public LevelConfigSO[] levels;

        private static GameSettingsSO _cached;
        private static bool _warnedMissing;

        /// <summary>Maior levelIndex do catálogo — é o cap do "próxima fase" (20 no MVP).</summary>
        public int MaxLevelIndex
        {
            get
            {
                int max = 0;
                if (levels != null)
                {
                    for (int i = 0; i < levels.Length; i++)
                        if (levels[i] != null && levels[i].levelIndex > max) max = levels[i].levelIndex;
                }
                return max;
            }
        }

        /// <summary>Busca por levelIndex (robusta à ordem do array). Retorna null se ausente.</summary>
        public LevelConfigSO GetLevel(int levelIndex)
        {
            if (levels == null) return null;
            for (int i = 0; i < levels.Length; i++)
                if (levels[i] != null && levels[i].levelIndex == levelIndex) return levels[i];
            return null;
        }

        /// <summary>Próxima fase pelo save: highestLevelCleared+1, clamp [1, MaxLevelIndex].</summary>
        public int NextLevelIndex(int highestLevelCleared)
        {
            return Mathf.Clamp(highestLevelCleared + 1, 1, Mathf.Max(1, MaxLevelIndex));
        }

        /// <summary>Load cacheado de Resources/GameSettings.asset. Ausente loga erro 1×.</summary>
        public static GameSettingsSO Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<GameSettingsSO>(ResourcesName);
            if (_cached == null && !_warnedMissing)
            {
                _warnedMissing = true;
                Debug.LogError("[GameSettingsSO] Resources/GameSettings.asset ausente — rode " +
                               "MAR Tools/Create MVP Content para gerar o catálogo de fases.");
            }
            return _cached;
        }
    }
}
