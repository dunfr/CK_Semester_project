using System.Collections;
using CK.SemesterProject.LevelDesign;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CK.SemesterProject.UI
{
    // 게임 클리어 화면. 클리어 지점의 UnityEvent 등에서 Show()를 부르면 열리고, Esc나 "창 닫기"로 닫힌다.
    // 떠 있는 동안 게임 시간을 멈추고 이동·상호작용을 잠근다. 씬에 켜 둔 채 두어도 시작할 때 스스로 숨는다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class GameClearPanel : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("나타나는 시간(초)")]
        private float _fadeInSeconds = 1f;
        [SerializeField, Min(0f), Tooltip("사라지는 시간(초)")]
        private float _fadeOutSeconds = 0.5f;
        [SerializeField, Tooltip("떠 있는 동안 게임 시간을 멈춥니다.")]
        private bool _pauseGame = true;
        [SerializeField, Tooltip("비워 두면 씬에서 찾습니다.")]
        private LevelProgress _progress;
        [SerializeField, Tooltip("완전히 닫힌 뒤 실행")]
        private UnityEvent _closed = new UnityEvent();

        private CanvasGroup _group;
        private Coroutine _fade;
        private bool _shown;
        // Show에서 멈춘 시간·커서·조작 잠금을 아직 돌려주지 않았다.
        private bool _holding;
        private float _savedTimeScale = 1f;
        private CursorLockMode _savedCursorLock;
        private bool _savedCursorVisible;

        // 인게임 메뉴는 이 화면이 떠 있는 동안 Esc로 열리지 않는다.
        public static bool IsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => IsOpen = false;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (_progress == null) _progress = FindAnyObjectByType<LevelProgress>(FindObjectsInactive.Include);
            if (!_shown) gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_holding && _pauseGame) Time.timeScale = 0f;
            if (_shown && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }

        private void OnDisable()
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = null;
            _shown = false;
            IsOpen = false;
            ReleaseHold();
        }

        public void Show()
        {
            if (_shown) return;
            _shown = true;
            IsOpen = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (!_holding)
            {
                _holding = true;
                _savedTimeScale = Time.timeScale;
                _savedCursorLock = Cursor.lockState;
                _savedCursorVisible = Cursor.visible;
                if (_progress != null) _progress.LockControls(this);
            }
            if (_pauseGame) Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _group.interactable = true;
            _group.blocksRaycasts = true;
            StartFade(1f, _fadeInSeconds);
        }

        public void Close()
        {
            if (!_shown) return;
            _shown = false;
            _group.interactable = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            StartFade(0f, _fadeOutSeconds);
        }

        private void StartFade(float to, float seconds)
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(Fade(to, seconds));
        }

        private IEnumerator Fade(float to, float seconds)
        {
            float from = _shown && _group.alpha >= 1f ? 0f : _group.alpha;
            for (float elapsed = 0f; elapsed < seconds;)
            {
                _group.alpha = Mathf.Lerp(from, to, elapsed / seconds);
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            _group.alpha = to;
            _fade = null;
            if (_shown) yield break;
            IsOpen = false;
            ReleaseHold();
            gameObject.SetActive(false);
            _closed.Invoke();
        }

        private void ReleaseHold()
        {
            if (!_holding) return;
            _holding = false;
            if (_pauseGame) Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : 1f;
            if (_progress != null) _progress.UnlockControls(this);
            Cursor.lockState = _savedCursorLock;
            Cursor.visible = _savedCursorVisible;
        }
    }
}
