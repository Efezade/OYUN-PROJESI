using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>Height-aware world mist. One camera pass veils every model/material together;
    /// geometry stays resident and the exploration field changes continuously.</summary>
    [DisallowMultipleComponent]
    public sealed class WorldFogEffect : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private FogOfWarManager _fog;
        [SerializeField] private Shader _shader;
        [SerializeField, Min(1)] private float _height = 12;
        [SerializeField, Min(.01f)] private float _density = .24f;
        [SerializeField] private Color _dayMist = new Color(.59f, .67f, .69f);
        [SerializeField] private Color _nightMist = new Color(.38f, .48f, .61f);
        [SerializeField, Min(.1f)] private float _transitionSeconds = 2.4f;
        private Material _material;
        private UniversalAdditionalCameraData _cameraData;
        private MistPass _pass;
        private float _nightWeight;
        public bool Ready => _material != null && _pass != null;

        private void Awake()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera != null) _cameraData = _camera.GetUniversalAdditionalCameraData();
            if (_shader == null || !_shader.isSupported || _fog == null) { enabled = false; return; }
            _material = CoreUtils.CreateEngineMaterial(_shader);
            _pass = new MistPass(_material);
            _fog.SetVolumeRendering(true);
        }
        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += Render;
            if (Ready) _fog.SetVolumeRendering(true);
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Render;
            if (_fog != null) _fog.SetVolumeRendering(false);
        }
        private void OnDestroy() => CoreUtils.Destroy(_material);
        private void Update()
        {
            if (_fog != null) _nightWeight = Mathf.MoveTowards(_nightWeight, _fog.IsNightMode ? 1 : 0,
                Time.deltaTime / Mathf.Max(.1f, _transitionSeconds));
        }
        private void Render(ScriptableRenderContext context, Camera camera)
        {
            if (camera != _camera || !Ready || _fog.FogField == null || !_fog.FogEnabled ||
                _cameraData == null || _cameraData.scriptableRenderer == null) return;
            _material.SetTexture("_WorldFogField", _fog.FogField);
            _material.SetVector("_WorldFogBounds", _fog.FogFieldBounds);
            _material.SetVector("_WorldFogSettings", new Vector4(_fog.FieldHexSize, _height, _density, HexMetrics.TileHeight));
            var tint = Color.Lerp(_dayMist, _nightMist, _nightWeight);
            _material.SetColor("_WorldFogTint", QualitySettings.activeColorSpace == ColorSpace.Linear ? tint.linear : tint);
            _cameraData.scriptableRenderer.EnqueuePass(_pass);
        }

        private sealed class MistPass : ScriptableRenderPass
        {
            private readonly Material _material;
            private class PassData { public TextureHandle Source; public Material Material; }
            public MistPass(Material material)
            {
                _material = material;
                // After transparent world effects, before miniature blur and colour grading.
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing - 2;
                ConfigureInput(ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || !resources.cameraDepthTexture.IsValid()) return;
                var source = resources.activeColorTexture;
                var desc = graph.GetTextureDesc(source);
                desc.name = "World fog composite"; desc.clearBuffer = false; desc.msaaSamples = MSAASamples.None;
                var destination = graph.CreateTexture(desc);
                using (var builder = graph.AddRasterRenderPass<PassData>("Continuous height fog", out var data))
                {
                    data.Source = source; data.Material = _material;
                    builder.UseTexture(source); builder.UseTexture(resources.cameraDepthTexture);
                    builder.SetRenderAttachment(destination, 0);
                    builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                        Blitter.BlitTexture(ctx.cmd, d.Source, new Vector4(1, 1, 0, 0), d.Material, 0));
                }
                resources.cameraColor = destination;
            }
        }
    }
}
