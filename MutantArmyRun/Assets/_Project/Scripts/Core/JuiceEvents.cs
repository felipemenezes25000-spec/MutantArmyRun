using System;
using UnityEngine;

namespace MutantArmy.Core
{
    /// <summary>
    /// Bus de eventos COSMÉTICOS de juice — separado do GameEvents (que é estado de jogo,
    /// doc 12 §3.2) para não poluir o contrato de dados. Existe porque Gameplay e Services
    /// não se enxergam (fronteira de asmdef, doc 12 §2.3): o JuiceController (Gameplay)
    /// detecta o pulso de hit no boss e o AudioManager (Services) toca o SFX assinando aqui.
    /// Mesmas regras do bus principal: estático sobrevive a cenas — limpar em OnDisable.
    /// </summary>
    public static class JuiceEvents
    {
        /// <summary>Pulso de feedback de dano no boss (rate-limited na origem): posição de mundo.</summary>
        public static event Action<Vector3> OnBossHitPulse;

        public static void RaiseBossHitPulse(Vector3 worldPosition) => OnBossHitPulse?.Invoke(worldPosition);

        /// <summary>
        /// Exército atingido por um obstáculo/armadilha da pista (doc 12 §4.11): posição de mundo.
        /// O AudioManager (Services) assina e toca a explosão — Gameplay não enxerga Services (§2.3).
        /// </summary>
        public static event Action<Vector3> OnObstacleHit;

        public static void RaiseObstacleHit(Vector3 worldPosition) => OnObstacleHit?.Invoke(worldPosition);
    }
}
