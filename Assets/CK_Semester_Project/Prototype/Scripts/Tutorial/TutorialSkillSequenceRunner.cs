using System;
using System.Collections.Generic;
using System.Linq;
using CK.SemesterProject.Battle;
using CK.SemesterProject.SkillSequence;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace CK.SemesterProject.Tutorial
{
    /// <summary>
    /// 스킬 연출 스튜디오에서 만든 스킬 시퀀스(SkillSequenceAsset)를 실제 전투에서 재생한다.
    /// 전투 컨트롤러(TutorialBattleController)와 같은 오브젝트에 두고, 스킬 ID·몬스터별로 시퀀스를 연결한다.
    /// 연결된 스킬은 기존 임시 연출(접근·기본 이펙트) 대신 시퀀스의 애니메이션·이동·이펙트·카메라 흔들림이 재생된다.
    /// </summary>
    public sealed class TutorialSkillSequenceRunner : MonoBehaviour
    {
        [Serializable]
        public sealed class PlayerSkillBinding
        {
            [Tooltip("Skill_DT 스킬 ID (예: SK00). 강화 단계 ID(SK03 등)도 따로 연결할 수 있습니다.")]
            public string SkillId;
            public SkillSequenceAsset Sequence;
        }

        [Serializable]
        public sealed class MonsterSkillBinding
        {
            [Tooltip("이 몬스터 정의에만 쓰려면 지정합니다. 비우면 아래 속성의 몬스터 전체에 씁니다.")]
            public MonsterBattleDefinition Definition;
            [Tooltip("몬스터 정의를 비웠을 때 적용할 속성")]
            public BattleElement Element;
            [Tooltip("적 스킬 시퀀스(스튜디오에서 '적 스킬'을 켠 것)")]
            public SkillSequenceAsset Sequence;
        }

        [SerializeField, Tooltip("플레이어 스킬 ID별 시퀀스")]
        private List<PlayerSkillBinding> _playerSkills = new List<PlayerSkillBinding>();
        [SerializeField, Tooltip("몬스터 공격 시퀀스. 몬스터 정의가 맞는 줄을 먼저, 없으면 속성이 맞는 줄을 씁니다.")]
        private List<MonsterSkillBinding> _monsterSkills = new List<MonsterSkillBinding>();
        [Header("In-game Slash Cutscene")]
        [SerializeField] private PlayableDirector _slashCutscenePrefab;

        private SkillSequencePlayback _playback;
        private SkillSequenceAsset _currentSequence;

        private int _hitCount;

        private TutorialSlashCameraTimeline _slashCamera;
        private TutorialSlashCutscenePool _slashPool;
        private float _elapsed;
        private float _duration;

        public bool IsPlaying => _playback != null;
        public SkillSequenceAsset CurrentSequence => _playback != null ? _currentSequence : null;
        public float CurrentTime => _playback != null ? _playback.Time : 0f;

        /// <summary>재생 중인 시퀀스의 타격 지점에 닿았을 때(타격 번호, 전체 타격 수). 피해 표시를 타격에 맞추는 데 쓴다.</summary>
        public event Action<int, int> HitReached;
        public bool IsSlashCameraActive => _slashCamera != null && _slashCamera.IsActive;

        public static double GetSlashCameraEnd(TimelineAsset timeline)
        {
            return timeline.GetOutputTracks().OfType<CinemachineTrack>().Select(track => track.end).DefaultIfEmpty().Max();
        }

        public SkillSequenceAsset FindPlayerSequence(string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                return null;
            }
            foreach (PlayerSkillBinding binding in _playerSkills)
            {
                if (binding != null && binding.Sequence != null && string.Equals(binding.SkillId, skillId, StringComparison.OrdinalIgnoreCase))
                {
                    return binding.Sequence;
                }
            }
            return null;
        }

        public SkillSequenceAsset FindMonsterSequence(MonsterBattleDefinition definition)
        {
            if (definition != null)
            {
                foreach (MonsterSkillBinding binding in _monsterSkills)
                {
                    if (binding != null && binding.Sequence != null && binding.Definition == definition)
                    {
                        return binding.Sequence;
                    }
                }
                BattleElement element = definition.Element;
                foreach (MonsterSkillBinding binding in _monsterSkills)
                {
                    if (binding != null && binding.Sequence != null && binding.Definition == null && binding.Element == element)
                    {
                        return binding.Sequence;
                    }
                }
            }
            foreach (MonsterSkillBinding binding in _monsterSkills)
            {
                if (binding != null && binding.Sequence != null)
                {
                    return binding.Sequence;
                }
            }
            return FindPlayerSequence("SK00") ?? FindPlayerSequence("SK02") ?? FindPlayerSequence("SK01");
        }

        /// <summary>
        /// 시퀀스를 재생하고 길이(초)를 돌려준다. 이미 재생 중이면 끊고 새로 시작한다.
        /// 몬스터가 '적 스킬'이 아닌(플레이어용) 시퀀스를 쓰면 시전자 클립(푸시아 모션)을 덮어쓰지 않고
        /// 이동·이펙트만 재생해, 몬스터 자신의 공격 모션(MonsterAnimationDriver의 Attack)이 보이게 한다.
        /// </summary>
        public float Play(SkillSequenceAsset sequence, Transform caster, IList<Transform> targets, int selectedTargetIndex,
            bool casterIsMonster = false)
        {
            Abort();
            if (sequence == null || caster == null)
            {
                return 0f;
            }
            var context = new SkillSequenceContext(caster, targets, null, selectedTargetIndex);
            _duration = sequence.Duration;
            if (!casterIsMonster && sequence.UseMovement && sequence.Cutscene == null
                && _slashCutscenePrefab != null && _slashCutscenePrefab.playableAsset is TimelineAsset timeline
                && context.SelectedTarget != null
                && TryGetComponent(out TutorialBattleController controller))
            {
                double cameraEnd = GetSlashCameraEnd(timeline);
                if (cameraEnd > 0d && sequence.ReturnEnd > sequence.ApproachStart)
                {
                    _slashPool ??= new TutorialSlashCutscenePool(_slashCutscenePrefab, transform);
                    _slashCamera = new TutorialSlashCameraTimeline(_slashPool, timeline, sequence,
                        controller.BattleCamera, controller.BattleBrain, context);
                    // 히트스톱 동안 시퀀스 시간이 거의 멈추므로 그만큼 실시간 길이가 늘어난다.
                    _duration = Mathf.Max(_duration + _slashCamera.ExtraRealSeconds, _slashCamera.Duration);
                }
            }
            _elapsed = 0f;
            _currentSequence = sequence;
            _playback = new SkillSequencePlayback(sequence, context,
                playCasterClip: !casterIsMonster || sequence.IsEnemySkill,
                slashTimelineControlsVfx: _slashCamera != null);
            _hitCount = sequence.HitTimes.Count;
            _playback.HitReached += OnHitReached;
            return _duration;
        }

        /// <summary>재생 중인 시퀀스를 멈추고 시전자를 제자리로 돌린다.</summary>
        public void Abort()
        {
            _slashCamera?.Dispose();
            _slashCamera = null;
            if (_playback == null)
            {
                _currentSequence = null;
                return;
            }
            _playback.HitReached -= OnHitReached;
            _playback.Dispose();
            _playback = null;
            _currentSequence = null;
        }

        private void OnHitReached(int index)
        {
            HitReached?.Invoke(index, _hitCount);
        }

        // Animator가 이번 프레임 포즈를 계산한 뒤에 시퀀스 포즈를 덮어써야 보이므로 LateUpdate에서 진행한다.
        private void LateUpdate()
        {
            if (_playback == null)
            {
                return;
            }
            float previousElapsed = _elapsed;
            _elapsed += Time.deltaTime;
            // 히트스톱 구간에서는 시퀀스(플레이어 이동·애니메이션·이펙트) 시계가 거의 멈춘다.
            float sequenceDelta = _slashCamera != null
                ? _slashCamera.ToSequenceTime(_elapsed) - _slashCamera.ToSequenceTime(previousElapsed)
                : Time.deltaTime;
            _playback.Advance(sequenceDelta);
            _slashCamera?.Sample(_elapsed);
            if (_elapsed >= _duration && _playback.IsFinished)
            {
                Abort();
            }
        }

        private void OnDisable()
        {
            Abort();
        }

        private void OnDestroy()
        {
            _slashPool?.Dispose();
        }
    }
}
