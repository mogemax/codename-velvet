using System;
using CodenameVelvet.Enemies;
using NUnit.Framework;

namespace CodenameVelvet.Tests
{
    public class WeightedRandomTests
    {
        [Test]
        public void Pick_ZeroWeight_IsNeverChosen()
        {
            var random = new System.Random(1);
            for (int i = 0; i < 1000; i++)
                Assert.That(WeightedRandom.Pick(new[] { 1f, 0f, 2f }, random), Is.Not.EqualTo(1));
        }

        [Test]
        public void Pick_FollowsWeightsApproximately()
        {
            var random = new System.Random(99);
            int[] counts = new int[2];
            const int samples = 20000;
            for (int i = 0; i < samples; i++)
                counts[WeightedRandom.Pick(new[] { 1f, 3f }, random)]++;

            Assert.That(counts[1] / (double)samples, Is.EqualTo(0.75).Within(0.02));
        }

        [Test]
        public void Pick_AllZeroOrEmpty_ReturnsMinusOne()
        {
            var random = new System.Random(5);
            Assert.That(WeightedRandom.Pick(new[] { 0f, 0f }, random), Is.EqualTo(-1));
            Assert.That(WeightedRandom.Pick(Array.Empty<float>(), random), Is.EqualTo(-1));
        }

        [Test]
        public void Pick_NegativeWeights_CountAsZero()
        {
            var random = new System.Random(3);
            for (int i = 0; i < 500; i++)
                Assert.That(WeightedRandom.Pick(new[] { -5f, 1f }, random), Is.EqualTo(1));
        }

        [Test]
        public void Pick_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => WeightedRandom.Pick(null, new System.Random(1)));
            Assert.Throws<ArgumentNullException>(() => WeightedRandom.Pick(new[] { 1f }, null));
        }
    }
}
