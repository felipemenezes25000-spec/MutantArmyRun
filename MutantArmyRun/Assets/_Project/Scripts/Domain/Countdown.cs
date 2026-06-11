using System;

namespace MutantArmy.Domain
{
    /// <summary>
    /// Timer puro de contagem regressiva (doc 12 §4.5) — sem MonoBehaviour: o dono chama
    /// Tick(dt) externamente, o que torna telegraphs e cooldowns testáveis com dt sintético.
    /// Invariante: Remaining nunca fica negativo; Tick antes de Set é no-op seguro.
    /// </summary>
    public sealed class Countdown
    {
        public float Remaining { get; private set; }

        public bool Done => Remaining <= 0f;

        public void Set(float seconds)
        {
            // Clamp no Set preserva o invariante "nunca negativo" mesmo com entrada inválida.
            Remaining = MathF.Max(0f, seconds);
        }

        public void Tick(float dt)
        {
            Remaining = MathF.Max(0f, Remaining - dt);
        }
    }
}
