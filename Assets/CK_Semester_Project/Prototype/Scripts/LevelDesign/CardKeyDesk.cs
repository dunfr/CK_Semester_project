using System.Collections;
using UnityEngine;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>콘솔 데스크의 카드 키(C01: 데스크 안 → 올라옴 → 보유). 올라온 뒤 E로 획득(I03).</summary>
    public sealed class CardKeyDesk : LevelInteractable
    {
        [SerializeField, Tooltip("데스크 안에 숨겨 둔 카드 키")]
        private Transform _cardKey;
        [SerializeField, Tooltip("카드 키가 올라왔을 때의 위치")]
        private Transform _raisedPoint;
        [SerializeField, Min(0.01f), Tooltip("카드 키 상승 시간(초). 기획서 1-3-A01 0.2초")]
        private float _raiseSeconds = 0.2f;

        public bool IsRaised { get; private set; }
        public override bool CanInteract => IsRaised && !Progress.HasCardKey;

        protected override void Awake()
        {
            base.Awake();
            if (_cardKey == null || _raisedPoint == null)
            {
                Debug.LogError(name + ": 카드 키 또는 상승 위치가 없습니다.", this);
                enabled = false;
            }
        }

        public IEnumerator Raise()
        {
            if (IsRaised)
            {
                yield break;
            }
            Vector3 start = _cardKey.position;
            _cardKey.gameObject.SetActive(true);
            for (float elapsed = 0f; elapsed < _raiseSeconds; elapsed += Time.deltaTime)
            {
                _cardKey.position = Vector3.Lerp(start, _raisedPoint.position, elapsed / _raiseSeconds);
                yield return null;
            }
            _cardKey.position = _raisedPoint.position;
            IsRaised = true;
        }

        public override void Interact()
        {
            _cardKey.gameObject.SetActive(false);
            Progress.GiveCardKey();
            Progress.SetObjective("카드 키로 문 열기");
        }
    }
}
