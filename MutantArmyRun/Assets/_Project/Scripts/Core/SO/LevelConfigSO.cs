using System;
using UnityEngine;

namespace MutantArmy.Core
{
    /// <summary>Slot de par de portais ao longo da pista (doc 12 §5.1/§4.3).</summary>
    [Serializable]
    public class GateSlot
    {
        public float trackPosition;
        public float depth01;                  // profundidade normalizada na fase (0 = início, 1 = arena)
        public bool autoBalance;               // true: GateManager monta o par contra o boss (rota ótima + armadilha)
        public GateConfigSO leftGate, rightGate;
    }

    /// <summary>Slot de obstáculo — respeita a zona de segurança pós-portal (doc 12 §4.11).</summary>
    [Serializable]
    public class ObstacleSlot
    {
        public float trackPosition;
        public GameObject prefab;
    }

    /// <summary>
    /// Config de fase (doc 12 §5.1). A cena Game é única para as 20+ fases: nível = este
    /// asset, nunca 1 cena por nível (doc 12 §2.2). SO é READ-ONLY em runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "MutantArmy/Level")]
    public class LevelConfigSO : ScriptableObject
    {
        public int levelIndex;                 // 1–20 no MVP
        public int seed;                       // pista determinística: mesma fase = mesma pista (§4.11); QA reproduz bug por seed
        public WorldConfigSO world;
        public float trackLength = 220f;       // ≈45–75 s a 4 m/s base
        public GateSlot[] gateSlots;           // posição + par de portais (ou autoBalance)
        public ObstacleSlot[] obstacles;
        public BossConfigSO boss;              // TODA fase termina em boss (CANON §6)
        public float bossHpMultiplier = 1f;    // escala da variante regional
        public RewardConfigSO winReward;
        public int startingUnits = 1;          // fase sempre começa com 1 + bônus de meta
    }
}
