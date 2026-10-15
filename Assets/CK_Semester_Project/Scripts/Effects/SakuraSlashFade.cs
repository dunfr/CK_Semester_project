using UnityEngine;

public class SakuraSlashFade : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float duration = 0.36f;

    [Header("Reveal")]
    [SerializeField] private AnimationCurve progressCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.34f, 1.04f), new Keyframe(1f, 1.04f));
    [SerializeField] private AnimationCurve tailProgressCurve = AnimationCurve.Linear(0f, -0.2f, 1f, 1.2f);

    [Header("Fade Curves")]
    [SerializeField] private AnimationCurve coreAlphaCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [SerializeField] private AnimationCurve bodyAlphaCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("Flash Curve")]
    [SerializeField] private AnimationCurve coreIntensityCurve = AnimationCurve.Linear(0f, 4f, 1f, 0f);

    [Header("Petals")]
    [SerializeField] private ParticleSystem petals;
    [SerializeField, Range(0, 48)] private int petalCount = 18;

    public float Duration => Mathf.Max(0.01f, duration);
    private int emittedPetals;
    private float arcStart;
    private float arcSpan;
    private Vector2 arcCenter;

    private MaterialPropertyBlock propertyBlock;

    // Shader Graph References
    private static readonly int ProgressID =
        Shader.PropertyToID("_Progress");
    private static readonly int TailProgressID = Shader.PropertyToID("_TailProgress");

    private static readonly int CoreAlphaID =
        Shader.PropertyToID("_CoreAlpha");

    private static readonly int BodyAlphaID =
        Shader.PropertyToID("_Body_Alpha");

    private static readonly int CoreIntensityID =
        Shader.PropertyToID("_CoreIntensity");

    private float elapsed;
    private bool playing;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
    }

    private void OnEnable()
    {
        Play();
    }

    private void Update()
    {
        if (!playing)
            return;

        elapsed += Time.deltaTime;

        float normalizedTime =
            Mathf.Clamp01(elapsed / Duration);

        ApplyEffect(normalizedTime);
        EmitPetals(Mathf.Clamp01(progressCurve.Evaluate(normalizedTime)));

        if (normalizedTime >= 1f)
        {
            playing = false;
        }
    }

    private void ApplyEffect(float normalizedTime)
    {
        if (targetRenderer == null)
            return;

        float progress =
            progressCurve.Evaluate(normalizedTime);

        float coreAlpha =
            coreAlphaCurve.Evaluate(normalizedTime);

        float bodyAlpha =
            bodyAlphaCurve.Evaluate(normalizedTime);

        float coreIntensity =
            coreIntensityCurve.Evaluate(normalizedTime);

        targetRenderer.GetPropertyBlock(propertyBlock);

        propertyBlock.SetFloat(
            ProgressID,
            progress
        );
        propertyBlock.SetFloat(TailProgressID, tailProgressCurve.Evaluate(normalizedTime));

        propertyBlock.SetFloat(
            CoreAlphaID,
            coreAlpha
        );

        propertyBlock.SetFloat(
            BodyAlphaID,
            bodyAlpha
        );

        propertyBlock.SetFloat(
            CoreIntensityID,
            coreIntensity
        );

        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    public void Play()
    {
        Initialize();
        elapsed = 0f;
        playing = true;
        emittedPetals = 0;
        if (petals != null)
        {
            petals.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            petals.Play();
        }
        if (targetRenderer != null && targetRenderer.sharedMaterial != null)
        {
            var material = targetRenderer.sharedMaterial;
            arcStart = material.GetFloat("_Arc_Start");
            arcSpan = material.GetFloat("_Arc_Span");
            arcCenter = material.GetVector("_Arc_Center");
        }

        ApplyEffect(0f);
    }

    private void EmitPetals(float progress)
    {
        if (petals == null || targetRenderer == null || petalCount == 0) return;
        int count = Mathf.Min(petalCount, Mathf.FloorToInt(progress * petalCount));
        while (emittedPetals < count)
        {
            float along = (emittedPetals + 0.5f) / petalCount;
            float angle = (arcStart + along * arcSpan - 0.5f) * (2f * Mathf.PI);
            Vector3 radial = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f);
            Vector3 tangent = new Vector3(Mathf.Cos(angle), -Mathf.Sin(angle), 0f);
            var emission = new ParticleSystem.EmitParams
            {
                position = targetRenderer.transform.TransformPoint(new Vector3(arcCenter.x - 0.5f, arcCenter.y - 0.5f, 0f) + radial * 0.4f),
                velocity = targetRenderer.transform.TransformDirection(tangent + radial * 0.25f).normalized * Random.Range(0.5f, 1.15f) + Vector3.up * 0.15f,
                startSize = Random.Range(0.06f, 0.12f),
                startLifetime = Random.Range(0.35f, 0.65f),
                rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f))
            };
            petals.Emit(emission, 1);
            emittedPetals++;
        }
    }

    // Editor scrubbing uses the same property calculation as runtime playback.
    public void Preview(float normalizedTime)
    {
        Initialize();
        ApplyEffect(Mathf.Clamp01(normalizedTime));
    }

    public void ClearPreview()
    {
        Initialize();
        if (targetRenderer == null) return;
        targetRenderer.GetPropertyBlock(propertyBlock);
        var material = targetRenderer.sharedMaterial;
        if (material == null) return;
        foreach (int id in new[] { ProgressID, TailProgressID, CoreAlphaID, BodyAlphaID, CoreIntensityID })
            propertyBlock.SetFloat(id, material.GetFloat(id));
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void OnDisable()
    {
        playing = false;
        if (petals != null) petals.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (targetRenderer != null && propertyBlock != null) ApplyEffect(1f);
    }
}
