using UnityEngine;

namespace MutantArmy.Core
{
    /// <summary>Config de mundo (doc 12 §5.1; CANON §7 — MVP: 3 mundos enxutos, 20 fases).</summary>
    [CreateAssetMenu(menuName = "MutantArmy/World")]
    public class WorldConfigSO : ScriptableObject
    {
        public int worldIndex;
        public string displayNameKey;          // "Campo Inicial", "Cidade Zumbi"...
        public LevelConfigSO[] levels;
        public BossConfigSO worldBoss;         // fase 10 (fase 7 no MVP p/ M1)
        public Material skyboxMaterial;
        public GameObject[] trackSegmentPrefabs;                  // tema visual da pista
        public AudioClip musicTrack;
        public RewardConfigSO worldClearReward;                   // boss de mundo: 10 gemas + baú
    }
}
