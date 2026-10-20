using Semester.Enemies;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace CK.SemesterProject.Tutorial
{
    // 이펙트 작업장은 전투를 시작하지 않고 배치 기준만 유지한다.
    [DefaultExecutionOrder(-100)]
    public sealed class BattleVfxTestController : MonoBehaviour
    {
        [SerializeField] private PlayerMovement _player;
        [SerializeField] private EnemyStateMachine _dummy;

        private Pose _playerHome;
        private Pose _dummyHome;

        private void Awake()
        {
            _playerHome = new Pose(_player.transform.position, _player.transform.rotation);
            _dummyHome = new Pose(_dummy.transform.position, _dummy.transform.rotation);
            StopDummy();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }
            if (keyboard.f5Key.wasPressedThisFrame)
            {
                RestartPreview();
            }
        }

        public void RestartPreview()
        {
            _player.Teleport(_playerHome.position);
            _player.transform.rotation = _playerHome.rotation;
            StopDummy();
            _dummy.transform.SetPositionAndRotation(_dummyHome.position, _dummyHome.rotation);
            _dummy.gameObject.SetActive(true);
            Physics.SyncTransforms();
        }

        private void StopDummy()
        {
            _dummy.enabled = false;
            NavMeshAgent agent = _dummy.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
            }
        }

    }
}
