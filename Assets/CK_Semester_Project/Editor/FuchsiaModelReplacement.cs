using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CK.SemesterProject.Editor
{
    // 새 정적 외형을 기존 Generic 리그의 바인드 공간에 연결한다. 원본 리깅 프리팹은 보존한다.
    public static class FuchsiaModelReplacement
    {
        private const string Folder = "Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/";
        private const string PrefabPath = Folder + "Prefabs/FuchsiaUpdatedAnimated.prefab";

        public static string BuildAndApply()
        {
            GameObject rig = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Prefabs/FuchsiaAnimated.prefab"));
            GameObject appearance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Prefabs/FuchsiaToon.prefab"), rig.transform);
            appearance.transform.localPosition = Vector3.zero;
            appearance.transform.localRotation = Quaternion.identity;
            appearance.transform.localScale = Vector3.one;
            appearance.name = "Updated Appearance";
            var sources = rig.GetComponentsInChildren<SkinnedMeshRenderer>().ToDictionary(item => item.name);
            var targets = new List<SkinnedMeshRenderer>();
            float maximumDistance = 0;
            if (!AssetDatabase.IsValidFolder(Folder + "Models/UpdatedSkin"))
            {
                AssetDatabase.CreateFolder(Folder + "Models", "UpdatedSkin");
            }
            try
            {
                foreach (MeshRenderer renderer in appearance.GetComponentsInChildren<MeshRenderer>())
                {
                    string group = GroupFor(renderer);
                    SkinnedMeshRenderer source = sources[group];
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    Mesh mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                    mesh.name = "FuchsiaUpdated_" + renderer.name;
                    Vector3[] vertices = mesh.vertices;
                    Vector3[] oldVertices = source.sharedMesh.vertices;
                    BoneWeight[] oldWeights = source.sharedMesh.boneWeights;
                    var weights = new BoneWeight[vertices.Length];
                    Matrix4x4 toSource = source.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                    for (int index = 0; index < vertices.Length; index++)
                    {
                        Vector3 point = toSource.MultiplyPoint3x4(vertices[index]);
                        float best = float.PositiveInfinity;
                        int nearest = 0;
                        for (int candidate = 0; candidate < oldVertices.Length; candidate++)
                        {
                            float distance = (oldVertices[candidate] - point).sqrMagnitude;
                            if (distance < best)
                            {
                                best = distance;
                                nearest = candidate;
                            }
                        }
                        maximumDistance = Mathf.Max(maximumDistance, Mathf.Sqrt(best));
                        weights[index] = oldWeights[nearest];
                    }
                    mesh.boneWeights = weights;
                    mesh.bindposes = source.sharedMesh.bindposes.Select(pose => pose * toSource).ToArray();
                    string path = Folder + "Models/UpdatedSkin/" + mesh.name + ".asset";
                    Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (existing == null)
                    {
                        AssetDatabase.CreateAsset(mesh, path);
                    }
                    else
                    {
                        EditorUtility.CopySerialized(mesh, existing);
                        UnityEngine.Object.DestroyImmediate(mesh);
                        mesh = existing;
                    }
                    GameObject item = renderer.gameObject;
                    Material[] materials = renderer.sharedMaterials;
                    var shadows = renderer.shadowCastingMode;
                    UnityEngine.Object.DestroyImmediate(renderer);
                    UnityEngine.Object.DestroyImmediate(filter);
                    SkinnedMeshRenderer target = item.AddComponent<SkinnedMeshRenderer>();
                    target.sharedMesh = mesh;
                    target.sharedMaterials = materials;
                    target.bones = source.bones;
                    target.rootBone = source.rootBone;
                    target.shadowCastingMode = shadows;
                    target.updateWhenOffscreen = true;
                    targets.Add(target);
                }
                // 검은 기존 리깅과 머티리얼을 그대로 사용한다.
                foreach (SkinnedMeshRenderer source in sources.Values.Where(item => item.name != "Katana"))
                {
                    source.enabled = false;
                }
                SampleBounds(rig, targets);
                rig.name = "FuchsiaUpdatedAnimated";
                PrefabUtility.SaveAsPrefabAsset(rig, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rig);
            }
            Transform player = GameObject.Find("Fuchsia Player").transform;
            Transform old = player.Find("Fuchsia Visual");
            Vector3 position = old.localPosition;
            Quaternion rotation = old.localRotation;
            Vector3 scale = old.localScale;
            GameObject replacement = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), player);
            replacement.name = "Fuchsia Visual";
            replacement.transform.localPosition = position;
            replacement.transform.localRotation = rotation;
            replacement.transform.localScale = scale;
            var movement = new SerializedObject(player.GetComponent<CK.SemesterProject.Tutorial.PlayerMovement>());
            movement.FindProperty("_animator").objectReferenceValue = replacement.GetComponentInChildren<Animator>();
            movement.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Object.DestroyImmediate(old.gameObject);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene);
            AssetDatabase.SaveAssets();
            return "Transferred " + targets.Count + " meshes; maximum bind correspondence distance=" + maximumDistance.ToString("F5") + "m";
        }

        private static string GroupFor(MeshRenderer renderer)
        {
            string name = renderer.name;
            if (renderer.sharedMaterial.name == "FuchsiaHairToon")
            {
                return "hair";
            }
            if (renderer.sharedMaterial.name == "FuchsiaFaceToon")
            {
                return "head";
            }
            if (name == "안경" || name == "안경알")
            {
                return "glasses";
            }
            if (name == "치마")
            {
                return "skirt";
            }
            if (name == "외투" || name == "리본")
            {
                return "outer";
            }
            return "body";
        }

        private static void SampleBounds(GameObject rig, List<SkinnedMeshRenderer> targets)
        {
            var bounds = new Bounds[targets.Count];
            var initialized = new bool[targets.Count];
            foreach (string clipName in new[] { "FuchsiaIdle", "FuchsiaWalk", "FuchsiaRun" })
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Animations/" + clipName + ".anim");
                for (int frame = 0; frame <= 24; frame++)
                {
                    clip.SampleAnimation(rig, clip.length * frame / 24f);
                    for (int index = 0; index < targets.Count; index++)
                    {
                        SkinnedMeshRenderer target = targets[index];
                        var baked = new Mesh();
                        target.BakeMesh(baked);
                        foreach (Vector3 vertex in baked.vertices)
                        {
                            Vector3 point = target.rootBone.InverseTransformPoint(target.transform.TransformPoint(vertex));
                            if (!initialized[index])
                            {
                                bounds[index] = new Bounds(point, Vector3.zero);
                                initialized[index] = true;
                            }
                            bounds[index].Encapsulate(point);
                        }
                        UnityEngine.Object.DestroyImmediate(baked);
                    }
                }
            }
            for (int index = 0; index < targets.Count; index++)
            {
                bounds[index].Expand(.15f);
                targets[index].localBounds = bounds[index];
                targets[index].updateWhenOffscreen = false;
            }
        }
    }
}
