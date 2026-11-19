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
    }
}
