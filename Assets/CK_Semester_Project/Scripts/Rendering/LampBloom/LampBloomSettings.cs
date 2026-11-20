using UnityEngine;
using UnityEngine.Rendering;

namespace CK.SemesterProject.Rendering
{
    // 조명 한 종류만 번지게 하는 Bloom의 공통 항목. 종류마다 아래 세 가지가 따로 있다.
    //   Corridor Ceiling Lamp Bloom    → 복도 천장 조명 (CeilingLight_Emissive)
    //   Living Room Ceiling Lamp Bloom → 거실 천장 조명 (CeilingLight_LivingRoom_Emissive)
    //   Pillar Lamp Bloom              → 기둥 조명 (PillarLight_Emissive)
    // 어느 Volume이 어느 램프를 맡는지는 램프 머티리얼의 "Lamp Bloom Volume" 항목이 정한다.
    // Intensity가 0이면 꺼진 것이고, 그때 그 종류의 램프는 예전처럼 씬 전체 Bloom(Lamp Bloom Volume)을 따른다.
    // 실제 그리기는 렌더러(PC_Renderer)의 Lamp Bloom 기능이 맡는다.
    public abstract class LampBloomSettings : VolumeComponent
    {
        [Tooltip("램프 면의 밝기(머티리얼의 Base Color + Emission Color) 중 이 값을 넘는 만큼만 번집니다. 낮출수록 같은 Emission으로 더 많이 번집니다.")]
        public MinFloatParameter threshold = new MinFloatParameter(1f, 0f);

        [Tooltip("번짐의 세기입니다. 0이면 이 조명 전용 Bloom을 끄고 씬 전체 Bloom을 따릅니다.")]
        public MinFloatParameter intensity = new MinFloatParameter(0f, 0f);

        [Tooltip("번짐이 퍼지는 넓이입니다. 낮으면 램프 가까이에만, 높으면 넓게 퍼집니다.")]
        public ClampedFloatParameter scatter = new ClampedFloatParameter(0.7f, 0f, 1f);

        [Tooltip("번짐에 곱하는 색입니다.")]
        public ColorParameter tint = new ColorParameter(Color.white, false, false, true);

        [Tooltip("번짐을 얹기 전 램프 면 자체의 밝기 상한입니다. 씬 전체 Bloom의 Threshold보다 조금 낮게 두면 씬 Bloom이 이 램프를 거의 잡지 않습니다. 올리면 면이 더 하얗게 보이지만 그만큼 씬 Bloom이 섞입니다.")]
        public ClampedFloatParameter bodyBrightness = new ClampedFloatParameter(0.8f, 0.1f, 1f);

        public bool IsActive()
        {
            return intensity.value > 0f;
        }
    }
}
