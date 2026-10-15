using System;
using System.Linq;
using CK.SemesterProject.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CK.SemesterProject.Editor
{
    // 기존 7개 스킨 메시와 리그를 유지하며 새 FBX에서 바뀐 몸 외형만 갱신한다.
    public static class FuchsiaModelReplacement
    {
        private const string Folder = "Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/";

        public static string BuildAndApply()
        {
            GameObject rig = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Prefabs/FuchsiaAnimated.prefab"));
            try
            {
                SkinnedMeshRenderer body = rig.GetComponentsInChildren<SkinnedMeshRenderer>().Single(item => item.name == "body");
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Models/character_fuchsia_01.fbx");
                MeshFilter changed = model.GetComponentsInChildren<MeshFilter>().Single(item => item.name == "몸");
                Mesh original = body.sharedMesh;
                Mesh incoming = changed.sharedMesh;
                Vector3[] positions = original.vertices;
                Vector3[] normals = original.normals;
                Vector4[] tangents = original.tangents;
                Vector2[] uv = original.uv;
                Vector3[] newPositions = incoming.vertices;
                Vector3[] newNormals = incoming.normals;
                Vector4[] newTangents = incoming.tangents;
                Vector2[] newUv = incoming.uv;
                Matrix4x4 toBody = body.transform.worldToLocalMatrix * changed.transform.localToWorldMatrix;
                Vector3[] converted = newPositions.Select(toBody.MultiplyPoint3x4).ToArray();
                int[] affected = Enumerable.Range(0, positions.Length)
                    .Where(index => newUv.Any(value => (value - uv[index]).sqrMagnitude < 1e-10f)).ToArray();
                if (affected.Length != newPositions.Length)
                {
                    throw new InvalidOperationException("몸 메시의 기존 정점 대응 수가 일치하지 않습니다. 리깅을 재계산하지 않습니다.");
                }
                Vector3 offset = affected.Select(index => positions[index]).Aggregate(Vector3.zero, (sum, value) => sum + value) / affected.Length
                    - converted.Aggregate(Vector3.zero, (sum, value) => sum + value) / converted.Length;
                bool[] used = new bool[converted.Length];
                Matrix4x4 normalMatrix = toBody.inverse.transpose;
                float maximumDistance = 0;
                foreach (int index in affected)
                {
                    int candidate = -1;
                    float distance = float.PositiveInfinity;
                    for (int next = 0; next < converted.Length; next++)
                    {
                        if (used[next] || (newUv[next] - uv[index]).sqrMagnitude >= 1e-10f)
                        {
                            continue;
                        }
                        float difference = (positions[index] - converted[next] - offset).sqrMagnitude;
                        if (difference < distance)
                        {
                            candidate = next;
                            distance = difference;
                        }
                    }
                    if (candidate < 0 || distance > .000004f)
                    {
                        throw new InvalidOperationException("기존 몸 정점에 맞지 않는 새 메시입니다: " + index);
                    }
                    used[candidate] = true;
                    maximumDistance = Mathf.Max(maximumDistance, Mathf.Sqrt(distance));
                    positions[index] = converted[candidate] + offset;
                    normals[index] = normalMatrix.MultiplyVector(newNormals[candidate]).normalized;
                    uv[index] = newUv[candidate];
                    if (newTangents.Length == newPositions.Length && tangents.Length == positions.Length)
                    {
                        Vector3 tangent = toBody.MultiplyVector(newTangents[candidate]).normalized;
                        tangents[index] = new Vector4(tangent.x, tangent.y, tangent.z, newTangents[candidate].w * Mathf.Sign(toBody.determinant));
                    }
                }
                // 원본 스킨 메시를 복제하므로 본, 가중치, bindpose, 삼각형, 나머지 부품은 그대로다.
                Mesh mesh = UnityEngine.Object.Instantiate(original);
                mesh.name = "FuchsiaUpdatedBody";
                mesh.vertices = positions;
                mesh.normals = normals;
                mesh.tangents = tangents;
                mesh.uv = uv;
                mesh.RecalculateBounds();
                if (!mesh.boneWeights.SequenceEqual(original.boneWeights) || !mesh.bindposes.SequenceEqual(original.bindposes))
                {
                    throw new InvalidOperationException("원본 리깅 데이터가 달라졌습니다.");
                }
                const string meshPath = Folder + "Models/FuchsiaUpdatedBody.asset";
                Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (saved == null)
                {
                    AssetDatabase.CreateAsset(mesh, meshPath);
                }
                else
                {
                    EditorUtility.CopySerialized(mesh, saved);
                    EditorUtility.SetDirty(saved);
                    UnityEngine.Object.DestroyImmediate(mesh);
                    mesh = saved;
                }
                body.sharedMesh = mesh;
                const string prefabPath = Folder + "Prefabs/FuchsiaUpdatedAnimated.prefab";
                rig.name = "FuchsiaUpdatedAnimated";
                PrefabUtility.SaveAsPrefabAsset(rig, prefabPath);
                Transform player = GameObject.Find("Fuchsia Player").transform;
                Transform previous = player.Find("Fuchsia Visual");
                GameObject replacement = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), player);
                replacement.name = "Fuchsia Visual";
                replacement.transform.localPosition = previous.localPosition;
                replacement.transform.localRotation = previous.localRotation;
                replacement.transform.localScale = previous.localScale;
                var movement = new SerializedObject(player.GetComponent<PlayerMovement>());
                movement.FindProperty("_animator").objectReferenceValue = replacement.GetComponentInChildren<Animator>();
                movement.ApplyModifiedPropertiesWithoutUndo();
                UnityEngine.Object.DestroyImmediate(previous.gameObject);
                EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
                EditorSceneManager.SaveScene(player.gameObject.scene);
                AssetDatabase.SaveAssets();
                return "Original 7 renderers/rig/weights retained; updated body vertices=" + affected.Length + "; position difference=" + maximumDistance;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rig);
            }
        }
    }
}
