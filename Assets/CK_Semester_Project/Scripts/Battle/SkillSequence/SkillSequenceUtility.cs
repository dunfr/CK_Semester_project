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
            return lifetime > 0f ? Mathf.Min(lifetime, MaximumLifetime) : DefaultLifetime;
        }

        public static float SnapToFrame(float time, float frameRate)
        {
            return frameRate <= 0f ? time : Mathf.Round(time * frameRate) / frameRate;
        }
    }
}
