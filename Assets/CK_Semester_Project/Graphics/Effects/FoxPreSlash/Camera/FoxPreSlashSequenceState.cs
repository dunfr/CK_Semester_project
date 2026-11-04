using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace FoxPreSlash
{
    /// <summary>Scopes animated camera properties to the nested Control clip.</summary>
    [ExecuteAlways, RequireComponent(typeof(PlayableDirector))]
    public sealed class FoxPreSlashSequenceState : MonoBehaviour
    {
        public Transform baseMotion;
        public CinemachineCamera virtualCamera;
        public CinemachineBasicMultiChannelPerlin noise;
        public CinemachineImpulseListener impulseListener;
        public Volume motionBlur;

        PlayableDirector director;
        bool captured;
        Vector3 basePosition, shakePosition;
        Quaternion baseRotation, shakeRotation;
        LensSettings lens;
        float amplitude, frequency, blurWeight;
        bool listenerEnabled;

        void OnEnable()
        {
            director = GetComponent<PlayableDirector>();
            director.played += Capture;
            director.stopped += Restore;
            Capture(director);
        }

        void Capture(PlayableDirector unused)
        {
            if (captured || !baseMotion || !virtualCamera || !noise || !motionBlur || !impulseListener)
                return;
            basePosition = baseMotion.localPosition;
            baseRotation = baseMotion.localRotation;
            shakePosition = virtualCamera.transform.localPosition;
            shakeRotation = virtualCamera.transform.localRotation;
            lens = virtualCamera.Lens;
            amplitude = noise.AmplitudeGain;
            frequency = noise.FrequencyGain;
            blurWeight = motionBlur.weight;
            listenerEnabled = impulseListener.enabled;
            captured = true;
            impulseListener.enabled = true;
        }

        void OnDisable()
        {
            if (director)
            {
                // Destroy the graph before restoring values so its last evaluation
                // cannot write the animated pose back over the neutral pose.
                director.Stop();
                director.played -= Capture;
                director.stopped -= Restore;
            }
            Restore(director);
        }

        void Restore(PlayableDirector unused)
        {
            if (!captured) return;
            captured = false;
            if (baseMotion) baseMotion.SetLocalPositionAndRotation(basePosition, baseRotation);
            if (virtualCamera)
            {
                virtualCamera.transform.SetLocalPositionAndRotation(shakePosition, shakeRotation);
                virtualCamera.Lens = lens;
                virtualCamera.PreviousStateIsValid = false;
            }
            if (noise)
            {
                noise.AmplitudeGain = amplitude;
                noise.FrequencyGain = frequency;
            }
            if (motionBlur) motionBlur.weight = blurWeight;
            if (impulseListener) impulseListener.enabled = listenerEnabled;
        }
    }
}
