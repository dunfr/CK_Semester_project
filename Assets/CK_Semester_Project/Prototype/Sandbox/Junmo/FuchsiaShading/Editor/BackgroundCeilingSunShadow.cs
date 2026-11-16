using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 천장 조각이 실시간 방향광(해)의 그림자를 떨구지 않게 한다.
//
// 왜 필요한가:
//   실내인데도 해(실시간 Directional Light)는 천장을 그대로 통과한다. 천장 타일의 넓은 면은 해 쪽에서 보면 뒷면이라
//   그림자를 만들지 않기 때문이다. 그런데 타일의 옆 테두리(높이 10cm)는 해 쪽을 향해 있어서 그것만 그림자를 만들고,
//   바닥에 타일 이음매 모양의 가는 줄이 격자로 떨어진다(홈 선 옆에 흐린 선이 한 줄 더 보이는 현상).
//
// 어떻게 고치는가:
//   아래를 향한 면이 대부분이고 위를 향한 면(뚜껑)이 없는 조각, 곧 바닥 타일을 뒤집어 천장으로 쓴 조각을 찾는다.
//   그 조각의 Rendering Layer Mask를 전용 레이어 하나로 바꾸고, 해의 Custom Shadow Layers를 켜서
//   그 레이어는 그림자를 만들지 않게 한다. 해의 빛 자체는 전처럼 받는다.
//   Cast Shadows는 On 그대로 둔다. 끄면 조명을 구울 때 천장이 하늘빛을 막지 못해 실내가 밝아지기 때문이다.
//   위아래가 다 막힌 두꺼운 천장 조각(Ceiling_001 등)은 온전한 그림자를 만들므로 건드리지 않는다.
//
// 쓰는 법:
//   천장 조각을 새로 놓았으면 메뉴 Tools > Background > Ceilings Ignore Sun Shadow (open scene)를 다시 실행한다.
//   씬 값이 바뀌므로 실행한 뒤 씬을 저장해야 한다.
public static class BackgroundCeilingSunShadow
{
    private const string MenuPath = "Tools/Background/Ceilings Ignore Sun Shadow (open scene)";
    // 천장 조각에 주는 Rendering Layer 번호(Project Settings > Tags and Layers > Rendering Layers의 1번).
    private const int CeilingLayerIndex = 1;
    private const string CeilingLayerName = "Ceiling (no sun shadow)";
    // 아래를 향한 면이 전체 넓이의 이 비율 이상이고,
    private const float MinDownRatio = .7f;
    // 위를 향한 면이 이 비율 이하면(뚜껑이 없으면) 뒤집어 놓은 천장 타일로 본다.
    private const float MaxUpRatio = .05f;
    // 면이 아래/위를 향한다고 보는 기준(법선의 세로 성분).
    private const float FacingLimit = .5f;

    [MenuItem(MenuPath)]
    private static void ApplyFromMenu()
    {
        string summary = ApplyToOpenScene(false);
        Debug.Log(summary);
        EditorUtility.DisplayDialog("Ceiling Sun Shadow", summary, "OK");
    }

    // dryRun이면 아무것도 바꾸지 않고, 어떤 조각이 천장으로 잡히는지만 알려 준다.
    public static string ApplyToOpenScene(bool dryRun = false)
    {
        uint ceilingBit = 1u << CeilingLayerIndex;
        var ratioByMesh = new Dictionary<(Mesh, bool, int), Vector2>();
        var changed = new List<MeshRenderer>();
        int already = 0;
        var counts = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
        foreach (MeshRenderer renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || renderer.shadowCastingMode == ShadowCastingMode.Off
                || (GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.ContributeGI) == 0)
            {
                continue;
            }

            if (!IsCeiling(filter.sharedMesh, renderer.transform, ratioByMesh))
            {
                continue;
            }

            counts.TryGetValue(filter.sharedMesh.name, out int count);
            counts[filter.sharedMesh.name] = count + 1;
            if (renderer.renderingLayerMask == ceilingBit)
            {
                already++;
                continue;
            }

            changed.Add(renderer);
        }

        if (dryRun)
        {
            var preview = new StringBuilder($"천장 조각 {changed.Count + already}개(이미 적용 {already}개)\n");
            foreach (KeyValuePair<string, int> pair in counts)
            {
                preview.AppendLine($"  {pair.Key}: {pair.Value}개");
            }

            return preview.ToString();
        }

        if (changed.Count > 0)
        {
            Undo.RecordObjects(changed.ToArray(), "Ceilings ignore sun shadow");
            foreach (MeshRenderer renderer in changed)
            {
                renderer.renderingLayerMask = ceilingBit;
                EditorUtility.SetDirty(renderer);
            }
        }

        // 실시간 방향광: 천장 레이어도 비추되, 그림자는 그 레이어를 빼고 만든다.
        int lights = 0;
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional || light.lightmapBakeType == LightmapBakeType.Baked)
            {
                continue;
            }

            var data = light.GetComponent<UniversalAdditionalLightData>();
            if (data == null)
            {
                continue;
            }

            uint lighting = data.renderingLayers | ceilingBit;
            uint shadow = (data.customShadowLayers ? (uint)data.shadowRenderingLayers : (uint)data.renderingLayers) & ~ceilingBit;
            if (data.renderingLayers == lighting && data.customShadowLayers && data.shadowRenderingLayers == shadow)
            {
                continue;
            }

            Undo.RecordObjects(new Object[] { data, light }, "Ceilings ignore sun shadow");
            data.renderingLayers = lighting;
            data.customShadowLayers = true;
            data.shadowRenderingLayers = shadow;
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(light);
            lights++;
        }

        NameCeilingLayer();

        var summary = new StringBuilder();
        summary.AppendLine($"천장 조각 {changed.Count + already}개 가운데 {changed.Count}개를 새로 바꿨습니다(이미 적용된 것 {already}개). 방향광 {lights}개를 바꿨습니다.");
        foreach (KeyValuePair<string, int> pair in counts)
        {
            summary.AppendLine($"  {pair.Key}: {pair.Value}개");
        }

        summary.Append(changed.Count + lights > 0 ? "씬을 저장해야 유지됩니다." : "바뀐 것이 없습니다.");
        return summary.ToString();
    }

    // 월드 기준으로 아래를 향한 면이 대부분이고 뚜껑이 없으면 천장 타일이다. 같은 메시라도 뒤집어 놓으면 달라지므로 놓인 방향별로 따로 센다.
    private static bool IsCeiling(Mesh mesh, Transform transform, Dictionary<(Mesh, bool, int), Vector2> cache)
    {
        Matrix4x4 matrix = transform.localToWorldMatrix;
        // 메시의 어느 축이 월드의 위쪽인지(부호 포함)로 놓인 방향을 구분한다. 비스듬히 놓인 조각은 따로 계산한다.
        Vector3 up = transform.InverseTransformDirection(Vector3.up);
        int axis = Mathf.Abs(up.x) > .99f ? 0 : Mathf.Abs(up.y) > .99f ? 1 : Mathf.Abs(up.z) > .99f ? 2 : -1;
        bool mirrored = matrix.determinant < 0f;
        var key = (mesh, mirrored, axis < 0 ? transform.GetInstanceID() : (axis + 1) * (up[axis] > 0f ? 1 : -1));
        if (!cache.TryGetValue(key, out Vector2 ratio))
        {
            ratio = DownUpRatio(mesh, matrix, mirrored);
            cache[key] = ratio;
        }

        return ratio.x >= MinDownRatio && ratio.y <= MaxUpRatio;
    }

    // (아래를 향한 넓이 비율, 위를 향한 넓이 비율). 천장 라이트맵 도구(BackgroundCeilingLightmapDetail)도 같이 쓴다.
    internal static Vector2 DownUpRatio(Mesh mesh, Matrix4x4 matrix, bool mirrored)
    {
        using (Mesh.MeshDataArray dataArray = MeshUtility.AcquireReadOnlyMeshData(mesh))
        {
            Mesh.MeshData data = dataArray[0];
            using (var positions = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
            {
                data.GetVertices(positions);
                double total = 0d, down = 0d, up = 0d;
                for (int subMesh = 0; subMesh < data.subMeshCount; subMesh++)
                {
                    SubMeshDescriptor descriptor = data.GetSubMesh(subMesh);
                    if (descriptor.topology != MeshTopology.Triangles)
                    {
                        continue;
                    }

                    using (var indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp))
                    {
                        data.GetIndices(indices, subMesh);
                        for (int i = 0; i + 2 < indices.Length; i += 3)
                        {
                            Vector3 a = matrix.MultiplyPoint3x4(positions[indices[i]]);
                            Vector3 b = matrix.MultiplyPoint3x4(positions[indices[i + 1]]);
                            Vector3 c = matrix.MultiplyPoint3x4(positions[indices[i + 2]]);
                            Vector3 cross = Vector3.Cross(b - a, c - a);
                            double area = cross.magnitude * .5d;
                            if (area <= 1e-10d)
                            {
                                continue;
                            }

                            // 거울처럼 뒤집어 놓은 조각은 삼각형 방향이 반대로 나온다.
                            float normalY = cross.normalized.y * (mirrored ? -1f : 1f);
                            total += area;
                            if (normalY < -FacingLimit)
                            {
                                down += area;
                            }
                            else if (normalY > FacingLimit)
                            {
                                up += area;
                            }
                        }
                    }
                }

                return total > 0d ? new Vector2((float)(down / total), (float)(up / total)) : Vector2.zero;
            }
        }
    }

    // Rendering Layer 1번에 알아볼 수 있는 이름을 붙인다(기본 이름 "Light Layer 1"일 때만).
    private static void NameCeilingLayer()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0)
        {
            return;
        }

        var tagManager = new SerializedObject(assets[0]);
        SerializedProperty layers = tagManager.FindProperty("m_RenderingLayers");
        if (layers == null || !layers.isArray || layers.arraySize <= CeilingLayerIndex)
        {
            return;
        }

        SerializedProperty element = layers.GetArrayElementAtIndex(CeilingLayerIndex);
        if (element.propertyType == SerializedPropertyType.String && element.stringValue == "Light Layer " + CeilingLayerIndex)
        {
            element.stringValue = CeilingLayerName;
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(assets[0]);
        }
    }
}
