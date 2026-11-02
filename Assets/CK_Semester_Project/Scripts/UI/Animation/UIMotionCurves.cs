using System;
using System.Collections.Generic;
using UnityEngine;

namespace CK.SemesterProject.UIAnimation
{
    // 구간 경계와 정착점을 키로 남겨 베이크와 인스펙터에서 같은 모양을 쓴다.
    public static class UIMotionCurves
    {
        private const int Samples = 24;

        public static AnimationCurve Window(UIEase ease, float start, float end)
        {
            if (start < 0f || end > 1f || start >= end) throw new ArgumentOutOfRangeException(nameof(start));
            var keys = new List<Keyframe>();
            if (start > 0f) keys.Add(new Keyframe(0f, 0f));
            Segment(keys, start, end, 0f, 1f, ease);
            if (end < 1f) keys.Add(new Keyframe(1f, 1f));
            return LinearKeys(keys);
        }

        public static AnimationCurve Settle(float peak, float peakTime, UIEase rise, UIEase fall)
        {
            if (peakTime <= 0f || peakTime >= 1f) throw new ArgumentOutOfRangeException(nameof(peakTime));
            var keys = new List<Keyframe>();
            Segment(keys, 0f, peakTime, 0f, peak, rise);
            Segment(keys, peakTime, 1f, peak, 1f, fall);
            return LinearKeys(keys);
        }

        public static AnimationCurve Anticipate(float dip, float dipTime, UIEase ease)
        {
            return Settle(-Mathf.Abs(dip), dipTime, UIEase.EaseOutQuad, ease);
        }

        public static AnimationCurve Steps(params (float t, float v)[] keys)
        {
            if (keys == null || keys.Length < 2) throw new ArgumentException("키가 두 개 이상 필요합니다.", nameof(keys));
            var frames = new Keyframe[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i].t < 0f || keys[i].t > 1f || i > 0 && keys[i].t <= keys[i - 1].t)
                    throw new ArgumentException("키 시간은 0~1에서 증가해야 합니다.", nameof(keys));
                frames[i] = new Keyframe(keys[i].t, keys[i].v, float.PositiveInfinity, float.PositiveInfinity);
            }
            return new AnimationCurve(frames);
        }

        public static AnimationCurve Reverse(AnimationCurve source)
        {
            Keyframe[] original = source.keys;
            var keys = new List<Keyframe>();
            for (int i = original.Length - 1; i >= 0; i--)
            {
                Keyframe key = original[i];
                var reversed = new Keyframe(1f - key.time, 1f - key.value, key.outTangent, key.inTangent,
                    key.outWeight, key.inWeight);
                reversed.weightedMode = key.weightedMode == WeightedMode.In ? WeightedMode.Out
                    : key.weightedMode == WeightedMode.Out ? WeightedMode.In : key.weightedMode;
                keys.Add(reversed);
                // 역방향 컷의 경계값과 경계 직후 값을 모두 보존한다.
                if (i > 0 && float.IsInfinity(original[i - 1].outTangent) && original[i - 1].value != key.value)
                {
                    float epsilon = Mathf.Min(0.000001f, (key.time - original[i - 1].time) * 0.01f);
                    reversed.outTangent = float.PositiveInfinity;
                    keys[keys.Count - 1] = reversed;
                    keys.Add(new Keyframe(1f - key.time + epsilon, 1f - original[i - 1].value,
                        float.PositiveInfinity, float.PositiveInfinity));
                }
            }
            return new AnimationCurve(keys.ToArray());
        }

        private static void Segment(List<Keyframe> keys, float start, float end, float from, float to, UIEase ease)
        {
            for (int i = keys.Count > 0 && keys[keys.Count - 1].time == start ? 1 : 0; i <= Samples; i++)
            {
                float u = i / (float)Samples;
                keys.Add(new Keyframe(Mathf.Lerp(start, end, u), Mathf.LerpUnclamped(from, to, UIEasing.Evaluate(ease, u))));
            }
        }

        private static AnimationCurve LinearKeys(List<Keyframe> keys)
        {
            for (int i = 0; i < keys.Count; i++)
            {
                Keyframe key = keys[i];
                key.inTangent = i == 0 ? 0f : (key.value - keys[i - 1].value) / (key.time - keys[i - 1].time);
                key.outTangent = i == keys.Count - 1 ? 0f : (keys[i + 1].value - key.value) / (keys[i + 1].time - key.time);
                keys[i] = key;
            }
            return new AnimationCurve(keys.ToArray());
        }
    }
}
