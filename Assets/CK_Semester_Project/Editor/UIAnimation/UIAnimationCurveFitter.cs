using System;
using System.Collections.Generic;
using UnityEngine;

namespace CK.SemesterProject.UIAnimation.EditorTools
{
    // 임의의 곡선을 AnimationCurve 키로 옮긴다. 오차가 큰 구간만 나누어 Animation 창에서도 읽을 만한 키 수를 유지한다.
    public static class UIAnimationCurveFitter
    {
        private const float Tolerance = 0.0005f;
        private const float SlopeStep = 0.0001f;
        private const int MaxDepth = 9;
        private const int ErrorSamples = 6;

        public static AnimationCurve Fit(Func<float, float> value, float duration)
        {
            var points = new List<float> { 0f };
            Split(value, 0f, 1f, 0, points);

            var keys = new Keyframe[points.Count];
            for (int index = 0; index < keys.Length; index++)
            {
                float u = points[index];
                float inSlope = index == 0 ? 0f : LeftSlope(value, u) / duration;
                float outSlope = index == keys.Length - 1 ? 0f : RightSlope(value, u) / duration;
                keys[index] = new Keyframe(u * duration, value(u), inSlope, outSlope);
            }
            return new AnimationCurve(keys);
        }

        // 일정 간격으로 값을 뛰게 해 애니메이션 작화처럼 끊어 보이게 한다.
        public static AnimationCurve Stepped(Func<float, float> value, float duration, int stepsPerSecond)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt(duration * stepsPerSecond));
            var keys = new Keyframe[count + 1];
            for (int index = 0; index <= count; index++)
            {
                float u = index / (float)count;
                keys[index] = new Keyframe(u * duration, value(u), float.PositiveInfinity, float.PositiveInfinity);
            }
            return new AnimationCurve(keys);
        }

        // 커스텀 곡선은 꺾이는 키부터 나눈다. 짧은 점멸이나 정착점을 적응 피팅이 건너뛰지 않게 하고, 나머지는 오차로 줄인다.
        public static AnimationCurve Fit(Func<float, float> value, float duration, UIEaseCurve source, bool roundTrip)
        {
            if (source == null || source.Ease != UIEase.Custom) return Fit(value, duration);
            Keyframe[] shape = ShapeKeys(source.Custom, roundTrip);
            var boundaries = new SortedSet<float> { 0f, 1f };
            foreach (Keyframe key in shape) if (key.time > 0f && key.time < 1f && IsCorner(key)) boundaries.Add(key.time);
            var seeds = new List<float>(boundaries);
            var points = new List<float> { 0f };
            for (int i = 1; i < seeds.Count; i++)
            {
                if (Holds(shape, (seeds[i - 1] + seeds[i]) * 0.5f)) points.Add(seeds[i]);
                else Split(value, seeds[i - 1], seeds[i], 0, points);
            }
            var keys = new Keyframe[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                float u = points[i];
                float left = i == 0 ? 0f : Mathf.Min(SlopeStep, (u - points[i - 1]) * 0.1f);
                float right = i == points.Count - 1 ? 0f : Mathf.Min(SlopeStep, (points[i + 1] - u) * 0.1f);
                float incoming = left == 0f ? 0f : (value(u) - value(u - left)) / (left * duration);
                float outgoing = right == 0f ? 0f : (value(u + right) - value(u)) / (right * duration);
                if (i > 0 && Holds(shape, (points[i - 1] + u) * 0.5f)) incoming = float.PositiveInfinity;
                if (i + 1 < points.Count && Holds(shape, (points[i + 1] + u) * 0.5f)) outgoing = float.PositiveInfinity;
                keys[i] = new Keyframe(u * duration, value(u), incoming, outgoing);
            }
            return new AnimationCurve(keys);
        }

        public static AnimationCurve Stepped(Func<float, float> value, float duration, int stepsPerSecond, UIEaseCurve source, bool roundTrip)
        {
            if (source == null || source.Ease != UIEase.Custom) return Stepped(value, duration, stepsPerSecond);
            int count = Mathf.Max(1, Mathf.CeilToInt(duration * stepsPerSecond));
            var times = new SortedSet<float>();
            for (int i = 0; i <= count; i++) times.Add(i / (float)count);
            Keyframe[] shape = ShapeKeys(source.Custom, roundTrip);
            foreach (Keyframe key in shape) if (key.time > 0f && key.time < 1f && IsCorner(key)) times.Add(key.time);
            var keys = new List<Keyframe>();
            foreach (float u in times) keys.Add(new Keyframe(u * duration, value(u), float.PositiveInfinity, float.PositiveInfinity));
            return new AnimationCurve(keys.ToArray());
        }

        // 끊긴 키, 방향이 바뀌는 키, 멈춤과 움직임의 경계만 모양을 정한다.
        private static bool IsCorner(Keyframe key)
        {
            bool cut = float.IsInfinity(key.inTangent) || float.IsInfinity(key.outTangent);
            bool turn = key.inTangent * key.outTangent < 0f;
            bool edge = Mathf.Approximately(key.inTangent, 0f) != Mathf.Approximately(key.outTangent, 0f);
            return cut || turn || edge;
        }

        private static Keyframe[] ShapeKeys(AnimationCurve curve, bool roundTrip)
        {
            if (!roundTrip) return curve.keys;
            var keys = new List<Keyframe>();
            foreach (Keyframe original in curve.keys)
                keys.Add(new Keyframe(original.time * 0.5f, original.value, original.inTangent * 2f, original.outTangent * 2f));
            foreach (Keyframe reversed in UIMotionCurves.Reverse(curve).keys)
            {
                if (reversed.time <= 0f) continue;
                keys.Add(new Keyframe(0.5f + reversed.time * 0.5f, 1f - reversed.value,
                    -reversed.inTangent * 2f, -reversed.outTangent * 2f));
            }
            return keys.ToArray();
        }

        private static bool Holds(Keyframe[] shape, float time)
        {
            for (int i = 0; i + 1 < shape.Length; i++)
                if (time >= shape[i].time && time < shape[i + 1].time)
                    return float.IsInfinity(shape[i].outTangent) || float.IsInfinity(shape[i + 1].inTangent);
            return false;
        }

        private static void Split(Func<float, float> value, float start, float end, int depth, List<float> points)
        {
            if (depth >= MaxDepth || Fits(value, start, end))
            {
                points.Add(end);
                return;
            }
            float middle = (start + end) * 0.5f;
            Split(value, start, middle, depth + 1, points);
            Split(value, middle, end, depth + 1, points);
        }

        private static bool Fits(Func<float, float> value, float start, float end)
        {
            float width = end - start;
            float p0 = value(start);
            float p1 = value(end);
            float m0 = RightSlope(value, start) * width;
            float m1 = LeftSlope(value, end) * width;
            for (int index = 1; index <= ErrorSamples; index++)
            {
                float s = index / (ErrorSamples + 1f);
                if (Mathf.Abs(Hermite(p0, m0, p1, m1, s) - value(start + width * s)) > Tolerance)
                {
                    return false;
                }
            }
            return true;
        }

        private static float Hermite(float p0, float m0, float p1, float m1, float s)
        {
            float s2 = s * s;
            float s3 = s2 * s;
            return (2f * s3 - 3f * s2 + 1f) * p0 + (s3 - 2f * s2 + s) * m0
                + (-2f * s3 + 3f * s2) * p1 + (s3 - s2) * m1;
        }

        // 꺾이는 지점(Bounce 등)을 살리려고 왼쪽과 오른쪽 기울기를 따로 구한다.
        // u 자체는 건너뛴다. Expo는 양 끝에서 값이 살짝 끊겨 그 점을 넣으면 기울기가 튄다.
        private static float LeftSlope(Func<float, float> value, float u)
        {
            float h = SlopeStep;
            return (3f * value(u - h) - 4f * value(u - 2f * h) + value(u - 3f * h)) / (2f * h);
        }

        private static float RightSlope(Func<float, float> value, float u)
        {
            float h = SlopeStep;
            return (-3f * value(u + h) + 4f * value(u + 2f * h) - value(u + 3f * h)) / (2f * h);
        }
    }
}
