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
    }
}
