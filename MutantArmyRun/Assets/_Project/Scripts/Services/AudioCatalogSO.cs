using UnityEngine;

namespace MutantArmy.Services
{
    /// <summary>
    /// Catálogo de áudio por EVENTO do jogo (1 asset, preenchido pelo MAR Tools/Build Audio —
    /// e ainda pelo legado Build Juice, que continua escrevendo o subconjunto antigo). Campo
    /// vazio é no-op silencioso no AudioManager — o jogo roda com qualquer subconjunto de clips
    /// (mesmo zero). Fontes (PLANO-DE-USO §1.7, todos CC0 1.0 Kenney): phaserUp/Down (portais),
    /// handleCoins (moeda), pepSound (pop de multiplicação), powerUp (fanfarra de Supply / mutação),
    /// impactPunch_heavy (hit no boss), lowFrequency_explosion (rugido — Lacuna L6; explosão),
    /// footstep_grass/concrete (passos da multidão), click/confirmation (UI), jingles_HIT (vitória/derrota).
    /// Música de fundo por mundo: <see cref="musicByWorld"/> indexado por worldIndex (0-based);
    /// na ausência de loops dedicados (Lacuna L7) o AudioFactory aponta um jingle adequado em loop.
    /// Slots da missão Nota 10 (weaknessHit/resistedHit/comboSting/bossDeath/coinBurst/enemyPop/
    /// goodChoice/badChoice/riskWin/riskLose/specialWarning) seguem a mesma regra de no-op e
    /// entram no gerador MAR Tools/Build Audio na Onda 4 (até lá ficam mudos — jogo intacto).
    /// </summary>
    [CreateAssetMenu(menuName = "MutantArmy/Audio Catalog")]
    public class AudioCatalogSO : ScriptableObject
    {
        [Header("Portais (CANON §3.4 — feedback honesto: positivo sobe, negativo desce)")]
        public AudioClip gatePositive;
        public AudioClip gateNegative;

        [Header("Economia e multidão")]
        public AudioClip coin;          // moeda voadora do overflow de Supply (rate-limit no manager)
        public AudioClip pop;           // pop de multiplicação 1→N (cascata com pitch random ±5%)
        public AudioClip supplyFanfare; // overflow de Supply (CANON §3.2 — prêmio, nunca punição)

        [Header("Multidão (passos agregados por densidade — PLANO §1.7)")]
        public AudioClip footstep;      // loop/one-shot de passo (pitch random ±10% por densidade)

        [Header("Boss e impacto")]
        public AudioClip bossHit;       // pulso de dano (round-robin de pitch)
        public AudioClip bossRoar;      // troca de fase — placeholder Lacuna L6 até roar CC0 dedicado
        public AudioClip explosion;     // telegraph do boss / golpe final (camada de peso)

        [Header("Boss elemental (missão Nota 10 — FRAQUEZA!/RESISTIU!)")]
        public AudioClip weaknessHit;   // acertou a fraqueza — sting brilhante (substitui o bossHit no hit)
        public AudioClip resistedHit;   // golpe resistido/imune — impacto seco ("bateu em parede")
        public AudioClip specialWarning;// telegraph do ataque especial (alarme curto, leitura clara)
        public AudioClip bossDeath;     // golpe final / morte cinematográfica (camada sobre explosion)

        [Header("Combos e celebração (missão Nota 10)")]
        public AudioClip comboSting;    // 1 sting por combo conquistado (cascata staggered no manager)
        public AudioClip coinBurst;     // chuva de moedas (wave de inimigos limpa)

        [Header("Inimigos de pista (missão Nota 10)")]
        public AudioClip enemyPop;      // morte de inimigo de pista (pitch random, rate-limited)

        [Header("Veredito de portal/risco (missão Nota 10 — BOA ESCOLHA!)")]
        public AudioClip goodChoice;    // rota ótima escolhida (camada sobre gatePositive)
        public AudioClip badChoice;     // portal armadilha
        public AudioClip riskWin;       // zona de risco vencida ("x10!")
        public AudioClip riskLose;      // zona de risco perdida (impacto seco)

        [Header("Progressão")]
        public AudioClip mutation;      // mutação ganha (CANON §4 — upgrade celebrado)

        [Header("UI")]
        public AudioClip uiClick;
        public AudioClip uiConfirm;

        [Header("Fim de fase (doc 09 §4.4/§4.5)")]
        public AudioClip victoryJingle;
        public AudioClip defeatJingle;

        [Header("Música de fundo em loop por mundo (indexada por worldIndex — Lacuna L7)")]
        [Tooltip("Indexada DIRETO por WorldConfigSO.worldIndex (assets do MVP são 1-based: " +
                 "W01=1, W02=2, W03=3 — o slot 0 fica vago). Vazio/curto cai no fallback: " +
                 "WorldConfigSO.musicTrack e, por fim, no-op silencioso.")]
        public AudioClip[] musicByWorld;

        /// <summary>
        /// Música do mundo, indexada direto por worldIndex (como autorado no WorldConfigSO).
        /// Null se fora do range ou slot vazio — o AudioManager trata como no-op silencioso.
        /// </summary>
        public AudioClip MusicForWorld(int worldIndex)
        {
            if (musicByWorld == null || worldIndex < 0 || worldIndex >= musicByWorld.Length)
                return null;
            return musicByWorld[worldIndex];
        }
    }
}
