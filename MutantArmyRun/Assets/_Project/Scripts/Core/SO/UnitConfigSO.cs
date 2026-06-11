using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Core
{
    /// <summary>
    /// Config de tropa (doc 12 §5.1; roster e baseline no CANON §5).
    /// SO é READ-ONLY em runtime: estado vivo mora nos arrays SoA do CrowdManager.
    /// </summary>
    [CreateAssetMenu(menuName = "MutantArmy/Unit")]
    public class UnitConfigSO : ScriptableObject
    {
        public string unitId;                  // "soldier", "archer"... (chave de save/analytics)
        public string displayNameKey;          // localização
        public Rarity rarity;
        public int supplyCost;                 // Soldado 1 · Mago 4 · Gigante 12 (CANON §5)
        public float baseHp, baseDps, moveSpeed, attackRange;     // baseline Soldado: 10/2/5
        public ElementType element;            // dano nativo (Lança-Chamas = Fire)
        public BodyType bodyType;
        public string specialAbilityId;        // "heal_allies", "dodge_traps", "build_turret"...
        public Mesh mesh;
        public Material material;              // material com VAT (doc 12 §6)
        public Texture2D vatTexture;           // animação assada (idle/run/attack)
        public Sprite cardIcon;
        public AnimationCurve levelHpCurve, levelDpsCurve;        // escala nv 1–10
    }
}
