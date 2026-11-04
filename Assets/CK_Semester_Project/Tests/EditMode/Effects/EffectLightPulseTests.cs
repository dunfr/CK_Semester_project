#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class EffectLightPulseTests
    {
        [Test]
        public void PlaybackUsesAuthoredIntensityAndEndsWithLightOff()
        {
            using (var fixture = new LightFixture())
            {
                fixture.Play();
                Assert.That(fixture.IsPlaying, Is.True);
                Assert.That(fixture.Light.intensity, Is.Zero);

                fixture.Advance(.5f);
                Assert.That(fixture.Light.intensity, Is.EqualTo(1.5f).Within(.001f));
                Assert.That(fixture.IsPlaying, Is.True);

                fixture.Advance(.5f);
                Assert.That(fixture.Light.intensity, Is.Zero,
                    "The light must turn off at the end even when the curve endpoint is not zero.");
                Assert.That(fixture.IsPlaying, Is.False);

                fixture.Advance(5f);
                Assert.That(fixture.Light.intensity, Is.Zero);
            }
        }

        [Test]
        public void RestartDuringPlaybackKeepsOriginalIntensityInsteadOfCapturingAnimatedValue()
        {
            using (var fixture = new LightFixture())
            {
                fixture.Play();
                fixture.Advance(.25f);
                Assert.That(fixture.Light.intensity, Is.EqualTo(.75f).Within(.001f));

                fixture.Play();
                fixture.Advance(.5f);

                Assert.That(fixture.Light.intensity, Is.EqualTo(1.5f).Within(.001f));
                fixture.Advance(.5f);
                fixture.Play();
                fixture.Advance(.5f);
                Assert.That(fixture.Light.intensity, Is.EqualTo(1.5f).Within(.001f),
                    "Replaying a completed pulse must not capture the previous zero intensity.");
            }
        }

        [Test]
        public void PreviewCanScrubBackwardAndClearRestoresNativeLightSettings()
        {
            using (var fixture = new LightFixture())
            {
                Color color = fixture.Light.color;
                float range = fixture.Light.range;
                bool enabled = fixture.Light.enabled;

                fixture.Preview(.75f);
                Assert.That(fixture.Light.intensity, Is.EqualTo(2.25f).Within(.001f));
                fixture.Preview(.25f);
                Assert.That(fixture.Light.intensity, Is.EqualTo(.75f).Within(.001f));
                fixture.Preview(1f);
                Assert.That(fixture.Light.intensity, Is.Zero);
                fixture.Preview(.5f);
                Assert.That(fixture.Light.intensity, Is.EqualTo(1.5f).Within(.001f));

                fixture.ClearPreview();

                Assert.That(fixture.Light.intensity, Is.EqualTo(3f));
                Assert.That(fixture.Light.color, Is.EqualTo(color));
                Assert.That(fixture.Light.range, Is.EqualTo(range));
                Assert.That(fixture.Light.enabled, Is.EqualTo(enabled));
                Assert.That(fixture.IsPlaying, Is.False);
            }
        }

        [Test]
        public void ClearPreviewAllowsNextPreviewToUseNewAuthoredIntensity()
        {
            using (var fixture = new LightFixture())
            {
                fixture.Preview(.5f);
                fixture.ClearPreview();
                fixture.Light.intensity = 6f;

                fixture.Preview(.5f);

                Assert.That(fixture.Light.intensity, Is.EqualTo(3f).Within(.001f));
                fixture.ClearPreview();
                Assert.That(fixture.Light.intensity, Is.EqualTo(6f));
            }
        }

        [Test]
        public void PoolDisableRestoresAuthoredIntensityBeforeNextPlayback()
        {
            using (var fixture = new LightFixture())
            {
                fixture.Play();
                fixture.Advance(.25f);

                // Normal MonoBehaviour lifecycle callbacks do not run in Edit mode.
                fixture.InvokeLifecycle("OnDisable");

                Assert.That(fixture.Light.intensity, Is.EqualTo(3f));
                Assert.That(fixture.IsPlaying, Is.False);
                fixture.InvokeLifecycle("OnEnable");
                Assert.That(fixture.Light.intensity, Is.EqualTo(3f),
                    "Enabling an authoring object in Edit mode must preserve its Inspector intensity.");
                fixture.Play();
                fixture.Advance(.5f);
                Assert.That(fixture.Light.intensity, Is.EqualTo(1.5f).Within(.001f));
            }
        }

        [Test]
        public void PreviewOnInstanceLeavesAuthoredSourceLightUntouched()
        {
            using (var source = new LightFixture())
            {
                var clone = Object.Instantiate(source.Root);
                try
                {
                    Component pulse = clone.GetComponent(source.Pulse.GetType());
                    pulse.GetType().GetMethod("Preview").Invoke(pulse, new object[] { .5f });
                    Assert.That(clone.GetComponent<Light>().intensity, Is.EqualTo(1.5f).Within(.001f));
                    Assert.That(source.Light.intensity, Is.EqualTo(3f));
                    Assert.That(source.Light.color, Is.EqualTo(new Color(.8f, .1f, .45f, 1f)));
                    Assert.That(source.Light.range, Is.EqualTo(7f));
                    Assert.That(source.IsPlaying, Is.False);
                }
                finally
                {
                    Object.DestroyImmediate(clone);
                }
            }
        }

        [Test]
        public void DisabledLightStaysDisabledAndNegativeAdvanceDoesNotRewindPlayback()
        {
            using (var fixture = new LightFixture())
            {
                fixture.Light.enabled = false;
                fixture.Play();
                fixture.Advance(.5f);
                fixture.Advance(-1f);

                Assert.That(fixture.Light.intensity, Is.EqualTo(1.5f).Within(.001f));
                Assert.That(fixture.Light.enabled, Is.False);
                fixture.ClearPreview();
                Assert.That(fixture.Light.enabled, Is.False);
                Assert.That(fixture.Light.intensity, Is.EqualTo(3f));
            }
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void NonPositiveDurationStillCompletesWithoutInvalidIntensity(float duration)
        {
            using (var fixture = new LightFixture())
            {
                fixture.SetDuration(duration);
                Assert.That(fixture.Duration, Is.EqualTo(.01f));

                fixture.Play();
                fixture.Advance(.01f);

                Assert.That(fixture.Light.intensity, Is.Zero);
                Assert.That(fixture.IsPlaying, Is.False);
            }
        }

        private sealed class LightFixture : IDisposable
        {
            public GameObject Root { get; }
            public Light Light { get; }
            public Component Pulse { get; }
            public bool IsPlaying => (bool)Pulse.GetType().GetProperty("IsPlaying").GetValue(Pulse);
            public float Duration => (float)Pulse.GetType().GetProperty("Duration").GetValue(Pulse);

            public LightFixture()
            {
                Root = new GameObject("Effect Light Pulse Test");
                Root.SetActive(false);
                Light = Root.AddComponent<Light>();
                Light.intensity = 3f;
                Light.color = new Color(.8f, .1f, .45f, 1f);
                Light.range = 7f;
                Type pulseType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("EffectLightPulse"))
                    .First(type => type != null);
                Pulse = Root.AddComponent(pulseType);
                var serialized = new SerializedObject(Pulse);
                serialized.FindProperty("_duration").floatValue = 1f;
                serialized.FindProperty("_intensityCurve").animationCurveValue = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            public void SetDuration(float duration)
            {
                var serialized = new SerializedObject(Pulse);
                serialized.FindProperty("_duration").floatValue = duration;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            public void Play()
            {
                Invoke("Play");
            }

            public void Advance(float seconds)
            {
                Invoke("Advance", seconds);
            }

            public void Preview(float normalizedTime)
            {
                Invoke("Preview", normalizedTime);
            }

            public void ClearPreview()
            {
                Invoke("ClearPreview");
            }

            public void InvokeLifecycle(string method)
            {
                Pulse.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(Pulse, null);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Root);
            }

            private void Invoke(string method, params object[] arguments)
            {
                Pulse.GetType().GetMethod(method).Invoke(Pulse, arguments);
            }
        }
    }
}
#endif
