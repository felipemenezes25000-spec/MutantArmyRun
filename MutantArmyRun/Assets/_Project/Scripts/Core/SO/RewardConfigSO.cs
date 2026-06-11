using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Core
{
    /// <summary>Config de recompensa — vitória, baús, drop de boss (doc 12 §5.1; CANON §8).</summary>
    [CreateAssetMenu(menuName = "MutantArmy/Reward")]
    public class RewardConfigSO : ScriptableObject
    {
        public int coins, gems, playerXp;
        public ChestType chest;                // None/Common/Rare/Epic
        [Range(0f, 1f)] public float cardDropChance;
        public UnitConfigSO[] cardPool;        // de qual pool a carta/fragmento sai
        public int shardAmount;
        public bool allowAdDouble = true;      // "dobrar com anúncio" na tela de vitória (CANON §11)
    }
}
