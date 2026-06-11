namespace MutantArmy.Domain
{
    /// <summary>
    /// Efeito de portal como função pura (doc 12 §4.3): o portal entrega um TOTAL-ALVO
    /// e o CrowdManager reconcilia atual→alvo. Aplicar multiplicador como delta é o
    /// bug "x2 que triplica" — por isso a semântica aqui é sempre de total.
    /// </summary>
    public static class GateMath
    {
        /// <summary>Retorna o novo total do exército; nunca menor que 1 (doc 04 §7.1).</summary>
        public static int Apply(GateType type, float value, int current)
        {
            switch (type)
            {
                case GateType.AddFlat:
                    return System.Math.Max(1, current + (int)value);
                case GateType.Multiply:
                    // ÷2 = value 0.5; ímpar arredonda a favor do jogador: ⌈n/2⌉ (doc 04)
                    return System.Math.Max(1, (int)System.Math.Ceiling(current * (double)value));
                default:
                    // Element/Mutation/ClassConvert/Risk não mudam a contagem aqui
                    return current;
            }
        }
    }
}
