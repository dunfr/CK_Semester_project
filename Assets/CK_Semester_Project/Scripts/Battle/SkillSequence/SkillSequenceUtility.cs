using UnityEngine;

namespace CK.SemesterProject.SkillSequence
{
    public static class SkillSequenceUtility
    {
        private const float DefaultLifetime = 2f;
        private const float MaximumLifetime = 8f;

        /// <summary>프리팹 안 파티클의 재생 길이 + 최대 수명으로 유지 시간을 추정한다.</summary>
        public static float EstimateLifetime(GameObject prefab)
        {
            if (prefab == null)
            {
                return DefaultLifetime;
            }
            float lifetime = 0f;
            foreach (ParticleSystem particle in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particle.main;
                float delay = main.startDelay.constantMax;
                lifetime = Mathf.Max(lifetime, delay + main.duration + main.startLifetime.constantMax);
            }
            // 이펙터가 Timeline(PlayableDirector)·Animator로 만든 이펙트는 그 길이도 포함한다.
            foreach (UnityEngine.Playables.PlayableDirector director in prefab.GetComponentsInChildren<UnityEngine.Playables.PlayableDirector>(true))
            {
                if (director.playableAsset != null)
                {
                    lifetime = Mathf.Max(lifetime, (float)director.playableAsset.duration);
                }
            }
            foreach (Animator animator in prefab.GetComponentsInChildren<Animator>(true))
            {
                if (animator.runtimeAnimatorController == null)
                {
                    continue;
                }
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                {
                    lifetime = Mathf.Max(lifetime, clip.length);
                }
            }
            return lifetime > 0f ? Mathf.Min(lifetime, MaximumLifetime) : DefaultLifetime;
        }

        public static float SnapToFrame(float time, float frameRate)
        {
            return frameRate <= 0f ? time : Mathf.Round(time * frameRate) / frameRate;
        }
    }
}
