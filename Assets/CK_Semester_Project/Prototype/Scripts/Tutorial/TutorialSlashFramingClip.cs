using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace CK.SemesterProject.Tutorial
{
    public sealed class TutorialSlashFramingClip : PlayableAsset, ITimelineClipAsset
    {
        [Serializable]
        public sealed class Frame : PlayableBehaviour
        {
            public float Right;
            public float Height;
            public float Forward;
            public float FieldOfView;
            public float TargetBias;
            public float AimHeight;
            public float FollowWeight;

            public Pose GetPose(Vector3 player, Vector3 target, Vector3 forward, Pose openingPose)
            {
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                Vector3 position = player + right * Right + Vector3.up * Height + forward * Forward;
                Vector3 focus = Vector3.Lerp(player, target, TargetBias) + Vector3.up * AimHeight;
                return new Pose(Vector3.Lerp(openingPose.position, position, FollowWeight),
                    Quaternion.Slerp(openingPose.rotation, Quaternion.LookRotation(focus - position), FollowWeight));
            }

            public float GetFieldOfView(float openingFieldOfView)
            {
                return Mathf.Lerp(openingFieldOfView, Mathf.Clamp(FieldOfView, 1f, 179f), FollowWeight);
            }
        }

        public Frame Template = new Frame();
        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return ScriptPlayable<Frame>.Create(graph, Template);
        }
    }
}
