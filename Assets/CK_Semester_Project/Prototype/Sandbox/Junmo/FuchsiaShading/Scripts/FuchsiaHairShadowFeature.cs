using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// 테스트 씬의 머리 전용 깊이를 얼굴 셰이더에 제공한다. 일반 PBR 배경은 이 패스에 참여하지 않는다.
public sealed class FuchsiaHairShadowFeature : ScriptableRendererFeature
{
    [SerializeField, Tooltip("머리 그림자 패스를 실행할 씬 이름입니다.")]
    private string _sceneName = "FuchsiaShadowTest";

    private HairDepthPass _pass;

    public override void Create()
    {
        _pass = new HairDepthPass();
        RenderPipelineManager.beginCameraRendering -= ResetHairDepth;
        RenderPipelineManager.beginCameraRendering += ResetHairDepth;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        string sceneName = camera.cameraType == CameraType.SceneView
            ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            : camera.gameObject.scene.name;
        if (sceneName != _sceneName || camera.cameraType == CameraType.Preview)
        {
            return;
        }

        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        RenderPipelineManager.beginCameraRendering -= ResetHairDepth;
        Shader.SetGlobalVector(HairDepthPass.DepthParamsId, Vector4.zero);
    }

    private static void ResetHairDepth(ScriptableRenderContext context, Camera camera)
    {
        // 다른 카메라나 비활성 패스가 직전 프레임의 임시 텍스처를 읽지 않게 한다.
        Shader.SetGlobalVector(HairDepthPass.DepthParamsId, Vector4.zero);
    }

    private sealed class HairDepthPass : ScriptableRenderPass
    {
        internal static readonly int DepthParamsId = Shader.PropertyToID("_FuchsiaHairDepthParams");
        private static readonly int DepthTextureId = Shader.PropertyToID("_FuchsiaHairDepthTexture");
        private static readonly ShaderTagId HairDepthTag = new ShaderTagId("FuchsiaHairDepth");

        private sealed class PassData
        {
            internal RendererListHandle RendererList;
            internal Vector4 DepthParams;
        }

        internal HairDepthPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();

            // 카메라의 크기·배열 형식을 유지하고, 양의 시선 깊이를 담는 단일 채널로 바꾼다.
            TextureDesc colorDesc = resources.activeColorTexture.GetDescriptor(renderGraph);
            colorDesc.name = "Fuchsia Hair Linear Depth";
            colorDesc.colorFormat = GraphicsFormat.R32_SFloat;
            colorDesc.depthBufferBits = DepthBits.None;
            colorDesc.msaaSamples = MSAASamples.None;
            colorDesc.bindTextureMS = false;
            colorDesc.filterMode = FilterMode.Point;
            colorDesc.clearBuffer = true;
            colorDesc.clearColor = new Color(cameraData.camera.farClipPlane, 0f, 0f, 0f);
            TextureHandle hairDepth = renderGraph.CreateTexture(colorDesc);

            TextureDesc depthDesc = colorDesc;
            depthDesc.name = "Fuchsia Hair Z Buffer";
            depthDesc.colorFormat = GraphicsFormat.None;
            depthDesc.depthBufferBits = DepthBits.Depth32;
            TextureHandle zBuffer = renderGraph.CreateTexture(depthDesc);

            DrawingSettings drawing = RenderingUtils.CreateDrawingSettings(HairDepthTag,
                renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
            FilteringSettings filtering = new FilteringSettings(RenderQueueRange.opaque);
            RendererListParams listParams = new RendererListParams(renderingData.cullResults, drawing, filtering);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Fuchsia Hair Shadow Depth", out var passData))
            {
                passData.RendererList = renderGraph.CreateRendererList(listParams);
                passData.DepthParams = new Vector4(cameraData.camera.pixelWidth,
                    cameraData.camera.pixelHeight, cameraData.camera.farClipPlane, 1f);
                builder.UseRendererList(passData.RendererList);
                builder.SetRenderAttachment(hairDepth, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(zBuffer, AccessFlags.Write);
                // URP의 뒤따르는 DrawObjects 패스가 UseAllGlobalTextures로 이 입력을 읽는다.
                builder.SetGlobalTextureAfterPass(hairDepth, DepthTextureId);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalVector(DepthParamsId, data.DepthParams);
                    context.cmd.DrawRendererList(data.RendererList);
                });
            }
        }
    }
}
