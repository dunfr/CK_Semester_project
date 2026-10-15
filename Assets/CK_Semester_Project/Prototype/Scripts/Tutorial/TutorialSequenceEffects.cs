using System.Collections;
using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    public sealed class TutorialSequenceEffects : MonoBehaviour
    {
        [SerializeField] private Renderer[] _capsuleIndicators;
        [SerializeField] private Material _alarmMaterial;
        [SerializeField] private Material _effectMaterial;
        [SerializeField, Range(0f, 1f), Tooltip("임시 효과음 재생 음량(0~1)")]
        private float _volume = 0.18f;

        private AudioSource _source;
        private AudioClip[] _cues;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.spatialBlend = 0f;
            _cues = new AudioClip[4];
            for (int cue = 0; cue < _cues.Length; cue++)
            {
                int rate = 22050;
                float seconds = cue == 3 ? 0.7f : 0.2f;
                var samples = new float[Mathf.CeilToInt(rate * seconds)];
                var random = new System.Random(314 + cue);
                for (int index = 0; index < samples.Length; index++)
                {
                    float time = index / (float)rate;
                    float envelope = Mathf.Sin(Mathf.PI * index / samples.Length);
                    float frequency = cue == 0 ? 180f : cue == 1 ? 520f + time * 1200f : 880f;
                    samples[index] = cue == 3 ? (float)(random.NextDouble() * 2 - 1) * envelope
                        : Mathf.Sin(2 * Mathf.PI * frequency * time) * envelope * 0.6f;
                }
                _cues[cue] = AudioClip.Create("Tutorial Placeholder Cue " + cue, samples.Length, 1, rate, false);
                _cues[cue].SetData(samples, 0);
            }
        }

        public void SetCapsuleAlarm(bool enabled)
        {
            if (!enabled)
            {
                return;
            }
            foreach (Renderer indicator in _capsuleIndicators)
            {
                indicator.sharedMaterial = _alarmMaterial;
            }
        }

        public void PlayCue(int cue, Vector3 position)
        {
            _source.PlayOneShot(_cues[Mathf.Clamp(cue, 0, _cues.Length - 1)], _volume);
        }

        public void PlayCollapse(Vector3 position)
        {
            PlayCue(3, position);
            StartCoroutine(CollapseEffect(position));
        }

        private IEnumerator CollapseEffect(Vector3 position)
        {
            var ringObject = new GameObject("Collapse Sound Wave");
            var ring = ringObject.AddComponent<LineRenderer>();
            ring.sharedMaterial = _effectMaterial;
            ring.positionCount = 49;
            ring.startWidth = ring.endWidth = 0.06f;
            ring.startColor = ring.endColor = new Color(0.4f, 0.85f, 1f, 0.7f);
            var fragments = new GameObject[8];
            for (int index = 0; index < fragments.Length; index++)
            {
                var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = "Duct Debris";
                Destroy(piece.GetComponent<Collider>());
                piece.GetComponent<Renderer>().sharedMaterial = _effectMaterial;
                piece.transform.localScale = Vector3.one * 0.12f;
                fragments[index] = piece;
            }
            for (float elapsed = 0; elapsed < 1f; elapsed += Time.deltaTime)
            {
                for (int index = 0; index < ring.positionCount; index++)
                {
                    float angle = index * Mathf.PI * 2 / 48;
                    ring.SetPosition(index, position + new Vector3(Mathf.Cos(angle), 0.08f, Mathf.Sin(angle)) * (elapsed * 12f));
                }
                for (int index = 0; index < fragments.Length; index++)
                {
                    float angle = index * Mathf.PI * 2 / fragments.Length;
                    fragments[index].transform.position = position + new Vector3(Mathf.Cos(angle) * elapsed * 2,
                        elapsed * 2 - elapsed * elapsed * 5, Mathf.Sin(angle) * elapsed * 2);
                    fragments[index].transform.Rotate(Vector3.one * (Time.deltaTime * 180));
                }
                yield return null;
            }
            Destroy(ringObject);
            foreach (GameObject fragment in fragments)
            {
                Destroy(fragment);
            }
        }

        private void OnDestroy()
        {
            if (_cues == null)
            {
                return;
            }
            foreach (AudioClip cue in _cues)
            {
                Destroy(cue);
            }
        }
    }
}
