using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using MutantArmy.Core;
using MutantArmy.Domain;
using Object = UnityEngine.Object;

namespace MutantArmy.Editor
{
    /// <summary>
    /// MAR Tools/Build Unit Visuals — liga a arte CC0 importada (KayKit Adventurers/Skeletons +
    /// Quaternius Ultimate Monsters; mapa em _assets-staging\PLANO-DE-USO.md §1.1/§1.4 + roster
    /// canônico CANON §5) aos SOs do MVP, tudo por código:
    /// 1. TROPAS (19, CANON §5): prefab VARIANT por tropa em Prefabs/Units com AnimatorController
    ///    criado via UnityEditor.Animations (Idle/Run/Attack trocados pelo int "State" — contrato
    ///    do CrowdViewPool) usando os clipes EMBUTIDOS no FBX (busca por nome contendo
    ///    idle/run|walk/attack; fallback 1º clipe). Escala por papel (tropa 0.6; Corredor leve
    ///    0.55; epicos 0.7–0.85; Gigante 1.3; lendarios 1.4–2.2). Cor de CLASSE viva e dominante
    ///    (material URP/Lit base color direto + emission leve 0.15) → UnitConfigSO.viewPrefab.
    ///    Se a tropa ainda não tem SO (MvpContentFactory cria só as 5 do MVP), ele é
    ///    LOAD-OR-CREATE com a identidade canônica do roster — nunca sobrescreve stats de SO
    ///    existente (só preenche viewPrefab); assim re-rodar só este factory já basta.
    /// 2. BOSSES (5): prefab por boss em Prefabs/Bosses (escala 3.5–5, controller Idle(0)/
    ///    Attack(1) — contrato do BossManager) → BossConfigSO.prefab. M3 Robô Escorpião =
    ///    Alien recolor metálico (PLACEHOLDER da Lacuna L2 do staging).
    /// 3. MUTAÇÕES VISÍVEIS (CANON §3.3): biblioteca de materiais de tint + acessórios "hero"
    ///    (asas/laser/armadura/tamanho) em Art/Materials/Mutations + Prefabs/Mutations, nomeados
    ///    por convenção a partir do shaderVariantFlag, p/ o runtime aplicar via MaterialPropertyBlock
    ///    sobre a multidão. Tamanho já é coberto por CrowdManager.MutationSizeMult; cor/emission
    ///    exige um hook de 1 linha no CrowdRenderer (ver avisos_integracao).
    /// FBX ausente = aviso + skip (fallback instanced/greybox fica intacto). Campos dos SOs
    /// preenchidos via SerializedObject. Idempotente: re-rodar atualiza no lugar.
    /// </summary>
    public static class UnitVisualFactory
    {
        private const string SoRoot = "Assets/_Project/ScriptableObjects";
        private const string ModelsRoot = "Assets/_Project/Art/Models";
        private const string AnimFolder = "Assets/_Project/Art/Animations";
        private const string ViewMaterialsFolder = "Assets/_Project/Art/Materials/Views";
        private const string MutationMaterialsFolder = "Assets/_Project/Art/Materials/Mutations";
        private const string UnitsPrefabFolder = "Assets/_Project/Prefabs/Units";
        private const string BossesPrefabFolder = "Assets/_Project/Prefabs/Bosses";
        private const string MutationsPrefabFolder = "Assets/_Project/Prefabs/Mutations";

        private sealed class ClipSet
        {
            public AnimationClip Idle, Run, Attack;
        }

        /// <summary>
        /// Linha do roster canônico (CANON §5 + mapa de modelos do brief): tudo que o factory
        /// precisa p/ montar a tropa — identidade (caso o SO ainda não exista), modelo CC0,
        /// escala por papel e cor de classe VIVA e dominante.
        /// </summary>
        private sealed class TroopSpec
        {
            public string AssetName;        // Unit_Soldier...
            public string UnitId;           // soldier...
            public string ModelName;        // FBX base (nome exato do arquivo)
            public string PreferFolder;     // desempate de pasta (Adventurers/Skeletons/Bosses/Big/Flying)
            public Rarity Rarity;
            public int Supply;
            public ElementType Element;     // dano nativo (CANON §4/§5)
            public BodyType Body;
            public float Scale;             // por papel
            public Color ClassColor;        // cor de classe dominante (silhueta legível na multidão)
        }

        // ordem dos keywords = ordem de preferência (varre todos os clipes por keyword)
        private static readonly string[] IdleKeywords = { "idle" };
        private static readonly string[] RunKeywords = { "run", "walk", "move", "fly", "flying" };
        private static readonly string[] AttackKeywords =
            { "attack", "melee", "chop", "slice", "slash", "punch", "bite", "shoot", "cast", "spell", "headbutt" };

        // ============================================================ ROSTER (CANON §5)
        // Cores de classe: VIVAS, saturadas e distintas (doc 01 §6) — silhueta lê à distância
        // numa multidão de 60. Os 5 primeiros são os do MVP (NÃO quebrar): mesmas cores de antes.
        private static readonly TroopSpec[] Roster =
        {
            // -------- Comuns
            new TroopSpec { AssetName = "Unit_Soldier",      UnitId = "soldier",      ModelName = "Barbarian",   PreferFolder = "Adventurers", Rarity = Rarity.Common,    Supply = 1,  Element = ElementType.None,      Body = BodyType.Organic, Scale = 0.6f,  ClassColor = new Color(0.16f, 0.52f, 1.00f) }, // azul vivo
            new TroopSpec { AssetName = "Unit_Archer",       UnitId = "archer",       ModelName = "Rogue",       PreferFolder = "Adventurers", Rarity = Rarity.Common,    Supply = 2,  Element = ElementType.None,      Body = BodyType.Organic, Scale = 0.6f,  ClassColor = new Color(0.22f, 0.82f, 0.32f) }, // verde vivo
            new TroopSpec { AssetName = "Unit_Shieldbearer", UnitId = "shieldbearer", ModelName = "Knight",      PreferFolder = "Adventurers", Rarity = Rarity.Common,    Supply = 3,  Element = ElementType.None,      Body = BodyType.Organic, Scale = 0.6f,  ClassColor = new Color(1.00f, 0.80f, 0.16f) }, // amarelo/dourado
            new TroopSpec { AssetName = "Unit_Runner",       UnitId = "runner",       ModelName = "RogueHooded", PreferFolder = "Adventurers", Rarity = Rarity.Common,    Supply = 1,  Element = ElementType.None,      Body = BodyType.Organic, Scale = 0.55f, ClassColor = new Color(0.20f, 0.85f, 0.85f) }, // ciano
            // -------- Raros
            new TroopSpec { AssetName = "Unit_Mage",         UnitId = "mage",         ModelName = "Mage",        PreferFolder = "Adventurers", Rarity = Rarity.Rare,      Supply = 4,  Element = ElementType.None,      Body = BodyType.Organic, Scale = 0.6f,  ClassColor = new Color(0.66f, 0.30f, 0.98f) }, // roxo vivo
            new TroopSpec { AssetName = "Unit_Ninja",        UnitId = "ninja",        ModelName = "RogueHooded", PreferFolder = "Adventurers", Rarity = Rarity.Rare,      Supply = 3,  Element = ElementType.None,      Body = BodyType.Organic, Scale = 0.6f,  ClassColor = new Color(0.25f, 0.25f, 0.30f) }, // cinza-escuro
            new TroopSpec { AssetName = "Unit_FlameTrooper", UnitId = "flametrooper", ModelName = "Barbarian",   PreferFolder = "Adventurers", Rarity = Rarity.Rare,      Supply = 4,  Element = ElementType.Fire,      Body = BodyType.Organic, Scale = 0.6f,  ClassColor = new Color(1.00f, 0.45f, 0.10f) }, // laranja-fogo
            new TroopSpec { AssetName = "Unit_FrostTrooper", UnitId = "frosttrooper", ModelName = "Mage",        PreferFolder = "Adventurers", Rarity = Rarity.Rare,      Supply = 4,  Element = ElementType.Ice,       Body = BodyType.Organic, Scale = 0.6f,  ClassColor = new Color(0.55f, 0.85f, 1.00f) }, // azul-gelo
            new TroopSpec { AssetName = "Unit_Medic",        UnitId = "medic",        ModelName = "Mage",        PreferFolder = "Adventurers", Rarity = Rarity.Rare,      Supply = 4,  Element = ElementType.None,      Body = BodyType.Organic, Scale = 0.6f,  ClassColor = new Color(0.85f, 1.00f, 0.85f) }, // branco-verde
            // -------- Épicos (Quaternius monsters p/ os robustos)
            new TroopSpec { AssetName = "Unit_Robot",        UnitId = "robot",        ModelName = "Yeti",        PreferFolder = "Bosses",      Rarity = Rarity.Epic,      Supply = 8,  Element = ElementType.None,      Body = BodyType.Machine, Scale = 0.85f, ClassColor = new Color(0.70f, 0.75f, 0.80f) }, // metalico
            new TroopSpec { AssetName = "Unit_Giant",        UnitId = "giant",        ModelName = "Barbarian",   PreferFolder = "Adventurers", Rarity = Rarity.Epic,      Supply = 12, Element = ElementType.None,      Body = BodyType.Organic, Scale = 1.3f,  ClassColor = new Color(1.00f, 0.42f, 0.14f) }, // laranja/vermelho
            new TroopSpec { AssetName = "Unit_Necromancer",  UnitId = "necromancer",  ModelName = "Skeleton_Mage", PreferFolder = "Skeletons", Rarity = Rarity.Epic,      Supply = 8,  Element = ElementType.Shadow,    Body = BodyType.Undead,  Scale = 0.7f,  ClassColor = new Color(0.45f, 0.20f, 0.55f) }, // roxo-escuro
            new TroopSpec { AssetName = "Unit_Engineer",     UnitId = "engineer",     ModelName = "Knight",      PreferFolder = "Adventurers", Rarity = Rarity.Epic,      Supply = 8,  Element = ElementType.None,      Body = BodyType.Organic, Scale = 0.6f,  ClassColor = new Color(0.95f, 0.55f, 0.15f) }, // laranja-tech
            new TroopSpec { AssetName = "Unit_Alien",        UnitId = "alien",        ModelName = "Alien",       PreferFolder = "Bosses",      Rarity = Rarity.Epic,      Supply = 8,  Element = ElementType.Alien,     Body = BodyType.Organic, Scale = 0.85f, ClassColor = new Color(0.45f, 1.00f, 0.35f) }, // verde-alien
            // -------- Lendários (grandes; Quaternius monsters)
            new TroopSpec { AssetName = "Unit_Dragon",       UnitId = "dragon",       ModelName = "Dragon_Evolved", PreferFolder = "Bosses",   Rarity = Rarity.Legendary, Supply = 20, Element = ElementType.Fire,      Body = BodyType.Organic, Scale = 1.6f,  ClassColor = new Color(1.00f, 0.35f, 0.10f) }, // dourado-vermelho
            new TroopSpec { AssetName = "Unit_Titan",        UnitId = "titan",        ModelName = "Orc_Skull",   PreferFolder = "Bosses",      Rarity = Rarity.Legendary, Supply = 25, Element = ElementType.None,      Body = BodyType.Organic, Scale = 2.2f,  ClassColor = new Color(1.00f, 0.78f, 0.20f) }, // dourado
            new TroopSpec { AssetName = "Unit_WarAngel",     UnitId = "warangel",     ModelName = "Mage",        PreferFolder = "Adventurers", Rarity = Rarity.Legendary, Supply = 18, Element = ElementType.Light,     Body = BodyType.Organic, Scale = 0.7f,  ClassColor = new Color(1.00f, 0.95f, 0.70f) }, // branco-dourado
            new TroopSpec { AssetName = "Unit_Demon",        UnitId = "demon",        ModelName = "Demon",       PreferFolder = "Bosses",      Rarity = Rarity.Legendary, Supply = 20, Element = ElementType.Shadow,    Body = BodyType.Organic, Scale = 1.4f,  ClassColor = new Color(0.50f, 0.15f, 0.60f) }, // roxo-sombrio
            // Mecha Supremo: sem mech CC0 no staging (Lacuna L2) — Goleling golem-tech recolor ciano
            new TroopSpec { AssetName = "Unit_Mecha",        UnitId = "mecha",        ModelName = "Goleling_Evolved", PreferFolder = "Bosses", Rarity = Rarity.Legendary, Supply = 25, Element = ElementType.Lightning, Body = BodyType.Machine, Scale = 1.8f,  ClassColor = new Color(0.20f, 0.90f, 1.00f) }, // ciano-tech
        };

        /// <summary>
        /// Espelho VISUAL das mutações do MVP (CANON §3.3). O efeito de gameplay (multiplicadores,
        /// flight, addsElement) é do MutationConfigSO/CrowdManager — aqui só o lado de ARTE:
        /// tint + emissão + acessório "hero". Flag = bit do shaderVariantFlag (convenção com o
        /// runtime); Tint/Emission alimentam o MaterialPropertyBlock; Attachment é o prop de
        /// asas/laser/armadura nas unidades próximas à câmera (UnitConfig.attachmentPrefab).
        /// </summary>
        private sealed class MutationVisualSpec
        {
            public string Id;               // wings/laser/armor/size
            public int Flag;                // bit (1,2,4,8) — casa com MutationConfigSO.shaderVariantFlag
            public Color Tint;              // cor dominante aplicada ao exército
            public float Emission;          // intensidade de emissão (pop sob bloom)
            public string AttachModel;      // FBX do acessório hero (null = só tint/escala)
            public Vector3 AttachOffset;    // posição local do acessório
            public float AttachScale;
        }

        private static readonly MutationVisualSpec[] MutationVisuals =
        {
            // asas: voo + tamanho (CrowdManager já escala) + tint céu-claro brilhante. Sem prop
            // hero dedicado: o staging não tem mesh de asa importado (o monstro voador Armabee
            // não foi trazido p/ Art/Models) — a leitura vem da escala+voo+emissão. Se um FBX de
            // asa for importado depois, basta preencher AttachModel aqui.
            new MutationVisualSpec { Id = "wings", Flag = 1, Tint = new Color(0.85f, 0.92f, 1.00f), Emission = 0.25f, AttachModel = null, AttachOffset = new Vector3(0f, 1.4f, -0.2f), AttachScale = 0.6f },
            // laser: dano de Raio — tint ciano elétrico + emissão forte; acessório = wand/staff
            new MutationVisualSpec { Id = "laser", Flag = 2, Tint = new Color(0.30f, 0.95f, 1.00f), Emission = 0.55f, AttachModel = "staff",  AttachOffset = new Vector3(0.25f, 0.9f, 0.1f), AttachScale = 0.5f },
            // armadura: tint metálico frio + emissão baixa; acessório = shield_square
            new MutationVisualSpec { Id = "armor", Flag = 4, Tint = new Color(0.72f, 0.78f, 0.85f), Emission = 0.12f, AttachModel = "shield_square", AttachOffset = new Vector3(-0.25f, 0.9f, 0.15f), AttachScale = 0.6f },
            // tamanho: só escala (já coberta pelo MutationSizeMult) + leve tint quente p/ leitura
            new MutationVisualSpec { Id = "size",  Flag = 8, Tint = new Color(1.00f, 0.75f, 0.45f), Emission = 0.15f, AttachModel = null,     AttachOffset = Vector3.zero, AttachScale = 1f },
        };

        [MenuItem("MAR Tools/Build Unit Visuals")]
        public static void BuildAll()
        {
            EnsureFolder(AnimFolder);
            EnsureFolder(ViewMaterialsFolder);
            EnsureFolder(MutationMaterialsFolder);
            EnsureFolder(UnitsPrefabFolder);
            EnsureFolder(BossesPrefabFolder);
            EnsureFolder(MutationsPrefabFolder);

            int troops = 0;
            // ---- Tropas (19, CANON §5). Os 5 primeiros são o MVP travado (NÃO quebrar):
            // mesmas cores/escala/modelos de antes. Os 14 restantes são adicionados aqui.
            for (int i = 0; i < Roster.Length; i++)
                if (BuildUnit(Roster[i])) troops++;

            int bosses = 0;
            // ---- Bosses (PLANO §1.4): monstros Quaternius, escala 3.5–5, recolor por boss.
            // Tints CLAREADOS (doc 01 §6): o atlas Quaternius é escuro; cores claras + emissão
            // leve = chefão imponente que lê inteiro sob a luz da cena sem virar plástico.
            if (BuildBoss("Boss_M1_GolemStone", "Goleling_Evolved", "Flying", 4.0f,
                          new Color(0.82f, 0.82f, 0.88f), 0.05f, 0.30f)) bosses++;
            if (BuildBoss("Boss_M1_WoodGiant", "Tribal", "Big", 4.0f,
                          new Color(0.78f, 0.56f, 0.34f), 0.00f, 0.30f)) bosses++;
            if (BuildBoss("Boss_M2_ZombieBruiser", "Orc", "Big", 3.5f,
                          new Color(0.66f, 0.84f, 0.48f), 0.00f, 0.30f)) bosses++;
            if (BuildBoss("Boss_M2_ZombieTitan", "Orc_Skull", "Big", 5.0f,
                          new Color(0.78f, 0.82f, 0.78f), 0.00f, 0.30f)) bosses++;
            if (BuildBoss("Boss_M3_ScorpionMech", "Alien", "Big", 4.0f,
                          new Color(0.78f, 0.84f, 0.92f), 0.85f, 0.70f))
            {
                bosses++;
                Debug.LogWarning("MAR Tools: Boss M3 'Robô Escorpião' usa PLACEHOLDER (Alien recolor " +
                                 "metálico) — Lacuna L2 do PLANO-DE-USO; trocar pelo mech CC0 quando baixado.");
            }

            // ---- Mutações visíveis (CANON §3.3): materiais de tint + acessórios hero.
            int mutations = BuildMutationVisuals();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"MAR Tools: visuais construídos — {troops}/{Roster.Length} tropas, {bosses}/5 bosses, " +
                      $"{mutations}/{MutationVisuals.Length} mutações (faltantes mantêm fallback instanced/greybox; ver avisos acima).");
        }

        // ------------------------------------------------------------------ tropas

        private static bool BuildUnit(TroopSpec spec)
        {
            // SO load-or-create: MvpContentFactory só cria as 5 do MVP; as 14 novas nascem aqui
            // com a identidade canônica do roster, mas SEM tocar stats de um SO já existente.
            UnitConfigSO unit = EnsureUnitConfig(spec);
            if (unit == null) return false;

            GameObject model = FindModel(spec.ModelName, spec.PreferFolder);
            if (model == null) return false;   // FindModel já avisou; viewPrefab fica como está (fallback instanced)

            string fbxPath = AssetDatabase.GetAssetPath(model);
            PrepareImporter(fbxPath);
            ClipSet clips = FindClips(fbxPath, spec.ModelName);

            // tropas: 3 estados — Idle(0)/Run(1)/Attack(2), contrato do CrowdViewPool
            AnimatorController controller = BuildController(
                AnimFolder + "/AC_" + spec.AssetName + ".controller",
                clips.Idle, clips.Run, clips.Attack, attackValue: 2);

            // Tropa SEMPRE recebe a cor de classe vibrante (nunca o atlas marrom cru do KayKit):
            // material URP/Lit com _BaseColor claro dominante, smoothness baixa e leve emissão
            // da própria cor p/ "pop" sob bloom (doc 01 §6).
            Material overrideMaterial = UnitClassMaterial("M_View_" + spec.AssetName, spec.ClassColor);

            GameObject prefab = BuildViewPrefab(
                model, UnitsPrefabFolder + "/" + spec.AssetName + "_View.prefab",
                spec.Scale, controller, overrideMaterial);

            SetObjectField(unit, "viewPrefab", prefab);
            return true;
        }

        /// <summary>
        /// Carrega o UnitConfigSO da tropa; se não existir, cria com a IDENTIDADE canônica do
        /// roster (unitId/raridade/supply/elemento/corpo) + curvas de nível padrão, p/ que rodar
        /// SÓ este factory já entregue uma tropa válida. Stats finos (hp/dps/range/ability) ficam
        /// para o MvpContentFactory — se o SO já existe, NADA dele é sobrescrito aqui.
        /// </summary>
        private static UnitConfigSO EnsureUnitConfig(TroopSpec spec)
        {
            string path = SoRoot + "/Units/" + spec.AssetName + ".asset";
            var unit = AssetDatabase.LoadAssetAtPath<UnitConfigSO>(path);
            if (unit != null) return unit;

            EnsureFolder(SoRoot + "/Units");
            unit = ScriptableObject.CreateInstance<UnitConfigSO>();
            unit.unitId = spec.UnitId;
            unit.displayNameKey = spec.UnitId + "_name";
            unit.rarity = spec.Rarity;
            unit.supplyCost = spec.Supply;
            unit.element = spec.Element;
            unit.bodyType = spec.Body;
            // baseline mínimo proporcional ao Supply (CANON §5: DPS+HP por ponto de Supply ≈ const):
            // valores de partida só p/ a tropa funcionar; o MvpContentFactory refina depois.
            unit.baseHp = 8f * spec.Supply + 6f;
            unit.baseDps = 2f * spec.Supply + 1f;
            unit.moveSpeed = Mathf.Lerp(5.0f, 3.3f, Mathf.InverseLerp(1f, 25f, spec.Supply));
            unit.attackRange = 1.5f;
            unit.specialAbilityId = spec.UnitId + "_ability";
            unit.levelHpCurve = DefaultLevelCurve();
            unit.levelDpsCurve = DefaultLevelCurve();
            AssetDatabase.CreateAsset(unit, path);
            Debug.Log($"MAR Tools: '{spec.AssetName}.asset' criado pelo UnitVisualFactory (roster CANON §5); " +
                      "stats finos ficam p/ o Create MVP Content quando for estendido.");
            return unit;
        }

        /// <summary>Escala de nível 1–10: ×1,15^(n−1) p/ HP e DPS (doc 03 §5) — só p/ SOs novos.</summary>
        private static AnimationCurve DefaultLevelCurve()
        {
            var keys = new Keyframe[10];
            for (int n = 1; n <= 10; n++)
                keys[n - 1] = new Keyframe(n, Mathf.Pow(1.15f, n - 1));
            return new AnimationCurve(keys);
        }

        // ------------------------------------------------------------------ bosses

        private static bool BuildBoss(string bossAssetName, string modelName, string preferPathFragment,
                                      float scale, Color tint, float metallic, float smoothness)
        {
            var boss = AssetDatabase.LoadAssetAtPath<BossConfigSO>(SoRoot + "/Bosses/" + bossAssetName + ".asset");
            if (boss == null)
            {
                Debug.LogWarning($"MAR Tools: {bossAssetName}.asset não existe — rode MAR Tools/Create MVP Content antes.");
                return false;
            }

            GameObject model = FindModel(modelName, preferPathFragment);
            if (model == null) return false;   // mantém o que estiver no campo (Boss_Greybox ou nulo→cápsula runtime)

            string fbxPath = AssetDatabase.GetAssetPath(model);
            PrepareImporter(fbxPath);
            ClipSet clips = FindClips(fbxPath, modelName);

            // bosses: 2 estados — Idle(0)/Attack(1), contrato do BossManager.ApplyViewAnim
            AnimatorController controller = BuildController(
                AnimFolder + "/AC_" + bossAssetName + ".controller",
                clips.Idle, null, clips.Attack, attackValue: 1);

            Material material = TintMaterial("M_View_" + bossAssetName, model, tint, metallic, smoothness);

            GameObject prefab = BuildViewPrefab(
                model, BossesPrefabFolder + "/" + bossAssetName + "_View.prefab",
                scale, controller, material);

            SetObjectField(boss, "prefab", prefab);
            return true;
        }

        // ------------------------------------------------------------------ mutações visíveis

        /// <summary>
        /// Gera os ASSETS de arte das mutações (CANON §3.3): 1 material de tint por mutação
        /// (M_Mutation_&lt;id&gt;, nomeado também pelo bit do shaderVariantFlag) + 1 prefab de
        /// acessório "hero" (Attach_Mutation_&lt;id&gt;) quando há modelo. O runtime aplica o tint
        /// sobre a multidão via MaterialPropertyBlock e instancia o acessório só nas unidades
        /// próximas à câmera (ver avisos_integracao p/ o hook). Tamanho já é coberto pelo
        /// MutationSizeMult — aqui o "size" só ganha tint p/ leitura.
        /// </summary>
        private static int BuildMutationVisuals()
        {
            int built = 0;
            for (int i = 0; i < MutationVisuals.Length; i++)
            {
                MutationVisualSpec m = MutationVisuals[i];

                // material de tint: URP/Lit aditivo-friendly p/ o MaterialPropertyBlock do runtime
                MutationTintMaterial("M_Mutation_" + m.Id + "_flag" + m.Flag, m.Tint, m.Emission);

                // acessório hero (asas/laser/armadura): prefab leve com o FBX, sem Animator
                if (!string.IsNullOrEmpty(m.AttachModel))
                {
                    GameObject model = FindModel(m.AttachModel, null);
                    if (model != null)
                        BuildAttachmentPrefab(
                            model, MutationsPrefabFolder + "/Attach_Mutation_" + m.Id + ".prefab",
                            m.AttachOffset, m.AttachScale, m.Tint, m.Emission);
                    else
                        Debug.LogWarning($"MAR Tools: acessório '{m.AttachModel}' da mutação '{m.Id}' não " +
                                         "encontrado — mutação fica só com tint/escala (sem prop hero).");
                }

                // tenta ligar o acessório no SO da mutação (se MvpContentFactory já tiver criado)
                TryWireMutationSo(m);
                built++;
            }
            return built;
        }

        /// <summary>
        /// Se o MutationConfigSO existir (ScriptableObjects/Mutations/Mutation_&lt;id&gt;.asset),
        /// preenche shaderVariantFlag e attachmentPrefab — sem criar o SO (gameplay é do
        /// MvpContentFactory). Ausente = silencioso: os assets de arte já ficam prontos por
        /// convenção de nome p/ o runtime/factory de conteúdo consumir.
        /// </summary>
        private static void TryWireMutationSo(MutationVisualSpec m)
        {
            string path = SoRoot + "/Mutations/Mutation_" + m.Id + ".asset";
            var so = AssetDatabase.LoadAssetAtPath<MutationConfigSO>(path);
            if (so == null) return;

            SetIntField(so, "shaderVariantFlag", m.Flag);
            if (!string.IsNullOrEmpty(m.AttachModel))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    MutationsPrefabFolder + "/Attach_Mutation_" + m.Id + ".prefab");
                if (prefab != null) SetObjectField(so, "attachmentPrefab", prefab);
            }
        }

        // ------------------------------------------------------------------ FBX / importer

        /// <summary>
        /// Acha o FBX por nome EXATO de arquivo (evita "Orc" casar "Orc_Skull"), preferindo
        /// Art/Models e, em empate (ex.: Tribal existe em Big e Flying), o caminho que
        /// contém o fragmento pedido. Ausente = aviso (assets ainda não importados).
        /// </summary>
        private static GameObject FindModel(string modelName, string preferPathFragment)
        {
            string[] folders = AssetDatabase.IsValidFolder(ModelsRoot)
                ? new[] { ModelsRoot }
                : new[] { "Assets" };
            string[] guids = AssetDatabase.FindAssets(modelName + " t:Model", folders);

            string best = null;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!string.Equals(file, modelName, StringComparison.OrdinalIgnoreCase)) continue;
                if (best == null) best = path;
                if (!string.IsNullOrEmpty(preferPathFragment) &&
                    path.IndexOf(preferPathFragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    best = path;
                    break;
                }
            }

            if (best == null)
            {
                Debug.LogWarning($"MAR Tools: modelo '{modelName}.fbx' não encontrado em '{folders[0]}' — " +
                                 "importe os assets do staging (PLANO-DE-USO.md) e rode Build Unit Visuals " +
                                 "de novo; até lá o fallback instanced/greybox continua valendo.");
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(best);
        }

        /// <summary>
        /// Garante animação utilizável no FBX: rig Generic (KayKit/Quaternius), importAnimation
        /// ligado e loopTime nos clipes embutidos — sem loop, Run/Idle congelam no último frame.
        /// </summary>
        private static void PrepareImporter(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return;

            bool reimport = false;
            if (importer.animationType == ModelImporterAnimationType.None)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                reimport = true;
            }
            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                reimport = true;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            bool clipChanged = false;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].loopTime) continue;
                clips[i].loopTime = true;
                clipChanged = true;
            }
            if (clipChanged)
            {
                importer.clipAnimations = clips;
                reimport = true;
            }

            if (reimport) importer.SaveAndReimport();
        }

        private static ClipSet FindClips(string fbxPath, string modelName)
        {
            var clips = new List<AnimationClip>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                var clip = o as AnimationClip;
                if (clip == null) continue;
                if (clip.name.StartsWith("__preview", StringComparison.OrdinalIgnoreCase)) continue;
                clips.Add(clip);
            }

            var set = new ClipSet
            {
                Idle = FindClip(clips, IdleKeywords),
                Run = FindClip(clips, RunKeywords),
                Attack = FindClip(clips, AttackKeywords)
            };

            AnimationClip first = clips.Count > 0 ? clips[0] : null;
            if (first == null)
                Debug.LogWarning($"MAR Tools: '{modelName}' importou SEM clipes de animação — " +
                                 "estados ficarão sem motion (T-pose); revisar import do FBX.");
            if (set.Idle == null) set.Idle = first;     // fallback: 1º clipe embutido
            if (set.Run == null) set.Run = set.Idle;
            if (set.Attack == null) set.Attack = set.Idle;
            return set;
        }

        private static AnimationClip FindClip(List<AnimationClip> clips, string[] keywords)
        {
            foreach (string keyword in keywords)
                foreach (AnimationClip clip in clips)
                    if (clip.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        return clip;
            return null;
        }

        // ------------------------------------------------------------------ animator controller

        /// <summary>
        /// Controller mínimo POR CÓDIGO (UnityEditor.Animations): int "State" + AnyState →
        /// estado com condição Equals — Idle(0) sempre; Run(1) se houver clipe; Attack(attackValue).
        /// Rebuild idempotente IN-PLACE: mantém o GUID (referências dos prefabs não quebram).
        /// </summary>
        private static AnimatorController BuildController(string path, AnimationClip idleClip,
                                                          AnimationClip runClip, AnimationClip attackClip,
                                                          int attackValue)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            controller.parameters = new AnimatorControllerParameter[0];   // reset atômico (setter oficial)
            controller.AddParameter("State", AnimatorControllerParameterType.Int);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (AnimatorStateTransition t in sm.anyStateTransitions)
                sm.RemoveAnyStateTransition(t);
            foreach (ChildAnimatorState child in sm.states)
                sm.RemoveState(child.state);

            AnimatorState idle = AddState(sm, "Idle", idleClip, 0);
            sm.defaultState = idle;
            if (runClip != null) AddState(sm, "Run", runClip, 1);
            if (attackClip != null) AddState(sm, "Attack", attackClip, attackValue);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorState AddState(AnimatorStateMachine sm, string name, AnimationClip clip, int value)
        {
            AnimatorState state = sm.AddState(name);
            state.motion = clip;
            AnimatorStateTransition t = sm.AddAnyStateTransition(state);
            t.AddCondition(AnimatorConditionMode.Equals, value, "State");
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = 0.12f;             // blend curto: troca legível sem pop
            t.canTransitionToSelf = false;  // crítico com AnyState: senão o estado reinicia todo frame
            return state;
        }

        // ------------------------------------------------------------------ prefab variant + material

        private static GameObject BuildViewPrefab(GameObject model, string outPath, float scale,
                                                  AnimatorController controller, Material overrideMaterial)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                instance.transform.localScale = new Vector3(scale, scale, scale);

                Animator animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;   // posição vem dos arrays SoA / do BossManager
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                if (overrideMaterial != null)
                {
                    foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
                    {
                        Material[] mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++) mats[i] = overrideMaterial;
                        r.sharedMaterials = mats;
                    }
                }

                // SaveAsPrefabAsset sobre instância de prefab = PREFAB VARIANT (mesmo GUID se já existe)
                return PrefabUtility.SaveAsPrefabAsset(instance, outPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// Acessório "hero" de mutação (asas/laser/armadura): instancia o FBX num GameObject
        /// raiz com offset/escala locais, recolorido pelo tint da mutação, SEM Animator (prop
        /// estático anexado pelo runtime só nas unidades próximas à câmera — doc da MutationConfigSO).
        /// </summary>
        private static GameObject BuildAttachmentPrefab(GameObject model, string outPath, Vector3 offset,
                                                        float scale, Color tint, float emission)
        {
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(outPath));
            try
            {
                var child = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                child.transform.localPosition = offset;
                child.transform.localScale = new Vector3(scale, scale, scale);

                Material mat = MutationTintMaterial(
                    "M_Attach_" + System.IO.Path.GetFileNameWithoutExtension(outPath), tint, emission);
                foreach (Renderer r in child.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }

                return PrefabUtility.SaveAsPrefabAsset(root, outPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Material de TROPA vibrante (doc 01 §6): URP/Lit com a cor de classe DOMINANTE em
        /// _BaseColor (cor direta, NÃO multiplicada pelo atlas marrom do KayKit — por isso
        /// NÃO bindamos _BaseMap), smoothness baixa (0.1 — fosco, sem brilho plástico) e leve
        /// EMISSION da própria cor (~0.15) p/ "pop" sob bloom sem estourar. Resultado: silhueta
        /// clara e colorida na multidão, em vez de massa marrom-lama.
        /// </summary>
        private static Material UnitClassMaterial(string name, Color classColor)
        {
            string path = ViewMaterialsFolder + "/" + name + ".mat";
            Material material = LoadOrCreateLit(path);

            // SEM textura: o atlas do KayKit é marrom/escuro e mataria a leitura de cor. A cor
            // chapada de classe é exatamente o look "casual premium" das peças KayKit low-poly.
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
            material.mainTexture = null;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", classColor);
            material.color = classColor;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.1f);

            // EMISSION leve (~0.15) da própria cor: a tropa "acende" de leve sob o bloom
            // (threshold 0.9) sem lavar — pop premium, silhueta destacada da pista.
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", classColor * 0.15f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Material de TINT de mutação (CANON §3.3): URP/Lit chapado com a cor da mutação e
        /// EMISSION proporcional (laser/asas brilham, armadura quase não) — pensado p/ o runtime
        /// sobrepor via MaterialPropertyBlock (_BaseColor/_EmissionColor) na multidão inteira.
        /// </summary>
        private static Material MutationTintMaterial(string name, Color tint, float emission)
        {
            string path = MutationMaterialsFolder + "/" + name + ".mat";
            Material material = LoadOrCreateLit(path);

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
            material.mainTexture = null;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            material.color = tint;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.2f);

            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", tint * emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Recolor CC0 (PLANO §1.4): URP/Lit com a MESMA textura-atlas do FBX e tint
        /// multiplicativo em _BaseColor (+ metallic/smoothness). Usado pelos BOSSES.
        /// CLÍMAX (doc 01 §6): o atlas Quaternius é escuro e, multiplicado por um tint fosco,
        /// o golem some na sombra. EMISSION leve da própria cor (~0.18) "acende" o chefão sob
        /// o bloom da cena — ele lê imponente sem virar plástico (smoothness continua baixa).
        /// </summary>
        private static Material TintMaterial(string name, GameObject model, Color tint,
                                             float metallic, float smoothness)
        {
            Texture mainTexture = null;
            Renderer renderer = model.GetComponentInChildren<Renderer>(true);
            if (renderer != null && renderer.sharedMaterial != null)
                mainTexture = renderer.sharedMaterial.mainTexture;

            string path = ViewMaterialsFolder + "/" + name + ".mat";
            Material material = LoadOrCreateLit(path);

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", mainTexture);
            material.mainTexture = mainTexture;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            material.color = tint;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);

            // EMISSION leve da própria cor: o chefão "acende" sob o bloom (threshold 0.9) e
            // não afunda na sombra do atlas escuro — leitura imponente sem brilho plástico.
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", tint * 0.18f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreateLit(string path)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("MAR Tools: shader 'Universal Render Pipeline/Lit' não encontrado — usando Standard.");
                lit = Shader.Find("Standard");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(lit);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != lit)
            {
                material.shader = lit;
            }
            return material;
        }

        // ------------------------------------------------------------------ infra

        /// <summary>Preenche campo objeto de SO existente via SerializedObject (caminho oficial de editor).</summary>
        private static void SetObjectField(Object asset, string fieldName, Object value)
        {
            var serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"MAR Tools: campo serializado '{fieldName}' não existe em {asset.GetType().Name}.", asset);
                return;
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        /// <summary>Preenche campo int de SO existente via SerializedObject.</summary>
        private static void SetIntField(Object asset, string fieldName, int value)
        {
            var serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"MAR Tools: campo serializado '{fieldName}' não existe em {asset.GetType().Name}.", asset);
                return;
            }
            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
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
