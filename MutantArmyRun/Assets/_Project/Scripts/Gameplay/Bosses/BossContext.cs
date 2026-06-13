using MutantArmy.Core;
using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Fotografia da luta entregue ao BossBehavior no OnFightStart (missão Nota 10, W2-A).
    /// Carrega as REFERÊNCIAS vivas da luta (runtime/config/view) + helpers null-safe para
    /// os singletons que o behavior pode tocar — o behavior nunca precisa repetir guardas
    /// de Instance espalhadas (contrato §1.12: greybox degrada, nunca quebra).
    /// Struct readonly: payload imutável por luta, zero alocação por BeginFight.
    /// </summary>
    public readonly struct BossContext
    {
        /// <summary>Estado VIVO da luta (HP/fase/fraqueza/multiplicadores). Pode ser null fora da luta.</summary>
        public readonly BossRuntime Runtime;

        /// <summary>Config READ-ONLY do boss — NUNCA mutar (contrato BossRuntime): tuning vive no .asset.</summary>
        public readonly BossConfigSO Config;

        /// <summary>Raiz da view POOLED do boss (prefab real ou cápsula fallback). Pode ser null.</summary>
        public readonly Transform View;

        public readonly int LevelIndex;

        /// <summary>Variante rara (RareBossMath): HP ×1.5, recompensa ×3 — behaviors podem exagerar o juice.</summary>
        public readonly bool IsRare;

        public BossContext(BossRuntime runtime, BossConfigSO config, Transform view, int levelIndex, bool isRare)
        {
            Runtime = runtime;
            Config = config;
            View = view;
            LevelIndex = levelIndex;
            IsRare = isRare;
        }

        // ------------------------------------------------------------------
        // Helpers null-safe: Gameplay enxerga tudo isto (mesmo asmdef); os getters podem
        // retornar null no greybox/ordem de init — chamadores usam os métodos de ação
        // abaixo quando não precisam do objeto em si.
        // ------------------------------------------------------------------

        public CrowdManager Crowd => CrowdManager.Instance;
        public VFXManager Vfx => VFXManager.Instance;
        public BossManager Boss => BossManager.Instance;

        /// <summary>Posição da view (âncora de VFX/textos); fallback = posição nominal da arena.</summary>
        public Vector3 ViewPosition
        {
            get
            {
                if (View != null) return View.position;
                return BossManager.Instance != null
                    ? BossManager.Instance.CurrentBossPosition
                    : CrowdAnchor.Position + Vector3.forward * 12f;
            }
        }

        /// <summary>Contagem jogável do exército (0 se o CrowdManager ainda não inicializou).</summary>
        public int ArmyCount => CrowdManager.Instance != null ? CrowdManager.Instance.Count : 0;

        /// <summary>
        /// Tropa placeholder para hordas invocadas (Zumbi Titã/drones) enquanto não há
        /// EnemyConfigSO dedicado por boss — decisão da missão: CrowdManager.DefaultUnit.
        /// </summary>
        public UnitConfigSO PlaceholderEnemy => CrowdManager.Instance != null ? CrowdManager.Instance.DefaultUnit : null;

        /// <summary>Invoca um grupo agregado extra na arena (API nova do BossManager). No-op sem manager.</summary>
        public void SpawnExtraWave(UnitConfigSO type, int count)
        {
            if (BossManager.Instance != null) BossManager.Instance.SpawnExtraWave(type, count);
        }

        /// <summary>Empurra o exército para trás (grito do Zumbi Titã). No-op sem CrowdManager.</summary>
        public void KnockbackArmy(float meters)
        {
            if (CrowdManager.Instance != null) CrowdManager.Instance.KnockbackArmy(meters);
        }

        /// <summary>Slow motion SEMPRE via VFXManager (contrato §1.10) — nunca Time.timeScale direto.</summary>
        public void SlowMotion(float scale, float seconds)
        {
            if (VFXManager.Instance != null) VFXManager.Instance.SlowMotion(scale, seconds);
        }

        /// <summary>
        /// RNG DERIVADO da seed da fase (contrato §1.6: System.Random, padrão seed×primo+constante).
        /// Cada behavior usa o seu (primo, offset) próprio para não competir com o RNG da pista.
        /// </summary>
        public System.Random CreateDerivedRng(int prime, int offset)
        {
            int seed = 0;
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.CurrentLevel != null) seed = gm.CurrentLevel.seed;
            return new System.Random(seed * prime + offset);
        }
    }
}
