using CK.SemesterProject.Battle;
using Semester.Enemies;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CK.SemesterProject.Tutorial
{
    // 테스트 씬 전용 조합 선택. 실제 계산·AI·연출은 TutorialBattleController를 사용한다.
    public sealed class MonsterAiTestPanel : MonoBehaviour
    {
        [SerializeField, Tooltip("전투 진행 컴포넌트")]
        private TutorialBattleController _controller;
        [SerializeField, Tooltip("각인 2기, 잔상 2기, 망각 2기 순서")]
        private EnemyStateMachine[] _monsters;
        [SerializeField, Tooltip("단독 3개, 동일 속성 3개, 혼합 속성 3개 조합 버튼")]
        private Button[] _presets;
        [SerializeField, Tooltip("일반, 플레이어 선제, 몬스터 선제 선택 버튼")]
        private Button[] _entryButtons;
        [SerializeField, Tooltip("모든 상태와 적을 초기화하는 버튼")]
        private Button _reset;
        [SerializeField, Tooltip("필드에서 표시하는 테스트 선택 패널")]
        private GameObject _selection;
        [SerializeField, Tooltip("선택한 진입 방식 표시")]
        private TMP_Text _entryLabel;
        [SerializeField, Tooltip("테스트 씬에서는 필드 마우스를 버튼 선택에 사용")]
        private CameraController _fieldCamera;
        private readonly int[][] _rosters =
        {
            new[] { 0 }, new[] { 2 }, new[] { 4 },
            new[] { 0, 1 }, new[] { 2, 3 }, new[] { 4, 5 },
            new[] { 0, 2 }, new[] { 2, 4 }, new[] { 4, 0 }
        };
        private Pose[] _homes;
        private UnityAction[] _presetActions;
        private UnityAction[] _entryActions;
        private BattleEntryCondition _entry;

        private void Start()
        {
            if (_controller == null || _monsters == null || _monsters.Length != 6
                || _presets == null || _presets.Length != 9 || _entryButtons == null || _entryButtons.Length != 3
                || _reset == null || _selection == null || _entryLabel == null || _fieldCamera == null)
            {
                Debug.LogError("MonsterAiTestPanel: 테스트 씬 참조가 누락되었습니다.", this);
                enabled = false;
                return;
            }
            _homes = new Pose[6];
            for (int i = 0; i < _monsters.Length; i++)
            {
                _homes[i] = new Pose(_monsters[i].transform.position, _monsters[i].transform.rotation);
            }
            _fieldCamera.enabled = false;
            _presetActions = new UnityAction[9];
            for (int i = 0; i < _presets.Length; i++)
            {
                int index = i;
                _presetActions[i] = () => StartPreset(index);
                _presets[i].onClick.AddListener(_presetActions[i]);
            }
            _entryActions = new UnityAction[3];
            for (int i = 0; i < _entryButtons.Length; i++)
            {
                int index = i;
                _entryActions[i] = () => SelectEntry(index);
                _entryButtons[i].onClick.AddListener(_entryActions[i]);
            }
            _reset.onClick.AddListener(ResetTest);
            SelectEntry(0);
            ResetTest();
        }

        private void Update()
        {
            bool inBattle = _controller.IsInBattle;
            _selection.SetActive(!inBattle);
            if (!inBattle)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public void SelectEntry(int index)
        {
            _entry = index == 1 ? BattleEntryCondition.PlayerInitiated
                : index == 2 ? BattleEntryCondition.MonsterCollision : BattleEntryCondition.Normal;
            _entryLabel.text = "진입: " + (index == 1 ? "플레이어 선제 +1행동" : index == 2 ? "몬스터 선제 +1행동 / 메모리 +1" : "일반 (메모리 순서)");
        }

        public void StartPreset(int index)
        {
            if (index < 0 || index >= _rosters.Length)
            {
                return;
            }
            ResetTest();
            int[] roster = _rosters[index];
            var members = new EnemyStateMachine[roster.Length];
            for (int i = 0; i < roster.Length; i++)
            {
                members[i] = _monsters[roster[i]];
            }
            if (!_controller.BeginBattle(members[0], _entry, members))
            {
                _entryLabel.text = "전투 진입 실패: Console과 전투 자리 설정을 확인하세요.";
            }
        }

        public void ResetTest()
        {
            _controller.CancelBattle();
            PlayerSessionState.Current.RestoreFull();
            for (int i = 0; i < _monsters.Length; i++)
            {
                EnemyStateMachine monster = _monsters[i];
                NavMeshAgent agent = monster.GetComponent<NavMeshAgent>();
                agent.enabled = false;
                monster.transform.SetPositionAndRotation(_homes[i].position, _homes[i].rotation);
                monster.gameObject.SetActive(true);
                monster.enabled = true;
                agent.enabled = true;
            }
            Physics.SyncTransforms();
            _selection.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDestroy()
        {
            if (_presetActions == null)
            {
                return;
            }
            for (int i = 0; i < _presetActions.Length; i++)
            {
                if (_presets[i] != null)
                {
                    _presets[i].onClick.RemoveListener(_presetActions[i]);
                }
            }
            for (int i = 0; i < _entryActions.Length; i++)
            {
                if (_entryButtons[i] != null)
                {
                    _entryButtons[i].onClick.RemoveListener(_entryActions[i]);
                }
            }
            if (_reset != null)
            {
                _reset.onClick.RemoveListener(ResetTest);
            }
        }
    }
}
