using UnityEngine;
using CK.SemesterProject.Audio;

namespace CK.SemesterProject.UIAnimation
{
    // 상태와 입력 반응에 넣은 효과음을 2D로 재생한다.
    internal static class UIAnimationSound
    {
        private static AudioSource s_source;

        public static void Play(AudioClip clip)
        {
            if (clip == null || !Application.isPlaying)
            {
                return;
            }
            if (s_source == null)
            {
                var host = new GameObject("UI Animation Sound") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(host);
                s_source = host.AddComponent<AudioSource>();
                s_source.playOnAwake = false;
                s_source.spatialBlend = 0f;
                GameAudioSettings.Register(s_source, AudioChannel.Sfx);
            }
            s_source.PlayOneShot(clip);
        }
    }
}
