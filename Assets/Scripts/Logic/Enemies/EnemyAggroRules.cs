using UnityEngine;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// Pure aggro decisions for an enemy. All distances are measured on the XZ
    /// ground plane, so height differences between enemy and player never affect
    /// detection or attacks. Detection and lose-interest use the centre-to-centre
    /// <see cref="PlanarDistance"/>; attacks use the gap between the two bodies'
    /// surfaces (computed by the caller), so attack range is independent of body size.
    /// Chasing uses hysteresis: a chase starts inside the detection range but is
    /// only dropped beyond the (larger) lose-interest range, so a target hovering
    /// at the detection edge does not make the enemy flicker between states.
    /// </summary>
    public static class EnemyAggroRules
    {
        /// <summary>Distance between two points ignoring their Y component.</summary>
        public static float PlanarDistance(Vector3 from, Vector3 to)
        {
            float dx = to.x - from.x;
            float dz = to.z - from.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>A living target at or inside the detection range starts a chase.</summary>
        public static bool ShouldStartChase(bool targetAlive, float distance, float detectionRange)
        {
            return targetAlive && distance <= detectionRange;
        }

        /// <summary>A chase ends when the target dies or gets beyond the lose-interest range.</summary>
        public static bool ShouldDropChase(bool targetAlive, float distance, float loseInterestRange)
        {
            return !targetAlive || distance > loseInterestRange;
        }

        /// <summary>
        /// <paramref name="gap"/> is the planar distance between the bodies' surfaces
        /// (0 when touching). Inclusive: exactly at the edge counts as in range.
        /// </summary>
        public static bool IsInAttackRange(float gap, float attackRange)
        {
            return gap <= attackRange;
        }
    }
}
