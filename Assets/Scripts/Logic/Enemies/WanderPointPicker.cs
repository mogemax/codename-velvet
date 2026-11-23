using System;
using UnityEngine;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// Picks random wander destinations uniformly inside a circle on the XZ plane.
    /// Randomness is injected so callers own the seed: enemies stay independent at
    /// runtime and tests stay deterministic.
    /// </summary>
    public static class WanderPointPicker
    {
        /// <summary>
        /// Returns a point within <paramref name="radius"/> of <paramref name="center"/>
        /// on the XZ plane, keeping the center's Y.
        /// </summary>
        public static Vector3 PickPoint(Vector3 center, float radius, System.Random random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (radius < 0f)
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "Radius must be non-negative.");

            // Square root keeps the distribution uniform over the disc area
            // instead of clustering points around the center.
            double distance = radius * Math.Sqrt(random.NextDouble());
            double angle = random.NextDouble() * 2.0 * Math.PI;

            return new Vector3(
                center.x + (float)(distance * Math.Cos(angle)),
                center.y,
                center.z + (float)(distance * Math.Sin(angle)));
        }

        /// <summary>
        /// Returns a point uniformly inside the axis-aligned rectangle centred on
        /// <paramref name="center"/> with half sizes <paramref name="halfExtents"/>
        /// (x = world X, y = world Z), keeping the center's Y.
        /// </summary>
        public static Vector3 PickPointInArea(Vector3 center, Vector2 halfExtents, System.Random random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (halfExtents.x < 0f || halfExtents.y < 0f)
                throw new ArgumentOutOfRangeException(nameof(halfExtents), halfExtents, "Half extents must be non-negative.");

            return new Vector3(
                center.x + (float)((random.NextDouble() * 2.0 - 1.0) * halfExtents.x),
                center.y,
                center.z + (float)((random.NextDouble() * 2.0 - 1.0) * halfExtents.y));
        }

        /// <summary>
        /// Clamps <paramref name="point"/> on the XZ plane into the rectangle centred on
        /// <paramref name="center"/> with half sizes <paramref name="halfExtents"/>, shrunk by
        /// <paramref name="margin"/> on every side (never below zero). Y is left untouched.
        /// </summary>
        public static Vector3 ClampToArea(Vector3 point, Vector3 center, Vector2 halfExtents, float margin = 0f)
        {
            float hx = Mathf.Max(0f, halfExtents.x - margin);
            float hz = Mathf.Max(0f, halfExtents.y - margin);
            return new Vector3(
                Mathf.Clamp(point.x, center.x - hx, center.x + hx),
                point.y,
                Mathf.Clamp(point.z, center.z - hz, center.z + hz));
        }
    }
}
