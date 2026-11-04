#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class EffectLightPulsePlayModeTests
    {
        private const string PrefabPath = "Assets/CK_Semester_Project/Prototype/Prefabs/BattleVfxTest/FuchsiaSkill01SakuraSlash.prefab";

        [UnityTest]
        public IEnumerator InstantiatedEffectAutomaticallyPulsesEndsAndReplaysAfterPoolingWithoutChangingSource()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Type pulseType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("EffectLightPulse"))
                .First(type => type != null);
            Component sourcePulse = prefab.GetComponentsInChildren(pulseType, true).Single();
            Light sourceLight = sourcePulse.GetComponent<Light>();
            Assert.That(sourceLight.intensity, Is.EqualTo(3f));
            string sourceLightJson = EditorJsonUtility.ToJson(sourceLight);
            string sourcePulseJson = EditorJsonUtility.ToJson(sourcePulse);
            float previousTimeScale = Time.timeScale;
            float previousCaptureDeltaTime = Time.captureDeltaTime;
            GameObject instance = null;
            try
            {
                // A slow editor frame must not skip the whole short pulse before it is observed.
                Time.timeScale = 1f;
                Time.captureDeltaTime = 1f / 60f;
                instance = Object.Instantiate(prefab);
                Component pulse = instance.GetComponentsInChildren(pulseType, true).Single();
                Light light = pulse.GetComponent<Light>();
                float duration = (float)pulseType.GetProperty("Duration").GetValue(pulse);
                Assert.That(pulse.transform, Is.Not.EqualTo(instance.transform),
                    "The effect should carry its light as an authored child.");
                Assert.That(pulse.gameObject.activeInHierarchy, Is.True);
                Assert.That(((Behaviour)pulse).enabled, Is.True);
                Assert.That(light.enabled, Is.True);
                Assert.That(instance.GetComponentsInChildren<MonoBehaviour>(true)
                    .Any(component => component.GetType().Name == "BattleVfxSceneTemplate"), Is.False);
                Assert.That(IsPlaying(pulse), Is.True, "OnEnable must start a normal runtime prefab instance.");
                Assert.That(light.intensity, Is.Zero);

                yield return new WaitForSeconds(.08f);

                Assert.That(light.intensity, Is.GreaterThan(0f), "Update must animate the light without editor preview code.");
                Assert.That(IsPlaying(pulse), Is.True);

                yield return new WaitForSeconds(duration + .08f);

                Assert.That(light.intensity, Is.Zero);
                Assert.That(IsPlaying(pulse), Is.False);

                instance.SetActive(false);
                Assert.That(light.intensity, Is.EqualTo(3f), "Returning to a pool must restore native authored intensity.");
                instance.SetActive(true);
                Assert.That(light.intensity, Is.Zero);
                Assert.That(IsPlaying(pulse), Is.True, "Reusing the pooled object must start another pulse.");

                yield return new WaitForSeconds(.08f);

                Assert.That(light.intensity, Is.GreaterThan(0f),
                    "Replaying must retain the original brightness instead of capturing the completed zero value.");

                yield return new WaitForSeconds(duration + .08f);

                Assert.That(light.intensity, Is.Zero);
                Assert.That(IsPlaying(pulse), Is.False);
                Assert.That(sourceLight.intensity, Is.EqualTo(3f));
                Assert.That(EditorJsonUtility.ToJson(sourceLight), Is.EqualTo(sourceLightJson));
                Assert.That(EditorJsonUtility.ToJson(sourcePulse), Is.EqualTo(sourcePulseJson));
            }
            finally
            {
                if (instance != null)
                {
                    Object.DestroyImmediate(instance);
                }
                Time.captureDeltaTime = previousCaptureDeltaTime;
                Time.timeScale = previousTimeScale;
            }
        }

        private static bool IsPlaying(Component pulse)
        {
            return (bool)pulse.GetType().GetProperty("IsPlaying").GetValue(pulse);
        }
    }
}
#endif
