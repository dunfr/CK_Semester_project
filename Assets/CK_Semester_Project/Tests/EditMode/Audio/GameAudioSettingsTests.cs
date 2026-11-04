using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CK.SemesterProject.Audio.Tests
{
    public sealed class GameAudioSettingsTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly string[] _keys = { GameAudioSettings.MasterVolumeKey, GameAudioSettings.BgmVolumeKey, GameAudioSettings.SfxVolumeKey };
        private bool[] _hadKey;
        private float[] _prefs, _volumes;
        private float _listener;

        [SetUp]
        public void SetUp()
        {
            _hadKey = new bool[_keys.Length];
            _prefs = new float[_keys.Length];
            for (int i = 0; i < _keys.Length; i++)
            {
                _hadKey[i] = PlayerPrefs.HasKey(_keys[i]);
                _prefs[i] = PlayerPrefs.GetFloat(_keys[i]);
            }
            _listener = AudioListener.volume;
            _volumes = new[] { GameAudioSettings.MasterVolume, GameAudioSettings.BgmVolume, GameAudioSettings.SfxVolume };
            GameAudioSettings.SetMasterVolume(1f);
            GameAudioSettings.SetBgmVolume(1f);
            GameAudioSettings.SetSfxVolume(1f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject host in _objects) Object.DestroyImmediate(host);
            _objects.Clear();
            GameAudioSettings.SetMasterVolume(_volumes[0]);
            GameAudioSettings.SetBgmVolume(_volumes[1]);
            GameAudioSettings.SetSfxVolume(_volumes[2]);
            AudioListener.volume = _listener;
            for (int i = 0; i < _keys.Length; i++)
                if (_hadKey[i]) PlayerPrefs.SetFloat(_keys[i], _prefs[i]);
                else PlayerPrefs.DeleteKey(_keys[i]);
        }

        private AudioSource Source(float volume, AudioChannel channel)
        {
            var host = new GameObject("Audio settings test");
            _objects.Add(host);
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.volume = volume;
            GameAudioSettings.Register(source, channel);
            return source;
        }

        [Test]
        public void MasterControlsListenerWithoutChangingChannelGains()
        {
            AudioSource music = Source(.8f, AudioChannel.Bgm);
            AudioSource effect = Source(.6f, AudioChannel.Sfx);
            GameAudioSettings.SetBgmVolume(.5f);
            GameAudioSettings.SetSfxVolume(.25f);
            GameAudioSettings.SetMasterVolume(.4f);
            Assert.That(AudioListener.volume, Is.EqualTo(.4f).Within(.0001f));
            Assert.That(music.volume, Is.EqualTo(.4f).Within(.0001f));
            Assert.That(effect.volume, Is.EqualTo(.15f).Within(.0001f));
        }

        [Test]
        public void ChannelsStayIndependentAndGainDoesNotCompound()
        {
            AudioSource music = Source(.8f, AudioChannel.Bgm);
            AudioSource effect = Source(.6f, AudioChannel.Sfx);
            GameAudioSettings.SetBgmVolume(.25f);
            GameAudioSettings.SetBgmVolume(.25f);
            Assert.That(music.volume, Is.EqualTo(.2f).Within(.0001f));
            Assert.That(effect.volume, Is.EqualTo(.6f).Within(.0001f));
            GameAudioSettings.SetSfxVolume(.5f);
            Assert.That(music.volume, Is.EqualTo(.2f).Within(.0001f));
            Assert.That(effect.volume, Is.EqualTo(.3f).Within(.0001f));
            GameAudioSettings.SetBgmVolume(1f);
            Assert.That(music.volume, Is.EqualTo(.8f).Within(.0001f));
        }

        [Test]
        public void LateRegistrationUsesCurrentMutedChannelBeforePlayback()
        {
            GameAudioSettings.SetSfxVolume(0f);
            AudioSource effect = Source(.7f, AudioChannel.Sfx);
            Assert.That(effect.volume, Is.Zero);
            Assert.That(effect.GetComponent<GameAudioSource>().BaseVolume, Is.EqualTo(.7f).Within(.0001f));
            GameAudioSettings.Register(effect, AudioChannel.Sfx);
            GameAudioSettings.SetSfxVolume(.5f);
            Assert.That(effect.volume, Is.EqualTo(.35f).Within(.0001f));
        }

        [Test]
        public void MultipleSourcesOnOneObjectKeepSeparateChannelsAndBaseGains()
        {
            var host = new GameObject("Two audio channels");
            _objects.Add(host);
            var music = host.AddComponent<AudioSource>();
            var effect = host.AddComponent<AudioSource>();
            music.volume = .8f;
            effect.volume = .6f;
            GameAudioSettings.SetBgmVolume(.25f);
            GameAudioSettings.SetSfxVolume(.5f);
            GameAudioSource musicRoute = GameAudioSettings.Register(music, AudioChannel.Bgm);
            GameAudioSource effectRoute = GameAudioSettings.Register(effect, AudioChannel.Sfx);
            Assert.That(musicRoute, Is.Not.SameAs(effectRoute));
            Assert.That(musicRoute.Source, Is.SameAs(music));
            Assert.That(effectRoute.Source, Is.SameAs(effect));
            Assert.That(music.volume, Is.EqualTo(.2f).Within(.0001f));
            Assert.That(effect.volume, Is.EqualTo(.3f).Within(.0001f));
            GameAudioSettings.SetBgmVolume(1f);
            Assert.That(music.volume, Is.EqualTo(.8f).Within(.0001f));
            Assert.That(effect.volume, Is.EqualTo(.3f).Within(.0001f));
        }

        [Test]
        public void InactiveSourcesAndChangedBaseGainUseCurrentChannelValue()
        {
            AudioSource music = Source(.8f, AudioChannel.Bgm);
            music.gameObject.SetActive(false);
            GameAudioSettings.SetBgmVolume(.25f);
            Assert.That(music.volume, Is.EqualTo(.2f).Within(.0001f));
            music.GetComponent<GameAudioSource>().BaseVolume = .4f;
            Assert.That(music.volume, Is.EqualTo(.1f).Within(.0001f));
        }

        [Test]
        public void ValuesAreClampedAndWrittenToPreferences()
        {
            GameAudioSettings.SetMasterVolume(2f);
            GameAudioSettings.SetBgmVolume(-1f);
            GameAudioSettings.SetSfxVolume(float.NaN);
            Assert.That(GameAudioSettings.MasterVolume, Is.EqualTo(1f));
            Assert.That(GameAudioSettings.BgmVolume, Is.Zero);
            Assert.That(GameAudioSettings.SfxVolume, Is.Zero);
            Assert.That(PlayerPrefs.GetFloat(_keys[0]), Is.EqualTo(1f));
            Assert.That(PlayerPrefs.GetFloat(_keys[1]), Is.Zero);
            Assert.That(PlayerPrefs.GetFloat(_keys[2]), Is.Zero);
        }
    }
}
