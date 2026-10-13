using System.Collections.Generic;
using UnityEngine;

namespace CodenameVelvet.Combat
{
    /// <summary>
    /// Pure aim assist for a melee swing on the XZ ground plane, using the same
    /// 2D → 3D mapping as <see cref="MeleeArc"/> (direction.x → world X, direction.y → world Z).
    /// Height differences are ignored.
    /// </summary>
    public static class MeleeAim
    {
        private const float SamePointSqrDistance = 1e-8f;

        /// <summary>
        /// Normalized planar direction from the attacker to the nearest candidate within
        /// <paramref name="range"/> (inclusive). Candidates on top of the attacker give no
        /// direction and are skipped. When no candidate qualifies, returns the normalized
        /// <paramref name="facing"/>, or <see cref="Vector2.down"/> if it is zero.
        /// </summary>
        public static Vector2 ChooseAimDirection(Vector3 attackerPosition, Vector2 facing, IReadOnlyList<Vector3> candidatePositions, float range)
        {
            float bestSqrDistance = range * range;
            Vector2 best = Vector2.zero;

            for (int i = 0; i < candidatePositions.Count; i++)
            {
                Vector3 candidate = candidatePositions[i];
                Vector2 toCandidate = new Vector2(candidate.x - attackerPosition.x, candidate.z - attackerPosition.z);
                float sqrDistance = toCandidate.sqrMagnitude;

                if (sqrDistance <= SamePointSqrDistance || sqrDistance > bestSqrDistance)
                    continue;

                bestSqrDistance = sqrDistance;
                best = toCandidate;
            }

            if (best != Vector2.zero)
                return best.normalized;

            return facing.sqrMagnitude < Mathf.Epsilon ? Vector2.down : facing.normalized;
        }
    }
}
