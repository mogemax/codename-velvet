using System;
using CodenameVelvet.Player;
using NUnit.Framework;

namespace CodenameVelvet.Tests
{
    public class PlayerHealthStateTests
    {
        [Test]
        public void NewState_StartsAtMaxHpAndAlive()
        {
            var health = new PlayerHealthState(3);

            Assert.That(health.CurrentHp, Is.EqualTo(3));
            Assert.That(health.MaxHp, Is.EqualTo(3));
            Assert.That(health.IsDead, Is.False);
        }

        [Test]
        public void ApplyDamage_ReducesHpByAmount()
        {
            var health = new PlayerHealthState(3);

            health.ApplyDamage(1);

            Assert.That(health.CurrentHp, Is.EqualTo(2));
            Assert.That(health.IsDead, Is.False);
        }

        [Test]
        public void ApplyDamage_BeyondRemainingHp_ClampsToZeroAndDies()
        {
            var health = new PlayerHealthState(3);

            health.ApplyDamage(10);

            Assert.That(health.CurrentHp, Is.EqualTo(0));
            Assert.That(health.IsDead, Is.True);
        }

        [Test]
        public void ApplyDamage_ExactlyRemainingHp_Dies()
        {
            var health = new PlayerHealthState(3);

            health.ApplyDamage(2);
            Assert.That(health.IsDead, Is.False);

            health.ApplyDamage(1);
            Assert.That(health.IsDead, Is.True);
        }

        [Test]
        public void ApplyDamage_AfterDeath_IsNoOp()
        {
            var health = new PlayerHealthState(1);
            health.ApplyDamage(1);

            Assert.DoesNotThrow(() => health.ApplyDamage(5));
            Assert.That(health.CurrentHp, Is.EqualTo(0));
            Assert.That(health.IsDead, Is.True);
        }

        [TestCase(0)]
        [TestCase(-2)]
        public void ApplyDamage_NonPositiveAmount_IsIgnored(int amount)
        {
            var health = new PlayerHealthState(3);
            health.ApplyDamage(1);

            health.ApplyDamage(amount);

            Assert.That(health.CurrentHp, Is.EqualTo(2));
        }

        [Test]
        public void ResetToFull_RevivesAtMaxHp()
        {
            var health = new PlayerHealthState(3);
            health.ApplyDamage(3);

            health.ResetToFull();

            Assert.That(health.CurrentHp, Is.EqualTo(3));
            Assert.That(health.IsDead, Is.False);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_NonPositiveMaxHp_Throws(int maxHp)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerHealthState(maxHp));
        }
    }
}
