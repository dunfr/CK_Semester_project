#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fixture = CK.SemesterProject.Battle.Tests.BattleVfxIndependentTestFixture;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class BladeSocketAuthoringTests
    {
        private const string ComponentSaverTypeName = "CK.SemesterProject.EditorTools.BattleVfxComponentSaver";
        private const string PreviewTypeName = "CK.SemesterProject.EditorTools.BattleVfxSavedEffectPreview";
        private const string EntryTypeName = "CK.SemesterProject.EditorTools.BattleVfxSavedEffectLibrary+Entry";

        [Test]
        public void ComponentSaveDetectsBladeParentAndPreservesAuthoredSocketPoseAndChildren()
        {
            using (var fixture = new Fixture())
            {
                Transform socket = CreateSocket(fixture);
                ParentSource(fixture.Source, socket);
                Vector3 position = fixture.Source.transform.position;
                Quaternion rotation = fixture.Source.transform.rotation;
                Vector3 scale = fixture.Source.transform.lossyScale;
                Vector3 localPosition = fixture.Source.transform.localPosition;
                Quaternion localRotation = fixture.Source.transform.localRotation;
                Transform child = fixture.Source.transform.Find("Particles");
                Vector3 childPosition = child.localPosition;
                Quaternion childRotation = child.localRotation;
                Vector3 childScale = child.localScale;

                GameObject saved = SaveComponent(fixture);
                object placement = fixture.GetPlayback(saved, out float baseSpeed);

                Assert.That(Convert.ToInt32(Fixture.Field<object>(placement, "AttachmentAnchor")), Is.EqualTo(1));
                Fixture.AssertVector(Fixture.Field<Vector3>(placement, "Position"), localPosition);
                Fixture.AssertRotation(Quaternion.Euler(Fixture.Field<Vector3>(placement, "Rotation")), localRotation);
                Fixture.AssertVector(saved.transform.localScale, scale);
                Fixture.AssertVector(saved.transform.Find("Particles").localPosition, childPosition);
                Fixture.AssertRotation(saved.transform.Find("Particles").localRotation, childRotation);
                Fixture.AssertVector(saved.transform.Find("Particles").localScale, childScale);
                Assert.That(baseSpeed, Is.EqualTo(1f));

                GameObject instance = fixture.Instantiate(saved);
                Fixture.Apply(placement, instance, fixture.Dummy);
                Assert.That(instance.transform.parent, Is.EqualTo(socket));
                Fixture.AssertVector(instance.transform.localPosition, localPosition);
                Fixture.AssertRotation(instance.transform.localRotation, localRotation);
                Fixture.AssertVector(instance.transform.position, position);
                Fixture.AssertRotation(instance.transform.rotation, rotation);
                Fixture.AssertVector(instance.transform.lossyScale, scale);

                Assert.That(fixture.Source.transform.parent, Is.EqualTo(socket));
                Fixture.AssertVector(fixture.Source.transform.position, position);
                Fixture.AssertRotation(fixture.Source.transform.rotation, rotation);
                Fixture.AssertVector(child.localPosition, childPosition);
                Fixture.AssertVector(fixture.SourcePrefab.transform.Find("Particles").localPosition, Vector3.zero);
            }
        }

        [Test]
        public void SavedPreviewFollowsBladeSocketWithoutBeingMovedBackToDummy()
        {
            using (var fixture = new Fixture())
            {
                Transform socket = CreateSocket(fixture);
                ParentSource(fixture.Source, socket);
                GameObject saved = SaveComponent(fixture);
                object placement = fixture.GetPlayback(saved, out float baseSpeed);
                Vector3 localPosition = Fixture.Field<Vector3>(placement, "Position");
                Quaternion localRotation = Quaternion.Euler(Fixture.Field<Vector3>(placement, "Rotation"));
                Vector3 worldScale = saved.transform.localScale;
                string savedBefore = EditorJsonUtility.ToJson(saved);
                object entry = CreateEntry(saved, placement, baseSpeed);
                var preview = (IDisposable)Activator.CreateInstance(Fixture.FindType(PreviewTypeName));
                try
                {
                    Invoke(preview, "Play", entry, fixture.Scene);
                    Invoke(preview, "SetPlaying", false);
                    Invoke(preview, "Sample", .1f);
                    GameObject instance = (GameObject)preview.GetType().GetProperty("Instance").GetValue(preview);
                    Assert.That(instance.transform.parent, Is.EqualTo(socket));

                    socket.SetPositionAndRotation(new Vector3(-7f, 3f, 8f), Quaternion.Euler(23f, 188f, -36f));
                    fixture.Dummy.SetPositionAndRotation(new Vector3(90f, -20f, -70f), Quaternion.Euler(0f, 20f, 0f));
                    Invoke(preview, "Sample", .2f);

                    Assert.That(instance.transform.parent, Is.EqualTo(socket));
                    Fixture.AssertVector(instance.transform.localPosition, localPosition);
                    Fixture.AssertRotation(instance.transform.localRotation, localRotation);
                    Fixture.AssertVector(instance.transform.position, socket.TransformPoint(localPosition));
                    Fixture.AssertRotation(instance.transform.rotation, socket.rotation * localRotation);
                    Fixture.AssertVector(instance.transform.lossyScale, worldScale);
                    Assert.That(EditorJsonUtility.ToJson(saved), Is.EqualTo(savedBefore));

                    Invoke(preview, "Stop");
                    Assert.That(instance == null, Is.True);
                    Assert.That(socket.childCount, Is.EqualTo(1), "The authored source must remain after the preview is removed.");
                }
                finally
                {
                    preview.Dispose();
                }
            }
        }

        [Test]
        public void SocketPlacementRejectsSceneWithoutBladeSocketInsteadOfSpawningAtDummy()
        {
            using (var fixture = new Fixture())
            {
                Transform socket = CreateSocket(fixture);
                ParentSource(fixture.Source, socket);
                GameObject saved = SaveComponent(fixture);
                object placement = fixture.GetPlayback(saved, out _);
                Scene missingSocketScene = EditorSceneManager.NewPreviewScene();
                try
                {
                    var dummy = new GameObject("VFX Training Dummy");
                    SceneManager.MoveGameObjectToScene(dummy, missingSocketScene);
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(saved, missingSocketScene);

                    TargetInvocationException error = Assert.Throws<TargetInvocationException>(
                        () => Fixture.Apply(placement, instance, dummy.transform));

                    Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
                    Assert.That(instance.transform.parent, Is.Null);
                    Fixture.AssertVector(instance.transform.position, Vector3.zero);
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(missingSocketScene);
                }
            }
        }

        [Test]
        public void IndependentSavedEffectStillUsesDummyWhenSceneAlsoContainsBladeSocket()
        {
            using (var fixture = new Fixture())
            {
                Transform socket = CreateSocket(fixture);
                Vector3 sourcePosition = fixture.Source.transform.position;
                Quaternion sourceRotation = fixture.Source.transform.rotation;
                GameObject saved = fixture.SaveAs();
                object placement = fixture.GetPlayback(saved, out _);
                Assert.That(Convert.ToInt32(Fixture.Field<object>(placement, "AttachmentAnchor")), Is.Zero);
                GameObject instance = fixture.Instantiate(saved);

                Fixture.Apply(placement, instance, fixture.Dummy);
                socket.position += Vector3.one * 50f;

                Assert.That(instance.transform.parent, Is.Null);
                Fixture.AssertVector(instance.transform.position, sourcePosition);
                Fixture.AssertRotation(instance.transform.rotation, sourceRotation);
            }
        }

        private static Transform CreateSocket(Fixture fixture)
        {
            GameObject actor = fixture.CreateObject("Socket Authoring Actor");
            actor.transform.SetPositionAndRotation(new Vector3(-2f, 1f, 3f), Quaternion.Euler(0f, 31f, 0f));
            actor.transform.localScale = Vector3.one * 1.75f;
            var socket = new GameObject("BladeSocket");
            socket.transform.SetParent(actor.transform, false);
            socket.transform.SetLocalPositionAndRotation(new Vector3(.8f, 1.1f, -.2f), Quaternion.Euler(13f, 71f, -8f));
            Component resolver = actor.AddComponent(Fixture.FindType("WeaponEffectSocket"));
            var serialized = new SerializedObject(resolver);
            serialized.FindProperty("_bladeSocket").objectReferenceValue = socket.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return socket.transform;
        }

        private static void ParentSource(GameObject source, Transform socket)
        {
            source.transform.SetParent(socket, false);
            source.transform.SetLocalPositionAndRotation(new Vector3(.12f, -.3f, .26f), Quaternion.Euler(12f, 34f, -19f));
            source.transform.localScale = new Vector3(.6f, 1.2f, 1.7f);
        }

        private static GameObject SaveComponent(Fixture fixture)
        {
            Component marker = fixture.AddMarker(fixture.Source);
            GameObject saved = (GameObject)Fixture.FindType(ComponentSaverTypeName).GetMethod("SaveAs")
                .Invoke(null, new object[] { marker, "Blade socket test " + fixture.Identifier });
            return fixture.Track(saved);
        }

        private static object CreateEntry(GameObject saved, object placement, float baseSpeed)
        {
            object entry = Activator.CreateInstance(Fixture.FindType(EntryTypeName));
            Fixture.SetField(entry, "Prefab", saved);
            Fixture.SetField(entry, "Placement", placement);
            Fixture.SetField(entry, "BasePlaybackSpeed", baseSpeed);
            return entry;
        }

        private static void Invoke(object target, string method, params object[] arguments)
        {
            target.GetType().GetMethod(method).Invoke(target, arguments);
        }
    }
}
#endif
