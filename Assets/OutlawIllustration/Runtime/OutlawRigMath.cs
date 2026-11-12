using System;

namespace OutlawIllustration
{
    // Source-image coordinates: +X right, +Y down. No Unity API dependency.
    public static class OutlawRigMath
    {
        public struct Point
        {
            public float x;
            public float y;
            public Point(float xValue, float yValue) { x = xValue; y = yValue; }
        }

        private struct DoublePoint
        {
            public double x;
            public double y;
            public DoublePoint(double xValue, double yValue) { x = xValue; y = yValue; }
        }

        public static double SmoothStep(double a, double b, double value)
        {
            if (Math.Abs(b - a) < 0.000001) return value >= b ? 1 : 0;
            double t = Math.Max(0, Math.Min(1, (value - a) / (b - a)));
            return t * t * (3 - 2 * t);
        }

        public static bool BlinkClosed(float time, float duration, OutlawBlinkData blink)
        {
            if (blink == null || blink.at == null || duration <= 0) return false;
            float wrapped = time % duration;
            if (wrapped < 0) wrapped += duration;
            float half = Math.Max(0.01f, blink.duration) * 0.5f;
            for (int i = 0; i < blink.at.Length; i++)
            {
                float centre = blink.at[i] % duration;
                if (centre < 0) centre += duration;
                float distance = Math.Abs(wrapped - centre);
                distance = Math.Min(distance, duration - distance);
                if (distance <= half) return true;
            }
            return false;
        }

        public static Point Evaluate(float x, float y, float time, OutlawRigData rig,
                                     OutlawLayerData layer, float intensity)
        {
            if (layer.IsBackground || intensity <= 0) return new Point(x, y);
            double seconds = time % rig.duration;
            if (seconds < 0) seconds += rig.duration;
            double u = Math.PI * 2 * seconds / rig.duration;
            OutlawMotionData motion = rig.motion ?? OutlawMotionData.Defaults();
            DoublePoint output;
            if (layer.role == "weapon") output = WeaponRaw(x, y, u, motion);
            else
            {
                output = StandardRaw(x, y, u, rig, layer, motion);
                if (layer.role == "left_arm")
                {
                    double weight = 1 - SmoothStep(motion.armWeightStartX, motion.armWeightEndX, x);
                    DoublePoint weapon = WeaponRaw(x, y, u, motion);
                    output.x = (1 - weight) * output.x + weight * weapon.x;
                    output.y = (1 - weight) * output.y + weight * weapon.y;
                }
            }
            return new Point((float)(x + intensity * (output.x - x)),
                             (float)(y + intensity * (output.y - y)));
        }

        private static DoublePoint StandardRaw(double originalX, double originalY, double u,
                                               OutlawRigData rig, OutlawLayerData layer, OutlawMotionData motion)
        {
            double px = originalX, py = originalY;
            OutlawExtraMotion extra = layer.extra;
            if (extra != null)
            {
                double coordinate = extra.axis == "x" ? originalX : originalY;
                double weight = SmoothStep(extra.weightStart, extra.weightEnd, coordinate);
                if (!String.IsNullOrEmpty(extra.axis2))
                {
                    double coordinate2 = extra.axis2 == "x" ? originalX : originalY;
                    double weight2 = SmoothStep(extra.weightStart2, extra.weightEnd2, coordinate2);
                    weight *= extra.invert2 ? 1 - weight2 : weight2;
                }
                int harmonic = extra.harmonic == 0 ? 1 : Math.Abs(extra.harmonic);
                double wave = Math.Sin(harmonic * u + extra.phase);
                float[] pivot = extra.pivot != null && extra.pivot.Length >= 2 ? extra.pivot : layer.pivot;
                double pivotX = pivot != null && pivot.Length >= 2 ? pivot[0] : 1700;
                double pivotY = pivot != null && pivot.Length >= 2 ? pivot[1] : 1700;
                double angle = extra.rotationDeg * Math.PI / 180 * wave;
                double c = Math.Cos(angle), s = Math.Sin(angle);
                double localX = px - pivotX, localY = py - pivotY;
                double qx = pivotX + c * localX - s * localY + extra.dx * wave;
                double qy = pivotY + s * localX + c * localY + extra.dy * wave;
                px += weight * (qx - px);
                py += weight * (qy - py);
            }
            DoublePoint offset = LocalOffset(px, py, u, motion);
            DoublePoint rotated = RotateRoot(px + offset.x, py + offset.y, u, motion);
            double edge = SmoothStep(0, 180, originalX) * SmoothStep(0, 140, originalY)
                          * SmoothStep(0, 160, rig.width - originalX)
                          * SmoothStep(0, 220, rig.height - originalY);
            return new DoublePoint(originalX + edge * (rotated.x - originalX),
                                   originalY + edge * (rotated.y - originalY));
        }

        private static DoublePoint LocalOffset(double x, double y, double u, OutlawMotionData motion)
        {
            double head = 1 - SmoothStep(650, 1350, y);
            double tx = (x - 1900) / 950, ty = (y - 1100) / 850;
            double torso = Math.Exp(-tx * tx - ty * ty);
            double breath = Math.Sin(2 * u - 0.25);
            return new DoublePoint(motion.swayX * Math.Sin(u)
                      + head * motion.headLagX * (Math.Sin(u - 0.35) - Math.Sin(u))
                      + (x - 1900) * motion.breathX * torso * breath,
                      -motion.headBobY * head * Math.Sin(u - 0.2)
                      + (y - 1650) * motion.breathY * torso * breath);
        }

        private static DoublePoint RotateRoot(double x, double y, double u, OutlawMotionData motion)
        {
            double angle = motion.rotationDeg * Math.PI / 180 * Math.Sin(u);
            double c = Math.Cos(angle), s = Math.Sin(angle);
            double px = x - 1700, py = y - 1700;
            return new DoublePoint(1700 + c * px - s * py, 1700 + s * px + c * py);
        }

        private static DoublePoint WeaponRaw(double x, double y, double u, OutlawMotionData motion)
        {
            double hx = motion.handAnchor != null && motion.handAnchor.Length >= 2 ? motion.handAnchor[0] : 1080;
            double hy = motion.handAnchor != null && motion.handAnchor.Length >= 2 ? motion.handAnchor[1] : 700;
            double wave = Math.Sin(u + motion.handPhase);
            double angle = motion.handRotationDeg * Math.PI / 180 * wave;
            double c = Math.Cos(angle), s = Math.Sin(angle);
            double xLocal = x - hx, yLocal = y - hy;
            double handX = hx + c * xLocal - s * yLocal + motion.handSwayX * wave;
            double handY = hy + s * xLocal + c * yLocal + motion.handSwayY * Math.Sin(u + motion.handPhase + 0.4);
            DoublePoint anchorOffset = LocalOffset(hx, hy, u, motion);
            // One affine field keeps the blade straight; no per-vertex edge pin.
            return RotateRoot(handX + anchorOffset.x, handY + anchorOffset.y, u, motion);
        }
    }
}
