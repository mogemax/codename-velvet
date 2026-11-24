using CodenameVelvet.Map;
using NUnit.Framework;
using UnityEngine;

namespace CodenameVelvet.Tests
{
    public class LadderRulesTests
    {
        [TestCase(5f, 1f)]
        [TestCase(-5f, -1f)]
        public void OutwardSide_PointsToThePlayersSide_AndStaysHorizontal(float playerZ, float expectedZ)
        {
            Vector3 side = LadderRules.OutwardSide(Vector3.zero, new Vector3(0f, 0.3f, 1f), new Vector3(0f, -9f, playerZ));
            Assert.That(side, Is.EqualTo(new Vector3(0f, 0f, expectedZ)));
        }

        [TestCase(0f, true)]
        [TestCase(5f, false)]
        [TestCase(10f, false)]
        public void CanStart_OnlyFromTheLowerHalf(float feetY, bool expected)
        {
            Assert.That(LadderRules.CanStart(feetY, 0f, 10f), Is.EqualTo(expected));
        }
    }
}
