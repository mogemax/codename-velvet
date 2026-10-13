using CodenameVelvet.Combat;
using NUnit.Framework;
using UnityEngine;

namespace CodenameVelvet.Tests
{
    public class MeleeAimTests
    {
        private const float Range = 2.5f;
        private const float Tolerance = 1e-5f;

        private static readonly Vector3 Origin = Vector3.zero;

        private static void AssertDirection(Vector2 expected, Vector2 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
        }

        [Test]
        public void NearestCandidate_Wins()
        {
            var candidates = new[] { new Vector3(0f, 0f, 2f), new Vector3(-1f, 0f, 0f) };

            Vector2 aim = MeleeAim.ChooseAimDirection(Origin, Vector2.right, candidates, Range);

            AssertDirection(Vector2.left, aim);
        }

        [Test]
        public void Direction_IsNormalized()
        {
            var candidates = new[] { new Vector3(1f, 0f, 1f) };

            Vector2 aim = MeleeAim.ChooseAimDirection(Origin, Vector2.down, candidates, Range);

            AssertDirection(new Vector2(1f, 1f).normalized, aim);
        }

        [Test]
        public void OutOfRangeCandidate_IsIgnored()
        {
            var candidates = new[] { new Vector3(0f, 0f, Range + 0.01f) };

            Vector2 aim = MeleeAim.ChooseAimDirection(Origin, Vector2.right, candidates, Range);

            AssertDirection(Vector2.right, aim);
        }

        [Test]
        public void CandidateAtExactRange_Counts()
        {
            var candidates = new[] { new Vector3(0f, 0f, Range) };

            Vector2 aim = MeleeAim.ChooseAimDirection(Origin, Vector2.right, candidates, Range);

            AssertDirection(Vector2.up, aim);
        }

        [Test]
        public void NoCandidates_ReturnsFacing()
        {
            Vector2 aim = MeleeAim.ChooseAimDirection(Origin, Vector2.right, new Vector3[0], Range);

            AssertDirection(Vector2.right, aim);
        }

        [Test]
        public void ZeroFacing_WithNoCandidates_ReturnsDown()
        {
            Vector2 aim = MeleeAim.ChooseAimDirection(Origin, Vector2.zero, new Vector3[0], Range);

            AssertDirection(Vector2.down, aim);
        }

        [Test]
        public void HeightDifference_IsIgnored()
        {
            // 10 units above but 1 unit away on the ground plane: in range, aims along +X.
            var candidates = new[] { new Vector3(1f, 10f, 0f) };

            Vector2 aim = MeleeAim.ChooseAimDirection(Origin, Vector2.up, candidates, Range);

            AssertDirection(Vector2.right, aim);
        }

        [Test]
        public void CandidateOnTheAttacker_IsSkipped_ForTheNextNearest()
        {
            var candidates = new[] { new Vector3(0f, 3f, 0f), new Vector3(0f, 0f, -2f) };

            Vector2 aim = MeleeAim.ChooseAimDirection(Origin, Vector2.right, candidates, Range);

            AssertDirection(Vector2.down, aim);
        }

        [Test]
        public void OnlyCandidateOnTheAttacker_FallsBackToFacing()
        {
            var candidates = new[] { Origin };

            Vector2 aim = MeleeAim.ChooseAimDirection(Origin, Vector2.left, candidates, Range);

            AssertDirection(Vector2.left, aim);
        }
    }
}
