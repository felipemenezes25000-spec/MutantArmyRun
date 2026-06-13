using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Core
{
    /// <summary>
    /// Config de inimigo DE PISTA (missão Nota 10): horda fraca, tanque, atirador e curador
    /// (TrackEnemyKind). Mora em Core/SO porque LevelConfigSO referencia (Core não enxerga
    /// Gameplay); estado vivo fica no TrackEnemyManager — SO é READ-ONLY em runtime.
    /// Conteúdo nasce na FACTORY (MvpContentFactory), nunca em .asset manual (regra 7).
    /// </summary>
    [CreateAssetMenu(menuName = "MutantArmy/Enemy")]
    public class EnemyConfigSO : ScriptableObject
    {
        public string enemyId;
        public string displayName;                          // nome amigável PT-BR já resolvido (padrão BossConfigSO)
        public TrackEnemyKind kind;
        public float maxHp = 10f;
        public float dps = 1f;
        public float moveSpeed = 0f;                        // 0 = estacionário (a pista vem até ele)
        public ElementType element = ElementType.None;
        public BodyType bodyType = BodyType.Organic;
        public int rewardCoins = 1;                         // drop por kill (TrackEnemyKilled.coins)
        public int worldIndex = 1;                          // mundo temático (1..10)
        public float attackRange = 2f;                      // Ranged usa >6 (ataca antes do contato)
        public float healPerSecond = 0f;                    // Healer: cura aliados vivos por segundo
        public GameObject prefab;                           // null → cápsula/cubo fallback tintado por kind (greybox)
    }
}
