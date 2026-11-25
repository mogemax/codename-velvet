using CodenameVelvet.Map;
using NUnit.Framework;
using UnityEngine;

namespace CodenameVelvet.Tests
{
    public class OcclusionRulesTests
    {
        private static readonly Vector3 Camera = new Vector3(0f, 0f, -10f);
        private static readonly Bounds Sprite = new Bounds(Vector3.zero, new Vector3(4f, 4f, 0f));

        [TestCase(0f, 5f, ExpectedResult = true, TestName = "Covers_PointBehindTheMiddle")]
        [TestCase(8f, 5f, ExpectedResult = false, TestName = "Covers_NotPointBesideIt")]
        [TestCase(2.8f, 5f, ExpectedResult = true, TestName = "Covers_UsesTheLineOfSight")] // crosses at x = 1.87
        [TestCase(0f, -15f, ExpectedResult = false, TestName = "Covers_NotSpriteBehindTheCamera")]
        [TestCase(0f, -10f, ExpectedResult = false, TestName = "Covers_NotEdgeOn")]
        public bool Covers(float x, float z) => OcclusionRules.Covers(Camera, new Vector3(x, 0f, z), Sprite, 1f);

        [Test]
        public void Covers_CoverageShrinksTheArea() =>
            Assert.IsFalse(OcclusionRules.Covers(Camera, new Vector3(2.8f, 0f, 5f), Sprite, 0.8f));
    }
}
