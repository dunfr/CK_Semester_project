// Run this method body through Unity MCP execute_code after ApplyCorridorArchitecture.cs.
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.name != "FuchsiaShadowTest" || EditorApplication.isPlaying)
{
    return "Open FuchsiaShadowTest in Edit mode before applying the lighting pass.";
}

var corridor = GameObject.Find("CorridorTest");
var key = GameObject.Find("KeyLight_AdjustRotation").GetComponent<Light>();
Undo.RecordObject(key, "Warm primary light");
key.color = new Color(1f, .93f, .85f);
key.intensity = .28f;

RenderSettings.ambientSkyColor = new Color(.47f, .49f, .54f);
RenderSettings.ambientEquatorColor = new Color(.34f, .36f, .41f);
RenderSettings.ambientGroundColor = new Color(.19f, .21f, .26f);

foreach (var light in corridor.GetComponentsInChildren<Light>())
{
    Undo.RecordObject(light, "Separate corridor warm key and cool fill");
    if (light.name.StartsWith("CeilingSpot_"))
    {
        light.color = new Color(1f, .91f, .82f);
        light.intensity = 14f;
        light.range = 6.8f;
    }
    else if (light.name.StartsWith("WallWash_"))
    {
        int side = light.transform.position.x < 0 ? -1 : 1;
        var position = light.transform.position;
        position.x = side * 2.18f;
        position.y = 2.94f;
        Undo.RecordObject(light.transform, "Graze recessed wall with warm light");
        light.transform.position = position;
        light.transform.rotation = Quaternion.LookRotation(
            new Vector3(side * 2.96f, 1.65f, position.z + 1.10f) - position);
        light.color = new Color(1f, .91f, .80f);
        light.intensity = 7f;
        light.range = 4.5f;
        light.spotAngle = 108f;
        light.innerSpotAngle = 55f;
        bool isNearWall = Mathf.Abs(position.z - 3.25f) < .1f;
        light.shadows = isNearWall ? LightShadows.Soft : LightShadows.None;
        light.shadowBias = .035f;
        light.shadowNormalBias = .10f;
        var data = light.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
        if (data != null && isNearWall)
        {
            var settings = new SerializedObject(data);
            settings.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = 1;
            settings.ApplyModifiedProperties();
        }
    }
    else if (light.name == "CoolFill_Left" || light.name == "WarmFill_Right" || light.name == "CoolFill_Right")
    {
        light.name = light.transform.position.x < 0 ? "CoolFill_Left" : "CoolFill_Right";
        light.color = new Color(.40f, .63f, 1f);
        light.intensity = .42f;
        light.range = 3.7f;
    }
    else if (light.name == "CharacterEdgeAccent")
    {
        light.color = new Color(1f, .48f, .68f);
        light.intensity = .50f;
    }
}

var oldAccent = corridor.transform.Find("WallArchitecturePass/CoolFloorAccents");
if (oldAccent != null)
{
    Undo.DestroyObjectImmediate(oldAccent.gameObject);
}
var accents = new GameObject("CoolFloorAccents");
Undo.RegisterCreatedObjectUndo(accents, "Cool architectural fill lights");
accents.transform.SetParent(corridor.transform.Find("WallArchitecturePass"), false);
foreach (int side in new[] { -1, 1 })
{
    for (int bay = 0; bay < 5; bay++)
    {
        var fixture = new GameObject("FloorBlueFill_" + side + "_" + bay);
        fixture.transform.SetParent(accents.transform, false);
        fixture.transform.position = new Vector3(side * 2.50f, .36f, bay * 4f);
        var light = fixture.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(.32f, .59f, 1f);
        light.intensity = .55f;
        light.range = 3.3f;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.Auto;
    }
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
SceneView.RepaintAll();
EditorApplication.QueuePlayerLoopUpdate();
return new { warmCeilingLights = 6, warmWallLights = 10, coolFloorLights = 10 };
