using System.Collections.Generic;
using CK.SemesterProject.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// 캐릭터 전용 자기 그림자 맵. 캐릭터마다 "툰 명암을 정하는 빛"(씬 주광과 구역 가상 주광을 섞은 방향)에서
// 캐릭터만 그린 깊이를 칸 하나에 담는다. 셰이더는 이 값을 툰 명암 계단 안에 넣어 치마·재킷 아래처럼 가려진 곳을 그늘지게 한다.
// HoyoToon/StarRail이 주광 그림자 맵을 명암 계단에 넣는 방식과 같지만, 가상 주광 방향에서도 동작하도록 캐릭터별로 직접 그린다.
// 별도 아트 데이터(라이트맵·램프 텍스처) 없이 메시만으로 동작한다.
// 담는 범위는 CharacterLighting의 고정 구(Self Shadow 중심·반지름)라 무기 길이·애니메이션에 따라 해상도가 바뀌지 않는다.
public sealed class CharacterSelfShadowFeature : ScriptableRendererFeature
{
    [SerializeField, Tooltip("캐릭터 하나당 그림자 맵 해상도(px)입니다. 반지름 1m에서 2048이면 텍셀 약 1mm입니다.")]
    private int _slotResolution = 2048;

    [SerializeField, Min(0f), Tooltip("캐릭터 구 바깥 여유(m)입니다.")]
    private float _boundsPadding = .05f;

    private SelfShadowPass _pass;

    public override void Create()
    {
        _pass = new SelfShadowPass();
        RenderPipelineManager.beginCameraRendering -= ResetShadow;
        RenderPipelineManager.beginCameraRendering += ResetShadow;
    }

    // 이 기능이 없는 렌더러의 카메라나 패스가 빠진 프레임이 이전 카메라의 임시 텍스처를 읽지 않게 한다.
    private static void ResetShadow(ScriptableRenderContext context, Camera camera)
    {
        Shader.SetGlobalVector(SelfShadowPass.ParamsId, Vector4.zero);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.camera.cameraType == CameraType.Preview)
        {
            return;
        }
        _pass.Setup(Mathf.Clamp(_slotResolution, 128, 2048), _boundsPadding);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        RenderPipelineManager.beginCameraRendering -= ResetShadow;
        Shader.SetGlobalVector(SelfShadowPass.ParamsId, Vector4.zero);
    }

    private sealed class SelfShadowPass : ScriptableRenderPass
    {
        internal static readonly int ParamsId = Shader.PropertyToID("_FuchsiaCharShadowParams");
        private static readonly int MapId = Shader.PropertyToID("_FuchsiaCharShadowMap");
        private static readonly int MatricesId = Shader.PropertyToID("_FuchsiaCharShadowMatrices");
        private static readonly int DepthScaleId = Shader.PropertyToID("_FuchsiaCharShadowDepthScale");
        private static readonly int RenderMatrixId = Shader.PropertyToID("_FuchsiaCharShadowRender");
        private const string PassName = "FuchsiaCharacterShadow";

        private readonly Matrix4x4[] _matrices = new Matrix4x4[CharacterLighting.MaxShadowSlots];
        private readonly Vector4[] _depthScale = new Vector4[CharacterLighting.MaxShadowSlots];
        private int _resolution;
        private float _padding;

        private struct Draw
        {
            public Renderer Renderer;
            public Material Material;
            public int SubMesh;
            public int Pass;
        }

        private sealed class PassData
        {
            internal readonly List<Draw>[] Draws = new List<Draw>[CharacterLighting.MaxShadowSlots];
            internal readonly Matrix4x4[] Render = new Matrix4x4[CharacterLighting.MaxShadowSlots];
            internal readonly bool[] Used = new bool[CharacterLighting.MaxShadowSlots];
            internal Matrix4x4[] Matrices;
            internal Vector4[] DepthScale;
            internal int Resolution;
        }

        internal SelfShadowPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
        }

        internal void Setup(int resolution, float padding)
        {
            _resolution = resolution;
            _padding = padding;
        }

        // 월드 → 칸 안 클립(x,y ∈ [-1,1]) + 광원 쪽에서 잰 깊이(z ∈ [0,1]).
        // 중심을 광원 시점의 텍셀 격자에 맞춰(cascade 안정화와 같은 방식) 캐릭터가 움직여도 그림자 경계가 지글거리지 않게 한다.
        private static Matrix4x4 BuildMatrix(Vector3 center, float radius, Vector3 toLight, int resolution,
            out float range, out float texelSize)
        {
            Vector3 l = toLight.normalized;
            Vector3 up = Mathf.Abs(l.y) > .95f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Normalize(Vector3.Cross(up, l));
            up = Vector3.Cross(l, right);
            texelSize = 2f * radius / resolution;
            float x = Vector3.Dot(center, right);
            float y = Vector3.Dot(center, up);
            Vector3 c = center + right * (Mathf.Round(x / texelSize) * texelSize - x)
                + up * (Mathf.Round(y / texelSize) * texelSize - y);
            Vector3 origin = c + l * radius;
            range = 2f * radius;
            var m = Matrix4x4.zero;
            m.SetRow(0, new Vector4(right.x, right.y, right.z, -Vector3.Dot(c, right)) / radius);
            m.SetRow(1, new Vector4(up.x, up.y, up.z, -Vector3.Dot(c, up)) / radius);
            m.SetRow(2, new Vector4(-l.x, -l.y, -l.z, Vector3.Dot(origin, l)) / range);
            m.SetRow(3, new Vector4(0f, 0f, 0f, 1f));
            return m;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var desc = new TextureDesc(_resolution * CharacterLighting.MaxShadowSlots, _resolution)
            {
                name = "Fuchsia Character Self Shadow",
                // 0~1 깊이를 16비트 부동소수로 저장한다(2m 범위에서 약 1mm 정밀도, 바이어스보다 충분히 작다).
                colorFormat = GraphicsFormat.R16_SFloat,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                clearBuffer = true,
                clearColor = Color.white,
            };
            TextureHandle map = renderGraph.CreateTexture(desc);
            desc.name = "Fuchsia Character Self Shadow Z";
            desc.colorFormat = GraphicsFormat.None;
            desc.depthBufferBits = DepthBits.Depth16;
            TextureHandle depth = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Fuchsia Character Self Shadow", out var data))
            {
                data.Resolution = _resolution;
                data.Matrices = _matrices;
                data.DepthScale = _depthScale;
                IReadOnlyList<CharacterLighting> characters = CharacterLighting.Active;
                for (int slot = 0; slot < CharacterLighting.MaxShadowSlots; slot++)
                {
                    data.Draws[slot] ??= new List<Draw>();
                    data.Draws[slot].Clear();
                    data.Used[slot] = false;
                    _matrices[slot] = Matrix4x4.identity;
                    _depthScale[slot] = Vector4.zero;
                    if (slot >= characters.Count) continue;
                    CharacterLighting character = characters[slot];
                    if (character == null || !character.TryGetSelfShadowSphere(out Vector3 center, out float radius)) continue;

                    Matrix4x4 m = BuildMatrix(center, radius + _padding, character.ShadingLightDirection, _resolution,
                        out float range, out float texelSize);
                    data.Render[slot] = m;
                    _matrices[slot] = m;
                    // x: 1/깊이 범위, y: 깊이 범위(m), z: 텍셀 크기(m) — 셰이더의 바이어스·노멀 밀기에 쓴다.
                    _depthScale[slot] = new Vector4(1f / range, range, texelSize, 0f);
                    foreach (Renderer renderer in character.Renderers)
                    {
                        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy
                            || renderer.shadowCastingMode == ShadowCastingMode.Off) continue;
                        Material[] materials = renderer.sharedMaterials;
                        for (int i = 0; i < materials.Length; i++)
                        {
                            Material material = materials[i];
                            int pass = material != null ? material.FindPass(PassName) : -1;
                            if (pass >= 0) data.Draws[slot].Add(new Draw { Renderer = renderer, Material = material, SubMesh = i, Pass = pass });
                        }
                    }
                    data.Used[slot] = data.Draws[slot].Count > 0;
                }

                builder.SetRenderAttachment(map, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
                builder.SetGlobalTextureAfterPass(map, MapId);
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData d, RasterGraphContext context) =>
                {
                    RasterCommandBuffer cmd = context.cmd;
                    bool any = false;
                    for (int slot = 0; slot < CharacterLighting.MaxShadowSlots; slot++)
                    {
                        if (!d.Used[slot]) continue;
                        any = true;
                        cmd.SetViewport(new Rect(slot * d.Resolution, 0, d.Resolution, d.Resolution));
                        cmd.SetGlobalMatrix(RenderMatrixId, d.Render[slot]);
                        foreach (Draw draw in d.Draws[slot])
                        {
                            cmd.DrawRenderer(draw.Renderer, draw.Material, draw.SubMesh, draw.Pass);
                        }
                    }
                    cmd.SetGlobalMatrixArray(MatricesId, d.Matrices);
                    cmd.SetGlobalVectorArray(DepthScaleId, d.DepthScale);
                    // x: 사용 여부, y: 칸 수, z: 칸 해상도
                    cmd.SetGlobalVector(ParamsId, new Vector4(any ? 1f : 0f, CharacterLighting.MaxShadowSlots, d.Resolution, 0f));
                });
            }
        }
    }
}
