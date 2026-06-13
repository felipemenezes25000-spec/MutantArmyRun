namespace MutantArmy.Domain
{
    public enum ElementType { None, Fire, Ice, Lightning, Poison, Light, Shadow, Metal, Alien }
    public enum Rarity { Common, Rare, Epic, Legendary }
    public enum GateType { AddFlat, Multiply, ClassConvert, Element, Mutation, Risk }
    public enum BodyType { Organic, Machine, Undead }
    public enum UpgradeTrack { StartDamage, StartHealth, Speed, RewardMultiplier,
                               StartArmy, CritChance, BossDamage, ObstacleResist }
    public enum GameState { Boot, MainMenu, BossScout, Running, BossFight, ReviveOffer, Victory, Defeat }
    public enum CurrencyType { Coin, Gem, Xp }
    // Ordinais ESTÁVEIS: None=0..Epic=3 preservam o que RewardConfigSO.chest já serializou;
    // Legendary/World entram no fim (contrato de telas RewardSystem.OpenChest, doc 07 §4).
    public enum ChestType { None, Common, Rare, Epic, Legendary, World }

    // ---- Enums da missão Nota 10 (CONTRACT §2) — append-only, ordinais são contrato ----

    // Classificação do multiplicador elemental (ElementChart) para o feedback
    // FRAQUEZA!/RESISTIU!/IMUNE! — limiares em WeaknessJudge (>1.05 / <0.95 / <=0).
    public enum ElementRelation { Neutral, Weakness, Resisted, Immune }

    // Combos de fim de corrida (ComboMath.Evaluate): cada um paga bônus de moedas próprio.
    public enum ComboKind { PerfectGate, WeaknessHit, BossBreaker, Clutch, NoLoss, Overkill }

    // Motivo RICO de derrota (FailReasonResolver) — substitui o DefeatReason de 3 valores
    // na tela de resultado; None reservado para vitória (derrota SEMPRE resolve um motivo).
    public enum FailReason { None, ArmyTooSmall, WrongElement, TooManyLossesOnTrack, BossResistedDamage,
                             NoTankUnits, NoAreaDamage, IgnoredHealers, HitByLava, HitByLaser, LowUpgradePower }

    // Arquétipos de inimigo de pista (EnemyConfigSO.kind) — horda fraca, tanque,
    // atirador de longa distância e curador de aliados.
    public enum TrackEnemyKind { WeakHorde, Tank, Ranged, Healer }
}
