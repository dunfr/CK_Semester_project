using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CK.SemesterProject.Rendering
{
    // 홀로그램 화면 전용 Bloom. 값은 Volume 프로필의 "Hologram Bloom" 항목에서 조절한다.
    //
    // URP의 Bloom은 화면 전체에서 밝은 픽셀을 한 가지 설정으로 번지게 해서, 조명과 홀로그램을 따로 조절할 수 없다.
    // 그래서 이 기능이 켜진 카메라에서는
    //   1. 홀로그램 셰이더가 본체를 상한(Body Brightness) 아래로 눌러 그려서 씬 Bloom(조명용)에 잡히지 않게 하고,
    //   2. 홀로그램이 내는 빛만 따로 모아(셰이더의 "HologramBloom" 패스, 앞을 가린 물체는 깊이로 걸러진다.
    //      선·글자는 임계값보다 밝은 부분만, 화면 전체가 광원으로서 내는 빛은 그대로 내보낸다)
    //   3. URP Bloom과 같은 방식(절반씩 줄이며 흐리기 → 다시 키우며 섞기)으로 번지게 한 뒤
    //   4. 포스트 프로세스가 끝난 화면 위에 얹는다. 씬 Bloom보다 뒤라서 두 Bloom이 서로의 결과를 다시 번지게 하지 않는다.
    //
    // "HologramBloom" 패스가 있는 셰이더로 그린 물체는 따로 등록하지 않아도 대상이 된다.
    // Hologram Bloom 항목이 없거나 Intensity가 0이면 아무것도 하지 않고, 홀로그램은 예전처럼 씬 Bloom을 따른다.
    public sealed class HologramBloomFeature : ScriptableRendererFeature
    {
        public const string ShaderName = "Hidden/Semester/Hologram Bloom";

        private const int MaxMips = 6;
        private const int PassPrefilter = 0;
        private const int PassBlurHorizontal = 1;
        private const int PassBlurVertical = 2;
        private const int PassUpsample = 3;
        private const int PassComposite = 4;

        // 셰이더 이름표는 ScriptableObject가 만들어지는 도중에는 만들 수 없어서(Unity 제약) 따로 둔다. 처음 그릴 때 만들어진다.
        private static class Ids
        {
            internal static readonly int BodyLimit = Shader.PropertyToID("_HologramBodyLimit");
            internal static readonly int FilterParams = Shader.PropertyToID("_HologramBloomFilter");
            internal static readonly int SourceParams = Shader.PropertyToID("_HologramBloomSource");
            internal static readonly int CompositeParams = Shader.PropertyToID("_HologramBloomComposite");
            internal static readonly int LowMip = Shader.PropertyToID("_HologramBloomLowMip");
            internal static readonly ShaderTagId SourceTag = new ShaderTagId("HologramBloom");
        }

        [SerializeField, Tooltip("번짐을 계산하는 셰이더입니다(Hidden/Semester/Hologram Bloom). 비어 있으면 이름으로 찾지만, 빌드에 포함되려면 여기에 들어 있어야 합니다.")]
        private Shader _shader;

        private Material _material;
        private SourcePass _sourcePass;
        private CompositePass _compositePass;

        public override void Create()
        {
            if (_shader == null)
            {
                _shader = Shader.Find(ShaderName);
            }

            _sourcePass = new SourcePass();
            _compositePass = new CompositePass();
            RenderPipelineManager.beginCameraRendering -= ResetBodyLimit;
            RenderPipelineManager.beginCameraRendering += ResetBodyLimit;
        }

        // 이 기능이 돌지 않는 카메라(바닥 반사, 미리보기, 포스트 프로세스를 끈 카메라)가 앞 카메라의 상한을 물려받지 않게 한다.
        // 상한이 0이면 홀로그램 셰이더는 본체를 누르지 않는다.
        private static void ResetBodyLimit(ScriptableRenderContext context, Camera camera)
        {
            Shader.SetGlobalFloat(Ids.BodyLimit, 0f);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            CameraData cameraData = renderingData.cameraData;
            CameraType cameraType = cameraData.camera.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
            {
                return;
            }

            // 포스트 프로세스 뒤에 얹는 효과라서 포스트 프로세스를 끈 카메라에서는 쓰지 않는다.
            if (!cameraData.postProcessEnabled || cameraData.renderType != CameraRenderType.Base)
            {
                return;
            }

            HologramBloom settings = VolumeManager.instance.stack.GetComponent<HologramBloom>();
            if (settings == null || !settings.IsActive() || !EnsureMaterial())
            {
                return;
            }

            // 불투명·투명 물체를 그리기 전에 정해져야 하는 값이라 패스 안이 아니라 여기서 넣는다.
            Shader.SetGlobalFloat(Ids.BodyLimit, settings.bodyBrightness.value);

            // URP Bloom과 같은 식으로 값을 바꾼다(임계값은 감마 → 리니어, 무릎은 임계값의 절반, 색은 밝기를 1로 맞춘다).
            // 임계값은 빛을 내보내는 셰이더("HologramBloom" 패스)가 적용하므로 전역 값으로 넘긴다.
            float threshold = Mathf.GammaToLinearSpace(settings.threshold.value);
            float scatter = Mathf.Lerp(.05f, .95f, settings.scatter.value);
            Shader.SetGlobalVector(Ids.SourceParams, new Vector4(threshold, threshold * .5f, 0f, 0f));
            _material.SetVector(Ids.FilterParams, new Vector4(scatter, 65472f, 0f, 0f));
            Color tint = settings.tint.value.linear;
            float luma = ColorUtils.Luminance(tint);
            tint = luma > 0f ? tint * (1f / luma) : Color.white;
            _material.SetVector(Ids.CompositeParams, new Vector4(settings.intensity.value, tint.r, tint.g, tint.b));

            _sourcePass.Setup(_material);
            _compositePass.Setup(_material);
            renderer.EnqueuePass(_sourcePass);
            renderer.EnqueuePass(_compositePass);
        }

        private bool EnsureMaterial()
        {
            if (_material != null)
            {
                return true;
            }

            if (_shader == null)
            {
                _shader = Shader.Find(ShaderName);
            }

            if (_shader == null)
            {
                return false;
            }

            _material = CoreUtils.CreateEngineMaterial(_shader);
            return _material != null;
        }

        protected override void Dispose(bool disposing)
        {
            RenderPipelineManager.beginCameraRendering -= ResetBodyLimit;
            Shader.SetGlobalFloat(Ids.BodyLimit, 0f);
            CoreUtils.Destroy(_material);
            _material = null;
        }

        // 같은 카메라의 두 패스(빛 모으기 → 얹기)가 번진 결과를 주고받는 자리.
        private sealed class FrameTextures : ContextItem
        {
            internal TextureHandle Bloom;

            public override void Reset()
            {
                Bloom = TextureHandle.nullHandle;
            }
        }

        // 홀로그램이 내는 빛만 따로 그리고, 번진 텍스처까지 만든다.
        private sealed class SourcePass : ScriptableRenderPass
        {
            private readonly TextureHandle[] _down = new TextureHandle[MaxMips];
            private readonly TextureHandle[] _up = new TextureHandle[MaxMips];
            private Material _material;

            private sealed class SourceData
            {
                internal RendererListHandle Renderers;
            }

            private sealed class PyramidData
            {
                internal Material Material;
                internal TextureHandle Source;
                internal TextureHandle[] Down;
                internal TextureHandle[] Up;
                internal int MipCount;
            }

            internal SourcePass()
            {
                // 불투명 물체의 깊이가 다 쌓인 뒤라야 캐릭터나 벽에 가린 부분을 뺄 수 있다.
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            }

            internal void Setup(Material material)
            {
                _material = material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                FrameTextures frame = frameData.GetOrCreate<FrameTextures>();
                frame.Bloom = TextureHandle.nullHandle;

                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (_material == null || resources.isActiveTargetBackBuffer)
                {
                    return;
                }

                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                TextureDesc cameraDesc = renderGraph.GetTextureDesc(resources.activeColorTexture);

                // 1을 넘는 색을 그대로 담아야 하므로 HDR 포맷을 쓴다.
                GraphicsFormat format = SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, GraphicsFormatUsage.Blend)
                    ? GraphicsFormat.B10G11R11_UFloatPack32
                    : GraphicsFormat.R16G16B16A16_SFloat;

                // 깊이 버퍼를 함께 쓰려면 카메라 색 버퍼와 크기·샘플 수가 같아야 한다.
                var sourceDesc = new TextureDesc(cameraDesc.width, cameraDesc.height)
                {
                    name = "Hologram Bloom Source",
                    colorFormat = format,
                    msaaSamples = cameraDesc.msaaSamples,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    clearBuffer = true,
                    clearColor = Color.clear,
                };
                TextureHandle source = renderGraph.CreateTexture(sourceDesc);

                using (var builder = renderGraph.AddRasterRenderPass<SourceData>("Hologram Bloom Source", out var data))
                {
                    DrawingSettings drawing = RenderingUtils.CreateDrawingSettings(Ids.SourceTag, renderingData, cameraData, lightData,
                        SortingCriteria.CommonTransparent);
                    var filtering = new FilteringSettings(RenderQueueRange.all);
                    data.Renderers = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing, filtering));

                    builder.UseRendererList(data.Renderers);
                    builder.SetRenderAttachment(source, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (SourceData d, RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(d.Renderers);
                    });
                }

                // 절반 해상도에서 시작해 절반씩 줄여 나간다(URP Bloom과 같은 단계 수).
                int width = Mathf.Max(1, cameraDesc.width >> 1);
                int height = Mathf.Max(1, cameraDesc.height >> 1);
                int mipCount = Mathf.Clamp(Mathf.FloorToInt(Mathf.Log(Mathf.Max(width, height), 2f) - 1f), 1, MaxMips);
                var mipDesc = new TextureDesc(width, height)
                {
                    colorFormat = format,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                for (int i = 0; i < mipCount; i++)
                {
                    mipDesc.width = width;
                    mipDesc.height = height;
                    mipDesc.name = "Hologram Bloom Down " + i;
                    _down[i] = renderGraph.CreateTexture(mipDesc);
                    mipDesc.name = "Hologram Bloom Up " + i;
                    _up[i] = renderGraph.CreateTexture(mipDesc);
                    width = Mathf.Max(1, width >> 1);
                    height = Mathf.Max(1, height >> 1);
                }

                using (var builder = renderGraph.AddUnsafePass<PyramidData>("Hologram Bloom Blur", out var data))
                {
                    data.Material = _material;
                    data.Source = source;
                    data.Down = _down;
                    data.Up = _up;
                    data.MipCount = mipCount;

                    builder.UseTexture(source, AccessFlags.Read);
                    for (int i = 0; i < mipCount; i++)
                    {
                        builder.UseTexture(_down[i], AccessFlags.ReadWrite);
                        builder.UseTexture(_up[i], AccessFlags.ReadWrite);
                    }

                    builder.AllowPassCulling(false);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (PyramidData d, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        const RenderBufferLoadAction load = RenderBufferLoadAction.DontCare;
                        const RenderBufferStoreAction store = RenderBufferStoreAction.Store;

                        // 절반 해상도로 옮긴다(임계값은 빛을 내보낸 쪽에서 이미 적용했다).
                        Blitter.BlitCameraTexture(cmd, d.Source, d.Down[0], load, store, d.Material, PassPrefilter);

                        // 줄이면서 가로·세로로 흐린다(Up[i]는 가로 흐림의 임시 자리).
                        TextureHandle last = d.Down[0];
                        for (int i = 1; i < d.MipCount; i++)
                        {
                            Blitter.BlitCameraTexture(cmd, last, d.Up[i], load, store, d.Material, PassBlurHorizontal);
                            Blitter.BlitCameraTexture(cmd, d.Up[i], d.Down[i], load, store, d.Material, PassBlurVertical);
                            last = d.Down[i];
                        }

                        // 작은 단계부터 다시 키우며 섞는다. 섞는 비율(Scatter)이 번짐의 넓이를 정한다.
                        for (int i = d.MipCount - 2; i >= 0; i--)
                        {
                            TextureHandle low = i == d.MipCount - 2 ? d.Down[i + 1] : d.Up[i + 1];
                            cmd.SetGlobalTexture(Ids.LowMip, (RTHandle)low);
                            Blitter.BlitCameraTexture(cmd, d.Down[i], d.Up[i], load, store, d.Material, PassUpsample);
                        }
                    });
                }

                frame.Bloom = mipCount == 1 ? _down[0] : _up[0];
            }
        }

        // 번진 결과를 포스트 프로세스가 끝난 화면 위에 얹는다.
        private sealed class CompositePass : ScriptableRenderPass
        {
            private Material _material;

            private sealed class CompositeData
            {
                internal Material Material;
                internal TextureHandle Bloom;
            }

            internal CompositePass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            }

            internal void Setup(Material material)
            {
                _material = material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null || !frameData.Contains<FrameTextures>())
                {
                    return;
                }

                FrameTextures frame = frameData.Get<FrameTextures>();
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (!frame.Bloom.IsValid() || resources.isActiveTargetBackBuffer)
                {
                    return;
                }

                using (var builder = renderGraph.AddRasterRenderPass<CompositeData>("Hologram Bloom Composite", out var data))
                {
                    data.Material = _material;
                    data.Bloom = frame.Bloom;

                    builder.UseTexture(frame.Bloom, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (CompositeData d, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, d.Bloom, new Vector4(1f, 1f, 0f, 0f), d.Material, PassComposite);
                    });
                }
            }
        }
    }
}
