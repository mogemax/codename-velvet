using UnityEngine;

namespace CodenameVelvet.Map
{
    /// <summary>When a flat sprite gets between the camera and a point. Everything is in the sprite's local space
    /// (the sprite lies on z = 0).</summary>
    public static class OcclusionRules
    {
        /// <summary>True when the line from the camera to the point crosses the sprite's area, shrunk around its
        /// center to <paramref name="coverage"/> (drawings rarely fill the corners). The point may be on either side
        /// of the sprite: who draws on top is decided by the sorting order, not by depth.</summary>
        public static bool Covers(Vector3 camera, Vector3 point, Bounds sprite, float coverage)
        {
            float depth = camera.z - point.z;
            float t = Mathf.Approximately(depth, 0f) ? 0f : camera.z / depth;
            if (t <= 0f) return false; // the sprite is edge-on or behind the camera

            Vector3 hit = camera + (point - camera) * t - sprite.center;
            return Mathf.Abs(hit.x) <= sprite.extents.x * coverage && Mathf.Abs(hit.y) <= sprite.extents.y * coverage;
        }
    }
}
