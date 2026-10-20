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
    public sealed class SkillVfxPlacementTests
    {
        private static Type RuntimeType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(
                "CK.SemesterProject.Tutorial." + name)).First(type => type != null);
        }

        private static object Placement(Vector3 position, Vector3 rotation, bool follow)
        {
            Type type = RuntimeType("SkillVfxPlacement");
            object value = Activator.CreateInstance(type);
            type.GetField("Enabled").SetValue(value, true);
            type.GetField("Anchor").SetValue(value, Enum.ToObject(RuntimeType("SkillVfxAnchor"), 1));
            type.GetField("Position").SetValue(value, position);
            type.GetField("Rotation").SetValue(value, rotation);
            type.GetField("Scale").SetValue(value, 2f);
            type.GetField("Follow").SetValue(value, follow);
            type.GetField("PlaybackSpeed").SetValue(value, .5f);
            type.GetField("StartDelay").SetValue(value, .2f);
            type.GetField("Lifetime").SetValue(value, 1.5f);
            return value;
        }

        [Test]
        public void PreviewParticleSimulationKeepsShortBurstsVisibleAndSupportsSlowMotion()
        {
            var root = new GameObject("Preview particles");
            try
            {
                ParticleSystem particle = root.AddComponent<ParticleSystem>();
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particle.main;
                main.loop = false;
                main.duration = .5f;
                main.startLifetime = .5f;
                main.startSpeed = 0f;
                var emission = particle.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
                particle.useAutoRandomSeed = false;
                particle.randomSeed = 12345u;
                particle.Simulate(.08f, false, true, false);
                particle.Pause(false);
                Assert.That(particle.particleCount, Is.EqualTo(24));
                for (int i = 0; i < 10; i++)
                {
                    particle.Simulate(.002f, false, false, false);
                    particle.Pause(false);
                }
                Assert.That(particle.particleCount, Is.EqualTo(24));
                particle.Simulate(1.2f, false, true, false);
                Assert.That(particle.particleCount, Is.Zero);
                main.simulationSpeed = .25f;
                particle.Simulate(1.2f, false, true, false);
                Assert.That(particle.particleCount, Is.EqualTo(24));
                float duration = (float)RuntimeType("SkillVfxPlayback").GetMethod("EstimateDuration").Invoke(null, new object[] { root, 1f });
                Assert.That(duration, Is.EqualTo(2f).Within(.001f));
                var emptyParent = new GameObject("Empty particle container");
                try
                {
                    ParticleSystem container = emptyParent.AddComponent<ParticleSystem>();
                    container.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var containerMain = container.main;
                    containerMain.duration = 20f;
                    var containerEmission = container.emission;
                    containerEmission.enabled = false;
                    root.transform.SetParent(emptyParent.transform);
                    float nestedDuration = (float)RuntimeType("SkillVfxPlayback").GetMethod("EstimateDuration").Invoke(null, new object[] { emptyParent, 1f });
                    Assert.That(nestedDuration, Is.EqualTo(2f).Within(.001f));
                    root.transform.SetParent(null);
                }
                finally
                {
                    root.transform.SetParent(null);
                    Object.DestroyImmediate(emptyParent);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }


        [UnityTest]
        public IEnumerator RuntimePlaybackAppliesDelaySpeedAndLifetime()
        {
            var root = new GameObject("Timed effect");
            try
            {
                ParticleSystem particle = root.AddComponent<ParticleSystem>();
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particle.main;
                main.loop = false;
                main.duration = .5f;
                main.startLifetime = 2f;
                var emission = particle.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
                object placement = Placement(Vector3.zero, Vector3.zero, false);
                Type placementType = placement.GetType();
                placementType.GetField("StartDelay").SetValue(placement, .2f);
                placementType.GetField("Lifetime").SetValue(placement, .7f);
                Component playback = root.AddComponent(RuntimeType("SkillVfxPlayback"));
                playback.GetType().GetMethod("Initialize").Invoke(playback, new[] { placement, (object)1f });
                Assert.That(particle.main.simulationSpeed, Is.EqualTo(.5f).Within(.001f));
                Assert.That(particle.particleCount, Is.Zero);
                yield return new WaitForSeconds(.35f);
                Assert.That(particle.particleCount, Is.GreaterThan(0));
                yield return new WaitForSeconds(.75f);
                Assert.That(root == null, Is.True);
            }
            finally
            {
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void PlacementUsesRotatedAnchorAndOptionalFollowing()
        {
            var actor = new GameObject("Actor");
            var target = new GameObject("Target");
            var effect = new GameObject("Effect");
            try
            {
                target.transform.SetPositionAndRotation(new Vector3(4f, 2f, -3f), Quaternion.Euler(0f, 90f, 0f));
                Vector3 offset = new Vector3(1f, 2f, 3f);
                Vector3 angles = new Vector3(20f, 40f, 60f);
                object placement = Placement(offset, angles, true);
                placement.GetType().GetMethod("Apply").Invoke(placement, new object[] { effect, actor.transform, target.transform });
                Vector3 expected = target.transform.position + target.transform.rotation * offset;
                Assert.That(Vector3.Distance(effect.transform.position, expected), Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(effect.transform.rotation,
                    target.transform.rotation * Quaternion.Euler(angles)), Is.LessThan(.01f));
                Assert.That(effect.transform.parent, Is.EqualTo(target.transform));
                Assert.That(effect.transform.lossyScale.x, Is.EqualTo(2f).Within(.0001f));
                target.transform.position += Vector3.right;
                Assert.That(Vector3.Distance(effect.transform.position, expected + Vector3.right), Is.LessThan(.0001f));
                effect.transform.SetParent(null, true);
                object fixedPlacement = Placement(offset, angles, false);
                fixedPlacement.GetType().GetMethod("Apply").Invoke(fixedPlacement, new object[] { effect, actor.transform, target.transform });
                Vector3 fixedPosition = effect.transform.position;
                target.transform.position += Vector3.forward;
                Assert.That(effect.transform.parent, Is.Null);
                Assert.That(effect.transform.position, Is.EqualTo(fixedPosition));
            }
            finally
            {
                Object.DestroyImmediate(effect);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void SavedPlacementSurvivesReloadAndKeepsStagesIndependent()
        {
            const string sourcePath = "Assets/CK_Semester_Project/Prototype/Data/BattleVfxTest/BattleVfxTestEnhancement.asset";
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/CK_Semester_Project/Tests/PlayMode/BattleDemo/VfxPlacementRoundTrip.asset");
            var source = AssetDatabase.LoadAssetAtPath<ScriptableObject>(sourcePath);
            Assert.That(source, Is.Not.Null);
            ScriptableObject settings = Object.Instantiate(source);
            try
            {
                Type type = settings.GetType();
                object stageTwoBefore = type.GetMethod("GetPlacement").Invoke(settings, new object[] { "SK00", 2, false });
                object placement = Placement(new Vector3(1f, 2f, 3f), new Vector3(10f, 20f, 30f), true);
                type.GetMethod("SetPlacement").Invoke(settings, new object[] { "SK00", 1, false, null, placement });
                type.GetMethod("SetPlacement").Invoke(settings, new object[] { "SK00", 1, true, null, placement });
                type.GetMethod("SetActionSettings").Invoke(settings, new object[] { "SK00", 1, null, .5f, .6f });
                AssetDatabase.CreateAsset(settings, path);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssetIfDirty(settings);
                Resources.UnloadAsset(settings);
                settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                Assert.That(type.GetMethod("GetPlacement").Invoke(settings, new object[] { "SK00", 1, false }), Is.EqualTo(placement));
                Assert.That(type.GetMethod("GetPlacement").Invoke(settings, new object[] { "SK03", 2, false }), Is.EqualTo(stageTwoBefore));
                Assert.That(type.GetMethod("GetPlacement").Invoke(settings, new object[] { "SK06", 3, true }), Is.EqualTo(placement));
                Assert.That(type.GetMethod("GetActionSpeed").Invoke(settings, new object[] { "SK00", 1 }), Is.EqualTo(.5f));
                Assert.That(type.GetMethod("GetImpactFraction").Invoke(settings, new object[] { "SK00", 1 }), Is.EqualTo(.6f));
                Assert.That(type.GetMethod("GetActionSpeed").Invoke(settings, new object[] { "SK03", 2 }), Is.EqualTo(1f));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                if (settings != null && !EditorUtility.IsPersistent(settings))
                {
                    Object.DestroyImmediate(settings);
                }
            }
        }
    }
}
