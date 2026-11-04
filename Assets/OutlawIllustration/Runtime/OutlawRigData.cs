using System;

namespace OutlawIllustration
{
    [Serializable]
    public sealed class OutlawRigData
    {
        public int version;
        public int width;
        public int height;
        public float duration;
        public int fps;
        public OutlawMotionData motion;
        public OutlawLayerData[] layers;
        public OutlawBlinkData blink;

        public void Normalize()
        {
            if (width <= 0) width = 3840;
            if (height <= 0) height = 2176;
            if (duration <= 0) duration = 8f;
            if (layers == null) layers = new OutlawLayerData[0];
            if (motion == null) motion = OutlawMotionData.Defaults();
            if (blink == null) blink = new OutlawBlinkData();
            if (blink.duration <= 0) blink.duration = 0.16f;
            if (blink.at == null) blink.at = new float[] { 2.8f, 6.6f };
        }
    }

    [Serializable]
    public sealed class OutlawLayerData
    {
        public string id;
        public string name;
        public string path;
        public string role;
        public int z;
        public float[] rect;
        public float[] bounds;
        public float[] pivot;
        public bool disabled;
        public bool blink;
        public int meshCols;
        public int meshRows;
        public OutlawExtraMotion extra;

        public float[] SourceRect(int width, int height)
        {
            if (rect != null && rect.Length >= 4) return rect;
            if (bounds != null && bounds.Length >= 4) return bounds;
            return new float[] { 0, 0, width, height };
        }

        public bool IsBackground { get { return role == "background"; } }
        public bool IsBlink { get { return blink || role == "blink"; } }
    }

    [Serializable]
    public sealed class OutlawMotionData
    {
        public float swayX;
        public float headBobY;
        public float headLagX;
        public float rotationDeg;
        public float breathX;
        public float breathY;
        public float handSwayX;
        public float handSwayY;
        public float handRotationDeg;
        public float handPhase;
        public float[] handAnchor;
        public float armWeightStartX;
        public float armWeightEndX;

        public static OutlawMotionData Defaults()
        {
            return new OutlawMotionData { swayX = 10, headBobY = 10, headLagX = 8,
                rotationDeg = 1, breathX = 0.0035f, breathY = 0.006f,
                handSwayX = 12, handSwayY = 4, handRotationDeg = 0.4f, handPhase = -0.45f,
                handAnchor = new float[] { 1080, 700 }, armWeightStartX = 1120, armWeightEndX = 1680 };
        }
    }

    [Serializable]
    public sealed class OutlawExtraMotion
    {
        public float dx;
        public float dy;
        public float rotationDeg;
        public float phase;
        public int harmonic;
        public float weightStart;
        public float weightEnd;
        public string axis;
        public string axis2;
        public float weightStart2;
        public float weightEnd2;
        public bool invert2;
        public float[] pivot;
    }

    [Serializable]
    public sealed class OutlawBlinkData
    {
        public float[] at;
        public float duration;
    }
}
