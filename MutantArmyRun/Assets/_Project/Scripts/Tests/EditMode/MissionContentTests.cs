using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MutantArmy.Core;
using MutantArmy.Domain;
using MutantArmy.Gameplay;

namespace MutantArmy.Tests
{
    /// <summary>
    /// Conteúdo da missão Nota 10 (Onda 5): EnemyConfigSO/EnemySlot em memória (padrão
    /// GateConfigTests — CreateInstance + DestroyImmediate), resolução do BossBehaviorRegistry
    /// e as REGRAS DE TUTORIAL das fases 1–5 sobre os assets REAIS do MvpContentFactory.
    /// Os asserts de SO espelham as regras do Editor/SoValidator (o validador é Editor-only;
    /// duplicar a regra aqui protege contra regressão SEM referenciar MutantArmy.Editor).
    /// Assets ausentes (pipeline ainda não rodou) = Assert.Ignore, nunca Fail — mesmo
    /// contrato dos testes PlayMode com LoadAsset.
    /// </summary>
    public class MissionContentTests
    {
        private const string SoRoot = "Assets/_Project/ScriptableObjects";

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object instance in _created)
            {
                if (instance != null) Object.DestroyImmediate(instance);
            }
            _created.Clear();
        }

        // ------------------------------------------------------------------ helpers

        private EnemyConfigSO MakeEnemy(string enemyId, TrackEnemyKind kind, float maxHp, float dps,
                                        int rewardCoins, int worldIndex,
                                        float attackRange = 2f, float healPerSecond = 0f)
        {
            var enemy = ScriptableObject.CreateInstance<EnemyConfigSO>();
            enemy.enemyId = enemyId;
            enemy.kind = kind;
            enemy.maxHp = maxHp;
            enemy.dps = dps;
            enemy.rewardCoins = rewardCoins;
            enemy.worldIndex = worldIndex;
            enemy.attackRange = attackRange;
            enemy.healPerSecond = healPerSecond;
            _created.Add(enemy);
            return enemy;
        }

        /// <summary>
        /// Espelho das regras de EnemyConfigSO do SoValidator.Validate (Editor/SoValidator.cs):
        /// enemyId obrigatório, maxHp &gt; 0, dps ≥ 0, rewardCoins ≥ 0, worldIndex 1..10,
        /// Healer cura de verdade (healPerSecond &gt; 0), Ranged ataca ANTES do contato
        /// (attackRange &gt; 6 — CONTRACT §2). Divergência aqui = atualizar os DOIS lugares.
        /// </summary>
        private static List<string> ViolacoesDoValidador(EnemyConfigSO enemy)
        {
            var violations = new List<string>();
            if (string.IsNullOrEmpty(enemy.enemyId)) violations.Add("sem enemyId");
            if (enemy.maxHp <= 0f) violations.Add("maxHp <= 0");
            if (enemy.dps < 0f) violations.Add("dps negativo");
            if (enemy.rewardCoins < 0) violations.Add("rewardCoins negativo");
            if (enemy.worldIndex < 1 || enemy.worldIndex > 10) violations.Add("worldIndex fora de 1..10");
            if (enemy.kind == TrackEnemyKind.Healer && enemy.healPerSecond <= 0f)
                violations.Add("Healer que não cura");
            if (enemy.kind == TrackEnemyKind.Ranged && enemy.attackRange <= 6f)
                violations.Add("Ranged com attackRange <= 6");
            return violations;
        }

        private static void AssertSemViolacoes(EnemyConfigSO enemy)
        {
            List<string> violations = ViolacoesDoValidador(enemy);
            Assert.IsEmpty(violations,
                "EnemyConfigSO '" + enemy.name + "' (" + enemy.enemyId + ") viola regras do SoValidator: " +
                string.Join("; ", violations));
        }

        // Assets reais: pipeline (MAR Tools/Create MVP Content) ainda não rodou ⇒ Ignore, não Fail.
        private static T LoadAssetOrIgnore<T>(string path) where T : Object
        {
            T asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                Assert.Ignore("Asset ainda não gerado pela pipeline: " + path +
                              " — rodar MAR Tools/Create MVP Content e re-executar.");
            return asset;
        }

        // ================================================================== EnemyConfigSO em memória

        [Test]
        public void EnemyConfig_HordaValida_PassaNosCriteriosDoValidador()
        {
            // Baseline do mundo 1 (MvpContentFactory.CreateEnemies): horda HP6/DPS1, 1 moeda.
            EnemyConfigSO horde = MakeEnemy("m1_horde", TrackEnemyKind.WeakHorde,
                maxHp: 6f, dps: 1f, rewardCoins: 1, worldIndex: 1);
            AssertSemViolacoes(horde);
        }

        [Test]
        public void EnemyConfig_RangedComAlcanceLongo_PassaNosCriteriosDoValidador()
        {
            // CONTRACT §2: Ranged usa attackRange > 6 (ataca ANTES do contato).
            EnemyConfigSO ranged = MakeEnemy("m1_ranged", TrackEnemyKind.Ranged,
                maxHp: 15f, dps: 3f, rewardCoins: 3, worldIndex: 1, attackRange: 14f);
            AssertSemViolacoes(ranged);
        }

        [Test]
        public void EnemyConfig_HealerQueCura_PassaNosCriteriosDoValidador()
        {
            // Suporte puro: dps 0 é válido; healPerSecond > 0 é obrigatório para o papel.
            EnemyConfigSO healer = MakeEnemy("m1_healer", TrackEnemyKind.Healer,
                maxHp: 6f, dps: 0f, rewardCoins: 4, worldIndex: 1, healPerSecond: 4f);
            AssertSemViolacoes(healer);
        }

        [Test]
        public void EnemyConfig_HealerSemCura_ViolaOValidador()
        {
            // Caso negativo obrigatório (padrão GateConfigTests): curador que não cura é
            // conteúdo quebrado — o SoValidator trata como ERRO, este espelho também.
            EnemyConfigSO healer = MakeEnemy("m1_healer_quebrado", TrackEnemyKind.Healer,
                maxHp: 6f, dps: 0f, rewardCoins: 4, worldIndex: 1, healPerSecond: 0f);
            Assert.IsNotEmpty(ViolacoesDoValidador(healer),
                "Healer com healPerSecond <= 0 deveria violar as regras do validador.");
        }

        [Test]
        public void EnemyConfig_RangedDeContato_ViolaOValidador()
        {
            EnemyConfigSO ranged = MakeEnemy("m1_ranged_quebrado", TrackEnemyKind.Ranged,
                maxHp: 15f, dps: 3f, rewardCoins: 3, worldIndex: 1, attackRange: 2f);
            Assert.IsNotEmpty(ViolacoesDoValidador(ranged),
                "Ranged com attackRange <= 6 deveria violar as regras do validador (CONTRACT §2).");
        }

        [Test]
        public void EnemyConfig_SemVida_ViolaOValidador()
        {
            EnemyConfigSO morto = MakeEnemy("m1_sem_vida", TrackEnemyKind.WeakHorde,
                maxHp: 0f, dps: 1f, rewardCoins: 1, worldIndex: 1);
            Assert.IsNotEmpty(ViolacoesDoValidador(morto),
                "maxHp <= 0 deveria violar as regras do validador.");
        }

        // ================================================================== EnemySlot round-trip

        [Test]
        public void EnemySlot_RoundTripEmMemoria_PreservaConfigECount()
        {
            // Mesmo funil de clone usado pelos testes PlayMode de derrota (Object.Instantiate
            // de LevelConfigSO): o serializador REAL do Unity copia o EnemySlot[] — referência
            // ao EnemyConfigSO preservada, count/trackPosition por valor.
            EnemyConfigSO horde = MakeEnemy("m1_horde", TrackEnemyKind.WeakHorde,
                maxHp: 6f, dps: 1f, rewardCoins: 1, worldIndex: 1);

            var level = ScriptableObject.CreateInstance<LevelConfigSO>();
            _created.Add(level);
            level.levelIndex = 4;
            level.trackLength = 220f;
            level.enemies = new[]
            {
                new EnemySlot { trackPosition = 60f, enemy = horde, count = 6 }
            };

            LevelConfigSO clone = Object.Instantiate(level);
            _created.Add(clone);

            Assert.IsNotNull(clone.enemies, "Clone perdeu o array de EnemySlot.");
            Assert.AreEqual(1, clone.enemies.Length);
            Assert.AreSame(horde, clone.enemies[0].enemy,
                "Referência ao EnemyConfigSO deveria sobreviver ao round-trip.");
            Assert.AreEqual(6, clone.enemies[0].count);
            Assert.GreaterOrEqual(clone.enemies[0].count, 1, "Slot válido tem count >= 1 (SoValidator).");
            Assert.AreEqual(60f, clone.enemies[0].trackPosition);
        }

        [Test]
        public void EnemySlot_Default_NasceComCountValido()
        {
            // O default serializado (count = 3) respeita a regra count >= 1 do SoValidator —
            // slot recém-criado no Inspector nunca nasce inválido.
            var slot = new EnemySlot();
            Assert.GreaterOrEqual(slot.count, 1);
        }

        // ================================================================== BossBehaviorRegistry

        [Test]
        public void Registry_BossesDoTutorial_ResolvemParaBehaviorsDedicados()
        {
            // ids canônicos do MvpContentFactory.ConfigureBoss — mudar lá exige mudar o registry.
            Assert.AreEqual(typeof(WoodGiantBossBehavior), BossBehaviorRegistry.Resolve("m1_final_wood_giant"));
            Assert.AreEqual(typeof(ZombieTitanBossBehavior), BossBehaviorRegistry.Resolve("m2_final_zombie_titan"));
            Assert.AreEqual(typeof(ScorpionMechBossBehavior), BossBehaviorRegistry.Resolve("m3_final_scorpion_mech"));
        }

        [Test]
        public void Registry_IdDesconhecido_CaiNoGenerico()
        {
            // Todo boss reage a algo: id sem entrada própria ganha o GenericBossBehavior.
            Assert.AreEqual(typeof(GenericBossBehavior), BossBehaviorRegistry.Resolve("m99_boss_inexistente"));
        }

        [Test]
        public void Registry_IdNuloOuVazio_CaiNoGenericoSemLancar()
        {
            Assert.AreEqual(typeof(GenericBossBehavior), BossBehaviorRegistry.Resolve(null));
            Assert.AreEqual(typeof(GenericBossBehavior), BossBehaviorRegistry.Resolve(string.Empty));
        }

        // ================================================================== Assets reais — tutorial F1–F5

        private static LevelConfigSO LoadLevelOrIgnore(int index)
        {
            return LoadAssetOrIgnore<LevelConfigSO>(
                string.Format("{0}/Levels/Level_{1:000}.asset", SoRoot, index));
        }

        [Test]
        public void Fase1_Asset_PistaLimpaESoSomasPositivas()
        {
            // CANON §16 / missão Nota 10: F1 é impossível de perder — pista limpa (sem
            // obstáculos/inimigos) e exatamente 2 pares MANUAIS de soma positiva (+N).
            LevelConfigSO level = LoadLevelOrIgnore(1);

            Assert.IsTrue(level.obstacles == null || level.obstacles.Length == 0,
                "Fase 1 com obstáculos — onboarding deve ser impossível de perder.");
            Assert.IsTrue(level.enemies == null || level.enemies.Length == 0,
                "Fase 1 com inimigos de pista — onboarding deve ser impossível de perder.");

            Assert.IsNotNull(level.gateSlots, "Fase 1 sem gateSlots.");
            Assert.AreEqual(2, level.gateSlots.Length,
                "F1 do tutorial usa exatamente 2 pares manuais (BuildOnboardingSlots).");
            foreach (GateSlot slot in level.gateSlots)
            {
                Assert.IsNotNull(slot, "Fase 1 com GateSlot nulo.");
                Assert.IsFalse(slot.autoBalance, "F1 usa só pares MANUAIS — autoBalance é proibido.");
                AssertPortalSomaPositiva(slot.leftGate);
                AssertPortalSomaPositiva(slot.rightGate);
            }
        }

        private static void AssertPortalSomaPositiva(GateConfigSO gate)
        {
            Assert.IsNotNull(gate, "F1 com meio-portal nulo (par manual incompleto).");
            Assert.AreEqual(GateType.AddFlat, gate.gateType,
                "F1 ensina só CRESCIMENTO — portal '" + gate.name + "' não é AddFlat.");
            Assert.Greater(gate.value, 0f,
                "F1 ensina só CRESCIMENTO — portal '" + gate.name + "' não é soma positiva.");
        }

        [Test]
        public void Fase3_Asset_BossFracoAFogoComPortalDeFogo()
        {
            // A fase que ensina FRAQUEZA (missão Nota 10): boss fraco a FOGO e pelo menos um
            // portal de elemento FOGO entre os slots — sem isso a lição não é jogável.
            LevelConfigSO level = LoadLevelOrIgnore(3);

            Assert.IsNotNull(level.boss, "Fase 3 sem boss — toda fase termina em boss (CANON §6).");
            Assert.IsNotNull(level.boss.weaknesses, "Boss da fase 3 sem array de fraquezas.");
            Assert.IsTrue(System.Array.IndexOf(level.boss.weaknesses, ElementType.Fire) >= 0,
                "Boss da fase 3 ('" + level.boss.bossId + "') precisa ser FRACO A FOGO.");

            bool hasFireGate = false;
            if (level.gateSlots != null)
            {
                foreach (GateSlot slot in level.gateSlots)
                {
                    if (slot == null) continue;
                    if (EhPortalDeFogo(slot.leftGate) || EhPortalDeFogo(slot.rightGate)) hasFireGate = true;
                }
            }
            Assert.IsTrue(hasFireGate,
                "Fase 3 sem portal de ELEMENTO FOGO — o jogador não consegue explorar a fraqueza ensinada.");
        }

        private static bool EhPortalDeFogo(GateConfigSO gate)
        {
            return gate != null && gate.gateType == GateType.Element && gate.element == ElementType.Fire;
        }

        [Test]
        public void Fase4_Asset_TemInimigosDePista()
        {
            // F4 introduz as hordas (o prazer de atropelar) — slots válidos, nunca grupo fantasma.
            LevelConfigSO level = LoadLevelOrIgnore(4);

            Assert.IsNotNull(level.enemies, "Fase 4 sem array de inimigos.");
            Assert.GreaterOrEqual(level.enemies.Length, 1,
                "Fase 4 deveria ter pelo menos 1 grupo de inimigos de pista (missão Nota 10).");
            foreach (EnemySlot slot in level.enemies)
            {
                Assert.IsNotNull(slot, "Fase 4 com EnemySlot nulo.");
                Assert.IsNotNull(slot.enemy, "Fase 4 com EnemySlot sem EnemyConfigSO — grupo fantasma.");
                Assert.GreaterOrEqual(slot.count, 1, "Fase 4 com EnemySlot de count < 1.");
                Assert.Greater(slot.trackPosition, 0f, "Fase 4 com inimigo antes do início da pista.");
                Assert.Less(slot.trackPosition, level.trackLength, "Fase 4 com inimigo além da arena.");
            }
        }

        [Test]
        public void Fase5_Asset_TemRecompensaDeVitoria()
        {
            // F5 fecha os primeiros 5 minutos com o 1º BAÚ — a decisão de supply paga tangível.
            LevelConfigSO level = LoadLevelOrIgnore(5);
            Assert.IsNotNull(level.winReward,
                "Fase 5 sem winReward — o primeiro baú da jornada sumiu (missão Nota 10).");
        }

        [Test]
        public void Fases1a5_Assets_BaselineDoValidador()
        {
            // Regras gerais de LevelConfigSO do SoValidator sobre as 5 fases do tutorial —
            // cobre também a F2 (que não tem regra dedicada própria).
            for (int i = 1; i <= 5; i++)
            {
                LevelConfigSO level = LoadLevelOrIgnore(i);
                Assert.AreEqual(i, level.levelIndex, "Level_" + i + " com levelIndex divergente do arquivo.");
                Assert.IsNotNull(level.boss, "Level_" + i + " sem boss (CANON §6).");
                Assert.IsNotNull(level.world, "Level_" + i + " sem WorldConfigSO.");
                Assert.Greater(level.trackLength, 0f, "Level_" + i + " com trackLength <= 0.");
                Assert.GreaterOrEqual(level.startingUnits, 1, "Level_" + i + " com startingUnits < 1.");
            }
        }

        // ================================================================== Assets reais — inimigos

        [Test]
        public void EnemyAssets_Mundo1_PassamNosCriteriosDoValidador()
        {
            // 3 papéis do mundo 1 (MvpContentFactory.CreateEnemies): horda, atirador e curador.
            // kind é CONTRATO do asset (o TrackEnemyManager despacha o comportamento por ele).
            var horde = LoadAssetOrIgnore<EnemyConfigSO>(SoRoot + "/Enemies/Enemy_M01_Horde.asset");
            Assert.AreEqual(TrackEnemyKind.WeakHorde, horde.kind, "Enemy_M01_Horde com kind errado.");
            Assert.AreEqual(1, horde.worldIndex, "Enemy_M01_Horde fora do mundo 1.");
            AssertSemViolacoes(horde);

            var ranged = LoadAssetOrIgnore<EnemyConfigSO>(SoRoot + "/Enemies/Enemy_M01_Ranged.asset");
            Assert.AreEqual(TrackEnemyKind.Ranged, ranged.kind, "Enemy_M01_Ranged com kind errado.");
            Assert.Greater(ranged.attackRange, 6f,
                "Enemy_M01_Ranged deveria atacar ANTES do contato (attackRange > 6, CONTRACT §2).");
            AssertSemViolacoes(ranged);

            var healer = LoadAssetOrIgnore<EnemyConfigSO>(SoRoot + "/Enemies/Enemy_M01_Healer.asset");
            Assert.AreEqual(TrackEnemyKind.Healer, healer.kind, "Enemy_M01_Healer com kind errado.");
            Assert.Greater(healer.healPerSecond, 0f, "Enemy_M01_Healer com healPerSecond <= 0 — curador que não cura.");
            AssertSemViolacoes(healer);
        }
    }
}
