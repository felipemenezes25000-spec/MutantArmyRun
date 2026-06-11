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
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] private float _damping = 4f;                        // k do damping exponencial
        [SerializeField] private Vector3 _baseOffset = new Vector3(0f, 9f, -7f);   // retrato 9:16

        private Vector3 _lastCentroid;   // cache anti-NaN do frame anterior

        private void LateUpdate()   // SEMPRE depois da sim do crowd (doc 12 §4.2)
        {
            CrowdManager crowd = CrowdManager.Instance;
            if (crowd == null) return;

            Vector3 centroid = crowd.Count > 0 ? crowd.Centroid : _lastCentroid;
            _lastCentroid = centroid;

            float t = 1f - Mathf.Exp(-_damping * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, centroid + DynamicOffset(), t);
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
