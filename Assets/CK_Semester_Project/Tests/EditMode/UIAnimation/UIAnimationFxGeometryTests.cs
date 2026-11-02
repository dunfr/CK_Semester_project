using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.UIAnimation.Tests
{
    public sealed class UIAnimationFxGeometryTests
    {
        private static UIAnimationFxShape Shape => new UIAnimationFxShape
        {
            Bounds = new Rect(0f, 0f, 100f, 100f), WipeAxis = Vector2.right, SheenAxis = Vector2.right,
            Bands = 8, CellSize = 16f, SheenWidth = 0.2f, SheenIntensity = 1f
        };

        private static UIVertex Vertex(float x, float y)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = new Vector3(x, y);
            vertex.uv0 = new Vector4(x / 100f, y / 100f, 0.25f, 0.75f);
            vertex.uv1 = vertex.uv2 = vertex.uv3 = vertex.uv0;
            return vertex;
        }

        private static List<UIVertex> Quad() => new List<UIVertex>
        {
            Vertex(0f, 0f), Vertex(0f, 100f), Vertex(100f, 100f),
            Vertex(100f, 100f), Vertex(100f, 0f), Vertex(0f, 0f)
        };

        private static float Area(List<UIVertex> stream)
        {
            float area = 0f;
            for (int i = 0; i < stream.Count; i += 3)
                area += Vector3.Cross(stream[i + 1].position - stream[i].position, stream[i + 2].position - stream[i].position).magnitude * 0.5f;
            return area;
        }

        [Test]
        public void Cut_ConservesAreaAndInterpolatesAllVertexChannels()
        {
            UIVertex a = Vertex(0f, 0f), b = Vertex(100f, 0f), c = Vertex(0f, 100f);
            a.color = new Color32(0, 20, 40, 60); b.color = new Color32(200, 220, 240, 160);
            b.normal = Vector3.up; b.tangent = Vector4.one;
            UIVertex midpoint = UIAnimationFxGeometry.Interpolate(a, b, 0.5f);
            Assert.That(midpoint.position, Is.EqualTo(new Vector3(50f, 0f)));
            Assert.That(midpoint.color.r, Is.EqualTo(100));
            Assert.That(midpoint.color.a, Is.EqualTo(110));
            Assert.That(midpoint.uv0, Is.EqualTo((a.uv0 + b.uv0) * 0.5f));
            Assert.That(midpoint.uv1, Is.EqualTo((a.uv1 + b.uv1) * 0.5f));
            Assert.That(midpoint.uv2, Is.EqualTo((a.uv2 + b.uv2) * 0.5f));
            Assert.That(midpoint.uv3, Is.EqualTo((a.uv3 + b.uv3) * 0.5f));
            Assert.That(midpoint.normal, Is.EqualTo((a.normal + b.normal) * 0.5f));
            Assert.That(midpoint.tangent, Is.EqualTo((a.tangent + b.tangent) * 0.5f));
            var output = new List<UIVertex>();
            UIAnimationFxGeometry.Cut(new List<UIVertex> { a, b, c }, output, Vector2.right, 50f);
            Assert.That(Area(output), Is.EqualTo(5000f).Within(0.01f));
            Assert.That(output.Any(v => v.position == midpoint.position && v.color.Equals(midpoint.color) && v.uv0 == midpoint.uv0), Is.True);
        }

        [Test]
        public void Skew_UsesDegreesAndRootCenter()
        {
            var values = new UIAnimationFxValues { Wipe = 1f, Skew = 45f };
            Assert.That(UIAnimationFxGeometry.Move(new Vector3(10f, 75f), 75f, values, Shape).x, Is.EqualTo(35f).Within(0.0001f));
            values.Skew = -45f;
            Assert.That(UIAnimationFxGeometry.Move(new Vector3(10f, 75f), 75f, values, Shape).x, Is.EqualTo(-15f).Within(0.0001f));
        }

        [Test]
        public void Rest_LeavesStreamAndVertexHelperUntouched()
        {
            List<UIVertex> stream = Quad();
            UIVertex[] original = stream.ToArray();
            UIAnimationFxGeometry.Apply(stream, new UIAnimationFxValues { Wipe = 1f }, Shape, true, new UIAnimationFxGeometry.Workspace());
            CollectionAssert.AreEqual(original, stream);
            var target = new GameObject("FX", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UIAnimationFx));
            try
            {
                UIAnimationFxMesh mesh = target.AddComponent<UIAnimationFxMesh>();
                mesh.Bind(target.GetComponent<UIAnimationFx>());
                using (var vh = new VertexHelper())
                {
                    var quad = new[] { Vertex(0f, 0f), Vertex(0f, 100f), Vertex(100f, 100f), Vertex(100f, 0f) };
                    // AddUIVertexQuad는 UV2와 UV3를 버리므로 모든 채널을 넣어 무변경을 확인한다.
                    foreach (UIVertex item in quad) vh.AddVert(item);
                    vh.AddTriangle(0, 1, 2);
                    vh.AddTriangle(2, 3, 0);
                    mesh.ModifyMesh(vh);
                    Assert.That(vh.currentVertCount, Is.EqualTo(4));
                    Assert.That(vh.currentIndexCount, Is.EqualTo(6));
                    for (int i = 0; i < quad.Length; i++)
                    {
                        UIVertex vertex = default;
                        vh.PopulateUIVertex(ref vertex, i);
                        Assert.That(vertex, Is.EqualTo(quad[i]), "정점 " + i);
                    }
                    var triangles = new List<UIVertex>();
                    vh.GetUIVertexStream(triangles);
                    CollectionAssert.AreEqual(original, triangles);
                }
            }
            finally { Object.DestroyImmediate(target); }
        }

        [Test]
        public void Wipe_SoftEdgeHasInterpolatedAlphaAndHardEdgeDiscardsOutside()
        {
            var values = new UIAnimationFxValues { Wipe = 0.5f };
            UIAnimationFxShape shape = Shape;
            shape.WipeSoftness = 0.4f;
            List<UIVertex> stream = Quad();
            UIAnimationFxGeometry.Apply(stream, values, shape, false, new UIAnimationFxGeometry.Workspace());
            UIVertex solid = stream.First(v => Mathf.Abs(v.position.x - 30f) < 0.001f && v.position.y == 0f);
            UIVertex clear = stream.First(v => Mathf.Abs(v.position.x - 70f) < 0.001f && v.position.y == 0f);
            Assert.That(solid.color.a, Is.GreaterThanOrEqualTo(254));
            Assert.That(clear.color.a, Is.LessThanOrEqualTo(1));
            Assert.That(UIAnimationFxGeometry.Interpolate(solid, clear, 0.5f).color.a, Is.InRange(126, 129));
            shape.WipeSoftness = 0f;
            stream = Quad();
            UIAnimationFxGeometry.Apply(stream, values, shape, false, new UIAnimationFxGeometry.Workspace());
            Assert.That(Area(stream), Is.EqualTo(5000f).Within(0.01f));
            Assert.That(stream.All(v => v.position.x <= 50.001f && v.color.a == 255), Is.True);
        }

        [Test]
        public void Wipe_HardSlantedEdgeStaysOpaqueAtEveryProgress()
        {
            UIAnimationFxShape shape = Shape;
            shape.WipeAxis = UIAnimationFxGeometry.Axis(31f);
            for (float wipe = 0.05f; wipe < 1f; wipe += 0.07f)
            {
                List<UIVertex> stream = Quad();
                UIAnimationFxGeometry.Apply(stream, new UIAnimationFxValues { Wipe = wipe }, shape, false, new UIAnimationFxGeometry.Workspace());
                Assert.That(stream, Is.Not.Empty);
                Assert.That(stream.All(v => v.color.a == 255), Is.True, $"wipe {wipe}");
            }
        }

        [Test]
        public void Slice_IsDeterministicAndStaysWithinTwelvePercent()
        {
            var values = new UIAnimationFxValues { Wipe = 1f, Slice = 1f, Seed = 7f };
            List<UIVertex> a = Quad(), b = Quad(), c = Quad();
            UIAnimationFxGeometry.Apply(a, values, Shape, false, new UIAnimationFxGeometry.Workspace());
            UIAnimationFxGeometry.Apply(b, values, Shape, false, new UIAnimationFxGeometry.Workspace());
            CollectionAssert.AreEqual(a, b);
            values.Seed++;
            UIAnimationFxGeometry.Apply(c, values, Shape, false, new UIAnimationFxGeometry.Workspace());
            Assert.That(a.SequenceEqual(c), Is.False);
            Assert.That(a.All(v => v.position.x >= -12.001f && v.position.x <= 112.001f), Is.True);
            for (int i = 1; i < Shape.Bands; i++)
                Assert.That(UIAnimationFxGeometry.SliceBoundary(i, Shape, 7f), Is.GreaterThan(UIAnimationFxGeometry.SliceBoundary(i - 1, Shape, 7f)));
        }

        [Test]
        public void Split_WritesRedThenCyanThenAllOriginals()
        {
            List<UIVertex> stream = Quad();
            UIAnimationFxGeometry.Apply(stream, new UIAnimationFxValues { Wipe = 1f, Split = 1f }, Shape, false, new UIAnimationFxGeometry.Workspace());
            Assert.That(stream.Count, Is.EqualTo(18));
            for (int i = 0; i < 6; i++)
            {
                Assert.That(stream[i].color.g, Is.Zero);
                Assert.That(stream[i].color.b, Is.Zero);
                Assert.That(stream[i + 6].color.r, Is.Zero);
                Assert.That(stream[i].color.a, Is.InRange(190, 192));
                Assert.That(stream[i + 12].color, Is.EqualTo(new Color32(255, 255, 255, 255)));
                Assert.That(stream[i].position.x, Is.EqualTo(stream[i + 12].position.x - 24f));
                Assert.That(stream[i + 6].position.x, Is.EqualTo(stream[i + 12].position.x + 24f));
            }
        }

        [Test]
        public void DissolveAndScan_BoundSubdivisionAndDisappearCompletely()
        {
            UIAnimationFxShape shape = Shape;
            shape.Bounds = new Rect(0f, 0f, 800f, 800f); shape.CellSize = 1f;
            Assert.That(UIAnimationFxGeometry.CellCount(800f, 1f), Is.EqualTo(40));
            Assert.That(UIAnimationFxGeometry.CellCount(10f, 16f), Is.EqualTo(1));
            Assert.That(UIAnimationFxGeometry.ScanCount(shape.Bounds), Is.EqualTo(48));
            List<UIVertex> stream = Quad();
            var values = new UIAnimationFxValues { Wipe = 1f, Dissolve = 0.4f, Scan = 1f, Split = 1f, Slice = 1f };
            for (int i = 0; i < stream.Count; i++)
            {
                UIVertex vertex = stream[i]; vertex.position *= 8f; stream[i] = vertex;
            }
            UIAnimationFxGeometry.Apply(stream, values, shape, false, new UIAnimationFxGeometry.Workspace());
            Assert.That(stream.Count, Is.LessThanOrEqualTo(63000));
            Assert.That(stream.Count, Is.GreaterThan(0));
            Assert.That(stream.Any(v => v.color.a < 255), Is.True);
            values.Dissolve = 1f;
            UIAnimationFxGeometry.Apply(stream, values, shape, false, new UIAnimationFxGeometry.Workspace());
            Assert.That(stream, Is.Empty);
        }

        [Test]
        public void SheenAndFlash_NormalizeVerticesForOnlyTheTargetMaterial()
        {
            var values = new UIAnimationFxValues { Wipe = 1f, Sheen = 0.5f, Flash = 0.5f };
            List<UIVertex> stream = Quad();
            UIAnimationFxGeometry.Apply(stream, values, Shape, true, new UIAnimationFxGeometry.Workspace());
            Assert.That(UIAnimationFxGeometry.Brightness(values, Shape), Is.EqualTo(2.5f));
            Assert.That(stream.First(v => Mathf.Abs(v.position.x - 50f) < 0.001f).color.r, Is.EqualTo(255));
            Assert.That(stream.First(v => v.position.x == 0f).color.r, Is.InRange(152, 154));
            stream = Quad();
            UIVertex[] original = stream.ToArray();
            UIAnimationFxGeometry.Apply(stream, values, Shape, false, new UIAnimationFxGeometry.Workspace());
            CollectionAssert.AreEqual(original, stream);
        }
    }
}
