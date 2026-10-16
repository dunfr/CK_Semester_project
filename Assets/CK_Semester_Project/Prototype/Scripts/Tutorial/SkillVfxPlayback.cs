using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    // 실제 스킬과 에디터 미리보기에서 같은 파티클 속도·시작 지연을 사용한다.
    public sealed class SkillVfxPlayback : MonoBehaviour
    {
        public void Initialize(SkillVfxPlacement settings, float baseSpeed = 1f)
        {
            ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
            float speed = settings.EffectivePlaybackSpeed * baseSpeed;
            foreach (ParticleSystem particle in particles)
            {
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particle.main;
                main.simulationSpeed *= speed;
            }
            TutorialSlashVfxPlacement slash = GetComponent<TutorialSlashVfxPlacement>();
            if (slash != null)
            {
                slash.SetPlaybackTiming(settings.StartDelay, settings.EffectivePlaybackSpeed);
            }
            StartCoroutine(PlayAfterDelay(particles, Mathf.Max(0f, settings.StartDelay), settings.Lifetime));
        }

        private IEnumerator PlayAfterDelay(ParticleSystem[] particles, float delay, float lifetime)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }
            foreach (ParticleSystem particle in particles)
            {
                if (particle != null && particle.gameObject.activeInHierarchy)
                {
                    particle.Play(false);
                }
            }
            if (lifetime > 0f)
            {
                yield return new WaitForSeconds(lifetime);
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }

        public static float EstimateDuration(GameObject root, float speed = 1f)
        {
            float duration = .1f;
            ParticleSystem[] particles = root.GetComponentsInChildren<ParticleSystem>(true);
            var subEmitters = new HashSet<ParticleSystem>();
            foreach (ParticleSystem particle in particles)
            {
                var module = particle.subEmitters;
                for (int i = 0; i < module.subEmittersCount; i++)
                {
                    subEmitters.Add(module.GetSubEmitterSystem(i));
                }
            }
            foreach (ParticleSystem particle in particles)
            {
                var main = particle.main;
                var emission = particle.emission;
                bool continuous = emission.enabled
                    && (Maximum(emission.rateOverTime) > 0f || Maximum(emission.rateOverDistance) > 0f);
                bool hasBursts = emission.enabled && emission.burstCount > 0;
                bool isSubEmitter = subEmitters.Contains(particle);
                if (!continuous && !hasBursts && !isSubEmitter)
                {
                    continue;
                }
                float lastEmission = continuous || main.loop || isSubEmitter ? main.duration : 0f;
                if (hasBursts)
                {
                    for (int i = 0; i < emission.burstCount; i++)
                    {
                        ParticleSystem.Burst burst = emission.GetBurst(i);
                        float lastBurst = burst.cycleCount <= 0 ? main.duration
                            : burst.time + burst.repeatInterval * (burst.cycleCount - 1);
                        lastEmission = Mathf.Max(lastEmission, Mathf.Min(main.duration, lastBurst));
                    }
                }
                // 단발 방출은 duration이 끝날 때까지 새 입자가 나오지 않는다.
                float particleDuration = Maximum(main.startDelay) + lastEmission + Maximum(main.startLifetime);
                duration = Mathf.Max(duration, particleDuration / Mathf.Max(.01f, main.simulationSpeed * speed));
            }
            return duration;
        }

        private static float Maximum(ParticleSystem.MinMaxCurve curve)
        {
            if (curve.mode == ParticleSystemCurveMode.Constant || curve.mode == ParticleSystemCurveMode.TwoConstants)
            {
                return Mathf.Max(curve.constantMin, curve.constantMax);
            }
            float maximum = 0f;
            foreach (AnimationCurve animation in new[] { curve.curveMin, curve.curveMax })
            {
                if (animation == null)
                {
                    continue;
                }
                foreach (Keyframe key in animation.keys)
                {
                    maximum = Mathf.Max(maximum, key.value * curve.curveMultiplier);
                }
            }
            return maximum;
        }
    }
}
