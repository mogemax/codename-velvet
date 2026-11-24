using System;
using System.Collections.Generic;
using CodenameVelvet.Spawning;
using NUnit.Framework;
using UnityEngine;

namespace CodenameVelvet.Tests
{
    public class SpawnRulesTests
    {
        [Test]
        public void IsFlatEnough_FlatGround_IsTrue()
        {
            Assert.That(SpawnRules.IsFlatEnough(Vector3.up, 30f), Is.True);
        }

        [Test]
        public void IsFlatEnough_SlopeAboveLimit_IsFalse()
        {
            Vector3 normal = new Vector3(0f, Mathf.Cos(45f * Mathf.Deg2Rad), Mathf.Sin(45f * Mathf.Deg2Rad)); // 45°
            Assert.That(SpawnRules.IsFlatEnough(normal, 30f), Is.False);
        }

        [Test]
        public void IsFlatEnough_Wall_IsFalse()
        {
            Assert.That(SpawnRules.IsFlatEnough(Vector3.right, 30f), Is.False);
        }

        [Test]
        public void IsFlatEnough_ZeroNormal_IsFalse()
        {
            Assert.That(SpawnRules.IsFlatEnough(Vector3.zero, 89f), Is.False);
        }

        [Test]
        public void IsClear_NothingOccupied_IsTrue()
        {
            Assert.That(SpawnRules.IsClear(Vector3.zero, new List<Vector3>(), 5f), Is.True);
        }

        [Test]
        public void IsClear_TooCloseOnXZ_IsFalse()
        {
            var occupied = new List<Vector3> { new Vector3(3f, 0f, 0f) };
            Assert.That(SpawnRules.IsClear(Vector3.zero, occupied, 4f), Is.False);
        }

        [Test]
        public void IsClear_IgnoresHeightDifference()
        {
            // Mismo punto en XZ pero 50 unidades arriba: sigue ocupado.
            var occupied = new List<Vector3> { new Vector3(0f, 50f, 0f) };
            Assert.That(SpawnRules.IsClear(Vector3.zero, occupied, 1f), Is.False);
        }

        [Test]
        public void IsClear_ExactlyAtMinDistance_IsTrue()
        {
            var occupied = new List<Vector3> { new Vector3(0f, 0f, 4f) };
            Assert.That(SpawnRules.IsClear(Vector3.zero, occupied, 4f), Is.True);
        }

        [Test]
        public void IsClear_NullList_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => SpawnRules.IsClear(Vector3.zero, null, 1f));
        }

        [Test]
        public void IsInsideViewport_CenterInFront_IsTrue()
        {
            Assert.That(SpawnRules.IsInsideViewport(new Vector3(0.5f, 0.5f, 10f), 0f), Is.True);
        }

        [Test]
        public void IsInsideViewport_BehindCamera_IsFalse()
        {
            Assert.That(SpawnRules.IsInsideViewport(new Vector3(0.5f, 0.5f, -10f), 0f), Is.False);
        }

        [Test]
        public void IsInsideViewport_JustOutside_CountsOnlyWithMargin()
        {
            var nearEdge = new Vector3(1.05f, 0.5f, 10f);
            Assert.That(SpawnRules.IsInsideViewport(nearEdge, 0f), Is.False);
            Assert.That(SpawnRules.IsInsideViewport(nearEdge, 0.1f), Is.True);
        }

        [Test]
        public void CountForArea_UsesDensityPerThousandUnits()
        {
            Assert.That(SpawnRules.CountForArea(4000f, 2.5f, 100), Is.EqualTo(10));
        }

        [Test]
        public void CountForArea_IsCappedAtMax()
        {
            Assert.That(SpawnRules.CountForArea(100000f, 2.5f, 30), Is.EqualTo(30));
        }

        [Test]
        public void CountForArea_NegativeInputs_AreZero()
        {
            Assert.That(SpawnRules.CountForArea(-50f, 2f, 10), Is.EqualTo(0));
            Assert.That(SpawnRules.CountForArea(5000f, -1f, 10), Is.EqualTo(0));
        }

        [Test]
        public void IsClearOfBodies_TouchingFootprints_IsFalse()
        {
            var bodies = new List<SpawnBody> { new SpawnBody(Vector3.zero, 2f) };
            Assert.That(SpawnRules.IsClearOfBodies(new Vector3(3f, 0f, 0f), 1.5f, bodies, 0f), Is.False);
        }

        [Test]
        public void IsClearOfBodies_GapCountsEdgeToEdge_AndIgnoresHeight()
        {
            var bodies = new List<SpawnBody> { new SpawnBody(Vector3.zero, 2f) };
            Vector3 candidate = new Vector3(6f, 40f, 0f);
            Assert.That(SpawnRules.IsClearOfBodies(candidate, 1f, bodies, 3f), Is.True);
            Assert.That(SpawnRules.IsClearOfBodies(candidate, 1f, bodies, 3.5f), Is.False);
        }

        [Test]
        public void PatchWeight_RampsBetweenThresholds()
        {
            Assert.That(SpawnRules.PatchWeight(0.2f, 0.3f, 0.7f), Is.EqualTo(0f));
            Assert.That(SpawnRules.PatchWeight(0.5f, 0.3f, 0.7f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(SpawnRules.PatchWeight(0.9f, 0.3f, 0.7f), Is.EqualTo(1f));
        }

        [Test]
        public void ClusterSize_CoversTheWholeRangeInclusive()
        {
            var range = new Vector2Int(1, 4);
            Assert.That(SpawnRules.ClusterSize(range, 0f, 99), Is.EqualTo(1));
            Assert.That(SpawnRules.ClusterSize(range, 0.999f, 99), Is.EqualTo(4));
            Assert.That(SpawnRules.ClusterSize(range, 1f, 99), Is.EqualTo(4));
        }

        [Test]
        public void ClusterSize_NeverExceedsWhatIsLeft_NorGoesBelowOne()
        {
            Assert.That(SpawnRules.ClusterSize(new Vector2Int(3, 6), 1f, 2), Is.EqualTo(2));
            Assert.That(SpawnRules.ClusterSize(new Vector2Int(0, 0), 0.5f, 10), Is.EqualTo(1));
        }

        [Test]
        public void ClusterMemberScale_CenterIsTheBigHalf_OthersTheSmallHalf()
        {
            var range = new Vector2(0.7f, 1.3f);
            Assert.That(SpawnRules.ClusterMemberScale(true, range, 0f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(SpawnRules.ClusterMemberScale(true, range, 1f), Is.EqualTo(1.3f).Within(1e-5f));
            Assert.That(SpawnRules.ClusterMemberScale(false, range, 0f), Is.EqualTo(0.7f).Within(1e-5f));
            Assert.That(SpawnRules.ClusterMemberScale(false, range, 1f), Is.EqualTo(1f).Within(1e-5f));
        }
    }
}
