using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace CK.SemesterProject.Tutorial
{
    // The imported effect uses coroutines. This adapter samples the same sequence
    // from clip time so Control Tracks can pause, replay and scrub backwards.
    public sealed class CherryBlossomTimelineControl : MonoBehaviour, ITimeControl, IPropertyPreview
    {
        [SerializeField] private Renderer outerRing, midRing, centerRing;
        [SerializeField] private Transform circleRoot;
        [SerializeField] private GameObject coreGlow, beamRoot;
        [SerializeField] private Renderer beamCoreRenderer, beamGlowRenderer;
        [SerializeField] private ParticleSystem risingStreaks, sparks, trail01, trail02, trail03, shockwave;
        [SerializeField] private float circleStartScale = .1f, circleFinalScale = 1f;
        [SerializeField] private float circleScaleInTime = .24f, circleFadeInTime = .18f;
        [SerializeField] private float startDelay = .5f, activeTime = 4f, circleFadeOutTime = .6f;
        [SerializeField] private float beamFadeInTime = .25f, beamFadeOutTime = .35f;
        [SerializeField] private float beamCoreAlpha = 1f, beamGlowAlpha = .2f;
        [SerializeField] private Transform outerPivot, midPivot, centerPivot;
        [SerializeField] private float outerSpeed = -7f, midSpeed = 11f, centerSpeed = 1.5f;
        [System.NonSerialized] private Quaternion _outerRotation, _midRotation, _centerRotation;
        [System.NonSerialized] private ParticleSystem[] _particles;
        [System.NonSerialized] private MaterialPropertyBlock _properties;
        private static readonly int Alpha = Shader.PropertyToID("_Alpha");

        private void Initialize()
        {
            if (_particles != null && _properties != null) return;
            _particles = GetComponentsInChildren<ParticleSystem>(true);
            _properties = new MaterialPropertyBlock();
            _outerRotation = InitialRotation(outerPivot);
            _midRotation = InitialRotation(midPivot);
            _centerRotation = InitialRotation(centerPivot);
        }

        private static Quaternion InitialRotation(Transform pivot)
        {
            if (pivot == null) return Quaternion.identity;
#if UNITY_EDITOR
            var original = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(pivot);
            if (original != null) return original.localRotation;
#endif
            return pivot.localRotation;
        }

        public void OnControlTimeStart()
        {
            Initialize();
            for (int i = 0; i < _particles.Length; i++)
            {
                _particles[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                _particles[i].useAutoRandomSeed = false;
                _particles[i].randomSeed = (uint)(1701 + i);
            }
        }

        public void GatherProperties(PlayableDirector director, IPropertyCollector driver)
        {
            foreach (var node in GetComponentsInChildren<Transform>(true))
            {
                driver.AddFromComponent(node.gameObject, node);
                driver.AddFromName(node.gameObject, "m_IsActive");
            }
            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
            {
                driver.AddFromName<ParticleSystem>(ps.gameObject, "randomSeed");
                driver.AddFromName<ParticleSystem>(ps.gameObject, "autoRandomSeed");
            }
        }
        // Pausing also calls this callback; preserve the sampled frame.
        // ControlPlayableAsset handles visibility when the clip ends.
        public void OnControlTimeStop() { }

        public void SetTime(double time)
        {
            Initialize();
            float t = Mathf.Max(0f, (float)time);
            float appear = startDelay + .12f;
            float coreStart = appear + circleScaleInTime;
            float beamStart = coreStart + .1f;
            float streakStart = beamStart + beamFadeInTime;
            float sparksStart = streakStart + .05f;
            float trailsStart = sparksStart + .05f;
            float trailsEnd = trailsStart + activeTime;
            float beamEnd = trailsEnd + .3f;
            float circleEnd = beamEnd + beamFadeOutTime;
            float alpha = Smooth((t - appear) / Mathf.Max(.001f, circleFadeInTime))
                * (1f - Smooth((t - circleEnd) / Mathf.Max(.001f, circleFadeOutTime)));
            float scaleT = Mathf.Clamp01((t - appear) / Mathf.Max(.001f, circleScaleInTime));
            scaleT = scaleT * scaleT * scaleT * (scaleT * (scaleT * 6f - 15f) + 10f);
            if (circleRoot != null) circleRoot.localScale = Vector3.one * Mathf.Lerp(circleStartScale, circleFinalScale, scaleT);
            SetAlpha(outerRing, alpha); SetAlpha(midRing, alpha); SetAlpha(centerRing, alpha);
            if (coreGlow != null) coreGlow.SetActive(t >= coreStart && t < circleEnd + circleFadeOutTime);
            if (beamRoot != null) beamRoot.SetActive(t >= beamStart && t < circleEnd);
            float beam = Smooth((t - beamStart) / Mathf.Max(.001f, beamFadeInTime))
                * (1f - Smooth((t - beamEnd) / Mathf.Max(.001f, beamFadeOutTime)));
            SetAlpha(beamCoreRenderer, beam * beamCoreAlpha);
            SetAlpha(beamGlowRenderer, beam * beamGlowAlpha);
            Rotate(outerPivot, _outerRotation, outerSpeed, t);
            Rotate(midPivot, _midRotation, midSpeed, t);
            Rotate(centerPivot, _centerRotation, centerSpeed, t);
            foreach (var ps in _particles)
            {
                float begin = 0f, end = float.PositiveInfinity;
                if (ps == shockwave) begin = startDelay;
                else if (ps == risingStreaks) { begin = streakStart; end = trailsEnd + .2f; }
                else if (ps == sparks) { begin = sparksStart; end = trailsEnd + .1f; }
                else if (ps == trail01 || ps == trail02 || ps == trail03) { begin = trailsStart; end = trailsEnd; }
                SampleParticle(ps, t, begin, end);
            }
        }

        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        private void SetAlpha(Renderer target, float value)
        {
            if (target == null) return;
            target.GetPropertyBlock(_properties);
            _properties.SetFloat(Alpha, value);
            target.SetPropertyBlock(_properties);
        }
        private static void Rotate(Transform pivot, Quaternion initial, float speed, float time)
        {
            if (pivot != null) pivot.localRotation = initial * Quaternion.AngleAxis(speed * time, Vector3.up);
        }
        private static void SampleParticle(ParticleSystem ps, float time, float begin, float end)
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (time < begin) return;
            ps.Simulate(Mathf.Min(time, end) - begin, false, true, true);
            if (time > end)
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                ps.Simulate(time - end, false, false, true);
            }
            ps.Pause(false);
        }
    }
}
