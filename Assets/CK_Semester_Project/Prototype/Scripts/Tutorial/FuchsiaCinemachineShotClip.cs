using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace CK.SemesterProject.Tutorial
{
    [Serializable]
    public sealed class FuchsiaCinemachineShotClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("이 숏을 출력하는 실제 Cinemachine Camera입니다.")]
        public ExposedReference<CinemachineCamera> Camera;
        [Tooltip("이 카메라의 위치·회전 키. Animation 창에서 수정할 수 있습니다.")]
        public AnimationClip Motion;
        [Tooltip("원본 모션에서 사용할 시작 시간. 음수 구간은 첫 프레임을 유지합니다.")]
        public double SourceIn;
        [Tooltip("원본 모션에서 사용할 끝 시간. 클립 길이를 바꾸면 이 구간 전체가 새 길이에 맞춰 재생됩니다.")]
        public double SourceOut = 1d;
        [Tooltip("가로 시야각. 0이면 Cinemachine Camera의 Lens 설정을 사용합니다.")]
        [Range(0f, 179f)] public float HorizontalFieldOfView = 45f;

        public override double duration => Math.Max(.001d, SourceOut - SourceIn);
        public ClipCaps clipCaps => ClipCaps.Blending;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<ShotFrame>.Create(graph);
            var frame = playable.GetBehaviour();
            frame.Camera = Camera.Resolve(graph.GetResolver());
            frame.SourceIn = SourceIn;
            frame.SourceOut = SourceOut;
            frame.HorizontalFieldOfView = HorizontalFieldOfView;
            Animator animator = frame.Camera != null ? frame.Camera.GetComponentInParent<Animator>() : null;
            if (animator != null && Motion != null)
            {
                frame.Root = animator.gameObject;
                frame.Motion = Motion;
            }
            return playable;
        }

        public sealed class ShotFrame : PlayableBehaviour
        {
            public CinemachineCamera Camera;
            public GameObject Root;
            public AnimationClip Motion;
            public double SourceIn, SourceOut;
            public float HorizontalFieldOfView;

            public void Sample(double localTime, double duration)
            {
                if (Motion == null || Root == null) return;
                double progress = Math.Clamp(localTime / Math.Max(.000001d, duration), 0d, 1d);
                double sourceTime = Math.Clamp(SourceIn + progress * (SourceOut - SourceIn), 0d, Motion.length);
                Motion.SampleAnimation(Root, (float)sourceTime);
            }
        }
    }
}
