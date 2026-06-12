using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MutantArmy.Core;
using MutantArmy.Domain;

namespace MutantArmy.Editor
{
    /// <summary>
    /// MAR Tools/Create MVP Content — gera por código os assets canônicos do jogo:
    /// 26 GateConfigSO (taxonomia ampliada, CANON §10 + doc 04: matemáticos, classe,
    /// elemento, mutação, risco) · 19 UnitConfigSO (roster completo CANON §5; os 5 do MVP
    /// intactos) · 9 MutationConfigSO (CANON §3.3) · 19 BossConfigSO (CANON §6: 10 arquétipos
    /// regionais + 10 bosses únicos de mundo, com os 5 do MVP preservados na sobreposição) ·
    /// ElementChartSO default (CANON §4) · 4 RarityConfigSO (CANON §8) · 4 UpgradeConfigSO
    /// (trilhas MVP, CANON §9) · 3 RewardConfigSO (CANON §8/§16) · 10 WorldConfigSO (CANON §7,
    /// worldIndex 1–10) · 100 LevelConfigSO (10 mundos × 10 fases) com seeds determinísticas,
    /// curva de dificuldade por fase/mundo e pares manuais curados nas fases-chave (doc 06 §8).
    /// Acima da fase 100 o GameSettingsSO gera fases proceduralmente (endless infinito, CANON §7).
    /// Idempotente: re-rodar atualiza os assets existentes.
    /// NOTA p/ runtime: o GateManager carrega TODOS os GateConfigSO da pasta no _autoBalancePool —
    /// negativos/risco/mutação entram nesse sorteio; ver avisos de integração para curar o pool.
    /// </summary>
    public static class MvpContentFactory
    {
        private const string Root = "Assets/_Project/ScriptableObjects";
        private const string UndoLabel = "Create MVP Content";

        private sealed class UnitSet
        {
            // Os 5 do MVP (não quebrar) — usados pelos gates/levels manuais abaixo.
            public UnitConfigSO Soldier, Archer, Shieldbearer, Mage, Giant;
            // Os 14 restantes do roster canônico (CANON §5) — adicionados nesta fase.
            public UnitConfigSO Runner, Ninja, FlameTrooper, FrostTrooper, Medic, Robot,
                                Necromancer, Engineer, Alien, Dragon, Titan, WarAngel, Demon, Mecha;
            // Tropas usadas como destino de portais de classe (ClassConvert).
            public UnitConfigSO Knight => Shieldbearer;
        }

        private sealed class GateSet
        {
            // MVP (CANON §10) — preservados; vários ainda referenciados por slots manuais.
            public GateConfigSO AddTen, AddTwentyFive, TimesTwo, TimesThree, Half, ClassArcher, ElementFire, RiskTen;
            // Matemáticos extra (taxonomia ampliada, doc 04).
            public GateConfigSO AddFifty, TimesFive, MinusTen, Div2Alt;
            // Classe (transformar o exército inteiro — gateType ClassConvert).
            public GateConfigSO ClassMage, ClassKnight, ClassNinja, ClassGiant;
            // Elemento (gateType Element — ciano).
            public GateConfigSO ElementIce, ElementLightning, ElementPoison;
            // Mutação (gateType Mutation — roxo; mutation = MutationConfigSO).
            public GateConfigSO MutWings, MutArmor, MutLaser, MutSize, MutSpeed, MutRegen;
            // Risco (dourado — odds honestas no rótulo).
            public GateConfigSO RiskTitan, RiskSacrifice;
        }

        private sealed class MutationSet
        {
            public MutationConfigSO Wings, Armor, Laser, Size, Speed, Clone, Regen, Shield, AreaBlast;
        }

        // 10 mundos × (1 arquétipo regional p/ fases 1–9 + 1 boss único p/ fase 10).
        // Os 5 do MVP (Golem/WoodGiant/Bruiser/Titan/Scorpion) continuam nos índices 0–2
        // intactos; M4–M10 são novos. arquetype[w]/final[w] por worldIndex 1..10.
        private sealed class BossSet
        {
            // MVP preservados (não quebrar W1/W2/W3 nem os 5 bosses).
            public BossConfigSO Golem, WoodGiant, Bruiser, Titan, Scorpion;
            // Arquétipos regionais (fases 1–9) por mundo, indexados por worldIndex 1..10.
            public readonly BossConfigSO[] Arquetype = new BossConfigSO[11];
            // Bosses ÚNICOS de mundo (fase 10) por worldIndex 1..10 — o contrato do visual.
            public readonly BossConfigSO[] Final = new BossConfigSO[11];
        }

        private sealed class RewardSet
        {
            public RewardConfigSO WorldBoss, BossDefault, Level10;
        }

        [MenuItem("MAR Tools/Create MVP Content")]
        public static void CreateAll()
        {
            EnsureFolder(Root + "/Units");
            EnsureFolder(Root + "/Gates");
            EnsureFolder(Root + "/Bosses");
            EnsureFolder(Root + "/Balance");
            EnsureFolder(Root + "/Mutations");
            EnsureFolder(Root + "/Upgrades");
            EnsureFolder(Root + "/Rewards");
            EnsureFolder(Root + "/Worlds");
            EnsureFolder(Root + "/Levels");

            UnitSet units = CreateUnits();
            MutationSet mutations = CreateMutations();
            GateSet gates = CreateGates(units, mutations);
            CreateElementChart();
            CreateRarities();
            CreateUpgrades();
            RewardSet rewards = CreateRewards(units);
            BossSet bosses = CreateBosses(rewards);
            List<LevelConfigSO> allLevels = CreateWorldsAndLevels(bosses, gates, rewards);
            CreateGameSettings(allLevels);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MAR Tools: conteúdo criado/atualizado — 25 portais, 19 tropas, 9 mutações, 19 bosses " +
                      "(10 arquétipos regionais + 10 bosses de mundo, com sobreposição dos 5 do MVP), " +
                      "chart elemental, 4 raridades, 4 trilhas de upgrade, 3 recompensas, 10 mundos, " +
                      "100 fases (+ endless procedural além da 100) e o catálogo Resources/GameSettings.asset.");
        }

        // ------------------------------------------------------------------ Units (CANON §5 · doc 03 §3.1/§4)

        // Baseline canônico (CANON §5): Soldado nv1 = HP10 / DPS2 / vel5 → 12 "pontos"
        // (HP+DPS) por 1 de Supply. As demais escalam por supply × prêmio de raridade
        // (Common 1.0 · Rare 1.15 · Epic 1.30 · Lendário 1.45), com a divisão HP/DPS ditada
        // pelo papel (tanque pende p/ HP, atirador p/ DPS). Os 5 do MVP ficam INTOCADOS
        // (valores afinados à mão) — só os 14 novos usam a banda calculada.
        private static UnitSet CreateUnits()
        {
            var set = new UnitSet
            {
                // ---- MVP (NÃO QUEBRAR — valores idênticos ao baseline travado) ----
                Soldier = ConfigureUnit("Unit_Soldier", "unit_soldier", "Soldado", Rarity.Common, 1,
                    hp: 10f, dps: 2f, speed: 5.0f, range: 1.5f, ability: "cohesion"),
                Archer = ConfigureUnit("Unit_Archer", "unit_archer", "Arqueiro", Rarity.Common, 2,
                    hp: 14f, dps: 8f, speed: 5.0f, range: 8.0f, ability: "sure_shot"),
                Shieldbearer = ConfigureUnit("Unit_Shieldbearer", "unit_shieldbearer", "Escudeiro", Rarity.Common, 3,
                    hp: 30f, dps: 4f, speed: 4.5f, range: 1.0f, ability: "shield_wall"),
                Mage = ConfigureUnit("Unit_Mage", "unit_mage", "Mago", Rarity.Rare, 4,
                    hp: 25f, dps: 16f, speed: 4.5f, range: 6.0f, ability: "arcane_nova"),
                Giant = ConfigureUnit("Unit_Giant", "unit_giant", "Gigante", Rarity.Epic, 12,
                    hp: 120f, dps: 40f, speed: 3.5f, range: 2.0f, ability: "seismic_slam")
            };

            // ---- COMUNS restantes (CANON §5) ----
            set.Runner = ConfigureUnit("Unit_Runner", "unit_runner", "Corredor", Rarity.Common, 1,
                hp: 7f, dps: 3f, speed: 6.5f, range: 1.2f, ability: "dodge_traps",
                element: ElementType.None, body: BodyType.Organic);   // rápido, desvia, frágil

            // ---- RAROS (CANON §5) ----
            set.Ninja = ConfigureUnit("Unit_Ninja", "unit_ninja", "Ninja", Rarity.Rare, 3,
                hp: 22f, dps: 19f, speed: 6.0f, range: 1.5f, ability: "dodge_traps",
                element: ElementType.None, body: BodyType.Organic);
            set.FlameTrooper = ConfigureUnit("Unit_FlameTrooper", "unit_flametrooper", "Lança-Chamas", Rarity.Rare, 4,
                hp: 30f, dps: 25f, speed: 4.5f, range: 3.0f, ability: "dot_fire",
                element: ElementType.Fire, body: BodyType.Organic);
            set.FrostTrooper = ConfigureUnit("Unit_FrostTrooper", "unit_frosttrooper", "Tropa Glacial", Rarity.Rare, 4,
                hp: 34f, dps: 21f, speed: 4.5f, range: 5.0f, ability: "slow",
                element: ElementType.Ice, body: BodyType.Organic);
            set.Medic = ConfigureUnit("Unit_Medic", "unit_medic", "Médico", Rarity.Rare, 4,
                hp: 38f, dps: 8f, speed: 4.8f, range: 4.0f, ability: "heal_allies",
                element: ElementType.None, body: BodyType.Organic);

            // ---- ÉPICOS (CANON §5) ----
            set.Robot = ConfigureUnit("Unit_Robot", "unit_robot", "Robô", Rarity.Epic, 8,
                hp: 90f, dps: 38f, speed: 4.0f, range: 3.0f, ability: "armor_plating",
                element: ElementType.None, body: BodyType.Machine);   // imune a Veneno via bodyType
            set.Necromancer = ConfigureUnit("Unit_Necromancer", "unit_necromancer", "Necromante", Rarity.Epic, 8,
                hp: 76f, dps: 30f, speed: 4.0f, range: 5.0f, ability: "revive_dead",
                element: ElementType.Shadow, body: BodyType.Organic);
            set.Engineer = ConfigureUnit("Unit_Engineer", "unit_engineer", "Engenheiro", Rarity.Epic, 8,
                hp: 84f, dps: 34f, speed: 4.0f, range: 4.0f, ability: "build_turret",
                element: ElementType.None, body: BodyType.Organic);
            set.Alien = ConfigureUnit("Unit_Alien", "unit_alien", "Alien", Rarity.Epic, 8,
                hp: 80f, dps: 36f, speed: 4.5f, range: 4.0f, ability: "chain",
                element: ElementType.Alien, body: BodyType.Organic);

            // ---- LENDÁRIOS (CANON §5) ----
            set.Dragon = ConfigureUnit("Unit_Dragon", "unit_dragon", "Dragão", Rarity.Legendary, 20,
                hp: 200f, dps: 110f, speed: 4.5f, range: 6.0f, ability: "flight",
                element: ElementType.Fire, body: BodyType.Organic);   // dano em área + voo
            set.Titan = ConfigureUnit("Unit_Titan", "unit_titan", "Titã", Rarity.Legendary, 25,
                hp: 320f, dps: 110f, speed: 3.0f, range: 2.5f, ability: "seismic_slam",
                element: ElementType.None, body: BodyType.Organic);   // enorme, forte, lento
            set.WarAngel = ConfigureUnit("Unit_WarAngel", "unit_warangel", "Anjo de Guerra", Rarity.Legendary, 18,
                hp: 210f, dps: 80f, speed: 5.0f, range: 6.0f, ability: "heal_allies",
                element: ElementType.Light, body: BodyType.Organic);  // cura + dano de Luz
            set.Demon = ConfigureUnit("Unit_Demon", "unit_demon", "Demônio Mutante", Rarity.Legendary, 20,
                hp: 230f, dps: 130f, speed: 4.2f, range: 3.0f, ability: "dot_shadow",
                element: ElementType.Shadow, body: BodyType.Organic); // dano brutal de Sombra
            set.Mecha = ConfigureUnit("Unit_Mecha", "unit_mecha", "Mecha Supremo", Rarity.Legendary, 25,
                hp: 300f, dps: 150f, speed: 3.5f, range: 7.0f, ability: "area_damage",
                element: ElementType.Lightning, body: BodyType.Machine); // laser + mísseis em área

            return set;
        }

        private static UnitConfigSO ConfigureUnit(string assetName, string unitId, string displayName,
                                                  Rarity rarity, int supply,
                                                  float hp, float dps, float speed, float range, string ability,
                                                  ElementType element = ElementType.None,
                                                  BodyType body = BodyType.Organic)
        {
            var unit = LoadOrCreate<UnitConfigSO>(Root + "/Units/" + assetName + ".asset");
            unit.unitId = unitId;
            unit.displayNameKey = unitId + "_name";
            unit.displayName = displayName;      // nome amigável PT-BR (CANON §5) — a UI exibe este campo
            unit.rarity = rarity;
            unit.supplyCost = supply;
            unit.baseHp = hp;
            unit.baseDps = dps;
            unit.moveSpeed = speed;
            unit.attackRange = range;
            unit.element = element;              // MVP mantém Soldado..Gigante neutros; novos podem ter elemento nativo
            unit.bodyType = body;
            unit.specialAbilityId = ability;
            unit.levelHpCurve = LevelCurve();
            unit.levelDpsCurve = LevelCurve();
            EditorUtility.SetDirty(unit);
            return unit;
        }

        /// <summary>Escala de nível 1–10: ×1,15^(n−1) para HP e DPS (doc 03 §5).</summary>
        private static AnimationCurve LevelCurve()
        {
            var keys = new Keyframe[10];
            for (int n = 1; n <= 10; n++)
                keys[n - 1] = new Keyframe(n, Mathf.Pow(1.15f, n - 1));
            return new AnimationCurve(keys);
        }

        // ------------------------------------------------------------------ Mutations (CANON §3.3 · doc 12 §5.1)

        // Aplicadas ao EXÉRCITO INTEIRO, 3 slots rotativos (a 4ª substitui a mais antiga).
        // O CrowdManager.RecomputeMutationMultipliers só multiplica dpsMult/hpMult/sizeMult
        // hoje; grantsFlight/addsElement/shaderVariantFlag são contratos para o runtime e o
        // shader VAT consumirem (ver avisos_integracao). Cada mutação tem um BIT distinto.
        private static MutationSet CreateMutations()
        {
            var set = new MutationSet();

            // asas: voo (ignora obstáculos de chão), leve ganho de velocidade.
            set.Wings = ConfigureMutation("Mutation_Wings", "mut_wings", "Asas", Rarity.Rare,
                dps: 1f, hp: 1f, speed: 1.15f, size: 1f, flight: true,
                element: ElementType.None, bit: 0);

            // armadura: +50% HP (CANON: hpMult 1.5).
            set.Armor = ConfigureMutation("Mutation_Armor", "mut_armor", "Armadura", Rarity.Rare,
                dps: 1f, hp: 1.5f, speed: 1f, size: 1.05f, flight: false,
                element: ElementType.None, bit: 1);

            // laser: adiciona dano de Raio + dpsMult 1.3 (CANON).
            set.Laser = ConfigureMutation("Mutation_Laser", "mut_laser", "Laser", Rarity.Epic,
                dps: 1.3f, hp: 1f, speed: 1f, size: 1f, flight: false,
                element: ElementType.Lightning, bit: 2);

            // tamanho: sizeMult 1.4, hpMult 1.3 (CANON), leve perda de velocidade.
            set.Size = ConfigureMutation("Mutation_Size", "mut_size", "Gigantismo", Rarity.Epic,
                dps: 1.15f, hp: 1.3f, speed: 0.95f, size: 1.4f, flight: false,
                element: ElementType.None, bit: 3);

            // velocidade: speedMult 1.4 (CANON).
            set.Speed = ConfigureMutation("Mutation_Speed", "mut_speed", "Velocidade", Rarity.Rare,
                dps: 1f, hp: 1f, speed: 1.4f, size: 0.95f, flight: false,
                element: ElementType.None, bit: 4);

            // clonagem: ganho agressivo de dano/HP (o "dobra o exército" visual fica a cargo
            // do runtime; aqui o efeito honesto é statístico) — lendária, momento de vídeo.
            set.Clone = ConfigureMutation("Mutation_Clone", "mut_clone", "Clonagem", Rarity.Legendary,
                dps: 1.5f, hp: 1.5f, speed: 1f, size: 1f, flight: false,
                element: ElementType.None, bit: 5);

            // regeneração: HP extra (proxy de cura contínua), épica.
            set.Regen = ConfigureMutation("Mutation_Regen", "mut_regen", "Regeneração", Rarity.Epic,
                dps: 1f, hp: 1.6f, speed: 1f, size: 1f, flight: false,
                element: ElementType.None, bit: 6);

            // escudo: muito HP, leve perda de dano — defensiva pura.
            set.Shield = ConfigureMutation("Mutation_Shield", "mut_shield", "Escudo", Rarity.Rare,
                dps: 0.95f, hp: 1.7f, speed: 1f, size: 1.05f, flight: false,
                element: ElementType.None, bit: 7);

            // ataque em área: dpsMult forte, adiciona Fogo (estética de explosão), épica.
            set.AreaBlast = ConfigureMutation("Mutation_AreaBlast", "mut_area_blast", "Onda de Choque", Rarity.Epic,
                dps: 1.4f, hp: 1f, speed: 1f, size: 1f, flight: false,
                element: ElementType.Fire, bit: 8);

            return set;
        }

        private static MutationConfigSO ConfigureMutation(string assetName, string mutationId, string displayName,
                                                          Rarity rarity,
                                                          float dps, float hp, float speed, float size,
                                                          bool flight, ElementType element, int bit)
        {
            var m = LoadOrCreate<MutationConfigSO>(Root + "/Mutations/" + assetName + ".asset");
            m.mutationId = mutationId;
            m.displayNameKey = mutationId + "_name";
            m.displayName = displayName;        // nome amigável PT-BR (CANON §3.3) — o HUD de mutação exibe este campo
            m.rarity = rarity;
            m.dpsMult = dps;
            m.hpMult = hp;
            m.speedMult = speed;
            m.sizeMult = size;
            m.grantsFlight = flight;
            m.addsElement = element;
            m.shaderVariantFlag = 1 << bit;   // bit distinto por mutação (doc 12 §6.2)
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ Gates (taxonomia ampliada · CANON §10 / doc 04)

        // Paleta por CATEGORIA (briefing): azul=positivo · vermelho/laranja=negativo ·
        // dourado=risco · roxo=mutação · ciano=elemento. displayLabel SEMPRE honesto.
        private static class GateColor
        {
            public static readonly Color Positive = new Color(0.20f, 0.75f, 1.00f);  // azul
            public static readonly Color Negative = new Color(1.00f, 0.30f, 0.22f);  // vermelho
            public static readonly Color Risk = new Color(1.00f, 0.80f, 0.20f);      // dourado
            public static readonly Color Mutation = new Color(0.70f, 0.35f, 1.00f);  // roxo
            public static readonly Color Element = new Color(0.30f, 0.90f, 1.00f);   // ciano
            public static readonly Color ClassConvert = new Color(0.35f, 0.80f, 1.00f); // azul-classe
        }

        private static GateSet CreateGates(UnitSet units, MutationSet mutations)
        {
            var set = new GateSet();

            // ---- MATEMÁTICOS (doc 04). unitToAdd = Soldado: AddFlat spawna soldados; Multiply
            // só reconcilia o total (spawnType é o piso quando precisa criar). ----
            set.AddTen = ConfigureGate("Gate_Add10", "gate_add_10", GateType.AddFlat, 10f, "+10", GateColor.Positive);
            set.AddTen.unitToAdd = units.Soldier;
            set.AddTwentyFive = ConfigureGate("Gate_Add25", "gate_add_25", GateType.AddFlat, 25f, "+25", GateColor.Positive);
            set.AddTwentyFive.unitToAdd = units.Soldier;
            set.AddFifty = ConfigureGate("Gate_Add50", "gate_add_50", GateType.AddFlat, 50f, "+50", GateColor.Positive);
            set.AddFifty.unitToAdd = units.Soldier;
            set.TimesTwo = ConfigureGate("Gate_X2", "gate_x2", GateType.Multiply, 2f, "x2", GateColor.Positive);
            set.TimesTwo.unitToAdd = units.Soldier;
            set.TimesThree = ConfigureGate("Gate_X3", "gate_x3", GateType.Multiply, 3f, "x3", GateColor.Positive);
            set.TimesThree.unitToAdd = units.Soldier;
            set.TimesFive = ConfigureGate("Gate_X5", "gate_x5", GateType.Multiply, 5f, "x5", GateColor.Positive);
            set.TimesFive.unitToAdd = units.Soldier;
            // Negativos (vermelho). −10 é AddFlat com value negativo (GateMath soma e aplica piso 1).
            set.MinusTen = ConfigureGate("Gate_Minus10", "gate_minus_10", GateType.AddFlat, -10f, "−10", GateColor.Negative);
            set.MinusTen.unitToAdd = units.Soldier;
            set.Half = ConfigureGate("Gate_Div2", "gate_div2", GateType.Multiply, 0.5f, "÷2", GateColor.Negative);
            set.Div2Alt = set.Half;   // alias semântico (CANON §10 lista ÷2 explicitamente)

            // ---- CLASSE (ClassConvert): transforma o exército inteiro na tropa-alvo. ----
            set.ClassArcher = ConfigureGate("Gate_ClassArcher", "gate_class_archer",
                GateType.ClassConvert, 1f, "VIRAR ARQUEIRO", GateColor.ClassConvert);
            set.ClassArcher.unitToAdd = units.Archer;
            set.ClassMage = ConfigureGate("Gate_ClassMage", "gate_class_mage",
                GateType.ClassConvert, 1f, "VIRAR MAGO", GateColor.ClassConvert);
            set.ClassMage.unitToAdd = units.Mage;
            set.ClassKnight = ConfigureGate("Gate_ClassKnight", "gate_class_knight",
                GateType.ClassConvert, 1f, "VIRAR ESCUDEIRO", GateColor.ClassConvert);
            set.ClassKnight.unitToAdd = units.Knight;
            set.ClassNinja = ConfigureGate("Gate_ClassNinja", "gate_class_ninja",
                GateType.ClassConvert, 1f, "VIRAR NINJA", GateColor.ClassConvert);
            set.ClassNinja.unitToAdd = units.Ninja;
            set.ClassGiant = ConfigureGate("Gate_ClassGiant", "gate_class_giant",
                GateType.ClassConvert, 1f, "VIRAR GIGANTE", GateColor.ClassConvert);
            set.ClassGiant.unitToAdd = units.Giant;

            // ---- ELEMENTO (Element · ciano): aplica elemento ao exército (SetElement). ----
            set.ElementFire = ConfigureGate("Gate_ElementFire", "gate_element_fire",
                GateType.Element, 0f, "FOGO", new Color(1.00f, 0.45f, 0.20f));
            set.ElementFire.element = ElementType.Fire;
            set.ElementIce = ConfigureGate("Gate_ElementIce", "gate_element_ice",
                GateType.Element, 0f, "GELO", new Color(0.55f, 0.85f, 1.00f));
            set.ElementIce.element = ElementType.Ice;
            set.ElementLightning = ConfigureGate("Gate_ElementLightning", "gate_element_lightning",
                GateType.Element, 0f, "RAIO", new Color(1.00f, 0.92f, 0.35f));
            set.ElementLightning.element = ElementType.Lightning;
            set.ElementPoison = ConfigureGate("Gate_ElementPoison", "gate_element_poison",
                GateType.Element, 0f, "VENENO", new Color(0.55f, 0.95f, 0.35f));
            set.ElementPoison.element = ElementType.Poison;

            // ---- MUTAÇÃO (Mutation · roxo): mutation = MutationConfigSO; ApplyMutation no toque. ----
            set.MutWings = ConfigureGate("Gate_MutWings", "gate_mut_wings",
                GateType.Mutation, 0f, "ASAS", GateColor.Mutation);
            set.MutWings.mutation = mutations.Wings;
            set.MutArmor = ConfigureGate("Gate_MutArmor", "gate_mut_armor",
                GateType.Mutation, 0f, "ARMADURA", GateColor.Mutation);
            set.MutArmor.mutation = mutations.Armor;
            set.MutLaser = ConfigureGate("Gate_MutLaser", "gate_mut_laser",
                GateType.Mutation, 0f, "LASER", GateColor.Mutation);
            set.MutLaser.mutation = mutations.Laser;
            set.MutSize = ConfigureGate("Gate_MutSize", "gate_mut_size",
                GateType.Mutation, 0f, "GIGANTISMO", GateColor.Mutation);
            set.MutSize.mutation = mutations.Size;
            set.MutSpeed = ConfigureGate("Gate_MutSpeed", "gate_mut_speed",
                GateType.Mutation, 0f, "VELOCIDADE", GateColor.Mutation);
            set.MutSpeed.mutation = mutations.Speed;
            set.MutRegen = ConfigureGate("Gate_MutRegen", "gate_mut_regen",
                GateType.Mutation, 0f, "REGENERAÇÃO", GateColor.Mutation);
            set.MutRegen.mutation = mutations.Regen;

            // ---- RISCO (dourado): rótulo HONESTO com as odds visíveis (CANON §3.4) — o RNG
            // usa exatamente riskSuccessChance/riskRewardMult/riskFailPenalty. ----
            set.RiskTen = ConfigureGate("Gate_RiskX10", "gate_risk_x10", GateType.Risk, 0f,
                "70% x10 / 30% −½", GateColor.Risk);
            set.RiskTen.riskSuccessChance = 0.7f;
            set.RiskTen.riskRewardMult = 10f;
            set.RiskTen.riskFailPenalty = 0.5f;
            // "x10 SE SOBREVIVER" agressivo: odds baixas, prêmio enorme. unitToAdd = Titã (lendário,
            // Supply 25 ≥ 18) ATIVA o Sacrifício do Titã no RiskResolver (doc 04 §3.5): no sucesso,
            // 50% das tropas → 1 Titã — sacrifício determinístico, mais interessante que o x10 simples.
            set.RiskTitan = ConfigureGate("Gate_RiskTitan", "gate_risk_titan", GateType.Risk, 0f,
                "50%: SACRIFÍCIO → TITÃ / 50% −½", GateColor.Risk);
            set.RiskTitan.riskSuccessChance = 0.5f;
            set.RiskTitan.riskRewardMult = 10f;
            set.RiskTitan.riskFailPenalty = 0.5f;
            set.RiskTitan.unitToAdd = units.Titan;
            // "sacrificar metade por um prêmio" — sucesso quase garantido, penalidade pesada.
            set.RiskSacrifice = ConfigureGate("Gate_RiskSacrifice", "gate_risk_sacrifice", GateType.Risk, 0f,
                "90% x3 / 10% −½", GateColor.Risk);
            set.RiskSacrifice.riskSuccessChance = 0.9f;
            set.RiskSacrifice.riskRewardMult = 3f;
            set.RiskSacrifice.riskFailPenalty = 0.5f;

            // dirty em massa (LoadOrCreate já marcou cada um; reforço para os campos pós-Configure)
            foreach (GateConfigSO g in AllGates(set)) EditorUtility.SetDirty(g);
            return set;
        }

        private static IEnumerable<GateConfigSO> AllGates(GateSet s)
        {
            yield return s.AddTen; yield return s.AddTwentyFive; yield return s.AddFifty;
            yield return s.TimesTwo; yield return s.TimesThree; yield return s.TimesFive;
            yield return s.MinusTen; yield return s.Half;
            yield return s.ClassArcher; yield return s.ClassMage; yield return s.ClassKnight;
            yield return s.ClassNinja; yield return s.ClassGiant;
            yield return s.ElementFire; yield return s.ElementIce; yield return s.ElementLightning; yield return s.ElementPoison;
            yield return s.MutWings; yield return s.MutArmor; yield return s.MutLaser;
            yield return s.MutSize; yield return s.MutSpeed; yield return s.MutRegen;
            yield return s.RiskTen; yield return s.RiskTitan; yield return s.RiskSacrifice;
        }

        private static GateConfigSO ConfigureGate(string assetName, string gateId, GateType type,
                                                  float value, string label, Color color)
        {
            var gate = LoadOrCreate<GateConfigSO>(Root + "/Gates/" + assetName + ".asset");
            gate.gateId = gateId;
            gate.gateType = type;
            gate.value = value;
            gate.displayLabel = label;
            gate.portalColor = color;
            EditorUtility.SetDirty(gate);
            return gate;
        }

        // ------------------------------------------------------------------ Element chart (CANON §4)

        private static void CreateElementChart()
        {
            var chart = LoadOrCreate<ElementChartSO>(Root + "/Balance/ElementChart_Default.asset");

            // Mesmas entradas do MutantArmy.Domain.ElementChart.Default() — o SO delega
            // o cálculo; aqui só serializa o dado canônico.
            ElementChart.Entry[] entries = BuildCanonicalEntries();
            ElementChart.BodyEntry[] bodyEntries = BuildCanonicalBodyEntries();

            // _entries/_bodyEntries são privados no SO (read-only em runtime, doc 12 §5.1) —
            // preenchidos via SerializedObject, o caminho oficial de editor.
            var serialized = new SerializedObject(chart);
            SerializedProperty array = serialized.FindProperty("_entries");
            if (array == null)
            {
                Debug.LogError("MAR Tools: ElementChartSO sem campo serializado '_entries'.", chart);
                return;
            }
            array.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                SerializedProperty element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("attacker").intValue = (int)entries[i].attacker;
                element.FindPropertyRelative("defender").intValue = (int)entries[i].defender;
                element.FindPropertyRelative("multiplier").floatValue = entries[i].multiplier;
            }

            // Regras de CORPO do CANON §4 (Veneno/Luz × Organic/Machine/Undead): sem elas o
            // GetBodyMultiplier do asset devolve 1.0 neutro e diverge do Domain.Default().
            SerializedProperty bodyArray = serialized.FindProperty("_bodyEntries");
            if (bodyArray == null)
            {
                Debug.LogError("MAR Tools: ElementChartSO sem campo serializado '_bodyEntries'.", chart);
                return;
            }
            bodyArray.arraySize = bodyEntries.Length;
            for (int i = 0; i < bodyEntries.Length; i++)
            {
                SerializedProperty element = bodyArray.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("attacker").intValue = (int)bodyEntries[i].attacker;
                element.FindPropertyRelative("defender").intValue = (int)bodyEntries[i].defender;
                element.FindPropertyRelative("multiplier").floatValue = bodyEntries[i].multiplier;
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(chart);
        }

        private static ElementChart.Entry[] BuildCanonicalEntries()
        {
            return new[]
            {
                // ciclo principal: Fogo > Gelo > Raio > Fogo (+50%)
                Entry(ElementType.Fire, ElementType.Ice, 1.5f),
                Entry(ElementType.Ice, ElementType.Lightning, 1.5f),
                Entry(ElementType.Lightning, ElementType.Fire, 1.5f),
                // mesmo elemento vs mesmo elemento: −50%
                Entry(ElementType.Fire, ElementType.Fire, 0.5f),
                Entry(ElementType.Ice, ElementType.Ice, 0.5f),
                Entry(ElementType.Lightning, ElementType.Lightning, 0.5f),
                Entry(ElementType.Poison, ElementType.Poison, 0.5f),
                Entry(ElementType.Light, ElementType.Light, 0.5f),
                Entry(ElementType.Shadow, ElementType.Shadow, 0.5f),
                Entry(ElementType.Metal, ElementType.Metal, 0.5f),
                Entry(ElementType.Alien, ElementType.Alien, 0.5f),
                // pós-MVP: Luz <-> Sombra; Metal conduz Raio
                Entry(ElementType.Light, ElementType.Shadow, 1.5f),
                Entry(ElementType.Shadow, ElementType.Light, 1.5f),
                Entry(ElementType.Lightning, ElementType.Metal, 1.5f)
            };
        }

        private static ElementChart.Entry Entry(ElementType attacker, ElementType defender, float multiplier)
        {
            return new ElementChart.Entry { attacker = attacker, defender = defender, multiplier = multiplier };
        }

        /// <summary>Mesmas 4 entradas de corpo de ElementChart.Default() (CANON §4).</summary>
        private static ElementChart.BodyEntry[] BuildCanonicalBodyEntries()
        {
            return new[]
            {
                // Veneno: +50% vs orgânicos, 0% vs máquinas e mortos-vivos
                BodyEntry(ElementType.Poison, BodyType.Organic, 1.5f),
                BodyEntry(ElementType.Poison, BodyType.Machine, 0f),
                BodyEntry(ElementType.Poison, BodyType.Undead, 0f),
                // Luz: +50% vs mortos-vivos (pós-MVP, mas o asset canônico já nasce completo)
                BodyEntry(ElementType.Light, BodyType.Undead, 1.5f)
            };
        }

        private static ElementChart.BodyEntry BodyEntry(ElementType attacker, BodyType defender, float multiplier)
        {
            return new ElementChart.BodyEntry { attacker = attacker, defender = defender, multiplier = multiplier };
        }

        // ------------------------------------------------------------------ Rarities (CANON §8)

        private static void CreateRarities()
        {
            ConfigureRarity("Rarity_Common", Rarity.Common,
                new Color(0.75f, 0.78f, 0.82f), new Color(0.60f, 0.78f, 0.90f), 1.00f, 70);
            ConfigureRarity("Rarity_Rare", Rarity.Rare,
                new Color(0.25f, 0.55f, 1.00f), new Color(0.45f, 0.70f, 1.00f), 1.15f, 24);
            ConfigureRarity("Rarity_Epic", Rarity.Epic,
                new Color(0.60f, 0.30f, 0.90f), new Color(0.75f, 0.50f, 1.00f), 1.30f, 5);
            ConfigureRarity("Rarity_Legendary", Rarity.Legendary,
                new Color(1.00f, 0.80f, 0.20f), new Color(1.00f, 0.90f, 0.50f), 1.45f, 1);
        }

        private static void ConfigureRarity(string assetName, Rarity rarity, Color frame, Color glow,
                                            float premium, int chestWeight)
        {
            var config = LoadOrCreate<RarityConfigSO>(Root + "/Balance/" + assetName + ".asset");
            config.rarity = rarity;
            config.frameColor = frame;
            config.glowColor = glow;
            config.statPremium = premium;
            config.chestWeight = chestWeight;
            EditorUtility.SetDirty(config);
        }

        // ------------------------------------------------------------------ Upgrades (CANON §9 — 4 trilhas MVP)

        private static void CreateUpgrades()
        {
            ConfigureUpgrade("Upgrade_StartDamage", UpgradeTrack.StartDamage, "upgrade_start_damage");
            ConfigureUpgrade("Upgrade_StartHealth", UpgradeTrack.StartHealth, "upgrade_start_health");
            ConfigureUpgrade("Upgrade_StartArmy", UpgradeTrack.StartArmy, "upgrade_start_army");
            ConfigureUpgrade("Upgrade_RewardMultiplier", UpgradeTrack.RewardMultiplier, "upgrade_reward_multiplier");
        }

        private static void ConfigureUpgrade(string assetName, UpgradeTrack track, string nameKey)
        {
            var upgrade = LoadOrCreate<UpgradeConfigSO>(Root + "/Upgrades/" + assetName + ".asset");
            upgrade.track = track;
            upgrade.displayNameKey = nameKey;
            upgrade.bonusPerLevel = 0.05f;      // +5%/nível (StartArmy: +1 unidade a cada 2 níveis)
            upgrade.maxLevel = 50;
            upgrade.costBase = 100f;            // custo(n) = 100 × 1,35^n (CANON §8/§9)
            upgrade.costGrowth = 1.35f;
            upgrade.inMvp = true;
            EditorUtility.SetDirty(upgrade);
        }

        // ------------------------------------------------------------------ Rewards (CANON §8/§16 · doc 12 §5.1)

        private static RewardSet CreateRewards(UnitSet units)
        {
            var set = new RewardSet();

            // CANON §8: boss de mundo dá 10 gemas; §16: fase 7 = boss de mundo M1 + baú "De Mundo".
            // O baú De Mundo (doc 07 §4: 10 pacotes 40/35/20/5%, 15 gemas, sem moedas) entra por
            // ChestType.World — RewardSystem.GrantReward abre o reward.chest na hora (não é mais
            // decorativo). Pool de carta inclui o roster expandido (Raro/Épico/Lendário) — boss de
            // mundo é a principal fonte grátis de tropas fortes (anti pay-to-win, CANON §11).
            set.WorldBoss = ConfigureReward("Reward_WorldBoss", coins: 0, gems: 10, playerXp: 0,
                chest: ChestType.World, cardDropChance: 0.30f, shardAmount: 10,
                cardPool: new[]
                {
                    units.Mage, units.Giant, units.Ninja, units.FlameTrooper, units.FrostTrooper,
                    units.Medic, units.Robot, units.Necromancer, units.Engineer, units.Alien,
                    units.Dragon, units.Titan, units.WarAngel, units.Demon, units.Mecha
                });

            // CANON §6: todo boss tem recompensa especial + chance de drop de carta/fragmento.
            // Valores não fixados pelo CANON (drop chance) são defaults recalibráveis por RC.
            set.BossDefault = ConfigureReward("Reward_BossDefault", coins: 0, gems: 0, playerXp: 0,
                chest: ChestType.None, cardDropChance: 0.10f, shardAmount: 10,
                cardPool: new[]
                {
                    units.Archer, units.Shieldbearer, units.Runner, units.Mage, units.Ninja,
                    units.FlameTrooper, units.FrostTrooper, units.Medic
                });

            // CANON §16: fase 10 = baú épico + 50 gemas.
            set.Level10 = ConfigureReward("Reward_Level10", coins: 0, gems: 50, playerXp: 0,
                chest: ChestType.Epic, cardDropChance: 0f, shardAmount: 0,
                cardPool: new UnitConfigSO[0]);

            return set;
        }

        private static RewardConfigSO ConfigureReward(string assetName, int coins, int gems, int playerXp,
                                                      ChestType chest, float cardDropChance, int shardAmount,
                                                      UnitConfigSO[] cardPool)
        {
            var reward = LoadOrCreate<RewardConfigSO>(Root + "/Rewards/" + assetName + ".asset");
            reward.coins = coins;
            reward.gems = gems;
            reward.playerXp = playerXp;
            reward.chest = chest;
            reward.cardDropChance = cardDropChance;
            reward.shardAmount = shardAmount;
            reward.cardPool = cardPool;
            reward.allowAdDouble = true;        // "dobrar com anúncio" na tela de vitória (CANON §11)
            EditorUtility.SetDirty(reward);
            return reward;
        }

        // ------------------------------------------------------------------ Bosses (CANON §6 · doc 05 §7)

        private static readonly ElementType[] NoElements = new ElementType[0];

        private static BossSet CreateBosses(RewardSet rewards)
        {
            var set = new BossSet();

            // ---- MVP preservados (não quebrar) ----
            // HP base do golem = aparição da fase 3 (400); as demais fases escalam via
            // bossHpMultiplier do LevelConfigSO (doc 05 §7.1: 100/220/400/550/700/900).
            set.Golem = ConfigureBoss("Boss_M1_GolemStone", "m1_golem_stone", "Golem de Pedra",
                weaknesses: new[] { ElementType.Fire }, immunities: NoElements,
                bodyType: BodyType.Organic, maxHp: 400f, contactDps: 6f,
                entranceSeconds: 1.8f, telegraphSeconds: 1.2f,
                specialDamage: 25f, specialArea: 3f, specialCooldown: 3.5f,
                killReward: rewards.BossDefault);

            set.WoodGiant = ConfigureBoss("Boss_M1_WoodGiant", "m1_final_wood_giant", "Gigante de Madeira",
                new[] { ElementType.Fire }, NoElements,
                BodyType.Organic, 1600f, 8f, 2.0f, 1.2f, 30f, 4f, 3.5f,
                rewards.WorldBoss);

            set.Bruiser = ConfigureBoss("Boss_M2_ZombieBruiser", "m2_zombie_bruiser", "Brutamontes Zumbi",
                new[] { ElementType.Fire }, new[] { ElementType.Poison },
                BodyType.Undead, 1000f, 8f, 1.6f, 1.0f, 28f, 2f, 3.0f,
                rewards.BossDefault);

            // Fraquezas listadas com Luz por consistência de canon (Luz é pós-MVP — doc 05 §7.4).
            set.Titan = ConfigureBoss("Boss_M2_ZombieTitan", "m2_final_zombie_titan", "Zumbi Titã",
                new[] { ElementType.Fire, ElementType.Light }, new[] { ElementType.Poison },
                BodyType.Undead, 2800f, 10f, 2.0f, 1.2f, 35f, 2.5f, 3.5f,
                rewards.WorldBoss);

            set.Scorpion = ConfigureBoss("Boss_M3_ScorpionMech", "m3_final_scorpion_mech", "Robô Escorpião",
                new[] { ElementType.Lightning }, new[] { ElementType.Poison },
                BodyType.Machine, 3000f, 12f, 2.0f, 1.1f, 40f, 4f, 3.0f,
                rewards.WorldBoss);

            // ---- W1–W3: arquétipo regional + boss final (reutiliza os MVP) ----
            set.Arquetype[1] = set.Golem; set.Final[1] = set.WoodGiant;
            set.Arquetype[2] = set.Bruiser; set.Final[2] = set.Titan;
            // W3 não tem arquétipo dedicado no MVP: o Escorpião serve de variante regional
            // (escalado p/ baixo nas fases 21–29) E de boss final (fase 30) — comportamento atual.
            set.Arquetype[3] = set.Scorpion; set.Final[3] = set.Scorpion;

            // ---- W4–W10: arquétipo regional NOVO + boss único de mundo NOVO (CANON §6) ----
            // bossId estável por mundo (contrato do visual). maxHp do boss final escala por
            // mundo; o arquétipo tem ~55% do HP do final e entra escalado por fase (1–9).
            // Fraqueza/imunidade/element seguem o CANON §6 exatamente.

            // M4 Floresta Mutante — Planta Carnívora Gigante (fraco Fogo+Veneno; orgânico).
            set.Arquetype[4] = ConfigureBoss("Boss_M4_CarnivorousSprout", "m4_carnivorous_sprout",
                "Broto Carnívoro", new[] { ElementType.Fire, ElementType.Poison }, NoElements,
                BodyType.Organic, 2200f, 11f, 1.7f, 1.1f, 34f, 3f, 3.0f, rewards.BossDefault);
            set.Final[4] = ConfigureBoss("Boss_M4_CarnivorousPlant", "m4_carnivorous_plant",
                "Planta Carnívora Gigante", new[] { ElementType.Fire, ElementType.Poison }, NoElements,
                BodyType.Organic, 4000f, 14f, 2.0f, 1.2f, 44f, 4.5f, 3.0f, rewards.WorldBoss);

            // M5 Vulcão dos Gigantes — Dragão de Lava (fraco Gelo; RESISTE Fogo → element=Fire).
            set.Arquetype[5] = ConfigureBoss("Boss_M5_LavaWhelp", "m5_lava_whelp",
                "Filhote de Lava", new[] { ElementType.Ice }, NoElements,
                BodyType.Organic, 2900f, 12f, 1.8f, 1.1f, 38f, 3.5f, 3.0f, rewards.BossDefault,
                element: ElementType.Fire);
            set.Final[5] = ConfigureBoss("Boss_M5_LavaDragon", "m5_lava_dragon",
                "Dragão de Lava", new[] { ElementType.Ice }, NoElements,
                BodyType.Organic, 5200f, 16f, 2.0f, 1.3f, 50f, 5f, 2.8f, rewards.WorldBoss,
                element: ElementType.Fire);

            // M6 Reino Congelado — Rei de Gelo (fraco Fogo; RESISTE Gelo → element=Ice).
            set.Arquetype[6] = ConfigureBoss("Boss_M6_FrostSentinel", "m6_frost_sentinel",
                "Sentinela de Gelo", new[] { ElementType.Fire }, NoElements,
                BodyType.Organic, 3400f, 13f, 1.8f, 1.1f, 40f, 3.5f, 3.0f, rewards.BossDefault,
                element: ElementType.Ice);
            set.Final[6] = ConfigureBoss("Boss_M6_IceKing", "m6_ice_king",
                "Rei de Gelo", new[] { ElementType.Fire }, NoElements,
                BodyType.Organic, 6200f, 17f, 2.0f, 1.3f, 52f, 5f, 2.8f, rewards.WorldBoss,
                element: ElementType.Ice);

            // M7 Arena Medieval — Cavaleiro Colosso (fraco Raio — armadura conduz; máquina/metal).
            set.Arquetype[7] = ConfigureBoss("Boss_M7_ArmoredKnight", "m7_armored_knight",
                "Cavaleiro Blindado", new[] { ElementType.Lightning }, NoElements,
                BodyType.Machine, 4000f, 14f, 1.8f, 1.1f, 42f, 3.5f, 3.0f, rewards.BossDefault);
            set.Final[7] = ConfigureBoss("Boss_M7_ColossusKnight", "m7_colossus_knight",
                "Cavaleiro Colosso", new[] { ElementType.Lightning }, NoElements,
                BodyType.Machine, 7200f, 18f, 2.0f, 1.3f, 56f, 5f, 2.6f, rewards.WorldBoss);

            // M8 Laboratório Alienígena — Alien Supremo (fraqueza ROTATIVA a cada 25% de HP).
            set.Arquetype[8] = ConfigureBoss("Boss_M8_AlienHybrid", "m8_alien_hybrid",
                "Híbrido Alienígena", new[] { ElementType.Fire, ElementType.Ice, ElementType.Lightning }, NoElements,
                BodyType.Organic, 4600f, 15f, 1.8f, 1.1f, 44f, 3.5f, 3.0f, rewards.BossDefault,
                rotatingWeakness: true);
            set.Final[8] = ConfigureBoss("Boss_M8_AlienSupreme", "m8_alien_supreme",
                "Alien Supremo", new[] { ElementType.Fire, ElementType.Ice, ElementType.Lightning }, NoElements,
                BodyType.Organic, 8400f, 19f, 2.0f, 1.3f, 60f, 5.5f, 2.6f, rewards.WorldBoss,
                rotatingWeakness: true);

            // M9 Planeta Mecânico — Mecha Supremo (fraco Raio; IMUNE Veneno; máquina).
            set.Arquetype[9] = ConfigureBoss("Boss_M9_WarDrone", "m9_war_drone",
                "Drone de Guerra", new[] { ElementType.Lightning }, new[] { ElementType.Poison },
                BodyType.Machine, 5400f, 16f, 1.8f, 1.1f, 46f, 3.5f, 3.0f, rewards.BossDefault);
            set.Final[9] = ConfigureBoss("Boss_M9_MechaSupreme", "m9_mecha_supreme",
                "Mecha Supremo", new[] { ElementType.Lightning }, new[] { ElementType.Poison },
                BodyType.Machine, 9800f, 20f, 2.0f, 1.3f, 64f, 6f, 2.4f, rewards.WorldBoss);

            // M10 Dimensão Final — Entidade Dimensional (ALTERNA elementos → rotatingWeakness).
            set.Arquetype[10] = ConfigureBoss("Boss_M10_DimRift", "m10_dim_rift",
                "Fenda Dimensional", new[] { ElementType.Fire, ElementType.Ice, ElementType.Lightning, ElementType.Poison }, NoElements,
                BodyType.Organic, 6400f, 17f, 1.8f, 1.1f, 48f, 4f, 2.8f, rewards.BossDefault,
                rotatingWeakness: true);
            set.Final[10] = ConfigureBoss("Boss_M10_DimensionalEntity", "m10_dimensional_entity",
                "Entidade Dimensional", new[] { ElementType.Fire, ElementType.Ice, ElementType.Lightning, ElementType.Poison }, NoElements,
                BodyType.Organic, 12000f, 22f, 2.0f, 1.4f, 70f, 6.5f, 2.2f, rewards.WorldBoss,
                rotatingWeakness: true);

            return set;
        }

        private static BossConfigSO ConfigureBoss(string assetName, string bossId, string displayName,
                                                  ElementType[] weaknesses, ElementType[] immunities,
                                                  BodyType bodyType, float maxHp, float contactDps,
                                                  float entranceSeconds, float telegraphSeconds,
                                                  float specialDamage, float specialArea, float specialCooldown,
                                                  RewardConfigSO killReward,
                                                  ElementType element = ElementType.None,
                                                  bool rotatingWeakness = false)
        {
            var boss = LoadOrCreate<BossConfigSO>(Root + "/Bosses/" + assetName + ".asset");
            boss.bossId = bossId;
            boss.displayNameKey = bossId + "_name";
            boss.displayName = displayName;     // nome amigável PT-BR (CANON §6) — o Boss Scout exibe este campo
            // element != None = boss "do elemento" (CANON §4: ataque do MESMO elemento sofre
            // −50%). É assim que Dragão de Lava resiste Fogo (element=Fire) e Rei de Gelo
            // resiste Gelo (element=Ice).
            boss.element = element;
            boss.weaknesses = weaknesses;
            boss.immunities = immunities;
            // CANON §6: só Alien Supremo (M8) e Entidade Dimensional (M10) — e seus arquétipos —
            // rotacionam a fraqueza a cada 25% de HP.
            boss.rotatingWeakness = rotatingWeakness;
            boss.bodyType = bodyType;
            boss.maxHp = maxHp;
            boss.contactDps = contactDps;
            boss.entranceSeconds = entranceSeconds;     // CANON §6: entrada ≤ 2 s
            boss.telegraphSeconds = telegraphSeconds;
            boss.specialAttackDamage = specialDamage;
            boss.specialAttackArea = specialArea;
            boss.specialBaseCooldown = specialCooldown;
            // CANON §8: sem killReward o RewardSystem.GrantBossReward é no-op — gema nenhuma entraria.
            boss.killReward = killReward;
            if (boss.arenaWaves == null) boss.arenaWaves = new ArenaWaveEvent[0];
            EditorUtility.SetDirty(boss);
            return boss;
        }

        // ------------------------------------------------------------------ Worlds + Levels (CANON §7/§8 · doc 06 §8)

        // 10 MUNDOS × 10 FASES = 100. Cada mundo: asset name (contrato do WorldVisualFactory —
        // W{NN}_Nome), worldIndex 1..10, displayNameKey e o elemento de FRAQUEZA dominante do
        // boss do mundo (usado para curar os pares de elemento das fases marquee).
        private sealed class WorldDef
        {
            public string AssetName, NameKey;
            public int Index;
            public ElementType Weakness;    // fraqueza primária do boss do mundo (rota ótima de elemento)
            public ElementType Immune;      // imunidade do boss (armadilha de elemento), None se não tem

            public WorldDef(int index, string assetName, string nameKey, ElementType weakness, ElementType immune)
            {
                Index = index; AssetName = assetName; NameKey = nameKey;
                Weakness = weakness; Immune = immune;
            }
        }

        // Tabela canônica (CANON §6/§7 + BRIEF). AssetName PRESERVA W01/W02/W03 (visual já os
        // conhece) e segue o padrão W{NN}_Nome para W04–W10 (o WorldVisualFactory estende os
        // temas por esse nome). Weakness/Immune ditam a rota ótima e a armadilha de elemento.
        private static readonly WorldDef[] Worlds =
        {
            new WorldDef(1,  "W01_CampoInicial",          "world_01_campo_inicial",          ElementType.Fire,      ElementType.None),
            new WorldDef(2,  "W02_CidadeZumbi",           "world_02_cidade_zumbi",           ElementType.Fire,      ElementType.Poison),
            new WorldDef(3,  "W03_DesertoRobotico",       "world_03_deserto_robotico",       ElementType.Lightning, ElementType.Poison),
            new WorldDef(4,  "W04_FlorestaMutante",       "world_04_floresta_mutante",       ElementType.Fire,      ElementType.None),
            new WorldDef(5,  "W05_VulcaoGigantes",        "world_05_vulcao_gigantes",        ElementType.Ice,       ElementType.None),
            new WorldDef(6,  "W06_ReinoCongelado",        "world_06_reino_congelado",        ElementType.Fire,      ElementType.None),
            new WorldDef(7,  "W07_ArenaMedieval",         "world_07_arena_medieval",         ElementType.Lightning, ElementType.None),
            new WorldDef(8,  "W08_LaboratorioAlienigena", "world_08_laboratorio_alienigena", ElementType.Fire,      ElementType.None),
            new WorldDef(9,  "W09_PlanetaMecanico",       "world_09_planeta_mecanico",       ElementType.Lightning, ElementType.Poison),
            new WorldDef(10, "W10_DimensaoFinal",         "world_10_dimensao_final",         ElementType.Fire,      ElementType.None)
        };

        private const int WorldCount = 10;
        private const int LevelsPerWorld = 10;
        private const int TotalLevels = WorldCount * LevelsPerWorld;   // 100

        private static List<LevelConfigSO> CreateWorldsAndLevels(BossSet bosses, GateSet gates, RewardSet rewards)
        {
            var all = new List<LevelConfigSO>(TotalLevels);

            foreach (WorldDef def in Worlds)
            {
                // Boss final do mundo (CANON §6) ligado ao WorldConfigSO p/ o Boss Scout/recompensa.
                WorldConfigSO world = ConfigureWorld(def.AssetName, def.Index, def.NameKey,
                                                     bosses.Final[def.Index], rewards.WorldBoss);
                var worldLevels = new List<LevelConfigSO>(LevelsPerWorld);

                for (int fase = 1; fase <= LevelsPerWorld; fase++)
                {
                    int globalIndex = (def.Index - 1) * LevelsPerWorld + fase;   // 1..100
                    var level = LoadOrCreate<LevelConfigSO>(
                        string.Format("{0}/Levels/Level_{1:000}.asset", Root, globalIndex));

                    level.levelIndex = globalIndex;
                    level.seed = LevelSeed(globalIndex);
                    level.world = world;
                    level.trackLength = TrackLengthForFase(globalIndex, fase);
                    // Fases 1–9: arquétipo regional (escalado); fase 10: boss único do mundo.
                    level.boss = fase == LevelsPerWorld ? bosses.Final[def.Index] : bosses.Arquetype[def.Index];
                    level.bossHpMultiplier = BossHpMultiplier(def.Index, fase);
                    // CANON §16: fase 10 de cada mundo = baú épico + 50 gemas (marco de mundo);
                    // as demais pagam moedas via EconomySystem.GrantLevelReward (curva §8).
                    level.winReward = fase == LevelsPerWorld ? rewards.Level10 : null;
                    level.startingUnits = 1;        // fase sempre começa com 1 + bônus de meta
                    level.obstacles = new ObstacleSlot[0];

                    // Fase global 1 = onboarding impossível de perder (CANON §16). As demais
                    // fases-chave de cada mundo recebem pares MANUAIS curados (elemento vs
                    // fraqueza/armadilha do boss); o resto cai no autoBalance vs o boss.
                    GateSlot[] manual = globalIndex == 1
                        ? BuildOnboardingSlots(level.trackLength, gates)
                        : BuildManualSlotsForLevel(def, fase, level.trackLength, gates);
                    level.gateSlots = manual ?? BuildAutoSlots(SlotCount(level.trackLength), level.trackLength);

                    EditorUtility.SetDirty(level);
                    worldLevels.Add(level);
                    all.Add(level);
                }

                world.levels = worldLevels.ToArray();
                EditorUtility.SetDirty(world);
            }

            return all;
        }

        /// <summary>
        /// Catálogo de fases em Resources/GameSettings.asset — ÚNICO asset permitido em
        /// Resources (bootstrap, doc 12 §2.1). É como o botão Jogar (Main) e o "próxima
        /// fase" (ResultScreen) resolvem LevelConfigSO em runtime.
        /// </summary>
        private static void CreateGameSettings(List<LevelConfigSO> levels)
        {
            EnsureFolder("Assets/_Project/Resources");
            var settings = LoadOrCreate<GameSettingsSO>(
                "Assets/_Project/Resources/" + GameSettingsSO.ResourcesName + ".asset");
            levels.Sort((a, b) => a.levelIndex.CompareTo(b.levelIndex));
            settings.levels = levels.ToArray();
            EditorUtility.SetDirty(settings);
        }

        private static WorldConfigSO ConfigureWorld(string assetName, int index, string nameKey,
                                                    BossConfigSO worldBoss, RewardConfigSO worldClearReward)
        {
            var world = LoadOrCreate<WorldConfigSO>(Root + "/Worlds/" + assetName + ".asset");
            world.worldIndex = index;
            world.displayNameKey = nameKey;
            world.worldBoss = worldBoss;
            world.worldClearReward = worldClearReward;   // boss de mundo: 10 gemas + baú (doc 12 §5.1)
            if (world.trackSegmentPrefabs == null) world.trackSegmentPrefabs = new GameObject[0];
            EditorUtility.SetDirty(world);
            return world;
        }

        /// <summary>
        /// Seed determinística e estável entre execuções do factory — mesma fase = mesma
        /// pista, byte a byte (doc 12 §4.11; QA reproduz bug por seed).
        /// </summary>
        private static int LevelSeed(int levelIndex)
        {
            return 7919 * levelIndex + 1234;
        }

        // Duração-alvo por fase dentro do mundo (doc 06 §8): a 1ª de cada mundo é mais curta
        // (re-onboarding do tema), o miolo é padrão, a fase 10 (boss de mundo) é a mais longa.
        // A fase GLOBAL 1 é o onboarding absoluto (mais curta de todas).
        private static float TrackLengthForFase(int globalIndex, int fase)
        {
            if (globalIndex == 1) return 140f;     // onboarding absoluto: vitória < 60 s (CANON §16)
            switch (fase)
            {
                case 1: return 170f;
                case 2: return 200f;
                case 3: return 220f;
                case 4: return 220f;
                case 5: return 240f;
                case 6: return 240f;
                case 7: return 260f;
                case 8: return 260f;
                case 9: return 260f;
                default: return 280f;              // fase 10: arena de boss de mundo, a mais longa
            }
        }

        /// <summary>
        /// Escala de HP do boss da fase (sobre o HP base do asset), modelando a curva de
        /// dificuldade canônica (CANON §12: vitória 95% nas fases 1–3 → ~55% na fase 10 de
        /// cada mundo). Dois fatores multiplicam:
        ///   1. <b>Curva por fase</b> (1..10): a fase 1 do mundo nasce fácil (boss enfraquecido),
        ///      sobe quase linear, e a fase 10 (boss único) entra com HP cheio do asset.
        ///   2. <b>Rampa por mundo</b> (1..10): mundos mais avançados ficam globalmente mais
        ///      duros (+8% de HP por mundo acima do 1º) — o jogador entra mais forte (meta), o
        ///      conteúdo acompanha. Determinístico: mesma fase = mesmo HP, byte a byte.
        /// </summary>
        private static float BossHpMultiplier(int worldIndex, int fase)
        {
            // Curva por fase: fase 1 ~ 0.30 (impossível perder no começo do mundo) → fase 9 ~ 0.95;
            // fase 10 = 1.00 (HP cheio do boss único do mundo).
            float faseCurve;
            if (fase >= LevelsPerWorld) faseCurve = 1.00f;
            else faseCurve = 0.30f + 0.085f * (fase - 1);   // 0.30, 0.385, ... , 0.98 na fase 9

            // O 1º mundo é ainda mais suave nas primeiras fases (FTUE — CANON §16).
            if (worldIndex == 1 && fase <= 2) faseCurve *= 0.85f;

            float worldRamp = 1f + 0.08f * (worldIndex - 1);   // +8% de HP por mundo (meta acompanha)
            return faseCurve * worldRamp;
        }

        private static int SlotCount(float trackLength)
        {
            if (trackLength <= 160f) return 3;
            if (trackLength <= 220f) return 4;
            return 5;
        }

        /// <summary>
        /// Fase 1 (CANON §16): impossível perder — pares manuais só com x2 e +10,
        /// os dois portais introduzidos no onboarding (doc 06 §8).
        /// </summary>
        private static GateSlot[] BuildOnboardingSlots(float trackLength, GateSet gates)
        {
            return new[]
            {
                ManualSlot(45f, trackLength, gates.AddTen, gates.TimesTwo),
                ManualSlot(95f, trackLength, gates.TimesTwo, gates.AddTen)
            };
        }

        private static GateSlot ManualSlot(float position, float trackLength, GateConfigSO left, GateConfigSO right)
        {
            return new GateSlot
            {
                trackPosition = position,
                depth01 = trackLength > 0f ? position / trackLength : 0f,
                autoBalance = false,
                leftGate = left,
                rightGate = right
            };
        }

        /// <summary>
        /// Pares MANUAIS marcantes para fases-chave de CADA mundo (CANON §16 + briefing
        /// "quantidade vs qualidade"). Retorna null para fases sem curadoria — elas caem no
        /// autoBalance vs o boss. As fases de FTUE do mundo 1 (2,3,4,6) mantêm a curadoria
        /// original do MVP; os demais mundos recebem 1–2 marquees TEMÁTICOS por mundo, sempre
        /// expondo a rota ótima de elemento (fraqueza do boss) vs uma armadilha plausível (número
        /// maior; ou o elemento IMUNE quando o boss tem imunidade). Posições terminam antes da
        /// zona de segurança (trackLength − 45).
        /// </summary>
        private static GateSlot[] BuildManualSlotsForLevel(WorldDef def, int fase, float trackLength, GateSet g)
        {
            float p1 = 35f, p2 = trackLength * 0.45f, p3 = trackLength * 0.66f, p4 = trackLength - 45f;

            // ---- Mundo 1: curadoria de FTUE original do MVP (não mexer no onboarding) ----
            if (def.Index == 1)
            {
                switch (fase)
                {
                    // Fase 2 (CANON §16): PRIMEIRA escolha estratégica real — quantidade vs qualidade.
                    case 2:
                        return new[]
                        {
                            ManualSlot(p1, trackLength, g.TimesThree, g.ClassArcher),
                            ManualSlot(p4, trackLength, g.AddTwentyFive, g.ClassMage)
                        };
                    // Fase 3 (boss "uau" FRACO A FOGO): rota ótima Fogo vs armadilha x5 sem elemento.
                    case 3:
                        return new[]
                        {
                            ManualSlot(p1, trackLength, g.TimesTwo, g.AddTwentyFive),
                            ManualSlot(p3, trackLength, g.ElementFire, g.TimesFive),
                            ManualSlot(p4, trackLength, g.MutWings, g.AddFifty)
                        };
                    // Fase 4: introduz o RISCO honesto + 1ª escolha de mutação dupla.
                    case 4:
                        return new[]
                        {
                            ManualSlot(p1, trackLength, g.TimesThree, g.ElementFire),
                            ManualSlot(p3, trackLength, g.MutArmor, g.MutSpeed),
                            ManualSlot(p4, trackLength, g.RiskTen, g.AddFifty)
                        };
                    // Fase 6: qualidade pesada antes de fechar a primeira leva do mundo.
                    case 6:
                        return new[]
                        {
                            ManualSlot(p1, trackLength, g.TimesThree, g.ClassMage),
                            ManualSlot(p2, trackLength, g.ElementFire, g.TimesFive),
                            ManualSlot(p3, trackLength, g.MutLaser, g.MutSize),
                            ManualSlot(p4, trackLength, g.RiskSacrifice, g.AddFifty)
                        };
                }
            }

            // ---- Mundos 2–10: marquees temáticos genéricos por mundo ----
            GateConfigSO weakGate = ElementGate(def.Weakness, g);       // rota ótima (fraqueza do boss)
            GateConfigSO immuneGate = ElementGate(def.Immune, g);       // armadilha (elemento imune), se houver

            switch (fase)
            {
                // Fase 2 do mundo: "quantidade vs qualidade" reapresentado no novo tema.
                case 2:
                    return new[]
                    {
                        ManualSlot(p1, trackLength, g.TimesThree, g.ClassNinja),
                        ManualSlot(p4, trackLength, g.AddTwentyFive, g.ClassMage)
                    };

                // Fase 4 do mundo: rota ótima de ELEMENTO (fraqueza) vs armadilha. Quando o boss
                // tem imunidade, a armadilha é o próprio elemento imune (0 dano honesto-mas-cruel);
                // senão é o número maior sem elemento.
                case 4:
                    return new[]
                    {
                        ManualSlot(p1, trackLength, g.TimesThree, g.ClassGiant),
                        ManualSlot(p3, trackLength, weakGate ?? g.TimesFive,
                                   immuneGate ?? g.TimesFive),
                        ManualSlot(p4, trackLength, g.MutArmor, g.AddFifty)
                    };

                // Fase 7 do mundo: pico de decisão — risco grande, mutação forte, elemento certo.
                case 7:
                    return new[]
                    {
                        ManualSlot(p1, trackLength, g.TimesFive, g.ClassGiant),
                        ManualSlot(p2, trackLength, weakGate ?? g.ElementFire, g.TimesFive),
                        ManualSlot(p3, trackLength, g.MutSize, g.MutSpeed),
                        ManualSlot(p4, trackLength, g.RiskTitan, g.AddFifty)
                    };

                // Fase 9 do mundo (véspera do boss único): prepara o plano elemental certo.
                case 9:
                    return new[]
                    {
                        ManualSlot(p1, trackLength, g.TimesThree, g.ClassMage),
                        ManualSlot(p3, trackLength, weakGate ?? g.ElementFire, g.MutLaser),
                        ManualSlot(p4, trackLength, g.RiskSacrifice, g.AddFifty)
                    };

                default:
                    return null;   // demais fases: autoBalance (rota ótima + armadilha geradas vs boss)
            }
        }

        /// <summary>Portal de elemento correspondente (ou null para None) — usado pelos marquees temáticos.</summary>
        private static GateConfigSO ElementGate(ElementType element, GateSet g)
        {
            switch (element)
            {
                case ElementType.Fire: return g.ElementFire;
                case ElementType.Ice: return g.ElementIce;
                case ElementType.Lightning: return g.ElementLightning;
                case ElementType.Poison: return g.ElementPoison;
                default: return null;
            }
        }

        /// <summary>
        /// Slots autoBalance: o GateManager monta os pares contra o boss em runtime,
        /// com a MESMA seed determinística da fase (doc 12 §4.3) — rota ótima + armadilha.
        /// </summary>
        private static GateSlot[] BuildAutoSlots(int count, float trackLength)
        {
            var slots = new GateSlot[count];
            float first = 30f;
            float last = trackLength - 40f;     // zona de segurança antes da arena (doc 12 §4.11)
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 0f;
                float position = Mathf.Lerp(first, last, t);
                slots[i] = new GateSlot
                {
                    trackPosition = position,
                    depth01 = trackLength > 0f ? position / trackLength : 0f,
                    autoBalance = true
                };
            }
            return slots;
        }

        // ------------------------------------------------------------------ infra

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
                Undo.RegisterCreatedObjectUndo(asset, UndoLabel);
            }
            else
            {
                Undo.RecordObject(asset, UndoLabel);
            }
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string leaf = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
