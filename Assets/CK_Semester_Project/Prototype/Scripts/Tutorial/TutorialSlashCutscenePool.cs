using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Pool;

namespace CK.SemesterProject.Tutorial
{
    internal sealed class TutorialSlashCutscenePool : IDisposable
    {
        internal sealed class Rig
        {
            private readonly struct ChildState
            {
                private readonly Transform _transform;
                private readonly Vector3 _position;
                private readonly Quaternion _rotation;
                private readonly Vector3 _scale;
                private readonly bool _active;

                public ChildState(Transform transform)
                {
                    _transform = transform;
                    _position = transform.localPosition;
                    _rotation = transform.localRotation;
                    _scale = transform.localScale;
                    _active = transform.gameObject.activeSelf;
                }

                public void Restore()
                {
                    _transform.SetLocalPositionAndRotation(_position, _rotation);
                    _transform.localScale = _scale;
                    _transform.gameObject.SetActive(_active);
                }
            }

            private readonly ChildState[] _children;
            private readonly ParticleSystem[] _particles;
            public PlayableDirector Director { get; }
            public CinemachineCamera Shot { get; }
            public TutorialSlashFramingReceiver Framing { get; }

            public Rig(PlayableDirector director)
            {
                Director = director;
                Shot = director.GetComponentInChildren<CinemachineCamera>(true);
                Framing = director.GetComponent<TutorialSlashFramingReceiver>();
                Transform[] hierarchy = director.GetComponentsInChildren<Transform>(true);
                _children = new ChildState[hierarchy.Length - 1];
                for (int i = 1; i < hierarchy.Length; i++)
                {
                    _children[i - 1] = new ChildState(hierarchy[i]);
                }
                _particles = director.GetComponentsInChildren<ParticleSystem>(true);
            }

            public void Reset(Transform parent)
            {
                Director.Stop();
                Director.gameObject.SetActive(false);
                foreach (ParticleSystem particle in _particles)
                {
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                foreach (ChildState child in _children)
                {
                    child.Restore();
                }
                Director.transform.SetParent(parent, false);
                Director.time = 0d;
                Framing.Frame = null;
            }
        }

        private readonly ObjectPool<Rig> _pool;

        public TutorialSlashCutscenePool(PlayableDirector prefab, Transform owner)
        {
            _pool = new ObjectPool<Rig>(
                createFunc: () =>
                {
                    var director = UnityEngine.Object.Instantiate(prefab, owner, false);
                    director.gameObject.SetActive(false);
                    return new Rig(director);
                },
                actionOnRelease: rig => rig.Reset(owner),
                actionOnDestroy: rig =>
                {
                    if (rig.Director != null)
                    {
                        UnityEngine.Object.Destroy(rig.Director.gameObject);
                    }
                },
                defaultCapacity: 1, maxSize: 1);
        }

        public Rig Get() => _pool.Get();
        public void Release(Rig rig) => _pool.Release(rig);
        public void Dispose() => _pool.Dispose();
    }
}
