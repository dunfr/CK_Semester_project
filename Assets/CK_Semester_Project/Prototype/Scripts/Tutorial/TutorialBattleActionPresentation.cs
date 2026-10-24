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
        private SocketSlashEffect _socketEffect;
        private GameObject _chargeInstance;
        private float _chargeFraction;
        private float _impactTime = .35f;
        private float _returnTime = .55f;
        private float _effectTailSeconds = EffectTailSeconds;
        private bool _hasSpawnedEffect;
        private bool _isSocketSlash;
        private bool _hasTriggeredSocketImpact;
        private bool _hasFinishedSocketSwing;
        private float _slashStartTime;
        private float _slashEndTime;
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

        public void Sample(BattleActionResult result, float progress, float presentationSeconds = 1.2f)
        {
            bool isAttack = result != null && !result.WasSkipped && !result.IsTurnStartEffect
                && result.Request.Kind == BattleActionKind.Skill && result.Request.ActorId == _actorId
                && result.Request.TargetId == _targetId;
            if (!isAttack || progress >= 1f || _actor == null || _target == null)
            {
                Restore(isAttack && progress >= 1f);
                return;
            }
            _hasApplied = true;
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
                _effectPrefab = _enhancement != null ? _enhancement.GetEffect(result.Request.SkillId, _skillStage) : null;
                _hasSpawnedEffect = false;
                _hasTriggeredSocketImpact = false;
                _hasFinishedSocketSwing = false;
                _isSocketSlash = _effectPrefab != null && (_effectPrefab.GetComponent<SocketSlashEffect>() != null
                    || _effectPrefab.GetComponent<TutorialSlashVfxPlacement>() != null);
                BeginAnimation();
                GameObject chargePrefab = _enhancement != null ? _enhancement.GetChargeEffect(result.Request.SkillId) : null;
                _chargeFraction = chargePrefab != null
                    ? _enhancement.GetChargeProgressFraction(result.Request.SkillId, _skillStage, presentationSeconds) : 0f;
                _impactTime = _enhancement != null ? _enhancement.GetImpactFraction(result.Request.SkillId, _skillStage) : .35f;
                _returnTime = Mathf.Min(.98f, Mathf.Max(.55f, _impactTime + .15f));
                SocketSlashEffect socketSettings = _effectPrefab != null ? _effectPrefab.GetComponent<SocketSlashEffect>() : null;
                _slashStartTime = Mathf.Max(0f, _impactTime - (socketSettings != null ? socketSettings.SwingLeadFraction : .12f));
                _slashEndTime = Mathf.Min(.98f, _impactTime + (socketSettings != null ? socketSettings.SwingTailFraction : .12f));
                if (chargePrefab != null && progress < _chargeFraction)
                {
                    _chargeInstance = Object.Instantiate(chargePrefab, _actor);
                    _chargeInstance.transform.localPosition = Vector3.zero;
                    _chargeInstance.transform.localRotation = Quaternion.identity;
                    SkillVfxPlacement manualCharge = _enhancement.GetPlacement(result.Request.SkillId, _skillStage, true);
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
            if (_animationGraph.IsValid())
            {
                _animationPlayable.SetTime(progress * _animation.length);
                _animationGraph.Evaluate(0f);
            }
            float approach = progress < _impactTime ? Mathf.SmoothStep(0f, 1f, progress / _impactTime)
                : progress < _returnTime ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (progress - _returnTime) / (1f - _returnTime));
            _actor.position = Vector3.Lerp(ActorHome, _attackPosition, approach);
            Vector3 facing = Vector3.ProjectOnPlane(TargetHome - ActorHome, Vector3.up);
            if (facing.sqrMagnitude > 0.001f)
            {
                _actor.rotation = Quaternion.LookRotation(facing);
            }
            float effectStartTime = _isSocketSlash ? _slashStartTime : _impactTime;
            if (!_hasSpawnedEffect && !isCharging && progress >= effectStartTime)
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
            if (_socketEffect != null && !_hasTriggeredSocketImpact && progress >= _impactTime)
            {
                _hasTriggeredSocketImpact = true;
                _socketEffect.ReleaseImpact(_target, Camera.main);
            }
            if (_socketEffect != null && !_hasFinishedSocketSwing && progress >= _slashEndTime)
            {
                _hasFinishedSocketSwing = true;
                _socketEffect.FinishSwing();
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
            bool reacts = tookDamage && result.Hit != null && result.Hit.IsHit
                && progress >= _impactTime && progress < _returnTime;
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
                if (_socketEffect != null)
                {
                    _socketEffect.FinishSwing();
                    if (letEffectFinish)
                    {
                        QueueTail(_socketEffect.TakeImpact());
                    }
                    else
                    {
                        _socketEffect.ClearImpact();
                    }
                    _effectInstance.SetActive(false);
                    Object.Destroy(_effectInstance);
                    _socketEffect = null;
                }
                else if (letEffectFinish)
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
            _isSocketSlash = false;
            _hasTriggeredSocketImpact = false;
            _hasFinishedSocketSwing = false;
            _chargeFraction = 0f;
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
            _socketEffect = _effectInstance.GetComponent<SocketSlashEffect>();
            if (_socketEffect == null)
            {
                _socketEffect = _effectInstance.AddComponent<SocketSlashEffect>();
            }
            _socketEffect.Attach(socket);
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
