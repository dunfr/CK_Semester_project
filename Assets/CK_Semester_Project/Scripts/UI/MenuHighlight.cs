using UnityEngine;
using UnityEngine.EventSystems;

namespace CK.SemesterProject.UI
{
    [DisallowMultipleComponent]
    public sealed class MenuHighlight : MonoBehaviour
    {
        [SerializeField] private RectTransform _highlight;
        [SerializeField] private UnityEngine.UI.Button[] _buttons;

        private UnityEngine.UI.Button _selected;
        private bool _selectFirst;

        private void OnEnable()
        {
            _selected = _buttons[0];
            PositionHighlight(_selected);
            _selectFirst = true;
        }

        private void LateUpdate()
        {
            var events = EventSystem.current;
            if (events == null) return;

            var selectedObject = events.currentSelectedGameObject;
            if (_selectFirst || selectedObject == null || !selectedObject.activeInHierarchy)
            {
                _selectFirst = false;
                _selected.Select();
                return;
            }

            if (selectedObject == _selected.gameObject) return;

            foreach (var button in _buttons)
            {
                if (button.gameObject != selectedObject) continue;

                _selected = button;
                // Re-enable the existing player to start its Open clip at the new position.
                _highlight.gameObject.SetActive(false);
                PositionHighlight(button);
                _highlight.gameObject.SetActive(true);
                break;
            }
        }

        private void PositionHighlight(UnityEngine.UI.Button button)
        {
            Vector2 position = _highlight.anchoredPosition;
            position.y = ((RectTransform)button.transform).anchoredPosition.y;
            _highlight.anchoredPosition = position;
        }
    }
}
