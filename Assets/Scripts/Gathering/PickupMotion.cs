using CodenameVelvet.Feedback;
using CodenameVelvet.Player;
using UnityEngine;

namespace CodenameVelvet.Gathering
{
    /// <summary>
    /// How a dropped piece moves: it flies out of the node in an arc and bounces once where it lands, then a living
    /// player nearby who has room for it pulls it in, faster and faster, until their body touches it and
    /// <see cref="ResourcePickup"/> collects it. A full player is ignored, so pieces never chase someone who can't
    /// take them.
    /// </summary>
    [RequireComponent(typeof(ResourcePickup))]
    public class PickupMotion : MonoBehaviour
    {
        [Header("Drop")]
        [SerializeField, Min(0.05f)] private float flightSeconds = 0.5f;
        [SerializeField, Min(0f)] private float arcHeight = 3f;

        [Header("Magnet")]
        [SerializeField, Min(0f)] private float magnetRadius = 5f;
        [SerializeField, Min(0f)] private float magnetAcceleration = 35f;
        [SerializeField, Min(0f)] private float magnetMaxSpeed = 16f;
        [Tooltip("Aim this high on the player's body.")]
        [SerializeField] private float targetHeight = 1f;
        [SerializeField, Min(0.02f)] private float searchInterval = 0.15f;
        [SerializeField] private LayerMask playerMask = 1 << 6;

        private readonly Collider[] nearby = new Collider[8];
        private ResourcePickup pickup;
        private Vector3 launchPoint;
        private Vector3 landingPoint;
        private float flightTime = -1f; // < 0 = not flying
        private PlayerInventory target;
        private float speed;
        private float nextSearch;

        private bool IsFlying => flightTime >= 0f;

        private void Awake() => pickup = GetComponent<ResourcePickup>();

        /// <summary>Flies from <paramref name="origin"/> to where the piece was placed.</summary>
        public void LaunchFrom(Vector3 origin)
        {
            landingPoint = transform.position;
            launchPoint = origin;
            flightTime = 0f;
            transform.position = origin;
        }

        private void Update()
        {
            if (IsFlying)
            {
                Fly();
                return;
            }

            if (Time.time >= nextSearch)
            {
                nextSearch = Time.time + searchInterval;
                if (!CanTake(target) || OutOfReach(target))
                    target = FindTaker();
            }

            if (target == null)
            {
                speed = 0f;
                return;
            }

            Vector3 goal = target.transform.position + Vector3.up * targetHeight;
            speed = Mathf.Min(magnetMaxSpeed, speed + magnetAcceleration * Time.deltaTime);
            transform.position = Vector3.MoveTowards(transform.position, goal, speed * Time.deltaTime);
        }

        private void Fly()
        {
            flightTime += Time.deltaTime;
            float t = Mathf.Clamp01(flightTime / flightSeconds);
            float ease = 1f - (1f - t) * (1f - t); // fast out of the node, slow on landing
            Vector3 ground = Vector3.Lerp(launchPoint, landingPoint, ease);
            transform.position = ground + Vector3.up * FeedbackRules.BounceHeight(t, arcHeight);
            if (t >= 1f)
                flightTime = -1f;
        }

        private PlayerInventory FindTaker()
        {
            Vector3 origin = transform.position;
            int count = Physics.OverlapSphereNonAlloc(origin, magnetRadius, nearby, playerMask, QueryTriggerInteraction.Ignore);
            PlayerInventory best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                PlayerInventory candidate = nearby[i].GetComponentInParent<PlayerInventory>();
                nearby[i] = null;
                if (!CanTake(candidate))
                    continue;
                float distance = (candidate.transform.position - origin).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private bool CanTake(PlayerInventory inventory)
        {
            if (inventory == null || inventory.SpaceFor(pickup.Type) <= 0)
                return false;
            return !inventory.TryGetComponent(out PlayerHealth health) || health.IsAlive;
        }

        // A little slack, so a target at the edge of the radius isn't dropped and picked again every search.
        private bool OutOfReach(PlayerInventory inventory) =>
            (inventory.transform.position - transform.position).sqrMagnitude > magnetRadius * magnetRadius * 2.25f;
    }
}
