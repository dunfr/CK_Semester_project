using System.Collections;
using CK.SemesterProject.LevelDesign;
using CK.SemesterProject.LevelDesign.WindPuzzle;
using CK.SemesterProject.Tutorial;
using CK.SemesterProject.UIAnimation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CK.SemesterProject.UI
{
    // 인게임 ESC 메뉴. Main의 Black·MenuPanel을 같은 애니메이션으로 열고 게임 시간을 멈춘다.
    // Esc를 먼저 쓰는 화면(전투 스킬 선택의 뒤로 가기, 바람길 퍼즐, 튜토리얼 팝업, 컷씬, 클리어 화면)이 떠 있으면 열지 않는다.
    // 카메라(-100)와 전투 HUD(-40)보다 먼저 Esc를 읽어, 메뉴를 연 프레임에 HUD가 같은 키를 쓰지 않게 한다.
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class InGameMenu : MonoBehaviour
    {
        // 닫기 애니메이션이 끝나지 않아도 이 시간이 지나면 게임으로 돌아간다.
        private const float CloseTimeout = 3f;

        [Header("Main과 같은 화면")]
        [SerializeField] private UIAnimationPlayer _black;
        [SerializeField] private UIAnimationPlayer _menu;
        [SerializeField] private GameObject _teamPanel;
        [SerializeField] private GameObject _settingsPanel;

        [Header("메인으로")]
        [SerializeField, Tooltip("Build Profiles의 Scene List에 등록된 메인 씬 이름")]
        private string _mainSceneName = "Main";
        [SerializeField, Min(0f), Tooltip("게임 화면이 검게 덮이는 시간(초)")]
        private float _fadeToBlackSeconds = 0.8f;
        [SerializeField, Min(0f), Tooltip("메인 화면에서 검은 화면이 걷히는 시간(초)")]
        private float _fadeFromBlackSeconds = 0.8f;

        [Header("게임 연결 (비워 두면 씬에서 찾습니다)")]
        [SerializeField] private LevelProgress _progress;
        [SerializeField] private TutorialBattleController _battle;
        [SerializeField] private PrototypeHudPresenter _hud;
        [SerializeField] private BattleTutorialDirector _tutorial;

        private WindPuzzleTerminal[] _puzzles = new WindPuzzleTerminal[0];
        private CutscenePreset[] _cutscenes = new CutscenePreset[0];
        private TutorialSkillCutscene[] _skillCutscenes = new TutorialSkillCutscene[0];
        private CanvasGroup _group;
        private bool _isOpen;
        private bool _closing;
        private bool _paused;
        private bool _leaving;
        private bool _hudBlocked;
        private float _closeStartedAt;
        private float _savedTimeScale = 1f;
        private CursorLockMode _savedCursorLock;
        private bool _savedCursorVisible;

        public bool IsOpen => _isOpen;
        public bool IsPaused => _paused;

        private void Awake()
        {
            if (_progress == null) _progress = FindAnyObjectByType<LevelProgress>(FindObjectsInactive.Include);
            if (_battle == null) _battle = FindAnyObjectByType<TutorialBattleController>(FindObjectsInactive.Include);
            if (_hud == null) _hud = FindAnyObjectByType<PrototypeHudPresenter>(FindObjectsInactive.Include);
            if (_tutorial == null) _tutorial = FindAnyObjectByType<BattleTutorialDirector>(FindObjectsInactive.Include);
            _puzzles = FindObjectsByType<WindPuzzleTerminal>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _cutscenes = FindObjectsByType<CutscenePreset>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _skillCutscenes = FindObjectsByType<TutorialSkillCutscene>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _group = GetComponent<CanvasGroup>();
            _black.gameObject.SetActive(false);
            _menu.gameObject.SetActive(false);
            _teamPanel.SetActive(false);
            _settingsPanel.SetActive(false);
        }

        private void Update()
        {
            if (_leaving) return;
            if (_closing && HasClosed())
            {
                FinishClosing();
            }
            if (_paused)
            {
                // 메뉴가 떠 있는 동안 다른 기능이 시간을 되돌려도 멈춘 상태를 유지한다.
                Time.timeScale = 0f;
            }
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (_isOpen)
            {
                if (IsShown(_teamPanel) || IsShown(_settingsPanel)) ClosePanels();
                else Resume();
            }
            else if (_closing || CanOpen())
            {
                Open();
            }
        }

        private void OnDisable()
        {
            // 메뉴가 떠 있는 채로 꺼지면 게임이 멈춘 채 남지 않게 되돌린다.
            if (_leaving) return;
            _isOpen = false;
            _closing = false;
            ReleasePause();
        }

        public void Open()
        {
            if (_leaving) return;
            if (!_paused)
            {
                _paused = true;
                _savedTimeScale = Time.timeScale;
                _savedCursorLock = Cursor.lockState;
                _savedCursorVisible = Cursor.visible;
                if (_progress != null) _progress.LockControls(this);
                // 전투 HUD는 Esc·방향키·Enter를 직접 읽으므로 메뉴가 떠 있는 동안 끈다(튜토리얼 팝업과 같은 방식).
                if (_hud != null && _hud.enabled && _battle != null && _battle.IsInBattle)
                {
                    _hud.enabled = false;
                    _hudBlocked = true;
                }
            }
            _isOpen = true;
            _closing = false;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _black.Open();
            _menu.Open();
        }

        // 게임으로: Black과 MenuPanel이 닫힌 뒤 시간이 다시 흐른다.
        public void Resume()
        {
            if (!_isOpen || _leaving) return;
            _isOpen = false;
            _closing = true;
            _closeStartedAt = Time.unscaledTime;
            ClosePanels();
            _black.Close();
            _menu.Close();
        }

        public void ShowTeamPanel()
        {
            if (!_isOpen) return;
            ClosePanel(_settingsPanel);
            OpenPanel(_teamPanel);
            _teamPanel.transform.SetAsLastSibling();
        }

        public void ShowSettingsPanel()
        {
            if (!_isOpen) return;
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

        // 메인으로: 로고 없는 검은 화면으로 덮고 Main을 Press Any Key 상태부터 연다.
        public void GoToMain()
        {
            if (!_isOpen || _leaving) return;
            _leaving = true;
            if (_group != null) _group.interactable = false;
            if (!SceneFadeTransition.Run(_mainSceneName, _fadeToBlackSeconds, _fadeFromBlackSeconds, PrepareMainScene))
            {
                _leaving = false;
                if (_group != null) _group.interactable = true;
            }
        }

        private static void PrepareMainScene()
        {
            StartSceneFlow.StartAtPressAnyKeyOnNextLoad();
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private bool CanOpen()
        {
            if (GameClearPanel.IsOpen || SceneFadeTransition.IsRunning) return false;
            if (_tutorial != null && _tutorial.IsOpen) return false;
            foreach (WindPuzzleTerminal puzzle in _puzzles)
                if (puzzle != null && puzzle.IsActive) return false;
            foreach (CutscenePreset cutscene in _cutscenes)
                if (cutscene != null && cutscene.IsPlaying) return false;
            foreach (TutorialSkillCutscene cutscene in _skillCutscenes)
                if (cutscene != null && cutscene.IsPlaying) return false;
            // 전투에서 스킬을 고르는 중이면 Esc는 HUD의 뒤로 가기다.
            return !(_hud != null && _hud.isActiveAndEnabled && _battle != null && _battle.IsInBattle
                && _battle.CanChooseAction && _battle.HasSelectedBattleTarget && !_hud.IsCommandPhase);
        }

        private bool HasClosed()
        {
            return (!_black.gameObject.activeSelf && !_menu.gameObject.activeSelf)
                || Time.unscaledTime - _closeStartedAt > CloseTimeout;
        }

        private void FinishClosing()
        {
            _closing = false;
            _black.gameObject.SetActive(false);
            _menu.gameObject.SetActive(false);
            ReleasePause();
        }

        private void ReleasePause()
        {
            if (!_paused) return;
            _paused = false;
            bool tutorialOpen = _tutorial != null && _tutorial.IsOpen;
            // 튜토리얼 팝업처럼 시간을 멈춘 다른 화면이 아직 떠 있으면 멈춘 채 둔다.
            Time.timeScale = tutorialOpen ? 0f : _savedTimeScale > 0f ? _savedTimeScale : 1f;
            if (_progress != null) _progress.UnlockControls(this);
            if (_hudBlocked)
            {
                _hudBlocked = false;
                // 메뉴가 떠 있는 동안 팝업이 열렸다면 팝업은 꺼진 HUD 상태를 기억한다. 팝업이 닫힌 뒤에 켠다.
                if (tutorialOpen && isActiveAndEnabled) StartCoroutine(EnableHudAfterTutorial());
                else if (_hud != null) _hud.enabled = true;
            }
            Cursor.lockState = _savedCursorLock;
            Cursor.visible = _savedCursorVisible;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private IEnumerator EnableHudAfterTutorial()
        {
            while (_tutorial != null && _tutorial.IsOpen) yield return null;
            if (_hud != null) _hud.enabled = true;
        }

        private static bool IsShown(GameObject panel)
        {
            if (panel == null || !panel.activeSelf) return false;
            UIAnimationPlayer animation = panel.GetComponent<UIAnimationPlayer>();
            return animation == null || !animation.IsClosing;
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
    }
}
