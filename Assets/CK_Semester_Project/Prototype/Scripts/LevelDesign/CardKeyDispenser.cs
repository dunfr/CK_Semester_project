using System.Collections;
using UnityEngine;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>
    /// 카드 키 디스펜서(05 구역 5-1). F로 사용하면 카드가 튀어나왔다가(5-1-A01) 플레이어가 가져간다.
    /// 여기서 받은 카드는 이 디스펜서를 '필요한 카드'로 지정한 카드 리더 문(CardKeyDoor)만 연다.
    /// </summary>
    public sealed class CardKeyDispenser : LevelInteractable
    {
        [SerializeField, Tooltip("튀어나올 카드 모델(선택)")]
        private Transform _card;
        [SerializeField, Tooltip("카드가 튀어나오는 방향과 거리(m, 디스펜서 기준)")]
        private Vector3 _ejectOffset = new Vector3(0f, 0f, 0.25f);
        [SerializeField, Min(0.01f), Tooltip("카드 배출 시간(초). 1-3-A01과 같은 0.2초")]
        private float _ejectSeconds = 0.2f;
        [SerializeField, Tooltip("카드를 얻었을 때 안내 문구")]
        private string _acquiredMessage = "카드 키를 얻었습니다.";
        [SerializeField, Tooltip("카드를 얻은 뒤 목표 문구(비우면 바꾸지 않음)")]
        private string _nextObjective = "카드 키로 다음 문 열기";
        [SerializeField, Tooltip("카드를 얻은 뒤 미니맵이 길을 안내할 곳(다음 문 등). 비우면 안내선을 숨깁니다.")]
        private Transform _nextObjectiveTarget;

        private bool _isDispensing;

        /// <summary>플레이어가 이 디스펜서의 카드를 가지고 있는지.</summary>
        public bool HasCard { get; private set; }

        public override bool CanInteract => !HasCard && !_isDispensing;

        public override void Interact()
        {
            if (!CanInteract)
            {
                return;
            }
            StartCoroutine(Dispense());
        }

        private IEnumerator Dispense()
        {
            _isDispensing = true;
            if (_card != null)
            {
                _card.gameObject.SetActive(true);
                Vector3 start = _card.localPosition;
                Vector3 end = start + _ejectOffset;
                for (float elapsed = 0f; elapsed < _ejectSeconds; elapsed += Time.deltaTime)
                {
                    _card.localPosition = Vector3.Lerp(start, end, elapsed / _ejectSeconds);
                    yield return null;
                }
                _card.localPosition = end;
                yield return new WaitForSeconds(0.15f);
                _card.gameObject.SetActive(false);
            }
            HasCard = true;
            _isDispensing = false;
            Progress.ShowMessage(_acquiredMessage, 2.5f);
            if (!string.IsNullOrEmpty(_nextObjective))
            {
                Progress.SetObjective(_nextObjective, _nextObjectiveTarget);
            }
        }
    }
}
