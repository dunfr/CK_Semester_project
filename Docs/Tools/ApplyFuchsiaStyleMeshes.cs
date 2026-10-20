// MCP execute_code method body. Source FBX and base UVs/normals remain intact.
var model = GameObject.Find("Fuchsia_Toon");
if (EditorApplication.isPlaying || model == null)
{
    return "Run in Edit mode with Fuchsia_Toon in the open scene.";
}
string root = "Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/";
string folder = root + "Models/CharacterStyle";
if (!AssetDatabase.IsValidFolder(folder))
{
    AssetDatabase.CreateFolder(root + "Models", "CharacterStyle");
}
var sourceMeshes = AssetDatabase.LoadAllAssetsAtPath(root + "Models/character_fuchsia_01.fbx")
    .OfType<Mesh>().ToArray();
int count = 0;
foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
{
    var material = renderer.sharedMaterial;
    bool hair = material.name == "FuchsiaHairToon";
    bool face = material.name == "FuchsiaFaceToon";
    bool legs = material.name == "FuchsiaOutfit02Toon";
    if (!hair && !face && !legs)
    {
        continue;
    }

    var filter = renderer.GetComponent<MeshFilter>();
    string sourceName = filter.sharedMesh.name.Replace("_Style", "");
    var source = sourceMeshes.First(m => m.name == sourceName);
    var mesh = UnityEngine.Object.Instantiate(source);
    mesh.name = source.name + "_Style";
    Vector3[] vertices = source.vertices;
    var styleNormals = new System.Collections.Generic.List<Vector3>();
    var styleCoordinates = new System.Collections.Generic.List<Vector4>();
    foreach (var vertex in vertices)
    {
        Vector3 point = model.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
        Vector3 styleNormal;
        if (legs)
        {
            float centerX = point.x < 0 ? -.092f : .092f;
            // A cylinder follows the length of each leg and removes the knee's upward-facing bulge.
            styleNormal = new Vector3((point.x - centerX) / .065f, .18f,
                (point.z - .33f) / .075f).normalized;
        }
        else
        {
            styleNormal = new Vector3(point.x / .12f, (point.y - 1.445f) / .13f,
                (point.z - .30f) / .095f).normalized;
        }
        styleNormals.Add(renderer.transform.InverseTransformDirection(
            model.transform.TransformDirection(styleNormal)).normalized);
        if (hair)
        {
            styleCoordinates.Add(new Vector4(point.x / .125f,
                (point.y - 1.31f) / .28f, (point.z - .30f) / .10f, .375f));
        }
        else
        {
            styleCoordinates.Add(new Vector4(point.x / .17f + .5f,
                (point.y - 1.445f) / .19f + .5f, (point.z - .33f) / .09f, .375f));
        }
    }
    mesh.SetUVs(1, styleNormals);
    mesh.SetUVs(2, styleCoordinates);
    string path = folder + "/" + source.name + "_Style.asset";
    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
    if (existing == null)
    {
        AssetDatabase.CreateAsset(mesh, path);
        existing = mesh;
    }
    else
    {
        EditorUtility.CopySerialized(mesh, existing);
        EditorUtility.SetDirty(existing);
        UnityEngine.Object.DestroyImmediate(mesh);
    }
    Undo.RecordObject(filter, "Character shading projection data");
    filter.sharedMesh = existing;
    if (hair || face)
    {
        material.SetVector("_StyleRightOS", renderer.transform.InverseTransformDirection(model.transform.right));
        material.SetVector("_StyleUpOS", renderer.transform.InverseTransformDirection(model.transform.up));
        material.SetVector("_StyleForwardOS", renderer.transform.InverseTransformDirection(model.transform.forward));
    }
    EditorUtility.SetDirty(material);
    count++;
}
AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
return new { styledRenderers = count };
