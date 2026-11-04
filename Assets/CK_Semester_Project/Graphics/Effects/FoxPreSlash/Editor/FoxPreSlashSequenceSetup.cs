using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace FoxPreSlash.Editor
{
    // Explicit menu actions only: importing these scripts never edits assets.
    public static class FoxPreSlashSequenceSetup
    {
        const string Root = "Assets/VFX/FoxPreSlash/";
        const string ScenePath = Root + "Scenes/VFX_FoxPreSlash_Demo.unity";
        const string MainPath = Root + "Timeline/TL_FoxPreSlash.playable";
        const string SequencePath = Root + "Timeline/TL_FoxPreSlash_Sequence.playable";
        static readonly string[] SequenceTracks =
        {
            "Cinemachine Shot", "Camera Base Motion", "Directional Camera Shake",
            "Cinemachine Impulse Signals", "VFX Sequence 0-48f", "Camera Motion Blur"
        };

        static IEnumerable<TrackAsset> Tracks(TimelineAsset asset) =>
            asset.GetRootTracks().SelectMany(Descendants);

        static IEnumerable<TrackAsset> Descendants(TrackAsset track)
        {
            yield return track;
            foreach (var child in track.GetChildTracks().SelectMany(Descendants))
                yield return child;
        }

        static GameObject Find(string name)
        {
            return SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name.Trim() == name).gameObject;
        }

        [MenuItem("Tools/FoxPreSlash/Integrate Sequence")]
        public static void Integrate()
        {
            Require(!EditorApplication.isPlaying, "Exit Play Mode before setup.");
            Require(SceneManager.GetActiveScene().path == ScenePath, "Open the FoxPreSlash demo scene first.");
            Require(!AssetDatabase.LoadAssetAtPath<TimelineAsset>(SequencePath), "Sequence already exists; use Validate instead.");
            TimelineEditor.GetWindow()?.ClearTimeline();
            var main = AssetDatabase.LoadAssetAtPath<TimelineAsset>(MainPath);
            var director = Find("TL_FoxPreSlash").GetComponent<PlayableDirector>();
            director.Stop();
            var rootTracks = main.GetRootTracks().ToArray();
            Require(SequenceTracks.All(name => rootTracks.Count(t => t.name == name) == 1), "Unexpected source tracks.");
            var shot = rootTracks.Single(t => t.name == "Cinemachine Shot").GetClips().Single();
            double start = shot.start;
            double length = shot.duration;
            var bindings = Tracks(main).ToDictionary(t => t.name, t => director.GetGenericBinding(t));
            var fox = (GameObject)director.GetReferenceValue("FoxVFX", out bool foxValid);
            var vcam = director.GetReferenceValue("FoxCamera", out bool cameraValid);
            Require(foxValid && fox && cameraValid && vcam, "Missing VFX/camera references.");
            var receiver = director.GetComponent<SignalReceiver>();
            Require(receiver, "Missing impulse SignalReceiver.");

            // Unity copies every sub-asset and its internal references, retaining
            // the clips, animation properties, blend curves and signal assets.
            EditorUtility.SetDirty(main);
            AssetDatabase.SaveAssets();
            Require(AssetDatabase.CopyAsset(MainPath, SequencePath), "Could not copy Timeline.");
            var sequence = AssetDatabase.LoadAssetAtPath<TimelineAsset>(SequencePath);
            sequence.name = "TL_FoxPreSlash_Sequence";
            foreach (var track in sequence.GetRootTracks().ToArray())
                if (!SequenceTracks.Contains(track.name)) sequence.DeleteTrack(track);
            foreach (var track in Tracks(sequence))
            {
                foreach (var clip in track.GetClips())
                {
                    // These two orphaned subtracks were left at global zero.
                    // Their intended local phase is zero with the other camera clips.
                    clip.start = track.name == "Camera Lens FOV" || track.name == "Cinemachine Micro Noise"
                        ? 0 : clip.start - start;
                    Require(clip.start >= 0, "A sequence clip starts before the shot.");
                }
                foreach (var marker in track.GetMarkers()) marker.time -= start;
                EditorUtility.SetDirty(track);
            }
            sequence.durationMode = TimelineAsset.DurationMode.FixedLength;
            sequence.fixedDuration = length;
            var sequenceObject = new GameObject("FoxPreSlash Sequence");
            sequenceObject.transform.SetParent(director.transform, false);
            var child = sequenceObject.AddComponent<PlayableDirector>();
            child.playableAsset = sequence;
            child.playOnAwake = false;
            child.extrapolationMode = DirectorWrapMode.None;
            child.timeUpdateMode = DirectorUpdateMode.Manual;
            var childReceiver = sequenceObject.AddComponent<SignalReceiver>();
            EditorUtility.CopySerialized(receiver, childReceiver);
            foreach (var track in Tracks(sequence))
                if (bindings.TryGetValue(track.name, out var binding) && binding)
                    child.SetGenericBinding(track, binding == receiver ? childReceiver : binding);
            child.SetReferenceValue("FoxVFX", fox);
            child.SetReferenceValue("FoxCamera", vcam);

            // Prevent playOnAwake particles from running while the parent clip is idle.
            // The prefab and every particle module are left intact.
            var fxControl = (ControlPlayableAsset)Tracks(sequence)
                .Single(t => t.name == "VFX Sequence 0-48f").GetClips().Single().asset;
            fxControl.active = true;
            fxControl.postPlayback = ActivationControlPlayable.PostPlaybackState.Inactive;
            fox.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(fox);
            sequenceObject.SetActive(false);
            ConfigureLifetime(child);

            foreach (var track in rootTracks.Where(t => SequenceTracks.Contains(t.name)))
            {
                foreach (var item in Descendants(track)) director.ClearGenericBinding(item);
                main.DeleteTrack(track);
            }
            director.ClearReferenceValue("FoxCamera");
            director.ClearReferenceValue("FoxVFX");
            Object.DestroyImmediate(receiver);
            var controlTrack = main.CreateTrack<ControlTrack>(null, "FoxPreSlash Sequence");
            var controlClip = controlTrack.CreateClip<ControlPlayableAsset>();
            controlClip.displayName = "TL_FoxPreSlash_Sequence";
            controlClip.start = start;
            controlClip.duration = length;
            var control = (ControlPlayableAsset)controlClip.asset;
            control.sourceGameObject = new ExposedReference<GameObject> { exposedName = "FoxPreSlashSequence" };
            control.updateDirector = true;
            control.updateParticle = false;
            control.updateITimeControl = false;
            control.searchHierarchy = false;
            control.active = true;
            control.postPlayback = ActivationControlPlayable.PostPlaybackState.Inactive;
            control.directorOnClipEnd = DirectorControlPlayable.PauseAction.StopDirector;
            director.SetReferenceValue("FoxPreSlashSequence", sequenceObject);
            main.durationMode = TimelineAsset.DurationMode.BasedOnClips;

            SetupCameras(fox);
            EditorUtility.SetDirty(main);
            EditorUtility.SetDirty(sequence);
            EditorUtility.SetDirty(fxControl);
            EditorUtility.SetDirty(control);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            TimelineEditor.GetWindow()?.SetTimeline(director);
            TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);
            Validate();
            Debug.Log($"FoxPreSlash integrated: parent {start * 30:0.###}–{(start + length) * 30:0.###}f, child 0–{length * 30:0.###}f.");
        }

        static void SetupCameras(GameObject fox)
        {
            int characterLayer = EnsureLayer("FoxCharacter");
            int effectsLayer = EnsureLayer("FoxEffects");
            SetRendererLayer(Find("FuchsiaToon"), characterLayer);
            SetRendererLayer(fox, effectsLayer);
            SetRendererLayer(Find("SakuraSlashParticles"), effectsLayer);
            var main = Find("Main Camera").GetComponent<Camera>();
            var data = main.GetUniversalAdditionalCameraData();
            Require(data.scriptableRenderer.SupportsCameraStackingType(CameraRenderType.Overlay),
                "The current renderer does not support overlays.");
            Require(data.cameraStack.Count == 0, "An existing camera stack needs manual integration.");
            var character = NewOverlay("Character Camera", main, 1 << characterLayer, false);
            var effects = NewOverlay("VFX Camera", main, 1 << effectsLayer, data.renderPostProcessing);
            // Both overlays retain the existing depth. VFX is drawn AFTER the
            // character so transparent slashes use its depth instead of being covered.
            data.cameraStack.Add(character);
            data.cameraStack.Add(effects);
            data.renderPostProcessing = false;
            main.cullingMask &= ~((1 << characterLayer) | (1 << effectsLayer));
            var stack = main.transform.parent.gameObject.AddComponent<FoxPreSlashCameraStack>();
            stack.mainCamera = main;
            stack.characterCamera = character;
            stack.effectsCamera = effects;
            stack.referenceSpace = main.transform.parent;
            stack.referencePosition = main.transform.localPosition;
            stack.referenceRotation = main.transform.localRotation;
            // Use the original neutral lens value, not a scrubbed animated value.
            var lens = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/CAM_Lens.anim");
            var lensBinding = AnimationUtility.GetCurveBindings(lens).Single(b => b.propertyName == "Lens.FieldOfView");
            stack.referenceFieldOfView = AnimationUtility.GetEditorCurve(lens, lensBinding).Evaluate(0);
            stack.Synchronize();
        }

        static Camera NewOverlay(string name, Camera source, int mask, bool postProcessing)
        {
            var go = new GameObject(name);
            go.transform.SetParent(source.transform.parent, false);
            go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            var camera = go.AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.cullingMask = mask;
            camera.clearFlags = CameraClearFlags.Nothing;
            camera.depth = source.depth + (postProcessing ? 2 : 1);
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            EditorUtility.CopySerialized(source.GetUniversalAdditionalCameraData(), data);
            data.renderType = CameraRenderType.Overlay;
            var serializedData = new SerializedObject(data);
            serializedData.FindProperty("m_ClearDepth").boolValue = false;
            serializedData.ApplyModifiedPropertiesWithoutUndo();
            data.renderPostProcessing = postProcessing;
            return camera;
        }

        static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = settings.FindProperty("layers");
            for (int i = 8; i < 32; i++)
            {
                if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) continue;
                layers.GetArrayElementAtIndex(i).stringValue = name;
                settings.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
            throw new InvalidOperationException("No free user layer.");
        }

        static void SetRendererLayer(GameObject root, int layer)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.gameObject.layer = layer;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.gameObject);
            }
        }

        static void ConfigureLifetime(PlayableDirector director)
        {
            var state = director.GetComponent<FoxPreSlashSequenceState>();
            if (!state) state = director.gameObject.AddComponent<FoxPreSlashSequenceState>();
            state.baseMotion = Find("Camera_BaseMotion").transform;
            state.virtualCamera = Find("CM_FoxPreSlash").GetComponent<CinemachineCamera>();
            state.noise = state.virtualCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
            state.impulseListener = state.virtualCamera.GetComponent<CinemachineImpulseListener>();
            state.motionBlur = Find("Motion Blur - Timeline envelope").GetComponent<UnityEngine.Rendering.Volume>();
            state.impulseListener.enabled = false;
        }

        [MenuItem("Tools/FoxPreSlash/Configure Sequence Lifetime %#F8")]
        public static void ConfigureLifetime()
        {
            Require(!EditorApplication.isPlaying && SceneManager.GetActiveScene().path == ScenePath, "Open the demo outside Play Mode.");
            TimelineEditor.GetWindow()?.ClearTimeline();
            var director = Find("TL_FoxPreSlash").GetComponent<PlayableDirector>();
            director.Stop();
            var child = Find("FoxPreSlash Sequence").GetComponent<PlayableDirector>();
            child.Stop();
            child.gameObject.SetActive(false);
            ConfigureLifetime(child);
            // The additional transparent impact effect must use the same final
            // VFX pass; otherwise the character overlay would paint over it.
            SetRendererLayer(Find("SakuraImpactStreakFX"), LayerMask.NameToLayer("FoxEffects"));
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            TimelineEditor.GetWindow()?.SetTimeline(director);
            Debug.Log("FoxPreSlash sequence lifetime restoration configured.");
        }

        [MenuItem("Tools/FoxPreSlash/Validate Sequence")]
        public static void Validate()
        {
            Require(SceneManager.GetActiveScene().path == ScenePath, "Open the FoxPreSlash demo scene.");
            var main = AssetDatabase.LoadAssetAtPath<TimelineAsset>(MainPath);
            var sequence = AssetDatabase.LoadAssetAtPath<TimelineAsset>(SequencePath);
            var director = Find("TL_FoxPreSlash").GetComponent<PlayableDirector>();
            var child = Find("FoxPreSlash Sequence").GetComponent<PlayableDirector>();
            var clip = main.GetRootTracks().Single(t => t.name == "FoxPreSlash Sequence").GetClips().Single();
            Require(main.GetRootTracks().Count() == 3, "Unexpected parent tracks.");
            Require(main.durationMode == TimelineAsset.DurationMode.BasedOnClips, "Parent duration is fixed.");
            Require(child.playableAsset == sequence && !child.playOnAwake, "Child playback configuration.");
            Require(child.timeUpdateMode == DirectorUpdateMode.Manual, "Child has an independent clock.");
            Require(Math.Abs(sequence.duration - clip.duration) < 1e-6, "Duration mismatch.");
            Require(Tracks(sequence).SelectMany(t => t.GetClips()).All(c => Math.Abs(c.start) < 1e-6), "Local clip phase mismatch.");
            Require(sequence.GetOutputTracks().All(t => t is ControlTrack || child.GetGenericBinding(t)), "Missing output binding.");
            var control = (ControlPlayableAsset)clip.asset;
            Require(control.sourceGameObject.Resolve(director) == child.gameObject && control.updateDirector && !control.updateParticle,
                "Parent Control binding or duplicate particle control.");
            Require(Tracks(sequence).SelectMany(t => t.GetMarkers()).Count() == 5, "Missing signal markers.");
            var stack = Find("Camera_System").GetComponent<FoxPreSlashCameraStack>();
            var cameras = stack.mainCamera.GetUniversalAdditionalCameraData().cameraStack;
            Require(cameras.SequenceEqual(new[] { stack.characterCamera, stack.effectsCamera }), "Incorrect stack order.");
            Require(!stack.characterCamera.GetUniversalAdditionalCameraData().clearDepth &&
                !stack.effectsCamera.GetUniversalAdditionalCameraData().clearDepth, "Depth is being cleared.");
            Require((stack.mainCamera.cullingMask & stack.characterCamera.cullingMask) == 0 &&
                (stack.effectsCamera.cullingMask & stack.characterCamera.cullingMask) == 0, "Character is rendered twice.");
            Debug.Log("FoxPreSlash validation PASS: bindings, duration, local phases, 5 signals, manual child clock, stack masks/depth. Visual playback still needs inspection.");
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("FoxPreSlash: " + message);
        }

        [MenuItem("Tools/FoxPreSlash/Check Playback and 100 Frame Move %#F9")]
        public static void CheckPlayback()
        {
            Require(EditorApplication.isPlaying, "Run this check in Play Mode (changes are restored in finally).");
            Validate();
            var main = AssetDatabase.LoadAssetAtPath<TimelineAsset>(MainPath);
            var director = Find("TL_FoxPreSlash").GetComponent<PlayableDirector>();
            var child = Find("FoxPreSlash Sequence").GetComponent<PlayableDirector>();
            var clip = main.GetRootTracks().Single(t => t.name == "FoxPreSlash Sequence").GetClips().Single();
            var stack = Find("Camera_System").GetComponent<FoxPreSlashCameraStack>();
            var vcam = Find("CM_FoxPreSlash").GetComponent<CinemachineCamera>();
            var noise = vcam.GetComponent<CinemachineBasicMultiChannelPerlin>();
            var character = Find("FuchsiaToon").transform;
            var worldPosition = character.position;
            var worldRotation = character.rotation;
            stack.Synchronize();
            var stablePoint = stack.characterCamera.WorldToViewportPoint(worldPosition);
            var signalReceiver = child.GetComponent<SignalReceiver>();
            int signalCount = 0;
            UnityEngine.Events.UnityAction countSignal = () => signalCount++;
            for (int i = 0; i < signalReceiver.Count(); i++)
                signalReceiver.GetReactionAtIndex(i).AddListener(countSignal);
            double originalStart = clip.start;
            try
            {
                foreach (int shift in new[] { 0, 100 })
                {
                    director.Stop();
                    child.Stop();
                    clip.start = originalStart + shift / 30.0;
                    director.RebuildGraph();
                    director.time = 0;
                    director.Play();
                    signalCount = 0;
                    int firstFrame = (int)Math.Round(clip.start * 30);
                    // Step through actual Playable graphs so SignalEmitter delivery,
                    // particle control, and nested director time are tested together.
                    for (int frame = 0; frame < firstFrame + 49; frame++)
                    {
                        director.time = frame / 30.0;
                        director.Evaluate();
                        if (frame == 30)
                        {
                            Require(!child.gameObject.activeSelf, "Child active during SakuraSlash.");
                            Require(Mathf.Abs(vcam.Lens.FieldOfView - stack.referenceFieldOfView) < 0.002f,
                                "FOV leaked into SakuraSlash: " + vcam.Lens.FieldOfView);
                            Require(Mathf.Abs(noise.AmplitudeGain) < 0.00001f, "Noise leaked into SakuraSlash.");
                        }
                        if (frame == firstFrame + 15 || frame == firstFrame + 36)
                        {
                            double localTime = (frame - firstFrame) / 30.0;
                            Require(Math.Abs(child.time - localTime) < 1e-5, "Nested time is not synchronized.");
                            var lens = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/CAM_Lens.anim");
                            var binding = AnimationUtility.GetCurveBindings(lens).Single(b => b.propertyName == "Lens.FieldOfView");
                            float expected = AnimationUtility.GetEditorCurve(lens, binding).Evaluate((float)localTime);
                            Require(Mathf.Abs(vcam.Lens.FieldOfView - expected) < 0.002f, "Lens phase/value mismatch.");
                            vcam.PreviousStateIsValid = false;
                            vcam.InternalUpdateCameraState(Vector3.up, 1f / 30);
                            stack.mainCamera.GetComponent<CinemachineBrain>().ManualUpdate();
                            stack.Synchronize();
                            Require(Vector3.Distance(stablePoint, stack.characterCamera.WorldToViewportPoint(worldPosition)) < 0.00001f,
                                "Character screen anchor moved.");
                            Require(character.position == worldPosition && character.rotation == worldRotation,
                                "Character world transform changed.");
                            Require(stack.effectsCamera.worldToCameraMatrix == stack.mainCamera.worldToCameraMatrix &&
                                stack.effectsCamera.projectionMatrix == stack.mainCamera.projectionMatrix, "VFX camera diverged from Cinemachine.");
                        }
                    }
                    Require(signalCount == 5, "Expected five impulses, received " + signalCount);
                    Debug.Log($"FoxPreSlash playback PASS: shift={shift}f, Sakura FOV/Noise neutral, child phase/lens correct, character anchor fixed, five signals.");
                }
            }
            finally
            {
                director.Stop();
                child.Stop();
                clip.start = originalStart;
                for (int i = 0; i < signalReceiver.Count(); i++)
                    signalReceiver.GetReactionAtIndex(i).RemoveListener(countSignal);
                director.RebuildGraph();
                director.time = 0;
                director.Evaluate();
                TimelineEditor.Refresh(RefreshReason.ContentsModified);
            }
        }
    }
}
