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
    }
}
