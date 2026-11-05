using System.Collections;
using CK.SemesterProject.Battle;
using Semester.Enemies;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>
    /// 01 튜토리얼 구역 흐름. T01 시작 시퀀스(페이드 인 → 캡슐 안에 누운 1인칭 시점 → 경비병 버튼 → 적색 점등 → 가스 분출
    /// → 캡슐 오류(깜빡임·스파크·흔들림) → 덮개 강제 개방 → 3인칭 전환, 캐릭터가 일어나 캡슐 밖으로 나옴),
    /// C02 20초 대기 후 경비병 접근, 경비병 처치(C03) 후 T02 카드 키 상승.
    /// </summary>
    public sealed class TutorialStartSequence : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField, Min(0f), Tooltip("경비병이 버튼까지 걷는 시간(초). 1-4-A01")]
        private float _guardWalkSeconds = 2.5f;
        [SerializeField, Min(0f), Tooltip("버튼 누르기 시간(초). 1-4-A02")]
        private float _buttonPressSeconds = 1.5f;
        [SerializeField, Min(0f), Tooltip("플레이 시작 후 1인칭으로 적을 바라보기만 하는 시간(초)")]
        private float _openingHoldSeconds = 1f;
        [SerializeField, Min(0f), Tooltip("적색 점등 후 가스가 나오기까지의 시간(초)")]
        private float _alarmToGasSeconds = 0.6f;
        [SerializeField, Min(0f), Tooltip("가스가 차오르는 시간(초). 이후 캡슐 오류가 시작됩니다.")]
        private float _gasSeconds = 2.5f;
        [SerializeField, Min(0f), Tooltip("화면이 검은색에서 밝아지는 시간(초)")]
        private float _fadeInSeconds = 1.5f;
        [SerializeField, Min(0f), Tooltip("캡슐 오류(깜빡임·스파크·흔들림) 시간(초)")]
        private float _errorSeconds = 1.6f;
        [SerializeField, Min(0.01f), Tooltip("덮개가 튕겨 열리는 시간(초)")]
        private float _lidOpenSeconds = 0.35f;
        [SerializeField, Min(0f), Tooltip("덮개가 열린 뒤 3인칭으로 바뀌기까지의 시간(초)")]
        private float _openToThirdPersonSeconds = 0.5f;
        [SerializeField, Min(0f), Tooltip("3인칭 전환 후 캐릭터를 다시 보여 주기까지의 시간(초). 카메라가 몸을 통과하는 장면을 가립니다.")]
        private float _revealPlayerDelay = 0.6f;
        [SerializeField, Min(0.01f), Tooltip("캐릭터가 누운 자세에서 일어나는 시간(초)")]
        private float _riseSeconds = 1.4f;
        [SerializeField, Min(0.01f), Tooltip("캐릭터가 캡슐 밖으로 뛰어내리는 시간(초)")]
        private float _stepOutSeconds = 0.7f;
        [SerializeField, Min(0.1f), Tooltip("조작 해제 후 플레이어가 공격하지 않으면 경비병이 다가오기까지의 시간(초). C02")]
        private float _guardWaitSeconds = 20f;
        [SerializeField, Min(0f), Tooltip("경비병 뒤돌기·놀람 시간(초). 1-4-A03")]
        private float _guardSurpriseSeconds = 1.5f;
        [SerializeField, Min(0f), Tooltip("카드 키 상승을 비추는 시간(초). T02")]
        private float _cardFocusSeconds = 1.5f;

        [Header("References")]
        [SerializeField] private LevelProgress _progress;
        [SerializeField, Tooltip("콘솔 데스크 앞 경비병(1-4). 비우면 경비병 연출 없이 캡슐 연출 뒤 바로 카드 키가 올라옵니다.")]
        private EnemyStateMachine _guard;
        [SerializeField, Tooltip("경비병이 버튼을 누르는 위치·방향")]
        private Transform _guardButtonPoint;
        [SerializeField] private CardKeyDesk _desk;
        [SerializeField, Tooltip("카드 키 상승을 비출 카메라 위치")]
        private Transform _deskView;
        [SerializeField, Tooltip("카메라가 바라볼 데스크 지점")]
        private Transform _deskFocus;
        [SerializeField, Tooltip("'경비병을 처치하거나 출입문 확인' 목표 때 미니맵이 안내할 곳(출입문). 비우면 안내선을 숨깁니다.")]
        private Transform _exitDoorTarget;
        [SerializeField, Tooltip("캡슐 안 1인칭 시점 카메라. 시작할 때 켜고, 연출이 끝나면 꺼서 3인칭 카메라로 블렌드합니다.")]
        private CinemachineCamera _firstPersonCamera;
        [SerializeField, Tooltip("1인칭 카메라 위치(캡슐 안 눈높이). 머리 뼈가 지정되면 머리 뼈를 따라갑니다.")]
        private Transform _firstPersonEye;
        [SerializeField, Tooltip("1인칭 카메라가 따라갈 캐릭터 머리 뼈(예: Bip001 Head)")]
        private Transform _firstPersonHeadBone;
        [SerializeField, Tooltip("머리 뼈에서 눈까지의 거리(얼굴 쪽, 정수리 쪽, m)")]
        private Vector2 _eyeOffsetFromHeadBone = new Vector2(0.14f, 0.05f);
        [SerializeField, Range(0f, 45f), Tooltip("캡슐 안에서 상체가 기대어 들려 있는 각도(도). 0이면 완전히 평평하게 눕습니다.")]
        private float _lyingReclineAngle = 15f;
        [SerializeField, Tooltip("1인칭 동안 숨길 렌더러(얼굴·머리카락·안경·손에 든 무기). 비우면 캐릭터 전체를 숨깁니다. 몸은 보여야 고개를 숙였을 때 자연스럽습니다.")]
        private Renderer[] _firstPersonHiddenRenderers;
        [SerializeField, Tooltip("1인칭 시선이 따라갈 경비병 지점(머리 등). 비우면 경비병 위치 + 1.5m")]
        private Transform _guardLookTarget;
        [SerializeField, Min(0f), Tooltip("1인칭 시선이 대상을 따라가는 속도. 클수록 빠르게 따라갑니다.")]
        private float _lookFollowSharpness = 3f;
        [SerializeField, Range(10f, 90f), Tooltip("누운 채 얼굴(천장 방향)에서 고개를 돌릴 수 있는 최대 각도(도). 대상이 이보다 바깥이면 이 각도까지만 돌립니다.")]
        private float _maxNeckAngle = 60f;
        [SerializeField, Range(0f, 45f), Tooltip("베개를 벤 것처럼 얼굴이 발 쪽으로 들려 있는 각도(도)")]
        private float _headLiftAngle = 22f;
        [SerializeField, Tooltip("캡슐에서 분출할 가스 파티클")]
        private ParticleSystem[] _gasVents;

        [Header("Pod")]
        [SerializeField, Tooltip("캡슐 덮개 회전축(경첩). 로컬 Z축 기준으로 돌려 엽니다.")]
        private Transform _podLidPivot;
        [SerializeField, Tooltip("덮개가 열리는 각도(로컬 Z, 도)")]
        private float _podLidOpenAngle = -105f;
        [SerializeField, Tooltip("캐릭터가 누워 있는 기준점(캡슐 바닥 윗면 중앙). 이 오브젝트의 -Z 방향이 머리 쪽입니다.")]
        private Transform _podLyingAnchor;
        [SerializeField, Tooltip("캡슐에서 나온 뒤 서 있을 위치·방향")]
        private Transform _podExitPoint;
        [SerializeField, Tooltip("오류 때 튀는 스파크")]
        private ParticleSystem _errorSparks;
        [SerializeField, Tooltip("시작 페이드용 검은 화면")]
        private CanvasGroup _fadeOverlay;
        [SerializeField, Tooltip("오류 안내 문구")]
        private string _errorMessage = "[경고] 캡슐 시스템 오류 - 비상 개방";
        [SerializeField, Tooltip("적색 점등할 캡슐 램프")]
        private Renderer[] _alarmRenderers;
        [SerializeField] private Color _alarmColor = new Color(1f, 0.08f, 0.05f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private NavMeshAgent _guardAgent;
        private float _waitElapsed;
        private bool _isWaitingForAttack;
        private bool _hasRaisedCardKey;
        private bool _isFirstPerson;
        private enum Gaze { Canopy, Guard, Lid }
        private Gaze _gaze = Gaze.Canopy;
        private float _cameraShake;
        private Transform _playerVisual;
        private Vector3 _visualRestPosition;
        private Quaternion _visualRestRotation;
        private const float HipHeight = 0.97f;
        private Renderer[] _hiddenPlayerRenderers = System.Array.Empty<Renderer>();

        private void Awake()
        {
            if (_progress == null || _desk == null || _deskView == null || _deskFocus == null)
            {
                Debug.LogError("TutorialStartSequence: 연출 참조가 누락되었습니다.", this);
                enabled = false;
                return;
            }
            _guardAgent = _guard != null ? _guard.GetComponent<NavMeshAgent>() : null;
            foreach (ParticleSystem gas in _gasVents)
            {
                if (gas != null)
                {
                    gas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
            if (_fadeOverlay != null)
            {
                _fadeOverlay.alpha = 1f;
                _fadeOverlay.gameObject.SetActive(true);
            }
            PlacePlayerLying();
            // 첫 프레임부터 1인칭으로 시작하도록 Awake에서 켠다(시작 시 블렌드 없이 바로 컷).
            _gaze = Gaze.Canopy;
            EnterFirstPerson();
        }

        private void OnEnable()
        {
            if (_progress != null && _progress.Battle != null)
            {
                _progress.Battle.FieldBattleEnded += OnBattleEnded;
            }
        }

        private IEnumerator Start()
        {
            _progress.LockControls(this);
            _progress.SetObjective("상황 지켜보기");
            bool hasGuard = _guard != null && _guardButtonPoint != null;
            if (hasGuard)
            {
                _guard.enabled = false;
            }
            yield return null;
            // 1단계: 플레이 시작 - 캡슐 안에서 눈을 뜬다(페이드 인, 천장을 보다가 경비병 쪽으로 시선).
            yield return FadeOverlay(1f, 0f, _fadeInSeconds);
            yield return new WaitForSeconds(_openingHoldSeconds);
            // 2단계: 1인칭(캡슐 안), 조작 잠금. 시선은 경비병에게 고정.
            _gaze = Gaze.Guard;
            if (hasGuard)
            {
                yield return WalkGuardToButton();
                yield return new WaitForSeconds(_buttonPressSeconds);
            }
            SetAlarm(true);
            yield return new WaitForSeconds(_alarmToGasSeconds);
            SetGas(true);
            yield return new WaitForSeconds(_gasSeconds);
            // 캡슐 오류: 램프 깜빡임, 스파크, 덮개·화면 흔들림.
            _progress.ShowMessage(_errorMessage, _errorSeconds + 1f);
            _gaze = Gaze.Lid;
            yield return PodMalfunction();
            // 덮개 강제 개방 + 가스가 한꺼번에 빠져나온다.
            foreach (ParticleSystem gas in _gasVents)
            {
                if (gas != null)
                {
                    gas.Emit(120);
                }
            }
            yield return OpenLid();
            yield return new WaitForSeconds(_openToThirdPersonSeconds);
            // 3단계: 3인칭 전환 - 캐릭터가 일어나 캡슐 밖으로 나온 뒤 조작 해제.
            ExitFirstPerson();
            SetAlarm(false);
            yield return RiseFromPod();
            SetGas(false);
            yield return StepOutOfPod();
            _progress.UnlockControls(this);
            if (!hasGuard)
            {
                // 경비병이 배치되지 않았으면 처치 조건 없이 카드 키를 올린다.
                _hasRaisedCardKey = true;
                yield return RaiseCardKey();
                yield break;
            }
            _progress.SetObjective("경비병을 처치하거나 출입문 확인", _exitDoorTarget);
            _guard.enabled = true;
            _isWaitingForAttack = true;
        }

        private void LateUpdate()
        {
            if (!_isFirstPerson || _firstPersonCamera == null)
            {
                return;
            }
            Transform cameraTransform = _firstPersonCamera.transform;
            cameraTransform.position = GetEyePosition();
            float blend = 1f - Mathf.Exp(-_lookFollowSharpness * Time.deltaTime);
            cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, GetGazeRotation(), blend);
            if (_cameraShake > 0f)
            {
                cameraTransform.rotation *= Quaternion.Euler(Random.insideUnitSphere * _cameraShake);
            }
        }

        /// <summary>
        /// 누운 몸 기준 시선. 얼굴은 위(천장), 화면 위쪽은 머리 방향으로 고정하고,
        /// 목이 돌아갈 수 있는 범위(_maxNeckAngle) 안에서만 대상 쪽으로 고개를 돌린다.
        /// </summary>
        private Quaternion GetGazeRotation()
        {
            Vector3 head = _podLyingAnchor != null ? -_podLyingAnchor.forward : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, head);
            // 베개를 벤 만큼 얼굴이 발 쪽으로 들려 있다.
            Vector3 face = Quaternion.AngleAxis(-_headLiftAngle, side) * Vector3.up;
            Vector3 eye = GetEyePosition();
            Vector3 desired;
            switch (_gaze)
            {
                case Gaze.Canopy:
                    // 눈을 막 떴을 때: 덮개 너머 천장을 보고, 살짝 발 쪽을 향한다.
                    desired = face;
                    break;
                case Gaze.Lid:
                    // 오류 때: 덜컹거리는 덮개 쪽(경첩 반대편 위)을 올려다본다.
                    Vector3 toHinge = _podLidPivot != null ? Vector3.ProjectOnPlane(_podLidPivot.position - eye, Vector3.up).normalized : Vector3.zero;
                    desired = (face * 3f + Vector3.up * 1.5f + toHinge * 0.7f).normalized;
                    break;
                default:
                    Vector3 target = _guardLookTarget != null ? _guardLookTarget.position
                        : _guard != null ? _guard.transform.position + Vector3.up * 1.5f : _deskFocus.position;
                    desired = (target + Vector3.up * 0.3f - eye).normalized;
                    break;
            }
            // 머리 뒤쪽(정수리 너머)으로는 보지 않는다.
            if (Vector3.Dot(desired, head) > 0.25f)
            {
                desired = (desired - head * (Vector3.Dot(desired, head) - 0.25f)).normalized;
            }
            float angle = Vector3.Angle(face, desired);
            if (angle > _maxNeckAngle)
            {
                desired = Vector3.Slerp(face, desired, _maxNeckAngle / angle).normalized;
            }
            // 천장을 볼 때는 화면 위쪽이 머리 방향, 고개를 들어 앞을 볼수록 수평선이 바로 선다.
            float level = Mathf.Clamp01(Vector3.Angle(Vector3.up, desired) / 70f);
            Vector3 upHint = Vector3.Slerp(Quaternion.AngleAxis(-_headLiftAngle, side) * head, Vector3.up, level * level);
            return Quaternion.LookRotation(desired, upHint);
        }

        private Vector3 GetEyePosition()
        {
            if (_firstPersonHeadBone != null)
            {
                Vector3 head = _podLyingAnchor != null ? -_podLyingAnchor.forward : Vector3.forward;
                // 누운 상태: 얼굴은 위, 정수리는 머리 방향.
                Vector3 side = Vector3.Cross(Vector3.up, head);
                Quaternion recline = Quaternion.AngleAxis(-_lyingReclineAngle, side);
                return _firstPersonHeadBone.position + recline * (Vector3.up * _eyeOffsetFromHeadBone.x + head * _eyeOffsetFromHeadBone.y);
            }
            return _firstPersonEye != null ? _firstPersonEye.position : _firstPersonCamera.transform.position;
        }

        private void EnterFirstPerson()
        {
            if (_firstPersonCamera == null)
            {
                return;
            }
            _isFirstPerson = true;
            Transform cameraTransform = _firstPersonCamera.transform;
            cameraTransform.position = GetEyePosition();
            cameraTransform.rotation = GetGazeRotation();
            _firstPersonCamera.gameObject.SetActive(true);
            // 1인칭 동안 얼굴·머리카락처럼 카메라를 가리는 부분만 숨긴다(몸은 보이게 둔다).
            Transform player = _progress.Player;
            if (_firstPersonHiddenRenderers != null && _firstPersonHiddenRenderers.Length > 0)
            {
                _hiddenPlayerRenderers = System.Array.FindAll(_firstPersonHiddenRenderers, r => r != null && r.enabled);
            }
            else
            {
                _hiddenPlayerRenderers = player != null
                    ? System.Array.FindAll(player.GetComponentsInChildren<Renderer>(), r => r.enabled)
                    : System.Array.Empty<Renderer>();
            }
            foreach (Renderer r in _hiddenPlayerRenderers)
            {
                r.enabled = false;
            }
        }

        private void ExitFirstPerson()
        {
            _isFirstPerson = false;
            if (_firstPersonCamera != null)
            {
                _firstPersonCamera.gameObject.SetActive(false);
            }
            StartCoroutine(RevealPlayer(_hiddenPlayerRenderers));
            _hiddenPlayerRenderers = System.Array.Empty<Renderer>();
        }

        private IEnumerator RevealPlayer(Renderer[] renderers)
        {
            // 블렌드 초반 카메라가 아직 머리 위치 근처라 몸을 통과해 보이지 않도록 잠시 뒤에 보인다.
            yield return new WaitForSeconds(_revealPlayerDelay);
            foreach (Renderer r in renderers)
            {
                if (r != null)
                {
                    r.enabled = true;
                }
            }
        }

        private void Update()
        {
            if (!_isWaitingForAttack || _guard == null)
            {
                return;
            }
            if (_progress.IsInBattle || !_guard.gameObject.activeInHierarchy)
            {
                // I02 전투가 발생하면 대기 시간은 더 이상 증가하지 않는다.
                _isWaitingForAttack = false;
                return;
            }
            _waitElapsed += Time.deltaTime;
            if (_waitElapsed >= _guardWaitSeconds)
            {
                _isWaitingForAttack = false;
                StartCoroutine(ApproachPlayer());
            }
        }

        private void OnDisable()
        {
            if (_progress != null && _progress.Battle != null)
            {
                _progress.Battle.FieldBattleEnded -= OnBattleEnded;
            }
        }

        private IEnumerator WalkGuardToButton()
        {
            Vector3 destination = _guardButtonPoint.position;
            bool canWalk = _guardAgent != null && _guardAgent.isOnNavMesh;
            if (canWalk)
            {
                float distance = Vector3.Distance(_guard.transform.position, destination);
                _guardAgent.isStopped = false;
                _guardAgent.speed = Mathf.Max(0.5f, distance / Mathf.Max(0.1f, _guardWalkSeconds));
                _guardAgent.SetDestination(destination);
            }
            Vector3 start = _guard.transform.position;
            for (float elapsed = 0f; elapsed < _guardWalkSeconds; elapsed += Time.deltaTime)
            {
                if (!canWalk)
                {
                    _guard.transform.position = Vector3.Lerp(start, destination, elapsed / _guardWalkSeconds);
                }
                yield return null;
            }
            if (canWalk)
            {
                _guardAgent.ResetPath();
                _guardAgent.Warp(destination);
            }
            else
            {
                _guard.transform.position = destination;
            }
            _guard.transform.rotation = Quaternion.Euler(0f, _guardButtonPoint.eulerAngles.y, 0f);
        }

        private IEnumerator FadeOverlay(float from, float to, float seconds)
        {
            if (_fadeOverlay == null)
            {
                yield break;
            }
            _fadeOverlay.gameObject.SetActive(true);
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                _fadeOverlay.alpha = Mathf.Lerp(from, to, elapsed / seconds);
                yield return null;
            }
            _fadeOverlay.alpha = to;
            _fadeOverlay.gameObject.SetActive(to > 0.001f);
        }

        private void SetGas(bool isOn)
        {
            foreach (ParticleSystem gas in _gasVents)
            {
                if (gas == null)
                {
                    continue;
                }
                if (isOn)
                {
                    var main = gas.main;
                    main.loop = true;
                    gas.Play(true);
                }
                else
                {
                    gas.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        private IEnumerator PodMalfunction()
        {
            Quaternion lidRest = _podLidPivot != null ? _podLidPivot.localRotation : Quaternion.identity;
            float nextFlicker = 0f;
            float nextJolt = 0.25f;
            bool alarmOn = true;
            for (float elapsed = 0f; elapsed < _errorSeconds; elapsed += Time.deltaTime)
            {
                if (elapsed >= nextFlicker)
                {
                    alarmOn = !alarmOn;
                    SetAlarm(alarmOn);
                    nextFlicker = elapsed + Random.Range(0.04f, 0.16f);
                }
                if (elapsed >= nextJolt)
                {
                    if (_errorSparks != null)
                    {
                        _errorSparks.Emit(Random.Range(12, 24));
                    }
                    nextJolt = elapsed + Random.Range(0.3f, 0.5f);
                    _cameraShake = 2.5f;
                }
                _cameraShake = Mathf.MoveTowards(_cameraShake, 0.4f, Time.deltaTime * 8f);
                if (_podLidPivot != null)
                {
                    // 잠금이 풀리려는 듯 덮개가 덜컹거린다.
                    _podLidPivot.localRotation = lidRest * Quaternion.Euler(0f, 0f, -Mathf.Abs(Mathf.Sin(elapsed * 40f)) * _cameraShake * 1.2f);
                }
                yield return null;
            }
            SetAlarm(true);
            if (_podLidPivot != null)
            {
                _podLidPivot.localRotation = lidRest;
            }
        }

        private IEnumerator OpenLid()
        {
            if (_errorSparks != null)
            {
                _errorSparks.Emit(40);
            }
            _cameraShake = 3f;
            if (_podLidPivot == null)
            {
                yield break;
            }
            Quaternion closed = _podLidPivot.localRotation;
            Quaternion open = closed * Quaternion.Euler(0f, 0f, _podLidOpenAngle);
            for (float elapsed = 0f; elapsed < _lidOpenSeconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / _lidOpenSeconds;
                // 튕겨 열린 뒤 살짝 되돌아오는 느낌(오버슈트).
                float eased = 1f + 1.6f * Mathf.Pow(t - 1f, 3f) + 0.6f * Mathf.Pow(t - 1f, 2f);
                _podLidPivot.localRotation = Quaternion.SlerpUnclamped(closed, open, eased);
                _cameraShake = Mathf.MoveTowards(_cameraShake, 0f, Time.deltaTime * 6f);
                yield return null;
            }
            _podLidPivot.localRotation = open;
            _cameraShake = 0f;
        }

        private void PlacePlayerLying()
        {
            Transform player = _progress.Player;
            if (player == null || _podLyingAnchor == null)
            {
                return;
            }
            Animator animator = player.GetComponentInChildren<Animator>();
            _playerVisual = animator != null ? animator.transform : null;
            if (_playerVisual == null)
            {
                return;
            }
            _visualRestPosition = _playerVisual.localPosition;
            _visualRestRotation = _playerVisual.localRotation;
            var controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }
            // 플레이어 -Z가 머리 쪽이 되도록 앵커 방향을 그대로 쓴다.
            player.SetPositionAndRotation(_podLyingAnchor.position, _podLyingAnchor.rotation);
            SetVisualPose(LyingPose);
        }

        /// <summary>0 = 등을 대고 누움, 1 = 섰음. 엉덩이를 축으로 몸을 일으킨다(전용 애니메이션이 생기면 교체).</summary>
        private void SetVisualPose(float stand)
        {
            if (_playerVisual == null)
            {
                return;
            }
            float tilt = Mathf.Lerp(90f, 0f, stand);
            Quaternion bend = Quaternion.Euler(-tilt, 0f, 0f);
            Vector3 hip = Vector3.up * HipHeight;
            // 다리가 캡슐 바닥을 뚫지 않도록, 기울어진 다리 길이만큼은 엉덩이를 올린다.
            float hipY = Mathf.Max(Mathf.Lerp(0.18f, HipHeight, stand), HipHeight * Mathf.Cos(tilt * Mathf.Deg2Rad) + 0.02f);
            Vector3 hipTarget = Vector3.up * hipY;
            _playerVisual.localRotation = bend * _visualRestRotation;
            _playerVisual.localPosition = _visualRestPosition + hipTarget - bend * hip;
        }

        private float LyingPose => Mathf.Clamp01(_lyingReclineAngle / 90f);

        private IEnumerator RiseFromPod()
        {
            if (_playerVisual == null)
            {
                yield break;
            }
            // 먼저 상체를 반쯤 일으켜 주변을 살피고, 이어서 일어선다.
            for (float elapsed = 0f; elapsed < _riseSeconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / _riseSeconds;
                float stand = t < 0.45f ? Mathf.SmoothStep(LyingPose, 0.55f, t / 0.45f)
                    : t < 0.6f ? 0.55f
                    : Mathf.SmoothStep(0.55f, 1f, (t - 0.6f) / 0.4f);
                SetVisualPose(stand);
                yield return null;
            }
            SetVisualPose(1f);
        }

        private IEnumerator StepOutOfPod()
        {
            Transform player = _progress.Player;
            if (player == null || _podExitPoint == null)
            {
                yield break;
            }
            Vector3 start = player.position;
            Quaternion startRotation = player.rotation;
            for (float elapsed = 0f; elapsed < _stepOutSeconds; elapsed += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / _stepOutSeconds);
                Vector3 position = Vector3.Lerp(start, _podExitPoint.position, t);
                position.y += Mathf.Sin(t * Mathf.PI) * 0.45f;
                player.SetPositionAndRotation(position, Quaternion.Slerp(startRotation, _podExitPoint.rotation, t));
                yield return null;
            }
            player.SetPositionAndRotation(_podExitPoint.position, _podExitPoint.rotation);
            if (_playerVisual != null)
            {
                _playerVisual.localPosition = _visualRestPosition;
                _playerVisual.localRotation = _visualRestRotation;
            }
            Physics.SyncTransforms();
            var controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = true;
            }
        }

        private IEnumerator ApproachPlayer()
        {
            _guard.enabled = false;
            Quaternion start = _guard.transform.rotation;
            for (float elapsed = 0f; elapsed < _guardSurpriseSeconds; elapsed += Time.deltaTime)
            {
                Vector3 direction = Vector3.ProjectOnPlane(_progress.Player.position - _guard.transform.position, Vector3.up);
                if (direction.sqrMagnitude > 0.0001f)
                {
                    _guard.transform.rotation = Quaternion.Slerp(start, Quaternion.LookRotation(direction), elapsed / _guardSurpriseSeconds);
                }
                yield return null;
            }
            if (_progress.IsInBattle || !_guard.gameObject.activeInHierarchy)
            {
                yield break;
            }
            _guard.enabled = true;
            _guard.InvestigatePosition(_progress.Player.position);
        }

        private void OnBattleEnded(BattleOutcome outcome)
        {
            if (_hasRaisedCardKey || outcome != BattleOutcome.Victory || _guard == null || _guard.gameObject.activeSelf)
            {
                return;
            }
            _hasRaisedCardKey = true;
            _isWaitingForAttack = false;
            StartCoroutine(RaiseCardKey());
        }

        private IEnumerator RaiseCardKey()
        {
            _progress.LockControls(this);
            _progress.FocusCamera(_deskView, _deskFocus, _cardFocusSeconds);
            yield return new WaitForSeconds(0.3f);
            yield return _desk.Raise();
            yield return new WaitForSeconds(Mathf.Max(0f, _cardFocusSeconds - 0.5f));
            _progress.UnlockControls(this);
            _progress.SetObjective("콘솔 데스크에서 카드 키 획득", _desk.transform);
        }

        private void SetAlarm(bool isOn)
        {
            var block = new MaterialPropertyBlock();
            foreach (Renderer alarmRenderer in _alarmRenderers)
            {
                if (alarmRenderer == null)
                {
                    continue;
                }
                alarmRenderer.GetPropertyBlock(block);
                if (isOn)
                {
                    block.SetColor(BaseColorId, _alarmColor);
                    block.SetColor(EmissionColorId, _alarmColor * 2f);
                }
                else
                {
                    block.Clear();
                }
                alarmRenderer.SetPropertyBlock(block);
            }
        }
    }
}
