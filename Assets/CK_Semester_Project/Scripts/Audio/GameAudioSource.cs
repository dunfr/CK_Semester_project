using UnityEngine;

namespace CK.SemesterProject.Audio
{
    public enum AudioChannel { Bgm, Sfx }

    /// <summary>Assign this beside an AudioSource to place it on the BGM or SFX volume control.</summary>
    [RequireComponent(typeof(AudioSource))]
    [DefaultExecutionOrder(-10000)]
    public sealed class GameAudioSource : MonoBehaviour
    {
        [SerializeField] private AudioChannel _channel = AudioChannel.Sfx;
        [SerializeField] private AudioSource _source;
        private float _baseVolume;
        private bool _captured;

        public AudioSource Source
        {
            get { CaptureSource(); return _source; }
        }

        public AudioChannel Channel
        {
            get => _channel;
            set { _channel = value; if (Application.isPlaying) ApplyVolume(); }
        }

        /// <summary>Unscaled authored gain; use this to change source gain after registration.</summary>
        public float BaseVolume
        {
            get { CaptureSource(); return _baseVolume; }
            set
            {
                CaptureSource();
                _baseVolume = float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
                ApplyVolume();
            }
        }

        private void Awake()
        {
            if (!CaptureSource()) return;
            GameAudioSettings.Add(this);
            ApplyVolume();
        }

        private void OnEnable()
        {
            if (!CaptureSource()) return;
            GameAudioSettings.Add(this);
            ApplyVolume();
        }

        private void OnDestroy() => GameAudioSettings.Remove(this);

        private void Reset()
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length == 1) _source = sources[0];
        }

        private bool CaptureSource()
        {
            if (_source == null)
            {
                AudioSource[] sources = GetComponents<AudioSource>();
                if (sources.Length != 1) return false;
                _source = sources[0];
            }
            if (_captured) return true;
            _baseVolume = _source.volume;
            _captured = true;
            return true;
        }

        internal void Bind(AudioSource source, float baseVolume)
        {
            _source = source;
            _baseVolume = baseVolume;
            _captured = true;
        }

        internal void ApplyVolume()
        {
            if (!CaptureSource()) return;
            float gain = _channel == AudioChannel.Bgm ? GameAudioSettings.BgmVolume : GameAudioSettings.SfxVolume;
            _source.volume = _baseVolume * gain;
        }
    }
}
