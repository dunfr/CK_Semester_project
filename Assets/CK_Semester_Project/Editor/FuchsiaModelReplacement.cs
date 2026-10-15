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
            // Instantiate(parent)의 기본 정렬에 맡기지 않고 프리팹의 원점 보정을 명시한다.
            appearance.transform.localPosition = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Prefabs/FuchsiaToon.prefab").transform.localPosition;
            appearance.transform.localRotation = Quaternion.identity;
            appearance.transform.localScale = Vector3.one;
            appearance.name = "Updated Appearance";
            var sources = rig.GetComponentsInChildren<SkinnedMeshRenderer>().ToDictionary(item => item.name);
            // 같은 치마 토폴로지의 중심을 기준으로 두 FBX의 미세한 원점 차이도 제거한다.
            MeshRenderer skirt = appearance.GetComponentsInChildren<MeshRenderer>().Single(item => item.name == "치마");
            Vector3 sourceCenter = sources["skirt"].sharedMesh.vertices.Aggregate(Vector3.zero, (sum, vertex) => sum + vertex)
                / sources["skirt"].sharedMesh.vertexCount;
            Mesh skirtMesh = skirt.GetComponent<MeshFilter>().sharedMesh;
            Vector3 targetCenter = skirtMesh.vertices.Aggregate(Vector3.zero, (sum, vertex) => sum + vertex) / skirtMesh.vertexCount;
            appearance.transform.position += sources["skirt"].transform.TransformPoint(sourceCenter) - skirt.transform.TransformPoint(targetCenter);
            var targets = new List<SkinnedMeshRenderer>();
            float maximumDistance = 0;
            if (!AssetDatabase.IsValidFolder(Folder + "Models/UpdatedSkinBindings"))
            {
                AssetDatabase.CreateFolder(Folder + "Models", "UpdatedSkinBindings");
            }
            try
            {
                foreach (MeshRenderer renderer in appearance.GetComponentsInChildren<MeshRenderer>())
                {
                    string group = GroupFor(renderer);
                    SkinnedMeshRenderer source = sources[group];
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    Mesh originalMesh = filter.sharedMesh;
                    var mesh = new Mesh();
                    mesh.indexFormat = originalMesh.indexFormat;
                    mesh.vertices = originalMesh.vertices;
                    mesh.normals = originalMesh.normals;
                    mesh.tangents = originalMesh.tangents;
                    mesh.uv = originalMesh.uv;
                    mesh.uv2 = originalMesh.uv2;
                    mesh.colors = originalMesh.colors;
                    mesh.subMeshCount = originalMesh.subMeshCount;
                    for (int submesh = 0; submesh < originalMesh.subMeshCount; submesh++)
                    {
                        mesh.SetTriangles(originalMesh.GetTriangles(submesh), submesh);
                    }
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
                    // FBX 부품별 비균일 스케일을 스키닝 Transform에 남기지 않는다.
                    // 모든 정점/법선을 기존 스킨 메시와 같은 바인드 공간으로 옮긴다.
                    mesh.vertices = vertices.Select(toSource.MultiplyPoint3x4).ToArray();
                    Matrix4x4 normalMatrix = toSource.inverse.transpose;
                    mesh.normals = mesh.normals.Select(normal => normalMatrix.MultiplyVector(normal).normalized).ToArray();
                    Vector4[] tangents = mesh.tangents;
                    for (int index = 0; index < tangents.Length; index++)
                    {
                        Vector3 tangent = toSource.MultiplyVector(tangents[index]).normalized;
                        tangents[index] = new Vector4(tangent.x, tangent.y, tangent.z,
                            tangents[index].w * Mathf.Sign(toSource.determinant));
                    }
                    mesh.tangents = tangents;
                    if (toSource.determinant < 0)
                    {
                        for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                        {
                            int[] triangles = mesh.GetTriangles(submesh);
                            for (int index = 0; index < triangles.Length; index += 3)
                            {
                                int first = triangles[index];
                                triangles[index] = triangles[index + 2];
                                triangles[index + 2] = first;
                            }
                            mesh.SetTriangles(triangles, submesh);
                        }
                    }
                    mesh.RecalculateBounds();
                    mesh.boneWeights = weights;
                    mesh.bindposes = source.sharedMesh.bindposes;
                    string path = Folder + "Models/UpdatedSkinBindings/" + mesh.name + ".asset";
                    Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (existing == null)
                    {
                        AssetDatabase.CreateAsset(mesh, path);
                    }
                    else
                    {
                        EditorUtility.CopySerialized(mesh, existing);
                        EditorUtility.SetDirty(existing);
                        UnityEngine.Object.DestroyImmediate(mesh);
                        mesh = existing;
                    }
                    var item = new GameObject(renderer.name);
                    item.transform.SetParent(source.transform.parent, false);
                    item.transform.localPosition = source.transform.localPosition;
                    item.transform.localRotation = source.transform.localRotation;
                    item.transform.localScale = source.transform.localScale;
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
                UnityEngine.Object.DestroyImmediate(appearance);
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
