using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CK.SemesterProject.Tutorial;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace CK.SemesterProject.Editor
{
    public static class FuchsiaUltReferenceTimelineValidation
    {
        public static void CaptureSamples()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(FuchsiaUltCinemachineBuilder.ScenePath);
            var director = Find<PlayableDirector>(scene);
            var brain = Find<CinemachineBrain>(scene);
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            brain.ChannelMask = (OutputChannels)(1u << 30);
            foreach (var camera in All<CinemachineCamera>(scene)) camera.OutputChannel = brain.ChannelMask;
            Camera output = brain.OutputCamera;
            output.scene = scene;
            var texture = new RenderTexture(960, 540, 24);
            var pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
            Directory.CreateDirectory("Temp/FuchsiaReferenceFrames");
            try
            {
                output.targetTexture = texture;
                output.aspect = 16f / 9f;
                director.timeUpdateMode = DirectorUpdateMode.Manual;
                director.Play();
                double[] sourceTimes = { .25d, 1.65d, 2.15d, 3.1d, 4.1d, 4.55d, 5.2d, 6.8d, 8.4d };
                double[] times = sourceTimes.Select(time => FuchsiaUltReferenceTimelineEditor.ToTimelineTime(
                    time - FuchsiaUltReferenceTimelineEditor.LeadIn)).ToArray();
                for (int i = 0; i < times.Length; i++)
                {
                    director.time = times[i]; director.Evaluate(); brain.ManualUpdate();
                    output.Render();
                    RenderTexture.active = texture;
                    pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); pixels.Apply();
                    File.WriteAllBytes($"Temp/FuchsiaReferenceFrames/new-{i:00}.png", pixels.EncodeToPNG());
                }
                File.WriteAllText("Temp/FuchsiaReferenceFrames/props.txt", string.Join("\n", All<Transform>(scene)
                    .Where(t => t.name == "Cylinder001" || t.name == "Sphere001")
                    .Select(t => t.name + " pos=" + t.position.ToString("R") + " scale=" + t.localScale.ToString("R") + " bounds=" + t.GetComponentInChildren<Renderer>().bounds)));
            }
            finally
            {
                director.Stop(); output.targetTexture = null; RenderTexture.active = null;
                Object.DestroyImmediate(texture); Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        public static void Validate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play Mode를 종료하세요.");
            Scene scene = EditorSceneManager.OpenPreviewScene(FuchsiaUltCinemachineBuilder.ScenePath);
            var sourceClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(FuchsiaUltCinemachineBuilder.SourcePath);
            var referenceClip = FuchsiaUltCinemachineBuilder.CreateCameraReference(sourceClip);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/Prefabs/FuchsiaUpdatedAnimated.prefab");
            var reference = FuchsiaUltCinemachineBuilder.CreateReferenceRig(prefab);
            var referenceCharacter = Object.Instantiate(prefab);
            referenceCharacter.hideFlags = HideFlags.HideAndDontSave;
            referenceCharacter.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var behaviour in referenceCharacter.GetComponentsInChildren<Behaviour>()) behaviour.enabled = false;
            foreach (var renderer in referenceCharacter.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            TimelineAsset retime = null;
            PlayableDirector director = null;
            try
            {
                director = Find<PlayableDirector>(scene);
                var timeline = (TimelineAsset)director.playableAsset;
                var track = timeline.GetOutputTracks().OfType<FuchsiaCinemachineShotTrack>().Single();
                var clips = track.GetClips().OrderBy(c => c.start).ToArray();
                var brain = Find<CinemachineBrain>(scene);
                brain.ChannelMask = (OutputChannels)(1u << 30);
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
                foreach (var camera in All<CinemachineCamera>(scene)) camera.OutputChannel = brain.ChannelMask;
                Require(director.timeUpdateMode != DirectorUpdateMode.Manual, "Saved Director still requires a manual clock");
                Require(clips.Length == 9, "Expected nine independently editable camera shots");
                Require(timeline.durationMode == TimelineAsset.DurationMode.BasedOnClips, "Timeline length is fixed");
                var character = All<Animator>(scene).Single(a => a.transform.Find("Move") != null);
                var transforms = character.GetComponentsInChildren<Transform>();
                var times = new SortedSet<double>();
                for (int frame = 0; frame < timeline.duration * 60d; frame++) times.Add(frame / 60d);
                foreach (var clip in clips)
                    foreach (double delta in new[] { -.0001d, 0d, .0001d })
                        if (clip.start + delta >= 0d) times.Add(clip.start + delta);
                director.timeUpdateMode = DirectorUpdateMode.Manual;
                director.Play();
                float maxPosition = 0f, maxAngle = 0f, maxCharacter = 0f;
                var errors = new List<string>();
                foreach (double time in times.Where(t => t < timeline.duration - .00001d))
                {
                    director.time = time;
                    director.Evaluate();
                    brain.ManualUpdate();
                    double sourceTime = FuchsiaUltReferenceTimelineEditor.ToSourceTime(time);
                    Pose expected = Reference(sourceTime);
                    float position = Vector3.Distance(brain.OutputCamera.transform.position, expected.position);
                    float angle = Quaternion.Angle(brain.OutputCamera.transform.rotation, expected.rotation);
                    maxPosition = Mathf.Max(maxPosition, position); maxAngle = Mathf.Max(maxAngle, angle);
                    if ((position >= .001f || angle >= .1f) && errors.Count < 10)
                        errors.Add($"time={time:R} source={sourceTime:R} position={position:R} angle={angle:R} active={brain.ActiveVirtualCamera?.Name}");
                    if (Math.Abs(time * 4d - Math.Round(time * 4d)) < .000001d)
                    {
                        sourceClip.SampleAnimation(referenceCharacter, (float)sourceTime);
                        foreach (var transform in transforms)
                        {
                            string path = AnimationUtility.CalculateTransformPath(transform, character.transform);
                            if (path.Length == 0) continue;
                            var expectedTransform = referenceCharacter.transform.Find(path);
                            if (expectedTransform != null) maxCharacter = Mathf.Max(maxCharacter, Vector3.Distance(transform.position, expectedTransform.position));
                        }
                    }
                }
                string report = $"samples={times.Count}\nshots={clips.Length}\nduration={timeline.duration:R}\nmaxPositionMeters={maxPosition:R}\nmaxAngleDegrees={maxAngle:R}\nmaxCharacterPositionMeters={maxCharacter:R}\n" + string.Join("\n", errors) + "\n";
                File.WriteAllText("Temp/FuchsiaUltReferenceTimelineValidation.txt", report);
                Require(maxPosition < .001f && maxAngle < .1f && maxCharacter < .001f, report);

                // Scrub backwards to exercise shot activation without relying on playback history.
                foreach (var clip in clips.Reverse())
                {
                    double time = clip.start + clip.duration * .5d;
                    director.time = time; director.Evaluate(); brain.ManualUpdate();
                    Require(Vector3.Distance(brain.OutputCamera.transform.position, Reference(FuchsiaUltReferenceTimelineEditor.ToSourceTime(time)).position) < .001f, "Reverse scrub mismatch");
                }
                director.Stop();
                retime = ScriptableObject.CreateInstance<TimelineAsset>();
                retime.durationMode = TimelineAsset.DurationMode.BasedOnClips;
                var retimeTrack = retime.CreateTrack<FuchsiaCinemachineShotTrack>();
                var retimeClip = retimeTrack.CreateClip<FuchsiaCinemachineShotClip>();
                var original = (FuchsiaCinemachineShotClip)clips[3].asset;
                var retimed = (FuchsiaCinemachineShotClip)retimeClip.asset;
                retimed.Motion = original.Motion; retimed.SourceIn = original.SourceIn; retimed.SourceOut = original.SourceOut;
                retimed.Camera.exposedName = GUID.Generate().ToString();
                director.SetReferenceValue(retimed.Camera.exposedName, original.Camera.Resolve(director));
                director.SetGenericBinding(retimeTrack, brain);
                director.playableAsset = retime;
                retimeClip.start = 2d; retimeClip.duration = 12d;
                director.Play();
                Require(director.duration > timeline.duration, "Extending a shot did not extend Timeline duration");
                director.time = 8d; director.Evaluate(); brain.ManualUpdate();
                Pose midpoint = Reference((retimed.SourceIn + retimed.SourceOut) * .5d);
                Require(Vector3.Distance(brain.OutputCamera.transform.position, midpoint.position) < .001f, "Stretching a shot did not retime the camera motion");
                director.time = 1d; director.Evaluate(); brain.ManualUpdate();
                Require(brain.ActiveVirtualCamera != null && brain.ActiveVirtualCamera.Name == "CM_Overview", "A gap did not release the camera shot");
                director.Stop();
                File.AppendAllText("Temp/FuchsiaUltReferenceTimelineValidation.txt", "PASS: reference motion, character synchronization, reverse scrubbing, clip movement, automatic retiming, extended duration and gaps\n");
                Debug.Log("Fuchsia reference Timeline validation passed. " + report);

                Pose Reference(double sourceTime)
                {
                    referenceClip.SampleAnimation(reference, (float)sourceTime);
                    return FuchsiaUltCinemachineBuilder.ReferencePose(reference.transform.Find("Ult_cam"));
                }
            }
            finally
            {
                if (director != null) director.Stop();
                if (retime != null)
                {
                    foreach (var track in retime.GetRootTracks().ToArray()) retime.DeleteTrack(track);
                    Object.DestroyImmediate(retime);
                }
                Object.DestroyImmediate(referenceClip); Object.DestroyImmediate(reference); Object.DestroyImmediate(referenceCharacter);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true)).ToArray();
        private static T Find<T>(Scene scene) where T : Component => All<T>(scene).First();
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
