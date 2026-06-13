using System.Collections.Generic;
using MutantArmy.Domain;
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
        private readonly IReadOnlyList<EnemyConfigSO> _enemies;   // catálogo de inimigos de pista (missão Nota 10)
        private readonly WorldConfigSO _fallbackWorld;
        private readonly BossConfigSO _fallbackBoss;

        // Cache de instâncias por índice — fase n reentrada devolve o MESMO LevelConfigSO.
        private readonly Dictionary<int, LevelConfigSO> _cache = new Dictionary<int, LevelConfigSO>();

        /// <param name="worlds">Mundos reais do catálogo (worldIndex 1..10), para reciclar atmosfera.</param>
        /// <param name="bosses">Bosses de mundo reais (10), para reciclar dados de fraqueza/HP.</param>
        /// <param name="enemies">Catálogo de EnemyConfigSO para popular inimigos de pista nas fases
        /// endless (missão Nota 10). Opcional: null/vazio ⇒ fases sem inimigos (comportamento
        /// anterior preservado) — o wiring do catálogo real entra pelo GameSettingsSO (Onda 4).</param>
        public EndlessLevelGenerator(IReadOnlyList<WorldConfigSO> worlds, IReadOnlyList<BossConfigSO> bosses,
                                     IReadOnlyList<EnemyConfigSO> enemies = null)
        {
            _worlds = worlds;
            _bosses = bosses;
            _enemies = enemies;
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

            // Inimigos de pista procedurais (missão Nota 10): 2–4 grupos por fase, kinds
            // liberados pela profundidade do endless. Sem catálogo ⇒ array vazio (idem antes).
            level.enemies = BuildEnemySlots(levelIndex, lapsBeyond, level.gateSlots);
            return level;
        }

        // ------------------------------------------------------------- inimigos procedurais

        private const float EnemyGateSafetyMeters = 8f;   // espelho da zona pós-portal do LevelManager

        /// <summary>
        /// Gera 2–4 EnemySlot determinísticos para a fase endless. RNG DERIVADO do índice
        /// (primo próprio, 48271·n+11): roda na GERAÇÃO, fora da cadeia gates → obstáculos →
        /// segmentos do LevelManager (CONTRACT §1.6) — e distinto do RNG de spawn do
        /// TrackEnemyManager (seed*92821+7) para layout e lane não ficarem correlacionados.
        /// </summary>
        private EnemySlot[] BuildEnemySlots(int levelIndex, int lapsBeyond, GateSlot[] gates)
        {
            if (_enemies == null || _enemies.Count == 0) return new EnemySlot[0];

            var rng = new System.Random(levelIndex * 48271 + 11);
            int slotCount = 2 + rng.Next(3);   // 2..4 grupos por fase
            var result = new List<EnemySlot>(slotCount);

            // janela útil da pista: depois da largada, antes da zona de aproximação da arena
            float first = 45f;
            float last = EndlessTrackLength - 50f;
            for (int i = 0; i < slotCount; i++)
            {
                float t = slotCount > 1 ? i / (float)(slotCount - 1) : 0.5f;
                float z = Mathf.Lerp(first, last, t) + (float)(rng.NextDouble() * 2.0 - 1.0) * 8f;
                z = PushOutOfGateSafetyZone(z, gates);

                TrackEnemyKind kind = PickKind(rng, lapsBeyond);
                int count = CountFor(kind, rng, lapsBeyond);
                EnemyConfigSO enemy = PickEnemy(kind, rng);
                if (enemy == null) continue;   // catálogo sem este kind: pula o grupo, fase segue válida

                result.Add(new EnemySlot { trackPosition = z, enemy = enemy, count = count });
            }
            return result.ToArray();
        }

        // Zona de segurança pós-portal (doc 12 §4.11): inimigo colado na saída do gate pune a
        // escolha certa — empurra o grupo para depois da janela (espaçamento dos gates ≫ 9 m).
        private static float PushOutOfGateSafetyZone(float z, GateSlot[] gates)
        {
            if (gates == null) return z;
            for (int i = 0; i < gates.Length; i++)
            {
                GateSlot g = gates[i];
                if (g == null) continue;
                if (z > g.trackPosition && z <= g.trackPosition + EnemyGateSafetyMeters)
                    return g.trackPosition + EnemyGateSafetyMeters + 1f;
            }
            return z;
        }

        // Profundidade libera kinds (variedade crescente, surpresa justa): voltas iniciais só
        // horda/tanque; 2ª volta adiciona atirador; da 3ª em diante o curador entra no sorteio.
        // Horda tem peso dobrado — é o "pão" da corrida; os outros são tempero.
        private static TrackEnemyKind PickKind(System.Random rng, int lapsBeyond)
        {
            int poolSize = lapsBeyond <= 0 ? 3 : lapsBeyond == 1 ? 4 : 5;
            int roll = rng.Next(poolSize);
            switch (roll)
            {
                case 2: return TrackEnemyKind.Tank;
                case 3: return TrackEnemyKind.Ranged;
                case 4: return TrackEnemyKind.Healer;
                default: return TrackEnemyKind.WeakHorde;   // 0 e 1: peso dobrado
            }
        }

        // Tamanho do grupo por papel: horda numerosa (cresce com a profundidade, teto 12),
        // tanque/curador raros, atirador em trio aproximado.
        private static int CountFor(TrackEnemyKind kind, System.Random rng, int lapsBeyond)
        {
            switch (kind)
            {
                case TrackEnemyKind.Tank: return 1 + rng.Next(2);                       // 1..2
                case TrackEnemyKind.Ranged: return 2 + rng.Next(3);                     // 2..4
                case TrackEnemyKind.Healer: return 1 + rng.Next(2);                     // 1..2
                default: return Mathf.Min(12, 6 + rng.Next(5) + lapsBeyond);            // 6..10 + profundidade
            }
        }

        // Resolve o EnemyConfigSO do kind no catálogo: prefere os do mundo final (o endless É
        // a Dimensão Final), senão qualquer um do kind; null se o catálogo não cobre o kind.
        private EnemyConfigSO PickEnemy(TrackEnemyKind kind, System.Random rng)
        {
            List<EnemyConfigSO> candidates = null;
            List<EnemyConfigSO> preferred = null;
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyConfigSO e = _enemies[i];
                if (e == null || e.kind != kind) continue;
                (candidates ?? (candidates = new List<EnemyConfigSO>())).Add(e);
                if (e.worldIndex == FinalWorldIndex)
                    (preferred ?? (preferred = new List<EnemyConfigSO>())).Add(e);
            }
            List<EnemyConfigSO> pool = preferred ?? candidates;
            if (pool == null) return null;
            return pool[rng.Next(pool.Count)];
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
