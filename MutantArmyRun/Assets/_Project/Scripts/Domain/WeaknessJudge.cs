namespace MutantArmy.Domain
{
    /// <summary>
    /// Classificador do multiplicador elemental (ElementChart.GetMultiplier) para o feedback
    /// FRAQUEZA!/RESISTIU!/IMUNE! da missão Nota 10. O ElementChart só devolve o número;
    /// AQUI mora a decisão de rótulo — nunca duplicar estes limiares na UI/Gameplay.
    /// Janela morta de ±5% em volta de 1.0: multiplicadores quase-neutros (ruído de tuning
    /// via Remote Config) não geram texto, evitando spam de feedback irrelevante.
    /// </summary>
    public static class WeaknessJudge
    {
        public static ElementRelation Classify(float multiplier)
        {
            // Imune vem primeiro: 0 (ou negativo defensivo) é "não causou dano", nunca "resistiu".
            if (multiplier <= 0f) return ElementRelation.Immune;
            if (multiplier > 1.05f) return ElementRelation.Weakness;
            if (multiplier < 0.95f) return ElementRelation.Resisted;
            return ElementRelation.Neutral;
        }
    }
}
