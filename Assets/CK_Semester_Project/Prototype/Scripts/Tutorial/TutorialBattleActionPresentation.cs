using CK.SemesterProject.Battle;
using UnityEngine;
using System.Linq;

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

        public Vector3 ActorHome { get; }
        public Vector3 TargetHome { get; }

        public TutorialBattleActionPresentation(Transform actor, Transform target, float stopDistance,
            string targetId = "monster", string actorId = "player")
        {
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
            float approach = progress < ImpactTime ? Mathf.SmoothStep(0f, 1f, progress / ImpactTime)
                : progress < ReturnTime ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (progress - ReturnTime) / (1f - ReturnTime));
            _actor.position = Vector3.Lerp(ActorHome, _attackPosition, approach);
            Vector3 facing = Vector3.ProjectOnPlane(TargetHome - ActorHome, Vector3.up);
            if (facing.sqrMagnitude > 0.001f)
            {
                _actor.rotation = Quaternion.LookRotation(facing);
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
        }
    }
}
