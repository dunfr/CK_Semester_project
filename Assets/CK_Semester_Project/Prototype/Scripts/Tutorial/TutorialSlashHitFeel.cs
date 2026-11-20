using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;

namespace CK.SemesterProject.Tutorial
{
    /// <summary>
    /// TutorialSlashHitFeelTrack의 히트스톱·카메라 흔들림을 읽어, 실시간(real)과 타임라인 시간(content)을 서로 바꿔 준다.
    /// 시간 변환은 베기 타임라인 0초(= 시퀀스의 ApproachStart) 기준의 로컬 시간이다.
    /// </summary>
    public sealed class TutorialSlashHitFeel
    {
        private readonly struct Stop
        {
            public readonly float Start;
            public readonly float Real;
            public readonly float Scale;
            public float Content => Real * Scale;

            public Stop(float start, float real, float scale)
            {
                Start = start;
                Real = real;
                Scale = scale;
            }
        }

        private readonly struct Shake
        {
            public readonly TutorialSlashHitFeelClip Clip;
            public readonly float StartContent;
            public readonly float Duration;
            public readonly float HoldTime;

            public Shake(TutorialSlashHitFeelClip clip, float startContent, float duration, float holdTime)
            {
                Clip = clip;
                StartContent = startContent;
                Duration = duration;
                HoldTime = holdTime;
            }
        }

        public static readonly TutorialSlashHitFeel None = new TutorialSlashHitFeel(null);

        private readonly Stop[] _stops;
        private readonly Shake[] _shakes;

        /// <summary>히트스톱 때문에 실시간이 타임라인보다 더 길어지는 총 시간(초).</summary>
        public float ExtraRealSeconds { get; }

        public TutorialSlashHitFeel(TimelineAsset timeline)
        {
            var stops = new List<Stop>();
            var shakeClips = new List<(TutorialSlashHitFeelClip clip, float start, float duration)>();
            if (timeline != null)
            {
                foreach (TrackAsset track in timeline.GetOutputTracks())
                {
                    if (!(track is TutorialSlashHitFeelTrack) || track.muted)
                    {
                        continue;
                    }
                    foreach (TimelineClip timelineClip in track.GetClips())
                    {
                        if (!(timelineClip.asset is TutorialSlashHitFeelClip clip))
                        {
                            continue;
                        }
                        if (clip.Kind == TutorialSlashHitFeelClip.FeelKind.HitStop)
                        {
                            stops.Add(new Stop((float)timelineClip.start, Mathf.Max(0f, (float)timelineClip.duration),
                                Mathf.Clamp01(clip.SlowScale)));
                        }
                        else
                        {
                            shakeClips.Add((clip, (float)timelineClip.start, (float)timelineClip.duration));
                        }
                    }
                }
            }
            stops.Sort((a, b) => a.Start.CompareTo(b.Start));
            // 겹치는 히트스톱은 뒤의 것을 버린다.
            for (int i = stops.Count - 1; i > 0; i--)
            {
                if (stops[i].Start < stops[i - 1].Start + stops[i - 1].Content)
                {
                    stops.RemoveAt(i);
                }
            }
            _stops = stops.ToArray();
            float extra = 0f;
            foreach (Stop stop in _stops)
            {
                extra += stop.Real - stop.Content;
            }
            ExtraRealSeconds = extra;

            _shakes = new Shake[shakeClips.Count];
            for (int i = 0; i < _shakes.Length; i++)
            {
                (TutorialSlashHitFeelClip clip, float start, float duration) = shakeClips[i];
                float hold = 0f;
                if (clip.HoldDuringHitStop)
                {
                    foreach (Stop stop in _stops)
                    {
                        if (stop.Start >= start - 0.0001f && stop.Start < start + duration)
                        {
                            hold = Mathf.Max(hold, Mathf.Min(duration, stop.Real));
                        }
                    }
                }
                _shakes[i] = new Shake(clip, start, duration, hold);
            }
        }

        public bool HasEffects => _stops.Length > 0 || _shakes.Length > 0;

        /// <summary>실시간(로컬 초) → 타임라인 시간.</summary>
        public float ToContent(float real)
        {
            float extra = 0f;
            foreach (Stop stop in _stops)
            {
                float realStart = stop.Start + extra;
                if (real < realStart)
                {
                    break;
                }
                if (real < realStart + stop.Real)
                {
                    return stop.Start + (real - realStart) * stop.Scale;
                }
                extra += stop.Real - stop.Content;
            }
            return real - extra;
        }

        /// <summary>타임라인 시간 → 그 시점에 도달하는 실시간(로컬 초).</summary>
        public float ToReal(float content)
        {
            float extra = 0f;
            foreach (Stop stop in _stops)
            {
                if (content <= stop.Start)
                {
                    break;
                }
                if (content < stop.Start + stop.Content)
                {
                    return stop.Start + extra + (stop.Scale > 0f ? (content - stop.Start) / stop.Scale : 0f);
                }
                extra += stop.Real - stop.Content;
            }
            return content + extra;
        }

        /// <summary>실시간(로컬 초)에서의 카메라 흔들림 합(위치 m, 회전 도). 흔들림이 없으면 false.</summary>
        public bool EvaluateShake(float real, out Vector3 position, out Vector3 rotation)
        {
            position = Vector3.zero;
            rotation = Vector3.zero;
            bool any = false;
            for (int i = 0; i < _shakes.Length; i++)
            {
                Shake shake = _shakes[i];
                float local = real - ToReal(shake.StartContent);
                if (shake.Clip.EvaluateShake(local, shake.Duration, shake.HoldTime, i * 13 + 5, out Vector3 shakePosition, out Vector3 shakeRotation))
                {
                    position += shakePosition;
                    rotation += shakeRotation;
                    any = true;
                }
            }
            return any;
        }
    }
}
