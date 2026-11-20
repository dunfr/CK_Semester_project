using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace CK.SemesterProject.Tutorial
{
    /// <summary>히트스톱·카메라 흔들림 클립을 담는 트랙. 재생 로직은 TutorialSlashHitFeel이 트랙 데이터를 직접 읽어 처리한다.</summary>
    [TrackClipType(typeof(TutorialSlashHitFeelClip))]
    public sealed class TutorialSlashHitFeelTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return Playable.Create(graph, inputCount);
        }
    }
}
