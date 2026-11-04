using System.IO;
using System.Linq;
using CK.SemesterProject.UI;
using CK.SemesterProject.UIAnimation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class PressAnyKeyBannerSetup
{
    [InitializeOnLoadMethod]
    private static void OnReload()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists("Temp/TitleBannerUseCode")) return;
            File.Delete("Temp/TitleBannerUseCode");
            Scene scene = SceneManager.GetActiveScene();
            GameObject banner = scene.GetRootGameObjects().Single(root => root.name == "Canvas")
                .transform.Find("Press Any key Banner").gameObject;
            UIAnimationPlayer player = banner.GetComponent<UIAnimationPlayer>();
            if (player != null) Object.DestroyImmediate(player);
            Animator animator = banner.GetComponent<Animator>();
            if (animator != null) Object.DestroyImmediate(animator);
            if (banner.GetComponent<CanvasGroup>() == null) banner.AddComponent<CanvasGroup>();
            banner.GetComponent<CanvasGroup>().alpha = 1f;
            if (banner.GetComponent<PressAnyKeyBanner>() == null) banner.AddComponent<PressAnyKeyBanner>();
            banner.SetActive(true);
            foreach (UIAnimationEditor editor in Object.FindObjectsByType<UIAnimationEditor>(FindObjectsSortMode.None))
                if (editor.gameObject.name == "UI Animation Editor") Object.DestroyImmediate(editor.gameObject);
            AssetDatabase.DeleteAsset("Assets/UI/Animation/Press Any key Banner");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = banner;
            Debug.Log("[TitleBanner] Saved: one script, 1.5s fade, any keyboard key hides the banner.");
            EditorApplication.ExecuteMenuItem("Tools/Title Screen/Verify Runtime Banner");
        };
    }
}
