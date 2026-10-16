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
        [SerializeField] private TutorialBattleController _battle;
        [SerializeField] private PlayerMovement _player;
        [SerializeField] private EnemyStateMachine _dummy;
        [SerializeField] private Button _restart;
        [SerializeField] private Button _stage;
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
            _stage.onClick.AddListener(NextStage);
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
            if (keyboard.f6Key.wasPressedThisFrame)
            {
                NextStage();
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
            _status.text = started ? "이펙트 테스트 · 강화 " + _battle.TestSkillStage
                + "단계\n스킬 클릭 / ↑↓ 선택 · Enter 사용 · F 투자 · 우클릭 드래그 회전"
                : "전투 진입 실패 · NavMesh와 Console을 확인하세요.";
        }

        private void OnDestroy()
        {
            if (_restart != null)
            {
                _restart.onClick.RemoveListener(RestartBattle);
            }
            if (_stage != null)
            {
                _stage.onClick.RemoveListener(NextStage);
            }
        }
    }
}
