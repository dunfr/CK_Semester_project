using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.Tutorial
{
    // 공격이 닿는 순간 맞은 대상의 머리 둘레에 그린 원 안 아무 곳에나 피해 숫자(DamageLabel 프리팹)를 띄운다.
    // 플레이어와 적 모두 같은 방식이며, 숫자는 그 자리에서 위로 떠오르며 사라진다.
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
            public float HalfWidth;
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
        [SerializeField, Tooltip("숫자가 흩어지는 원의 중심으로 쓸 뼈 이름. 위에서부터 찾아 처음 나오는 뼈의 위치가 중심입니다(기본값은 머리 → 목 → 가슴). 자세가 바뀌어도 그 뼈를 따라갑니다")]
        private string[] _hitBoneNames = { "Bip001 Head", "Bip001 Neck", "Bip001 Spine2" };
        [SerializeField, Range(0f, 1f), Tooltip("위 뼈를 찾지 못한 대상의 원 중심 높이. 몸 높이 비율이며 0 = 발, 0.5 = 허리, 1 = 머리 끝")]
        private float _hitHeight = 0.9f;
        [SerializeField, Min(0f), Tooltip("숫자가 흩어지는 원의 반지름(1920x1080 기준 픽셀). 대상이 멀든 가깝든 화면에서 같은 크기이며, 숫자는 이 원 안 아무 곳에나 뜹니다")]
        private float _spreadRadiusPixels = 80f;
        [SerializeField, Min(0f), Tooltip("숫자가 화면 밖으로 나가지 않게 두는 가장자리 여백(1920x1080 기준 픽셀)")]
        private float _screenMargin = 24f;
        [SerializeField, Tooltip("숫자 크기를 맞출 기준 해상도(전투 HUD 캔버스와 같게 둡니다)")]
        private Vector2 _referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField, Tooltip("전용 캔버스의 정렬 순서. 전투 HUD(50)보다 크면 HUD 위에 그려집니다")]
        private int _sortingOrder = 60;

        [Header("Size")]
        [SerializeField, Min(0f), Tooltip("피해가 클수록 숫자 글자를 키우는 최대 폭. 프리팹에 정해 둔 글자 크기가 가장 작은 크기이고, 여기에 이 값까지 더해집니다. 약점 숫자(WEAK)는 키우지 않습니다")]
        private float _fontGrowMax = 4f;
        [SerializeField, Tooltip("글자가 커지는 피해 범위(X = 이 피해 이하면 프리팹 크기 그대로, Y = 이 피해 이상이면 가장 큰 크기). 그 사이는 피해에 비례해 커집니다")]
        private Vector2 _fontGrowDamageRange = new Vector2(10f, 200f);

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
        // 대상마다 찾아 둔 피격 지점 뼈(없으면 null).
        private readonly Dictionary<Transform, Transform> _hitBones = new Dictionary<Transform, Transform>();
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
                // 피해가 클수록 글자를 키운다. 프리팹 크기가 최소이고, 약점 숫자는 프리팹 크기 그대로 둔다.
                if (!isWeak)
                {
                    number.enableAutoSizing = false;
                    number.fontSize += _fontGrowMax * Mathf.InverseLerp(_fontGrowDamageRange.x, _fontGrowDamageRange.y, damage);
                }
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
            Bounds body = GetBodyBounds(target);
            // 가로 중심은 대상이 서 있는 자리(루트)로 잡는다. 메시 범위의 중심은 든 무기 쪽으로 치우친다.
            body.center = new Vector3(target.position.x, body.center.y, target.position.z);
            var label = new Label
            {
                Rect = (RectTransform)instance.transform,
                Group = group,
                WorldPoint = GetHitPoint(target, body),
                // 중심 둘레의 원 안 아무 곳(원 안에 고르게 퍼진다)
                ScreenOffset = Random.insideUnitCircle * _spreadRadiusPixels,
                HalfWidth = number != null ? number.preferredWidth * 0.5f : 0f,
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
            Vector2 position = local + label.ScreenOffset + Vector2.up * (_risePixels * rise);
            // 컷신처럼 카메라가 움직여 대상이 화면 가장자리로 가도 숫자가 잘리지 않게 한다.
            float limit = Mathf.Max(0f, _canvasRect.rect.width * 0.5f - label.HalfWidth - _screenMargin);
            position.x = Mathf.Clamp(position.x, -limit, limit);
            label.Rect.anchoredPosition = position;
            float pop = Mathf.Clamp01(label.Age / PopSeconds);
            label.Rect.localScale = Vector3.one * Mathf.Lerp(_popScale, 1f, pop * pop * (3f - 2f * pop));
        }

        private Vector2 ToCanvas(Vector3 worldPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, _camera.WorldToScreenPoint(worldPoint), null, out Vector2 local);
            return local;
        }

        // 피격 지점: 대상의 가슴 뼈 위치. 뼈를 찾지 못하면 서 있는 자리 위로 Hit Height 비율만큼 올라간 곳.
        private Vector3 GetHitPoint(Transform target, Bounds body)
        {
            if (!_hitBones.TryGetValue(target, out Transform bone))
            {
                foreach (string boneName in _hitBoneNames)
                {
                    bone = FindChild(target, boneName);
                    if (bone != null)
                    {
                        break;
                    }
                }
                _hitBones[target] = bone;
            }
            return bone != null ? bone.position
                : new Vector3(body.center.x, Mathf.Lerp(body.min.y, body.max.y, _hitHeight), body.center.z);
        }

        private static Transform FindChild(Transform root, string childName)
        {
            if (string.IsNullOrEmpty(childName))
            {
                return null;
            }
            foreach (Transform child in root.GetComponentsInChildren<Transform>(false))
            {
                if (child.name == childName)
                {
                    return child;
                }
            }
            return null;
        }

        // 대상 몸(캐릭터 메시)이 차지하는 범위. 메시가 없으면 발 위로 사람 크기만큼 잡는다.
        private Bounds GetBodyBounds(Transform target)
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
            return hasBounds ? bounds : new Bounds(target.position + Vector3.up * 0.85f, new Vector3(0.6f, 1.7f, 0.6f));
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
