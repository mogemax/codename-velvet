using CodenameVelvet.Feedback;
using CodenameVelvet.Inventory;
using NUnit.Framework;

namespace CodenameVelvet.Tests
{
    public class SpringTests
    {
        [Test]
        public void Displaced_SettlesBackToRest()
        {
            var spring = new Spring();
            spring.Displace(0.3f);
            for (int i = 0; i < 120; i++) spring.Step(260f, 14f, 1f / 60f);
            Assert.That(spring.Value, Is.EqualTo(0f));
            Assert.That(spring.IsSettled, Is.True);
        }

        [Test]
        public void Underdamped_OvershootsPastRest()
        {
            var spring = new Spring();
            spring.Displace(0.3f);
            float lowest = 0f;
            for (int i = 0; i < 60; i++)
            {
                spring.Step(260f, 14f, 1f / 60f);
                lowest = System.Math.Min(lowest, spring.Value);
            }
            Assert.That(lowest, Is.LessThan(0f));
        }

        [Test]
        public void LongFrame_StaysStable()
        {
            var spring = new Spring();
            spring.Displace(0.5f);
            spring.Step(260f, 14f, 0.5f);
            Assert.That(System.Math.Abs(spring.Value), Is.LessThan(0.5f));
        }

        [Test]
        public void Displace_IsClamped()
        {
            var spring = new Spring();
            spring.Displace(0.4f, 0.5f);
            spring.Displace(0.4f, 0.5f);
            Assert.That(spring.Value, Is.EqualTo(0.5f));
        }
    }

    public class RumbleMixTests
    {
        [Test]
        public void Overlapping_TakesStrongestPerMotor()
        {
            var mix = new RumbleMix();
            mix.Add(0.8f, 0.1f, 1f, 0f);
            mix.Add(0.2f, 0.6f, 1f, 0f);
            RumbleLevel level = mix.Sample(0f);
            Assert.That(level.Low, Is.EqualTo(0.8f).Within(1e-5));
            Assert.That(level.High, Is.EqualTo(0.6f).Within(1e-5));
        }

        [Test]
        public void Pulse_FadesLinearly()
        {
            var mix = new RumbleMix();
            mix.Add(1f, 0.5f, 2f, 10f);
            RumbleLevel level = mix.Sample(11f);
            Assert.That(level.Low, Is.EqualTo(0.5f).Within(1e-5));
            Assert.That(level.High, Is.EqualTo(0.25f).Within(1e-5));
        }

        [Test]
        public void Expired_IsSilent()
        {
            var mix = new RumbleMix();
            mix.Add(1f, 1f, 0.1f, 0f);
            Assert.That(mix.Sample(0.2f).IsSilent, Is.True);
        }

        [Test]
        public void ZeroLengthOrSilent_IsIgnored()
        {
            var mix = new RumbleMix();
            mix.Add(1f, 1f, 0f, 0f);
            mix.Add(0f, 0f, 1f, 0f);
            Assert.That(mix.Sample(0f).IsSilent, Is.True);
        }
    }

    public class FeedbackRulesTests
    {
        [TestCase(1, 3, true)]
        [TestCase(2, 3, false)]
        [TestCase(0, 3, false)]
        [TestCase(3, 10, true)]
        [TestCase(4, 10, false)]
        public void IsLowHealth_ThirtyPercentOrLastPoint(int hp, int max, bool expected)
        {
            Assert.That(FeedbackRules.IsLowHealth(hp, max, 0.3f), Is.EqualTo(expected));
        }

        [Test]
        public void BounceHeight_StartsAndLandsOnGround()
        {
            Assert.That(FeedbackRules.BounceHeight(0f, 2f), Is.EqualTo(0f));
            Assert.That(FeedbackRules.BounceHeight(0.75f, 2f), Is.EqualTo(0f).Within(1e-5));
            Assert.That(FeedbackRules.BounceHeight(1f, 2f), Is.EqualTo(0f).Within(1e-5));
        }

        [Test]
        public void BounceHeight_ArcPeaksHigherThanBounce()
        {
            float arc = FeedbackRules.BounceHeight(0.375f, 2f);
            float bounce = FeedbackRules.BounceHeight(0.875f, 2f);
            Assert.That(arc, Is.EqualTo(2f).Within(1e-5));
            Assert.That(bounce, Is.GreaterThan(0f).And.LessThan(arc));
        }

        [Test]
        public void HurtIntensity_GrowsWithDamage_Capped()
        {
            Assert.That(FeedbackRules.HurtIntensity(1, 0.5f, 0.25f), Is.EqualTo(0.5f));
            Assert.That(FeedbackRules.HurtIntensity(2, 0.5f, 0.25f), Is.EqualTo(0.75f));
            Assert.That(FeedbackRules.HurtIntensity(5, 0.5f, 0.25f), Is.EqualTo(1f));
        }
    }

    public class InventorySpaceTests
    {
        [Test]
        public void Empty_HasRoomInEverySlot()
        {
            var inventory = new ResourceInventory(4, 20);
            Assert.That(inventory.SpaceFor(ResourceType.Wood), Is.EqualTo(80));
        }

        [Test]
        public void CountsOwnStacksAndEmptySlots_NotOtherTypes()
        {
            var inventory = new ResourceInventory(2, 20);
            inventory.TryAdd(ResourceType.Wood, 15);
            Assert.That(inventory.SpaceFor(ResourceType.Wood), Is.EqualTo(25));
            Assert.That(inventory.SpaceFor(ResourceType.Stone), Is.EqualTo(20));
        }

        [Test]
        public void Full_HasNoRoom()
        {
            var inventory = new ResourceInventory(1, 5);
            inventory.TryAdd(ResourceType.Stone, 5);
            Assert.That(inventory.SpaceFor(ResourceType.Stone), Is.EqualTo(0));
            Assert.That(inventory.SpaceFor(ResourceType.Wood), Is.EqualTo(0));
        }
    }
}
