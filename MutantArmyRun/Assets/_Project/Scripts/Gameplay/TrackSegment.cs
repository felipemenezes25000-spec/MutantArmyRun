using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Segmento-prefab "burro" da pista (doc 12 §4.11): geometria + âncoras de pontos de
    /// interesse, zero decisão de conteúdo — quem decide é o LevelManager, populando as
    /// âncoras a partir do LevelConfigSO com a seed determinística da fase.
    /// </summary>
    public class TrackSegment : MonoBehaviour
    {
        public Transform[] gatePairAnchors;    // onde um GatePair L/R pode nascer
        public Transform[] obstacleAnchors;
        public float length = 30f;

        /// <summary>Z mundial onde o segmento termina — usado na reciclagem por distância.</summary>
        public float EndZ { get; set; }
    }
}
