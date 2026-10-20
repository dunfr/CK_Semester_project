#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Fixture = CK.SemesterProject.Battle.Tests.BattleVfxIndependentTestFixture;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class BattleVfxComponentSaverTests
    {
        private const string SaverTypeName = "CK.SemesterProject.EditorTools.BattleVfxComponentSaver";

        [Test]
        public void SaveAutomaticallyUsesOwnSceneDummyAndPreservesRootAndChildEdits()
        {
            using (var fixture = new Fixture())
            {
                Vector3 position = fixture.Source.transform.position;
                Quaternion rotation = fixture.Source.transform.rotation;
                Vector3 scale = fixture.Source.transform.lossyScale;
                Vector3 offset = Quaternion.Inverse(fixture.Dummy.rotation) * (position - fixture.Dummy.position);
                Quaternion relativeRotation = Quaternion.Inverse(fixture.Dummy.rotation) * rotation;

                GameObject saved = InvokeSave(fixture, "Save", fixture.AddMarker(fixture.Source));

                Assert.That(saved.GetComponent(Fixture.TemplateType), Is.Null);
                Assert.That(fixture.SavedPrefab(fixture.Source), Is.EqualTo(saved));
                Fixture.AssertVector(saved.transform.localPosition, Vector3.zero);
                Fixture.AssertRotation(saved.transform.localRotation, Quaternion.identity);
                Fixture.AssertVector(saved.transform.localScale, scale);
                Transform savedChild = saved.transform.Find("Particles");
                Transform sourceChild = fixture.Source.transform.Find("Particles");
                Fixture.AssertVector(savedChild.localPosition, sourceChild.localPosition);
                Fixture.AssertRotation(savedChild.localRotation, sourceChild.localRotation);
                Fixture.AssertVector(savedChild.localScale, sourceChild.localScale);
                Assert.That(savedChild.GetComponent<ParticleSystem>().main.simulationSpeed, Is.EqualTo(.37f));
                Fixture.AssertVector(savedChild.GetComponent<BoxCollider>().size, new Vector3(2f, 3f, 4f));
                Assert.That(saved.transform.Find("Inspector Added Child").gameObject.activeSelf, Is.False);
                object placement = fixture.GetPlayback(saved, out float baseSpeed);
                Fixture.AssertVector(Fixture.Field<Vector3>(placement, "Position"), offset);
                Fixture.AssertRotation(Quaternion.Euler(Fixture.Field<Vector3>(placement, "Rotation")), relativeRotation);
                Assert.That(Fixture.Field<float>(placement, "Scale"), Is.EqualTo(1f));
                Assert.That(baseSpeed, Is.EqualTo(1f));
                Fixture.AssertVector(fixture.Source.transform.position, position);
                Fixture.AssertRotation(fixture.Source.transform.rotation, rotation);
                Fixture.AssertVector(fixture.Source.transform.lossyScale, scale);
                Assert.That(fixture.SourcePrefab.transform.Find("Particles").GetComponent<BoxCollider>(), Is.Null);
                Assert.That(fixture.SourcePrefab.transform.Find("Inspector Added Child"), Is.Null);
                Assert.That(fixture.SourcePrefab.transform.Find("Particles").GetComponent<ParticleSystem>()
                    .main.simulationSpeed, Is.EqualTo(.65f));
            }
        }

        [Test]
        public void ComponentOnChildAutomaticallySavesOwningEffectPrefabRoot()
        {
            using (var fixture = new Fixture())
            {
                Component childMarker = fixture.AddMarker(fixture.Source.transform.Find("Particles").gameObject);

                GameObject saved = InvokeSave(fixture, "Save", childMarker);

                Assert.That(saved.transform.Find("Particles"), Is.Not.Null);
                Assert.That(saved.transform.Find("Inspector Added Child"), Is.Not.Null);
                Assert.That(fixture.SavedPrefab(fixture.Source), Is.EqualTo(saved));
                Assert.That(saved.GetComponentsInChildren(Fixture.TemplateType, true), Is.Empty);
            }
        }

        [Test]
        public void DefaultSaveAlwaysCreatesNewCopyAndLeavesPreviousSnapshotUnchanged()
        {
            using (var fixture = new Fixture())
            {
                Component marker = fixture.AddMarker(fixture.Source);
                GameObject first = InvokeSave(fixture, "Save", marker);
                var previous = new AssetState(first);
                string firstName = fixture.GetDisplayName(first);
                Vector3 firstChildPosition = first.transform.Find("Particles").localPosition;
                fixture.Source.transform.position += new Vector3(2f, 1f, -3f);
                fixture.Source.transform.Find("Particles").localPosition = new Vector3(7f, 8f, 9f);

                GameObject second = InvokeSave(fixture, "Save", marker);

                Assert.That(AssetDatabase.GetAssetPath(second), Is.Not.EqualTo(previous.Path));
                previous.AssertUnchanged();
                Assert.That(fixture.GetDisplayName(second), Is.Not.EqualTo(firstName));
                Fixture.AssertVector(first.transform.Find("Particles").localPosition, firstChildPosition);
                Fixture.AssertVector(second.transform.Find("Particles").localPosition, new Vector3(7f, 8f, 9f));
                Assert.That(fixture.SavedPrefab(fixture.Source), Is.EqualTo(second));
                Assert.That(fixture.SnapshotPaths, Has.Count.EqualTo(2));
            }
        }

        [Test]
        public void NamedSaveAddsSuffixAndPreservesBothCopiesAndOriginal()
        {
            using (var fixture = new Fixture())
            {
                Component marker = fixture.AddMarker(fixture.Source);
                string name = "벚꽃 베기 " + fixture.Identifier;
                var original = new AssetState(fixture.SourcePrefab);
                GameObject first = InvokeSave(fixture, "SaveAs", marker, name);
                var previous = new AssetState(first);
                fixture.Source.transform.Find("Particles").localPosition = new Vector3(4f, 5f, 6f);

                GameObject second = InvokeSave(fixture, "SaveAs", marker, name);

                Assert.That(fixture.GetDisplayName(first), Is.EqualTo(name));
                Assert.That(fixture.GetDisplayName(second), Is.EqualTo(name + " (2)"));
                Assert.That(AssetDatabase.GetAssetPath(second), Is.Not.EqualTo(previous.Path));
                previous.AssertUnchanged();
                original.AssertUnchanged();
                Assert.That(fixture.GetSourceGuid(first), Is.EqualTo(original.Guid));
                Assert.That(fixture.GetSourceGuid(second), Is.EqualTo(original.Guid));
                Fixture.AssertVector(second.transform.Find("Particles").localPosition, new Vector3(4f, 5f, 6f));
                Assert.That(fixture.SnapshotPaths, Has.Count.EqualTo(2));
            }
        }

        [Test]
        public void ExplicitOverwriteKeepsSelectedSnapshotGuidAndNameAndRecreatesPose()
        {
            using (var fixture = new Fixture())
            {
                Component marker = fixture.AddMarker(fixture.Source);
                GameObject first = InvokeSave(fixture, "Save", marker);
                string path = AssetDatabase.GetAssetPath(first);
                string guid = AssetDatabase.AssetPathToGUID(path);
                string displayName = fixture.GetDisplayName(first);
                GameObject unselected = InvokeSave(fixture, "SaveAs", marker, "다른 이펙트 " + fixture.Identifier);
                var unselectedState = new AssetState(unselected);
                fixture.Source.transform.SetPositionAndRotation(new Vector3(-2f, 3.2f, 7.1f),
                    Quaternion.Euler(28f, -52f, 19f));
                fixture.Source.transform.localScale = new Vector3(.9f, 1.7f, 2.3f);
                Transform child = fixture.Source.transform.Find("Particles");
                child.localPosition = new Vector3(3f, -1.2f, .8f);
                var main = child.GetComponent<ParticleSystem>().main;
                main.simulationSpeed = .81f;
                Vector3 offset = Quaternion.Inverse(fixture.Dummy.rotation)
                    * (fixture.Source.transform.position - fixture.Dummy.position);
                Quaternion relativeRotation = Quaternion.Inverse(fixture.Dummy.rotation)
                    * fixture.Source.transform.rotation;

                GameObject second = InvokeSave(fixture, "Overwrite", marker, first);

                Assert.That(AssetDatabase.GetAssetPath(second), Is.EqualTo(path));
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                Assert.That(fixture.GetDisplayName(second), Is.EqualTo(displayName));
                unselectedState.AssertUnchanged();
                Assert.That(fixture.SnapshotPaths, Has.Count.EqualTo(2));
                Assert.That(fixture.SavedPrefab(fixture.Source), Is.EqualTo(second));
                Assert.That(fixture.Source.GetComponents(Fixture.TemplateType).Length, Is.EqualTo(1));
                Assert.That(second.transform.Find("Particles").GetComponent<ParticleSystem>()
                    .main.simulationSpeed, Is.EqualTo(.81f));
                fixture.Dummy.SetPositionAndRotation(new Vector3(10f, -2f, 6f), Quaternion.Euler(13f, 215f, -9f));
                GameObject instance = fixture.Instantiate(second);
                object placement = fixture.GetPlayback(second, out _);
                Fixture.Apply(placement, instance, fixture.Dummy);
                Fixture.AssertVector(instance.transform.position, fixture.Dummy.position + fixture.Dummy.rotation * offset);
                Fixture.AssertRotation(instance.transform.rotation, fixture.Dummy.rotation * relativeRotation);
                Fixture.AssertVector(instance.transform.lossyScale, fixture.Source.transform.lossyScale);
                Fixture.AssertVector(instance.transform.Find("Particles").localPosition, child.localPosition);
            }
        }

        [TestCase("")]
        [TestCase("   ")]
        public void EmptyNamedSaveDoesNotCreateAssetOrChangeAuthoringMarker(string displayName)
        {
            using (var fixture = new Fixture())
            {
                Component marker = fixture.AddMarker(fixture.Source);

                var exception = Assert.Throws<TargetInvocationException>(() =>
                    InvokeSave(fixture, "SaveAs", marker, displayName));

                Assert.That(exception.InnerException, Is.InstanceOf<ArgumentException>());
                Assert.That(fixture.SnapshotPaths, Is.Empty);
                Assert.That(fixture.SavedPrefab(fixture.Source), Is.Null);
            }
        }

        [Test]
        public void OverwriteRejectsOriginalPrefabWithoutChangingIt()
        {
            using (var fixture = new Fixture())
            {
                Component marker = fixture.AddMarker(fixture.Source);
                var original = new AssetState(fixture.SourcePrefab);

                var exception = Assert.Throws<TargetInvocationException>(() =>
                    InvokeSave(fixture, "Overwrite", marker, fixture.SourcePrefab));

                Assert.That(exception.InnerException, Is.InstanceOf<ArgumentException>());
                original.AssertUnchanged();
                Assert.That(fixture.SnapshotPaths, Is.Empty);
                Assert.That(fixture.SavedPrefab(fixture.Source), Is.Null);
            }
        }

        [Test]
        public void SaveOverwriteListAndDeleteNeverModifyBattleSkillSettingsInMemoryOrOnDisk()
        {
            string[] paths = AssetDatabase.FindAssets("t:BasicSkillEnhancementAsset")
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            Assert.That(paths, Is.Not.Empty);
            var snapshots = paths.Select(path => new SettingsState(path)).ToArray();
            using (var fixture = new Fixture())
            {
                Component marker = fixture.AddMarker(fixture.Source);
                GameObject saved = InvokeSave(fixture, "SaveAs", marker, "독립 이펙트 " + fixture.Identifier);
                AssertSettingsUnchanged(snapshots);
                fixture.Source.transform.position += new Vector3(1f, 2f, 3f);
                saved = InvokeSave(fixture, "Overwrite", marker, saved);
                AssertSettingsUnchanged(snapshots);
                Fixture.FindType(Fixture.LibraryTypeName).GetMethod("GetEntries").Invoke(null, null);
                AssertSettingsUnchanged(snapshots);
                Fixture.FindType(Fixture.LibraryTypeName).GetMethod("Delete").Invoke(null, new object[] { saved });
                AssertSettingsUnchanged(snapshots);
            }
            AssertSettingsUnchanged(snapshots);
        }

        private static GameObject InvokeSave(Fixture fixture, string name, params object[] arguments)
        {
            return fixture.Track((GameObject)Fixture.FindType(SaverTypeName).GetMethod(name).Invoke(null, arguments));
        }

        private static void AssertSettingsUnchanged(SettingsState[] snapshots)
        {
            foreach (SettingsState snapshot in snapshots)
            {
                snapshot.AssertUnchanged();
            }
        }

        private sealed class AssetState
        {
            private readonly Hash128 _hash;
            private readonly string _metadata;
            public string Path { get; }
            public string Guid { get; }

            public AssetState(GameObject asset)
            {
                Path = AssetDatabase.GetAssetPath(asset);
                Guid = AssetDatabase.AssetPathToGUID(Path);
                _hash = AssetDatabase.GetAssetDependencyHash(Path);
                _metadata = AssetImporter.GetAtPath(Path).userData;
            }

            public void AssertUnchanged()
            {
                Assert.That(AssetDatabase.AssetPathToGUID(Path), Is.EqualTo(Guid));
                Assert.That(AssetDatabase.GetAssetDependencyHash(Path), Is.EqualTo(_hash));
                Assert.That(AssetImporter.GetAtPath(Path).userData, Is.EqualTo(_metadata));
            }
        }

        private sealed class SettingsState
        {
            private readonly string _path;
            private readonly ScriptableObject _asset;
            private readonly string _json;
            private readonly byte[] _bytes;

            public SettingsState(string path)
            {
                _path = path;
                _asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                Assert.That(_asset, Is.Not.Null);
                _json = EditorJsonUtility.ToJson(_asset);
                _bytes = File.ReadAllBytes(path);
            }

            public void AssertUnchanged()
            {
                Assert.That(EditorJsonUtility.ToJson(_asset), Is.EqualTo(_json), _path + " in memory");
                Assert.That(File.ReadAllBytes(_path), Is.EqualTo(_bytes), _path + " on disk");
            }
        }
    }
}
#endif
