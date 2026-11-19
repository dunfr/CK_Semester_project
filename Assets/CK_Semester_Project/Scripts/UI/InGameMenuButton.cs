using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.UI
{
    // HUD 버튼(오른쪽 위 톱니바퀴 등)으로 Esc 메뉴를 연다. 설정 화면까지 바로 열 수 있다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class InGameMenuButton : MonoBehaviour
    {
        [SerializeField, Tooltip("비워 두면 씬의 InGameMenu를 찾습니다.")]
        private InGameMenu _menu;
        [SerializeField, Tooltip("메뉴를 열면서 설정 화면까지 엽니다.")]
        private bool _openSettings = true;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_menu == null)
            {
                _menu = FindAnyObjectByType<InGameMenu>(FindObjectsInactive.Include);
            }
            if (_menu == null)
            {
                Debug.LogWarning($"{nameof(InGameMenuButton)}: 씬에 InGameMenu가 없어 버튼을 끕니다. ({name})", this);
                _button.interactable = false;
            }
        }

        private void OnEnable()
        {
            _button.onClick.AddListener(OpenMenu);
        }

        private void OnDisable()
        {
            _button.onClick.RemoveListener(OpenMenu);
        }

        private void OpenMenu()
        {
            if (_menu == null)
            {
                return;
            }
            if (!_menu.IsOpen)
            {
                _menu.Open();
            }
            if (_openSettings)
            {
                _menu.ShowSettingsPanel();
            }
        }
    }
}
