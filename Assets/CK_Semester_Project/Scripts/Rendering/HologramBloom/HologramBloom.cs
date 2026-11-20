using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CK.SemesterProject.Rendering
{
    // 홀로그램 화면만 번지게 하는 Bloom. Volume 프로필에 기본 Bloom과 나란히 넣어서 쓴다.
    //   Bloom           → 천장·기둥 조명처럼 씬에서 밝은 것 전부
    //   Hologram Bloom  → 홀로그램 셰이더(Semester/Environment/Hologram Panel)로 그린 것만
    // Intensity가 0이면 꺼진 것이고, 그때는 홀로그램도 예전처럼 기본 Bloom을 따른다.
    // 실제 그리기는 렌더러(PC_Renderer)의 Hologram Bloom 기능이 맡는다.
    [Serializable, VolumeComponentMenu("Semester/Hologram Bloom")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class HologramBloom : VolumeComponent
    {
        [Tooltip("선·글자 중 이 값보다 밝은 부분만 번집니다. 낮출수록 옅은 면까지 번집니다. (홀로그램 머티리얼의 Line·Accent 색 세기와 비교되는 값입니다) 화면 전체가 내는 빛(머티리얼의 Whole screen glow)은 이 값과 상관없이 번집니다.")]
        public MinFloatParameter threshold = new MinFloatParameter(0.9f, 0f);

        [Tooltip("번짐의 세기입니다. 0이면 홀로그램 전용 Bloom을 끄고 기본 Bloom을 따릅니다.")]
        public MinFloatParameter intensity = new MinFloatParameter(0f, 0f);

        [Tooltip("번짐이 퍼지는 넓이입니다. 낮으면 선 가까이에만, 높으면 넓게 퍼집니다.")]
        public ClampedFloatParameter scatter = new ClampedFloatParameter(0.7f, 0f, 1f);

        [Tooltip("번짐에 곱하는 색입니다.")]
        public ColorParameter tint = new ColorParameter(Color.white, false, false, true);

        [Tooltip("화면에 그려지는 홀로그램 본체 밝기의 상한입니다. 기본 Bloom의 Threshold 절반쯤 이하로 두면 조명용 Bloom이 홀로그램을 거의 잡지 않습니다. 올리면 본체가 밝아지지만 그만큼 조명용 Bloom이 섞이기 시작합니다.")]
        public ClampedFloatParameter bodyBrightness = new ClampedFloatParameter(0.5f, 0.1f, 1f);

        public bool IsActive()
        {
            return intensity.value > 0f;
        }
    }
}
