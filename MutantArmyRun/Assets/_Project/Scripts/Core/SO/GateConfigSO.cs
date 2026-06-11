using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Core
{
    /// <summary>
    /// Config de portal (doc 12 §5.1; os 8 tipos do MVP no CANON §10). O rótulo do portal é
    /// SEMPRE renderizado de displayLabel/icon/portalColor — portais honestos (CANON §3.4).
    /// </summary>
    [CreateAssetMenu(menuName = "MutantArmy/Gate")]
    public class GateConfigSO : ScriptableObject
    {
        public string gateId;
        public GateType gateType;
        public float value;                    // +10/+25 → 10/25 · x2 → 2 · ÷2 → 0.5
        public UnitConfigSO unitToAdd;         // AddFlat/ClassConvert
        public ElementType element;            // Element gate
        public MutationConfigSO mutation;      // Mutation gate
        [Range(0f, 1f)] public float riskSuccessChance;  // Risco: ex. 0.7 → "70% x10 / 30% perde metade"
        public float riskRewardMult, riskFailPenalty;    // ex. 10 e 0.5
        public string displayLabel;            // texto honesto exibido no portal (CANON §3.4); renderizado via OnValidate (§4.3)
        public Sprite icon;
        public Color portalColor;

        /// <summary>
        /// Efeito como função pura int→int com semântica de TOTAL-ALVO (doc 12 §4.3) —
        /// delega ao Domain (<see cref="GateMath"/>, testado via dotnet test), que também
        /// carrega o piso de 1 unidade. O CrowdManager reconcilia atual→alvo; aplicar como
        /// delta é o bug "x2 que triplica".
        /// </summary>
        public int Apply(int current)
        {
            return GateMath.Apply(gateType, value, current);
        }
    }
}
