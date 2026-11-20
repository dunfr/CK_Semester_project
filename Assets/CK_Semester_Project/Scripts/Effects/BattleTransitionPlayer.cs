using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.Effects
{
    public enum BattleTransitionStyle
    {
        // 비스듬한 띠가 색 → 검정으로 겹겹이 덮였다가 붉은 잉크와 함께 걷힌다.
        LayeredStripe,
        // 직전 화면에 금이 가고 대각선으로 갈라진 조각이 미끄러져 새 화면을 드러낸다.
        ShatterSplit
    }

    // 전투 진입 화면 전환 연출. 화면이 가려진 시점(onCovered)에 호출한 쪽이 씬·카메라를 바꾸면 된다.
    // TODO: 몬스터 접촉 → 전투 진입 흐름과 연결 (현재는 테스트 입력으로만 재생).
    [DisallowMultipleComponent]
    public sealed class BattleTransitionPlayer : MonoBehaviour
    {
        // 씬 전환 페이드(32000)보다 아래, 일반 HUD보다 위
        private const int DefaultSortingOrder = 31000;
        // 긴 프레임 하나가 연출 구간을 통째로 건너뛰지 않게 한다.
        private const float MaxStep = 0.05f;

        [Header("Shaders")]
        [Tooltip("CK/UI/BattleTransitionStripe")]
        [SerializeField] private Shader _stripeShader;
        [Tooltip("CK/UI/BattleTransitionShatter")]
        [SerializeField] private Shader _shatterShader;

        [Header("Layered Stripe")]
        [Tooltip("띠가 쌓여 화면이 완전히 덮이기까지의 시간(초)")]
        [SerializeField, Min(0.05f)] private float _stripeCoverSeconds = 0.7f;
        [Tooltip("완전히 덮인 채 유지하는 시간(초). 이 동안 씬 전환이 일어난다.")]
        [SerializeField, Min(0f)] private float _stripeHoldSeconds = 0.3f;
        [Tooltip("띠가 걷혀 새 화면이 드러나기까지의 시간(초)")]
        [SerializeField, Min(0.05f)] private float _stripeRevealSeconds = 0.7f;
        [Tooltip("덮을 때 아래→위 순서로 쌓이는 세 겹의 색. 알파 = 비침 정도(마지막 겹은 1 권장)")]
        [SerializeField] private Color[] _coverLayerColors =
        {
            new Color(0.02f, 0.02f, 0.03f, 0.5f),
            new Color(0.02f, 0.02f, 0.03f, 0.8f),
            new Color(0.02f, 0.02f, 0.03f, 1f)
        };
        [Tooltip("걷힐 때 세 겹의 색. 위 겹부터 걷힌다.")]
        [SerializeField] private Color[] _revealLayerColors =
        {
            new Color(0.02f, 0.02f, 0.03f, 0.5f),
            new Color(0.02f, 0.02f, 0.03f, 0.8f),
            new Color(0.02f, 0.02f, 0.03f, 1f)
        };
        [Tooltip("띠 기울기(도). 0 = 가로")]
        [SerializeField] private float _stripeAngle = -62f;
        [Tooltip("띠 하나의 폭(화면 높이 비율)")]
        [SerializeField, Range(0.05f, 0.6f)] private float _stripeBandWidth = 0.2f;

        [Header("Shatter Split")]
        [Tooltip("시작 시 검은 화면에서 멈춘 화면으로 페이드 인되는 시간(초). 이후 금이 간다.")]
        [SerializeField, Min(0f)] private float _preFadeInSeconds = 0.2f;
        [Tooltip("페이드 인 시작 어둡기. 1 = 완전히 검정에서 시작")]
        [SerializeField, Range(0f, 1f)] private float _preFadeDim = 1f;
        [Tooltip("금이 퍼지는 시간(초)")]
        [SerializeField, Min(0.01f)] private float _crackSeconds = 0.08f;
        [Tooltip("금이 다 간 뒤 갈라지기 전까지 멈추는 시간(초)")]
        [SerializeField, Min(0f)] private float _crackHoldSeconds = 0.05f;
        [Tooltip("조각이 미끄러져 화면 밖으로 나가는 시간(초)")]
        [SerializeField, Min(0.05f)] private float _splitSeconds = 0.8f;
        [Tooltip("갈라짐 시작 후 틈의 빛이 최대로 차오르기까지 걸리는 비율(갈라짐 시간 대비 0~1)")]
        [SerializeField, Range(0.01f, 1f)] private float _lightBurstPortion = 0.25f;
        [Tooltip("조각이 다 나간 뒤 빛이 가라앉으며 새 화면이 드러나는 시간(초)")]
        [SerializeField, Min(0f)] private float _lightSettleSeconds = 0.6f;
        [Tooltip("틈에서 나오는 빛기둥 두께(화면 높이 비율). 클수록 굵다")]
        [SerializeField, Range(0.02f, 0.8f)] private float _lightCoreWidth = 0.28f;
        [Tooltip("빛이 덮는 화면 전체 밝기. 1 = 틈 밖도 하얗게")]
        [SerializeField, Range(0f, 1f)] private float _lightWash = 0.8f;
        [Tooltip("빛기둥 바깥 후광이 퍼지는 폭(빛기둥 두께의 배수)")]
        [SerializeField, Range(1f, 8f)] private float _lightSpread = 3f;
        [Tooltip("틈에서 뿜어지는 빛 색")]
        [SerializeField] private Color _lightColor = new Color(0.85f, 0.95f, 1f, 1f);
        [Tooltip("금이 갈 때 번쩍이는 흰빛 최대 세기")]
        [SerializeField, Range(0f, 1f)] private float _flashPeak = 0.55f;
        [Tooltip("켜면 화면 왼쪽 아래 → 오른쪽 위 모서리를 잇는 대각선으로 자른다(해상도 비율에 맞춤). 끄면 _cutAngle 사용")]
        [SerializeField] private bool _cutAlongScreenDiagonal;
        [Tooltip("자르는 선 기울기(도). 0 = 가로, 90 = 세로. _cutAlongScreenDiagonal이 꺼져 있을 때만 사용")]
        [SerializeField, Range(0f, 180f)] private float _cutAngle = 60f;
        [Tooltip("켜면 두 조각이 가로로(왼쪽은 왼쪽, 오른쪽은 오른쪽) 갈라진다. 끄면 자르는 선을 따라 미끄러진다.")]
        [SerializeField] private bool _slideHorizontally = true;
        [Tooltip("자르는 선 위치(화면 중심 기준, 화면 높이 비율). 1~3개. 0 하나 = 중앙에서 두 조각으로 대칭 분할")]
        [SerializeField] private float[] _cutOffsets = { 0f };

        [Header("Overlay")]
        [Tooltip("연출 Canvas의 정렬 순서")]
        [SerializeField] private int _sortingOrder = DefaultSortingOrder;

        private Canvas _canvas;
        private RawImage _image;
        private Material _stripeMaterial;
        private Material _shatterMaterial;
        private Texture2D _capturedScreen;
        private bool _isPlaying;

        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int[] LayerColorIds =
        {
            Shader.PropertyToID("_ColorA"),
            Shader.PropertyToID("_ColorB"),
            Shader.PropertyToID("_ColorC")
        };
        private static readonly int AngleId = Shader.PropertyToID("_Angle");
        private static readonly int BandWidthId = Shader.PropertyToID("_BandWidth");
        private static readonly int SweepFlipId = Shader.PropertyToID("_SweepFlip");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int CrackId = Shader.PropertyToID("_Crack");
        private static readonly int FlashId = Shader.PropertyToID("_Flash");
        private static readonly int SplitId = Shader.PropertyToID("_Split");
        private static readonly int DimId = Shader.PropertyToID("_Dim");
        private static readonly int SlideModeId = Shader.PropertyToID("_SlideMode");
        private static readonly int LightId = Shader.PropertyToID("_Light");
        private static readonly int LightColorId = Shader.PropertyToID("_LightColor");
        private static readonly int LightCoreWidthId = Shader.PropertyToID("_LightCoreWidth");
        private static readonly int LightWashId = Shader.PropertyToID("_LightWash");
        private static readonly int LightSpreadId = Shader.PropertyToID("_LightSpread");
        private static readonly int CutAngleId = Shader.PropertyToID("_CutAngle");
        private static readonly int CutOffsetsId = Shader.PropertyToID("_CutOffsets");
        private static readonly int CutCountId = Shader.PropertyToID("_CutCount");

        public bool IsPlaying => _isPlaying;

        private void Awake()
        {
            _stripeMaterial = CreateMaterial(_stripeShader, "CK/UI/BattleTransitionStripe");
            _shatterMaterial = CreateMaterial(_shatterShader, "CK/UI/BattleTransitionShatter");
            CreateOverlay();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            FinishPlayback();
        }

        private void OnDestroy()
        {
            Destroy(_stripeMaterial);
            Destroy(_shatterMaterial);
        }

        /// <summary>전환을 재생한다. 재생 중이거나 셰이더가 없으면 false.</summary>
        /// <param name="onCovered">직전 화면이 가려진 순간. 여기서 씬·카메라를 바꾼다.</param>
        /// <param name="onFinished">새 화면이 다 드러난 뒤.</param>
        public bool Play(BattleTransitionStyle style, Action onCovered = null, Action onFinished = null)
        {
            if (_isPlaying)
            {
                return false;
            }

            Material material = style == BattleTransitionStyle.LayeredStripe ? _stripeMaterial : _shatterMaterial;
            if (material == null)
            {
                Debug.LogError($"{nameof(BattleTransitionPlayer)}: {style} 셰이더가 없어 재생할 수 없습니다.", this);
                return false;
            }

            _isPlaying = true;
            IEnumerator routine = style == BattleTransitionStyle.LayeredStripe
                ? PlayLayeredStripe(onCovered, onFinished)
                : PlayShatterSplit(onCovered, onFinished);
            StartCoroutine(routine);
            return true;
        }

        private IEnumerator PlayLayeredStripe(Action onCovered, Action onFinished)
        {
            Material material = _stripeMaterial;
            material.SetFloat(AspectId, ScreenAspect());
            material.SetFloat(AngleId, _stripeAngle);
            material.SetFloat(BandWidthId, _stripeBandWidth);
            material.SetFloat(SeedId, UnityEngine.Random.Range(0f, 100f));
            ApplyLayerColors(material, _coverLayerColors);
            material.SetFloat(SweepFlipId, 0f);
            material.SetFloat(ProgressId, 0f);
            ShowOverlay(material, null);

            yield return Animate(_stripeCoverSeconds, t => material.SetFloat(ProgressId, EaseInOut(t)));

            onCovered?.Invoke();
            yield return WaitUnscaled(_stripeHoldSeconds);

            // 걷힐 때는 색을 바꾸고 들어온 반대쪽으로 빠져나간다. 띠 배치도 새로 뽑는다.
            ApplyLayerColors(material, _revealLayerColors);
            material.SetFloat(SweepFlipId, 1f);
            material.SetFloat(SeedId, UnityEngine.Random.Range(0f, 100f));
            yield return Animate(_stripeRevealSeconds, t => material.SetFloat(ProgressId, 1f - EaseInOut(t)));

            FinishPlayback();
            onFinished?.Invoke();
        }

        private IEnumerator PlayShatterSplit(Action onCovered, Action onFinished)
        {
            // 연출 Canvas가 찍히지 않도록 띄우기 전에 이번 프레임을 그대로 저장한다.
            yield return new WaitForEndOfFrame();
            ReleaseCapture();
            _capturedScreen = ScreenCapture.CaptureScreenshotAsTexture();

            Material material = _shatterMaterial;
            material.SetFloat(AspectId, ScreenAspect());
            material.SetFloat(SeedId, UnityEngine.Random.Range(0f, 100f));
            float cutAngle = _cutAlongScreenDiagonal ? Mathf.Atan2(1f, ScreenAspect()) * Mathf.Rad2Deg : _cutAngle;
            material.SetFloat(CutAngleId, cutAngle);
            material.SetFloat(SlideModeId, _slideHorizontally ? 1f : 0f);
            ApplyCutOffsets(material);
            material.SetFloat(CrackId, 0f);
            material.SetFloat(FlashId, 0f);
            material.SetFloat(SplitId, 0f);
            material.SetFloat(DimId, _preFadeDim);
            material.SetFloat(LightId, 0f);
            material.SetColor(LightColorId, _lightColor);
            material.SetFloat(LightCoreWidthId, _lightCoreWidth);
            material.SetFloat(LightWashId, _lightWash);
            material.SetFloat(LightSpreadId, _lightSpread);
            ShowOverlay(material, _capturedScreen);

            // 멈춘 화면이 덮고 있으므로 아래에서는 바로 새 화면으로 바꿔도 된다.
            onCovered?.Invoke();

            // 시작에만 검정 → 멈춘 화면 페이드 인. 끝은 조각이 갈라지며 바로 새 화면이 드러난다.
            yield return Animate(_preFadeInSeconds, t => material.SetFloat(DimId, _preFadeDim * (1f - EaseInOut(t))));

            yield return Animate(_crackSeconds, t =>
            {
                material.SetFloat(CrackId, EaseOut(t));
                material.SetFloat(FlashId, _flashPeak * (1f - t));
            });
            material.SetFloat(FlashId, 0f);
            yield return WaitUnscaled(_crackHoldSeconds);

            // 갈라지는 틈에서 빛이 뿜어져 나오고, 조각이 다 나간 뒤 빛이 가라앉으며 전환이 끝난다.
            yield return Animate(_splitSeconds, t =>
            {
                material.SetFloat(SplitId, t * t);
                material.SetFloat(LightId, EaseOut(Mathf.Clamp01(t / _lightBurstPortion)));
            });
            yield return Animate(_lightSettleSeconds, t => material.SetFloat(LightId, 1f - EaseInOut(t)));

            FinishPlayback();
            onFinished?.Invoke();
        }

        private void CreateOverlay()
        {
            var root = new GameObject("BattleTransitionOverlay", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false);
            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = _sortingOrder;

            var imageObject = new GameObject("Screen", typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(root.transform, false);
            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _image = imageObject.GetComponent<RawImage>();
            _image.raycastTarget = false;

            root.SetActive(false);
        }

        private void ShowOverlay(Material material, Texture texture)
        {
            _image.material = material;
            _image.texture = texture;
            _canvas.gameObject.SetActive(true);
        }

        private void FinishPlayback()
        {
            if (_canvas != null)
            {
                _canvas.gameObject.SetActive(false);
            }
            if (_image != null)
            {
                _image.texture = null;
            }
            ReleaseCapture();
            _isPlaying = false;
        }

        private void ReleaseCapture()
        {
            if (_capturedScreen == null)
            {
                return;
            }
            Destroy(_capturedScreen);
            _capturedScreen = null;
        }

        private void ApplyLayerColors(Material material, Color[] colors)
        {
            for (int i = 0; i < LayerColorIds.Length; i++)
            {
                bool hasColor = colors != null && i < colors.Length;
                // 비어 있는 겹은 보이지 않게 둔다.
                material.SetColor(LayerColorIds[i], hasColor ? colors[i] : Color.clear);
            }
        }

        private void ApplyCutOffsets(Material material)
        {
            int count = _cutOffsets == null ? 0 : Mathf.Clamp(_cutOffsets.Length, 0, 3);
            if (count == 0)
            {
                material.SetVector(CutOffsetsId, Vector4.zero);
                material.SetFloat(CutCountId, 1f);
                return;
            }

            // 셰이더는 선이 아래에서 위 순서라고 가정한다.
            float[] sorted = new float[count];
            Array.Copy(_cutOffsets, sorted, count);
            Array.Sort(sorted);
            var offsets = new Vector4(sorted[0], count > 1 ? sorted[1] : 0f, count > 2 ? sorted[2] : 0f, 0f);
            material.SetVector(CutOffsetsId, offsets);
            material.SetFloat(CutCountId, count);
        }

        private Material CreateMaterial(Shader shader, string shaderName)
        {
            if (shader == null)
            {
                shader = Shader.Find(shaderName);
            }
            if (shader == null)
            {
                Debug.LogError($"{nameof(BattleTransitionPlayer)}: 셰이더 '{shaderName}'를 찾지 못했습니다. Inspector에 연결하세요.", this);
                return null;
            }
            return new Material(shader) { hideFlags = HideFlags.DontSave };
        }

        private static IEnumerator Animate(float seconds, Action<float> apply)
        {
            for (float elapsed = 0f; elapsed < seconds;)
            {
                apply(Mathf.Clamp01(elapsed / seconds));
                yield return null;
                elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
            }
            apply(1f);
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            if (seconds > 0f)
            {
                yield return new WaitForSecondsRealtime(seconds);
            }
        }

        private static float ScreenAspect()
        {
            return Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
        }

        private static float EaseInOut(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private static float EaseOut(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }
    }
}
