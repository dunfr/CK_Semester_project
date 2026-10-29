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
    /// 레벨 기믹이 함께 쓰는 진행 상태(카드 키 C01), 조작 잠금, E 상호작용 입력, 안내 문구, 카메라 고정을 관리한다.
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

        private readonly List<LevelInteractable> _candidates = new List<LevelInteractable>();
        private readonly HashSet<object> _lockOwners = new HashSet<object>();
        private float _feedbackUntil;
        private Coroutine _focusRoutine;
        private bool _isReady;

        public bool HasCardKey { get; private set; }
        public bool IsControlLocked => _lockOwners.Count > 0;
        public bool IsInBattle => _battle != null && _battle.IsInBattle;
        public Transform Player => _movement != null ? _movement.transform : null;
        public TutorialBattleController Battle => _battle;

        private void Awake()
        {
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

        public void SetObjective(string objective)
        {
            if (_objectivePanel == null || _objectiveText == null)
            {
                return;
            }
            _objectiveText.text = objective;
            _objectivePanel.SetActive(!string.IsNullOrEmpty(objective));
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
            _hintText.text = "[E] " + current.Prompt;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
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
