// Run through Unity MCP execute_code in FuchsiaShadowTest, in Edit mode.
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.name != "FuchsiaShadowTest" || EditorApplication.isPlaying)
{
    return "Open FuchsiaShadowTest in Edit mode.";
}
string root = "Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/";
string rendererPath = root + "Data/FuchsiaTestRenderer.asset";
if (AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(rendererPath) == null)
{
    AssetDatabase.CopyAsset("Assets/CK_Semester_Project/Settings/PC_Renderer.asset", rendererPath);
}
var rendererData = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(rendererPath);
var feature = rendererData.rendererFeatures.OfType<FuchsiaHairShadowFeature>().FirstOrDefault();
if (feature == null)
{
    feature = ScriptableObject.CreateInstance<FuchsiaHairShadowFeature>();
    feature.name = "Fuchsia Front Hair Shadow";
    AssetDatabase.AddObjectToAsset(feature, rendererData);
    rendererData.rendererFeatures.Add(feature);
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
feature.SetActive(true);
rendererData.SetDirty();
EditorUtility.SetDirty(rendererData);

var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
var pipelineSerialized = new SerializedObject(pipeline);
var rendererList = pipelineSerialized.FindProperty("m_RendererDataList");
int rendererIndex = -1;
for (int i = 0; i < rendererList.arraySize; i++)
{
    if (rendererList.GetArrayElementAtIndex(i).objectReferenceValue == rendererData)
    {
        rendererIndex = i;
    }
}
if (rendererIndex < 0)
{
    rendererIndex = rendererList.arraySize;
    rendererList.arraySize++;
    rendererList.GetArrayElementAtIndex(rendererIndex).objectReferenceValue = rendererData;
    pipelineSerialized.ApplyModifiedPropertiesWithoutUndo();
}
foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
{
    if (camera.gameObject.scene == scene)
    {
        var data = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (data != null)
        {
            Undo.RecordObject(data, "Fuchsia test renderer");
            data.SetRenderer(rendererIndex);
            EditorUtility.SetDirty(data);
        }
    }
}
foreach (var reflection in UnityEngine.Object.FindObjectsByType<CorridorPlanarReflection>(FindObjectsSortMode.None))
{
    var serialized = new SerializedObject(reflection);
    serialized.FindProperty("_rendererIndex").intValue = rendererIndex;
    serialized.ApplyModifiedProperties();
}

string hairPath = root + "Textures/character_fuchsia_hair_control_img.png";
var importer = (TextureImporter)AssetImporter.GetAtPath(hairPath);
importer.sRGBTexture = false;
importer.mipmapEnabled = false;
importer.textureCompression = TextureImporterCompression.Uncompressed;
importer.wrapModeU = TextureWrapMode.Repeat;
importer.wrapModeV = TextureWrapMode.Clamp;
importer.filterMode = FilterMode.Bilinear;
importer.SaveAndReimport();
var hairMap = AssetDatabase.LoadAssetAtPath<Texture2D>(hairPath);
var model = GameObject.Find("Fuchsia_Toon");
var materials = model.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials)
    .Where(m=>m!=null && m.shader.name != "Semester/Fuchsia/Glasses Lens").Distinct().ToArray();
foreach (var material in materials)
{
    if (!material.HasProperty("_AdditionalLightStrength"))
    {
        continue;
    }
    Undo.RecordObject(material, "Fuchsia key and fill shading");
    bool isFace = material.name.Contains("Face") || material.name.Contains("Eyes");
    bool isHair = material.name == "FuchsiaHairToon";
    material.SetFloat("_AdditionalLightStrength", isFace ? .35f : .65f);
    material.SetFloat("_AdditionalLightCeiling", isFace ? .35f : .55f);
    material.SetFloat("_AdditionalShadowFill", .8f);
    material.SetFloat("_DirectLightCeiling", isFace ? .48f : isHair ? .55f : .60f);
    material.SetFloat("_DirectLightStrength", 1.6f);
    if (material.name == "FuchsiaFaceToon")
    {
        material.SetFloat("_FaceShadowOffset", .12f);
        material.SetFloat("_FaceShadowFeather", .025f);
        material.SetFloat("_FaceAmbientFill", .35f);
        material.SetFloat("_HairShadowStrength", .75f);
        material.SetFloat("_HairShadowDistance", .035f);
        material.SetFloat("_HairShadowBias", .003f);
        material.SetColor("_ShadeColor", new Color(.80f,.71f,.76f,1f));
        material.SetFloat("_DirectLightStrength", 1.8f);
    }
    else if (material.name == "FuchsiaHairToon")
    {
        material.SetTexture("_HairHighlightMap", hairMap);
        material.SetFloat("_HairHighlightMaskStrength", 1f);
        material.SetFloat("_HairBandWidth", .033f);
        material.SetFloat("_HairBandFeather", .012f);
        material.SetFloat("_HairAmbientFill", .18f);
        material.SetFloat("_HighlightStrength", .65f);
        material.SetFloat("_ReceiveShadowStrength", .50f);
        material.SetColor("_ShadeColor", new Color(.69f,.73f,.85f,1f));
    }
    EditorUtility.SetDirty(material);
}
var indoorVolume = GameObject.Find("FuchsiaIndoorLightingVolume");
if (indoorVolume != null)
{
    var indoorLighting = indoorVolume.GetComponent<FuchsiaIndoorLighting>();
    if (indoorLighting != null)
    {
        var indoorSerialized = new SerializedObject(indoorLighting);
        indoorSerialized.FindProperty("_keyIntensity").floatValue = .12f;
        indoorSerialized.ApplyModifiedProperties();
        indoorLighting.RefreshLighting();
    }
}
EditorUtility.SetDirty(pipeline);
AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return new { rendererIndex, rendererPath, materials = materials.Length };
