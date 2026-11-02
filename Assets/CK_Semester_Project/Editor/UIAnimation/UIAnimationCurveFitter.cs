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
