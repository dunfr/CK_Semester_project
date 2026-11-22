using System.Linq;
using CK.SemesterProject.SkillSequence;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace CK.CutsceneTools
{
    /// <summary>
    /// 궁극기 작업 씬(SC_Ultimate_Workspace)의 컷신을 게임 씬에서 재생할 수 있는 프리팹과 SkillCutsceneAsset으로 묶는다.
    /// - 작업 씬 타임라인을 UltimateCutsceneBuilder로 다시 만든 뒤, Ultimate_Director와 UltimateRig를 한 루트 아래로 모아 프리팹으로 저장한다.
    /// - 작업 씬에만 있는 참조(캐릭터 Animator, 작업용 Brain, Ult_cam 노드)는 비우고, 재생 시 SkillCutscenePlayer가 시전자·게임 Brain에 연결한다.
    /// - 작업 씬 자체는 저장하지 않고 닫는다.
    /// </summary>
    public static class UltimateGamePrefabBuilder
    {
        private const string Root = "Assets/CK_Semester_Project/Graphics/Effects/Cutscenes/Ultimate/";
        private const string PrefabFolder = Root + "Prefabs";
        private const string PrefabPath = PrefabFolder + "/PF_Ultimate_CutsceneRoot_Game.prefab";
        private const string CutscenePath = Root + "SkillCutscene_Ultimate_Game.asset";
        private const string HitSignalPath = "Assets/CK_Semester_Project/Graphics/Effects/Cutscenes/SpecialSkill_ZZZ/Timeline/SIG_SpecialSkill_Hit.signal";
        private const string GameScenePath = "Assets/CK_Semester_Project/Prototype/Scenes/TutorialDummyModelingDemo.unity";
        private const string CasterTrackName = "Fuchsia";
        private const string OriginalCameraName = "CM_Ult_Original";
        // 안경을 올린 직후 폭발하는 순간(UltimateCutsceneBuilder.BreakTime). 이때 피해 숫자를 띄운다.
        private const float HitTime = 6.15f;

        [MenuItem("CK/Ultimate Cutscene/Build Game Prefab (SK10)")]
        public static string Build()
        {
            string built = UltimateCutsceneBuilder.Build();
            if (!built.StartsWith("궁극기 타임라인 구성 완료"))
            {
                Debug.LogError("UltimateGamePrefabBuilder: " + built);
                return built;
            }

            var director = GameObject.Find("Ultimate_Director")?.GetComponent<PlayableDirector>();
            GameObject rig = GameObject.Find("UltimateRig");
            Transform player = GameObject.Find("FuchsiaUpdatedAnimated")?.transform;
            var timeline = director != null ? director.playableAsset as TimelineAsset : null;
            if (director == null || rig == null || player == null || timeline == null)
            {
                const string Missing = "Ultimate_Director, UltimateRig, FuchsiaUpdatedAnimated 또는 타임라인을 찾지 못했습니다.";
                Debug.LogError("UltimateGamePrefabBuilder: " + Missing);
                return Missing;
            }

            AddHitSignal(timeline);
            ClearExternalBindings(director, timeline);
            AttachCasterCamera(rig.transform);

            // 컷신 루트는 시전자 위치·방향에 생성되므로, 작업 씬 캐릭터의 자리를 루트 원점으로 삼는다.
            var root = new GameObject("PF_Ultimate_CutsceneRoot_Game");
            root.transform.SetPositionAndRotation(player.position, Quaternion.Euler(0f, player.eulerAngles.y, 0f));
            director.transform.SetParent(root.transform, true);
            rig.transform.SetParent(root.transform, true);
            director.playOnAwake = false;

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder(Root.TrimEnd('/'), "Prefabs");
            }
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            if (!saved)
            {
                Debug.LogError("UltimateGamePrefabBuilder: 프리팹 저장 실패 " + PrefabPath);
                return "프리팹 저장 실패";
            }
            SaveCutsceneAsset(prefab);
            AssetDatabase.SaveAssets();

            // 작업 씬에서 옮긴 오브젝트는 저장하지 않고 게임 씬으로 돌아간다.
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            string message = "궁극기 게임 프리팹 생성 완료: " + PrefabPath + " / " + CutscenePath;
            Debug.Log(message);
            return message;
        }

        // 타임라인에 Hit 신호가 없으면 폭발 순간에 하나 넣는다. 다시 실행해도 하나만 남는다.
        private static void AddHitSignal(TimelineAsset timeline)
        {
            var signal = AssetDatabase.LoadAssetAtPath<SignalAsset>(HitSignalPath);
            if (signal == null)
            {
                Debug.LogWarning("UltimateGamePrefabBuilder: Hit 신호 에셋이 없습니다. " + HitSignalPath);
                return;
            }
            if (timeline.markerTrack == null)
            {
                timeline.CreateMarkerTrack();
            }
            foreach (SignalEmitter old in timeline.markerTrack.GetMarkers().OfType<SignalEmitter>().Where(e => e.asset == signal).ToList())
            {
                timeline.markerTrack.DeleteMarker(old);
            }
            var emitter = timeline.markerTrack.CreateMarker<SignalEmitter>(HitTime);
            emitter.asset = signal;
            emitter.retroactive = false;
            emitter.emitOnce = true;
            EditorUtility.SetDirty(timeline);
        }

        // 프리팹 밖(작업 씬 캐릭터·카메라)을 가리키는 바인딩은 비운다. 재생 시 SkillCutscenePlayer가 시전자와 게임 Brain에 다시 연결한다.
        private static void ClearExternalBindings(PlayableDirector director, TimelineAsset timeline)
        {
            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                bool isExternal = track.name == CasterTrackName || track is CinemachineTrack;
                if (isExternal)
                {
                    director.SetGenericBinding(track, null);
                }
            }
        }

        private static void AttachCasterCamera(Transform rig)
        {
            Transform original = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == OriginalCameraName);
            if (original == null)
            {
                Debug.LogWarning("UltimateGamePrefabBuilder: " + OriginalCameraName + " 카메라가 없습니다.");
                return;
            }
            var camera = original.GetComponent<CinemachineCamera>();
            camera.Follow = null;
            camera.LookAt = null;
            if (original.GetComponent<SkillCutsceneCasterCamera>() == null)
            {
                original.gameObject.AddComponent<SkillCutsceneCasterCamera>();
            }
        }

        private static void SaveCutsceneAsset(GameObject prefab)
        {
            var asset = AssetDatabase.LoadAssetAtPath<SkillCutsceneAsset>(CutscenePath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<SkillCutsceneAsset>();
                AssetDatabase.CreateAsset(asset, CutscenePath);
            }
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("_prefab").objectReferenceValue = prefab;
            serialized.FindProperty("_alignToCaster").boolValue = true;
            serialized.FindProperty("_casterTrackName").stringValue = CasterTrackName;
            serialized.FindProperty("_targetTrackName").stringValue = "Target";
            serialized.FindProperty("_destroyOnFinish").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }
    }
}
