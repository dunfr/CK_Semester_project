using System;
using System.Collections.Generic;
using System.Linq;
using CK.SemesterProject.Battle;
using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    // 기본 스킬의 각성 전(1단계)·각성 후(2단계) 데이터와 연출을 연결한다.
    [CreateAssetMenu(menuName = "Battle/Basic Skill Enhancement")]
    public sealed class BasicSkillEnhancementAsset : ScriptableObject
    {
        private const int StageCount = 2;

        [Serializable]
        private sealed class SkillStages
        {
            [SerializeField, Tooltip("Skill_DT의 기본 스킬 ID. 대소문자를 유지합니다")]
            private string _skillId;
            [SerializeField, Tooltip("Skill_DT의 각성 전·각성 후 스킬 ID. 각 행의 수치를 그대로 사용합니다")]
            private string[] _stageSkillIds;
            [SerializeField, Tooltip("각성 전·후 공격 애니메이션. 비어 있으면 기존 임시 연출을 사용합니다")]
            private AnimationClip[] _animations = new AnimationClip[StageCount];
            [SerializeField, Tooltip("각성 전·후 타격 이펙트 프리팹. 비어 있으면 이펙트를 생성하지 않습니다")]
            private GameObject[] _effects = new GameObject[StageCount];
            [SerializeField, Tooltip("공격 전에 캐릭터를 기준으로 재생하는 차징 이펙트")]
            private GameObject _chargeEffect;
            [SerializeField] private SkillVfxPlacement[] _effectPlacements = new SkillVfxPlacement[StageCount];
            [SerializeField] private SkillVfxPlacement _chargePlacement;
            [SerializeField] private float[] _actionSpeeds = new float[StageCount];
            [SerializeField] private float[] _impactFractions = new float[StageCount];

            public string SkillId => _skillId;
            public string[] StageSkillIds => _stageSkillIds;
            public GameObject ChargeEffect => _chargeEffect;

            public bool Matches(string skillId)
            {
                return _skillId == skillId || (_stageSkillIds != null && _stageSkillIds.Contains(skillId));
            }

            public int GetStage(string skillId)
            {
                return _stageSkillIds == null ? 1 : Math.Max(1, Array.IndexOf(_stageSkillIds, skillId) + 1);
            }

            public AnimationClip GetAnimation(int stage)
            {
                return _animations != null && _animations.Length >= stage ? _animations[stage - 1] : null;
            }

            public GameObject GetEffect(int stage)
            {
                return _effects != null && _effects.Length >= stage ? _effects[stage - 1] : null;
            }

            public float GetActionSpeed(int stage)
            {
                float speed = _actionSpeeds != null && _actionSpeeds.Length >= stage ? _actionSpeeds[stage - 1] : 0f;
                return speed > 0f ? Mathf.Clamp(speed, .05f, 4f) : 1f;
            }

            public float GetImpactFraction(int stage)
            {
                float impact = _impactFractions != null && _impactFractions.Length >= stage ? _impactFractions[stage - 1] : 0f;
                return impact > 0f ? Mathf.Clamp(impact, .05f, .9f) : .35f;
            }

            public void SetActionSettings(int stage, AnimationClip animation, float speed, float impact)
            {
                Array.Resize(ref _animations, StageCount);
                Array.Resize(ref _actionSpeeds, StageCount);
                Array.Resize(ref _impactFractions, StageCount);
                _animations[stage - 1] = animation;
                _actionSpeeds[stage - 1] = Mathf.Clamp(speed, .05f, 4f);
                _impactFractions[stage - 1] = Mathf.Clamp(impact, .05f, .9f);
            }

            public SkillVfxPlacement GetPlacement(int stage, bool charge)
            {
                return charge ? _chargePlacement : _effectPlacements != null && _effectPlacements.Length >= stage
                    ? _effectPlacements[stage - 1] : default;
            }

            public void SetPlacement(int stage, bool charge, GameObject prefab, SkillVfxPlacement placement)
            {
                if (charge)
                {
                    _chargeEffect = prefab;
                    _chargePlacement = placement;
                    return;
                }
                if (_effects == null || _effects.Length != StageCount)
                {
                    Array.Resize(ref _effects, StageCount);
                }
                if (_effectPlacements == null || _effectPlacements.Length != StageCount)
                {
                    Array.Resize(ref _effectPlacements, StageCount);
                }
                _effects[stage - 1] = prefab;
                _effectPlacements[stage - 1] = placement;
            }
        }

        [SerializeField, Min(0), Tooltip("기본 스킬 1개를 각성하는 데 소모하는 BP. 승리 1회당 1 BP")]
        private int _awakeningCost = 3;
        [SerializeField, Tooltip("기본 스킬 3종의 각성 전·후 ID와 연출 연결")]
        private SkillStages[] _skills;
        [SerializeField, Tooltip("각성 없이 연출만 연결하는 스킬(특수스킬·궁극기 등). 공격력은 Skill_DT 값을 그대로 씁니다")]
        private SkillStages[] _extraSkills = new SkillStages[0];
        [SerializeField, Range(0.05f, 0.45f), Tooltip("전체 행동 시간 중 공격 전 차징에 사용하는 비율(0~1)")]
        private float _chargeFraction = .25f;
        [SerializeField, Range(.1f, 2f), Tooltip("차징 이펙트 재생 속도(배). 0.5는 절반 속도이며 차징 시간은 두 배가 됩니다")]
        private float _chargePlaybackSpeed = .5f;

        public float ChargeFraction => _chargeFraction;
        public float ChargePlaybackSpeed => _chargePlaybackSpeed;
        public float ChargeDurationScale => 1f + _chargeFraction * (1f / _chargePlaybackSpeed - 1f);
        public float ScaledChargeFraction => _chargeFraction / _chargePlaybackSpeed / ChargeDurationScale;

        public int AwakeningCost => _awakeningCost;
        // 각성 대상인 기본 스킬의 각성 전 ID. PlayerRuntimeState.AwakenedSkillIds와 같은 키를 쓴다.
        public IEnumerable<string> BasicSkillIds => (_skills ?? Array.Empty<SkillStages>())
            .Where(skill => skill != null).Select(skill => skill.SkillId);

        // 기본 스킬 3종(각성 여부 반영) 뒤에 나머지 스킬(특수·궁극기)을 원래 순서대로 붙인다.
        public SkillData[] CreateSkills(IReadOnlyList<SkillData> source, IReadOnlyCollection<string> awakenedIds)
        {
            if (_skills == null || source == null || _skills.Any(skill => skill == null || skill.StageSkillIds == null))
            {
                throw new ArgumentException("기본 스킬 각성 데이터: 각성 전·후 ID 설정이 비어 있습니다.");
            }
            string[] basicIds = _skills.SelectMany(skill => skill.StageSkillIds).ToArray();
            SkillData[] basic = source.Where(skill => basicIds.Contains(skill.Id)).ToArray();
            return SkillTable.SelectAwakening(basic, _skills.Select(skill => skill.StageSkillIds), awakenedIds)
                .Concat(source.Where(skill => !basicIds.Contains(skill.Id))).ToArray();
        }

        // 스킬 ID가 각성 후 ID면 2, 그 외(각성 전·특수·궁극기·몬스터)는 1.
        public int GetStage(string skillId)
        {
            return Find(skillId)?.GetStage(skillId) ?? 1;
        }

        public AnimationClip GetAnimation(string skillId, int stage)
        {
            ValidateStage(stage);
            return Find(skillId)?.GetAnimation(stage);
        }

        public GameObject GetEffect(string skillId, int stage)
        {
            ValidateStage(stage);
            return Find(skillId)?.GetEffect(stage);
        }

        public GameObject GetChargeEffect(string skillId)
        {
            return Find(skillId)?.ChargeEffect;
        }

        public SkillVfxPlacement GetPlacement(string skillId, int stage, bool charge)
        {
            ValidateStage(stage);
            return Find(skillId)?.GetPlacement(stage, charge) ?? default;
        }

        public void SetPlacement(string skillId, int stage, bool charge, GameObject prefab, SkillVfxPlacement placement)
        {
            ValidateStage(stage);
            SkillStages settings = Find(skillId);
            if (settings == null)
            {
                throw new ArgumentException("이펙트 배치: 스킬 ID를 찾을 수 없습니다: " + skillId);
            }
            settings.SetPlacement(stage, charge, prefab, placement);
        }

        public float GetActionSpeed(string skillId, int stage)
        {
            ValidateStage(stage);
            return Find(skillId)?.GetActionSpeed(stage) ?? 1f;
        }

        public float GetImpactFraction(string skillId, int stage)
        {
            ValidateStage(stage);
            return Find(skillId)?.GetImpactFraction(stage) ?? .35f;
        }

        public void SetActionSettings(string skillId, int stage, AnimationClip animation, float speed, float impact)
        {
            ValidateStage(stage);
            SkillStages settings = Find(skillId);
            if (settings == null)
            {
                throw new ArgumentException("공격 연출: 스킬 ID를 찾을 수 없습니다: " + skillId);
            }
            settings.SetActionSettings(stage, animation, speed, impact);
        }

        public float GetPresentationSeconds(string skillId, int stage, float baseSeconds)
        {
            float seconds = baseSeconds / GetActionSpeed(skillId, stage);
            if (GetChargeEffect(skillId) == null)
            {
                return seconds;
            }
            SkillVfxPlacement charge = GetPlacement(skillId, stage, true);
            float chargeSpeed = ChargePlaybackSpeed * charge.EffectivePlaybackSpeed;
            return seconds * (1f - ChargeFraction + ChargeFraction / chargeSpeed) + Mathf.Max(0f, charge.StartDelay);
        }

        public float GetChargeProgressFraction(string skillId, int stage, float presentationSeconds)
        {
            if (GetChargeEffect(skillId) == null)
            {
                return 0f;
            }
            SkillVfxPlacement charge = GetPlacement(skillId, stage, true);
            float delay = Mathf.Max(0f, charge.StartDelay);
            float chargePart = ChargeFraction / (ChargePlaybackSpeed * charge.EffectivePlaybackSpeed);
            float originalSeconds = Mathf.Max(0f, presentationSeconds - delay) / (1f - ChargeFraction + chargePart);
            return Mathf.Clamp01((originalSeconds * chargePart + delay) / Mathf.Max(.001f, presentationSeconds));
        }

        // 기본 스킬을 먼저 찾고, 없으면 강화 없는 연출 전용 스킬(특수·궁극기)에서 찾는다.
        private SkillStages Find(string skillId)
        {
            return _skills?.FirstOrDefault(skill => skill != null && skill.Matches(skillId))
                ?? _extraSkills?.FirstOrDefault(skill => skill != null && skill.Matches(skillId));
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
