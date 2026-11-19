using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// 두꺼운 천장 조각(Ceiling_001, Object070 같은 것)의 라이트맵 해상도를 올린다.
//
// 왜 필요한가:
//   이 조각들은 보·막대가 달려 있어 라이트맵 UV가 80개가 넘는 조각으로 나뉜다. 씬은 1m당 5칸으로 굽기 때문에
//   3m짜리 천장 조각 하나가 가로세로 24칸뿐이고, 눈에 보이는 아랫면은 10칸 남짓이다.
//   조각 사이 간격이 한 칸의 1/3이라, 위쪽(보이지 않는 면)의 검은 값이 아랫면 가장자리로 섞여 들어온다.
//   그래서 천장 판마다 밝기가 다르고 판 사이가 매끄럽게 이어지지 않는다.
//
// 어떻게 고치는가:
//   그 조각들의 Scale In Lightmap을 올려서 조각 사이 간격이 두 칸 이상 되게 한다.
//   바닥 타일처럼 한 조각으로 펼 수 있는 메시(Flat Lightmap UV로 표시한 것)는 건드리지 않는다.
//
// 쓰는 법:
//   천장 조각을 새로 놓았으면 메뉴 Tools > Background > Ceiling Lightmap Detail (open scene)를 실행하고 조명을 다시 굽는다.
//   씬 값이 바뀌므로 실행한 뒤 씬을 저장해야 한다.
public static class BackgroundCeilingLightmapDetail
{
    private const string MenuPath = "Tools/Background/Ceiling Lightmap Detail (open scene)";
    private const string ShaderName = "Semester/Background/Stylized Lit";
    // FBX 임포트가 조각 사이 간격을 1m당 40칸 기준으로 잡아 두었다. 5칸으로 구우면 간격이 1/8이 되므로,
    // 이 조각들만 6배로 올려 간격을 두 칸 이상으로 만든다(8배는 라이트맵이 너무 커진다).
    private const float Scale = 6f;
    // 천장 조각으로 보는 모양: 얇고(높이 이 값 이하) 넓으며(가로세로 이 값 이상),
    private const float MaxHeight = .6f;
    private const float MinSide = 1.2f;
    // 아래를 향한 면과 위를 향한 면이 둘 다 이 비율 이상 있다(위아래가 막힌 덩어리).
    private const float MinFacingRatio = .25f;

    [MenuItem(MenuPath)]
    private static void ApplyFromMenu()
    {
        string summary = ApplyToOpenScene(false);
        Debug.Log(summary);
        EditorUtility.DisplayDialog("Ceiling Lightmap Detail", summary, "OK");
    }

    // dryRun이면 아무것도 바꾸지 않고, 어떤 조각이 잡히는지만 알려 준다.
    public static string ApplyToOpenScene(bool dryRun = false)
    {
        var ratioCache = new Dictionary<(Mesh, int), Vector2>();
        var flatCache = new Dictionary<string, HashSet<string>>();
        var changed = new List<MeshRenderer>();
        var counts = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
        int already = 0;
        foreach (MeshRenderer renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var filter = renderer.GetComponent<MeshFilter>();
            Material material = renderer.sharedMaterial;
            if (filter == null || filter.sharedMesh == null || material == null || material.shader == null
                || material.shader.name != ShaderName
                || (GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.ContributeGI) == 0
                || renderer.receiveGI != ReceiveGI.Lightmaps)
            {
                continue;
            }

            Vector3 size = renderer.bounds.size;
            if (size.y > MaxHeight || Mathf.Min(size.x, size.z) < MinSide)
            {
                continue;
            }

            Mesh mesh = filter.sharedMesh;
            if (IsFlatMarked(mesh, flatCache))
            {
                continue;
            }

            Matrix4x4 matrix = renderer.transform.localToWorldMatrix;
            // 같은 메시라도 놓인 방향이 다르면 따로 센다(90도 단위 회전만 묶는다).
            Vector3 up = renderer.transform.InverseTransformDirection(Vector3.up);
            int axis = Mathf.Abs(up.x) > .99f ? 0 : Mathf.Abs(up.y) > .99f ? 1 : Mathf.Abs(up.z) > .99f ? 2 : -1;
            var key = (mesh, axis < 0 ? renderer.GetInstanceID() : (axis + 1) * (up[axis] > 0f ? 1 : -1) * (matrix.determinant < 0f ? 10 : 1));
            if (!ratioCache.TryGetValue(key, out Vector2 ratio))
            {
                ratio = BackgroundCeilingSunShadow.DownUpRatio(mesh, matrix, matrix.determinant < 0f);
                ratioCache[key] = ratio;
            }

            if (ratio.x < MinFacingRatio || ratio.y < MinFacingRatio)
            {
                continue;
            }

            counts.TryGetValue(mesh.name, out int count);
            counts[mesh.name] = count + 1;
            if (Mathf.Approximately(renderer.scaleInLightmap, Scale))
            {
                already++;
            }
            else
            {
                changed.Add(renderer);
            }
        }

        if (!dryRun && changed.Count > 0)
        {
            Undo.RecordObjects(changed.ToArray(), "Ceiling lightmap detail");
            foreach (MeshRenderer renderer in changed)
            {
                renderer.scaleInLightmap = Scale;
                EditorUtility.SetDirty(renderer);
            }
        }

        var summary = new StringBuilder();
        summary.AppendLine($"천장 조각 {changed.Count + already}개 가운데 {changed.Count}개의 Scale In Lightmap을 {Scale}로 {(dryRun ? "바꿀 예정입니다" : "바꿨습니다")}(이미 적용된 것 {already}개).");
        foreach (KeyValuePair<string, int> pair in counts)
        {
            summary.AppendLine($"  {pair.Key}: {pair.Value}개");
        }

        summary.Append(changed.Count > 0 ? "조명을 다시 굽고 씬을 저장해야 반영됩니다." : "바뀐 것이 없습니다.");
        return summary.ToString();
    }

    // FBX의 userData에 Flat Lightmap UV로 표시된 메시인지.
    private static bool IsFlatMarked(Mesh mesh, Dictionary<string, HashSet<string>> cache)
    {
        string path = AssetDatabase.GetAssetPath(mesh);
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        if (!cache.TryGetValue(path, out HashSet<string> names))
        {
            AssetImporter importer = AssetImporter.GetAtPath(path);
            names = importer != null ? BackgroundFlatLightmapUv.ParseMarker(importer.userData) : new HashSet<string>();
            cache[path] = names;
        }

        return names.Contains(mesh.name);
    }
}
