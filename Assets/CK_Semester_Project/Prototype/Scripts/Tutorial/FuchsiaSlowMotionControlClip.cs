using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace CK.SemesterProject.Tutorial
{
    public sealed class FuchsiaSlowMotionControlClip : ControlPlayableAsset
    {
        [SerializeField] private SlowMotionRange[] slowMotionRanges = Array.Empty<SlowMotionRange>();
        [SerializeField, HideInInspector] private double slowMotionStart;
        [SerializeField, HideInInspector] private double slowMotionDuration;

        [Serializable]
        private struct SlowMotionRange
        {
            // Start and duration use the clip's stretched time, including its clip-in offset.
            [Min(0f)] public double start;
            [Min(0f)] public double duration;
            [Range(0f, 1f)] public double speed;
        }

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var control = base.CreatePlayable(graph, owner);
            var playable = ScriptPlayable<SlowMotionTime>.Create(graph, 1);
            playable.ConnectInput(0, control, 0, 1f);
            playable.GetBehaviour().Ranges = slowMotionRanges.Length > 0
                ? slowMotionRanges
                : new[] { new SlowMotionRange { start = slowMotionStart, duration = slowMotionDuration, speed = .5d } };
            return playable;
        }

        private sealed class SlowMotionTime : PlayableBehaviour
        {
            public SlowMotionRange[] Ranges;

            public override void PrepareFrame(Playable playable, FrameData info)
            {
                // Keep one control graph active through speed changes so effects retain their state when scrubbing.
                double time = playable.GetTime();
                double sourceTime = time;
                foreach (var range in Ranges)
                {
                    double elapsed = Math.Clamp(time - range.start, 0d, range.duration);
                    sourceTime -= elapsed * (1d - range.speed);
                }
                playable.GetInput(0).SetTime(sourceTime);
            }
        }
    }
}
