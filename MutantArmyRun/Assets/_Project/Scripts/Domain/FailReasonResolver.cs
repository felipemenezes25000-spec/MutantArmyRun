namespace MutantArmy.Domain
{
    /// <summary>
    /// Resolve o motivo RICO de derrota (missão Nota 10) a partir de uma fotografia da corrida.
    /// Mesmo formato do InterstitialPolicy: função pura, o chamador (GameManager na transição
    /// para Defeat) coleta o contexto e o Domain só decide. Em derrota NUNCA retorna None —
    /// a cadeia de prioridade termina em LowUpgradePower (dica acionável padrão: "evolua seus
    /// upgrades"), então a ResultScreen sempre tem algo útil para mostrar.
    /// </summary>
    public static class FailReasonResolver
    {
        /// <summary>
        /// Contexto da derrota. Campos minúsculos preenchidos pelos managers que já medem
        /// cada dado (CrowdManager: perdas/pico; BossRuntime: laser/dano; LevelManager: hazard).
        /// </summary>
        public struct DefeatContext
        {
            public int armySizeAtBossStart;     // exército ao ENTRAR na arena
            public int unitsLostOnTrack;        // perdas na pista (obstáculos/inimigos)
            public int armyPeak;                // maior tamanho do exército na corrida
            public float damageDealtToBoss;     // dano total causado ao boss
            public float bossMaxHp;
            public bool bossUsedLaser;          // o especial do boss conectou nesta luta
            public bool armyHadResistedElement; // elemento dominante do exército era resistido pelo boss
            public bool diedOnTrack;            // exército zerou ainda NA PISTA (nem chegou ao boss)
            public bool diedToHazard;           // golpe fatal veio de hazard (lava/laser de pista)
        }

        /// <summary>
        /// Prioridade fixa (contrato Onda 1): a razão mais ACIONÁVEL vence — primeiro erros de
        /// escolha (elemento), depois causa direta da morte (laser/lava), depois gestão de
        /// exército (perdas/tamanho), depois dano insuficiente, e por fim o fallback de meta.
        /// </summary>
        public static FailReason Resolve(DefeatContext c)
        {
            if (c.armyHadResistedElement)
                return FailReason.WrongElement;

            // Laser só é "a causa" se a morte foi na arena — quem morreu na pista nunca viu o especial.
            if (c.bossUsedLaser && !c.diedOnTrack)
                return FailReason.HitByLaser;

            if (c.diedToHazard)
                return FailReason.HitByLava;

            // Pico mínimo de 10 evita acusar "perdeu demais" em corrida que nunca cresceu.
            if (c.armyPeak >= 10 && c.unitsLostOnTrack >= c.armyPeak / 2)
                return FailReason.TooManyLossesOnTrack;

            if (c.armySizeAtBossStart < 10)
                return FailReason.ArmyTooSmall;

            if (c.damageDealtToBoss < c.bossMaxHp * 0.5f)
                return FailReason.BossResistedDamage;

            return FailReason.LowUpgradePower;
        }
    }
}
