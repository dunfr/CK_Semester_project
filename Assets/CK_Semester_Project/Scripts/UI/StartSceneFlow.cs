using System.Collections;
using CK.SemesterProject.UIAnimation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace CK.SemesterProject.UI
{
    [DisallowMultipleComponent]
    public sealed class StartSceneFlow : MonoBehaviour
    {
        [Header("Scene transition")]
        [Tooltip("통합 후 이동할 씬 이름을 입력하세요. 비어 있으면 로딩 화면에서 대기합니다.")]
        [SerializeField] private string _nextSceneName = "";
        [SerializeField, Min(2f)] private float _loadingSeconds = 2f;
        [Header("Intro — each logo fades in/out over one second")]
        [SerializeField, Min(.1f)] private float _logoSeconds = 1f;
        [SerializeField] private GameObject _introPanel;
        [SerializeField] private CanvasGroup _schoolLogo;
        [SerializeField] private CanvasGroup _teamLogo;
        [Header("Existing main UI")]
        [SerializeField] private GameObject _background;
        [SerializeField] private GameObject _banner;
        [SerializeField] private UIAnimationPlayer _black;
        [SerializeField] private UIAnimationPlayer _menu;
        [SerializeField] private GameObject _teamPanel;
        [SerializeField] private GameObject _settingsPanel;
        [Header("Loading")]
        [SerializeField] private GameObject _loadingPanel;
        [SerializeField] private UnityEngine.UI.RawImage _chibi;
        [SerializeField, Min(1f)] private float _framesPerSecond = 12f;
        private bool _mainReady, _loading;
        private float _animationTime;

        private void Awake()
        {
            _introPanel.SetActive(true);
            _schoolLogo.gameObject.SetActive(false);
            _teamLogo.gameObject.SetActive(false);
            _background.SetActive(false);
            _banner.SetActive(false);
            _black.gameObject.SetActive(false);
            _menu.gameObject.SetActive(false);
            _teamPanel.SetActive(false);
            _settingsPanel.SetActive(false);
            _loadingPanel.SetActive(false);
        }

        private IEnumerator Start()
        {
            yield return FadeLogo(_schoolLogo);
            yield return FadeLogo(_teamLogo);
            _background.SetActive(true);
            _introPanel.SetActive(false);
            _banner.SetActive(true);
            _mainReady = true;
        }

        private IEnumerator FadeLogo(CanvasGroup logo)
        {
            logo.gameObject.SetActive(true);
            float elapsed = 0f;
            while (elapsed < _logoSeconds)
            {
                float t = elapsed / _logoSeconds;
                logo.alpha = Mathf.Min(Mathf.Clamp01(t / .25f), Mathf.Clamp01((1f - t) / .25f));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            logo.alpha = 0f;
            logo.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_loading)
            {
                _animationTime += Time.unscaledDeltaTime;
                // 6 columns; 32 frames, top-left to bottom-right (last row has two frames).
                int frame = (int)(_animationTime * _framesPerSecond) % 32;
                _chibi.uvRect = new Rect(frame % 6 / 6f, (5 - frame / 6) / 6f, 1f / 6f, 1f / 6f);
            }
            else if (_mainReady && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ClosePanels();
        }

        public void ShowPanels()
        {
            if (!_mainReady || _loading) return;
            _teamPanel.SetActive(true);
            _settingsPanel.SetActive(true);
            _teamPanel.transform.SetAsLastSibling();
            _settingsPanel.transform.SetAsLastSibling();
        }

        public void ClosePanels()
        {
            _teamPanel.SetActive(false);
            _settingsPanel.SetActive(false);
        }

        public void StartGame()
        {
            if (!_mainReady || _loading) return;
            _loading = true;
            ClosePanels();
            _banner.SetActive(false);
            _menu.gameObject.SetActive(false);
            _black.gameObject.SetActive(false);
            _loadingPanel.transform.SetAsLastSibling();
            _loadingPanel.SetActive(true);
            _animationTime = 0f;
            StartCoroutine(LoadNextScene());
        }

        private IEnumerator LoadNextScene()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(2f, _loadingSeconds));
            if (string.IsNullOrWhiteSpace(_nextSceneName)) yield break;
            if (!Application.CanStreamedLevelBeLoaded(_nextSceneName))
            {
                Debug.LogError($"씬 '{_nextSceneName}'을 Build Profiles의 Scene List에 추가하세요.", this);
                yield break;
            }
            yield return SceneManager.LoadSceneAsync(_nextSceneName);
        }

        public void ExitGame()
        {
            if (!_mainReady || _loading) return;
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
