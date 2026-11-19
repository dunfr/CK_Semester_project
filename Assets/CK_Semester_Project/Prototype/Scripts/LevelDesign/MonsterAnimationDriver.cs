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
        [Header("공격 타격 시점")]
        [SerializeField, Tooltip("공격 모션의 Animator 상태 이름")]
        private string _attackState = "Attack";
        [SerializeField, Min(0f), Tooltip("공격 모션에서 타격이 상대에게 닿는 프레임(클립 첫 프레임을 0으로 센 번호). 이 순간에 플레이어 체력이 줄고 피격 모션이 나옵니다. 0이면 지정하지 않은 것으로 보고 기존 연출 타이밍을 씁니다.")]
        private float _attackHitFrame;

        // 트리거를 당기고 이 시간(초) 안에 공격 상태로 들어가지 못하면 닿은 것으로 처리해 전투가 멈추지 않게 한다.
        private const float AttackStateWaitSeconds = 0.5f;

        private readonly System.Collections.Generic.List<AnimatorClipInfo> _clipInfos = new System.Collections.Generic.List<AnimatorClipInfo>();
        private int _attackStateHash;
        private bool _hasAttackState;
        private bool _isWatchingHit;
        private bool _sawAttackState;
        private float _watchSeconds;

        /// <summary>공격 모션과 타격 프레임이 모두 준비돼 있어, 타격 순간을 모션에서 읽을 수 있는지.</summary>
        public bool HasAttackHitTiming => _hasAttack && _hasAttackState && _attackHitFrame > 0f;
        /// <summary>공격 모션을 시작한 횟수. 전투 쪽은 이 값이 바뀌었는지로 이번 행동의 공격 모션이 시작됐는지 안다.</summary>
        public int AttackCount { get; private set; }
        /// <summary>가장 최근에 시작한 공격 모션이 타격 프레임을 지났는지.</summary>
        public bool HasAttackLanded { get; private set; }

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
            _attackStateHash = Animator.StringToHash(_attackState);
            _hasAttackState = !string.IsNullOrEmpty(_attackState) && _animator.HasState(0, _attackStateHash);
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
            _isWatchingHit = false;
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
                UpdateAttackHit();
                return;
            }
            float offset = Vector3.Distance(position, _restPosition);
            if (!_attacking && offset > _lungeDistance)
            {
                _attacking = true;
                _animator.SetTrigger(AttackHash);
                AttackCount++;
                HasAttackLanded = false;
                _isWatchingHit = true;
                _sawAttackState = false;
                _watchSeconds = 0f;
            }
            else if (_attacking && offset < _returnDistance)
            {
                _attacking = false;
            }
            UpdateAttackHit();
        }

        // 공격 모션이 타격 프레임을 지나는 순간 HasAttackLanded를 켠다.
        private void UpdateAttackHit()
        {
            if (!_isWatchingHit)
            {
                return;
            }
            if (TryGetAttackFrame(out float frame))
            {
                _sawAttackState = true;
                if (frame >= _attackHitFrame)
                {
                    LandAttack();
                }
                return;
            }
            // 타격 프레임 전에 모션이 끊겼거나, 트리거를 당겼는데 공격 상태로 들어가지 못했으면 닿은 것으로 본다.
            _watchSeconds += Time.deltaTime;
            if (_sawAttackState || _watchSeconds > AttackStateWaitSeconds)
            {
                LandAttack();
            }
        }

        private void LandAttack()
        {
            HasAttackLanded = true;
            _isWatchingHit = false;
        }

        // 지금 공격 상태를 재생 중이면 그 클립에서 몇 번째 프레임인지 돌려준다. 공격 상태로 넘어가는 중이면 넘어갈 상태 기준이다.
        private bool TryGetAttackFrame(out float frame)
        {
            frame = 0f;
            if (!_hasAttackState)
            {
                return false;
            }
            AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
            bool isNext = false;
            if (_animator.IsInTransition(0))
            {
                AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(0);
                if (next.shortNameHash == _attackStateHash)
                {
                    info = next;
                    isNext = true;
                }
            }
            if (info.shortNameHash != _attackStateHash)
            {
                return false;
            }
            _clipInfos.Clear();
            if (isNext)
            {
                _animator.GetNextAnimatorClipInfo(0, _clipInfos);
            }
            else
            {
                _animator.GetCurrentAnimatorClipInfo(0, _clipInfos);
            }
            if (_clipInfos.Count == 0 || _clipInfos[0].clip == null)
            {
                return false;
            }
            AnimationClip clip = _clipInfos[0].clip;
            frame = info.normalizedTime * clip.length * clip.frameRate;
            return true;
        }
    }
}
