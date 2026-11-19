using CK.SemesterProject.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// FSR 2를 고를 수 있게 URP 에셋을 맞춘다. 품질 단계에 쓰는 모든 URP 에셋에
// FSR 2 옵션 데이터를 넣고, 업스케일러 이름을 FSR 2로 적어 둔다(업스케일 필터 자체는 건드리지 않는다).
// 에셋 인스펙터에서 업스케일러를 다른 것으로 바꿔 FSR 2가 "지원 안 됨"으로 나오면 이 메뉴를 다시 실행하면 된다.
public static class AntiAliasingFsr2Setup
{
    private const string MenuPath = "Tools/Graphics/FSR 2 설정 맞추기 (URP 에셋)";

    [MenuItem(MenuPath)]
    public static void Apply()
    {
#if ENABLE_UPSCALER_FRAMEWORK
        int changed = 0;
        for (int level = 0; level < QualitySettings.names.Length; level++)
        {
            if (QualitySettings.GetRenderPipelineAssetAt(level) is UniversalRenderPipelineAsset asset && ApplyTo(asset))
            {
                changed++;
            }
        }

        if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset defaultAsset && ApplyTo(defaultAsset))
        {
            changed++;
        }

        Debug.Log($"[AntiAliasing] FSR 2 설정 확인 완료. 바뀐 URP 에셋 {changed}개.");
#else
        Debug.LogWarning("[AntiAliasing] ENABLE_UPSCALER_FRAMEWORK 정의가 없어 FSR 2를 쓸 수 없습니다. Player Settings의 Scripting Define Symbols를 확인하세요.");
#endif
    }

#if ENABLE_UPSCALER_FRAMEWORK
    private static bool ApplyTo(UniversalRenderPipelineAsset asset)
    {
        var serialized = new SerializedObject(asset);
        SerializedProperty options = serialized.FindProperty("m_UpscalerOptions");
        SerializedProperty upscalerName = serialized.FindProperty("m_IUpscalerName");
        if (options == null || upscalerName == null)
        {
            Debug.LogWarning($"[AntiAliasing] {asset.name}: 이 URP 버전에서 업스케일러 설정 항목을 찾지 못했습니다.");
            return false;
        }

        bool changed = UpscalerOptions.ValidateSerializedUpscalerOptionReferencesWithinRPAsset(asset, options);
        if (upscalerName.stringValue != AntiAliasingSettings.Fsr2UpscalerName)
        {
            upscalerName.stringValue = AntiAliasingSettings.Fsr2UpscalerName;
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
        return true;
    }
#endif
}
