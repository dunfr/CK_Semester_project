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

    [MenuItem("Tools/Fuchsia/Build Skinned Style Meshes")]
    private static void BuildFromMenu()
    {
        string summary = Build();
        Debug.Log(summary);
        EditorUtility.DisplayDialog("Skinned Style Meshes", summary, "OK");
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
            head.sharedMesh = SaveMesh(headStyle, "FuchsiaUpdated_head_Style");
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
