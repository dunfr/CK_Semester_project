using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// 캐릭터 전용 패스만 그리므로 일반 PBR 배경의 외곽에는 선이 생기지 않는다.
public sealed class FuchsiaOutlineFeature : ScriptableRendererFeature
{
    [SerializeField, Tooltip("캐릭터 외곽선 패스를 실행할 테스트 씬 이름입니다.")]
    private string _sceneName = "FuchsiaShadowTest";

    private OutlinePass _pass;

    public override void Create()
    {
        _pass = new OutlinePass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        string sceneName = camera.cameraType == CameraType.SceneView
            ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            : camera.gameObject.scene.name;
        if (_pass == null || sceneName != _sceneName || camera.cameraType == CameraType.Preview)
        {
            return;
        }

        renderer.EnqueuePass(_pass);
    }

    private sealed class OutlinePass : ScriptableRenderPass
    {
        private static readonly ShaderTagId OutlineTag = new ShaderTagId("FuchsiaOutline");

        private sealed class PassData
        {
            internal RendererListHandle RendererList;
        }

        internal OutlinePass()
        {
            // 완성된 불투명 깊이가 몸 안쪽과 배경 뒤의 팽창 셸을 가려 준다.
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();

            DrawingSettings drawing = RenderingUtils.CreateDrawingSettings(OutlineTag,
                renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
            FilteringSettings filtering = new FilteringSettings(RenderQueueRange.opaque);
            RendererListParams listParams = new RendererListParams(renderingData.cullResults, drawing, filtering);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Fuchsia Character Outline", out var passData))
            {
                passData.RendererList = renderGraph.CreateRendererList(listParams);
                builder.UseRendererList(passData.RendererList);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(data.RendererList);
                });
            }
        }
    }
}
