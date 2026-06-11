using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Core
{
    /// <summary>
    /// Config de trilha de upgrade de meta (doc 12 §5.1; CANON §9: 8 trilhas, +5%/nível,
    /// custo 100 × 1,35^n). As curvas em si são funções puras do Domain (EconomyMath) —
    /// o UpgradeSystem injeta costBase/costGrowth deste asset lá.
    /// </summary>
    [CreateAssetMenu(menuName = "MutantArmy/Upgrade")]
    public class UpgradeConfigSO : ScriptableObject
    {
        public UpgradeTrack track;
        public string displayNameKey;
        public Sprite icon;
        public float bonusPerLevel = 0.05f;    // +5%/nível (StartArmy: +1 unidade a cada 2 níveis)
        public int maxLevel = 50;
        public float costBase = 100f, costGrowth = 1.35f;         // custo(n) = 100 × 1,35^n
        public bool inMvp;                     // 4 trilhas true no MVP (CANON §9)
    }
}
