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

            // Inimigos de pista (missão Nota 10 — CONTRACT §2): papéis com contrato de stats
            // (Ranged ataca ANTES do contato; Healer precisa curar de verdade).
            foreach (EnemyConfigSO enemy in FindAll<EnemyConfigSO>())
            {
                if (string.IsNullOrEmpty(enemy.enemyId))
                    Error("EnemyConfigSO '" + enemy.name + "' sem enemyId.", enemy);
                if (enemy.maxHp <= 0f)
                    Error("EnemyConfigSO '" + enemy.name + "' com maxHp <= 0.", enemy);
                if (enemy.dps < 0f)
                    Error("EnemyConfigSO '" + enemy.name + "' com dps negativo.", enemy);
                if (enemy.rewardCoins < 0)
                    Error("EnemyConfigSO '" + enemy.name + "' com rewardCoins negativo.", enemy);
                if (enemy.worldIndex < 1 || enemy.worldIndex > 10)
                    Error("EnemyConfigSO '" + enemy.name + "' com worldIndex " + enemy.worldIndex +
                          " fora de 1..10 (mundo temático).", enemy);
                if (enemy.kind == TrackEnemyKind.Healer && enemy.healPerSecond <= 0f)
                    Error("EnemyConfigSO '" + enemy.name + "' é Healer com healPerSecond <= 0 — curador que não cura.", enemy);
                if (enemy.kind == TrackEnemyKind.Ranged && enemy.attackRange <= 6f)
                    Error("EnemyConfigSO '" + enemy.name + "' é Ranged com attackRange <= 6 — atirador deve atacar ANTES do contato (CONTRACT §2).", enemy);
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

                // Inimigos de pista (missão Nota 10): slot precisa de config e de posição na pista.
                if (level.enemies != null)
                {
                    foreach (EnemySlot slot in level.enemies)
                    {
                        if (slot == null) continue;
                        if (slot.enemy == null)
                            Error("LevelConfigSO '" + level.name + "' tem EnemySlot sem EnemyConfigSO — grupo fantasma.", level);
                        if (slot.trackPosition <= 0f || slot.trackPosition >= level.trackLength)
                            Error("LevelConfigSO '" + level.name + "' tem EnemySlot fora da pista (z=" +
                                  slot.trackPosition + ", pista=" + level.trackLength + " m).", level);
                        if (slot.count < 1)
                            Error("LevelConfigSO '" + level.name + "' tem EnemySlot com count < 1.", level);
                    }
                }

                ValidateTutorialRules(level, Error);
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

        /// <summary>
        /// Regras de TUTORIAL das fases-chave (missão Nota 10; CANON §16) — protegem o desenho
        /// dos primeiros 5 minutos contra regressão de conteúdo:
        /// F1 (levelIndex 1): pista LIMPA (sem obstáculos/inimigos) e só pares manuais de soma
        /// positiva (AddFlat &gt; 0) — mantém o PlayMode GameScene_Fase1_VitoriaCompleta vencível.
        /// A proibição de inimigos vale SOMENTE para a F1: F2/F3 JÁ TÊM hordas fracas de
        /// propósito (introduzir cedo o prazer de atropelar — ver MvpContentFactory), então
        /// nenhuma regra aqui pode barrar inimigos em F2+.
        /// F3 (levelIndex 3): a fase que ensina FRAQUEZA — boss fraco a FOGO e pelo menos um
        /// portal de elemento FOGO entre os slots.
        /// </summary>
        private static void ValidateTutorialRules(LevelConfigSO level, System.Action<string, Object> error)
        {
            if (level.levelIndex == 1)
            {
                if (level.obstacles != null && level.obstacles.Length > 0)
                    error("Fase 1 '" + level.name + "' com obstáculos — onboarding deve ser impossível de perder (CANON §16).", level);
                if (level.enemies != null && level.enemies.Length > 0)
                    error("Fase 1 '" + level.name + "' com inimigos de pista — onboarding deve ser impossível de perder (CANON §16). " +
                          "A regra vale SÓ p/ F1: F2+ podem (e devem) ter inimigos.", level);
                if (level.gateSlots != null)
                {
                    foreach (GateSlot slot in level.gateSlots)
                    {
                        if (slot == null) continue;
                        if (slot.autoBalance)
                        {
                            error("Fase 1 '" + level.name + "' com slot autoBalance — onboarding usa só pares manuais de soma (+N).", level);
                            continue;
                        }
                        ValidateF1Gate(level, slot.leftGate, error);
                        ValidateF1Gate(level, slot.rightGate, error);
                    }
                }
            }

            if (level.levelIndex == 3)
            {
                bool weakToFire = level.boss != null && level.boss.weaknesses != null &&
                                  System.Array.IndexOf(level.boss.weaknesses, ElementType.Fire) >= 0;
                if (!weakToFire)
                    error("Fase 3 '" + level.name + "' precisa de boss FRACO A FOGO — é a fase que ensina fraqueza elemental (missão Nota 10).", level);

                bool hasFireGate = false;
                if (level.gateSlots != null)
                {
                    foreach (GateSlot slot in level.gateSlots)
                    {
                        if (slot == null) continue;
                        if (IsFireElementGate(slot.leftGate) || IsFireElementGate(slot.rightGate))
                            hasFireGate = true;
                    }
                }
                if (!hasFireGate)
                    error("Fase 3 '" + level.name + "' sem portal de ELEMENTO FOGO — o jogador não consegue explorar a fraqueza ensinada.", level);
            }
        }

        private static void ValidateF1Gate(LevelConfigSO level, GateConfigSO gate, System.Action<string, Object> error)
        {
            if (gate == null) return;   // par incompleto já é erro na checagem geral de slots
            if (gate.gateType != GateType.AddFlat || gate.value <= 0f)
                error("Fase 1 '" + level.name + "' com portal '" + gate.name +
                      "' que não é soma positiva (+N) — F1 ensina só CRESCIMENTO (missão Nota 10).", level);
        }

        private static bool IsFireElementGate(GateConfigSO gate)
        {
            return gate != null && gate.gateType == GateType.Element && gate.element == ElementType.Fire;
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
