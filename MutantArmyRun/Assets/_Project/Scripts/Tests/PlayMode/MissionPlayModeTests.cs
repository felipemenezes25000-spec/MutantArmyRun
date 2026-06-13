using System;
using System.Collections;
using System.Collections.Generic;
using MutantArmy.Core;
using MutantArmy.Domain;
using MutantArmy.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MutantArmy.Tests
{
    /// <summary>
    /// Testes PlayMode da missão Nota 10 (Onda 5): caminhos novos do loop — veredito
    /// elemental do boss (F3), ordem OnBossDied → Victory com combos/failReason no
    /// LevelResult, fail reason rico na derrota e inimigos de pista engajando (F4).
    /// Mesmo padrão do GameLoopPlayModeTests: rig por código, AutoPilot determinístico,
    /// timeScale acelerado com timeouts em RELÓGIO real (a morte do boss leva +1,2 s
    /// REAIS — sequência cinematográfica em tempo unscaled) e LogAssert estrito
    /// (qualquer LogError novo derruba o teste).
    /// </summary>
    public class MissionPlayModeTests
    {
        private const string SoRoot = "Assets/_Project/ScriptableObjects";
        private const string Level001Path = SoRoot + "/Levels/Level_001.asset";
        private const string Level003Path = SoRoot + "/Levels/Level_003.asset";
        private const string Level004Path = SoRoot + "/Levels/Level_004.asset";
        private const string SoldierPath = SoRoot + "/Units/Unit_Soldier.asset";
        private const string ChartPath = SoRoot + "/Balance/ElementChart_Default.asset";

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

        // ------------------------------------------------------------------ (a) F3: veredito elemental

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Fase3_EnsinaFraqueza_BossElementalHitDispara()
        {
            // Pragmático (anti-flakiness): o AutoPilot decide por valor ESPERADO de contagem,
            // então o portal de FOGO da F3 (vs x5) não é garantido — o que É garantido é o
            // VEREDITO ELEMENTAL: o CombatSystem publica OnBossElementalHit no 1º tick de dano
            // da luta (rate-limit zerado no InitFight) com QUALQUER relation. O teste cobre o
            // caminho novo (luta começa, HP publica, veredito sai, fase TERMINA sem exceções)
            // sem apostar na rota do piloto.
            LevelConfigSO level = LoadAsset<LevelConfigSO>(Level003Path);
            BuildRig("Rig_Fase3");

            HashSet<GameState> statesSeen = new HashSet<GameState>();
            _rig.Gm.StateEntered += s => statesSeen.Add(s);

            int elementalHits = 0;
            Action<BossElementalHit> onElementalHit = h => elementalHits++;
            int hpRaises = 0;
            Action<float> onBossHp = hp => hpRaises++;
            LevelResult? finished = null;
            Action<LevelResult> onFinished = r => finished = r;
            GameEvents.OnBossElementalHit += onElementalHit;
            GameEvents.OnBossHpChanged += onBossHp;
            GameEvents.OnLevelFinished += onFinished;

            try
            {
                _rig.Pilot.Configure(AutoPilot.SideStrategy.BestExpectedValue, 1234);
                _rig.Pilot.Active = true;
                Time.timeScale = 8f;                       // acelerado; timeout duro abaixo é de RELÓGIO
                _rig.StartLevel(level);

                yield return WaitUntilOrTimeout(
                    () => _rig.Gm.State == GameState.Victory || _rig.Gm.State == GameState.Defeat
                          || _rig.Gm.State == GameState.ReviveOffer,
                    120f, "fim da fase 3 (Victory/Defeat/ReviveOffer)");

                // Derrota com revive disponível empilha ReviveOffer — recusar fecha a fase
                // pelo fluxo real (o objetivo aqui é a fase TERMINAR, não exigir vitória).
                if (_rig.Gm.State == GameState.ReviveOffer)
                {
                    _rig.Gm.ResolveRevive(false);
                    yield return null;
                }

                Assert.IsTrue(statesSeen.Contains(GameState.BossFight),
                    "Fase 3 não chegou à luta de boss — a lição de fraqueza nem começou.");
                Assert.GreaterOrEqual(hpRaises, 1,
                    "OnBossHpChanged não disparou — a luta não publicou HP no bus.");
                Assert.GreaterOrEqual(elementalHits, 1,
                    "OnBossElementalHit não disparou — o veredito elemental do tick sumiu (CombatSystem.EmitElementalFeedback).");
                Assert.IsTrue(_rig.Gm.State == GameState.Victory || _rig.Gm.State == GameState.Defeat,
                    "Fase 3 deveria terminar em Victory ou Defeat — terminou em " + _rig.Gm.State + ".");
                Assert.IsTrue(finished.HasValue, "OnLevelFinished não foi publicado no fim da fase 3.");
            }
            finally
            {
                GameEvents.OnBossElementalHit -= onElementalHit;
                GameEvents.OnBossHpChanged -= onBossHp;
                GameEvents.OnLevelFinished -= onFinished;
            }
        }

        // ------------------------------------------------------------------ (b) ordem BossDied → Victory

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator BossDied_DisparaAntesDaVitoria()
        {
            // Contrato §5: OnBossDied dispara na MORTE, ~1,2 s reais ANTES do ChangeState
            // (Victory) — sequência cinematográfica com o estado ainda em BossFight. O
            // LevelResult da vitória chega depois, com os acumuladores da missão consolidados.
            LevelConfigSO level = LoadAsset<LevelConfigSO>(Level001Path);
            BuildRig("Rig_BossDied");

            bool bossDiedRaised = false;
            bool diedBeforeResult = false;
            GameState stateAtDeath = GameState.Boot;
            LevelResult? finished = null;
            Action<LevelResult> onFinished = r => finished = r;
            Action<BossDied> onBossDied = d =>
            {
                bossDiedRaised = true;
                diedBeforeResult = !finished.HasValue;     // o result ainda NÃO pode ter saído
                stateAtDeath = _rig.Gm.State;              // sequência roda com BossFight no topo
            };
            GameEvents.OnBossDied += onBossDied;
            GameEvents.OnLevelFinished += onFinished;

            try
            {
                _rig.Pilot.Configure(AutoPilot.SideStrategy.BestExpectedValue, 1234);
                _rig.Pilot.Active = true;
                Time.timeScale = 8f;
                _rig.StartLevel(level);

                // ReviveOffer na condição só para FALHAR RÁPIDO se a fase 1 regredir.
                yield return WaitUntilOrTimeout(
                    () => _rig.Gm.State == GameState.Victory || _rig.Gm.State == GameState.Defeat
                          || _rig.Gm.State == GameState.ReviveOffer,
                    120f, "fim da fase 1 (Victory/Defeat/ReviveOffer)");

                Assert.AreEqual(GameState.Victory, _rig.Gm.State,
                    "Fase 1 (impossível de perder) deveria terminar em Victory.");
                Assert.IsTrue(bossDiedRaised, "OnBossDied não disparou na vitória.");
                Assert.IsTrue(diedBeforeResult,
                    "OnBossDied deveria disparar ANTES do LevelResult/Victory (contrato §5 + sequência de 1,2 s).");
                Assert.AreEqual(GameState.BossFight, stateAtDeath,
                    "Na morte do boss o estado ainda deveria ser BossFight (morte cinematográfica).");

                Assert.IsTrue(finished.HasValue && finished.Value.won,
                    "OnLevelFinished(won=true) não foi publicado no bus.");
                Assert.GreaterOrEqual(finished.Value.comboCount, 0, "comboCount negativo no LevelResult.");
                Assert.GreaterOrEqual(finished.Value.comboBonusCoins, 0, "comboBonusCoins negativo no LevelResult.");
                // invariante do ComboMath: todo combo paga bônus > 0 — contagem e bônus andam juntos
                Assert.AreEqual(finished.Value.comboCount == 0, finished.Value.comboBonusCoins == 0,
                    "comboCount e comboBonusCoins divergem (combo sem bônus ou bônus sem combo).");
                Assert.AreEqual(FailReason.None, finished.Value.failReason,
                    "Vitória NUNCA carrega failReason (GameManager.ResolveEnd).");
            }
            finally
            {
                GameEvents.OnBossDied -= onBossDied;
                GameEvents.OnLevelFinished -= onFinished;
            }
        }

        // ------------------------------------------------------------------ (c) derrota com fail reason rico

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Derrota_TemFailReason()
        {
            // Mesmo funil do teste de derrota existente: clone em memória com boss imbatível
            // (multiplier 1e6) e pista curta. O wipe na arena resolve o FailReasonResolver
            // ANTES da transição — o LevelResult da derrota chega com motivo rico, nunca None.
            LevelConfigSO baseLevel = LoadAsset<LevelConfigSO>(Level001Path);
            LevelConfigSO hardLevel = UnityEngine.Object.Instantiate(baseLevel);
            hardLevel.bossHpMultiplier = 1000000f;
            hardLevel.trackLength = 60f;
            _clonedAssets.Add(hardLevel);

            BuildRig("Rig_FailReason");
            _rig.Save.Data.usedReviveThisLevel = false;    // elegível: o fluxo real passa pelo ReviveOffer

            LevelResult? finished = null;
            Action<LevelResult> onFinished = r => finished = r;
            FailReason resolvedOnBus = FailReason.None;
            Action<FailReason> onFailReason = r => resolvedOnBus = r;
            GameEvents.OnLevelFinished += onFinished;
            GameEvents.OnFailReasonResolved += onFailReason;

            try
            {
                _rig.Pilot.Configure(AutoPilot.SideStrategy.BestExpectedValue, 1234);
                _rig.Pilot.Active = true;
                Time.timeScale = 8f;
                _rig.StartLevel(hardLevel);

                yield return WaitUntilOrTimeout(
                    () => _rig.Gm.State == GameState.ReviveOffer || _rig.Gm.State == GameState.Defeat,
                    120f, "wipe do exército na arena (ReviveOffer ou Defeat)");

                if (_rig.Gm.State == GameState.ReviveOffer)
                {
                    _rig.Gm.ResolveRevive(false);          // jogador recusa — segue para Defeat
                    yield return null;
                }

                Assert.AreEqual(GameState.Defeat, _rig.Gm.State, "Derrota não chegou ao estado Defeat.");
                Assert.AreNotEqual(FailReason.None, resolvedOnBus,
                    "OnFailReasonResolved não publicou motivo rico ANTES da transição (contrato §5).");
                Assert.IsTrue(finished.HasValue, "OnLevelFinished não foi publicado na derrota.");
                Assert.IsFalse(finished.Value.won, "LevelResult da derrota veio com won=true.");
                Assert.AreNotEqual(FailReason.None, finished.Value.failReason,
                    "LevelResult.failReason da derrota deveria carregar o motivo rico (missão Nota 10).");
                Assert.AreEqual(resolvedOnBus, finished.Value.failReason,
                    "GameManager deveria consolidar no result o MESMO motivo publicado no bus.");
            }
            finally
            {
                GameEvents.OnLevelFinished -= onFinished;
                GameEvents.OnFailReasonResolved -= onFailReason;
            }
        }

        // ------------------------------------------------------------------ (d) F4: inimigos de pista

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator Fase4_InimigosDePista_SpawnamEEngajam()
        {
            LevelConfigSO level = LoadAsset<LevelConfigSO>(Level004Path);
            if (level.enemies == null || level.enemies.Length == 0)
                Assert.Ignore("Level_004 sem EnemySlot[] (asset antigo) — rodar MAR Tools/Create MVP Content.");

            BuildRig("Rig_Fase4");
            Assert.IsNotNull(TrackEnemyManager.Instance,
                "TrackEnemyManager.Instance nulo após o Build — o rig não espelha mais o composition root.");

            int groupsKilled = 0;
            int coinsDropped = 0;
            Action<TrackEnemyKilled> onKilled = k =>
            {
                groupsKilled++;
                coinsDropped += k.coins;
            };
            GameEvents.OnTrackEnemyKilled += onKilled;

            try
            {
                _rig.Pilot.Configure(AutoPilot.SideStrategy.BestExpectedValue, 1234);
                _rig.Pilot.Active = true;
                Time.timeScale = 8f;
                _rig.StartLevel(level);

                // Timeout GENEROSO de relógio: a horda fraca da F4 morre no contato bem antes
                // do fim da corrida — mas o fim da fase entra na condição para falhar com
                // diagnóstico claro (e não por timeout) se a corrida terminar sem kill.
                yield return WaitUntilOrTimeout(
                    () => groupsKilled >= 1
                          || _rig.Gm.State == GameState.Victory || _rig.Gm.State == GameState.Defeat
                          || _rig.Gm.State == GameState.ReviveOffer,
                    150f, "primeira horda de pista morrer no contato (OnTrackEnemyKilled)");

                Assert.GreaterOrEqual(groupsKilled, 1,
                    "Nenhum OnTrackEnemyKilled antes do fim da fase — horda fraca da F4 não morreu no contato.");
                Assert.GreaterOrEqual(coinsDropped, 0, "Drop agregado negativo no TrackEnemyKilled.");
            }
            finally
            {
                GameEvents.OnTrackEnemyKilled -= onKilled;
            }
        }

        // ------------------------------------------------------------------ (e) abandono mid-luta (regressão)

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator AbandonoMidLuta_LiberaBossEReseta()
        {
            // REGRESSÃO (revisão adversarial): RestartLevelFromAnyState/GoToMainMenuFromAnyState
            // zeram a pilha SEM passar por ChangeState, então o ExitState(BossFight) normal não
            // disparava e o BossManager nunca soltava a view (boss-fantasma vivo + câmera travada
            // na corrida seguinte, mesma cena). O fix dispara StateExited do estado corrente ANTES
            // de recriar a pilha. Aqui: chegar à luta, abandonar via RestartLevelFromAnyState e
            // exigir que StateExited(BossFight) tenha disparado e o boss tenha sido solto.
            LevelConfigSO level = LoadAsset<LevelConfigSO>(Level001Path);
            BuildRig("Rig_Abandono");

            bool bossFightExited = false;
            Action<GameState> onExited = s => { if (s == GameState.BossFight) bossFightExited = true; };
            _rig.Gm.StateExited += onExited;

            try
            {
                _rig.Pilot.Configure(AutoPilot.SideStrategy.BestExpectedValue, 1234);
                _rig.Pilot.Active = true;
                Time.timeScale = 8f;
                _rig.StartLevel(level);

                yield return WaitUntilOrTimeout(
                    () => _rig.Gm.State == GameState.BossFight, 60f, "entrar na luta de boss");
                Assert.IsNotNull(BossManager.Instance.Current, "Luta não armou o BossRuntime.");

                // abandono mid-luta (mesmo caminho do botão REINICIAR FASE da pausa)
                _rig.Pilot.Active = false;
                bossFightExited = false;                 // ignora qualquer saída anterior
                _rig.Gm.RestartLevelFromAnyState(level);
                yield return null;

                Assert.IsTrue(bossFightExited,
                    "RestartLevelFromAnyState não disparou StateExited(BossFight) — boss-fantasma não seria liberado.");
                // após o restart o fluxo refaz MainMenu→BossScout→…; a luta anterior foi solta
                Assert.AreNotEqual(GameState.BossFight, _rig.Gm.State,
                    "Após o abandono o estado deveria ter saído de BossFight.");
            }
            finally
            {
                _rig.Gm.StateExited -= onExited;
            }
        }

        // ------------------------------------------------------------------ infra (padrão GameLoopPlayModeTests)

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
