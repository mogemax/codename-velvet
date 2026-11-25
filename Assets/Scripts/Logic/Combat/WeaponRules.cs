using UnityEngine;

namespace CodenameVelvet.Combat
{
    /// <summary>Pure rules of the player's weapons (sword damage, how an arrow looks in flight).</summary>
    public static class WeaponRules
    {
        /// <summary>Damage of a swing: the sword's with one in hand, otherwise bare-handed.</summary>
        public static int SwingDamage(bool hasSword, int unarmedDamage, int swordDamage) =>
            hasSword ? swordDamage : unarmedDamage;

        /// <summary>
        /// Angle on screen (degrees, 0 = right, 90 = up) of a flight along <paramref name="direction"/> on the
        /// ground plane (x → world X, y → world Z), seen by a camera looking down by <paramref name="pitchDegrees"/>:
        /// moving away from the camera only climbs the screen by the sine of the pitch.
        /// </summary>
        public static float ScreenAngle(Vector2 direction, float pitchDegrees)
        {
            float up = direction.y * Mathf.Sin(pitchDegrees * Mathf.Deg2Rad);
            return Mathf.Atan2(up, direction.x) * Mathf.Rad2Deg;
        }
    }
}
