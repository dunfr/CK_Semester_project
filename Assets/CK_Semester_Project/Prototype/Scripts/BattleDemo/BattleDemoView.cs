using System.Linq;
using UnityEngine;

namespace CK.SemesterProject.Battle.Demo
{
    [RequireComponent(typeof(BattleDemoController))]
    public sealed class BattleDemoView : MonoBehaviour
    {
        private const float CanvasWidth = 1440f;
        private const float CanvasHeight = 900f;
        private static readonly Color Ink = new Color(0.035f, 0.05f, 0.08f);
        private static readonly Color Panel = new Color(0.065f, 0.09f, 0.13f);
        private static readonly Color Muted = new Color(0.56f, 0.65f, 0.73f);
        private static readonly Color Cyan = new Color(0.34f, 0.92f, 0.83f);
        private static readonly Color Pink = new Color(1f, 0.43f, 0.52f);

        private BattleDemoController _controller;
        private Font _font;
        private GUIStyle _label;
        private GUIStyle _button;

        private void Awake()
        {
            _controller = GetComponent<BattleDemoController>();
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Arial" }, 20);
        }

        private void OnDestroy()
        {
            if (_font != null)
            {
                Destroy(_font);
            }
        }

        private void OnGUI()
        {
            if (_controller.Snapshot == null)
            {
                return;
            }
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { font = _font, padding = new RectOffset(0, 0, 0, 0) };
                _button = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            }
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / CanvasWidth, Screen.height / CanvasHeight);
            var origin = new Vector3((Screen.width - CanvasWidth * scale) * 0.5f,
                (Screen.height - CanvasHeight * scale) * 0.5f, 0f);
            GUI.matrix = Matrix4x4.TRS(origin, Quaternion.identity, Vector3.one * scale);

            DrawHeader();
            DrawOrder();
            DrawStatusCards();
            DrawCommands();
            DrawHistory();
            DrawResult();
            GUI.matrix = previous;
        }

        private void DrawHeader()
        {
            Fill(new Rect(0, 0, 1440, 205), Ink);
            Text(new Rect(36, 22, 520, 24), "CK  /  BATTLE SYSTEM     ·     PROTOTYPE 01", 14, Cyan);
            Text(new Rect(36, 52, 650, 55), "전투 코어 플레이그라운드", 34, Color.white);
            Text(new Rect(38, 106, 700, 25), "스킬과 대상을 고른 뒤 공격하세요. 메모리가 높은 순서로 행동합니다.", 16, Muted);
            string[] scenarios = { "일반 전투", "메모리 동률", "행동 불능" };
            for (int i = 0; i < scenarios.Length; i++)
            {
                if (Button(new Rect(926 + i * 158, 34, 148, 39), scenarios[i], _controller.Scenario == i))
                {
                    _controller.RestartScenario(i);
                }
            }
            if (Button(new Rect(926, 85, 225, 36), "자동 진행  " + (_controller.AutoAdvance ? "ON" : "OFF"), _controller.AutoAdvance))
            {
                _controller.SetAutoAdvance(!_controller.AutoAdvance);
            }
            if (Button(new Rect(1167, 85, 223, 36), "처음부터 다시"))
            {
                _controller.RestartScenario(_controller.Scenario);
            }
        }

        private void DrawOrder()
        {
            BattleSnapshot state = _controller.Snapshot;
            Text(new Rect(38, 153, 130, 30), "ROUND  " + state.Round.ToString("00"), 18, Color.white);
            float x = 202;
            foreach (string id in state.TurnOrder)
            {
                bool current = id == state.CurrentActorId;
                Fill(new Rect(x, 145, 246, 44), current ? new Color(0.11f, 0.29f, 0.29f) : Panel);
                CombatantState unit = state.Combatants.First(item => item.InstanceId == id);
                Text(new Rect(x + 14, 154, 230, 28), _controller.GetName(id) + "   /   MEM " + unit.Memory, 17,
                    current ? Cyan : Color.white);
                x += 258;
            }
            string phase = state.Phase == BattlePhase.AwaitingPresentation ? "행동 연출 중"
                : state.Phase == BattlePhase.Finished ? "전투 종료"
                : _controller.IsPlayerInput ? "플레이어 입력 대기" : "몬스터 행동 대기";
            Text(new Rect(1060, 152, 330, 32), phase, 18, Cyan, TextAnchor.MiddleRight);
            Fill(new Rect(36, 204, 1368, 1), new Color(0.17f, 0.23f, 0.29f));
        }

        private void DrawStatusCards()
        {
            string[] ids = { "player", "sentinel_a", "sentinel_b" };
            float[] positions = { 58, 626, 1036 };
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                CombatantState unit = _controller.GetDisplayedState(id);
                float x = positions[i];
                Color accent = i == 0 ? Cyan : Pink;
                bool selected = id == _controller.SelectedTargetId && _controller.IsPlayerInput && !unit.IsDead;
                Fill(new Rect(x, 469, 344, 132), selected ? new Color(0.23f, 0.13f, 0.18f, 0.96f) : Panel);
                Fill(new Rect(x, 469, 344, 3), unit.IsDead ? Muted : accent);
                Text(new Rect(x + 16, 482, 240, 30), _controller.GetName(id), 22, unit.IsDead ? Muted : Color.white);
                Text(new Rect(x + 230, 485, 98, 26), unit.IsDead ? "사망" : selected ? "선택됨" : "", 14, accent, TextAnchor.MiddleRight);
                Text(new Rect(x + 16, 522, 190, 24), "HP  " + unit.Hp + " / " + unit.Data.MaxHp, 16, Color.white);
                Text(new Rect(x + 196, 522, 132, 24), "MEM  " + unit.Memory, 16, Cyan, TextAnchor.MiddleRight);
                Fill(new Rect(x + 16, 557, 312, 7), new Color(0.15f, 0.2f, 0.25f));
                Fill(new Rect(x + 16, 557, 312f * unit.Hp / unit.Data.MaxHp, 7), accent);
                Text(new Rect(x + 16, 572, 312, 22), unit.SkippedTurns > 0 ? "행동 불능 · " + unit.SkippedTurns + "회 남음"
                    : unit.IsDead ? "행동 및 선택 대상에서 제외" : "메모리 기준 우선순위", 12, Muted);
            }
            if (_controller.PendingResult != null && _controller.PresentationProgress >= 0.4f)
            {
                foreach (BattleStateChange change in _controller.PendingResult.Changes)
                {
                    int index = System.Array.IndexOf(ids, change.After.InstanceId);
                    if (index >= 0 && change.HpDelta < 0)
                    {
                        Text(new Rect(positions[index], 380, 344, 65), change.HpDelta.ToString(), 42, Pink, TextAnchor.MiddleCenter);
                    }
                }
            }
        }

        private void DrawCommands()
        {
            Fill(new Rect(0, 621, 1440, 279), Ink);
            Text(new Rect(36, 636, 790, 34), "행동 선택", 21, Color.white);
            string[] ids = { "strike", "heavy", "disrupt" };
            string[] labels = { "기본 공격\n고정 피해 24", "강타\n고정 피해 36", "메모리 교란\n피해 12 · 메모리 −8" };
            for (int i = 0; i < ids.Length; i++)
            {
                if (Button(new Rect(36 + i * 254, 681, 242, 67), labels[i], _controller.SelectedSkillId == ids[i], _controller.IsPlayerInput))
                {
                    _controller.SelectSkill(ids[i]);
                }
            }
            for (int i = 1; i < _controller.Snapshot.Combatants.Count; i++)
            {
                CombatantState target = _controller.Snapshot.Combatants[i];
                if (Button(new Rect(36 + (i - 1) * 194, 762, 182, 42), _controller.GetName(target.InstanceId),
                    _controller.SelectedTargetId == target.InstanceId, _controller.IsPlayerInput && !target.IsDead))
                {
                    _controller.SelectTarget(target.InstanceId);
                }
            }
            if (Button(new Rect(436, 762, 176, 42), "공격 실행  →", true, _controller.IsPlayerInput))
            {
                _controller.Attack();
            }
            if (Button(new Rect(626, 762, 160, 42), "대기", false, _controller.IsPlayerInput))
            {
                _controller.Wait();
            }
            bool canAdvance = _controller.Snapshot.Phase == BattlePhase.AwaitingPresentation
                || (_controller.Snapshot.Phase == BattlePhase.AwaitingAction && !_controller.IsPlayerInput);
            if (Button(new Rect(36, 818, 368, 39), "다음 단계로 진행  →", false, canAdvance))
            {
                _controller.Advance();
            }
            Text(new Rect(424, 827, 392, 27), "자동 진행 OFF로 한 단계씩 확인", 14, Muted);
            Text(new Rect(36, 871, 1340, 22), "검증용 모델·수치  /  메모리 교란은 순서 재계산 확인용 샘플  /  방어·치명타·최종 AI는 후속 단계", 13, Muted);
        }

        private void DrawHistory()
        {
            Fill(new Rect(826, 636, 578, 221), Panel);
            Text(new Rect(848, 650, 530, 28), "전투 기록", 18, Cyan);
            for (int i = 0; i < _controller.History.Count; i++)
            {
                Text(new Rect(848, 691 + i * 29, 536, 27), _controller.History[i], 14, i == 0 ? Color.white : Muted);
            }
        }

        private void DrawResult()
        {
            if (_controller.Snapshot.Phase != BattlePhase.Finished)
            {
                return;
            }
            BattleOutcome outcome = _controller.Snapshot.Outcome;
            Fill(new Rect(492, 260, 458, 151), new Color(0.025f, 0.065f, 0.09f, 0.97f));
            Fill(new Rect(492, 260, 458, 3), Cyan);
            Text(new Rect(512, 281, 418, 61), outcome == BattleOutcome.Victory ? "승리  /  VICTORY"
                : outcome == BattleOutcome.Defeat ? "패배  /  DEFEAT" : "무승부  /  DRAW", 30, Color.white, TextAnchor.MiddleCenter);
            Text(new Rect(512, 353, 418, 32), "상단의 ‘처음부터 다시’로 재시작", 16, Muted, TextAnchor.MiddleCenter);
        }

        private void Text(Rect rect, string text, int size, Color color, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            _label.fontSize = size;
            _label.normal.textColor = color;
            _label.alignment = alignment;
            GUI.Label(rect, text, _label);
        }

        private bool Button(Rect rect, string label, bool selected = false, bool enabled = true)
        {
            Color background = selected ? new Color(0.13f, 0.31f, 0.31f) : Panel;
            if (enabled && rect.Contains(Event.current.mousePosition))
            {
                background += new Color(0.045f, 0.06f, 0.065f, 0);
            }
            Fill(rect, enabled ? background : new Color(0.065f, 0.08f, 0.105f));
            if (selected)
            {
                Fill(new Rect(rect.x, rect.yMax - 2, rect.width, 2), enabled ? Cyan : Muted * 0.5f);
            }
            _button.fontSize = 16;
            _button.normal.textColor = enabled ? (selected ? Cyan : Color.white) : Muted * 0.6f;
            _button.hover.textColor = _button.normal.textColor;
            _button.active.textColor = _button.normal.textColor;
            bool previous = GUI.enabled;
            GUI.enabled = enabled;
            bool clicked = GUI.Button(rect, label, _button);
            GUI.enabled = previous;
            return clicked;
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
