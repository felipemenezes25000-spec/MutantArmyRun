using UnityEngine;

namespace MutantArmy.Services
{
    /// <summary>
    /// Catálogo de SFX por EVENTO do jogo (1 asset, preenchido pelo MAR Tools/Build Juice
    /// com os clips CC0 importados em Assets/_Project/Audio). Campo vazio é no-op silencioso
    /// no AudioManager — o jogo roda com qualquer subconjunto de clips (mesmo zero).
    /// Fontes (PLANO-DE-USO §1.7, todos CC0 1.0 Kenney): phaserUp/Down (portais),
    /// handleCoins (moeda), pepSound (pop de multiplicação), impactPunch_heavy (hit no boss),
    /// lowFrequency_explosion pitch baixo (placeholder de rugido — Lacuna L6),
    /// click/confirmation (UI), jingles_HIT (vitória/derrota).
    /// </summary>
    [CreateAssetMenu(menuName = "MutantArmy/Audio Catalog")]
    public class AudioCatalogSO : ScriptableObject
    {
        [Header("Portais (CANON §3.4 — feedback honesto: positivo sobe, negativo desce)")]
        public AudioClip gatePositive;
        public AudioClip gateNegative;

        [Header("Economia e multidão")]
        public AudioClip coin;          // moeda voadora do overflow de Supply (rate-limit no manager)
        public AudioClip pop;           // pop de multiplicação (cascata com pitch random ±5%)

        [Header("Boss")]
        public AudioClip bossHit;       // pulso de dano (round-robin de pitch)
        public AudioClip bossRoar;      // troca de fase — placeholder Lacuna L6 até roar CC0 dedicado

        [Header("UI")]
        public AudioClip uiClick;
        public AudioClip uiConfirm;

        [Header("Fim de fase (doc 09 §4.4/§4.5)")]
        public AudioClip victoryJingle;
        public AudioClip defeatJingle;
    }
}
