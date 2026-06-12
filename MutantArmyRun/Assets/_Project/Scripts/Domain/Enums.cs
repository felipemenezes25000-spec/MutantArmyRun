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
}
