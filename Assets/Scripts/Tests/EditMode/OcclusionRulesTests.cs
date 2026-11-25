using CodenameVelvet.Map;
using NUnit.Framework;
using UnityEngine;

namespace CodenameVelvet.Tests
{
    public class OcclusionRulesTests
    {
        private static readonly Vector3 Camera = new Vector3(0f, 0f, -10f);
        private static readonly Rect Bounds = new Rect(-2f, -2f, 4f, 4f);

        private static readonly Vector2[][] Full = { Box(-2f, 2f, 2f) };
        private static readonly Vector2[][] LeftHalf = { Box(-2f, 0f, 2f) };
        private static readonly Vector2[][] Ring = { Box(-2f, 2f, 2f), Box(-1f, 1f, 1f) };

        private static Vector2[] Box(float left, float right, float halfHeight) => new[]
        {
            new Vector2(left, -halfHeight), new Vector2(right, -halfHeight),
            new Vector2(right, halfHeight), new Vector2(left, halfHeight),
        };

        [TestCase(0f, 5f, ExpectedResult = true, TestName = "Covers_PointBehindTheMiddle")]
        [TestCase(8f, 5f, ExpectedResult = false, TestName = "Covers_NotPointBesideIt")]
        [TestCase(2.8f, 5f, ExpectedResult = true, TestName = "Covers_UsesTheLineOfSight")] // crosses at x = 1.87
        [TestCase(0f, -15f, ExpectedResult = false, TestName = "Covers_NotSpriteBehindTheCamera")]
        [TestCase(0f, -10f, ExpectedResult = false, TestName = "Covers_NotEdgeOn")]
        public bool Covers(float x, float z) =>
            OcclusionRules.Covers(Camera, new Vector3(x, 0f, z), Bounds, Full, false, false);

        [TestCase(-1.5f, false, ExpectedResult = true, TestName = "Covers_DrawnHalf")]
        [TestCase(1.5f, false, ExpectedResult = false, TestName = "Covers_NotEmptyHalf")]
        [TestCase(-1.5f, true, ExpectedResult = false, TestName = "Covers_FlippedNotEmptyHalf")]
        [TestCase(1.5f, true, ExpectedResult = true, TestName = "Covers_FlippedDrawnHalf")]
        public bool Covers_FollowsTheDrawing(float x, bool flipX) =>
            OcclusionRules.Covers(Camera, new Vector3(x, 0f, 1f), Bounds, LeftHalf, flipX, false);

        [TestCase(1.5f, ExpectedResult = true, TestName = "IsInside_Ring")]
        [TestCase(0f, ExpectedResult = false, TestName = "IsInside_NotTheHoleOfTheRing")]
        [TestCase(3f, ExpectedResult = false, TestName = "IsInside_NotOutside")]
        public bool IsInside(float x) => OcclusionRules.IsInside(new Vector2(x, 0f), Ring);

        [TestCase(6, false, ExpectedResult = false, TestName = "IsCovered_NotWithTwoThirds")]
        [TestCase(7, false, ExpectedResult = true, TestName = "IsCovered_WithMost")]
        [TestCase(5, true, ExpectedResult = true, TestName = "IsCovered_StaysNearTheThreshold")]
        [TestCase(4, true, ExpectedResult = false, TestName = "IsCovered_EndsWithLessThanHalf")]
        public bool IsCovered(int hidden, bool was) => OcclusionRules.IsCovered(hidden, was, 0.7f);

        [Test]
        public void BodyPoint_SpreadsOverTheDrawing()
        {
            Vector3 first = OcclusionRules.BodyPoint(0, Vector3.zero, Vector3.right, Vector3.up);
            Vector3 last = OcclusionRules.BodyPoint(OcclusionRules.BodyPoints - 1, Vector3.zero, Vector3.right, Vector3.up);

            Assert.Greater(first.y, 0f); // above the ground at the feet
            Assert.Greater(last.y, first.y);
            Assert.Less(first.x, 0f);
            Assert.Greater(last.x, 0f);
        }
    }
}
