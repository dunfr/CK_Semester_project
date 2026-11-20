using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace CK.CutsceneTools
{
    /// <summary>
    /// 궁극기(FuchsiaUlt) 컷신 작업 씬을 만든다. 특수스킬 워크스페이스 씬(조명·바닥·타임라인 카메라·플레이어 배치)을 복제하고,
    /// 특수스킬 컷신 루트를 빼고, FuchsiaUlt 애니메이션 하나만 든 타임라인(TL_Ultimate_Base)을 플레이어 Animator에 연결한다.
    /// 에디터 전용. 이미 만들어진 에셋은 덮어쓰지 않는다.
    /// </summary>
    public static class UltimateWorkspaceBuilder
    {
        private const string Root = "Assets/CK_Semester_Project/Graphics/Effects/Cutscenes/";
        private const string SourceScene = Root + "SpecialSkill_ZZZ/Scenes/SC_SpecialSkill_Workspace.unity";
        private const string SceneFolder = Root + "Ultimate/Scenes";
        private const string TimelineFolder = Root + "Ultimate/Timeline";
        private const string ScenePath = SceneFolder + "/SC_Ultimate_Workspace.unity";
        private const string TimelinePath = TimelineFolder + "/TL_Ultimate_Base.playable";
        private const string UltClipPath =
            "Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/Animations/FuchsiaUlt.anim";
        private const string CutsceneRootName = "PF_SpecialSkill_CutsceneRoot";
        private const string DirectorName = "Ultimate_Director";

        [MenuItem("CK/Ultimate Cutscene/Create Workspace Scene")]
        public static string Create()
        {
            var ultClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(UltClipPath);
            if (ultClip == null)
            {
                return "FuchsiaUlt.anim 을 찾지 못했습니다: " + UltClipPath;
            }
            EnsureFolder(SceneFolder);
            EnsureFolder(TimelineFolder);

            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            if (timeline == null)
            {
                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                timeline.name = "TL_Ultimate_Base";
                AssetDatabase.CreateAsset(timeline, TimelinePath);
                timeline.editorSettings.frameRate = 60.0;
            }

            AnimationTrack track = timeline.GetRootTracks().OfType<AnimationTrack>().FirstOrDefault(t => t.name == "Fuchsia");
            if (track == null)
            {
                track = timeline.CreateTrack<AnimationTrack>(null, "Fuchsia");
            }
            if (!track.GetClips().Any())
            {
                TimelineClip clip = track.CreateClip(ultClip);
                clip.start = 0d;
                clip.displayName = "FuchsiaUlt";
            }
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                if (!AssetDatabase.CopyAsset(SourceScene, ScenePath))
                {
                    return "씬을 복제하지 못했습니다: " + SourceScene;
                }
            }
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // 씬을 열면 사용하지 않는 에셋이 언로드되므로 타임라인과 트랙을 다시 불러온다.
            timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            track = timeline.GetRootTracks().OfType<AnimationTrack>().First(t => t.name == "Fuchsia");

            foreach (GameObject root in scene.GetRootGameObjects().Where(g => g.name == CutsceneRootName).ToArray())
            {
                Object.DestroyImmediate(root);
            }

            Animator player = scene.GetRootGameObjects().Select(g => g.GetComponent<Animator>()).FirstOrDefault(a => a != null);
            if (player == null)
            {
                return "씬에서 플레이어(Animator)를 찾지 못했습니다.";
            }

            GameObject directorObject = scene.GetRootGameObjects().FirstOrDefault(g => g.name == DirectorName);
            if (directorObject == null)
            {
                directorObject = new GameObject(DirectorName);
                SceneManager.MoveGameObjectToScene(directorObject, scene);
            }
            var director = directorObject.GetComponent<PlayableDirector>();
            if (director == null)
            {
                director = directorObject.AddComponent<PlayableDirector>();
            }
            director.playableAsset = timeline;
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.Hold;
            director.SetGenericBinding(track, player);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = directorObject;
            string message = "궁극기 작업 씬 준비 완료: " + ScenePath + " / 타임라인 " + TimelinePath
                + " (FuchsiaUlt " + ultClip.length.ToString("F2") + "s → " + player.name + ")";
            Debug.Log(message);
            return message;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
