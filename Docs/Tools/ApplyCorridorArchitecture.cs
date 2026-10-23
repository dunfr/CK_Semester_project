// Run this method body through Unity MCP execute_code in the open FuchsiaShadowTest scene.
// It updates only the named corridor pass; the main camera and character transforms are preserved.
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.name != "FuchsiaShadowTest" || EditorApplication.isPlaying)
{
    return "Open FuchsiaShadowTest in Edit mode before applying this pass.";
}

string root = "Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/";
var corridor = GameObject.Find("CorridorTest");
var geometry = corridor.transform.Find("Geometry_PBR");
var paint = AssetDatabase.LoadAssetAtPath<Material>(root + "Materials/Corridor/WallPaint.mat");
var inset = AssetDatabase.LoadAssetAtPath<Material>(root + "Materials/Corridor/WallInsetCoating.mat");
var trim = AssetDatabase.LoadAssetAtPath<Material>(root + "Materials/Corridor/PanelTrim.mat");
var steel = AssetDatabase.LoadAssetAtPath<Material>(root + "Materials/Corridor/DarkSteel.mat");
var rubber = AssetDatabase.LoadAssetAtPath<Material>(root + "Materials/Corridor/RubberGasket.mat");
var lamp = AssetDatabase.LoadAssetAtPath<Material>(root + "Materials/Corridor/WhiteLamp.mat");
var blueLamp = AssetDatabase.LoadAssetAtPath<Material>(root + "Materials/Corridor/BlueLamp.mat");
var pipe = AssetDatabase.LoadAssetAtPath<Material>(root + "Materials/Corridor/PipeBlue.mat");
foreach (string surface in new[] { "wall_coated", "wall_inset_coated" })
{
    AssetDatabase.ImportAsset(root + "Textures/Corridor/object_corridor_" + surface + "_mask_img.png",
        ImportAssetOptions.ForceSynchronousImport);
}

Undo.RecordObject(paint, "Separate painted wall shell");
paint.SetColor("_BaseColor", new Color(.82f, .83f, .81f, 1));
paint.SetFloat("_ClearCoatMask", .28f);
paint.SetFloat("_ClearCoatSmoothness", .76f);
paint.SetFloat("_BumpScale", .08f);
EditorUtility.SetDirty(paint);
Undo.RecordObject(inset, "Neutral metallic inset panels");
inset.SetColor("_BaseColor", new Color(.60f, .64f, .68f, 1));
inset.SetFloat("_ClearCoatMask", .18f);
inset.SetFloat("_BumpScale", .08f);
EditorUtility.SetDirty(inset);
Undo.RecordObject(trim, "Neutral exposed frame metal");
trim.SetColor("_BaseColor", new Color(.70f, .72f, .74f, 1));
trim.SetFloat("_BumpScale", .15f);
EditorUtility.SetDirty(trim);
Undo.RecordObject(steel, "Rough structural steel");
steel.SetColor("_BaseColor", new Color(.32f, .34f, .37f, 1));
steel.SetFloat("_BumpScale", .18f);
EditorUtility.SetDirty(steel);
Undo.RecordObject(pipe, "Separate blue pipe coating from silver couplings");
pipe.SetColor("_BaseColor", new Color(.17f, .36f, .55f, 1));
pipe.SetFloat("_BumpScale", .12f);
EditorUtility.SetDirty(pipe);
lamp.SetColor("_EmissionColor", new Color(6f, 5.1f, 4.15f, 1));
blueLamp.SetColor("_EmissionColor", new Color(.45f, 1.55f, 5f, 1));
EditorUtility.SetDirty(lamp);
EditorUtility.SetDirty(blueLamp);

// Combined by finish within a module: 24 bays reuse six mesh assets.
var parts = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<CombineInstance>>();
var materials = new System.Collections.Generic.Dictionary<string, Material>
{
    { "PaintShell", paint }, { "InsetPanel", inset }, { "BareMetal", trim },
    { "DarkStructure", steel }, { "Gasket", rubber }, { "WarmDiffuser", lamp }
};
var temporaryMeshes = new System.Collections.Generic.List<Mesh>();
Action<string, Vector3, Vector3, float> add = (finish, position, size, angle) =>
{
    if (!parts.ContainsKey(finish))
    {
        parts.Add(finish, new System.Collections.Generic.List<CombineInstance>());
    }
    var mesh = CorridorDetailMeshBuilder.BuildBeveledBox(size, .018f);
    temporaryMeshes.Add(mesh);
    parts[finish].Add(new CombineInstance
    {
        mesh = mesh,
        transform = Matrix4x4.TRS(position, Quaternion.Euler(0, 0, angle), Vector3.one)
    });
};

// The back of the recess sits 22 cm behind its surrounding painted frame.
add("Gasket", new Vector3(.045f, 2.08f, 0), new Vector3(.06f, 1.53f, 1.77f), 0);
add("InsetPanel", new Vector3(.105f, 2.08f, 0), new Vector3(.075f, 1.36f, 1.60f), 0);
foreach (int side in new[] { -1, 1 })
{
    add("PaintShell", new Vector3(.19f, 2.09f, side * .89f), new Vector3(.30f, 1.70f, .14f), 0);
    add("BareMetal", new Vector3(.33f, 2.10f, side * .79f), new Vector3(.045f, 1.48f, .035f), 0);
    add("PaintShell", new Vector3(.18f, 3.17f, side * .48f), new Vector3(.28f, .32f, .82f), 0);
    add("DarkStructure", new Vector3(.32f, 3.18f, side * .48f), new Vector3(.035f, .04f, .58f), 0);
    add("BareMetal", new Vector3(.37f, 2.92f, side * .74f), new Vector3(.025f, .055f, .055f), 0);
    add("BareMetal", new Vector3(.39f, 1.25f, side * .74f), new Vector3(.025f, .055f, .055f), 0);
}
add("PaintShell", new Vector3(.19f, 2.92f, 0), new Vector3(.34f, .20f, 1.94f), 0);
add("PaintShell", new Vector3(.20f, 1.25f, 0), new Vector3(.36f, .18f, 1.94f), 0);
add("BareMetal", new Vector3(.35f, 1.35f, 0), new Vector3(.05f, .035f, 1.70f), 0);
add("BareMetal", new Vector3(.35f, 2.82f, 0), new Vector3(.05f, .035f, 1.70f), 0);

// A real shadow channel behind the existing two blue pipes.
add("DarkStructure", new Vector3(.03f, .87f, 0), new Vector3(.04f, .55f, 1.88f), 0);
add("PaintShell", new Vector3(.16f, .45f, 0), new Vector3(.27f, .22f, 1.92f), 0);
add("BareMetal", new Vector3(.29f, .57f, 0), new Vector3(.06f, .04f, 1.88f), 0);

// Lower angled vent cassette, with separate blades and an actual recessed backing.
add("Gasket", new Vector3(.34f, .30f, 0), new Vector3(.10f, .28f, 1.55f), -18);
for (int blade = 0; blade < 12; blade++)
{
    add("BareMetal", new Vector3(.405f, .30f, -.68f + blade * .124f),
        new Vector3(.08f, .22f, .025f), -18);
}

// Fixture is attached to the shell rather than a floating strip.
add("DarkStructure", new Vector3(.37f, 2.15f, -.89f), new Vector3(.09f, 1.06f, .12f), 0);
add("WarmDiffuser", new Vector3(.421f, 2.15f, -.89f), new Vector3(.016f, .88f, .062f), 0);

var meshes = new System.Collections.Generic.Dictionary<string, Mesh>();
foreach (var entry in parts)
{
    var combined = new Mesh { name = "CorridorArchitecture_" + entry.Key };
    combined.CombineMeshes(entry.Value.ToArray(), true, true);
    // Baked GI용 라이트맵 UV(UV1). 합친 메시는 원본 UV1이 겹칠 수 있으므로 새로 만든다.
    Unwrapping.GenerateSecondaryUVSet(combined);
    combined.RecalculateBounds();
    string path = root + "Models/Corridor/Architecture_" + entry.Key + ".asset";
    var stored = AssetDatabase.LoadAssetAtPath<Mesh>(path);
    if (stored == null)
    {
        AssetDatabase.CreateAsset(combined, path);
        stored = combined;
    }
    else
    {
        EditorUtility.CopySerialized(combined, stored);
        EditorUtility.SetDirty(stored);
        UnityEngine.Object.DestroyImmediate(combined);
    }
    meshes.Add(entry.Key, stored);
}
foreach (var mesh in temporaryMeshes)
{
    UnityEngine.Object.DestroyImmediate(mesh);
}

var oldPass = corridor.transform.Find("WallArchitecturePass");
if (oldPass != null)
{
    Undo.DestroyObjectImmediate(oldPass.gameObject);
}
var pass = new GameObject("WallArchitecturePass");
Undo.RegisterCreatedObjectUndo(pass, "Corridor wall depth pass");
pass.transform.SetParent(corridor.transform, false);
foreach (int side in new[] { -1, 1 })
{
    for (int bay = 0; bay < 12; bay++)
    {
        var module = new GameObject("RecessedWall_" + side + "_" + bay);
        module.transform.SetParent(pass.transform, false);
        module.transform.localPosition = new Vector3(side * 3f, 0, -6 + bay * 2);
        module.transform.localRotation = Quaternion.Euler(0, side < 0 ? 0 : 180, 0);
        foreach (var mesh in meshes)
        {
            var piece = new GameObject(mesh.Key);
            piece.transform.SetParent(module.transform, false);
            piece.AddComponent<MeshFilter>().sharedMesh = mesh.Value;
            var renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials[mesh.Key];
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
            renderer.shadowCastingMode = mesh.Key == "WarmDiffuser"
                ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
        }
        var oldInset = geometry.Find("WallInset_" + side + "_" + bay);
        if (oldInset != null)
        {
            Undo.RecordObject(oldInset.GetComponent<Renderer>(), "Replace flat inset");
            oldInset.GetComponent<Renderer>().enabled = false;
        }
    }
}

// Preserve old service labels/handles, moving them forward onto the new recessed plate.
var surfaceDetails = corridor.transform.Find("SurfaceDetailPass");
foreach (var item in surfaceDetails.GetComponentsInChildren<Transform>(true))
{
    if (item == surfaceDetails)
    {
        continue;
    }
    if (item.name.StartsWith("PanelGasket_") || item.name.StartsWith("InsetServiceLid_") ||
        item.name.StartsWith("LidHandle_") || item.name.StartsWith("ServiceStencil_") ||
        item.name.StartsWith("CautionStencil_"))
    {
        var position = item.position;
        float depth = item.name.StartsWith("PanelGasket_") ? 2.845f :
            item.name.StartsWith("InsetServiceLid_") ? 2.825f : 2.795f;
        position.x = Mathf.Sign(position.x) * depth;
        Undo.RecordObject(item, "Place service detail on recessed wall");
        item.position = position;
    }
}

// Keep the existing distribution boxes within the pipe channel, clear of the new sill.
foreach (var item in corridor.transform.Find("ReferenceLookPass").GetComponentsInChildren<Transform>(true))
{
    if (item.name.StartsWith("WallServiceHousing_") || item.name.StartsWith("Vent_"))
    {
        var position = item.position;
        if (item.name.StartsWith("WallServiceHousing_"))
        {
            position.y = .88f;
        }
        else
        {
            int blade = int.Parse(item.name.Substring(item.name.LastIndexOf('_') + 1));
            position.y = .71f + blade * .10f;
        }
        Undo.RecordObject(item, "Clear pipe distribution box from wall sill");
        item.position = position;
    }
}

AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
SceneView.RepaintAll();
EditorApplication.QueuePlayerLoopUpdate();
return new { modules = 24, sharedMeshes = meshes.Count, renderers = pass.GetComponentsInChildren<Renderer>().Length };
