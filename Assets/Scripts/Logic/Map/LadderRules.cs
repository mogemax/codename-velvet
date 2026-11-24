using UnityEngine;

namespace CodenameVelvet.Map
{
    /// <summary>Geometry rules for climbing a ladder, free of scene state so they can be tested.</summary>
    public static class LadderRules
    {
        /// <summary>Horizontal unit vector from the ladder toward the side (front or back) where the player stands.</summary>
        public static Vector3 OutwardSide(Vector3 ladderCenter, Vector3 ladderForward, Vector3 player)
        {
            Vector3 normal = new Vector3(ladderForward.x, 0f, ladderForward.z).normalized;
            return Vector3.Dot(player - ladderCenter, normal) >= 0f ? normal : -normal;
        }

        /// <summary>Only from the lower half: someone standing on the ledge must not be pulled back onto it.</summary>
        public static bool CanStart(float feetY, float bottomY, float topY) => feetY < (bottomY + topY) * 0.5f;
    }
}
