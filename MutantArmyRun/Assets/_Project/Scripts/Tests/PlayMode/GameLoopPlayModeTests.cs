using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MutantArmy.Core;
using MutantArmy.Domain;
using MutantArmy.Gameplay;
using MutantArmy.Meta;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MutantArmy.Tests
{
    /// <summary>
    /// Testes PlayMode do loop completo (doc 12 §4.1): boot real, fase 1 até a vitória
    /// com AutoPilot, fluxo de derrota com oferta de revive e overflow de Supply em
    /// moedas. Todos batchmode/-nographics-safe: nenhum assert visual — só estado,
    /// eventos do bus e save. LogAssert.ignoreFailingMessages fica false: qualquer
    /// LogError/exceção real derruba o teste.
    /// </summary>
    public class GameLoopPlayModeTests
    {
        private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";
        private const string SoRoot = "Assets/_Project/ScriptableObjects";
        private const string Level001Path = SoRoot + "/Levels/Level_001.asset";
        private const string SoldierPath = SoRoot + "/Units/Unit_Soldier.asset";
        private const string ChartPath = SoRoot + "/Balance/ElementChart_Default.asset";
        private const string GateX3Path = SoRoot + "/Gates/Gate_X3.asset";

        private GameLoopRig _rig;
        private readonly List<UnityEngine.Object> _clonedAssets = new List<UnityEngine.Object>();
        private float _baseFixedDelta;

        [UnitySetUp]
        public IEnumerator BeforeEach()
        {
            LogAssert.ignoreFailingMessages = false;   // exceções/LogError reais DEVEM falhar o teste
            Time.timeScale = 1f;
            _baseFixedDelta = Time.fixedDeltaTime;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator AfterEach()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _baseFixedDelta;

            // O teste de boot deixa o [Services] real em DontDestroyOnLoad — derrubar
            // sempre, para nenhum manager persistente vazar para o teste seguinte.
            GameObject services = GameObject.Find("[Services]");
            if (services != null) UnityEngine.Object.Destroy(services);

            if (_rig != null)
            {
                if (_rig.Pilot != null) _rig.Pilot.Active = false;
                IEnumerator unload = _rig.Unload();
                _rig = null;
                while (unload.MoveNext()) yield return unload.Current;
            }

            for (int i = 0; i < _clonedAssets.Count; i++)
                if (_clonedAssets[i] != null) UnityEngine.Object.Destroy(_clonedAssets[i]);
            _clonedAssets.Clear();

            yield return null;
        }

        // ------------------------------------------------------------------ (a) boot

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator BootScene_CarregaSemExcecoes()
        {
#if UNITY_EDITOR
            // Por caminho (não por nome): independe do Build Settings estar populado.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                BootScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene("Boot", LoadSceneMode.Single);
#endif

            // O GameBootstrap real roda: Save → RC (Null) → Analytics → Ads → managers →
            // LoadSceneAsync(Main) → MainMenu. Qualquer exceção no caminho reprova via LogAssert.
            yield return WaitUntilOrTimeout(
                () => GameManager.Instance != null && GameManager.Instance.State == GameState.MainMenu,
                60f, "boot completo (GameManager em MainMenu após carregar a cena Main)");

            Assert.AreEqual(GameState.MainMenu, GameManager.Instance.State,
                "Boot deveria terminar no MainMenu (doc 12 §4.1).");
            Assert.AreEqual("Main", SceneManager.GetActiveScene().name,
                "Boot deveria ter carregado a cena Main (doc 12 §2.2).");
            Assert.IsNotNull(SaveSystem.Instance, "SaveSystem não foi inicializado pelo GameBootstrap.");
            Assert.IsNotNull(SaveSystem.Instance.Data, "SaveSystem.Data nulo após o boot.");
        }

        // ------------------------------------------------------------------ (b) fase 1 → vitória

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator GameScene_Fase1_VitoriaCompleta()
        {
            LevelConfigSO level = LoadAsset<LevelConfigSO>(Level001Path);
            BuildRig("Rig_Vitoria");

            HashSet<GameState> statesSeen = new HashSet<GameState>();
            _rig.Gm.StateEntered += s => statesSeen.Add(s);

            int gatesConsumed = 0;
            Action<GateResult> onGate = r => gatesConsumed++;
            LevelResult? finished = null;
            Action<LevelResult> onFinished = r => finished = r;
            GameEvents.OnGateConsumed += onGate;
            GameEvents.OnLevelFinished += onFinished;

            try
            {
                long coinsBefore = _rig.Save.Data.coins;
                string savePath = Path.Combine(Application.persistentDataPath, "save.json");
                DateTime saveStampBefore = File.Exists(savePath)
                    ? File.GetLastWriteTimeUtc(savePath)
                    : DateTime.MinValue;

                _rig.Pilot.Configure(AutoPilot.SideStrategy.BestExpectedValue, 1234);
                _rig.Pilot.Active = true;
                Time.timeScale = 8f;                       // acelerado; timeout duro abaixo é de RELÓGIO
                _rig.StartLevel(level);

                // ReviveOffer entra na condição só para FALHAR RÁPIDO (com estado claro)
                // se a fase 1 — impossível de perder — terminar em wipe por regressão.
                yield return WaitUntilOrTimeout(
                    () => _rig.Gm.State == GameState.Victory || _rig.Gm.State == GameState.Defeat
                          || _rig.Gm.State == GameState.ReviveOffer,
                    120f, "fim da fase 1 (Victory/Defeat/ReviveOffer)");

                Assert.AreEqual(GameState.Victory, _rig.Gm.State,
                    "Fase 1 (CANON §16: impossível perder) deveria terminar em Victory.");
                Assert.GreaterOrEqual(gatesConsumed, 1,
                    "Nenhum portal foi consumido — AutoPilot/trigger do par falhou.");
                Assert.IsTrue(statesSeen.Contains(GameState.BossFight),
                    "O fluxo não passou por BossFight antes da vitória (doc 12 §4.1).");
                Assert.IsTrue(finished.HasValue && finished.Value.won,
                    "OnLevelFinished(won=true) não foi publicado no bus.");
                Assert.Greater(finished.Value.survivors, 0, "Vitória sem sobreviventes no LevelResult.");

                // Moedas comitadas pelo ResolveEnd real (commit do RunWallet + recompensa
                // de vitória via EconomySystem.GrantLevelReward — CANON §8).
                Assert.Greater(_rig.Save.Data.coins, coinsBefore,
                    "Vitória não creditou moeda nenhuma na carteira persistente.");

                // Save gravado: RecordLevelEnd dispara SaveAsync — espera o I/O real.
                yield return WaitUntilOrTimeout(
                    () => File.Exists(savePath) && File.GetLastWriteTimeUtc(savePath) > saveStampBefore,
                    15f, "gravação do save.json pós-vitória");

                LevelRecord record = _rig.Save.Data.levelRecords.Find(r => r.levelIndex == level.levelIndex);
                Assert.IsNotNull(record, "RecordLevelEnd não criou o LevelRecord da fase 1.");
                Assert.IsTrue(record.won, "LevelRecord da fase 1 deveria estar marcado como vencido.");
            }
            finally
            {
                GameEvents.OnGateConsumed -= onGate;
                GameEvents.OnLevelFinished -= onFinished;
            }
        }

        // ------------------------------------------------------------------ (c) derrota → revive → defeat

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator GameScene_Derrota_OfereceReviveEDefeat()
        {
            LevelConfigSO baseLevel = LoadAsset<LevelConfigSO>(Level001Path);

            // Clone em memória (SO do projeto é read-only em runtime): boss imbatível via
            // multiplier e pista curta para chegar à arena rápido.
            LevelConfigSO hardLevel = UnityEngine.Object.Instantiate(baseLevel);
            hardLevel.bossHpMultiplier = 1000000f;
            hardLevel.trackLength = 60f;
            _clonedAssets.Add(hardLevel);

            BuildRig("Rig_Derrota");
            _rig.Save.Data.usedReviveThisLevel = false;    // garante elegibilidade do revive (CANON §11)

            HashSet<GameState> statesSeen = new HashSet<GameState>();
            bool reviveOffered = false;
            _rig.Gm.StateEntered += s =>
            {
                statesSeen.Add(s);
                if (s == GameState.ReviveOffer) reviveOffered = true;
            };

            LevelResult? finished = null;
            Action<LevelResult> onFinished = r => finished = r;
            GameEvents.OnLevelFinished += onFinished;

            try
            {
                long coinsBefore = _rig.Save.Data.coins;
                int defeatsBefore = _rig.Save.Data.consecutiveDefeats;

                _rig.Pilot.Configure(AutoPilot.SideStrategy.BestExpectedValue, 1234);
                _rig.Pilot.Active = true;
                Time.timeScale = 8f;
                _rig.StartLevel(hardLevel);

                // Boss com HP absurdo: o exército é dizimado na arena → wipe em BossFight
                // → GameManager.OfferRevive faz PUSH de ReviveOffer SOBRE a luta.
                yield return WaitUntilOrTimeout(
                    () => _rig.Gm.State == GameState.ReviveOffer || _rig.Gm.State == GameState.Defeat,
                    120f, "wipe do exército na arena (ReviveOffer ou Defeat)");

                Assert.IsTrue(reviveOffered,
                    "Derrota no boss com revive disponível deveria OFERECER o revive antes do Defeat (CANON §11).");
                Assert.IsTrue(statesSeen.Contains(GameState.BossFight),
                    "O fluxo não passou por BossFight antes do wipe.");
                Assert.AreEqual(GameState.ReviveOffer, _rig.Gm.State,
                    "Estado deveria estar em ReviveOffer (push sobre o BossFight).");

                // Jogador recusa o revive — o fluxo real segue para Defeat.
                _rig.Gm.ResolveRevive(false);
                yield return null;

                Assert.AreEqual(GameState.Defeat, _rig.Gm.State, "Recusar o revive deveria levar a Defeat.");
                Assert.IsTrue(finished.HasValue, "OnLevelFinished não foi publicado na derrota.");
                Assert.IsFalse(finished.Value.won, "LevelResult da derrota veio com won=true.");
                Assert.AreEqual(coinsBefore, _rig.Save.Data.coins,
                    "Derrota descarta as moedas da corrida — carteira não podia mudar (doc 12 §4.6).");
                Assert.AreEqual(defeatsBefore + 1, _rig.Save.Data.consecutiveDefeats,
                    "RecordLevelEnd deveria incrementar consecutiveDefeats na derrota.");
            }
            finally
            {
                GameEvents.OnLevelFinished -= onFinished;
            }
        }

        // ------------------------------------------------------------------ (d) overflow de Supply

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Supply_Overflow_ViraMoedas()
        {
            UnitConfigSO soldier = LoadAsset<UnitConfigSO>(SoldierPath);
            GateConfigSO gateX3 = LoadAsset<GateConfigSO>(GateX3Path);
            BuildRig("Rig_Overflow");

            SupplyOverflow? overflow = null;
            Action<SupplyOverflow> onOverflow = o => overflow = o;
            GameEvents.OnSupplyOverflow += onOverflow;

            try
            {
                // Exército grande DENTRO do cap (50 de 60), pelo funil real de reconciliação.
                _rig.Crowd.ReconcileTo(50, soldier);
                Assert.AreEqual(50, _rig.Crowd.Count, "Setup: exército deveria ter 50 unidades.");
                Assert.LessOrEqual(_rig.Crowd.SupplyUsed, _rig.Crowd.SupplyCap, "Setup não podia estourar o cap.");

                long coinsBefore = _rig.Save.Data.coins;

                // Portal x3 consumido pelo funil real do GateManager: 50 → 150 estoura o
                // cap de 60 e o excedente VIRA MOEDA na hora (CANON §3.2 / doc 12 §4.6).
                _rig.Gates.Consume(gateX3, null);

                Assert.IsTrue(overflow.HasValue, "OnSupplyOverflow não foi publicado no estouro do cap.");
                Assert.Greater(overflow.Value.unitsConverted, 0, "Overflow sem unidades convertidas.");
                Assert.Greater(overflow.Value.coinsGranted, 0, "Overflow sem moedas concedidas.");
                Assert.AreEqual(coinsBefore + overflow.Value.coinsGranted, _rig.Save.Data.coins,
                    "EconomySystem deveria creditar o overflow NA HORA, no mesmo frame do evento.");

                // O metering (1 conversão a cada ~80 ms) drena o excedente até o cap.
                Time.timeScale = 10f;
                yield return WaitUntilOrTimeout(
                    () => _rig.Crowd.SupplyUsed <= _rig.Crowd.SupplyCap,
                    60f, "metering do overflow drenar o Supply até o cap");

                Assert.AreEqual(_rig.Crowd.SupplyCap, _rig.Crowd.Count,
                    "Com soldados (custo 1), o exército deveria assentar exatamente no cap de Supply.");
            }
            finally
            {
                GameEvents.OnSupplyOverflow -= onOverflow;
            }
        }

        // ------------------------------------------------------------------ infra

        private void BuildRig(string sceneName)
        {
            ElementChartSO chart = LoadAsset<ElementChartSO>(ChartPath);
            UnitConfigSO soldier = LoadAsset<UnitConfigSO>(SoldierPath);
            _rig = new GameLoopRig();
            _rig.Build(sceneName + "_" + Time.frameCount, chart, soldier);
        }

        private static T LoadAsset<T>(string path) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            T asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset,
                "Asset não encontrado: " + path + " — rodar MAR Tools/Create MVP Content antes dos testes.");
            return asset;
#else
            // Player build de testes: os SOs do projeto não são endereçáveis fora do editor.
            Assert.Ignore("Teste requer AssetDatabase (rodar no editor/batchmode do editor).");
            return null;
#endif
        }

        private static IEnumerator WaitUntilOrTimeout(Func<bool> condition, float timeoutRealSeconds, string what)
        {
            float start = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - start > timeoutRealSeconds)
                    Assert.Fail("Timeout de " + timeoutRealSeconds + "s (relógio) esperando: " + what);
                yield return null;
            }
        }
    }
}
