using System.Collections.Generic;
using UnityEngine;

namespace MutantArmy.Core
{
    /// <summary>
    /// Asset de bootstrap em Resources (doc 12 §2.1: "SOMENTE bootstrap — GameSettings.asset").
    /// Carrega o catálogo das fases para a UI resolver LevelConfigSO em runtime sem cena
    /// referenciar asset de fase diretamente: o botão Jogar (Main) e o "próxima fase"
    /// (ResultScreen, na MESMA cena Game) decidem a fase por highestLevelCleared+1.
    ///
    /// Campanha: 100 fases DESENHADAS (10 mundos × 10 fases). Acima da 100 o catálogo gera
    /// fases PROCEDURAIS infinitas via <see cref="EndlessLevelGenerator"/> (CANON §7) — o
    /// jogo nunca "acaba". <see cref="GetLevel"/> resolve as duas faixas de forma transparente
    /// para os chamadores (Main/ResultScreen/DevScreenshotRig). Gerado/atualizado pelo
    /// MvpContentFactory.
    /// </summary>
    [CreateAssetMenu(menuName = "MutantArmy/Game Settings")]
    public class GameSettingsSO : ScriptableObject
    {
        /// <summary>Nome do asset dentro de Resources/ — único uso de Resources permitido (doc 12 §2.1).</summary>
        public const string ResourcesName = "GameSettings";

        [Tooltip("Catálogo das fases DESENHADAS (ordenado por levelIndex; 1–100 na campanha completa). " +
                 "Acima do maior índice, as fases são geradas proceduralmente (endless infinito).")]
        public LevelConfigSO[] levels;

        [Tooltip("Catálogo de inimigos de pista (missão Nota 10; 4 papéis × 10 mundos, ordem " +
                 "mundo → papel). Repassado ao EndlessLevelGenerator para as fases >100 ganharem " +
                 "inimigos procedurais. Vazio/null = endless sem inimigos (degrada, nunca quebra).")]
        public EnemyConfigSO[] enemies;

        private static GameSettingsSO _cached;
        private static bool _warnedMissing;

        // Gerador procedural lazy: construído na 1ª fase acima do catálogo, a partir dos
        // mundos/bosses REAIS já referenciados pelas fases desenhadas (reuso, não duplicação).
        private EndlessLevelGenerator _endless;

        /// <summary>
        /// Maior levelIndex DESENHADO no catálogo (100 na campanha completa). NÃO é o teto do
        /// jogo: acima dele, <see cref="GetLevel"/> gera fases proceduralmente (endless).
        /// </summary>
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

        /// <summary>
        /// Resolve a fase do índice pedido. <paramref name="levelIndex"/> ≤ catálogo: a fase
        /// desenhada (busca robusta à ordem do array). Acima: uma fase PROCEDURAL determinística
        /// (seed = índice), escalando dificuldade/recompensa sem fim. Retorna null só se o índice
        /// for inválido ou o catálogo estiver vazio.
        /// </summary>
        public LevelConfigSO GetLevel(int levelIndex)
        {
            if (levelIndex < 1 || levels == null) return null;

            for (int i = 0; i < levels.Length; i++)
                if (levels[i] != null && levels[i].levelIndex == levelIndex) return levels[i];

            // Acima do catálogo desenhado: endless procedural (CANON §7).
            if (levelIndex > MaxLevelIndex)
                return EnsureEndless()?.GetOrCreate(levelIndex);

            return null;   // lacuna interna no catálogo (não deveria acontecer com o factory)
        }

        /// <summary>
        /// Próxima fase a partir do resultado da fase atual. Vitória avança +1 SEM teto (o
        /// endless cobre além de 100); derrota repete a mesma fase. Centraliza a regra de
        /// avanço usada pela ResultScreen para o fluxo funcionar além da campanha desenhada.
        /// </summary>
        public int NextLevelAfter(int currentLevelIndex, bool won)
        {
            if (currentLevelIndex < 1) currentLevelIndex = 1;
            return won ? currentLevelIndex + 1 : currentLevelIndex;
        }

        /// <summary>Próxima fase pelo save: highestLevelCleared+1 (mín. 1). Sem teto — endless infinito.</summary>
        public int NextLevelIndex(int highestLevelCleared)
        {
            return Mathf.Max(1, highestLevelCleared + 1);
        }

        private EndlessLevelGenerator EnsureEndless()
        {
            if (_endless != null) return _endless;
            // O catálogo de inimigos entra como 3º parâmetro (missão Nota 10): fases endless
            // ganham grupos procedurais dos mesmos EnemyConfigSO das fases desenhadas.
            _endless = new EndlessLevelGenerator(CollectWorlds(), CollectBosses(), enemies);
            return _endless;
        }

        // Mundos REAIS referenciados pelas fases desenhadas (ordenados por worldIndex, sem
        // duplicatas) — o gerador recicla a atmosfera deles nas fases endless.
        private List<WorldConfigSO> CollectWorlds()
        {
            var result = new List<WorldConfigSO>();
            if (levels == null) return result;
            for (int i = 0; i < levels.Length; i++)
            {
                WorldConfigSO w = levels[i] != null ? levels[i].world : null;
                if (w != null && !result.Contains(w)) result.Add(w);
            }
            result.Sort((a, b) => a.worldIndex.CompareTo(b.worldIndex));
            return result;
        }

        // Bosses de MUNDO reais (worldBoss de cada WorldConfigSO; fallback: bosses das fases) —
        // o gerador recicla os dados de fraqueza/HP. Sem duplicatas.
        private List<BossConfigSO> CollectBosses()
        {
            var result = new List<BossConfigSO>();
            foreach (WorldConfigSO w in CollectWorlds())
                if (w.worldBoss != null && !result.Contains(w.worldBoss)) result.Add(w.worldBoss);

            if (result.Count == 0 && levels != null)   // fallback: nenhum worldBoss ligado
            {
                for (int i = 0; i < levels.Length; i++)
                {
                    BossConfigSO b = levels[i] != null ? levels[i].boss : null;
                    if (b != null && !result.Contains(b)) result.Add(b);
                }
            }
            return result;
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
