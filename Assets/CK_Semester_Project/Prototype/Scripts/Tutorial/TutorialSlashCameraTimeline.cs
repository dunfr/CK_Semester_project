using System;
using CK.SemesterProject.SkillSequence;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace CK.SemesterProject.Tutorial
{
    internal sealed class TutorialSlashCameraTimeline : IDisposable
    {
        private readonly TutorialSlashCutscenePool _pool;
        private readonly SkillSequenceContext _context;
        private readonly SkillSequenceAsset _sequence;
        private readonly Camera _camera;
        private readonly CinemachineBrain _brain;
        private readonly Transform _player;
        private readonly Transform _target;
        private readonly Vector3 _home;
        private readonly Vector3 _forward;
        private readonly SkinnedMeshRenderer[] _playerMeshes;
        private readonly Plane[] _shotFrustum = new Plane[6];
        private readonly TutorialSlashHitFeel _hitFeel;
        private PlayableDirector _director;
        private TutorialSlashCutscenePool.Rig _rig;
        private CinemachineCamera _shot;
        private TutorialSlashFramingReceiver _framing;
        private Pose _originalPose;
        private Pose _shotPose;
        private LensSettings _originalLens;
        private CinemachineBrain.UpdateMethods _originalUpdate;
        private CinemachineBrain.BrainUpdateMethods _originalBlendUpdate;
        private bool _brainWasEnabled;
        private bool _cameraReleased;

        public bool IsActive => _director != null && !_cameraReleased;
        public float Duration { get; }

        public TutorialSlashCameraTimeline(TutorialSlashCutscenePool pool, TimelineAsset timeline, SkillSequenceAsset sequence, Camera camera,
            CinemachineBrain brain, SkillSequenceContext context)
        {
            _hitFeel = new TutorialSlashHitFeel(timeline);
            _pool = pool;
            _context = context;
            _sequence = sequence;
            _camera = camera;
            _brain = brain;
            _player = context.Caster;
            _target = context.SelectedTarget;
            _home = _player.position;
            _forward = Vector3.ProjectOnPlane(_target.position - _home, Vector3.up).normalized;
            _playerMeshes = _player.GetComponentsInChildren<SkinnedMeshRenderer>();
            // 히트스톱으로 실시간이 타임라인보다 길어진 만큼 전체 길이도 늘어난다.
            Duration = sequence.ApproachStart + (float)timeline.duration + _hitFeel.ExtraRealSeconds;
            ExtraRealSeconds = _hitFeel.ExtraRealSeconds;
        }

        /// <summary>히트스톱으로 늘어난 실시간(초).</summary>
        public float ExtraRealSeconds { get; }

        /// <summary>
        /// 연출 시작 후 실시간(초)을 스킬 시퀀스 시간으로 바꾼다. 히트스톱 구간에서는 시퀀스 시간이 거의 흐르지 않는다.
        /// 플레이어 이동·애니메이션과 이펙트가 같은 시계를 쓰도록 재생기에 이 값을 넘긴다.
        /// </summary>
        public float ToSequenceTime(float elapsed)
        {
            return elapsed <= _sequence.ApproachStart
                ? elapsed
                : _sequence.ApproachStart + _hitFeel.ToContent(elapsed - _sequence.ApproachStart);
        }

        public void Sample(float time)
        {
            if (_player == null || _target == null || _camera == null || _brain == null
                || _forward.sqrMagnitude < 0.01f || time <= _sequence.ApproachStart)
            {
                return;
            }
            if (_director == null)
            {
                Start();
            }
            float realLocal = time - _sequence.ApproachStart;
            float contentLocal = _hitFeel.ToContent(realLocal);
            float sequenceTime = _sequence.ApproachStart + contentLocal;
            if (IsActive && (sequenceTime >= _sequence.ReturnEnd || (sequenceTime >= _sequence.ReturnStart && PlayerLeftShot())))
            {
                ReleaseCamera();
            }

            _director.time = Math.Min(contentLocal, _director.duration);
            _director.Evaluate();
            if (!IsActive)
            {
                return;
            }
            if (sequenceTime <= _sequence.ReturnStart && _framing.Frame != null)
            {
                FollowPlayer(_framing.Frame);
            }
            ApplyShake(realLocal);
            _brain.ManualUpdate();
        }

        // 흔들림은 실시간 기준이라 히트스톱 중에도 이어진다. 기준 포즈(_shotPose)는 흔들리지 않게 보관한다.
        private void ApplyShake(float realLocal)
        {
            if (!_hitFeel.HasEffects)
            {
                return;
            }
            Vector3 position = Vector3.zero;
            Vector3 rotation = Vector3.zero;
            _hitFeel.EvaluateShake(realLocal, out position, out rotation);
            _shot.transform.SetPositionAndRotation(_shotPose.position + _shotPose.rotation * position,
                _shotPose.rotation * Quaternion.Euler(rotation));
        }

        private void Start()
        {
            _originalPose = new Pose(_camera.transform.position, _camera.transform.rotation);
            _shotPose = _originalPose;
            _originalLens = LensSettings.FromCamera(_camera);
            _brainWasEnabled = _brain.enabled;
            _originalUpdate = _brain.UpdateMethod;
            _originalBlendUpdate = _brain.BlendUpdateMethod;

            _rig = _pool.Get();
            _director = _rig.Director;
            _director.transform.SetParent(null, false);
            _director.transform.SetPositionAndRotation(_context.GetTargetCenter(_target), _context.Facing);
            _framing = _rig.Framing;
            _shot = _rig.Shot;
            _director.timeUpdateMode = DirectorUpdateMode.Manual;
            _director.extrapolationMode = DirectorWrapMode.Hold;
            _shot.transform.SetPositionAndRotation(_originalPose.position, _originalPose.rotation);
            _shot.Lens = _originalLens;

            // Drive the existing gameplay camera through its Brain so the arena, layers and camera stack stay live.
            _brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            _brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
            _brain.enabled = true;
            BindBrain(_director, _brain);
            _shot.enabled = true;
            _director.gameObject.SetActive(true);
            _director.Play();
        }

        public static void BindBrain(PlayableDirector director, CinemachineBrain brain)
        {
            foreach (TrackAsset track in ((TimelineAsset)director.playableAsset).GetOutputTracks())
            {
                if (track is CinemachineTrack)
                {
                    if (brain != null)
                    {
                        director.SetGenericBinding(track, brain);
                    }
                    else
                    {
                        director.ClearGenericBinding(track);
                    }
                }
            }
        }

        private void FollowPlayer(TutorialSlashFramingClip.Frame frame)
        {
            Vector3 targetPosition = _target.position;
            Pose desired = frame.GetPose(_player.position, targetPosition, _forward, _originalPose);
            float fov = frame.GetFieldOfView(_originalLens.FieldOfView);
            if (!TutorialBattleCamera.CanTravel(_shotPose.position, desired.position, _player, _target)
                || !TutorialBattleCamera.IsFramed(targetPosition + Vector3.up * 0.8f, desired, fov, _camera.aspect, _player, _target))
            {
                return;
            }

            _shotPose = desired;
            _shot.transform.SetPositionAndRotation(desired.position, desired.rotation);
            LensSettings lens = _originalLens;
            lens.FieldOfView = fov;
            _shot.Lens = lens;
        }

        private bool PlayerLeftShot()
        {
            GeometryUtility.CalculateFrustumPlanes(_camera, _shotFrustum);
            bool hasVisibleMesh = false;
            foreach (SkinnedMeshRenderer mesh in _playerMeshes)
            {
                if (mesh == null || !mesh.enabled || !mesh.gameObject.activeInHierarchy || mesh.forceRenderingOff)
                {
                    continue;
                }
                hasVisibleMesh = true;
                if (GeometryUtility.TestPlanesAABB(_shotFrustum, mesh.bounds))
                {
                    return false;
                }
            }
            if (hasVisibleMesh)
            {
                return true;
            }

            Vector3 feet = _camera.WorldToViewportPoint(_player.position);
            Vector3 head = _camera.WorldToViewportPoint(_player.position + Vector3.up * 1.8f);
            const float margin = 0.07f;
            return (feet.z < _camera.nearClipPlane && head.z < _camera.nearClipPlane)
                || (feet.x < -margin && head.x < -margin) || (feet.x > 1f + margin && head.x > 1f + margin)
                || (feet.y < -margin && head.y < -margin) || (feet.y > 1f + margin && head.y > 1f + margin);
        }

        public void Dispose()
        {
            if (_director == null)
            {
                return;
            }
            ReleaseCamera();
            _pool.Release(_rig);
            BindBrain(_director, null);
            _rig = null;
            _director = null;
        }

        private void ReleaseCamera()
        {
            if (_cameraReleased)
            {
                return;
            }
            _cameraReleased = true;
            _shot.enabled = false;
            if (_brain != null)
            {
                _brain.enabled = _brainWasEnabled;
                _brain.UpdateMethod = _originalUpdate;
                _brain.BlendUpdateMethod = _originalBlendUpdate;
            }
            if (_camera != null)
            {
                _camera.transform.SetPositionAndRotation(_originalPose.position, _originalPose.rotation);
                _camera.fieldOfView = _originalLens.FieldOfView;
            }
        }
    }
}
