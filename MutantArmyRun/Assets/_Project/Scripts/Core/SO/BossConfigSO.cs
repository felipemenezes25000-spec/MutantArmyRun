using System;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Core
{
    /// <summary>
    /// Evento de wave da arena como DADO (doc 12 §4.5, decisão 1): lista ordenada por tempo,
    /// consumida por ponteiro de próximo evento — nunca polling "(int)timer == x".
    /// </summary>
    [Serializable]
    public class ArenaWaveEvent
    {
        public float time;
        public UnitConfigSO enemyType;
        public int count;
    }

    /// <summary>
    /// Config de boss (doc 12 §5.1; regras canônicas no CANON §6). Estado vivo (HP, fase,
    /// fraqueza rotativa) mora no BossRuntime — SO é READ-ONLY em runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "MutantArmy/Boss")]
    public class BossConfigSO : ScriptableObject
    {
        public string bossId, displayNameKey;
        public ElementType element;
        public ElementType[] weaknesses;       // Zumbi Titã: Fire + Light
        public ElementType[] immunities;       // Zumbi Titã: Poison
        public bool rotatingWeakness;          // Alien Supremo: troca a cada 25% de HP
        public BodyType bodyType;
        public float maxHp, contactDps;
        public float entranceSeconds = 2f;     // CANON §6: ≤ 2 s
        public float telegraphSeconds = 1f;    // janela de leitura do especial
        public float specialAttackDamage;
        public float specialAttackArea;
        public float specialBaseCooldown;
        public ArenaWaveEvent[] arenaWaves;    // lista ORDENADA por tempo; consumida por ponteiro (§4.5)
        public GameObject prefab;
        public Sprite scoutCardArt;            // arte do Boss Scout (CANON §3.1)
        public RewardConfigSO killReward;      // gemas + chance de carta/fragmento
    }
}
