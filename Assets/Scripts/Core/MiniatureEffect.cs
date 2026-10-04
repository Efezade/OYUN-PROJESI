using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace TacticalRPG.Core
{
    /// <summary>
    /// MİNYATÜR görünümü (tilt-shift, Efe 2026-10-04: "For The King'deki gibi uzak yerler bulanık,
    /// yakın yerler net — tanrısal bir bakış"). YALNIZ OVERWORLD'de çalışır; savaşta/yerleştirmede
    /// yumuşakça söner. Aç/kapa ayarı <see cref="DisplaySettings"/>'te (model), bu sınıf yalnız uygular.
    ///
    /// NEDEN URP DEPTH OF FIELD DEĞİL: kamera ORTOGRAFİK; URP'nin DoF'u derinliği perspektif formülüyle
    /// doğrusallaştırdığı için ortografikte anlamsız sonuç veriyor. Bu yüzden kendi geçişimiz var
    /// (<c>Hidden/TacticalRPG/MiniatureTiltShift</c>): küçük çözünürlükte Gauss bulanıklığı + derinliğe
    /// göre net/bulanık karışımı.
    ///
    /// ODAK = takip hedefi (Kam / oyuncu jetonu). <c>CameraFollow</c> onu ekranın ortasında tuttuğu için
    /// net bant ekranın ortasından geçer. Bant ve geçiş genişliği EKRAN YÜKSEKLİĞİ cinsinden verilir
    /// (yakınlaştırma değişince oran aynı kalır), kameranın eğiminden derinliğe çevrilir.
    ///
    /// Renderer asset'ine dokunmaz: geçiş <c>RenderPipelineManager.beginCameraRendering</c> ile yalnız
    /// <see cref="_camera"/> için kuyruğa eklenir (minimap / sahne kamerası etkilenmez).
    /// </summary>
    [DisallowMultipleComponent]
    public class MiniatureEffect : MonoBehaviour
    {
        [Header("Bağlantılar")]
        [SerializeField] private Camera _camera;
        [Tooltip("Atanmazsa efekt her durumda açık kalır (savaşta sönmez).")]
        [SerializeField] private GameStateManager _state;
        [Tooltip("Netlik bu noktada (Kam / oyuncu jetonu). Boş/pasifse ekran ortasının y=0 " +
                 "düzlemine değdiği nokta kullanılır.")]
        [SerializeField] private Transform _focusTarget;
        [Tooltip("Hidden/TacticalRPG/MiniatureTiltShift — build'e girmesi için buradan referanslanır.")]
        [SerializeField] private Shader _shader;

        [Header("Görünüm (ekran yarı-yüksekliği cinsinden)")]
        [Tooltip("Odağın iki yanında TAMAMEN NET kalan bant (0.25 = ekran ortasından yukarı/aşağı " +
                 "yarı yüksekliğin %25'i).")]
        [SerializeField, Range(0f, 1f)] private float _sharpBand = 0.22f;
        [Tooltip("Netten tam bulanığa geçiş mesafesi.")]
        [SerializeField, Range(0.05f, 2f)] private float _falloff = 0.6f;
        [Tooltip("YAKIN tarafın (ekranın altı) en fazla ne kadar bulanacağı. 0 = alt taraf hep net.")]
        [SerializeField, Range(0f, 1f)] private float _nearStrength = 0.3f;
        [Tooltip("Genel güç (1 = tam bulanık uzak).")]
        [SerializeField, Range(0f, 1f)] private float _intensity = 1f;

        [Header("Bulanıklık")]
        [Tooltip("Bulanıklık dokusunun küçültme oranı (büyük = daha yumuşak ve ucuz).")]
        [SerializeField, Range(1, 8)] private int _downsample = 3;
        [Tooltip("Gauss adım çarpanı (büyük = daha geniş bulanıklık; 2'nin üstü desen yapabilir).")]
        [SerializeField, Range(0.5f, 3f)] private float _blurSpread = 1.3f;

        [Header("Geçiş")]
        [Tooltip("Aç/kapa ve overworld↔savaş solma hızı (birim/sn).")]
        [SerializeField, Min(0.1f)] private float _fadeSpeed = 3f;

        private bool  _userEnabled = true;  // DisplaySettings yazar (Awake sırası önemsiz)
        private bool  _stateAllows = true;
        private float _weight;
        private bool  _weightSnapped;

        private Material _material;
        private MiniaturePass _pass;

        private static readonly int BlurTexelId  = Shader.PropertyToID("_MiniBlurTexel");
        private static readonly int FocusId      = Shader.PropertyToID("_MiniFocus");
        private static readonly int CameraId     = Shader.PropertyToID("_MiniCamera");
        private static readonly int IntensityId  = Shader.PropertyToID("_MiniIntensity");
        private static readonly int BlurTexId    = Shader.PropertyToID("_MiniatureBlurTex");

        /// <summary>Kullanıcı ayarı (Ayarlar → GÖRÜNTÜ → MİNYATÜR).</summary>
        public void SetUserEnabled(bool on) => _userEnabled = on;

        private void Awake()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_shader == null) _shader = Shader.Find("Hidden/TacticalRPG/MiniatureTiltShift");
            if (_shader == null || !_shader.isSupported)
            {
                Debug.LogWarning("[Minyatur] Shader yok/desteklenmiyor — efekt kapalı.");
                enabled = false;
                return;
            }
            _material = CoreUtils.CreateEngineMaterial(_shader);
            _pass     = new MiniaturePass(_material);
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += HandleBeginCamera;
            if (_state != null)
            {
                _state.OnStateChanged += HandleStateChanged;
                HandleStateChanged(_state.State);
            }
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCamera;
            if (_state != null) _state.OnStateChanged -= HandleStateChanged;
        }

        private void OnDestroy() => CoreUtils.Destroy(_material);

        // Savaş/yerleştirme arenası ayrı bir "masa"; minyatür bakışı yalnız dünya haritasında.
        private void HandleStateChanged(GameState s)
            => _stateAllows = s == GameState.Overworld || s == GameState.ConfirmMission;

        private void Update()
        {
            float target = _userEnabled && _stateAllows ? 1f : 0f;
            if (!_weightSnapped) { _weight = target; _weightSnapped = true; } // açılışta solma yok
            else _weight = Mathf.MoveTowards(_weight, target, _fadeSpeed * Time.unscaledDeltaTime);
        }

        private void HandleBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != _camera || _material == null || _weight <= 0.001f) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null || data.scriptableRenderer == null) return;

            PushParameters(cam);
            _pass.Downsample = _downsample;
            data.scriptableRenderer.EnqueuePass(_pass);
        }

        private void PushParameters(Camera cam)
        {
            Transform ct = cam.transform;
            Vector3 fwd  = ct.forward;

            // Odak derinliği: hedef varsa onun, yoksa ekran ortası ışınının y=0'a değdiği nokta.
            float focusDepth;
            if (_focusTarget != null && _focusTarget.gameObject.activeInHierarchy)
                focusDepth = Vector3.Dot(_focusTarget.position - ct.position, fwd);
            else
                focusDepth = fwd.y < -0.01f ? -ct.position.y / fwd.y : cam.farClipPlane * 0.5f;

            // Ekranda yukarı 1 birim zemin = derinlikte cot(eğim) birim. Yarı-yükseklik:
            // ortografikte orthographicSize, perspektifte odak uzaklığındaki görüş yarı-yüksekliği.
            float pitchSin = Mathf.Clamp(-fwd.y, 0.05f, 1f);
            float depthPerScreen = Mathf.Sqrt(1f - pitchSin * pitchSin) / pitchSin;
            float halfHeight = cam.orthographic
                ? cam.orthographicSize
                : focusDepth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float unit = Mathf.Max(0.01f, halfHeight * depthPerScreen);

            _material.SetVector(FocusId, new Vector4(focusDepth, _sharpBand * unit, _falloff * unit, _nearStrength));
            _material.SetVector(CameraId, new Vector4(cam.nearClipPlane, cam.farClipPlane, cam.orthographic ? 1f : 0f, 0f));
            _material.SetFloat(IntensityId, _intensity * _weight);

            int w = Mathf.Max(1, cam.pixelWidth  / _downsample);
            int h = Mathf.Max(1, cam.pixelHeight / _downsample);
            _material.SetVector(BlurTexelId, new Vector4(_blurSpread / w, _blurSpread / h, 0f, 0f));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Render Graph geçişi: H bulanık → V bulanık (küçük) → derinliğe göre birleştir
        // ─────────────────────────────────────────────────────────────────────
        private sealed class MiniaturePass : ScriptableRenderPass
        {
            private readonly Material _mat;
            public int Downsample = 3;

            private class CompositeData
            {
                public TextureHandle Source;
                public TextureHandle Blur;
                public Material      Material;
            }

            public MiniaturePass(Material mat)
            {
                _mat = mat;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camData   = frameData.Get<UniversalCameraData>();
                if (resources.isActiveTargetBackBuffer || !resources.cameraDepthTexture.IsValid()) return;

                TextureHandle source = resources.activeColorTexture;

                TextureDesc fullDesc = graph.GetTextureDesc(source);
                fullDesc.name        = "_MiniatureComposite";
                fullDesc.clearBuffer = false;
                fullDesc.msaaSamples = MSAASamples.None;

                TextureDesc smallDesc = fullDesc;
                smallDesc.name       = "_MiniatureBlur";
                smallDesc.sizeMode   = TextureSizeMode.Explicit;
                smallDesc.width      = Mathf.Max(1, camData.cameraTargetDescriptor.width  / Downsample);
                smallDesc.height     = Mathf.Max(1, camData.cameraTargetDescriptor.height / Downsample);
                smallDesc.filterMode = FilterMode.Bilinear;
                smallDesc.wrapMode   = TextureWrapMode.Clamp;

                TextureHandle blurA = graph.CreateTexture(smallDesc);
                TextureHandle blurB = graph.CreateTexture(smallDesc);
                TextureHandle dest  = graph.CreateTexture(fullDesc);

                graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, blurA, _mat, 0), "Miniature Blur H");
                graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(blurA, blurB, _mat, 1), "Miniature Blur V");

                using (var builder = graph.AddRasterRenderPass<CompositeData>("Miniature Composite", out var data))
                {
                    data.Source   = source;
                    data.Blur     = blurB;
                    data.Material = _mat;
                    builder.UseTexture(source);
                    builder.UseTexture(blurB);
                    builder.UseTexture(resources.cameraDepthTexture);
                    builder.SetRenderAttachment(dest, 0);
                    builder.SetRenderFunc((CompositeData d, RasterGraphContext ctx) =>
                    {
                        d.Material.SetTexture(BlurTexId, (RTHandle)d.Blur);
                        Blitter.BlitTexture(ctx.cmd, d.Source, new Vector4(1f, 1f, 0f, 0f), d.Material, 2);
                    });
                }

                resources.cameraColor = dest;
            }
        }
    }
}
