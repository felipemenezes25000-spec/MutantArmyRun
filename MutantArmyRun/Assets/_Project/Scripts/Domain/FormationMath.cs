using System;

namespace MutantArmy.Domain
{
    /// <summary>
    /// Vetor 2D próprio do Domain (regra: zero UnityEngine aqui). Campos minúsculos
    /// (x, y) de propósito — espelham Vector2 para a camada Unity mapear 1:1.
    /// </summary>
    public struct Float2
    {
        public float x;
        public float y;

        public Float2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }
    }

    /// <summary>
    /// Formação filotáxica (girassol/espiral de Vogel) do doc 12 §4.2: offset de slot
    /// calculado O(1) por índice — n arbitrário até o teto de Supply, sem capacidade
    /// precomputada. Raio cresce com √n; ângulo de ouro evita sobreposição.
    /// </summary>
    public static class FormationMath
    {
        private const float Spacing = 0.45f;
        private const float Golden = 2.39996f; // 137,5° em radianos

        public static Float2 GetSlotOffset(int slotIndex)
        {
            float r = Spacing * MathF.Sqrt(slotIndex + 1);
            return new Float2(r * MathF.Cos(slotIndex * Golden), r * MathF.Sin(slotIndex * Golden));
        }
    }
}
