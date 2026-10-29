using UnityEngine;

[DisallowMultipleComponent]
public class SakuraImpactStreakPlayer : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float duration = 0.30f;

    [Header("Scale Curves")]
    [SerializeField] private AnimationCurve lengthCurve = CreateLengthCurve();
    [SerializeField] private AnimationCurve thicknessCurve = CreateThicknessCurve();

    [Header("Fade Curves")]
    [SerializeField] private AnimationCurve coreAlphaCurve = CreateCoreAlphaCurve();
    [SerializeField] private AnimationCurve glowAlphaCurve = CreateGlowAlphaCurve();

    private static readonly int CoreAlphaID = Shader.PropertyToID("_CoreAlpha");
    private static readonly int GlowAlphaID = Shader.PropertyToID("_GlowAlpha");

    private Vector3 baseScale;
    private MaterialPropertyBlock propertyBlock;
    private float elapsed;
    private bool playing;

    private void Awake()
    {
        baseScale = transform.localScale;
        propertyBlock = new MaterialPropertyBlock();

        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();
    }

    private void OnEnable()
    {
        Play();
    }

    public void Play()
    {
        InitializeEmptyCurves();
        elapsed = 0f;
        playing = true;
        ApplyEffect(0f);
    }

    private void Update()
    {
        if (!playing)
            return;

        elapsed += Time.deltaTime;
        float normalizedTime = duration > 0f
            ? Mathf.Clamp01(elapsed / duration)
            : 1f;

        ApplyEffect(normalizedTime);

        if (normalizedTime >= 1f)
        {
            playing = false;
            gameObject.SetActive(false);
        }
    }

    private void ApplyEffect(float normalizedTime)
    {
        transform.localScale = new Vector3(
            baseScale.x * lengthCurve.Evaluate(normalizedTime),
            baseScale.y * thicknessCurve.Evaluate(normalizedTime),
            baseScale.z);

        if (targetRenderer == null)
            return;

        // Preserve any other per-renderer overrides without changing the material.
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(CoreAlphaID, coreAlphaCurve.Evaluate(normalizedTime));
        propertyBlock.SetFloat(GlowAlphaID, glowAlphaCurve.Evaluate(normalizedTime));
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void OnDisable()
    {
        playing = false;
    }

    private void InitializeEmptyCurves()
    {
        // Keep authored Inspector curves intact across repeated playback.
        if (lengthCurve == null || lengthCurve.length == 0)
            lengthCurve = CreateLengthCurve();

        if (thicknessCurve == null || thicknessCurve.length == 0)
            thicknessCurve = CreateThicknessCurve();

        if (coreAlphaCurve == null || coreAlphaCurve.length == 0)
            coreAlphaCurve = CreateCoreAlphaCurve();

        if (glowAlphaCurve == null || glowAlphaCurve.length == 0)
            glowAlphaCurve = CreateGlowAlphaCurve();
    }

    private static AnimationCurve CreateLengthCurve()
    {
        return new AnimationCurve(
            new Keyframe(0.00f, 0.15f),
            new Keyframe(0.10f, 0.40f),
            new Keyframe(0.25f, 0.80f),
            new Keyframe(0.40f, 1.10f),
            new Keyframe(0.55f, 1.00f),
            new Keyframe(1.00f, 1.00f));
    }

    private static AnimationCurve CreateThicknessCurve()
    {
        return new AnimationCurve(
            new Keyframe(0.00f, 0.35f),
            new Keyframe(0.10f, 0.65f),
            new Keyframe(0.25f, 1.00f),
            new Keyframe(0.40f, 1.12f),
            new Keyframe(0.60f, 1.00f),
            new Keyframe(1.00f, 0.90f));
    }

    private static AnimationCurve CreateCoreAlphaCurve()
    {
        return new AnimationCurve(
            new Keyframe(0.00f, 0.65f),
            new Keyframe(0.08f, 1.00f),
            new Keyframe(0.45f, 1.00f),
            new Keyframe(0.68f, 0.00f),
            new Keyframe(1.00f, 0.00f));
    }

    private static AnimationCurve CreateGlowAlphaCurve()
    {
        return new AnimationCurve(
            new Keyframe(0.00f, 0.50f),
            new Keyframe(0.08f, 1.00f),
            new Keyframe(0.50f, 1.00f),
            new Keyframe(0.72f, 0.70f),
            new Keyframe(0.88f, 0.25f),
            new Keyframe(1.00f, 0.00f));
    }
}
