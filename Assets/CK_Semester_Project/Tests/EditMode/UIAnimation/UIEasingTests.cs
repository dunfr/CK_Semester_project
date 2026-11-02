using System;
using System.Linq;
using CK.SemesterProject.UIAnimation.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace CK.SemesterProject.UIAnimation.Tests
{
    public sealed class UIEasingTests
    {
        private const float Precision = 0.00002f;

        // 기대값은 easings.net에 공개된 식을 코드와 별도로 계산해 넣었다.
        [TestCase(UIEase.EaseInSine, 0.076120f, 0.292893f, 0.690983f)]
        [TestCase(UIEase.EaseOutSine, 0.382683f, 0.707107f, 0.951057f)]
        [TestCase(UIEase.EaseInOutSine, 0.146447f, 0.500000f, 0.904508f)]
        [TestCase(UIEase.EaseInQuad, 0.062500f, 0.250000f, 0.640000f)]
        [TestCase(UIEase.EaseOutQuad, 0.437500f, 0.750000f, 0.960000f)]
        [TestCase(UIEase.EaseInOutQuad, 0.125000f, 0.500000f, 0.920000f)]
        [TestCase(UIEase.EaseInCubic, 0.015625f, 0.125000f, 0.512000f)]
        [TestCase(UIEase.EaseOutCubic, 0.578125f, 0.875000f, 0.992000f)]
        [TestCase(UIEase.EaseInOutCubic, 0.062500f, 0.500000f, 0.968000f)]
        [TestCase(UIEase.EaseInQuart, 0.003906f, 0.062500f, 0.409600f)]
        [TestCase(UIEase.EaseOutQuart, 0.683594f, 0.937500f, 0.998400f)]
        [TestCase(UIEase.EaseInOutQuart, 0.031250f, 0.500000f, 0.987200f)]
        [TestCase(UIEase.EaseInQuint, 0.000977f, 0.031250f, 0.327680f)]
        [TestCase(UIEase.EaseOutQuint, 0.762695f, 0.968750f, 0.999680f)]
        [TestCase(UIEase.EaseInOutQuint, 0.015625f, 0.500000f, 0.994880f)]
        [TestCase(UIEase.EaseInExpo, 0.005524f, 0.031250f, 0.250000f)]
        [TestCase(UIEase.EaseOutExpo, 0.823223f, 0.968750f, 0.996094f)]
        [TestCase(UIEase.EaseInOutExpo, 0.015625f, 0.500000f, 0.992188f)]
        [TestCase(UIEase.EaseInCirc, 0.031754f, 0.133975f, 0.400000f)]
        [TestCase(UIEase.EaseOutCirc, 0.661438f, 0.866025f, 0.979796f)]
        [TestCase(UIEase.EaseInOutCirc, 0.066987f, 0.500000f, 0.958258f)]
        [TestCase(UIEase.EaseInBack, -0.064137f, -0.087698f, 0.294198f)]
        [TestCase(UIEase.EaseOutBack, 0.817410f, 1.087697f, 1.046451f)]
        [TestCase(UIEase.EaseInOutBack, -0.099682f, 0.500000f, 1.092556f)]
        [TestCase(UIEase.EaseInElastic, -0.005524f, -0.015625f, -0.125000f)]
        [TestCase(UIEase.EaseOutElastic, 0.911612f, 1.015625f, 1.001953f)]
        [TestCase(UIEase.EaseInOutElastic, 0.011969f, 0.500000f, 1.003906f)]
        [TestCase(UIEase.EaseInBounce, 0.027344f, 0.234375f, 0.697500f)]
        [TestCase(UIEase.EaseOutBounce, 0.472656f, 0.765625f, 0.940000f)]
        [TestCase(UIEase.EaseInOutBounce, 0.117188f, 0.500000f, 0.886250f)]
        public void Evaluate_MatchesPublishedFormula(UIEase ease, float at25, float at50, float at80)
        {
            Assert.That(UIEasing.Evaluate(ease, 0.25f), Is.EqualTo(at25).Within(Precision));
            Assert.That(UIEasing.Evaluate(ease, 0.5f), Is.EqualTo(at50).Within(Precision));
            Assert.That(UIEasing.Evaluate(ease, 0.8f), Is.EqualTo(at80).Within(Precision));
        }

        [Test]
        public void EveryEase_StartsAtZeroAndEndsAtOne()
        {
            foreach (UIEase ease in Enum.GetValues(typeof(UIEase)))
            {
                Assert.That(UIEasing.Evaluate(ease, 0f), Is.EqualTo(0f).Within(Precision), ease.ToString());
                Assert.That(UIEasing.Evaluate(ease, 1f), Is.EqualTo(1f).Within(Precision), ease.ToString());
            }
        }

        [Test]
        public void Overshoots_MarksBackAndElasticOnly()
        {
            foreach (UIEase ease in Enum.GetValues(typeof(UIEase)))
            {
                bool leaves = false;
                for (int step = 0; step <= 1000; step++)
                {
                    float value = UIEasing.Evaluate(ease, step / 1000f);
                    leaves |= value < -0.0001f || value > 1.0001f;
                }
                Assert.That(UIEasing.Overshoots(ease), Is.EqualTo(leaves), ease.ToString());
            }
        }

        [Test]
        public void Fit_FollowsEveryEaseClosely()
        {
            const float duration = 0.4f;
            foreach (UIEase ease in Enum.GetValues(typeof(UIEase)))
            {
                AnimationCurve curve = UIAnimationCurveFitter.Fit(t => UIEasing.Evaluate(ease, t), duration);
                for (int step = 0; step <= 500; step++)
                {
                    float u = step / 500f;
                    Assert.That(curve.Evaluate(u * duration), Is.EqualTo(UIEasing.Evaluate(ease, u)).Within(0.002f), $"{ease} at {u}");
                }
                Assert.That(curve.length, Is.LessThanOrEqualTo(200), ease.ToString());
            }
        }

        [TestCase(UIEase.EaseInCubic, UIEase.EaseOutCubic)]
        [TestCase(UIEase.EaseOutBack, UIEase.EaseInBack)]
        [TestCase(UIEase.EaseInOutSine, UIEase.EaseInOutSine)]
        [TestCase(UIEase.Linear, UIEase.Linear)]
        [TestCase(UIEase.Custom, UIEase.Custom)]
        public void Mirror_SwapsInAndOut(UIEase ease, UIEase mirrored)
        {
            Assert.That(UIEasing.Mirror(ease), Is.EqualTo(mirrored));
        }

        [Test]
        public void Stepped_HoldsEachValueUntilTheNextStep()
        {
            AnimationCurve curve = UIAnimationCurveFitter.Stepped(t => t, 1f, 4);

            Assert.That(curve.length, Is.EqualTo(5));
            Assert.That(curve.Evaluate(0.3f), Is.EqualTo(0.25f).Within(Precision));
            Assert.That(curve.Evaluate(1f), Is.EqualTo(1f).Within(Precision));
        }

        [Test]
        public void Fit_FlatValueNeedsOnlyTwoKeys()
        {
            AnimationCurve curve = UIAnimationCurveFitter.Fit(t => 0.5f, 1f);

            Assert.That(curve.length, Is.EqualTo(2));
            Assert.That(curve.Evaluate(0.5f), Is.EqualTo(0.5f).Within(Precision));
        }

        [Test]
        public void CustomFit_PreservesWindowAnticipationAndSettle()
        {
            foreach (AnimationCurve motion in new[]
            {
                UIMotionCurves.Window(UIEase.EaseOutCubic, 0.17f, 0.43f),
                UIMotionCurves.Settle(1.07f, 0.73f, UIEase.EaseOutCubic, UIEase.EaseInOutSine),
                UIMotionCurves.Anticipate(0.08f, 0.14f, UIEase.EaseOutQuart)
            })
            {
                var source = new UIEaseCurve();
                source.SetCustom(motion);
                AnimationCurve baked = UIAnimationCurveFitter.Fit(source.Evaluate, 0.37f, source, false);
                for (int i = 0; i <= 1000; i++)
                    Assert.That(baked.Evaluate(i / 1000f * 0.37f), Is.EqualTo(source.Evaluate(i / 1000f)).Within(0.002f));
                AnimationCurve stepped = UIAnimationCurveFitter.Stepped(source.Evaluate, 0.37f, 12, source, false);
                foreach (Keyframe key in motion.keys)
                    if (key.inTangent * key.outTangent < 0f)
                        Assert.That(stepped.Evaluate(key.time * 0.37f), Is.EqualTo(key.value).Within(0.0001f));
            }
        }

        [Test]
        public void CustomFitAndStepped_KeepShortHardCutsAtTheirExactTimes()
        {
            var source = new UIEaseCurve();
            source.SetCustom(UIMotionCurves.Steps((0f, 0f), (0.013f, 1f), (0.017f, 0.2f), (0.023f, 1f), (1f, 1f)));
            foreach (AnimationCurve baked in new[]
            {
                UIAnimationCurveFitter.Fit(source.Evaluate, 0.4f, source, false),
                UIAnimationCurveFitter.Stepped(source.Evaluate, 0.4f, 8, source, false)
            })
            {
                foreach (float t in new[] { 0f, 0.012f, 0.014f, 0.016f, 0.018f, 0.022f, 0.024f, 0.5f, 1f })
                    Assert.That(baked.Evaluate(t * 0.4f), Is.EqualTo(source.Evaluate(t)).Within(Precision));
                Assert.That(baked.keys.Take(baked.length - 1).All(key => float.IsInfinity(key.outTangent)), Is.True);
            }
        }
    }
}
