using System;
using System.Collections.Generic;
using CK.SemesterProject.Battle;
using CK.SemesterProject.SkillSequence;
using UnityEngine;

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

        private SkillSequencePlayback _playback;

        public bool IsPlaying => _playback != null;

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
            _playback = new SkillSequencePlayback(sequence, context, playCasterClip: !casterIsMonster || sequence.IsEnemySkill);
            return sequence.Duration;
        }

        /// <summary>재생 중인 시퀀스를 멈추고 시전자를 제자리로 돌린다.</summary>
        public void Abort()
        {
            if (_playback == null)
            {
                return;
            }
            _playback.Dispose();
            _playback = null;
        }

        // Animator가 이번 프레임 포즈를 계산한 뒤에 시퀀스 포즈를 덮어써야 보이므로 LateUpdate에서 진행한다.
        private void LateUpdate()
        {
            if (_playback == null)
            {
                return;
            }
            _playback.Advance(Time.deltaTime);
            if (_playback.IsFinished)
            {
                Abort();
            }
        }

        private void OnDisable()
        {
            Abort();
        }
    }
}
