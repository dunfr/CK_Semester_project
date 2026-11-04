using System.Collections.Generic;
using UnityEngine;

namespace CK.SemesterProject.Audio
{
    /// <summary>Saved user volume multipliers. Channel gains retain each source's authored volume.</summary>
    public static class GameAudioSettings
    {
        public const string MasterVolumeKey = "CK.Settings.Audio.Master";
        public const string BgmVolumeKey = "CK.Settings.Audio.Bgm";
        public const string SfxVolumeKey = "CK.Settings.Audio.Sfx";

        private static readonly HashSet<GameAudioSource> Sources = new HashSet<GameAudioSource>();
        private static bool _initialized;
        private static float _masterVolume = 1f;
        private static float _bgmVolume = 1f;
        private static float _sfxVolume = 1f;

        public static float MasterVolume { get { Initialize(); return _masterVolume; } }
        public static float BgmVolume { get { Initialize(); return _bgmVolume; } }
        public static float SfxVolume { get { Initialize(); return _sfxVolume; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Sources.Clear();
            _initialized = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            Initialize();
            // With scene/domain reload disabled, existing routers may not receive Awake again.
            foreach (GameAudioSource source in Object.FindObjectsByType<GameAudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Add(source);
                source.ApplyVolume();
            }
        }

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _masterVolume = Clamp(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
            _bgmVolume = Clamp(PlayerPrefs.GetFloat(BgmVolumeKey, 1f));
            _sfxVolume = Clamp(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
            AudioListener.volume = _masterVolume;
        }

        public static void SetMasterVolume(float value)
        {
            Initialize();
            _masterVolume = Clamp(value);
            AudioListener.volume = _masterVolume;
            PlayerPrefs.SetFloat(MasterVolumeKey, _masterVolume);
        }

        public static void SetBgmVolume(float value)
        {
            Initialize();
            _bgmVolume = Clamp(value);
            PlayerPrefs.SetFloat(BgmVolumeKey, _bgmVolume);
            ApplySources(AudioChannel.Bgm);
        }

        public static void SetSfxVolume(float value)
        {
            Initialize();
            _sfxVolume = Clamp(value);
            PlayerPrefs.SetFloat(SfxVolumeKey, _sfxVolume);
            ApplySources(AudioChannel.Sfx);
        }

        /// <summary>Register runtime-created sources before playing their first clip.</summary>
        public static GameAudioSource Register(AudioSource source, AudioChannel channel)
        {
            if (source == null) return null;
            float baseVolume = source.volume;
            GameAudioSource routed = null;
            foreach (GameAudioSource candidate in source.GetComponents<GameAudioSource>())
                if (candidate.Source == source) { routed = candidate; break; }
            if (routed == null)
            {
                routed = source.gameObject.AddComponent<GameAudioSource>();
                routed.Bind(source, baseVolume);
            }
            routed.Channel = channel;
            Add(routed);
            routed.ApplyVolume();
            return routed;
        }

        internal static void Add(GameAudioSource source)
        {
            Initialize();
            Sources.Add(source);
        }

        internal static void Remove(GameAudioSource source) => Sources.Remove(source);

        private static void ApplySources(AudioChannel channel)
        {
            foreach (GameAudioSource source in Sources)
                if (source != null && source.Channel == channel) source.ApplyVolume();
        }

        private static float Clamp(float value) => float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
    }
}
