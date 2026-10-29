using CK.SemesterProject.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.Tutorial
{
    public sealed class BattleHistoryPanel : MonoBehaviour
    {
        private TutorialBattleController _controller;
        private Button _toggle;
        private TMP_Text _toggleLabel;
        private GameObject _panel;
        private TMP_Text _text;
        private ScrollRect _scroll;
        private int _count = -1;
        private BattleHistoryEntry _lastEntry;

        public static void Create(TutorialBattleController controller, TMP_Text fontSource, Button buttonSource)
        {
            var host = new GameObject("Battle History", typeof(RectTransform), typeof(BattleHistoryPanel));
            host.layer = 5;
            host.transform.SetParent(buttonSource.transform.parent, false);
            var ui = host.GetComponent<BattleHistoryPanel>();
            ui.Initialize(controller, fontSource, buttonSource);
        }

        private void Initialize(TutorialBattleController controller, TMP_Text fontSource, Button buttonSource)
        {
            _controller = controller;
            RectTransform root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.sizeDelta = Vector2.zero;
            _toggle = Instantiate(buttonSource, transform);
            _toggle.name = "Toggle Battle History";
            _toggle.onClick = new Button.ButtonClickedEvent();
            _toggle.GetComponent<RectTransform>().anchoredPosition = new Vector2(-145, -85);
            _toggleLabel = _toggle.GetComponentInChildren<TMP_Text>();
            _toggleLabel.text = "전투 기록 보기";
            _toggle.onClick.AddListener(Toggle);
            _panel = new GameObject("History Panel", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            _panel.layer = 5;
            _panel.transform.SetParent(transform, false);
            RectTransform panel = _panel.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = new Vector2(1, 1);
            panel.pivot = new Vector2(1, 1);
            panel.anchoredPosition = new Vector2(-15, -115);
            panel.sizeDelta = new Vector2(760, 640);
            _panel.GetComponent<Image>().color = new Color(.04f, .05f, .07f, .97f);
            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportObject.layer = 5;
            viewportObject.transform.SetParent(panel, false);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(12, 12);
            viewport.offsetMax = new Vector2(-12, -12);
            var content = new GameObject("History Content", typeof(RectTransform), typeof(TextMeshProUGUI));
            content.layer = 5;
            content.transform.SetParent(viewport, false);
            _text = content.GetComponent<TextMeshProUGUI>();
            _text.font = fontSource.font;
            _text.fontSize = 19;
            _text.color = Color.white;
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.richText = false;
            _text.raycastTarget = false;
            RectTransform rect = _text.rectTransform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(0, 600);
            _scroll = _panel.GetComponent<ScrollRect>();
            _scroll.viewport = viewport;
            _scroll.content = rect;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.scrollSensitivity = 35;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _panel.SetActive(false);
        }

        private void LateUpdate()
        {
            var history = _controller.History;
            BattleHistoryEntry last = history.Count == 0 ? null : history[history.Count - 1];
            if (_count == history.Count && ReferenceEquals(last, _lastEntry))
            {
                return;
            }
            bool follow = _count < 0 || _scroll.verticalNormalizedPosition <= .05f;
            _count = history.Count;
            _lastEntry = last;
            var text = new System.Text.StringBuilder("전투 기록 · 마우스 휠로 스크롤\n");
            text.AppendLine(_count == 0 ? "아직 기록된 행동이 없습니다." : "전투 시작");
            foreach (BattleHistoryEntry entry in history)
            {
                text.AppendLine().AppendLine(entry.ToString());
            }
            _text.text = text.ToString();
            _text.rectTransform.sizeDelta = new Vector2(0, Mathf.Max(600, _text.preferredHeight + 24));
            _toggleLabel.text = (_panel.activeSelf ? "기록 닫기" : "전투 기록") + " (" + _count + ")";
            if (follow)
            {
                Canvas.ForceUpdateCanvases();
                _scroll.verticalNormalizedPosition = 0;
            }
        }

        private void Toggle()
        {
            _panel.SetActive(!_panel.activeSelf);
            _toggleLabel.text = (_panel.activeSelf ? "기록 닫기" : "전투 기록") + " (" + Mathf.Max(0, _count) + ")";
        }

        private void OnDestroy()
        {
            if (_toggle != null)
            {
                _toggle.onClick.RemoveListener(Toggle);
            }
        }
    }
}
