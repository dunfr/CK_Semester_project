using System;
using System.Linq;
using CK.SemesterProject.Tutorial;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace CK.SemesterProject.Editor
{
    public static class FuchsiaUltReferenceTimelineEditor
    {
        public const string Folder = FuchsiaUltCinemachineBuilder.Folder + "/ReferenceEditable";
        public const double LeadIn = 16d / 30d;
        public const double SourceEnd = 256d / 30d;
        private static readonly double[] Boundaries = { -LeadIn, .8666666150093079d, 1.3333333730697632d,
            2.1000001430511475d, 3.366666793823242d, 3.799999952316284d, 127d / 30d, 5.5d, 227d / 30d, SourceEnd };
        private static readonly string[] Names = { "준비·발도", "눈 클로즈업", "발 로우앵글", "정면 조준", "눈 확대",
            "어깨 너머", "표적 타격", "후퇴·회전", "마무리" };
        private static readonly (int Index, double Speed)[] SlowShots = { (1, .5d), (3, .25d) };

        internal static double ToTimelineTime(double sourceTime)
        {
            double timelineTime = sourceTime + LeadIn;
            foreach (var (index, speed) in SlowShots)
            {
                double start = Boundaries[index];
                double elapsed = Math.Clamp(sourceTime - start, 0d, Boundaries[index + 1] - start);
                timelineTime += elapsed * (1d / speed - 1d);
            }
            return timelineTime;
        }

        internal static double ToSourceTime(double timelineTime)
        {
            double sourceTime = timelineTime - LeadIn;
            foreach (var (index, speed) in SlowShots)
            {
                double start = Boundaries[index];
                double duration = (Boundaries[index + 1] - start) / speed;
                double elapsed = Math.Clamp(sourceTime - start, 0d, duration);
                sourceTime -= elapsed * (1d - speed);
            }
            return Math.Max(0d, sourceTime);
        }

        [MenuItem("Tools/Fuchsia/영상 카메라 Timeline 열기")]
        public static void OpenTimeline()
        {
            var scene = SceneManager.GetActiveScene();
            var preview = Find<FuchsiaUltCinemachinePreview>(scene);
            Selection.activeGameObject = preview.gameObject;
            TimelineEditor.GetOrCreateWindow().SetTimeline(preview.GetComponent<PlayableDirector>());
        }

        [MenuItem("Tools/Fuchsia/영상 카메라 편집 트랙 추가 또는 재생성")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play Mode를 종료하세요.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != FuchsiaUltCinemachineBuilder.ScenePath)
                throw new InvalidOperationException("FuchsiaUltCinemachinePreview 씬을 열고 실행하세요.");
            EnsureFolder(Folder);
            var preview = Find<FuchsiaUltCinemachinePreview>(scene);
            var director = preview.GetComponent<PlayableDirector>();
            director.Stop();
            var brain = Find<CinemachineBrain>(scene);
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            Camera output = brain.GetComponent<Camera>();
            output.clearFlags = CameraClearFlags.SolidColor;
            output.backgroundColor = new Color(.075f, .075f, .075f);
            var character = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Animator>(true))
                .First(a => a.transform.Find("Move") != null);
            var characterClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(FuchsiaUltCinemachineBuilder.Folder + "/FuchsiaUlt_Character.anim");
            var originalCameraClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(FuchsiaUltCinemachineBuilder.Folder + "/FuchsiaUlt_Camera.anim");
            var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(FuchsiaUltCinemachineBuilder.SourcePath);

            // Only generated objects are replaced. The actor, original reference camera and its clips remain available.
            foreach (string name in new[] { "Reference Cinemachine Cameras", "Reference Stage Props" })
            {
                var old = scene.GetRootGameObjects().FirstOrDefault(go => go.name == name);
                if (old != null) Object.DestroyImmediate(old);
            }
            var camerasRoot = new GameObject("Reference Cinemachine Cameras");
            var cameras = new CinemachineCamera[Names.Length];
            var motions = new AnimationClip[Names.Length];
            for (int i = 0; i < cameras.Length; i++)
            {
                string cameraName = $"CM_Reference_{i + 1:00}";
                var rig = new GameObject(cameraName + " · " + Names[i]);
                rig.transform.SetParent(camerasRoot.transform, false);
                rig.AddComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var camera = new GameObject(cameraName).AddComponent<CinemachineCamera>();
                camera.transform.SetParent(rig.transform, false);
                camera.Priority = -10;
                camera.StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
                camera.Lens = LensSettings.FromCamera(output);
                cameras[i] = camera;
                var motion = new AnimationClip { name = cameraName + "_Motion", frameRate = originalCameraClip.frameRate };
                var originalBindings = AnimationUtility.GetCurveBindings(originalCameraClip);
                var bindings = originalBindings.Select(b => { b.path = cameraName; return b; }).ToArray();
                AnimationUtility.SetEditorCurves(motion, bindings, originalBindings.Select(b => AnimationUtility.GetEditorCurve(originalCameraClip, b)).ToArray());
                AnimationUtility.SetAnimationClipSettings(motion, AnimationUtility.GetAnimationClipSettings(originalCameraClip));
                motions[i] = Save(motion, Folder + "/" + motion.name + ".anim");
                motions[i].SampleAnimation(rig, Mathf.Max(0f, (float)Boundaries[i]));
            }

            var props = new GameObject("Reference Stage Props");
            var propsAnimator = props.AddComponent<Animator>();
            propsAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FuchsiaAnimation/Ult.fbx");
            foreach (string name in new[] { "Cylinder001", "Sphere001" })
            {
                Transform original = model != null ? model.transform.Find(name) : null;
                GameObject prop = name != "Cylinder001" && original != null ? Object.Instantiate(original.gameObject, props.transform) :
                    GameObject.CreatePrimitive(name == "Cylinder001" ? PrimitiveType.Cylinder : PrimitiveType.Sphere);
                prop.name = name;
                prop.transform.SetParent(props.transform, false);
                if (name == "Cylinder001")
                {
                    // The reference's stationary target is a stage marker, not the animated imported disk.
                    var cameraReference = FuchsiaUltCinemachineBuilder.CreateCameraReference(source);
                    var shading = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/Prefabs/FuchsiaUpdatedAnimated.prefab");
                    var sample = FuchsiaUltCinemachineBuilder.CreateReferenceRig(shading);
                    cameraReference.SampleAnimation(sample, 4.5f);
                    Vector3 center = sample.transform.Find("Ult_cam.Target").position;
                    prop.transform.localPosition = center;
                    prop.transform.localScale = new Vector3(.65f, Mathf.Max(.8f, center.y), .65f);
                    Object.DestroyImmediate(cameraReference); Object.DestroyImmediate(sample);
                }
                var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name + "_Reference" };
                material.SetColor("_BaseColor", name == "Cylinder001" ? Color.yellow : new Color(1f, 0f, .72f));
                material = Save(material, Folder + "/" + material.name + ".mat");
                foreach (var renderer in prop.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                foreach (var collider in prop.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            }
            var propsClip = new AnimationClip { name = "FuchsiaUlt_ReferenceProps", frameRate = source.frameRate };
            var propBindings = AnimationUtility.GetCurveBindings(source).Where(b => b.path == "Sphere001").ToArray();
            AnimationUtility.SetEditorCurves(propsClip, propBindings, propBindings.Select(b => AnimationUtility.GetEditorCurve(source, b)).ToArray());
            AnimationUtility.SetAnimationClipSettings(propsClip, AnimationUtility.GetAnimationClipSettings(source));
            propsClip = Save(propsClip, Folder + "/FuchsiaUlt_ReferenceProps.anim");
            propsClip.SampleAnimation(props, 0f);
            PrepareFloor(scene);

            TimelineAsset both = CreateTimeline("TL_FuchsiaUlt_ReferenceEditable", director, brain, character, characterClip, propsAnimator, propsClip, cameras, motions, true, true);
            TimelineAsset animationOnly = CreateTimeline("TL_FuchsiaUlt_ReferenceAnimationOnly", director, brain, character, characterClip, propsAnimator, propsClip, cameras, motions, true, false);
            TimelineAsset cameraOnly = CreateTimeline("TL_FuchsiaUlt_ReferenceCameraOnly", director, brain, character, characterClip, propsAnimator, propsClip, cameras, motions, false, true);
            director.playableAsset = both;
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            director.extrapolationMode = DirectorWrapMode.None;
            director.playOnAwake = false;
            using (var data = new SerializedObject(preview))
            {
                data.FindProperty("_synchronized").objectReferenceValue = both;
                data.FindProperty("_animationOnly").objectReferenceValue = animationOnly;
                data.FindProperty("_cameraOnly").objectReferenceValue = cameraOnly;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            OpenTimeline();
            Debug.Log("영상 기준 Cinemachine 편집 트랙 생성 완료: 9개 숏. 클립 이동=시작 시점, 클립 길이=카메라 무브 길이.");
        }

        private static TimelineAsset CreateTimeline(string name, PlayableDirector director, CinemachineBrain brain,
            Animator character, AnimationClip characterClip, Animator props, AnimationClip propsClip,
            CinemachineCamera[] cameras, AnimationClip[] motions, bool animateCharacter, bool animateCamera)
        {
            string path = Folder + "/" + name + ".playable";
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            if (timeline == null) { timeline = ScriptableObject.CreateInstance<TimelineAsset>(); AssetDatabase.CreateAsset(timeline, path); }
            foreach (var track in timeline.GetRootTracks().ToArray())
            {
                director.ClearGenericBinding(track);
                foreach (var clip in track.GetClips())
                    if (clip.asset is FuchsiaCinemachineShotClip shot) director.ClearReferenceValue(shot.Camera.exposedName);
                timeline.DeleteTrack(track);
            }
            timeline.name = name;
            timeline.editorSettings.frameRate = 30d;
            timeline.durationMode = TimelineAsset.DurationMode.BasedOnClips;
            if (animateCharacter) AddAnimation("Fuchsia · Character", character, characterClip);
            AddAnimation("Reference · Target & Projectile", props, propsClip);
            if (animateCamera)
            {
                var track = timeline.CreateTrack<FuchsiaCinemachineShotTrack>(null, "Cinemachine · 영상 카메라 (클립 길이로 속도 조절)");
                director.SetGenericBinding(track, brain);
                for (int i = 0; i < cameras.Length; i++)
                {
                    var clip = track.CreateClip<FuchsiaCinemachineShotClip>();
                    clip.displayName = $"{i + 1:00} · {Names[i]}";
                    clip.start = ToTimelineTime(Boundaries[i]);
                    clip.duration = ToTimelineTime(Boundaries[i + 1]) - clip.start;
                    clip.easeInDuration = clip.easeOutDuration = 0d;
                    var asset = (FuchsiaCinemachineShotClip)clip.asset;
                    asset.SourceIn = Boundaries[i]; asset.SourceOut = Boundaries[i + 1];
                    asset.HorizontalFieldOfView = i == 0 ? 65f : i == 2 ? 50f : 45f;
                    asset.Camera.exposedName = GUID.Generate().ToString();
                    asset.Motion = motions[i];
                    director.SetReferenceValue(asset.Camera.exposedName, cameras[i]);
                }
            }
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssetIfDirty(timeline);
            return timeline;

            void AddAnimation(string trackName, Animator animator, AnimationClip motion)
            {
                var track = timeline.CreateTrack<AnimationTrack>(null, trackName);
                track.trackOffset = TrackOffset.ApplyTransformOffsets;
                director.SetGenericBinding(track, animator);
                double sourceTime = 0d;
                foreach (var (index, speed) in SlowShots)
                {
                    AddSegment(sourceTime, Boundaries[index], 1d);
                    AddSegment(Boundaries[index], Boundaries[index + 1], speed);
                    sourceTime = Boundaries[index + 1];
                }
                AddSegment(sourceTime, SourceEnd, 1d);

                void AddSegment(double sourceStart, double sourceEnd, double speed)
                {
                    var clip = track.CreateClip(motion);
                    clip.start = ToTimelineTime(sourceStart);
                    clip.duration = ToTimelineTime(sourceEnd) - clip.start;
                    clip.clipIn = sourceStart;
                    clip.timeScale = speed;
                    if (speed < 1d)
                        clip.displayName = FormattableString.Invariant($"{motion.name} ({speed:0.##}x)");
                    var asset = (AnimationPlayableAsset)clip.asset;
                    asset.removeStartOffset = false;
                    asset.applyFootIK = false;
                }
            }
        }

        private static void PrepareFloor(Scene scene)
        {
            var floor = scene.GetRootGameObjects().FirstOrDefault(go => go.name.StartsWith("Floor"));
            if (floor == null) return;
            var scale = floor.transform.localScale;
            Vector3 size = floor.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            floor.transform.localScale = new Vector3(20f / Mathf.Max(.001f, size.x), scale.y, 20f / Mathf.Max(.001f, size.z));
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "ReferenceChecker", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                texture.SetPixel(x, y, ((x / 4 + y / 4) & 1) == 0 ? new Color(.55f, .55f, .55f) : new Color(.27f, .27f, .27f));
            texture.Apply();
            texture = Save(texture, Folder + "/ReferenceChecker.asset");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "ReferenceFloor" };
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", new Vector2(4f, 4f));
            material.SetFloat("_Smoothness", 0f);
            floor.GetComponent<Renderer>().sharedMaterial = Save(material, Folder + "/ReferenceFloor.mat");
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

        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true)).First();
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
