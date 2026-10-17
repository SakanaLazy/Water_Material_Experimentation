using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// Closed water meshes belong on the dedicated layer (30 by default). The nearest
// back face supplies exit depth; overlapping volumes require separate rendering.
public sealed class WaterVolumeBackDepthFeature : ScriptableRendererFeature
{
    public LayerMask volumeLayer = 1 << 30;
    [SerializeField] Shader backDepthShader;
    BackDepthPass pass;
    public override void Create()
    {
        pass?.Dispose();
        if (!backDepthShader) backDepthShader = Shader.Find("Hidden/Water/VolumeBackDepth");
        pass = new BackDepthPass(volumeLayer, backDepthShader) { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents };
    }
    protected override void Dispose(bool disposing) { pass?.Dispose(); }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (backDepthShader && renderingData.cameraData.cameraType != CameraType.Preview) { renderer.EnqueuePass(pass); }
    }
    sealed class BackDepthPass : ScriptableRenderPass
    {
        readonly LayerMask layer;
        readonly Shader shader;
        RTHandle target;
        readonly List<ShaderTagId> tags = new List<ShaderTagId> { new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("SRPDefaultUnlit") };
        sealed class Data { public RendererListHandle list; }
        public BackDepthPass(LayerMask layer, Shader shader) { this.layer = layer; this.shader = shader; }
        public void Dispose() { target?.Release(); target=null; }
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var camera = frameData.Get<UniversalCameraData>();
            var rendering = frameData.Get<UniversalRenderingData>();
            var light = frameData.Get<UniversalLightData>();
            var desc = camera.cameraTargetDescriptor;
            desc.msaaSamples = 1; desc.depthBufferBits = 0; desc.graphicsFormat = GraphicsFormat.R32_SFloat;
            desc.depthStencilFormat=GraphicsFormat.None;
            RenderingUtils.ReAllocateHandleIfNeeded(ref target,desc,FilterMode.Point,TextureWrapMode.Clamp,name:"Water volume exit depth");
            var color = graph.ImportTexture(target,new ImportResourceParams{clearOnFirstUse=true,clearColor=Color.clear,discardOnLastUse=false});
            desc.graphicsFormat = GraphicsFormat.None; desc.depthStencilFormat = GraphicsFormat.D32_SFloat;
            var depth = UniversalRenderer.CreateRenderGraphTexture(graph, desc, "Water volume exit Z", true, FilterMode.Point);
            using (var builder = graph.AddRasterRenderPass<Data>("Water volume — back-face thickness", out var data))
            {
                var drawing = RenderingUtils.CreateDrawingSettings(tags, rendering, camera, light, SortingCriteria.CommonOpaque);
                drawing.overrideShader = shader;
                drawing.overrideShaderPassIndex = 0;
                var filter = new FilteringSettings(RenderQueueRange.transparent, layer);
                data.list = graph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filter));
                builder.UseRendererList(data.list);
                builder.SetRenderAttachment(color, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
                builder.SetGlobalTextureAfterPass(color, Shader.PropertyToID("_WaterVolumeBackDepth"));
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((Data input, RasterGraphContext context) => context.cmd.DrawRendererList(input.list));
            }
        }
    }
}
