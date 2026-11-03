using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Rendering.Universal;
#endif

namespace CK.SemesterProject.Tutorial
{
    // FSM의 시간을 그대로 쓰기 위해 속도는 Animator.speed 대신 Time.timeScale로 조절한다.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FuchsiaSkillPreview : MonoBehaviour
    {
        [SerializeField, Tooltip("푸시아의 전투 모션 컴포넌트. 플레이 후 버튼이나 숫자키로 재생합니다.")]
        private PlayerBattleAnimation _battleAnimation;
        [SerializeField, Tooltip("비우면 MainCamera를 사용합니다. 오른쪽 드래그로 회전, 휠로 거리를 조절합니다.")]
        private Camera _camera;
        [SerializeField] private bool _useCinematicCamera = true;
        [SerializeField] private bool _autoRepeat;
        [SerializeField, Tooltip("편집 중 Animation 창에서 푸시아 클립을 열어 시간을 넘기면 이 스킬 ID의 카메라로 봅니다. 비우면 첫 PhysCamera001 스킬입니다.")]
        private string _editorPreviewSkill = "";

        private Camera _runtimeCamera;
        private bool _cinematicActive;
        private Pose _returnPose;
        private float _returnFieldOfView;
        private float _previousTimeScale;
        private bool _ownsTimeScale;
        private Vector3 _centerOffset;
        private float _yaw, _pitch, _distance;
        private string _lastSkill;
        private float _repeatDelay = .5f;
        private bool _dragging;
        private Rect PanelRect => new Rect(12f, 12f, 275f, 218f + (_battleAnimation != null ? _battleAnimation.SkillMotionCount : 0) * 29f);

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                _previousTimeScale = Time.timeScale;
                _ownsTimeScale = true;
            }
#if UNITY_EDITOR
            EditorApplication.update += EditorTick;
            AssemblyReloadEvents.beforeAssemblyReload += DestroyEditorCamera;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
        }

        private void Start()
        {
            if (!Application.isPlaying) return;
            _runtimeCamera = _camera != null ? _camera : Camera.main;
            if (_battleAnimation == null) return;
            CacheCenter();
            InitializeOrbit();
            _battleAnimation.EnterBattle();
        }

        private void OnDisable()
        {
            RestoreCamera();
            _dragging = false;
            if (_ownsTimeScale)
            {
                Time.timeScale = _previousTimeScale;
                _ownsTimeScale = false;
            }
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
            AssemblyReloadEvents.beforeAssemblyReload -= DestroyEditorCamera;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            DestroyEditorCamera();
#endif
        }

        private void CacheCenter()
        {
            Transform root = _battleAnimation.transform;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = new Bounds(root.position + Vector3.up * .9f, Vector3.zero);
            bool found = false;
            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            _centerOffset = root.InverseTransformPoint(bounds.center);
        }

        private Vector3 Center => _battleAnimation.transform.TransformPoint(_centerOffset);

        private void InitializeOrbit()
        {
            if (_runtimeCamera == null) return;
            Transform node = _battleAnimation.transform.Find("PhysCamera001");
            Vector3 fromCenter = node != null ? node.position - Center : new Vector3(2.4f, 1f, 3f);
            ReadOrbit(fromCenter);
            ApplyOrbit();
        }

        private void ReadOrbit(Vector3 fromCenter)
        {
            _distance = Mathf.Clamp(fromCenter.magnitude, .4f, 12f);
            Vector3 direction = fromCenter.sqrMagnitude > 1e-6f ? fromCenter.normalized : Vector3.forward;
            _yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            _pitch = Mathf.Clamp(Mathf.Asin(direction.y) * Mathf.Rad2Deg, -75f, 80f);
        }

        private void ApplyOrbit()
        {
            Vector3 offset = Quaternion.Euler(-_pitch, _yaw, 0f) * Vector3.forward * _distance;
            _runtimeCamera.transform.SetPositionAndRotation(Center + offset, Quaternion.LookRotation(-offset, Vector3.up));
        }

        // UI와 배치 PlayMode 테스트가 같은 진입점을 사용한다.
        public float PlayPreviewSkill(string skillId)
        {
            if (!Application.isPlaying || _battleAnimation == null) return 0f;
            RestoreCamera();
            float length = _battleAnimation.PlaySkill(skillId, out _);
            _lastSkill = length > 0f ? skillId : null;
            _repeatDelay = .5f;
            if (length > 0f) UpdateCinematicCamera();
            return length;
        }

        private void Update()
        {
            if (!Application.isPlaying || _battleAnimation == null) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                for (int i = 0; i < Mathf.Min(9, _battleAnimation.SkillMotionCount); i++)
                {
                    if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame
                        && _battleAnimation.TryGetSkillMotion(i, out string id, out _)) PlayPreviewSkill(id);
                }
            }
            if (_battleAnimation.ActionLength > 0f) _repeatDelay = .5f;
            else if (_autoRepeat && !string.IsNullOrEmpty(_lastSkill))
            {
                _repeatDelay -= Time.deltaTime;
                if (_repeatDelay <= 0f) PlayPreviewSkill(_lastSkill);
            }
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || _runtimeCamera == null || _battleAnimation == null) return;
            if (UpdateCinematicCamera()) { _dragging = false; return; }
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pointer = mouse.position.ReadValue();
            bool overPanel = PanelRect.Contains(new Vector2(pointer.x, Screen.height - pointer.y));
            if (mouse.rightButton.wasPressedThisFrame) _dragging = !overPanel;
            if (!mouse.rightButton.isPressed) _dragging = false;
            bool changed = false;
            if (_dragging && !overPanel)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yaw += delta.x * .2f;
                _pitch = Mathf.Clamp(_pitch + delta.y * .2f, -75f, 80f);
                changed = delta.sqrMagnitude > 0f;
            }
            float wheel = mouse.scroll.ReadValue().y;
            if (!overPanel && Mathf.Abs(wheel) > 0f)
            {
                _distance = Mathf.Clamp(_distance * Mathf.Exp(-wheel * .001f), .4f, 12f);
                changed = true;
            }
            // 복구 후 입력이 없으면 저장한 roll까지 그대로 유지한다.
            if (changed) ApplyOrbit();
        }

        private bool UpdateCinematicCamera()
        {
            if (_runtimeCamera == null) return false;
            if (_useCinematicCamera && _battleAnimation != null
                && _battleAnimation.TryGetCinematicCamera(out Pose pose, out float horizontal))
            {
                if (!_cinematicActive)
                {
                    _returnPose = new Pose(_runtimeCamera.transform.position, _runtimeCamera.transform.rotation);
                    _returnFieldOfView = _runtimeCamera.fieldOfView;
                    _cinematicActive = true;
                }
                _runtimeCamera.transform.SetPositionAndRotation(pose.position, pose.rotation);
                if (horizontal > 0f) _runtimeCamera.fieldOfView = Camera.HorizontalToVerticalFieldOfView(horizontal, _runtimeCamera.aspect);
                return true;
            }
            RestoreCamera();
            return false;
        }

        private void RestoreCamera()
        {
            if (!_cinematicActive) return;
            _cinematicActive = false;
            if (_runtimeCamera == null) return;
            _runtimeCamera.transform.SetPositionAndRotation(_returnPose.position, _returnPose.rotation);
            _runtimeCamera.fieldOfView = _returnFieldOfView;
            if (_battleAnimation != null) ReadOrbit(_returnPose.position - Center);
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || _battleAnimation == null) return;
            GUILayout.BeginArea(PanelRect, "푸시아 스킬 미리보기", GUI.skin.window);
            for (int i = 0; i < _battleAnimation.SkillMotionCount; i++)
            {
                if (!_battleAnimation.TryGetSkillMotion(i, out string id, out string state)) continue;
                string key = i < 9 ? $" [{i + 1}]" : "";
                if (GUILayout.Button(state + key, GUILayout.Height(25f))) PlayPreviewSkill(id);
            }
            _useCinematicCamera = GUILayout.Toggle(_useCinematicCamera, "연출 카메라 사용");
            _autoRepeat = GUILayout.Toggle(_autoRepeat, "자동 반복 (종료 후 0.5초)");
            GUILayout.Label("재생 속도");
            GUILayout.BeginHorizontal();
            foreach (float speed in new[] { .25f, .5f, 1f })
            {
                if (GUILayout.Toggle(Mathf.Approximately(Time.timeScale, speed), speed.ToString("0.##") + "배", GUI.skin.button)) Time.timeScale = speed;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label($"상태: {_battleAnimation.CurrentStateName}");
            GUILayout.Label($"{_battleAnimation.ActionElapsed:F2} / {_battleAnimation.ActionLength:F2} 초");
            GUILayout.Label("오른쪽 드래그: 회전 / 휠: 거리");
            GUILayout.EndArea();
        }

#if UNITY_EDITOR
        private Camera _editorCamera;
        private Camera _editorSource;

        private void OnPlayModeChanged(PlayModeStateChange state) => DestroyEditorCamera();

        private void EditorTick()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || !isActiveAndEnabled
                || !gameObject.scene.IsValid() || EditorUtility.IsPersistent(this) || _battleAnimation == null
                || !AnimationMode.InAnimationMode())
            {
                DestroyEditorCamera();
                return;
            }
            string id = _editorPreviewSkill;
            if (string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < _battleAnimation.SkillMotionCount; i++)
                {
                    _battleAnimation.TryGetSkillMotion(i, out string candidate, out _);
                    if (_battleAnimation.TryGetSkillCinematicCamera(candidate, out Transform c, out _, out _) && c.name == "PhysCamera001")
                    { id = candidate; break; }
                }
            }
            Camera source = _camera != null ? _camera : Camera.main;
            if (source == null || !_battleAnimation.TryGetSkillCinematicCamera(id, out Transform node, out Transform target, out float horizontal))
            {
                DestroyEditorCamera();
                return;
            }
            if (_editorCamera == null || _editorSource != source)
            {
                DestroyEditorCamera();
                var temporary = new GameObject("Fuchsia Animation Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
                _editorCamera = temporary.AddComponent<Camera>();
                _editorCamera.hideFlags = HideFlags.HideAndDontSave;
                _editorSource = source;
                if (source.TryGetComponent(out UniversalAdditionalCameraData sourceData))
                {
                    var data = temporary.AddComponent<UniversalAdditionalCameraData>();
                    EditorUtility.CopySerialized(sourceData, data);
                    data.hideFlags = HideFlags.HideAndDontSave;
                    data.renderType = CameraRenderType.Base;
                    data.cameraStack?.Clear();
                }
            }
            _editorCamera.CopyFrom(source);
            _editorCamera.depth = source.depth + 100f;
            _editorCamera.enabled = true;
            Pose pose = PlayerBattleAnimation.GetCinematicCameraPose(node, target);
            _editorCamera.transform.SetPositionAndRotation(pose.position, pose.rotation);
            if (horizontal > 0f) _editorCamera.fieldOfView = Camera.HorizontalToVerticalFieldOfView(horizontal, source.aspect);
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private void DestroyEditorCamera()
        {
            if (_editorCamera != null) DestroyImmediate(_editorCamera.gameObject);
            _editorCamera = _editorSource = null;
        }
#endif
    }
}
