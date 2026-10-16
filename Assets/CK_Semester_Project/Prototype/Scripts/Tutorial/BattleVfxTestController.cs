using System.Collections;
using CK.SemesterProject.Battle;
using Semester.Enemies;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CK.SemesterProject.Tutorial
{
    // 테스트 씬에서만 전투를 초기화한다. 스킬 계산과 이펙트 생성은 기존 전투 시스템을 사용한다.
    public sealed class BattleVfxTestController : MonoBehaviour
    {
        public const string Instructions = "이펙트 테스트\n↑/↓ 스킬 선택 · Enter 재생 · F5 재시작 · 우클릭 드래그 회전";

        [SerializeField] private TutorialBattleController _battle;
        [SerializeField] private PlayerMovement _player;
        [SerializeField] private EnemyStateMachine _dummy;
        [SerializeField] private Button _restart;
        [SerializeField] private TMP_Text _status;

        private Pose _playerHome;
        private Pose _dummyHome;
        private bool _selectTargetWhenReady;

        public TutorialBattleController Battle => _battle;

        private IEnumerator Start()
        {
            _playerHome = new Pose(_player.transform.position, _player.transform.rotation);
            _dummyHome = new Pose(_dummy.transform.position, _dummy.transform.rotation);
            _restart.onClick.AddListener(RestartBattle);
            yield return null;
            RestartBattle();
        }

        private void Update()
        {
            if (_selectTargetWhenReady && _battle.CanChooseAction)
            {
                _battle.SelectTarget(0);
                _selectTargetWhenReady = false;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }
            if (keyboard.f5Key.wasPressedThisFrame)
            {
                RestartBattle();
            }
        }

        public void NextStage()
        {
            _battle.CancelBattle();
            _battle.CycleTestSkillStage();
            RestartBattle();
        }

        public void RestartBattle()
        {
            _battle.CancelBattle();
            PlayerSessionState.Current.RestoreFull();
            _player.Teleport(_playerHome.position);
            _player.transform.rotation = _playerHome.rotation;
            NavMeshAgent agent = _dummy.GetComponent<NavMeshAgent>();
            agent.enabled = false;
            _dummy.transform.SetPositionAndRotation(_dummyHome.position, _dummyHome.rotation);
            _dummy.gameObject.SetActive(true);
            _dummy.enabled = true;
            agent.enabled = true;
            Physics.SyncTransforms();
            bool started = _battle.BeginBattle(_dummy, BattleEntryCondition.PlayerInitiated);
            _selectTargetWhenReady = started;
            _status.text = started ? Instructions
                : "전투 진입 실패 · NavMesh와 Console을 확인하세요.";
        }

        private void OnDestroy()
        {
            if (_restart != null)
            {
                _restart.onClick.RemoveListener(RestartBattle);
            }
        }
    }
}
