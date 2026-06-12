using System.Collections.Generic;
using UnityEngine;

namespace MutantArmy.Core
{
    /// <summary>
    /// Gerador PROCEDURAL de fases "infinitas" (CANON §7 estende a campanha de 100 fases
    /// desenhadas para um endless sem fim). Para qualquer índice acima do catálogo desenhado
    /// (n &gt; <see cref="DesignedLevelCount"/>), produz um <see cref="LevelConfigSO"/> em
    /// memória — nunca um asset no disco — escalando dificuldade e recompensa indefinidamente.
    ///
    /// Mora em Core (não em Meta) por uma razão de arquitetura: quem o consome é o
    /// <see cref="GameSettingsSO"/> (Core), e Core só enxerga Domain (doc 12 §2.3). O gerador
    /// só toca tipos de Core (Level/World/Boss ConfigSO), então cabe aqui sem furar a fronteira.
    ///
    /// Determinismo: a fase n é SEMPRE idêntica (seed = n; mesmos slots autoBalance; mesmo
    /// boss/mundo reciclados) — QA reproduz por índice, igual às fases desenhadas (doc 12 §4.11).
    /// Reuso: o gerador NÃO cria mundos nem bosses; recicla os <see cref="WorldConfigSO"/> e
    /// <see cref="BossConfigSO"/> reais do catálogo desenhado (passados pelo
    /// <see cref="GameSettingsSO"/>), então a atmosfera por mundo e os dados de boss continuam
    /// válidos (WorldAtmosphereApplier, Boss Scout, fraquezas elementais). A partir da fase 101
    /// o ciclo fixa a Dimensão Final (último mundo) — o "endgame" canônico — com HP e recompensa
    /// crescentes a cada volta.
    ///
    /// Cache: instâncias geradas são cacheadas por índice (HideFlags.HideAndDontSave) — reentrar
    /// na mesma fase devolve o MESMO objeto, sem vazar assets nem acumular lixo entre sessões.
    /// </summary>
    public sealed class EndlessLevelGenerator
    {
        /// <summary>Quantidade de fases DESENHADAS à mão (10 mundos × 10 fases). Acima disso é procedural.</summary>
        public const int DesignedLevelCount = 100;

        private const int FinalWorldIndex = 10;        // Dimensão Final — o mundo do endgame infinito
        private const string FinalBossId = "m10_dimensional_entity";   // bossId estável do boss de M10
        private const float EndlessTrackLength = 260f; // pista longa "padrão" do endless (doc 06 §8)

        // Fonte de mundos/bosses reais (catálogo desenhado): o gerador só recicla, nunca cria.
        private readonly IReadOnlyList<WorldConfigSO> _worlds;
        private readonly IReadOnlyList<BossConfigSO> _bosses;
        private readonly WorldConfigSO _fallbackWorld;
        private readonly BossConfigSO _fallbackBoss;

        // Cache de instâncias por índice — fase n reentrada devolve o MESMO LevelConfigSO.
        private readonly Dictionary<int, LevelConfigSO> _cache = new Dictionary<int, LevelConfigSO>();

        /// <param name="worlds">Mundos reais do catálogo (worldIndex 1..10), para reciclar atmosfera.</param>
        /// <param name="bosses">Bosses de mundo reais (10), para reciclar dados de fraqueza/HP.</param>
        public EndlessLevelGenerator(IReadOnlyList<WorldConfigSO> worlds, IReadOnlyList<BossConfigSO> bosses)
        {
            _worlds = worlds;
            _bosses = bosses;
            _fallbackWorld = PickWorld(FinalWorldIndex);
            _fallbackBoss = PickBoss();
        }

        /// <summary>
        /// Fase procedural para o índice <paramref name="levelIndex"/> (espera-se &gt; 100).
        /// Cacheada e determinística. Retorna null só se o catálogo não tiver mundo/boss algum
        /// para reciclar (projeto sem conteúdo — já avisado pelo chamador).
        /// </summary>
        public LevelConfigSO GetOrCreate(int levelIndex)
        {
            if (levelIndex <= DesignedLevelCount) return null;   // abaixo do cap é responsabilidade do catálogo

            if (_cache.TryGetValue(levelIndex, out LevelConfigSO cached) && cached != null)
                return cached;

            LevelConfigSO level = Build(levelIndex);
            if (level != null) _cache[levelIndex] = level;
            return level;
        }

        private LevelConfigSO Build(int levelIndex)
        {
            WorldConfigSO world = _fallbackWorld;
            BossConfigSO boss = _fallbackBoss;
            if (world == null || boss == null) return null;   // projeto sem conteúdo: nada a reciclar

            var level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.hideFlags = HideFlags.HideAndDontSave;   // runtime puro — nunca persiste no disco
            level.name = "Level_Endless_" + levelIndex;

            level.levelIndex = levelIndex;
            level.seed = levelIndex;                       // determinismo por índice (doc 12 §4.11)
            level.world = world;
            level.boss = boss;
            level.trackLength = EndlessTrackLength;
            level.startingUnits = 1;
            level.obstacles = new ObstacleSlot[0];
            level.winReward = null;                        // recompensa por curva (EconomySystem) — sem baú fixo

            // Escala ALÉM da fase 100: cada "volta" do endless (10 fases) sobe o HP do boss.
            // base 1.0 na fase 101, +25% por volta e +10% por passo dentro da volta — cresce
            // sem teto, mantendo a corrida tensa indefinidamente.
            int beyond = levelIndex - DesignedLevelCount - 1;   // 0 na fase 101
            int lapsBeyond = beyond / 10;                       // 0 nas 101–110, 1 nas 111–120, ...
            int stepInLap = beyond % 10;                        // 0..9 dentro da volta
            level.bossHpMultiplier = (1f + 0.25f * lapsBeyond) * (1f + 0.10f * stepInLap);

            // Slots 100% autoBalance: o GateManager monta os pares contra o boss reciclado
            // com a MESMA seed da fase — rota ótima + armadilha geradas, infinitamente.
            level.gateSlots = BuildAutoSlots(5, EndlessTrackLength);
            return level;
        }

        private static GateSlot[] BuildAutoSlots(int count, float trackLength)
        {
            var slots = new GateSlot[count];
            float first = 30f;
            float last = trackLength - 40f;                // zona de segurança antes da arena (doc 12 §4.11)
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 0f;
                float position = Mathf.Lerp(first, last, t);
                slots[i] = new GateSlot
                {
                    trackPosition = position,
                    depth01 = trackLength > 0f ? position / trackLength : 0f,
                    autoBalance = true
                };
            }
            return slots;
        }

        private WorldConfigSO PickWorld(int worldIndex)
        {
            if (_worlds != null)
            {
                for (int i = 0; i < _worlds.Count; i++)
                    if (_worlds[i] != null && _worlds[i].worldIndex == worldIndex) return _worlds[i];
                for (int i = _worlds.Count - 1; i >= 0; i--)
                    if (_worlds[i] != null) return _worlds[i];   // último mundo serve de fallback
            }
            return null;
        }

        private BossConfigSO PickBoss()
        {
            // Prefere o boss da Dimensão Final (bossId estável); senão o último disponível.
            if (_bosses != null)
            {
                for (int i = 0; i < _bosses.Count; i++)
                    if (_bosses[i] != null && _bosses[i].bossId == FinalBossId) return _bosses[i];
                for (int i = _bosses.Count - 1; i >= 0; i--)
                    if (_bosses[i] != null) return _bosses[i];
            }
            return null;
        }
    }
}
