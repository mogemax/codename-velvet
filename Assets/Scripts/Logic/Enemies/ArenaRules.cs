using UnityEngine;

namespace CodenameVelvet.Enemies
{
    /// <summary>Boss arena rules, free of scene state so they can be tested.</summary>
    public static class ArenaRules
    {
        /// <summary>
        /// Whether an offset from the center of a box (in the box's axes, world units) is inside it and at
        /// least <paramref name="margin"/> away from its four sides. Height only has to be within the box.
        /// </summary>
        public static bool Contains(Vector3 offset, Vector3 size, float margin)
        {
            return Mathf.Abs(offset.x) <= size.x * 0.5f - margin
                && Mathf.Abs(offset.y) <= size.y * 0.5f
                && Mathf.Abs(offset.z) <= size.z * 0.5f - margin;
        }
    }
}
