using System.Collections.Generic;
using MutantArmy.Core;
using UnityEngine;

namespace MutantArmy.UI
{
    /// <summary>
    /// Helper de PREVIEW 3D do boss para o Boss Scout (OVL-01): instancia o prefab do
    /// boss (BossConfigSO.prefab — Quaternius/KayKit, já existem) numa "mini-cena" offscreen
    /// e o renderiza num RenderTexture que o cartão exibe num RawImage. O boss gira devagar
    /// = "vivo", preenchendo o vazio do card sem depender de arte (scoutCardArt é null).
    ///
    /// Sem layer dedicado (não mexo no TagManager): o rig é montado MUITO longe da pista
    /// (PreviewOrigin, y≈-9000) e a câmera tem near/far apertados em torno do modelo — nada
    /// mais do mundo cai no frustum. Câmera SÓ liga enquanto o cartão está aberto (~2 s) e
    /// desliga depois; RT pequena (default 384²). Cleanup total em Stop()/OnDestroy — destrói
    /// RT/câmera/luz/instância/root, sem vazar (regra dura de pooling/limpeza do contrato §1.5).
    ///
    /// Null-safe: se o prefab for null (ou a instância falhar), Show() retorna false e o
    /// BossScoutOverlay cai no fallback de silhueta (Opção B). Roda em unscaled time para
    /// girar mesmo com timeScale 0/slow-motion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossPortraitRenderer : MonoBehaviour
    {
        // Longe o suficiente da pista (que vive perto da origem) para o frustum apertado da
        // câmera de preview não pegar segmentos/props/exército por engano.
        private static readonly Vector3 PreviewOrigin = new Vector3(0f, -9000f, 0f);

        private const int DefaultSize = 384;             // RT quadrada barata (mobile)
        private const float RotateDegreesPerSecond = 24f; // giro lento e contínuo = "vivo"

        private RenderTexture _rt;
        private Camera _camera;
        private Light _keyLight;
        private GameObject _root;        // pai do rig (câmera+luz+instância), desativado quando ocioso
        private Transform _modelPivot;   // gira em torno do centro do modelo
        private GameObject _instance;    // prefab do boss instanciado
        private bool _spinning;

        // P2-UILAYOUT — backdrop com LEVE gradiente atrás do modelo (o clear da câmera é cor
        // chapada; o quad dá o degradê pedido). Tingido pelo elemento, claro em cima e um pouco
        // mais escuro embaixo — integra ao card creme sem virar retângulo preto. Material/quad
        // gerados em código e destruídos no OnDestroy (sem vazar, regra §1.5).
        private Renderer _backdrop;
        private Material _backdropMat;

        /// <summary>RenderTexture vivo do preview (null se nada está sendo renderizado).</summary>
        public RenderTexture Texture => _rt;

        /// <summary>
        /// Monta o rig (idempotente: reusa o que já existe) e instancia o prefab do boss,
        /// enquadrado pela câmera com fundo tingido por <paramref name="tint"/> (cor do
        /// elemento). Retorna true se conseguiu um preview 3D; false se o prefab é null/falhou
        /// (o chamador deve usar o fallback de silhueta). <paramref name="size"/> em pixels da RT.
        /// </summary>
        public bool Show(BossConfigSO boss, Color tint, int size = DefaultSize)
        {
            if (boss == null || boss.prefab == null) return false;

            EnsureRig(size);
            ClearInstance();

            // P2-UILAYOUT — fundo do retrato. ANTES: tint × 0,18/0,22 = um marrom-escuro quase
            // preto que destoava do card creme (parecia um buraco preto no shot). AGORA: mistura
            // a cor do elemento com um creme claro (mesma família do card) e mantém boa saturação
            // — backdrop CLARO e tingido que integra ao card, com contraste suficiente para o
            // modelo escuro 3D ler bem. Alpha cheio (o RawImage é opaco; o card tem moldura).
            Color bg = TintedBackdrop(tint);
            if (_camera != null) _camera.backgroundColor = bg;
            // Backdrop com leve gradiente sobre a cor de clear (topo claro → base levemente escura).
            if (_backdropMat != null) _backdropMat.color = bg;

            _instance = TryInstantiate(boss.prefab);
            if (_instance == null) return false;

            FrameModel(_instance);
            PlaceBackdrop();
            _root.SetActive(true);
            _camera.enabled = true;
            _spinning = true;
            return true;
        }

        /// <summary>
        /// Para o preview: desliga a câmera, destrói a instância e ESCONDE o rig (mantém
        /// câmera/luz/RT para o próximo boss — reuso barato). Chame ao fechar o cartão.
        /// </summary>
        public void Stop()
        {
            _spinning = false;
            if (_camera != null) _camera.enabled = false;
            ClearInstance();
            if (_root != null) _root.SetActive(false);
        }

        private void LateUpdate()
        {
            // Gira em unscaled time: continua vivo com timeScale 0 (pausa) ou slow-motion.
            if (!_spinning || _modelPivot == null) return;
            _modelPivot.Rotate(0f, RotateDegreesPerSecond * Time.unscaledDeltaTime, 0f, Space.Self);
        }

        private void OnDestroy()
        {
            // Limpeza total: RT/câmera/luz/instância/backdrop/root — nada vaza (contrato §1.5).
            ClearInstance();
            if (_rt != null)
            {
                _rt.Release();
                DestroyImmediateSafe(_rt);
                _rt = null;
            }
            // Material e textura do backdrop são gerados em código → liberados à mão (o quad em si
            // morre com o _root). mainTexture é a gradiente criada no EnsureBackdrop.
            if (_backdropMat != null)
            {
                DestroyImmediateSafe(_backdropMat.mainTexture);
                DestroyImmediateSafe(_backdropMat);
                _backdropMat = null;
            }
            _backdrop = null;
            if (_root != null)
            {
                DestroyImmediateSafe(_root);
                _root = null;
            }
            _camera = null;
            _keyLight = null;
            _modelPivot = null;
        }

        // ------------------------------------------------------------------ montagem do rig

        private void EnsureRig(int size)
        {
            if (size < 64) size = DefaultSize;

            if (_rt == null || _rt.width != size)
            {
                if (_rt != null) { _rt.Release(); DestroyImmediateSafe(_rt); }
                _rt = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32)
                {
                    name = "BossPortraitRT",
                    antiAliasing = 1,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                _rt.Create();
                if (_camera != null) _camera.targetTexture = _rt;
            }

            if (_root == null)
            {
                // Root NA RAIZ da cena (sem pai): este renderer vive num GameObject de UI sob um
                // Canvas; herdar o RectTransform/CanvasScaler distorceria a "mini-cena" 3D. O rig
                // fica isolado em PreviewOrigin e é destruído no OnDestroy deste componente.
                _root = new GameObject("[BossPortraitRig]");
                _root.transform.SetParent(null, false);
                _root.transform.position = PreviewOrigin;
                _root.hideFlags = HideFlags.DontSave;

                _modelPivot = new GameObject("ModelPivot").transform;
                _modelPivot.SetParent(_root.transform, false);

                // Câmera de preview: perspectiva suave, fundo sólido, SÓ a "mini-cena" no frustum.
                var camGo = new GameObject("PreviewCamera");
                camGo.transform.SetParent(_root.transform, false);
                _camera = camGo.AddComponent<Camera>();
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.orthographic = false;
                _camera.fieldOfView = 30f;            // tele leve: menos distorção no retrato
                _camera.nearClipPlane = 0.05f;
                _camera.farClipPlane = 50f;
                _camera.depth = -50f;                  // renderiza ANTES da câmera principal
                _camera.allowHDR = false;
                _camera.allowMSAA = false;
                _camera.useOcclusionCulling = false;
                _camera.targetTexture = _rt;
                _camera.enabled = false;

                // Luz-chave: dá volume ao modelo (a cena de gameplay pode ter luz flat).
                var lightGo = new GameObject("KeyLight");
                lightGo.transform.SetParent(_root.transform, false);
                lightGo.transform.localRotation = Quaternion.Euler(35f, 145f, 0f);
                _keyLight = lightGo.AddComponent<Light>();
                _keyLight.type = LightType.Directional;
                _keyLight.intensity = 1.25f;
                _keyLight.color = new Color(1f, 0.97f, 0.9f);
                // Só ilumina o preview: render fora dos passes globais é controlado pela
                // proximidade; manter cullingMask amplo é ok pois o rig está isolado em -9000.

                EnsureBackdrop();

                // Nasce DESLIGADO: o prefab do boss é instanciado sob este root inativo, então
                // os Awake/OnEnable dos scripts de gameplay do boss NÃO disparam (evita registrar
                // no manager errado / LogError). DisableGameplayBehaviours desliga antes do
                // SetActive(true) — quando ativa, só renderers/animator entram em cena.
                _root.SetActive(false);
            }
        }

        /// <summary>
        /// P2-UILAYOUT — cria UMA vez um quad de fundo com gradiente vertical (claro→levemente
        /// escuro), atrás do modelo. Usa shader unlit (URP "Unlit" ou, no fallback, o
        /// "Sprites/Default" sempre presente) com uma textura 1×N gerada em código. Se nenhum
        /// shader resolver, deixa _backdrop null e o preview cai só na cor de clear da câmera
        /// (degrada, não quebra). Tudo destruído no OnDestroy.
        /// </summary>
        private void EnsureBackdrop()
        {
            if (_backdrop != null || _root == null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return;     // sem shader: fundo fica só na cor de clear

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            // Quad de gameplay não deve ter colisão na mini-cena.
            var col = go.GetComponent<Collider>();
            if (col != null) DestroyImmediateSafe(col);
            go.name = "Backdrop";
            go.transform.SetParent(_root.transform, false);

            _backdrop = go.GetComponent<Renderer>();
            _backdropMat = new Material(shader) { name = "BossPortraitBackdrop" };
            _backdropMat.mainTexture = CreateGradientTexture();
            _backdrop.sharedMaterial = _backdropMat;
            _backdrop.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _backdrop.receiveShadows = false;
        }

        /// <summary>
        /// Textura 1×64 com gradiente vertical sutil: topo em branco cheio, base levemente
        /// escurecida (multiplica a cor do material). Dá o "leve gradiente" pedido sem segundo
        /// material. Liberada junto do material no OnDestroy.
        /// </summary>
        private static Texture2D CreateGradientTexture()
        {
            const int h = 64;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false)
            {
                name = "BossPortraitGradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);              // 0 base, 1 topo
                float v = Mathf.Lerp(0.78f, 1f, t);        // base 78% → topo 100%
                tex.SetPixel(0, y, new Color(v, v, v, 1f));
            }
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>
        /// Enquadra o quad de fundo logo atrás do pivot, perpendicular à câmera e grande o
        /// suficiente para preencher a vista. Chamado após FrameModel (precisa da câmera já
        /// posicionada). Null-safe: sem backdrop, no-op.
        /// </summary>
        private void PlaceBackdrop()
        {
            if (_backdrop == null || _camera == null) return;

            Transform bt = _backdrop.transform;
            Vector3 pivotWorld = _modelPivot != null ? _modelPivot.position : _root.transform.position;
            Vector3 camPos = _camera.transform.position;
            Vector3 toCam = (camPos - pivotWorld);
            float dist = toCam.magnitude;

            // Atrás do pivot; a face visível do Quad (normal +Z local) precisa encarar a câmera,
            // então o +Z aponta para a câmera (-forward da câmera) — senão é culada por backface.
            bt.position = pivotWorld - _camera.transform.forward * Mathf.Max(0.5f, dist * 0.6f);
            bt.rotation = Quaternion.LookRotation(-_camera.transform.forward, _camera.transform.up);

            // Tamanho que cobre o frustum à distância do quad, com folga.
            float quadDist = Vector3.Distance(camPos, bt.position);
            float halfFov = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float fillH = 2f * quadDist * Mathf.Tan(halfFov) * 1.4f;
            bt.localScale = new Vector3(fillH, fillH, 1f);
        }

        /// <summary>
        /// P2-UILAYOUT — cor de fundo CLARA tingida pelo elemento (não o marrom-escuro anterior).
        /// Mistura a cor do elemento com um creme (~0,86) preservando o matiz: dá identidade de
        /// elemento sem virar um retângulo preto que destoa do card. Clamp final garante um piso
        /// claro mesmo para elementos escuros (Sombra/Metal).
        /// </summary>
        private static Color TintedBackdrop(Color tint)
        {
            const float cream = 0.86f;     // base creme do card
            const float tintMix = 0.42f;   // quanto da cor do elemento entra no creme
            float r = Mathf.Lerp(cream, tint.r, tintMix);
            float g = Mathf.Lerp(cream, tint.g, tintMix);
            float b = Mathf.Lerp(cream, tint.b, tintMix);
            // Piso de luminosidade: nunca deixa o fundo afundar (elementos escuros).
            return new Color(Mathf.Clamp(r, 0.6f, 1f), Mathf.Clamp(g, 0.6f, 1f), Mathf.Clamp(b, 0.6f, 1f), 1f);
        }

        // ------------------------------------------------------------------ enquadramento

        /// <summary>
        /// Posiciona o modelo no pivot e ajusta a câmera para enquadrar os bounds do mesh —
        /// funciona para qualquer escala de prefab (Quaternius/KayKit variam). Mira levemente
        /// de cima para um ângulo heroico.
        /// </summary>
        private void FrameModel(GameObject model)
        {
            // Pivot fica na origem da mini-cena (PreviewOrigin); o modelo é deslocado para que
            // o CENTRO dos seus bounds caia exatamente no pivot → gira em torno do próprio centro.
            _modelPivot.localRotation = Quaternion.identity;
            _modelPivot.localPosition = Vector3.zero;
            model.transform.SetParent(_modelPivot, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            Bounds b = ComputeBounds(model);                  // bounds em espaço de MUNDO
            Vector3 pivotWorld = _modelPivot.position;        // == PreviewOrigin
            // offset que leva o centro do mesh para o pivot (mundo → local do pivot, sem escala extra).
            model.transform.position += (pivotWorld - b.center);

            float radius = Mathf.Max(0.25f, b.extents.magnitude);
            // Distância para caber o raio no FOV, com folga (1.25×).
            float halfFov = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float dist = (radius * 1.25f) / Mathf.Tan(halfFov);

            // Câmera num ângulo 3/4 ligeiramente acima: pose heroica.
            Vector3 dir = (Quaternion.Euler(12f, 18f, 0f) * Vector3.back).normalized;
            _camera.transform.position = pivotWorld + dir * dist;
            _camera.transform.LookAt(pivotWorld);
            _camera.nearClipPlane = Mathf.Max(0.02f, dist - radius * 2f);
            _camera.farClipPlane = dist + radius * 4f;
        }

        private static Bounds ComputeBounds(GameObject model)
        {
            var renderers = ListPool.Get();
            model.GetComponentsInChildren(true, renderers);
            if (renderers.Count == 0)
            {
                ListPool.Release(renderers);
                return new Bounds(model.transform.position, Vector3.one);
            }

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Count; i++) b.Encapsulate(renderers[i].bounds);
            ListPool.Release(renderers);
            return b;
        }

        // ------------------------------------------------------------------ utilidades

        private GameObject TryInstantiate(GameObject prefab)
        {
            GameObject go = Instantiate(prefab, _modelPivot);
            if (go == null) return null;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            DisableGameplayBehaviours(go);
            return go;
        }

        /// <summary>
        /// Desliga componentes de GAMEPLAY do prefab (colliders, rigidbodies, scripts) para o
        /// preview ser puramente visual — o prefab do boss carrega lógica que não deve rodar
        /// na "mini-cena". Mantém só renderers/animators (a pose idle/respiração dá vida).
        /// </summary>
        private static void DisableGameplayBehaviours(GameObject go)
        {
            var colliders = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

            var bodies = go.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++) bodies[i].isKinematic = true;

            // MonoBehaviours custom: desliga tudo que NÃO seja Animator (mantém idle).
            var behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] != null) behaviours[i].enabled = false;
        }

        private void ClearInstance()
        {
            if (_instance != null)
            {
                DestroyImmediateSafe(_instance);
                _instance = null;
            }
        }

        // Em runtime usa Destroy; em testes EditMode (sem play) Destroy não roda no mesmo
        // frame — DestroyImmediate cobre os dois sem vazar. Null-safe.
        private static void DestroyImmediateSafe(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        // Pool minúsculo de listas de Renderer p/ ComputeBounds (zero alloc por frame —
        // só é chamado no Show, mas mantém o padrão de não alocar à toa).
        private static class ListPool
        {
            private static readonly Stack<List<Renderer>> s_pool = new Stack<List<Renderer>>();

            public static List<Renderer> Get()
            {
                return s_pool.Count > 0 ? s_pool.Pop() : new List<Renderer>(16);
            }

            public static void Release(List<Renderer> list)
            {
                list.Clear();
                s_pool.Push(list);
            }
        }
    }
}
