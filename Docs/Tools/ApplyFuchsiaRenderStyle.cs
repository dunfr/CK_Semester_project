// Unity CLI eval_file method body. Run with FuchsiaShadowTest open, in Edit mode.
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (EditorApplication.isPlaying || scene.name != "FuchsiaShadowTest")
{
    return "Open FuchsiaShadowTest in Edit mode first.";
}
string root = "Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/";
var environmentShader = Shader.Find("CK/Prototype/Fuchsia/Environment");
if (environmentShader == null || ShaderUtil.ShaderHasError(environmentShader))
{
    return "Import and compile FuchsiaEnvironment.shader first.";
}
var pipeline = UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset;
var cameraData = Camera.main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
var lookVolume = GameObject.Find("CorridorLook_GlobalVolume").GetComponent<UnityEngine.Rendering.Volume>();
var lookProfile = lookVolume.sharedProfile;
if (pipeline == null || !pipeline.supportsHDR || !cameraData.renderPostProcessing
    || !lookVolume.enabled || (cameraData.volumeLayerMask & (1 << lookVolume.gameObject.layer)) == 0
    || !lookProfile.TryGet<UnityEngine.Rendering.Universal.ColorAdjustments>(out var colorAdjustments)
    || !lookProfile.TryGet<UnityEngine.Rendering.Universal.Bloom>(out var bloom))
{
    return "Check HDR, camera post-processing, volume mask and look profile first.";
}
var character = GameObject.Find("Fuchsia_Toon");
var materials = character.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials)
    .Where(m => m != null).Distinct().ToArray();
foreach (var material in materials)
{
    Undo.RecordObject(material, "Fuchsia reference rendering");
    if (material.shader.name == "Semester/Fuchsia/Lens Toon")
    {
        material.SetFloat("_Opacity", .015f);
        material.SetFloat("_EdgeOpacity", .06f);
        material.SetFloat("_HighlightStrength", .08f);
        EditorUtility.SetDirty(material);
        continue;
    }
    bool isFace = material.name == "FuchsiaFaceToon";
    bool isEyes = material.name == "FuchsiaEyesToon";
    bool isHair = material.name == "FuchsiaHairToon";
    bool isMetal = material.shader.name.Contains("Accessory");
    bool isLegs = material.name == "FuchsiaOutfit02Toon";
    bool isHands = material.name == "FuchsiaHandsToon";
    bool isCoat = material.name == "FuchsiaCoatToon";
    material.SetColor("_BaseColor", isFace || isHands ? new Color(1f, .95f, .93f, 1f) : Color.white);
    material.SetFloat("_OutlineWidth", isEyes || isLegs ? 0f : isFace ? .3f : isHair ? .6f : isMetal ? .5f : .65f);
    material.SetColor("_OutlineColor", new Color(.19f, .12f, .18f, 1));
    material.DisableKeyword("_EMISSION");
    material.SetFloat("_DirectLightStrength", isFace ? 2f : isEyes ? 1.7f : isCoat ? 1.8f : isHair ? 1.6f : 1.3f);
    material.SetFloat("_DirectLightCeiling", isFace ? .90f : isEyes ? .80f : .68f);
    material.SetFloat("_AmbientStrength", isFace ? .60f : .55f);
    material.SetFloat("_AdditionalLightStrength", isFace ? .60f : isEyes ? .50f : .65f);
    material.SetFloat("_AdditionalLightCeiling", isFace ? .35f : .32f);
    material.SetFloat("_AdditionalShadowFill", 1f);
    material.SetFloat("_ShadingStrength", isFace ? .65f : isEyes ? .15f : isCoat ? .70f : isHair ? .85f : 1f);
    material.SetFloat("_ShadeFeather", .016f);
    material.SetFloat("_ShadeStep", isHair ? .32f : isLegs ? .18f : .38f);
    material.SetFloat("_RimStrength", isEyes || isFace ? 0f : isMetal ? .06f : isHair ? .05f : .03f);
    material.SetFloat("_RimPower", 6f);
    material.SetColor("_RimColor", Color.white);
    if (isFace)
    {
        material.SetFloat("_StyleNormalStrength", .65f);
        material.SetFloat("_FaceShadowOffset", .05f);
        material.SetFloat("_FaceShadowFeather", .018f);
        material.SetFloat("_FaceAmbientFill", .10f);
        material.SetColor("_ShadeColor", new Color(.82f, .72f, .70f, 1));
    }
    else if (isHair)
    {
        material.SetFloat("_StyleNormalStrength", .8f);
        material.SetFloat("_HairAmbientFill", .06f);
        material.SetFloat("_HairBandWidth", .025f);
        material.SetFloat("_HairBandFeather", .008f);
        material.SetFloat("_HighlightStrength", .16f);
        material.SetColor("_ShadeColor", new Color(.78f, .74f, .80f, 1));
    }
    else if (!isEyes)
    {
        material.SetColor("_ShadeColor", isCoat ? new Color(.78f, .74f, .80f, 1)
            : isLegs ? new Color(.72f, .71f, .74f, 1)
            : new Color(.64f, .64f, .68f, 1));
        if (isMetal)
        {
            material.SetFloat("_HighlightStrength", .16f);
            material.SetFloat("_HighlightSize", .035f);
            material.SetFloat("_HighlightFeather", .01f);
        }
    }
    // Remove serialized remnants as well as the unused shader controls.
    var serialized = new SerializedObject(material);
    foreach (string group in new[] { "m_Floats", "m_Colors", "m_TexEnvs" })
    {
        var entries = serialized.FindProperty("m_SavedProperties." + group);
        for (int i = entries.arraySize - 1; i >= 0; i--)
        {
            string key = entries.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
            if (new[] { "_DeepShadeColor", "_DeepShadeStep", "_UseEmission", "_EmissionColor", "_EmissionMap" }.Contains(key))
            {
                entries.DeleteArrayElementAtIndex(i);
            }
        }
    }
    serialized.ApplyModifiedPropertiesWithoutUndo();
    EditorUtility.SetDirty(material);
}
int environmentCount = 0;
foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { root + "Materials/Corridor" }))
{
    var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
    if (material.renderQueue >= 2500 || material.shader.name.Contains("Floor Reflection"))
    {
        continue;
    }
    Undo.RecordObject(material, "Reference PBR environment");
    string[] oldKeywords = material.shaderKeywords;
    material.shader = environmentShader;
    material.shaderKeywords = oldKeywords;
    string[] properties = { "_UseNormalMap", "_UseMetallicMap", "_UseOcclusionMap", "_UseEmission", "_UseClearCoat", "_UseClearCoatMap" };
    string[] keywords = { "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP", "_EMISSION", "_CLEARCOAT", "_CLEARCOATMAP" };
    for (int i = 0; i < properties.Length; i++)
    {
        material.SetFloat(properties[i], oldKeywords.Contains(keywords[i]) ? 1f : 0f);
    }
    bool isRubber = material.name.Contains("Rubber");
    bool isFloor = material.name == "FloorMetal";
    bool isCoating = material.name.Contains("Paint") || material.name.Contains("Coating");
    bool isLamp = material.name.Contains("Lamp");
    material.SetFloat("_SmoothnessLimit", isRubber ? .22f : isFloor ? .56f : isCoating ? .46f : .70f);
    material.SetFloat("_DetailStrength", .20f);
    material.SetFloat("_UseDetail", oldKeywords.Contains("_DETAIL_SCALED") || oldKeywords.Contains("_DETAIL_MULX2") ? 1 : 0);
    material.SetFloat("_BumpScale", isFloor ? .25f : material.name == "PanelTrim" ? .20f : isCoating ? .45f : .65f);
    material.SetFloat("_IndirectStrength", isFloor ? .90f : 1.20f);
    material.SetColor("_IndirectTint", new Color(.98f, .99f, 1f, 1f));
    material.SetFloat("_EmissionStrength", isLamp ? .65f : 1f);
    if (isFloor)
    {
        material.SetColor("_BaseColor", new Color(.69f, .70f, .72f, 1f));
    }
    else if (material.name == "WallInsetCoating")
    {
        material.SetColor("_BaseColor", new Color(.72f, .74f, .78f, 1f));
    }
    EditorUtility.SetDirty(material);
    environmentCount++;
}
var volume = GameObject.Find("FuchsiaIndoorLightingVolume").GetComponent<FuchsiaIndoorLighting>();
Undo.RecordObject(volume, "Reference character key");
var volumeSerialized = new SerializedObject(volume);
volumeSerialized.FindProperty("_keyIntensity").floatValue = .24f;
volumeSerialized.ApplyModifiedProperties();
volume.RefreshLighting();
// 가상 스크린샷의 회청색 간접광, 따뜻한 천장광, 차가운 보조광을 분리한다.
RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
RenderSettings.ambientSkyColor = new Color(.58f, .61f, .69f);
RenderSettings.ambientEquatorColor = new Color(.46f, .49f, .57f);
RenderSettings.ambientGroundColor = new Color(.27f, .30f, .37f);
DynamicGI.UpdateEnvironment();
foreach (var source in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
{
    Undo.RecordObject(source, "Mock screenshot corridor lighting");
    if (source.name.StartsWith("CeilingSpot_"))
    {
        source.color = new Color(1f, .95f, .90f);
        source.intensity = 14f;
    }
    else if (source.name.StartsWith("WallWash_"))
    {
        source.color = new Color(1f, .95f, .90f);
        source.intensity = 7f;
    }
    else if (source.name.StartsWith("FloorBlueFill_"))
    {
        source.color = new Color(.46f, .65f, 1f);
        source.intensity = .35f;
        source.range = 3.8f;
    }
    else if (source.name.StartsWith("CoolFill_"))
    {
        source.color = new Color(.72f, .82f, 1f);
        source.intensity = .75f;
        source.range = 5.5f;
        Undo.RecordObject(source.transform, "Corridor side fill position");
        source.transform.position = new Vector3(source.transform.position.x, 1.55f, 2.2f);
    }
    else if (source.name == "CharacterEdgeAccent")
    {
        source.color = new Color(1f, .62f, .78f);
        source.intensity = .8f;
        source.range = 3.8f;
        Undo.RecordObject(source.transform, "Corridor character accent position");
        source.transform.position = new Vector3(-1.35f, 1.8f, 4.18f);
    }
    EditorUtility.SetDirty(source);
}
Undo.RecordObject(colorAdjustments, "Mock screenshot exposure");
Undo.RecordObject(bloom, "Mock screenshot bloom");
colorAdjustments.postExposure.Override(.35f);
colorAdjustments.contrast.Override(4f);
bloom.intensity.Override(.20f);
EditorUtility.SetDirty(colorAdjustments);
EditorUtility.SetDirty(bloom);
EditorUtility.SetDirty(lookProfile);
// 기존 모델 교체 후에도 전신·플레이 구도가 같은 캐릭터 중심을 보도록 맞춘다.
var characterRenderers = character.GetComponentsInChildren<Renderer>();
Bounds bounds = characterRenderers[0].bounds;
foreach (var renderer in characterRenderers)
{
    bounds.Encapsulate(renderer.bounds);
}
var wideView = GameObject.Find("WideComparisonCamera").GetComponent<Camera>();
var rearView = GameObject.Find("ToonDetailCamera").GetComponent<Camera>();
Undo.RecordObject(wideView.transform, "Reference gameplay camera");
Undo.RecordObject(rearView.transform, "Reference rear camera");
wideView.transform.position = bounds.center + new Vector3(.45f, .65f, -3.4f);
wideView.transform.LookAt(bounds.center + Vector3.up * .15f);
rearView.transform.position = bounds.center + new Vector3(.15f, .60f, -2.45f);
rearView.transform.LookAt(bounds.center + Vector3.up * .48f);
Undo.RecordObject(rearView, "Mock screenshot rear framing");
rearView.fieldOfView = 50f;
var movement = GameObject.Find("FuchsiaMovementRig").GetComponent("FuchsiaTestMovement");
var movementSerialized = new SerializedObject(movement);
movementSerialized.FindProperty("_initialCameraView").intValue = 1;
movementSerialized.ApplyModifiedProperties();
Undo.RecordObject(Camera.main.transform, "Reference rear preview");
Undo.RecordObject(Camera.main, "Reference preview field of view");
Camera.main.transform.SetPositionAndRotation(rearView.transform.position, rearView.transform.rotation);
Camera.main.fieldOfView = rearView.fieldOfView;
var reflection = GameObject.Find("PolishedFloorReflection");
if (reflection != null)
{
    Undo.DestroyObjectImmediate(reflection);
}
var rendererData = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(
    root + "Data/FuchsiaTestRenderer.asset");
var outline = rendererData.rendererFeatures.OfType<FuchsiaOutlineFeature>().FirstOrDefault();
if (outline == null)
{
    outline = ScriptableObject.CreateInstance<FuchsiaOutlineFeature>();
    outline.name = "Fuchsia Character Outline";
    AssetDatabase.AddObjectToAsset(outline, rendererData);
    rendererData.rendererFeatures.Add(outline);
    var rendererSerialized = new SerializedObject(rendererData);
    var featureMap = rendererSerialized.FindProperty("m_RendererFeatureMap");
    featureMap.arraySize = rendererData.rendererFeatures.Count;
    for (int i = 0; i < rendererData.rendererFeatures.Count; i++)
    {
        string guid;
        long localId;
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(rendererData.rendererFeatures[i], out guid, out localId);
        featureMap.GetArrayElementAtIndex(i).longValue = localId;
    }
    rendererSerialized.ApplyModifiedPropertiesWithoutUndo();
}
outline.SetActive(true);
rendererData.SetDirty();
EditorUtility.SetDirty(rendererData);
AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return new { characterMaterials = materials.Length, environmentMaterials = environmentCount };
