using System.Collections.Generic;
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
        private const float EffectTailSeconds = .9f;
        private const float DefaultSlashLeadFraction = .12f;
        private readonly List<GameObject> _effectTails = new List<GameObject>();
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
        private bool _movesActor;
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
        private GameObject _chargeInstance;
        private float _chargeFraction;
        private float _impactTime = .35f;
        private float _returnTime = .55f;
        private float _effectTailSeconds = EffectTailSeconds;
        private bool _hasSpawnedEffect;
        private bool _isSocketSlash;
        private float _slashStartTime;
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
            if (_animator == null)
            {
                _animator = actor.GetComponentsInChildren<Animator>(true).FirstOrDefault(item => item.isActiveAndEnabled);
            }
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

        // approachTarget이 false면 대상 앞까지 다가갔다 돌아오지 않고 제자리에서 대상을 바라본다(동작이 모션 안에 다 들어 있는 궁극기 등).
        public void Sample(BattleActionResult result, float progress, bool approachTarget = true, float presentationSeconds = 1.2f, bool playAnimation = true)
        {
            bool isAttack = result != null && !result.WasSkipped && !result.IsTurnStartEffect
                && result.Request.Kind == BattleActionKind.Skill && result.Request.ActorId == _actorId
                && (result.Request.TargetId == _targetId || result.TargetHits.ContainsKey(_targetId));
            if (!isAttack || progress >= 1f || _actor == null || _target == null)
            {
                Restore(isAttack && progress >= 1f);
                return;
            }
            _hasApplied = true;
            _movesActor = result.Request.TargetId == _targetId;
            progress = Mathf.Clamp01(progress);
            if (_actionId != result.ActionId)
            {
                if (_actionId != 0)
                {
                    Restore(false);
                    _hasApplied = true;
                }
                _actionId = result.ActionId;
                _animation = _enhancement != null ? _enhancement.GetAnimation(result.Request.SkillId, _skillStage) : null;
                if (_animation == null && _enhancement != null)
                {
                    _animation = _enhancement.GetAnimation("SK00", _skillStage);
                }
                _effectPrefab = _enhancement != null ? _enhancement.GetEffect(result.Request.SkillId, _skillStage) : null;
                _hasSpawnedEffect = false;
                // TODO: 슬래시 이펙트 재제작 후 SocketSlashEffect 기반 칼날 궤적·타격 분리 연출을 다시 연결한다.
                _isSocketSlash = _effectPrefab != null && _effectPrefab.GetComponent<TutorialSlashVfxPlacement>() != null;
                if (_movesActor && playAnimation)
                {
                    BeginAnimation();
                }
                string timingSkillId = result.Request.SkillId;
                if (_enhancement != null && _enhancement.GetAnimation(timingSkillId, _skillStage) == null)
                {
                    timingSkillId = "SK00";
                }
                GameObject chargePrefab = _enhancement != null ? _enhancement.GetChargeEffect(timingSkillId) : null;
                _chargeFraction = chargePrefab != null
                    ? _enhancement.GetChargeProgressFraction(timingSkillId, _skillStage, presentationSeconds) : 0f;
                _impactTime = _enhancement != null ? _enhancement.GetImpactFraction(timingSkillId, _skillStage) : .35f;
                if (!playAnimation && _actorId != "player")
                {
                    // 몬스터 기본 공격 시퀀스(Sample_BasicAttack_*_Slash, 3.35s 중 타격 약 2.05s)와 피격 타이밍을 맞춘다.
                    _chargeFraction = 0f;
                    _impactTime = 0.61f;
                }
                _returnTime = Mathf.Min(.98f, Mathf.Max(.55f, _impactTime + .18f));
                _slashStartTime = Mathf.Max(0f, _impactTime - DefaultSlashLeadFraction);
                if (playAnimation && chargePrefab != null && progress < _chargeFraction)
                {
                    _chargeInstance = Object.Instantiate(chargePrefab, _actor);
                    _chargeInstance.transform.localPosition = Vector3.zero;
                    _chargeInstance.transform.localRotation = Quaternion.identity;
                    SkillVfxPlacement manualCharge = _enhancement.GetPlacement(timingSkillId, _skillStage, true);
                    if (manualCharge.Enabled)
                    {
                        _chargeInstance.transform.SetParent(null, true);
                        manualCharge.Apply(_chargeInstance, _actor, _target);
                    }
                    TutorialChargeVfxPlacement chargePlacement = _chargeInstance.GetComponent<TutorialChargeVfxPlacement>();
                    if (chargePlacement != null && !manualCharge.Enabled)
                    {
                        chargePlacement.Initialize(Camera.main);
                    }
                    _chargeInstance.AddComponent<SkillVfxPlayback>().Initialize(manualCharge, _enhancement.ChargePlaybackSpeed);
                }
            }
            bool isCharging = progress < _chargeFraction;
            if (!isCharging && _chargeInstance != null)
            {
                _chargeInstance.SetActive(false);
                Object.Destroy(_chargeInstance);
                _chargeInstance = null;
            }
            // 차징 동안 원래 자리에서 대기하고 이후에만 기존 접근·베기·복귀 연출을 진행한다.
            progress = Mathf.Clamp01((progress - _chargeFraction) / (1f - _chargeFraction));
            if (playAnimation && _animationGraph.IsValid())
            {
                _animationPlayable.SetTime(progress * _animation.length);
                _animationGraph.Evaluate(0f);
            }
            float approach = progress < _impactTime ? Mathf.SmoothStep(0f, 1f, progress / _impactTime)
                : progress < _returnTime ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (progress - _returnTime) / (1f - _returnTime));
            bool primaryTarget = result.Request.TargetId == _targetId;
            if (primaryTarget && approachTarget)
            {
                _actor.position = Vector3.Lerp(ActorHome, _attackPosition, approach);
            }
            Vector3 facing = Vector3.ProjectOnPlane(TargetHome - ActorHome, Vector3.up);
            if (primaryTarget && facing.sqrMagnitude > 0.001f)
            {
                _actor.rotation = Quaternion.LookRotation(facing);
            }
            float effectStartTime = _isSocketSlash ? _slashStartTime : _impactTime;
            if (playAnimation && !_hasSpawnedEffect && !isCharging && progress >= effectStartTime)
            {
                _hasSpawnedEffect = true;
                if (_effectPrefab != null)
                {
                    SkillVfxPlacement manualEffect = _enhancement.GetPlacement(result.Request.SkillId, _skillStage, false);
                    if (_isSocketSlash)
                    {
                        SpawnSocketSlash(manualEffect);
                    }
                    else
                    {
                        _effectInstance = Object.Instantiate(_effectPrefab, _target.position, _target.rotation);
                        if (manualEffect.Enabled)
                        {
                            manualEffect.Apply(_effectInstance, _actor, _target);
                        }
                    }
                    if (_effectInstance != null)
                    {
                        _effectInstance.AddComponent<SkillVfxPlayback>().Initialize(manualEffect);
                        _effectTailSeconds = Mathf.Max(EffectTailSeconds, Mathf.Max(0f, manualEffect.StartDelay)
                            + (manualEffect.Lifetime > 0f ? manualEffect.Lifetime : SkillVfxPlayback.EstimateDuration(_effectInstance)));
                    }
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
            float reaction = Mathf.Clamp01((progress - _impactTime) / (_returnTime - _impactTime));
            BattleHitResult targetHit = result.TargetHits.TryGetValue(_targetId, out BattleHitResult foundHit) ? foundHit : result.Hit;
            bool reacts = tookDamage && targetHit != null && targetHit.IsHit
                && progress >= _impactTime && progress < _returnTime;
            float strength = reacts ? 1f - reaction : 0f;
            Vector3 forward = Vector3.ProjectOnPlane(TargetHome - ActorHome, Vector3.up).normalized;
            float recoil = reacts ? Mathf.Sin(Mathf.Pow(reaction, 0.55f) * Mathf.PI) * 0.14f : 0f;
            _target.position = TargetHome + forward * recoil;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null)
                {
                    continue;
                }
                if (strength > 0f)
                {
                    Color flash = Color.Lerp(_baseColors[i], new Color(1f, 0.15f, 0.15f), strength);
                    _flashBlocks[i].SetColor(_baseColorId, flash);
                    _flashBlocks[i].SetColor(_colorId, flash);
                    _renderers[i].SetPropertyBlock(_flashBlocks[i]);
                }
                else
                {
                    _renderers[i].SetPropertyBlock(_originalBlocks[i]);
                }
            }
            if (_targetId == "player")
            {
                if (reacts && strength > 0f)
                {
                    float envelope = Mathf.Pow(1f - reaction, 2.2f);
                    float phase = reaction * 28f;
                    float noiseX = (Mathf.PerlinNoise(phase, 1.37f) - 0.5f) * 2f;
                    float noiseY = (Mathf.PerlinNoise(3.91f, phase) - 0.5f) * 2f;
                    float punch = Mathf.Sin(reaction * Mathf.PI * 5f);
                    Vector3 shakeOffset = new Vector3(
                        (noiseX * 0.65f + punch * 0.35f) * 0.055f * envelope,
                        (noiseY * 0.6f - Mathf.Abs(punch) * 0.4f) * 0.042f * envelope,
                        -0.045f * envelope
                    );
                    Vector3 shakeRotation = new Vector3(
                        (Mathf.Abs(punch) * 0.85f + noiseY * 0.35f) * 1.4f * envelope,
                        noiseX * 0.9f * envelope,
                        (punch * 0.7f + noiseX * 0.3f) * 1.6f * envelope
                    );
                    CK.SemesterProject.SkillSequence.SkillCameraShake.SetOffset(Camera.main, shakeOffset, shakeRotation);
                }
                else
                {
                    CK.SemesterProject.SkillSequence.SkillCameraShake.Clear();
                }
            }
        }

        public void Restore()
        {
            Restore(false);
        }

        private void Restore(bool letEffectFinish)
        {
            if (!letEffectFinish)
            {
                foreach (GameObject tail in _effectTails)
                {
                    if (tail != null)
                    {
                        Object.Destroy(tail);
                    }
                }
                _effectTails.Clear();
            }
            if (!_hasApplied)
            {
                return;
            }
            if (_effectInstance != null)
            {
                if (letEffectFinish)
                {
                    TutorialSlashVfxPlacement placement = _effectInstance.GetComponent<TutorialSlashVfxPlacement>();
                    if (placement != null)
                    {
                        placement.enabled = false;
                    }
                    QueueTail(_effectInstance);
                }
                else
                {
                    Object.Destroy(_effectInstance);
                }
                _effectInstance = null;
            }
            if (_chargeInstance != null)
            {
                Object.Destroy(_chargeInstance);
                _chargeInstance = null;
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
            if (_movesActor && _actor != null)
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
            _isSocketSlash = false;
            _chargeFraction = 0f;
            if (_targetId == "player")
            {
                CK.SemesterProject.SkillSequence.SkillCameraShake.Clear();
            }
        }

        private void SpawnSocketSlash(SkillVfxPlacement settings)
        {
            Transform socket = WeaponEffectSocket.Find(_actor);
            if (socket == null)
            {
                Debug.LogError("칼날 소켓(BladeSocket)을 찾지 못해 슬래시를 생성하지 않았습니다: " + _actor.name, _actor);
                return;
            }
            _effectInstance = Object.Instantiate(_effectPrefab, socket, false);
            if (settings.Enabled)
            {
                // A slash's authored offsets are socket-local even for older Actor/Target placement records.
                settings.Anchor = SkillVfxAnchor.BladeSocket;
                settings.Follow = true;
                settings.Apply(_effectInstance, _actor, _target);
            }
        }

        private void QueueTail(GameObject tail)
        {
            if (tail == null)
            {
                return;
            }
            _effectTails.RemoveAll(item => item == null);
            _effectTails.Add(tail);
            Object.Destroy(tail, _effectTailSeconds);
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
