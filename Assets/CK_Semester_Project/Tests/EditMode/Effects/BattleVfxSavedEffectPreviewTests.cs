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
    public sealed class BattleVfxSavedEffectPreviewTests
    {
        private const string PreviewTypeName = "CK.SemesterProject.EditorTools.BattleVfxSavedEffectPreview";
        private const string EntryTypeName = "CK.SemesterProject.EditorTools.BattleVfxSavedEffectLibrary+Entry";
        private const string PlacementTypeName = "CK.SemesterProject.Tutorial.BattleVfxEffectPlacement";
        private const string TemplateTypeName = "CK.SemesterProject.Tutorial.BattleVfxSceneTemplate";
        private const string SlashTypeName = "CK.SemesterProject.Tutorial.TutorialSlashVfxPlacement";
        private const string ChargeTypeName = "CK.SemesterProject.Tutorial.TutorialChargeVfxPlacement";

        [Test]
        public void PreviewUsesDummyRelativePoseWithoutChangingAuthoredChildrenOrSourcePrefab()
        {
            using (var fixture = new PreviewFixture())
            using (var preview = new PreviewHandle())
            {
                GameObject prefab = fixture.Prefab;
                Transform sourceChild = prefab.transform.Find("Authored Child");
                Vector3 childPosition = sourceChild.localPosition;
                Quaternion childRotation = sourceChild.localRotation;
                Vector3 childScale = sourceChild.localScale;
                Vector3 prefabScale = prefab.transform.localScale;
                object entry = fixture.CreateEntry();

                preview.Play(entry, fixture.Scene);
                preview.Sample(.1f);

                GameObject instance = preview.Instance;
                Assert.That(instance, Is.Not.Null);
                Assert.That(instance.scene, Is.EqualTo(fixture.Scene));
                Assert.That(instance.transform.parent, Is.Null, "Monster scale must not multiply the saved scale.");
                AssertVector(instance.transform.position,
                    fixture.Dummy.position + fixture.Dummy.rotation * fixture.Offset);
                AssertRotation(instance.transform.rotation,
                    fixture.Dummy.rotation * Quaternion.Euler(fixture.Rotation));
                AssertVector(instance.transform.lossyScale, prefabScale * 1.7f);
                Transform cloneChild = instance.transform.Find("Authored Child");
                AssertVector(cloneChild.localPosition, childPosition);
                AssertRotation(cloneChild.localRotation, childRotation);
                AssertVector(cloneChild.localScale, childScale);
                Assert.That(cloneChild.GetComponent<BoxCollider>().enabled, Is.False);
                Assert.That(instance.GetComponentsInChildren(FindType(TemplateTypeName), true), Is.Empty);
                foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    Assert.That(behaviour.enabled, Is.False, behaviour.GetType().Name);
                }
                foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                {
                    Assert.That(child.gameObject.hideFlags & HideFlags.DontSaveInEditor, Is.Not.EqualTo(HideFlags.None));
                    Assert.That(child.gameObject.hideFlags & HideFlags.DontSaveInBuild, Is.Not.EqualTo(HideFlags.None));
                }

                fixture.Dummy.SetPositionAndRotation(new Vector3(-8f, 4f, 7f), Quaternion.Euler(9f, 215f, -6f));
                preview.Sample(.2f);
                AssertVector(instance.transform.position,
                    fixture.Dummy.position + fixture.Dummy.rotation * fixture.Offset);
                AssertRotation(instance.transform.rotation,
                    fixture.Dummy.rotation * Quaternion.Euler(fixture.Rotation));
                AssertVector(instance.transform.lossyScale, prefabScale * 1.7f);

                AssertVector(prefab.transform.localScale, prefabScale);
                AssertVector(sourceChild.localPosition, childPosition);
                AssertRotation(sourceChild.localRotation, childRotation);
                AssertVector(sourceChild.localScale, childScale);
                Assert.That(sourceChild.GetComponent<BoxCollider>().enabled, Is.True);
                Assert.That(((Behaviour)prefab.GetComponent(FindType(SlashTypeName))).enabled, Is.True);
                Assert.That(((Behaviour)sourceChild.GetComponent(FindType(ChargeTypeName))).enabled, Is.True);
                Assert.That(prefab.GetComponentsInChildren(FindType(TemplateTypeName), true).Length, Is.EqualTo(1));
                Assert.That(prefab.transform.Find("Particles").GetComponent<ParticleSystem>().main.simulationSpeed,
                    Is.EqualTo(.65f));
                Assert.That(GetField<object>(entry, "Placement").GetType().GetField("Follow"), Is.Null);
            }
        }

        [Test]
        public void DelaySpeedBackwardScrubAndLoopKeepParticlesAndMeshTimelinesTogether()
        {
            using (var fixture = new PreviewFixture(true))
            using (var preview = new PreviewHandle())
            {
                const float delay = .25f;
                const float elapsed = .1f;
                const float effectSpeed = 3f;
                preview.Play(fixture.CreateEntry(delay, 1f, 1.5f, 2f), fixture.Scene);
                GameObject instance = preview.Instance;
                Renderer fade = instance.transform.Find("Fade").GetComponent<Renderer>();
                Transform streak = instance.transform.Find("Streak");

                preview.Sample(delay - .05f);
                Assert.That(preview.ParticleCount, Is.Zero);
                Assert.That(fade.enabled, Is.False);
                Assert.That(streak.GetComponent<Renderer>().enabled, Is.False);

                preview.Sample(delay + elapsed);
                ParticleSystem particles = instance.transform.Find("Particles").GetComponent<ParticleSystem>();
                ParticleSystem.Particle[] firstParticles = ReadParticles(particles);
                Assert.That(firstParticles.Length, Is.GreaterThan(0));
                Assert.That(fade.enabled, Is.True);
                Assert.That(ReadAlpha(fade), Is.EqualTo(1f - elapsed * effectSpeed).Within(.001f));
                Assert.That(streak.localScale.x, Is.EqualTo(2f * (.5f + elapsed * effectSpeed)).Within(.001f));
                AssertVector(new Vector3(streak.localScale.y, streak.localScale.z, 0f), new Vector3(3f, 4f, 0f));
                Assert.That(particles.main.simulationSpeed, Is.EqualTo(.65f * effectSpeed).Within(.001f));

                preview.Sample(delay + .3f);
                Assert.That(ReadAlpha(fade), Is.EqualTo(.1f).Within(.001f));
                Assert.That(preview.ParticleCount, Is.GreaterThan(firstParticles.Length));
                preview.Sample(delay + elapsed);
                AssertParticles(ReadParticles(particles), firstParticles);
                Assert.That(ReadAlpha(fade), Is.EqualTo(.7f).Within(.001f));
                Assert.That(streak.localScale.x, Is.EqualTo(1.6f).Within(.001f));

                preview.Sample(preview.Duration);
                Assert.That(preview.ParticleCount, Is.Zero);
                Assert.That(fade.enabled, Is.False);
                preview.Sample(0f);
                preview.Set("Loop", true);
                preview.Advance(preview.Duration + delay + elapsed);
                Assert.That(preview.Time, Is.EqualTo(delay + elapsed).Within(.001f));
                AssertParticles(ReadParticles(particles), firstParticles);
                Assert.That(ReadAlpha(fade), Is.EqualTo(.7f).Within(.001f));
                Assert.That(streak.localScale.x, Is.EqualTo(1.6f).Within(.001f));
                Assert.That(ReadAlpha(fixture.Prefab.transform.Find("Fade").GetComponent<Renderer>()), Is.Zero,
                    "Scrubbing must not write renderer overrides to the source asset.");
                AssertVector(fixture.Prefab.transform.Find("Streak").localScale, new Vector3(2f, 3f, 4f));
            }
        }

        [Test]
        public void PlaybackSpeedAndNonLoopingEndRespectControlsAndStopDisposesClone()
        {
            using (var fixture = new PreviewFixture())
            using (var preview = new PreviewHandle())
            {
                preview.Play(fixture.CreateEntry(), fixture.Scene);
                GameObject instance = preview.Instance;
                preview.Sample(0f);
                preview.Set("Speed", 2f);
                preview.Advance(.1f);
                Assert.That(preview.Time, Is.EqualTo(.2f).Within(.001f));
                preview.Set("Loop", false);
                preview.Advance(preview.Duration);
                Assert.That(preview.IsPlaying, Is.False);
                Assert.That(preview.Time, Is.EqualTo(preview.Duration));
                Assert.That(preview.ParticleCount, Is.Zero);
                preview.Stop();
                Assert.That(instance == null, Is.True);
                Assert.That(preview.Instance, Is.Null);
                Assert.That(preview.Time, Is.Zero);
                Assert.That(preview.ParticleCount, Is.Zero);

                preview.Play(fixture.CreateEntry(), fixture.Scene);
                instance = preview.Instance;
                preview.Dispose();
                Assert.That(instance == null, Is.True);
                Assert.That(preview.IsPlaying, Is.False);
                Assert.That(preview.Instance, Is.Null);
                Assert.That(fixture.Scene.GetRootGameObjects().Count(root => root.name == "Saved VFX Preview"),
                    Is.Zero);
            }
        }

        [Test]
        public void StartingAnotherPreviewOrReplayingReplacesPreviousClone()
        {
            using (var fixture = new PreviewFixture())
            using (var first = new PreviewHandle())
            using (var second = new PreviewHandle())
            {
                object entry = fixture.CreateEntry();
                first.Play(entry, fixture.Scene);
                GameObject firstClone = first.Instance;
                second.Play(entry, fixture.Scene);
                Assert.That(firstClone == null, Is.True);
                Assert.That(first.Instance, Is.Null);
                Assert.That(first.IsPlaying, Is.False);
                GameObject secondClone = second.Instance;
                second.Play(entry, fixture.Scene);
                Assert.That(secondClone == null, Is.True);
                Assert.That(second.Instance, Is.Not.Null);
                Assert.That(fixture.Scene.GetRootGameObjects().Count(root => root.name == "Saved VFX Preview"),
                    Is.EqualTo(1));
                first.Dispose();
                Assert.That(second.Instance, Is.Not.Null, "Disposing a stopped preview must not stop the active one.");
                second.Stop();
                Assert.That(fixture.Scene.GetRootGameObjects().Count(root => root.name == "Saved VFX Preview"),
                    Is.Zero);
            }
        }

        private static Type FindType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name))
                .First(type => type != null);
        }

        private static T GetField<T>(object value, string name)
        {
            return (T)value.GetType().GetField(name).GetValue(value);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(.001f),
                "Expected " + expected.ToString("R") + ", got " + actual.ToString("R"));
        }

        private static void AssertRotation(Quaternion actual, Quaternion expected)
        {
            Assert.That(Quaternion.Angle(actual, expected), Is.LessThan(.05f));
        }

        private static float ReadAlpha(Renderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetFloat(Shader.PropertyToID("_CoreAlpha"));
        }

        private static ParticleSystem.Particle[] ReadParticles(ParticleSystem particles)
        {
            var values = new ParticleSystem.Particle[particles.particleCount];
            particles.GetParticles(values);
            return values;
        }

        private static void AssertParticles(ParticleSystem.Particle[] actual, ParticleSystem.Particle[] expected)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int index = 0; index < expected.Length; index++)
            {
                AssertVector(actual[index].position, expected[index].position);
                Assert.That(actual[index].remainingLifetime, Is.EqualTo(expected[index].remainingLifetime).Within(.001f));
            }
        }

        private sealed class PreviewHandle : IDisposable
        {
            private readonly object _preview = Activator.CreateInstance(FindType(PreviewTypeName));

            public GameObject Instance => Get<GameObject>("Instance");
            public float Time => Get<float>("Time");
            public float Duration => Get<float>("Duration");
            public int ParticleCount => Get<int>("ParticleCount");
            public bool IsPlaying => Get<bool>("IsPlaying");

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

        private sealed class PreviewFixture : IDisposable
        {
            private readonly string _prefabPath = "Assets/CK_Semester_Project/Tests/EditMode/Effects/"
                + "VfxPreview_" + Guid.NewGuid().ToString("N") + ".prefab";

            public Scene Scene { get; }
            public GameObject Prefab { get; }
            public Transform Dummy { get; }
            public Vector3 Offset => new Vector3(.6f, 1.2f, -.8f);
            public Vector3 Rotation => new Vector3(13f, 27f, -19f);

            public PreviewFixture(bool timeline = false)
            {
                Scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    Dummy = CreateRoot("VFX Training Dummy").transform;
                    Dummy.SetPositionAndRotation(new Vector3(4f, .3f, -3f), Quaternion.Euler(0f, 78f, 0f));
                    Dummy.localScale = new Vector3(2f, 3f, .7f);
                    GameObject root = CreateRoot("Saved VFX Test Source");
                    root.SetActive(false);
                    root.transform.localScale = new Vector3(2f, .75f, 1.3f);
                    root.AddComponent(FindType(SlashTypeName));
                    GameObject authored = CreateChild(root, "Authored Child");
                    authored.transform.SetLocalPositionAndRotation(new Vector3(1.2f, -.7f, 2.3f),
                        Quaternion.Euler(17f, 33f, 29f));
                    authored.transform.localScale = new Vector3(.4f, 1.6f, 2.1f);
                    authored.AddComponent<BoxCollider>();
                    authored.AddComponent(FindType(ChargeTypeName));
                    authored.AddComponent(FindType(TemplateTypeName));
                    ParticleSystem particles = CreateChild(root, "Particles").AddComponent<ParticleSystem>();
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = particles.main;
                    main.playOnAwake = false;
                    main.loop = false;
                    main.duration = 1f;
                    main.startLifetime = 2f;
                    main.startSpeed = .5f;
                    main.simulationSpeed = .65f;
                    var emission = particles.emission;
                    emission.rateOverTime = 40f;
                    var shape = particles.shape;
                    shape.enabled = false;
                    if (timeline)
                    {
                        GameObject fade = CreateChild(root, "Fade");
                        Renderer renderer = fade.AddComponent<MeshRenderer>();
                        Component player = fade.AddComponent(FindType("SakuraSlashFade"));
                        SetField(player, "targetRenderer", renderer);
                        SetField(player, "duration", 1f);
                        SetField(player, "coreAlphaCurve", AnimationCurve.Linear(0f, 1f, 1f, 0f));
                        GameObject streak = CreateChild(root, "Streak");
                        streak.transform.localScale = new Vector3(2f, 3f, 4f);
                        renderer = streak.AddComponent<MeshRenderer>();
                        player = streak.AddComponent(FindType("SakuraImpactStreakPlayer"));
                        SetField(player, "targetRenderer", renderer);
                        SetField(player, "duration", 1f);
                        SetField(player, "lengthCurve", AnimationCurve.Linear(0f, .5f, 1f, 1.5f));
                        SetField(player, "thicknessCurve", AnimationCurve.Linear(0f, 1f, 1f, 1f));
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

            public object CreateEntry(float delay = 0f, float lifetime = 1f, float speed = 1f, float baseSpeed = 1f)
            {
                object placement = Activator.CreateInstance(FindType(PlacementTypeName));
                SetField(placement, "Position", Offset);
                SetField(placement, "Rotation", Rotation);
                SetField(placement, "Scale", 1.7f);
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

            private static GameObject CreateChild(GameObject root, string name)
            {
                var child = new GameObject(name);
                child.transform.SetParent(root.transform, false);
                return child;
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
