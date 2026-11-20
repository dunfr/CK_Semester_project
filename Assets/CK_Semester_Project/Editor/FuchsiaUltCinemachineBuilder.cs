using System;
using System.Linq;
using CK.SemesterProject.Tutorial;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace CK.SemesterProject.Editor
{
    public static class FuchsiaUltCinemachineBuilder
    {
        public const string ScenePath = "Assets/CK_Semester_Project/Prototype/Scenes/FuchsiaUltCinemachinePreview.unity";
        public const string Folder = "Assets/CK_Semester_Project/Cinematics/FuchsiaUlt";
        public const string SourcePath = "Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/Animations/FuchsiaUlt.anim";
        private const string Shading = "Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading";
        private const string CameraPath = "CM_Ult";

        [MenuItem("Tools/Fuchsia/궁극기 Cinemachine 씬 만들기")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("플레이 모드를 종료한 뒤 실행하세요.");
            const string templateScene = "Assets/CK_Semester_Project/Prototype/Scenes/FuchsiaSkillPreview.unity";
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded || SceneManager.GetSceneByPath(templateScene).isLoaded)
                throw new InvalidOperationException("FuchsiaSkillPreview와 FuchsiaUltCinemachinePreview 씬을 닫고 다른 씬에서 생성 메뉴를 실행하세요.");
            EnsureFolder(Folder);
            var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourcePath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Shading + "/Prefabs/FuchsiaUpdatedAnimated.prefab");
            if (source == null || prefab == null) throw new InvalidOperationException("Fuchsia Ult 원본이 없습니다.");

            var characterClip = Object.Instantiate(source);
            characterClip.name = "FuchsiaUlt_Character";
            foreach (var binding in AnimationUtility.GetCurveBindings(characterClip))
                if (!IsCharacterPath(binding.path)) AnimationUtility.SetEditorCurve(characterClip, binding, null);
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(characterClip))
                if (!IsCharacterPath(binding.path)) AnimationUtility.SetObjectReferenceCurve(characterClip, binding, null);
            characterClip = Save(characterClip, Folder + "/FuchsiaUlt_Character.anim");
            AnimationClip cameraClip = BakeCamera(source);

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(templateScene, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                Object.DestroyImmediate(Find<FuchsiaSkillPreview>(scene).gameObject);
                var battle = Find<PlayerBattleAnimation>(scene);
                Animator character = battle.GetComponent<Animator>();
                GameObject root = battle.gameObject;
                // This scene has no animation-camera nodes, FSM or legacy camera writer.
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                Object.DestroyImmediate(battle);
                foreach (string name in new[] { "Ult_cam", "Ult_cam.Target", "PhysCamera001", "PhysCamera001.Target" })
                {
                    Transform node = root.transform.Find(name);
                    if (node != null) Object.DestroyImmediate(node.gameObject);
                }
                string controllerPath = Folder + "/FuchsiaUltPreviewIdle.controller";
                var idleController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (idleController == null)
                {
                    idleController = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                    var state = idleController.layers[0].stateMachine.AddState("BattleIdle");
                    state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(Shading + "/Animations/FuchsiaAttackIdle.anim");
                    idleController.layers[0].stateMachine.defaultState = state;
                    EditorUtility.SetDirty(idleController);
                    AssetDatabase.SaveAssetIfDirty(idleController);
                }
                character.runtimeAnimatorController = idleController;
                character.applyRootMotion = false;
                character.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                Camera output = Find<Camera>(scene);
                var brain = output.gameObject.AddComponent<CinemachineBrain>();
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
                var idle = new GameObject("CM_Overview").AddComponent<CinemachineCamera>();
                idle.transform.SetPositionAndRotation(output.transform.position, output.transform.rotation);
                idle.Lens = LensSettings.FromCamera(output);
                idle.Priority = 10;

                var rig = new GameObject("Fuchsia Ult Cinemachine Timeline");
                var cameraAnimator = rig.AddComponent<Animator>();
                cameraAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var shot = new GameObject(CameraPath).AddComponent<CinemachineCamera>();
                shot.transform.SetParent(rig.transform, false);
                shot.Lens = LensSettings.FromCamera(output);
                var lens = shot.Lens;
                lens.FieldOfView = Camera.HorizontalToVerticalFieldOfView(45f, output.aspect);
                shot.Lens = lens;
                shot.Priority = 0;
                shot.StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
                cameraClip.SampleAnimation(rig, 0f);
                var director = rig.AddComponent<PlayableDirector>();
                director.playOnAwake = false;
                director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
                director.extrapolationMode = DirectorWrapMode.None;
                TimelineAsset both = CreateTimeline("TL_FuchsiaUlt", characterClip, cameraClip, director, character, cameraAnimator, shot, brain, true, true);
                TimelineAsset animation = CreateTimeline("TL_FuchsiaUlt_AnimationOnly", characterClip, cameraClip, director, character, cameraAnimator, shot, brain, true, false);
                TimelineAsset camera = CreateTimeline("TL_FuchsiaUlt_CameraOnly", characterClip, cameraClip, director, character, cameraAnimator, shot, brain, false, true);
                director.playableAsset = both;
                var preview = rig.AddComponent<FuchsiaUltCinemachinePreview>();
                using (var data = new SerializedObject(preview))
                {
                    Set("_director", director);
                    Set("_character", character); Set("_synchronized", both); Set("_animationOnly", animation); Set("_cameraOnly", camera);
                    data.ApplyModifiedPropertiesWithoutUndo();
                    void Set(string name, Object value) => data.FindProperty(name).objectReferenceValue = value;
                }
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("씬 저장 실패");
                FuchsiaUltReferenceTimelineEditor.Upgrade();
                Debug.Log($"Fuchsia Ult Cinemachine 씬 생성 완료: {ScenePath} (원본 카메라 곡선 보존, {source.length:F3}초)");
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static bool IsCharacterPath(string path) => path == "Move" || path.StartsWith("Move/", StringComparison.Ordinal)
            || path == "head" || path.StartsWith("head/", StringComparison.Ordinal);

        // Copy authored curves without retiming, resampling, target aiming or damping.
        // Max cameras face -Z. Right-multiplication by (0,1,0,0) maps q to (-z,w,x,-y).
        // This linear mapping preserves both quaternion values and tangents.
        private static AnimationClip BakeCamera(AnimationClip source)
        {
            var clip = new AnimationClip { name = "FuchsiaUlt_Camera", frameRate = source.frameRate };
            foreach (char axis in "xyz")
                clip.SetCurve(CameraPath, typeof(Transform), "m_LocalPosition." + axis, Read("m_LocalPosition." + axis));
            string sourceAxes = "zwxy";
            for (int a = 0; a < 4; a++)
            {
                AnimationCurve curve = Read("m_LocalRotation." + sourceAxes[a]);
                if (a == 0 || a == 3)
                {
                    var keys = curve.keys;
                    for (int k = 0; k < keys.Length; k++)
                    {
                        keys[k].value = -keys[k].value;
                        keys[k].inTangent = -keys[k].inTangent;
                        keys[k].outTangent = -keys[k].outTangent;
                    }
                    curve.keys = keys;
                }
                clip.SetCurve(CameraPath, typeof(Transform), "m_LocalRotation." + "xyzw"[a], curve);
            }
            var settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return Save(clip, Folder + "/FuchsiaUlt_Camera.anim");

            AnimationCurve Read(string property) => AnimationUtility.GetEditorCurve(source,
                EditorCurveBinding.FloatCurve("Ult_cam", typeof(Transform), property))
                ?? throw new InvalidOperationException("Ult_cam curve missing: " + property);
        }

        public static AnimationClip CreateCameraReference(AnimationClip source)
        {
            var reference = new AnimationClip { frameRate = source.frameRate };
            var bindings = AnimationUtility.GetCurveBindings(source).Where(b => b.path == "Ult_cam" || b.path == "Ult_cam.Target").ToArray();
            AnimationUtility.SetEditorCurves(reference, bindings, bindings.Select(b => AnimationUtility.GetEditorCurve(source, b)).ToArray());
            return reference;
        }

        public static GameObject CreateReferenceRig(GameObject prefab)
        {
            var rig = new GameObject("Ult source comparison") { hideFlags = HideFlags.HideAndDontSave };
            foreach (string name in new[] { "Ult_cam", "Ult_cam.Target" })
            {
                Transform original = prefab.transform.Find(name);
                var node = new GameObject(name).transform;
                node.SetParent(rig.transform, false);
                node.localPosition = original.localPosition;
                node.localRotation = original.localRotation;
                node.localScale = original.localScale;
            }
            return rig;
        }

        // This conversion is used only while authoring/validating, never by the runtime camera.
        public static Pose ReferencePose(Transform node) =>
            new Pose(node.position, node.rotation * new Quaternion(0f, 1f, 0f, 0f));

        private static TimelineAsset CreateTimeline(string name, AnimationClip characterClip, AnimationClip cameraClip,
            PlayableDirector director, Animator character, Animator cameraAnimator, CinemachineCamera shot, CinemachineBrain brain,
            bool animateCharacter, bool animateCamera)
        {
            string path = Folder + "/" + name + ".playable";
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            if (timeline == null) { timeline = ScriptableObject.CreateInstance<TimelineAsset>(); AssetDatabase.CreateAsset(timeline, path); }
            else
            {
                foreach (var track in timeline.GetRootTracks().ToArray())
                {
                    director.ClearGenericBinding(track);
                    foreach (var clip in track.GetClips())
                        if (clip.asset is CinemachineShot oldShot) director.ClearReferenceValue(oldShot.VirtualCamera.exposedName);
                    timeline.DeleteTrack(track);
                }
            }
            timeline.name = name;
            timeline.editorSettings.frameRate = 30d;
            timeline.durationMode = TimelineAsset.DurationMode.BasedOnClips;
            if (animateCharacter)
            {
                var track = timeline.CreateTrack<AnimationTrack>(null, "Fuchsia Ult · Character");
                track.trackOffset = TrackOffset.ApplyTransformOffsets;
                AddClip(track, characterClip, 0d, characterClip.length);
                director.SetGenericBinding(track, character);
            }
            if (animateCamera)
            {
                var track = timeline.CreateTrack<AnimationTrack>(null, "Original camera motion · Cinemachine");
                track.trackOffset = TrackOffset.ApplyTransformOffsets;
                AddClip(track, cameraClip, 0d, cameraClip.length);
                director.SetGenericBinding(track, cameraAnimator);
                var cameras = timeline.CreateTrack<CinemachineTrack>(null, "Cinemachine output");
                var clip = cameras.CreateClip<CinemachineShot>();
                clip.start = 0d; clip.duration = characterClip.length;
                clip.displayName = "Fuchsia Ult · original camera";
                var asset = (CinemachineShot)clip.asset;
                asset.VirtualCamera.exposedName = GUID.Generate().ToString();
                director.SetReferenceValue(asset.VirtualCamera.exposedName, shot);
                director.SetGenericBinding(cameras, brain);
            }
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssetIfDirty(timeline);
            return timeline;
        }

        private static void AddClip(AnimationTrack track, AnimationClip source, double start, double duration)
        {
            var clip = track.CreateClip(source);
            clip.start = start; clip.duration = duration;
            clip.easeInDuration = clip.easeOutDuration = 0d;
            var animation = (AnimationPlayableAsset)clip.asset;
            animation.removeStartOffset = false;
            animation.applyFootIK = false;
        }

        private static T Save<T>(T value, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, existing);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssetIfDirty(existing);
            Object.DestroyImmediate(value);
            return existing;
        }

        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).First();

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
