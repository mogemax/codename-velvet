using System;
using CodenameVelvet.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace CodenameVelvet.Tests
{
    public class WanderPointPickerTests
    {
        private const float Tolerance = 0.0001f;
        private const int Samples = 1000;

        private static readonly Vector3 Center = new Vector3(10f, 2.5f, -4f);

        [Test]
        public void PickPoint_AlwaysWithinRadiusOnCenterHeight()
        {
            const float radius = 5f;
            var random = new System.Random(1234);

            for (int i = 0; i < Samples; i++)
            {
                Vector3 point = WanderPointPicker.PickPoint(Center, radius, random);

                Assert.That(EnemyAggroRules.PlanarDistance(Center, point), Is.LessThanOrEqualTo(radius + Tolerance));
                Assert.That(point.y, Is.EqualTo(Center.y));
            }
        }

        [Test]
        public void PickPoint_SameSeed_ProducesSameSequence()
        {
            var first = new System.Random(42);
            var second = new System.Random(42);

            for (int i = 0; i < 20; i++)
            {
                Vector3 a = WanderPointPicker.PickPoint(Center, 5f, first);
                Vector3 b = WanderPointPicker.PickPoint(Center, 5f, second);

                Assert.That(b.x, Is.EqualTo(a.x));
                Assert.That(b.y, Is.EqualTo(a.y));
                Assert.That(b.z, Is.EqualTo(a.z));
            }
        }

        [Test]
        public void PickPoint_ZeroRadius_ReturnsCenter()
        {
            Vector3 point = WanderPointPicker.PickPoint(Center, 0f, new System.Random(7));

            Assert.That(point.x, Is.EqualTo(Center.x).Within(Tolerance));
            Assert.That(point.y, Is.EqualTo(Center.y));
            Assert.That(point.z, Is.EqualTo(Center.z).Within(Tolerance));
        }

        [Test]
        public void PickPoint_NegativeRadius_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => WanderPointPicker.PickPoint(Center, -1f, new System.Random(7)));
        }

        [Test]
        public void PickPoint_NullRandom_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => WanderPointPicker.PickPoint(Center, 1f, null));
        }
    }
}
