using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MutantArmy.Core;
using MutantArmy.Domain;

namespace MutantArmy.Editor
{
    /// <summary>
    /// MAR Tools/Create MVP Content — gera por código os assets canônicos do MVP:
    /// 8 GateConfigSO (CANON §10) · 5 UnitConfigSO (CANON §5 + doc 03 §3.1/§4) ·
    /// 5 BossConfigSO (CANON §6 + doc 05 §7) · ElementChartSO default (CANON §4) ·
    /// 4 RarityConfigSO (CANON §8) · 4 UpgradeConfigSO (trilhas MVP, CANON §9) ·
    /// 3 RewardConfigSO (CANON §8/§16) · 3 WorldConfigSO (CANON §7/§15) ·
    /// 20 LevelConfigSO com seeds determinísticas e bosses por fase (doc 06 §8).
    /// Idempotente: re-rodar atualiza os assets existentes.
    /// </summary>
    public static class MvpContentFactory
    {
        private const string Root = "Assets/_Project/ScriptableObjects";
        private const string UndoLabel = "Create MVP Content";

        private sealed class UnitSet
        {
            public UnitConfigSO Soldier, Archer, Shieldbearer, Mage, Giant;
        }

        private sealed class GateSet
        {
            public GateConfigSO AddTen, AddTwentyFive, TimesTwo, TimesThree, Half, ClassArcher, ElementFire, RiskTen;
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
            EnsureFolder(Root + "/Upgrades");
            EnsureFolder(Root + "/Rewards");
            EnsureFolder(Root + "/Worlds");
            EnsureFolder(Root + "/Levels");

            UnitSet units = CreateUnits();
            GateSet gates = CreateGates(units);
            CreateElementChart();
            CreateRarities();
            CreateUpgrades();
            RewardSet rewards = CreateRewards(units);
            BossSet bosses = CreateBosses(rewards);
            CreateWorldsAndLevels(bosses, gates, rewards);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MAR Tools: conteúdo MVP criado/atualizado — 8 portais, 5 tropas, 5 bosses, " +
                      "chart elemental, 4 raridades, 4 trilhas de upgrade, 3 recompensas, 3 mundos e 20 fases.");
        }

        // ------------------------------------------------------------------ Units (CANON §5 · doc 03 §3.1/§4)

        private static UnitSet CreateUnits()
        {
            var set = new UnitSet
            {
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
            return set;
        }

        private static UnitConfigSO ConfigureUnit(string assetName, string unitId, Rarity rarity, int supply,
                                                  float hp, float dps, float speed, float range, string ability)
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
            unit.element = ElementType.None;     // MVP: tropas neutras (CANON §15)
            unit.bodyType = BodyType.Organic;
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

        // ------------------------------------------------------------------ Gates (CANON §10)

        private static GateSet CreateGates(UnitSet units)
        {
            Color positive = new Color(0.20f, 0.75f, 1.00f);   // azul/ciano = positivo (doc 09 §4.2)
            Color negative = new Color(1.00f, 0.45f, 0.10f);   // laranja = negativo
            Color special = new Color(0.95f, 0.80f, 0.20f);

            var set = new GateSet();

            set.AddTen = ConfigureGate("Gate_Add10", "gate_add_10", GateType.AddFlat, 10f, "+10", positive);
            set.AddTen.unitToAdd = units.Soldier;

            set.AddTwentyFive = ConfigureGate("Gate_Add25", "gate_add_25", GateType.AddFlat, 25f, "+25", positive);
            set.AddTwentyFive.unitToAdd = units.Soldier;

            set.TimesTwo = ConfigureGate("Gate_X2", "gate_x2", GateType.Multiply, 2f, "x2", positive);
            set.TimesTwo.unitToAdd = units.Soldier;

            set.TimesThree = ConfigureGate("Gate_X3", "gate_x3", GateType.Multiply, 3f, "x3", positive);
            set.TimesThree.unitToAdd = units.Soldier;

            set.Half = ConfigureGate("Gate_Div2", "gate_div2", GateType.Multiply, 0.5f, "÷2", negative);

            set.ClassArcher = ConfigureGate("Gate_ClassArcher", "gate_class_archer",
                GateType.ClassConvert, 1f, "VIRAR ARQUEIRO", positive);
            set.ClassArcher.unitToAdd = units.Archer;

            set.ElementFire = ConfigureGate("Gate_ElementFire", "gate_element_fire",
                GateType.Element, 0f, "FOGO", new Color(1.00f, 0.35f, 0.15f));
            set.ElementFire.element = ElementType.Fire;

            // Rótulo HONESTO com as odds visíveis (CANON §3.4) — o RNG usa exatamente estas odds.
            set.RiskTen = ConfigureGate("Gate_RiskX10", "gate_risk_x10", GateType.Risk, 0f,
                "70% x10 / 30% −½", special);
            set.RiskTen.riskSuccessChance = 0.7f;
            set.RiskTen.riskRewardMult = 10f;
            set.RiskTen.riskFailPenalty = 0.5f;

            EditorUtility.SetDirty(set.AddTen);
            EditorUtility.SetDirty(set.AddTwentyFive);
            EditorUtility.SetDirty(set.TimesTwo);
            EditorUtility.SetDirty(set.TimesThree);
            EditorUtility.SetDirty(set.ClassArcher);
            EditorUtility.SetDirty(set.ElementFire);
            EditorUtility.SetDirty(set.RiskTen);
            return set;
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
            set.WorldBoss = ConfigureReward("Reward_WorldBoss", coins: 0, gems: 10, playerXp: 0,
                chest: ChestType.Rare, cardDropChance: 0.30f, shardAmount: 10,
                cardPool: new[] { units.Archer, units.Shieldbearer, units.Mage, units.Giant });

            // CANON §6: todo boss tem recompensa especial + chance de drop de carta/fragmento.
            // Valores não fixados pelo CANON (drop chance) são defaults recalibráveis por RC.
            set.BossDefault = ConfigureReward("Reward_BossDefault", coins: 0, gems: 0, playerXp: 0,
                chest: ChestType.None, cardDropChance: 0.10f, shardAmount: 10,
                cardPool: new[] { units.Archer, units.Shieldbearer, units.Mage });

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
            set.Golem = ConfigureBoss("Boss_M1_GolemStone", "m1_golem_stone",
                weaknesses: new[] { ElementType.Fire }, immunities: new ElementType[0],
                bodyType: BodyType.Organic, maxHp: 400f, contactDps: 6f,
                entranceSeconds: 1.8f, telegraphSeconds: 1.2f,
                specialDamage: 25f, specialArea: 3f, specialCooldown: 3.5f,
                killReward: rewards.BossDefault);

            set.WoodGiant = ConfigureBoss("Boss_M1_WoodGiant", "m1_final_wood_giant",
                new[] { ElementType.Fire }, new ElementType[0],
                BodyType.Organic, 1600f, 8f, 2.0f, 1.2f, 30f, 4f, 3.5f,
                rewards.WorldBoss);

            set.Bruiser = ConfigureBoss("Boss_M2_ZombieBruiser", "m2_zombie_bruiser",
                new[] { ElementType.Fire }, new[] { ElementType.Poison },
                BodyType.Undead, 1000f, 8f, 1.6f, 1.0f, 28f, 2f, 3.0f,
                rewards.BossDefault);

            // Fraquezas listadas com Luz por consistência de canon (Luz é pós-MVP — doc 05 §7.4).
            set.Titan = ConfigureBoss("Boss_M2_ZombieTitan", "m2_final_zombie_titan",
                new[] { ElementType.Fire, ElementType.Light }, new[] { ElementType.Poison },
                BodyType.Undead, 2800f, 10f, 2.0f, 1.2f, 35f, 2.5f, 3.5f,
                rewards.WorldBoss);

            set.Scorpion = ConfigureBoss("Boss_M3_ScorpionMech", "m3_final_scorpion_mech",
                new[] { ElementType.Lightning }, new[] { ElementType.Poison },
                BodyType.Machine, 3000f, 12f, 2.0f, 1.1f, 40f, 4f, 3.0f,
                rewards.WorldBoss);

            return set;
        }

        private static BossConfigSO ConfigureBoss(string assetName, string bossId,
                                                  ElementType[] weaknesses, ElementType[] immunities,
                                                  BodyType bodyType, float maxHp, float contactDps,
                                                  float entranceSeconds, float telegraphSeconds,
                                                  float specialDamage, float specialArea, float specialCooldown,
                                                  RewardConfigSO killReward)
        {
            var boss = LoadOrCreate<BossConfigSO>(Root + "/Bosses/" + assetName + ".asset");
            boss.bossId = bossId;
            boss.displayNameKey = bossId + "_name";
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

        private static void CreateWorldsAndLevels(BossSet bosses, GateSet gates, RewardSet rewards)
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
                level.gateSlots = levelIndex == 1
                    ? BuildOnboardingSlots(level.trackLength, gates)
                    : BuildAutoSlots(SlotCount(level.trackLength), level.trackLength);

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
