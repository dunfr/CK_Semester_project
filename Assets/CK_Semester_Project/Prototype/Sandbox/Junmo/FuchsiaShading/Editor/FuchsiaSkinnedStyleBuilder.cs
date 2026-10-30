using System.Collections.Generic;
using System.Linq;
using CK.SemesterProject.Prototype;
using UnityEditor;
using UnityEngine;

// 스킨 메시(FuchsiaUpdatedAnimated)의 얼굴·머리카락에 툰 스타일 데이터를 굽는다.
// - UV1: 머리 부피 법선(모델 루트 기준축 성분). 셰이더가 FuchsiaStyleAxes의 머리 축으로 다시 조립한다.
// - UV2: 얼굴 SDF 좌표 / 머리카락 광택 띠 좌표.
// 정적 모델용 ApplyFuchsiaStyleMeshes의 식을 머리 범위 기준 비율로 바꿔 새 모델의 머리 크기·위치에 맞춘다.
// 원본 FBX는 건드리지 않고 Models/CharacterStyle에 사본(.asset)을 만들어 프리팹에 연결한다.
public static class FuchsiaSkinnedStyleBuilder
{
    private const string Root = "Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/";
    private const string AnimatedPrefab = Root + "Prefabs/FuchsiaUpdatedAnimated.prefab";
    private const string OutputFolder = Root + "Models/CharacterStyle";
    // 텍스처 UV가 이 거리 안에 있으면 같은 지점으로 보고 기존 모델의 스타일 좌표를 옮긴다.
    private const float UvMatchDistance = .01f;
    private const string FaceMaterial = "FuchsiaFaceToon";
    private const string HairMaterial = "FuchsiaHairToon";
    private const string HeadStyleName = "FuchsiaUpdated_head_Style";
    // 표정 블렌드셰이프 원본: 그래픽 파트 모션 FBX의 머리 메시(다섯 모션 FBX의 머리가 모두 같다).
    // 표정 때문에 정점이 더 쪼개져 있어(2164 대 1904) 정점 번호 대신 위치·텍스처 UV로 짝을 찾는다.
    private const string ExpressionSourceModel = "Assets/FuchsiaAnimation/standing_idle.fbx";
    private const float ExpressionMatchDistance = 1e-4f;

    [MenuItem("Tools/Fuchsia/Build Skinned Style Meshes")]
    private static void BuildFromMenu()
    {
        string summary = Build();
        Debug.Log(summary);
        EditorUtility.DisplayDialog("Skinned Style Meshes", summary, "OK");
    }

    [MenuItem("Tools/Fuchsia/Transfer Head Expressions")]
    private static void TransferHeadExpressionsFromMenu()
    {
        string summary = TransferHeadExpressions();
        Debug.Log(summary);
        EditorUtility.DisplayDialog("Head Expressions", summary, "OK");
    }

    // 이미 구운 머리 스타일 메시에 표정 블렌드셰이프만 다시 옮긴다. UV1/UV2·정점 색·프리팹은 그대로 둔다.
    public static string TransferHeadExpressions()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{OutputFolder}/{HeadStyleName}.asset");
        if (mesh == null)
        {
            return $"{HeadStyleName}.asset을 찾지 못했습니다.";
        }
        bool wasReadable = mesh.isReadable;
        SetReadable(mesh, true);
        string summary = CopyBlendShapes(mesh, ExpressionSource());
        SetReadable(mesh, wasReadable);
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssetIfDirty(mesh);
        return summary;
    }

    public static string Build()
    {
        var log = new List<string>();
        // 원본 메시를 읽을 수 있게 한다: FBX는 임포트 설정 Read/Write, .asset 사본은 m_IsReadable을 굽는 동안만 켠다.
        var restore = new List<System.Action>();
        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(AnimatedPrefab);
        foreach (SkinnedMeshRenderer renderer in prefabAsset.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!renderer.sharedMaterials.Any(m => m != null && (m.name == FaceMaterial || m.name == HairMaterial)) || renderer.sharedMesh == null)
            {
                continue;
            }
            Mesh mesh = renderer.sharedMesh;
            string path = AssetDatabase.GetAssetPath(mesh);
            if (AssetImporter.GetAtPath(path) is ModelImporter importer)
            {
                // 얼굴·머리카락이 같은 FBX면 한 번만 켜고 한 번만 되돌린다.
                if (!importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    restore.Add(() =>
                    {
                        importer.isReadable = false;
                        importer.SaveAndReimport();
                    });
                }
            }
            else if (!mesh.isReadable)
            {
                SetReadable(mesh, true);
                restore.Add(() => SetReadable(mesh, false));
            }
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(AnimatedPrefab);
        try
        {
            var skinned = contents.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            SkinnedMeshRenderer head = skinned.FirstOrDefault(r => r.sharedMaterials.Any(m => m != null && m.name == FaceMaterial));
            SkinnedMeshRenderer hair = skinned.FirstOrDefault(r => r.sharedMaterials.Any(m => m != null && m.name == HairMaterial));
            if (head == null || hair == null)
            {
                return $"얼굴/머리카락 렌더러를 찾지 못했습니다. head={head} hair={hair}";
            }
            Transform modelRoot = contents.transform;

            Vector3[] headPoints = BakedRootPoints(head, modelRoot);
            int faceSubmesh = System.Array.FindIndex(head.sharedMaterials, m => m != null && m.name == FaceMaterial);
            HashSet<int> faceVertices = SubmeshVertices(SourceMesh(head), faceSubmesh);
            Bounds face = BoundsOf(faceVertices.Select(i => headPoints[i]), .005f);
            log.Add($"face bounds: center {face.center} size {face.size}");
            Mesh headStyle = CreateStyleMesh(head, headPoints, i => faceVertices.Contains(i), face, isHair: false);
            Mesh hairStyle = CreateStyleMesh(hair, BakedRootPoints(hair, modelRoot), _ => true, face, isHair: true);
            // 얼굴 SDF 맵과 머리 광택 제어 맵은 기존 모델 좌표에 맞춰 그려져 있다.
            // 두 모델은 같은 텍스처 UV 배치를 쓰므로, 텍스처 UV가 같은 기존 정점의 스타일 좌표(UV2)를 그대로 옮긴다.
            log.Add("face " + TransferStyleData(headStyle, i => faceVertices.Contains(i), StaticStyleSamples(StaticFaceMeshes)));
            log.Add("hair " + TransferStyleData(hairStyle, _ => true, StaticStyleSamples(StaticHairMeshes)));
            log.Add("expression " + CopyBlendShapes(headStyle, ExpressionSource()));
            head.sharedMesh = SaveMesh(headStyle, HeadStyleName);
            hair.sharedMesh = SaveMesh(hairStyle, "FuchsiaUpdated_hair_Style");

            Animator animator = contents.GetComponentInChildren<Animator>(true);
            Transform headBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (headBone == null)
            {
                // Generic 리그: 이름이 Head로 끝나는 뼈(예: Bip001 Head, mixamorig:Head) 중 가장 얕은 것을 쓴다.
                // 대소문자를 구분하고 렌더러가 붙은 오브젝트(메시 'head')는 뺀다.
                headBone = contents.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name.EndsWith("Head", System.StringComparison.Ordinal) && t.GetComponent<Renderer>() == null)
                    .OrderBy(t => Depth(t)).FirstOrDefault();
            }
            var axes = contents.GetComponent<FuchsiaStyleAxes>();
            if (axes == null) axes = contents.AddComponent<FuchsiaStyleAxes>();
            axes.Configure(headBone, new Renderer[] { head, hair });
            PrefabUtility.SaveAsPrefabAsset(contents, AnimatedPrefab);
            log.Add($"head: 얼굴 정점 {faceVertices.Count}/{headPoints.Length}, hair: 정점 {hair.sharedMesh.vertexCount}, 머리 뼈 {(headBone != null ? headBone.name : "없음")}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
            foreach (System.Action action in restore)
            {
                action();
            }
        }
        AssetDatabase.SaveAssets();
        return string.Join("\n", log);
    }

    private static int Depth(Transform t)
    {
        int depth = 0;
        for (; t.parent != null; t = t.parent) depth++;
        return depth;
    }

    private static Mesh SourceMesh(SkinnedMeshRenderer renderer)
    {
        // 이미 사본이 연결돼 있으면 사본의 원본 이름으로 FBX 메시를 다시 찾지 않고 그대로 쓴다(정점 순서 동일).
        return renderer.sharedMesh;
    }

    private static Mesh CreateStyleMesh(SkinnedMeshRenderer renderer, Vector3[] rootPoints,
        System.Func<int, bool> include, Bounds face, bool isHair)
    {
        Mesh source = renderer.sharedMesh;
        Mesh mesh = Object.Instantiate(source);
        mesh.name = source.name.Replace("_Style", "") + "_Style";
        var styleNormals = new List<Vector3>(rootPoints.Length);
        var styleData = new List<Vector4>(rootPoints.Length);
        for (int i = 0; i < rootPoints.Length; i++)
        {
            if (!include(i))
            {
                // 얼굴 외 부위(같은 메시의 옷 등)는 스타일 데이터를 비워 기본 셰이딩을 쓰게 한다.
                styleNormals.Add(Vector3.zero);
                styleData.Add(Vector4.zero);
                continue;
            }
            // 정적 모델용 식(ApplyFuchsiaStyleMeshes)은 머리 범위 중심 (0, 1.445, .30), 크기 (.17, .19, .17)로 정규화한 것과 같다.
            // 같은 비율을 새 머리 범위에 적용한다. d = 머리 중심 기준 위치, s = 머리 크기.
            Vector3 d = rootPoints[i] - face.center;
            Vector3 s = Vector3.Max(face.size, Vector3.one * 1e-4f);
            styleNormals.Add(new Vector3(d.x / (.706f * s.x), d.y / (.684f * s.y), d.z / (.559f * s.z)).normalized);
            styleData.Add(isHair
                ? new Vector4(d.x / (.735f * s.x), (d.y + .711f * s.y) / (1.474f * s.y), d.z / (.588f * s.z), .375f)
                : new Vector4(d.x / s.x + .5f, d.y / s.y + .5f, (d.z - .176f * s.z) / (.529f * s.z), .375f));
        }
        mesh.SetUVs(1, styleNormals);
        mesh.SetUVs(2, styleData);
        // 정점 색 채널도 항상 둔다(외곽선 두께 기본값 1). 얼굴·머리카락의 정점 레이아웃을 같게 맞춰
        // 스킨 렌더러가 레이아웃 변경 뒤 이전 버퍼를 읽어 UV1/UV2가 상수로 보이던 문제를 피한다.
        if (mesh.colors == null || mesh.colors.Length != mesh.vertexCount)
        {
            mesh.colors = Enumerable.Repeat(Color.white, mesh.vertexCount).ToArray();
        }
        PadBoneWeights(mesh, 4);
        return mesh;
    }

    // 정점당 뼈 가중치를 4개로 맞춘다(남는 칸은 가중치 0). 정점당 2개인 메시는 스킨 렌더러에서
    // 추가 UV(UV1/UV2)가 셰이더에 상수로 들어가는 현상이 있어, 정상 동작하는 머리카락 메시와 같은 4개 레이아웃으로 맞춘다.
    private static void PadBoneWeights(Mesh mesh, int perVertex)
    {
        var counts = mesh.GetBonesPerVertex();
        var weights = mesh.GetAllBoneWeights();
        if (counts.Length == 0 || counts.All(c => c == perVertex))
        {
            return;
        }
        var newCounts = new Unity.Collections.NativeArray<byte>(counts.Length, Unity.Collections.Allocator.Temp);
        var newWeights = new Unity.Collections.NativeArray<BoneWeight1>(counts.Length * perVertex, Unity.Collections.Allocator.Temp);
        int read = 0;
        for (int v = 0; v < counts.Length; v++)
        {
            int count = counts[v];
            for (int k = 0; k < perVertex; k++)
            {
                newWeights[v * perVertex + k] = k < count
                    ? weights[read + k]
                    : new BoneWeight1 { boneIndex = 0, weight = 0f };
            }
            read += count;
            newCounts[v] = (byte)perVertex;
        }
        mesh.SetBoneWeights(newCounts, newWeights);
        newCounts.Dispose();
        newWeights.Dispose();
    }

    private struct StyleSample
    {
        public Vector2 Uv;
        public Vector4 Data;
    }

    // 기존 정적 모델의 스타일 메시(.asset)에서 (텍스처 UV, 스타일 좌표) 목록을 모은다.
    // 정적 프리팹은 원본 FBX 메시를 가리키고, 스타일 메시는 씬 인스턴스에만 연결돼 있으므로 사본 자산을 직접 읽는다.
    private static readonly string[] StaticFaceMeshes = { "머리_Style" };
    private static readonly string[] StaticHairMeshes = { "겉머리_Style", "뒷머리_Style", "앞머리_Style", "옆머리_Style" };

    private static List<StyleSample> StaticStyleSamples(string[] meshNames)
    {
        var samples = new List<StyleSample>();
        foreach (string name in meshNames)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{OutputFolder}/{name}.asset");
            if (mesh == null)
            {
                continue;
            }
            bool wasReadable = mesh.isReadable;
            SetReadable(mesh, true);
            var uv0 = new List<Vector2>();
            var uv2 = new List<Vector4>();
            mesh.GetUVs(0, uv0);
            mesh.GetUVs(2, uv2);
            SetReadable(mesh, wasReadable);
            for (int i = 0; i < Mathf.Min(uv0.Count, uv2.Count); i++)
            {
                if (uv2[i].w > .1f) samples.Add(new StyleSample { Uv = uv0[i], Data = uv2[i] });
            }
        }
        return samples;
    }

    private static string TransferStyleData(Mesh mesh, System.Func<int, bool> include, List<StyleSample> samples)
    {
        if (samples.Count == 0)
        {
            return "기존 샘플 없음, 식으로 계산한 값 유지";
        }
        var uv0 = new List<Vector2>();
        var uv2 = new List<Vector4>();
        mesh.GetUVs(0, uv0);
        mesh.GetUVs(2, uv2);
        // UV 격자로 가까운 후보만 검사한다.
        float cell = UvMatchDistance;
        var grid = new Dictionary<Vector2Int, List<int>>();
        for (int s = 0; s < samples.Count; s++)
        {
            var key = new Vector2Int(Mathf.FloorToInt(samples[s].Uv.x / cell), Mathf.FloorToInt(samples[s].Uv.y / cell));
            if (!grid.TryGetValue(key, out List<int> list)) grid[key] = list = new List<int>();
            list.Add(s);
        }
        int matched = 0, total = 0;
        for (int i = 0; i < uv0.Count; i++)
        {
            if (!include(i))
            {
                continue;
            }
            total++;
            var key = new Vector2Int(Mathf.FloorToInt(uv0[i].x / cell), Mathf.FloorToInt(uv0[i].y / cell));
            float best = UvMatchDistance * UvMatchDistance;
            int bestIndex = -1;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (!grid.TryGetValue(key + new Vector2Int(dx, dy), out List<int> list)) continue;
                foreach (int s in list)
                {
                    float d = (samples[s].Uv - uv0[i]).sqrMagnitude;
                    if (d < best) { best = d; bestIndex = s; }
                }
            }
            if (bestIndex >= 0)
            {
                uv2[i] = samples[bestIndex].Data;
                matched++;
            }
        }
        mesh.SetUVs(2, uv2);
        return $"UV 전이 {matched}/{total} (나머지는 식으로 계산한 값)";
    }

    private static Mesh ExpressionSource()
    {
        return AssetDatabase.LoadAllAssetsAtPath(ExpressionSourceModel).OfType<Mesh>()
            .FirstOrDefault(m => m.name == "head" && m.blendShapeCount > 0);
    }

    // 표정 블렌드셰이프를 옮긴다. 위치 델타는 원본 값을 그대로 쓴다.
    // 노멀·탄젠트 델타는 원본 값을 쓰지 않는다. 임포트 때 다시 계산한 노멀과 원본 노멀의 차이가 움직이지 않는 정점에도 들어 있어
    // (EB_anger는 45정점만 움직이는데 2114정점에 노멀 델타가 있다) 표정이 켜질 때마다 얼굴 전체 명암·외곽선이 흔들린다.
    // 대신 표정으로 기울어진 면 방향만큼 이 메시의 노멀·탄젠트를 돌린다. 움직임이 닿지 않는 정점의 델타는 0이다.
    private static string CopyBlendShapes(Mesh target, Mesh source)
    {
        if (source == null)
        {
            return $"표정 원본({ExpressionSourceModel}의 head)이 없어 기존 블렌드셰이프 유지";
        }
        int[] match = MatchVertices(target, source);
        Vector3[] points = target.vertices;
        Vector3[] normals = target.normals;
        Vector4[] tangents = target.tangents;
        bool hasTangents = tangents.Length == points.Length;
        int[] triangles = target.triangles;
        int[] weld = WeldByPosition(points);
        Vector3[] restFacing = WeldedFaceNormals(points, triangles, weld);
        var sourceDeltas = new Vector3[source.vertexCount];
        var shaped = new Vector3[points.Length];
        var log = new List<string>();
        target.ClearBlendShapes();
        for (int shape = 0; shape < source.blendShapeCount; shape++)
        {
            string name = source.GetBlendShapeName(shape);
            for (int frame = 0; frame < source.GetBlendShapeFrameCount(shape); frame++)
            {
                source.GetBlendShapeFrameVertices(shape, frame, sourceDeltas, null, null);
                var deltaPoints = new Vector3[points.Length];
                var deltaNormals = new Vector3[points.Length];
                var deltaTangents = hasTangents ? new Vector3[points.Length] : null;
                int moved = 0, turned = 0;
                for (int i = 0; i < points.Length; i++)
                {
                    deltaPoints[i] = match[i] >= 0 ? sourceDeltas[match[i]] : Vector3.zero;
                    shaped[i] = points[i] + deltaPoints[i];
                    if (deltaPoints[i].sqrMagnitude > 0f) moved++;
                }
                Vector3[] shapedFacing = WeldedFaceNormals(shaped, triangles, weld);
                for (int i = 0; i < points.Length; i++)
                {
                    if (restFacing[i] == shapedFacing[i] || restFacing[i] == Vector3.zero || shapedFacing[i] == Vector3.zero)
                    {
                        continue;
                    }
                    Quaternion turn = Quaternion.FromToRotation(restFacing[i], shapedFacing[i]);
                    deltaNormals[i] = turn * normals[i] - normals[i];
                    if (hasTangents) deltaTangents[i] = turn * (Vector3)tangents[i] - (Vector3)tangents[i];
                    turned++;
                }
                target.AddBlendShapeFrame(name, source.GetBlendShapeFrameWeight(shape, frame), deltaPoints, deltaNormals, deltaTangents);
                log.Add($"{name} 이동 {moved}·노멀 {turned}");
            }
        }
        return $"블렌드셰이프 {target.blendShapeCount}개 ({string.Join(", ", log)}), 짝 없는 정점 {match.Count(i => i < 0)}";
    }

    // 대상 정점마다 위치·텍스처 UV가 같은 원본 정점을 찾는다. 같은 지점에서 쪼개진 정점이 여럿이면 노멀·탄젠트가 가장 가까운 것을 고른다.
    // 짝이 없으면 -1(표정 델타 0).
    private static int[] MatchVertices(Mesh target, Mesh source)
    {
        Vector3[] targetPoints = target.vertices, sourcePoints = source.vertices;
        Vector3[] targetNormals = target.normals, sourceNormals = source.normals;
        Vector4[] targetTangents = target.tangents, sourceTangents = source.tangents;
        bool compareTangents = targetTangents.Length == targetPoints.Length && sourceTangents.Length == sourcePoints.Length;
        var targetUv = new List<Vector2>();
        var sourceUv = new List<Vector2>();
        target.GetUVs(0, targetUv);
        source.GetUVs(0, sourceUv);
        float cell = ExpressionMatchDistance * 10f;
        float limit = ExpressionMatchDistance * ExpressionMatchDistance;
        var grid = new Dictionary<Vector3Int, List<int>>();
        for (int s = 0; s < sourcePoints.Length; s++)
        {
            Vector3Int key = Vector3Int.FloorToInt(sourcePoints[s] / cell);
            if (!grid.TryGetValue(key, out List<int> list)) grid[key] = list = new List<int>();
            list.Add(s);
        }
        var match = new int[targetPoints.Length];
        for (int i = 0; i < targetPoints.Length; i++)
        {
            Vector3Int key = Vector3Int.FloorToInt(targetPoints[i] / cell);
            float best = float.MaxValue;
            match[i] = -1;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!grid.TryGetValue(key + new Vector3Int(dx, dy, dz), out List<int> list)) continue;
                foreach (int s in list)
                {
                    if ((sourcePoints[s] - targetPoints[i]).sqrMagnitude > limit || (sourceUv[s] - targetUv[i]).sqrMagnitude > limit) continue;
                    float score = (sourceNormals[s] - targetNormals[i]).sqrMagnitude
                        + (compareTangents ? ((Vector3)sourceTangents[s] - (Vector3)targetTangents[i]).sqrMagnitude : 0f);
                    if (score < best) { best = score; match[i] = s; }
                }
            }
        }
        return match;
    }

    // UV 이음매·하드 엣지로 쪼개진 정점을 같은 위치끼리 한 묶음으로 본다.
    private static int[] WeldByPosition(Vector3[] points)
    {
        var groups = new Dictionary<Vector3, int>();
        var weld = new int[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            if (!groups.TryGetValue(points[i], out int group))
            {
                group = groups.Count;
                groups.Add(points[i], group);
            }
            weld[i] = group;
        }
        return weld;
    }

    // 묶음별 면적 가중 면 방향. 쪼개진 정점도 같은 방향을 받아 표정 노멀 회전이 이음매에서 갈라지지 않는다.
    private static Vector3[] WeldedFaceNormals(Vector3[] points, int[] triangles, int[] weld)
    {
        var sums = new Vector3[points.Length];
        for (int t = 0; t < triangles.Length; t += 3)
        {
            int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
            Vector3 face = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
            sums[weld[a]] += face;
            sums[weld[b]] += face;
            sums[weld[c]] += face;
        }
        var facing = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 sum = sums[weld[i]];
            float length = sum.magnitude;
            facing[i] = length > 1e-12f ? sum / length : Vector3.zero;
        }
        return facing;
    }

    private static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = $"{OutputFolder}/{name}.asset";
        mesh.name = name;
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            SetReadable(mesh, false);
            return mesh;
        }
        // 기존 사본의 정점 색(외곽선 두께)은 유지한다.
        Color[] colors = existing.vertexCount == mesh.vertexCount ? existing.colors : null;
        EditorUtility.CopySerialized(mesh, existing);
        if (colors != null && colors.Length == existing.vertexCount)
        {
            existing.colors = colors;
        }
        SetReadable(existing, false);
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(mesh);
        return existing;
    }

    private static Vector3[] BakedRootPoints(SkinnedMeshRenderer renderer, Transform root)
    {
        var baked = new Mesh();
        // useScale=false: 렌더러 스케일 없이 로컬 공간으로 굽고, TransformPoint에서 스케일을 한 번만 적용한다.
        renderer.BakeMesh(baked, false);
        Vector3[] points = baked.vertices;
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = root.InverseTransformPoint(renderer.transform.TransformPoint(points[i]));
        }
        Object.DestroyImmediate(baked);
        return points;
    }

    private static HashSet<int> SubmeshVertices(Mesh mesh, int submesh)
    {
        return submesh >= 0 && submesh < mesh.subMeshCount
            ? new HashSet<int>(mesh.GetTriangles(submesh))
            : new HashSet<int>(Enumerable.Range(0, mesh.vertexCount));
    }

    // 축별 백분위 범위(trim ~ 1-trim). 뼈 가중치가 없는 정점처럼 멀리 떨어진 소수 정점이 맞춤 배율을 망치지 않게 한다.
    private static Bounds BoundsOf(IEnumerable<Vector3> points, float trim)
    {
        List<Vector3> list = points.ToList();
        if (list.Count == 0)
        {
            return default;
        }
        float Percentile(List<float> values, float t) => values[Mathf.Clamp(Mathf.RoundToInt((values.Count - 1) * t), 0, values.Count - 1)];
        var min = new Vector3();
        var max = new Vector3();
        for (int axis = 0; axis < 3; axis++)
        {
            List<float> values = list.Select(p => p[axis]).OrderBy(v => v).ToList();
            min[axis] = Percentile(values, trim);
            max[axis] = Percentile(values, 1f - trim);
        }
        var bounds = new Bounds();
        bounds.SetMinMax(min, max);
        return bounds;
    }

    private static void SetReadable(Mesh mesh, bool readable)
    {
        if (mesh.isReadable == readable || !AssetDatabase.GetAssetPath(mesh).EndsWith(".asset"))
        {
            return;
        }
        var serialized = new SerializedObject(mesh);
        serialized.FindProperty("m_IsReadable").boolValue = readable;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
