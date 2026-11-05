using System.Collections;
using CK.SemesterProject.LevelDesign;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CK.SemesterProject.UI
{
    // 게임 클리어 화면. Show()를 부르면 화면이 검게 덮였다가 걷히면서 배경이 나오고,
    // 검은 화면이 걷히기 시작할 때 비네팅·QR·글자 묶음이 오른쪽에서 밀려 들어온다. Esc나 "창 닫기"로 닫힌다.
    // 떠 있는 동안 게임 시간을 멈추고 이동·상호작용을 잠근다. 씬에 켜 둔 채 두어도 시작할 때 스스로 숨는다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class GameClearPanel : MonoBehaviour
    {
        [Header("등장 연출")]
        [SerializeField, Tooltip("배경·비네팅·QR·글자 전체. 화면이 검게 다 덮인 뒤 보인다.")]
        private CanvasGroup _body;
        [SerializeField, Tooltip("화면을 덮는 검은 이미지")]
        private Image _blackFade;
        [SerializeField, Tooltip("오른쪽에서 들어오는 비네팅·QR·글자 묶음")]
        private RectTransform _slide;
        [SerializeField, Min(0f), Tooltip("게임 화면이 검게 덮이는 시간(초)")]
        private float _fadeToBlackSeconds = 0.5f;
        [SerializeField, Min(0f), Tooltip("검은 화면이 걷히는 시간(초)")]
        private float _fadeFromBlackSeconds = 0.5f;
        [SerializeField, Min(0f), Tooltip("비네팅·QR·글자가 들어오는 시간(초). 검은 화면이 걷히기 시작할 때 함께 시작한다. Main 메뉴와 같은 0.5초")]
        private float _slideSeconds = 0.5f;
        [SerializeField, Tooltip("비네팅·QR·글자가 출발하는 오른쪽 거리(px). Main의 Black과 같은 500")]
        private float _slideDistance = 500f;

        [Header("닫기")]
        [SerializeField, Min(0f), Tooltip("사라지는 시간(초)")]
        private float _fadeOutSeconds = 0.5f;
        [SerializeField, Tooltip("떠 있는 동안 게임 시간을 멈춥니다.")]
        private bool _pauseGame = true;
        [SerializeField, Tooltip("비워 두면 씬에서 찾습니다.")]
        private LevelProgress _progress;
        [SerializeField, Tooltip("완전히 닫힌 뒤 실행")]
        private UnityEvent _closed = new UnityEvent();

        private CanvasGroup _group;
        private Coroutine _routine;
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
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
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
            Run(Appear());
        }

        public void Close()
        {
            if (!_shown) return;
            _shown = false;
            _group.interactable = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            Run(Disappear());
        }

        private void Run(IEnumerator routine)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(routine);
        }

        private IEnumerator Appear()
        {
            _group.alpha = 1f;
            if (_body == null || _blackFade == null)
            {
                // 연출용 오브젝트가 없으면 전체를 서서히 보여 준다.
                _group.alpha = 0f;
                for (float elapsed = 0f; elapsed < _fadeFromBlackSeconds;)
                {
                    yield return null;
                    elapsed += Time.unscaledDeltaTime;
                    _group.alpha = Progress(elapsed, _fadeFromBlackSeconds);
                }
                _group.alpha = 1f;
                _routine = null;
                yield break;
            }

            // 1) 게임 화면 위로 검은 화면이 덮인다.
            _body.alpha = 0f;
            SetSlide(1f);
            SetBlack(0f);
            _blackFade.gameObject.SetActive(true);
            for (float elapsed = 0f; elapsed < _fadeToBlackSeconds;)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                SetBlack(Progress(elapsed, _fadeToBlackSeconds));
            }

            // 2) 검은 화면이 걷히면서 배경이 보이고, 비네팅·QR·글자가 오른쪽에서 함께 들어온다.
            SetBlack(1f);
            _body.alpha = 1f;
            float total = Mathf.Max(_fadeFromBlackSeconds, _slideSeconds);
            for (float elapsed = 0f; elapsed < total;)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                SetBlack(1f - Progress(elapsed, _fadeFromBlackSeconds));
                SetSlide(1f - EaseOut(Progress(elapsed, _slideSeconds)));
            }
            SetBlack(0f);
            SetSlide(0f);
            _blackFade.gameObject.SetActive(false);
            _routine = null;
        }

        private IEnumerator Disappear()
        {
            float from = _group.alpha;
            for (float elapsed = 0f; elapsed < _fadeOutSeconds;)
            {
                _group.alpha = Mathf.Lerp(from, 0f, elapsed / _fadeOutSeconds);
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            _group.alpha = 0f;
            _routine = null;
            IsOpen = false;
            ReleaseHold();
            gameObject.SetActive(false);
            _closed.Invoke();
        }

        private void SetBlack(float alpha)
        {
            Color color = _blackFade.color;
            color.a = alpha;
            _blackFade.color = color;
        }

        // 1이면 오른쪽 출발 위치, 0이면 제자리. Main 메뉴처럼 투명도는 바꾸지 않고 위치만 움직인다.
        private void SetSlide(float amount)
        {
            if (_slide != null) _slide.anchoredPosition = new Vector2(_slideDistance * amount, 0f);
        }

        private static float Progress(float elapsed, float seconds) => seconds <= 0f ? 1f : Mathf.Clamp01(elapsed / seconds);

        // Main 메뉴 Open 클립과 같은 OutCubic
        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

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
