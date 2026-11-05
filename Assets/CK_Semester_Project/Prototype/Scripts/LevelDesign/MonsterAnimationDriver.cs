using Semester.Enemies;
using UnityEngine;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>
    /// 몬스터 모델 Animator 구동기. 모델(Animator가 있는 오브젝트)에 붙인다.
    /// - Speed: 몬스터 본체가 움직이는 속도(m/s)
    /// - InBattle: 전투 중이면 true (전투 대기 자세)
    /// - Attack: 전투 중(필드 AI가 꺼진 동안) 자리에서 튀어나가면 한 번 당기고, 제자리로 돌아오면 다음 공격을 기다린다.
    /// Animator에 해당 파라미터가 없으면 그 기능은 건너뛴다. 전투 코드는 건드리지 않고 이동만 보고 판단한다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class MonsterAnimationDriver : MonoBehaviour
    {
        public bool IsCutsceneControlled { get; set; }
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int InBattleHash = Animator.StringToHash("InBattle");

        [SerializeField, Min(0.05f), Tooltip("제자리에서 이만큼(m) 벗어나면 공격으로 봅니다.")]
        private float _lungeDistance = 0.25f;
        [SerializeField, Min(0.01f), Tooltip("기준 위치에서 이만큼(m) 안으로 돌아오면 다음 공격을 받을 준비를 합니다.")]
        private float _returnDistance = 0.08f;
        [SerializeField, Min(0.1f), Tooltip("한 프레임에 이보다 멀리(m) 움직이면 순간이동(전투 장소 이동)으로 보고 무시합니다.")]
        private float _teleportDistance = 1.2f;
        [SerializeField, Min(0f), Tooltip("Speed 값을 부드럽게 바꾸는 정도. 클수록 빨리 따라갑니다.")]
        private float _speedSmoothing = 8f;

        private Animator _animator;
        private EnemyStateMachine _fieldAi;
        private EncounterFollower _follower;
        private Transform _body;
        private Vector3 _restPosition;
        private Vector3 _lastPosition;
        private bool _attacking;
        private bool _hasSpeed;
        private bool _hasAttack;
        private bool _hasInBattle;
        private float _speed;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _fieldAi = GetComponentInParent<EnemyStateMachine>();
            _follower = GetComponentInParent<EncounterFollower>();
            _body = _fieldAi != null ? _fieldAi.transform : transform;
            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                _hasSpeed |= parameter.nameHash == SpeedHash && parameter.type == AnimatorControllerParameterType.Float;
                _hasAttack |= parameter.nameHash == AttackHash && parameter.type == AnimatorControllerParameterType.Trigger;
                _hasInBattle |= parameter.nameHash == InBattleHash && parameter.type == AnimatorControllerParameterType.Bool;
            }
        }

        private void OnEnable()
        {
            if (_body == null)
            {
                return;
            }
            _restPosition = _lastPosition = _body.position;
            _attacking = false;
            _speed = 0f;
        }

        private void LateUpdate()
        {
            if (IsCutsceneControlled)
            {
                if (_body != null) _lastPosition = _body.position;
                return;
            }
            Vector3 position = _body.position;
            Vector3 delta = position - _lastPosition;
            _lastPosition = position;
            float step = delta.magnitude;
            // 동료 몬스터는 필드에서도 AI를 꺼 두므로 리더 상태로 판단한다.
            bool inBattle = _follower != null ? !_follower.IsFollowing : _fieldAi != null && !_fieldAi.enabled;
            bool teleported = step > _teleportDistance;

            if (_hasSpeed)
            {
                delta.y = 0f;
                float target = teleported || inBattle || Time.deltaTime <= 0f ? 0f : delta.magnitude / Time.deltaTime;
                _speed = Mathf.Lerp(_speed, target, 1f - Mathf.Exp(-_speedSmoothing * Time.deltaTime));
                _animator.SetFloat(SpeedHash, _speed);
            }

            if (_hasInBattle)
            {
                _animator.SetBool(InBattleHash, inBattle);
            }

            if (!_hasAttack)
            {
                return;
            }
            // 탐색 중이거나 전투 장소로 순간이동했으면 지금 자리를 기준 위치로 삼는다.
            if (!inBattle || teleported)
            {
                _restPosition = position;
                _attacking = false;
                return;
            }
            float offset = Vector3.Distance(position, _restPosition);
            if (!_attacking && offset > _lungeDistance)
            {
                _attacking = true;
                _animator.SetTrigger(AttackHash);
            }
            else if (_attacking && offset < _returnDistance)
            {
                _attacking = false;
            }
        }
    }
}
