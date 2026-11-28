using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    /// <summary>
    /// 전투에서 고른 몬스터의 몸통에 록온 조준 표시를 띄운다.
    /// 튜토리얼 조준 표시(TutorialEnemyReticle)와 같은 디자인을 쓰고, 몬스터 기준점에서 고정 높이에 붙여 애니메이션에 흔들리지 않게 한다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class BattleLockOnReticle : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TutorialBattleController _battle;
        [SerializeField, Tooltip("조준 표시 묶음. 부모는 화면 전체를 덮는 RectTransform이어야 한다.")]
        private RectTransform _reticle;

        [Header("Look")]
        [SerializeField, Tooltip("조준 표시 색")]
        private Color _color = new Color(1f, 0.32f, 0.12f, 1f);
        [SerializeField, Range(0f, 1f), Tooltip("글로우 강도")]
        private float _glow = 0.85f;
        [SerializeField, Min(1f), Tooltip("조준 표시 지름(px)")]
        private float _diameter = 104f;
        [SerializeField, Min(0f), Tooltip("몬스터 기준점(발밑)에서 조준할 몸통 높이(m). 좌우·앞뒤는 몸통 메시 중심을 따른다.")]
        private float _aimHeight = 1.05f;
        [SerializeField, Tooltip("화면 좌우 보정(m). 음수면 화면 왼쪽으로 옮긴다.")]
        private float _aimSideOffset = -0.1f;

        private TutorialEnemyReticle _graphic;
        private Transform _target;
        private Vector3 _bodyOffset;
        private float _lockedAt;
        private bool _isShown;

        private void Awake()
        {
            if (_reticle != null)
            {
                // 씬에 남은 구버전 스프라이트 조준 표시(Ring/Core)는 쓰지 않는다.
                foreach (Transform child in _reticle)
                {
                    child.gameObject.SetActive(false);
                }
                var marker = new GameObject("Lock On Graphic", typeof(RectTransform), typeof(TutorialEnemyReticle));
                marker.layer = _reticle.gameObject.layer;
                marker.transform.SetParent(_reticle, false);
                _graphic = marker.GetComponent<TutorialEnemyReticle>();
                // 표시가 몬스터 위를 덮어도 클릭이 몬스터 선택으로 넘어가도록 UI 레이캐스트를 막지 않는다.
                _graphic.raycastTarget = false;
            }
            Hide();
        }

        private void OnDisable()
        {
            Hide();
        }

        private void LateUpdate()
        {
            if (!TryGetTarget(out Transform target))
            {
                Hide();
                return;
            }
            if (target != _target || !_isShown)
            {
                _target = target;
                _bodyOffset = TutorialEnemyReticle.MeasureBodyOffset(target);
                _lockedAt = Time.unscaledTime;
            }
            Camera camera = Camera.main;
            if (camera == null)
            {
                Hide();
                return;
            }
            Vector3 screenPoint = camera.WorldToScreenPoint(TutorialEnemyReticle.GetAimPoint(target, _bodyOffset, _aimHeight)
                + camera.transform.right * _aimSideOffset);
            RectTransform parent = (RectTransform)_reticle.parent;
            if (screenPoint.z <= 0f
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, null, out Vector2 local))
            {
                Hide();
                return;
            }
            Show();
            _reticle.localPosition = new Vector3(local.x, local.y, 0f);
            _graphic.SetFrame(_diameter, _color, _glow, Time.unscaledTime - _lockedAt);
        }

        private bool TryGetTarget(out Transform target)
        {
            target = null;
            if (_battle == null || _reticle == null || _graphic == null || !_battle.IsInBattle
                || !_battle.HasSelectedBattleTarget || !_battle.CanChooseAction)
            {
                return false;
            }
            return _battle.TryGetMonsterTransform(_battle.TargetId, out target) && target != null
                && target.gameObject.activeInHierarchy;
        }

        private void Show()
        {
            if (!_isShown)
            {
                _isShown = true;
                _reticle.gameObject.SetActive(true);
            }
        }

        private void Hide()
        {
            if (_reticle != null && _reticle.gameObject.activeSelf)
            {
                _reticle.gameObject.SetActive(false);
            }
            _isShown = false;
        }
    }
}
