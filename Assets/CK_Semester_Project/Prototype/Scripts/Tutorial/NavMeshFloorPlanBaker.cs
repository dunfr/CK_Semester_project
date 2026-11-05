using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace CK.SemesterProject.Tutorial
{
    /// <summary>
    /// 한 층의 NavMesh(걸을 수 있는 바닥)를 위에서 본 평면도 텍스처로 굽는다.
    /// 텍스처에는 바닥 경계까지의 거리가 들어 있다(0.5 = 경계, 작을수록 바닥 안쪽, 클수록 바깥).
    /// 셰이더가 이 값으로 벽선·빗금·바닥을 지도 배율과 상관없이 또렷하게 그린다.
    /// </summary>
    public sealed class NavMeshFloorPlanBaker
    {
        // NavMesh 타일 경계에서 생기는 가짜 경계선을 거를 때 쓰는 값(m).
        private const float PieceLength = 0.25f;
        private const float NudgeDistance = 0.04f;
        private const float CellSize = 4f;

        public NavMeshFloorPlanBaker(float range)
        {
            Range = Mathf.Max(0.5f, range);
        }

        /// <summary>텍스처에 담는 최대 거리(m). 0과 1이 각각 -Range, +Range다.</summary>
        public float Range { get; }
        public Texture2D Texture { get; private set; }
        /// <summary>텍스처가 덮는 월드 범위(최소 X, 최소 Z, 너비, 깊이).</summary>
        public Vector4 WorldRect { get; private set; }
        public float FloorY { get; private set; }
        public bool HasPlan => Texture != null;

        /// <summary>
        /// floorY 높이의 층을 굽는다. 프레임마다 frameBudgetMs만큼만 일하고 넘기므로 코루틴으로 돌린다.
        /// 다 구운 뒤에만 텍스처를 바꾸므로 굽는 동안에는 이전 평면도가 그대로 보인다.
        /// </summary>
        public IEnumerator Bake(float floorY, float below, float above, float pixelsPerMeter, int maxSize, float frameBudgetMs)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            NavMeshTriangulation navMesh = NavMesh.CalculateTriangulation();
            Vector3[] vertices = navMesh.vertices;
            int[] indices = navMesh.indices;

            var triangles = new List<int>();
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                Vector3 a = vertices[indices[t]], b = vertices[indices[t + 1]], c = vertices[indices[t + 2]];
                float low = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
                float high = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                if (high < floorY - below || low > floorY + above)
                {
                    continue;
                }
                triangles.Add(t);
                minX = Mathf.Min(minX, Mathf.Min(a.x, Mathf.Min(b.x, c.x)));
                maxX = Mathf.Max(maxX, Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
                minZ = Mathf.Min(minZ, Mathf.Min(a.z, Mathf.Min(b.z, c.z)));
                maxZ = Mathf.Max(maxZ, Mathf.Max(a.z, Mathf.Max(b.z, c.z)));
            }
            if (triangles.Count == 0)
            {
                yield break;
            }

            float margin = Range + 1f;
            minX -= margin;
            minZ -= margin;
            maxX += margin;
            maxZ += margin;
            float ppm = Mathf.Min(pixelsPerMeter, maxSize / Mathf.Max(maxX - minX, maxZ - minZ));
            int width = Mathf.Max(4, Mathf.CeilToInt((maxX - minX) * ppm));
            int height = Mathf.Max(4, Mathf.CeilToInt((maxZ - minZ) * ppm));
            float texel = 1f / ppm;

            // 1. 텍셀 중심이 바닥 삼각형 안에 있는지 표시한다.
            var inside = new bool[width * height];
            foreach (int t in triangles)
            {
                FillTriangle(inside, width, height, minX, minZ, ppm,
                    vertices[indices[t]], vertices[indices[t + 1]], vertices[indices[t + 2]]);
                if (watch.Elapsed.TotalMilliseconds > frameBudgetMs)
                {
                    yield return null;
                    watch.Restart();
                }
            }

            // 2. 한 삼각형에만 속한 변이 바닥 경계다. 이웃 타일 삼각형이 덮는 조각(가짜 경계)은 뺀다.
            var cells = new Dictionary<long, List<int>>();
            foreach (int t in triangles)
            {
                AddToCells(cells, t, vertices[indices[t]], vertices[indices[t + 1]], vertices[indices[t + 2]]);
            }
            var edges = new Dictionary<long, Edge>();
            var vertexIds = new Dictionary<Vector3Int, int>();
            foreach (int t in triangles)
            {
                int a = VertexId(vertexIds, vertices[indices[t]]);
                int b = VertexId(vertexIds, vertices[indices[t + 1]]);
                int c = VertexId(vertexIds, vertices[indices[t + 2]]);
                CountEdge(edges, a, b, vertices[indices[t]], vertices[indices[t + 1]], vertices[indices[t + 2]]);
                CountEdge(edges, b, c, vertices[indices[t + 1]], vertices[indices[t + 2]], vertices[indices[t]]);
                CountEdge(edges, c, a, vertices[indices[t + 2]], vertices[indices[t]], vertices[indices[t + 1]]);
            }
            var segments = new List<Vector4>();
            foreach (Edge edge in edges.Values)
            {
                if (edge.Count == 1)
                {
                    AddBoundaryPieces(segments, edge, cells, triangles, vertices, indices);
                }
                if (watch.Elapsed.TotalMilliseconds > frameBudgetMs)
                {
                    yield return null;
                    watch.Restart();
                }
            }

            // 3. 경계선까지의 거리를 Range 안에서만 정확히 잰다.
            var distance = new float[width * height];
            for (int i = 0; i < distance.Length; i++)
            {
                distance[i] = Range;
            }
            foreach (Vector4 segment in segments)
            {
                StampSegment(distance, width, height, minX, minZ, ppm, texel, segment);
                if (watch.Elapsed.TotalMilliseconds > frameBudgetMs)
                {
                    yield return null;
                    watch.Restart();
                }
            }

            // 4. 바닥 안쪽은 음수로 바꿔 0~255에 담는다.
            var bytes = new byte[width * height];
            float scale = 127.5f / Range;
            for (int i = 0; i < bytes.Length; i++)
            {
                float signedDistance = inside[i] ? -distance[i] : distance[i];
                bytes[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(127.5f + signedDistance * scale), 0, 255);
            }
            var texture = new Texture2D(width, height, TextureFormat.R8, false, true)
            {
                name = "Minimap Floor Plan",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixelData(bytes, 0);
            texture.Apply(false, true);

            Release();
            Texture = texture;
            WorldRect = new Vector4(minX, minZ, width * texel, height * texel);
            FloorY = floorY;
        }

        public void Release()
        {
            if (Texture == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Object.Destroy(Texture);
            }
            else
            {
                Object.DestroyImmediate(Texture);
            }
            Texture = null;
        }

        private struct Edge
        {
            public int Count;
            public Vector3 A;
            public Vector3 B;
            public Vector3 Opposite;
        }

        private static int VertexId(Dictionary<Vector3Int, int> ids, Vector3 position)
        {
            var key = new Vector3Int(Mathf.RoundToInt(position.x * 50f), Mathf.RoundToInt(position.y * 20f),
                Mathf.RoundToInt(position.z * 50f));
            if (!ids.TryGetValue(key, out int id))
            {
                id = ids.Count;
                ids.Add(key, id);
            }
            return id;
        }

        private static void CountEdge(Dictionary<long, Edge> edges, int a, int b, Vector3 from, Vector3 to, Vector3 opposite)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (edges.TryGetValue(key, out Edge edge))
            {
                edge.Count++;
                edges[key] = edge;
                return;
            }
            edges.Add(key, new Edge { Count = 1, A = from, B = to, Opposite = opposite });
        }

        private static long CellKey(int x, int z)
        {
            return ((long)x << 32) | (uint)z;
        }

        private static void AddToCells(Dictionary<long, List<int>> cells, int triangle, Vector3 a, Vector3 b, Vector3 c)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) / CellSize);
            int x1 = Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) / CellSize);
            int z0 = Mathf.FloorToInt(Mathf.Min(a.z, Mathf.Min(b.z, c.z)) / CellSize);
            int z1 = Mathf.FloorToInt(Mathf.Max(a.z, Mathf.Max(b.z, c.z)) / CellSize);
            for (int x = x0; x <= x1; x++)
            {
                for (int z = z0; z <= z1; z++)
                {
                    long key = CellKey(x, z);
                    if (!cells.TryGetValue(key, out List<int> list))
                    {
                        list = new List<int>();
                        cells.Add(key, list);
                    }
                    list.Add(triangle);
                }
            }
        }

        private static void AddBoundaryPieces(List<Vector4> segments, Edge edge, Dictionary<long, List<int>> cells,
            List<int> triangles, Vector3[] vertices, int[] indices)
        {
            var a = new Vector2(edge.A.x, edge.A.z);
            var b = new Vector2(edge.B.x, edge.B.z);
            Vector2 direction = b - a;
            float length = direction.magnitude;
            if (length < 0.001f)
            {
                return;
            }
            var normal = new Vector2(-direction.y, direction.x) / length;
            if (Vector2.Dot(normal, new Vector2(edge.Opposite.x, edge.Opposite.z) - a) > 0f)
            {
                normal = -normal;
            }
            float edgeY = (edge.A.y + edge.B.y) * 0.5f;
            int pieces = Mathf.Max(1, Mathf.CeilToInt(length / PieceLength));
            int runStart = -1;
            for (int i = 0; i <= pieces; i++)
            {
                bool isBoundary = false;
                if (i < pieces)
                {
                    Vector2 middle = a + direction * ((i + 0.5f) / pieces);
                    isBoundary = !IsCovered(middle + normal * NudgeDistance, edgeY, cells, vertices, indices);
                }
                if (isBoundary && runStart < 0)
                {
                    runStart = i;
                }
                else if (!isBoundary && runStart >= 0)
                {
                    Vector2 start = a + direction * ((float)runStart / pieces);
                    Vector2 end = a + direction * ((float)i / pieces);
                    segments.Add(new Vector4(start.x, start.y, end.x, end.y));
                    runStart = -1;
                }
            }
        }

        private static bool IsCovered(Vector2 point, float y, Dictionary<long, List<int>> cells, Vector3[] vertices, int[] indices)
        {
            long key = CellKey(Mathf.FloorToInt(point.x / CellSize), Mathf.FloorToInt(point.y / CellSize));
            if (!cells.TryGetValue(key, out List<int> list))
            {
                return false;
            }
            foreach (int t in list)
            {
                Vector3 a = vertices[indices[t]], b = vertices[indices[t + 1]], c = vertices[indices[t + 2]];
                float low = Mathf.Min(a.y, Mathf.Min(b.y, c.y)) - 0.75f;
                float high = Mathf.Max(a.y, Mathf.Max(b.y, c.y)) + 0.75f;
                if (y < low || y > high)
                {
                    continue;
                }
                if (ContainsXZ(point, a, b, c))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ContainsXZ(Vector2 p, Vector3 a, Vector3 b, Vector3 c)
        {
            float d0 = (b.x - a.x) * (p.y - a.z) - (b.z - a.z) * (p.x - a.x);
            float d1 = (c.x - b.x) * (p.y - b.z) - (c.z - b.z) * (p.x - b.x);
            float d2 = (a.x - c.x) * (p.y - c.z) - (a.z - c.z) * (p.x - c.x);
            bool hasNegative = d0 < 0f || d1 < 0f || d2 < 0f;
            bool hasPositive = d0 > 0f || d1 > 0f || d2 > 0f;
            return !(hasNegative && hasPositive);
        }

        private static void FillTriangle(bool[] inside, int width, int height, float minX, float minZ, float ppm,
            Vector3 a, Vector3 b, Vector3 c)
        {
            // 텍셀 중심이 정수 좌표가 되도록 옮긴다.
            float ax = (a.x - minX) * ppm - 0.5f, az = (a.z - minZ) * ppm - 0.5f;
            float bx = (b.x - minX) * ppm - 0.5f, bz = (b.z - minZ) * ppm - 0.5f;
            float cx = (c.x - minX) * ppm - 0.5f, cz = (c.z - minZ) * ppm - 0.5f;
            float area = (bx - ax) * (cz - az) - (bz - az) * (cx - ax);
            if (Mathf.Abs(area) < 1e-6f)
            {
                return;
            }
            float sign = area > 0f ? 1f : -1f;
            int i0 = Mathf.Max(0, Mathf.CeilToInt(Mathf.Min(ax, Mathf.Min(bx, cx))));
            int i1 = Mathf.Min(width - 1, Mathf.FloorToInt(Mathf.Max(ax, Mathf.Max(bx, cx))));
            int j0 = Mathf.Max(0, Mathf.CeilToInt(Mathf.Min(az, Mathf.Min(bz, cz))));
            int j1 = Mathf.Min(height - 1, Mathf.FloorToInt(Mathf.Max(az, Mathf.Max(bz, cz))));
            const float epsilon = -1e-4f;
            for (int j = j0; j <= j1; j++)
            {
                for (int i = i0; i <= i1; i++)
                {
                    float e0 = ((bx - ax) * (j - az) - (bz - az) * (i - ax)) * sign;
                    float e1 = ((cx - bx) * (j - bz) - (cz - bz) * (i - bx)) * sign;
                    float e2 = ((ax - cx) * (j - cz) - (az - cz) * (i - cx)) * sign;
                    if (e0 >= epsilon && e1 >= epsilon && e2 >= epsilon)
                    {
                        inside[j * width + i] = true;
                    }
                }
            }
        }

        private void StampSegment(float[] distance, int width, int height, float minX, float minZ, float ppm, float texel,
            Vector4 segment)
        {
            float ax = segment.x, az = segment.y, bx = segment.z, bz = segment.w;
            float dx = bx - ax, dz = bz - az;
            float lengthSquared = Mathf.Max(dx * dx + dz * dz, 1e-8f);
            int i0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(ax, bx) - Range - minX) * ppm));
            int i1 = Mathf.Min(width - 1, Mathf.CeilToInt((Mathf.Max(ax, bx) + Range - minX) * ppm));
            int j0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(az, bz) - Range - minZ) * ppm));
            int j1 = Mathf.Min(height - 1, Mathf.CeilToInt((Mathf.Max(az, bz) + Range - minZ) * ppm));
            for (int j = j0; j <= j1; j++)
            {
                float pz = minZ + (j + 0.5f) * texel;
                int row = j * width;
                for (int i = i0; i <= i1; i++)
                {
                    float px = minX + (i + 0.5f) * texel;
                    float t = Mathf.Clamp01(((px - ax) * dx + (pz - az) * dz) / lengthSquared);
                    float ex = px - (ax + dx * t);
                    float ez = pz - (az + dz * t);
                    float d = Mathf.Sqrt(ex * ex + ez * ez);
                    if (d < distance[row + i])
                    {
                        distance[row + i] = d;
                    }
                }
            }
        }
    }
}
