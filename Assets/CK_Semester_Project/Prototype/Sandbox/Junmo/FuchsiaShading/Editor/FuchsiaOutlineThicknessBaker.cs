using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 외곽선 굵기용 두께를 정점 색 A(0~1)에 굽는다.
// 각 정점에서 법선 반대 방향으로 광선을 쏴서 반대편 면까지의 거리를 재고, 메시별 기준 두께로 정규화한다.
// 열린 씬에서 _OutlineUseThickness가 켜진 재질을 쓰는 렌더러의 메시(.asset 사본)에만 쓴다. 원본 FBX는 건드리지 않는다.
// 주의: ApplyFuchsiaStyleMeshes로 스타일 메시를 다시 만들면 정점 색이 지워지므로 이 메뉴를 다시 실행해야 한다.
public static class FuchsiaOutlineThicknessBaker
{
    private const string MenuPath = "Tools/Fuchsia/Bake Outline Thickness";
    private static readonly int UseThicknessId = Shader.PropertyToID("_OutlineUseThickness");

    // 이 비율보다 두꺼운 부위는 모두 1로 본다(메시 안 두께 분포의 백분위).
    private const float ReferencePercentile = .8f;
    // 반대편 면을 찾지 못한 정점(열린 판 모양)은 얇은 부위로 본다.
    private const float OpenSurfaceThickness = .15f;
    private const int SmoothingIterations = 2;

    [MenuItem(MenuPath)]
    private static void BakeFromMenu()
    {
        string summary = BakeOpenScene();
        Debug.Log(summary);
        EditorUtility.DisplayDialog("Outline Thickness", summary, "OK");
    }

    public static string BakeOpenScene()
    {
        var baked = new HashSet<Mesh>();
        var lines = new List<string>();
        foreach (MeshRenderer renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!UsesThickness(renderer))
            {
                continue;
            }
            var filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || baked.Contains(mesh))
            {
                continue;
            }
            string path = AssetDatabase.GetAssetPath(mesh);
            if (!path.EndsWith(".asset"))
            {
                lines.Add($"건너뜀 {renderer.name}: {mesh.name}은 FBX 내부 메시라 저장할 수 없습니다. 스타일 메시(.asset) 사본을 먼저 만드세요.");
                continue;
            }

            Undo.RecordObject(mesh, "Bake outline thickness");
            // 스타일 메시는 Read/Write가 꺼져 있다. 굽는 동안만 켜고 원래대로 되돌린다.
            bool wasReadable = mesh.isReadable;
            SetReadable(mesh, true);
            float[] thickness = ComputeThickness(mesh, out int openCount);
            Color[] colors = mesh.colors;
            if (colors == null || colors.Length != mesh.vertexCount)
            {
                colors = new Color[mesh.vertexCount];
                for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            }
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i].a = thickness[i];
            }
            mesh.colors = colors;
            SetReadable(mesh, wasReadable);
            EditorUtility.SetDirty(mesh);
            baked.Add(mesh);
            lines.Add($"{mesh.name}: 정점 {mesh.vertexCount}, 열린 면 {openCount}, 평균 두께 {Average(thickness):0.00}");
        }
        AssetDatabase.SaveAssets();
        return lines.Count > 0 ? string.Join("\n", lines)
            : "_OutlineUseThickness가 켜진 재질을 쓰는 렌더러가 열린 씬에 없습니다.";
    }

    private static void SetReadable(Mesh mesh, bool readable)
    {
        if (mesh.isReadable == readable)
        {
            return;
        }
        var serialized = new SerializedObject(mesh);
        serialized.FindProperty("m_IsReadable").boolValue = readable;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool UsesThickness(Renderer renderer)
    {
        foreach (Material material in renderer.sharedMaterials)
        {
            if (material != null && material.HasProperty(UseThicknessId) && material.GetFloat(UseThicknessId) > .5f)
            {
                return true;
            }
        }
        return false;
    }

    private static float[] ComputeThickness(Mesh mesh, out int openCount)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        int[] triangles = mesh.triangles;
        int count = vertices.Length;
        float diagonal = Mathf.Max(mesh.bounds.size.magnitude, 1e-4f);
        float epsilon = diagonal * 1e-4f;

        // UV 이음새로 갈라진 같은 위치의 정점을 하나로 묶는다.
        int[] group = WeldGroups(vertices, diagonal * 1e-5f, out int groupCount);

        var distance = new float[count];
        var hits = new List<float>(count);
        for (int i = 0; i < count; i++)
        {
            Vector3 direction = -normals[i].normalized;
            Vector3 origin = vertices[i] + direction * epsilon;
            float best = float.PositiveInfinity;
            int self = group[i];
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                // 자기 정점이 포함된 인접 삼각형은 제외한다.
                if (group[a] == self || group[b] == self || group[c] == self)
                {
                    continue;
                }
                if (RayTriangle(origin, direction, vertices[a], vertices[b], vertices[c], out float hit)
                    && hit > epsilon && hit < best)
                {
                    best = hit;
                }
            }
            distance[i] = best;
            if (!float.IsInfinity(best))
            {
                hits.Add(best);
            }
        }

        float reference = diagonal * .05f;
        if (hits.Count > 0)
        {
            hits.Sort();
            reference = Mathf.Max(hits[Mathf.Clamp((int)(hits.Count * ReferencePercentile), 0, hits.Count - 1)], epsilon);
        }

        openCount = 0;
        var value = new float[count];
        for (int i = 0; i < count; i++)
        {
            if (float.IsInfinity(distance[i]))
            {
                value[i] = OpenSurfaceThickness;
                openCount++;
            }
            else
            {
                value[i] = Mathf.Clamp01(distance[i] / reference);
            }
        }

        // 이웃 정점과 평균해 광선 한 줄의 잡음을 줄인다.
        var neighbors = new List<int>[groupCount];
        for (int g = 0; g < groupCount; g++) neighbors[g] = new List<int>();
        for (int t = 0; t < triangles.Length; t += 3)
        {
            int a = group[triangles[t]], b = group[triangles[t + 1]], c = group[triangles[t + 2]];
            neighbors[a].Add(b); neighbors[a].Add(c);
            neighbors[b].Add(a); neighbors[b].Add(c);
            neighbors[c].Add(a); neighbors[c].Add(b);
        }
        var groupValue = new float[groupCount];
        var groupMembers = new int[groupCount];
        for (int i = 0; i < count; i++)
        {
            groupValue[group[i]] += value[i];
            groupMembers[group[i]]++;
        }
        for (int g = 0; g < groupCount; g++)
        {
            groupValue[g] /= Mathf.Max(groupMembers[g], 1);
        }
        for (int iteration = 0; iteration < SmoothingIterations; iteration++)
        {
            var next = new float[groupCount];
            for (int g = 0; g < groupCount; g++)
            {
                float sum = 0;
                foreach (int n in neighbors[g]) sum += groupValue[n];
                next[g] = neighbors[g].Count > 0
                    ? groupValue[g] * .5f + sum / neighbors[g].Count * .5f
                    : groupValue[g];
            }
            groupValue = next;
        }
        for (int i = 0; i < count; i++)
        {
            value[i] = groupValue[group[i]];
        }
        return value;
    }

    private static int[] WeldGroups(Vector3[] vertices, float cell, out int groupCount)
    {
        var map = new Dictionary<Vector3Int, int>();
        var group = new int[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 p = vertices[i] / cell;
            var key = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
            if (!map.TryGetValue(key, out int id))
            {
                id = map.Count;
                map.Add(key, id);
            }
            group[i] = id;
        }
        groupCount = map.Count;
        return group;
    }

    // Möller–Trumbore. 양면 모두 판정한다.
    private static bool RayTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c, out float t)
    {
        t = 0;
        Vector3 edge1 = b - a, edge2 = c - a;
        Vector3 p = Vector3.Cross(direction, edge2);
        float determinant = Vector3.Dot(edge1, p);
        if (Mathf.Abs(determinant) < 1e-12f) return false;
        float inverse = 1f / determinant;
        Vector3 s = origin - a;
        float u = Vector3.Dot(s, p) * inverse;
        if (u < 0f || u > 1f) return false;
        Vector3 q = Vector3.Cross(s, edge1);
        float v = Vector3.Dot(direction, q) * inverse;
        if (v < 0f || u + v > 1f) return false;
        t = Vector3.Dot(edge2, q) * inverse;
        return t > 0f;
    }

    private static float Average(float[] values)
    {
        float sum = 0;
        foreach (float v in values) sum += v;
        return values.Length > 0 ? sum / values.Length : 0;
    }
}
