using System.Collections;
using System.Collections.Generic;
using CK.SemesterProject.Battle;
using CK.SemesterProject.Tutorial;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>
    /// 레벨 기믹이 함께 쓰는 진행 상태(카드 키 C01), 조작 잠금, F 상호작용 입력, 안내 문구, 카메라 고정을 관리한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelProgress : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("조작 잠금 대상인 플레이어 이동 컴포넌트")]
        private PlayerMovement _movement;
        [SerializeField, Tooltip("전투 중에는 상호작용과 잠금 적용을 보류합니다.")]
        private TutorialBattleController _battle;
        [SerializeField, Tooltip("데스크 등 지정 지점을 비출 때 잠시 켜는 카메라. 평소에는 꺼 둡니다.")]
        private CinemachineCamera _focusCamera;
        [SerializeField, Tooltip("상황 안내 문구 패널")]
        private GameObject _feedbackPanel;
        [SerializeField] private TMP_Text _feedbackText;
        [SerializeField, Tooltip("상호작용 키 안내 패널")]
        private GameObject _hintPanel;
        [SerializeField] private TMP_Text _hintText;
        [SerializeField, Tooltip("현재 목표 표시 패널")]
        private GameObject _objectivePanel;
        [SerializeField] private TMP_Text _objectiveText;

        [Header("Field HUD")]
        [SerializeField, Tooltip("한글 위치 문구. 비워도 동작합니다.")]
        private TMP_Text _areaNameText;
        [SerializeField, Tooltip("영문 위치 문구. 비워도 동작합니다.")]
        private TMP_Text _areaNameEnglishText;
        [SerializeField, Tooltip("배너의 한글 위치 문구. 비워도 동작합니다.")]
        private TMP_Text _bannerAreaNameText;
        [SerializeField, Tooltip("배너의 영문 위치 문구. 비워도 동작합니다.")]
        private TMP_Text _bannerAreaNameEnglishText;
        [SerializeField, Tooltip("스토리 번호. 비워도 동작합니다.")]
        private TMP_Text _storyNumberText;
        [SerializeField, Tooltip("한글 스토리 제목. 비워도 동작합니다.")]
        private TMP_Text _storyTitleText;
        [SerializeField, Tooltip("영문 스토리 제목. 비워도 동작합니다.")]
        private TMP_Text _storyTitleEnglishText;
        [SerializeField, Tooltip("스토리 패널의 현재 목표. 비워도 동작합니다.")]
        private TMP_Text _storyObjectiveText;
        [SerializeField, Tooltip("시작 시 한글 위치. 비우면 유지합니다.")]
        private string _startAreaName;
        [SerializeField, Tooltip("시작 시 영문 위치. 입력한 대소문자를 유지합니다.")]
        private string _startAreaNameEnglish;
        [SerializeField, Tooltip("시작 시 스토리 번호. 비우면 유지합니다.")]
        private string _startStoryNumber = "#01";
        [SerializeField, Tooltip("시작 시 한글 스토리 제목. 비우면 유지합니다.")]
        private string _startStoryTitle;
        [SerializeField, Tooltip("시작 시 영문 스토리 제목. 입력한 대소문자를 유지합니다.")]
        private string _startStoryTitleEnglish;

        private const string InteractKeyLabel = "F";

        private readonly List<LevelInteractable> _candidates = new List<LevelInteractable>();
        private readonly HashSet<object> _lockOwners = new HashSet<object>();
        private float _feedbackUntil;
        private Coroutine _focusRoutine;
        private bool _isReady;
        private string _storyNumber = string.Empty;
        private string _storyTitleEnglish = string.Empty;

        public string AreaName { get; private set; } = string.Empty;
        public string AreaNameEnglish { get; private set; } = string.Empty;
        public string StoryTitle { get; private set; } = string.Empty;
        public string Objective { get; private set; } = string.Empty;
        /// <summary>현재 목표 위치. 미니맵이 이곳까지 노란 안내선을 그린다. 없으면 안내선을 숨긴다.</summary>
        public Transform ObjectiveTarget { get; private set; }
        public bool HasCardKey { get; private set; }
        public bool IsControlLocked => _lockOwners.Count > 0;
        public bool IsInBattle => _battle != null && _battle.IsInBattle;
        public Transform Player => _movement != null ? _movement.transform : null;
        public TutorialBattleController Battle => _battle;

        /// <summary>목표 문구나 목표 위치가 바뀌었을 때.</summary>
        public event System.Action ObjectiveChanged;

        private void Awake()
        {
            SetArea(_startAreaName, _startAreaNameEnglish);
            SetStory(_startStoryNumber, _startStoryTitle, _startStoryTitleEnglish);
            if (_movement == null || _battle == null || _focusCamera == null || _feedbackPanel == null || _feedbackText == null
                || _hintPanel == null || _hintText == null)
            {
                Debug.LogError("LevelProgress: 플레이어·전투·카메라·HUD 참조가 누락되었습니다.", this);
                enabled = false;
                return;
            }
            _focusCamera.gameObject.SetActive(false);
            _feedbackPanel.SetActive(false);
            _hintPanel.SetActive(false);
            _isReady = true;
        }

        private void OnEnable()
        {
            if (_battle != null)
            {
                _battle.FieldBattleEnded += OnBattleEnded;
            }
        }

        private void Update()
        {
            if (_feedbackPanel.activeSelf && Time.unscaledTime >= _feedbackUntil)
            {
                _feedbackPanel.SetActive(false);
            }
            UpdateInteraction();
        }

        private void OnDisable()
        {
            if (_battle != null)
            {
                _battle.FieldBattleEnded -= OnBattleEnded;
            }
        }

        public void GiveCardKey()
        {
            HasCardKey = true;
        }

        public void LockControls(object owner)
        {
            if (_lockOwners.Add(owner))
            {
                ApplyControlLock();
            }
        }

        public void UnlockControls(object owner)
        {
            if (_lockOwners.Remove(owner))
            {
                ApplyControlLock();
            }
        }

        public void ShowMessage(string message, float seconds = 3f)
        {
            if (!_isReady || string.IsNullOrEmpty(message))
            {
                return;
            }
            _feedbackText.text = message;
            _feedbackPanel.SetActive(true);
            _feedbackUntil = Time.unscaledTime + Mathf.Max(0.1f, seconds);
        }

        public void SetArea(string name, string englishName)
        {
            if (!string.IsNullOrEmpty(name))
            {
                AreaName = name;
            }
            if (!string.IsNullOrEmpty(englishName))
            {
                AreaNameEnglish = englishName;
            }
            if (_areaNameText != null)
            {
                _areaNameText.text = AreaName;
            }
            if (_areaNameEnglishText != null)
            {
                _areaNameEnglishText.text = AreaNameEnglish;
            }
            if (_bannerAreaNameText != null)
            {
                _bannerAreaNameText.text = AreaName;
            }
            if (_bannerAreaNameEnglishText != null)
            {
                _bannerAreaNameEnglishText.text = AreaNameEnglish;
            }
        }

        public void SetStory(string number, string title, string englishTitle)
        {
            if (!string.IsNullOrEmpty(number))
            {
                _storyNumber = number;
            }
            if (!string.IsNullOrEmpty(title))
            {
                StoryTitle = title;
            }
            if (!string.IsNullOrEmpty(englishTitle))
            {
                _storyTitleEnglish = englishTitle;
            }
            if (_storyNumberText != null)
            {
                _storyNumberText.text = _storyNumber;
            }
            if (_storyTitleText != null)
            {
                _storyTitleText.text = StoryTitle;
            }
            if (_storyTitleEnglishText != null)
            {
                _storyTitleEnglishText.text = _storyTitleEnglish;
            }
        }

        /// <summary>목표 문구만 바꾼다. 미니맵 안내선은 숨긴다.</summary>
        public void SetObjective(string objective)
        {
            SetObjective(objective, null);
        }

        /// <summary>목표 문구와 함께 미니맵에 길을 안내할 위치를 정한다. target이 없으면 안내선을 숨긴다.</summary>
        public void SetObjective(string objective, Transform target)
        {
            Objective = objective ?? string.Empty;
            ObjectiveTarget = target;
            if (_storyObjectiveText != null)
            {
                _storyObjectiveText.text = Objective;
            }
            if (_objectivePanel != null && _objectiveText != null)
            {
                _objectiveText.text = Objective;
                _objectivePanel.SetActive(!string.IsNullOrEmpty(Objective));
            }
            ObjectiveChanged?.Invoke();
        }

        /// <summary>viewPoint 위치에서 lookTarget을 seconds 동안 비춘 뒤 플레이어 카메라로 돌아간다.</summary>
        public void FocusCamera(Transform viewPoint, Transform lookTarget, float seconds)
        {
            if (!_isReady || viewPoint == null || lookTarget == null)
            {
                return;
            }
            if (_focusRoutine != null)
            {
                StopCoroutine(_focusRoutine);
            }
            _focusRoutine = StartCoroutine(FocusRoutine(viewPoint, lookTarget, seconds));
        }

        public void RegisterInteractable(LevelInteractable interactable)
        {
            if (interactable != null && !_candidates.Contains(interactable))
            {
                _candidates.Add(interactable);
            }
        }

        public void UnregisterInteractable(LevelInteractable interactable)
        {
            _candidates.Remove(interactable);
        }

        private IEnumerator FocusRoutine(Transform viewPoint, Transform lookTarget, float seconds)
        {
            Vector3 direction = lookTarget.position - viewPoint.position;
            _focusCamera.transform.SetPositionAndRotation(viewPoint.position,
                direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : viewPoint.rotation);
            _focusCamera.gameObject.SetActive(true);
            yield return new WaitForSeconds(Mathf.Max(0f, seconds));
            _focusCamera.gameObject.SetActive(false);
            _focusRoutine = null;
        }

        private void OnBattleEnded(BattleOutcome outcome)
        {
            // 전투 종료 시 TutorialBattleController가 이동을 복원하므로 진행 중인 연출 잠금을 다시 적용한다.
            ApplyControlLock();
        }

        private void ApplyControlLock()
        {
            if (IsInBattle)
            {
                return;
            }
            _movement.enabled = !IsControlLocked;
            if (_battle != null)
            {
                _battle.SetEnemiesCutscenePaused(IsControlLocked);
            }
        }

        private void UpdateInteraction()
        {
            LevelInteractable current = FindCurrentInteractable();
            bool canInteract = current != null && !IsControlLocked && !IsInBattle;
            _hintPanel.SetActive(canInteract);
            if (!canInteract)
            {
                return;
            }
            _hintText.text = "[" + InteractKeyLabel + "] " + current.Prompt;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                current.Interact();
            }
        }

        private LevelInteractable FindCurrentInteractable()
        {
            LevelInteractable nearest = null;
            float nearestDistance = float.MaxValue;
            Vector3 position = Player.position;
            for (int i = _candidates.Count - 1; i >= 0; i--)
            {
                LevelInteractable candidate = _candidates[i];
                if (candidate == null)
                {
                    _candidates.RemoveAt(i);
                    continue;
                }
                if (!candidate.isActiveAndEnabled || !candidate.CanInteract)
                {
                    continue;
                }
                float distance = (candidate.transform.position - position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }
    }
}
