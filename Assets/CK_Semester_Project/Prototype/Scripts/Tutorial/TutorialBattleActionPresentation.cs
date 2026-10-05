using System.Linq;
using CK.SemesterProject.Battle;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CK.SemesterProject.Tutorial
{
    // 임시 전투 연출. HP/턴 계산에는 관여하지 않으며 전투 자리로 반드시 복귀한다.
    public sealed class TutorialBattleActionPresentation
    {
        private const float ImpactTime = 0.35f;
        private const float ReturnTime = 0.55f;
        private readonly Transform _actor;
        private readonly Transform _target;
        private readonly Vector3 _attackPosition;
        private readonly Renderer[] _renderers;
        private readonly MaterialPropertyBlock[] _originalBlocks;
        private readonly MaterialPropertyBlock[] _flashBlocks;
        private readonly Color[] _baseColors;
        private readonly int _baseColorId = Shader.PropertyToID("_BaseColor");
        private readonly int _colorId = Shader.PropertyToID("_Color");
        private bool _hasApplied;
        private readonly string _targetId;
        private readonly string _actorId;
        private readonly Quaternion _actorRotation;
        private readonly BasicSkillEnhancementAsset _enhancement;
        private readonly int _skillStage;
        private readonly Animator _animator;
        private long _actionId;
        private AnimationClip _animation;
        private GameObject _effectPrefab;
        private GameObject _effectInstance;
        private bool _hasSpawnedEffect;
        private PlayableGraph _animationGraph;
        private AnimationClipPlayable _animationPlayable;
        private Transform[] _animatedTransforms;
        private Vector3[] _localPositions;
        private Quaternion[] _localRotations;
        private Vector3[] _localScales;

        public Vector3 ActorHome { get; }
        public Vector3 TargetHome { get; }

        public TutorialBattleActionPresentation(Transform actor, Transform target, float stopDistance,
            string targetId = "monster", string actorId = "player",
            BasicSkillEnhancementAsset enhancement = null, int skillStage = 1)
        {
            _enhancement = enhancement;
            _skillStage = skillStage;
            _animator = actor.GetComponent<Animator>();
            _actorId = actorId;
            _actorRotation = actor.rotation;
            _targetId = targetId;
            _actor = actor;
            _target = target;
            ActorHome = actor.position;
            TargetHome = target.position;
            Vector3 direction = (TargetHome - ActorHome).normalized;
            float travel = Mathf.Max(0f, Vector3.Distance(ActorHome, TargetHome) - stopDistance);
            _attackPosition = ActorHome + direction * travel;
            foreach (RaycastHit hit in Physics.CapsuleCastAll(ActorHome + Vector3.up * 0.4f,
                ActorHome + Vector3.up * 1.4f, 0.3f, direction, travel, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(actor) && !hit.transform.IsChildOf(target))
                {
                    _attackPosition = ActorHome;
                    break;
                }
            }
            _renderers = target.GetComponentsInChildren<Renderer>()
                .Where(renderer => renderer.GetComponent<TMPro.TMP_Text>() == null && renderer.GetComponent<TextMesh>() == null).ToArray();
            _originalBlocks = new MaterialPropertyBlock[_renderers.Length];
            _flashBlocks = new MaterialPropertyBlock[_renderers.Length];
            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _originalBlocks[i] = new MaterialPropertyBlock();
                _flashBlocks[i] = new MaterialPropertyBlock();
                _renderers[i].GetPropertyBlock(_originalBlocks[i]);
                _renderers[i].GetPropertyBlock(_flashBlocks[i]);
                Material material = _renderers[i].sharedMaterial;
                _baseColors[i] = material != null && material.HasProperty(_baseColorId)
                    ? material.GetColor(_baseColorId)
                    : material != null && material.HasProperty(_colorId) ? material.GetColor(_colorId) : Color.gray;
            }
        }

        public void Sample(BattleActionResult result, float progress)
        {
            bool isAttack = result != null && !result.WasSkipped && !result.IsTurnStartEffect
                && result.Request.Kind == BattleActionKind.Skill && result.Request.ActorId == _actorId
                && result.Request.TargetId == _targetId;
            if (!isAttack || progress >= 1f || _actor == null || _target == null)
            {
                Restore();
                return;
            }
            _hasApplied = true;
            progress = Mathf.Clamp01(progress);
            if (_actionId != result.ActionId)
            {
                _actionId = result.ActionId;
                _animation = _enhancement != null ? _enhancement.GetAnimation(result.Request.SkillId, _skillStage) : null;
                _effectPrefab = _enhancement != null ? _enhancement.GetEffect(result.Request.SkillId, _skillStage) : null;
                _hasSpawnedEffect = false;
                BeginAnimation();
            }
            if (_animationGraph.IsValid())
            {
                _animationPlayable.SetTime(progress * _animation.length);
                _animationGraph.Evaluate(0f);
            }
            float approach = progress < ImpactTime ? Mathf.SmoothStep(0f, 1f, progress / ImpactTime)
                : progress < ReturnTime ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (progress - ReturnTime) / (1f - ReturnTime));
            _actor.position = Vector3.Lerp(ActorHome, _attackPosition, approach);
            Vector3 facing = Vector3.ProjectOnPlane(TargetHome - ActorHome, Vector3.up);
            if (facing.sqrMagnitude > 0.001f)
            {
                _actor.rotation = Quaternion.LookRotation(facing);
            }
            if (!_hasSpawnedEffect && progress >= ImpactTime)
            {
                _hasSpawnedEffect = true;
                if (_effectPrefab != null)
                {
                    _effectInstance = Object.Instantiate(_effectPrefab, _target.position, _target.rotation);
                }
            }
            bool tookDamage = false;
            foreach (BattleStateChange change in result.Changes)
            {
                if (change.After.InstanceId == _targetId && change.HpDelta < 0)
                {
                    tookDamage = true;
                    break;
                }
            }
            float reaction = Mathf.Clamp01((progress - ImpactTime) / (ReturnTime - ImpactTime));
            bool reacts = tookDamage && result.Hit != null && result.Hit.IsHit
                && progress >= ImpactTime && progress < ReturnTime;
            float strength = reacts ? 1f - reaction : 0f;
            Vector3 forward = Vector3.ProjectOnPlane(TargetHome - ActorHome, Vector3.up).normalized;
            Vector3 sideways = Vector3.Cross(Vector3.up, forward);
            _target.position = TargetHome + forward * (Mathf.Sin(reaction * Mathf.PI) * 0.18f * strength)
                + sideways * (Mathf.Sin(reaction * Mathf.PI * 8f) * 0.09f * strength);
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null)
                {
                    continue;
                }
                if (strength > 0f)
                {
                    Color flash = Color.Lerp(_baseColors[i], new Color(1f, 0.3f, 0.2f), strength);
                    _flashBlocks[i].SetColor(_baseColorId, flash);
                    _flashBlocks[i].SetColor(_colorId, flash);
                    _renderers[i].SetPropertyBlock(_flashBlocks[i]);
                }
                else
                {
                    _renderers[i].SetPropertyBlock(_originalBlocks[i]);
                }
            }
        }

        public void Restore()
        {
            if (!_hasApplied)
            {
                return;
            }
            if (_effectInstance != null)
            {
                Object.Destroy(_effectInstance);
                _effectInstance = null;
            }
            if (_animationGraph.IsValid())
            {
                _animationGraph.Destroy();
                for (int i = 0; i < _animatedTransforms.Length; i++)
                {
                    if (_animatedTransforms[i] != null)
                    {
                        _animatedTransforms[i].localPosition = _localPositions[i];
                        _animatedTransforms[i].localRotation = _localRotations[i];
                        _animatedTransforms[i].localScale = _localScales[i];
                    }
                }
            }
            if (_actor != null)
            {
                _actor.position = ActorHome;
                _actor.rotation = _actorRotation;
            }
            if (_target != null)
            {
                _target.position = TargetHome;
            }
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].SetPropertyBlock(_originalBlocks[i]);
                }
            }
            _hasApplied = false;
            _actionId = 0;
            _animation = null;
            _effectPrefab = null;
            _hasSpawnedEffect = false;
        }

        private void BeginAnimation()
        {
            if (_animation == null || _animator == null)
            {
                return;
            }
            if (_animation.legacy)
            {
                Debug.LogWarning("기본 스킬 강화: Animator용 클립을 연결하세요. Legacy 클립: " + _animation.name, _actor);
                return;
            }
            if (_animatedTransforms == null)
            {
                _animatedTransforms = _actor.GetComponentsInChildren<Transform>(true);
                _localPositions = new Vector3[_animatedTransforms.Length];
                _localRotations = new Quaternion[_animatedTransforms.Length];
                _localScales = new Vector3[_animatedTransforms.Length];
            }
            for (int i = 0; i < _animatedTransforms.Length; i++)
            {
                _localPositions[i] = _animatedTransforms[i].localPosition;
                _localRotations[i] = _animatedTransforms[i].localRotation;
                _localScales[i] = _animatedTransforms[i].localScale;
            }
            // Humanoid 클립도 같은 행동 시간에 맞춰 재생하고 원래 포즈를 복구한다.
            _animationGraph = PlayableGraph.Create("Basic Skill Enhancement");
            _animationGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _animationPlayable = AnimationClipPlayable.Create(_animationGraph, _animation);
            _animationPlayable.SetSpeed(0);
            var output = AnimationPlayableOutput.Create(_animationGraph, "Skill", _animator);
            output.SetSourcePlayable(_animationPlayable);
            _animationGraph.Play();
        }
    }
}
