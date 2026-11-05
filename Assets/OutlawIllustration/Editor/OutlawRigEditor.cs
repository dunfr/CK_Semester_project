using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OutlawIllustration.Editor
{
    public static class OutlawRigSetup
    {
        private const string PackageRoot = "Assets/OutlawIllustration";
        private const string DefaultRigPath = PackageRoot + "/Data/rig.json";

        [MenuItem("GameObject/UI/Outlaw Animated Illustration", false, 2050)]
        [MenuItem("Tools/Outlaw Illustration/Create UI Illustration")]
        public static void CreateUI()
        {
            TextAsset json = FindRig();
            if (json == null)
            {
                EditorUtility.DisplayDialog("Outlaw Illustration", "Select the included rig.json asset, then run this menu again.", "OK");
                return;
            }
            RectTransform parent = Selection.activeTransform as RectTransform;
            Canvas canvas = parent != null ? parent.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
            {
                canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
                parent = canvas != null ? canvas.transform as RectTransform : null;
            }
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("Outlaw Illustration Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1088);
                scaler.matchWidthOrHeight = 0.5f;
                parent = canvasObject.transform as RectTransform;
                Undo.RegisterCreatedObjectUndo(canvasObject, "Create Outlaw Illustration Canvas");
            }
            if (parent == null) parent = canvas.transform as RectTransform;
            GameObject root = new GameObject("Outlaw Animated Illustration", typeof(RectTransform), typeof(Canvas), typeof(AspectRatioFitter), typeof(OutlawRigPlayer));
            root.transform.SetParent(parent, false);
            root.GetComponent<Canvas>().overrideSorting = false;
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1920, 1088);
            OutlawRigPlayer player = root.GetComponent<OutlawRigPlayer>();
            BindTextures(player, json);
            AspectRatioFitter fit = root.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = (float)player.Rig.width / player.Rig.height;
            Undo.RegisterCreatedObjectUndo(root, "Create Outlaw Animated Illustration");
            Selection.activeGameObject = root;
            EditorUtility.SetDirty(root);
            Debug.Log("Outlaw illustration created. Press Play. Adjust Intensity / Speed on OutlawRigPlayer.", root);
        }

        public static void BindTextures(OutlawRigPlayer player, TextAsset json)
        {
            OutlawRigData rig = JsonUtility.FromJson<OutlawRigData>(json.text);
            if (rig == null) throw new InvalidDataException("rig.json could not be read.");
            rig.Normalize();
            string jsonPath = AssetDatabase.GetAssetPath(json);
            string jsonFolder = Path.GetDirectoryName(jsonPath);
            player.rigJson = json;
            player.textures.Clear();
            for (int i = 0; i < rig.layers.Length; i++)
            {
                OutlawLayerData layer = rig.layers[i];
                string texturePath = ResolveAssetPath(jsonFolder, layer.path);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (texture == null) throw new FileNotFoundException("Missing layer PNG: " + texturePath);
                player.textures.Add(new OutlawRigPlayer.TextureBinding { layerName = layer.name, texture = texture });
            }
            player.Rebuild();
            EditorUtility.SetDirty(player);
        }

        private static string ResolveAssetPath(string jsonFolder, string relativePath)
        {
            if (String.IsNullOrEmpty(relativePath)) throw new InvalidDataException("A layer has no texture path.");
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string absolute = Path.GetFullPath(Path.Combine(projectRoot, jsonFolder, relativePath));
            string assetRoot = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            if (!absolute.StartsWith(assetRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A layer texture path leaves the project's Assets folder.");
            return "Assets/" + absolute.Substring(assetRoot.Length).Replace('\\', '/');
        }

        private static TextAsset FindRig()
        {
            TextAsset selected = Selection.activeObject as TextAsset;
            if (selected != null && AssetDatabase.GetAssetPath(selected).EndsWith("rig.json", StringComparison.OrdinalIgnoreCase)) return selected;
            TextAsset included = AssetDatabase.LoadAssetAtPath<TextAsset>(DefaultRigPath);
            if (included != null) return included;
            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new string[] { PackageRoot });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.EndsWith("/rig.json", StringComparison.OrdinalIgnoreCase)) return AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            }
            return null;
        }
    }

    [CustomEditor(typeof(OutlawRigPlayer))]
    public sealed class OutlawRigPlayerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            OutlawRigPlayer player = (OutlawRigPlayer)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Intensity changes the amount of movement. Speed changes playback speed. The background can be disabled for transparent UI placement.", MessageType.Info);
            if (GUILayout.Button("Rebind Included Textures / Rebuild"))
            {
                if (player.rigJson != null) OutlawRigSetup.BindTextures(player, player.rigJson);
            }
            if (Application.isPlaying)
            {
                if (GUILayout.Button(player.IsPlaying ? "Pause" : "Play"))
                {
                    if (player.IsPlaying) player.Pause(); else player.Play();
                }
            }
            else if (player.Rig != null)
            {
                EditorGUI.BeginChangeCheck();
                float time = EditorGUILayout.Slider("Preview Time", player.previewTime, 0, player.Rig.duration);
                if (EditorGUI.EndChangeCheck()) { player.SetTime(time); EditorUtility.SetDirty(player); }
            }
        }
    }
}
