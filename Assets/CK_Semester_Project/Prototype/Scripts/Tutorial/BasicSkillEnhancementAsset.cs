using System;
using System.Collections.Generic;
using System.Linq;
using CK.SemesterProject.Battle;
using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    [CreateAssetMenu(menuName = "Battle/Basic Skill Enhancement")]
    public sealed class BasicSkillEnhancementAsset : ScriptableObject
    {
        private const int StageCount = 3;

        [Serializable]
        private sealed class SkillStages
        {
            [SerializeField, Tooltip("Skill_DT의 기본 스킬 ID. 대소문자를 유지합니다")]
            private string _skillId;
            [SerializeField, Tooltip("Skill_DT의 1·2·3단계 스킬 ID. 지정 시 각 행의 수치를 그대로 사용합니다")]
            private string[] _stageSkillIds;
            [SerializeField, Tooltip("기존 3행 Skill_DT용 단계별 공격력. 9행 테이블에서는 각 단계 행의 공격력을 사용합니다")]
            private int[] _stagePowers = { 100, 120, 150 };
            [SerializeField, Tooltip("1·2·3단계 공격 애니메이션. 비어 있으면 기존 임시 연출을 사용합니다")]
            private AnimationClip[] _animations = new AnimationClip[StageCount];
            [SerializeField, Tooltip("1·2·3단계 타격 이펙트 프리팹. 비어 있으면 이펙트를 생성하지 않습니다")]
            private GameObject[] _effects = new GameObject[StageCount];

            public string SkillId => _skillId;
            public string[] StageSkillIds => _stageSkillIds;

            public bool Matches(string skillId)
            {
                return _skillId == skillId || (_stageSkillIds != null && _stageSkillIds.Contains(skillId));
            }

            public SkillData CreateSkill(SkillData source, int stage)
            {
                if (_stagePowers == null || _stagePowers.Length != StageCount
                    || _stagePowers.Any(power => power < 0))
                {
                    throw new ArgumentException("기본 스킬 " + _skillId + ": 단계별 공격력 3개의 0 이상 값이 필요합니다.");
                }
                return source.WithPower(_stagePowers[stage - 1]);
            }

            public AnimationClip GetAnimation(int stage)
            {
                return _animations != null && _animations.Length >= stage ? _animations[stage - 1] : null;
            }

            public GameObject GetEffect(int stage)
            {
                return _effects != null && _effects.Length >= stage ? _effects[stage - 1] : null;
            }
        }

        [SerializeField, Min(1), Tooltip("다음 강화 단계에 필요한 승리 횟수. 5면 5승에 2단계, 10승에 3단계")]
        private int _victoriesPerStage = 5;
        [SerializeField, Tooltip("기본 스킬 3종의 ID와 단계별 공격력·연출 연결")]
        private SkillStages[] _skills;

        public SkillEnhancementRules CreateRules()
        {
            return new SkillEnhancementRules(_victoriesPerStage);
        }

        public SkillData[] CreateSkills(IReadOnlyList<SkillData> source, int victories)
        {
            return CreateSkillsForStage(source, CreateRules().GetStage(victories));
        }

        public SkillData[] CreateSkillsForStage(IReadOnlyList<SkillData> source, int stage)
        {
            ValidateStage(stage);
            if (_skills != null && source != null && source.Count == 9)
            {
                if (_skills.Any(skill => skill == null))
                {
                    throw new ArgumentException("기본 스킬 강화 데이터: 단계 설정이 비어 있습니다.");
                }
                return SkillTable.SelectStage(source, _skills.Select(skill => skill.StageSkillIds), stage);
            }
            if (_skills == null || source == null || _skills.Length != source.Count
                || _skills.Any(skill => skill == null || string.IsNullOrWhiteSpace(skill.SkillId))
                || _skills.Select(skill => skill.SkillId).Distinct(StringComparer.Ordinal).Count() != _skills.Length)
            {
                throw new ArgumentException("기본 스킬 강화 데이터: Skill_DT와 같은 수의 고유한 스킬 ID가 필요합니다.");
            }
            var result = new SkillData[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                SkillStages settings = _skills.FirstOrDefault(skill => skill.SkillId == source[i].Id);
                if (settings == null)
                {
                    throw new ArgumentException("기본 스킬 강화 데이터: " + source[i].Id + "의 단계 설정이 없습니다.");
                }
                result[i] = settings.CreateSkill(source[i], stage);
            }
            return result;
        }

        public AnimationClip GetAnimation(string skillId, int stage)
        {
            ValidateStage(stage);
            return _skills?.FirstOrDefault(skill => skill != null && skill.Matches(skillId))?.GetAnimation(stage);
        }

        public GameObject GetEffect(string skillId, int stage)
        {
            ValidateStage(stage);
            return _skills?.FirstOrDefault(skill => skill != null && skill.Matches(skillId))?.GetEffect(stage);
        }

        private static void ValidateStage(int stage)
        {
            if (stage < 1 || stage > StageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(stage));
            }
        }
    }
}
