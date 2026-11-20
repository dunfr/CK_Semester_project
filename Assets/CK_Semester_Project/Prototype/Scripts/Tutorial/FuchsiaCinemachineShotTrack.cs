using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace CK.SemesterProject.Tutorial
{
    [TrackColor(.53f, .08f, .25f)]
    [TrackClipType(typeof(FuchsiaCinemachineShotClip))]
    [TrackBindingType(typeof(CinemachineBrain), TrackBindingFlags.None)]
    public sealed class FuchsiaCinemachineShotTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount) =>
            ScriptPlayable<ShotMixer>.Create(graph, inputCount);

        public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
        {
            base.GatherProperties(director, driver);
            foreach (var clip in GetClips())
            {
                var asset = (FuchsiaCinemachineShotClip)clip.asset;
                var camera = asset.Camera.Resolve(director);
                var animator = camera != null ? camera.GetComponentInParent<Animator>() : null;
                if (animator != null && asset.Motion != null) driver.AddFromClip(animator.gameObject, asset.Motion);
                if (camera != null) driver.AddFromName<CinemachineCamera>(camera.gameObject, "Lens.FieldOfView");
            }
            var brain = director.GetGenericBinding(this) as CinemachineBrain;
            if (brain == null) return;
            foreach (string property in new[] { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
                         "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" })
                driver.AddFromName<Transform>(brain.gameObject, property);
            driver.AddFromName<Camera>(brain.gameObject, "field of view");
        }

        private sealed class ShotMixer : PlayableBehaviour
        {
            private CinemachineBrain _brain;
            private int _override = -1;

            public override void ProcessFrame(Playable playable, FrameData info, object playerData)
            {
                var brain = playerData as CinemachineBrain;
                if (brain != _brain)
                {
                    Release();
                    _brain = brain;
                }
                if (_brain == null) return;
                CinemachineCamera first = null, second = null;
                float firstWeight = 0f, secondWeight = 0f;
                for (int i = 0; i < playable.GetInputCount(); i++)
                {
                    float weight = playable.GetInputWeight(i);
                    if (weight <= 0f) continue;
                    var input = (ScriptPlayable<FuchsiaCinemachineShotClip.ShotFrame>)playable.GetInput(i);
                    var frame = input.GetBehaviour();
                    if (frame.Camera == null) continue;
                    frame.Sample(input.GetTime(), input.GetDuration());
                    if (frame.HorizontalFieldOfView > 0f && _brain.OutputCamera != null)
                    {
                        var lens = frame.Camera.Lens;
                        lens.FieldOfView = Camera.HorizontalToVerticalFieldOfView(frame.HorizontalFieldOfView, _brain.OutputCamera.aspect);
                        frame.Camera.Lens = lens;
                    }
                    first = second; firstWeight = secondWeight;
                    second = frame.Camera; secondWeight = weight;
                }
                if (second == null) { Release(); return; }
                float blend = first == null ? 1f : secondWeight / Mathf.Max(.0001f, firstWeight + secondWeight);
                _override = _brain.SetCameraOverride(_override, 100, first, second, blend, Application.isPlaying ? info.deltaTime : -1f);
            }

            public override void OnPlayableDestroy(Playable playable) => Release();

            private void Release()
            {
                if (_brain != null && _override >= 0) _brain.ReleaseCameraOverride(_override);
                _override = -1;
            }
        }
    }
}
