using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using MutantArmy.Core;
using MutantArmy.Domain;

namespace MutantArmy.Tests
{
    /// <summary>
    /// TESTES DE REGRESSÃO DE PLAYTEST (missão Nota 10): travam os 3 bugs que o dono
    /// reportou jogando e que JÁ foram corrigidos, para que nunca regridam em silêncio.
    ///
    /// BUG 1 — "as tropas não andam": os prefabs de view nasciam com Animator SEM avatar
    ///   (ou sem RuntimeAnimatorController), então a animação de corrida não tocava. Sem
    ///   avatar o Animator não anima — esta é a regressão exata que travamos aqui.
    /// BUG 2 — "inimigos só apareciam no boss": fases 2/3 não tinham inimigos de pista
    ///   (LevelConfigSO.enemies vazio), então a corrida era um corredor vazio até a arena.
    /// BUG 3 — "inimigos sem modelo": os EnemyConfigSO do mundo 1 ficavam sem prefab (a
    ///   UnitVisualFactory não preenchia), caindo no fallback de cápsula cinza.
    ///
    /// Padrão herdado dos testes EditMode existentes (MissionContentTests/GateConfigTests):
    /// LoadAssetAtPath por caminho com Assert.Ignore quando o asset não existe (estes testes
    /// rodam DEPOIS da factory; asset ausente = pipeline não rodou, nunca Fail); nomes
    /// PT-BR Cenario_Condicao_Resultado; TearDown com DestroyImmediate das instâncias criadas.
    ///
    /// APIs usadas (todas públicas — nenhuma reflection necessária): UnitConfigSO.viewPrefab,
    /// BossConfigSO.prefab, EnemyConfigSO.prefab, LevelConfigSO.enemies/EnemySlot.{enemy,count}.
    /// Inspeção de estados do controller via UnityEditor.Animations.AnimatorController
    /// (asmdef é Editor-only: includePlatforms = ["Editor"], logo UnityEditor.* é legal aqui).
    /// </summary>
    public class PlaytestRegressionTests
    {
        private const string SoRoot = "Assets/_Project/ScriptableObjects";
        private const string AnimRoot = "Assets/_Project/Art/Animations";

        // Prefabs carregados por LoadAssetAtPath não precisam de Destroy (são o asset, não
        // instâncias); mantemos a lista só para qualquer CreateInstance eventual, por consistência.
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

        // Asset ausente = pipeline (MAR Tools/Create MVP Content) ainda não rodou ⇒ Ignore, nunca Fail.
        private static T LoadAssetOrIgnore<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                Assert.Ignore("Asset ainda não gerado pela pipeline: " + path +
                              " — rodar MAR Tools/Create MVP Content e re-executar.");
            return asset;
        }

        private static T[] LoadAllOfTypeOrIgnore<T>(string folder) where T : Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder });
            if (guids == null || guids.Length == 0)
                Assert.Ignore("Nenhum " + typeof(T).Name + " em " + folder +
                              " — pipeline (MAR Tools/Create MVP Content) ainda não rodou.");

            var list = new List<T>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) list.Add(asset);
            }
            return list.ToArray();
        }

        /// <summary>
        /// Coração do BUG 1: um prefab de view PRECISA ter Animator com avatar E controller,
        /// senão não anima. Inclui filhos inativos (GetComponentInChildren(true)) porque a view
        /// pode ser variante de prefab com o Animator num filho.
        /// </summary>
        private static void AssertAnimatorAnimavel(GameObject prefab, string contexto, bool mustAnimate)
        {
            Assert.IsNotNull(prefab, contexto + ": viewPrefab/prefab nulo (esperava-se um modelo).");

            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(animator,
                contexto + ": prefab '" + prefab.name + "' sem Animator — não anima (BUG 'tropas não andam').");

            Assert.IsNotNull(animator.runtimeAnimatorController,
                contexto + ": Animator de '" + prefab.name + "' sem RuntimeAnimatorController — nenhum estado para tocar.");

            // Animação esquelética EXIGE SkinnedMeshRenderer (rig). Modelos CC0 estáticos
            // (MeshRenderer puro — comum nos monstros Quaternius sem a UAL) NÃO podem animar:
            // exigir avatar deles seria falso-positivo. A regra do avatar (a regressão real do
            // "tropas não andam") vale para o que É riggável.
            bool riggable = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;

            if (mustAnimate)
            {
                // Tropas: TÊM de ser riggáveis e ter avatar — é o coração do fix de andar.
                Assert.IsTrue(riggable,
                    contexto + ": prefab '" + prefab.name + "' sem SkinnedMeshRenderer — modelo estático não anda (regressão do rig da tropa).");
                Assert.IsNotNull(animator.avatar,
                    contexto + ": Animator de '" + prefab.name + "' sem avatar — sem avatar a animação de corrida não toca (BUG 'tropas não andam').");
            }
            else if (riggable)
            {
                // Boss/inimigo com modelo riggável: avatar obrigatório (senão anim quebrada);
                // se for modelo estático, tolera (prop sem rig — limitação de asset, não bug).
                Assert.IsNotNull(animator.avatar,
                    contexto + ": Animator riggável de '" + prefab.name + "' sem avatar — anim não toca.");
            }
        }

        // ================================================================== BUG 1: animação/avatar

        [Test]
        public void Tropas_TodosOsViewPrefabs_TemAnimatorComAvatarEController()
        {
            // Trava BUG 1 ("tropas não andam") para TODO o roster: cada UnitConfigSO com
            // viewPrefab precisa de Animator animável (avatar + controller).
            UnitConfigSO[] units = LoadAllOfTypeOrIgnore<UnitConfigSO>(SoRoot + "/Units");

            int comView = 0;
            foreach (UnitConfigSO unit in units)
            {
                if (unit.viewPrefab == null) continue;   // greybox sem modelo cai no fallback VAT — fora do escopo do bug
                comView++;
                AssertAnimatorAnimavel(unit.viewPrefab, "Unidade '" + unit.unitId + "'", mustAnimate: true);
            }

            Assert.Greater(comView, 0,
                "Nenhuma unidade tinha viewPrefab — a UnitVisualFactory não preencheu os modelos (regressão do fix de tropas).");
        }

        [Test]
        public void Bosses_TodosOsPrefabs_TemAnimatorComAvatarEController()
        {
            // Trava BUG 1 para os bosses: prefab preenchido pela factory deve animar.
            BossConfigSO[] bosses = LoadAllOfTypeOrIgnore<BossConfigSO>(SoRoot + "/Bosses");

            foreach (BossConfigSO boss in bosses)
            {
                // Greybox é o fallback DOCUMENTADO p/ boss sem modelo CC0 dedicado (não tem
                // Animator e não é regressão). O teste valida os bosses com MODELO real.
                if (boss.prefab == null || boss.prefab.name.Contains("Greybox")) continue;
                AssertAnimatorAnimavel(boss.prefab, "Boss '" + boss.bossId + "'", mustAnimate: false);
            }
        }

        [Test]
        public void Inimigos_TodosOsPrefabs_TemAnimatorComAvatarEController()
        {
            // Trava BUG 1 para os inimigos de pista que tenham modelo.
            EnemyConfigSO[] enemies = LoadAllOfTypeOrIgnore<EnemyConfigSO>(SoRoot + "/Enemies");

            foreach (EnemyConfigSO enemy in enemies)
            {
                if (enemy.prefab == null) continue;
                AssertAnimatorAnimavel(enemy.prefab, "Inimigo '" + enemy.enemyId + "'", mustAnimate: false);
            }
        }

        [Test]
        public void Soldado_TropaBase_TemViewComAvatarControllerEEstadoRun()
        {
            // Teste específico da TROPA BASE (a mais visível): o Soldado é a primeira coisa
            // que o jogador vê correndo. Trava BUG 1 no caso canônico.
            UnitConfigSO soldier = LoadAssetOrIgnore<UnitConfigSO>(SoRoot + "/Units/Unit_Soldier.asset");

            Assert.IsNotNull(soldier.viewPrefab,
                "Soldado sem viewPrefab — a tropa base ficaria invisível/sem modelo.");
            AssertAnimatorAnimavel(soldier.viewPrefab, "Soldado (tropa base)", mustAnimate: true);

            // O controller real precisa ter um estado de CORRIDA com clip de verdade —
            // sem ele, mesmo com avatar a tropa fica parada (o bug que o dono viu).
            AnimatorController controller = LoadAssetOrIgnore<AnimatorController>(AnimRoot + "/AC_Unit_Soldier.controller");

            Assert.IsNotNull(controller.layers, "AC_Unit_Soldier sem camadas.");
            Assert.Greater(controller.layers.Length, 0, "AC_Unit_Soldier sem camada base.");

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            Assert.IsNotNull(sm, "AC_Unit_Soldier sem stateMachine na camada base.");

            bool achouRunComMotion = false;
            foreach (ChildAnimatorState child in sm.states)
            {
                AnimatorState state = child.state;
                if (state == null) continue;
                if (state.name != null && state.name.IndexOf("Run", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // motion != null = clip REAL atribuído ao estado de corrida.
                    if (state.motion != null) achouRunComMotion = true;
                }
            }

            Assert.IsTrue(achouRunComMotion,
                "AC_Unit_Soldier sem estado 'Run' com motion (clip) — a tropa base não tem animação de corrida (BUG 'tropas não andam').");
        }

        // ================================================================== BUG 2: inimigos cedo (antes do boss)

        // Carrega Level_00N por caminho canônico (mesmo padrão de MissionContentTests).
        private static LevelConfigSO LoadLevelOrIgnore(int index)
        {
            return LoadAssetOrIgnore<LevelConfigSO>(
                string.Format("{0}/Levels/Level_{1:000}.asset", SoRoot, index));
        }

        private static void AssertSlotsDeInimigoValidos(LevelConfigSO level, int fase)
        {
            Assert.IsNotNull(level.enemies, "Fase " + fase + " sem array de inimigos.");
            Assert.GreaterOrEqual(level.enemies.Length, 1,
                "Fase " + fase + " sem inimigos de pista — eles deveriam aparecer ANTES do boss (BUG 'inimigos só no boss').");
            foreach (EnemySlot slot in level.enemies)
            {
                Assert.IsNotNull(slot, "Fase " + fase + " com EnemySlot nulo.");
                Assert.IsNotNull(slot.enemy,
                    "Fase " + fase + " com EnemySlot sem EnemyConfigSO — grupo fantasma.");
                Assert.GreaterOrEqual(slot.count, 1, "Fase " + fase + " com EnemySlot de count < 1.");
            }
        }

        [Test]
        public void Fase1_Onboarding_ContinuaSemInimigosDePista()
        {
            // F1 é o onboarding impossível de perder — NÃO deve ganhar inimigos de pista.
            // Trava o lado oposto do BUG 2: a correção não pode vazar inimigos para a F1.
            LevelConfigSO level = LoadLevelOrIgnore(1);
            Assert.IsTrue(level.enemies == null || level.enemies.Length == 0,
                "Fase 1 ganhou inimigos de pista — o onboarding deve permanecer impossível de perder.");
        }

        [Test]
        public void Fase2_TemInimigosDePistaAntesDoBoss()
        {
            // Trava BUG 2 na F2: o dono jogou e só viu inimigos no boss; agora há inimigos na pista.
            LevelConfigSO level = LoadLevelOrIgnore(2);
            AssertSlotsDeInimigoValidos(level, 2);
        }

        [Test]
        public void Fase3_TemInimigosDePistaAntesDoBoss()
        {
            // Trava BUG 2 na F3.
            LevelConfigSO level = LoadLevelOrIgnore(3);
            AssertSlotsDeInimigoValidos(level, 3);
        }

        [Test]
        public void Fase4_TemInimigosDePistaAntesDoBoss()
        {
            // F4 introduz as hordas — reforço do invariante já coberto em MissionContentTests.
            LevelConfigSO level = LoadLevelOrIgnore(4);
            AssertSlotsDeInimigoValidos(level, 4);
        }

        // ================================================================== BUG 3: inimigos têm visual

        [Test]
        public void Inimigos_Mundo1_TemPrefabDeModelo()
        {
            // Trava BUG 3 ("inimigos sem modelo") para o mundo 1: a UnitVisualFactory deve ter
            // preenchido prefab nos EnemyConfigSO. Se o asset existe mas prefab veio null,
            // é regressão real do fix dos modelos CC0 — Fail. Sem assets = Ignore.
            EnemyConfigSO[] enemies = LoadAllOfTypeOrIgnore<EnemyConfigSO>(SoRoot + "/Enemies");

            int doMundo1 = 0;
            foreach (EnemyConfigSO enemy in enemies)
            {
                if (enemy.worldIndex != 1) continue;
                doMundo1++;
                Assert.IsNotNull(enemy.prefab,
                    "Inimigo do mundo 1 '" + enemy.enemyId + "' sem prefab — a UnitVisualFactory não preencheu o modelo (BUG 'inimigos sem modelo').");
            }

            Assert.Greater(doMundo1, 0,
                "Nenhum EnemyConfigSO do mundo 1 encontrado — esperava-se o roster Enemy_M01_* gerado pela factory.");
        }
    }
}
