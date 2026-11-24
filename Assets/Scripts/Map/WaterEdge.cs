using UnityEngine;

namespace CodenameVelvet.Map
{
    /// <summary>
    /// Keeps bodies out of the water: the water surface has a collider on the "Water" layer, and a point counts
    /// as water when that surface is the first thing a ray from above hits (terrain over a water plane wins).
    /// </summary>
    public static class WaterEdge
    {
        private const float ProbeHeight = 3f;
        private const float ProbeDepth = 15f;

        private static int waterLayer = -2;
        private static int surfaceMask;

        /// <summary>
        /// Returns <paramref name="velocity"/> (XZ is used, Y kept) without the part that would carry a body of
        /// <paramref name="radius"/> onto water during the next <paramref name="deltaTime"/>. Blocked diagonals
        /// keep the free axis, so bodies slide along the shore instead of sticking to it.
        /// </summary>
        public static Vector3 Clamp(Vector3 position, Vector3 velocity, float radius, float deltaTime)
        {
            var planar = new Vector3(velocity.x, 0f, velocity.z);
            if (planar.sqrMagnitude < 1e-6f || !EnsureLayers() || !IsWaterAhead(position, planar, radius, deltaTime))
                return velocity;

            var alongX = new Vector3(planar.x, 0f, 0f);
            if (!IsWaterAhead(position, alongX, radius, deltaTime)) return new Vector3(alongX.x, velocity.y, 0f);

            var alongZ = new Vector3(0f, 0f, planar.z);
            if (!IsWaterAhead(position, alongZ, radius, deltaTime)) return new Vector3(0f, velocity.y, alongZ.z);

            return new Vector3(0f, velocity.y, 0f);
        }

        /// <summary>True when the first surface above or at <paramref name="point"/> is water.</summary>
        public static bool IsOverWater(Vector3 point)
        {
            if (!EnsureLayers()) return false;
            return Physics.Raycast(point + Vector3.up * ProbeHeight, Vector3.down, out RaycastHit hit,
                       ProbeHeight + ProbeDepth, surfaceMask, QueryTriggerInteraction.Ignore)
                   && hit.collider.gameObject.layer == waterLayer;
        }

        private static bool IsWaterAhead(Vector3 position, Vector3 planar, float radius, float deltaTime)
        {
            if (planar.sqrMagnitude < 1e-6f) return false;
            return IsOverWater(position + planar.normalized * radius + planar * deltaTime);
        }

        private static bool EnsureLayers()
        {
            if (waterLayer == -2)
            {
                waterLayer = LayerMask.NameToLayer("Water");
                surfaceMask = LayerMask.GetMask("Ground", "Water");
            }
            return waterLayer >= 0;
        }
    }
}
