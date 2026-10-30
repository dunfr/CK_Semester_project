using System.Collections.Generic;
using UnityEngine;

namespace CK.SemesterProject.SkillSequence
{
    /// <summary>
    /// 스킬 연출 한 번에 참여하는 시전자·대상·무대 기준점 정보. 에디터 미리보기와 실제 전투가 같은 계산을 쓴다.
    /// 대상의 중심·발밑·반경은 생성 시점의 렌더러 크기로 계산해 캐시한다(몬스터 크기가 달라도 같은 부위에 맞추기 위함).
    /// </summary>
    public sealed class SkillSequenceContext
    {
        private readonly List<Transform> _targets = new List<Transform>();
        private readonly Dictionary<Transform, Bounds> _targetBounds = new Dictionary<Transform, Bounds>();
        private readonly Transform _stageCenter;
        private Animator _casterAnimator;
        private Transform _weaponSocket;
        private bool _hasSearchedSocket;

        public SkillSequenceContext(Transform caster, IEnumerable<Transform> targets, Transform stageCenter = null, int selectedTargetIndex = 0)
        {
            Caster = caster;
            CasterHome = new Pose(caster.position, caster.rotation);
            _stageCenter = stageCenter;
            if (targets != null)
            {
                foreach (Transform target in targets)
                {
                    if (target != null)
                    {
                        _targets.Add(target);
                        _targetBounds[target] = CalculateBounds(target);
                    }
                }
            }
            SelectedTargetIndex = Mathf.Clamp(selectedTargetIndex, 0, Mathf.Max(0, _targets.Count - 1));
            _casterAnimator = caster.GetComponentInChildren<Animator>();
        }

        public Transform Caster { get; }
        public Pose CasterHome { get; }
        public IReadOnlyList<Transform> Targets => _targets;
        public int SelectedTargetIndex { get; }
        public Transform SelectedTarget => _targets.Count == 0 ? null : _targets[SelectedTargetIndex];
        public Animator CasterAnimator => _casterAnimator;
        public Vector3 StagePosition => _stageCenter != null ? _stageCenter.position : CasterHome.position;

        /// <summary>시전자 제자리에서 선택 대상을 바라보는 방향(수평).</summary>
        public Quaternion Facing
        {
            get
            {
                Vector3 direction = SelectedTarget != null
                    ? Vector3.ProjectOnPlane(GetTargetFeet(SelectedTarget) - CasterHome.position, Vector3.up)
                    : Vector3.ProjectOnPlane(CasterHome.rotation * Vector3.forward, Vector3.up);
                return direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction.normalized) : Quaternion.identity;
            }
        }

        public Vector3 GetTargetCenter(Transform target)
        {
            return GetBounds(target).center;
        }

        public Vector3 GetTargetFeet(Transform target)
        {
            Bounds bounds = GetBounds(target);
            return new Vector3(bounds.center.x, target.position.y, bounds.center.z);
        }

        /// <summary>대상의 수평 반경(m). 접근 거리를 대상 표면 기준으로 맞출 때 쓴다.</summary>
        public float GetTargetRadius(Transform target)
        {
            Vector3 extents = GetBounds(target).extents;
            return Mathf.Max(extents.x, extents.z);
        }

        public Vector3 GetGroupCenter()
        {
            if (_targets.Count == 0)
            {
                return StagePosition;
            }
            Vector3 sum = Vector3.zero;
            foreach (Transform target in _targets)
            {
                sum += GetTargetCenter(target);
            }
            return sum / _targets.Count;
        }

        public Transform FindBone(HumanBodyBones bone)
        {
            if (_casterAnimator != null && _casterAnimator.isHuman && bone != HumanBodyBones.LastBone)
            {
                Transform boneTransform = _casterAnimator.GetBoneTransform(bone);
                if (boneTransform != null)
                {
                    return boneTransform;
                }
            }
            return Caster;
        }

        public Transform FindWeaponSocket()
        {
            if (!_hasSearchedSocket)
            {
                _weaponSocket = WeaponEffectSocket.Find(Caster);
                _hasSearchedSocket = true;
            }
            return _weaponSocket != null ? _weaponSocket : Caster;
        }

        /// <summary>현재 포즈 기준으로 이펙트 기준점의 위치·방향과 따라갈 부모를 구한다.</summary>
        public void ResolveAnchor(SkillVfxEvent vfxEvent, Transform target, out Vector3 position, out Quaternion rotation, out Transform anchorTransform)
        {
            Quaternion facing = Facing;
            Transform resolvedTarget = target != null ? target : SelectedTarget;
            switch (vfxEvent.Anchor)
            {
                case SkillVfxAnchorPoint.CasterRoot:
                    position = Caster.position;
                    rotation = Caster.rotation;
                    anchorTransform = Caster;
                    return;
                case SkillVfxAnchorPoint.CasterBone:
                    anchorTransform = FindBone(vfxEvent.Bone);
                    position = anchorTransform.position;
                    rotation = anchorTransform.rotation;
                    return;
                case SkillVfxAnchorPoint.WeaponSocket:
                    anchorTransform = FindWeaponSocket();
                    position = anchorTransform.position;
                    rotation = anchorTransform.rotation;
                    return;
                case SkillVfxAnchorPoint.TargetCenter:
                case SkillVfxAnchorPoint.TargetFeet:
                    if (resolvedTarget == null)
                    {
                        position = CasterHome.position + facing * Vector3.forward * 3f;
                        rotation = facing;
                        anchorTransform = null;
                        return;
                    }
                    position = vfxEvent.Anchor == SkillVfxAnchorPoint.TargetCenter ? GetTargetCenter(resolvedTarget) : GetTargetFeet(resolvedTarget);
                    rotation = facing;
                    anchorTransform = resolvedTarget;
                    return;
                case SkillVfxAnchorPoint.TargetGroupCenter:
                    position = GetGroupCenter();
                    rotation = facing;
                    anchorTransform = null;
                    return;
                default:
                    position = StagePosition;
                    rotation = facing;
                    anchorTransform = _stageCenter;
                    return;
            }
        }

        /// <summary>기준점 + 차이값으로 실제 생성 위치를 구한다. 따라가기가 꺼져 있으면 parent는 null이다.</summary>
        public void GetSpawnPose(SkillVfxEvent vfxEvent, Transform target, out Vector3 position, out Quaternion rotation, out Transform parent)
        {
            ResolveAnchor(vfxEvent, target, out Vector3 anchorPosition, out Quaternion anchorRotation, out Transform anchorTransform);
            position = anchorPosition + anchorRotation * vfxEvent.PositionOffset;
            rotation = anchorRotation * Quaternion.Euler(vfxEvent.RotationOffset);
            parent = vfxEvent.FollowAnchor ? anchorTransform : null;
        }

        /// <summary>월드 위치·회전을 기준점 기준 차이값으로 되돌린다(에디터 씬 핸들 편집용).</summary>
        public void ConvertToOffsets(SkillVfxEvent vfxEvent, Transform target, Vector3 worldPosition, Quaternion worldRotation,
            out Vector3 positionOffset, out Vector3 rotationOffset)
        {
            ResolveAnchor(vfxEvent, target, out Vector3 anchorPosition, out Quaternion anchorRotation, out _);
            Quaternion inverse = Quaternion.Inverse(anchorRotation);
            positionOffset = inverse * (worldPosition - anchorPosition);
            rotationOffset = (inverse * worldRotation).eulerAngles;
        }

        public IEnumerable<Transform> GetEventTargets(SkillVfxEvent vfxEvent)
        {
            if (!vfxEvent.UsesTarget || vfxEvent.TargetMode == SkillVfxTargetMode.SelectedTarget || _targets.Count == 0)
            {
                yield return SelectedTarget;
                yield break;
            }
            foreach (Transform target in _targets)
            {
                yield return target;
            }
        }

        private Bounds GetBounds(Transform target)
        {
            return _targetBounds.TryGetValue(target, out Bounds bounds) ? bounds : CalculateBounds(target);
        }

        private static Bounds CalculateBounds(Transform target)
        {
            bool hasBounds = false;
            Bounds bounds = new Bounds(target.position + Vector3.up, Vector3.one);
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
            {
                // 이펙트(파티클·트레일)가 붙어 있어도 몸 크기만 쓰도록 메시 렌더러만 포함한다.
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                {
                    continue;
                }
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return bounds;
        }
    }
}
