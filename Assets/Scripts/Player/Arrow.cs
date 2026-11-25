using CodenameVelvet.Cameras;
using CodenameVelvet.Combat;
using CodenameVelvet.Gathering;
using UnityEngine;

namespace CodenameVelvet.Player
{
    /// <summary>
    /// An arrow in flight: straight over the ground plane at the height it was shot from. It hurts the first living
    /// enemy in its way and stops there; anything solid (a wall, a cliff, a tree or rock) stops it too, without harm
    /// (arrows never gather). It flies through players and through triggers, and is gone after
    /// <see cref="maxDistance"/>. Moves in Update with scaled time, so a paused match freezes it.
    /// </summary>
    public class Arrow : MonoBehaviour
    {
        private const string PlayerTag = "Player";

        [SerializeField, Min(0.1f)] private float speed = 30f;
        [SerializeField, Min(0.1f)] private float maxDistance = 25f;
        [Tooltip("Thickness of the arrow when checking what it hits.")]
        [SerializeField, Min(0.01f)] private float radius = 0.35f;
        [SerializeField] private float knockbackForce = 8f;
        [SerializeField, Min(0f)] private float stunSeconds = 0.15f;
        [Tooltip("Layers that stop the arrow or get hurt by it (players and water excluded).")]
        [SerializeField] private LayerMask hitMask = ~((1 << 2) | (1 << 4) | (1 << 6));
        [Tooltip("The drawing, turned to point where the arrow flies (the art points left).")]
        [SerializeField] private Transform drawing;

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private Vector3 direction;
        private int damage;
        private float travelled;

        /// <summary>A living target that is not a player nor a resource node (arrows only hurt enemies).</summary>
        public static bool IsEnemy(Collider collider, out IDamageable target, out Component owner)
        {
            target = collider.GetComponentInParent<IDamageable>();
            owner = target as Component;
            return owner != null && target.IsAlive && owner is not ResourceNode && !owner.CompareTag(PlayerTag);
        }

        public void Launch(Vector2 aim, int arrowDamage)
        {
            direction = new Vector3(aim.x, 0f, aim.y).normalized;
            damage = arrowDamage;
            if (drawing != null)
                drawing.localRotation = Quaternion.Euler(0f, 0f, WeaponRules.ScreenAngle(aim, ViewAngle.Pitch) + 180f);
        }

        private void Update()
        {
            float step = speed * Time.deltaTime;
            if (step <= 0f)
                return;

            if (HitSomething(step))
            {
                Destroy(gameObject);
                return;
            }

            transform.position += direction * step;
            travelled += step;
            if (travelled >= maxDistance)
                Destroy(gameObject);
        }

        /// <summary>Looks along the next step, nearest first: true when the arrow ends there.</summary>
        private bool HitSomething(float step)
        {
            int count = Physics.SphereCastNonAlloc(transform.position, radius, direction, hits, step, hitMask, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, 0, count, HitDistance.Instance);

            for (int i = 0; i < count; i++)
            {
                Collider other = hits[i].collider;
                if (IsEnemy(other, out IDamageable target, out Component owner))
                {
                    target.ApplyDamage(damage);
                    owner.GetComponentInParent<IKnockbackable>()?.ApplyKnockback(direction, knockbackForce, stunSeconds);
                    return true;
                }

                if (!other.isTrigger && !other.CompareTag(PlayerTag))
                    return true; // a wall, the ground, a tree...
            }

            return false;
        }

        private sealed class HitDistance : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistance Instance = new HitDistance();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
