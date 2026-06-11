using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Core
{
    /// <summary>
    /// Config de raridade (doc 12 §5.1). Cores canônicas (CANON §8): Comum cinza/azul claro ·
    /// Raro azul · Épico roxo · Lendário dourado.
    /// </summary>
    [CreateAssetMenu(menuName = "MutantArmy/Rarity")]
    public class RarityConfigSO : ScriptableObject
    {
        public Rarity rarity;
        public Color frameColor;
        public Color glowColor;
        public float statPremium = 1.15f;      // +10–20% por raridade sobre o baseline/Supply (CANON §5)
        public int chestWeight;                // peso de sorteio em baús
    }
}
