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
    /// 2. BOSSES (até 10 bosses de mundo, CANON §6): prefab por boss em Prefabs/Bosses (escala
    ///    com PISO ≥3.5x — chefão imponente —, controller Idle(0)/Attack(1) — contrato do
    ///    BossManager) → BossConfigSO.prefab. O loop é dirigido pelo bossId ESTÁVEL (doc 05 §7:
    ///    m1_final_wood_giant … m10_final_*), não pelo nome do arquivo, então funciona seja qual
    ///    for o asset que o agente de campanha gerou e é re-rodável: varre TODO BossConfigSO da
    ///    pasta, casa por bossId na BossModelTable e monta. Bosses 6–10 que ainda não existirem
    ///    (se este factory rodar antes do Create MVP Content) são só pulados — re-rodar fecha.
    ///    Os 5 do MVP mantêm modelo/recolor IDÊNTICOS aos de hoje. M3 Robô Escorpião = Alien
    ///    recolor metálico (PLACEHOLDER da Lacuna L2 do staging). load-or-keep: só grava o prefab
    ///    se o campo estiver vazio, apontar p/ greybox, ou já apontar p/ a view que ESTE factory
    ///    gera — um prefab específico setado pelo agente de dados é preservado. Os 3 modelos novos
    ///    (BlueDemon/Squidle/Hywirl) são copiados do _assets-staging sob demanda (idempotente).
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
        private const string BossesModelFolder = "Assets/_Project/Art/Models/Bosses";
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

        // ============================================================ BOSSES (CANON §6 / doc 05 §7)
        private const float BossScaleFloor = 3.5f;   // PISO de escala do brief: chefão sempre imponente

        /// <summary>
        /// Mapa boss → arte, chaveado pelo bossId ESTÁVEL (doc 05 §7), nunca pelo nome do arquivo.
        /// Cobre os 10 bosses de mundo do CANON §6: 5 do MVP (modelo/recolor idênticos aos de hoje)
        /// + 5 novos mundos (W4–W10 menos os 3 já no MVP). Tints CLAREADOS (doc 01 §6): o atlas
        /// Quaternius é escuro, então a cor é clara e a emissão leve p/ o chefão ler inteiro sob o
        /// bloom sem virar plástico. Recolor coerente com o tema do mundo (WorldVisualFactory).
        /// </summary>
        private sealed class BossModelSpec
        {
            public string BossId;        // estável (doc 05 §7) — chave do casamento com o BossConfigSO
            public string ModelName;     // FBX base (nome exato do arquivo, já no projeto ou copiado do staging)
            public string PreferFolder;  // desempate de pasta no FindModel
            public float Scale;          // ≥ BossScaleFloor (reforçado no build)
            public Color Tint;           // recolor temático do mundo
            public float Metallic;
            public float Smoothness;
        }

        // ordem livre — o build casa por bossId. Os 5 primeiros = MVP (NÃO mudar modelo/recolor).
        private static readonly BossModelSpec[] BossModelTable =
        {
            // -------- MVP (travado: mesmos modelos/recolor/escala de antes) --------
            // M1 arquétipo Golem de Pedra (fases 1–6) — Goleling tech-pedra, cinza claro.
            new BossModelSpec { BossId = "m1_golem_stone",          ModelName = "Goleling_Evolved", PreferFolder = "Flying", Scale = 4.0f, Tint = new Color(0.82f, 0.82f, 0.88f), Metallic = 0.05f, Smoothness = 0.30f },
            // W1 Gigante de Madeira — Tribal recolor madeira.
            new BossModelSpec { BossId = "m1_final_wood_giant",     ModelName = "Tribal",           PreferFolder = "Big",    Scale = 4.0f, Tint = new Color(0.78f, 0.56f, 0.34f), Metallic = 0.00f, Smoothness = 0.30f },
            // M2 arquétipo Brutamontes Zumbi (fases 8–13) — Orc recolor verde-pútrido.
            new BossModelSpec { BossId = "m2_zombie_bruiser",       ModelName = "Orc",              PreferFolder = "Big",    Scale = 3.5f, Tint = new Color(0.66f, 0.84f, 0.48f), Metallic = 0.00f, Smoothness = 0.30f },
            // W2 Zumbi Titã — Orc_Skull recolor acinzentado.
            new BossModelSpec { BossId = "m2_final_zombie_titan",   ModelName = "Orc_Skull",        PreferFolder = "Big",    Scale = 5.0f, Tint = new Color(0.78f, 0.82f, 0.78f), Metallic = 0.00f, Smoothness = 0.30f },
            // W3 Robô Escorpião — Alien recolor metálico (PLACEHOLDER Lacuna L2).
            new BossModelSpec { BossId = "m3_final_scorpion_mech",  ModelName = "Alien",            PreferFolder = "Big",    Scale = 4.0f, Tint = new Color(0.78f, 0.84f, 0.92f), Metallic = 0.85f, Smoothness = 0.70f },

            // -------- W4–W10: bosses de mundo NOVOS (CANON §6 / doc 05 §7.x) --------
            // W4 Planta Carnívora (fraco Fogo+Veneno) — MushroomKing orgânico, verde tóxico.
            new BossModelSpec { BossId = "m4_final_carnivore_plant", ModelName = "MushroomKing",    PreferFolder = "Big",    Scale = 4.2f, Tint = new Color(0.52f, 0.86f, 0.40f), Metallic = 0.00f, Smoothness = 0.32f },
            // W5 Dragão de Lava (fraco Gelo, resiste Fogo) — Dragon_Evolved recolor lava.
            new BossModelSpec { BossId = "m5_final_lava_dragon",     ModelName = "Dragon_Evolved",  PreferFolder = "Flying", Scale = 4.5f, Tint = new Color(1.00f, 0.50f, 0.22f), Metallic = 0.10f, Smoothness = 0.45f },
            // W6 Rei de Gelo (fraco Fogo, resiste Gelo) — Yeti recolor gelo azul-branco.
            new BossModelSpec { BossId = "m6_final_ice_king",        ModelName = "Yeti",            PreferFolder = "Big",    Scale = 4.2f, Tint = new Color(0.70f, 0.90f, 1.00f), Metallic = 0.05f, Smoothness = 0.55f },
            // W7 Cavaleiro Colosso (fraco Raio — armadura conduz) — BlueDemon recolor dourado-bronze armadura.
            new BossModelSpec { BossId = "m7_final_colossus_knight", ModelName = "BlueDemon",       PreferFolder = "Big",    Scale = 4.4f, Tint = new Color(0.92f, 0.74f, 0.36f), Metallic = 0.55f, Smoothness = 0.50f },
            // W8 Alien Supremo (fraqueza rotativa) — Squidle exótico neon roxo-ciano, emissivo forte.
            new BossModelSpec { BossId = "m8_final_alien_supreme",   ModelName = "Squidle",         PreferFolder = "Flying", Scale = 4.0f, Tint = new Color(0.70f, 0.45f, 1.00f), Metallic = 0.20f, Smoothness = 0.60f },
            // W9 Mecha Supremo (fraco Raio, imune Veneno) — Goleling_Evolved recolor industrial ciano-tech.
            new BossModelSpec { BossId = "m9_final_mecha_supreme",   ModelName = "Goleling_Evolved", PreferFolder = "Flying", Scale = 4.6f, Tint = new Color(0.55f, 0.80f, 0.92f), Metallic = 0.80f, Smoothness = 0.65f },
            // W10 Entidade Dimensional (alterna elementos) — Hywirl exótico, caos vibrante magenta-ciano.
            new BossModelSpec { BossId = "m10_final_dimensional_entity", ModelName = "Hywirl",      PreferFolder = "Flying", Scale = 4.3f, Tint = new Color(0.95f, 0.40f, 0.95f), Metallic = 0.25f, Smoothness = 0.55f },
        };

        /// <summary>
        /// FBX de boss que o MVP NÃO trouxe p/ Art/Models/Bosses (os 3 novos arquétipos visuais dos
        /// mundos W7/W8/W10). Copiados do _assets-staging sob demanda — idempotente (skip se já há
        /// o asset). Os outros modelos da tabela já estão no projeto desde a fase beauty.
        /// rel = caminho dentro de _assets-staging\models\Quaternius-UltimateMonsters.
        /// </summary>
        private static readonly (string File, string StagingRel)[] BossModelsToImport =
        {
            ("BlueDemon.fbx", "models/Quaternius-UltimateMonsters/Big/FBX/BlueDemon.fbx"),
            ("Squidle.fbx",   "models/Quaternius-UltimateMonsters/Flying/FBX/Squidle.fbx"),
            ("Hywirl.fbx",    "models/Quaternius-UltimateMonsters/Flying/FBX/Hywirl.fbx"),
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

            // ---- Bosses (CANON §6, até 10 mundos): monstros Quaternius, escala ≥3.5x, recolor
            // temático por boss. Dirigido pelo bossId estável (doc 05 §7) — varre TODO BossConfigSO
            // da pasta e casa na BossModelTable, então não depende do nome do arquivo do agente de
            // dados e é re-rodável (bosses 6–10 inexistentes são só pulados). Copia sob demanda os
            // 3 FBX novos do staging antes de procurar os modelos.
            EnsureBossModelsImported();
            int bosses = BuildAllBosses();

            // ---- Mutações visíveis (CANON §3.3): materiais de tint + acessórios hero.
            int mutations = BuildMutationVisuals();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"MAR Tools: visuais construídos — {troops}/{Roster.Length} tropas, {bosses} bosses " +
                      $"(de {BossModelTable.Length} mundos mapeados; os ausentes serão preenchidos ao re-rodar após " +
                      $"Create MVP Content), {mutations}/{MutationVisuals.Length} mutações (faltantes mantêm fallback " +
                      "instanced/greybox; ver avisos acima).");
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

        /// <summary>
        /// Varre TODO BossConfigSO de ScriptableObjects/Bosses e, p/ cada um cujo bossId esteja na
        /// BossModelTable (doc 05 §7), monta o prefab de view (modelo Quaternius + recolor + escala
        /// ≥3.5x + controller Idle/Attack) e grava em BossConfigSO.prefab via load-or-keep. Dirigir
        /// pelo bossId (não pelo nome do arquivo) torna o factory independente do asset que o agente
        /// de campanha gerou e re-rodável: bosses de mundo ainda inexistentes simplesmente não
        /// aparecem na varredura. Retorna quantos bosses receberam view.
        /// </summary>
        private static int BuildAllBosses()
        {
            string bossFolder = SoRoot + "/Bosses";
            if (!AssetDatabase.IsValidFolder(bossFolder))
            {
                Debug.LogWarning("MAR Tools: pasta de Bosses ainda não existe — rode MAR Tools/Create MVP Content " +
                                 "antes (este factory roda DEPOIS dele).");
                return 0;
            }

            int built = 0;
            var seen = new HashSet<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:BossConfigSO", new[] { bossFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var boss = AssetDatabase.LoadAssetAtPath<BossConfigSO>(path);
                if (boss == null) continue;

                BossModelSpec spec = FindBossSpec(boss.bossId);
                if (spec == null) continue;     // boss fora da tabela (ex.: arquétipos de fase) — mantém prefab atual
                if (!seen.Add(boss.bossId)) continue;

                if (BuildBoss(boss, path, spec)) built++;
            }

            int total = BossModelTable.Length;
            if (built < total)
                Debug.Log($"MAR Tools: {built}/{total} bosses de mundo com modelo nesta passada — os faltantes " +
                          "(bosses cujo SO ainda não existe) entram ao re-rodar após Create MVP Content.");
            return built;
        }

        /// <summary>
        /// Casa o BossConfigSO com a linha da BossModelTable. 1º por bossId EXATO (doc 05 §7); se o
        /// agente de campanha tiver usado outro sufixo descritivo p/ o boss de mundo, cai no
        /// fallback por PREFIXO "m&lt;N&gt;_final_" — cada mundo tem exatamente 1 boss "_final_", então
        /// o número do mundo identifica o modelo sem ambiguidade. Arquétipos de fase (ex.:
        /// m1_golem_stone, sem "_final_") casam só pelo id exato — nada de fallback p/ eles.
        /// </summary>
        private static BossModelSpec FindBossSpec(string bossId)
        {
            if (string.IsNullOrEmpty(bossId)) return null;

            for (int i = 0; i < BossModelTable.Length; i++)
                if (string.Equals(BossModelTable[i].BossId, bossId, StringComparison.Ordinal))
                    return BossModelTable[i];

            string worldFinalPrefix = WorldFinalPrefix(bossId);   // "m5_final_" p/ um boss de mundo; null senão
            if (worldFinalPrefix == null) return null;
            for (int i = 0; i < BossModelTable.Length; i++)
                if (BossModelTable[i].BossId.StartsWith(worldFinalPrefix, StringComparison.Ordinal))
                    return BossModelTable[i];
            return null;
        }

        /// <summary>"m7_final_qualquer_coisa" → "m7_final_"; ids sem o marcador "_final_" → null.</summary>
        private static string WorldFinalPrefix(string bossId)
        {
            const string marker = "_final_";
            int idx = bossId.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return null;
            return bossId.Substring(0, idx + marker.Length);
        }

        private static bool BuildBoss(BossConfigSO boss, string bossAssetPath, BossModelSpec spec)
        {
            GameObject model = FindModel(spec.ModelName, spec.PreferFolder);
            if (model == null) return false;   // FindModel avisou; mantém o campo (greybox/nulo→runtime)

            string bossAssetName = System.IO.Path.GetFileNameWithoutExtension(bossAssetPath);

            string fbxPath = AssetDatabase.GetAssetPath(model);
            PrepareImporter(fbxPath);
            ClipSet clips = FindClips(fbxPath, spec.ModelName);

            // bosses: 2 estados — Idle(0)/Attack(1), contrato do BossManager.ApplyViewAnim
            AnimatorController controller = BuildController(
                AnimFolder + "/AC_" + bossAssetName + ".controller",
                clips.Idle, null, clips.Attack, attackValue: 1);

            Material material = TintMaterial("M_View_" + bossAssetName, model, spec.Tint, spec.Metallic, spec.Smoothness);

            float scale = Mathf.Max(spec.Scale, BossScaleFloor);   // piso de imponência do brief
            string viewPath = BossesPrefabFolder + "/" + bossAssetName + "_View.prefab";
            GameObject prefab = BuildViewPrefab(model, viewPath, scale, controller, material);

            // load-or-keep: só grava se o campo estiver vazio, apontar p/ greybox, ou já apontar p/
            // a view que ESTE factory gera (re-rodar é idempotente). Um prefab específico que o
            // agente de dados tenha setado de propósito é preservado.
            if (ShouldSetBossPrefab(boss, viewPath))
                SetObjectField(boss, "prefab", prefab);

            if (string.Equals(spec.BossId, "m3_final_scorpion_mech", StringComparison.Ordinal))
                Debug.LogWarning("MAR Tools: Boss 'Robô Escorpião' usa PLACEHOLDER (Alien recolor metálico) — " +
                                 "Lacuna L2 do PLANO-DE-USO; trocar pelo mech CC0 quando baixado.");
            return true;
        }

        /// <summary>
        /// load-or-keep do prefab do boss: grava quando vazio, quando aponta p/ um Boss_Greybox,
        /// ou quando JÁ aponta p/ a view deste factory (caminho Prefabs/Bosses/&lt;asset&gt;_View).
        /// Caso contrário, preserva o que o agente de dados pôs — sem sobrescrever.
        /// </summary>
        private static bool ShouldSetBossPrefab(BossConfigSO boss, string viewPath)
        {
            if (boss.prefab == null) return true;
            string current = AssetDatabase.GetAssetPath(boss.prefab);
            if (string.IsNullOrEmpty(current)) return true;
            if (string.Equals(current, viewPath, StringComparison.OrdinalIgnoreCase)) return true;   // re-rodar
            string file = System.IO.Path.GetFileNameWithoutExtension(current);
            return file.IndexOf("greybox", StringComparison.OrdinalIgnoreCase) >= 0;                  // placeholder
        }

        /// <summary>
        /// Copia do _assets-staging os FBX de boss que o MVP não trouxe (BlueDemon/Squidle/Hywirl,
        /// CC0 Quaternius) p/ Art/Models/Bosses e configura o importador (rig Generic + animação)
        /// igual aos demais. Idempotente: pula o que já existe. Sem staging = aviso e o boss fica
        /// no fallback (FindModel avisa quando o modelo não aparece).
        /// </summary>
        private static void EnsureBossModelsImported()
        {
            string staging = FindStagingRoot();
            bool copied = false;
            for (int i = 0; i < BossModelsToImport.Length; i++)
            {
                string destAsset = BossesModelFolder + "/" + BossModelsToImport[i].File;
                if (CopyStagingFile(staging, BossModelsToImport[i].StagingRel, destAsset)) copied = true;
            }
            if (copied) AssetDatabase.Refresh();

            for (int i = 0; i < BossModelsToImport.Length; i++)
                ConfigureBossModelImporter(BossesModelFolder + "/" + BossModelsToImport[i].File);
        }

        /// <summary>Boss = modelo ANIMADO: rig Generic + animação (igual ao ImportConfigurator).</summary>
        private static void ConfigureBossModelImporter(string assetPath)
        {
            if (!System.IO.File.Exists(AbsolutePath(assetPath))) return;
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return;

            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                changed = true;
            }
            if (!importer.importAnimation) { importer.importAnimation = true; changed = true; }
            if (importer.importCameras) { importer.importCameras = false; changed = true; }
            if (importer.importLights) { importer.importLights = false; changed = true; }
            if (changed) importer.SaveAndReimport();
        }

        private static string FindStagingRoot()
        {
            string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;   // .../MutantArmyRun
            System.IO.DirectoryInfo parent = System.IO.Directory.GetParent(projectRoot);
            if (parent == null) return null;
            string candidate = System.IO.Path.Combine(parent.FullName, "_assets-staging");
            return System.IO.Directory.Exists(candidate) ? candidate : null;
        }

        private static string AbsolutePath(string assetPath)
        {
            return System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(),
                                          assetPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        }

        /// <summary>Copia 1 arquivo do staging p/ um asset path (skip se já existe). True = copiou agora.</summary>
        private static bool CopyStagingFile(string stagingRoot, string relSource, string destAssetPath)
        {
            if (System.IO.File.Exists(AbsolutePath(destAssetPath))) return false;   // idempotente
            if (stagingRoot == null)
            {
                Debug.LogWarning("MAR Tools: _assets-staging não encontrado ao lado do projeto — modelo de boss '" +
                                 System.IO.Path.GetFileName(destAssetPath) + "' será pulado (fallback mantido).");
                return false;
            }

            string src = System.IO.Path.Combine(stagingRoot, relSource.Replace('/', System.IO.Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(src))
            {
                Debug.LogWarning("MAR Tools: FBX de boss CC0 ausente no staging: " + relSource);
                return false;
            }
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(AbsolutePath(destAssetPath)));
            System.IO.File.Copy(src, AbsolutePath(destAssetPath));
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

            // FBX recém-copiado pode vir SEM material resolvido (atlas ainda não extraído) → o
            // recolor multiplicaria por nada e o chefão ficaria chapado. Fallback: o atlas
            // compartilhado dos monstros Quaternius, já em Art/Models/Bosses (todos os modelos da
            // BossModelTable são desse atlas), preservando a leitura de detalhe do mesh.
            if (mainTexture == null)
                mainTexture = AssetDatabase.LoadAssetAtPath<Texture>(
                    BossesModelFolder + "/Atlas_Monsters.png");

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
