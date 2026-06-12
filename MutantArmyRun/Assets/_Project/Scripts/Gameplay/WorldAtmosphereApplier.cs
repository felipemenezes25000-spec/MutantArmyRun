using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Aplica a atmosfera do mundo (skybox, fog, ambiente, cor do sol) no BeginRun: assina
    /// GameManager.StateEntered e, ao entrar em Running, lê o WorldConfigSO da fase atual e
    /// escreve em RenderSettings. Mesma regra do LevelManager (doc 12 §4.1): Core não enxerga
    /// Gameplay, então é este manager que assina o evento — registrado no GameSceneBootstrap
    /// pelo WorldVisualFactory. O método estático <see cref="Apply"/> é a fonte única da
    /// receita de atmosfera: o factory de editor usa o MESMO código para gravar o default da
    /// cena Game (W01), garantindo que editor e runtime nunca divirjam.
    /// </summary>
    public class WorldAtmosphereApplier : MonoBehaviour, IInitializable
    {
        [SerializeField] private Light _sunLight;   // directional da cena Game (wired pelo factory)

        // Fog linear casando com a janela de spawn do LevelManager (60 m à frente):
        // segmentos/props nascem JÁ dentro da névoa — nunca "pop" seco no horizonte.
        private const float FogStart = 25f;
        private const float FogEnd = 80f;

        public void Init()   // chamado pelo GameSceneBootstrap (doc 12 §3.3)
        {
            if (GameManager.Instance == null) return;
            // -= antes de += : Init repetido (soft reset de cena) não duplica a inscrição.
            GameManager.Instance.StateEntered -= HandleStateEntered;
            GameManager.Instance.StateEntered += HandleStateEntered;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.StateEntered -= HandleStateEntered;
        }

        private void HandleStateEntered(GameState state)
        {
            // Aplica só na entrada da corrida; BossFight herda a atmosfera (mesma cena/fase).
            if (state != GameState.Running) return;
            LevelConfigSO level = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : null;
            if (level == null || level.world == null) return;
            Apply(level.world, ResolveSun());
        }

        private Light ResolveSun()
        {
            if (_sunLight != null) return _sunLight;
            return RenderSettings.sun;   // fallback: o sol registrado na cena
        }

        /// <summary>
        /// Receita única de atmosfera por mundo (runtime E editor): skybox tintado, fog
        /// linear, ambiente Trilight derivado de ambientColor e sol com a cor do mundo.
        /// Ambiente Trilight é barato (3 cores, zero probes) — dentro do orçamento mobile
        /// do doc 12 §2.4.
        /// </summary>
        public static void Apply(WorldConfigSO world, Light sun)
        {
            if (world == null) return;

            if (world.skyboxMaterial != null) RenderSettings.skybox = world.skyboxMaterial;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = world.fogColor;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = world.ambientColor * 1.15f;
            RenderSettings.ambientEquatorColor = world.ambientColor;
            RenderSettings.ambientGroundColor = world.ambientColor * 0.55f;

            if (sun != null) sun.color = world.sunColor;
        }
    }
}
