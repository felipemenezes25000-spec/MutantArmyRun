using MutantArmy.Core;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Líder da multidão (doc 12 §4.2): avança sozinho pela pista e responde ao drag
    /// lateral do jogador. As unidades convergem aos slots filotáxicos ao redor desta
    /// âncora. Também carrega o ÚNICO trigger do exército ("proxy"): portais e gatilhos
    /// detectam o grupo com 1 evento — zero colliders por unidade (contrato §4.2/§4.3).
    /// </summary>
    public class CrowdAnchor : MonoBehaviour, IInitializable
    {
        public static CrowdAnchor Instance { get; private set; }

        /// <summary>Posição do líder consumida pelo CrowdManager/LevelManager (doc 12 §4.2/§4.11).</summary>
        public static Vector3 Position { get; private set; }

        [SerializeField] private float _forwardSpeed = 4f;              // 4 m/s base: pista de 220 m ≈ 45–75 s
        [SerializeField] private float _laneHalfWidth = 2.2f;
        [SerializeField] private float _dragMetersPerScreenWidth = 4.4f;

        private float _speedMultiplier = 1f;
        private bool _dragging;
        private float _lastPointerX;
        private BoxCollider _proxy;

        public void Init()   // chamado pelo bootstrap da cena Game (doc 12 §3.3)
        {
            Instance = this;
            Position = transform.position;
            _dragging = false;
            EnsureArmyProxy();
        }

        /// <summary>Trilha de meta "Velocidade" (+5%/nível) entra por aqui — Gameplay não enxerga Meta.</summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = Mathf.Max(0.1f, multiplier);
        }

        /// <summary>Soft reset da corrida (doc 12 §4.11): reposiciona sem recarregar cena.</summary>
        public void ResetTo(Vector3 position)
        {
            transform.position = position;
            Position = position;
            _dragging = false;
        }

        private void Update()
        {
            GameManager gm = GameManager.Instance;
            bool running = gm != null && gm.State == GameState.Running;

            Vector3 p = transform.position;
            if (running)
            {
                p.z += _forwardSpeed * _speedMultiplier * Time.deltaTime;
                p.x = Mathf.Clamp(p.x + ReadDragDeltaMeters(), -_laneHalfWidth, _laneHalfWidth);
            }
            else
            {
                _dragging = false;
            }

            transform.position = p;
            Position = p;
            UpdateProxyBounds();
        }

        // Input clássico de propósito: é o caminho mais simples estável enquanto o projeto
        // usa o activeInputHandler default; mouse simula toque no device (simulateMouseWithTouches).
        private float ReadDragDeltaMeters()
        {
            if (!Input.GetMouseButton(0))
            {
                _dragging = false;
                return 0f;
            }
            if (!_dragging)
            {
                _dragging = true;
                _lastPointerX = Input.mousePosition.x;
                return 0f;
            }

            float deltaPixels = Input.mousePosition.x - _lastPointerX;
            _lastPointerX = Input.mousePosition.x;
            return deltaPixels / Mathf.Max(1, Screen.width) * _dragMetersPerScreenWidth;
        }

        private void EnsureArmyProxy()
        {
            // trigger precisa de 1 Rigidbody (kinemático) para gerar eventos contra colliders estáticos
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            _proxy = GetComponent<BoxCollider>();
            if (_proxy == null) _proxy = gameObject.AddComponent<BoxCollider>();
            _proxy.isTrigger = true;
            _proxy.center = new Vector3(0f, 1f, 0f);
            _proxy.size = new Vector3(1.5f, 2f, 1.5f);
        }

        private void UpdateProxyBounds()
        {
            if (_proxy == null) return;
            CrowdManager crowd = CrowdManager.Instance;
            int n = crowd != null ? crowd.Count : 0;

            // AABB acompanha o raio filotáxico (√n) — mas nunca cobre a pista inteira:
            // a escolha de portal continua sendo posição lateral do jogador
            float radius = 0.45f * Mathf.Sqrt(n + 1);
            float width = Mathf.Clamp(radius * 2f, 1f, _laneHalfWidth * 2f);
            _proxy.size = new Vector3(width, 2f, Mathf.Max(1.5f, radius * 2f));
        }
    }
}
