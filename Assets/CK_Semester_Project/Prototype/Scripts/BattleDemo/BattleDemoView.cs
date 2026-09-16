using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.Battle.Demo
{
    [RequireComponent(typeof(BattleDemoController))]
    public sealed class BattleDemoView : MonoBehaviour
    {
        [SerializeField, Tooltip("씬에 저장된 전투 HUD")]
        private RectTransform _hud;
        [SerializeField, Tooltip("전투 카메라")]
        private Camera _camera;
        [SerializeField, Tooltip("플레이어, 센티널 A, 센티널 B 순서의 모델")]
        private Transform[] _figures;

        private readonly string[] _ids = { "player", "sentinel_a", "sentinel_b" };
        private readonly string[] _skills = { "strike", "heavy", "disrupt" };
        private BattleDemoController _controller;
        private Text[] _texts;
        private Button[] _buttons;
        private Image[] _images;

        private void Awake()
        {
            _controller = GetComponent<BattleDemoController>();
            if (_hud == null || _camera == null || _figures == null || _figures.Length != 3)
            {
                Debug.LogError("BattleDemoView: HUD, 카메라와 모델 참조가 필요합니다.", this);
                enabled = false;
                return;
            }
            _texts = _hud.GetComponentsInChildren<Text>(true);
            _buttons = _hud.GetComponentsInChildren<Button>(true);
            _images = _hud.GetComponentsInChildren<Image>(true);
            foreach (Button button in _buttons)
            {
                string command = button.name;
                button.onClick.AddListener(() => Execute(command));
            }
        }

        private void LateUpdate()
        {
            if (_controller.Snapshot == null)
            {
                return;
            }
            BattleSnapshot snapshot = _controller.Snapshot;
            CombatantState player = _controller.GetDisplayedState("player");
            SetText("Health", player.Hp + " / " + player.Data.MaxHp);
            SetText("Memory", "메모리  " + player.Memory);
            SetText("Rage", "폭주  " + player.RageEnergy);
            SetText("InvestmentLabel", "메모리 투자  " + _controller.MemoryInvestment + "  +");
            SetText("Round", snapshot.Round.ToString("00") + "  /  ROUND");
            SetText("Status", player.IsDead ? "전투 불능" : player.IsDefending ? "방어 중" : player.SkippedTurns > 0 ? "행동 불능" : "");
            SetFill("HealthFill", (float)player.Hp / player.Data.MaxHp);
            SetFill("MemoryFill", player.Memory / 40f);
            SetFill("RageFill", player.RageEnergy / 100f);
            for (int i = 0; i < 3; i++)
            {
                string id = i < snapshot.TurnOrder.Count ? snapshot.TurnOrder[i] : null;
                SetText("Order" + i, id == null ? "—" : _controller.GetName(id));
                SetText("OrderMemory" + i, id == null ? "" : "MEM " + snapshot.Combatants.First(unit => unit.InstanceId == id).Memory);
                SetColor("OrderCard" + i, id == snapshot.CurrentActorId ? new Color(0.08f, 0.38f, 0.48f, 0.94f) : new Color(0.025f, 0.04f, 0.065f, 0.8f));
                SetColor("Skill" + i, _controller.SelectedSkillId == _skills[i] ? new Color(0.12f, 0.42f, 0.51f, 0.95f) : new Color(0.035f, 0.055f, 0.075f, 0.92f));
            }
            foreach (Button button in _buttons)
            {
                button.interactable = button.name == "Restart" || _controller.IsPlayerInput;
            }
            for (int i = 1; i < 3; i++)
            {
                CombatantState enemy = _controller.GetDisplayedState(_ids[i]);
                Button target = _buttons.First(button => button.name == "Target" + i);
                target.gameObject.SetActive(!enemy.IsDead);
                target.interactable = _controller.IsPlayerInput && !enemy.IsDead;
                Vector3 screen = _camera.WorldToViewportPoint(_figures[i].position + Vector3.up * 2.8f);
                RectTransform rect = (RectTransform)target.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(screen.x, screen.y);
                rect.anchoredPosition = Vector2.zero;
                SetText("EnemyName" + i, (_controller.SelectedTargetId == _ids[i] ? "◇  " : "") + _controller.GetName(_ids[i]));
                SetFill("EnemyHealth" + i, (float)enemy.Hp / enemy.Data.MaxHp);
                SetText("Damage" + i, "");
            }
            SetText("PlayerDamage", "");
            BattleActionResult result = _controller.PendingResult;
            if (result != null && _controller.PresentationProgress >= 0.4f)
            {
                foreach (BattleStateChange change in result.Changes)
                {
                    int index = System.Array.IndexOf(_ids, change.After.InstanceId);
                    if (change.HpDelta < 0 && index >= 0)
                    {
                        SetText(index == 0 ? "PlayerDamage" : "Damage" + index, (-change.HpDelta).ToString());
                    }
                }
            }
            SetText("Notice", _controller.LastError != BattleActionError.None ? "행동할 수 없습니다. 스킬과 메모리를 확인하세요."
                : snapshot.Phase == BattlePhase.AwaitingPresentation ? "" : _controller.IsPlayerInput ? "대상을 선택하세요" : "적의 행동");
            Text outcome = _texts.First(label => label.name == "Outcome");
            outcome.transform.parent.gameObject.SetActive(snapshot.Phase == BattlePhase.Finished);
            outcome.text = snapshot.Outcome == BattleOutcome.Victory ? "승리" : snapshot.Outcome == BattleOutcome.Defeat ? "패배" : "무승부";
        }

        private void OnDestroy()
        {
            if (_buttons == null)
            {
                return;
            }
            foreach (Button button in _buttons)
            {
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                }
            }
        }

        private void Execute(string command)
        {
            switch (command)
            {
                case "Skill0": _controller.SelectSkill("strike"); break;
                case "Skill1": _controller.SelectSkill("heavy"); break;
                case "Skill2": _controller.SelectSkill("disrupt"); break;
                case "Target1": _controller.SelectTarget("sentinel_a"); break;
                case "Target2": _controller.SelectTarget("sentinel_b"); break;
                case "Attack": _controller.Attack(); break;
                case "Defend": _controller.Defend(); break;
                case "Investment": _controller.CycleInvestment(); break;
                case "Restart": _controller.RestartScenario(_controller.Scenario); break;
            }
        }

        private void SetText(string name, string value)
        {
            _texts.First(label => label.name == name).text = value;
        }

        private void SetFill(string name, float value)
        {
            _images.First(image => image.name == name).fillAmount = Mathf.Clamp01(value);
        }

        private void SetColor(string name, Color color)
        {
            _images.First(image => image.name == name).color = color;
        }
    }
}
