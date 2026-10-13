using UnityEngine;

namespace CodenameVelvet.Combat
{
    /// <summary>
    /// Pure hit test for a melee swing: a pie slice on the XZ ground plane.
    /// Facing uses the same 2D → 3D mapping as player movement
    /// (facing.x → world X, facing.y → world Z); height differences are ignored.
    /// </summary>
    public static class MeleeArc
    {
        // Absorbs float error so a target exactly on the arc edge still counts.
        private const float AngleToleranceDegrees = 0.01f;
        private const float SamePointSqrDistance = 1e-8f;

        /// <summary>
        /// True when <paramref name="targetPosition"/> lies within <paramref name="range"/>
        /// (inclusive) and no more than half of <paramref name="arcDegrees"/> away from
        /// <paramref name="facing"/>. A target on top of the attacker always counts;
        /// a zero facing falls back to <see cref="Vector2.down"/> (the player's default).
        /// </summary>
        public static bool IsInArc(Vector3 attackerPosition, Vector2 facing, Vector3 targetPosition, float range, float arcDegrees)
        {
            Vector2 toTarget = new Vector2(targetPosition.x - attackerPosition.x, targetPosition.z - attackerPosition.z);
            float sqrDistance = toTarget.sqrMagnitude;

            if (sqrDistance > range * range)
                return false;

            if (sqrDistance <= SamePointSqrDistance)
                return true;

            if (facing.sqrMagnitude < Mathf.Epsilon)
                facing = Vector2.down;

            float halfArc = arcDegrees * 0.5f;
            return Vector2.Angle(facing, toTarget) <= halfArc + AngleToleranceDegrees;
        }
    }
}
