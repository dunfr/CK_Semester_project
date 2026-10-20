#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CK.SemesterProject.Battle.Tests
{
    internal sealed class BattleVfxIndependentTestFixture : IDisposable
    {
        public const string SaverTypeName = "CK.SemesterProject.EditorTools.BattleVfxScenePlacementSaver";
        public const string LibraryTypeName = "CK.SemesterProject.EditorTools.BattleVfxSavedEffectLibrary";
        public const string PlacementTypeName = "CK.SemesterProject.Tutorial.BattleVfxEffectPlacement";
        private const string TemplateTypeName = "CK.SemesterProject.Tutorial.BattleVfxSceneTemplate";
        private const string TestFolder = "Assets/CK_Semester_Project/Tests/EditMode/Effects/";

        public static Type TemplateType => FindType(TemplateTypeName);
        public string Identifier { get; } = Guid.NewGuid().ToString("N");
        public HashSet<string> SnapshotPaths { get; } = new HashSet<string>();
        public Scene Scene { get; }
        public string SourcePrefabPath { get; }
        public GameObject SourcePrefab { get; }
        public GameObject Source { get; }
        public Transform Dummy { get; }

        public BattleVfxIndependentTestFixture()
        {
            SourcePrefabPath = TestFolder + "VfxIndependentSource_" + Identifier + ".prefab";
            Scene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = CreateObject("Vfx Source " + Identifier);
                root.SetActive(false);
                var child = new GameObject("Particles");
                child.transform.SetParent(root.transform, false);
                ParticleSystem particles = child.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.playOnAwake = false;
                main.simulationSpeed = .65f;
                root.SetActive(true);
                SourcePrefab = PrefabUtility.SaveAsPrefabAsset(root, SourcePrefabPath);
                Assert.That(SourcePrefab, Is.Not.Null);
                Object.DestroyImmediate(root);
                Source = Instantiate(SourcePrefab);
                Source.transform.SetPositionAndRotation(new Vector3(4.3f, 2.6f, -1.7f),
                    Quaternion.Euler(17f, 38f, -29f));
                Source.transform.localScale = new Vector3(2f, 3f, .5f);
                Transform edited = Source.transform.Find("Particles");
                edited.SetLocalPositionAndRotation(new Vector3(1.2f, 2.3f, 3.4f), Quaternion.Euler(12f, 34f, 56f));
                edited.localScale = new Vector3(.6f, 1.4f, 2.2f);
                var editedMain = edited.GetComponent<ParticleSystem>().main;
                editedMain.simulationSpeed = .37f;
                edited.gameObject.AddComponent<BoxCollider>().size = new Vector3(2f, 3f, 4f);
                var added = new GameObject("Inspector Added Child");
                added.transform.SetParent(Source.transform, false);
                added.transform.localPosition = Vector3.up * 2f;
                added.SetActive(false);
                Dummy = CreateObject("VFX Training Dummy").transform;
                Dummy.SetPositionAndRotation(new Vector3(2f, .4f, -3f), Quaternion.Euler(0f, 90f, 0f));
                Dummy.localScale = new Vector3(2f, 3f, .7f);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public static Type FindType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name))
                .First(type => type != null);
        }

        public static T Field<T>(object value, string name)
        {
            return (T)value.GetType().GetField(name).GetValue(value);
        }

        public static void SetField(object value, string name, object fieldValue)
        {
            value.GetType().GetField(name).SetValue(value, fieldValue);
        }

        public static void Apply(object placement, GameObject instance, Transform dummy)
        {
            placement.GetType().GetMethod("Apply").Invoke(placement, new object[] { instance, dummy });
        }

        public static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(.001f),
                "Expected " + expected.ToString("R") + ", got " + actual.ToString("R"));
        }

        public static void AssertRotation(Quaternion actual, Quaternion expected)
        {
            Assert.That(Quaternion.Angle(actual, expected), Is.LessThan(.05f));
        }

        public Component AddMarker(GameObject target)
        {
            return target.AddComponent(TemplateType);
        }

        public GameObject SavedPrefab(GameObject target)
        {
            Component marker = target.GetComponent(TemplateType);
            Assert.That(marker, Is.Not.Null);
            return (GameObject)marker.GetType().GetProperty("SavedPrefab").GetValue(marker);
        }

        public GameObject SaveAs(string displayName = null)
        {
            return Track((GameObject)FindType(SaverTypeName).GetMethod("SaveAs").Invoke(null,
                new object[] { Source, Dummy, displayName ?? Source.name }));
        }

        public GameObject Overwrite(GameObject saved)
        {
            return Track((GameObject)FindType(SaverTypeName).GetMethod("Overwrite").Invoke(null,
                new object[] { Source, Dummy, saved }));
        }

        public GameObject Track(GameObject saved)
        {
            Assert.That(saved, Is.Not.Null);
            SnapshotPaths.Add(AssetDatabase.GetAssetPath(saved));
            return saved;
        }

        public object GetPlayback(GameObject saved, out float baseSpeed)
        {
            var arguments = new object[] { saved, null, 0f };
            Assert.That((bool)FindType(SaverTypeName).GetMethod("TryGetSavedPlayback").Invoke(null, arguments), Is.True);
            baseSpeed = (float)arguments[2];
            return arguments[1];
        }

        public string GetDisplayName(GameObject saved)
        {
            return (string)FindType(SaverTypeName).GetMethod("GetSavedDisplayName")
                .Invoke(null, new object[] { saved });
        }

        public string GetSourceGuid(GameObject saved)
        {
            return (string)FindType(SaverTypeName).GetMethod("GetSourceGuid")
                .Invoke(null, new object[] { saved });
        }

        public GameObject Instantiate(GameObject prefab)
        {
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab, Scene);
        }

        public GameObject CreateObject(string name)
        {
            var created = new GameObject(name);
            SceneManager.MoveGameObjectToScene(created, Scene);
            return created;
        }

        public void Dispose()
        {
            if (Scene.IsValid() && Scene.isLoaded)
            {
                foreach (GameObject root in Scene.GetRootGameObjects())
                {
                    foreach (ParticleSystem particles in root.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                    foreach (Component component in root.GetComponentsInChildren<Component>(true))
                    {
                        if (component != null)
                        {
                            Undo.ClearUndo(component);
                        }
                    }
                    Undo.ClearUndo(root);
                }
                EditorSceneManager.ClosePreviewScene(Scene);
            }
            foreach (string path in SnapshotPaths)
            {
                AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.DeleteAsset(SourcePrefabPath);
        }
    }
}
#endif
