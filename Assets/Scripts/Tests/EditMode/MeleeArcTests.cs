using CodenameVelvet.Combat;
using NUnit.Framework;
using UnityEngine;

namespace CodenameVelvet.Tests
{
    public class MeleeArcTests
    {
        private const float Range = 2.5f;
        private const float Arc = 120f;

        private static readonly Vector3 Origin = Vector3.zero;
        private static readonly Vector2 FacingForward = Vector2.up; // world +Z

        private static Vector3 PointAt(float degreesFromForward, float distance)
        {
            float radians = degreesFromForward * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians) * distance, 0f, Mathf.Cos(radians) * distance);
        }

        [Test]
        public void TargetInFront_IsHit()
        {
            Assert.That(MeleeArc.IsInArc(Origin, FacingForward, new Vector3(0f, 0f, 1f), Range, Arc), Is.True);
        }

        [Test]
        public void TargetBehind_IsMissed()
        {
            Assert.That(MeleeArc.IsInArc(Origin, FacingForward, new Vector3(0f, 0f, -1f), Range, Arc), Is.False);
        }

        [Test]
        public void TargetToTheSide_IsMissedByA120DegreeArc()
        {
            Assert.That(MeleeArc.IsInArc(Origin, FacingForward, new Vector3(1f, 0f, 0f), Range, Arc), Is.False);
            Assert.That(MeleeArc.IsInArc(Origin, FacingForward, new Vector3(-1f, 0f, 0f), Range, Arc), Is.False);
        }

        [Test]
        public void FacingMapsXToWorldX_AndYToWorldZ()
        {
            Assert.That(MeleeArc.IsInArc(Origin, Vector2.right, new Vector3(1f, 0f, 0f), Range, Arc), Is.True);
            Assert.That(MeleeArc.IsInArc(Origin, Vector2.right, new Vector3(0f, 0f, 1f), Range, Arc), Is.False);
        }

        [Test]
        public void Range_IsInclusive()
        {
            Assert.That(MeleeArc.IsInArc(Origin, FacingForward, new Vector3(0f, 0f, Range), Range, Arc), Is.True);
        }

        [Test]
        public void JustBeyondRange_IsMissed()
        {
            Assert.That(MeleeArc.IsInArc(Origin, FacingForward, new Vector3(0f, 0f, Range + 0.01f), Range, Arc), Is.False);
        }

        [Test]
        public void ArcEdge_IsInclusive()
        {
            Assert.That(MeleeArc.IsInArc(Origin, FacingForward, PointAt(Arc * 0.5f, 1f), Range, Arc), Is.True);
            Assert.That(MeleeArc.IsInArc(Origin, FacingForward, PointAt(-Arc * 0.5f, 1f), Range, Arc), Is.True);
        }

        [Test]
        public void JustOutsideArcEdge_IsMissed()
        {
            Assert.That(MeleeArc.IsInArc(Origin, FacingForward, PointAt(Arc * 0.5f + 1f, 1f), Range, Arc), Is.False);
            Assert.That(MeleeArc.IsInArc(Origin, FacingForward, PointAt(-Arc * 0.5f - 1f, 1f), Range, Arc), Is.False);
        }

        [Test]
        public void FullCircleArc_HitsEveryDirectionInRange()
        {
            Assert.That(MeleeArc.IsInArc(Origin, FacingForward, new Vector3(0f, 0f, -1f), Range, 360f), Is.True);
            Assert.That(MeleeArc.IsInArc(Origin, FacingForward, new Vector3(1f, 0f, 0f), Range, 360f), Is.True);
            Assert.That(MeleeArc.IsInArc(Origin, FacingForward, new Vector3(0f, 0f, -Range - 0.01f), Range, 360f), Is.False);
        }

        [Test]
        public void TargetOnAttackerPosition_IsHit()
        {
            Assert.That(MeleeArc.IsInArc(Origin, FacingForward, Origin, Range, Arc), Is.True);
        }

        [Test]
        public void ZeroFacing_FallsBackToDown()
        {
            // Vector2.down maps to world -Z.
            Assert.That(MeleeArc.IsInArc(Origin, Vector2.zero, new Vector3(0f, 0f, -1f), Range, Arc), Is.True);
            Assert.That(MeleeArc.IsInArc(Origin, Vector2.zero, new Vector3(0f, 0f, 1f), Range, Arc), Is.False);
        }

        [Test]
        public void HeightDifference_IsIgnored()
        {
            Vector3 attacker = new Vector3(0f, 5f, 0f);

            Assert.That(MeleeArc.IsInArc(attacker, FacingForward, new Vector3(0f, -10f, Range), Range, Arc), Is.True);
            Assert.That(MeleeArc.IsInArc(attacker, FacingForward, new Vector3(0f, -10f, 0f), Range, Arc), Is.True);
        }

        [Test]
        public void NonNormalizedFacing_BehavesLikeNormalized()
        {
            Assert.That(MeleeArc.IsInArc(Origin, new Vector2(0f, 7f), PointAt(Arc * 0.5f, 1f), Range, Arc), Is.True);
        }
    }
}
