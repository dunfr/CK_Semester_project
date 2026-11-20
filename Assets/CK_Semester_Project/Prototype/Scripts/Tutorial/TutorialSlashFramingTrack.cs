using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace CK.SemesterProject.Tutorial
{
    [TrackClipType(typeof(TutorialSlashFramingClip))]
    [TrackBindingType(typeof(TutorialSlashFramingReceiver))]
    public sealed class TutorialSlashFramingTrack : TrackAsset
    {
        private sealed class Mixer : PlayableBehaviour
        {
            public override void ProcessFrame(Playable playable, FrameData info, object playerData)
            {
                if (!(playerData is TutorialSlashFramingReceiver receiver))
                {
                    return;
                }
                for (int index = 0; index < playable.GetInputCount(); index++)
                {
                    if (playable.GetInputWeight(index) > 0f)
                    {
                        var clip = (ScriptPlayable<TutorialSlashFramingClip.Frame>)playable.GetInput(index);
                        receiver.Frame = clip.GetBehaviour();
                        return;
                    }
                }
            }
        }

        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<Mixer>.Create(graph, inputCount);
        }
    }
}
