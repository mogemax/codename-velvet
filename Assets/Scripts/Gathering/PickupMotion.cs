using CodenameVelvet.Feedback;
using CodenameVelvet.Player;
using UnityEngine;

namespace CodenameVelvet.Gathering
{
    /// <summary>
    /// How a dropped piece moves: it flies out of the node in an arc and bounces once where it lands, then a living
    /// player nearby who has room for it pulls it in, faster and faster, until their body touches it and
    /// the pickup (<see cref="IPickup"/>: a resource, an apple) is taken. Players who can't take it (full inventory,
    /// full health) are ignored, so pieces never chase someone who can't take them.
    /// </summary>
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
        private IPickup pickup;
        private Vector3 launchPoint;
        private Vector3 landingPoint;
        private float flightTime = -1f; // < 0 = not flying
        private PlayerHealth target;
        private float speed;
        private float nextSearch;

        private bool IsFlying => flightTime >= 0f;

        private void Awake() => pickup = GetComponent<IPickup>();

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

        private PlayerHealth FindTaker()
        {
            Vector3 origin = transform.position;
            int count = Physics.OverlapSphereNonAlloc(origin, magnetRadius, nearby, playerMask, QueryTriggerInteraction.Ignore);
            PlayerHealth best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                PlayerHealth candidate = nearby[i].GetComponentInParent<PlayerHealth>();
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

        private bool CanTake(PlayerHealth player) =>
            player != null && pickup != null && player.IsAlive && pickup.CanBeTakenBy(player);

        // A little slack, so a target at the edge of the radius isn't dropped and picked again every search.
        private bool OutOfReach(PlayerHealth player) =>
            (player.transform.position - transform.position).sqrMagnitude > magnetRadius * magnetRadius * 2.25f;
    }
}
