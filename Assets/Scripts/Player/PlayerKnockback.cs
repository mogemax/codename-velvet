using CodenameVelvet.Combat;
using UnityEngine;

namespace CodenameVelvet.Player
{
    /// <summary>
    /// Lets attacks push the player (enemy hits, the cyclops' slams) through <see cref="PlayerMovement.Push"/>.
    /// Only the hit that actually landed pushes: a hit blocked by the post-hit invulnerability (or dev invulnerability)
    /// does not, and neither does anything while dead or climbing.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerKnockback : MonoBehaviour, IKnockbackable
    {
        [Tooltip("Multiplies every push this player receives (1 = as the attacker says).")]
        [SerializeField, Min(0f)] private float strength = 1f;

        private PlayerMovement movement;
        private PlayerHealth health;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            health = GetComponent<PlayerHealth>();
        }

        public void ApplyKnockback(Vector3 direction, float force, float stunSeconds)
        {
            if (health != null && (!health.IsAlive || health.Invulnerable || (health.IsRecovering && !health.WasHitThisFrame)))
                return;

            Vector2 planar = new Vector2(direction.x, direction.z);
            if (planar.sqrMagnitude < 1e-6f || force <= 0f)
                return;

            movement.Push(planar.normalized * (force * strength), stunSeconds);
        }
    }
}
