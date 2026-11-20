using System.Collections.Generic;
using CK.SemesterProject.LevelDesign;
using CK.SemesterProject.Tutorial;
using TMPro;
using UnityEngine;

namespace CK.SemesterProject.UI
{
    /// <summary>
    /// 미니맵 안내 경로를 화면에도 보여 준다. 다음에 꺾어야 할 지점(가까우면 목표 자체)에 미니맵과 같은 목표 아이콘을 띄우고,
    /// 그 지점이 화면 밖이거나 카메라 뒤에 있으면 화면 가장자리에 붙어 방향을 알려 준다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObjectiveGuideMarker : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("안내 경로를 계산하는 필드 미니맵")]
        private FieldMinimapBlueprint _minimap;
        [SerializeField, Tooltip("전투·연출 중 표시 여부를 판단할 레벨 진행")]
        private LevelProgress _progress;
        [SerializeField, Tooltip("비워 두면 Camera.main을 씁니다.")]
        private Camera _camera;
        [SerializeField, Tooltip("움직일 표시 묶음(아이콘과 거리 문구의 부모)")]
        private RectTransform _marker;
        [SerializeField, Tooltip("표시 묶음의 투명도(선택)")]
        private CanvasGroup _group;
        [SerializeField, Tooltip("목표까지 남은 거리 문구(선택)")]
        private TMP_Text _distanceText;

        [Header("Placement")]
        [SerializeField, Min(0.5f), Tooltip("플레이어와 이보다 가까운 꺾임점은 건너뛰고 그다음 점을 가리킨다(m)")]
        private float _cornerSkipDistance = 3f;
        [SerializeField, Tooltip("가리키는 지점 위로 띄우는 높이(m)")]
        private float _heightOffset = 0.6f;
        [SerializeField, Min(0f), Tooltip("화면 밖일 때 가장자리에서 떨어뜨리는 여백(px, 기준 해상도)")]
        private float _edgeMargin = 80f;
        [SerializeField, Min(0f), Tooltip("목표에 이만큼 가까워지면 숨긴다(m)")]
        private float _arriveDistance = 1.5f;
        [SerializeField, Min(0.1f), Tooltip("표시가 새 위치로 따라가는 속도(1/초)")]
        private float _followSpeed = 14f;

        [Header("Motion")]
        [SerializeField, Min(0f), Tooltip("위아래로 떠 있는 움직임 폭(px)")]
        private float _bobAmplitude = 6f;
        [SerializeField, Min(0f), Tooltip("위아래 움직임 속도(rad/초)")]
        private float _bobSpeed = 3.5f;
        [SerializeField, Min(0.1f), Tooltip("나타나고 사라지는 속도(1/초)")]
        private float _fadeSpeed = 6f;

        private RectTransform _area;
        private Vector2 _shownPosition;
        private bool _hasShownPosition;
        private float _alpha;
        private int _shownDistance = -1;

        private void Awake()
        {
            _area = transform as RectTransform;
            if (_camera == null)
            {
                _camera = Camera.main;
            }
            if (_minimap == null || _progress == null || _marker == null || _area == null)
            {
                Debug.LogError(name + ": 미니맵·레벨 진행·표시 RectTransform 참조가 없어 화면 길 안내를 끕니다.", this);
                enabled = false;
                return;
            }
            SetAlpha(0f);
        }

        private void LateUpdate()
        {
            Vector3 guidePoint = Vector3.zero;
            bool canShow = CanShow() && TryGetGuidePoint(out guidePoint);
            if (canShow)
            {
                PlaceMarker(guidePoint);
                UpdateDistanceText();
            }
            else
            {
                _hasShownPosition = false;
            }
            float targetAlpha = canShow ? 1f : 0f;
            SetAlpha(Mathf.MoveTowards(_alpha, targetAlpha, _fadeSpeed * Time.unscaledDeltaTime));
        }

        private bool CanShow()
        {
            return _camera != null && _camera.isActiveAndEnabled && _progress.ObjectiveTarget != null
                && _progress.Player != null && !_progress.IsInBattle && !_progress.IsControlLocked
                && _minimap.RoutePointCount >= 2;
        }

        private bool TryGetGuidePoint(out Vector3 point)
        {
            IReadOnlyList<Vector3> route = _minimap.RoutePoints;
            Vector3 player = _progress.Player.position;
            point = route[route.Count - 1];
            if (FlatDistance(player, _progress.ObjectiveTarget.position) <= _arriveDistance)
            {
                return false;
            }
            // 첫 점은 경로 계산 당시 플레이어 위치라 건너뛴다.
            for (int i = 1; i < route.Count; i++)
            {
                if (FlatDistance(player, route[i]) >= _cornerSkipDistance)
                {
                    point = route[i];
                    break;
                }
            }
            point += Vector3.up * _heightOffset;
            return true;
        }

        private void PlaceMarker(Vector3 worldPoint)
        {
            Vector3 screen = _camera.WorldToScreenPoint(worldPoint);
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 screenPoint = new Vector2(screen.x, screen.y);
            // 카메라 뒤에 있으면 투영이 뒤집히므로 방향을 반대로 돌려 화면 가장자리에 붙인다.
            if (screen.z < 0f)
            {
                screenPoint = center - (screenPoint - center);
                screenPoint = center + (screenPoint - center).normalized * Mathf.Max(Screen.width, Screen.height);
            }
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, screenPoint, null, out Vector2 local))
            {
                return;
            }
            local = ClampToArea(local);
            if (!_hasShownPosition)
            {
                _shownPosition = local;
                _hasShownPosition = true;
            }
            _shownPosition = Vector2.Lerp(_shownPosition, local, 1f - Mathf.Exp(-_followSpeed * Time.unscaledDeltaTime));
            float bob = Mathf.Sin(Time.unscaledTime * _bobSpeed) * _bobAmplitude;
            _marker.anchoredPosition = _shownPosition + Vector2.up * bob;
        }

        // 화면 밖 지점은 중심에서 그 방향으로 뻗은 선이 여백 안쪽 사각형과 만나는 곳에 둔다.
        private Vector2 ClampToArea(Vector2 local)
        {
            Rect rect = _area.rect;
            Vector2 center = rect.center;
            Vector2 half = new Vector2(rect.width * 0.5f - _edgeMargin, rect.height * 0.5f - _edgeMargin);
            Vector2 offset = local - center;
            if (half.x <= 0f || half.y <= 0f || (Mathf.Abs(offset.x) <= half.x && Mathf.Abs(offset.y) <= half.y))
            {
                return local;
            }
            float scale = Mathf.Min(half.x / Mathf.Max(0.0001f, Mathf.Abs(offset.x)),
                half.y / Mathf.Max(0.0001f, Mathf.Abs(offset.y)));
            return center + offset * scale;
        }

        private void UpdateDistanceText()
        {
            if (_distanceText == null)
            {
                return;
            }
            int distance = Mathf.RoundToInt(_minimap.RemainingDistance);
            if (distance == _shownDistance)
            {
                return;
            }
            _shownDistance = distance;
            _distanceText.text = distance + "m";
        }

        private void SetAlpha(float alpha)
        {
            _alpha = alpha;
            if (_group != null)
            {
                _group.alpha = alpha;
            }
            else
            {
                _marker.gameObject.SetActive(alpha > 0f);
            }
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            return new Vector2(a.x - b.x, a.z - b.z).magnitude;
        }
    }
}
