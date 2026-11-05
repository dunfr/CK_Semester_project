using CK.SemesterProject.Battle;
using Semester.Enemies;
using UnityEngine;
using UnityEngine.Events;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>
    /// 보스룸 강제 전투. 플레이어가 영역에 들어오면 보스와 1:1 전투를 바로 시작한다(피할 수 없다).
    /// 지면 재도전 지점(보스룸 입구 밖)에서 다시 시작하고, 다시 들어오면 또 싸운다. 이기면 한 번만 _bossDefeated를 실행한다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BossBattleTrigger : MonoBehaviour
    {
        // 플레이어 몸통 높이(발 기준)
        private const float BodyHeight = 0.9f;

        [SerializeField] private LevelProgress _progress;
        [SerializeField, Tooltip("싸울 보스(TutorialBattleController의 Enemies 목록에 있어야 합니다)")]
        private EnemyStateMachine _boss;
        [SerializeField, Tooltip("전투에서 지면 다시 시작할 위치(보스룸 입구 밖). 비우면 기존 체크포인트를 씁니다.")]
        private Transform _retryPoint;
        [SerializeField, Tooltip("보스를 이긴 뒤 실행(클리어 화면 등)")]
        private UnityEvent _bossDefeated = new UnityEvent();

        private BoxCollider _box;
        private bool _defeated;

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            _box.isTrigger = true;
            if (_progress == null || _progress.Battle == null || _boss == null)
            {
                Debug.LogError(name + ": LevelProgress·보스 참조가 없습니다.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_progress != null && _progress.Battle != null)
            {
                _progress.Battle.FieldBattleEnded += OnBattleEnded;
            }
        }

        private void OnDisable()
        {
            if (_progress != null && _progress.Battle != null)
            {
                _progress.Battle.FieldBattleEnded -= OnBattleEnded;
            }
        }

        // 전투 장소로 순간이동하면 OnTriggerExit가 오지 않으므로 매 프레임 위치로 판단한다.
        // 전투 직후 대기 시간 등으로 시작하지 못해도 안에 있는 동안 다시 시도한다.
        private void Update()
        {
            Transform player = _progress.Player;
            if (_defeated || player == null || _boss == null || !_boss.isActiveAndEnabled || _progress.Battle.IsInBattle
                || !_box.bounds.Contains(player.position + Vector3.up * BodyHeight))
            {
                return;
            }
            if (_progress.Battle.BeginBattle(_boss, BattleEntryCondition.Normal, new[] { _boss }) && _retryPoint != null)
            {
                _progress.Battle.SetRespawnPoint(_retryPoint.position);
            }
        }

        private void OnBattleEnded(BattleOutcome outcome)
        {
            if (_defeated || outcome != BattleOutcome.Victory || _boss == null || _boss.gameObject.activeSelf)
            {
                return;
            }
            _defeated = true;
            _bossDefeated.Invoke();
        }

        private void OnDrawGizmos()
        {
            BoxCollider box = _box != null ? _box : GetComponent<BoxCollider>();
            if (box == null)
            {
                return;
            }
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.2f, 0.4f, 0.15f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(1f, 0.2f, 0.4f, 1f);
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
