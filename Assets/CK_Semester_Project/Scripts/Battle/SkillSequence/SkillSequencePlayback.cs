using System;
using System.Collections.Generic;
using UnityEngine;

namespace CK.SemesterProject.SkillSequence
{
    /// <summary>
    /// 실제 게임에서 스킬 연출을 재생한다. Advance(deltaTime)를 매 프레임 호출하면 이동·애니메이션을 적용하고
    /// 시점에 맞춰 이펙트를 생성하며, 타격 시점마다 HitReached(타격 번호)를 알린다.
    /// TODO: 애니메이션 완성 후 TutorialBattleActionPresentation의 고정 타이밍 연출을 이 재생기로 교체한다.
    /// </summary>
    public sealed class SkillSequencePlayback : IDisposable
    {
        private readonly SkillSequenceAsset _sequence;
        private readonly SkillSequenceSampler _sampler;
        private readonly List<GameObject> _instances = new List<GameObject>();
        private float _time;
        private bool _isDisposed;

        public SkillSequencePlayback(SkillSequenceAsset sequence, SkillSequenceContext context)
        {
            _sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
            _sampler = new SkillSequenceSampler(sequence, context);
            _sampler.Sample(0f);
            SpawnEventsBetween(-1f, 0f);
        }

        public event Action<int> HitReached;

        public float Time => _time;
        public bool IsFinished => _time >= _sequence.Duration;

        public void Advance(float deltaTime)
        {
            if (_isDisposed || IsFinished)
            {
                return;
            }
            float previous = _time;
            _time = Mathf.Min(_sequence.Duration, _time + Mathf.Max(0f, deltaTime));
            _sampler.Sample(_time);
            SpawnEventsBetween(previous, _time);
            for (int i = 0; i < _sequence.HitTimes.Count; i++)
            {
                float hitTime = _sequence.HitTimes[i];
                if (hitTime > previous && hitTime <= _time)
                {
                    HitReached?.Invoke(i);
                }
            }
        }

        /// <summary>이동·포즈를 제자리로 되돌린다. 이미 생성된 이펙트는 각자 유지 시간이 끝나면 사라진다.</summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;
            _sampler.Dispose();
            _instances.Clear();
        }

        private void SpawnEventsBetween(float previous, float current)
        {
            foreach (SkillVfxEvent vfxEvent in _sequence.VfxEvents)
            {
                if (vfxEvent.Prefab == null || vfxEvent.Time <= previous || vfxEvent.Time > current)
                {
                    continue;
                }
                foreach (Transform target in _sampler.Context.GetEventTargets(vfxEvent))
                {
                    Spawn(vfxEvent, target);
                }
            }
        }

        private void Spawn(SkillVfxEvent vfxEvent, Transform target)
        {
            _sampler.Context.GetSpawnPose(vfxEvent, target, out Vector3 position, out Quaternion rotation, out Transform parent);
            GameObject instance = UnityEngine.Object.Instantiate(vfxEvent.Prefab, position, rotation);
            instance.transform.localScale = vfxEvent.Prefab.transform.localScale * vfxEvent.Scale;
            if (parent != null)
            {
                instance.transform.SetParent(parent, true);
            }
            foreach (ParticleSystem particle in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particle.main;
                main.simulationSpeed *= vfxEvent.PlaybackSpeed;
            }
            _instances.Add(instance);
            UnityEngine.Object.Destroy(instance, vfxEvent.GetEffectiveLifetime());
        }
    }
}
