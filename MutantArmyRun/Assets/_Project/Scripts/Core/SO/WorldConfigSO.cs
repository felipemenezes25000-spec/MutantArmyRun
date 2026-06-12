using UnityEngine;

namespace MutantArmy.Core
{
    /// <summary>Config de mundo (doc 12 §5.1; CANON §7 — MVP: 3 mundos enxutos, 20 fases).</summary>
    [CreateAssetMenu(menuName = "MutantArmy/World")]
    public class WorldConfigSO : ScriptableObject
    {
        public int worldIndex;
        public string displayNameKey;          // "Campo Inicial", "Cidade Zumbi"...
        public LevelConfigSO[] levels;
        public BossConfigSO worldBoss;         // fase 10 (fase 7 no MVP p/ M1)
        public Material skyboxMaterial;
        public GameObject[] trackSegmentPrefabs;                  // tema visual da pista
        public AudioClip musicTrack;
        public RewardConfigSO worldClearReward;                   // boss de mundo: 10 gemas + baú

        // ---- Atmosfera & vestimenta visual (preenchidos pelo WorldVisualFactory; lidos
        // pelo WorldAtmosphereApplier no BeginRun — doc 01 §6: vibrante e legível em 3 s).
        // Os initializers são o fallback do W01 p/ assets antigos sem os campos no YAML.
        [Header("Atmosfera (WorldVisualFactory)")]
        public Color skyTopColor = new Color(0.34f, 0.62f, 0.99f);
        public Color skyHorizonColor = new Color(0.78f, 0.92f, 1.00f);
        public Color fogColor = new Color(0.75f, 0.88f, 0.98f);
        public Color sunColor = new Color(1.00f, 0.96f, 0.86f);
        public Color ambientColor = new Color(0.62f, 0.68f, 0.75f);
        public Material trackMaterial;         // chão da pista (grama/asfalto/areia)
        public GameObject[] propPrefabs;       // decoração das bordas (árvores/prédios/cactos)
    }
}
