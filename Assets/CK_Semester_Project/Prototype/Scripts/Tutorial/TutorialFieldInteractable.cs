using System.Collections;
using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    public enum TutorialFieldAction { CardDoor, ElevatorDoor, ArrivalDoor, ElevatorPanel, HallDoor, ExitDoor }

    public sealed class TutorialFieldInteractable : MonoBehaviour
    {
        [SerializeField] private TutorialLevelFlow _flow;
        [SerializeField] private TutorialFieldAction _kind;
        [SerializeField] private Transform _movingPart;
        [SerializeField] private Transform _interactionPoint;
        [SerializeField] private string _prompt = "문 열기";
        [SerializeField, Min(0.1f), Tooltip("상호작용 거리(m)")]
        private float _range = 2.6f;
        [SerializeField, Min(0.1f), Tooltip("문 개폐 시간(초)")]
        private float _openSeconds = 1f;
        [SerializeField, Tooltip("문을 열 때 월드 이동량(m)")]
        private Vector3 _openOffset = new Vector3(0, 4.2f, 0);

        private Vector3 _closedPosition;
        private Coroutine _animation;
        public bool IsOpen { get; private set; }
        public TutorialFieldAction Kind => _kind;
        public Vector3 InteractionPosition => _interactionPoint.position;
        public float Range => _range;
        public bool IsAvailable => !_flow.IsBusy && !IsOpen
            && (_kind != TutorialFieldAction.ElevatorPanel || _flow.Stage == TutorialLevelStage.Elevator);
        public string Prompt => !_flow.HasCardKey ? "카드키 필요"
            : _kind == TutorialFieldAction.HallDoor && !_flow.HasCollapsed ? "덕트 경로를 먼저 통과하세요"
            : _kind == TutorialFieldAction.ExitDoor && _flow.Stage < TutorialLevelStage.UpperExit ? "홀 상층으로 올라가세요"
            : _prompt;

        private void Awake()
        {
            _closedPosition = _movingPart.position;
        }

        public void Interact()
        {
            if (!IsAvailable)
            {
                return;
            }
            if (!_flow.HasCardKey || (_kind == TutorialFieldAction.HallDoor && !_flow.HasCollapsed)
                || (_kind == TutorialFieldAction.ExitDoor && _flow.Stage < TutorialLevelStage.UpperExit))
            {
                _flow.ShowFeedback(Prompt);
                return;
            }
            if (_kind == TutorialFieldAction.ElevatorPanel)
            {
                _flow.BeginElevator();
                return;
            }
            Open();
            _flow.OpenedDoor(this);
        }

        public void Open()
        {
            Animate(true);
        }

        public void Close()
        {
            Animate(false);
        }

        private void Animate(bool isOpen)
        {
            if (_animation != null)
            {
                StopCoroutine(_animation);
            }
            IsOpen = isOpen;
            _animation = StartCoroutine(MoveDoor(_closedPosition + (isOpen ? _openOffset : Vector3.zero)));
        }

        private IEnumerator MoveDoor(Vector3 destination)
        {
            Vector3 start = _movingPart.position;
            for (float elapsed = 0; elapsed < _openSeconds; elapsed += Time.deltaTime)
            {
                _movingPart.position = Vector3.Lerp(start, destination, Mathf.SmoothStep(0, 1, elapsed / _openSeconds));
                yield return null;
            }
            _movingPart.position = destination;
            _animation = null;
        }
    }
}
