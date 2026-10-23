#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class EffectLightPreviewTests
    {
        private const string PreviewTypeName = "CK.SemesterProject.EditorTools.BattleVfxSavedEffectPreview";
        private const string EntryTypeName = "CK.SemesterProject.EditorTools.BattleVfxSavedEffectLibrary+Entry";
        private const string PlacementTypeName = "CK.SemesterProject.Tutorial.BattleVfxEffectPlacement";

        [Test]
        public void PulseFollowsSavedDelayEffectSpeedScrubbingAndPreviewLoop()
        {
            using (var fixture = new LightFixture())
            using (var preview = new PreviewHandle())
            {
                const float delay = .25f;
                preview.Play(fixture.CreateEntry(delay, 1f, 1.5f, 2f), fixture.Scene);
                Light light = preview.Instance.transform.Find("Pulsed Light").GetComponent<Light>();

                preview.Sample(delay - .05f);
                Assert.That(light.enabled, Is.False);
                Assert.That(light.intensity, Is.Zero);

                preview.Sample(delay + .2f);
                Assert.That(light.enabled, Is.True);
                Assert.That(light.intensity, Is.EqualTo(4.8f).Within(.001f));
                preview.Sample(delay + .6f);
                Assert.That(light.intensity, Is.EqualTo(2.4f).Within(.001f));
                preview.Sample(delay + .2f);
                Assert.That(light.intensity, Is.EqualTo(4.8f).Within(.001f),
                    "Backward scrubbing must sample the original intensity, not the previous animated value.");

                preview.Sample(preview.Duration);
                Assert.That(light.enabled, Is.False);
                Assert.That(light.intensity, Is.Zero);
                preview.Sample(0f);
                preview.Advance(preview.Duration + delay + .2f);
                Assert.That(preview.Time, Is.EqualTo(delay + .2f).Within(.001f));
                Assert.That(light.enabled, Is.True);
                Assert.That(light.intensity, Is.EqualTo(4.8f).Within(.001f));

                preview.Sample(0f);
                preview.Set("Speed", 2f);
                preview.Advance((delay + .2f) / 2f);
                Assert.That(preview.Time, Is.EqualTo(delay + .2f).Within(.001f));
                Assert.That(light.intensity, Is.EqualTo(4.8f).Within(.001f));
                Assert.That(fixture.Prefab.transform.Find("Pulsed Light").GetComponent<Light>().intensity,
                    Is.EqualTo(6f), "Preview must not animate the source asset.");
            }
        }

        [Test]
        public void PulseDurationExtendsPreviewAndUsesCombinedSavedPlaybackSpeed()
        {
            using (var fixture = new LightFixture())
            using (var preview = new PreviewHandle())
            {
                preview.Play(fixture.CreateEntry(.25f, 0f, 2f, 3f), fixture.Scene);
                Assert.That(preview.Duration, Is.EqualTo(.9f).Within(.001f),
                    "A 3 second light pulse at combined speed 6 needs .5 seconds plus delay and loop gap.");
                Light light = preview.Instance.transform.Find("Pulsed Light").GetComponent<Light>();
                preview.Sample(.65f);
                Assert.That(light.enabled, Is.True);
                Assert.That(light.intensity, Is.EqualTo(1.2f).Within(.001f));
                preview.Sample(.76f);
                Assert.That(light.enabled, Is.False);
                Assert.That(light.intensity, Is.Zero);
            }
        }

        [Test]
        public void AllLightsEndWithEffectAndAuthoredDisabledStatesAndSceneLightingRemainUnchanged()
        {
            using (var fixture = new LightFixture(true))
            using (var preview = new PreviewHandle())
            {
                preview.Play(fixture.CreateEntry(.2f, .8f), fixture.Scene);
                GameObject instance = preview.Instance;
                Light staticLight = instance.transform.Find("Static Light").GetComponent<Light>();
                Light disabledLight = instance.transform.Find("Disabled Light").GetComponent<Light>();
                Light inactiveLight = instance.transform.Find("Inactive Light").GetComponent<Light>();
                Light disabledPulse = instance.transform.Find("Disabled Pulse").GetComponent<Light>();

                preview.Sample(.1f);
                Assert.That(staticLight.enabled, Is.False);
                Assert.That(disabledLight.enabled, Is.False);
                Assert.That(inactiveLight.gameObject.activeSelf, Is.False);
                preview.Sample(.4f);
                Assert.That(staticLight.enabled, Is.True);
                Assert.That(staticLight.intensity, Is.EqualTo(4f));
                Assert.That(disabledLight.enabled, Is.False,
                    "Sampling a pulse must not turn on a Light that was authored disabled.");
                Assert.That(inactiveLight.gameObject.activeSelf, Is.False);
                Assert.That(inactiveLight.isActiveAndEnabled, Is.False);
                Assert.That(disabledPulse.intensity, Is.EqualTo(5f),
                    "An authored disabled pulse must not animate a static Light.");
                Assert.That(staticLight.color, Is.EqualTo(Color.magenta));
                Assert.That(staticLight.range, Is.EqualTo(7f));

                preview.Sample(preview.Duration);
                Assert.That(instance.GetComponentsInChildren<Light>(true).All(item => !item.enabled), Is.True,
                    "Plain child Lights must not keep illuminating during the loop gap.");
                preview.Sample(.4f);
                Assert.That(staticLight.enabled, Is.True);
                Assert.That(disabledLight.enabled, Is.False);

                Light sourceStatic = fixture.Prefab.transform.Find("Static Light").GetComponent<Light>();
                Assert.That(sourceStatic.enabled, Is.True);
                Assert.That(sourceStatic.intensity, Is.EqualTo(4f));
                Assert.That(sourceStatic.color, Is.EqualTo(Color.magenta));
                Assert.That(sourceStatic.range, Is.EqualTo(7f));
                Assert.That(fixture.Prefab.transform.Find("Disabled Light").GetComponent<Light>().enabled,
                    Is.False);
                Assert.That(fixture.Prefab.transform.Find("Inactive Light").gameObject.activeSelf, Is.False);
                Assert.That(fixture.SceneLight.enabled, Is.True);
                Assert.That(fixture.SceneLight.intensity, Is.EqualTo(2f));
                Assert.That(fixture.SceneLight.color, Is.EqualTo(Color.blue));

                preview.Stop();
                Assert.That(instance == null, Is.True);
                Assert.That(preview.Instance, Is.Null);
                Assert.That(fixture.Scene.GetRootGameObjects().Count(item => item.name == "Saved VFX Preview"),
                    Is.Zero);
                Assert.That(fixture.SceneLight.enabled, Is.True);
                Assert.That(fixture.SceneLight.intensity, Is.EqualTo(2f));
            }
        }

        private static Type FindType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name))
                .First(type => type != null);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private sealed class PreviewHandle : IDisposable
        {
            private readonly object _preview = Activator.CreateInstance(FindType(PreviewTypeName));

            public GameObject Instance => Get<GameObject>("Instance");
            public float Time => Get<float>("Time");
            public float Duration => Get<float>("Duration");

            public void Play(object entry, Scene scene)
            {
                Invoke("Play", entry, scene);
            }

            public void Sample(float time)
            {
                Invoke("Sample", time);
            }

            public void Advance(float time)
            {
                Invoke("Advance", time);
            }

            public void Stop()
            {
                Invoke("Stop");
            }

            public void Set(string name, object value)
            {
                _preview.GetType().GetProperty(name).SetValue(_preview, value);
            }

            public void Dispose()
            {
                ((IDisposable)_preview).Dispose();
            }

            private T Get<T>(string name)
            {
                return (T)_preview.GetType().GetProperty(name).GetValue(_preview);
            }

            private void Invoke(string name, params object[] arguments)
            {
                _preview.GetType().GetMethod(name).Invoke(_preview, arguments);
            }
        }

        private sealed class LightFixture : IDisposable
        {
            private readonly string _prefabPath = "Assets/CK_Semester_Project/Tests/EditMode/Effects/"
                + "EffectLightPreview_" + Guid.NewGuid().ToString("N") + ".prefab";

            public Scene Scene { get; }
            public GameObject Prefab { get; }
            public Light SceneLight { get; }

            public LightFixture(bool includeAuthoredStates = false)
            {
                Scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    CreateRoot("VFX Training Dummy");
                    SceneLight = CreateRoot("Unrelated Scene Light").AddComponent<Light>();
                    SceneLight.intensity = 2f;
                    SceneLight.color = Color.blue;
                    GameObject root = CreateRoot("Light Preview Source");
                    root.SetActive(false);
                    AddLight(root, "Pulsed Light", 6f, true);
                    if (includeAuthoredStates)
                    {
                        Light staticLight = AddLight(root, "Static Light", 4f, false);
                        staticLight.color = Color.magenta;
                        staticLight.range = 7f;
                        AddLight(root, "Disabled Light", 8f, true).enabled = false;
                        AddLight(root, "Inactive Light", 9f, true).gameObject.SetActive(false);
                        Light disabledPulse = AddLight(root, "Disabled Pulse", 5f, true);
                        ((Behaviour)disabledPulse.GetComponent(FindType("EffectLightPulse"))).enabled = false;
                    }
                    root.SetActive(true);
                    Prefab = PrefabUtility.SaveAsPrefabAsset(root, _prefabPath);
                    Assert.That(Prefab, Is.Not.Null);
                    Object.DestroyImmediate(root);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public object CreateEntry(float delay = 0f, float lifetime = 0f, float speed = 1f,
                float baseSpeed = 1f)
            {
                object placement = Activator.CreateInstance(FindType(PlacementTypeName));
                SetField(placement, "Scale", 1f);
                SetField(placement, "StartDelay", delay);
                SetField(placement, "Lifetime", lifetime);
                SetField(placement, "PlaybackSpeed", speed);
                object entry = Activator.CreateInstance(FindType(EntryTypeName));
                SetField(entry, "Prefab", Prefab);
                SetField(entry, "Placement", placement);
                SetField(entry, "BasePlaybackSpeed", baseSpeed);
                return entry;
            }

            private GameObject CreateRoot(string name)
            {
                var root = new GameObject(name);
                SceneManager.MoveGameObjectToScene(root, Scene);
                return root;
            }

            private static Light AddLight(GameObject parent, string name, float intensity, bool pulse)
            {
                var child = new GameObject(name);
                child.transform.SetParent(parent.transform, false);
                Light light = child.AddComponent<Light>();
                light.type = LightType.Point;
                light.intensity = intensity;
                if (pulse)
                {
                    Component component = child.AddComponent(FindType("EffectLightPulse"));
                    SetField(component, "_duration", 3f);
                    SetField(component, "_intensityCurve", AnimationCurve.Linear(0f, 1f, 1f, 0f));
                }
                return light;
            }

            public void Dispose()
            {
                if (Scene.IsValid() && Scene.isLoaded)
                {
                    EditorSceneManager.ClosePreviewScene(Scene);
                }
                AssetDatabase.DeleteAsset(_prefabPath);
            }
        }
    }
}
#endif
