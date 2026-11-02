using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CK.SemesterProject.UIAnimation.EditorTools
{
    // 플레이 없이 씬의 Image들을 한 타임라인으로 재생한다. AnimationMode가 값을 되돌리므로 씬은 바뀌지 않는다.
    public sealed class UIAnimationPreviewer
    {
        public readonly struct Entry
        {
            public Entry(GameObject target, AnimationClip clip, double start, float speed)
            {
                Target = target;
                Clip = clip;
                Start = start;
                Speed = speed;
            }

            public GameObject Target { get; }
            public AnimationClip Clip { get; }
            public double Start { get; }
            public float Speed { get; }
            public double End => Start + Clip.length / Speed;
        }

        private const double EndHoldSeconds = 0.35;

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<AnimationClip> _temporaryClips = new List<AnimationClip>();
        private readonly List<GameObject> _turnedOn = new List<GameObject>();
        private double _lastTick;

        public event Action Stopped;
        public event Action Ticked;

        public bool IsPlaying { get; private set; }
        public bool Loop { get; set; }
        public float Rate { get; set; } = 1f;
        public bool Paused { get; private set; }
        public double Position { get; private set; }
        public double Duration { get; private set; }

        public void Play(IEnumerable<Entry> entries, IEnumerable<AnimationClip> temporaryClips = null)
        {
            Stop();
            _entries.AddRange(entries);
            if (temporaryClips != null)
            {
                _temporaryClips.AddRange(temporaryClips);
            }
            if (_entries.Count == 0)
            {
                DestroyTemporaryClips();
                return;
            }

            IsPlaying = true;
            Paused = false;
            Position = 0.0;
            Duration = _entries.Max(entry => entry.End);
            _lastTick = EditorApplication.timeSinceStartup;
            if (!AnimationMode.InAnimationMode())
            {
                AnimationMode.StartAnimationMode();
            }

            // 꺼 둔 패널도 미리보기에서 보이게 켰다가 멈출 때 되돌린다.
            foreach (GameObject target in _entries.Select(entry => entry.Target).Distinct())
            {
                if (!target.activeSelf)
                {
                    target.SetActive(true);
                    _turnedOn.Add(target);
                }
            }
            EditorApplication.update += Tick;
        }

        // 슬라이더로 원하는 순간을 멈춰 볼 때 쓴다.
        public void Seek(double position)
        {
            Paused = true;
            Position = Math.Max(0.0, Math.Min(Duration, position));
        }

        public void Resume()
        {
            Paused = false;
            _lastTick = EditorApplication.timeSinceStartup;
        }

        public void Stop()
        {
            if (!IsPlaying)
            {
                return;
            }

            IsPlaying = false;
            EditorApplication.update -= Tick;
            if (AnimationMode.InAnimationMode())
            {
                AnimationMode.StopAnimationMode();
            }
            DestroyTemporaryClips();
            foreach (GameObject target in _turnedOn)
            {
                if (target != null)
                {
                    target.SetActive(false);
                }
            }
            _turnedOn.Clear();
            _entries.Clear();
            Refresh();
            Stopped?.Invoke();
        }

        private void DestroyTemporaryClips()
        {
            foreach (AnimationClip clip in _temporaryClips)
            {
                if (clip != null)
                {
                    UnityEngine.Object.DestroyImmediate(clip);
                }
            }
            _temporaryClips.Clear();
        }

        private void Tick()
        {
            // 대상이 지워졌거나 다른 창이 AnimationMode를 끝냈으면 미리보기도 끝낸다.
            if (!AnimationMode.InAnimationMode() || _entries.Any(entry => entry.Target == null))
            {
                Stop();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (!Paused)
            {
                Position += (now - _lastTick) * Rate;
            }
            _lastTick = now;

            if (Position > Duration + EndHoldSeconds)
            {
                if (!Loop)
                {
                    Stop();
                    return;
                }
                Position = 0.0;
            }
            Sample(Math.Min(Position, Duration));
            Ticked?.Invoke();
        }

        private void Sample(double position)
        {
            AnimationMode.BeginSampling();
            try
            {
                foreach (IGrouping<GameObject, Entry> group in _entries.GroupBy(entry => entry.Target))
                {
                    // 아직 시작 전이면 첫 항목의 시작 모습을 보여 준다.
                    Entry current = group.First();
                    foreach (Entry entry in group)
                    {
                        if (entry.Start <= position)
                        {
                            current = entry;
                        }
                    }
                    double time = Math.Max(0.0, position - current.Start) * current.Speed;
                    AnimationMode.SampleAnimationClip(current.Target, current.Clip, Mathf.Min((float)time, current.Clip.length));
                }
            }
            finally
            {
                AnimationMode.EndSampling();
            }
            Refresh();
        }

        private static void Refresh()
        {
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }
}
