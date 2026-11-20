using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CK.SemesterProject.Rendering
{
    // 씬 전체 Bloom을 배경과 동적 객체(캐릭터·몬스터)로 나눈다. 값은 Volume 항목 Background Bloom / Dynamic Object Bloom에서 조절한다.
    //
    // URP의 Bloom은 화면 전체에서 밝은 픽셀을 한 가지 설정으로 번지게 해서, 캐릭터와 배경을 따로 조절할 수 없다.
    // 그래서 두 항목 중 하나라도 켜져 있으면(Intensity > 0)
    //   1. URP 기본 Bloom은 이 카메라에서 끄고,
    //   2. 동적 객체가 보이는 픽셀만 1로 칠한 마스크를 만들고(카메라 깊이로 가려진 부분은 빠진다),
    //   3. 화면을 마스크로 둘로 나눠 각자의 Threshold로 밝은 부분을 고른 뒤 각자의 Scatter로 번지게 하고,
    //   4. 포스트 프로세스(톤매핑) 직전에 각자의 Intensity·Tint로 화면에 더한다.
    // 고르기·흐림·더하기가 URP Bloom과 같은 식이라, 두 항목을 같은 값으로 두면 URP Bloom 하나와 같은 그림이 된다.
    //
    // 동적 객체는 따로 등록하지 않는다. 캐릭터 셰이더(Semester/Fuchsia/...)에만 있는 패스로 가려내고,
    // 그 밖의 물체를 동적 객체로 다루고 싶으면 그 물체를 Extra Dynamic Layers의 레이어에 넣는다.
    // High Quality Filtering, Clamp 같은 세부 항목은 URP 기본 Bloom 항목의 값을 그대로 따른다.
    // 조명·홀로그램 전용 Bloom은 이 기능과 별개로 그대로 동작한다.
    public sealed class SceneBloomFeature : ScriptableRendererFeature
    {
        public const string ShaderName = "Hidden/Semester/Scene Bloom";

        // 0 = 배경, 1 = 동적 객체.
        private const int GroupCount = 2;
        private const int MaxMips = 6;
        private const int PassMask = 0;
        private const int PassPrefilter = 1;
        private const int PassBlurHorizontal = 2;
        private const int PassBlurVertical = 3;
        private const int PassUpsample = 4;
        private const int PassComposite = 5;
        private const string HighQualityKeyword = "_SCENE_BLOOM_HQ";

        // 셰이더 이름표는 ScriptableObject가 만들어지는 도중에는 만들 수 없어서(Unity 제약) 따로 둔다. 처음 그릴 때 만들어진다.
        private static class Ids
        {
            internal static readonly int Mask = Shader.PropertyToID("_SceneBloomMask");
            internal static readonly int LowMip = Shader.PropertyToID("_SceneBloomLowMip");
            internal static readonly int LowMipSize = Shader.PropertyToID("_SceneBloomLowMipSize");
            internal static readonly int Filter = Shader.PropertyToID("_SceneBloomFilter");
            internal static readonly int Group = Shader.PropertyToID("_SceneBloomGroup");
            internal static readonly int Composite = Shader.PropertyToID("_SceneBloomComposite");
            // 캐릭터 셰이더에만 있는 패스. 이 패스가 있는 셰이더로 그린 물체가 동적 객체다.
            internal static readonly ShaderTagId CharacterTag = new ShaderTagId("FuchsiaCharacterShadow");
            internal static readonly ShaderTagId[] CommonTags =
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit"),
            };
        }

        [SerializeField, Tooltip("번짐을 계산하는 셰이더입니다(Hidden/Semester/Scene Bloom). 비어 있으면 이름으로 찾지만, 빌드에 포함되려면 여기에 들어 있어야 합니다.")]
        private Shader _shader;

        [SerializeField, Tooltip("캐릭터 셰이더를 쓰지 않는 물체 중 동적 객체로 다룰 레이어입니다. 비워 두면 캐릭터·몬스터만 동적 객체입니다.")]
        private LayerMask _extraDynamicLayers = 0;

        private readonly Material[] _materials = new Material[GroupCount];
        private readonly bool[] _active = new bool[GroupCount];
        private SceneBloomPass _pass;

        public override void Create()
        {
            if (_shader == null)
            {
                _shader = Shader.Find(ShaderName);
            }

            _pass = new SceneBloomPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            CameraData cameraData = renderingData.cameraData;
            CameraType cameraType = cameraData.camera.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
            {
                return;
            }

            // URP Bloom을 대신하는 효과라서, 포스트 프로세스를 끈 카메라에서는 쓰지 않는다.
            if (!cameraData.postProcessEnabled || cameraData.renderType != CameraRenderType.Base)
            {
                return;
            }

            VolumeStack stack = VolumeManager.instance.stack;
            SceneBloomSettings background = stack.GetComponent<BackgroundBloom>();
            SceneBloomSettings dynamic = stack.GetComponent<DynamicObjectBloom>();
            _active[0] = background != null && background.IsActive();
            _active[1] = dynamic != null && dynamic.IsActive();
            if ((!_active[0] && !_active[1]) || !EnsureMaterials())
            {
                return;
            }

            // 세부 항목은 URP 기본 Bloom의 값을 따른다. 그 뒤 기본 Bloom은 이 카메라에서 끈다(같은 빛이 두 번 번지지 않게).
            // Volume 값은 카메라마다 다시 계산되므로, 이 기능이 돌지 않는 카메라의 기본 Bloom은 그대로다.
            Bloom builtin = stack.GetComponent<Bloom>();
            bool highQuality = builtin != null && builtin.highQualityFiltering.value;
            float clampMax = builtin != null ? builtin.clamp.value : 65472f;
            int maxMips = builtin != null ? Mathf.Clamp(builtin.maxIterations.value, 2, MaxMips) : MaxMips;
            if (builtin != null)
            {
                builtin.intensity.value = 0f;
            }

            SetupMaterial(_materials[0], background, 0f, highQuality, clampMax);
            SetupMaterial(_materials[1], dynamic, 1f, highQuality, clampMax);
            _pass.Setup(_materials, _active, maxMips, _extraDynamicLayers);
            renderer.EnqueuePass(_pass);
        }

        // URP Bloom과 같은 식으로 값을 바꾼다(임계값은 감마 → 리니어, 무릎은 임계값의 절반, 색은 밝기를 1로 맞춘다).
        private static void SetupMaterial(Material material, SceneBloomSettings settings, float group, bool highQuality, float clampMax)
        {
            float threshold = Mathf.GammaToLinearSpace(settings.threshold.value);
            float scatter = Mathf.Lerp(.05f, .95f, settings.scatter.value);
            material.SetVector(Ids.Filter, new Vector4(scatter, clampMax, threshold, threshold * .5f));
            material.SetVector(Ids.Group, new Vector4(group, 0f, 0f, 0f));

            Color tint = settings.tint.value.linear;
            float luma = ColorUtils.Luminance(tint);
            tint = luma > 0f ? tint * (1f / luma) : Color.white;
            float intensity = settings.intensity.value;
            material.SetVector(Ids.Composite, new Vector4(tint.r * intensity, tint.g * intensity, tint.b * intensity, 0f));
            CoreUtils.SetKeyword(material, HighQualityKeyword, highQuality);
        }

        private bool EnsureMaterials()
        {
            if (_shader == null)
            {
                _shader = Shader.Find(ShaderName);
            }

            if (_shader == null)
            {
                return false;
            }

            for (int i = 0; i < _materials.Length; i++)
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
            for (int i = 0; i < _materials.Length; i++)
            {
                CoreUtils.Destroy(_materials[i]);
                _materials[i] = null;
            }
        }

        private sealed class SceneBloomPass : ScriptableRenderPass
        {
            private readonly TextureHandle[][] _down = new TextureHandle[GroupCount][];
            private readonly TextureHandle[][] _up = new TextureHandle[GroupCount][];
            private readonly Vector4[][] _sizes = new Vector4[GroupCount][];
            private readonly TextureHandle[] _results = new TextureHandle[GroupCount];
            private Material[] _materials;
            private bool[] _active;
            private int _maxMips;
            private LayerMask _extraLayers;

            private sealed class MaskData
            {
                internal RendererListHandle Characters;
                internal RendererListHandle Extra;
                internal bool HasExtra;
            }

            private sealed class PyramidData
            {
                internal Material Material;
                internal TextureHandle Source;
                internal TextureHandle Mask;
                internal TextureHandle[] Down;
                internal TextureHandle[] Up;
                internal Vector4[] Sizes;
                internal int MipCount;
            }

            private sealed class CompositeData
            {
                internal Material Material;
                internal TextureHandle Bloom;
            }

            internal SceneBloomPass()
            {
                // 톤매핑 전의 HDR 화면에 더해야 URP Bloom과 같은 결과가 된다.
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                for (int i = 0; i < GroupCount; i++)
                {
                    _down[i] = new TextureHandle[MaxMips];
                    _up[i] = new TextureHandle[MaxMips];
                    _sizes[i] = new Vector4[MaxMips];
                }
            }

            internal void Setup(Material[] materials, bool[] active, int maxMips, LayerMask extraLayers)
            {
                _materials = materials;
                _active = active;
                _maxMips = maxMips;
                _extraLayers = extraLayers;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (_materials == null || resources.isActiveTargetBackBuffer)
                {
                    return;
                }

                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                TextureHandle cameraColor = resources.activeColorTexture;
                TextureDesc cameraDesc = renderGraph.GetTextureDesc(cameraColor);

                // 1. 동적 객체 마스크. 깊이 버퍼를 함께 쓰려면 카메라 색 버퍼와 크기·샘플 수가 같아야 한다.
                var maskDesc = new TextureDesc(cameraDesc.width, cameraDesc.height)
                {
                    name = "Scene Bloom Dynamic Mask",
                    colorFormat = GraphicsFormat.R8_UNorm,
                    msaaSamples = cameraDesc.msaaSamples,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    clearBuffer = true,
                    clearColor = Color.clear,
                };
                TextureHandle mask = renderGraph.CreateTexture(maskDesc);

                using (var builder = renderGraph.AddRasterRenderPass<MaskData>("Scene Bloom Dynamic Mask", out var data))
                {
                    DrawingSettings characters = RenderingUtils.CreateDrawingSettings(Ids.CharacterTag, renderingData, cameraData, lightData,
                        SortingCriteria.CommonOpaque);
                    characters.overrideMaterial = _materials[1];
                    characters.overrideMaterialPassIndex = PassMask;
                    var allLayers = new FilteringSettings(RenderQueueRange.all);
                    data.Characters = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, characters, allLayers));
                    builder.UseRendererList(data.Characters);

                    data.HasExtra = _extraLayers.value != 0;
                    if (data.HasExtra)
                    {
                        DrawingSettings extra = RenderingUtils.CreateDrawingSettings(Ids.CommonTags[0], renderingData, cameraData, lightData,
                            SortingCriteria.CommonOpaque);
                        for (int i = 1; i < Ids.CommonTags.Length; i++)
                        {
                            extra.SetShaderPassName(i, Ids.CommonTags[i]);
                        }

                        extra.overrideMaterial = _materials[1];
                        extra.overrideMaterialPassIndex = PassMask;
                        var extraLayers = new FilteringSettings(RenderQueueRange.all, _extraLayers.value);
                        data.Extra = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, extra, extraLayers));
                        builder.UseRendererList(data.Extra);
                    }

                    builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (MaskData d, RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(d.Characters);
                        if (d.HasExtra)
                        {
                            context.cmd.DrawRendererList(d.Extra);
                        }
                    });
                }

                // 1을 넘는 색을 그대로 담아야 하므로 HDR 포맷을 쓴다.
                GraphicsFormat format = SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, GraphicsFormatUsage.Blend)
                    ? GraphicsFormat.B10G11R11_UFloatPack32
                    : GraphicsFormat.R16G16B16A16_SFloat;

                // 절반 해상도에서 시작해 절반씩 줄여 나간다(URP Bloom과 같은 단계 수).
                int halfWidth = Mathf.Max(1, cameraDesc.width >> 1);
                int halfHeight = Mathf.Max(1, cameraDesc.height >> 1);
                int mipCount = Mathf.Clamp(Mathf.FloorToInt(Mathf.Log(Mathf.Max(halfWidth, halfHeight), 2f) - 1f), 1, _maxMips);

                // 2~3. 그룹마다 자기 몫의 밝은 부분을 고르고 번지게 한다. 두 그룹 모두 "더하기 전" 화면에서 골라야 하므로 더하기는 맨 뒤에 한다.
                for (int group = 0; group < GroupCount; group++)
                {
                    _results[group] = TextureHandle.nullHandle;
                    if (!_active[group] || _materials[group] == null)
                    {
                        continue;
                    }

                    TextureHandle[] down = _down[group];
                    TextureHandle[] up = _up[group];
                    Vector4[] sizes = _sizes[group];
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
                        mipDesc.name = "Scene Bloom Down " + group + "-" + i;
                        down[i] = renderGraph.CreateTexture(mipDesc);
                        mipDesc.name = "Scene Bloom Up " + group + "-" + i;
                        up[i] = renderGraph.CreateTexture(mipDesc);
                        sizes[i] = new Vector4(width, height, 1f / width, 1f / height);
                        width = Mathf.Max(1, width >> 1);
                        height = Mathf.Max(1, height >> 1);
                    }

                    using (var builder = renderGraph.AddUnsafePass<PyramidData>("Scene Bloom Blur", out var data))
                    {
                        data.Material = _materials[group];
                        data.Source = cameraColor;
                        data.Mask = mask;
                        data.Down = down;
                        data.Up = up;
                        data.Sizes = sizes;
                        data.MipCount = mipCount;

                        builder.UseTexture(cameraColor, AccessFlags.Read);
                        builder.UseTexture(mask, AccessFlags.Read);
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

                            // 절반 해상도로 옮기면서 이 그룹의 몫만, 임계값을 넘는 만큼만 남긴다.
                            cmd.SetGlobalTexture(Ids.Mask, (RTHandle)d.Mask);
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
                                cmd.SetGlobalVector(Ids.LowMipSize, d.Sizes[i + 1]);
                                Blitter.BlitCameraTexture(cmd, d.Down[i], d.Up[i], load, store, d.Material, PassUpsample);
                            }
                        });
                    }

                    _results[group] = mipCount == 1 ? down[0] : up[0];
                }

                // 4. 화면에 더한다.
                for (int group = 0; group < GroupCount; group++)
                {
                    if (!_results[group].IsValid())
                    {
                        continue;
                    }

                    using (var builder = renderGraph.AddRasterRenderPass<CompositeData>("Scene Bloom Composite", out var data))
                    {
                        data.Material = _materials[group];
                        data.Bloom = _results[group];

                        builder.UseTexture(_results[group], AccessFlags.Read);
                        builder.SetRenderAttachment(cameraColor, 0, AccessFlags.Write);
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
