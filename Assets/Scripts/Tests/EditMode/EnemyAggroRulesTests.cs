using CodenameVelvet.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace CodenameVelvet.Tests
{
    public class EnemyAggroRulesTests
    {
        private const float Detection = 6f;
        private const float LoseInterest = 9f;
        private const float Attack = 1.5f;

        [Test]
        public void StartChase_AtOrInsideDetectionRange_IsTrue()
        {
            Assert.That(EnemyAggroRules.ShouldStartChase(true, 0f, Detection), Is.True);
            Assert.That(EnemyAggroRules.ShouldStartChase(true, Detection, Detection), Is.True);
        }

        [Test]
        public void StartChase_BeyondDetectionRange_IsFalse()
        {
            Assert.That(EnemyAggroRules.ShouldStartChase(true, Detection + 0.01f, Detection), Is.False);
        }

        [Test]
        public void StartChase_DeadTarget_IsFalse()
        {
            Assert.That(EnemyAggroRules.ShouldStartChase(false, 0f, Detection), Is.False);
        }

        [Test]
        public void DropChase_BetweenDetectionAndLoseInterest_KeepsChasing()
        {
            // Hysteresis band: too far to start a chase, close enough to keep one.
            float distance = (Detection + LoseInterest) * 0.5f;

            Assert.That(EnemyAggroRules.ShouldStartChase(true, distance, Detection), Is.False);
            Assert.That(EnemyAggroRules.ShouldDropChase(true, distance, LoseInterest), Is.False);
        }

        [Test]
        public void DropChase_ExactlyAtLoseInterestRange_KeepsChasing()
        {
            Assert.That(EnemyAggroRules.ShouldDropChase(true, LoseInterest, LoseInterest), Is.False);
        }

        [Test]
        public void DropChase_BeyondLoseInterestRange_Drops()
        {
            Assert.That(EnemyAggroRules.ShouldDropChase(true, LoseInterest + 0.01f, LoseInterest), Is.True);
        }

        [Test]
        public void DropChase_DeadTarget_DropsEvenWhenAdjacent()
        {
            Assert.That(EnemyAggroRules.ShouldDropChase(false, 0f, LoseInterest), Is.True);
        }

        [Test]
        public void AttackRange_IsInclusiveAtTheEdge()
        {
            Assert.That(EnemyAggroRules.IsInAttackRange(Attack - 0.01f, Attack), Is.True);
            Assert.That(EnemyAggroRules.IsInAttackRange(Attack, Attack), Is.True);
            Assert.That(EnemyAggroRules.IsInAttackRange(Attack + 0.01f, Attack), Is.False);
        }

        [Test]
        public void PlanarDistance_IgnoresHeight()
        {
            var from = new Vector3(0f, 0f, 0f);
            var to = new Vector3(3f, 50f, 4f);

            Assert.That(EnemyAggroRules.PlanarDistance(from, to), Is.EqualTo(5f).Within(0.0001f));
        }
    }
}
