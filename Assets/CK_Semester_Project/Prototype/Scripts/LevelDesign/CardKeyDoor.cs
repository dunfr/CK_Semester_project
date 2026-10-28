using UnityEngine;
using UnityEngine.Events;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>카드 리더 상호작용(I01). 카드 키가 있으면 문을 열고, 없으면 안내 후 지정 지점을 잠시 비춘다.</summary>
    public sealed class CardKeyDoor : LevelInteractable
    {
        [SerializeField] private LevelDoor _door;
        [SerializeField, Tooltip("끄면 카드 키 없이 열리는 문(푸시 바 도어 등)")]
        private bool _requiresCardKey = true;
        [SerializeField, Tooltip("카드 키가 없을 때 안내 문구")]
        private string _lockedMessage = "카드 키가 필요합니다.";
        [SerializeField, Tooltip("카드 키가 없을 때 카메라 위치(선택)")]
        private Transform _lockedFocusView;
        [SerializeField, Tooltip("카드 키가 없을 때 카메라가 바라볼 지점(선택)")]
        private Transform _lockedFocusTarget;
        [SerializeField, Min(0f), Tooltip("카드 키가 없을 때 카메라 고정 시간(초)")]
        private float _lockedFocusSeconds = 0.5f;
        [SerializeField, Tooltip("문을 연 직후 실행(덕트 파손음 T01 등)")]
        private UnityEvent _opened;

        public override bool CanInteract => _door != null && !_door.IsOpen;
        public override string Prompt => _requiresCardKey && !Progress.HasCardKey ? "카드 리더 확인" : base.Prompt;

        protected override void Awake()
        {
            base.Awake();
            if (_door == null)
            {
                Debug.LogError(name + ": 연결된 문이 없습니다.", this);
                enabled = false;
            }
        }

        public override void Interact()
        {
            if (_requiresCardKey && !Progress.HasCardKey)
            {
                Progress.ShowMessage(_lockedMessage, 2.5f);
                Progress.FocusCamera(_lockedFocusView, _lockedFocusTarget, _lockedFocusSeconds);
                return;
            }
            _door.Open();
            _opened.Invoke();
        }
    }
}
