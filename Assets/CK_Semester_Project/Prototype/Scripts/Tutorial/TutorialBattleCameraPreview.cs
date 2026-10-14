using Unity.Cinemachine;
using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    // 비전투 중 카메라만 빌려 쓴다. 전투 데이터, 배치, UI에는 관여하지 않는다.
    internal sealed class TutorialBattleCameraPreview
    {
        private readonly Camera _camera;
        private readonly CinemachineBrain _brain;
        private readonly CameraController _orbit;
        private readonly PlayerMovement _movement;
        private readonly Pose _savedPose;
        private readonly float _savedFieldOfView;
        private readonly bool _savedOrthographic;
        private readonly bool _brainEnabled;
        private readonly bool _orbitEnabled;
        private readonly CursorLockMode _cursorLock;
        private readonly bool _cursorVisible;
        private readonly Vector3 _forward;

        public TutorialBattleCameraPreview(Camera camera, CinemachineBrain brain,
            CameraController orbit, PlayerMovement movement)
        {
            _camera = camera;
            _brain = brain;
            _orbit = orbit;
            _movement = movement;
            _savedPose = new Pose(camera.transform.position, camera.transform.rotation);
            _savedFieldOfView = camera.fieldOfView;
            _savedOrthographic = camera.orthographic;
            _brainEnabled = brain.enabled;
            _orbitEnabled = orbit.enabled;
            _cursorLock = Cursor.lockState;
            _cursorVisible = Cursor.visible;
            _forward = Vector3.ProjectOnPlane(movement.transform.forward, Vector3.up).normalized;
            if (_forward.sqrMagnitude < 0.01f)
            {
                _forward = Vector3.forward;
            }
            orbit.enabled = false;
            brain.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Show(Transform target, float spacing, Vector3 offset, float fieldOfView)
        {
            Vector3 player = _movement.transform.position;
            Vector3 enemy = target != null ? target.position : player + _forward * spacing;
            if (Vector3.ProjectOnPlane(enemy - player, Vector3.up).sqrMagnitude < 0.01f)
            {
                enemy = player + _forward * Mathf.Max(2f, spacing);
            }
            // 조정 중에는 흔들림/장애물 보정 없이 지정한 기본 구도를 그대로 표시한다.
            Pose pose = TutorialBattleCamera.GetBasePose(player, enemy, offset);
            _camera.orthographic = false;
            _camera.fieldOfView = fieldOfView;
            _camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
        }

        public void Restore()
        {
            if (_camera != null)
            {
                _camera.transform.SetPositionAndRotation(_savedPose.position, _savedPose.rotation);
                _camera.fieldOfView = _savedFieldOfView;
                _camera.orthographic = _savedOrthographic;
            }
            if (_brain != null) _brain.enabled = _brainEnabled;
            if (_orbit != null) _orbit.enabled = _orbitEnabled;
            Cursor.lockState = _cursorLock;
            Cursor.visible = _cursorVisible;
        }
    }
}
