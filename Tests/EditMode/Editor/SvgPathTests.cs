using System.Linq;
using CG.CombatFramework.Editor;
using NUnit.Framework;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public sealed class SvgPathTests
    {
        private const float k_Near = 1e-3f;
        private const float k_ArcNear = 0.05f;

        private static SvgShape Parse(string data)
        {
            Assert.IsTrue(SvgPath.TryParse(data, out var shape), $"couldn't parse \"{data}\"");
            return shape;
        }

        private static void Ops(SvgShape shape, params SvgOp[] expected)
        {
            CollectionAssert.AreEqual(expected, shape.Segments.Select(s => s.Op).ToArray());
        }

        private static void Near(Vector2 expected, Vector2 actual, float tolerance = k_Near)
        {
            Assert.Less(Vector2.Distance(expected, actual), tolerance, $"expected {expected}, got {actual}");
        }

        private static void Near(Rect expected, Rect actual, float tolerance = k_Near)
        {
            Near(expected.min, actual.min, tolerance);
            Near(expected.max, actual.max, tolerance);
        }

        [Test]
        public void MoveLineClose_Absolute()
        {
            var shape = Parse("M1 2L5 2L5 6Z");
            Ops(shape, SvgOp.Move, SvgOp.Line, SvgOp.Line, SvgOp.Close);
            Near(new Vector2(1f, 2f), shape.Segments[0].P1);
            Near(new Vector2(5f, 2f), shape.Segments[1].P1);
            Near(new Vector2(5f, 6f), shape.Segments[2].P1);
            Near(new Rect(1f, 2f, 4f, 4f), shape.Bounds);
        }

        [Test]
        public void MoveLineClose_Relative()
        {
            var shape = Parse("m1 2l4 0l0 4z");
            Ops(shape, SvgOp.Move, SvgOp.Line, SvgOp.Line, SvgOp.Close);
            Near(new Vector2(5f, 2f), shape.Segments[1].P1);
            Near(new Vector2(5f, 6f), shape.Segments[2].P1);
        }

        [Test]
        public void RepeatedPairsAfterAMove_AreLines()
        {
            var absolute = Parse("M0 0 10 0 10 10");
            Ops(absolute, SvgOp.Move, SvgOp.Line, SvgOp.Line);
            Near(new Vector2(10f, 10f), absolute.Segments[2].P1);

            var relative = Parse("m1 1 2 0 0 2");
            Ops(relative, SvgOp.Move, SvgOp.Line, SvgOp.Line);
            Near(new Vector2(3f, 1f), relative.Segments[1].P1);
            Near(new Vector2(3f, 3f), relative.Segments[2].P1);
        }

        [Test]
        public void RepeatedCommands_ReadEveryGroup()
        {
            var shape = Parse("M0 0L1 0 2 0 3 1C3 2 4 2 4 1 5 0 6 0 6 1");
            Ops(shape, SvgOp.Move, SvgOp.Line, SvgOp.Line, SvgOp.Line, SvgOp.Cubic, SvgOp.Cubic);
            Near(new Vector2(6f, 1f), shape.Segments[5].P3);
        }

        [Test]
        public void HorizontalAndVertical_AbsoluteAndRelative()
        {
            var shape = Parse("M0 0H10V5h-4v-5");
            Ops(shape, SvgOp.Move, SvgOp.Line, SvgOp.Line, SvgOp.Line, SvgOp.Line);
            Near(new Vector2(10f, 0f), shape.Segments[1].P1);
            Near(new Vector2(10f, 5f), shape.Segments[2].P1);
            Near(new Vector2(6f, 5f), shape.Segments[3].P1);
            Near(new Vector2(6f, 0f), shape.Segments[4].P1);
        }

        [Test]
        public void Cubic_AbsoluteAndRelative()
        {
            var absolute = Parse("M0 0C0 10 10 10 10 0");
            Ops(absolute, SvgOp.Move, SvgOp.Cubic);
            Near(new Vector2(0f, 10f), absolute.Segments[1].P1);
            Near(new Vector2(10f, 10f), absolute.Segments[1].P2);
            Near(new Vector2(10f, 0f), absolute.Segments[1].P3);
            Near(new Rect(0f, 0f, 10f, 7.5f), absolute.Bounds);

            var relative = Parse("M5 5c0 10 10 10 10 0");
            Near(new Vector2(5f, 15f), relative.Segments[1].P1);
            Near(new Vector2(15f, 15f), relative.Segments[1].P2);
            Near(new Vector2(15f, 5f), relative.Segments[1].P3);
        }

        [Test]
        public void SmoothCubic_ReflectsThePreviousControlPoint()
        {
            var absolute = Parse("M0 0C0 -10 10 -10 10 0S20 10 20 0");
            Near(new Vector2(10f, 10f), absolute.Segments[2].P1);
            Near(new Vector2(20f, 10f), absolute.Segments[2].P2);
            Near(new Vector2(20f, 0f), absolute.Segments[2].P3);

            var relative = Parse("M0 0c0 -10 10 -10 10 0s10 10 10 0");
            Near(new Vector2(10f, 10f), relative.Segments[2].P1);
            Near(new Vector2(20f, 10f), relative.Segments[2].P2);
            Near(new Vector2(20f, 0f), relative.Segments[2].P3);
        }

        [Test]
        public void SmoothCubic_WithoutAPreviousCubic_StartsAtTheCurrentPoint()
        {
            var shape = Parse("M2 3S10 10 10 0");
            Near(new Vector2(2f, 3f), shape.Segments[1].P1);
        }

        [Test]
        public void Quadratic_AndSmoothQuadratic()
        {
            var absolute = Parse("M0 0Q5 10 10 0T20 0");
            Ops(absolute, SvgOp.Move, SvgOp.Quad, SvgOp.Quad);
            Near(new Vector2(5f, 10f), absolute.Segments[1].P1);
            Near(new Vector2(15f, -10f), absolute.Segments[2].P1);
            Near(new Vector2(20f, 0f), absolute.Segments[2].P2);
            Near(new Rect(0f, -5f, 20f, 10f), absolute.Bounds);

            var relative = Parse("m0 0q5 10 10 0t10 0");
            Near(new Vector2(15f, -10f), relative.Segments[2].P1);
            Near(new Vector2(20f, 0f), relative.Segments[2].P2);
        }

        [Test]
        public void SmoothQuadratic_WithoutAPreviousQuadratic_IsAStraightCurve()
        {
            var shape = Parse("M2 3T10 3");
            Near(new Vector2(2f, 3f), shape.Segments[1].P1);
        }

        [Test]
        public void Arc_BecomesCurvesOnTheCircle()
        {
            var shape = Parse("M0 0A5 5 0 0 1 10 0");
            Assert.IsTrue(shape.Segments.Skip(1).All(s => s.Op == SvgOp.Cubic));
            Assert.AreEqual(3, shape.Segments.Length);
            Near(new Vector2(10f, 0f), shape.Segments[2].P3);
            Near(new Rect(0f, -5f, 10f, 5f), shape.Bounds, k_ArcNear);
        }

        [Test]
        public void Arc_SweepFlagPicksTheSide()
        {
            Near(new Rect(0f, 0f, 10f, 5f), Parse("M0 0A5 5 0 0 0 10 0").Bounds, k_ArcNear);
        }

        [Test]
        public void Arc_LargeFlagTakesTheLongWayRound()
        {
            var small = Parse("M0 0A5 5 0 0 1 5 5").Bounds;
            var large = Parse("M0 0A5 5 0 1 1 5 5").Bounds;
            Near(new Rect(0f, 0f, 5f, 5f), small, k_ArcNear);
            Near(new Rect(0f, -5f, 10f, 10f), large, k_ArcNear);
        }

        [Test]
        public void Arc_Relative_WithPackedFlags()
        {
            var shape = Parse("M0 0a5 5 0 0110 0");
            Near(new Vector2(10f, 0f), shape.Segments[shape.Segments.Length - 1].P3);
            Near(new Rect(0f, -5f, 10f, 5f), shape.Bounds, k_ArcNear);
        }

        [Test]
        public void Arc_RadiiTooSmall_AreScaledUp()
        {
            Near(new Rect(0f, -5f, 10f, 5f), Parse("M0 0A1 1 0 0 1 10 0").Bounds, k_ArcNear);
        }

        [Test]
        public void Arc_WithAZeroRadius_IsALine()
        {
            Ops(Parse("M0 0A0 5 0 0 1 10 0"), SvgOp.Move, SvgOp.Line);
        }

        [Test]
        public void Arc_Rotated()
        {
            var shape = Parse("M0 0A10 5 90 0 1 0 20");
            Near(new Vector2(0f, 20f), shape.Segments[shape.Segments.Length - 1].P3);
            Near(new Rect(0f, 0f, 5f, 20f), shape.Bounds, k_ArcNear);
        }

        [Test]
        public void CompactNumbers()
        {
            var shape = Parse("M1.5.5L-1-2");
            Near(new Vector2(1.5f, 0.5f), shape.Segments[0].P1);
            Near(new Vector2(-1f, -2f), shape.Segments[1].P1);

            var exponents = Parse("M1e1 2E-1L.5-.5 1e+1-0");
            Near(new Vector2(10f, 0.2f), exponents.Segments[0].P1);
            Near(new Vector2(0.5f, -0.5f), exponents.Segments[1].P1);
            Near(new Vector2(10f, 0f), exponents.Segments[2].P1);
        }

        [Test]
        public void CommasAndWhitespace()
        {
            var shape = Parse("  M 1,2 \n L 3 , 4\tz ");
            Ops(shape, SvgOp.Move, SvgOp.Line, SvgOp.Close);
            Near(new Vector2(3f, 4f), shape.Segments[1].P1);
        }

        [Test]
        public void AfterClose_TheNextSubpathStartsAtTheLastStart()
        {
            var shape = Parse("M2 2l10 0z l0 10");
            Ops(shape, SvgOp.Move, SvgOp.Line, SvgOp.Close, SvgOp.Move, SvgOp.Line);
            Near(new Vector2(2f, 2f), shape.Segments[3].P1);
            Near(new Vector2(2f, 12f), shape.Segments[4].P1);
        }

        [Test]
        public void SeveralSubpaths_ShareOneBounds()
        {
            var shape = Parse("M0 0h4v4h-4z M10 10h4v4h-4z");
            Near(new Rect(0f, 0f, 14f, 14f), shape.Bounds);
        }

        [Test]
        public void Fitting_KeepsProportionsAndCenters()
        {
            var shape = Parse("M0 0L20 0L20 10Z");
            var area = new Rect(0f, 0f, 10f, 10f);
            Near(new Vector2(0f, 2.5f), shape.Map(new Vector2(0f, 0f), area));
            Near(new Vector2(10f, 7.5f), shape.Map(new Vector2(20f, 10f), area));
        }

        [Test]
        public void Fitting_AnyViewBox()
        {
            var large = Parse("M0 0L512 512");
            var area = new Rect(2f, 2f, 12f, 12f);
            Near(new Vector2(2f, 2f), large.Map(Vector2.zero, area));
            Near(new Vector2(14f, 14f), large.Map(new Vector2(512f, 512f), area));

            var offset = Parse("M100 100h16v16h-16z");
            Near(new Vector2(2f, 2f), offset.Map(new Vector2(100f, 100f), area));
            Near(new Vector2(14f, 14f), offset.Map(new Vector2(116f, 116f), area));
        }

        [Test]
        public void Fitting_AFlatPath_UsesItsLength()
        {
            var shape = Parse("M0 5H10");
            var area = new Rect(0f, 0f, 20f, 20f);
            Near(new Vector2(0f, 10f), shape.Map(new Vector2(0f, 5f), area));
            Near(new Vector2(20f, 10f), shape.Map(new Vector2(10f, 5f), area));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("L1 1")]
        [TestCase("1 1")]
        [TestCase("M1")]
        [TestCase("M1 1 L2")]
        [TestCase("M1 1X2 2")]
        [TestCase("M1 1L2 2Z 3 3")]
        [TestCase("M1 1A1 1 0 2 1 2 2")]
        [TestCase("M1 1L2 e")]
        [TestCase("M0 0")]
        [TestCase("M0 0L0 0")]
        [TestCase("M0 0L1 1#")]
        public void BadInput_IsRefused(string data)
        {
            Assert.IsFalse(SvgPath.TryParse(data, out var shape));
            Assert.IsNull(shape);
        }
    }
}
