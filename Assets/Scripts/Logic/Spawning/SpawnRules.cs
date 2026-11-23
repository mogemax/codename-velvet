using System;
using System.Collections.Generic;
using CodenameVelvet.Enemies;
using UnityEngine;

namespace CodenameVelvet.Spawning
{
    /// <summary>
    /// Pure rules that decide whether a point can host a spawn (enemy or resource). <c>SpawnZone</c> runs
    /// the physics and camera queries; this class only makes the decision.
    /// </summary>
    public static class SpawnRules
    {
        /// <summary>True when the surface (given its normal) is no steeper than <paramref name="maxSlopeDegrees"/>.</summary>
        public static bool IsFlatEnough(Vector3 surfaceNormal, float maxSlopeDegrees)
        {
            if (surfaceNormal == Vector3.zero)
                return false;
            return Vector3.Angle(surfaceNormal, Vector3.up) <= maxSlopeDegrees;
        }

        /// <summary>
        /// True when <paramref name="candidate"/> is at least <paramref name="minDistance"/> (on the XZ plane)
        /// from every occupied point.
        /// </summary>
        public static bool IsClear(Vector3 candidate, IReadOnlyList<Vector3> occupied, float minDistance)
        {
            if (occupied == null)
                throw new ArgumentNullException(nameof(occupied));

            foreach (Vector3 point in occupied)
            {
                if (EnemyAggroRules.PlanarDistance(candidate, point) < minDistance)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// True when a viewport-space point (Camera.WorldToViewportPoint) is on screen, with the view grown
        /// by <paramref name="margin"/> so spawns never pop in right at the edge.
        /// </summary>
        public static bool IsInsideViewport(Vector3 viewportPoint, float margin)
        {
            return viewportPoint.z > 0f
                && viewportPoint.x >= -margin && viewportPoint.x <= 1f + margin
                && viewportPoint.y >= -margin && viewportPoint.y <= 1f + margin;
        }
    }
}
