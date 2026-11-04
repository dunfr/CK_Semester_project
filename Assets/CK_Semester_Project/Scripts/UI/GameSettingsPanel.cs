using System.Collections.Generic;
using CK.SemesterProject.Audio;
using TMPro;
using UnityEngine;

namespace CK.SemesterProject.UI
{
    [DisallowMultipleComponent]
    public sealed class GameSettingsPanel : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Slider _masterVolume;
        [SerializeField] private UnityEngine.UI.Slider _bgmVolume;
        [SerializeField] private UnityEngine.UI.Slider _sfxVolume;
        [SerializeField] private TMP_Dropdown _resolution;

        private void OnEnable()
        {
            if (_masterVolume == null || _bgmVolume == null || _sfxVolume == null || _resolution == null)
            {
                Debug.LogError("GameSettingsPanel needs its three volume sliders and resolution dropdown.", this);
                return;
            }

            GameAudioSettings.Initialize();
            GameResolutionSettings.Initialize();
            _masterVolume.SetValueWithoutNotify(GameAudioSettings.MasterVolume);
            _bgmVolume.SetValueWithoutNotify(GameAudioSettings.BgmVolume);
            _sfxVolume.SetValueWithoutNotify(GameAudioSettings.SfxVolume);

            var options = new List<string>();
            foreach (Vector2Int size in GameResolutionSettings.Options)
                options.Add($"{size.x} x {size.y}");
            _resolution.ClearOptions();
            _resolution.AddOptions(options);
            _resolution.SetValueWithoutNotify(GameResolutionSettings.SelectedIndex);
            _resolution.RefreshShownValue();

            _masterVolume.onValueChanged.AddListener(GameAudioSettings.SetMasterVolume);
            _bgmVolume.onValueChanged.AddListener(GameAudioSettings.SetBgmVolume);
            _sfxVolume.onValueChanged.AddListener(GameAudioSettings.SetSfxVolume);
            _resolution.onValueChanged.AddListener(GameResolutionSettings.SetResolutionIndex);
        }

        private void OnDisable()
        {
            if (_masterVolume != null) _masterVolume.onValueChanged.RemoveListener(GameAudioSettings.SetMasterVolume);
            if (_bgmVolume != null) _bgmVolume.onValueChanged.RemoveListener(GameAudioSettings.SetBgmVolume);
            if (_sfxVolume != null) _sfxVolume.onValueChanged.RemoveListener(GameAudioSettings.SetSfxVolume);
            if (_resolution != null) _resolution.onValueChanged.RemoveListener(GameResolutionSettings.SetResolutionIndex);
            if (Application.isPlaying) PlayerPrefs.Save();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) PlayerPrefs.Save();
        }

        private void OnApplicationQuit() => PlayerPrefs.Save();
    }
}
