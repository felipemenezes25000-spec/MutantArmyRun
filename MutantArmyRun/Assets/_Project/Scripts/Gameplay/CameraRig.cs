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
    ///
    /// MORTE/DESTAQUE (missão Nota 10): <see cref="PunchFocus"/> é um zoom TEMPORÁRIO num
    /// ponto arbitrário (morte do boss, núcleo exposto) implementado como CAMADA decadente
    /// por cima do enquadramento corrente — não é um 3º estado do blend binário: o peso sobe
    /// e desce com exponencial em tempo unscaled e a câmera SEMPRE volta ao enquadramento
    /// vigente (corrida ou boss), mesmo com troca de estado no meio.
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

        // ---- PunchFocus (missão Nota 10): zoom TEMPORÁRIO num ponto do mundo ----
        // Camada decadente POR CIMA do enquadramento corrente (corrida OU boss) — não é um
        // 3º estado: o peso sobe/desce com blend exponencial e morre sozinho, então troca de
        // estado no meio (ClearBossFraming, restart) nunca deixa a câmera presa.
        [SerializeField] private float _punchCloseFactor = 0.45f;   // 1 = parado · 0 = colado no ponto
        [SerializeField] private float _punchMinHeight = 2.5f;      // nunca mergulha abaixo do ponto

        private Vector3 _lastCentroid;   // cache anti-NaN do frame anterior
        private Quaternion _runRotation; // orientação de corrida autorada na cena (volta no fim do boss)

        // dados empurrados pelo BossManager — a CameraRig só INTERPOLA (não conhece o boss)
        private bool _bossActive;
        private Vector3 _bossFocus;      // ponto-alvo do olhar: meio entre exército e boss
        private bool _hasBossFocus;
        private float _bossBlend;        // 0 = corrida · 1 = enquadramento de boss (sobe/desce suave)

        // estado do PunchFocus — relógio UNSCALED: o zoom de morte acontece DURANTE o slow-mo
        private Vector3 _punchPoint;
        private float _punchStrength;    // 0..1 — quanto da aproximação máxima aplicar
        private float _punchUntil;       // Time.unscaledTime em que o peso começa a cair
        private float _punchBlendK = 8f; // k do blend exponencial (derivado de seconds no Punch)
        private float _punchWeight;      // 0 = enquadramento corrente · 1 = close no ponto

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

        /// <summary>
        /// Zoom/aproximação TEMPORÁRIA a um ponto do mundo (morte do boss, núcleo exposto —
        /// missão Nota 10). <paramref name="strength"/> 0..1 dosa quanto da aproximação máxima
        /// aplicar; <paramref name="seconds"/> é a janela de sustain (clamp 0,1–3 s). O peso
        /// sobe e desce com blend EXPONENCIAL em tempo unscaled (o close de morte roda dentro
        /// do slow-mo canônico) e a restauração ao enquadramento corrente — corrida OU boss —
        /// é garantida porque o punch é camada decadente, não estado: ClearBossFraming e o
        /// soft reset continuam funcionando por baixo dele.
        /// </summary>
        public void PunchFocus(Vector3 worldPoint, float strength, float seconds)
        {
            // entrada inválida (NaN/Inf de view ainda não posicionada) nunca entra no rig
            if (float.IsNaN(worldPoint.x) || float.IsNaN(worldPoint.y) || float.IsNaN(worldPoint.z)) return;
            if (float.IsInfinity(worldPoint.x) || float.IsInfinity(worldPoint.y) || float.IsInfinity(worldPoint.z)) return;

            _punchPoint = worldPoint;
            _punchStrength = Mathf.Clamp01(strength);
            seconds = Mathf.Clamp(seconds, 0.1f, 3f);
            _punchUntil = Time.unscaledTime + seconds;
            // entrada/saída em ~35% da janela (4 constantes de tempo ≈ 98% do caminho)
            _punchBlendK = 4f / Mathf.Max(0.12f, seconds * 0.35f);
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

            // PunchFocus: peso sobe enquanto a janela vive e DECAI sozinho depois — tudo em
            // tempo UNSCALED (o close de morte roda dentro do slow-mo canônico do golpe final)
            float punchTarget = Time.unscaledTime < _punchUntil ? 1f : 0f;
            float punchT = 1f - Mathf.Exp(-_punchBlendK * Time.unscaledDeltaTime);
            _punchWeight = Mathf.Clamp01(Mathf.Lerp(_punchWeight, punchTarget, punchT));
            if (_punchWeight > 0.001f)
            {
                // close = anda pela RETA ponto→câmera (mantém o ângulo de leitura), com piso
                // de altura para nunca mergulhar no chão da arena
                Vector3 closeUp = _punchPoint + (desired - _punchPoint) * _punchCloseFactor;
                closeUp.y = Mathf.Max(closeUp.y, _punchPoint.y + _punchMinHeight);
                desired = Vector3.Lerp(desired, closeUp, _punchWeight * _punchStrength);
            }

            // durante o punch o follow corre em tempo real (Lerp do dt): o zoom de morte não
            // fica 0,3× mais lento por causa do slow-mo; sem punch, comportamento idêntico
            float followDt = Mathf.Lerp(Time.deltaTime, Time.unscaledDeltaTime, _punchWeight);
            float t = 1f - Mathf.Exp(-_damping * followDt);
            transform.position = Vector3.Lerp(transform.position, desired, t);

            // leve ângulo para baixo no clímax: mira o foco (entre exército e boss) para o
            // golem ler INTEIRO no terço superior. O alvo de rotação MISTURA o LookAt do boss
            // com a orientação de corrida autorada por _bossBlend — ao sair, volta suave ao
            // pitch de corrida em vez de travar inclinada. O PunchFocus entra como camada
            // final: olha para o ponto na proporção do peso e devolve sozinho ao decair.
            bool bossLooking = _bossBlend > 0.001f && _hasBossFocus;
            bool punchLooking = _punchWeight > 0.001f;
            if (bossLooking || punchLooking)
            {
                Quaternion targetRot = _runRotation;
                if (bossLooking)
                {
                    Vector3 toFocus = _bossFocus - transform.position;
                    Quaternion bossLook = toFocus.sqrMagnitude > 1e-4f
                        ? Quaternion.LookRotation(toFocus.normalized, Vector3.up)
                        : _runRotation;
                    targetRot = Quaternion.Slerp(_runRotation, bossLook, _bossBlend);
                }
                if (punchLooking)
                {
                    Vector3 toPunch = _punchPoint - transform.position;
                    if (toPunch.sqrMagnitude > 1e-4f)
                    {
                        Quaternion punchLook = Quaternion.LookRotation(toPunch.normalized, Vector3.up);
                        targetRot = Quaternion.Slerp(targetRot, punchLook, _punchWeight * _punchStrength);
                    }
                }
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
