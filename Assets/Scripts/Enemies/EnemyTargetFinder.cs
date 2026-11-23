using CodenameVelvet.Combat;
using UnityEngine;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// Finds the nearest living player around a point with a non-allocating
    /// overlap query. Works for any number of local players because it only
    /// inspects what is physically nearby — no global player list or tag lookup.
    /// </summary>
    public class EnemyTargetFinder : MonoBehaviour
    {
        private const string PlayerTag = "Player";

        [Tooltip("Layers scanned for players. Empty = the 'Player' layer.")]
        [SerializeField] private LayerMask searchMask;
        [Tooltip("Max colliders inspected per search. Raise it if busy scenes miss players.")]
        [SerializeField] private int maxHits = 32;

        private Collider[] hits;

        private void Awake()
        {
            hits = new Collider[Mathf.Max(1, maxHits)];
            if (searchMask.value == 0) searchMask = LayerMask.GetMask("Player");
        }

        /// <summary>
        /// Looks for the nearest player (tagged "Player", with a living
        /// <see cref="IDamageable"/>) within <paramref name="range"/> of
        /// <paramref name="origin"/>, measured centre to centre on the XZ plane.
        /// <paramref name="targetCollider"/> is the collider that matched, so callers
        /// can measure the gap to the player's body.
        /// </summary>
        public bool TryFindNearest(Vector3 origin, float range, out IDamageable target, out Transform targetTransform, out Collider targetCollider)
        {
            target = null;
            targetTransform = null;
            targetCollider = null;
            float nearestDistance = float.MaxValue;

            int count = Physics.OverlapSphereNonAlloc(origin, range, hits, searchMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = hits[i];
                IDamageable candidate = hit.GetComponentInParent<IDamageable>();
                hits[i] = null; // Don't keep scene objects alive through the buffer.

                if (candidate is not Component owner || !owner.CompareTag(PlayerTag) || !candidate.IsAlive)
                    continue;

                float distance = EnemyAggroRules.PlanarDistance(origin, owner.transform.position);
                if (distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                target = candidate;
                targetTransform = owner.transform;
                targetCollider = hit;
            }

            return target != null;
        }
    }
}
