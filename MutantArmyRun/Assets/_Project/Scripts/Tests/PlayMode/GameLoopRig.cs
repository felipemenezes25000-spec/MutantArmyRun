using System.Collections;
using System.Reflection;
using MutantArmy.Core;
using MutantArmy.Domain;
using MutantArmy.Gameplay;
using MutantArmy.Meta;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MutantArmy.Tests
{
    /// <summary>
    /// Bootstrap mínimo do loop de jogo para PlayMode, montado por código numa cena
    /// aditiva descartável. Espelha os DOIS composition roots reais (doc 12 §3.3):
    /// GameBootstrap (GameManager → Save → Economy) e GameSceneBootstrap
    /// (Level → Enemies → Crowd → Gate → Boss → Combat → Combo → Risk → Anchor —
    /// ordem do ProjectSetup.CreateGameScene, missão Nota 10), chamando os MESMOS
    /// Init() na MESMA ordem. Fica de fora só o que é apresentação null-safe nos
    /// managers (CrowdRenderer/VFX/UI) — é isso que mantém o rig 100% batchmode
    /// -nographics: nenhum assert visual, nenhuma chamada de render.
    /// </summary>
    internal sealed class GameLoopRig
    {
        public GameManager Gm { get; private set; }
        public SaveSystem Save { get; private set; }
        public EconomySystem Economy { get; private set; }
        public CrowdAnchor Anchor { get; private set; }
        public LevelManager Level { get; private set; }
        public TrackEnemyManager Enemies { get; private set; }
        public CrowdManager Crowd { get; private set; }
        public GateManager Gates { get; private set; }
        public BossManager Boss { get; private set; }
        public CombatSystem Combat { get; private set; }
        public ComboSystem Combo { get; private set; }
        public RiskResolver Risk { get; private set; }
        public AutoPilot Pilot { get; private set; }

        private Scene _scene;
        private Scene _previousActive;
        private bool _built;

        public void Build(string sceneName, ElementChartSO chart, UnitConfigSO defaultUnit)
        {
            Assert.IsFalse(_built, "GameLoopRig.Build chamado duas vezes.");
            _built = true;

            // Cena aditiva própria: TODO objeto criado pelo rig (e pelos pools dos
            // managers, que instanciam na cena ativa) morre junto no Unload.
            _previousActive = SceneManager.GetActiveScene();
            _scene = SceneManager.CreateScene(sceneName);
            SceneManager.SetActiveScene(_scene);

            // ---- managers persistentes (ordem do GameBootstrap §3.3) ----
            Gm = NewManager<GameManager>("GameManager");
            Gm.Init();

            Save = NewManager<SaveSystem>("SaveSystem");
            Save.Init();

            Economy = NewManager<EconomySystem>("EconomySystem");
            Economy.Init();

            // ---- managers da cena Game (ordem do GameSceneBootstrap §3.3) ----
            Anchor = NewManager<CrowdAnchor>("CrowdAnchor");

            Level = NewManager<LevelManager>("LevelManager");
            Level.Init();

            // Inimigos de pista (missão Nota 10): APÓS Level e ANTES de Crowd/Combat, como no
            // ProjectSetup.CreateGameScene. Zero wiring: os [SerializeField] dele são tuning
            // com defaults válidos e as views nascem de primitivos (greybox batchmode-safe).
            Enemies = NewManager<TrackEnemyManager>("TrackEnemyManager");
            Enemies.Init();

            Crowd = NewManager<CrowdManager>("CrowdManager");
            SetPrivateField(Crowd, "_chart", chart);
            SetPrivateField(Crowd, "_defaultUnit", defaultUnit);
            Crowd.Init();

            Gates = NewManager<GateManager>("GateManager");
            SetPrivateField(Gates, "_pairPrefab", BuildGatePairTemplate());
            Gates.Init();

            Boss = NewManager<BossManager>("BossManager");
            Boss.Init();

            Combat = NewManager<CombatSystem>("CombatSystem");
            SetPrivateField(Combat, "_chart", chart);
            Combat.Init();

            // Combos (missão Nota 10): APÓS Combat, como no ProjectSetup — na morte do boss
            // ele fotografa CrowdManager/BossManager/CombatSystem (contrato W2-C §5).
            // Sem campos serializados: deps chegam por singleton/bus em runtime.
            Combo = NewManager<ComboSystem>("ComboSystem");
            Combo.Init();

            Risk = NewManager<RiskResolver>("RiskResolver");
            Risk.Init();

            // Anchor por último, como na cena Game real: o Init dele cria o trigger-proxy.
            Anchor.Init();

            Pilot = Anchor.gameObject.AddComponent<AutoPilot>();   // nasce desligado por padrão
        }

        /// <summary>
        /// Dirige o fluxo REAL de início de fase: MainMenu → StartLevel (BossScout) → Running.
        /// Sem UIManager no rig não existe o cartão do Boss Scout (OVL-01) para disparar o
        /// onDone — o teste executa a transição que o cartão executaria.
        /// </summary>
        public void StartLevel(LevelConfigSO level)
        {
            Gm.ChangeState(GameState.MainMenu);
            Gm.StartLevel(level);
            Gm.ChangeState(GameState.Running);
        }

        public IEnumerator Unload()
        {
            if (!_built) yield break;
            if (_previousActive.IsValid() && _previousActive.isLoaded)
                SceneManager.SetActiveScene(_previousActive);
            if (_scene.IsValid() && _scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_scene);
        }

        // ------------------------------------------------------------------ helpers

        private static T NewManager<T>(string name) where T : Component
        {
            GameObject go = new GameObject(name);
            return go.AddComponent<T>();
        }

        /// <summary>
        /// Liga campos [SerializeField] privados por reflexão — o equivalente runtime do
        /// wiring que o ProjectSetup faz por SerializedObject no editor. Campo inexistente
        /// é falha EXPLÍCITA do teste: significa que o contrato do manager mudou.
        /// </summary>
        internal static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field,
                target.GetType().Name + " não tem mais o campo serializado '" + fieldName +
                "' — atualizar o rig de PlayMode.");
            field.SetValue(target, value);
        }

        /// <summary>
        /// "Prefab" de par de portais construído em código (inativo — Instantiate clona
        /// inativo, igual a um prefab desativado). Meios-portais em x=±1,8 com triggers
        /// ESTREITOS (1,2 m) e PROFUNDOS (6 m): estreitos para o proxy do exército nunca
        /// tocar os dois lados no mesmo step; profundos para o líder acelerado pelo
        /// AutoPilot (timeScale alto) não atravessar o trigger entre dois steps de física.
        /// </summary>
        private static GatePairView BuildGatePairTemplate()
        {
            GameObject root = new GameObject("GatePairTemplate");
            root.SetActive(false);
            GatePairView pair = root.AddComponent<GatePairView>();
            GateView left = BuildGateHalf(root.transform, "GateL", -1.8f);
            GateView right = BuildGateHalf(root.transform, "GateR", 1.8f);
            SetPrivateField(pair, "_left", left);
            SetPrivateField(pair, "_right", right);
            return pair;
        }

        private static GateView BuildGateHalf(Transform parent, string name, float x)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, 1f, 0f);
            BoxCollider trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.2f, 3f, 6f);
            GateView view = go.AddComponent<GateView>();
            SetPrivateField(view, "_trigger", trigger);
            return view;
        }
    }
}
