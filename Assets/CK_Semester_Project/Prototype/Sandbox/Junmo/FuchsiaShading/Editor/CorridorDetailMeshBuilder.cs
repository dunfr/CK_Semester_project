using System.Collections.Generic;
using UnityEngine;

public static class CorridorDetailMeshBuilder
{
    public static Mesh BuildBeveledBox(Vector3 size, float bevel)
    {
        Vector3 half = size * 0.5f;
        bevel = Mathf.Clamp(bevel, 0.001f, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.45f);
        Vector3 inner = half - Vector3.one * bevel;
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var coordinates = new List<Vector2>();
        var triangles = new List<int>();

        for (int axis = 0; axis < 3; axis++)
        {
            int next = (axis + 1) % 3;
            int last = (axis + 2) % 3;
            foreach (int sign in new[] { -1, 1 })
            {
                var points = new Vector3[4];
                int[] nextSigns = { -1, 1, 1, -1 };
                int[] lastSigns = { -1, -1, 1, 1 };
                for (int point = 0; point < 4; point++)
                {
                    points[point][axis] = sign * half[axis];
                    points[point][next] = nextSigns[point] * inner[next];
                    points[point][last] = lastSigns[point] * inner[last];
                }

                Vector3 normal = Vector3.zero;
                normal[axis] = sign;
                AddFace(points, normal, size, vertices, normals, coordinates, triangles);
            }
        }

        for (int first = 0; first < 3; first++)
        {
            for (int second = first + 1; second < 3; second++)
            {
                int remaining = 3 - first - second;
                foreach (int firstSign in new[] { -1, 1 })
                {
                    foreach (int secondSign in new[] { -1, 1 })
                    {
                        var points = new Vector3[4];
                        for (int point = 0; point < 4; point++)
                        {
                            bool firstOuter = point == 0 || point == 3;
                            points[point][first] = firstSign * (firstOuter ? half[first] : inner[first]);
                            points[point][second] = secondSign * (firstOuter ? inner[second] : half[second]);
                            points[point][remaining] = (point < 2 ? -1 : 1) * inner[remaining];
                        }

                        Vector3 normal = Vector3.zero;
                        normal[first] = firstSign;
                        normal[second] = secondSign;
                        AddFace(points, normal.normalized, size, vertices, normals, coordinates, triangles);
                    }
                }
            }
        }

        foreach (int x in new[] { -1, 1 })
        {
            foreach (int y in new[] { -1, 1 })
            {
                foreach (int z in new[] { -1, 1 })
                {
                    var points = new[]
                    {
                        new Vector3(x * half.x, y * inner.y, z * inner.z),
                        new Vector3(x * inner.x, y * half.y, z * inner.z),
                        new Vector3(x * inner.x, y * inner.y, z * half.z)
                    };
                    AddFace(points, new Vector3(x, y, z).normalized, size, vertices, normals, coordinates, triangles);
                }
            }
        }

        var mesh = new Mesh
        {
            name = "CorridorBeveledBox"
        };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, coordinates);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        // 배경은 정적 오브젝트로 간접광을 굽는다. 라이트맵 UV(UV1)를 같이 만든다.
        UnityEditor.Unwrapping.GenerateSecondaryUVSet(mesh);
        return mesh;
    }

    private static void AddFace(Vector3[] points, Vector3 normal, Vector3 size,
        List<Vector3> vertices, List<Vector3> normals, List<Vector2> coordinates, List<int> triangles)
    {
        int start = vertices.Count;
        int axis = Mathf.Abs(normal.x) > Mathf.Abs(normal.y) ? 0 : 1;
        axis = Mathf.Abs(normal[axis]) > Mathf.Abs(normal.z) ? axis : 2;
        int next = (axis + 1) % 3;
        int last = (axis + 2) % 3;
        foreach (Vector3 point in points)
        {
            vertices.Add(point);
            normals.Add(normal);
            coordinates.Add(new Vector2(point[next] / size[next] + 0.5f, point[last] / size[last] + 0.5f));
        }

        bool forward = Vector3.Dot(Vector3.Cross(points[1] - points[0], points[2] - points[0]), normal) > 0;
        for (int index = 1; index < points.Length - 1; index++)
        {
            triangles.Add(start);
            triangles.Add(start + (forward ? index : index + 1));
            triangles.Add(start + (forward ? index + 1 : index));
        }
    }
}
