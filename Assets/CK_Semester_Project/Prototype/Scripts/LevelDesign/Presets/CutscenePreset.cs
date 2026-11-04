using System;
using System.Collections;
using System.Collections.Generic;
using CK.SemesterProject.Tutorial;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>
    /// 컷씬 프리셋. 플레이어가 트리거 영역에 들어오면 단계(대사·캐릭터 이동·카메라·대기)를 순서대로 재생한다.
    /// 단계마다 '앞 단계가 끝난 뒤 / 앞 단계와 동시에'와 시작 전 대기 시간으로 타이밍을 정한다.
    /// 카메라 샷·도착 지점은 이 오브젝트 아래 빈 오브젝트로 두고 씬에서 옮겨 설정한다.
    /// </summary>
    public sealed class CutscenePreset : MonoBehaviour
    {
        private const int CameraPriority = 1000;
        private const float ArrivalDistance = 0.1f;
        private const float MoveTimeout = 15f;

        [Header("발동")]
        [SerializeField, Tooltip("플레이어가 들어오면 컷씬을 시작하는 영역. 비우면 다른 곳에서 Play()로만 시작합니다.")]
        private PresetTriggerZone _trigger;
        [SerializeField, Tooltip("한 번만 재생합니다.")]
        private bool _playOnce = true;
        [SerializeField, Tooltip("컷씬 동안 플레이어 조작을 잠급니다.")]
        private bool _lockPlayer = true;

        [Header("단계")]
        [SerializeField] private List<CutsceneStep> _steps = new List<CutsceneStep>();

        [Header("연출 설정")]
        [SerializeField, Min(1f), Tooltip("대사 글자가 나타나는 속도(글자/초)")]
        private float _typingSpeed = 40f;
        [SerializeField, Tooltip("대사창 글꼴. 비우면 씬의 UI 글꼴을 씁니다.")]
        private TMP_FontAsset _dialogueFont;
        [SerializeField, Tooltip("이동 중 재생할 Animator 상태 이름")]
        private string _walkState = "Walk";
        [SerializeField, Tooltip("멈춰 있을 때 재생할 Animator 상태 이름")]
        private string _idleState = "Idle";
        [SerializeField, Min(0f), Tooltip("컷씬 카메라 시야각")]
        private float _cameraFieldOfView = 45f;

        [Header("이벤트")]
        [SerializeField] private UnityEvent _started;
        [SerializeField] private UnityEvent _finished;

        private CinemachineCamera _camera;
        private LevelProgress _progress;
        private Transform _player;
        private bool _hasPlayed;
        private Coroutine _routine;

        public bool IsPlaying => _routine != null;
        public PresetTriggerZone Trigger => _trigger;
        public IReadOnlyList<CutsceneStep> Steps => _steps;

        private void Awake()
        {
            _progress = FindFirstObjectByType<LevelProgress>();
        }

        private void OnEnable()
        {
            if (_trigger != null)
            {
                _trigger.PlayerEntered += OnPlayerEntered;
            }
        }

        private void OnDisable()
        {
            if (_trigger != null)
            {
                _trigger.PlayerEntered -= OnPlayerEntered;
            }
            if (_routine != null)
            {
                StopAllCoroutines();
                Finish();
            }
        }

        /// <summary>컷씬을 시작한다(트리거 외에 UnityEvent 등에서도 호출 가능).</summary>
        public void Play()
        {
            if (_routine != null || (_playOnce && _hasPlayed))
            {
                return;
            }
            if (_progress != null && _progress.IsInBattle)
            {
                return;
            }
            if (_player == null)
            {
                PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
                _player = movement != null ? movement.transform : null;
            }
            _hasPlayed = true;
            _routine = StartCoroutine(PlayRoutine());
        }

        private void OnPlayerEntered(Transform player)
        {
            _player = player;
            Play();
        }

        private IEnumerator PlayRoutine()
        {
            if (_lockPlayer)
            {
                LockPlayer(true);
            }
            _started.Invoke();

            int index = 0;
            while (index < _steps.Count)
            {
                // 첫 단계와 그 뒤의 '동시에' 단계를 한 묶음으로 함께 시작하고, 모두 끝나면 다음 묶음으로 넘어간다.
                int end = index + 1;
                while (end < _steps.Count && _steps[end].Start == CutsceneStepStart.WithPrevious)
                {
                    end++;
                }
                int running = end - index;
                for (int i = index; i < end; i++)
                {
                    StartCoroutine(RunStep(_steps[i], () => running--));
                }
                while (running > 0)
                {
                    yield return null;
                }
                index = end;
            }
            Finish();
        }

        private void Finish()
        {
            if (_camera != null)
            {
                _camera.gameObject.SetActive(false);
            }
            CutsceneDialogueView.HideActive();
            if (_lockPlayer)
            {
                LockPlayer(false);
            }
            _routine = null;
            _finished.Invoke();
        }

        private IEnumerator RunStep(CutsceneStep step, Action done)
        {
            if (step.Delay > 0f)
            {
                yield return new WaitForSeconds(step.Delay);
            }
            switch (step.Type)
            {
                case CutsceneStepType.Dialogue:
                    yield return DialogueRoutine(step);
                    break;
                case CutsceneStepType.Move:
                    yield return MoveRoutine(step);
                    break;
                case CutsceneStepType.Camera:
                    yield return CameraRoutine(step);
                    break;
                default:
                    yield return new WaitForSeconds(step.WaitSeconds);
                    break;
            }
            done();
        }

        private IEnumerator DialogueRoutine(CutsceneStep step)
        {
            CutsceneDialogueView view = CutsceneDialogueView.GetOrCreate(GetFont());
            bool waitsForInput = step.Duration <= 0f;
            view.Show(step.Speaker, step.Text, waitsForInput);
            int length = step.Text != null ? step.Text.Length : 0;
            float shown = 0f;
            // 첫 프레임의 키 입력(트리거 직전 F 등)으로 바로 넘어가지 않도록 한 프레임 쉰다.
            yield return null;
            while (shown < length)
            {
                if (WasAdvancePressed())
                {
                    shown = length;
                }
                else
                {
                    shown += _typingSpeed * Time.deltaTime;
                }
                view.SetVisibleCharacters(Mathf.Min(length, Mathf.FloorToInt(shown)));
                yield return null;
            }
            view.SetVisibleCharacters(length);
            if (waitsForInput)
            {
                yield return null;
                while (!WasAdvancePressed())
                {
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(step.Duration);
            }
            view.Hide();
        }

        private IEnumerator MoveRoutine(CutsceneStep step)
        {
            Transform actor = step.Actor != null ? step.Actor : _player;
            if (actor == null || step.Destination == null)
            {
                yield break;
            }
            Animator animator = actor.GetComponentInChildren<Animator>();
            PlayAnimatorState(animator, _walkState);
            Vector3 destination = step.Destination.position;
            NavMeshAgent agent = actor.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                yield return MoveWithAgent(agent, destination, step.MoveSpeed);
            }
            else
            {
                yield return MoveDirectly(actor, destination, step.MoveSpeed);
            }
            if (step.MatchDestinationRotation)
            {
                yield return RotateTo(actor, Quaternion.Euler(0f, step.Destination.eulerAngles.y, 0f));
            }
            PlayAnimatorState(animator, _idleState);
        }

        private IEnumerator MoveWithAgent(NavMeshAgent agent, Vector3 destination, float speed)
        {
            float originalSpeed = agent.speed;
            agent.speed = speed;
            agent.isStopped = false;
            agent.SetDestination(destination);
            float timeout = Time.time + MoveTimeout;
            while (Time.time < timeout && (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + ArrivalDistance))
            {
                yield return null;
            }
            agent.ResetPath();
            agent.speed = originalSpeed;
        }

        // CharacterController가 있는 플레이어는 충돌을 지키며 움직이고, 그 외에는 위치를 직접 옮긴다.
        private IEnumerator MoveDirectly(Transform actor, Vector3 destination, float speed)
        {
            CharacterController controller = actor.GetComponent<CharacterController>();
            float timeout = Time.time + MoveTimeout;
            while (Time.time < timeout)
            {
                Vector3 offset = Vector3.ProjectOnPlane(destination - actor.position, Vector3.up);
                if (offset.magnitude <= ArrivalDistance)
                {
                    break;
                }
                Vector3 step = Vector3.ClampMagnitude(offset, speed * Time.deltaTime);
                actor.rotation = Quaternion.RotateTowards(actor.rotation, Quaternion.LookRotation(offset.normalized), 540f * Time.deltaTime);
                if (controller != null && controller.enabled)
                {
                    controller.Move(step + Vector3.down * 2f * Time.deltaTime);
                }
                else
                {
                    actor.position += step;
                }
                yield return null;
            }
        }

        private static IEnumerator RotateTo(Transform actor, Quaternion target)
        {
            while (Quaternion.Angle(actor.rotation, target) > 1f)
            {
                actor.rotation = Quaternion.RotateTowards(actor.rotation, target, 360f * Time.deltaTime);
                yield return null;
            }
            actor.rotation = target;
        }

        private IEnumerator CameraRoutine(CutsceneStep step)
        {
            Transform[] shots = step.Shots;
            if (shots == null || shots.Length == 0 || shots[0] == null)
            {
                yield break;
            }
            CinemachineCamera cutsceneCamera = GetCamera();
            ApplyShot(cutsceneCamera.transform, shots[0].position, shots[0].rotation, step.LookAt);
            cutsceneCamera.gameObject.SetActive(true);
            for (int i = 1; i < shots.Length; i++)
            {
                Transform from = shots[i - 1];
                Transform to = shots[i];
                if (from == null || to == null)
                {
                    continue;
                }
                for (float elapsed = 0f; elapsed < step.TravelSeconds; elapsed += Time.deltaTime)
                {
                    float t = elapsed / Mathf.Max(0.01f, step.TravelSeconds);
                    if (step.EaseInOut)
                    {
                        t = Mathf.SmoothStep(0f, 1f, t);
                    }
                    ApplyShot(cutsceneCamera.transform, Vector3.Lerp(from.position, to.position, t), Quaternion.Slerp(from.rotation, to.rotation, t), step.LookAt);
                    yield return null;
                }
                ApplyShot(cutsceneCamera.transform, to.position, to.rotation, step.LookAt);
            }
            for (float elapsed = 0f; elapsed < step.HoldSeconds; elapsed += Time.deltaTime)
            {
                if (step.LookAt != null)
                {
                    ApplyShot(cutsceneCamera.transform, cutsceneCamera.transform.position, cutsceneCamera.transform.rotation, step.LookAt);
                }
                yield return null;
            }
        }

        private static void ApplyShot(Transform cameraTransform, Vector3 position, Quaternion rotation, Transform lookAt)
        {
            if (lookAt != null)
            {
                Vector3 direction = lookAt.position - position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    rotation = Quaternion.LookRotation(direction);
                }
            }
            cameraTransform.SetPositionAndRotation(position, rotation);
        }

        // 컷씬 전용 Cinemachine 카메라. 켜져 있는 동안 우선순위가 가장 높아 화면을 가져오고, 끄면 원래 카메라로 블렌드된다.
        private CinemachineCamera GetCamera()
        {
            if (_camera != null)
            {
                return _camera;
            }
            var cameraObject = new GameObject("CutsceneCamera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.SetActive(false);
            _camera = cameraObject.AddComponent<CinemachineCamera>();
            _camera.Priority = CameraPriority;
            LensSettings lens = _camera.Lens;
            lens.FieldOfView = _cameraFieldOfView;
            _camera.Lens = lens;
            return _camera;
        }

        private void LockPlayer(bool isLocked)
        {
            if (_progress != null)
            {
                if (isLocked)
                {
                    _progress.LockControls(this);
                }
                else
                {
                    _progress.UnlockControls(this);
                }
            }
            else if (_player != null)
            {
                PlayerMovement movement = _player.GetComponent<PlayerMovement>();
                if (movement != null)
                {
                    movement.enabled = !isLocked;
                }
            }
            // 이동 중에 잠기면 걷는 모션이 남으므로 대기 모션으로 바꾼다.
            if (isLocked && _player != null)
            {
                PlayAnimatorState(_player.GetComponentInChildren<Animator>(), _idleState);
            }
        }

        private static void PlayAnimatorState(Animator animator, string stateName)
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(stateName))
            {
                return;
            }
            int hash = Animator.StringToHash(stateName);
            if (animator.HasState(0, hash))
            {
                animator.CrossFadeInFixedTime(hash, 0.2f, 0);
            }
        }

        private static bool WasAdvancePressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.fKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
        }

        private TMP_FontAsset GetFont()
        {
            if (_dialogueFont == null)
            {
                TMP_Text sceneText = FindFirstObjectByType<TMP_Text>();
                _dialogueFont = sceneText != null ? sceneText.font : null;
            }
            return _dialogueFont;
        }

        private void OnDrawGizmos()
        {
            for (int i = 0; i < _steps.Count; i++)
            {
                CutsceneStep step = _steps[i];
                if (step == null)
                {
                    continue;
                }
                if (step.Type == CutsceneStepType.Camera && step.Shots != null)
                {
                    Gizmos.color = new Color(0.6f, 0.4f, 1f);
                    for (int s = 0; s < step.Shots.Length; s++)
                    {
                        Transform shot = step.Shots[s];
                        if (shot == null)
                        {
                            continue;
                        }
                        Gizmos.matrix = Matrix4x4.TRS(shot.position, shot.rotation, Vector3.one);
                        Gizmos.DrawFrustum(Vector3.zero, _cameraFieldOfView, 0.8f, 0.05f, 16f / 9f);
                        Gizmos.matrix = Matrix4x4.identity;
                        if (s > 0 && step.Shots[s - 1] != null)
                        {
                            Gizmos.DrawLine(step.Shots[s - 1].position, shot.position);
                        }
                    }
                }
                else if (step.Type == CutsceneStepType.Move && step.Destination != null)
                {
                    Gizmos.color = new Color(0.3f, 1f, 0.5f);
                    Vector3 end = step.Destination.position + Vector3.up * 0.05f;
                    Gizmos.DrawWireSphere(end, 0.3f);
                    Gizmos.DrawLine(end, end + step.Destination.forward * 0.8f);
                    if (step.Actor != null)
                    {
                        Gizmos.DrawLine(step.Actor.position + Vector3.up * 0.05f, end);
                    }
                }
            }
        }
    }
}
