using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using MutantArmy.Core;
using Object = UnityEngine.Object;

namespace MutantArmy.Editor
{
    /// <summary>
    /// MAR Tools/Build Unit Visuals — liga a arte CC0 importada (KayKit Adventurers +
    /// Quaternius Ultimate Monsters; mapa em _assets-staging\PLANO-DE-USO.md §1.1/§1.4)
    /// aos SOs do MVP, tudo por código:
    /// 1. TROPAS: prefab VARIANT por tropa em Prefabs/Units com AnimatorController criado
    ///    via UnityEditor.Animations (Idle/Run/Attack trocados pelo int "State" — contrato
    ///    do CrowdViewPool) usando os clipes EMBUTIDOS no FBX (busca por nome contendo
    ///    idle/run|walk/attack; fallback 1º clipe). Escala 0.6; Gigante = mesmo Barbarian
    ///    em 1.3 (~2.1× a tropa) + recolor escuro → UnitConfigSO.viewPrefab.
    /// 2. BOSSES: prefab por boss em Prefabs/Bosses (escala 3.5–5, controller Idle(0)/
    ///    Attack(1) — contrato do BossManager) → BossConfigSO.prefab. M3 Robô Escorpião =
    ///    Alien recolor metálico (PLACEHOLDER da Lacuna L2 do staging).
    /// FBX ausente = aviso + skip (fallback instanced/greybox fica intacto). Campos dos SOs
    /// preenchidos via SerializedObject. Idempotente: re-rodar atualiza no lugar.
    /// </summary>
    public static class UnitVisualFactory
    {
        private const string SoRoot = "Assets/_Project/ScriptableObjects";
        private const string ModelsRoot = "Assets/_Project/Art/Models";
        private const string AnimFolder = "Assets/_Project/Art/Animations";
        private const string ViewMaterialsFolder = "Assets/_Project/Art/Materials/Views";
        private const string UnitsPrefabFolder = "Assets/_Project/Prefabs/Units";
        private const string BossesPrefabFolder = "Assets/_Project/Prefabs/Bosses";

        private sealed class ClipSet
        {
            public AnimationClip Idle, Run, Attack;
        }

        // ordem dos keywords = ordem de preferência (varre todos os clipes por keyword)
        private static readonly string[] IdleKeywords = { "idle" };
        private static readonly string[] RunKeywords = { "run", "walk", "move" };
        private static readonly string[] AttackKeywords =
            { "attack", "melee", "chop", "slice", "slash", "punch", "bite", "shoot", "cast" };

        [MenuItem("MAR Tools/Build Unit Visuals")]
        public static void BuildAll()
        {
            EnsureFolder(AnimFolder);
            EnsureFolder(ViewMaterialsFolder);
            EnsureFolder(UnitsPrefabFolder);
            EnsureFolder(BossesPrefabFolder);

            int built = 0;

            // ---- Tropas (PLANO §1.1): Soldado=Barbarian · Arqueiro=Rogue · Escudeiro=Knight ·
            // ---- Mago=Mage · Gigante=Barbarian 1.3 (≈2.1× a tropa de 0.6) + recolor escuro
            if (BuildUnit("Unit_Soldier", "Barbarian", null, 0.6f, null)) built++;
            if (BuildUnit("Unit_Archer", "Rogue", null, 0.6f, null)) built++;
            if (BuildUnit("Unit_Shieldbearer", "Knight", null, 0.6f, null)) built++;
            if (BuildUnit("Unit_Mage", "Mage", null, 0.6f, null)) built++;
            if (BuildUnit("Unit_Giant", "Barbarian", null, 1.3f, new Color(0.42f, 0.38f, 0.50f))) built++;

            // ---- Bosses (PLANO §1.4): monstros Quaternius, escala 3.5–5, recolor por boss
            if (BuildBoss("Boss_M1_GolemStone", "Goleling_Evolved", "Flying", 4.0f,
                          new Color(0.62f, 0.62f, 0.66f), 0.05f, 0.30f)) built++;
            if (BuildBoss("Boss_M1_WoodGiant", "Tribal", "Big", 4.0f,
                          new Color(0.55f, 0.38f, 0.22f), 0.00f, 0.30f)) built++;
            if (BuildBoss("Boss_M2_ZombieBruiser", "Orc", "Big", 3.5f,
                          new Color(0.50f, 0.65f, 0.35f), 0.00f, 0.30f)) built++;
            if (BuildBoss("Boss_M2_ZombieTitan", "Orc_Skull", "Big", 5.0f,
                          new Color(0.58f, 0.62f, 0.58f), 0.00f, 0.30f)) built++;
            if (BuildBoss("Boss_M3_ScorpionMech", "Alien", "Big", 4.0f,
                          new Color(0.66f, 0.72f, 0.80f), 0.85f, 0.70f))
            {
                built++;
                Debug.LogWarning("MAR Tools: Boss M3 'Robô Escorpião' usa PLACEHOLDER (Alien recolor " +
                                 "metálico) — Lacuna L2 do PLANO-DE-USO; trocar pelo mech CC0 quando baixado.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"MAR Tools: visuais de tropas/bosses construídos — {built}/10 " +
                      "(faltantes mantêm fallback instanced/greybox; ver avisos acima).");
        }

        // ------------------------------------------------------------------ tropas

        private static bool BuildUnit(string unitAssetName, string modelName, string preferPathFragment,
                                      float scale, Color? tint)
        {
            var unit = AssetDatabase.LoadAssetAtPath<UnitConfigSO>(SoRoot + "/Units/" + unitAssetName + ".asset");
            if (unit == null)
            {
                Debug.LogWarning($"MAR Tools: {unitAssetName}.asset não existe — rode MAR Tools/Create MVP Content antes.");
                return false;
            }

            GameObject model = FindModel(modelName, preferPathFragment);
            if (model == null) return false;   // FindModel já avisou; viewPrefab fica como está (fallback instanced)

            string fbxPath = AssetDatabase.GetAssetPath(model);
            PrepareImporter(fbxPath);
            ClipSet clips = FindClips(fbxPath, modelName);

            // tropas: 3 estados — Idle(0)/Run(1)/Attack(2), contrato do CrowdViewPool
            AnimatorController controller = BuildController(
                AnimFolder + "/AC_" + unitAssetName + ".controller",
                clips.Idle, clips.Run, clips.Attack, attackValue: 2);

            Material overrideMaterial = tint.HasValue
                ? TintMaterial("M_View_" + unitAssetName, model, tint.Value, 0f, 0.30f)
                : null;

            GameObject prefab = BuildViewPrefab(
                model, UnitsPrefabFolder + "/" + unitAssetName + "_View.prefab",
                scale, controller, overrideMaterial);

            SetObjectField(unit, "viewPrefab", prefab);
            return true;
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
        /// Recolor CC0 (PLANO §1.1/§1.4): URP/Lit com a MESMA textura-atlas do FBX e tint
        /// multiplicativo em _BaseColor (+ metallic/smoothness p/ o placeholder do M3).
        /// </summary>
        private static Material TintMaterial(string name, GameObject model, Color tint,
                                             float metallic, float smoothness)
        {
            Texture mainTexture = null;
            Renderer renderer = model.GetComponentInChildren<Renderer>(true);
            if (renderer != null && renderer.sharedMaterial != null)
                mainTexture = renderer.sharedMaterial.mainTexture;

            string path = ViewMaterialsFolder + "/" + name + ".mat";
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

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", mainTexture);
            material.mainTexture = mainTexture;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            material.color = tint;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ------------------------------------------------------------------ infra

        /// <summary>Preenche campo de SO existente via SerializedObject (caminho oficial de editor).</summary>
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
