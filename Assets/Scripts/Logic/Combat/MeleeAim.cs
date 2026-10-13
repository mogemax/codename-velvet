using System.Collections.Generic;
using UnityEngine;

namespace CodenameVelvet.Combat
{
    /// <summary>
    /// Pure aim assist for a melee swing on the XZ ground plane, using the same
    /// 2D → 3D mapping as <see cref="MeleeArc"/> (direction.x → world X, direction.y → world Z).
    /// "Nearest" means the smallest precomputed gap between body surfaces; the returned
    /// direction points at the candidate's centre. Height differences are ignored.
    /// </summary>
    public static class MeleeAim
    {
        private const float SamePointSqrDistance = 1e-8f;

        /// <summary>
        /// Normalized planar direction from the attacker to the candidate with the smallest
        /// gap within <paramref name="range"/> (inclusive); the first wins on ties.
        /// <paramref name="candidateGaps"/> is index-aligned with <paramref name="candidatePositions"/>.
        /// Candidates centred on the attacker give no direction and are skipped. When no
        /// candidate qualifies, returns the normalized <paramref name="facing"/>, or
        /// <see cref="Vector2.down"/> if it is zero.
        /// </summary>
        public static Vector2 ChooseAimDirection(Vector3 attackerPosition, Vector2 facing, IReadOnlyList<Vector3> candidatePositions, IReadOnlyList<float> candidateGaps, float range)
        {
            float bestGap = float.MaxValue;
            Vector2 best = Vector2.zero;

            for (int i = 0; i < candidatePositions.Count; i++)
            {
                float gap = candidateGaps[i];
                if (gap > range || gap >= bestGap)
                    continue;

                Vector3 candidate = candidatePositions[i];
                Vector2 toCandidate = new Vector2(candidate.x - attackerPosition.x, candidate.z - attackerPosition.z);
                if (toCandidate.sqrMagnitude <= SamePointSqrDistance)
                    continue;

                bestGap = gap;
                best = toCandidate;
            }

            if (best != Vector2.zero)
                return best.normalized;

            return facing.sqrMagnitude < Mathf.Epsilon ? Vector2.down : facing.normalized;
        }
    }
}
