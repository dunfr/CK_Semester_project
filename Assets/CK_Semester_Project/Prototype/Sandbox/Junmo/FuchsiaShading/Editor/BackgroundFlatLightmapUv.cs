using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// 평평한 바닥 타일의 라이트맵 UV(UV2)를 한 조각으로 편다.
//
// 왜 필요한가:
//   Unity의 자동 라이트맵 UV(Generate Lightmap UVs)는 바닥 타일 윗면을 홈마다 잘라 조각 수십 개로 만든다(6m 타일이 51조각).
//   이 프로젝트는 라이트맵을 1m당 5칸으로 굽는데, 조각 사이 간격이 한 칸의 절반도 안 돼서 옆 조각의 빛이 섞여 들어온다.
//   특히 타일 가장자리의 좁은 띠는 폭이 라이트맵 한 칸뿐이라 통째로 섞여서, 바닥에 줄무늬가 생긴다.
//
// 어떻게 고치는가:
//   표시해 둔 메시는 위에서 내려다본 모양 그대로(평평한 면에 수직인 방향으로 눌러서) 한 조각으로 편다.
//   조각이 하나뿐이라 섞일 이웃이 없고, 같은 해상도에서도 빛이 타일 전체에 매끄럽게 이어진다.
//   옆면처럼 수직인 면은 넓이가 0인 선이 되어 라이트맵 칸을 쓰지 않는다(이웃 타일에 가려 보이지 않는 면이다).
//   FBX의 Generate Lightmap UVs는 켜 둔다. Unity가 먼저 자동으로 만든 뒤 이 스크립트가 표시된 메시만 바꿔 쓰고,
//   표시하지 않은 메시는 Unity가 만든 그대로 둔다.
//
// 쓰는 법:
//   씬이나 Project 창에서 바닥 타일을 고르고 메뉴 Tools > Background > Flat Lightmap UV (selected mesh)를 누른다.
//   표시는 FBX의 .meta `userData`에 "FlatLightmapUV=메시이름,메시이름"으로 저장된다. 다시 누르면 표시가 풀린다.
//   표시를 바꾸거나 메시 모양이 바뀌면 조명을 다시 구워야 한다.
public sealed class BackgroundFlatLightmapUv : AssetPostprocessor
{
    private const string MenuPath = "Tools/Background/Flat Lightmap UV (selected mesh)";
    private const string Marker = "FlatLightmapUV=";
    // 이 값을 올리면 Unity가 모든 모델을 다시 임포트한다. 펴는 방식을 바꿨을 때만 올린다.
    private const uint Version = 1;

    // 평평한 방향을 향한 면이 전체 넓이에서 차지해야 하는 최소 비율.
    private const float MinFlatAreaRatio = .7f;
    // 반대쪽(아래)을 향한 면이 있으면 눌러 폈을 때 겹치므로 펴지 않는다. 이 값보다 더 아래를 향하면 반대쪽 면으로 본다.
    private const float BackFaceLimit = -.05f;

    public override uint GetVersion()
    {
        return Version;
    }

    private void OnPostprocessModel(GameObject root)
    {
        HashSet<string> flatNames = ParseMarker(assetImporter.userData);
        if (flatNames.Count == 0)
        {
            return;
        }

        var seen = new HashSet<Mesh>();
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || !seen.Add(mesh) || !flatNames.Contains(mesh.name))
            {
                continue;
            }

            if (!TryFlatten(mesh, out string reason))
            {
                Debug.LogWarning($"{assetPath}의 '{mesh.name}'은(는) 평평한 타일이 아니라서 라이트맵 UV를 한 조각으로 펴지 않았다: {reason}", assetImporter);
            }
        }
    }

    // 메시를 가장 넓은 평면에 수직으로 눌러 UV2를 만든다. 겹치는 면이 생기는 모양이면 false.
    public static bool TryFlatten(Mesh mesh, out string reason)
    {
        if (!TryComputeFlatUv(mesh.vertices, mesh.triangles, mesh.bounds, out Vector2[] uv2, out reason))
        {
            return false;
        }

        mesh.uv2 = uv2;
        return true;
    }

    // 임포트가 끝난 메시(읽기 꺼짐)도 시험할 수 있게, 메시를 바꾸지 않고 펼 수 있는 모양인지만 본다.
    private static bool CanFlatten(Mesh mesh, out string reason)
    {
        using (Mesh.MeshDataArray data = MeshUtility.AcquireReadOnlyMeshData(mesh))
        {
            Mesh.MeshData meshData = data[0];
            using (var positions = new Unity.Collections.NativeArray<Vector3>(meshData.vertexCount, Unity.Collections.Allocator.Temp))
            {
                meshData.GetVertices(positions);
                var triangles = new List<int>();
                for (int subMesh = 0; subMesh < meshData.subMeshCount; subMesh++)
                {
                    using (var indices = new Unity.Collections.NativeArray<int>(meshData.GetSubMesh(subMesh).indexCount, Unity.Collections.Allocator.Temp))
                    {
                        meshData.GetIndices(indices, subMesh);
                        triangles.AddRange(indices);
                    }
                }

                return TryComputeFlatUv(positions.ToArray(), triangles.ToArray(), mesh.bounds, out _, out reason);
            }
        }
    }

    private static bool TryComputeFlatUv(Vector3[] vertices, int[] triangles, Bounds bounds, out Vector2[] uv2, out string reason)
    {
        uv2 = null;
        var areaByAxis = new double[6];
        double totalArea = 0d;
        var normals = new Vector3[triangles.Length / 3];
        var areas = new double[triangles.Length / 3];
        for (int t = 0; t < normals.Length; t++)
        {
            Vector3 a = vertices[triangles[t * 3]];
            Vector3 cross = Vector3.Cross(vertices[triangles[t * 3 + 1]] - a, vertices[triangles[t * 3 + 2]] - a);
            double area = cross.magnitude * .5d;
            normals[t] = area > 1e-12d ? cross.normalized : Vector3.zero;
            areas[t] = area;
            totalArea += area;
            for (int axis = 0; axis < 3; axis++)
            {
                float along = normals[t][axis];
                if (along > .99f)
                {
                    areaByAxis[axis * 2] += area;
                }
                else if (along < -.99f)
                {
                    areaByAxis[axis * 2 + 1] += area;
                }
            }
        }

        int best = 0;
        for (int i = 1; i < 6; i++)
        {
            if (areaByAxis[i] > areaByAxis[best])
            {
                best = i;
            }
        }

        if (totalArea <= 0d || areaByAxis[best] / totalArea < MinFlatAreaRatio)
        {
            reason = $"한 방향을 향한 평평한 면이 전체의 {(totalArea > 0d ? areaByAxis[best] / totalArea * 100d : 0d):F0}%뿐이다";
            return false;
        }

        int upAxis = best / 2;
        float upSign = best % 2 == 0 ? 1f : -1f;
        for (int t = 0; t < normals.Length; t++)
        {
            if (areas[t] > 1e-8d && normals[t][upAxis] * upSign < BackFaceLimit)
            {
                reason = "반대쪽을 향한 면이 있어 눌러 펴면 겹친다";
                return false;
            }
        }

        int uAxis = (upAxis + 1) % 3;
        int vAxis = (upAxis + 2) % 3;
        float size = Mathf.Max(bounds.size[uAxis], bounds.size[vAxis]);
        if (size <= 0f)
        {
            reason = "크기가 0이다";
            return false;
        }

        // 가로세로를 같은 비율로 줄여서 라이트맵 칸이 한쪽으로 늘어나지 않게 한다.
        uv2 = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            uv2[i] = new Vector2(
                Mathf.Clamp01((vertices[i][uAxis] - bounds.min[uAxis]) / size),
                Mathf.Clamp01((vertices[i][vAxis] - bounds.min[vAxis]) / size));
        }

        reason = null;
        return true;
    }

    public static HashSet<string> ParseMarker(string userData)
    {
        var names = new HashSet<string>();
        if (string.IsNullOrEmpty(userData))
        {
            return names;
        }

        foreach (string part in userData.Split('\n', ';'))
        {
            string trimmed = part.Trim();
            if (!trimmed.StartsWith(Marker))
            {
                continue;
            }

            foreach (string name in trimmed.Substring(Marker.Length).Split(','))
            {
                if (name.Trim().Length > 0)
                {
                    names.Add(name.Trim());
                }
            }
        }

        return names;
    }

    // 표시 줄만 바꾸고, userData에 다른 내용이 있으면 그대로 둔다.
    private static string WriteMarker(string userData, HashSet<string> names)
    {
        var parts = new List<string>();
        foreach (string part in (userData ?? string.Empty).Split('\n', ';'))
        {
            if (part.Trim().Length > 0 && !part.Trim().StartsWith(Marker))
            {
                parts.Add(part.Trim());
            }
        }

        if (names.Count > 0)
        {
            var sorted = new List<string>(names);
            sorted.Sort(string.CompareOrdinal);
            parts.Add(Marker + string.Join(",", sorted));
        }

        return string.Join(";", parts);
    }

    // 고른 것들에서 모델 파일(FBX) 안의 메시를 모은다: 모델 경로 → 메시 목록.
    private static Dictionary<string, List<Mesh>> SelectedModelMeshes()
    {
        var result = new Dictionary<string, List<Mesh>>();
        foreach (Object selected in Selection.objects)
        {
            var meshes = new List<Mesh>();
            if (selected is Mesh mesh)
            {
                meshes.Add(mesh);
            }
            else if (selected is GameObject gameObject)
            {
                foreach (MeshFilter filter in gameObject.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh != null)
                    {
                        meshes.Add(filter.sharedMesh);
                    }
                }
            }

            foreach (Mesh found in meshes)
            {
                string path = AssetDatabase.GetAssetPath(found);
                if (string.IsNullOrEmpty(path) || !(AssetImporter.GetAtPath(path) is ModelImporter))
                {
                    continue;
                }

                if (!result.TryGetValue(path, out List<Mesh> list))
                {
                    list = new List<Mesh>();
                    result[path] = list;
                }

                if (!list.Contains(found))
                {
                    list.Add(found);
                }
            }
        }

        return result;
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateToggle()
    {
        Dictionary<string, List<Mesh>> selection = SelectedModelMeshes();
        bool any = false;
        bool allMarked = true;
        foreach (KeyValuePair<string, List<Mesh>> pair in selection)
        {
            HashSet<string> marked = ParseMarker(AssetImporter.GetAtPath(pair.Key).userData);
            foreach (Mesh mesh in pair.Value)
            {
                any = true;
                allMarked &= marked.Contains(mesh.name);
            }
        }

        Menu.SetChecked(MenuPath, any && allMarked);
        return any;
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        Dictionary<string, List<Mesh>> selection = SelectedModelMeshes();
        bool allMarked = true;
        foreach (KeyValuePair<string, List<Mesh>> pair in selection)
        {
            HashSet<string> marked = ParseMarker(AssetImporter.GetAtPath(pair.Key).userData);
            foreach (Mesh mesh in pair.Value)
            {
                allMarked &= marked.Contains(mesh.name);
            }
        }

        bool mark = !allMarked;
        var summary = new StringBuilder();
        int changed = 0;
        foreach (KeyValuePair<string, List<Mesh>> pair in selection)
        {
            AssetImporter importer = AssetImporter.GetAtPath(pair.Key);
            HashSet<string> marked = ParseMarker(importer.userData);
            int before = marked.Count;
            foreach (Mesh mesh in pair.Value)
            {
                if (!mark)
                {
                    marked.Remove(mesh.name);
                    continue;
                }

                // 미리 시험해서, 펼 수 없는 모양이면 표시하지 않고 이유를 알려 준다.
                if (CanFlatten(mesh, out string reason))
                {
                    marked.Add(mesh.name);
                }
                else
                {
                    summary.AppendLine($"건너뜀: {mesh.name} ({reason})");
                }
            }

            if (marked.Count == before)
            {
                continue;
            }

            importer.userData = WriteMarker(importer.userData, marked);
            importer.SaveAndReimport();
            changed += Mathf.Abs(marked.Count - before);
            summary.AppendLine($"{System.IO.Path.GetFileName(pair.Key)}: {(marked.Count > 0 ? string.Join(", ", marked) : "표시 없음")}");
        }

        summary.Insert(0, mark
            ? $"메시 {changed}개의 라이트맵 UV를 한 조각으로 바꿨습니다.\n조명을 다시 구워야 화면에 반영됩니다(Window > Rendering > Lighting > Generate Lighting).\n\n"
            : $"메시 {changed}개의 표시를 풀었습니다. Unity의 자동 라이트맵 UV로 돌아갑니다.\n조명을 다시 구워야 화면에 반영됩니다.\n\n");
        Debug.Log(summary.ToString());
        EditorUtility.DisplayDialog("Flat Lightmap UV", summary.ToString(), "OK");
    }
}
