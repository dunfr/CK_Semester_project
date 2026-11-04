using System.Collections.Generic;
using UnityEngine;
using UIVertex = UnityEngine.UIVertex;

namespace CK.SemesterProject.UIAnimation
{
    public struct UIAnimationFxValues
    {
        public float Skew, Wipe, Slice, Split, Scan, Dissolve, Sheen, Flash, Seed;
        public bool IsRest => Skew == 0f && Wipe >= 1f && Slice == 0f && Split == 0f
            && Scan == 0f && Dissolve == 0f && (Sheen <= 0f || Sheen >= 1f) && Flash == 0f;

        public bool SameGeometry(UIAnimationFxValues other) => Skew == other.Skew && Wipe == other.Wipe
            && Slice == other.Slice && Split == other.Split && Scan == other.Scan && Dissolve == other.Dissolve
            && Sheen == other.Sheen && Flash == other.Flash && (Slice == 0f || Seed == other.Seed);

        public bool SameText(UIAnimationFxValues other) => Skew == other.Skew && Wipe == other.Wipe
            && Slice == other.Slice && Scan == other.Scan && Dissolve == other.Dissolve
            && (Slice == 0f || Seed == other.Seed);
    }

    public struct UIAnimationFxShape
    {
        public Rect Bounds;
        public Vector2 WipeAxis, SheenAxis;
        public float WipeSoftness, CellSize, SheenWidth, SheenIntensity;
        public int Bands;
    }

    // 씬이나 시간을 읽지 않는다. 입력과 작업용 목록만으로 같은 결과를 만든다.
    public static class UIAnimationFxGeometry
    {
        private const int VertexBudget = 21000;

        public sealed class Workspace
        {
            internal List<UIVertex> A = new List<UIVertex>(128);
            internal List<UIVertex> B = new List<UIVertex>(128);
        }

        public static Vector2 Axis(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float x = Mathf.Cos(radians), y = Mathf.Sin(radians);
            return new Vector2(Mathf.Abs(x) < 0.000001f ? 0f : x, Mathf.Abs(y) < 0.000001f ? 0f : y);
        }

        public static float Hash(int x, int y = 0, int seed = 0)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u + 3266489917u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0x00ffffffu) / 16777216f;
            }
        }

        public static UIVertex Interpolate(UIVertex a, UIVertex b, float t)
        {
            a.position = Vector3.LerpUnclamped(a.position, b.position, t);
            a.color = Color.LerpUnclamped(a.color, b.color, t);
            a.uv0 = Vector4.LerpUnclamped(a.uv0, b.uv0, t);
            a.uv1 = Vector4.LerpUnclamped(a.uv1, b.uv1, t);
            a.uv2 = Vector4.LerpUnclamped(a.uv2, b.uv2, t);
            a.uv3 = Vector4.LerpUnclamped(a.uv3, b.uv3, t);
            a.normal = Vector3.LerpUnclamped(a.normal, b.normal, t);
            a.tangent = Vector4.LerpUnclamped(a.tangent, b.tangent, t);
            return a;
        }

        private struct Polygon
        {
            private UIVertex _a, _b, _c, _d;
            private int _count;
            public void Add(UIVertex v)
            {
                switch (_count++) { case 0: _a = v; break; case 1: _b = v; break; case 2: _c = v; break; default: _d = v; break; }
            }
            public void Write(List<UIVertex> output)
            {
                if (_count < 3) return;
                Triangle(output, _a, _b, _c);
                if (_count == 4) Triangle(output, _a, _c, _d);
            }
        }

        private static void Triangle(List<UIVertex> output, UIVertex a, UIVertex b, UIVertex c)
        {
            Vector3 ab = b.position - a.position, ac = c.position - a.position;
            if (Vector3.Cross(ab, ac).sqrMagnitude < 0.0000000001f) return;
            output.Add(a); output.Add(b); output.Add(c);
        }

        private static void Edge(ref Polygon polygon, UIVertex a, UIVertex b, Vector2 normal, float distance, bool positive)
        {
            float da = Vector2.Dot(normal, a.position) - distance;
            float db = Vector2.Dot(normal, b.position) - distance;
            bool insideA = positive ? da >= 0f : da <= 0f;
            bool insideB = positive ? db >= 0f : db <= 0f;
            if (insideA != insideB) polygon.Add(Interpolate(a, b, da / (da - db)));
            if (insideB) polygon.Add(b);
        }

        public static void ClipTriangle(UIVertex a, UIVertex b, UIVertex c, Vector2 normal, float distance,
            bool positive, List<UIVertex> output)
        {
            var polygon = new Polygon();
            Edge(ref polygon, c, a, normal, distance, positive);
            Edge(ref polygon, a, b, normal, distance, positive);
            Edge(ref polygon, b, c, normal, distance, positive);
            polygon.Write(output);
        }

        // 선에 닿기만 하는 삼각형은 그대로 둬 같은 면이 두 번 생기지 않게 한다.
        public static void Cut(List<UIVertex> source, List<UIVertex> output, Vector2 normal, float distance)
        {
            output.Clear();
            for (int i = 0; i + 2 < source.Count; i += 3)
            {
                UIVertex a = source[i], b = source[i + 1], c = source[i + 2];
                float da = Vector2.Dot(normal, a.position) - distance;
                float db = Vector2.Dot(normal, b.position) - distance;
                float dc = Vector2.Dot(normal, c.position) - distance;
                if (Mathf.Min(da, Mathf.Min(db, dc)) >= -0.00001f || Mathf.Max(da, Mathf.Max(db, dc)) <= 0.00001f)
                { output.Add(a); output.Add(b); output.Add(c); continue; }
                ClipTriangle(a, b, c, normal, distance, false, output);
                ClipTriangle(a, b, c, normal, distance, true, output);
            }
        }

        private static void Cut(ref List<UIVertex> current, ref List<UIVertex> spare, Vector2 axis, float distance)
        {
            Cut(current, spare, axis, distance);
            // uGUI의 16비트 정점 한도 안에서 복제본까지 그린다. 복합 효과가 극단적으로 촘촘할 때만 추가 절단을 생략한다.
            if (spare.Count > VertexBudget) return;
            List<UIVertex> swap = current; current = spare; spare = swap;
        }

        public static int CellCount(float extent, float cellSize) => Mathf.Clamp(Mathf.CeilToInt(extent / Mathf.Max(1f, cellSize)), 1, 40);
        public static int ScanCount(Rect bounds) => Mathf.Clamp(Mathf.CeilToInt(bounds.height / 4f), 1, 48);

        public static float SliceBoundary(int index, UIAnimationFxShape shape, float seed)
        {
            int bands = Mathf.Clamp(shape.Bands, 1, 48);
            if (index <= 0) return shape.Bounds.yMin;
            if (index >= bands) return shape.Bounds.yMax;
            return shape.Bounds.yMin + shape.Bounds.height * (index + (Hash(index, 17, Mathf.FloorToInt(seed)) - 0.5f) * 0.6f) / bands;
        }

        public static int SliceBand(float y, UIAnimationFxShape shape, float seed)
        {
            int bands = Mathf.Clamp(shape.Bands, 1, 48);
            for (int i = 1; i < bands; i++) if (y < SliceBoundary(i, shape, seed)) return i - 1;
            return bands - 1;
        }

        public static float SliceOffset(int band, UIAnimationFxValues values, UIAnimationFxShape shape)
            => (Hash(band, 71, Mathf.FloorToInt(values.Seed)) * 2f - 1f) * shape.Bounds.width * 0.12f * Mathf.Clamp01(values.Slice);

        public static Vector3 Move(Vector3 position, float bandY, UIAnimationFxValues values, UIAnimationFxShape shape)
        {
            position.x += Mathf.Tan(Mathf.Clamp(values.Skew, -45f, 45f) * Mathf.Deg2Rad) * (position.y - shape.Bounds.center.y);
            if (values.Slice != 0f) position.x += SliceOffset(SliceBand(bandY, shape, values.Seed), values, shape);
            return position;
        }

        private static void Projection(Rect bounds, Vector2 axis, out float min, out float extent)
        {
            float center = Vector2.Dot(bounds.center, axis);
            extent = Mathf.Max(0.0001f, Mathf.Abs(axis.x) * bounds.width + Mathf.Abs(axis.y) * bounds.height);
            min = center - extent * 0.5f;
        }

        private static float Coordinate(Vector2 position, Rect bounds, Vector2 axis)
        {
            Projection(bounds, axis, out float min, out float extent);
            return (Vector2.Dot(position, axis) - min) / extent;
        }

        public static float WipeAlpha(Vector2 position, UIAnimationFxValues values, UIAnimationFxShape shape)
        {
            if (values.Wipe >= 1f) return 1f;
            if (values.Wipe <= 0f) return 0f;
            float q = Coordinate(position, shape.Bounds, shape.WipeAxis);
            float width = Mathf.Clamp01(shape.WipeSoftness);
            if (width == 0f) return q <= values.Wipe ? 1f : 0f;
            return Mathf.Clamp01((values.Wipe * (1f + width) - q) / width);
        }

        public static float DissolveAlpha(Vector2 position, UIAnimationFxValues values, UIAnimationFxShape shape)
        {
            if (values.Dissolve <= 0f) return 1f;
            if (values.Dissolve >= 1f) return 0f;
            int nx = CellCount(shape.Bounds.width, shape.CellSize), ny = CellCount(shape.Bounds.height, shape.CellSize);
            int x = Mathf.Clamp(Mathf.FloorToInt((position.x - shape.Bounds.xMin) / Mathf.Max(0.0001f, shape.Bounds.width) * nx), 0, nx - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((position.y - shape.Bounds.yMin) / Mathf.Max(0.0001f, shape.Bounds.height) * ny), 0, ny - 1);
            return Mathf.Clamp01((Hash(x, y) - values.Dissolve * 1.08f + 0.08f) / 0.08f);
        }

        public static float ScanAlpha(float y, UIAnimationFxValues values, UIAnimationFxShape shape)
        {
            int row = Mathf.FloorToInt((y - shape.Bounds.yMin) / Mathf.Max(0.0001f, shape.Bounds.height) * ScanCount(shape.Bounds));
            return (row & 1) == 0 ? 1f : 1f - Mathf.Clamp01(values.Scan) * 0.85f;
        }

        public static float TextAlpha(Vector2 center, UIAnimationFxValues values, UIAnimationFxShape shape)
            => WipeAlpha(center, values, shape) * DissolveAlpha(center, values, shape) * ScanAlpha(center.y, values, shape);

        private static float SheenLight(Vector2 position, UIAnimationFxValues values, UIAnimationFxShape shape)
        {
            if (values.Sheen <= 0f || values.Sheen >= 1f) return 0f;
            float width = Mathf.Clamp(shape.SheenWidth, 0.01f, 1f);
            float center = Mathf.Lerp(-width, 1f + width, values.Sheen);
            return Mathf.Max(0f, 1f - Mathf.Abs(Coordinate(position, shape.Bounds, shape.SheenAxis) - center) / width);
        }

        public static float Brightness(UIAnimationFxValues values, UIAnimationFxShape shape)
            => 1f + Mathf.Clamp01(values.Flash) + (values.Sheen > 0f && values.Sheen < 1f ? Mathf.Max(0f, shape.SheenIntensity) : 0f);

        private static void CutProjection(ref List<UIVertex> current, ref List<UIVertex> spare, Rect bounds, Vector2 axis, float normalized)
        {
            Projection(bounds, axis, out float min, out float extent);
            Cut(ref current, ref spare, axis, min + extent * normalized);
        }

        public static void Apply(List<UIVertex> stream, UIAnimationFxValues values, UIAnimationFxShape shape, bool targetImage, Workspace workspace)
        {
            if (values.IsRest || stream.Count == 0) return;
            List<UIVertex> current = workspace.A, spare = workspace.B;
            current.Clear(); current.AddRange(stream);
            if (values.Wipe <= 0f || values.Dissolve >= 1f) { stream.Clear(); return; }

            if (values.Dissolve > 0f)
            {
                int nx = CellCount(shape.Bounds.width, shape.CellSize), ny = CellCount(shape.Bounds.height, shape.CellSize);
                for (int x = 1; x < nx; x++) Cut(ref current, ref spare, Vector2.right, shape.Bounds.xMin + shape.Bounds.width * x / nx);
                for (int y = 1; y < ny; y++) Cut(ref current, ref spare, Vector2.up, shape.Bounds.yMin + shape.Bounds.height * y / ny);
            }
            if (values.Scan > 0f)
            {
                int rows = ScanCount(shape.Bounds);
                for (int y = 1; y < rows; y++) Cut(ref current, ref spare, Vector2.up, shape.Bounds.yMin + shape.Bounds.height * y / rows);
            }
            if (values.Slice > 0f)
                for (int y = 1; y < Mathf.Clamp(shape.Bands, 1, 48); y++) Cut(ref current, ref spare, Vector2.up, SliceBoundary(y, shape, values.Seed));
            if (values.Wipe < 1f)
            {
                float end = values.Wipe * (1f + shape.WipeSoftness);
                CutProjection(ref current, ref spare, shape.Bounds, shape.WipeAxis, end);
                if (shape.WipeSoftness > 0f) CutProjection(ref current, ref spare, shape.Bounds, shape.WipeAxis, end - shape.WipeSoftness);
            }
            if (targetImage && values.Sheen > 0f && values.Sheen < 1f && shape.SheenIntensity > 0f)
            {
                float width = Mathf.Clamp(shape.SheenWidth, 0.01f, 1f), center = Mathf.Lerp(-width, 1f + width, values.Sheen);
                CutProjection(ref current, ref spare, shape.Bounds, shape.SheenAxis, center - width);
                CutProjection(ref current, ref spare, shape.Bounds, shape.SheenAxis, center);
                CutProjection(ref current, ref spare, shape.Bounds, shape.SheenAxis, center + width);
            }

            float multiplier = targetImage ? Brightness(values, shape) : 1f;
            stream.Clear();
            // 분리 복제본은 모든 원본보다 먼저 넣어 원본 아래에 그려진다.
            if (values.Split > 0f && current.Count <= VertexBudget)
            {
                Write(current, stream, values, shape, targetImage, multiplier, -24f * Mathf.Clamp01(values.Split), 1);
                Write(current, stream, values, shape, targetImage, multiplier, 24f * Mathf.Clamp01(values.Split), 2);
            }
            Write(current, stream, values, shape, targetImage, multiplier, 0f, 0);
        }

        private static void Write(List<UIVertex> source, List<UIVertex> output, UIAnimationFxValues values,
            UIAnimationFxShape shape, bool targetImage, float multiplier, float shift, int tint)
        {
            for (int i = 0; i + 2 < source.Count; i += 3)
            {
                Vector3 center = (source[i].position + source[i + 1].position + source[i + 2].position) / 3f;
                float alpha = DissolveAlpha(center, values, shape) * ScanAlpha(center.y, values, shape);
                if (alpha <= 0f || WipeAlpha(center, values, shape) <= 0f && shape.WipeSoftness == 0f) continue;
                for (int k = 0; k < 3; k++)
                {
                    UIVertex v = source[i + k];
                    Color color = v.color;
                    // 선명한 경계는 위에서 삼각형째 걸렀다. 잘린 정점은 오차로 선 밖에 놓일 수 있어 다시 재지 않는다.
                    color.a *= shape.WipeSoftness > 0f ? alpha * WipeAlpha(v.position, values, shape) : alpha;
                    if (targetImage)
                    {
                        float light = (1f + Mathf.Clamp01(values.Flash) + SheenLight(v.position, values, shape) * shape.SheenIntensity) / multiplier;
                        color.r *= light; color.g *= light; color.b *= light;
                    }
                    if (tint == 1) { color.g = color.b = 0f; color.a *= 0.75f; }
                    if (tint == 2) { color.r = 0f; color.a *= 0.75f; }
                    v.color = color;
                    v.position = Move(v.position, center.y, values, shape) + Vector3.right * shift;
                    output.Add(v);
                }
            }
        }
    }
}
