using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CK.SemesterProject.Rendering
{
    // 조명 종류별 Bloom. 값은 종류마다 따로 있는 Volume 항목(Corridor Ceiling / Living Room Ceiling / Pillar Lamp Bloom)에서 조절한다.
    //
    // URP의 Bloom은 화면 전체에서 밝은 픽셀을 한 가지 설정으로 번지게 해서, 조명마다 따로 조절할 수 없다.
    // 그래서 전용 Bloom이 켜진 종류의 램프는 (홀로그램 전용 Bloom과 같은 방식으로)
    //   1. 램프 셰이더가 면을 상한(Body Brightness) 아래로 눌러 그려서 씬 Bloom에 잡히지 않게 하고,
    //   2. 램프가 내는 빛만 따로 모아(셰이더의 "LampBloom" 패스, 앞을 가린 물체는 깊이로 걸러진다.
    //      Threshold·Intensity·Tint는 이때 종류별로 적용된다)
    //   3. URP Bloom과 같은 방식으로 번지게 한 뒤
    //   4. 포스트 프로세스가 끝난 화면 위에 얹는다.
    //
    // 번지는 넓이(Scatter)는 흐리는 단계에서 정해지므로, Scatter가 같은 종류끼리는 한 번에 흐리고 다른 종류만 따로 흐린다.
    // 얹는 식이 덧셈과 같아서(1 - exp(-합)) 묶어서 흐려도, 따로 흐려도 결과는 같다.
    //
    // "LampBloom" 패스가 있는 셰이더(Semester/Environment/Lamp Emissive)로 그린 물체는 따로 등록하지 않아도 대상이 된다.
    // 어떤 종류의 항목이 없거나 Intensity가 0이면 그 종류는 건드리지 않고, 예전처럼 씬 Bloom을 따른다.
    public sealed class LampBloomFeature : ScriptableRendererFeature
    {
        // 흐림·얹기 계산은 홀로그램 전용 Bloom의 셰이더를 같이 쓴다(식이 같다).
        public const string ShaderName = "Hidden/Semester/Hologram Bloom";

        // 조명 종류의 수. 번호는 램프 머티리얼의 Lamp Bloom Volume 항목과 같다(0 복도 천장, 1 거실 천장, 2 기둥).
        private const int GroupCount = 3;
        private const int MaxMips = 6;
        private const int PassPrefilter = 0;
        private const int PassBlurHorizontal = 1;
        private const int PassBlurVertical = 2;
        private const int PassUpsample = 3;
        private const int PassComposite = 4;

        // 셰이더 이름표는 ScriptableObject가 만들어지는 도중에는 만들 수 없어서(Unity 제약) 따로 둔다. 처음 그릴 때 만들어진다.
        private static class Ids
        {
            internal static readonly int BodyLimits = Shader.PropertyToID("_LampBloomBodyLimits");
            internal static readonly int Thresholds = Shader.PropertyToID("_LampBloomThresholds");
            internal static readonly int Intensities = Shader.PropertyToID("_LampBloomIntensities");
            internal static readonly int[] Tints =
            {
                Shader.PropertyToID("_LampBloomTint0"),
                Shader.PropertyToID("_LampBloomTint1"),
                Shader.PropertyToID("_LampBloomTint2"),
            };
            internal static readonly int Bucket = Shader.PropertyToID("_LampBloomBucket");
            internal static readonly int FilterParams = Shader.PropertyToID("_HologramBloomFilter");
            internal static readonly int CompositeParams = Shader.PropertyToID("_HologramBloomComposite");
            internal static readonly int LowMip = Shader.PropertyToID("_HologramBloomLowMip");
            internal static readonly ShaderTagId SourceTag = new ShaderTagId("LampBloom");
        }

        [SerializeField, Tooltip("번짐을 계산하는 셰이더입니다(Hidden/Semester/Hologram Bloom, 홀로그램 전용 Bloom과 같이 씁니다). 비어 있으면 이름으로 찾지만, 빌드에 포함되려면 여기에 들어 있어야 합니다.")]
        private Shader _shader;

        // 묶음(Scatter가 같은 종류들)마다 흐림 설정이 달라서 머티리얼도 묶음마다 하나씩 둔다.
        private readonly Material[] _materials = new Material[GroupCount];
        private readonly Vector4[] _bucketMasks = new Vector4[GroupCount];
        private readonly float[] _bucketScatters = new float[GroupCount];
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
            RenderPipelineManager.beginCameraRendering -= ResetBodyLimits;
            RenderPipelineManager.beginCameraRendering += ResetBodyLimits;
        }

        // 이 기능이 돌지 않는 카메라(바닥 반사, 미리보기, 포스트 프로세스를 끈 카메라)가 앞 카메라의 상한을 물려받지 않게 한다.
        // 상한이 0이면 램프 셰이더는 면을 누르지 않는다.
        private static void ResetBodyLimits(ScriptableRenderContext context, Camera camera)
        {
            Shader.SetGlobalVector(Ids.BodyLimits, Vector4.zero);
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

            VolumeStack stack = VolumeManager.instance.stack;
            Vector4 bodyLimits = Vector4.zero;
            Vector4 thresholds = Vector4.one;
            Vector4 intensities = Vector4.zero;
            int bucketCount = 0;
            for (int group = 0; group < GroupCount; group++)
            {
                LampBloomSettings settings = GetSettings(stack, group);
                if (settings == null || !settings.IsActive())
                {
                    continue;
                }

                // URP Bloom과 같은 식으로 값을 바꾼다(임계값은 감마 → 리니어, 색은 밝기를 1로 맞춘다).
                bodyLimits[group] = settings.bodyBrightness.value;
                thresholds[group] = Mathf.GammaToLinearSpace(settings.threshold.value);
                intensities[group] = settings.intensity.value;
                Color tint = settings.tint.value.linear;
                float luma = ColorUtils.Luminance(tint);
                tint = luma > 0f ? tint * (1f / luma) : Color.white;
                Shader.SetGlobalVector(Ids.Tints[group], new Vector4(tint.r, tint.g, tint.b, 1f));

                // Scatter가 같은 종류는 같은 묶음에 넣어 한 번에 흐린다.
                float scatter = Mathf.Lerp(.05f, .95f, settings.scatter.value);
                int bucket = 0;
                while (bucket < bucketCount && !Mathf.Approximately(_bucketScatters[bucket], scatter))
                {
                    bucket++;
                }

                if (bucket == bucketCount)
                {
                    _bucketScatters[bucket] = scatter;
                    _bucketMasks[bucket] = Vector4.zero;
                    bucketCount++;
                }

                _bucketMasks[bucket][group] = 1f;
            }

            if (bucketCount == 0 || !EnsureMaterials(bucketCount))
            {
                return;
            }

            // 불투명 물체를 그리기 전에 정해져야 하는 값이라 패스 안이 아니라 여기서 넣는다.
            Shader.SetGlobalVector(Ids.BodyLimits, bodyLimits);
            Shader.SetGlobalVector(Ids.Thresholds, thresholds);
            Shader.SetGlobalVector(Ids.Intensities, intensities);
            for (int bucket = 0; bucket < bucketCount; bucket++)
            {
                _materials[bucket].SetVector(Ids.FilterParams, new Vector4(_bucketScatters[bucket], 65472f, 0f, 0f));
                // 세기와 색은 빛을 모을 때 이미 곱했다.
                _materials[bucket].SetVector(Ids.CompositeParams, Vector4.one);
            }

            _sourcePass.Setup(_materials, _bucketMasks, bucketCount);
            _compositePass.Setup(_materials);
            renderer.EnqueuePass(_sourcePass);
            renderer.EnqueuePass(_compositePass);
        }

        private static LampBloomSettings GetSettings(VolumeStack stack, int group)
        {
            switch (group)
            {
                case 0: return stack.GetComponent<CorridorCeilingLampBloom>();
                case 1: return stack.GetComponent<LivingRoomCeilingLampBloom>();
                default: return stack.GetComponent<PillarLampBloom>();
            }
        }

        private bool EnsureMaterials(int count)
        {
            if (_shader == null)
            {
                _shader = Shader.Find(ShaderName);
            }

            if (_shader == null)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                if (_materials[i] == null)
                {
                    _materials[i] = CoreUtils.CreateEngineMaterial(_shader);
                }

                if (_materials[i] == null)
                {
                    return false;
                }
            }

            return true;
        }

        protected override void Dispose(bool disposing)
        {
            RenderPipelineManager.beginCameraRendering -= ResetBodyLimits;
            Shader.SetGlobalVector(Ids.BodyLimits, Vector4.zero);
            for (int i = 0; i < _materials.Length; i++)
            {
                CoreUtils.Destroy(_materials[i]);
                _materials[i] = null;
            }
        }

        // 같은 카메라의 두 패스(빛 모으기 → 얹기)가 번진 결과를 주고받는 자리. 묶음마다 하나씩이다.
        private sealed class FrameTextures : ContextItem
        {
            internal readonly TextureHandle[] Bloom = new TextureHandle[GroupCount];
            internal int Count;

            public override void Reset()
            {
                Count = 0;
                for (int i = 0; i < Bloom.Length; i++)
                {
                    Bloom[i] = TextureHandle.nullHandle;
                }
            }
        }

        // 묶음마다 그 묶음에 속한 램프의 빛만 따로 그리고, 번진 텍스처까지 만든다.
        private sealed class SourcePass : ScriptableRenderPass
        {
            private readonly TextureHandle[][] _down = new TextureHandle[GroupCount][];
            private readonly TextureHandle[][] _up = new TextureHandle[GroupCount][];
            private Material[] _materials;
            private Vector4[] _bucketMasks;
            private int _bucketCount;

            private sealed class SourceData
            {
                internal RendererListHandle Renderers;
                internal Vector4 BucketMask;
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
                for (int i = 0; i < GroupCount; i++)
                {
                    _down[i] = new TextureHandle[MaxMips];
                    _up[i] = new TextureHandle[MaxMips];
                }
            }

            internal void Setup(Material[] materials, Vector4[] bucketMasks, int bucketCount)
            {
                _materials = materials;
                _bucketMasks = bucketMasks;
                _bucketCount = bucketCount;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                FrameTextures frame = frameData.GetOrCreate<FrameTextures>();
                frame.Reset();

                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (_materials == null || resources.isActiveTargetBackBuffer)
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

                // 절반 해상도에서 시작해 절반씩 줄여 나간다(URP Bloom과 같은 단계 수).
                int halfWidth = Mathf.Max(1, cameraDesc.width >> 1);
                int halfHeight = Mathf.Max(1, cameraDesc.height >> 1);
                int mipCount = Mathf.Clamp(Mathf.FloorToInt(Mathf.Log(Mathf.Max(halfWidth, halfHeight), 2f) - 1f), 1, MaxMips);

                for (int bucket = 0; bucket < _bucketCount; bucket++)
                {
                    Material material = _materials[bucket];
                    if (material == null)
                    {
                        continue;
                    }

                    // 깊이 버퍼를 함께 쓰려면 카메라 색 버퍼와 크기·샘플 수가 같아야 한다.
                    var sourceDesc = new TextureDesc(cameraDesc.width, cameraDesc.height)
                    {
                        name = "Lamp Bloom Source " + bucket,
                        colorFormat = format,
                        msaaSamples = cameraDesc.msaaSamples,
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp,
                        clearBuffer = true,
                        clearColor = Color.clear,
                    };
                    TextureHandle source = renderGraph.CreateTexture(sourceDesc);

                    using (var builder = renderGraph.AddRasterRenderPass<SourceData>("Lamp Bloom Source", out var data))
                    {
                        DrawingSettings drawing = RenderingUtils.CreateDrawingSettings(Ids.SourceTag, renderingData, cameraData, lightData,
                            SortingCriteria.CommonOpaque);
                        var filtering = new FilteringSettings(RenderQueueRange.all);
                        data.Renderers = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing, filtering));
                        data.BucketMask = _bucketMasks[bucket];

                        builder.UseRendererList(data.Renderers);
                        builder.SetRenderAttachment(source, 0, AccessFlags.Write);
                        builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                        builder.AllowPassCulling(false);
                        builder.AllowGlobalStateModification(true);
                        builder.SetRenderFunc(static (SourceData d, RasterGraphContext context) =>
                        {
                            // 램프 셰이더가 이 묶음에 속한 종류만 남기고 나머지는 버린다.
                            context.cmd.SetGlobalVector(Ids.Bucket, d.BucketMask);
                            context.cmd.DrawRendererList(d.Renderers);
                        });
                    }

                    TextureHandle[] down = _down[bucket];
                    TextureHandle[] up = _up[bucket];
                    int width = halfWidth;
                    int height = halfHeight;
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
                        mipDesc.name = "Lamp Bloom Down " + bucket + "-" + i;
                        down[i] = renderGraph.CreateTexture(mipDesc);
                        mipDesc.name = "Lamp Bloom Up " + bucket + "-" + i;
                        up[i] = renderGraph.CreateTexture(mipDesc);
                        width = Mathf.Max(1, width >> 1);
                        height = Mathf.Max(1, height >> 1);
                    }

                    using (var builder = renderGraph.AddUnsafePass<PyramidData>("Lamp Bloom Blur", out var data))
                    {
                        data.Material = material;
                        data.Source = source;
                        data.Down = down;
                        data.Up = up;
                        data.MipCount = mipCount;

                        builder.UseTexture(source, AccessFlags.Read);
                        for (int i = 0; i < mipCount; i++)
                        {
                            builder.UseTexture(down[i], AccessFlags.ReadWrite);
                            builder.UseTexture(up[i], AccessFlags.ReadWrite);
                        }

                        builder.AllowPassCulling(false);
                        builder.AllowGlobalStateModification(true);
                        builder.SetRenderFunc(static (PyramidData d, UnsafeGraphContext context) =>
                        {
                            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                            const RenderBufferLoadAction load = RenderBufferLoadAction.DontCare;
                            const RenderBufferStoreAction store = RenderBufferStoreAction.Store;

                            // 절반 해상도로 옮긴다(임계값·세기·색은 빛을 내보낸 쪽에서 이미 적용했다).
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

                    frame.Bloom[frame.Count] = mipCount == 1 ? down[0] : up[0];
                    frame.Count++;
                }
            }
        }

        // 번진 결과를 포스트 프로세스가 끝난 화면 위에 얹는다.
        private sealed class CompositePass : ScriptableRenderPass
        {
            private Material[] _materials;

            private sealed class CompositeData
            {
                internal Material Material;
                internal TextureHandle Bloom;
            }

            internal CompositePass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            }

            internal void Setup(Material[] materials)
            {
                _materials = materials;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_materials == null || !frameData.Contains<FrameTextures>())
                {
                    return;
                }

                FrameTextures frame = frameData.Get<FrameTextures>();
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer)
                {
                    return;
                }

                for (int i = 0; i < frame.Count; i++)
                {
                    // 얹을 때는 세기·색이 모두 1이라 어느 묶음의 머티리얼을 써도 같다.
                    Material material = _materials[0];
                    if (material == null || !frame.Bloom[i].IsValid())
                    {
                        continue;
                    }

                    using (var builder = renderGraph.AddRasterRenderPass<CompositeData>("Lamp Bloom Composite", out var data))
                    {
                        data.Material = material;
                        data.Bloom = frame.Bloom[i];

                        builder.UseTexture(frame.Bloom[i], AccessFlags.Read);
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
}
