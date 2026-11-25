using CodenameVelvet.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace CodenameVelvet.Tests
{
    public class ArenaRulesTests
    {
        [TestCase(0f, 0f, 0f, true)]
        [TestCase(3f, 0f, -8f, true)]
        [TestCase(3.1f, 0f, 0f, false)] // inside the box, but too close to a side
        [TestCase(0f, 0f, 8.1f, false)]
        [TestCase(0f, 2f, 0f, true)] // the margin does not apply to height
        [TestCase(0f, -2.1f, 0f, false)] // below the arena
        public void Contains_LeavesAMarginAtTheSides(float x, float y, float z, bool expected)
        {
            Assert.That(ArenaRules.Contains(new Vector3(x, y, z), new Vector3(10f, 4f, 20f), 2f), Is.EqualTo(expected));
        }

        [TestCase(5.5f, 0f, 0f, true)] // just outside the side
        [TestCase(7.1f, 0f, 0f, false)] // beyond the margin
        [TestCase(0f, 2.1f, 0f, false)] // height is unaffected by the margin
        public void Contains_NegativeMarginReachesPastTheSides(float x, float y, float z, bool expected)
        {
            Assert.That(ArenaRules.Contains(new Vector3(x, y, z), new Vector3(10f, 4f, 20f), -2f), Is.EqualTo(expected));
        }
    }
}
