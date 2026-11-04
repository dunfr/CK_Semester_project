using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
[AddComponentMenu("CK/이펙트 조명 재생")]
public sealed class EffectLightPulse : MonoBehaviour
{
    [SerializeField, Min(0.01f), Tooltip("빛이 켜졌다 꺼지는 시간입니다. 단위: 초.")]
    private float _duration = 0.45f;

    [SerializeField, Tooltip("시간 0~1에 따른 밝기 배율입니다. Light의 Intensity를 최대 밝기로 사용합니다.")]
    private AnimationCurve _intensityCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.12f, 1f),
        new Keyframe(0.35f, 0.8f), new Keyframe(1f, 0f));

    private Light _light;
    private float _baseIntensity;
    private float _elapsed;
    private bool _isInitialized;
    private bool _isPlaying;

    public float Duration => Mathf.Max(0.01f, _duration);
    public bool IsPlaying => _isPlaying;

    private void OnEnable()
    {
        // Edit mode keeps the native Light settings available for normal Inspector authoring.
        if (Application.isPlaying)
        {
            Play();
        }
    }

    private void Update()
    {
        Advance(Time.deltaTime);
    }

    private void OnDisable()
    {
        ClearPreview();
    }

    public void Play()
    {
        Initialize();
        _elapsed = 0f;
        _isPlaying = true;
        Preview(0f);
    }

    public void Advance(float seconds)
    {
        if (!_isPlaying)
        {
            return;
        }
        _elapsed = Mathf.Min(Duration, _elapsed + Mathf.Max(0f, seconds));
        Preview(_elapsed / Duration);
        if (_elapsed >= Duration)
        {
            _isPlaying = false;
        }
    }

    public void Preview(float normalizedTime)
    {
        Initialize();
        if (_light == null)
        {
            return;
        }
        float time = Mathf.Clamp01(normalizedTime);
        float multiplier = _intensityCurve != null && _intensityCurve.length > 0
            ? Mathf.Max(0f, _intensityCurve.Evaluate(time)) : 1f;
        _light.intensity = time >= 1f ? 0f : _baseIntensity * multiplier;
    }

    public void ClearPreview()
    {
        if (_isInitialized && _light != null)
        {
            _light.intensity = _baseIntensity;
        }
        _isInitialized = false;
        _isPlaying = false;
        _elapsed = 0f;
    }

    private void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }
        _light = GetComponent<Light>();
        if (_light == null)
        {
            return;
        }
        _baseIntensity = _light.intensity;
        _isInitialized = true;
    }
}
