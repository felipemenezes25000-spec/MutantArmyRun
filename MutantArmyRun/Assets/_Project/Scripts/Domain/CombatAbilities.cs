namespace MutantArmy.Domain
{
    /// <summary>
    /// Catálogo de habilidades especiais por id (UnitConfigSO.specialAbilityId), como dado puro
    /// (doc 03 §3.2). Tudo AGREGADO (doc 12 §4.4): nada por unidade — o CrowdManager soma a parcela
    /// ofensiva no DPS do exército e o CombatSystem aplica as de suporte por tick. Números canônicos
    /// do doc 03 §3.2/§4; ajustáveis por Remote Config (chaves rc_* listadas no doc 03 §6) — aqui
    /// ficam só os FALLBACKS de fábrica (o jogo é jogável offline).
    /// </summary>
    public static class CombatAbilities
    {
        // ids canônicos (doc 12 §5.1) — ESTES SÃO OS IDS QUE A FACTORY DE CONTEÚDO ATRIBUI
        // em UnitConfigSO.specialAbilityId. Se a factory mudar um id, atualize aqui (avisos_integracao).
        public const string HealAllies = "heal_allies";     // Médico / Anjo de Guerra (Triagem / Aura Solar)
        public const string ReviveDead = "revive_dead";     // Necromante (Levantar)
        public const string BuildTurret = "build_turret";   // Engenheiro (Torreta Mk-1)
        public const string DodgeTraps = "dodge_traps";     // Corredor / Ninja (esquiva de armadilhas da pista)

        // ---- Armadilhas/obstáculos da pista (doc 12 §4.11) ----
        // Quando o exército passa por um obstáculo, uma FRAÇÃO das unidades na faixa de impacto
        // é eliminada. Tropas com DodgeTraps (Corredor/Ninja) reduzem a perda (esquiva) e a
        // trilha de meta ObstacleResist (RunStartBonuses.obstacleLossFactor) também reduz —
        // os dois multiplicam sobre a perda-base. Números canônicos como dado puro (testável).
        public const float ObstacleBaseLossFraction = 0.25f;   // 25% das unidades da faixa por obstáculo (greybox)
        public const float DodgeTrapsMaxReduction = 0.6f;      // exército 100% esquivo perde até 60% menos

        /// <summary>
        /// Fração das unidades da faixa de impacto eliminada por UM obstáculo (doc 12 §4.11),
        /// já combinada com a esquiva (DodgeTraps) e o fator de meta ObstacleResist.
        /// <paramref name="dodgeRatio"/> = unidades esquivas / total vivo (0..1); reduz a perda
        /// linearmente até <see cref="DodgeTrapsMaxReduction"/>. <paramref name="lossFactor"/> é o
        /// RunStartBonuses.obstacleLossFactor (1 = sem bônus; &lt;1 reduz; clamp ≥0). Resultado em [0,1].
        /// </summary>
        public static float ObstacleLossFraction(float baseFraction, float dodgeRatio, float lossFactor)
        {
            if (baseFraction <= 0f) return 0f;
            float dodge = 1f - DodgeTrapsMaxReduction * Clamp01(dodgeRatio);
            float factor = lossFactor < 0f ? 0f : lossFactor;   // meta nunca aumenta a perda além do dado
            float result = baseFraction * dodge * factor;
            return Clamp01(result);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        // Habilidades de DANO em área/contínuo: somam um uplift de DPS no agregado (OffenseBonusDps).
        public const string AreaDamage = "area_damage";     // Mecha (Arsenal Total)
        public const string ArcaneNova = "arcane_nova";     // Mago (Nova Arcana: pulso 3× DPS a cada 8 s)
        public const string SeismicSlam = "seismic_slam";   // Gigante/Titã (Pancada Sísmica, golpe em área)
        public const string DotFire = "dot_fire";           // Lança-Chamas (Cone Ígneo: queimadura)
        public const string DotShadow = "dot_shadow";       // Demônio Mutante (dano contínuo de Sombra)
        public const string Chain = "chain";                // Alien/Raio (encadeia)
        public const string Slow = "slow";                  // Tropa Glacial (Rajada Gélida)

        // ---- Cura agregada (heal_allies) ----
        public const float MedicHealPerSecond = 8f;         // doc 03 §3.2: "cura 8 HP/s"
        public const float AngelHealPerSecond = 12f;        // doc 03 §3.2: "Aura Solar: 12 HP/s"

        // ---- Revive (revive_dead) — Necromante: a cada 6 s revive até 3 caídos, Supply total ≤6 ----
        public const float ReviveIntervalSeconds = 6f;
        public const int ReviveCountPerProc = 3;
        public const int ReviveSupplyBudget = 6;
        public const float ReviveHpFraction = 0.6f;         // revive com 60% HP / 100% DPS

        // ---- Torreta (build_turret) — Engenheiro: HP 30 · DPS 25 · constrói em 1,5 s ----
        public const float TurretBuildSeconds = 1.5f;
        public const float TurretDps = 25f;
        public const float TurretHp = 30f;

        /// <summary>
        /// Parcela de DPS CONTÍNUO das habilidades de DANO de uma unidade, somada ao baseDps no
        /// agregado (doc 12 §4.4). Habilidades de suporte (cura/revive/torreta) retornam 0 — elas
        /// rodam por tick no CombatSystem, não somam dano direto. Modeladas como pulso ÷ intervalo
        /// para virar um DPS médio estável (o agregado não simula cada pulso).
        /// </summary>
        public static float OffenseBonusDps(string abilityId, float baseDps)
        {
            if (string.IsNullOrEmpty(abilityId)) return 0f;
            switch (abilityId)
            {
                // Nova Arcana (Mago): pulso em área de 3× o DPS a cada 8 s ⇒ +~0,375× DPS médio.
                // Mecha (Arsenal) e Pancada Sísmica (Gigante/Titã) têm parcela de ÁREA equivalente.
                // Uplift agregado modesto e estável — o card vende "área", o agregado a entrega.
                case ArcaneNova:
                case AreaDamage:
                case SeismicSlam: return baseDps * 0.375f;
                // DoTs (Cone Ígneo, Sombra): queimadura/corrosão contínua — uplift pequeno e estável.
                case DotFire:
                case DotShadow: return baseDps * 0.15f;
                default: return 0f;
            }
        }

        /// <summary>HP/s de cura por id de habilidade (0 se não cura). Fallback de fábrica.</summary>
        public static float HealPerSecond(string abilityId)
        {
            // distinção Médico vs Anjo pelo próprio ALCANCE de DPS não é confiável; o id é o mesmo
            // ("heal_allies"). O número fino (8 vs 12) vem do Remote Config por unitId no CombatSystem;
            // este fallback usa o valor do Médico (mais comum, MVP-friendly).
            return abilityId == HealAllies ? MedicHealPerSecond : 0f;
        }
    }
}
