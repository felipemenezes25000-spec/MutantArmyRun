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
    /// intactos) · 9 MutationConfigSO (CANON §3.3) · 5 BossConfigSO (CANON §6 + doc 05 §7) ·
    /// ElementChartSO default (CANON §4) · 4 RarityConfigSO (CANON §8) · 4 UpgradeConfigSO
    /// (trilhas MVP, CANON §9) · 3 RewardConfigSO (CANON §8/§16) · 3 WorldConfigSO (CANON
    /// §7/§15) · 20 LevelConfigSO com seeds determinísticas, bosses por fase e pares manuais
    /// curados nas fases-chave (doc 06 §8). Idempotente: re-rodar atualiza os assets existentes.
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

        private sealed class BossSet
        {
            public BossConfigSO Golem, WoodGiant, Bruiser, Titan, Scorpion;
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
            Debug.Log("MAR Tools: conteúdo criado/atualizado — 25 portais, 19 tropas, 9 mutações, 5 bosses, " +
                      "chart elemental, 4 raridades, 4 trilhas de upgrade, 3 recompensas, 3 mundos, " +
                      "20 fases e o catálogo Resources/GameSettings.asset.");
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
                Soldier = ConfigureUnit("Unit_Soldier", "unit_soldier", Rarity.Common, 1,
                    hp: 10f, dps: 2f, speed: 5.0f, range: 1.5f, ability: "cohesion"),
                Archer = ConfigureUnit("Unit_Archer", "unit_archer", Rarity.Common, 2,
                    hp: 14f, dps: 8f, speed: 5.0f, range: 8.0f, ability: "sure_shot"),
                Shieldbearer = ConfigureUnit("Unit_Shieldbearer", "unit_shieldbearer", Rarity.Common, 3,
                    hp: 30f, dps: 4f, speed: 4.5f, range: 1.0f, ability: "shield_wall"),
                Mage = ConfigureUnit("Unit_Mage", "unit_mage", Rarity.Rare, 4,
                    hp: 25f, dps: 16f, speed: 4.5f, range: 6.0f, ability: "arcane_nova"),
                Giant = ConfigureUnit("Unit_Giant", "unit_giant", Rarity.Epic, 12,
                    hp: 120f, dps: 40f, speed: 3.5f, range: 2.0f, ability: "seismic_slam")
            };

            // ---- COMUNS restantes (CANON §5) ----
            set.Runner = ConfigureUnit("Unit_Runner", "unit_runner", Rarity.Common, 1,
                hp: 7f, dps: 3f, speed: 6.5f, range: 1.2f, ability: "dodge_traps",
                element: ElementType.None, body: BodyType.Organic);   // rápido, desvia, frágil

            // ---- RAROS (CANON §5) ----
            set.Ninja = ConfigureUnit("Unit_Ninja", "unit_ninja", Rarity.Rare, 3,
                hp: 22f, dps: 19f, speed: 6.0f, range: 1.5f, ability: "dodge_traps",
                element: ElementType.None, body: BodyType.Organic);
            set.FlameTrooper = ConfigureUnit("Unit_FlameTrooper", "unit_flametrooper", Rarity.Rare, 4,
                hp: 30f, dps: 25f, speed: 4.5f, range: 3.0f, ability: "dot_fire",
                element: ElementType.Fire, body: BodyType.Organic);
            set.FrostTrooper = ConfigureUnit("Unit_FrostTrooper", "unit_frosttrooper", Rarity.Rare, 4,
                hp: 34f, dps: 21f, speed: 4.5f, range: 5.0f, ability: "slow",
                element: ElementType.Ice, body: BodyType.Organic);
            set.Medic = ConfigureUnit("Unit_Medic", "unit_medic", Rarity.Rare, 4,
                hp: 38f, dps: 8f, speed: 4.8f, range: 4.0f, ability: "heal_allies",
                element: ElementType.None, body: BodyType.Organic);

            // ---- ÉPICOS (CANON §5) ----
            set.Robot = ConfigureUnit("Unit_Robot", "unit_robot", Rarity.Epic, 8,
                hp: 90f, dps: 38f, speed: 4.0f, range: 3.0f, ability: "armor_plating",
                element: ElementType.None, body: BodyType.Machine);   // imune a Veneno via bodyType
            set.Necromancer = ConfigureUnit("Unit_Necromancer", "unit_necromancer", Rarity.Epic, 8,
                hp: 76f, dps: 30f, speed: 4.0f, range: 5.0f, ability: "revive_dead",
                element: ElementType.Shadow, body: BodyType.Organic);
            set.Engineer = ConfigureUnit("Unit_Engineer", "unit_engineer", Rarity.Epic, 8,
                hp: 84f, dps: 34f, speed: 4.0f, range: 4.0f, ability: "build_turret",
                element: ElementType.None, body: BodyType.Organic);
            set.Alien = ConfigureUnit("Unit_Alien", "unit_alien", Rarity.Epic, 8,
                hp: 80f, dps: 36f, speed: 4.5f, range: 4.0f, ability: "chain",
                element: ElementType.Alien, body: BodyType.Organic);

            // ---- LENDÁRIOS (CANON §5) ----
            set.Dragon = ConfigureUnit("Unit_Dragon", "unit_dragon", Rarity.Legendary, 20,
                hp: 200f, dps: 110f, speed: 4.5f, range: 6.0f, ability: "flight",
                element: ElementType.Fire, body: BodyType.Organic);   // dano em área + voo
            set.Titan = ConfigureUnit("Unit_Titan", "unit_titan", Rarity.Legendary, 25,
                hp: 320f, dps: 110f, speed: 3.0f, range: 2.5f, ability: "seismic_slam",
                element: ElementType.None, body: BodyType.Organic);   // enorme, forte, lento
            set.WarAngel = ConfigureUnit("Unit_WarAngel", "unit_warangel", Rarity.Legendary, 18,
                hp: 210f, dps: 80f, speed: 5.0f, range: 6.0f, ability: "heal_allies",
                element: ElementType.Light, body: BodyType.Organic);  // cura + dano de Luz
            set.Demon = ConfigureUnit("Unit_Demon", "unit_demon", Rarity.Legendary, 20,
                hp: 230f, dps: 130f, speed: 4.2f, range: 3.0f, ability: "dot_shadow",
                element: ElementType.Shadow, body: BodyType.Organic); // dano brutal de Sombra
            set.Mecha = ConfigureUnit("Unit_Mecha", "unit_mecha", Rarity.Legendary, 25,
                hp: 300f, dps: 150f, speed: 3.5f, range: 7.0f, ability: "area_damage",
                element: ElementType.Lightning, body: BodyType.Machine); // laser + mísseis em área

            return set;
        }

        private static UnitConfigSO ConfigureUnit(string assetName, string unitId, Rarity rarity, int supply,
                                                  float hp, float dps, float speed, float range, string ability,
                                                  ElementType element = ElementType.None,
                                                  BodyType body = BodyType.Organic)
        {
            var unit = LoadOrCreate<UnitConfigSO>(Root + "/Units/" + assetName + ".asset");
            unit.unitId = unitId;
            unit.displayNameKey = unitId + "_name";
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
            set.Wings = ConfigureMutation("Mutation_Wings", "mut_wings", Rarity.Rare,
                dps: 1f, hp: 1f, speed: 1.15f, size: 1f, flight: true,
                element: ElementType.None, bit: 0);

            // armadura: +50% HP (CANON: hpMult 1.5).
            set.Armor = ConfigureMutation("Mutation_Armor", "mut_armor", Rarity.Rare,
                dps: 1f, hp: 1.5f, speed: 1f, size: 1.05f, flight: false,
                element: ElementType.None, bit: 1);

            // laser: adiciona dano de Raio + dpsMult 1.3 (CANON).
            set.Laser = ConfigureMutation("Mutation_Laser", "mut_laser", Rarity.Epic,
                dps: 1.3f, hp: 1f, speed: 1f, size: 1f, flight: false,
                element: ElementType.Lightning, bit: 2);

            // tamanho: sizeMult 1.4, hpMult 1.3 (CANON), leve perda de velocidade.
            set.Size = ConfigureMutation("Mutation_Size", "mut_size", Rarity.Epic,
                dps: 1.15f, hp: 1.3f, speed: 0.95f, size: 1.4f, flight: false,
                element: ElementType.None, bit: 3);

            // velocidade: speedMult 1.4 (CANON).
            set.Speed = ConfigureMutation("Mutation_Speed", "mut_speed", Rarity.Rare,
                dps: 1f, hp: 1f, speed: 1.4f, size: 0.95f, flight: false,
                element: ElementType.None, bit: 4);

            // clonagem: ganho agressivo de dano/HP (o "dobra o exército" visual fica a cargo
            // do runtime; aqui o efeito honesto é statístico) — lendária, momento de vídeo.
            set.Clone = ConfigureMutation("Mutation_Clone", "mut_clone", Rarity.Legendary,
                dps: 1.5f, hp: 1.5f, speed: 1f, size: 1f, flight: false,
                element: ElementType.None, bit: 5);

            // regeneração: HP extra (proxy de cura contínua), épica.
            set.Regen = ConfigureMutation("Mutation_Regen", "mut_regen", Rarity.Epic,
                dps: 1f, hp: 1.6f, speed: 1f, size: 1f, flight: false,
                element: ElementType.None, bit: 6);

            // escudo: muito HP, leve perda de dano — defensiva pura.
            set.Shield = ConfigureMutation("Mutation_Shield", "mut_shield", Rarity.Rare,
                dps: 0.95f, hp: 1.7f, speed: 1f, size: 1.05f, flight: false,
                element: ElementType.None, bit: 7);

            // ataque em área: dpsMult forte, adiciona Fogo (estética de explosão), épica.
            set.AreaBlast = ConfigureMutation("Mutation_AreaBlast", "mut_area_blast", Rarity.Epic,
                dps: 1.4f, hp: 1f, speed: 1f, size: 1f, flight: false,
                element: ElementType.Fire, bit: 8);

            return set;
        }

        private static MutationConfigSO ConfigureMutation(string assetName, string mutationId, Rarity rarity,
                                                          float dps, float hp, float speed, float size,
                                                          bool flight, ElementType element, int bit)
        {
            var m = LoadOrCreate<MutationConfigSO>(Root + "/Mutations/" + assetName + ".asset");
            m.mutationId = mutationId;
            m.displayNameKey = mutationId + "_name";
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

            // CANON §8: boss de mundo dá 10 gemas; §16: fase 7 = boss de mundo M1 + baú grande.
            // Pool de carta inclui o roster expandido (Raro/Épico/Lendário) — boss de mundo
            // é a principal fonte grátis de tropas fortes (anti pay-to-win, CANON §11).
            set.WorldBoss = ConfigureReward("Reward_WorldBoss", coins: 0, gems: 10, playerXp: 0,
                chest: ChestType.Rare, cardDropChance: 0.30f, shardAmount: 10,
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

        private static BossSet CreateBosses(RewardSet rewards)
        {
            var set = new BossSet();

            // HP base do golem = aparição da fase 3 (400); as demais fases escalam via
            // bossHpMultiplier do LevelConfigSO (doc 05 §7.1: 100/220/400/550/700/900).
            set.Golem = ConfigureBoss("Boss_M1_GolemStone", "m1_golem_stone", "Golem de Pedra",
                weaknesses: new[] { ElementType.Fire }, immunities: new ElementType[0],
                bodyType: BodyType.Organic, maxHp: 400f, contactDps: 6f,
                entranceSeconds: 1.8f, telegraphSeconds: 1.2f,
                specialDamage: 25f, specialArea: 3f, specialCooldown: 3.5f,
                killReward: rewards.BossDefault);

            set.WoodGiant = ConfigureBoss("Boss_M1_WoodGiant", "m1_final_wood_giant", "Gigante de Madeira",
                new[] { ElementType.Fire }, new ElementType[0],
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

            return set;
        }

        private static BossConfigSO ConfigureBoss(string assetName, string bossId, string displayName,
                                                  ElementType[] weaknesses, ElementType[] immunities,
                                                  BodyType bodyType, float maxHp, float contactDps,
                                                  float entranceSeconds, float telegraphSeconds,
                                                  float specialDamage, float specialArea, float specialCooldown,
                                                  RewardConfigSO killReward)
        {
            var boss = LoadOrCreate<BossConfigSO>(Root + "/Bosses/" + assetName + ".asset");
            boss.bossId = bossId;
            boss.displayNameKey = bossId + "_name";
            boss.displayName = displayName;     // nome amigável PT-BR (CANON §6) — o Boss Scout exibe este campo
            boss.element = ElementType.None;
            boss.weaknesses = weaknesses;
            boss.immunities = immunities;
            boss.rotatingWeakness = false;      // só o Alien Supremo (M8, pós-MVP) rotaciona
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

        // ------------------------------------------------------------------ Worlds + Levels (CANON §7/§15 · doc 06 §8)

        // Duração-alvo por variante (doc 06 §8): Onboarding/Curta/Padrão/Longa.
        private static readonly float[] TrackLengths =
        {
            140f, 160f, 220f, 220f, 220f, 220f, 260f,           // M1: fases 1–7
            160f, 220f, 220f, 220f, 260f, 260f, 260f,           // M2: fases 8–14
            160f, 220f, 220f, 260f, 260f, 280f                  // M3: fases 15–20
        };

        private static List<LevelConfigSO> CreateWorldsAndLevels(BossSet bosses, GateSet gates, RewardSet rewards)
        {
            WorldConfigSO w1 = ConfigureWorld("W01_CampoInicial", 1, "world_01_campo_inicial", bosses.WoodGiant, rewards.WorldBoss);
            WorldConfigSO w2 = ConfigureWorld("W02_CidadeZumbi", 2, "world_02_cidade_zumbi", bosses.Titan, rewards.WorldBoss);
            WorldConfigSO w3 = ConfigureWorld("W03_DesertoRobotico", 3, "world_03_deserto_robotico", bosses.Scorpion, rewards.WorldBoss);

            var w1Levels = new List<LevelConfigSO>();
            var w2Levels = new List<LevelConfigSO>();
            var w3Levels = new List<LevelConfigSO>();

            for (int levelIndex = 1; levelIndex <= 20; levelIndex++)
            {
                WorldConfigSO world = levelIndex <= 7 ? w1 : levelIndex <= 14 ? w2 : w3;
                var level = LoadOrCreate<LevelConfigSO>(
                    string.Format("{0}/Levels/Level_{1:000}.asset", Root, levelIndex));

                level.levelIndex = levelIndex;
                level.seed = LevelSeed(levelIndex);
                level.world = world;
                level.trackLength = TrackLengths[levelIndex - 1];
                level.boss = BossForLevel(levelIndex, bosses);
                level.bossHpMultiplier = BossHpMultiplier(levelIndex);
                // CANON §16: fase 10 = baú épico + 50 gemas; as demais fases pagam moedas
                // via EconomySystem.GrantLevelReward (curva §8) — winReward fica vazio.
                level.winReward = levelIndex == 10 ? rewards.Level10 : null;
                level.startingUnits = 1;        // fase sempre começa com 1 + bônus de meta
                level.obstacles = new ObstacleSlot[0];

                // Marquee levels recebem pares MANUAIS curados (quantidade vs qualidade,
                // elemento vs fraqueza do boss, mutação, risco); o resto fica autoBalance
                // (o GateManager monta contra o boss). Fase 1 = onboarding impossível de perder.
                GateSlot[] manual = levelIndex == 1
                    ? BuildOnboardingSlots(level.trackLength, gates)
                    : BuildManualSlotsForLevel(levelIndex, level.trackLength, gates);
                level.gateSlots = manual ?? BuildAutoSlots(SlotCount(level.trackLength), level.trackLength);

                EditorUtility.SetDirty(level);

                if (levelIndex <= 7) w1Levels.Add(level);
                else if (levelIndex <= 14) w2Levels.Add(level);
                else w3Levels.Add(level);
            }

            w1.levels = w1Levels.ToArray();
            w2.levels = w2Levels.ToArray();
            w3.levels = w3Levels.ToArray();
            EditorUtility.SetDirty(w1);
            EditorUtility.SetDirty(w2);
            EditorUtility.SetDirty(w3);

            var all = new List<LevelConfigSO>(20);
            all.AddRange(w1Levels);
            all.AddRange(w2Levels);
            all.AddRange(w3Levels);
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

        /// <summary>Boss por fase, tabela do doc 06 §8 (M1 1–7 · M2 8–14 · M3 15–20).</summary>
        private static BossConfigSO BossForLevel(int levelIndex, BossSet bosses)
        {
            if (levelIndex <= 6) return bosses.Golem;
            if (levelIndex == 7) return bosses.WoodGiant;
            if (levelIndex <= 13) return bosses.Bruiser;
            if (levelIndex == 14) return bosses.Titan;
            return bosses.Scorpion;
        }

        /// <summary>
        /// Escala da variante regional sobre o HP base do asset:
        /// golem (base 400) → 100/220/400/550/700/900 nas fases 1–6 (doc 05 §7.1);
        /// brutamontes (base 1000) → 1000–2100 nas fases 8–13 (doc 05 §7.3);
        /// escorpião (base 3000) → rampa de protótipos nas fases 15–19 (doc 05 §4.5).
        /// </summary>
        private static float BossHpMultiplier(int levelIndex)
        {
            switch (levelIndex)
            {
                case 1: return 0.25f;
                case 2: return 0.55f;
                case 3: return 1.00f;
                case 4: return 1.375f;
                case 5: return 1.75f;
                case 6: return 2.25f;
                case 8: return 1.00f;
                case 9: return 1.15f;
                case 10: return 1.40f;
                case 11: return 1.60f;
                case 12: return 1.85f;
                case 13: return 2.10f;
                case 15: return 0.45f;
                case 16: return 0.55f;
                case 17: return 0.65f;
                case 18: return 0.78f;
                case 19: return 0.90f;
                default: return 1.00f;      // bosses de mundo (7/14/20) usam o HP cheio do asset
            }
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
        /// Pares MANUAIS marcantes para fases-chave (CANON §16 + briefing "quantidade vs
        /// qualidade"). Retorna null para fases sem curadoria — elas caem no autoBalance.
        /// Posições distribuídas ao longo da pista, terminando antes da zona de segurança
        /// (trackLength − 40). Boss da fase (BossForLevel): M1 Fogo · M2 Fogo/Luz, imune Veneno ·
        /// M3 Raio, imune Veneno — os pares de elemento expõem a rota ótima vs armadilha.
        /// </summary>
        private static GateSlot[] BuildManualSlotsForLevel(int levelIndex, float trackLength, GateSet g)
        {
            float p1 = 35f, p2 = trackLength * 0.45f, p3 = trackLength * 0.66f, p4 = trackLength - 45f;

            switch (levelIndex)
            {
                // Fase 2 (CANON §16): PRIMEIRA escolha estratégica real — quantidade vs qualidade.
                // x3 soldados frágeis  vs  virar Arqueiro (menos corpos, muito mais DPS à distância).
                case 2:
                    return new[]
                    {
                        ManualSlot(p1, trackLength, g.TimesThree, g.ClassArcher),
                        ManualSlot(trackLength - 45f, trackLength, g.AddTwentyFive, g.ClassMage)
                    };

                // Fase 3 (boss "uau" Golem, FRACO A FOGO): a rota ótima é elemento Fogo;
                // a armadilha aparentemente boa é o número maior (x5) sem elemento.
                case 3:
                    return new[]
                    {
                        ManualSlot(p1, trackLength, g.TimesTwo, g.AddTwentyFive),
                        ManualSlot(p3, trackLength, g.ElementFire, g.TimesFive),       // Fogo (ótima) vs x5 (armadilha)
                        ManualSlot(p4, trackLength, g.MutWings, g.AddFifty)            // 1ª mutação ofertada
                    };

                // Fase 4: introduz o RISCO honesto + primeira escolha de mutação dupla.
                case 4:
                    return new[]
                    {
                        ManualSlot(p1, trackLength, g.TimesThree, g.ElementFire),
                        ManualSlot(p3, trackLength, g.MutArmor, g.MutSpeed),           // tanque vs velocidade
                        ManualSlot(p4, trackLength, g.RiskTen, g.AddFifty)            // risco vs garantido
                    };

                // Fase 6 (última variante do Golem antes do boss de mundo): qualidade pesada.
                case 6:
                    return new[]
                    {
                        ManualSlot(p1, trackLength, g.TimesThree, g.ClassMage),
                        ManualSlot(p2, trackLength, g.ElementFire, g.TimesFive),
                        ManualSlot(p3, trackLength, g.MutLaser, g.MutSize),
                        ManualSlot(p4, trackLength, g.RiskSacrifice, g.AddFifty)
                    };

                // Fase 8 (abre M2 Cidade Zumbi, Brutamontes IMUNE A VENENO): a armadilha é
                // justamente o portal de Veneno (0 dano vs morto-vivo); ótima é Fogo.
                case 8:
                    return new[]
                    {
                        ManualSlot(p1, trackLength, g.TimesThree, g.ClassNinja),
                        ManualSlot(p3, trackLength, g.ElementFire, g.ElementPoison),   // Fogo (ótima) vs Veneno (armadilha — imune)
                        ManualSlot(trackLength - 45f, trackLength, g.MutArmor, g.AddFifty)
                    };

                // Fase 11: quantidade absurda vs lendária única (x5 soldados vs virar Gigante).
                case 11:
                    return new[]
                    {
                        ManualSlot(p1, trackLength, g.TimesFive, g.ClassGiant),        // multidão vs poucos tanques
                        ManualSlot(p3, trackLength, g.ElementFire, g.MutLaser),
                        ManualSlot(p4, trackLength, g.RiskTitan, g.TimesThree)
                    };

                // Fase 15 (abre M3 Deserto Robótico, Escorpião FRACO A RAIO, IMUNE A VENENO):
                // ótima é Raio; armadilha é Veneno (0 dano vs máquina).
                case 15:
                    return new[]
                    {
                        ManualSlot(p1, trackLength, g.TimesThree, g.ClassMage),
                        ManualSlot(p3, trackLength, g.ElementLightning, g.ElementPoison), // Raio (ótima) vs Veneno (armadilha)
                        ManualSlot(p4, trackLength, g.MutLaser, g.AddFifty)            // Laser (adiciona Raio!) vs número
                    };

                // Fase 18: pico de decisão — risco grande, mutação lendária, elemento certo.
                case 18:
                    return new[]
                    {
                        ManualSlot(p1, trackLength, g.TimesFive, g.ClassGiant),
                        ManualSlot(p2, trackLength, g.ElementLightning, g.TimesFive),
                        ManualSlot(p3, trackLength, g.MutSize, g.MutSpeed),
                        ManualSlot(p4, trackLength, g.RiskTitan, g.AddFifty)
                    };

                default:
                    return null;   // demais fases: autoBalance (rota ótima + armadilha geradas vs boss)
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
