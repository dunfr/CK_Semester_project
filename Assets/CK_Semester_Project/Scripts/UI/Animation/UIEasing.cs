using UnityEngine;

namespace CK.SemesterProject.UIAnimation
{
    // Robert Penner의 easing 식(© 2001 Robert Penner, MIT/BSD-3-Clause)을 수학 정의에서 직접 구현했다.
    // 상수와 이름은 easings.net을 따른다. 그 저장소 코드는 GPL-3.0이라 옮겨 오지 않는다.
    public static class UIEasing
    {
        private const float BackOvershoot = 1.70158f;
        private const float BackInOutOvershoot = BackOvershoot * 1.525f;
        private const float BackCubic = BackOvershoot + 1f;
        private const float ElasticPeriod = 2f * Mathf.PI / 3f;
        private const float ElasticInOutPeriod = 2f * Mathf.PI / 4.5f;
        private const float BounceScale = 7.5625f;
        private const float BounceSpan = 2.75f;

        // 0~1 범위를 벗어나는 곡선이다.
        public static bool Overshoots(UIEase ease)
        {
            return ease >= UIEase.EaseInBack && ease <= UIEase.EaseInOutElastic;
        }

        // In과 Out을 맞바꾼다. Open을 거꾸로 돌린 Close에 쓴다.
        public static UIEase Mirror(UIEase ease)
        {
            if (ease == UIEase.Linear || ease == UIEase.Custom)
            {
                return ease;
            }
            int kind = ((int)ease - 1) % 3;
            return kind == 0 ? ease + 1 : kind == 1 ? ease - 1 : ease;
        }

        public static float Evaluate(UIEase ease, float t)
        {
            float x = Mathf.Clamp01(t);
            switch (ease)
            {
                case UIEase.EaseInSine: return 1f - Mathf.Cos(x * Mathf.PI * 0.5f);
                case UIEase.EaseOutSine: return Mathf.Sin(x * Mathf.PI * 0.5f);
                case UIEase.EaseInOutSine: return (1f - Mathf.Cos(x * Mathf.PI)) * 0.5f;

                case UIEase.EaseInQuad: return In(x, 2);
                case UIEase.EaseOutQuad: return Out(x, 2);
                case UIEase.EaseInOutQuad: return InOut(x, 2);
                case UIEase.EaseInCubic: return In(x, 3);
                case UIEase.EaseOutCubic: return Out(x, 3);
                case UIEase.EaseInOutCubic: return InOut(x, 3);
                case UIEase.EaseInQuart: return In(x, 4);
                case UIEase.EaseOutQuart: return Out(x, 4);
                case UIEase.EaseInOutQuart: return InOut(x, 4);
                case UIEase.EaseInQuint: return In(x, 5);
                case UIEase.EaseOutQuint: return Out(x, 5);
                case UIEase.EaseInOutQuint: return InOut(x, 5);

                case UIEase.EaseInExpo: return x <= 0f ? 0f : Mathf.Pow(2f, 10f * x - 10f);
                case UIEase.EaseOutExpo: return x >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * x);
                case UIEase.EaseInOutExpo: return ExpoInOut(x);

                case UIEase.EaseInCirc: return 1f - Root(x);
                case UIEase.EaseOutCirc: return Root(1f - x);
                case UIEase.EaseInOutCirc:
                    return x < 0.5f ? (1f - Root(2f * x)) * 0.5f : (1f + Root(2f - 2f * x)) * 0.5f;

                case UIEase.EaseInBack: return BackIn(x);
                case UIEase.EaseOutBack: return 1f - BackIn(1f - x);
                case UIEase.EaseInOutBack: return BackInOut(x);

                case UIEase.EaseInElastic: return ElasticIn(x);
                case UIEase.EaseOutElastic: return ElasticOut(x);
                case UIEase.EaseInOutElastic: return ElasticInOut(x);

                case UIEase.EaseInBounce: return 1f - Bounce(1f - x);
                case UIEase.EaseOutBounce: return Bounce(x);
                case UIEase.EaseInOutBounce:
                    return x < 0.5f ? (1f - Bounce(1f - 2f * x)) * 0.5f : (1f + Bounce(2f * x - 1f)) * 0.5f;

                default: return x;
            }
        }

        private static float In(float x, int power)
        {
            return Mathf.Pow(x, power);
        }

        private static float Out(float x, int power)
        {
            return 1f - Mathf.Pow(1f - x, power);
        }

        private static float InOut(float x, int power)
        {
            return x < 0.5f
                ? Mathf.Pow(2f, power - 1) * Mathf.Pow(x, power)
                : 1f - Mathf.Pow(2f - 2f * x, power) * 0.5f;
        }

        private static float ExpoInOut(float x)
        {
            if (x <= 0f || x >= 1f)
            {
                return x <= 0f ? 0f : 1f;
            }
            return x < 0.5f
                ? Mathf.Pow(2f, 20f * x - 10f) * 0.5f
                : (2f - Mathf.Pow(2f, 10f - 20f * x)) * 0.5f;
        }

        private static float Root(float x)
        {
            return Mathf.Sqrt(Mathf.Max(0f, 1f - x * x));
        }

        private static float BackIn(float x)
        {
            return BackCubic * x * x * x - BackOvershoot * x * x;
        }

        private static float BackInOut(float x)
        {
            float k = BackInOutOvershoot + 1f;
            if (x < 0.5f)
            {
                float a = 2f * x;
                return a * a * (k * a - BackInOutOvershoot) * 0.5f;
            }
            float b = 2f * x - 2f;
            return (b * b * (k * b + BackInOutOvershoot) + 2f) * 0.5f;
        }

        private static float ElasticIn(float x)
        {
            if (x <= 0f || x >= 1f)
            {
                return x <= 0f ? 0f : 1f;
            }
            return -Mathf.Pow(2f, 10f * x - 10f) * Mathf.Sin((10f * x - 10.75f) * ElasticPeriod);
        }

        private static float ElasticOut(float x)
        {
            if (x <= 0f || x >= 1f)
            {
                return x <= 0f ? 0f : 1f;
            }
            return Mathf.Pow(2f, -10f * x) * Mathf.Sin((10f * x - 0.75f) * ElasticPeriod) + 1f;
        }

        private static float ElasticInOut(float x)
        {
            if (x <= 0f || x >= 1f)
            {
                return x <= 0f ? 0f : 1f;
            }
            float wave = Mathf.Sin((20f * x - 11.125f) * ElasticInOutPeriod);
            return x < 0.5f
                ? -Mathf.Pow(2f, 20f * x - 10f) * wave * 0.5f
                : Mathf.Pow(2f, 10f - 20f * x) * wave * 0.5f + 1f;
        }

        private static float Bounce(float x)
        {
            if (x < 1f / BounceSpan)
            {
                return BounceScale * x * x;
            }
            if (x < 2f / BounceSpan)
            {
                x -= 1.5f / BounceSpan;
                return BounceScale * x * x + 0.75f;
            }
            if (x < 2.5f / BounceSpan)
            {
                x -= 2.25f / BounceSpan;
                return BounceScale * x * x + 0.9375f;
            }
            x -= 2.625f / BounceSpan;
            return BounceScale * x * x + 0.984375f;
        }
    }
}
