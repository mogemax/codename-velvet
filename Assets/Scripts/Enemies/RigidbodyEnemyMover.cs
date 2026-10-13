using UnityEngine;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// <see cref="IEnemyMover"/> that drives a non-kinematic Rigidbody straight
    /// toward its destination on the XZ plane (no pathfinding). Y velocity is left
    /// untouched so gravity keeps working, matching PlayerMovement's convention.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RigidbodyEnemyMover : MonoBehaviour, IEnemyMover
    {
        [Tooltip("Planar distance at which the destination counts as reached.")]
        [SerializeField] private float arrivalTolerance = 0.2f;

        private Rigidbody rb;
        private Vector3 destination;
        private float speed;
        private bool hasDestination;

        public bool HasReachedDestination => !hasDestination || IsWithinTolerance(rb.position);

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.freezeRotation = true;
        }

        public void MoveTo(Vector3 destination, float speed)
        {
            this.destination = destination;
            this.speed = Mathf.Max(0f, speed);
            hasDestination = true;
        }

        public void Stop()
        {
            hasDestination = false;
            SetPlanarVelocity(Vector3.zero);
        }

        private void FixedUpdate()
        {
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

        private bool IsWithinTolerance(Vector3 position)
        {
            Vector3 offset = destination - position;
            offset.y = 0f;
            return offset.sqrMagnitude <= arrivalTolerance * arrivalTolerance;
        }

        private void SetPlanarVelocity(Vector3 planarVelocity)
        {
            rb.linearVelocity = new Vector3(planarVelocity.x, rb.linearVelocity.y, planarVelocity.z);
        }
    }
}
