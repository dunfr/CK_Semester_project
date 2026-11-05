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
        [Tooltip("로딩 화면이 완전히 나타난 뒤 유지하는 시간. 최소 2초입니다.")]
        [SerializeField, Min(2f)] private float _loadingSeconds = 2f;
        [Header("School logo timing (seconds)")]
        [SerializeField, Min(0f)] private float _schoolFadeInSeconds = 1f;
        [SerializeField, Min(0f)] private float _schoolHoldSeconds = 1.5f;
        [SerializeField, Min(0f)] private float _schoolFadeOutSeconds = 1f;
        [Header("Team logo timing (seconds)")]
        [SerializeField, Min(0f)] private float _teamFadeInSeconds = 1f;
        [SerializeField, Min(0f)] private float _teamHoldSeconds = 1.5f;
        [SerializeField, Min(0f)] private float _teamFadeOutSeconds = 1f;
        [Header("Reveal main screen")]
        [Tooltip("마지막 로고가 사라진 뒤 회색 배경만 유지하는 시간")]
        [SerializeField, Min(0f)] private float _introBackgroundHoldSeconds = 1f;
        [SerializeField, Min(0f)] private float _introBackgroundFadeOutSeconds = 1f;
        [Header("Return from game")]
        [Tooltip("인게임에서 메인으로 돌아온 뒤 Press Any Key 배너가 나타나 깜빡이기 시작할 때까지의 시간. 최소 1.5초입니다.")]
        [SerializeField, Min(1.5f)] private float _returnBannerDelaySeconds = 1.5f;
        [Header("Intro references")]
        [SerializeField] private GameObject _introPanel;
        [SerializeField] private CanvasGroup _introGroup;
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
        [SerializeField] private CanvasGroup _loadingGroup;
        [SerializeField, Min(0f)] private float _loadingFadeInSeconds = .5f;
        [SerializeField, Min(0f)] private float _loadingFadeOutSeconds = .5f;
        [SerializeField] private UnityEngine.UI.RawImage _chibi;
        [SerializeField, Min(1f)] private float _framesPerSecond = 12f;
        private bool _mainReady, _loading, _returnedFromGame;
        private float _animationTime;
        private static bool _startAtPressAnyKey;

        // 인게임 메뉴의 "메인으로"가 부른다. 다음에 이 씬을 열 때 로고 연출 없이 Press Any Key 화면부터 시작한다.
        public static void StartAtPressAnyKeyOnNextLoad() => _startAtPressAnyKey = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStartRequest() => _startAtPressAnyKey = false;

        private void Awake()
        {
            _introGroup.alpha = 1f;
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
            _loadingGroup.alpha = 0f;
            if (_startAtPressAnyKey)
            {
                // 로고 없이 배경부터 보여 주고, 배너는 Start에서 잠시 뒤에 켠다.
                _startAtPressAnyKey = false;
                _returnedFromGame = true;
                _introPanel.SetActive(false);
                _background.SetActive(true);
            }
        }

        private IEnumerator Start()
        {
            if (_returnedFromGame)
            {
                // "메인으로"로 돌아온 경우: 화면이 자리 잡은 뒤 배너가 나타나 깜빡이고, 그때부터 아무 키 입력을 받는다.
                yield return new WaitForSecondsRealtime(Mathf.Max(1.5f, _returnBannerDelaySeconds));
                _banner.SetActive(true);
                _mainReady = true;
                yield break;
            }
            // Let the first rendered frame settle before starting the timed fades.
            yield return null;
            yield return FadeLogo(_schoolLogo, _schoolFadeInSeconds, _schoolHoldSeconds, _schoolFadeOutSeconds);
            yield return FadeLogo(_teamLogo, _teamFadeInSeconds, _teamHoldSeconds, _teamFadeOutSeconds);
            if (_introBackgroundHoldSeconds > 0f)
                yield return new WaitForSecondsRealtime(_introBackgroundHoldSeconds);
            _background.SetActive(true);
            yield return Fade(_introGroup, 1f, 0f, _introBackgroundFadeOutSeconds);
            _introPanel.SetActive(false);
            _banner.SetActive(true);
            _mainReady = true;
        }

        private IEnumerator FadeLogo(CanvasGroup logo, float fadeIn, float hold, float fadeOut)
        {
            logo.alpha = 0f;
            logo.gameObject.SetActive(true);
            yield return Fade(logo, 0f, 1f, fadeIn);
            if (hold > 0f) yield return new WaitForSecondsRealtime(hold);
            yield return Fade(logo, 1f, 0f, fadeOut);
            logo.gameObject.SetActive(false);
        }

        private static IEnumerator Fade(CanvasGroup group, float from, float to, float seconds)
        {
            group.alpha = from;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
            }
            group.alpha = to;
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
            OpenPanel(_teamPanel);
            OpenPanel(_settingsPanel);
            _teamPanel.transform.SetAsLastSibling();
            _settingsPanel.transform.SetAsLastSibling();
        }

        public void ShowTeamPanel()
        {
            if (!_mainReady || _loading) return;
            ClosePanel(_settingsPanel);
            OpenPanel(_teamPanel);
            _teamPanel.transform.SetAsLastSibling();
        }

        public void ShowSettingsPanel()
        {
            if (!_mainReady || _loading) return;
            ClosePanel(_teamPanel);
            OpenPanel(_settingsPanel);
            _settingsPanel.transform.SetAsLastSibling();
        }

        public void CloseTeamPanel() => ClosePanel(_teamPanel);
        public void CloseSettingsPanel() => ClosePanel(_settingsPanel);

        public void ClosePanels()
        {
            ClosePanel(_teamPanel);
            ClosePanel(_settingsPanel);
        }

        private static void OpenPanel(GameObject panel)
        {
            UIAnimationPlayer animation = panel.GetComponent<UIAnimationPlayer>();
            if (animation != null && animation.enabled && animation.Preset != null)
                animation.Open();
            else
                panel.SetActive(true);
        }

        private static void ClosePanel(GameObject panel)
        {
            UIAnimationPlayer animation = panel.GetComponent<UIAnimationPlayer>();
            if (animation != null && animation.enabled && animation.Preset != null)
                animation.Close();
            else
                panel.SetActive(false);
        }

        public void StartGame()
        {
            if (!_mainReady || _loading) return;
            _loading = true;
            ClosePanels();
            _banner.SetActive(false);
            _loadingPanel.transform.SetAsLastSibling();
            _loadingGroup.alpha = 0f;
            _loadingPanel.SetActive(true);
            _animationTime = 0f;
            StartCoroutine(LoadNextScene());
        }

        private IEnumerator LoadNextScene()
        {
            yield return Fade(_loadingGroup, 0f, 1f, _loadingFadeInSeconds);
            _menu.gameObject.SetActive(false);
            _black.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(Mathf.Max(2f, _loadingSeconds));
            if (string.IsNullOrWhiteSpace(_nextSceneName)) yield break;
            if (!Application.CanStreamedLevelBeLoaded(_nextSceneName))
            {
                Debug.LogError($"씬 '{_nextSceneName}'을 Build Profiles의 Scene List에 추가하세요.", this);
                yield break;
            }
            AsyncOperation load = SceneManager.LoadSceneAsync(_nextSceneName);
            load.allowSceneActivation = false;
            while (load.progress < .9f) yield return null;
            // Fade onto the same dark backdrop, then reveal the destination scene.
            _introGroup.alpha = 1f;
            _introPanel.SetActive(true);
            _introPanel.transform.SetSiblingIndex(_loadingPanel.transform.GetSiblingIndex());
            _loadingPanel.transform.SetAsLastSibling();
            yield return Fade(_loadingGroup, 1f, 0f, _loadingFadeOutSeconds);
            load.allowSceneActivation = true;
            yield return load;
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
