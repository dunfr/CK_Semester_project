using System.Collections;
using UnityEngine;

public class MagicCircleController : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("Circle Renderers")]
    [SerializeField] private Renderer outerRing;
    [SerializeField] private Renderer midRing;
    [SerializeField] private Renderer centerRing;

    [Header("Circle Scale")]
    [SerializeField] private Transform circleRoot;

    [Header("Core / Beam")]
    [SerializeField] private GameObject coreGlow;
    [SerializeField] private GameObject beamRoot;

    [Header("Beam Renderers")]
    [SerializeField] private Renderer beamCoreRenderer;
    [SerializeField] private Renderer beamGlowRenderer;

    [Header("Particles")]
    [SerializeField] private ParticleSystem risingStreaks;
    [SerializeField] private ParticleSystem sparks;
    [SerializeField] private ParticleSystem trail01;
    [SerializeField] private ParticleSystem trail02;
    [SerializeField] private ParticleSystem trail03;
    [SerializeField] private ParticleSystem shockwave;

    // =========================================================
    // Circle Scale Settings
    // =========================================================

    [Header("Circle Scale Settings")]

    // ���� ũ��
    [SerializeField] private float circleStartScale = 0.03f;

    // ���� ũ��
    [SerializeField] private float circleFinalScale = 1.00f;

    // ���� �� ����ũ����� �ɸ��� �ð�
    [SerializeField] private float circleScaleInTime = 0.24f;

    // ������ �� Alpha�� ���� Fade
    [SerializeField] private float circleFadeInTime = 0.18f;

    // =========================================================
    // Timing
    // =========================================================

    [Header("Sequence Timing")]
    [SerializeField] private float startDelay = 0.5f;
    [SerializeField] private float activeTime = 4f;
    [SerializeField] private float circleFadeOutTime = 0.6f;

    [Header("Beam Timing")]
    [SerializeField] private float beamFadeInTime = 0.25f;
    [SerializeField] private float beamFadeOutTime = 0.35f;

    // =========================================================
    // Beam Alpha
    // =========================================================

    [Header("Beam Alpha")]
    [SerializeField] private float beamCoreAlpha = 1f;
    [SerializeField] private float beamGlowAlpha = 0.2f;

    // =========================================================
    // Runtime Materials
    // =========================================================

    private Material outerMaterial;
    private Material midMaterial;
    private Material centerMaterial;

    private Material beamCoreMaterial;
    private Material beamGlowMaterial;

    private static readonly int AlphaID =
        Shader.PropertyToID("_Alpha");

    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        if (outerRing != null)
            outerMaterial = outerRing.material;

        if (midRing != null)
            midMaterial = midRing.material;

        if (centerRing != null)
            centerMaterial = centerRing.material;

        if (beamCoreRenderer != null)
            beamCoreMaterial = beamCoreRenderer.material;

        if (beamGlowRenderer != null)
            beamGlowMaterial = beamGlowRenderer.material;
    }

    private void Start()
    {
        StartCoroutine(PlaySequence());
    }

    // =========================================================
    // Main Sequence
    // =========================================================

    private IEnumerator PlaySequence()
    {
        // -----------------------------------------------------
        // 0. �ʱ�ȭ
        // -----------------------------------------------------

        SetCircleAlpha(0f);
        SetCircleScale(circleStartScale);
        SetBeamAlpha(0f);

        if (coreGlow != null)
            coreGlow.SetActive(false);

        if (beamRoot != null)
            beamRoot.SetActive(false);

        PrepareParticle(risingStreaks);
        PrepareParticle(sparks);
        PrepareParticle(trail01);
        PrepareParticle(trail02);
        PrepareParticle(trail03);
        PrepareParticle(shockwave);

        // ó�� ������ �� ���̴� �ð�
        yield return new WaitForSeconds(startDelay);

        // -----------------------------------------------------
        // 1. ������ �ڿ������� Ȯ�� + Fade In
        // -----------------------------------------------------

        PlayParticle(shockwave, true);

        yield return new WaitForSeconds(0.12f);

        yield return SmoothCircleAppear();

        // -----------------------------------------------------
        // 2. Core Glow
        // -----------------------------------------------------

        if (coreGlow != null)
            coreGlow.SetActive(true);

        yield return new WaitForSeconds(0.10f);

        // -----------------------------------------------------
        // 4. Beam
        // -----------------------------------------------------

        if (beamRoot != null)
            beamRoot.SetActive(true);

        SetBeamAlpha(0f);

        yield return FadeBeam(
            0f,
            1f,
            beamFadeInTime
        );

        // -----------------------------------------------------
        // 5. Rising Streaks
        // -----------------------------------------------------

        PlayParticle(risingStreaks);

        yield return new WaitForSeconds(0.05f);

        // -----------------------------------------------------
        // 6. Sparks
        // -----------------------------------------------------

        PlayParticle(sparks);

        yield return new WaitForSeconds(0.05f);

        // -----------------------------------------------------
        // 7. Trails
        // -----------------------------------------------------

        PlayParticle(trail01);
        PlayParticle(trail02);
        PlayParticle(trail03);

        // -----------------------------------------------------
        // 8. ����
        // -----------------------------------------------------

        yield return new WaitForSeconds(activeTime);

        // -----------------------------------------------------
        // 9. ����
        // -----------------------------------------------------

        StopParticle(trail01);
        StopParticle(trail02);
        StopParticle(trail03);

        yield return new WaitForSeconds(0.10f);

        StopParticle(sparks);

        yield return new WaitForSeconds(0.10f);

        StopParticle(risingStreaks);

        yield return new WaitForSeconds(0.10f);

        // Beam Fade Out
        yield return FadeBeam(
            1f,
            0f,
            beamFadeOutTime
        );

        if (beamRoot != null)
            beamRoot.SetActive(false);

        // Circle Fade Out
        yield return FadeCircle(
            1f,
            0f,
            circleFadeOutTime
        );

        if (coreGlow != null)
            coreGlow.SetActive(false);
    }

    // =========================================================
    // Smooth Circle Appear
    // =========================================================

    private IEnumerator SmoothCircleAppear()
    {
        if (circleRoot == null)
        {
            SetCircleAlpha(1f);
            yield break;
        }

        SetCircleScale(circleStartScale);
        SetCircleAlpha(0f);

        float elapsed = 0f;

        // Scale ���� �ð��� ���
        while (elapsed < circleScaleInTime)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / circleScaleInTime
            );

            // -----------------------------------------
            // SmootherStep
            //
            // ���� : ����
            // �߰� : �ڿ������� ������
            // ��   : �ٽ� õõ�� ����
            // -----------------------------------------

            float smoothT =
                t * t * t *
                (t * (t * 6f - 15f) + 10f);

            float scale = Mathf.Lerp(
                circleStartScale,
                circleFinalScale,
                smoothT
            );

            SetCircleScale(scale);

            // -----------------------------------------
            // Alpha�� Scale���� �ణ ���� ��Ÿ������
            // -----------------------------------------

            float alphaT = Mathf.Clamp01(
                elapsed / circleFadeInTime
            );

            float smoothAlpha =
                alphaT * alphaT *
                (3f - 2f * alphaT);

            SetCircleAlpha(smoothAlpha);

            yield return null;
        }

        SetCircleScale(circleFinalScale);
        SetCircleAlpha(1f);
    }

    // =========================================================
    // Circle Scale
    // =========================================================

    private void SetCircleScale(float scale)
    {
        if (circleRoot == null)
            return;

        circleRoot.localScale =
            new Vector3(
                scale,
                scale,
                scale
            );
    }

    // =========================================================
    // Circle Fade
    // =========================================================

    private IEnumerator FadeCircle(
        float from,
        float to,
        float duration
    )
    {
        if (duration <= 0f)
        {
            SetCircleAlpha(to);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / duration
            );

            float smoothT =
                t * t *
                (3f - 2f * t);

            float alpha = Mathf.Lerp(
                from,
                to,
                smoothT
            );

            SetCircleAlpha(alpha);

            yield return null;
        }

        SetCircleAlpha(to);
    }

    private void SetCircleAlpha(float alpha)
    {
        if (outerMaterial != null)
            outerMaterial.SetFloat(
                AlphaID,
                alpha
            );

        if (midMaterial != null)
            midMaterial.SetFloat(
                AlphaID,
                alpha
            );

        if (centerMaterial != null)
            centerMaterial.SetFloat(
                AlphaID,
                alpha
            );
    }

    // =========================================================
    // Beam Fade
    // =========================================================

    private IEnumerator FadeBeam(
        float from,
        float to,
        float duration
    )
    {
        if (duration <= 0f)
        {
            SetBeamAlpha(to);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / duration
            );

            float smoothT =
                t * t *
                (3f - 2f * t);

            float alpha = Mathf.Lerp(
                from,
                to,
                smoothT
            );

            SetBeamAlpha(alpha);

            yield return null;
        }

        SetBeamAlpha(to);
    }

    private void SetBeamAlpha(float factor)
    {
        factor = Mathf.Clamp01(factor);

        if (beamCoreMaterial != null)
        {
            beamCoreMaterial.SetFloat(
                AlphaID,
                beamCoreAlpha * factor
            );
        }

        if (beamGlowMaterial != null)
        {
            beamGlowMaterial.SetFloat(
                AlphaID,
                beamGlowAlpha * factor
            );
        }
    }

    // =========================================================
    // Particle Helpers
    // =========================================================

    private void PrepareParticle(
        ParticleSystem particle
    )
    {
        if (particle == null)
            return;

        if (!particle.gameObject.activeSelf)
            particle.gameObject.SetActive(true);

        particle.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        particle.Clear(true);
    }

    private void PlayParticle(
        ParticleSystem particle,
        bool clearBeforePlay = false
    )
    {
        if (particle == null)
            return;

        if (!particle.gameObject.activeSelf)
            particle.gameObject.SetActive(true);

        if (clearBeforePlay)
        {
            particle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );

            particle.Clear(true);
        }

        particle.Play(true);
    }

    private void StopParticle(
        ParticleSystem particle
    )
    {
        if (particle == null)
            return;

        particle.Stop(
            true,
            ParticleSystemStopBehavior.StopEmitting
        );
    }
}