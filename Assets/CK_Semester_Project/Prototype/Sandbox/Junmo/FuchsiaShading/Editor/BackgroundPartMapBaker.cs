using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 배경 모듈 메시에서 "부위 맵"을 굽는다. 모델러의 FBX·텍스처는 읽기만 하고, 같은 UV에 맞는 보조 텍스처를 새로 만든다.
// 부위 = 매끈하게 이어진 면 묶음(모델러가 꺾이는 모서리로 나눠 둔 판·테두리·모서리 면 하나하나).
//   R: 부위 번호(1~255). 셰이더가 부위마다 매끈함을 조금씩 다르게 줄 때 쓴다.
//   G: 모서리 면 정도(폭이 좁은 면일수록 1). 베벨·얇은 테두리에 광택을 줄 때 쓴다.
//   B: 부위 가장자리에서 안쪽으로 들어간 정도(0 = 가장자리, 1 = 안쪽).
// 열린 씬에서 Semester/Background/Stylized Lit 재질을 쓰는 렌더러를 찾아, 러프니스 맵(Metallic 슬롯)이 같은 재질끼리
// 한 장씩 굽고 그 재질들의 Part map 슬롯에 넣는다. 모듈 메시나 UV가 바뀌면 이 메뉴를 다시 실행한다.
public static class BackgroundPartMapBaker
{
    private const string MenuPath = "Tools/Background/Bake Part Maps (open scene)";
    private const string ShaderName = "Semester/Background/Stylized Lit";
    private const string OutputFolder = "Assets/CK_Semester_Project/Graphics/Environment/ModuleMaterials/PartMaps";
    private static readonly int SurfaceMapId = Shader.PropertyToID("_MetallicGlossMap");
    private static readonly int PartMapId = Shader.PropertyToID("_StylePartMap");

    private const int MapSize = 2048;
    // 면의 폭(= 넓이 × 2 ÷ 둘레, m)이 이 값 이하면 완전한 모서리 면으로 본다.
    private const float BevelFullWidth = .035f;
    // 폭이 이 값 이상이면 모서리 면이 아니다. 두 값 사이는 부드럽게 줄어든다.
    private const float BevelZeroWidth = .07f;
    // 가장자리에서 이 거리(m)만큼 들어가면 완전히 안쪽(B = 1)으로 본다.
    private const float InsideDistance = .25f;
    // UV 조각 바깥 여백을 채우는 횟수(픽셀). 밉맵에서 빈 색이 번지지 않게 한다.
    private const int DilatePasses = 12;
    // 삼각형 가장자리에서 이만큼(픽셀) 벗어난 픽셀 중심까지 칠한다. 조각 사이에 빈 줄이 생기지 않게 한다.
    private const float RasterTolerance = .45f;
    // 좁은 띠 합치기: 바닥 타일 가장자리처럼 홈 하나를 사이에 두고 넓은 판 옆에 붙은 좁은 면은 그 판과 같은 부위로 본다.
    // 따로 두면 띠만 매끈함이 달라져서, 타일 경계를 따라 줄무늬가 생긴다.
    // 폭이 이 값(m)보다 좁은 면만 합친다(모서리 면보다 넓고, 판보다는 좁은 면).
    private const float StripMaxWidth = .5f;
    // 띠의 둘레와 판의 둘레가 이 거리(m) 안에서 나란히 가야 한다(홈 하나의 폭).
    private const float StripGap = .05f;
    // 띠 둘레 가운데 판과 나란히 가는 길이가 이 비율 이상이어야 한다(긴 변 하나가 통째로 붙어 있는 정도).
    private const float StripMinShare = .3f;
    // 같은 평면으로 보는 높이 차(m)와 법선 차(코사인).
    private const float StripPlaneOffset = .02f;
    private const float StripPlaneCos = .999f;

    private sealed class Region
    {
        public double Area;       // 메시 공간 넓이(m²)
        public double UvArea;     // UV 공간 넓이
        public double Perimeter;  // 꺾이는 모서리로 이루어진 둘레(m)
        public Vector3 Sum;       // 넓이 가중 중심 합
        public Vector3 NormalSum; // 넓이 가중 법선 합
        public byte Id;           // 부위 번호(1~255)
        public byte Bevel;        // 모서리 면 정도(0~255)
        public float TexelMeters; // 텍셀 하나의 실제 길이(m)
        public float Width;       // 면의 폭(m)
        public readonly List<Vector3> Boundary = new List<Vector3>(); // 둘레 선분(두 점씩)
    }

    private sealed class Group
    {
        public Texture SurfaceMap;
        public readonly List<Material> Materials = new List<Material>();
        public readonly List<KeyValuePair<Mesh, int>> Meshes = new List<KeyValuePair<Mesh, int>>();
        public readonly HashSet<(Mesh, int)> Seen = new HashSet<(Mesh, int)>();
    }

    [MenuItem(MenuPath)]
    private static void BakeFromMenu()
    {
        string summary = BakeOpenScene();
        Debug.Log(summary);
        EditorUtility.DisplayDialog("Part Maps", summary, "OK");
    }

    public static string BakeOpenScene()
    {
        var groups = new Dictionary<Texture, Group>();
        foreach (MeshRenderer renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                continue;
            }
            Mesh mesh = filter.sharedMesh;
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null || material.shader == null || material.shader.name != ShaderName
                    || !material.HasProperty(SurfaceMapId))
                {
                    continue;
                }
                Texture surface = material.GetTexture(SurfaceMapId);
                if (surface == null)
                {
                    continue;
                }
                if (!groups.TryGetValue(surface, out Group group))
                {
                    group = new Group { SurfaceMap = surface };
                    groups.Add(surface, group);
                }
                if (!group.Materials.Contains(material))
                {
                    group.Materials.Add(material);
                }
                int subMesh = Mathf.Min(i, mesh.subMeshCount - 1);
                if (group.Seen.Add((mesh, subMesh)))
                {
                    group.Meshes.Add(new KeyValuePair<Mesh, int>(mesh, subMesh));
                }
            }
        }

        if (groups.Count == 0)
        {
            return "열린 씬에 " + ShaderName + " 재질(러프니스 맵 포함)을 쓰는 렌더러가 없습니다.";
        }

        Directory.CreateDirectory(OutputFolder);
        var summary = new StringBuilder();
        foreach (Group group in groups.Values)
        {
            // 굽는 순서가 실행할 때마다 같도록 이름순으로 정렬한다.
            group.Meshes.Sort((a, b) =>
            {
                int byName = string.CompareOrdinal(a.Key.name, b.Key.name);
                return byName != 0 ? byName : a.Value.CompareTo(b.Value);
            });
            string baseName = group.SurfaceMap.name.Replace("_MetallicSmoothness", string.Empty);
            string path = OutputFolder + "/" + baseName + "_PartMap.png";
            summary.AppendLine(BakeGroup(group, path));
        }
        return summary.ToString().TrimEnd();
    }

    private static string BakeGroup(Group group, string path)
    {
        int size = MapSize;
        var owner = new int[size * size];          // 0 = 빈 곳, 그 밖은 부위 번호 + 1
        var cover = new float[size * size];        // 지금 주인 삼각형 안쪽으로 들어온 깊이(픽셀). 더 깊이 덮는 쪽이 이긴다.
        var edgeSeed = new bool[size * size];      // 꺾이는 모서리(부위 경계)가 지나는 픽셀
        var regions = new List<Region>();
        int skippedMeshes = 0;
        foreach (KeyValuePair<Mesh, int> entry in group.Meshes)
        {
            if (!AddMesh(entry.Key, entry.Value, size, owner, cover, edgeSeed, regions))
            {
                skippedMeshes++;
            }
        }

        float[] distance = InteriorDistance(size, owner, edgeSeed);
        var pixels = new Color32[size * size];
        var filled = new bool[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            if (owner[i] == 0)
            {
                pixels[i] = new Color32(0, 0, 0, 255);
                continue;
            }
            Region region = regions[owner[i] - 1];
            float inside = Mathf.Clamp01(distance[i] * region.TexelMeters / InsideDistance);
            pixels[i] = new Color32(region.Id, region.Bevel, (byte)Mathf.RoundToInt(inside * 255f), 255);
            filled[i] = true;
        }
        Dilate(size, pixels, filled);

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;                       // 색이 아니라 수치이므로 색 공간 변환을 끈다.
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;  // 압축하면 부위 번호가 흐트러진다.
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = MapSize;
        importer.anisoLevel = 4;
        importer.SaveAndReimport();

        var baked = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var names = new List<string>();
        foreach (Material material in group.Materials)
        {
            material.SetTexture(PartMapId, baked);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            names.Add(material.name);
        }

        double totalArea = 0, bevelArea = 0;
        var widthShare = new double[5];   // 폭 구간별 넓이: <3.5 cm, <7 cm, <20 cm, <60 cm, 그 이상
        foreach (Region region in regions)
        {
            totalArea += region.Area;
            bevelArea += region.Area * region.Bevel / 255.0;
            int bucket = region.Width < BevelFullWidth ? 0 : region.Width < BevelZeroWidth ? 1
                : region.Width < .2f ? 2 : region.Width < .6f ? 3 : 4;
            widthShare[bucket] += region.Area;
        }
        totalArea = System.Math.Max(totalArea, 1e-6);
        return $"{Path.GetFileName(path)}: 메시 {group.Meshes.Count}개(건너뜀 {skippedMeshes}), 부위 {regions.Count}개, "
            + $"모서리 면 넓이 {bevelArea / totalArea * 100.0:F1}%, 폭 구간별 넓이 "
            + $"[<3.5cm {widthShare[0] / totalArea * 100.0:F1}% | <7cm {widthShare[1] / totalArea * 100.0:F1}% | "
            + $"<20cm {widthShare[2] / totalArea * 100.0:F1}% | <60cm {widthShare[3] / totalArea * 100.0:F1}% | "
            + $"그 이상 {widthShare[4] / totalArea * 100.0:F1}%] → 재질 {string.Join(", ", names)}";
    }

    // 메시 하나를 부위로 나누고 UV 위치에 칠한다. UV나 법선이 없으면 false.
    private static bool AddMesh(Mesh mesh, int subMesh, int size, int[] owner, float[] cover, bool[] edgeSeed, List<Region> regions)
    {
        // Read/Write가 꺼진 메시도 에디터에서는 읽을 수 있다.
        using (Mesh.MeshDataArray dataArray = MeshUtility.AcquireReadOnlyMeshData(mesh))
        {
            Mesh.MeshData data = dataArray[0];
            if (!data.HasVertexAttribute(VertexAttribute.TexCoord0) || !data.HasVertexAttribute(VertexAttribute.Normal)
                || subMesh >= data.subMeshCount)
            {
                return false;
            }
            SubMeshDescriptor descriptor = data.GetSubMesh(subMesh);
            if (descriptor.topology != MeshTopology.Triangles || descriptor.indexCount < 3)
            {
                return false;
            }
            int vertexCount = data.vertexCount;
            var positionArray = new NativeArray<Vector3>(vertexCount, Allocator.Temp);
            var normalArray = new NativeArray<Vector3>(vertexCount, Allocator.Temp);
            var uvArray = new NativeArray<Vector2>(vertexCount, Allocator.Temp);
            var indexArray = new NativeArray<int>(descriptor.indexCount, Allocator.Temp);
            data.GetVertices(positionArray);
            data.GetNormals(normalArray);
            data.GetUVs(0, uvArray);
            data.GetIndices(indexArray, subMesh);
            Vector3[] positions = positionArray.ToArray();
            Vector3[] normals = normalArray.ToArray();
            Vector2[] uvs = uvArray.ToArray();
            int[] indices = indexArray.ToArray();
            positionArray.Dispose();
            normalArray.Dispose();
            uvArray.Dispose();
            indexArray.Dispose();

            // 위치와 법선이 같은 정점은 하나로 본다(UV 이음매로 갈라진 정점을 다시 붙인다).
            // 꺾이는 모서리는 양쪽 면의 법선이 달라 정점이 따로 남으므로, 거기서 부위가 나뉜다.
            var weldLookup = new Dictionary<(int, int, int, int, int, int), int>();
            var weld = new int[vertexCount];
            for (int v = 0; v < vertexCount; v++)
            {
                Vector3 p = positions[v];
                Vector3 n = normals[v];
                var key = (Mathf.RoundToInt(p.x * 2000f), Mathf.RoundToInt(p.y * 2000f), Mathf.RoundToInt(p.z * 2000f),
                    Mathf.RoundToInt(n.x * 12f), Mathf.RoundToInt(n.y * 12f), Mathf.RoundToInt(n.z * 12f));
                if (!weldLookup.TryGetValue(key, out int id))
                {
                    id = weldLookup.Count;
                    weldLookup.Add(key, id);
                }
                weld[v] = id;
            }

            int triangleCount = indices.Length / 3;
            var parent = new int[triangleCount];
            for (int t = 0; t < triangleCount; t++)
            {
                parent[t] = t;
            }
            var edgeOwner = new Dictionary<long, int>(triangleCount * 3);
            var edgeUse = new Dictionary<long, int>(triangleCount * 3);
            for (int t = 0; t < triangleCount; t++)
            {
                for (int e = 0; e < 3; e++)
                {
                    int a = weld[indices[t * 3 + e]];
                    int b = weld[indices[t * 3 + (e + 1) % 3]];
                    if (a == b)
                    {
                        continue;
                    }
                    long key = EdgeKey(a, b);
                    if (edgeOwner.TryGetValue(key, out int other))
                    {
                        Union(parent, t, other);
                        edgeUse[key] = edgeUse[key] + 1;
                    }
                    else
                    {
                        edgeOwner.Add(key, t);
                        edgeUse.Add(key, 1);
                    }
                }
            }

            var regionOfRoot = new Dictionary<int, int>();
            var regionOfTriangle = new int[triangleCount];
            int firstRegion = regions.Count;
            for (int t = 0; t < triangleCount; t++)
            {
                int root = Find(parent, t);
                if (!regionOfRoot.TryGetValue(root, out int regionIndex))
                {
                    regionIndex = regions.Count;
                    regions.Add(new Region());
                    regionOfRoot.Add(root, regionIndex);
                }
                regionOfTriangle[t] = regionIndex;
                Region region = regions[regionIndex];
                int i0 = indices[t * 3], i1 = indices[t * 3 + 1], i2 = indices[t * 3 + 2];
                Vector3 p0 = positions[i0], p1 = positions[i1], p2 = positions[i2];
                double area = Vector3.Cross(p1 - p0, p2 - p0).magnitude * .5;
                region.Area += area;
                region.UvArea += Mathf.Abs(Cross(uvs[i1] - uvs[i0], uvs[i2] - uvs[i0])) * .5;
                region.Sum += (p0 + p1 + p2) * (float)(area / 3.0);
                region.NormalSum += Vector3.Cross(p1 - p0, p2 - p0) * .5f;
                for (int e = 0; e < 3; e++)
                {
                    int va = indices[t * 3 + e], vb = indices[t * 3 + (e + 1) % 3];
                    int a = weld[va], b = weld[vb];
                    // 한 삼각형만 쓰는 변 = 꺾이는 모서리이거나 메시의 끝 = 부위의 둘레.
                    if (a != b && edgeUse[EdgeKey(a, b)] == 1)
                    {
                        region.Perimeter += Vector3.Distance(positions[va], positions[vb]);
                        region.Boundary.Add(positions[va]);
                        region.Boundary.Add(positions[vb]);
                    }
                }
            }

            for (int r = firstRegion; r < regions.Count; r++)
            {
                Region region = regions[r];
                region.Width = region.Perimeter > 1e-5 ? (float)(2.0 * region.Area / region.Perimeter) : 1000f;
                region.Bevel = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01((BevelZeroWidth - region.Width) / (BevelZeroWidth - BevelFullWidth)));
                region.TexelMeters = region.UvArea > 1e-12 ? (float)System.Math.Sqrt(region.Area / region.UvArea) / size : 0f;
                Vector3 centre = region.Area > 1e-9 ? region.Sum / (float)region.Area : Vector3.zero;
                // 다시 구워도 같은 번호가 나오도록 부위의 위치와 넓이로 번호를 정한다(1~255, 0은 빈 곳).
                uint hash = Hash((uint)Mathf.RoundToInt(centre.x * 100f), (uint)Mathf.RoundToInt(centre.y * 100f),
                    (uint)Mathf.RoundToInt(centre.z * 100f), (uint)System.Math.Round(region.Area * 10000.0));
                region.Id = (byte)(1 + hash % 255u);
            }
            MergeNarrowStrips(regions, firstRegion);

            for (int t = 0; t < triangleCount; t++)
            {
                int i0 = indices[t * 3], i1 = indices[t * 3 + 1], i2 = indices[t * 3 + 2];
                Vector2 a = uvs[i0], b = uvs[i1], c = uvs[i2];
                // 0~1 밖으로 나간 UV는 타일 반복으로 보고 제자리로 옮긴다.
                Vector2 centre = (a + b + c) / 3f;
                var shift = new Vector2(Mathf.Floor(centre.x), Mathf.Floor(centre.y));
                a = (a - shift) * size;
                b = (b - shift) * size;
                c = (c - shift) * size;
                RasterizeTriangle(a, b, c, regionOfTriangle[t] + 1, size, owner, cover);
                for (int e = 0; e < 3; e++)
                {
                    int va = indices[t * 3 + e], vb = indices[t * 3 + (e + 1) % 3];
                    int wa = weld[va], wb = weld[vb];
                    if (wa != wb && edgeUse[EdgeKey(wa, wb)] == 1)
                    {
                        StampLine((uvs[va] - shift) * size, (uvs[vb] - shift) * size, size, edgeSeed);
                    }
                }
            }
            return true;
        }
    }

    // 한 메시 안에서, 넓은 판 옆에 홈 하나를 두고 나란히 붙은 좁은 띠에 그 판의 부위 번호를 준다.
    private static void MergeNarrowStrips(List<Region> regions, int firstRegion)
    {
        for (int r = firstRegion; r < regions.Count; r++)
        {
            Region strip = regions[r];
            if (strip.Width < BevelZeroWidth || strip.Width >= StripMaxWidth || strip.Area <= 1e-9 || strip.Boundary.Count == 0)
            {
                continue;
            }
            Vector3 stripNormal = strip.NormalSum.normalized;
            Vector3 stripCentre = strip.Sum / (float)strip.Area;
            Region best = null;
            float bestShared = 0f;
            for (int o = firstRegion; o < regions.Count; o++)
            {
                Region plate = regions[o];
                if (o == r || plate.Width < StripMaxWidth || plate.Area <= 1e-9)
                {
                    continue;
                }
                Vector3 plateNormal = plate.NormalSum.normalized;
                Vector3 plateCentre = plate.Sum / (float)plate.Area;
                if (Vector3.Dot(stripNormal, plateNormal) < StripPlaneCos
                    || Mathf.Abs(Vector3.Dot(plateNormal, stripCentre - plateCentre)) > StripPlaneOffset)
                {
                    continue;
                }
                float shared = SharedBoundaryLength(strip.Boundary, plate.Boundary);
                if (shared > bestShared)
                {
                    bestShared = shared;
                    best = plate;
                }
            }
            if (best != null && bestShared >= strip.Perimeter * StripMinShare)
            {
                strip.Id = best.Id;
            }
        }
    }

    // 둘레 a 가운데 둘레 b와 StripGap 안에서 나란히 가는 길이(m). a를 5cm 간격으로 짚어 가며 잰다.
    private static float SharedBoundaryLength(List<Vector3> a, List<Vector3> b)
    {
        const float step = .05f;
        float shared = 0f;
        for (int i = 0; i + 1 < a.Count; i += 2)
        {
            Vector3 start = a[i], end = a[i + 1];
            float length = Vector3.Distance(start, end);
            int samples = Mathf.Max(1, Mathf.CeilToInt(length / step));
            float piece = length / samples;
            for (int s = 0; s < samples; s++)
            {
                Vector3 point = Vector3.Lerp(start, end, (s + .5f) / samples);
                for (int j = 0; j + 1 < b.Count; j += 2)
                {
                    Vector3 segment = b[j + 1] - b[j];
                    float along = segment.sqrMagnitude > 1e-12f ? Mathf.Clamp01(Vector3.Dot(point - b[j], segment) / segment.sqrMagnitude) : 0f;
                    if ((b[j] + segment * along - point).sqrMagnitude <= StripGap * StripGap)
                    {
                        shared += piece;
                        break;
                    }
                }
            }
        }
        return shared;
    }

    private static void RasterizeTriangle(Vector2 a, Vector2 b, Vector2 c, int value, int size, int[] owner, float[] cover)
    {
        float area2 = Cross(b - a, c - a);
        if (Mathf.Abs(area2) < 1e-5f)
        {
            return;
        }
        float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
        float minY = Mathf.Min(a.y, Mathf.Min(b.y, c.y)), maxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
        // 잘못된 UV(텍스처 한 장보다 큰 삼각형)는 건너뛴다.
        if (maxX - minX > size || maxY - minY > size)
        {
            return;
        }
        int x0 = Mathf.Max(0, Mathf.FloorToInt(minX - 1f)), x1 = Mathf.Min(size - 1, Mathf.CeilToInt(maxX + 1f));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(minY - 1f)), y1 = Mathf.Min(size - 1, Mathf.CeilToInt(maxY + 1f));
        float sign = area2 > 0f ? 1f : -1f;
        Vector2 ab = b - a, bc = c - b, ca = a - c;
        float lab = Mathf.Max(ab.magnitude, 1e-6f), lbc = Mathf.Max(bc.magnitude, 1e-6f), lca = Mathf.Max(ca.magnitude, 1e-6f);
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2(x + .5f, y + .5f);
                // 세 변 안쪽으로 들어온 깊이(픽셀). 가장 작은 값이 이 픽셀이 삼각형 안에 든 정도다.
                float depth = Mathf.Min(Cross(ab, p - a) * sign / lab,
                    Mathf.Min(Cross(bc, p - b) * sign / lbc, Cross(ca, p - c) * sign / lca));
                if (depth < -RasterTolerance)
                {
                    continue;
                }
                int index = y * size + x;
                if (owner[index] == 0 || depth > cover[index])
                {
                    owner[index] = value;
                    cover[index] = depth;
                }
            }
        }
    }

    // 부위 경계선이 지나는 픽셀과 그 둘레 한 칸을 표시한다.
    private static void StampLine(Vector2 a, Vector2 b, int size, bool[] edgeSeed)
    {
        float length = Vector2.Distance(a, b);
        if (length > size)
        {
            return;
        }
        int steps = Mathf.Max(1, Mathf.CeilToInt(length * 2f));
        for (int s = 0; s <= steps; s++)
        {
            Vector2 p = Vector2.Lerp(a, b, s / (float)steps);
            int cx = Mathf.FloorToInt(p.x), cy = Mathf.FloorToInt(p.y);
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (x >= 0 && x < size && y >= 0 && y < size)
                    {
                        edgeSeed[y * size + x] = true;
                    }
                }
            }
        }
    }

    // 부위 경계에서 안쪽으로의 거리(픽셀). 같은 부위 안으로만 퍼뜨려, UV 이음매나 옆 조각의 경계가 섞이지 않게 한다.
    private static float[] InteriorDistance(int size, int[] owner, bool[] edgeSeed)
    {
        const float big = 1e6f;
        const float diagonal = 1.41421356f;
        var distance = new float[size * size];
        for (int i = 0; i < distance.Length; i++)
        {
            distance[i] = owner[i] == 0 ? 0f : edgeSeed[i] ? .5f : big;
        }
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                int id = owner[i];
                if (id == 0 || distance[i] <= .5f)
                {
                    continue;
                }
                float d = distance[i];
                if (x > 0 && owner[i - 1] == id) d = Mathf.Min(d, distance[i - 1] + 1f);
                if (y > 0)
                {
                    if (owner[i - size] == id) d = Mathf.Min(d, distance[i - size] + 1f);
                    if (x > 0 && owner[i - size - 1] == id) d = Mathf.Min(d, distance[i - size - 1] + diagonal);
                    if (x < size - 1 && owner[i - size + 1] == id) d = Mathf.Min(d, distance[i - size + 1] + diagonal);
                }
                distance[i] = d;
            }
        }
        for (int y = size - 1; y >= 0; y--)
        {
            for (int x = size - 1; x >= 0; x--)
            {
                int i = y * size + x;
                int id = owner[i];
                if (id == 0 || distance[i] <= .5f)
                {
                    continue;
                }
                float d = distance[i];
                if (x < size - 1 && owner[i + 1] == id) d = Mathf.Min(d, distance[i + 1] + 1f);
                if (y < size - 1)
                {
                    if (owner[i + size] == id) d = Mathf.Min(d, distance[i + size] + 1f);
                    if (x < size - 1 && owner[i + size + 1] == id) d = Mathf.Min(d, distance[i + size + 1] + diagonal);
                    if (x > 0 && owner[i + size - 1] == id) d = Mathf.Min(d, distance[i + size - 1] + diagonal);
                }
                distance[i] = d;
            }
        }
        return distance;
    }

    // 빈 픽셀을 이웃 값으로 채워 UV 조각 바깥 여백을 만든다.
    private static void Dilate(int size, Color32[] pixels, bool[] filled)
    {
        var next = new List<int>();
        for (int pass = 0; pass < DilatePasses; pass++)
        {
            next.Clear();
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    if (filled[i])
                    {
                        continue;
                    }
                    int source = -1;
                    if (x > 0 && filled[i - 1]) source = i - 1;
                    else if (x < size - 1 && filled[i + 1]) source = i + 1;
                    else if (y > 0 && filled[i - size]) source = i - size;
                    else if (y < size - 1 && filled[i + size]) source = i + size;
                    if (source >= 0)
                    {
                        pixels[i] = pixels[source];
                        next.Add(i);
                    }
                }
            }
            if (next.Count == 0)
            {
                break;
            }
            foreach (int i in next)
            {
                filled[i] = true;
            }
        }
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private static long EdgeKey(int a, int b)
    {
        return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
    }

    private static int Find(int[] parent, int index)
    {
        while (parent[index] != index)
        {
            parent[index] = parent[parent[index]];
            index = parent[index];
        }
        return index;
    }

    private static void Union(int[] parent, int a, int b)
    {
        int rootA = Find(parent, a), rootB = Find(parent, b);
        if (rootA != rootB)
        {
            parent[rootB] = rootA;
        }
    }

    private static uint Hash(uint a, uint b, uint c, uint d)
    {
        uint h = 2166136261u;
        h = (h ^ a) * 16777619u;
        h = (h ^ b) * 16777619u;
        h = (h ^ c) * 16777619u;
        h = (h ^ d) * 16777619u;
        h ^= h >> 15;
        h *= 2246822519u;
        h ^= h >> 13;
        return h;
    }
}
