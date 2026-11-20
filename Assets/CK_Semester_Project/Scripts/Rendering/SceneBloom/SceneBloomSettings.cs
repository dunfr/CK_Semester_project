using UnityEngine;
using UnityEngine.Rendering;

namespace CK.SemesterProject.Rendering
{
    // 씬 전체 Bloom을 둘로 나눈 것의 공통 항목. 아래 두 가지가 따로 있다.
    //   Background Bloom      → 배경(동적 객체가 아닌 모든 것)
    //   Dynamic Object Bloom  → 동적 객체(캐릭터·몬스터)
    // 둘 중 하나라도 Intensity가 0보다 크면 URP 기본 Bloom(Lamp Bloom Volume의 Bloom) 대신 이 두 항목이 쓰인다.
    // 둘 다 0이면 예전처럼 기본 Bloom 하나가 화면 전체에 걸린다.
    // 두 항목을 기본 Bloom과 같은 값으로 두면 기본 Bloom과 같은 그림이 나온다.
    // 실제 그리기는 렌더러(PC_Renderer)의 Scene Bloom 기능이 맡는다.
    public abstract class SceneBloomSettings : VolumeComponent
    {
        [Tooltip("이 값보다 밝은 부분만 번집니다. 낮출수록 덜 밝은 곳까지 번집니다.")]
        public MinFloatParameter threshold = new MinFloatParameter(1f, 0f);

        [Tooltip("번짐의 세기입니다. 0이면 이쪽은 번지지 않습니다.")]
        public MinFloatParameter intensity = new MinFloatParameter(0f, 0f);

        [Tooltip("번짐이 퍼지는 넓이입니다. 낮으면 밝은 곳 가까이에만, 높으면 넓게 퍼집니다.")]
        public ClampedFloatParameter scatter = new ClampedFloatParameter(0.7f, 0f, 1f);

        [Tooltip("번짐에 곱하는 색입니다.")]
        public ColorParameter tint = new ColorParameter(Color.white, false, false, true);

        public bool IsActive()
        {
            return intensity.value > 0f;
        }
    }
}
