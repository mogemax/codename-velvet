using UnityEngine;

namespace CodenameVelvet.Map
{
    /// <summary>How much of a player something between them and the camera hides. The player's drawing is sampled at
    /// <see cref="BodyPoints"/> points, and only counts as covered when most of them are hidden.</summary>
    public static class OcclusionRules
    {
        /// <summary>A 3x3 grid over the drawing: left, middle and right at the legs, the chest and the head.</summary>
        public const int BodyPoints = 9;

        /// <summary>Once covered, it stays so until this much less is hidden, so it never flickers at the edge.</summary>
        public const float Hysteresis = 0.2f;

        // Of the player's drawing (the collider is narrower and shorter), like the hole of "Velvet/Lit".
        private const float BodyWidth = 2.4f;
        private const float BodyHeight = 4.8f;

        /// <summary>Sample point of a drawing that stands on <paramref name="feet"/> facing the camera.
        /// <paramref name="right"/> and <paramref name="up"/> are the camera's, one world unit long.</summary>
        public static Vector3 BodyPoint(int index, Vector3 feet, Vector3 right, Vector3 up)
        {
            int column = index % 3 - 1;
            int row = index / 3;
            return feet + right * (column * 0.35f * BodyWidth) + up * ((0.15f + row * 0.35f) * BodyHeight);
        }

        /// <summary>True when <paramref name="hiddenPoints"/> reach <paramref name="threshold"/> (a fraction of all of
        /// them), or stay near it if it was covered already.</summary>
        public static bool IsCovered(int hiddenPoints, bool wasCovered, float threshold) =>
            hiddenPoints >= (wasCovered ? threshold - Hysteresis : threshold) * BodyPoints;

        /// <summary>True when the line from the camera to the point crosses a sprite's drawing. Camera and point are in
        /// the sprite's local space (it lies on z = 0); <paramref name="bounds"/> and <paramref name="outline"/> are the
        /// sprite's own, before flipping. The point may be on either side of the sprite: who draws on top is decided by
        /// the sorting order, not by depth.</summary>
        public static bool Covers(Vector3 camera, Vector3 point, Rect bounds, Vector2[][] outline, bool flipX, bool flipY)
        {
            float depth = camera.z - point.z;
            float t = Mathf.Approximately(depth, 0f) ? 0f : camera.z / depth;
            if (t <= 0f) return false; // the sprite is edge-on or behind the camera

            Vector2 hit = camera + (point - camera) * t;
            if (flipX) hit.x = -hit.x;
            if (flipY) hit.y = -hit.y;
            return bounds.Contains(hit) && IsInside(hit, outline);
        }

        /// <summary>Point in polygon (even-odd over every path, so a path inside another is a hole).</summary>
        public static bool IsInside(Vector2 point, Vector2[][] outline)
        {
            bool inside = false;
            foreach (Vector2[] path in outline)
            {
                for (int i = 0, j = path.Length - 1; i < path.Length; j = i++)
                {
                    Vector2 a = path[i], b = path[j];
                    if (a.y > point.y != b.y > point.y && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                        inside = !inside;
                }
            }
            return inside;
        }
    }
}
