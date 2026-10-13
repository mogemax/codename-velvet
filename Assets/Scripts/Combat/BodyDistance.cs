using UnityEngine;

namespace CodenameVelvet.Combat
{
    /// <summary>
    /// Planar (XZ) distances between physical bodies, measured surface to surface so
    /// attack ranges stay independent of how big each body is. Height is ignored.
    /// </summary>
    public static class BodyDistance
    {
        /// <summary>
        /// Planar gap between the surfaces of <paramref name="colliderA"/> and
        /// <paramref name="colliderB"/>; 0 when they touch or overlap.
        /// Approximation: one round of closest-point projection
        /// (A's closest point to B's bounds centre, then B's closest point to that),
        /// exact for spheres and close enough for boxes and capsules at gameplay scale.
        /// <see cref="Collider.ClosestPoint"/> only supports box, sphere, capsule and convex
        /// mesh colliders; others return the query point and so read as overlapping.
        /// When either collider is missing, falls back to the planar distance between
        /// <paramref name="positionA"/> and <paramref name="positionB"/> (centre to centre).
        /// </summary>
        public static float PlanarGap(Vector3 positionA, Collider colliderA, Vector3 positionB, Collider colliderB)
        {
            if (colliderA == null || colliderB == null)
                return PlanarDistance(positionA, positionB);

            Vector3 pointOnA = colliderA.ClosestPoint(colliderB.bounds.center);
            Vector3 pointOnB = colliderB.ClosestPoint(pointOnA);
            return PlanarDistance(pointOnA, pointOnB);
        }

        /// <summary>
        /// Planar radius of a body: the circle enclosing the XZ footprint of its world
        /// bounds (conservative, corners included). Used to grow overlap queries so
        /// targets whose surface is in reach are found. 0 when missing.
        /// </summary>
        public static float PlanarRadius(Collider body)
        {
            if (body == null)
                return 0f;

            Vector3 extents = body.bounds.extents;
            return new Vector2(extents.x, extents.z).magnitude;
        }

        private static float PlanarDistance(Vector3 from, Vector3 to)
        {
            float dx = to.x - from.x;
            float dz = to.z - from.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
