using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Rig de câmera independente (doc 12 §4.12) — JAMAIS filho do CrowdAnchor/player,
    /// ou herda cada solavanco lateral do drag. Três regras do contrato:
    /// 1. Segue o CENTRÓIDE da multidão, não o líder (multiplicações deslocam a massa);
    ///    lista zerada num frame usa o cache — nunca NaN na câmera.
    /// 2. Damping exponencial Exp(−k·dt): framerate-independente (mesmo enquadramento a
    ///    30 ou 60 fps) — Lerp com fator fixo NÃO é.
    /// 3. Enquadramento dinâmico: o raio da formação cresce com √n; a câmera recua e sobe
    ///    na mesma proporção para o exército inteiro caber na tela (Pilar 3: espetáculo).
    ///
    /// CLÍMAX (boss): durante o BossFight a corrida cede lugar ao confronto. O BossManager
    /// EMPURRA os dados do enquadramento de boss via <see cref="SetBossFraming"/> (a CameraRig
    /// NÃO referencia o BossManager — sem acoplamento/ciclo, doc 12 §2.3): a câmera recua,
    /// sobe e inclina para baixo para o golem aparecer INTEIRO e imponente no terço
    /// superior-central, com a frente do exército no terço inferior. A transição (entrada
    /// e saída) é uma mistura suave framerate-independente — nunca um corte seco.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        // Instância de cena para o BossManager EMPURRAR o enquadramento de boss sem que a
        // CameraRig precise conhecer o BossManager (evita acoplamento/ciclo, doc 12 §2.3).
        public static CameraRig Instance { get; private set; }

        [SerializeField] private float _damping = 4f;                        // k do damping exponencial
        [SerializeField] private Vector3 _baseOffset = new Vector3(0f, 9f, -7f);   // retrato 9:16

        // ---- Enquadramento de boss (clímax) ----
        // Recuo/elevação generosos: o golem nasce ~12 m à frente e elevado; a câmera precisa
        // recuar o bastante (−Z grande) e subir para o chefão caber INTEIRO sem cortar o topo,
        // com leve ângulo para baixo (o pitch sai do LookAt no foco entre exército e boss).
        [SerializeField] private Vector3 _bossOffset = new Vector3(0f, 11f, -15f);
        [SerializeField] private float _bossBlendSeconds = 0.8f;             // transição entrada/saída ≈0,6–1,0 s

        private Vector3 _lastCentroid;   // cache anti-NaN do frame anterior
        private Quaternion _runRotation; // orientação de corrida autorada na cena (volta no fim do boss)

        // dados empurrados pelo BossManager — a CameraRig só INTERPOLA (não conhece o boss)
        private bool _bossActive;
        private Vector3 _bossFocus;      // ponto-alvo do olhar: meio entre exército e boss
        private bool _hasBossFocus;
        private float _bossBlend;        // 0 = corrida · 1 = enquadramento de boss (sobe/desce suave)

        private void Awake()
        {
            Instance = this;
            _runRotation = transform.rotation;   // pitch de corrida autorado no prefab/cena
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// O BossManager empurra o enquadramento de boss no BeginFight: a câmera transiciona
        /// para mostrar o chefão imponente. <paramref name="focusPoint"/> é o ponto que a
        /// câmera deve olhar (meio entre o Centroid do exército e a posição do boss).
        /// </summary>
        public void SetBossFraming(Vector3 focusPoint)
        {
            _bossActive = true;
            _bossFocus = focusPoint;
            _hasBossFocus = true;
        }

        /// <summary>Saída do BossFight (vitória/derrota): volta suavemente ao enquadramento de corrida.</summary>
        public void ClearBossFraming()
        {
            _bossActive = false;
        }

        private void LateUpdate()   // SEMPRE depois da sim do crowd (doc 12 §4.2)
        {
            CrowdManager crowd = CrowdManager.Instance;
            if (crowd == null) return;

            Vector3 centroid = crowd.Count > 0 ? crowd.Centroid : _lastCentroid;
            _lastCentroid = centroid;

            // blend do clímax sobe/desce com damping exponencial (framerate-independente, como
            // o resto do rig): o enquadramento de boss "abre" ao entrar e "fecha" ao sair em
            // ≈_bossBlendSeconds. k = 4/T → ~98% do caminho em T (4 constantes de tempo).
            float blendK = _bossBlendSeconds > 0f ? 4f / _bossBlendSeconds : 1000f;
            float target = _bossActive ? 1f : 0f;
            float blendT = 1f - Mathf.Exp(-blendK * Time.deltaTime);
            _bossBlend = Mathf.Clamp01(Mathf.Lerp(_bossBlend, target, blendT));

            // posição-alvo: mistura entre o enquadramento de corrida e o de boss
            Vector3 runTarget = centroid + DynamicOffset();
            Vector3 desired = runTarget;
            if (_bossBlend > 0f && _hasBossFocus)
            {
                Vector3 bossTarget = _bossFocus + _bossOffset;
                desired = Vector3.Lerp(runTarget, bossTarget, _bossBlend);
            }

            float t = 1f - Mathf.Exp(-_damping * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, t);

            // leve ângulo para baixo no clímax: mira o foco (entre exército e boss) para o
            // golem ler INTEIRO no terço superior. O alvo de rotação MISTURA o LookAt do boss
            // com a orientação de corrida autorada por _bossBlend — ao sair, volta suave ao
            // pitch de corrida em vez de travar inclinada.
            if (_bossBlend > 0.001f && _hasBossFocus)
            {
                Vector3 toFocus = _bossFocus - transform.position;
                Quaternion bossLook = toFocus.sqrMagnitude > 1e-4f
                    ? Quaternion.LookRotation(toFocus.normalized, Vector3.up)
                    : _runRotation;
                Quaternion targetRot = Quaternion.Slerp(_runRotation, bossLook, _bossBlend);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
            }
        }

        private Vector3 DynamicOffset()
        {
            CrowdManager crowd = CrowdManager.Instance;
            int n = crowd != null ? crowd.Count : 0;
            float radius = 0.45f * Mathf.Sqrt(n + 1);   // raio filotáxico √n (doc 12 §4.2)
            return _baseOffset + new Vector3(0f, radius * 0.6f, -radius * 0.8f);
        }
    }
}
