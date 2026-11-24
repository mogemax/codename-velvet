using CodenameVelvet.Core.Debugging;
using CodenameVelvet.Map;
using UnityEngine;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// <see cref="IEnemyMover"/> that drives a non-kinematic Rigidbody straight
    /// toward its destination on the XZ plane (no pathfinding). Y velocity is left
    /// untouched so gravity keeps working, matching PlayerMovement's convention. Never moves onto water.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RigidbodyEnemyMover : MonoBehaviour, IEnemyMover
    {
        private const string LogCategory = "Mover";

        [Tooltip("Planar distance at which the destination counts as reached.")]
        [SerializeField] private float arrivalTolerance = 0.2f;

        [Header("Debug")]
        [Tooltip("Logs when a push starts (body state) and how far it actually moved the body.")]
        [SerializeField] private bool debugLogs = true;

        private Rigidbody rb;
        private float edgeRadius = 0.5f;
        private Vector3 destination;
        private float speed;
        private bool hasDestination;

        private Vector3 pushVelocity;
        private float pushDuration;
        private float pushEndTime;
        // Debug-only: where the logged push started, and whether its end is still unreported.
        private Vector3 pushStartPosition;
        private bool isPushLogPending;

        public bool HasReachedDestination => !hasDestination || IsWithinTolerance(rb.position);

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.freezeRotation = true;
            if (TryGetComponent(out Collider body))
                edgeRadius = Mathf.Min(body.bounds.extents.x, body.bounds.extents.z);
        }

        public void MoveTo(Vector3 destination, float speed)
        {
            this.destination = destination;
            this.speed = Mathf.Max(0f, speed);
            hasDestination = true;
        }

        public void Stop()
        {
            if (isPushLogPending && Time.time < pushEndTime)
                LogPushFinished("cancelled by Stop");

            hasDestination = false;
            pushEndTime = 0f;
            SetPlanarVelocity(Vector3.zero);
        }

        public void Push(Vector3 velocity, float duration)
        {
            hasDestination = false;
            pushVelocity = new Vector3(velocity.x, 0f, velocity.z);
            pushDuration = Mathf.Max(0f, duration);
            pushEndTime = Time.time + pushDuration;

            if (debugLogs)
            {
                pushStartPosition = rb.position;
                isPushLogPending = true;
                VelvetLog.Info(LogCategory, this, $"Push started: speed {pushVelocity.magnitude:F2} for {pushDuration:F2}s, current velocity {rb.linearVelocity}, isKinematic {rb.isKinematic}, constraints {rb.constraints}");
            }
        }

        private void FixedUpdate()
        {
            if (isPushLogPending && Time.time >= pushEndTime)
                LogPushFinished("completed");

            if (Time.time < pushEndTime)
            {
                // Linear decay: full speed on the hit, zero when the push ends.
                SetPlanarVelocity(pushVelocity * ((pushEndTime - Time.time) / pushDuration));
                return;
            }

            if (!hasDestination || IsWithinTolerance(rb.position))
            {
                SetPlanarVelocity(Vector3.zero);
                return;
            }

            Vector3 toDestination = destination - rb.position;
            toDestination.y = 0f;
            float distance = toDestination.magnitude;

            // Never cover more than the remaining distance in one step, so the
            // body lands on the point instead of overshooting and jittering.
            float stepSpeed = Mathf.Min(speed, distance / Time.fixedDeltaTime);
            SetPlanarVelocity(toDestination / distance * stepSpeed);
        }

        /// <summary>Debug-only: reports the planar distance the body really moved since the push started.</summary>
        private void LogPushFinished(string outcome)
        {
            isPushLogPending = false;
            if (!debugLogs)
                return;

            Vector3 travelled = rb.position - pushStartPosition;
            travelled.y = 0f;
            VelvetLog.Info(LogCategory, this, $"Push {outcome}: travelled {travelled.magnitude:F2}u on XZ");
        }

        private bool IsWithinTolerance(Vector3 position)
        {
            Vector3 offset = destination - position;
            offset.y = 0f;
            return offset.sqrMagnitude <= arrivalTolerance * arrivalTolerance;
        }

        private void SetPlanarVelocity(Vector3 planarVelocity)
        {
            planarVelocity = WaterEdge.Clamp(rb.position, planarVelocity, edgeRadius, Time.fixedDeltaTime);
            rb.linearVelocity = new Vector3(planarVelocity.x, rb.linearVelocity.y, planarVelocity.z);
        }
    }
}
