using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using MutantArmy.Core;
using MutantArmy.Domain;

namespace MutantArmy.Editor
{
    /// <summary>
    /// Validador de ScriptableObjects (doc 12 §5.2 item 4): campo essencial nulo, fase
    /// sem boss, fase sem mundo etc. viram ERRO com link para o asset no Console.
    /// Roda pelo menu e automaticamente no build (guard abaixo) — erro bloqueia o build.
    /// </summary>
    public static class SoValidator
    {
        [MenuItem("MAR Tools/Validate ScriptableObjects")]
        public static void ValidateMenu()
        {
            int errors = Validate();
            if (errors == 0)
                EditorUtility.DisplayDialog("MAR Tools", "Validação de SOs: nenhum erro encontrado.", "OK");
            else
                EditorUtility.DisplayDialog("MAR Tools",
                    "Validação de SOs: " + errors + " erro(s) — detalhes no Console.", "OK");
        }

        /// <summary>Valida todos os SOs do projeto; retorna o número de erros.</summary>
        public static int Validate()
        {
            int errors = 0;

            void Error(string message, Object context)
            {
                Debug.LogError("[SoValidator] " + message, context);
                errors++;
            }

            void Warn(string message, Object context)
            {
                Debug.LogWarning("[SoValidator] " + message, context);
            }

            foreach (UnitConfigSO unit in FindAll<UnitConfigSO>())
            {
                if (string.IsNullOrEmpty(unit.unitId)) Error("UnitConfigSO '" + unit.name + "' sem unitId.", unit);
                if (unit.supplyCost < 1) Error("UnitConfigSO '" + unit.name + "' com supplyCost < 1 (CANON §5).", unit);
                if (unit.baseHp <= 0f) Error("UnitConfigSO '" + unit.name + "' com baseHp <= 0.", unit);
                if (unit.baseDps < 0f) Error("UnitConfigSO '" + unit.name + "' com baseDps negativo.", unit);
                if (unit.moveSpeed <= 0f) Error("UnitConfigSO '" + unit.name + "' com moveSpeed <= 0.", unit);
            }

            foreach (GateConfigSO gate in FindAll<GateConfigSO>())
            {
                if (string.IsNullOrEmpty(gate.gateId)) Error("GateConfigSO '" + gate.name + "' sem gateId.", gate);
                if (string.IsNullOrEmpty(gate.displayLabel))
                    Error("GateConfigSO '" + gate.name + "' sem displayLabel — viola portais honestos (CANON §3.4).", gate);

                switch (gate.gateType)
                {
                    case GateType.Multiply:
                        if (gate.value <= 0f) Error("GateConfigSO '" + gate.name + "' Multiply com value <= 0.", gate);
                        break;
                    case GateType.ClassConvert:
                        if (gate.unitToAdd == null) Error("GateConfigSO '" + gate.name + "' ClassConvert sem unitToAdd.", gate);
                        break;
                    case GateType.Element:
                        if (gate.element == ElementType.None) Error("GateConfigSO '" + gate.name + "' Element com elemento None.", gate);
                        break;
                    case GateType.Mutation:
                        if (gate.mutation == null) Error("GateConfigSO '" + gate.name + "' Mutation sem MutationConfigSO.", gate);
                        break;
                    case GateType.Risk:
                        if (gate.riskSuccessChance <= 0f || gate.riskSuccessChance > 1f)
                            Error("GateConfigSO '" + gate.name + "' Risk com riskSuccessChance fora de (0,1].", gate);
                        if (gate.riskRewardMult <= 1f)
                            Error("GateConfigSO '" + gate.name + "' Risk com riskRewardMult <= 1 (sem recompensa).", gate);
                        break;
                }
            }

            foreach (BossConfigSO boss in FindAll<BossConfigSO>())
            {
                if (string.IsNullOrEmpty(boss.bossId)) Error("BossConfigSO '" + boss.name + "' sem bossId.", boss);
                if (boss.maxHp <= 0f) Error("BossConfigSO '" + boss.name + "' com maxHp <= 0.", boss);
                if (boss.entranceSeconds > 2.05f)
                    Warn("BossConfigSO '" + boss.name + "' com entrada > 2 s (CANON §6: animação de entrada ≤ 2 s).", boss);
                if (boss.weaknesses == null || boss.weaknesses.Length == 0)
                    Warn("BossConfigSO '" + boss.name + "' sem fraqueza — Boss Scout exibirá dica genérica.", boss);
            }

            foreach (LevelConfigSO level in FindAll<LevelConfigSO>())
            {
                if (level.boss == null)
                    Error("LevelConfigSO '" + level.name + "' SEM BOSS — toda fase termina em boss (CANON §6).", level);
                if (level.world == null) Error("LevelConfigSO '" + level.name + "' sem WorldConfigSO.", level);
                if (level.levelIndex < 1) Error("LevelConfigSO '" + level.name + "' com levelIndex < 1.", level);
                if (level.trackLength <= 0f) Error("LevelConfigSO '" + level.name + "' com trackLength <= 0.", level);
                if (level.startingUnits < 1)
                    Error("LevelConfigSO '" + level.name + "' com startingUnits < 1 (fase começa com 1 unidade).", level);

                if (level.gateSlots == null || level.gateSlots.Length == 0)
                {
                    Warn("LevelConfigSO '" + level.name + "' sem gateSlots — corrida sem portais.", level);
                }
                else
                {
                    foreach (GateSlot slot in level.gateSlots)
                    {
                        if (slot == null) continue;
                        if (!slot.autoBalance && (slot.leftGate == null || slot.rightGate == null))
                            Error("LevelConfigSO '" + level.name + "' tem GateSlot manual sem par completo (esquerda/direita).", level);
                    }
                }
            }

            foreach (WorldConfigSO world in FindAll<WorldConfigSO>())
            {
                if (world.levels == null || world.levels.Length == 0)
                    Error("WorldConfigSO '" + world.name + "' sem fases.", world);
                if (world.worldBoss == null)
                    Error("WorldConfigSO '" + world.name + "' sem boss de mundo.", world);
            }

            return errors;
        }

        private static List<T> FindAll<T>() where T : ScriptableObject
        {
            var result = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) result.Add(asset);
            }
            return result;
        }
    }

    /// <summary>
    /// Guard de build (doc 12 §5.2): SO inválido falha o build ANTES da compilação do
    /// player, com link para o asset no Console.
    /// </summary>
    public class SoValidatorBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder
        {
            get { return 0; }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            int errors = SoValidator.Validate();
            if (errors > 0)
                throw new BuildFailedException("[SoValidator] " + errors +
                    " erro(s) de ScriptableObject — corrija antes do build (veja o Console).");
        }
    }
}
