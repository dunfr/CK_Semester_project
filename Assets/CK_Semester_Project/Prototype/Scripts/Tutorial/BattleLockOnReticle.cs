using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.Tutorial
{
    /// <summary>
    /// 전투에서 고른 몬스터의 몸 중앙에 록온 조준 표시를 띄운다.
    /// 대상이 바뀌거나 다시 나타나면 크게 돌며 조여 들어와 자리를 잡고, 이후 가운데 점이 맥박처럼 뛴다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class BattleLockOnReticle : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TutorialBattleController _battle;
        [SerializeField, Tooltip("조준 표시 묶음. 부모는 화면 전체를 덮는 RectTransform이어야 한다.")]
        private RectTransform _reticle;
        [SerializeField, Tooltip("바깥 고리(삼각 눈금 포함)")]
        private Image _ring;
        [SerializeField, Tooltip("가운데 작은 고리와 점")]
        private Image _core;

        [Header("Look")]
        [SerializeField, Tooltip("조준 표시 색")]
        private Color _color = new Color(1f, 0.28f, 0.52f, 1f);
        [SerializeField, Range(0.1f, 1f), Tooltip("몬스터 화면 높이 대비 고리 지름 비율")]
        private float _sizeByHeight = 0.42f;
        [SerializeField, Min(10f), Tooltip("고리 최소 지름(px)")]
        private float _minDiameter = 90f;
        [SerializeField, Min(10f), Tooltip("고리 최대 지름(px)")]
        private float _maxDiameter = 210f;
        [SerializeField, Range(0f, 1f), Tooltip("몸통 높이 중 조준할 지점(0 = 발, 1 = 머리)")]
        private float _aimHeight = 0.55f;

        [Header("Motion")]
        [SerializeField, Min(0.01f), Tooltip("조여 들어와 자리 잡는 시간(초)")]
        private float _lockSeconds = 0.3f;
        [SerializeField, Min(1f), Tooltip("나타날 때 시작 크기 배율")]
        private float _startScale = 2.2f;
        [SerializeField, Tooltip("나타날 때 시작 회전 각도(도)")]
        private float _startAngle = -90f;

        private const float CorePulseSpeed = 6f;
        private const float BreatheSpeed = 3f;

        private Transform _target;
        private Renderer[] _renderers = new Renderer[0];
        private float _lockedAt;
        private bool _isShown;

        private void Awake()
        {
            if (_ring != null)
            {
                _ring.raycastTarget = false;
            }
            if (_core != null)
            {
                _core.raycastTarget = false;
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
            if (target != _target)
            {
                BindTarget(target);
                _lockedAt = Time.unscaledTime;
            }
            else if (!_isShown)
            {
                _lockedAt = Time.unscaledTime;
            }
            Camera camera = Camera.main;
            if (camera == null || !TryGetAimRect(camera, out Vector2 screenPoint, out float screenHeight))
            {
                Hide();
                return;
            }
            RectTransform parent = (RectTransform)_reticle.parent;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, null, out Vector2 local))
            {
                Hide();
                return;
            }
            Show();
            float canvasScale = parent.lossyScale.x > 0f ? parent.lossyScale.x : 1f;
            float diameter = Mathf.Clamp(screenHeight / canvasScale * _sizeByHeight, _minDiameter, _maxDiameter);
            _reticle.localPosition = new Vector3(local.x, local.y, 0f);
            _reticle.sizeDelta = new Vector2(diameter, diameter);
            Animate(Time.unscaledTime - _lockedAt);
        }

        private bool TryGetTarget(out Transform target)
        {
            target = null;
            if (_battle == null || _reticle == null || !_battle.IsInBattle || !_battle.HasSelectedBattleTarget
                || !_battle.CanChooseAction)
            {
                return false;
            }
            return _battle.TryGetMonsterTransform(_battle.TargetId, out target) && target != null
                && target.gameObject.activeInHierarchy;
        }

        private void BindTarget(Transform target)
        {
            _target = target;
            _renderers = target.GetComponentsInChildren<Renderer>()
                .Where(item => !(item is ParticleSystemRenderer) && !(item is LineRenderer)
                    && !(item is TrailRenderer) && item.GetComponent<TextMesh>() == null)
                .ToArray();
        }

        // 렌더러 묶음의 높이에서 몸통 지점을 화면으로 옮기고, 화면에서의 키도 함께 구한다.
        private bool TryGetAimRect(Camera camera, out Vector2 screenPoint, out float screenHeight)
        {
            screenPoint = Vector2.zero;
            screenHeight = 0f;
            Bounds bounds = new Bounds(_target.position, Vector3.zero);
            bool hasBounds = false;
            foreach (Renderer item in _renderers)
            {
                if (item == null || !item.enabled || !item.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (hasBounds)
                {
                    bounds.Encapsulate(item.bounds);
                }
                else
                {
                    bounds = item.bounds;
                    hasBounds = true;
                }
            }
            if (!hasBounds)
            {
                bounds = new Bounds(_target.position + Vector3.up, new Vector3(0.6f, 2f, 0.6f));
            }
            Vector3 bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Vector3 top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            Vector3 aim = Vector3.Lerp(bottom, top, _aimHeight);
            Vector3 aimScreen = camera.WorldToScreenPoint(aim);
            if (aimScreen.z <= 0f)
            {
                return false;
            }
            screenPoint = aimScreen;
            screenHeight = Mathf.Abs(camera.WorldToScreenPoint(top).y - camera.WorldToScreenPoint(bottom).y);
            return true;
        }

        private void Animate(float elapsed)
        {
            float progress = Mathf.Clamp01(elapsed / _lockSeconds);
            float inverse = 1f - progress;
            float eased = 1f - inverse * inverse * inverse;
            float breathe = progress < 1f ? 1f : 1f + 0.03f * Mathf.Sin(elapsed * BreatheSpeed);
            _reticle.localScale = Vector3.one * (Mathf.Lerp(_startScale, 1f, eased) * breathe);
            _reticle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(_startAngle, 0f, eased));
            if (_ring != null)
            {
                Color ringColor = Color.Lerp(Color.white, _color, eased);
                ringColor.a = eased;
                _ring.color = ringColor;
            }
            if (_core != null)
            {
                // 고리가 자리 잡은 뒤에 가운데 점이 찍힌다.
                float coreIn = Mathf.Clamp01((elapsed - _lockSeconds * 0.6f) / (_lockSeconds * 0.6f));
                float pulse = 1f + 0.12f * Mathf.Sin(elapsed * CorePulseSpeed);
                _core.rectTransform.localScale = Vector3.one * (Mathf.Lerp(1.8f, 1f, coreIn) * pulse);
                Color coreColor = _color;
                coreColor.a = coreIn;
                _core.color = coreColor;
            }
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
