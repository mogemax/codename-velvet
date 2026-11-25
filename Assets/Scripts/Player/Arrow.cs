using CodenameVelvet.Cameras;
using CodenameVelvet.Combat;
using CodenameVelvet.Gathering;
using UnityEngine;

namespace CodenameVelvet.Player
{
    /// <summary>
    /// An arrow in flight: straight over the ground plane at the height it was shot from. A player's arrow hurts the
    /// first living enemy in its way and flies through players; an enemy's arrow (an archer's) hurts the first living
    /// player and flies through enemies. Either stops there; anything solid (a wall, a cliff, a tree or rock) stops it
    /// too, without harm (arrows never gather). It flies through triggers and is gone after
    /// <see cref="maxDistance"/>. Moves in Update with scaled time, so a paused match freezes it.
    /// </summary>
    public class Arrow : MonoBehaviour
    {
        private const string PlayerTag = "Player";
        private const string PlayerLayer = "Player";
        private const string EnemyLayer = "ENEMY";

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
        private bool hurtsPlayers;
        private LayerMask mask;

        /// <summary>A living target that is not a player nor a resource node (arrows only hurt enemies).</summary>
        public static bool IsEnemy(Collider collider, out IDamageable target, out Component owner)
        {
            target = collider.GetComponentInParent<IDamageable>();
            owner = target as Component;
            return owner != null && target.IsAlive && owner is not ResourceNode && !owner.CompareTag(PlayerTag);
        }

        /// <summary>A living player, for an enemy's arrow.</summary>
        public static bool IsPlayer(Collider collider, out IDamageable target, out Component owner)
        {
            target = collider.GetComponentInParent<IDamageable>();
            owner = target as Component;
            return owner != null && target.IsAlive && owner.CompareTag(PlayerTag);
        }

        /// <param name="shotByEnemy">An archer's arrow: it hurts players instead of enemies.</param>
        public void Launch(Vector2 aim, int arrowDamage, bool shotByEnemy = false)
        {
            direction = new Vector3(aim.x, 0f, aim.y).normalized;
            damage = arrowDamage;
            hurtsPlayers = shotByEnemy;
            mask = shotByEnemy
                ? (hitMask | LayerMask.GetMask(PlayerLayer)) & ~LayerMask.GetMask(EnemyLayer)
                : hitMask;
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
            int count = Physics.SphereCastNonAlloc(transform.position, radius, direction, hits, step, mask, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, 0, count, HitDistance.Instance);

            for (int i = 0; i < count; i++)
            {
                Collider other = hits[i].collider;
                IDamageable target;
                Component owner;
                if (hurtsPlayers ? IsPlayer(other, out target, out owner) : IsEnemy(other, out target, out owner))
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
