using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Zona de perigo do portal de risco (CANON §10: "x10 se sobreviver à zona de perigo").
    /// O desfecho é resolvido pelo Domain.RiskGate com RNG INJETADO (doc 12 §4.3): as odds
    /// usadas são exatamente as exibidas no rótulo (CANON §3.4, portais honestos) e a seed
    /// derivada da fase torna o resultado reproduzível em QA. A zona em si é a dramatização:
    /// um Countdown puro do Domain segura o suspense antes do veredito.
    /// </summary>
    public class RiskResolver : MonoBehaviour, IInitializable
    {
        public static RiskResolver Instance { get; private set; }

        [SerializeField] private float _zoneSeconds = 1.5f;
        [SerializeField] private GameObject _zoneVisual;   // decal/VFX da zona; opcional até a arte existir

        private readonly Countdown _zone = new Countdown();   // Domain: testável com dt sintético
        private GateConfigSO _activeGate;
        private System.Random _rng = new System.Random();

        public void Init()   // chamado pelo bootstrap da cena Game (doc 12 §3.3)
        {
            Instance = this;
            if (_zoneVisual != null) _zoneVisual.SetActive(false);
        }

        /// <summary>
        /// Seed derivada da fase, injetada pelo LevelManager. RNG separado do RNG da pista:
        /// compartilhar o mesmo System.Random quebraria o determinismo dos segmentos.
        /// </summary>
        public void Configure(System.Random rng)
        {
            if (rng != null) _rng = rng;
        }

        public static void Begin(GateConfigSO gate)   // assinatura do contrato (doc 12 §4.3)
        {
            if (Instance != null) Instance.BeginInternal(gate);
        }

        private void BeginInternal(GateConfigSO gate)
        {
            if (gate == null || _activeGate != null) return;   // 1 zona por vez
            _activeGate = gate;
            _zone.Set(_zoneSeconds);
            if (_zoneVisual != null) _zoneVisual.SetActive(true);
        }

        private void Update()
        {
            if (_activeGate == null) return;

            _zone.Tick(Time.deltaTime);
            if (!_zone.Done) return;

            GateConfigSO gate = _activeGate;
            _activeGate = null;
            if (_zoneVisual != null) _zoneVisual.SetActive(false);

            CrowdManager crowd = CrowdManager.Instance;
            if (crowd == null) return;

            // Domain decide: sucesso → ×rewardMult; falha → ×failPenalty com piso 1.
            // Mesma semântica de TOTAL-ALVO dos demais portais — reconciliada no funil único.
            int target = RiskGate.Resolve(
                _rng, gate.riskSuccessChance, gate.riskRewardMult, gate.riskFailPenalty, crowd.Count);
            crowd.ReconcileTo(target, gate.unitToAdd);
        }
    }
}
