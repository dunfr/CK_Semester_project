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

        /// <param name="playCasterClip">false면 시전자 클립을 덮어쓰지 않는다(몬스터가 플레이어용 시퀀스를 빌려 쓸 때 자기 공격 모션 유지).</param>
        public SkillSequencePlayback(SkillSequenceAsset sequence, SkillSequenceContext context, bool playCasterClip = true)
        {
            _sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
            _sampler = new SkillSequenceSampler(sequence, context, playCasterClip);
            _sampler.Sample(0f);
            SpawnEventsBetween(-1f, 0f);
        }

        public event Action<int> HitReached;
        /// <summary>컷신(Timeline) 재생을 시작했을 때. 전투 쪽에서 UI 숨기기 등에 쓴다.</summary>
        public event Action<SkillCutscenePlayer> CutsceneStarted;

        /// <summary>재생 중인 컷신(없으면 null).</summary>
        public SkillCutscenePlayer Cutscene { get; private set; }

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
            ApplyCameraShake();
            if (_sequence.Cutscene != null && Cutscene == null && _sequence.CutsceneStart > previous && _sequence.CutsceneStart <= _time)
            {
                Cutscene = SkillCutscenePlayer.Play(_sequence.Cutscene, _sampler.Context.Caster, _sampler.Context.SelectedTarget);
                if (Cutscene != null)
                {
                    CutsceneStarted?.Invoke(Cutscene);
                }
            }
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
            SkillCameraShake.Clear();
            _instances.Clear();
        }

        private void ApplyCameraShake()
        {
            Camera camera = Camera.main;
            if (SkillCameraShake.Evaluate(_sequence.ShakeEvents, _time, out Vector3 position, out Vector3 rotation))
            {
                SkillCameraShake.SetOffset(camera, position, rotation);
            }
            else
            {
                SkillCameraShake.Clear();
            }
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
            // 이펙터가 Timeline으로 만든 이펙트: 생성 즉시 재생하고 재생 속도를 맞춘다.
            foreach (UnityEngine.Playables.PlayableDirector director in instance.GetComponentsInChildren<UnityEngine.Playables.PlayableDirector>(true))
            {
                if (director.playableAsset == null)
                {
                    continue;
                }
                director.time = 0d;
                director.Play();
                if (director.playableGraph.IsValid() && director.playableGraph.GetRootPlayableCount() > 0)
                {
                    UnityEngine.Playables.PlayableExtensions.SetSpeed(director.playableGraph.GetRootPlayable(0), vfxEvent.PlaybackSpeed);
                }
            }
            foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
            {
                animator.speed *= vfxEvent.PlaybackSpeed;
            }
            _instances.Add(instance);
            UnityEngine.Object.Destroy(instance, vfxEvent.GetEffectiveLifetime());
        }
    }
}
