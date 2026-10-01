using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CK.SemesterProject.Tutorial
{
    [DefaultExecutionOrder(-50)]
    public sealed class TutorialFieldInteraction : MonoBehaviour
    {
        [SerializeField] private TutorialLevelFlow _flow;
        [SerializeField] private TutorialBattleController _battle;
        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private Camera _camera;
        [SerializeField] private TutorialFieldInteractable[] _targets;
        [SerializeField] private TMP_Text _hint;

        public TutorialFieldInteractable Current { get; private set; }
        public int ConsumedInputFrame { get; private set; } = -1;
        public bool IsSequenceLocked => _flow.IsBusy;
        public bool CanAct => !_flow.IsBusy && !_battle.IsInBattle && _movement.isActiveAndEnabled
            && (Current != null || _battle.CanInitiateNearbyBattle);

        private void Update()
        {
            Current = null;
            if (_battle.IsInBattle || _flow.IsBusy || !_movement.isActiveAndEnabled)
            {
                _hint.text = "";
                _hint.transform.parent.gameObject.SetActive(false);
                return;
            }
            float bestScore = float.NegativeInfinity;
            foreach (TutorialFieldInteractable target in _targets)
            {
                if (target == null || !target.isActiveAndEnabled || !target.IsAvailable)
                {
                    continue;
                }
                Vector3 delta = target.InteractionPosition - (_movement.transform.position + Vector3.up);
                if (delta.magnitude > target.Range)
                {
                    continue;
                }
                Vector3 view = target.InteractionPosition - _camera.transform.position;
                float alignment = Vector3.Dot(view.normalized, _camera.transform.forward);
                if (alignment < 0.25f)
                {
                    continue;
                }
                if (Physics.Linecast(_movement.transform.position + Vector3.up, target.InteractionPosition,
                    out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)
                    && !hit.transform.IsChildOf(target.transform) && !hit.transform.IsChildOf(_movement.transform))
                {
                    continue;
                }
                float score = alignment * 3f - delta.magnitude;
                if (score > bestScore)
                {
                    bestScore = score;
                    Current = target;
                }
            }
            _hint.text = Current != null ? "좌클릭 · " + Current.Prompt
                : _battle.CanInitiateNearbyBattle ? "좌클릭 · 선제 전투" : "";
            _hint.transform.parent.gameObject.SetActive(_hint.text.Length > 0);
            if (Current != null && Application.isFocused && Cursor.lockState == CursorLockMode.Locked
                && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
                && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                TryInteract();
            }
        }

        public bool TryInteract()
        {
            if (_battle.IsInBattle || _flow.IsBusy || Current == null)
            {
                return false;
            }
            ConsumedInputFrame = Time.frameCount;
            Current.Interact();
            return true;
        }
    }
}
