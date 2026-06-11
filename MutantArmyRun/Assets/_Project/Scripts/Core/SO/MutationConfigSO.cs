using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Core
{
    /// <summary>
    /// Config de mutação (doc 12 §5.1; CANON §3.3: persistentes, visíveis no exército inteiro,
    /// máximo 3 slots — a 4ª substitui a mais antiga).
    /// </summary>
    [CreateAssetMenu(menuName = "MutantArmy/Mutation")]
    public class MutationConfigSO : ScriptableObject
    {
        public string mutationId, displayNameKey;    // "wings", "laser", "armor", "size"...
        public Rarity rarity;
        public float dpsMult = 1f, hpMult = 1f, speedMult = 1f, sizeMult = 1f;
        public bool grantsFlight;                    // asas: ignora obstáculos de chão
        public ElementType addsElement;              // laser pode adicionar dano de Raio
        public int shaderVariantFlag;                // bit usado pelo shader VAT p/ trocar visual (doc 12 §6.2)
        public GameObject attachmentPrefab;          // acessório instanciado só em unidades "hero" próximas à câmera
        public Sprite hudIcon;                       // slot de mutação no HUD (máx. 3)
    }
}
