using System;
using CodenameVelvet.Multiplayer;
using NUnit.Framework;
using UnityEngine;

namespace CodenameVelvet.Tests
{
    public class SplitScreenLayoutTests
    {
        private const float Tolerance = 0.0001f;

        private static void AssertRect(Rect expected, Rect actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance), "x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance), "y");
            Assert.That(actual.width, Is.EqualTo(expected.width).Within(Tolerance), "width");
            Assert.That(actual.height, Is.EqualTo(expected.height).Within(Tolerance), "height");
        }

        [Test]
        public void OnePlayer_IsFullscreen()
        {
            Rect[] rects = SplitScreenLayout.ComputeRects(1);

            Assert.That(rects, Has.Length.EqualTo(1));
            AssertRect(new Rect(0f, 0f, 1f, 1f), rects[0]);
        }

        [Test]
        public void TwoPlayers_AreSideBySide()
        {
            Rect[] rects = SplitScreenLayout.ComputeRects(2);

            Assert.That(rects, Has.Length.EqualTo(2));
            AssertRect(new Rect(0f, 0f, 0.5f, 1f), rects[0]);
            AssertRect(new Rect(0.5f, 0f, 0.5f, 1f), rects[1]);
        }

        [Test]
        public void ThreePlayers_AreTwoOnTopAndOneCenteredBelow()
        {
            Rect[] rects = SplitScreenLayout.ComputeRects(3);

            Assert.That(rects, Has.Length.EqualTo(3));
            AssertRect(new Rect(0f, 0.5f, 0.5f, 0.5f), rects[0]);
            AssertRect(new Rect(0.5f, 0.5f, 0.5f, 0.5f), rects[1]);
            AssertRect(new Rect(0.25f, 0f, 0.5f, 0.5f), rects[2]);
        }

        [Test]
        public void FourPlayers_FillATwoByTwoGrid()
        {
            Rect[] rects = SplitScreenLayout.ComputeRects(4);

            Assert.That(rects, Has.Length.EqualTo(4));
            AssertRect(new Rect(0f, 0.5f, 0.5f, 0.5f), rects[0]);
            AssertRect(new Rect(0.5f, 0.5f, 0.5f, 0.5f), rects[1]);
            AssertRect(new Rect(0f, 0f, 0.5f, 0.5f), rects[2]);
            AssertRect(new Rect(0.5f, 0f, 0.5f, 0.5f), rects[3]);
        }

        [TestCase(0)]
        [TestCase(5)]
        [TestCase(-1)]
        public void OutOfRangeCount_Throws(int playerCount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SplitScreenLayout.ComputeRects(playerCount));
        }

        [Test]
        public void EveryLayout_StaysInsideTheScreenWithoutOverlaps(
            [NUnit.Framework.Range(SplitScreenLayout.MinPlayers, SplitScreenLayout.MaxPlayers)] int playerCount)
        {
            Rect[] rects = SplitScreenLayout.ComputeRects(playerCount);

            for (int i = 0; i < rects.Length; i++)
            {
                Rect rect = rects[i];
                Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(-Tolerance));
                Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(-Tolerance));
                Assert.That(rect.xMax, Is.LessThanOrEqualTo(1f + Tolerance));
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(1f + Tolerance));

                for (int j = i + 1; j < rects.Length; j++)
                {
                    Rect other = rects[j];
                    bool overlaps = rect.xMin < other.xMax - Tolerance && other.xMin < rect.xMax - Tolerance
                                 && rect.yMin < other.yMax - Tolerance && other.yMin < rect.yMax - Tolerance;
                    Assert.That(overlaps, Is.False, $"P{i + 1} overlaps P{j + 1}");
                }
            }
        }
    }
}
