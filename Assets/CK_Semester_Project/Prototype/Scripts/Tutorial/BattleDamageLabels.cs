using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.Tutorial
{
    // 공격이 닿는 순간 맞은 부위에 피해 숫자(DamageLabel 프리팹)를 띄운다. 숫자는 맞은 자리에서 위로 떠오르며 사라진다.
    // 언제 띄울지는 TutorialBattleController가 정하고(타격 시점), 이 컴포넌트는 프리팹 선택·위치·움직임만 맡는다.
    // 전투 UI가 꺼지는 컷신(궁극기) 중에도 보이도록 전용 캔버스를 쓴다.
    [DisallowMultipleComponent]
    public sealed class BattleDamageLabels : MonoBehaviour
    {
        private sealed class Label
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Vector3 WorldPoint;
            public Vector2 ScreenOffset;
            public float Age;
        }

        [Header("Prefabs")]
        [SerializeField, Tooltip("적이 맞았을 때 띄우는 피해 숫자")]
        private GameObject _enemyLabel;
        [SerializeField, Tooltip("적이 약점을 맞았을 때 띄우는 피해 숫자(WEAK 표시 포함)")]
        private GameObject _enemyWeakLabel;
        [SerializeField, Tooltip("플레이어 캐릭터가 맞았을 때 띄우는 피해 숫자")]
        private GameObject _playerLabel;
        [SerializeField, Tooltip("플레이어 캐릭터가 약점을 맞았을 때 띄우는 피해 숫자(WEAK 표시 포함)")]
        private GameObject _playerWeakLabel;

        [Header("Placement")]
        [SerializeField, Range(0f, 1f), Tooltip("맞은 대상의 몸 높이 중 숫자가 뜨는 위치. 0 = 발, 0.5 = 몸 중심, 1 = 머리 위")]
        private float _bodyHeight = 0.6f;
        [SerializeField, Min(0f), Tooltip("연속으로 맞을 때 숫자가 겹치지 않게 흩뿌리는 범위(1920x1080 기준 픽셀)")]
        private float _scatterPixels = 45f;
        [SerializeField, Tooltip("숫자 크기를 맞출 기준 해상도(전투 HUD 캔버스와 같게 둡니다)")]
        private Vector2 _referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField, Tooltip("전용 캔버스의 정렬 순서. 전투 HUD(50)보다 크면 HUD 위에 그려집니다")]
        private int _sortingOrder = 60;

        [Header("Motion")]
        [SerializeField, Min(0.1f), Tooltip("숫자가 떠 있는 시간(초)")]
        private float _lifetime = 0.9f;
        [SerializeField, Min(0f), Tooltip("사라질 때까지 위로 떠오르는 거리(1920x1080 기준 픽셀)")]
        private float _risePixels = 70f;
        [SerializeField, Min(1f), Tooltip("뜨는 순간의 크기 배율. 이 크기에서 원래 크기로 빠르게 줄어듭니다")]
        private float _popScale = 1.5f;
        [SerializeField, Range(0.05f, 1f), Tooltip("떠 있는 시간 중 마지막 이 비율 동안 서서히 사라집니다")]
        private float _fadeFraction = 0.35f;

        private const float PopSeconds = 0.12f;
        private readonly List<Label> _labels = new List<Label>();
        private readonly List<Renderer> _rendererBuffer = new List<Renderer>();
        private RectTransform _canvasRect;
        private Camera _camera;

        private void Awake()
        {
            // 캔버스 크기 맞춤이 첫 숫자를 띄우기 전에 끝나 있도록 미리 만들어 둔다.
            EnsureCanvas();
        }

        // target이 맞은 자리에 피해 숫자를 띄운다. isPlayerTarget이 참이면 플레이어용, 거짓이면 적용 프리팹을 쓴다.
        public void Show(Transform target, int damage, bool isPlayerTarget, bool isWeak, Camera camera)
        {
            if (target == null || damage <= 0 || camera == null)
            {
                return;
            }
            GameObject prefab = isPlayerTarget ? (isWeak && _playerWeakLabel != null ? _playerWeakLabel : _playerLabel)
                : (isWeak && _enemyWeakLabel != null ? _enemyWeakLabel : _enemyLabel);
            if (prefab == null)
            {
                return;
            }
            _camera = camera;
            EnsureCanvas();
            GameObject instance = Instantiate(prefab, _canvasRect, false);
            // 프리팹 루트의 TMP가 피해 숫자다(WEAK 문구는 자식에 따로 있다).
            TMP_Text number = instance.GetComponent<TMP_Text>();
            if (number != null)
            {
                number.text = damage.ToString();
            }
            foreach (Graphic graphic in instance.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
            CanvasGroup group = instance.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = instance.AddComponent<CanvasGroup>();
            }
            group.blocksRaycasts = false;
            var label = new Label
            {
                Rect = (RectTransform)instance.transform,
                Group = group,
                WorldPoint = GetHitPoint(target),
                ScreenOffset = Random.insideUnitCircle * _scatterPixels,
            };
            _labels.Add(label);
            Place(label);
        }

        // 떠 있는 숫자를 모두 지운다(전투 종료 등).
        public void Clear()
        {
            foreach (Label label in _labels)
            {
                if (label.Rect != null)
                {
                    Destroy(label.Rect.gameObject);
                }
            }
            _labels.Clear();
        }

        // 카메라가 그 프레임 위치를 확정한 뒤에 화면 위치를 맞춘다(컷신 카메라는 Cinemachine이 LateUpdate에서 움직인다).
        private void LateUpdate()
        {
            for (int i = _labels.Count - 1; i >= 0; i--)
            {
                Label label = _labels[i];
                label.Age += Time.deltaTime;
                if (label.Rect == null || label.Age >= _lifetime)
                {
                    if (label.Rect != null)
                    {
                        Destroy(label.Rect.gameObject);
                    }
                    _labels.RemoveAt(i);
                    continue;
                }
                Place(label);
            }
        }

        private void OnDisable()
        {
            Clear();
        }

        private void Place(Label label)
        {
            if (_camera == null)
            {
                return;
            }
            Vector3 screen = _camera.WorldToScreenPoint(label.WorldPoint);
            bool isVisible = screen.z > 0f;
            float progress = Mathf.Clamp01(label.Age / _lifetime);
            float fadeStart = 1f - _fadeFraction;
            label.Group.alpha = !isVisible ? 0f : progress <= fadeStart ? 1f : 1f - (progress - fadeStart) / _fadeFraction;
            if (!isVisible)
            {
                return;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out Vector2 local);
            // 처음에 빠르게 떠오르고 점점 느려진다.
            float rise = 1f - (1f - progress) * (1f - progress);
            label.Rect.anchoredPosition = local + label.ScreenOffset + Vector2.up * (_risePixels * rise);
            float pop = Mathf.Clamp01(label.Age / PopSeconds);
            label.Rect.localScale = Vector3.one * Mathf.Lerp(_popScale, 1f, pop * pop * (3f - 2f * pop));
        }

        // 맞은 부위: 대상 몸(메시)의 가로 중심, 높이는 Body Height 비율. 메시가 없으면 발 위 1m.
        private Vector3 GetHitPoint(Transform target)
        {
            target.GetComponentsInChildren(false, _rendererBuffer);
            bool hasSkinned = _rendererBuffer.Exists(item => item is SkinnedMeshRenderer && item.enabled);
            bool hasBounds = false;
            Bounds bounds = default;
            foreach (Renderer item in _rendererBuffer)
            {
                // 이펙트·무기 궤적이 섞이지 않게 캐릭터 메시만 본다.
                bool isBody = hasSkinned ? item is SkinnedMeshRenderer : item is MeshRenderer;
                if (!isBody || !item.enabled)
                {
                    continue;
                }
                if (!hasBounds)
                {
                    bounds = item.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(item.bounds);
                }
            }
            _rendererBuffer.Clear();
            if (!hasBounds)
            {
                return target.position + Vector3.up;
            }
            return new Vector3(bounds.center.x, Mathf.Lerp(bounds.min.y, bounds.max.y, _bodyHeight), bounds.center.z);
        }

        private void EnsureCanvas()
        {
            if (_canvasRect != null)
            {
                return;
            }
            var root = new GameObject("Damage Labels", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.layer = LayerMask.NameToLayer("UI");
            root.transform.SetParent(transform, false);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = _sortingOrder;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = _referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _canvasRect = (RectTransform)root.transform;
        }
    }
}
